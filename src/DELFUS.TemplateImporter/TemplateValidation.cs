using Aspose.Words;
using Aspose.Words.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.TemplateImporter
{
    public class TemplateValidation
    {
        public TemplateValidationInfo VerifyTemplate(byte[] template, List<string> dbMergeFields)
        {
            TemplateValidationInfo templateValidationInfo = new TemplateValidationInfo();
            try
            {
                Document doc = new Document(new MemoryStream(template));
                
                templateValidationInfo.IsValidDoc = true;
                
                #region Check Anchor Issue
                foreach (Shape shape in doc.GetChildNodes(NodeType.Shape, true))
                {
                    if (shape.NodeType == NodeType.Shape)
                    {
                        if (shape.ShapeType == ShapeType.TextBox)
                        {
                            if (shape.GetText().ToLower().Contains("cut and return"))
                            {
                                if (shape.RelativeVerticalPosition == RelativeVerticalPosition.Paragraph)
                                {
                                    templateValidationInfo.HasAnchorTagIssue = true;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Check if unique fields found

                var templatefields = doc.MailMerge.GetFieldNames();
                var newMergeFields = templatefields.Except(dbMergeFields, StringComparer.OrdinalIgnoreCase).ToList();

                templateValidationInfo.NewMergeFields = newMergeFields;

                #endregion

                templateValidationInfo.UsesAgencyAddress = false;
            }
            catch (Exception)
            {
                templateValidationInfo.IsValidDoc = false;
            }
            return templateValidationInfo;
        }
    }
}
