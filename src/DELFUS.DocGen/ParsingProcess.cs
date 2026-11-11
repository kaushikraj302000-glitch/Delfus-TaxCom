//using Dapper;
//using DELFUS.DocGen.Service;
//using DELFUS.Entities;
//using DELFUS.Lib;
//using log4net;
//using Microsoft.Extensions.Configuration;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using SmartDocs.Entities;
//using System;
//using System.Collections.Generic;
//using System.Data.Common;
//using System.IO;
//using System.IO.Compression;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Reflection.Metadata;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Transactions;


//namespace DELFUS.DocGen
//{
//    public static class ParsingProcess
//    {
//        public static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
//        public static IConfigurationRoot Configuration { get; set; }
//        private static string DelfusTemplatePath;
//        private static string DelfusRootPath;
//        private static string DelfusRootArchivePath;
//        private static string DelfusSourcePath;
//        private static string Delfus_API;
//        private static string DelfusAMBatchPath;
//        private static string DelfusStellentPath;
//        private static string AppEnvironment;
//        private static IEnumerable<SpecialRequirement> SpecialRequirements;
//        private static Dictionary<string, ClientConsistency> ClientConsistencies;

//        static ParsingProcess()
//        {
//            Configuration = Handlers.Config();
//            DelfusTemplatePath = "LetterTemplates";
//            DelfusRootPath = Configuration.GetSection("DelfusRootPath").Value;
//            DelfusRootArchivePath = Configuration.GetSection("DelfusRootArchivePath").Value;
//            DelfusSourcePath = Configuration.GetSection("DelfusSourcePath").Value;
//            Delfus_API = Configuration.GetSection("Delfus_API").Value;
//            DelfusAMBatchPath = Configuration.GetSection("DelfusAMBatchPath").Value;
//            DelfusStellentPath = Configuration.GetSection("DelfusStellentPath").Value;
//            AppEnvironment = Configuration.GetSection("AppEnvironment").Value;
//            using (LienService lienService = new LienService())
//            {
//                SpecialRequirements = lienService.GetSpecialRequirements();
//                ClientConsistencies = lienService.GetClientConsistencies().ToDictionary(c => c.TemplateId, c => c);
//            }
//        }

//        internal static async Task Download()
//        {
//            log.Debug("Downloading Task Started");
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                string appEnvironment = AppEnvironment;
                
//                if (appEnvironment == "T")
//                    appEnvironment = "F";

//                var fileNameRegex = new Regex($@"^{appEnvironment}.*(D(?<date>\d+)\.T(?<time>\d+))$");

//                var files = Directory
//                    .EnumerateFiles(DelfusSourcePath, "*.txt")
//                    .Select(f => new { file = f, match = fileNameRegex.Match(Path.GetFileNameWithoutExtension(f)) })
//                    .Where(f => f.match.Success);

//                log.Info($"Found Total {files.Count()} Names :{String.Join(",", files.Select(f => Path.GetFileName(f.file)))}");

//                foreach (var file in files)
//                {
//                    try
//                    {
//                        log.Info("File downloading started : {0}".Fill(Path.GetFileNameWithoutExtension(file.file)));

//                        string batchName = Path.GetFileNameWithoutExtension(file.file);

//                        Batch batch = new Batch()
//                        {
//                            BatchName = batchName,
//                            FileDate = $"{file.match.Groups["date"]} {file.match.Groups["time"]}".AnyFormatToDateTime().Value,
//                            FileType = batchName.Contains("PDFA") ? BatchFileType.Pdf_A : BatchFileType.Regular,
//                            BatchStatus = BatchStatus.InProgress.ToString()
//                        };
//                        batch.Id = await lienService.InsertBatchInfo(batch);

//                        BatchInfo batchInfo = new BatchInfo(batch.Id, DelfusRootPath, batch.BatchName);

//                        string batchPath = batchInfo.RequestPath;

//                        Directory.CreateDirectory(batchPath);

//                        string batchFilePath = Path.Combine(batchPath, $"{batchInfo.BatchName}.txt");

//                        File.Copy(file.file, batchFilePath, true);

//                        if (File.Exists(batchFilePath))
//                        {
//                            File.Move(file.file, Path.Combine(DelfusRootArchivePath, Path.GetFileName(file.file)), true);
//                        }

//                        await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Downloaded);
//                        log.Info($"File downloading completed : {Path.GetFileNameWithoutExtension(file.file)}");
//                    }
//                    catch (Exception ex)
//                    {
//                        log.Error($"Error occured while downloading Batch Name {Path.GetFileNameWithoutExtension(file.file)}", ex);
//                    }
//                }
//            }
//            log.Debug("Downloading Task Completed");
//        }

