using Aspose.Words;
using Aspose.Words.Drawing;
using Dapper;
using DELFUS.TemplateImporter;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.TemplateVerifier
{
    public static class MergeFieldReport
    {
        public static void GenerateMergeFieldReport()
        {
            var docList = Directory.EnumerateFiles(@"C:\Users\sagar.g\Desktop\Desktop\DELFUS\Tax Templates", "*.docx", SearchOption.AllDirectories);
            List<MergeFieldInfo> mergeFieldList = new List<MergeFieldInfo>();
            List<string> incorrectAnchorSetting = new List<string>();
            foreach (var item in docList)
            {
                var fileName = Path.GetFileName(item);
                Document doc = new Document(item);
                var mergeFields = doc.MailMerge.GetFieldNames();
                foreach (var field in mergeFields)
                {
                    mergeFieldList.Add(new MergeFieldInfo() { TemplateName = fileName, MergeFieldName = field });
                }

                foreach (Shape shape in doc.GetChildNodes(NodeType.Shape, true))
                {
                    if (shape.NodeType == NodeType.Shape)
                    {
                        if (shape.ShapeType == ShapeType.TextBox)
                        {
                            if (shape.GetText().Contains("Cut and return with Proof of Tax Payment"))
                            {
                                if (shape.RelativeVerticalPosition == RelativeVerticalPosition.Paragraph)
                                {
                                    incorrectAnchorSetting.Add(fileName);
                                }
                            }
                        }
                    }
                }
            }

            string filePath = @"../../Report/MergeFieldReport.xls";
            XLS excel = new XLS();
            excel.CreateWorkSheet("MergeFieldList");
            excel.LoadData("MergeFieldList", mergeFieldList.ToDataTable(), false);

            //Generate Unique Merge Field List
            var uniqueMergeFields = mergeFieldList.Select(f => f.MergeFieldName).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
            excel.CreateWorkSheet("UniqueMergeFieldList");
            excel.LoadData("UniqueMergeFieldList", uniqueMergeFields.ToStringDataTable<string>("UniqueMergeFieldList", "UniqueMergeField"), false);

            //Take Master Unique Merge Field List
            List<string> mastermergeList;
            using (IDbConnection db = new SqlConnection(ConfigurationManager.ConnectionStrings["DELFUS"].ConnectionString))
            {
                mastermergeList = db.Query<string>("SELECT TemplateFieldMapName FROM [Schema].[Request] WHERE RecordType='L' AND TemplateFieldMapName IS NOT NULL").ToList();
            }

            //Compare Both Unique Merge Field with Master Merge Field List and if found generate report and add it in separate in sheet
            var notMatchingMergeFieldList = uniqueMergeFields.Except(mastermergeList, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (notMatchingMergeFieldList.Count > 0)
            {
                excel.CreateWorkSheet("NonMatchingMergeFieldList");
                excel.LoadData("NonMatchingMergeFieldList", notMatchingMergeFieldList.ToStringDataTable<string>("NonMatchingMergeFieldList", "NonMatchingMergeField"), false);

                var listOfMissingFieldTemplates = mergeFieldList.Where(fld => notMatchingMergeFieldList.Contains(fld.MergeFieldName, StringComparer.CurrentCultureIgnoreCase)).ToList();

                excel.CreateWorkSheet("ListOfMissingFieldTemplates");
                excel.LoadData("ListOfMissingFieldTemplates", listOfMissingFieldTemplates.ToDataTable(), false);

            }

            var unusedMergeFieldList = mastermergeList.Except(uniqueMergeFields, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (unusedMergeFieldList.Count > 0)
            {
                excel.CreateWorkSheet("UnusedMergeFieldList");
                excel.LoadData("UnusedMergeFieldList", unusedMergeFieldList.ToStringDataTable<string>("UnusedMergeFieldList", "UnusedMergeField"), false);
            }

            if (incorrectAnchorSetting.Count > 0)
            {
                excel.CreateWorkSheet("IncorrectAnchorTemplateList");
                excel.LoadData("IncorrectAnchorTemplateList", incorrectAnchorSetting.ToStringDataTable<string>("IncorrectAnchorTemplateList", "IncorrectAnchorTemplate"), false);
            }

            excel.SaveAs(filePath);
        }

        public static void GenerateTemplateVerifierReport()
        {
            //var l1 = Directory.EnumerateFiles(@"C:\Users\sagar.g\Desktop\Desktop\DELFUS\Tax Templates\DelfusTemplates\TEMPLATES-1st Group HIGH VOLUME", "*.docx", SearchOption.AllDirectories).ToList();
            //var l2 = Directory.EnumerateFiles(@"C:\Users\sagar.g\Desktop\Desktop\DELFUS\Tax Templates\DelfusTemplates\TEMPLATES- 2nd Group LOW VOLUME", "*.docx", SearchOption.AllDirectories).ToList();

            //l1.AddRange(l2);

            //var docList = l1;
            var docList = Directory.EnumerateFiles(@"C:\Users\sagar.g\Desktop\Desktop\DELFUS\Tax Templates\IndividualChecks", "*", SearchOption.AllDirectories).ToList();

            string filePath = @"Report/MergeFieldReport.xls";

            DataTable dtTemplateValidationReport = new DataTable();
            dtTemplateValidationReport.Columns.Add("FileName", typeof(string));
            dtTemplateValidationReport.Columns.Add("HasAnchorTagIssue", typeof(bool));
            dtTemplateValidationReport.Columns.Add("UsesAgencyAddress", typeof(bool));
            dtTemplateValidationReport.Columns.Add("MergeFields", typeof(string));

            XLS excel = new XLS();

            List<string> mastermergeList;
            using (IDbConnection db = new SqlConnection(ConfigurationManager.ConnectionStrings["DELFUS"].ConnectionString))
            {
                mastermergeList = db.Query<string>("SELECT TemplateFieldMapName FROM [Schema].[Request] WHERE RecordType IN ('L','C') AND TemplateFieldMapName IS NOT NULL").ToList();
            }

            foreach (var item in docList)
            {
                var fileName = Path.GetFileName(item);

                if (fileName.StartsWith("~"))
                    continue;

                TemplateValidation templateValidation = new TemplateValidation();
                var validationInfo = templateValidation.VerifyTemplate(File.ReadAllBytes(item), mastermergeList);

                DataRow dr = dtTemplateValidationReport.NewRow();
                dr["FileName"] = fileName;
                dr["HasAnchorTagIssue"] = validationInfo.HasAnchorTagIssue;
                dr["UsesAgencyAddress"] = validationInfo.UsesAgencyAddress;
                dr["MergeFields"] = String.Join(",", validationInfo.NewMergeFields);
                dtTemplateValidationReport.Rows.Add(dr);
            }

            excel.CreateWorkSheet("TemplateVerifierReport");
            excel.LoadData("TemplateVerifierReport", dtTemplateValidationReport, false);
            excel.SaveAs(filePath);
        }
    }
    public class MergeFieldInfo
    {
        public string TemplateName { get; set; }
        public string MergeFieldName { get; set; }
    }

    public class MasterMergeFieldInfo
    {
        public string MergeFieldName { get; set; }
        public string Comments { get; set; }
    }

    public class MasterMappingMergeFieldInfo
    {
        public string InputFileFieldName { get; set; }
        public string TemplateMappingField { get; set; }
    }

    public class FieldTypes
    {
        public string TemplateName { get; set; }
        public string FieldType { get; set; }
        public string FieldCount { get; set; }
    }

    public class StrObject
    {
        public string FieldValue { get; set; }
    }
}