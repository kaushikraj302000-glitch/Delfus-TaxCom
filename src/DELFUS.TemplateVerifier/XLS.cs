using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DELFUS.TemplateVerifier
{
    public class ReadSettings
    {
        /// <summary>
        /// Default it will take name of first sheet
        /// </summary>
        public string SheetName { get; set; }

        /// <summary>
        /// Default true (will remove trailing rows)
        /// </summary>
        public bool TrimTrailingEmptyRows { get; set; }

        /// <summary>
        /// It will skip all empty rows from sheet within given table(colxrow) Default true (will remove rows)
        /// </summary>
        public bool TrimEmptyRows { get; set; }

        /// <summary>
        /// Excel can have two columns "CITI" and "CITI ". Space is valid character for second column. But after reading we'll have "CITI" and "CITI1" like two columns
        /// </summary>
        public bool TrimColumnHeaders { get; set; }

        /// <summary>
        /// Excel headers cells can be multi-line sometimes. So, to consider them single line pass true.
        /// </summary>
        public bool SinglelineHeaders { get; set; }

        /// <summary>
        /// When you want to trim data of some columns
        /// </summary>
        public List<string> ColumnsToTrim { get; set; }

        /// <summary>
        /// When you want to trim data of some columns
        /// </summary>
        public List<string> ColumnsToGet { get; set; }

        /// <summary>
        /// Excel Row# of the header row in worksheet. By default 0.
        /// </summary>
        public int HeaderIndex { get; set; }

        /// <summary>
        /// Data row index to remove from table.
        /// </summary>
        public List<int> RowsToDelete { get; set; }

        public ReadSettings()
        {
            ColumnsToTrim = new List<string>();
            RowsToDelete = new List<int>();
            ColumnsToGet = new List<string>();
        }
    }

    public class XLS
    {
        IWorkbook workbook;
        const int MaximumNumberOfRowsPerSheet = 65536;
        IFormulaEvaluator formulaEvaluator;
        DataFormatter dataFormatter;

        public XLS()
        {
            Stream stream = null;
            Initialize(stream, false);
        }

        public XLS(bool isOpenXml)
        {
            Stream stream = null;
            Initialize(stream, isOpenXml);
        }

        public XLS(Stream file)
        {
            Initialize(file, false);
        }

        public XLS(Stream file, bool isOpenXml)
        {
            Initialize(file, isOpenXml);
        }

        public XLS(string file)
        {
            var isOpenXml = Path.GetExtension(file) == ".xlsx";
            Initialize(file, isOpenXml);
        }

        private void Initialize(string file, bool isOpenXml)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                Initialize(stream, isOpenXml);
            }
        }

        private void Initialize(Stream stream, bool isOpenXml)
        {
            if (isOpenXml)
            {
                workbook = stream == null ? new XSSFWorkbook() : new XSSFWorkbook(stream);
                formulaEvaluator = new XSSFFormulaEvaluator(workbook);
            }
            else
            {
                workbook = stream == null ? new HSSFWorkbook() : new HSSFWorkbook(stream);
                formulaEvaluator = new HSSFFormulaEvaluator(workbook);
            }

            dataFormatter = new DataFormatter(CultureInfo.InvariantCulture);
        }

        public List<string> GetAllSheetNames()
        {
            List<string> sheetNames = new List<string>();

            for (int i = 0; i < workbook.NumberOfSheets; i++)
            {
                sheetNames.Add(workbook.GetSheetName(i));
            }

            return sheetNames;
        }

        /// <summary>
        /// Return data in DataTable format from the first worksheet of the associated workbook. It will trim column headers and trim trailing/empty data rows 
        /// </summary>
        /// <returns></returns>
        public DataTable GetData()
        {
            return GetData(String.Empty);
        }

        /// <summary>
        /// Return data in DataTable format from the provided named worksheet of the associated workbook.It will trim column headers and trim trailing/empty data rows
        /// </summary>
        /// <returns></returns>
        public DataTable GetData(string sheetName)
        {
            ReadSettings readerSettings = new ReadSettings()
            {
                SheetName = sheetName,
                HeaderIndex = 0,
                TrimTrailingEmptyRows = true,
                TrimColumnHeaders = true,
                TrimEmptyRows = true
            };

            return GetData(readerSettings);
        }

        /// <summary>
        /// When you provide any of settings; you need to provide all other settings - you may ignore default properties.
        /// </summary>
        /// <param name="readSettings">How to read the data from which excel sheet.</param> 
        /// <returns>Datatable read from excel sheet.</returns>
        public DataTable GetData(ReadSettings readSettings)
        {

            #region Initialize reader settings

            readSettings = readSettings ?? new ReadSettings();

            if (workbook.NumberOfSheets == 0)
            {
                throw new ArgumentException("No worksheet found.");
            }

            if (readSettings.SheetName.IsNullOrEmpty())
            {
                readSettings.SheetName = workbook.GetSheetName(0);
            }

            ISheet sheet = workbook.GetSheet(readSettings.SheetName);

            if (sheet == null)
            {
                throw new ArgumentException("The specified sheet name or index is invalid.", readSettings.SheetName);
            }

            // get your target sheet by name
            DataTable dt = new DataTable(sheet.SheetName);

            #endregion

            #region Read Header Row from sheet

            Dictionary<string, int> columnRegister = new Dictionary<string, int>();

            // write header row
            IRow headerRow = sheet.GetRow(readSettings.HeaderIndex);

            if (headerRow == null)
            {
                throw new ArgumentException($"The specified header row index is invalid. Unable to read data from the sheet: {readSettings.SheetName}.", "HeaderIndex");
            }

            if (headerRow.Cells.Select(v => v.StringCellValue.ToLower().Trim()).Distinct().Count() != headerRow.Cells.Count)
            {
                throw new ArgumentException("In specified file, header row has duplicate column names.");
            }

            //if (readSettings.ColumnsToGet.Count > 0)
            //{
            //if (headerRow.Cells.Any(x => readSettings.ColumnsToGet.Any(y => y.ToLower().Trim() == x.StringCellValue.ToLower().Trim())))
            //{
            //    throw new ArgumentException("The specified sheet header row has some empty column name.", readSettings.SheetName);
            //}
            //}

            foreach (ICell headerCell in headerRow)
            {
                var columnName = headerCell.ToString().Trim();
                //Code added to ignore columns wout headers.If unnecesary data is present after some rows - after last row then also it will be ignored.
                //Example path : "$/General/LienReleaseDemo/Resources/InvalidExcelDummyDataAtEnd.xlsx (V53,Y53)"
                if (!string.IsNullOrEmpty(columnName))
                {
                    if (readSettings.SinglelineHeaders)
                    {
                        columnName = Regex.Replace(columnName, @"\s+", " ");
                        readSettings.TrimColumnHeaders = true;
                    }

                    if (readSettings.TrimColumnHeaders)
                    {
                        columnName = columnName.Trim();
                    }

                    if (columnRegister.ContainsKey(columnName))
                    {
                        columnRegister[columnName] = columnRegister[columnName] + 1;
                        columnName = String.Format("{0}{1}", columnName, columnRegister[columnName]);
                    }
                    else
                    {
                        columnRegister.Add(columnName, 0);
                    }
                    //for specified columns to get in dt
                    if (readSettings.ColumnsToGet.Count > 0 && readSettings.ColumnsToGet.Contains(columnName.ToLower().Trim()))
                    {
                        dt.Columns.Add(columnName);
                    }

                    //for all columns to get in dt
                    if (readSettings.ColumnsToGet.Count == 0)
                    {
                        dt.Columns.Add(columnName);
                    }
                }
            }

            #endregion

            #region Read DataRows from sheet

            var columnsToTrim = readSettings.ColumnsToTrim.Select(c => dt.Columns.IndexOf(c)).ToList();

            foreach (IRow row in sheet)
            {
                // skip header row and rows above it
                if (row.RowNum <= readSettings.HeaderIndex) continue;
                // skip empty rows
                if (readSettings.TrimEmptyRows)
                    if (row.Cells.All(c => c.CellType == CellType.Blank)) continue;

                DataRow dataRow = dt.NewRow();

                try
                {
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        var headerColumncell = headerRow.Cells.Single(s => s.StringCellValue.ToLower().Trim() == dt.Columns[i].ColumnName.ToLower());

                        var cellRefrence = new CellReference(headerColumncell);

                        var cell = row.GetCell(cellRefrence.Col, MissingCellPolicy.CREATE_NULL_AS_BLANK);

                        object value;

                        if (cell == null)
                        {
                            value = null;
                        }
                        else
                        {
                            switch (cell.CellType)
                            {
                                case CellType.Formula:
                                    {
                                        object returnValue = null;
                                        // When failed in evaluating the formula, use stored values instead...
                                        // and set cell value for reference from formula in other cells...
                                        switch (cell.CachedFormulaResultType)
                                        {
                                            case CellType.String:
                                                returnValue = cell.StringCellValue;
                                                break;
                                            case CellType.Numeric:
                                                if (DateUtil.IsCellDateFormatted(cell))
                                                {
                                                    returnValue = cell.DateCellValue;
                                                }
                                                else
                                                {
                                                    returnValue = cell.NumericCellValue;
                                                }
                                                //dataFormatter.FormatRawCellContents(cell.NumericCellValue, 0, cell.CellStyle.GetDataFormatString());
                                                break;
                                            case CellType.Boolean:
                                                returnValue = cell.BooleanCellValue.ToString();
                                                break;
                                            default:
                                                returnValue = (string)null;
                                                break;
                                        }
                                        value = returnValue;
                                    }
                                    break;
                                case CellType.Boolean:
                                    value = cell.BooleanCellValue;
                                    break;
                                case CellType.Numeric:
                                    if (DateUtil.IsCellDateFormatted(cell))
                                    {
                                        value = cell.DateCellValue;
                                    }
                                    else
                                    {
                                        value = cell.NumericCellValue;
                                    }
                                    break;
                                case CellType.String:
                                    value = cell.StringCellValue;
                                    break;
                                default:
                                    value = cell.ToString();
                                    break;
                            }

                            if (value is string && columnsToTrim.Contains(i))
                            {
                                value = ((string)value).Trim();
                            }

                            if (value is string)
                            {
                                value = ((string)value) == "[{{DBNull}}]" ? null : value;
                            }
                        }

                        dataRow[i] = value.ToNullOrString();
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Row is incomplete
                    continue;
                }

                dt.Rows.Add(dataRow);
            }

            #endregion

            #region Apply reader settings

            if (readSettings.TrimTrailingEmptyRows)
            {
                dt = TrimEmptyRow(dt);
            }

            readSettings.RowsToDelete
                .Where(i => i >= 0 && i < dt.Rows.Count)
                .Distinct()
                .OrderByDescending(i => i)
                .ToList()
                .ForEach(i => dt.Rows.RemoveAt(i));

            #endregion

            return dt;
        }

        /// <summary>
        /// This method will always remove empty rows from bottom those we might have received from the excel sheet.
        /// </summary>
        /// <param name="dt">Data read from the excel sheet</param>
        /// <returns>Data without any blank/empty rows at the end of excel sheet.</returns>
        public DataTable TrimEmptyRow(DataTable dt)
        {
            if (dt.Rows.Count == 0 || dt == null)
            {
                return dt;
            }

            while (dt.Rows.Count > 0 && dt.Rows[dt.Rows.Count - 1].ItemArray.All(i => i.ToNullOrString().IsNullOrWhiteSpace()))
            {
                dt.Rows.RemoveAt(dt.Rows.Count - 1);
            }

            return dt;
        }

        public int GetRowCount(int sheetIndex)
        {
            ISheet sheet = null;

            sheet = sheet ?? workbook.GetSheetAt(sheetIndex);
            // var sheet 
            // return  sheet.PhysicalNumberOfRows;

            return sheet.LastRowNum;
        }

        public bool CreateWorkSheet(string sheetName)
        {
            return workbook.CreateSheet(sheetName) == null ? false : true;
        }

        public void RemoveSheet(string sheetName)
        {
            int sheetIndex = workbook.GetSheetIndex(sheetName);

            if (sheetIndex == -1)
            {
                throw new ArgumentException("Sheet not found.", sheetName);
            }

            workbook.RemoveSheetAt(sheetIndex);
        }

        public void LoadData(DataTable dt)
        {
            LoadData(0, dt, true, true, null);
        }

        public void LoadData(string sheetName, DataTable dt)
        {
            LoadData(sheetName, dt, true, true, null);
        }

        public void LoadData(DataTable dt, bool ignoreColumnTitles)
        {
            LoadData(0, dt, ignoreColumnTitles, true, null);
        }

        public void LoadData(string sheetName, DataTable dt, bool ignoreColumnTitles)
        {
            LoadData(sheetName, dt, ignoreColumnTitles, true, null);
        }

        public void LoadData(DataTable dt, Action<IWorkbook> formatLogic)
        {
            LoadData(0, dt, true, true, formatLogic);
        }

        public void LoadData(string sheetName, DataTable dt, bool ignoreColumnTitles, bool autofitColumns, Action<IWorkbook> formatLogic)
        {
            LoadData(workbook.GetSheetIndex(sheetName), dt, ignoreColumnTitles, autofitColumns, formatLogic);
        }

        public void LoadData(int sheetIndex, DataTable dt, bool ignoreColumnTitles, bool autofitColumns, Action<IWorkbook> formatLogic)
        {
            ISheet sheet = null;

            if (sheetIndex == 0 && workbook.NumberOfSheets == 0)
            {
                sheet = workbook.CreateSheet("Sheet1");
            }

            if (sheetIndex < 0 || sheetIndex >= workbook.NumberOfSheets)
            {
                throw new ArgumentException("The specified sheet name or index is invalid.");
            }

            if (dt.Rows.Count >= MaximumNumberOfRowsPerSheet)
            {
                throw new Exception(string.Format("Excel 97-2003 format does not allow more than {0} rows of data", MaximumNumberOfRowsPerSheet));
            }

            sheet = sheet ?? workbook.GetSheetAt(sheetIndex);

            if (!ignoreColumnTitles)
            {
                // Write header row
                var row = sheet.CreateRow(0);

                for (var colIndex = 0; colIndex < dt.Columns.Count; colIndex++)
                {
                    var cell = row.CreateCell(colIndex);
                    cell.SetCellValue(dt.Columns[colIndex].ColumnName);

                    //if (headerRowStyle != null)
                    //    cell.CellStyle = headerRowStyle;
                }
            }

            for (var rowIndex = 1; rowIndex <= dt.Rows.Count; rowIndex++)
            {
                IRow row;
                //if cell exist then get other wise create
                if (rowIndex < sheet.LastRowNum)
                    row = sheet.GetRow(rowIndex);
                else
                    row = sheet.CreateRow(rowIndex);


                for (var colIndex = 0; colIndex < dt.Columns.Count; colIndex++)
                {
                    ICell cell;
                    //if cell exist then get other wise create
                    if (colIndex < row.Cells.Count)
                        cell = row.GetCell(colIndex);
                    else
                        cell = row.CreateCell(colIndex);

                    cell.SetValue(dt.Rows[rowIndex - 1][colIndex]);
                    cell.CellStyle = sheet.GetColumnStyle(colIndex);
                }
            }

            if (autofitColumns)
            {
                AutofitColumns(sheetIndex, dt.Columns.Count);
            }

            if (formatLogic != null)
            {
                formatLogic(workbook);
            }
        }

        public void FormulaEvaluator()
        {
            workbook.GetCreationHelper().CreateFormulaEvaluator().EvaluateAll();
        }

        public void AutofitColumns(string sheetName, int columnCount)
        {
            var sheetIndex = workbook.GetSheetIndex(sheetName);
            AutofitColumns(sheetIndex, columnCount);
        }

        public void AutofitColumns(int sheetIndex, int columnCount)
        {
            var sheet = this.workbook.GetSheetAt(sheetIndex);

            for (var colIndex = 0; colIndex < columnCount; colIndex++)
            {
                sheet.AutoSizeColumn(colIndex);
            }
        }

        public void SaveAs(string path)
        {
            using (FileStream file = new FileStream(path, FileMode.Create))
            {
                workbook.Write(file);
            }
        }

        public void SaveAs(Stream s)
        {
            workbook.Write(s);
        }
    }

    static class Extensions
    {
        public static void SetCellValue(this DataRow row, DataColumn column, object value)
        {
            if (value == null || (value is string && String.IsNullOrWhiteSpace(value.ToString())))
            {
                row[column] = DBNull.Value;
            }
            else if (value.GetType() == column.DataType)
            {
                row[column] = value;
            }
            else
            {
                MethodInfo Convert = typeof(Convert).GetMethod("To" + column.DataType.Name, new Type[] { typeof(object) });

                row[column] = Convert.Invoke(null, new object[] { value });

            }
        }

        public static void SetCellValue(this DataRow row, int columnIndex, object value)
        {
            row.SetCellValue(row.Table.Columns[columnIndex], value);
        }

        public static void SetCellValue(this DataRow row, string columnName, object value)
        {
            row.SetCellValue(row.Table.Columns[columnName], value);
        }

        public static void SetDataType(this DataColumn col, string excelType)
        {
            switch (excelType.ToUpper())
            {
                case "NUMERIC":
                    col.DataType = typeof(double);
                    break;
                case "DATE":
                    col.DataType = typeof(DateTime);
                    break;
                case "BOOLEAN":
                    col.DataType = typeof(bool);
                    break;
                default:
                    col.DataType = typeof(string);
                    break;
            }
        }

        public static void SetValue(this ICell cell, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                cell.SetCellValue("");
            }
            else if (value is string)
            {
                cell.SetCellValue((string)value);
            }
            else if (value is double || value is int || value is float || value is short || value is long)
            {
                cell.SetCellValue((double)Convert.ChangeType(value, typeof(double)));
            }
            else if (value is DateTime)
            {
                cell.SetCellValue((DateTime)Convert.ChangeType(value, typeof(DateTime)));
            }
            else if (value is bool)
            {
                cell.SetCellValue((bool)Convert.ChangeType(value, typeof(bool)));
            }
            else
            {
                // default 
                // written separately to make it run faster, most columns are assumed to be string
                cell.SetCellValue(value.ToString());
            }
        }

        public static bool IsNullOrEmpty(this string inputString)
        {
            return string.IsNullOrEmpty(inputString);
        }

        public static string ToNullOrString(this object inputString)
        {
            return (inputString == null || inputString == DBNull.Value) ? (string)null : inputString.ToString();
        }

        public static DataTable ToDataTable<T>(this List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);

            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in Props)
            {
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name);
            }

            foreach (T item in items)
            {
                var values = new object[Props.Length];

                for (int i = 0; i < Props.Length; i++)
                {
                    //inserting property values to datatable rows
                    values[i] = Props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }

            //put a breakpoint here and check datatable
            return dataTable;
        }

        public static List<T> ConvertDataTableToList<T>(this DataTable dt)
        {
            List<T> data = new List<T>();
            foreach (DataRow row in dt.Rows)
            {
                T item = GetItem<T>(row);
                data.Add(item);
            }
            return data;
        }
        private static T GetItem<T>(DataRow dr)
        {
            Type temp = typeof(T);
            T obj = Activator.CreateInstance<T>();

            foreach (DataColumn column in dr.Table.Columns)
            {
                foreach (PropertyInfo pro in temp.GetProperties())
                {
                    if (pro.Name == column.ColumnName)
                        pro.SetValue(obj, dr[column.ColumnName], null);
                    else
                        continue;
                }
            }
            return obj;
        }


        public static DataTable ToStringDataTable<T>(this List<string> items, string tableName, string colName)
        {
            DataTable dataTable = new DataTable(tableName);

            dataTable.Columns.Add(colName);

            foreach (string item in items)
            {
                dataTable.Rows.Add(item);
            }

            //put a breakpoint here and check datatable
            return dataTable;
        }
    }
}