//        internal static async Task Parse()
//        {
//            log.Debug("Parsing Task Started");
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                try
//                {
//                    var batches = await lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Downloaded);

//                    var lienRecordSchema = await commonService.GetParsingSchema("L");
//                    var billRecordSchema = await commonService.GetParsingSchema("B");

//                    foreach (var batch in batches)
//                    {
//                        await lienService.InsertBatchData(batch.Id, DelfusRootPath, batch.BatchName, lienRecordSchema, billRecordSchema);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    log.Error("Error occured while parsing batches ", ex);
//                }
//            }
//            log.Debug("Parsing Task Completed");
//        }

//        internal static async Task Validate()
//        {
//            log.Debug("Validation Task Started");
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                List<Batch> batches = (await lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Parsed)).AsList();

//                foreach (var batch in batches)
//                {
//                    try
//                    {
//                        var docTypeUpdate = await lienService.UpdateLienRecordsDocumentType(batch.Id);

//                        var logoFileUpdate = await lienService.UpdateLienRecordsLogoFile(batch.Id);

//                        var invalidDocTypes = await lienService.GetLienIdsForInvalidDocTypes(batch.Id);

//                        var invalidLogoFiles = await lienService.GetLienIdsForInvalidLogoFiles(batch.Id);

//                        await lienService.UpdateBillRecordsWithoutLienRecords(batch.Id);

//                        var lienData = await lienService.GetLienDataByBatchId(batch.Id);

//                        foreach (var lienInfo in lienData)
//                        {
//                            try
//                            {
//                                var errors = new List<string>();

//                                foreach (var error in ValidateLienRecord(lienInfo, invalidDocTypes.ToHashSet<int>(), invalidLogoFiles.ToHashSet<int>()))
//                                {
//                                    errors.Add(error);
//                                }

//                                if (errors.Count == 0)
//                                {
//                                    await commonService.UpdateLienRecordStatus(lienInfo.Id, RecordStatus.Qualified);
//                                }
//                                else
//                                {
//                                    await commonService.UpdateLienDisqualifyReason(lienInfo.Id, String.Join(" | ", errors));
//                                }
//                            }
//                            catch (Exception ex)
//                            {
//                                log.Error($"Error occured while processing Lien Record {lienInfo.Id}", ex);
//                            }
//                        }

//                        var batchQualifiedCounts = await lienService.GetLienQualifiedCountsByBatchId(batch.Id);
//                        var totalRecordCount = batchQualifiedCounts.TotalCounts;
//                        var qualifiedRecordCount = batchQualifiedCounts.QualifedCount;
//                        var disqualifiedRecordCount = batchQualifiedCounts.DisqualifedCount;
//                        var inProgressCount = batchQualifiedCounts.InProgressCount;

//                        if (inProgressCount > 0)
//                        {
//                            continue;
//                        }

//                        if (totalRecordCount == qualifiedRecordCount)
//                        {
//                            await commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Qualified);
//                        }
//                        else if (qualifiedRecordCount > 0 && qualifiedRecordCount < totalRecordCount)
//                        {
//                            await commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.PartiallyQualified);
//                        }
//                        else
//                        {
//                            await commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Disqualified, "All letters are disqualified due to some invalid configurations or inputs.");
//                        }

//                        await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Validated);
//                    }
//                    catch (Exception ex)
//                    {
//                        log.Error($"Error occured while validating batch {batch.Id}", ex);
//                    }
//                }
//            }
//            log.Debug("Validation Task Completed");
//        }

//        private static IEnumerable<string> ValidateLienRecord(LienData lienInfo, HashSet<int> invalidDocTypes, HashSet<int> invalidLogoFiles)
//        {
//            string templateId = $"{lienInfo.RD_CUST_ID}{lienInfo.RD_TMPLT_CD}.docx";

//            if (!File.Exists(Path.Combine(DelfusTemplatePath, templateId)))
//            {
//                yield return DisqualificationReasons.TemplateNotFound;
//            }

//            if (lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.Email)
//            {
//                yield return DisqualificationReasons.InvalidDeliveryMethod;
//            }

//            if (invalidDocTypes.Contains(lienInfo.Id))
//            {
//                yield return DisqualificationReasons.DoctypeNotConfigured;
//            }
//            if (invalidLogoFiles.Contains(lienInfo.Id))
//            {
//                yield return DisqualificationReasons.LogoNotConfigured;
//            }

//            if (lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.BorrBulk || lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.Bulk || lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.BulkEmail)
//            {
//                if (lienInfo.RD_DLVR_NM.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_ADDR1_TXT.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_CITY_NM.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_STATE_ABBR.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_ZIP_CD.IsNullOrWhiteSpace())
//                {
//                    yield return DisqualificationReasons.InvalidBulkMailAdd;
//                }
//            }
//            yield break;
//        }

