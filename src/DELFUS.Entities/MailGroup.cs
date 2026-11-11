using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
{
    public class MailGroup
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string LoanId { get; set; }
        public string CustomerId { get; set; }
        public string DocumentType { get; set; }
        public string PdfFileName { get; set; }
        public string LogoFileName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Country { get; set; }
        public string GroupType { get; set; }
        public List<LienData> Letters { get; set; }
    }

    public class LienMailGroup
    {
        public int LienDataId { get; set; }
        public int MailGroupId { get; set; }
    }
}
