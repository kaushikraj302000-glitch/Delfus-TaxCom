using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public class DocGenResponse
    {
        public Guid TransactionId { get; set; }
        public byte[] DocumentData { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