//        internal static async Task Acknowledge()
//        {
//            log.Debug("Acknowledgement Task Started");
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                List<Batch> batches = (await lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Validated)).AsList();

//                foreach (var batch in batches)
//                {
//                    try
//                    {
//                        BatchInfo batchInfo = new BatchInfo(batch.Id, DelfusRootPath, batch.BatchName);

//                        var lienData = (await lienService.GetBatchDataByBatchId(batch.Id)).AsList().Distinct().ToList();

//                        var qualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.Qualified).ToList();
//                        var disqualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.Disqualified).ToList();

//                        string disqualifiedReportFileName = "Error-{0}.txt".Fill(Common.Now.ToString("yyyyMMdd-HHmmss"));
//                        string disqualifiedReportPath = Path.Combine(batchInfo.RequestPath, disqualifiedReportFileName);
//                        var disqualifiedReport = GenerateDisqualifiedReport(disqualifiedRecords);

//                        if (disqualifiedReport != null)
//                        {
//                            File.WriteAllText(disqualifiedReportPath, disqualifiedReport.ToString());
//                        }
//                        else
//                            disqualifiedReportPath = "";

//                        if (batch.ValidationStatus == BatchValidationStatus.Disqualified)
//                        {
//                            //Send Email Notification
//                            // Batch status Report Prepped
//                            await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Rejected);
//                        }
//                        else if (batch.ValidationStatus == BatchValidationStatus.PartiallyQualified)
//                        {
//                            //Send Email Notification with disqualified record Report
//                            // Batch status Report Prepped
//                            await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Acknowledged);
//                        }
//                        else
//                        {
//                            //Send Email Notification with counts
//                            await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Acknowledged);
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        log.Error($"Error occured while acknowledging batch {batch.Id}", ex);
//                    }
//                }
//            }
//            log.Debug("Acknowledgement Task Completed");
//        }

//        private static StringBuilder GenerateDisqualifiedReport(List<LienData> lienRecords)
//        {
//            if (lienRecords.Count == 0)
//            {
//                return null;
//            }

//            var columnName = String.Join(",", "BatchName", "OrderNumber", "Customer-Id", "Loan-Id", "TemplateCode", "Sub-App-Code", "Disqualification Reason");

//            StringBuilder errorReport = new StringBuilder(columnName);

//            errorReport.AppendLine();
//            foreach (var lienData in lienRecords)
//            {
//                var line = String.Join(",", lienData.BatchInfo.BatchName, lienData.RD_CNTRCT_ID_OL, lienData.RD_CUST_ID, lienData.RD_LOAN_ID, lienData.RD_TMPLT_CD, lienData.RD_SUBAPPL_CD_OL, lienData.DisqualifyReason);
//                errorReport.AppendLine(line);
//            }
//            return errorReport;
//        }

//        internal static async Task GenerateLetters()
//        {
//            log.Debug("GenerateLetters Task Started");
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                var batches = await lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Acknowledged);
//                var dbFields = await commonService.GetTemplateMergeFields();

//                DelfusRequest delfusRequest = new DelfusRequest();
//                DocGenResponse docGenResponse = new DocGenResponse();

//                foreach (var batch in batches)
//                {
//                    try
//                    {
//                        BatchInfo batchInfo = new BatchInfo(batch.Id, DelfusRootPath, batch.BatchName);

//                        Directory.CreateDirectory(batchInfo.LettersPath);

//                        var lienData = await lienService.GetLienDataByBatchId(batch.Id);

//                        var qualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.Qualified);

//                        foreach (var lienRecord in qualifiedRecords)
//                        {
//                            try
//                            {
//                                var billRecords = await lienService.GetBillDataByLienId(lienRecord.Id);
//                                //code for generating letter
//                                var templateId = $"{lienRecord.RD_CUST_ID}{lienRecord.RD_TMPLT_CD}";
//                                string templateFileName = $"{templateId}.docx";

//                                var base64Template = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(DelfusTemplatePath, templateFileName)));
//                                var specialRequirementByTemplateId = SpecialRequirements.Where(req => req.TemplateId == templateId);

//                                TransformData transform = new TransformData();
//                                transform.LoanId = "<<[LoanId]>>";
//                                foreach (var sr in specialRequirementByTemplateId)
//                                {
//                                    switch (sr.SpecialReq)
//                                    {
//                                        case SpecialReqs.MaskingLoanNoFirst3Digit:
//                                            transform.LoanId = File.ReadAllText(@"Snippets\3DigitMaskingLoanId.txt");
//                                            break;
//                                        case SpecialReqs.MaskingLoanNoLast4Digit:
//                                            transform.LoanId = File.ReadAllText(@"Snippets\AllExceptLast4DigitMaskingLoanId.txt");
//                                            break;
//                                        default:
//                                            break;
//                                    }
//                                }

