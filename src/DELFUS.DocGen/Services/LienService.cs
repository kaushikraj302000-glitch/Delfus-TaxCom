using Dapper;
using DELFUS.Entities;
using DELFUS.Lib;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.DocGen.Service
{
    public class LienService : Disposable
    {
        private ILogger<LienService> _logger;

        protected DbConnection _db = null;
        private CommonService _commonService;
        private ClientEmailConfiguration _clientEmailConfig;
        private DelfusConfig _delfusConfig;

        public LienService(ILogger<LienService> logger, DbConnection db, CommonService commonService, IOptionsMonitor<DelfusConfig> delfusConfig, IOptionsMonitor<ClientEmailConfiguration> clientEmailConfig)
        {
            _logger = logger;
            _db = db;
            _commonService = commonService;
            _clientEmailConfig = clientEmailConfig.CurrentValue;
            _delfusConfig = delfusConfig.CurrentValue;
        }

        public async Task<int> InsertBatchInfo(Batch batchInfo)
        {
            string query = @"INSERT INTO [dbo].[Batches]([BatchName],[FileDate],[FileType],[BatchStatus],[ValidationStatus],[ValidationErrors]) Output Inserted.Id VALUES (@BatchName,@FileDate,@FileType,@BatchStatus,@ValidationStatus,@ValidationErrors)";

            return await _db.ExecuteScalarAsync<int>(query, batchInfo);
        }

        public async Task<IEnumerable<Batch>> GetBatchesByPipelineStatus(string pipelineStatus)
        {
            string query = "SELECT * FROM [dbo].[Batches] WHERE PipelineStatus=@PipelineStatus";

            switch (pipelineStatus)
            {
                case BatchPipelineStatus.Downloaded:
                    query += " AND IsDownloaded=1 AND IsValidated=0";
                    break;
                case BatchPipelineStatus.Validated:
                    query += " AND IsValidated=1 AND IsLetterGenerated=0";
                    break;
                case BatchPipelineStatus.LetterGenerated:
                    query += " AND IsLetterGenerated=1 AND IsSubmittedToAuthoMail=0";
                    break;
                case BatchPipelineStatus.SubmittedToAuthoMail:
                    query += " AND IsSubmittedToAuthoMail=1 AND IsAcknowledged=0";
                    break;
                case BatchPipelineStatus.Acknowledged:
                    query += " AND IsAcknowledged=1 AND IsSubmittedToStellent=0";
                    break;
                case BatchPipelineStatus.SubmittedToStellent:
                    query += " AND IsSubmittedToStellent=1";
                    break;
                default:
                    break;
            }
            return await _db.QueryAsync<Batch>(query, new { PipelineStatus = pipelineStatus });
        }

        public async Task<int> InsertLienInfo(int batchId, Dictionary<string, object> lienRecord, DbTransaction t)
        {
            lienRecord.Add("BatchId", batchId);
            lienRecord.Add("RecordStatus", RecordStatus.InProgress);

            var paramList = new DynamicParameters();
            List<string> paramNameList = new List<string>();
            foreach (var pair in lienRecord)
            {
                var paramName = $"@{pair.Key}";
                paramList.Add(paramName, pair.Value);
                paramNameList.Add(paramName);
            }

            var cols = String.Join(",", lienRecord.Keys);
            var paramNames = String.Join(",", paramNameList);

            var query = $"INSERT INTO [dbo].[LienData] ({cols}) Output Inserted.Id VALUES ({paramNames})";

            return await _db.ExecuteScalarAsync<int>(query, paramList, transaction: t);
        }

        public async Task<int> InsertBillInfo(int? lienId, int batchId, Dictionary<string, object> billRecord, DbTransaction t)
        {
            billRecord.Add("LienDataId", lienId);
            billRecord.Add("BatchId", batchId);

            var paramList = new DynamicParameters();
            List<string> paramNameList = new List<string>();
            foreach (var pair in billRecord)
            {
                var paramName = $"@{pair.Key}";
                paramList.Add(paramName, pair.Value);
                paramNameList.Add(paramName);
            }

            var cols = String.Join(",", billRecord.Keys);
            var paramNames = String.Join(",", paramNameList);

            var query = $"INSERT INTO [dbo].[BillData] ({cols}) VALUES ({paramNames})";

            return await _db.ExecuteAsync(query, paramList, transaction: t);
        }

        public async Task<IEnumerable<LienData>> GetLienDataByBatchId(int batchId)
        {
            string query = @"SELECT L.*, ISNULL(COUNT(*), 0) AS BillCount FROM [dbo].[vLienData] L
                            LEFT OUTER JOIN[dbo].[vBillData] B ON B.LienDataId = L.Id
                            WHERE L.BatchId = @batchId
                            GROUP  BY L.Id, L.BatchId, L.RecordStatus, L.DisqualifyReason, L.DocumentType, L.LogoFileName, L.PdfFileName, L.RD_LIEN_OUT, L.RD_CNTRCT_ID_OL, L.RD_TINET_SEQ_NBR_OL, L.RD_TA_ORDR_ID, L.RD_TA_ORDR_MOD_ID, L.RD_TA_LNDR_BRNCH_ID, L.RD_TMPLT_CD, L.RD_BRRW_FRST_NM, L.RD_BRRW_LAST_NM, L.RD_BRRW_ADDR1_TXT, L.RD_BRRW_ADDR2_TXT, L.RD_BRRW_CITY_NM, L.RD_BRRW_FOREIGN_ADDR_TXT, L.RD_BRRW_STATE_ABBR, L.RD_BRRW_CNTRY_CD, L.RD_BRRW_ZIP_CD, L.RD_SITUS_STR_NBR, L.RD_SITUS_STR_NM, L.RD_SITUS_CITY_NM, L.RD_SITUS_CNTRY_CD, L.RD_SITUS_STATE_ABBR, L.RD_SITUS_ZIP_CD, L.RD_TAX_ID, L.RD_AGENCY_ID, L.RD_AGENCY_NM, L.RD_AGCY_ADDR1_TXT, L.RD_AGCY_CNTRY_CD, L.RD_AGCY_PAYEE_NM, L.RD_AGCY_PHONE_TXT, L.RD_AGCY_STATE_ABBR, L.RD_AGCY_ZIP_CD, L.RD_AGNCY_CITY_NM, L.RD_BATCH_NBR, L.RD_BEG_DELQ_YR, L.RD_CNTCT_NM, L.RD_CURR_YR_BASE_AMT, L.RD_CURR_YR_DELQ_AMT, L.RD_CURR_YR_SUPP_AMT, L.RD_CUST_ID, L.RD_CUST_NM, L.RD_CUST_ADDR1_TXT, L.RD_CUST_ADDR2_TXT, L.RD_CUST_CITY_NM, L.RD_CUST_CNTRY_CD, L.RD_CUST_FAX_TXT, L.RD_CUST_LTR_CD, L.RD_CUST_STATE_ABBR, L.RD_CUST_ZIP_CD, L.RD_DELQ_LTR_DOWNLOAD_DT, L.RD_DELQ_LTR_WIP_NBR, L.RD_DELQ_POST_DT, L.RD_DELQ_POST_DT_TXT, L.RD_DLVR_ADDR1_TXT, L.RD_DLVR_CITY_NM, L.RD_DLVR_EMAIL_ADDR_TXT, L.RD_DLVR_STATE_ABBR, L.RD_DLVR_TYP_CD, L.RD_DLVR_ZIP_CD, L.RD_FIRST_INSTL_FOR_YR, L.RD_INSRT_TYP_CD, L.RD_INVESTOR_ID, L.RD_LANG_CD, L.RD_DLVR_NM, L.RD_LIEN_SEARCH_DT, L.RD_LIEN_SEARCH_DT_TXT, L.RD_LOAN_ID, L.RD_LTR_PDF_DOC_ID, L.RD_NOTE_CD, L.RD_NOTE_CD_DESC, L.RD_PRFL_ID_OL, L.RD_PRIOR_YR_DELQ_AMT, L.RD_REDEM1_AMT, L.RD_REDEM1_DT, L.RD_REDEM2_DT, L.RD_REDEM2_AMT, L.RD_RT_TAXR_ID, L.RD_SIMPLEX_DUPLEX_FLG, L.RD_STATUS_MSG_DESC, L.RD_SUBAPPL_CD_OL, L.RD_SUBMIT_DT, L.RD_TOT_BASE_AMT, L.RD_OUT_ENVLP_TYP_ID, L.RD_IN_ENVLP_TYP_ID, L.RD_CARR_CD, L.RD_PAPER_TYP_ID, L.RD_ELD_DT, L.RD_ELD_DT_TXT, L.RD_PROCESS_CD, L.FILLER";

            return await _db.QueryAsync<LienData>(query, new { batchId });
        }

        public async Task<IEnumerable<MailGroup>> GetMailGroupData(int batchId)
        {
            string query = "SELECT * FROM MailGroups WHERE BatchId = @batchId";

            return await _db.QueryAsync<MailGroup>(query, new { batchId });
        }
        public async Task<IEnumerable<BillData>> GetBillDataByLienId(int lienId)
        {
            string query = "SELECT * FROM [dbo].[vBillData] L WHERE L.LienDataId=@lienId";

            return await _db.QueryAsync<BillData>(query, new { lienId });
        }

        public async Task<IEnumerable<LienData>> GetBatchDataByBatchId(int batchId)
        {
            string query = "SELECT * FROM [dbo].[vLienData] L INNER JOIN [dbo].[vBillData] BL ON L.Id=BL.LienDataId INNER JOIN [dbo].[Batches] B ON L.BatchId=B.Id  WHERE L.BatchId=@BatchId";

            var lienDictionary = new Dictionary<int, LienData>();

            return await _db.QueryAsync<LienData, BillData, Batch, LienData>(query,
                (l, bl, b) =>
                {
                    LienData ld;
                    if (!lienDictionary.TryGetValue(l.Id, out ld))
                    {
                        ld = l;
                        ld.BillRecords = new List<BillData>();
                        ld.BatchInfo = b;
                        lienDictionary.Add(ld.Id, ld);
                    }
                    ld.BillRecords.Add(bl);
                    return ld;
                },
                new
                {
                    BatchId = batchId
                });
        }
        public async Task<DocumentType> GetDocumentType(string outerEnvelope, string breEnvelope, bool isColor)
        {
            string query = "SELECT * FROM [dbo].[DocumentTypes] WHERE OuterEnvelope=@OuterEnvelope AND BREEnvelope=@BREEnvelope AND IsColor=@IsColor";

            return await _db.QuerySingleOrDefaultAsync<DocumentType>(query, new { OuterEnvelope = outerEnvelope, BREEnvelope = breEnvelope, IsColor = isColor });
        }

        public async Task<int> UpdateLienRecordsDocumentType(int batchId)
        {
            string query = @"
                            UPDATE LienData
                            SET DocumentType=(SELECT DocumentName FROM DocumentTypes D
				                              WHERE D.OuterEnvelope = RTRIM(LTRIM(RD_OUT_ENVLP_TYP_ID)) AND
				                                    D.BREEnvelope = RTRIM(LTRIM(RD_IN_ENVLP_TYP_ID)) AND
						                            D.IsColor = CASE WHEN RTRIM(LTRIM(RD_PAPER_TYP_ID)) LIKE '%COLOR%' THEN 1 
													WHEN RTRIM(LTRIM(RD_PAPER_TYP_ID)) LIKE '%BLACK%' THEN 0 ELSE NULL END )
                            WHERE BatchId=@batchId
                        ";
            return await _db.ExecuteAsync(query, new { batchId });
        }

        public async Task<int> UpdateLienRecordsLogoFile(int batchId)
        {
            string query = @"   UPDATE LienData
                                SET LogoFileName = CASE WHEN RTRIM(LTRIM(RD_OUT_ENVLP_TYP_ID)) LIKE '%COLOR%' OR RTRIM(LTRIM(RD_OUT_ENVLP_TYP_ID)) LIKE '%BLACK%' 
					            THEN (
							            SELECT FileName FROM CoversheetLogos cl
				                        WHERE cl.OuterEnvelope = RTRIM(LTRIM(RD_OUT_ENVLP_TYP_ID)) 
							            AND cl.CustomerId = RTRIM(LTRIM(RD_CUST_ID)) 
							            AND cl.TemplateCode = RTRIM(LTRIM(RD_TMPLT_CD))
						             ) 
					            ELSE 'NA' END
					            WHERE BatchId=@batchId";
            return await _db.ExecuteAsync(query, new { batchId });
        }
        public async Task<int> UpdateBillRecordsWithoutLienRecords(int batchId)
        {
            int billRecordsCounts = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM [dbo].[BillData] WHERE BatchId=@batchId AND LienDataId IS NULL", new { batchId });

            if (billRecordsCounts > 0)
            {
                string billRecordsValidation = $"System has found total {billRecordsCounts} bill records which doesn't contain lien reference.";

                await _commonService.UpdateBatchValidationStatus(batchId, BatchValidationStatus.PartiallyQualified, billRecordsValidation);
            }
            return 0;
        }

        public async Task InsertBatchData(int batchId, string rootPath, string batchName, IEnumerable<ParsingField> lienRecordSchema, IEnumerable<ParsingField> billRecordSchema)
        {
            if (_db.State != System.Data.ConnectionState.Open)
                await _db.OpenAsync();

            using var t = await _db.BeginTransactionAsync();

            try
            {
                var batchInfo = new BatchInfo(batchId, rootPath, batchName);

                string filePath = Path.Combine(batchInfo.RequestPath, $"{batchInfo.BatchName}.txt");

                string[] output = File.ReadAllLines(filePath);
                StringBuilder invalidRecordString = new StringBuilder();
                var lienIdLookup = new Dictionary<string, int>();
                int? lienId = 0;

                if (output.Length == 0)
                {
                    Common.SendEmailNotification(_clientEmailConfig.To, _clientEmailConfig.Cc, $"{_delfusConfig.AppEnvironment} : Download Summary of {batchInfo.BatchName}", "Batch does not contain any record for processing.", false, false);
                    await _commonService.UpdateBatchValidationStatus(batchId, BatchValidationStatus.Disqualified, DisqualificationReasons.NoRecordFound, t);
                    await _commonService.UpdateBatchPipelineStatus(batchId, BatchPipelineStatus.Rejected, t);
                    await t.CommitAsync();
                    return;
                }

                if (!output[0].IsNullOrWhiteSpace() && output[0].Substring(0, 1) != "L")
                {

                    await _commonService.UpdateBatchValidationStatus(batchId, BatchValidationStatus.Disqualified, DisqualificationReasons.FileNotStartingWithLienRecord, t);
                    await _commonService.UpdateBatchPipelineStatus(batchId, BatchPipelineStatus.Rejected, t);
                    await t.CommitAsync();
                    return;
                }

                foreach (string line in output)
                {
                    if (line.IsNullOrWhiteSpace())
                    {
                        lienId = null;
                        continue;
                    }

                    var record = line.Replace('\0', ' ');

                    string recordType = record.Substring(0, 1);
                    //NOTE : Here system will consider that After L records there will be B record only which is related to L record only. If any invalid record found between L or B record LienId will become null
                    if (recordType == "L")
                    {
                        if (record.Length >= 1749)
                        {
                            var lienData = ParseRecord(record, lienRecordSchema);

                            lienId = await InsertLienInfo(batchId, lienData, t);
                        }
                        else
                        {
                            lienId = null;
                            invalidRecordString.AppendLine(record);
                        }
                    }
                    else if (recordType == "B")
                    {
                        if (record.Length >= 260)
                        {
                            var billData = ParseRecord(record, billRecordSchema);

                            await InsertBillInfo(lienId, batchId, billData, t);
                        }
                        else
                        {
                            lienId = null;
                            invalidRecordString.AppendLine(record);
                        }
                    }
                    else
                    {
                        lienId = null;
                        invalidRecordString.AppendLine(record);
                    }
                }

                if (invalidRecordString.Length > 0)
                {
                    await _commonService.UpdateBatchValidationStatus(batchId, BatchValidationStatus.PartiallyQualified, invalidRecordString.ToString(), t);
                }

                File.Move(filePath, Path.ChangeExtension(filePath, ".Parsed"));

                await _commonService.UpdateBatchPipelineStatus(batchId, BatchPipelineStatus.Parsed, t);

                await t.CommitAsync();
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Error occured while parsing document for Batch Id : {batchId}");
                await t.RollbackAsync();
                throw ex;
            }
        }

        internal int UpdateLienRecordZipCode(string name, string zipValue, int lienId)
        {
            string query = $"UPDATE [dbo].[LienData] SET {name} = @zipValue WHERE Id=@lienId";

            return _db.Execute(query, new { zipValue, lienId });
        }

        public async Task<IEnumerable<int>> GetLienIdsForInvalidDocTypes(int batchId)
        {
            string query = "SELECT Id FROM [dbo].[LienData] L WHERE L.BatchId=@batchId AND L.DocumentType IS NULL";

            return await _db.QueryAsync<int>(query, new { batchId });
        }

        public async Task<IEnumerable<int>> GetLienIdsForInvalidLogoFiles(int batchId)
        {
            string query = "SELECT Id FROM [dbo].[LienData] L WHERE L.BatchId=@batchId AND L.LogoFileName IS NULL";

            return await _db.QueryAsync<int>(query, new { batchId });
        }

        public async Task<dynamic> GetLienQualifiedCountsByBatchId(int batchId)
        {
            string query = "SELECT COUNT(Id) AS TotalCounts, SUM(CASE WHEN RecordStatus = 'InProgress' THEN 1 ELSE 0 END) AS InProgressCount, SUM(CASE WHEN RecordStatus = 'Qualified' THEN 1 ELSE 0 END) AS QualifedCount, SUM(CASE WHEN RecordStatus = 'Disqualified' THEN 1 ELSE 0 END) AS DisqualifedCount, SUM(CASE WHEN RecordStatus = 'LetterGenerated' THEN 1 ELSE 0 END) AS LetterGeneratedCount FROM LienData WHERE BatchId = @batchId";

            return await _db.QuerySingleAsync<dynamic>(query, new { batchId });
        }
        public IEnumerable<SpecialRequirement> GetSpecialRequirements()
        {
            string query = @"SELECT * FROM [dbo].[SpecialRequirements]";
            return _db.Query<SpecialRequirement>(query);
        }

        public IEnumerable<ClientConsistency> GetClientConsistencies()
        {
            string query = @"SELECT * FROM [dbo].[ClientConsistencies]";
            return _db.Query<ClientConsistency>(query);
        }

        public HashSet<string> GetAMClientList()
        {
            string query = @"SELECT CustId FROM [dbo].[AMClients]";
            return _db.Query<string>(query).ToHashSet();
        }

        public async Task<int> UpdateLienRecordPdfFileName(int lienId, string pdfFileName)
        {
            string query = @"   UPDATE LienData
                                SET PdfFileName = @pdfFileName
					            WHERE Id=@lienId";
            return await _db.ExecuteAsync(query, new { lienId, pdfFileName });
        }

        private static Dictionary<string, object> ParseRecord(string record, IEnumerable<ParsingField> schema)
        {
            return schema.ToDictionary(field => field.FieldName, field => (object)record.Substring(field.StartPosition, field.Length));
        }

        public async Task<int> InsertMailGroupInfo(MailGroup mgInfo)
        {
            string query = @"INSERT INTO [dbo].[MailGroups](BatchId, LoanId, CustomerId, DocumentType, PdfFileName, LogoFileName, FirstName, LastName, AddressLine1, AddressLine2, City, State, Zip, Country, GroupType) Output Inserted.Id VALUES (@BatchId, @LoanId, @CustomerId, @DocumentType, @PdfFileName, @LogoFileName, @FirstName, @LastName, @AddressLine1, @AddressLine2, @City, @State, @Zip, @Country, @GroupType)";

            return await _db.ExecuteScalarAsync<int>(query, mgInfo);
        }

        public async Task<int> InsertLienMailGroupInfo(List<LienMailGroup> lienMGInfo)
        {
            string query = @"INSERT INTO [dbo].[LienMailGroups](LienDataId, MailGroupId) VALUES (@LienDataId, @MailGroupId)";

            return await _db.ExecuteAsync(query, lienMGInfo);
        }

        public async Task<int> UpdateMailGroupInfo(int mgId, string pdfFileName)
        {
            string query = @"UPDATE [dbo].[MailGroups] SET PdfFileName = @pdfFileName WHERE Id=@mgId";

            return await _db.ExecuteAsync(query, new { mgId, pdfFileName });
        }

        public async Task InsertMailGroupInfo(IEnumerable<MailGroup> mailGroups, BatchInfo batchInfo)
        {
            foreach (var gRecord in mailGroups)
            {
                gRecord.Id = await InsertMailGroupInfo(gRecord);

                await InsertLienMailGroupInfo(gRecord.Letters.Select(rd => new LienMailGroup() { LienDataId = rd.Id, MailGroupId = gRecord.Id }).ToList());

                var pdfList = gRecord.Letters.Select(flName => Path.Combine(batchInfo.LettersPath, flName.PdfFileName)).ToList();

                if (pdfList.Count > 1)
                {
                    string mrgPdfFileName;
                    if (gRecord.GroupType == GroupTypes.HouseHolding)
                    {
                        mrgPdfFileName = $"{gRecord.CustomerId}_{gRecord.LoanId}_{gRecord.Id}_{gRecord.GroupType}.pdf";
                    }
                    else
                    {
                        mrgPdfFileName = $"{gRecord.CustomerId}_{gRecord.Id}_{gRecord.GroupType}.pdf";
                    }

                    var bmPdfPath = Path.Combine(batchInfo.LettersPath, mrgPdfFileName);
                    pdfList.Add(bmPdfPath);

                    PdfUtil.MergePDFs(pdfList.ToArray());

                    //if householding and total page gt 54
                    //then
                    //check its mailgroup address and see if anything meesing in 
                    //FirstName, Address1, city,state,zip  OR for foreign Address(country) firstname, address1 or ForeignAddressline is null or whilespace
                    //Then disqualify its related lien Id and remove lienMailgroup and delete mail group entry

                    if ((gRecord.GroupType == GroupTypes.HouseHolding || gRecord.GroupType == GroupTypes.AgencyGrouping) && bmPdfPath.GetPageCount() > 54)
                    {
                        if (!gRecord.Country.IsNullOrWhiteSpace())
                        {
                            if (gRecord.FirstName.IsNullOrWhiteSpace() || gRecord.AddressLine1.IsNullOrWhiteSpace())
                            {
                                foreach (var lien in gRecord.Letters)
                                {
                                    await _commonService.UpdateLienDisqualifyReason(lien.Id, DisqualificationReasons.InvalidBulkMailAdd);
                                }

                                await _commonService.DeleteLienMailGroupMap(gRecord.Id);

                                await _commonService.DeleteMailGroup(gRecord.Id);

                                continue;
                            }
                        }
                        else
                        {
                            if (gRecord.FirstName.IsNullOrWhiteSpace() || gRecord.AddressLine1.IsNullOrWhiteSpace() || gRecord.City.IsNullOrWhiteSpace() || gRecord.State.IsNullOrWhiteSpace() || gRecord.Zip.IsNullOrWhiteSpace())
                            {

                                foreach (var lien in gRecord.Letters)
                                {
                                    await _commonService.UpdateLienDisqualifyReason(lien.Id, DisqualificationReasons.InvalidBulkMailAdd);
                                }

                                await _commonService.DeleteLienMailGroupMap(gRecord.Id);

                                await _commonService.DeleteMailGroup(gRecord.Id);

                                continue;
                            }
                        }
                    }

                    gRecord.PdfFileName = mrgPdfFileName;

                    await UpdateMailGroupInfo(gRecord.Id, gRecord.PdfFileName);
                }
                else if (gRecord.GroupType == GroupTypes.BulkMailing)
                {
                    var bulkPdfFileName = $"{gRecord.Id}_{gRecord.GroupType}.pdf";

                    File.Copy(pdfList.Single(), Path.Combine(batchInfo.LettersPath, bulkPdfFileName));

                    await UpdateMailGroupInfo(gRecord.Id, bulkPdfFileName);
                }
            }
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
