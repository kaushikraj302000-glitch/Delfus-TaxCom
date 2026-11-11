using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
{
    public class SpecialRequirement
    {
        public int Id { get; set; }
        public string CustId { get; set; }
        public string TemplateCode { get; set; }
        public string SpecialReq { get; set; }
    }

    public static class TemplateCodes
    {
        public const string All = "*";
    }
}
