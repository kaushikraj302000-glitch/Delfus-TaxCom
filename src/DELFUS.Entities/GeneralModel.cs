using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
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
    }

    public enum BatchStatus
    {
        InProgress,
        Aborted,
        Processed,
    }

    public static class BatchFileType
    {
        public const string Regular = "Regular";
        public const string Pdf_A = "Pdf_A";
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

    public static class SpecialReqs
    {
        public const string HideBorrower2Name = "HideBorr2Name";
        public const string MaskingLoanNoFirst3Digit = "MaskLoanNoFirst3";
        public const string MaskingLoanNoLast4Digit = "MaskLoanNoLast4";
        public const string UseAgencyDetails = "AgencyName";
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
}
