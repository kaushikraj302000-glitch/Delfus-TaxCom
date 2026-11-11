using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.TemplateImporter
{
    public class TemplateValidationInfo
    {
        public bool IsValidDoc { get; set; }
        public bool HasAnchorTagIssue { get; set; }
        public bool UsesAgencyAddress { get; set; }
        public List<string> NewMergeFields { get; set; }
    }
}
