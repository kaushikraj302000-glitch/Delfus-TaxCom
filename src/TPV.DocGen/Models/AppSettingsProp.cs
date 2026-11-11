using System;
using System.Collections.Generic;
using System.Text;

namespace TPV.DocGen
{
    public class TPVConfig
    {
        public string TPVTemplatePath { get; set; }
        public string TPVRootPath { get; set; }
        public string TPVSourceArchivePath { get; set; }
        public string TPVSourcePath { get; set; }
        public string TPV_API { get; set; }
        public string TPVAMBatchPath { get; set; }
        public string TPVDocumentType { get; set; }
        public ClientEmailConfig ClientEmailConfig { get; set; }
        public UnexpectedEmailConfig UnexpectedEmailConfig { get; set; }
    }

    public class ClientEmailConfig
    {
        public string To { get; set; }
        public string Cc { get; set; }
        public string TemplatePath { get; set; }
    }

    public class UnexpectedEmailConfig
    {
        public string To { get; set; }
        public string Cc { get; set; }
        public string Subject { get; set; }
    }
}
