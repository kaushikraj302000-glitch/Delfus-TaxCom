using SmartDocs.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public class DelfusRequest : MergeRequest<TransformData, DelfusData>
    {
        public List<string> DatabaseFields { get; set; }
    }
}