//                                transform.TaxDetail = File.ReadAllText(@"Snippets\TaxDetail.txt");
//                                transform.TaxDetail2 = File.ReadAllText(@"Snippets\TaxDetail2.txt");
//                                transform.TaxId = File.ReadAllText(@"Snippets\TaxId.txt");
//                                delfusRequest.TransformData = transform;

//                                delfusRequest.DatabaseFields = dbFields.ToList();
//                                delfusRequest.Data = new DelfusData
//                                {
//                                    Borrower1stNm = lienRecord.RD_BRRW_FRST_NM,
//                                    Borrower2ndNm = lienRecord.RD_BRRW_LAST_NM,
//                                    BorrowerAddr1Txt = lienRecord.RD_BRRW_ADDR1_TXT,
//                                    BorrowerAddr2Txt = lienRecord.RD_BRRW_ADDR2_TXT,
//                                    BorrowerCityNm = lienRecord.RD_BRRW_CITY_NM,
//                                    BorrowerStateAbbr = lienRecord.RD_BRRW_STATE_ABBR,
//                                    BorrowerZIPCD = lienRecord.RD_BRRW_ZIP_CD,
//                                    LoanId = lienRecord.RD_LOAN_ID,
//                                    ParcelSitusStrNbr = lienRecord.RD_SITUS_STR_NBR,
//                                    ParcelSitusStrNm = lienRecord.RD_SITUS_STR_NM,
//                                    ParcelSitusCityNm = lienRecord.RD_SITUS_CITY_NM,
//                                    ParcelSitusStAbbr = lienRecord.RD_SITUS_STATE_ABBR,
//                                    ParcelSitusZIPCD = lienRecord.RD_SITUS_ZIP_CD,
//                                    OrderNbr = lienRecord.RD_CNTRCT_ID_OL,
//                                    LienTotalBase = lienRecord.RD_TOT_BASE_AMT,
//                                    TaxId = lienRecord.RD_TAX_ID,
//                                    AgencyNm = lienRecord.RD_AGENCY_NM,
//                                    AgencyPhnNbr = lienRecord.RD_AGCY_PHONE_TXT,
//                                    CustId = lienRecord.RD_CUST_ID,
//                                    CustNm = lienRecord.RD_CUST_NM,
//                                    TmpltId = lienRecord.RD_TMPLT_CD,
//                                    LienSearchDteTxt = lienRecord.RD_LIEN_SEARCH_DT_TXT,
//                                    AgencyNbr = lienRecord.RD_AGENCY_ID,
//                                    AgencyStr = lienRecord.RD_AGCY_ADDR1_TXT,
//                                    AgencyCityNm = lienRecord.RD_AGNCY_CITY_NM,
//                                    AgencyStAbbr = lienRecord.RD_AGCY_STATE_ABBR,
//                                    AgencyZIP = lienRecord.RD_AGCY_ZIP_CD,
//                                    LienPostingDteTxt = lienRecord.RD_DELQ_POST_DT_TXT,
//                                    BorrForeignAddr = lienRecord.RD_BRRW_FOREIGN_ADDR_TXT,
//                                    LienPostingDteUsa = lienRecord.RD_LIEN_SEARCH_DT,
//                                    LienSearchDteUsa = lienRecord.RD_LIEN_SEARCH_DT_TXT,
//                                    AgencyPayeeNm = lienRecord.RD_AGCY_PAYEE_NM,
//                                    TaxDetails = billRecords.Select(rc => new TaxDetails() { BillInstallmentNbr = rc.RD_INSTL_NBR, BillTaxAmount = rc.RD_TAX_AMT, BillTaxYear = rc.RD_TA_TAX_BILL_YR }).ToList(),
//                                    __Templates = new Templates
//                                    {
//                                        TaxId = Convert.ToBase64String(File.ReadAllBytes(@"Snippets\TaxId_Template.docx")),
//                                        TaxDetail = Convert.ToBase64String(File.ReadAllBytes(@"Snippets\TaxDetail_Template.docx")),
//                                        TaxDetail2 = Convert.ToBase64String(File.ReadAllBytes(@"Snippets\TaxDetail2_Template.docx"))
//                                    }
//                                };

//                                delfusRequest.Data.FormatTextCase(ClientConsistencies);

