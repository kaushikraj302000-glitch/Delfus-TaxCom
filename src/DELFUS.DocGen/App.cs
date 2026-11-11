using Dapper;
using DELFUS.DocGen.Service;
using DELFUS.Entities;
using DELFUS.Lib;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using SmartDocs.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DELFUS.DocGen
{
    public class App
    {
        private ILogger<App> _logger;
        private IHostApplicationLifetime _appLifeTime;

        private LienService _lienService;
        private CommonService _commonService;
        private DelfusConfig _delfusConfig;
        private ClientEmailConfiguration _clientEmailConfig;
        private UnexpectedEmailConfiguration _unexpectedEmailConfig;

        private IEnumerable<SpecialRequirement> SpecialRequirements;
        private List<ClientConsistency> ClientConsistencies;
        private HashSet<string> AMClients;

        public App(IHostApplicationLifetime appLifeTime, IConfiguration config, ILogger<App> logger, LienService lienService, CommonService commonService, IOptionsMonitor<DelfusConfig> delfusConfig, IOptionsMonitor<ClientEmailConfiguration> clientEmailConfig, IOptionsMonitor<UnexpectedEmailConfiguration> unexpectedEmailConfig)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _lienService = lienService;
            _commonService = commonService;
            _delfusConfig = delfusConfig.CurrentValue;
            _clientEmailConfig = clientEmailConfig.CurrentValue;
            _unexpectedEmailConfig = unexpectedEmailConfig.CurrentValue;


            try
            {
                SpecialRequirements = _lienService.GetSpecialRequirements();
                ClientConsistencies = _lienService.GetClientConsistencies().ToList();
                AMClients = _lienService.GetAMClientList();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Unexpected error. Process aborted.");
                throw;
            }
        }

        public async Task RunAsync()
        {
            try
            {
                await Download();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Download failed");
            }

            try
            {
                await Parse();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Parsing failed");
            }
            try
            {
                await Validate();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Validation failed");
            }

            //TODO Acknowledge(Email)
            try
            {
                await GenerateLetters();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "GenerateLetters failed");
            }
            try
            {
                await PrepareAuthoMailBatch();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "PrepareAuthoMailBatch failed");
            }
            try
            {
                await SubmitToAuthoMail();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "SubmitToAuthoMail failed");
            }
            try
            {
                await Acknowledge();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Acknowledge failed");
            }
            try
            {
                await SubmitToStellent();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "SubmitToStellent failed");
            }

            _appLifeTime.StopApplication();
        }

        private async Task Download()
        {
            _logger.LogDebug("Downloading Task Started");

            string appEnvironment = _delfusConfig.AppEnvironment;

            if (appEnvironment == "T")
                appEnvironment = "F";

            var fileNameRegex = new Regex($@"^{appEnvironment}.*(D(?<date>\d+)\.T(?<time>\d+))$");

            var files = Directory
                .EnumerateFiles(_delfusConfig.DelfusSourcePath, "*.txt")
                .Select(f => new { file = f, match = fileNameRegex.Match(Path.GetFileNameWithoutExtension(f)) })
                .Where(f => f.match.Success);

            _logger.LogInformation($"Found Total {files.Count()} Names :{String.Join(",", files.Select(f => Path.GetFileName(f.file)))}");

            foreach (var file in files)
            {
                try
                {
                    _logger.LogInformation("File downloading started : {0}".Fill(Path.GetFileNameWithoutExtension(file.file)));

                    string batchName = Path.GetFileNameWithoutExtension(file.file);

                    Batch batch = new Batch()
                    {
                        BatchName = batchName,
                        FileDate = $"{file.match.Groups["date"]} {file.match.Groups["time"]}".AnyFormatToDateTime(),
                        FileType = batchName.Contains("PDFA") ? BatchFileType.Pdf_A : BatchFileType.Regular,
                        BatchStatus = BatchStatus.InProgress.ToString()
                    };
                    batch.Id = await _lienService.InsertBatchInfo(batch);

                    BatchInfo batchInfo = new BatchInfo(batch.Id, _delfusConfig.DelfusRootPath, batch.BatchName);

                    string batchPath = batchInfo.RequestPath;

                    Directory.CreateDirectory(batchPath);

                    string batchFilePath = Path.Combine(batchPath, $"{batchInfo.BatchName}.txt");

                    File.Copy(file.file, batchFilePath, true);

                    if (File.Exists(batchFilePath))
                    {
                        File.Move(file.file, Path.Combine(_delfusConfig.DelfusSourceArchivePath, Path.GetFileName(file.file)), true);
                    }

                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Downloaded);
                    _logger.LogInformation($"File downloading completed : {Path.GetFileNameWithoutExtension(file.file)}");
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while downloading Batch Name {Path.GetFileNameWithoutExtension(file.file)}");
                }
            }
            _logger.LogDebug("Downloading Task Completed");
        }

        private async Task Parse()
        {
            _logger.LogDebug("Parsing Task Started");


            var batches = await _lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Downloaded);

            var lienRecordSchema = await _commonService.GetParsingSchema("L");
            var billRecordSchema = await _commonService.GetParsingSchema("B");

            foreach (var batch in batches)
            {
                try
                {
                    await _lienService.InsertBatchData(batch.Id, _delfusConfig.DelfusRootPath, batch.BatchName, lienRecordSchema, billRecordSchema);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while parsing batches {batch.Id}");
                }
            }

            _logger.LogDebug("Parsing Task Completed");
        }

        private async Task Validate()
        {
            _logger.LogDebug("Validation Task Started");

            var batches = await _lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Parsed);

            foreach (var batch in batches)
            {
                try
                {
                    var docTypeUpdate = await _lienService.UpdateLienRecordsDocumentType(batch.Id);

                    var logoFileUpdate = await _lienService.UpdateLienRecordsLogoFile(batch.Id);

                    var invalidDocTypes = await _lienService.GetLienIdsForInvalidDocTypes(batch.Id);

                    var invalidLogoFiles = await _lienService.GetLienIdsForInvalidLogoFiles(batch.Id);

                    await _lienService.UpdateBillRecordsWithoutLienRecords(batch.Id);

                    var lienData = await _lienService.GetLienDataByBatchId(batch.Id);

                    foreach (var lienInfo in lienData)
                    {
                        try
                        {
                            var errors = new List<string>();

                            foreach (var error in ValidateLienRecord(lienInfo, invalidDocTypes.ToHashSet<int>(), invalidLogoFiles.ToHashSet<int>()))
                            {
                                errors.Add(error);
                            }

                            if (errors.Count == 0)
                            {
                                await _commonService.UpdateLienRecordStatus(lienInfo.Id, RecordStatus.Qualified);
                            }
                            else
                            {
                                await _commonService.UpdateLienDisqualifyReason(lienInfo.Id, String.Join(" | ", errors));
                            }
                        }
                        catch (Exception ex)
                        {
                            LogAndSendUnexpectedEmailNotification(ex, $"Error occured while processing Lien Record {lienInfo.Id}");
                        }
                    }

                    var batchQualifiedCounts = await _lienService.GetLienQualifiedCountsByBatchId(batch.Id);
                    var totalRecordCount = batchQualifiedCounts.TotalCounts;
                    var qualifiedRecordCount = batchQualifiedCounts.QualifedCount;
                    var disqualifiedRecordCount = batchQualifiedCounts.DisqualifedCount;
                    var inProgressCount = batchQualifiedCounts.InProgressCount;

                    if (inProgressCount > 0)
                    {
                        continue;
                    }

                    if (totalRecordCount == qualifiedRecordCount)
                    {
                        await _commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Qualified);
                    }
                    else if (qualifiedRecordCount > 0 && qualifiedRecordCount < totalRecordCount)
                    {
                        await _commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.PartiallyQualified);
                    }
                    else
                    {
                        await _commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Disqualified, "All letters are disqualified due to some invalid configurations or inputs.");
                    }

                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Validated);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while validating batch {batch.Id}");
                }
            }
            _logger.LogDebug("Validation Task Completed");
        }

        private IEnumerable<string> ValidateLienRecord(LienData lienInfo, HashSet<int> invalidDocTypes, HashSet<int> invalidLogoFiles)
        {
            string templateId = $"{lienInfo.RD_CUST_ID}{lienInfo.RD_TMPLT_CD}.docx";

            if (!File.Exists(Path.Combine(_delfusConfig.DelfusTemplatePath, templateId)))
            {
                yield return DisqualificationReasons.TemplateNotFound;
            }

            if (lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.Email)
            {
                yield return DisqualificationReasons.InvalidDeliveryMethod;
            }

            if (invalidDocTypes.Contains(lienInfo.Id))
            {
                yield return DisqualificationReasons.DoctypeNotConfigured;
            }
            if (invalidLogoFiles.Contains(lienInfo.Id))
            {
                yield return DisqualificationReasons.LogoNotConfigured;
            }

            if (lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.Agency || lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.AgencyBulk)
            {
                if (!IsZIPValid(lienInfo.RD_AGCY_ZIP_CD))
                {
                    _logger.LogInformation($"Original Agency Zip Code : {lienInfo.RD_AGCY_ZIP_CD}");
                    lienInfo.RD_AGCY_ZIP_CD = lienInfo.RD_AGCY_ZIP_CD.Length > 5 ? lienInfo.RD_AGCY_ZIP_CD.Substring(0, 5) : lienInfo.RD_AGCY_ZIP_CD;
                    _logger.LogInformation($"New Agency Zip Code : {lienInfo.RD_AGCY_ZIP_CD}");
                    if (!IsZIPValid(lienInfo.RD_AGCY_ZIP_CD))
                    {
                        yield return DisqualificationReasons.InvalidZip;
                    }
                    else
                    {
                        _lienService.UpdateLienRecordZipCode("RD_AGCY_ZIP_CD", lienInfo.RD_AGCY_ZIP_CD, lienInfo.Id);
                    }
                }
            }
            else
            {
                if (lienInfo.RD_BRRW_FOREIGN_ADDR_TXT.IsNullOrWhiteSpace())
                {
                    if (!IsZIPValid(lienInfo.RD_BRRW_ZIP_CD))
                    {
                        _logger.LogInformation($"Original Zip Code : {lienInfo.RD_BRRW_ZIP_CD}");
                        lienInfo.RD_BRRW_ZIP_CD = lienInfo.RD_BRRW_ZIP_CD.Length > 5 ? lienInfo.RD_BRRW_ZIP_CD.Substring(0, 5) : lienInfo.RD_BRRW_ZIP_CD;
                        _logger.LogInformation($"New Zip Code : {lienInfo.RD_BRRW_ZIP_CD}");
                        if (!IsZIPValid(lienInfo.RD_BRRW_ZIP_CD))
                        {
                            yield return DisqualificationReasons.InvalidZip;
                        }
                        else
                        {
                            //update zip code
                            _lienService.UpdateLienRecordZipCode("RD_BRRW_ZIP_CD", lienInfo.RD_BRRW_ZIP_CD, lienInfo.Id);
                        }
                    }
                }
            }


            if (lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.BorrBulk || lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.Bulk || lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.BulkEmail || lienInfo.RD_DLVR_TYP_CD == DeliveryCodes.AgencyBulk)
            {
                if (lienInfo.RD_DLVR_NM.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_ADDR1_TXT.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_CITY_NM.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_STATE_ABBR.IsNullOrWhiteSpace() || lienInfo.RD_DLVR_ZIP_CD.IsNullOrWhiteSpace())
                {
                    yield return DisqualificationReasons.InvalidBulkMailAdd;
                }

                if (!IsZIPValid(lienInfo.RD_DLVR_ZIP_CD))
                {
                    _logger.LogInformation($"Original Delivery Zip Code : {lienInfo.RD_DLVR_ZIP_CD}");
                    lienInfo.RD_DLVR_ZIP_CD = lienInfo.RD_DLVR_ZIP_CD.Length > 5 ? lienInfo.RD_DLVR_ZIP_CD.Substring(0, 5) : lienInfo.RD_DLVR_ZIP_CD;
                    _logger.LogInformation($"New Delivery Zip Code : {lienInfo.RD_DLVR_ZIP_CD}");
                    if (!IsZIPValid(lienInfo.RD_DLVR_ZIP_CD))
                    {
                        yield return DisqualificationReasons.InvalidZip;
                    }
                    else
                    {
                        //update zip code
                        _lienService.UpdateLienRecordZipCode("RD_DLVR_ZIP_CD", lienInfo.RD_DLVR_ZIP_CD, lienInfo.Id);
                    }
                }
            }
            yield break;
        }

        private bool IsZIPValid(string zipCode)
        {
            Regex regEx = new Regex(@"^(?(^00000(|-0000))|\d{5}(?:|[-\s]+\d{4}))$");
            var param = regEx.Match(zipCode);

            return param.Success;
        }

        private async Task GenerateLetters()
        {
            _logger.LogDebug("GenerateLetters Task Started");

            var batches = await _lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Validated);
            var dbFields = await _commonService.GetTemplateMergeFields();

            DelfusRequest delfusRequest = new DelfusRequest();
            DocGenResponse docGenResponse = new DocGenResponse();

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _delfusConfig.DelfusRootPath, batch.BatchName);

                    Directory.CreateDirectory(batchInfo.LettersPath);

                    var lienData = await _lienService.GetLienDataByBatchId(batch.Id);

                    var qualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.Qualified);

                    foreach (var lienRecord in qualifiedRecords)
                    {
                        try
                        {
                            var billRecords = await _lienService.GetBillDataByLienId(lienRecord.Id);
                            //code for generating letter
                            var templateId = $"{lienRecord.RD_CUST_ID}{lienRecord.RD_TMPLT_CD}";
                            string templateFileName = $"{templateId}.docx";

                            var base64Template = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_delfusConfig.DelfusTemplatePath, templateFileName)));
                            var specialRequirementByTemplateId = SpecialRequirements
                                .Where(req => req.CustId == lienRecord.RD_CUST_ID && (req.TemplateCode == TemplateCodes.All || req.TemplateCode == lienRecord.RD_TMPLT_CD));

                            TransformData transform = new TransformData();
                            transform.LoanId = "<<[LoanId]>>";
                            foreach (var sr in specialRequirementByTemplateId)
                            {
                                switch (sr.SpecialReq)
                                {
                                    case SpecialReqs.MaskingLoanNoFirst3Digit:
                                        transform.LoanId = File.ReadAllText(@"Snippets\3DigitMaskingLoanId.txt");
                                        break;
                                    case SpecialReqs.MaskingLoanNoLast4Digit:
                                        transform.LoanId = File.ReadAllText(@"Snippets\AllExceptLast4DigitMaskingLoanId.txt");
                                        break;
                                    default:
                                        break;
                                }
                            }

                            transform.TaxDetail = File.ReadAllText(@"Snippets\TaxDetail.txt");
                            transform.TaxDetail2 = File.ReadAllText(@"Snippets\TaxDetail2.txt");
                            transform.TaxId = File.ReadAllText(@"Snippets\TaxId.txt");
                            delfusRequest.TransformData = transform;

                            delfusRequest.DatabaseFields = dbFields.ToList();
                            delfusRequest.Data = new DelfusData
                            {
                                Borrower1stNm = lienRecord.RD_BRRW_FRST_NM,
                                Borrower2ndNm = lienRecord.RD_BRRW_LAST_NM,
                                BorrowerAddr1Txt = lienRecord.RD_BRRW_ADDR1_TXT,
                                BorrowerAddr2Txt = lienRecord.RD_BRRW_ADDR2_TXT,
                                BorrowerCityNm = lienRecord.RD_BRRW_CITY_NM,
                                BorrowerStateAbbr = lienRecord.RD_BRRW_STATE_ABBR,
                                BorrowerZIPCD = lienRecord.RD_BRRW_ZIP_CD,
                                LoanId = lienRecord.RD_LOAN_ID,
                                ParcelSitusStrNbr = lienRecord.RD_SITUS_STR_NBR,
                                ParcelSitusStrNm = lienRecord.RD_SITUS_STR_NM,
                                ParcelSitusCityNm = lienRecord.RD_SITUS_CITY_NM,
                                ParcelSitusStAbbr = lienRecord.RD_SITUS_STATE_ABBR,
                                ParcelSitusZIPCD = lienRecord.RD_SITUS_ZIP_CD,
                                OrderNbr = lienRecord.RD_CNTRCT_ID_OL,
                                LienTotalBase = lienRecord.RD_TOT_BASE_AMT,
                                TaxId = lienRecord.RD_TAX_ID,
                                AgencyNm = lienRecord.RD_AGENCY_NM,
                                AgencyPhnNbr = lienRecord.RD_AGCY_PHONE_TXT,
                                CustId = lienRecord.RD_CUST_ID,
                                CustNm = lienRecord.RD_CUST_NM,
                                TmpltId = lienRecord.RD_TMPLT_CD,
                                LienSearchDteTxt = lienRecord.RD_LIEN_SEARCH_DT_TXT,
                                AgencyNbr = lienRecord.RD_AGENCY_ID,
                                AgencyStr = lienRecord.RD_AGCY_ADDR1_TXT,
                                AgencyCityNm = lienRecord.RD_AGNCY_CITY_NM,
                                AgencyStAbbr = lienRecord.RD_AGCY_STATE_ABBR,
                                AgencyZIP = lienRecord.RD_AGCY_ZIP_CD,
                                LienPostingDteTxt = lienRecord.RD_DELQ_POST_DT_TXT,
                                BorrForeignAddr = lienRecord.RD_BRRW_FOREIGN_ADDR_TXT,
                                LienPostingDteUsa = lienRecord.RD_LIEN_SEARCH_DT,
                                LienSearchDteUsa = lienRecord.RD_LIEN_SEARCH_DT_TXT,
                                AgencyPayeeNm = lienRecord.RD_AGCY_PAYEE_NM,
                                TaxDetails = billRecords.Select(rc => new TaxDetails() { BillInstallmentNbr = rc.RD_INSTL_NBR, BillTaxAmount = rc.RD_TAX_AMT, BillTaxYear = rc.RD_TA_TAX_BILL_YR }).ToList(),
                                __Templates = new Templates
                                {
                                    TaxId = Convert.ToBase64String(File.ReadAllBytes(@"Snippets\TaxId_Template.docx")),
                                    TaxDetail = Convert.ToBase64String(File.ReadAllBytes(@"Snippets\TaxDetail_Template.docx")),
                                    TaxDetail2 = Convert.ToBase64String(File.ReadAllBytes(@"Snippets\TaxDetail2_Template.docx"))
                                }
                            };

                            delfusRequest.Data.FormatTextCase(ClientConsistencies);

                            if (batch.FileType == BatchFileType.Pdf_A)
                            {
                                delfusRequest.Settings = new MergeSettings
                                {
                                    MailMergeOptions = "RemoveEmptyParagraphs",
                                    ReportEngineOptions = "RemoveEmptyParagraphs",
                                    OutputDocType = "pdf",
                                    OutputDocOptions = "pdfa1a",
                                    Template = base64Template
                                };
                            }
                            else
                            {
                                delfusRequest.Settings = new MergeSettings
                                {
                                    MailMergeOptions = "RemoveEmptyParagraphs",
                                    ReportEngineOptions = "RemoveEmptyParagraphs",
                                    OutputDocType = "pdf",
                                    Template = base64Template
                                };
                            }

                            using (var client = new HttpClient())
                            using (var stringContent = new StringContent(JsonConvert.SerializeObject(delfusRequest), Encoding.UTF8, "application/json"))
                            {
                                HttpResponseMessage httpResponse = await client.PostAsync(_delfusConfig.Delfus_API, stringContent);
                                var responseString = await httpResponse.Content.ReadAsStringAsync();
                                if (!httpResponse.IsSuccessStatusCode || httpResponse.StatusCode != HttpStatusCode.OK)
                                {
                                    await _commonService.UpdateLienDisqualifyReason(lienRecord.Id, responseString);
                                    continue;
                                }
                                docGenResponse = JsonConvert.DeserializeObject<DocGenResponse>(responseString);
                            }

                            var pdfFileName = $"{lienRecord.Id.ToString().PadLeft(6, '0')}_{lienRecord.RD_CUST_ID}_{lienRecord.RD_LTR_PDF_DOC_ID}.pdf";
                            string finalPdfPath = Path.Combine(batchInfo.LettersPath, pdfFileName);

                            File.WriteAllBytes(finalPdfPath, docGenResponse.DocumentData);

                            //update lien record status
                            await _commonService.UpdateLienRecordStatus(lienRecord.Id, RecordStatus.LetterGenerated);
                            //update lien filename 
                            await _lienService.UpdateLienRecordPdfFileName(lienRecord.Id, pdfFileName);
                        }
                        catch (Exception ex)
                        {
                            LogAndSendUnexpectedEmailNotification(ex, $"Error occured while generating letter for lien Id {lienRecord.Id}");
                        }
                    }

                    var batchQualifiedCounts = await _lienService.GetLienQualifiedCountsByBatchId(batch.Id);
                    var letterGeneratedCount = batchQualifiedCounts.LetterGeneratedCount;
                    var qualifiedRecordCount = batchQualifiedCounts.QualifedCount;

                    if (qualifiedRecordCount > 0)
                    {
                        continue;
                    }

                    if (letterGeneratedCount == 0)
                    {
                        await _commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Disqualified, "All letters are disqualified due to unexpected errors in GenerateLetters method.");

                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Rejected);

                        continue;
                    }

                    //update Batch status
                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.LetterGenerated);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while generating letters for Batch {batch.Id}");
                }
            }
            _logger.LogDebug("GenerateLetters Task Completed");
        }

        private async Task PrepareAuthoMailBatch()
        {
            _logger.LogDebug("PrepareAuthoMailBatch Task Started");

            var batches = (await _lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.LetterGenerated));

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _delfusConfig.DelfusRootPath, batch.BatchName);

                    var lienData = await _lienService.GetLienDataByBatchId(batch.Id);

                    var letterGeneratedPdfs = lienData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated);

                    var agencyLetters = letterGeneratedPdfs.Where(rd => rd.RD_DLVR_TYP_CD == DeliveryCodes.Agency || rd.RD_DLVR_TYP_CD == DeliveryCodes.AgencyBulk);

                    var agencyGroupLetters = agencyLetters
                       .GroupBy(l => new { l.RD_SUBAPPL_CD_OL, l.RD_CUST_ID, l.RD_AGENCY_NM, l.RD_AGCY_ADDR1_TXT, l.RD_AGNCY_CITY_NM, l.RD_AGCY_STATE_ABBR, l.RD_AGCY_ZIP_CD })
                       .Select(mg =>
                       {
                           var firstRecord = mg.First();
                           var recordCount = mg.Count();
                           var g = new MailGroup();
                           g.BatchId = batch.Id;
                           g.LoanId = recordCount == 1 ? $"{firstRecord.RD_LOAN_ID}_{mg.Key.RD_CUST_ID}" : mg.Key.RD_CUST_ID;
                           g.CustomerId = mg.Key.RD_CUST_ID;
                           g.DocumentType = firstRecord.DocumentType;
                           g.PdfFileName = recordCount == 1 ? firstRecord.PdfFileName : null;
                           g.LogoFileName = firstRecord.LogoFileName;
                           g.FirstName = mg.Key.RD_AGENCY_NM;
                           g.LastName = null;
                           g.AddressLine1 = mg.Key.RD_AGCY_ADDR1_TXT;
                           g.AddressLine2 = null;
                           g.City = mg.Key.RD_AGNCY_CITY_NM;
                           g.State = mg.Key.RD_AGCY_STATE_ABBR;
                           g.Zip = mg.Key.RD_AGCY_ZIP_CD;
                           g.GroupType = recordCount == 1 ? GroupTypes.Agency : GroupTypes.AgencyGrouping;
                           g.Letters = mg.ToList();

                           return g;
                       });

                    var borrowerLetters = letterGeneratedPdfs.Where(rd => rd.RD_DLVR_TYP_CD == DeliveryCodes.Borr || rd.RD_DLVR_TYP_CD == DeliveryCodes.BorrBulk || rd.RD_DLVR_TYP_CD == DeliveryCodes.BorrEmail);

                    var borrowerGNRLetters = borrowerLetters.Where(rd => rd.RD_SUBAPPL_CD_OL == "GNR").Select(mg =>
                    {
                        var g = new MailGroup();
                        g.BatchId = batch.Id;
                        g.LoanId = $"{mg.RD_LOAN_ID}_{mg.RD_CUST_ID}";
                        g.CustomerId = mg.RD_CUST_ID;
                        g.DocumentType = mg.DocumentType;
                        g.PdfFileName = mg.PdfFileName;
                        g.LogoFileName = mg.LogoFileName;
                        g.FirstName = mg.RD_BRRW_FRST_NM;

                        var hideBorr2Name = SpecialRequirements.Any(r => r.CustId == mg.RD_CUST_ID && (r.TemplateCode == TemplateCodes.All || r.TemplateCode == mg.RD_TMPLT_CD) && r.SpecialReq == SpecialReqs.HideBorrower2Name);

                        if (!hideBorr2Name)
                        {
                            g.LastName = mg.RD_BRRW_LAST_NM;
                        }

                        g.AddressLine1 = mg.RD_BRRW_ADDR1_TXT;
                        g.AddressLine2 = mg.RD_BRRW_ADDR2_TXT;
                        g.City = mg.RD_BRRW_CITY_NM;



                        if (mg.RD_BRRW_FOREIGN_ADDR_TXT.IsNullOrWhiteSpace())
                        {
                            g.State = mg.RD_BRRW_STATE_ABBR;
                            g.Zip = mg.RD_BRRW_ZIP_CD;
                        }
                        else
                        {
                            g.Country = mg.RD_BRRW_FOREIGN_ADDR_TXT;
                        }

                        g.GroupType = GroupTypes.Single;
                        g.Letters = new List<LienData>() { mg };
                        return g;
                    });

                    var borrowerDLQLetters = borrowerLetters.Where(rd => rd.RD_SUBAPPL_CD_OL == "DLQ")
                        .GroupBy(l => new { l.RD_CUST_ID, l.RD_LOAN_ID })
                        .Select(mg =>
                        {
                            var firstRecord = mg.First();
                            var recordCount = mg.Count();
                            var g = new MailGroup();
                            g.BatchId = batch.Id;
                            g.LoanId = $"{ mg.Key.RD_LOAN_ID}_{firstRecord.RD_CUST_ID}";
                            g.CustomerId = firstRecord.RD_CUST_ID;
                            g.DocumentType = firstRecord.DocumentType;
                            g.PdfFileName = recordCount == 1 ? firstRecord.PdfFileName : null;
                            g.LogoFileName = firstRecord.LogoFileName;
                            g.FirstName = firstRecord.RD_BRRW_FRST_NM;


                            var hideBorr2Name = SpecialRequirements.Any(r => r.CustId == mg.Key.RD_CUST_ID && r.TemplateCode == TemplateCodes.All && r.SpecialReq == SpecialReqs.HideBorrower2Name);
                            if (!hideBorr2Name)
                            {
                                g.LastName = firstRecord.RD_BRRW_LAST_NM;
                            }

                            g.AddressLine1 = firstRecord.RD_BRRW_ADDR1_TXT;
                            g.AddressLine2 = firstRecord.RD_BRRW_ADDR2_TXT;
                            g.City = firstRecord.RD_BRRW_CITY_NM;

                            if (firstRecord.RD_BRRW_FOREIGN_ADDR_TXT.IsNullOrWhiteSpace())
                            {
                                g.State = firstRecord.RD_BRRW_STATE_ABBR;
                                g.Zip = firstRecord.RD_BRRW_ZIP_CD;
                            }
                            else
                            {
                                g.Country = firstRecord.RD_BRRW_FOREIGN_ADDR_TXT;
                            }

                            g.GroupType = recordCount == 1 ? GroupTypes.Single : GroupTypes.HouseHolding;
                            g.Letters = mg.ToList();

                            return g;
                        });

                    var bulkMailingRecords = from pdf in letterGeneratedPdfs.Where(c => c.RD_DLVR_TYP_CD == DeliveryCodes.Bulk || c.RD_DLVR_TYP_CD == DeliveryCodes.BorrBulk || c.RD_DLVR_TYP_CD == DeliveryCodes.BulkEmail || c.RD_DLVR_TYP_CD == DeliveryCodes.AgencyBulk)
                                             group pdf by new
                                             {
                                                 pdf.RD_DLVR_NM,
                                                 pdf.RD_DLVR_ADDR1_TXT,
                                                 pdf.RD_DLVR_CITY_NM,
                                                 pdf.RD_DLVR_STATE_ABBR,
                                                 pdf.RD_DLVR_ZIP_CD,
                                                 IsColor = pdf.RD_PAPER_TYP_ID.Contains("COLOR")
                                             } into bmr
                                             select new MailGroup()
                                             {
                                                 BatchId = batch.Id,
                                                 LoanId = bmr.Count() == 1 ? $"{bmr.First().RD_LOAN_ID}_{bmr.First().RD_CUST_ID}" : bmr.First().RD_CUST_ID,
                                                 CustomerId = bmr.First().RD_CUST_ID,
                                                 DocumentType = bmr.Key.IsColor ? "BULK_COLOR" : "BULK",
                                                 PdfFileName = null,
                                                 LogoFileName = "NA",
                                                 FirstName = bmr.Key.RD_DLVR_NM,
                                                 LastName = null,
                                                 AddressLine1 = bmr.Key.RD_DLVR_ADDR1_TXT,
                                                 AddressLine2 = null,
                                                 City = bmr.Key.RD_DLVR_CITY_NM,
                                                 State = bmr.Key.RD_DLVR_STATE_ABBR,
                                                 Zip = bmr.Key.RD_DLVR_ZIP_CD,
                                                 Country = null,
                                                 GroupType = GroupTypes.BulkMailing,
                                                 Letters = bmr.ToList(),
                                             };

                    await _lienService.InsertMailGroupInfo(agencyGroupLetters, batchInfo);
                    await _lienService.InsertMailGroupInfo(borrowerGNRLetters, batchInfo);
                    await _lienService.InsertMailGroupInfo(borrowerDLQLetters, batchInfo);
                    await _lienService.InsertMailGroupInfo(bulkMailingRecords, batchInfo);

                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Prepped);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while preparing AuthoMail Batch {batch.Id}");
                }
            }
            _logger.LogDebug("SubmitToAuthoMail Task Completed");
        }

        private async Task SubmitToAuthoMail()
        {
            _logger.LogDebug("SubmitToAuthoMail Task Started");

            var batches = (await _lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Prepped));
            foreach (var batch in batches)
            {
                try
                {
                    var mgData = await _lienService.GetMailGroupData(batch.Id);

                    var amGroupsData = mgData.GroupBy(mg => AMClients.Contains(mg.CustomerId) ? mg.CustomerId : "DELFUS");

                    foreach (var amGroup in amGroupsData)
                    {
                        GenerateAMBatch(amGroup, batch);
                    }
                    //update Batch status
                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToAuthoMail);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while submitting batch to Authomail for Batch {batch.Id}");
                }
            }
            _logger.LogDebug("SubmitToAuthoMail Task Completed");
        }

        private void GenerateAMBatch(IGrouping<string, MailGroup> amGroup, Batch batch)
        {
            BatchInfo batchInfo = new BatchInfo(batch.Id, _delfusConfig.DelfusRootPath, batch.BatchName);

            var zipFileName = $"{batch.Id}_{amGroup.Key}_{batch.BatchName}.zip";
            var zipFilePath = Path.Combine(batchInfo.LettersPath, zipFileName);
            string xmlFileName = $"{batch.Id}_{amGroup.Key}_{batch.BatchName}.xml";
            var xmlFilePath = Path.Combine(batchInfo.LettersPath, xmlFileName);
            MailBatch mailBatch = new MailBatch()
            {
                Id = Guid.NewGuid(),
                ClientCode = amGroup.Key == "DELFUS" ? amGroup.Key : $"DELFUS_{amGroup.Key}",
                Priority = Priority.Medium
            };

            if (File.Exists(zipFilePath))
                File.Delete(zipFilePath);
            if (File.Exists(xmlFilePath))
                File.Delete(xmlFilePath);

            using (ZipArchive newFile = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                foreach (var mp in amGroup)
                {
                    try
                    {
                        MailPiece mailPiece = new MailPiece();
                        mailPiece.Id = Guid.NewGuid();
                        mailPiece.BusinessFriendlyIdName = "LoanId";
                        mailPiece.BusinessFriendlyIdValue = mp.LoanId;
                        mailPiece.DocumentOrPackage = new DocumentOrPackage() { Name = mp.PdfFileName };
                        mailPiece.DocumentOrPackageType = mp.DocumentType;

                        mailPiece.RecipientInfo = new RecipientInfo()
                        {
                            Recipient = new Recipient()
                            {
                                FirstName = mp.FirstName,
                                LastName = mp.LastName
                            },
                            Address = new Address()
                            {
                                Street = mp.AddressLine1,
                                OptionalLine1BelowStreet = mp.AddressLine2,
                                City = mp.City,
                                State = mp.State,
                                Zip = mp.Zip,
                                Country = mp.Country
                            }
                        };

                        var customInfo = new List<Metadata>();

                        if (mp.LogoFileName != "NA")
                        {
                            customInfo.Add(new Metadata()
                            {
                                Key = "Base64_Logo",
                                Value = mp.LogoFileName,
                                DataType = DataType.String
                            });
                        }

                        customInfo.Add(new Metadata()
                        {
                            Key = "CustomerId",
                            Value = mp.CustomerId,
                            DataType = DataType.String
                        });

                        mailPiece.CustomInfo = customInfo;


                        mailBatch.MailPieces.Add(mailPiece);
                        var localFilePath = Path.Combine(batchInfo.LettersPath, mp.PdfFileName);
                        newFile.CreateEntryFromFile(localFilePath, mp.PdfFileName);
                    }
                    catch (Exception ex)
                    {
                        LogAndSendUnexpectedEmailNotification(ex, $"Error occured while generating AM Batch");
                    }
                }

                //Generate xml
                var xmlString = mailBatch.ToXML();
                File.WriteAllText(xmlFilePath, xmlString);
                newFile.CreateEntryFromFile(xmlFilePath, Path.GetFileName(xmlFilePath));
            }
            File.Move(zipFilePath, Path.Combine(_delfusConfig.DelfusAMBatchPath, zipFileName));
            File.WriteAllText(Path.Combine(_delfusConfig.DelfusAMBatchPath, $"{batch.Id}_{amGroup.Key}_{batch.BatchName}.GO"), string.Empty);
        }

        private async Task Acknowledge()
        {
            _logger.LogDebug("Acknowledgement Task Started");

            var batches = await _lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.SubmittedToAuthoMail);

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _delfusConfig.DelfusRootPath, batch.BatchName);

                    var lienData = await _lienService.GetLienDataByBatchId(batch.Id);

                    var qualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated).ToList();

                    var disqualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.Disqualified).ToList();

                    string disqualifiedReportFileName = $"ErrorReport_{batch.BatchName}.csv";

                    string disqualifiedReportPath = Path.Combine(batchInfo.RequestPath, disqualifiedReportFileName);

                    var disqualifiedReport = GenerateDisqualifiedReport(batchInfo, disqualifiedRecords);

                    if (disqualifiedReport != null)
                    {
                        File.WriteAllText(disqualifiedReportPath, disqualifiedReport.ToString());
                    }
                    else
                        disqualifiedReportPath = "";

                    if (batch.ValidationStatus == BatchValidationStatus.Disqualified)
                    {
                        //Send Email Notification
                        SendAcknowledmentEmail(batch, lienData.ToList(), disqualifiedReportPath);
                        // Batch status Report Prepped
                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Rejected);
                    }
                    else if (batch.ValidationStatus == BatchValidationStatus.PartiallyQualified)
                    {
                        //Send Email Notification with disqualified record Report
                        SendAcknowledmentEmail(batch, lienData.ToList(), disqualifiedReportPath);
                        // Batch status Report Prepped
                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Acknowledged);
                    }
                    else
                    {
                        //Send Email Notification with counts
                        SendAcknowledmentEmail(batch, lienData.ToList(), "");
                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Acknowledged);
                    }
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while acknowledging batch {batch.Id}");
                }
            }
            _logger.LogDebug("Acknowledgement Task Completed");
        }

        private static StringBuilder GenerateDisqualifiedReport(BatchInfo batchInfo, List<LienData> lienRecords)
        {
            if (lienRecords.Count == 0)
            {
                return null;
            }

            var columnName = String.Join(",", "BatchName", "OrderNumber", "Customer-Id", "Loan-Id", "TemplateCode", "Sub-App-Code", "Disqualification Reason");

            StringBuilder errorReport = new StringBuilder(columnName);

            errorReport.AppendLine();
            foreach (var lienData in lienRecords)
            {
                var line = String.Join(",", batchInfo.BatchName, lienData.RD_CNTRCT_ID_OL.ToCsvString(), lienData.RD_CUST_ID.ToCsvString(), lienData.RD_LOAN_ID.ToCsvString(), lienData.RD_TMPLT_CD, lienData.RD_SUBAPPL_CD_OL, lienData.DisqualifyReason.ToCsvString());
                errorReport.AppendLine(line);
            }
            return errorReport;
        }

        private bool SendAcknowledmentEmail(Batch batchInfo, List<LienData> lienData, string errorReportPath)
        {
            try
            {
                var subject = $"{_delfusConfig.AppEnvironment} : DELFUS Batch Processed {batchInfo.BatchName}";
                string body = string.Empty;
                bool hasAttachment = false;
                if (lienData.Count > 0)
                {
                    #region Prepare email body

                    string downloadEmail_TD_SrNoStyle = "style='width:10%;border:solid windowtext 1.0pt;border-top:none;padding:0in 5.4pt 0in 5.4pt' valign='top' width='213'><p>";
                    string downloadEmail_TD_DescriptionStyle = "style='width:80%;border-top:none;border-left:none;border-bottom:solid windowtext 1.0pt;border-right:solid windowtext 1.0pt;padding:0in 5.4pt 0in 5.4pt'' valign='top' width='213'><p>";
                    string downloadEmail_TD_CountStyle = "style='width:10%;border-top:none;border-left:none;border-bottom:solid windowtext 1.0pt;border-right:solid windowtext 1.0pt;padding:0in 5.4pt 0in 5.4pt' valign='top' width='213'><p>";

                    StringBuilder sb = new StringBuilder(File.ReadAllText(_clientEmailConfig.TemplatePath));
                    sb = sb.Replace("<%FileName%>", batchInfo.BatchName);
                    sb = sb.Replace("<%yyyymmdd%>", Common.Now.UtcToEst().ToString("MM/dd/yyyy hh:mm tt"));

                    StringBuilder data = new StringBuilder();
                    data.AppendLine(string.Format("<tr> <td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_SrNoStyle, "1"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_DescriptionStyle, "TOTAL LIEN RECORDS"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td> </tr>", downloadEmail_TD_CountStyle, lienData.Count));
                    data.AppendLine(string.Format("<tr> <td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_SrNoStyle, "2"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p>  </td>", downloadEmail_TD_DescriptionStyle, "VALID LIEN RECORDS"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td> </tr>", downloadEmail_TD_CountStyle, lienData.Where(l => l.RecordStatus == RecordStatus.LetterGenerated).Count()));
                    data.AppendLine(string.Format("<tr> <td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_SrNoStyle, "3"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p>  </td>", downloadEmail_TD_DescriptionStyle, "INVALID LIEN RECORDS"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td> </tr>", downloadEmail_TD_CountStyle, lienData.Where(l => l.RecordStatus == RecordStatus.Disqualified).Count()));

                    sb = sb.Replace("<%trdata%>", data.ToString());
                    body = sb.ToString();

                    #endregion
                }
                else if (lienData.Count == 0)
                {
                    body = string.Format("No records available in the batch file {0}.", batchInfo.BatchName);
                }

                if (!errorReportPath.IsNullOrWhiteSpace())
                    hasAttachment = true;


                Common.SendEmailNotification(_clientEmailConfig.To, _clientEmailConfig.Cc, subject, body, true, hasAttachment, new string[] { errorReportPath });
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Error occured while sending achnowlegement email.");
            }

            return true;
        }

        private async Task SubmitToStellent()
        {

            var batches = await _lienService.GetBatchesByPipelineStatus(BatchPipelineStatus.Acknowledged);
            int batchCount = 0;
            foreach (var batch in batches)
            {
                try
                {
                    batchCount++;

                    var lienData = await _lienService.GetLienDataByBatchId(batch.Id);

                    var letterGeneratedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated);

                    var batchGenerated = GenerateStellentPackage(letterGeneratedRecords, batch, batchCount);

                    if (batchGenerated)
                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToStellent);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while submitting batch to stellent {batch.Id}");
                }
            }
        }

        private bool GenerateStellentPackage(IEnumerable<LienData> letterGeneratedRecords, Batch batch, int batchCount)
        {
            try
            {
                string returnZipFileName = string.Empty;
                string returnFolderName = string.Empty;
                if (batch.FileType == BatchFileType.Regular)
                {
                    returnFolderName = $"LEAPVEN1.{batch.Id.ToString().PadLeft(6, '0')}";
                    returnZipFileName = $"{_delfusConfig.AppEnvironment}ASFA.ZYINT.CL.{returnFolderName}.zip";
                }
                else
                {
                    returnFolderName = $"LEAPVEN2.{batch.Id.ToString().PadLeft(6, '0')}";
                    returnZipFileName = $"{_delfusConfig.AppEnvironment}ASFA.ZYINT.CL.{returnFolderName}.zip";
                }

                var returnTextFileName = $"{returnFolderName}.txt";

                BatchInfo batchInfo = new BatchInfo(batch.Id, _delfusConfig.DelfusRootPath, batch.BatchName);

                Directory.CreateDirectory(batchInfo.StellentPath);

                var returnTextFilePath = Path.Combine(batchInfo.StellentPath, returnTextFileName);

                var destFilePath = Path.Combine(batchInfo.StellentPath, returnFolderName);
                Directory.CreateDirectory(Path.Combine(batchInfo.StellentPath, returnFolderName));
                var returnZipFilePath = Path.Combine(batchInfo.LettersPath, returnZipFileName);

                StringBuilder returnFileData = new StringBuilder();

                foreach (var lien in letterGeneratedRecords)
                {
                    var record = String.Join('|', $@"C:\IMAGE\{returnFolderName}\{lien.PdfFileName}", lien.RD_LTR_PDF_DOC_ID, lien.RD_SUBAPPL_CD_OL, lien.RD_TMPLT_CD, lien.RD_CNTRCT_ID_OL, lien.RD_TINET_SEQ_NBR_OL, lien.RD_CUST_ID, lien.RD_LOAN_ID, lien.RD_TAX_ID, lien.RD_AGENCY_ID, Common.EstToday.ToString("MM'/'dd'/'yyyy"), lien.RD_PRFL_ID_OL);

                    var localFilePath = Path.Combine(batchInfo.LettersPath, lien.PdfFileName);
                    File.Copy(localFilePath, Path.Combine(destFilePath, lien.PdfFileName));

                    returnFileData.AppendLine($"{record}|");
                }
                File.WriteAllText(returnTextFilePath, returnFileData.ToString());

                ZipFile.CreateFromDirectory(batchInfo.StellentPath, returnZipFilePath);

                Directory.Delete(batchInfo.StellentPath, true);

                File.Copy(returnZipFilePath, Path.Combine(_delfusConfig.DelfusStellentPath, Path.GetFileName(returnZipFilePath)));

                return true;
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, $"Error occured while generating stellent batch {batch.Id}");
            }

            return false;
        }

        private void LogAndSendUnexpectedEmailNotification(Exception exception, string errorMsg)
        {
            _logger.LogError(exception, errorMsg);

            var msg = $"{errorMsg} < br/> {exception}";

            Common.SendEmailNotification(_unexpectedEmailConfig.To, _unexpectedEmailConfig.Cc, _unexpectedEmailConfig.Subject, msg, true, false);
        }
    }
}
