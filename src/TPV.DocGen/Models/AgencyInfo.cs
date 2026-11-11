using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace TPV.DocGen
{
    public class AgencyInfo
    {
        [Ignore]
        public int Id { get; set; }
        [Ignore]
        public int BatchId { get; set; }
        [Ignore]
        public string RecordStatus { get; set; }
        [Ignore]
        public string DisqualifyReason { get; set; }
        [Ignore]
        public string PdfFileName { get; set; }
        [Name("Agency Number")]
        public string AgencyNumber { get; set; }
        [Name("Agency Name")]
        public string AgencyName { get; set; }
        [Name("Tax Collector Name")]
        public string TaxCollectorName { get; set; }
        [Name("Tax Collector Address")]
        public string TaxCollectorAddress { get; set; }
        [Name("Tax Collector City")]
        public string TaxCollectorCity { get; set; }
        [Name("Tax Collector State")]
        public string TaxCollectorState { get; set; }
        [Name("Tax Collector Zip Code")]
        public string TaxCollectorZip { get; set; }
        public string TaxCollectorCityStateZip { get { return $"{TaxCollectorCity} {TaxCollectorState} {TaxCollectorZip}"; } }
        [Name("ELD")]
        public string ELD { get; set; }
        [Name("Balance Due")]
        public string DueDate { get; set; }
        public IEnumerable<AgencyPropertyInfo> Properties { get; set; }
    }

    public class AgencyPropertyInfo
    {
        [Ignore]
        public int Id { get; set; }
        [Ignore]
        public int BatchId { get; set; }
        [Ignore]
        public int? AgencyId { get; set; }
        [Name("Agency Number")]
        public string AgencyNumber { get; set; }
        [Name("Agency Name")]
        public string AgencyName { get; set; }
        [Name("Parcel Number")]
        public string ParcelNumber { get; set; }
        [Name("Property Name")]
        public string PropertyName { get; set; }
        [Name("Property Street Address")]
        public string PropertyStreetAddress { get; set; }
        [Name("Key")]
        public string KeyNo { get; set; }
    }
}