//                                if (batch.FileType == BatchFileType.Pdf_A)
//                                {
//                                    delfusRequest.Settings = new MergeSettings
//                                    {
//                                        MailMergeOptions = "RemoveEmptyParagraphs",
//                                        ReportEngineOptions = "RemoveEmptyParagraphs",
//                                        OutputDocType = "pdf",
//                                        OutputDocOptions = "pdfa1a",
//                                        Template = base64Template
//                                    };
//                                }
//                                else
//                                {
//                                    delfusRequest.Settings = new MergeSettings
//                                    {
//                                        MailMergeOptions = "RemoveEmptyParagraphs",
//                                        ReportEngineOptions = "RemoveEmptyParagraphs",
//                                        OutputDocType = "pdf",
//                                        Template = base64Template
//                                    };
//                                }

//                                using (var client = new HttpClient())
//                                using (var stringContent = new StringContent(JsonConvert.SerializeObject(delfusRequest), Encoding.UTF8, "application/json"))
//                                {
//                                    HttpResponseMessage httpResponse = await client.PostAsync(Delfus_API, stringContent);
//                                    var responseString = await httpResponse.Content.ReadAsStringAsync();
//                                    if (!httpResponse.IsSuccessStatusCode || httpResponse.StatusCode != HttpStatusCode.OK)
//                                    {
//                                        await commonService.UpdateLienDisqualifyReason(lienRecord.Id, responseString);
//                                        continue;
//                                    }
//                                    docGenResponse = JsonConvert.DeserializeObject<DocGenResponse>(responseString);
//                                }

//                                var pdfFileName = $"{lienRecord.Id.ToString().PadLeft(6, '0')}_{lienRecord.RD_CUST_ID}_{lienRecord.RD_LTR_PDF_DOC_ID}.pdf";
//                                string finalPdfPath = Path.Combine(batchInfo.LettersPath, pdfFileName);

//                                File.WriteAllBytes(finalPdfPath, docGenResponse.DocumentData);

//                                //update lien record status
//                                await commonService.UpdateLienRecordStatus(lienRecord.Id, RecordStatus.LetterGenerated);
//                                //update lien filename 
//                                await lienService.UpdateLienRecordPdfFileName(lienRecord.Id, pdfFileName);
//                            }
//                            catch (Exception ex)
//                            {
//                                log.Error($"Error occured while generating letter for lien Id {lienRecord.Id}", ex);
//                            }
//                        }

//                        var batchQualifiedCounts = await lienService.GetLienQualifiedCountsByBatchId(batch.Id);
//                        var letterGeneratedCount = batchQualifiedCounts.LetterGeneratedCount;
//                        var qualifiedRecordCount = batchQualifiedCounts.QualifedCount;

//                        if (qualifiedRecordCount > 0)
//                        {
//                            continue;
//                        }

//                        if (letterGeneratedCount == 0)
//                        {
//                            await commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Disqualified, "All letters are disqualified due to unexpected errors in GenerateLetters method.");

//                            await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Rejected);

//                            continue;
//                        }

//                        //update Batch status
//                        await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.LetterGenerated);
//                    }
//                    catch (Exception ex)
//                    {
//                        log.Error($"Error occured while generating letters for Batch {batch.Id}", ex);
//                    }
//                }
//            }
//            log.Debug("GenerateLetters Task Completed");
//        }

//        internal static async Task PrepareAuthoMailBatch()
//        {
//            log.Debug("PrepareAuthoMailBatch Task Started");
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                var batches = (await lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.LetterGenerated));

//                foreach (var batch in batches)
//                {
//                    try
//                    {
//                        BatchInfo batchInfo = new BatchInfo(batch.Id, DelfusRootPath, batch.BatchName);

//                        var lienData = await lienService.GetLienDataByBatchId(batch.Id);

//                        var letterGeneratedPdfs = lienData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated);

//                        var borrowerLetters = letterGeneratedPdfs.Where(rd => rd.RD_DLVR_TYP_CD == DeliveryCodes.Borr || rd.RD_DLVR_TYP_CD == DeliveryCodes.BorrBulk || rd.RD_DLVR_TYP_CD == DeliveryCodes.BorrEmail);

//                        var borrowerGNRLetters = borrowerLetters.Where(rd => rd.RD_SUBAPPL_CD_OL == "GNR").Select(mg =>
//                        {
//                            var g = new MailGroup();
//                            g.BatchId = batch.Id;
//                            g.LoanId = mg.RD_LOAN_ID;
//                            g.CustomerId = mg.RD_CUST_ID;
//                            g.DocumentType = mg.DocumentType;
//                            g.PdfFileName = mg.PdfFileName;
//                            g.LogoFileName = mg.LogoFileName;
//                            g.FirstName = mg.RD_BRRW_FRST_NM;

