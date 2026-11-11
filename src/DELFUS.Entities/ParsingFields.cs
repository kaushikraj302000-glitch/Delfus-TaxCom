using DELFUS.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DELFUS.Entities
{
    public class ParsingField
    {
        public int Id { get; set; }
        public string FieldName { get; set; }
        public int StartPosition { get; set; }
        public int Length { get; set; }
        public bool IsActive { get; set; }
        public SqlDbTypeCustom _DataTypeParse
        {
            get
            {
                var regex = new Regex(@"[^(]*");
                return regex.Match(Convert.ToString(DataType)).Value.ParseEnum<SqlDbTypeCustom>(true);
            }
        }
        public string DataFormatTemplate { get; set; }
        public string DefaultValue { get; set; }
        public string DataType { get; set; }

        public string DataFormat
        {
            get { return DataFormatTemplate.Replace("<len>", Length.ToString()); }
        }

        public string FormattedValue
        {
            get { return DataFormat.Fill(Value); }
        }

        private string strValue;

        public string StrValue
        {
            get { return strValue; }
            set
            {
                strValue = value;
                if (_DataTypeParse == SqlDbTypeCustom.DateTime || _DataTypeParse == SqlDbTypeCustom.Date || _DataTypeParse == SqlDbTypeCustom.DateTime2)
                    clrValue = strValue.ToClrType(_DataTypeParse, DataFormatTemplate.Replace("{0:", "").Replace("}", ""));
                else
                    clrValue = strValue.ToClrType(_DataTypeParse);
            }
        }

        private object clrValue;

        public object Value
        {
            get { return clrValue; }
            //set
            //{
            //    clrValue = value;
            //    strValue = value.ToString();
            //}
        }

        public ParsingField Clone()
        {
            return (ParsingField)this.MemberwiseClone();
        }

        public override string ToString()
        {
            return StrValue;
        }
    }

    public class ParsingFieldData
    {
        public string RecordType { get; set; }
        public string ContractId { get; set; }
        public List<ParsingField> ParsingFields { get; set; }
    }
}
