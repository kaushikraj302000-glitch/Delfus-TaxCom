using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
{
    public class AgencyInfo
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string AgencyNumber { get; set; }
        public string TaxCollectorName { get; set; }
        public string TaxCollectorAddress { get; set; }
        public string TaxCollectorCity { get; set; }
        public string TaxCollectorState { get; set; }
        public string TaxCollectorZip { get; set; }
        public string ELD { get; set; }
        public string DueDate { get; set; }
    }

    public class AgencyPropertyInfo
    {
        public int Id { get; set; }
        public int AgencyId { get; set; }
        public string AgencyName { get; set; }
        public string ParcelNumber { get; set; }
        public string PropertyName { get; set; }
        public string PropertyFullAddress { get; set; }
        public string PropertyKey { get; set; }
    }
}