//                            var hideBorr2Name = SpecialRequirements.Any(r => r.TemplateId == $"{mg.RD_CUST_ID}{mg.RD_TMPLT_CD}" && r.SpecialReq == SpecialReqs.HideBorrower2Name);

//                            if (!hideBorr2Name)
//                            {
//                                g.LastName = mg.RD_BRRW_LAST_NM;
//                            }

//                            g.AddressLine1 = mg.RD_BRRW_ADDR1_TXT;
//                            g.AddressLine2 = mg.RD_BRRW_ADDR2_TXT;
//                            g.City = mg.RD_BRRW_CITY_NM;



//                            if (mg.RD_BRRW_FOREIGN_ADDR_TXT.IsNullOrWhiteSpace())
//                            {
//                                g.State = mg.RD_BRRW_STATE_ABBR;
//                                g.Zip = mg.RD_BRRW_ZIP_CD;
//                            }
//                            else
//                            {
//                                g.Country = mg.RD_BRRW_FOREIGN_ADDR_TXT;
//                            }

//                            g.GroupType = GroupTypes.Single;
//                            g.Letters = new List<LienData>() { mg };
//                            return g;
//                        });

//                        var borrowerDLQLetters = borrowerLetters.Where(rd => rd.RD_SUBAPPL_CD_OL == "DLQ")
//                            .GroupBy(l => new { l.RD_CUST_ID, l.RD_LOAN_ID })
//                            .Select(mg =>
//                            {
//                                var firstRecord = mg.First();

//                                var g = new MailGroup();
//                                g.BatchId = batch.Id;
//                                g.LoanId = mg.Key.RD_LOAN_ID;
//                                g.CustomerId = firstRecord.RD_CUST_ID;
//                                g.DocumentType = firstRecord.DocumentType;
//                                g.PdfFileName = mg.Count() == 1 ? firstRecord.PdfFileName : null;
//                                g.LogoFileName = firstRecord.LogoFileName;
//                                g.FirstName = firstRecord.RD_BRRW_FRST_NM;

//                                var hideBorr2Name = SpecialRequirements.Any(r => r.TemplateId == $"{firstRecord.RD_CUST_ID}{firstRecord.RD_TMPLT_CD}" && r.SpecialReq == SpecialReqs.HideBorrower2Name);

//                                if (!hideBorr2Name)
//                                {
//                                    g.LastName = firstRecord.RD_BRRW_LAST_NM;
//                                }

//                                g.AddressLine1 = firstRecord.RD_BRRW_ADDR1_TXT;
//                                g.AddressLine2 = firstRecord.RD_BRRW_ADDR2_TXT;
//                                g.City = firstRecord.RD_BRRW_CITY_NM;

//                                if (firstRecord.RD_BRRW_FOREIGN_ADDR_TXT.IsNullOrWhiteSpace())
//                                {
//                                    g.State = firstRecord.RD_BRRW_STATE_ABBR;
//                                    g.Zip = firstRecord.RD_BRRW_ZIP_CD;
//                                }
//                                else
//                                {
//                                    g.Country = firstRecord.RD_BRRW_FOREIGN_ADDR_TXT;
//                                }

//                                g.GroupType = GroupTypes.HouseHolding;
//                                g.Letters = mg.ToList();

//                                return g;
//                            });

//                        var bulkMailingRecords = from pdf in letterGeneratedPdfs.Where(c => c.RD_DLVR_TYP_CD == DeliveryCodes.Bulk || c.RD_DLVR_TYP_CD == DeliveryCodes.BorrBulk || c.RD_DLVR_TYP_CD == DeliveryCodes.BulkEmail)
//                                                 group pdf by new
//                                                 {
//                                                     pdf.RD_DLVR_NM,
//                                                     pdf.RD_DLVR_ADDR1_TXT,
//                                                     pdf.RD_DLVR_CITY_NM,
//                                                     pdf.RD_DLVR_STATE_ABBR,
//                                                     pdf.RD_DLVR_ZIP_CD,
//                                                     IsColor = pdf.RD_PAPER_TYP_ID.Contains("COLOR")
//                                                 } into bmr
//                                                 select new MailGroup()
//                                                 {
//                                                     BatchId = batch.Id,
//                                                     LoanId = bmr.First().RD_CUST_ID,
//                                                     CustomerId = bmr.First().RD_CUST_ID,
//                                                     DocumentType = bmr.Key.IsColor ? "BULK_COLOR" : "BULK",
//                                                     PdfFileName = null,
//                                                     LogoFileName = "NA",
//                                                     FirstName = bmr.Key.RD_DLVR_NM,
//                                                     LastName = null,
//                                                     AddressLine1 = bmr.Key.RD_DLVR_ADDR1_TXT,
//                                                     AddressLine2 = null,
//                                                     City = bmr.Key.RD_DLVR_CITY_NM,
//                                                     State = bmr.Key.RD_DLVR_STATE_ABBR,
//                                                     Zip = bmr.Key.RD_DLVR_ZIP_CD,
//                                                     Country = null,
//                                                     GroupType = GroupTypes.BulkMailing,
//                                                     Letters = bmr.ToList(),
//                                                 };

