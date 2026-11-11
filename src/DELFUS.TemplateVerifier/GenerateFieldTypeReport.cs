using Aspose.Words;
using Aspose.Words.Fields;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.TemplateVerifier
{
    public static class FieldTypeReport
    {
        public static void GenerateFieldTypeReport()
        {
            var docList = Directory.EnumerateFiles(@"C:\Users\sagar.g\Desktop\Desktop\DELFUS\Tax Templates\IndividualChecks", "*.docx", SearchOption.AllDirectories);
            List<FieldTypes> fieldTypes = new List<FieldTypes>();
            foreach (var item in docList)
            {
                var fileName = Path.GetFileName(item);
                Document doc = new Document(item);

                var str = doc.Range.Text;
                NodeCollection fieldStarts = doc.GetChildNodes(NodeType.FieldStart, true);

                foreach (FieldStart fieldStart in fieldStarts)
                {
                    fieldTypes.Add(new FieldTypes
                    {
                        TemplateName = fileName,
                        FieldType = fieldStart.FieldType.ToString()
                    });
                }
            }

            string filePath = @"../../Report/FieldTypeReport.xls";
            XLS excel = new XLS();

            //Generate List Of Field Types
            var consolidatedFieldTypes = from c in fieldTypes
                                         group c by new
                                         {
                                             c.FieldType,
                                             c.TemplateName
                                         } into gcs
                                         select new FieldTypes()
                                         {
                                             TemplateName = gcs.Key.TemplateName,
                                             FieldType = gcs.Key.FieldType,
                                             FieldCount = gcs.ToList().Count.ToString(),
                                         };

            excel.CreateWorkSheet("FieldTypeList");
            excel.LoadData("FieldTypeList", consolidatedFieldTypes.ToList().ToDataTable(), false);

            //Generate Unique Field Type List
            var uniqueFieldsTypes = consolidatedFieldTypes.Select(f => f.FieldType).Distinct().ToList();
            excel.CreateWorkSheet("UniqueFieldTypesList");
            excel.LoadData("UniqueFieldTypesList", uniqueFieldsTypes.ToStringDataTable<string>("UniqueFieldTypesList", "UniqueFieldTypes"), false);

            //Take Master Unique Field Types List
            XLS excelReadFieldType = new XLS(@"../../MasterFiles/KnowFieldTypes.xlsx");
            var masterFieldTypeList = excelReadFieldType.GetData("KnowFieldTypes").ConvertDataTableToList<FieldTypes>().Select(f => f.FieldType).ToList();

            //Compare Both Unique Field Types with Master Field Types List and if found generate report and add it in separate in sheet
            var notMatchingFieldTypeList = uniqueFieldsTypes.Except(masterFieldTypeList).ToList();
            if (notMatchingFieldTypeList.Count > 0)
            {
                excel.CreateWorkSheet("NonMatchingFieldTypeList");
                excel.LoadData("NonMatchingFieldTypeList", notMatchingFieldTypeList.ToStringDataTable<string>("NonMatchingFieldTypeList", "NonMatchingFieldType"), false);
            }


            excel.SaveAs(filePath);
        }
    }
}
