using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public class LienData
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string DisqualifyReason { get; set; }
        public string RecordStatus { get; set; }
        public string DocumentType { get; set; }
        public string LogoFileName { get; set; }
        public string PdfFileName { get; set; }
        public int BillCount { get; set; }
        public string RD_LIEN_OUT { get; set; }
        public string RD_CNTRCT_ID_OL { get; set; }
        public string RD_TINET_SEQ_NBR_OL { get; set; }
        public string RD_TA_ORDR_ID { get; set; }
        public string RD_TA_ORDR_MOD_ID { get; set; }
        public string RD_TA_LNDR_BRNCH_ID { get; set; }
        public string RD_TMPLT_CD { get; set; }
        public string RD_BRRW_FRST_NM { get; set; }
        public string RD_BRRW_LAST_NM { get; set; }
        public string RD_BRRW_ADDR1_TXT { get; set; }
        public string RD_BRRW_ADDR2_TXT { get; set; }
        public string RD_BRRW_CITY_NM { get; set; }
        public string RD_BRRW_FOREIGN_ADDR_TXT { get; set; }
        public string RD_BRRW_STATE_ABBR { get; set; }
        public string RD_BRRW_CNTRY_CD { get; set; }
        public string RD_BRRW_ZIP_CD { get; set; }
        public string RD_SITUS_STR_NBR { get; set; }
        public string RD_SITUS_STR_NM { get; set; }
        public string RD_SITUS_CITY_NM { get; set; }
        public string RD_SITUS_CNTRY_CD { get; set; }
        public string RD_SITUS_STATE_ABBR { get; set; }
        public string RD_SITUS_ZIP_CD { get; set; }
        public string RD_TAX_ID { get; set; }
        public string RD_AGENCY_ID { get; set; }
        public string RD_AGENCY_NM { get; set; }
        public string RD_AGCY_ADDR1_TXT { get; set; }
        public string RD_AGCY_CNTRY_CD { get; set; }
        public string RD_AGCY_PAYEE_NM { get; set; }
        public string RD_AGCY_PHONE_TXT { get; set; }
        public string RD_AGCY_STATE_ABBR { get; set; }
        public string RD_AGCY_ZIP_CD { get; set; }
        public string RD_AGNCY_CITY_NM { get; set; }
        public string RD_BATCH_NBR { get; set; }
        public string RD_BEG_DELQ_YR { get; set; }
        public string RD_CNTCT_NM { get; set; }
        public string RD_CURR_YR_BASE_AMT { get; set; }
        public string RD_CURR_YR_DELQ_AMT { get; set; }
        public string RD_CURR_YR_SUPP_AMT { get; set; }
        public string RD_CUST_ID { get; set; }
        public string RD_CUST_NM { get; set; }
        public string RD_CUST_ADDR1_TXT { get; set; }
        public string RD_CUST_ADDR2_TXT { get; set; }
        public string RD_CUST_CITY_NM { get; set; }
        public string RD_CUST_CNTRY_CD { get; set; }
        public string RD_CUST_FAX_TXT { get; set; }
        public string RD_CUST_LTR_CD { get; set; }
        public string RD_CUST_STATE_ABBR { get; set; }
        public string RD_CUST_ZIP_CD { get; set; }
        public string RD_DELQ_LTR_DOWNLOAD_DT { get; set; }
        public string RD_DELQ_LTR_WIP_NBR { get; set; }
        public string RD_DELQ_POST_DT { get; set; }
        public string RD_DELQ_POST_DT_TXT { get; set; }
        public string RD_DLVR_ADDR1_TXT { get; set; }
        public string RD_DLVR_CITY_NM { get; set; }
        public string RD_DLVR_EMAIL_ADDR_TXT { get; set; }
        public string RD_DLVR_STATE_ABBR { get; set; }
        public string RD_DLVR_TYP_CD { get; set; }
        public string RD_DLVR_ZIP_CD { get; set; }
        public string RD_FIRST_INSTL_FOR_YR { get; set; }
        public string RD_INSRT_TYP_CD { get; set; }
        public string RD_INVESTOR_ID { get; set; }
        public string RD_LANG_CD { get; set; }
        public string RD_DLVR_NM { get; set; }
        public string RD_LIEN_SEARCH_DT { get; set; }
        public string RD_LIEN_SEARCH_DT_TXT { get; set; }
        public string RD_LOAN_ID { get; set; }
        public string RD_LTR_PDF_DOC_ID { get; set; }
        public string RD_NOTE_CD { get; set; }
        public string RD_NOTE_CD_DESC { get; set; }
        public string RD_PRFL_ID_OL { get; set; }
        public string RD_PRIOR_YR_DELQ_AMT { get; set; }
        public string RD_REDEM1_AMT { get; set; }
        public string RD_REDEM1_DT { get; set; }
        public string RD_REDEM2_DT { get; set; }
        public string RD_REDEM2_AMT { get; set; }
        public string RD_RT_TAXR_ID { get; set; }
        public string RD_SIMPLEX_DUPLEX_FLG { get; set; }
        public string RD_STATUS_MSG_DESC { get; set; }
        public string RD_SUBAPPL_CD_OL { get; set; }
        public string RD_SUBMIT_DT { get; set; }
        public string RD_TOT_BASE_AMT { get; set; }
        public string RD_OUT_ENVLP_TYP_ID { get; set; }
        public string RD_IN_ENVLP_TYP_ID { get; set; }
        public string RD_CARR_CD { get; set; }
        public string RD_PAPER_TYP_ID { get; set; }
        public string RD_ELD_DT { get; set; }
        public string RD_ELD_DT_TXT { get; set; }
        public string RD_PROCESS_CD { get; set; }
        public string FILLER { get; set; }
        public bool IsEDeliveryRecord { get; set; }
        public Guid EmailId { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string EDeliveryStatus { get; set; }
        public DateTime EDeliveryStatusDate { get; set; }

        public List<BillData> BillRecords { get; set; }
        public Batch BatchInfo { get; set; }
    }
}