//                        await lienService.InsertMailGroupInfo(borrowerGNRLetters, batchInfo);
//                        await lienService.InsertMailGroupInfo(borrowerDLQLetters, batchInfo);
//                        await lienService.InsertMailGroupInfo(bulkMailingRecords, batchInfo);

//                        await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Prepped);
//                    }
//                    catch (Exception ex)
//                    {
//                        log.Error($"Error occured while preparing AuthoMail Batch {batch.Id}", ex);
//                    }
//                }
//            }
//            log.Debug("SubmitToAuthoMail Task Completed");
//        }

//        internal static async Task SubmitToAuthoMail()
//        {
//            log.Debug("SubmitToAuthoMail Task Started");
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                var batches = (await lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Prepped));
//                foreach (var batch in batches)
//                {
//                    try
//                    {
//                        BatchInfo batchInfo = new BatchInfo(batch.Id, DelfusRootPath, batch.BatchName);

//                        var mgData = await lienService.GetMailGroupData(batch.Id);

//                        var zipFileName = $"{batch.Id}_{batch.BatchName}.zip";
//                        var zipFilePath = Path.Combine(batchInfo.LettersPath, zipFileName);
//                        string xmlFileName = $"{batch.Id}_{batch.BatchName}.xml";
//                        var xmlFilePath = Path.Combine(batchInfo.LettersPath, xmlFileName);
//                        MailBatch mailBatch = new MailBatch()
//                        {
//                            Id = Guid.NewGuid(),
//                            ClientCode = "DELFUS",
//                            Priority = Priority.Medium
//                        };

//                        if (File.Exists(zipFilePath))
//                            File.Delete(zipFilePath);
//                        if (File.Exists(xmlFilePath))
//                            File.Delete(xmlFilePath);

//                        using (ZipArchive newFile = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
//                        {
//                            foreach (var mp in mgData)
//                            {
//                                try
//                                {
//                                    MailPiece mailPiece = new MailPiece();
//                                    mailPiece.Id = Guid.NewGuid();
//                                    mailPiece.BusinessFriendlyIdName = "LoanId";
//                                    mailPiece.BusinessFriendlyIdValue = mp.LoanId;
//                                    mailPiece.DocumentOrPackage = new DocumentOrPackage() { Name = mp.PdfFileName };
//                                    mailPiece.DocumentOrPackageType = mp.DocumentType;

//                                    mailPiece.RecipientInfo = new RecipientInfo()
//                                    {
//                                        Recipient = new Recipient()
//                                        {
//                                            FirstName = mp.FirstName,
//                                            LastName = mp.LastName
//                                        },
//                                        Address = new Address()
//                                        {
//                                            Street = mp.AddressLine1,
//                                            OptionalLine1BelowStreet = mp.AddressLine2,
//                                            City = mp.City,
//                                            State = mp.State,
//                                            Zip = mp.Zip,
//                                            Country = mp.Country
//                                        }
//                                    };

//                                    var customInfo = new List<Metadata>();

//                                    if (mp.LogoFileName != "NA")
//                                    {
//                                        customInfo.Add(new Metadata()
//                                        {
//                                            Key = "Base64_Logo",
//                                            Value = mp.LogoFileName,
//                                            DataType = DataType.String
//                                        });
//                                    }

//                                    customInfo.Add(new Metadata()
//                                    {
//                                        Key = "CustomerId",
//                                        Value = mp.CustomerId,
//                                        DataType = DataType.String
//                                    });

//                                    mailPiece.CustomInfo = customInfo;


//                                    mailBatch.MailPieces.Add(mailPiece);
//                                    var localFilePath = Path.Combine(batchInfo.LettersPath, mp.PdfFileName);
//                                    newFile.CreateEntryFromFile(localFilePath, mp.PdfFileName);
//                                }
//                                catch (Exception ex)
//                                {
//                                    log.Error($"Error occured while generating AM Batch", ex);
//                                }
//                            }

