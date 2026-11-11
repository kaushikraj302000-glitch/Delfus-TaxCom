using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public class DelfusData
    {
        private const string DateFormat = "MMMM dd, yyyy";

        public DelfusData()
        {
            this.TaxDetails = new List<TaxDetails>();
        }
        
        public string CurrentDte { get { return Common.EstToday.ToString(DateFormat); } }
        public string DueDte { get; set; }
        public string DueDteYr { get; set; }
        public string Borrower1stNm { get; set; }
        public string Borrower2ndNm { get; set; }
        public string BorrowerAddr1Txt { get; set; }
        public string BorrowerAddr2Txt { get; set; }
        public string BorrowerCityNm { get; set; }
        public string BorrowerStateAbbr { get; set; }
        public string BorrowerZIPCD { get; set; }
        public string LoanId { get; set; }
        public string ParcelSitusStrNbr { get; set; }
        public string ParcelSitusStrNm { get; set; }
        public string ParcelSitusCityNm { get; set; }
        public string ParcelSitusStAbbr { get; set; }
        public string ParcelSitusZIPCD { get; set; }
        public string OrderNbr { get; set; }
        public string LienTotalBase { get; set; }
        public string TaxId { get; set; }
        public string AgencyNm { get; set; }
        public string AgencyPhnNbr { get; set; }
        public string CustNm { get; set; }
        public string CustId { get; set; }
        public string TmpltId { get; set; }
        public string LienSearchDteTxt { get; set; }
        public string DtePlus30 { get { return Common.EstToday.AddDays(30).ToString(DateFormat); } }
        public string DtePlus21 { get { return Common.EstToday.AddDays(21).ToString(DateFormat); } }
        public string DtePlus15 { get { return Common.EstToday.AddDays(15).ToString(DateFormat); } }
        public string AgencyNbr { get; set; }
        public string AgencyStr { get; set; }
        public string AgencyCityNm { get; set; }
        public string AgencyStAbbr { get; set; }
        public string AgencyZIP { get; set; }
        public string LienPostingDteTxt { get; set; }
        public string PostDate { get => this.LienPostingDteUsa; }
        public string BorrForeignAddr { get; set; }
        public string LienPostingDteUsa { get; set; }
        public string LienSearchDteUsa { get; set; }
        public string AgencyPayeeNm { get; set; }
        public string PropOwnerTxt { get; set; }
        public string LoanGroup { get; set; }
        public List<TaxDetails> TaxDetails { get; set; }
    }

    public class TaxDetails
    {
        public string BillTypeDesc { get; set; }
        public string BillSearchDteUSA { get; set; }
        public string BillSearchDteYrUSA { get; set; }
        public string AgencyName { get; set; }
        public double BillTaxAmount { get; set; }
        public string SyiCodeDesc { get; set; }
    }

    public class Context
    {

    }

}

