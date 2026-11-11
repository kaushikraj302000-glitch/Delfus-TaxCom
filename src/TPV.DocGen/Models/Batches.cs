using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TPV.DocGen
{
    public class Batch
    {
        public int Id { get; set; }
        public string BatchName { get; set; }
        public string ValidationStatus { get; set; }
        public string ValidationErrors { get; set; }
        public string PipelineStatus { get; set; }
        public bool IsDownloaded { get; set; }
        public DateTime? DownloadedOn { get; set; }
        public bool IsParsed { get; set; }
        public DateTime? ParsedOn { get; set; }
        public bool IsValidated { get; set; }
        public DateTime? ValidatedOn { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedOn { get; set; }
        public bool IsLetterGeneratedOn { get; set; }
        public DateTime? LetterGeneratedOn { get; set; }
        public bool IsSubmittedToAuthoMail { get; set; }
        public DateTime? SubmittedToAuthoMailOn { get; set; }
        public bool IsRejected { get; set; }
        public DateTime? RejectedOn { get; set; }
    }

    public class BatchInfo
    {
        public string BatchRepo { get; private set; }
        public string RequestPath { get; private set; }
        public string LettersPath { get; private set; }
        public string BatchName { get; private set; }

        public BatchInfo(int batchId, string rootPath, string batchName)
        {
            BatchName = batchName.Trim();
            //Combined path
            BatchRepo = Path.Combine(rootPath, $"{batchId}");

            RequestPath = Path.Combine(BatchRepo, "Request");
            LettersPath = Path.Combine(BatchRepo, "Letters");
        }
    }
}
