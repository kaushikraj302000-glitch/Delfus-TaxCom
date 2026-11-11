using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DELFUS.Lib
{
    public static class PdfUtil
    {
        public static string FlattenPDF(string srcPath, string destPath)
        {
            PdfReader.unethicalreading = true;
            PdfReader reader = new PdfReader(srcPath);
            PdfStamper stamper = new PdfStamper(reader, new FileStream(destPath, FileMode.Create));
            stamper.FormFlattening = true;
            stamper.Close();
            reader.Close();
            PdfReader.unethicalreading = false;
            return destPath;
        }

        public static void MergePDFs(params string[] pdflist)
        {
            // Make sure we have enough arguments to work with
            if (pdflist.Length < 3)
            {
                throw new ArgumentException("There should be at least three arguments to use this method.");
            }

            // Make sure we find all input files
            for (int i = 0; i < pdflist.Length - 1; i++)
            {
                var pdf = pdflist[i];

                if (!File.Exists(pdf))
                {
                    throw new ArgumentException(String.Format("File '{0}' not found.", pdf));
                }
            }

            var dest = pdflist[pdflist.Length - 1];

            if (Path.GetExtension(dest).ToLower() != ".pdf")
            {
                throw new ArgumentException("A valid destination PDF name must be specified.");
            }

            var temp = Path.Combine(Path.GetDirectoryName(dest), Path.GetRandomFileName());

            using (var ds = new FileStream(temp, FileMode.Create))
            using (var doc = new Document())
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

        public static void BookmarkedPdfFilesMerge(string outputPdf, string[] sourcePdfs)
        {
            PdfReader reader = null;
            Document document = new Document();
            PdfImportedPage page = null;
            PdfCopy pdfCpy = null;
            int n = 0;
            int totalPages = 0;
            int page_offset = 0;
            List<Dictionary<string, object>> bookmarks = new List<Dictionary<string, object>>();
            IList<Dictionary<string, object>> tempBookmarks;
            for (int i = 0; i <= sourcePdfs.GetUpperBound(0); i++)
            {
                reader = new PdfReader(sourcePdfs[i]);
                reader.ConsolidateNamedDestinations();
                n = reader.NumberOfPages;
                tempBookmarks = SimpleBookmark.GetBookmark(reader);

                if (i == 0)
                {
                    document = new iTextSharp.text.Document(reader.GetPageSizeWithRotation(1));
                    pdfCpy = new PdfCopy(document, new FileStream(outputPdf, FileMode.Create));
                    document.Open();
                    PdfReader.unethicalreading = true;
                    SimpleBookmark.ShiftPageNumbers(tempBookmarks, page_offset, null);
                    page_offset += n;
                    if (tempBookmarks != null)
                        bookmarks.AddRange(tempBookmarks);
                    totalPages = n;
                }
                else
                {
                    SimpleBookmark.ShiftPageNumbers(tempBookmarks, page_offset, null);
                    if (tempBookmarks != null)
                        bookmarks.AddRange(tempBookmarks);

                    page_offset += n;
                    totalPages += n;
                }

                for (int j = 1; j <= n; j++)
                {
                    page = pdfCpy.GetImportedPage(reader, j);
                    pdfCpy.AddPage(page);

                }
                PdfReader.unethicalreading = false;
                reader.Close();

            }
            pdfCpy.Outlines = bookmarks;
            document.Close();
        }

        public static void ShrinkAndCenterPDF(string src, string dest, float scalePercent)
        {
            float percent = scalePercent / 100.0f;
            PdfReader reader = new PdfReader(src);
            PdfStamper stamper = new PdfStamper(reader, new FileStream(dest, FileMode.Create));
            int n = reader.NumberOfPages;
            for (int p = 1; p <= n; p++)
            {
                float offsetX = (reader.GetPageSize(p).Width * (1 - percent)) / 2;
                float offsetY = (reader.GetPageSize(p).Height * (1 - percent)) / 2;
                stamper.GetUnderContent(p).SetLiteral(String.Format("\nq {0} 0 0 {0} {1} {2} cm\nq\n", percent, offsetX, offsetY));
                stamper.GetOverContent(p).SetLiteral("\nQ\nQ\n");
            }
            stamper.Close();
            reader.Close();
        }

        public static int GetPageCount(this string filePath)
        {
            using (PdfReader reader = new PdfReader(filePath))
            {
                return reader.NumberOfPages;
            }
        }

        public static float GetWidth(this PdfArray size)
        {
            return size.GetAsNumber(2).FloatValue - size.GetAsNumber(0).FloatValue;
        }

        public static float GetHeight(this PdfArray size)
        {
            return size.GetAsNumber(3).FloatValue - size.GetAsNumber(1).FloatValue;
        }

        public static void PdfFormFiller(string templatePDFPath, string filledPDFPath, Dictionary<string, string> fieldData)
        {
            using (var existingFileStream = new FileStream(templatePDFPath, FileMode.Open))
            using (var newFileStream = new FileStream(filledPDFPath, FileMode.Create))
            {
                // Open existing PDF
                var pdfReader = new PdfReader(existingFileStream);

                // PdfStamper, which will create
                var stamper = new PdfStamper(pdfReader, newFileStream);

                var form = stamper.AcroFields;
                foreach (var model in fieldData)
                {
                    form.SetField(model.Key, model.Value);
                }

                // "Flatten" the form so it wont be editable/usable anymore
                stamper.FormFlattening = true;

                // You can also specify fields to be flattened, which
                // leaves the rest of the form still be editable/usable
                // stamper.PartialFormFlattening("Address");

                stamper.Close();
                pdfReader.Close();
            }
        }
    }
}
