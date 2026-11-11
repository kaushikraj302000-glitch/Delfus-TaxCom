using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace TaxCommercial.DocGen
{
    public static class Extensions
    {
        private static Regex inc;
        private static Regex llc;
        private static Regex ltd;

        static Extensions()
        {
            inc = new Regex(@"\bINC\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            llc = new Regex(@"\bLLC\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            ltd = new Regex(@"\bLTD\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public static bool IsNullOrWhiteSpace(this string value)
        {
            return String.IsNullOrWhiteSpace(value);
        }
        public static string TrimIfNotNullOrWhiteSpace(this string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value.Trim();
            }
            else
            {
                return value;
            }
        }

        public static string ToTitleCase(this string value)
        {
            if (value.IsNullOrWhiteSpace())
                return value;

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
        }

        public static string ToFormatBusinessAbbr(this string value)
        {
            if (value.IsNullOrWhiteSpace())
                return value;
            value = inc.Replace(value, "Inc");
            value = llc.Replace(value, "LLC");
            return ltd.Replace(value, "Ltd");
        }

        public static string ToDelfusClientConsistencyFormat(this string value, string searchText, string replaceText)
        {
            if (value.IsNullOrWhiteSpace())
                return value;
            return Regex.Replace(value, $@"\b({searchText})\b", replaceText, RegexOptions.IgnoreCase);
        }

        public static string ToStreetDirectional(this string value)
        {
            if (value.IsNullOrWhiteSpace())
                return value;
            return Regex.Replace(value, @"\b(N|NNE|ENE|E|ESE|SSE|S|SSW|WSW|W|WNW|NNW|NE|NW|SE|SW|PO Box|P O Box|P\.O\. Box|P\.O Box)\b", m => m.Value.ToUpper(), RegexOptions.IgnoreCase);
        }

        public static string Fill(this string format, params object[] args)
        {
            return String.Format(format, args);
        }

        public static bool IsFileExists(this string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }
            else if (File.Exists(filePath))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static MethodInfo GetConversionMethod(this Type t)
        {
            MethodInfo Convert;

            switch (t.FullName)
            {
                case "System.TimeSpan":
                    Convert = typeof(TimeSpan).GetMethod("Parse", new Type[] { typeof(string) });
                    break;
                default:
                    Convert = typeof(Convert).GetMethod("To" + t.Name, new Type[] { typeof(object) });
                    break;
            }

            return Convert;
        }

        public static void CreateDirectoryIfNotExist(this string dirPath)
        {
            if (!dirPath.IsNullOrWhiteSpace())
            {
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);
            }
        }

        public static T ParseEnum<T>(this string value)
        {
            return value.ParseEnum<T>(true);
        }

        public static T ParseEnum<T>(this string value, bool ignoreCase)
        {
            return (T)Enum.Parse(typeof(T), value, ignoreCase);
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
      
        public static bool IsNullOrEmpty(this string value)
        {
            return String.IsNullOrEmpty(value);
        }

        public static string ToCsvString(this string value)
        {
            return "\"{0}\"".Fill(value ?? "");
        }

        public static string FormattedName(this string name)
        {
            return name.Trim().TrimEnd(',');
        }

        public static void CleanDirectory(this string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }

        public static T Deserialize<T>(this string xml) where T : new()
        {
            T entity;
            var xmlSerializer = new XmlSerializer(typeof(T));

            //var attributes = typeof(T).GetCustomAttributes(typeof(SerializableAttribute), true);

            //if (attributes.Length == 0)
            //    throw new ArgumentException("T does not have attribute InsertableAttribute");

            using (var sr = new StringReader(xml))
            {
                entity = (T)xmlSerializer.Deserialize(sr);
                sr.Close();
            }

            return entity;
        }

        public static string Serialize<T>(this T obj) where T : new()
        {
            return obj.Serialize(null);
        }

        public static string Serialize<T>(this T obj, XmlSerializerNamespaces namespaces) where T : new()
        {
            var serializer = new XmlSerializer(typeof(T));
            string xml = null;
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, obj, namespaces);
                xml = stringWriter.ToString();
            }
            return xml;
        }

        public static string GetValidFileName(this string value)
        {
            if (!value.IsNullOrWhiteSpace())
            {
                value = value.Replace(Environment.NewLine, "_");
            }
            foreach (var ch in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(ch.ToString(), "");
            }
            return value.Length > 160 ? value.Substring(0, 160) : value;
        }

        public static bool IsValidForFileName(this string value)
        {
            return !value.ToCharArray().Any(ch => Path.GetInvalidFileNameChars().Contains(ch));
            //return !Regex.IsMatch(value, Global.FileNameRegex);
        }

        public static string AnyFormatToDateTimeString(this string date)
        {
            string[] formats = {"M/d/yyyy h:mm:ss tt", "M/d/yyyy h:mm tt",
"MM/dd/yyyy hh:mm:ss", "M/d/yyyy h:mm:ss","M/d/yyyy hh:mm tt", "M/d/yyyy hh tt","M/d/yyyy h:mm", "M/d/yyyy h:mm","MM/dd/yyyy hh:mm", "M/dd/yyyy hh:mm","dd-MMM-yyyy hh:mm:ss","MMMM     dd, yyyy","MMM-dd-yyyy hh:mm:ss","MMMM dd, yyyy","MMMM dd, yyyy hh:mm:ss","MM-dd-yyyy hh:mm tt","dd-MM-yyyy hh:mm:ss","MM-dd-yyyy hh:mm:ss","M/d/yyyy H:mm:ss tt", "M/d/yyyy H:mm tt","MM/dd/yyyy HH:mm:ss", "M/d/yyyy h:mm:ss","M/d/yyyy HH:mm tt", "M/d/yyyy HH tt","M/d/yyyy h:mm", "M/d/yyyy h:mm","MM/dd/yyyy HH:mm", "M/dd/yyyy HH:mm","dd-MMM-yyyy HH:mm:ss","MMMM     dd, yyyy","MMM-dd-yyyy HH:mm:ss","MMMM dd, yyyy","MMMM dd, yyyy HH:mm:ss","MM-dd-yyyy HH:mm","dd-MM-yyyy HH:mm:ss","MM-dd-yyyy HH:mm:ss","yyyyMMdd","yyyyMMdd hhmmss"};

            DateTime dateValue;
            if (DateTime.TryParseExact(date, formats, new CultureInfo("en-US"), DateTimeStyles.None, out dateValue))
                return dateValue.ToString();
            else
                return null;
        }

        public static DateTime? AnyFormatToDateTime(this string date)
        {
            string[] formats = {"M/d/yyyy h:mm:ss tt", "M/d/yyyy h:mm tt",
"MM/dd/yyyy hh:mm:ss", "M/d/yyyy h:mm:ss","M/d/yyyy hh:mm tt", "M/d/yyyy hh tt","M/d/yyyy h:mm", "M/d/yyyy h:mm","MM/dd/yyyy hh:mm", "M/dd/yyyy hh:mm","dd-MMM-yyyy hh:mm:ss","MMMM     dd, yyyy","MMM-dd-yyyy hh:mm:ss","MMMM dd, yyyy","MMMM dd, yyyy hh:mm:ss","dd-MM-yyyy hh:mm tt","MM-dd-yyyy hh:mm tt","dd-MM-yyyy hh:mm:ss","MM-dd-yyyy hh:mm:ss","M/d/yyyy H:mm:ss tt", "M/d/yyyy H:mm tt","MM/dd/yyyy HH:mm:ss", "M/d/yyyy h:mm:ss","M/d/yyyy HH:mm tt", "M/d/yyyy HH tt","M/d/yyyy h:mm", "M/d/yyyy h:mm","MM/dd/yyyy HH:mm", "M/dd/yyyy HH:mm","dd-MMM-yyyy HH:mm:ss","MMMM     dd, yyyy","MMM-dd-yyyy HH:mm:ss","MMMM dd, yyyy","MMMM dd, yyyy HH:mm:ss","MM-dd-yyyy HH:mm","dd-MM-yyyy HH:mm:ss","MM-dd-yyyy HH:mm:ss","yyyyMMdd","yyyyMMdd hhmmss","yyyyMMdd HHmmss","yyMMdd HHmmss"};

            DateTime dateValue;
            if (DateTime.TryParseExact(date, formats, new CultureInfo("en-US"), DateTimeStyles.None, out dateValue))
                return dateValue;
            else
                return null;
        }

        public static string RemoveCSVStringFormat(this string str)
        {
            return str.Replace("\"", "");
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

        public static DateTime PstToUtc(this DateTime dt)
        {
            return dt.ClientTimeZoneToUtc("Pacific Standard Time");
        }

        public static DateTime ToStartDateTime(this DateTime dt)
        {
            return (dt.Date + new TimeSpan(00, 00, 00));
        }
        public static DateTime ToEndDateTime(this DateTime dt)
        {
            return (dt.Date + new TimeSpan(23, 59, 59));
        }

        public static string RemoveFormatting(this string value)
        {
            return value.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(",", "");
        }

        public static object GetValueOfPropertyFromPropertyName<T>(this T cls, string propertyName)
        {
            return (object)cls.GetType().GetProperty(propertyName).GetValue(cls, null);
        }

        public static string SeparateZip4And5(this string value, int number)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            string[] zipCode = value.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (zipCode.Length > 0)
            {
                foreach (string zip in zipCode)
                {
                    if (zip.Length == number)
                    {
                        return zip;
                    }
                    else if (zip.Length == number)
                    {
                        return zip;
                    }
                }
            }
            else
            {
                if (zipCode[0].Length == number)
                {
                    return zipCode[0];
                }
                else if (zipCode[0].Length == number)
                {
                    return zipCode[0];
                }
            }

            return "";
        }

        public static List<string> SplitCsv(this string csv)
        {
            var values = new List<string>();

            int last = -1;
            bool inQuotes = false;

            int n = 0;
            while (n < csv.Length)
            {
                switch (csv[n])
                {
                    case '"':
                        inQuotes = !inQuotes;
                        break;
                    case ',':
                        if (!inQuotes)
                        {
                            values.Add(csv.Substring(last + 1, (n - last)).Trim(' ', ','));
                            last = n;
                        }
                        break;
                }
                n++;
            }

            if (last != csv.Length - 1)
                values.Add(csv.Substring(last + 1).Trim());

            return values;
        }

        public static object ToClrType(this string value, SqlDbTypeCustom sqlDbTypeCustom)
        {
            return value.ToClrType(sqlDbTypeCustom, null);
        }

        public static object ToClrType(this string value, SqlDbTypeCustom sqlDbTypeCustom, string dateTimeFormat)
        {
            if (value.IsNullOrWhiteSpace()) return null;

            switch (sqlDbTypeCustom)
            {
                case SqlDbTypeCustom.BigInt:
                    return Convert.ChangeType(value, TypeCode.Int64);

                case SqlDbTypeCustom.Binary:
                case SqlDbTypeCustom.Image:
                case SqlDbTypeCustom.Timestamp:
                case SqlDbTypeCustom.VarBinary:
                    throw new NotImplementedException();

                case SqlDbTypeCustom.Bit:
                    return Convert.ChangeType(value, TypeCode.Boolean);

                case SqlDbTypeCustom.Char:
                case SqlDbTypeCustom.NChar:
                case SqlDbTypeCustom.NText:
                case SqlDbTypeCustom.NVarChar:
                case SqlDbTypeCustom.Text:
                case SqlDbTypeCustom.VarChar:
                case SqlDbTypeCustom.Xml:
                    return value;

                case SqlDbTypeCustom.DateTime:
                case SqlDbTypeCustom.SmallDateTime:
                case SqlDbTypeCustom.Date:
                case SqlDbTypeCustom.Time:
                case SqlDbTypeCustom.DateTime2:
                    if (dateTimeFormat.IsNullOrWhiteSpace())
                        return Convert.ChangeType(value, TypeCode.DateTime);
                    else
                        return DateTime.ParseExact(value, dateTimeFormat, null);

                case SqlDbTypeCustom.Decimal:
                case SqlDbTypeCustom.Amount:
                case SqlDbTypeCustom.InterestRate:
                case SqlDbTypeCustom.Money:
                case SqlDbTypeCustom.SmallMoney:
                    return Convert.ChangeType(value, TypeCode.Decimal);

                case SqlDbTypeCustom.Float:
                    return Convert.ChangeType(value, TypeCode.Double);

                case SqlDbTypeCustom.Int:
                    return Convert.ChangeType(value, TypeCode.Int32);

                case SqlDbTypeCustom.Real:
                    return Convert.ChangeType(value, TypeCode.Single);

                case SqlDbTypeCustom.UniqueIdentifier:
                    return Guid.Parse(value);

                case SqlDbTypeCustom.SmallInt:
                    return Convert.ChangeType(value, TypeCode.Int16);

                case SqlDbTypeCustom.TinyInt:
                    return Convert.ChangeType(value, TypeCode.Byte);

                case SqlDbTypeCustom.Variant:
                case SqlDbTypeCustom.Udt:
                    throw new NotImplementedException();

                case SqlDbTypeCustom.Structured:
                    throw new NotImplementedException();

                case SqlDbTypeCustom.DateTimeOffset:
                    return DateTimeOffset.Parse(value);

                default:
                    throw new ArgumentOutOfRangeException("sqlType");
            }
        }

        public static void MergePDFs(params string[] pdflist)
        {
            // Make sure we have enough arguments to work with
            //if (pdflist.Length < 3)
            //{
            //    throw new ArgumentException("There should be at least three arguments to use this method.");
            //}

            //// Make sure we find all input files
            //for (int i = 0; i < pdflist.Length - 1; i++)
            //{
            //    var pdf = pdflist[i];

            //    if (!File.Exists(pdf))
            //    {
            //        throw new ArgumentException(String.Format("File '{0}' not found.", pdf));
            //    }
            //}

            var dest = pdflist[pdflist.Length - 1];

            if (Path.GetExtension(dest).ToLower() != ".pdf")
            {
                throw new ArgumentException("A valid destination PDF name must be specified.");
            }

            var temp = Path.Combine(Path.GetDirectoryName(dest), Path.GetRandomFileName());

            using (var ds = new FileStream(temp, FileMode.Create))
            using (iTextSharp.text.Document doc = new iTextSharp.text.Document())
            using (var copy = new PdfSmartCopy(doc, ds))
            {
                doc.Open();
                // This is done to open and merge (Secured) PDFs which allow printing but not copying or merging
                PdfReader.unethicalreading = true;
                //Loop through each input file
                for (int i = 0; i < pdflist.Length - 1; i++)
                {
                    var pdf = pdflist[i];

                    //Create a PdfReader
                    using (var reader = new PdfReader(pdf))
                    {
                        //Add the entire document
                        copy.AddDocument(reader);
                        reader.Close();
                    }
                }
                PdfReader.unethicalreading = false;
                doc.Close();
            }

            // This is done to ensure when file is seen by other programs, it is completely ready
            if (File.Exists(dest))
            {
                File.Delete(dest);
            }

            File.Move(temp, dest);
        }

        public enum SqlDbTypeCustom
        {
            // Summary:
            //     System.Int64. A 64-bit signed integer.
            BigInt = 0,
            //
            // Summary:
            //     System.Array of type System.Byte. A fixed-length stream of binary data ranging
            //     between 1 and 8,000 bytes.
            Binary = 1,
            //
            // Summary:
            //     System.Boolean. An unsigned numeric value that can be 0, 1, or null.
            Bit = 2,
            //
            // Summary:
            //     System.String. A fixed-length stream of non-Unicode characters ranging between
            //     1 and 8,000 characters.
            Char = 3,
            //
            // Summary:
            //     System.DateTime. Date and time data ranging in value from January 1, 1753
            //     to December 31, 9999 to an accuracy of 3.33 milliseconds.
            DateTime = 4,
            //
            // Summary:
            //     System.Decimal. A fixed precision and scale numeric value between -10 38
            //     -1 and 10 38 -1.
            Decimal = 5,
            //
            // Summary:
            //     System.Double. A floating point number within the range of -1.79E +308 through
            //     1.79E +308.
            Float = 6,
            //
            // Summary:
            //     System.Array of type System.Byte. A variable-length stream of binary data
            //     ranging from 0 to 2 31 -1 (or 2,147,483,647) bytes.
            Image = 7,
            //
            // Summary:
            //     System.Int32. A 32-bit signed integer.
            Int = 8,
            //
            // Summary:
            //     System.Decimal. A currency value ranging from -2 63 (or -9,223,372,036,854,775,808)
            //     to 2 63 -1 (or +9,223,372,036,854,775,807) with an accuracy to a ten-thousandth
            //     of a currency unit.
            Money = 9,
            //
            // Summary:
            //     System.String. A fixed-length stream of Unicode characters ranging between
            //     1 and 4,000 characters.
            NChar = 10,
            //
            // Summary:
            //     System.String. A variable-length stream of Unicode data with a maximum length
            //     of 2 30 - 1 (or 1,073,741,823) characters.
            NText = 11,
            //
            // Summary:
            //     System.String. A variable-length stream of Unicode characters ranging between
            //     1 and 4,000 characters. Implicit conversion fails if the string is greater
            //     than 4,000 characters. Explicitly set the object when working with strings
            //     longer than 4,000 characters. Use System.Data.SqlDbType.NVarChar when the
            //     database column is nvarchar(max).
            NVarChar = 12,
            //
            // Summary:
            //     System.Single. A floating point number within the range of -3.40E +38 through
            //     3.40E +38.
            Real = 13,
            //
            // Summary:
            //     System.Guid. A globally unique identifier (or GUID).
            UniqueIdentifier = 14,
            //
            // Summary:
            //     System.DateTime. Date and time data ranging in value from January 1, 1900
            //     to June 6, 2079 to an accuracy of one minute.
            SmallDateTime = 15,
            //
            // Summary:
            //     System.Int16. A 16-bit signed integer.
            SmallInt = 16,
            //
            // Summary:
            //     System.Decimal. A currency value ranging from -214,748.3648 to +214,748.3647
            //     with an accuracy to a ten-thousandth of a currency unit.
            SmallMoney = 17,
            //
            // Summary:
            //     System.String. A variable-length stream of non-Unicode data with a maximum
            //     length of 2 31 -1 (or 2,147,483,647) characters.
            Text = 18,
            //
            // Summary:
            //     System.Array of type System.Byte. Automatically generated binary numbers,
            //     which are guaranteed to be unique within a database. timestamp is used typically
            //     as a mechanism for version-stamping table rows. The storage size is 8 bytes.
            Timestamp = 19,
            //
            // Summary:
            //     System.Byte. An 8-bit unsigned integer.
            TinyInt = 20,
            //
            // Summary:
            //     System.Array of type System.Byte. A variable-length stream of binary data
            //     ranging between 1 and 8,000 bytes. Implicit conversion fails if the byte
            //     array is greater than 8,000 bytes. Explicitly set the object when working
            //     with byte arrays larger than 8,000 bytes.
            VarBinary = 21,
            //
            // Summary:
            //     System.String. A variable-length stream of non-Unicode characters ranging
            //     between 1 and 8,000 characters. Use System.Data.SqlDbType.VarChar when the
            //     database column is varchar(max).
            VarChar = 22,
            //
            // Summary:
            //     System.Object. A special data type that can contain numeric, string, binary,
            //     or date data as well as the SQL Server values Empty and Null, which is assumed
            //     if no other type is declared.
            Variant = 23,
            //
            // Summary:
            //     An XML value. Obtain the XML as a string using the System.Data.SqlClient.SqlDataReader.GetValue(System.Int32)
            //     method or System.Data.SqlTypes.SqlXml.Value property, or as an System.Xml.XmlReader
            //     by calling the System.Data.SqlTypes.SqlXml.CreateReader() method.
            Xml = 25,
            //
            // Summary:
            //     A SQL Server user-defined type (UDT).
            Udt = 29,
            //
            // Summary:
            //     A special data type for specifying structured data contained in table-valued
            //     parameters.
            Structured = 30,
            //
            // Summary:
            //     Date data ranging in value from January 1,1 AD through December 31, 9999
            //     AD.
            Date = 31,
            //
            // Summary:
            //     Time data based on a 24-hour clock. Time value range is 00:00:00 through
            //     23:59:59.9999999 with an accuracy of 100 nanoseconds. Corresponds to a SQL
            //     Server time value.
            Time = 32,
            //
            // Summary:
            //     Date and time data. Date value range is from January 1,1 AD through December
            //     31, 9999 AD. Time value range is 00:00:00 through 23:59:59.9999999 with an
            //     accuracy of 100 nanoseconds.
            DateTime2 = 33,
            //
            // Summary:
            //     Date and time data with time zone awareness. Date value range is from January
            //     1,1 AD through December 31, 9999 AD. Time value range is 00:00:00 through
            //     23:59:59.9999999 with an accuracy of 100 nanoseconds. Time zone value range
            //     is -14:00 through +14:00.
            DateTimeOffset = 34,
            //
            // Summary:
            //     Custom Decimal type with (14,2) precision
            Amount = 101,
            //
            // Summary:
            //     Custom Decimal type with (6,3) precision
            InterestRate = 102,
        }
    }
}
