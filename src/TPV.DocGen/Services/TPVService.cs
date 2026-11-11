using CsvHelper;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TPV.DocGen
{
    public class TPVService : Disposable
    {
        private ILogger<TPVService> _logger;

        protected DbConnection _db = null;
        private TPVConfig _delfusConfig;

        public TPVService(ILogger<TPVService> logger, DbConnection db, IOptionsMonitor<TPVConfig> delfusConfig)
        {
            _logger = logger;
            _db = db;
            _delfusConfig = delfusConfig.CurrentValue;
        }

        public async Task<int> InsertBatchInfo(Batch batchInfo)
        {
            string query = @"INSERT INTO [tpv].[Batches]([BatchName],[ValidationStatus],[ValidationErrors]) Output Inserted.Id VALUES (@BatchName,@ValidationStatus,@ValidationErrors)";

            return await _db.ExecuteScalarAsync<int>(query, batchInfo);
        }

        public async Task<IEnumerable<Batch>> GetBatchesByPipelineStatus(string pipelineStatus)
        {
            string query = "SELECT * FROM [tpv].[Batches] WHERE PipelineStatus=@PipelineStatus";

            switch (pipelineStatus)
            {
                case BatchPipelineStatus.Downloaded:
                    query += " AND IsDownloaded=1 AND IsValidated=0";
                    break;
                case BatchPipelineStatus.Validated:
                    query += " AND IsValidated=1 AND IsAcknowledged=0";
                    break;
                case BatchPipelineStatus.Acknowledged:
                    query += " AND IsAcknowledged=1 AND IsLetterGenerated=0";
                    break;
                case BatchPipelineStatus.LetterGenerated:
                    query += " AND IsLetterGenerated=1 AND IsSubmittedToAuthoMail=0";
                    break;
                default:
                    break;
            }
            return await _db.QueryAsync<Batch>(query, new { PipelineStatus = pipelineStatus });
        }

        public async Task<int> UpdateBatchPipelineStatus(int batchId, string pipelineStatus, DbTransaction t = null)
        {
            string query = $"UPDATE [tpv].[Batches] SET PipelineStatus = @PipelineStatus, {pipelineStatus}On = @TimeStamp WHERE Id = @BatchId";

            return await _db.ExecuteAsync(query, new { PipelineStatus = pipelineStatus, TimeStamp = Common.Now, BatchId = batchId }, transaction: t);
        }

        public async Task<int> UpdateBatchValidationStatus(int batchId, string validationStatus, string validationError = null, DbTransaction t = null)
        {
            var valStatus = await _db.ExecuteScalarAsync<string>("SELECT ValidationStatus FROM [tpv].[Batches] WHERE Id=@batchId", new { batchId }, transaction: t);

            return await _db.ExecuteAsync(@"
                UPDATE [tpv].[Batches] 
                SET 
                ValidationStatus = @ValidationStatus, 
                ValidationErrors = CASE WHEN ValidationErrors IS NULL THEN @ValidationErrors ELSE ValidationErrors + ' | '+  @ValidationErrors END 
                WHERE Id = @BatchId
            ", new { ValidationStatus = validationStatus, ValidationErrors = validationError, BatchId = batchId }, transaction: t);
        }

        public async Task<int> UpdateAgencyDisqualifyReason(int agencyId, string disqualifyReason)
        {
            string query = "UPDATE [tpv].[AgencyInfo] SET DisqualifyReason = @DisqualifyReason, RecordStatus=@RecordStatus WHERE Id = @AgencyId";

            return await _db.ExecuteAsync(query, new { DisqualifyReason = disqualifyReason, RecordStatus = RecordStatus.Disqualified, AgencyId = agencyId });
        }
        public async Task<int> UpdateAgencyTaxCollectorZipCode(string name, string zipValue, int agencyId)
        {
            string query = $"UPDATE [tpv].[AgencyInfo] SET {name} = @zipValue WHERE Id=@agencyId";

            return await _db.ExecuteAsync(query, new { zipValue, agencyId });
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedObjects()
        {
            if (_db != null)
            {
                // Closing a closed connection doesn't make a different so calling this method as a backup
                // in case the connection was kept open.
                _db.Close();
                _db.Dispose();
                _db = null;
            }
        }

        internal async Task InsertBatchData(int batchId, string tpvRootPath, string batchName)
        {
            if (_db.State != System.Data.ConnectionState.Open)
                await _db.OpenAsync();

            using var t = await _db.BeginTransactionAsync();

            try
            {
                var batchInfo = new BatchInfo(batchId, tpvRootPath, batchName);

                var files = Directory.EnumerateFiles(batchInfo.RequestPath, "*.csv");

                var agencyInfoPath = files.SingleOrDefault(f => Path.GetFileName(f).Contains("Agency"));
                var agencyPropertyInfoPath = files.SingleOrDefault(f => Path.GetFileName(f).Contains("Property"));

                Dictionary<string, int> agencyIdLookup = new Dictionary<string, int>();
                using (var reader = new StreamReader(agencyInfoPath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    await foreach (var agency in csv.GetRecordsAsync<AgencyInfo>())
                    {
                        agency.BatchId = batchId;

                        agency.Id = await InsertAgencyInfo(agency, t);

                        agencyIdLookup.Add(agency.AgencyNumber, agency.Id);
                    }
                }

                using (var reader = new StreamReader(agencyPropertyInfoPath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    await foreach (var property in csv.GetRecordsAsync<AgencyPropertyInfo>())
                    {
                        property.AgencyId = agencyIdLookup.ContainsKey(property.AgencyNumber) ? agencyIdLookup[property.AgencyNumber] : (int?)null;
                        property.BatchId = batchId;
                        await InsertAgencyPropertyInfo(property, t);
                    }
                }

                File.Move(agencyInfoPath, Path.ChangeExtension(agencyInfoPath, ".Parsed"));
                File.Move(agencyPropertyInfoPath, Path.ChangeExtension(agencyPropertyInfoPath, ".Parsed"));

                await UpdateBatchPipelineStatus(batchId, BatchPipelineStatus.Parsed, t);

                await t.CommitAsync();
            }
            catch (Exception ex)
            {
                await t.RollbackAsync();
                throw ex;
            }
        }

        internal async Task<IEnumerable<AgencyPropertyInfo>> GetPropertiesInfoByBatchId(int batchId)
        {
            string query = "SELECT * FROM [dbo].[vAgencyPropertyInfo] WHERE BatchId=@batchId";

            return await _db.QueryAsync<AgencyPropertyInfo>(query, new { batchId });
        }

        private async Task<int> UpdateAgencyNameInAgencyInfo(int batchId, DbTransaction t)
        {
            string query = "WITH AP AS(SELECT DISTINCT AgencyNumber, AgencyName FROM [tpv].[AgencyPropertyInfo] WHERE BatchId=@batchId)UPDATE P SET P.AgencyName=AP.AgencyName FROM [tpv].[AgencyInfo] P INNER JOIN AP ON P.AgencyNumber=AP.AgencyNumber WHERE P.BatchId=@batchId";

            return await _db.ExecuteAsync(query, new { batchId }, transaction: t);
        }

        internal async Task<int> UpdateAgencyRecordStatus(int agencyId, string recordStatus, DbTransaction t = null)
        {
            string query = "UPDATE [tpv].[AgencyInfo] SET RecordStatus = @RecordStatus WHERE Id = @AgencyId";

            return await _db.ExecuteAsync(query, new { RecordStatus = recordStatus, AgencyId = agencyId }, transaction: t);
        }

        internal async Task<IEnumerable<AgencyInfo>> GetAgenciesDataByBatchId(int batchId)
        {
            string query = "SELECT * FROM [dbo].[vAgencyInfo] WHERE BatchId=@batchId";

            return await _db.QueryAsync<AgencyInfo>(query, new { batchId });
        }

        internal async Task<dynamic> GetAgencyQualifiedCountsByBatchId(int batchId)
        {
            string query = "SELECT COUNT(Id) AS TotalCounts, SUM(CASE WHEN RecordStatus = 'InProgress' THEN 1 ELSE 0 END) AS InProgressCount, SUM(CASE WHEN RecordStatus = 'Qualified' THEN 1 ELSE 0 END) AS QualifedCount, SUM(CASE WHEN RecordStatus = 'Disqualified' THEN 1 ELSE 0 END) AS DisqualifedCount, SUM(CASE WHEN RecordStatus = 'LetterGenerated' THEN 1 ELSE 0 END) AS LetterGeneratedCount FROM [tpv].[AgencyInfo] WHERE BatchId = @batchId";

            return await _db.QuerySingleAsync<dynamic>(query, new { batchId });
        }

        internal async Task<IEnumerable<AgencyPropertyInfo>> GetPropertiesInfoByAgencyId(int agencyId)
        {
            string query = "SELECT * FROM [dbo].[vAgencyPropertyInfo] WHERE AgencyId=@agencyId";

            return await _db.QueryAsync<AgencyPropertyInfo>(query, new { agencyId });
        }

        private async Task<int> InsertAgencyInfo(AgencyInfo agency, DbTransaction t)
        {
            string query = "INSERT INTO [tpv].[AgencyInfo]([BatchId],[RecordStatus],[DisqualifyReason],[PdfFileName],[AgencyName],[AgencyNumber],[TaxCollectorName],[TaxCollectorAddress],[TaxCollectorCity],[TaxCollectorState],[TaxCollectorZip],[ELD],[DueDate]) Output Inserted.Id VALUES (@BatchId, @RecordStatus, @DisqualifyReason, @PdfFileName, @AgencyName, @AgencyNumber, @TaxCollectorName, @TaxCollectorAddress, @TaxCollectorCity, @TaxCollectorState, @TaxCollectorZip, @ELD, @DueDate)";

            return await _db.ExecuteScalarAsync<int>(query, agency, transaction: t);
        }

        private async Task<int> InsertAgencyPropertyInfo(AgencyPropertyInfo agencyProperties, DbTransaction t)
        {
            string query = "INSERT INTO [tpv].[AgencyPropertyInfo]( BatchId, AgencyId, AgencyNumber, AgencyName, ParcelNumber, PropertyName, PropertyStreetAddress, KeyNo) VALUES ( @BatchId, @AgencyId, @AgencyNumber, @AgencyName, @ParcelNumber, @PropertyName, @PropertyStreetAddress, @KeyNo)";

            return await _db.ExecuteAsync(query, agencyProperties, transaction: t);
        }

        /// <summary>
        /// Free your own state (unmanaged objects).
        /// Set large fields to null.
        /// </summary>
        protected override void DisposeUnManagedObjects()
        {

            // There are no unmanaged resources to free
            return;
        }

        internal async Task<int> UpdateAgencyRecordPdfFileName(int agencyId, string pdfFileName)
        {
            string query = @"   UPDATE [tpv].[AgencyInfo]
                                SET PdfFileName = @pdfFileName
					            WHERE Id=@agencyId";
            return await _db.ExecuteAsync(query, new { agencyId, pdfFileName });
        }
    }
}
