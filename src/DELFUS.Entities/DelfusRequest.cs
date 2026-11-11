using SmartDocs.Entities;
using System;
using System.Collections.Generic;

namespace DELFUS.Entities
{

    public class DelfusRequest : MergeRequest<TransformData, DelfusData>
    {
        public List<string> DatabaseFields { get; set; }
    }
}