//                            //Generate xml
//                            var xmlString = mailBatch.ToXML();
//                            File.WriteAllText(xmlFilePath, xmlString);
//                            newFile.CreateEntryFromFile(xmlFilePath, Path.GetFileName(xmlFilePath));
//                        }
//                        File.Move(zipFilePath, Path.Combine(DelfusAMBatchPath, zipFileName));
//                        File.WriteAllText(Path.Combine(DelfusAMBatchPath, $"{batch.Id}_{batchInfo.BatchName}.GO"), string.Empty);
//                        //update Batch status
//                        await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToAuthoMail);
//                    }
//                    catch (Exception ex)
//                    {
//                        log.Error($"Error occured while submitting batch to Authomail for Batch {batch.Id}", ex);
//                    }
//                }
//            }
//            log.Debug("SubmitToAuthoMail Task Completed");
//        }

//        internal static async Task SubmitToStellent()
//        {
//            using (LienService lienService = new LienService())
//            using (CommonService commonService = new CommonService())
//            {
//                var batches = await lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.SubmittedToAuthoMail);
//                int batchCount = 0;
//                foreach (var batch in batches)
//                {
//                    try
//                    {
//                        batchCount++;

//                        var lienData = (await lienService.GetLienDataByBatchId(batch.Id));

//                        var letterGeneratedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated);

//                        var batchGenerated = GenerateStellentPackage(letterGeneratedRecords, batch, batchCount);

//                        if (batchGenerated)
//                            await commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToStellent);
//                    }
//                    catch (Exception ex)
//                    {
//                        log.Error($"Error occured while submitting batch to stellent {batch.Id}", ex);
//                    }
//                }
//            }
//        }

//        private static bool GenerateStellentPackage(IEnumerable<LienData> letterGeneratedRecords, Batch batch, int batchCount)
//        {
//            try
//            {
//                string returnZipFileName = string.Empty;
//                string returnFolderName = string.Empty;
//                if (batch.FileType == BatchFileType.Regular)
//                {
//                    returnFolderName = $"LEAPVEN1.{batch.Id.ToString().PadLeft(6, '0')}";
//                    returnZipFileName = $"{AppEnvironment}ASFA.ZYINT.CL.LEAPVEN1.{returnFolderName}.zip";
//                }
//                else
//                {
//                    returnFolderName = $"LEAPVEN2.{batch.Id.ToString().PadLeft(6, '0')}";
//                    returnZipFileName = $"{AppEnvironment}ASFA.ZYINT.CL.LEAPVEN2.{returnFolderName}.zip";
//                }

//                var returnTextFileName = $"{returnFolderName}.txt";

//                BatchInfo batchInfo = new BatchInfo(batch.Id, DelfusRootPath, batch.BatchName);

//                Directory.CreateDirectory(batchInfo.StellentPath);

//                var returnTextFilePath = Path.Combine(batchInfo.StellentPath, returnTextFileName);

//                var destFilePath = Path.Combine(batchInfo.StellentPath, returnFolderName);
//                Directory.CreateDirectory(Path.Combine(batchInfo.StellentPath, returnFolderName));
//                var returnZipFilePath = Path.Combine(batchInfo.LettersPath, returnZipFileName);

//                StringBuilder returnFileData = new StringBuilder();

//                foreach (var lien in letterGeneratedRecords)
//                {
//                    var record = String.Join('|', $@"C:\IMAGE\{returnFolderName}\{lien.PdfFileName}", lien.RD_LTR_PDF_DOC_ID, lien.RD_SUBAPPL_CD_OL, lien.RD_TMPLT_CD, lien.RD_CNTRCT_ID_OL, lien.RD_TINET_SEQ_NBR_OL, lien.RD_CUST_ID, lien.RD_LOAN_ID, lien.RD_TAX_ID, lien.RD_AGENCY_ID, DateTime.Now.ToString("MM'/'dd'/'yyyy"), lien.RD_PRFL_ID_OL);

//                    var localFilePath = Path.Combine(batchInfo.LettersPath, lien.PdfFileName);
//                    File.Copy(localFilePath, Path.Combine(destFilePath, lien.PdfFileName));

//                    returnFileData.AppendLine($"{record}|");
//                }
//                File.WriteAllText(returnTextFilePath, returnFileData.ToString());

//                ZipFile.CreateFromDirectory(batchInfo.StellentPath, returnZipFilePath);

//                Directory.Delete(batchInfo.StellentPath, true);

//                File.Copy(returnZipFilePath, Path.Combine(DelfusStellentPath, Path.GetFileName(returnZipFilePath)));

//                return true;
//            }
//            catch (Exception ex)
//            {
//                log.Error($"Error occured while generating stellent batch {batch.Id}", ex);
//            }

//            return false;
//        }
//    }
//}
