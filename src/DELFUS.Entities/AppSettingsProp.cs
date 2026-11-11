using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
{
    public class DelfusConfig
    {
        public string DelfusTemplatePath { get; set; }
        public string DelfusRootPath { get; set; }
        public string DelfusSourceArchivePath { get; set; }
        public string DelfusSourcePath { get; set; }
        public string Delfus_API { get; set; }
        public string DelfusAMBatchPath { get; set; }
        public string DelfusStellentPath { get; set; }
        public string AppEnvironment { get; set; }
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
