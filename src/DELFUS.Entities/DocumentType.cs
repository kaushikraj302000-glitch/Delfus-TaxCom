using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
{
    public class DocumentType
    {
        public int Id { get; set; }
        public string DocumentName { get; set; }
        public string OuterEnvelope { get; set; }
        public string BREEnvelope { get; set; }
        public bool IsColor { get; set; }
    }
}
