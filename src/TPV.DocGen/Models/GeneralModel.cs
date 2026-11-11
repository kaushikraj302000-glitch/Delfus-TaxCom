using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Serialization;

namespace TPV.DocGen
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
        public const string InvalidZip = "Provided zip code is invalid. It must be specified in any one of following format 00000, 00000-0000";
        public const string NoKeyNumber = "Key number does not exist.";
        public const string NoParcelNumber = "Parcel number does not exist.";
        public const string InvalidTaxAddress = "Missing or invalid tax collector address.";
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
    }

    public static class GroupTypes
    {
        public const string HouseHolding = "HH";
        public const string BulkMailing = "BM";
        public const string Single = "SG";
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

        public static DateTime UtcToClientTimeZone(this DateTime dt, string clientTimeZone)
        {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.FindSystemTimeZoneById("UTC"), TimeZoneInfo.FindSystemTimeZoneById(clientTimeZone));
        }

        public static DateTime ClientTimeZoneToUtc(this DateTime dt, string clientTimeZone)
        {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.FindSystemTimeZoneById(clientTimeZone), TimeZoneInfo.FindSystemTimeZoneById("UTC"));
        }

        public static DateTime UtcToEst(this DateTime dt)
        {
            return dt.UtcToClientTimeZone("Eastern Standard Time");
        }

        public static DateTime EstToUtc(this DateTime dt)
        {
            return dt.ClientTimeZoneToUtc("Eastern Standard Time");
        }

        public static string ToXML(this object value)
        {
            using (var stringwriter = new System.IO.StringWriter())
            {
                var serializer = new XmlSerializer(value.GetType());
                serializer.Serialize(stringwriter, value);
                return stringwriter.ToString();
            }
        }

        public static string AnyFormatToDateTimeString(this string date, string format = null)
        {
            string[] formats = {"M/d/yyyy h:mm:ss tt", "M/d/yyyy h:mm tt",
"MM/dd/yyyy hh:mm:ss", "M/d/yyyy h:mm:ss","M/d/yyyy hh:mm tt", "M/d/yyyy hh tt","M/d/yyyy h:mm", "M/d/yyyy h:mm","MM/dd/yyyy hh:mm", "M/dd/yyyy hh:mm","dd-MMM-yyyy hh:mm:ss","MMMM     dd, yyyy","MMM-dd-yyyy hh:mm:ss","MMMM dd, yyyy","MMMM dd, yyyy hh:mm:ss","MM-dd-yyyy hh:mm tt","dd-MM-yyyy hh:mm:ss","MM-dd-yyyy hh:mm:ss","M/d/yyyy H:mm:ss tt", "M/d/yyyy H:mm tt","MM/dd/yyyy HH:mm:ss", "M/d/yyyy h:mm:ss","M/d/yyyy HH:mm tt", "M/d/yyyy HH tt","M/d/yyyy h:mm", "M/d/yyyy h:mm","MM/dd/yyyy HH:mm", "M/dd/yyyy HH:mm","dd-MMM-yyyy HH:mm:ss","MMMM     dd, yyyy","MMM-dd-yyyy HH:mm:ss","MMMM dd, yyyy","MMMM dd, yyyy HH:mm:ss","MM-dd-yyyy HH:mm","dd-MM-yyyy HH:mm:ss","MM-dd-yyyy HH:mm:ss","yyyyMMdd","yyyyMMdd hhmmss","dd-MM-yyyy"};

            DateTime dateValue;
            if (DateTime.TryParseExact(date, formats, new CultureInfo("en-US"), DateTimeStyles.None, out dateValue))
            {
                if (!string.IsNullOrWhiteSpace(format))
                {
                    return dateValue.ToString(format);
                }
                else
                {
                    return dateValue.ToString();
                }
            }
            else
                return null;
        }
    }
}
