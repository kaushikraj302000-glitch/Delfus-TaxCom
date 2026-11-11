using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
{
    public class BillData
    {
        public int Id { get; set; }
        public int LienDataId { get; set; }
        public string RD_BILL_OUT { get; set; }
        public string RD_CNTRCT_ID_OB { get; set; }
        public string RD_TINET_SEQ_NBR_OB { get; set; }
        public string RD_TAX_BILL_YR { get; set; }
        public string RD_BILL_TYP_DESC { get; set; }
        public string RD_BILL_SEARCH_DT { get; set; }
        public string RD_BILL_SEARCH_DT_TXT { get; set; }
        public string RD_INSTL_NBR { get; set; }
        public string RD_TAX_AMT { get; set; }
        public string RD_PRFL_ID_OB { get; set; }
        public string RD_SUBAPPL_CD_OB { get; set; }
        public string RD_SYI_CD { get; set; }
        public string RD_SYI_CD_DESC { get; set; }
        public string RD_TA_TAX_BILL_YR { get; set; }
        public string RD_TAX_1_AMT { get; set; }
        public string RD_TAX_2_AMT { get; set; }
        public string RD_TAX_3_AMT { get; set; }
        public string RD_TAX_4_AMT { get; set; }
        public string FILLER { get; set; }
        public LienData LienRecord { get; set; }
    }
}
