using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public class TaxComConfig
    {
        public string TaxComTemplatePath { get; set; }
        public string TaxComRootPath { get; set; }
        public string TaxComInputArchivePath { get; set; }
        public string TaxComSourcePath { get; set; }
        public string TaxComPdfsStellentPath { get; set; }
        public string TaxCom_API { get; set; }
        public string TaxComAMBatchPath { get; set; }
        public string GoogleBucketName { get; set; }
        public string GoogleArchiveBucketName { get; set; }
        public string AppEnvironment { get; set; }

        public string TaxComNTSFile { get; set; }
    }

    public class CLPConfig
    {
        public string Token_URL { get; set; }
        public string API_URL { get; set; }
        public string Client_Cred { get; set; }
        public string CCEmailAddresses { get; set; }
    }

    public class ClientEmailConfiguration
    {
        public string To { get; set; }
        public string Cc { get; set; }
        public string TemplatePath { get; set; }
    }

    public class UnexpectedEmailConfiguration
    {
        public string To { get; set; }
        public string Cc { get; set; }
        public string Subject { get; set; }
    }
}
