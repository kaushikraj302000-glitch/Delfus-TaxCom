using DELFUS.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DELFUS.Entities
{
    public class DelfusData
    {
        private const string DateFormat = "MM'/'dd'/'yyyy";

        private static HashSet<string> _TitleCaseExceptions = new HashSet<string> { "TAXID", "BORROWERSTATEABBR", "BORROWERZIPCD", "LOANID", "PARCELSITUSSTABBR", "PARCELSITUSZIPCD", "ORDERNBR", "CUSTID", "TMPLTID", "AGENCYSTABBR", "AGENCYZIP", "__CONTEXT", "__TEMPLATES", "TAXDETAILS" };
        private static HashSet<string> _StreetDirectional = new HashSet<string> { "BORROWERADDR1TXT", "BORROWERADDR2TXT", "AGENCYSTR", "BORRFOREIGNADDR", "PARCELSITUSSTRNM" };

        public DelfusData()
        {
            this.TaxDetails = new List<TaxDetails>();
        }

        public void FormatTextCase(List<ClientConsistency> clientConsistencies)
        {
            var properties = this.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var p in properties)
            {
                if (!p.CanWrite)
                    continue;

                if (!_TitleCaseExceptions.Contains(p.Name.ToUpper()))
                {
                    var value = p.GetValue(this)?.ToString()?.ToTitleCase()?.ToFormatBusinessAbbr();

                    if (_StreetDirectional.Contains(p.Name.ToUpper()))
                    {
                        value = value.ToStreetDirectional();
                    }

                    if (p.Name.ToUpper() == "CUSTNM")
                    {
                        var consistencies = clientConsistencies.Where(r => r.CustId == this.CustId);
                        foreach(var consistency in consistencies)
                        {
                            value = value.ToDelfusClientConsistencyFormat(consistency.SearchText, consistency.ReplaceText);
                        }
                    }

                    p.SetValue(this, value);
                }
            }
        }

        public string CurrentDte { get { return Common.EstToday.ToString(DateFormat); } }
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
        public Context __Context { get; set; }
        public Templates __Templates { get; set; }
        public List<TaxDetails> TaxDetails { get; set; }
    }

    public class TaxDetails
    {
        public string BillTaxYear { get; set; }
        public string BillInstallmentNbr { get; set; }
        public string BillTaxAmount { get; set; }
    }

    public class Context
    {

    }

    public class Templates
    {
        public string TaxId { get; set; }
        public string TaxDetail { get; set; }
        public string TaxDetail2 { get; set; }
    }
}
