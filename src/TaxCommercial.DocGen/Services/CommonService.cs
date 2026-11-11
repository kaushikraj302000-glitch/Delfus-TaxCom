using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TaxCommercial.DocGen
{
    public class CommonService : Disposable
    {
        private ILogger<CommonService> _logger;

        protected DbConnection _db = null;

        public CommonService(ILogger<CommonService> logger, DbConnection db)
        {
            _logger = logger;
            _db = db;
        }


        public async Task<int> UpdateBatchPipelineStatus(int batchId, string pipelineStatus, DbTransaction t = null)
        {
            string query = "UPDATE [dls].[Batches] SET PipelineStatus = @PipelineStatus, {0}On = @TimeStamp WHERE Id = @BatchId".Fill(pipelineStatus);

            return await _db.ExecuteAsync(query, new { PipelineStatus = pipelineStatus, TimeStamp = Common.Now, BatchId = batchId }, transaction: t);
        }

        public async Task<IEnumerable<ParsingField>> GetParsingSchema(string recordType)
        {
            return await _db.QueryAsync<ParsingField>("SELECT * FROM [Schema].[Request] WHERE RecordType=@RecordType AND IsActive=1", new { RecordType = recordType });
        }

        public async Task<int> UpdateBatchValidationStatus(int batchId, string validationStatus, string validationError = null, DbTransaction t = null)
        {          
            return await _db.ExecuteAsync(@"
                UPDATE [dls].[Batches] 
                SET 
                ValidationStatus = @ValidationStatus, 
                ValidationErrors = CASE WHEN ValidationErrors IS NULL THEN @ValidationErrors ELSE ValidationErrors + ' | '+  @ValidationErrors END 
                WHERE Id = @BatchId
            ", new { ValidationStatus = validationStatus, ValidationErrors = validationError, BatchId = batchId }, transaction: t);           
        }

        public async Task<int> UpdateLienRecordStatus(int lienId, string lienRecordStatus, DbTransaction t = null)
        {
            string query = "UPDATE [dls].[LienData] SET RecordStatus = @RecordStatus WHERE Id = @LienId";

            return await _db.ExecuteAsync(query, new { RecordStatus = lienRecordStatus, LienId = lienId }, transaction: t);
        }

        public async Task<int> UpdateLienDisqualifyReason(int lienId, string disqualifyReason)
        {
            string query = "UPDATE [dls].[LienData] SET DisqualifyReason = @DisqualifyReason, RecordStatus=@RecordStatus WHERE Id = @LienId";

            return await _db.ExecuteAsync(query, new { DisqualifyReason = disqualifyReason, RecordStatus = RecordStatus.Disqualified, LienId = lienId });
        }

        public async Task<IEnumerable<string>> GetTemplateMergeFields()
        {
            return await _db.QueryAsync<string>("SELECT TemplateFieldMapName FROM [Schema].[Request] WHERE IsActive=1 AND TemplateFieldMapName IS NOT NULL", new { });
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

        /// <summary>
        /// Free your own state (unmanaged objects).
        /// Set large fields to null.
        /// </summary>
        protected override void DisposeUnManagedObjects()
        {

            // There are no unmanaged resources to free
            return;
        }
    }
}
