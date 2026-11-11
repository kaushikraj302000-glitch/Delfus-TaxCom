using Dapper;
using DELFUS.Entities;
using DELFUS.Lib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.DocGen.Service
{
    public interface ILienService : IDisposable
    {
        Task<int> InsertBatchInfo(Batch batchInfo);
        Task<IEnumerable<Batch>> GetBatchesByPipelineStatus(string pipelineStatus);
        Task<int> InsertLienInfo(LienData lienData);
        Task<int> InsertBillInfo(List<BillData> billData);
        Task<int> InsertLienInfo(int batchId, ParsingFieldData lienRecord);
        Task<int> InsertBillInfo(int lienId, List<ParsingFieldData> billRecords);
        Task<IEnumerable<LienData>> GetLienDataByBatchId(int batchId);
    }

    public class LienService : ServiceBase, ILienService
    {
        public async Task<int> InsertBatchInfo(Batch batchInfo)
        {
            string query = @"INSERT INTO [dbo].[Batches]([BatchName],[BatchStatus],[ValidationStatus],[ValidationErrors]) Output Inserted.Id VALUES (@BatchName,@BatchStatus,@ValidationStatus,@ValidationErrors)";

            return await db.ExecuteScalarAsync<int>(query, batchInfo);
        }

        public async Task<int> InsertLienInfo(LienData lienData)
        {
            string query = @"INSERT INTO [dbo].[LienData]([BatchId],[MailPieceStatus],[RD_LIEN_OUT],[RD_CNTRCT_ID_OL],[RD_TINET_SEQ_NBR_OL],[RD_TA_ORDR_ID],[RD_TA_ORDR_MOD_ID],[RD_TA_LNDR_BRNCH_ID],[RD_TMPLT_CD],[RD_BRRW_FRST_NM],[RD_BRRW_LAST_NM],[RD_BRRW_ADDR1_TXT],[RD_BRRW_ADDR2_TXT],[RD_BRRW_CITY_NM],[RD_BRRW_FOREIGN_ADDR_TXT],[RD_BRRW_STATE_ABBR],[RD_BRRW_CNTRY_CD],[RD_BRRW_ZIP_CD],[RD_SITUS_STR_NBR],[RD_SITUS_STR_NM],[RD_SITUS_CITY_NM],[RD_SITUS_CNTRY_CD],[RD_SITUS_STATE_ABBR],[RD_SITUS_ZIP_CD],[RD_TAX_ID],[RD_AGENCY_ID],[RD_AGENCY_NM],[RD_AGCY_ADDR1_TXT],[RD_AGCY_CNTRY_CD],[RD_AGCY_PAYEE_NM],[RD_AGCY_PHONE_TXT],[RD_AGCY_STATE_ABBR],[RD_AGCY_ZIP_CD],[RD_AGNCY_CITY_NM],[RD_BATCH_NBR],[RD_BEG_DELQ_YR],[RD_CNTCT_NM],[RD_CURR_YR_BASE_AMT],[RD_CURR_YR_DELQ_AMT],[RD_CURR_YR_SUPP_AMT],[RD_CUST_ID],[RD_CUST_NM],[RD_CUST_ADDR1_TXT],[RD_CUST_ADDR2_TXT],[RD_CUST_CITY_NM],[RD_CUST_CNTRY_CD],[RD_CUST_FAX_TXT],[RD_CUST_LTR_CD],[RD_CUST_STATE_ABBR],[RD_CUST_ZIP_CD],[RD_DELQ_LTR_DOWNLOAD_DT],[RD_DELQ_LTR_WIP_NBR],[RD_DELQ_POST_DT],[RD_DELQ_POST_DT_TXT],[RD_DLVR_ADDR1_TXT],[RD_DLVR_CITY_NM],[RD_DLVR_EMAIL_ADDR_TXT],[RD_DLVR_STATE_ABBR],[RD_DLVR_TYP_CD],[RD_DLVR_ZIP_CD],[RD_FIRST_INSTL_FOR_YR],[RD_INSRT_TYP_CD],[RD_INVESTOR_ID],[RD_LANG_CD],[RD_DLVR_NM],[RD_LIEN_SEARCH_DT],[RD_LIEN_SEARCH_DT_TXT],[RD_LOAN_ID],[RD_LTR_PDF_DOC_ID],[RD_NOTE_CD],[RD_NOTE_CD_DESC],[RD_PRFL_ID_OL],[RD_PRIOR_YR_DELQ_AMT],[RD_REDEM1_AMT],[RD_REDEM1_DT],[RD_REDEM2_DT],[RD_REDEM2_AMT],[RD_RT_TAXR_ID],[RD_SIMPLEX_DUPLEX_FLG],[RD_STATUS_MSG_DESC],[RD_SUBAPPL_CD_OL],[RD_SUBMIT_DT],[RD_TOT_BASE_AMT],[RD_OUT_ENVLP_TYP_ID],[RD_IN_ENVLP_TYP_ID],[RD_CARR_CD],[RD_PAPER_TYP_ID],[RD_ELD_DT],[RD_ELD_DT_TXT],[RD_PROCESS_CD],[FILLER]) Output Inserted.Id VALUES (@BatchId,@MailPieceStatus,@RD_LIEN_OUT,@RD_CNTRCT_ID_OL,@RD_TINET_SEQ_NBR_OL,@RD_TA_ORDR_ID,@RD_TA_ORDR_MOD_ID,@RD_TA_LNDR_BRNCH_ID,@RD_TMPLT_CD,@RD_BRRW_FRST_NM,@RD_BRRW_LAST_NM,@RD_BRRW_ADDR1_TXT,@RD_BRRW_ADDR2_TXT,@RD_BRRW_CITY_NM,@RD_BRRW_FOREIGN_ADDR_TXT,@RD_BRRW_STATE_ABBR,@RD_BRRW_CNTRY_CD,@RD_BRRW_ZIP_CD,@RD_SITUS_STR_NBR,@RD_SITUS_STR_NM,@RD_SITUS_CITY_NM,@RD_SITUS_CNTRY_CD,@RD_SITUS_STATE_ABBR,@RD_SITUS_ZIP_CD,@RD_TAX_ID,@RD_AGENCY_ID,@RD_AGENCY_NM,@RD_AGCY_ADDR1_TXT,@RD_AGCY_CNTRY_CD,@RD_AGCY_PAYEE_NM,@RD_AGCY_PHONE_TXT,@RD_AGCY_STATE_ABBR,@RD_AGCY_ZIP_CD,@RD_AGNCY_CITY_NM,@RD_BATCH_NBR,@RD_BEG_DELQ_YR,@RD_CNTCT_NM,@RD_CURR_YR_BASE_AMT,@RD_CURR_YR_DELQ_AMT,@RD_CURR_YR_SUPP_AMT,@RD_CUST_ID,@RD_CUST_NM,@RD_CUST_ADDR1_TXT,@RD_CUST_ADDR2_TXT,@RD_CUST_CITY_NM,@RD_CUST_CNTRY_CD,@RD_CUST_FAX_TXT,@RD_CUST_LTR_CD,@RD_CUST_STATE_ABBR,@RD_CUST_ZIP_CD,@RD_DELQ_LTR_DOWNLOAD_DT,@RD_DELQ_LTR_WIP_NBR,@RD_DELQ_POST_DT,@RD_DELQ_POST_DT_TXT,@RD_DLVR_ADDR1_TXT,@RD_DLVR_CITY_NM,@RD_DLVR_EMAIL_ADDR_TXT,@RD_DLVR_STATE_ABBR,@RD_DLVR_TYP_CD,@RD_DLVR_ZIP_CD,@RD_FIRST_INSTL_FOR_YR,@RD_INSRT_TYP_CD,@RD_INVESTOR_ID,@RD_LANG_CD,@RD_DLVR_NM,@RD_LIEN_SEARCH_DT,@RD_LIEN_SEARCH_DT_TXT,@RD_LOAN_ID,@RD_LTR_PDF_DOC_ID,@RD_NOTE_CD,@RD_NOTE_CD_DESC,@RD_PRFL_ID_OL,@RD_PRIOR_YR_DELQ_AMT,@RD_REDEM1_AMT,@RD_REDEM1_DT,@RD_REDEM2_DT,@RD_REDEM2_AMT,@RD_RT_TAXR_ID,@RD_SIMPLEX_DUPLEX_FLG,@RD_STATUS_MSG_DESC,@RD_SUBAPPL_CD_OL,@RD_SUBMIT_DT,@RD_TOT_BASE_AMT,@RD_OUT_ENVLP_TYP_ID,@RD_IN_ENVLP_TYP_ID,@RD_CARR_CD,@RD_PAPER_TYP_ID,@RD_ELD_DT,@RD_ELD_DT_TXT,@RD_PROCESS_CD,@FILLER)";

            return await db.ExecuteScalarAsync<int>(query, lienData);
        }

        public async Task<int> InsertBillInfo(List<BillData> billData)
        {
            string query = @"INSERT INTO [dbo].[BillData]([LienDataId],[RD_BILL_OUT],[RD_CNTRCT_ID_OB],[RD_TINET_SEQ_NBR_OB],[RD_TAX_BILL_YR],[RD_BILL_TYP_DESC],[RD_BILL_SEARCH_DT],[RD_BILL_SEARCH_DT_TXT],[RD_INSTL_NBR],[RD_TAX_AMT],[RD_PRFL_ID_OB],[RD_SUBAPPL_CD_OB],[RD_SYI_CD],[RD_SYI_CD_DESC],[RD_TA_TAX_BILL_YR],[RD_TAX_1_AMT],[RD_TAX_2_AMT],[RD_TAX_3_AMT],[RD_TAX_4_AMT],[FILLER]) VALUES (LienDataId,@RD_BILL_OUT,@RD_CNTRCT_ID_OB,@RD_TINET_SEQ_NBR_OB,@RD_TAX_BILL_YR,@RD_BILL_TYP_DESC,@RD_BILL_SEARCH_DT,@RD_BILL_SEARCH_DT_TXT,@RD_INSTL_NBR,@RD_TAX_AMT,@RD_PRFL_ID_OB,@RD_SUBAPPL_CD_OB,@RD_SYI_CD,@RD_SYI_CD_DESC,@RD_TA_TAX_BILL_YR,@RD_TAX_1_AMT,@RD_TAX_2_AMT,@RD_TAX_3_AMT,@RD_TAX_4_AMT,@FILLER)";

            return await db.ExecuteScalarAsync<int>(query, billData);
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
                    query += " AND IsValidated=1 AND IsAcknowledged=0";
                    break;
                case BatchPipelineStatus.Acknowledged:
                    query += " AND IsAcknowledged=1 AND IsPrepped=0";
                    break;
                case BatchPipelineStatus.LetterGenerated:
                    query += " AND IsLetterGenerated=1 AND IsSubmittedToAuthoMail=0";
                    break;
                case BatchPipelineStatus.SubmittedToAuthoMail:
                    query += " AND IsSubmittedToAuthoMail=1 AND IsSubmittedToStellent=0";
                    break;
                case BatchPipelineStatus.SubmittedToStellent:
                    query += " AND IsSubmittedToStellent=1";
                    break;
                default:
                    break;
            }
            return await db.QueryAsync<Batch>(query, new { PipelineStatus = pipelineStatus });
        }

        public async Task<int> InsertLienInfo(int batchId, ParsingFieldData lienRecord)
        {
            Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
            keyValuePairs.Add("BatchId", batchId);
            keyValuePairs.Add("MailPieceStatus", RecordStatus.InProgress);

            lienRecord.ParsingFields.ForEach(fld =>
            {
                keyValuePairs.Add(fld.FieldName, fld.StrValue);
            });

            var paramList = new DynamicParameters();
            List<string> paramNameList = new List<string>();
            foreach (var pair in keyValuePairs)
            {
                var paramName = $"@{pair.Key}";
                paramList.Add(paramName, pair.Value);
                paramNameList.Add(paramName);
            }

            var cols = String.Join(",", keyValuePairs.Keys);
            var paramNames = String.Join(",", paramNameList);

            var query = "INSERT INTO [dbo].[LienData] ({0}) Output Inserted.Id VALUES ({1})".Fill(cols, paramNames);

            return await db.ExecuteScalarAsync<int>(query, paramList);
        }

        public async Task<int> InsertBillInfo(int lienId, List<ParsingFieldData> billRecords)
        {
            int val = 0;
            foreach (var billRecord in billRecords)
            {
                Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
                keyValuePairs.Add("LienDataId", lienId.ToString());

                billRecord.ParsingFields.ForEach(fld =>
                {
                    keyValuePairs.Add(fld.FieldName, fld.StrValue);
                });

                var paramList = new DynamicParameters();
                List<string> paramNameList = new List<string>();
                foreach (var pair in keyValuePairs)
                {
                    var paramName = $"@{pair.Key}";
                    paramList.Add(paramName, pair.Value);
                    paramNameList.Add(paramName);
                }

                var cols = String.Join(",", keyValuePairs.Keys);
                var paramNames = String.Join(",", paramNameList);

                var query = "INSERT INTO [dbo].[BillData] ({0}) VALUES ({1})".Fill(cols, paramNames);

                val = await db.ExecuteScalarAsync<int>(query, paramList);
            }

            return val;
        }

        public async Task<IEnumerable<LienData>> GetLienDataByBatchId(int batchId)
        {
            string query = "SELECT * FROM [dbo].[LienData] L INNER JOIN [dbo].[BillData] BL ON L.Id=BL.LienDataId INNER JOIN [dbo].[Batches] B ON L.BatchId=B.Id  WHERE L.BatchId=@BatchId";

            var lienDictionary = new Dictionary<int, LienData>();

            return await db.QueryAsync<LienData, BillData, Batch, LienData>(query,
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
    }
}
