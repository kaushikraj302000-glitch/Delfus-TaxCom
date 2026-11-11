using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Text;

namespace TaxCommercial.DocGen
{
    public static class BatchPipelineStatus
    {
        public const string Downloaded = "Downloaded";
        public const string Parsed = "Parsed";
        public const string Validated = "Validated";
        public const string Acknowledged = "Acknowledged";
        public const string LetterGenerated = "LetterGenerated";
        public const string Prepped = "Prepped";
        public const string SubmittedToAuthoMail = "SubmittedToAuthoMail";
        public const string SubmittedToStellent = "SubmittedToStellent";
        public const string SubmittedToStellentGCP = "SubmittedToStellentGCP";
        public const string Rejected = "Rejected";
    }

    public static class DisqualificationReasons
    {
        public const string NoRecordFound = "File does not contain any record.";
        public const string TemplateNotFound = "No matching template found.";
        public const string LogoNotConfigured = "No matching logo configuration found.";
        public const string DoctypeNotConfigured = "Unknown Print and ship configuration.";
        public const string InvalidBulkMailAdd = "Client address details are missing or invalid.";
        public const string InvalidDeliveryMethod = "Email delivery method is not supported.";
        public const string FileNotStartingWithLienRecord = "File is not starting with lien record.";
        public const string InvalidZip = "Provided zip code is invalid. It must be specified in any one of following format NNNNN, NNNNN-NNNN, NNNNN NNNN";
        public const string EmailAddressNotFound = "Email address is not provided.";
        public const string InvalidEmailAddress = "Email address is not in a valid format.";
    }

    public enum BatchStatus
    {
        InProgress,
        Aborted,
        Processed,
    }

    public static class BatchFileType
    {
        public const string PdfOnly = "PdfOnly";
        public const string Pdf_Print = "Pdf_P";
        public const string Pdf_FedEx = "Pdf_F";
    }

    public static class BatchValidationStatus
    {
        public const string Qualified = "Qualified";
        public const string PartiallyQualified = "PartiallyQualified";
        public const string Disqualified = "Disqualified";
    }

    public static class RecordStatus
    {
        public const string InProgress = "InProgress";
        public const string Qualified = "Qualified";
        public const string Disqualified = "Disqualified";
        public const string LetterGenerated = "LetterGenerated";
        public const string SubmittedToAuthoMail = "SubmittedToAuthoMail";
        public const string SubmittedToStellent = "SubmittedToStellent";
    }

    public static class DeliveryCodes
    {
        public const string Borr = "1";
        public const string BorrBulk = "2";
        public const string BorrEmail = "3";
        public const string Bulk = "4";
        public const string BulkEmail = "5";
        public const string Email = "6";
        public const string Agency = "9";
        public const string AgencyBulk = "10";
    }

    public static class GroupTypes
    {
        public const string HouseHolding = "HH";
        public const string AgencyGrouping = "AG";
        public const string BulkMailing = "BM";
        public const string Single = "SG";
        public const string Agency = "A";
    }

    public class GroupPdfs
    {
        public string CustId { get; set; }
        public string TemplateCode { get; set; }
        public string EmailAddress { get; set; }
        public string PdfFilePath { get; set; }
        public int LienId { get; set; }
        public Guid EmailUniqueId { get; set; }
        public DateTime SubmittedDate { get; set; }
    }

    public static class Common
    {
        public static DateTime Now
        {
            get { return DateTime.UtcNow; }
        }

        public static DateTime Today
        {
            get { return DateTime.UtcNow.Date; }
        }

        public static DateTime EstToday
        {
            get { return DateTime.UtcNow.UtcToEst().Date; }
        }

        public static bool SendEmailNotification(string to, string cc, string subject, string body, bool isHTML, bool isAttachmentExist, string[] attachmentPaths = null)
        {
            try
            {
                using (SmtpClient smtpClient = new SmtpClient("smtp.corelogic.com"))
                {
                    using (MailMessage msg = new MailMessage())
                    {
                        msg.From = new MailAddress("donotreply@corelogic.com");
                        msg.IsBodyHtml = true;
                        msg.To.Add(to);
                        msg.CC.Add(cc);
                        msg.Subject = subject;
                        msg.Priority = MailPriority.High;
                        msg.Body = body;
                        msg.IsBodyHtml = isHTML;

                        if (isAttachmentExist)
                        {
                            foreach (var attachment in attachmentPaths)
                            {
                                if (File.Exists(attachment))
                                    msg.Attachments.Add(new System.Net.Mail.Attachment(attachment));
                            }
                        }
                        smtpClient.Send(msg);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }
}
