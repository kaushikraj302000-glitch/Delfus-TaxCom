using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.TemplateVerifier
{
    public static class ParsingFieldExtensions
    {
        public static bool IsNullOrWhiteSpace(this string value)
        {
            return String.IsNullOrWhiteSpace(value);
        }

        public static string ToTitleCase(this string value)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
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

        public static object ToClrType(this string value, string strSqlDbType)
        {
            var sqlDbType = strSqlDbType.ParseEnum<SqlDbTypeCustom>(true);
            return value.ToClrType(sqlDbType);
        }
    }

    public static class DataExtension
    {
        public static object[] ToPropertyValueArray<T>(this T obj) where T : class
        {
            var props = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            return props.Select(p => p.GetValue(obj)).ToArray();
        }

        public static IEnumerable<T> DataTableAccessor<T>(this DataSet ds) where T : class, new()
        {
            string tableName = typeof(T).Name;
            if (ds != null && ds.Tables.Count > 0)
            {
                ds.Tables[0].TableName = tableName;
            }
            return DataTableAccessor<T>(ds, tableName, true);
        }

        public static IEnumerable<T> DataTableAccessor<T>(this DataSet ds, bool mapAllProperties) where T : class, new()
        {
            string tableName = typeof(T).Name;
            if (ds != null && ds.Tables.Count > 0)
            {
                ds.Tables[0].TableName = tableName;
            }
            return DataTableAccessor<T>(ds, tableName, mapAllProperties);
        }

        public static IEnumerable<T> DataTableAccessor<T>(this DataSet ds, string tableName) where T : class, new()
        {
            return DataTableAccessor<T>(ds, tableName, true);
        }

        public static IEnumerable<T> DataTableAccessor<T>(this DataSet ds, string tableName, bool mapAllProperties) where T : class, new()
        {
            if (!ds.Tables.Contains(tableName))
                throw new ArgumentException(string.Format("Table '{0}' does not exist", tableName), "tableName");

            var table = ds.Tables[tableName];

            List<T> collection = new List<T>(table.Rows.Count);

            // Select only public instance properties that can be written to and have corresponding column in the table
            var props = typeof(T)
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanWrite && (mapAllProperties || table.Columns.Contains(p.Name)))
                            .ToList();

            foreach (var row in table.AsEnumerable())
            {
                T obj = new T();
                foreach (var prop in props)
                {
                    //if (!table.Columns.Contains(prop.Name)) throw new ArgumentNullException(prop.Name, string.Format("DataTable does not contain '{0}' column.", prop.Name));
                    if (row[prop.Name] == DBNull.Value)
                    {
                        prop.SetValue(obj, null);
                    }
                    else
                    {
                        Type pType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                        MethodInfo convert;
                        if (pType.FullName == "System.TimeSpan")
                        {
                            convert = typeof(TimeSpan).GetMethod("Parse", new Type[] { typeof(string) });
                            prop.SetValue(obj, convert.Invoke(null, new object[] { row[prop.Name] }));
                        }
                        else
                        {
                            var value = row[prop.Name] == null ? null : Convert.ChangeType(row[prop.Name], pType);
                            prop.SetValue(obj, value);
                        }

                    }
                }

                collection.Add(obj);
            }

            return collection;
        }
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
