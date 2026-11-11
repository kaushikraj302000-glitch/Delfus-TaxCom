using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SmartDocs.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TaxCommercial.DocGen
{
    public class App
    {
        private ILogger<App> _logger;
        private IHostApplicationLifetime _appLifeTime;

        private ClientEmailConfiguration _clientEmailConfig;
        private UnexpectedEmailConfiguration _unexpectedEmailConfig;

        private CommonService _commonService;
        private TaxComService _taxcomService;
        private TaxComConfig _taxcomConfig;
        private CLPConfig _clpConfing;

        public App(IHostApplicationLifetime appLifeTime, IConfiguration config, ILogger<App> logger, CommonService commonService, TaxComService taxcomService, IOptionsMonitor<TaxComConfig> taxcomConfig, IOptionsMonitor<CLPConfig> clpConfig, IOptionsMonitor<ClientEmailConfiguration> clientEmailConfig, IOptionsMonitor<UnexpectedEmailConfiguration> unexpectedEmailConfig)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _commonService = commonService;
            _taxcomService = taxcomService;
            _taxcomConfig = taxcomConfig.CurrentValue;
            _clpConfing = clpConfig.CurrentValue;
            _clientEmailConfig = clientEmailConfig.CurrentValue;
            _unexpectedEmailConfig = unexpectedEmailConfig.CurrentValue;
        }

        public async Task RunAsync()
        {
            //try
            //{
            //    await Download();
            //}
            //catch (Exception ex)
            //{
            //    LogAndSendUnexpectedEmailNotification(ex, "Download failed");
            //}

            try
            {
                await DownloadFileFromGCPBucket();
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

            //MOVE Letters to below Path 
            //\\CETQN1VPSHFIL01.infosolco.net\Group\FA_APPS\FATV\PROCUREMENT\NON ESCROW\DLS\DLS Letters Archive - New
            try
            {
                await SubmitToTaxCommercial();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "SubmitToTaxCommercial failed");
            }

            try
            {
                await SubmitToTaxCommercialGCP();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "SubmitToTaxCommercialGCP failed");
            }


            try
            {
                // To check for Blocked, Invalid, Bounced emails
                await CheckEdeliverySuppressionsStatus();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Checking EDelivery Suppressions Status failed");
            }

            try
            {
                await GetEdeliveryStatus();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Checking EDelivery Status failed");
            }


            _appLifeTime.StopApplication();
        }

        public async Task RunStatusCheckAsync()
        {
            try
            {
                // To check for Blocked, Invalid, Bounced emails
                await CheckEdeliverySuppressionsStatus();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Checking EDelivery Suppressions Status failed");
            }

            try
            {
                await GetEdeliveryStatus();
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Checking EDelivery Status failed");
            }


            _appLifeTime.StopApplication();
        }

        private async Task Download()
        {
            _logger.LogDebug("Downloading Task Started");

            string appEnvironment = _taxcomConfig.AppEnvironment;

            if (appEnvironment == "T")
                appEnvironment = "F";

            var fileNameRegex = new Regex($@"^{appEnvironment}.*(D(?<date>\d+)\.T(?<time>\d+))$");

            var files = Directory
                .EnumerateFiles(_taxcomConfig.TaxComSourcePath, "*.txt")
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
                        FileType = batchName.Contains("PDF_F") ? BatchFileType.Pdf_FedEx : batchName.Contains("PDF_P") ? BatchFileType.Pdf_Print : BatchFileType.PdfOnly,
                        BatchStatus = BatchStatus.InProgress.ToString()
                    };
                    batch.Id = await _taxcomService.InsertBatchInfo(batch);

                    BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);

                    string batchPath = batchInfo.RequestPath;

                    Directory.CreateDirectory(batchPath);

                    string batchFilePath = Path.Combine(batchPath, $"{batchInfo.BatchName}.txt");

                    File.Copy(file.file, batchFilePath, true);

                    if (File.Exists(batchFilePath))
                    {
                        File.Move(file.file, Path.Combine(_taxcomConfig.TaxComInputArchivePath, Path.GetFileName(file.file)), true);
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

        private async Task DownloadFileFromGCPBucket()
        {
            _logger.LogDebug("Downloading Task Started");
            try
            {
                string appEnvironment = _taxcomConfig.AppEnvironment;

                if (appEnvironment == "T")
                    appEnvironment = "F";
                var storageClient = await CreateGCSClientConnection(appEnvironment);

                var fileNameRegex = new Regex($@"^{appEnvironment}.*(D(?<date>\d+)\.T(?<time>\d+)).(?i)TXT$");

                // List objects
                foreach (var obj in storageClient.ListObjects(_taxcomConfig.GoogleBucketName))
                {
                    var file = fileNameRegex.Match(obj.Name);

                    if (file.Success)
                    {
                        string batchName = obj.Name.Substring(0, obj.Name.LastIndexOf("."));
                        // Download file
                        Batch batch = new Batch()
                        {
                            BatchName = batchName,
                            FileDate = $"{file.Groups["date"]} {file.Groups["time"]}".AnyFormatToDateTime(),
                            FileType = batchName.Contains("PDF_F") ? BatchFileType.Pdf_FedEx : batchName.Contains("PDF_P") ? BatchFileType.Pdf_Print : BatchFileType.PdfOnly,
                            BatchStatus = BatchStatus.InProgress.ToString()
                        };
                        batch.Id = await _taxcomService.InsertBatchInfo(batch);

                        BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);

                        string batchPath = batchInfo.RequestPath;

                        Directory.CreateDirectory(batchPath);

                        string batchFilePath = Path.Combine(batchPath, $"{batchInfo.BatchName}.txt");

                        using (var fileStream = File.Create(batchFilePath))
                        {
                            storageClient.DownloadObject(_taxcomConfig.GoogleBucketName, obj.Name, fileStream);
                        }

                        //TaxCom team Informed Us To DELETE the File From The Bucket After We Downloaded.
                        // GCP does not provide option to move the files, so after copying the file to archive bucket delete the file from the source bucket.

                        //storageClient.CopyObject(_taxcomConfig.GoogleBucketName, obj.Name, _taxcomConfig.GoogleArchiveBucketName, obj.Name);
                        storageClient.DeleteObject(_taxcomConfig.GoogleBucketName, obj.Name);

                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Downloaded);
                        _logger.LogInformation($"File downloading completed : {Path.GetFileNameWithoutExtension(batchFilePath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Error occured while downloading Batch.");
            }

            _logger.LogDebug("Downloading Task Completed");
        }

        private async Task Parse()
        {
            _logger.LogDebug("Parsing Task Started");


            var batches = await _taxcomService.GetBatchesByPipelineStatus(BatchPipelineStatus.Downloaded);

            var lienRecordSchema = await _commonService.GetParsingSchema("L");
            var billRecordSchema = await _commonService.GetParsingSchema("B");

            foreach (var batch in batches)
            {
                try
                {
                    await _taxcomService.InsertBatchData(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName, lienRecordSchema, billRecordSchema);
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

            var batches = await _taxcomService.GetBatchesByPipelineStatus(BatchPipelineStatus.Parsed);

            foreach (var batch in batches)
            {
                try
                {
                    var docTypeUpdate = await _taxcomService.UpdateLienRecordsDocumentType(batch.Id);

                    var invalidDocTypes = await _taxcomService.GetLienIdsForInvalidDocTypes(batch.Id);

                    // TODO : Not required for now
                    //var invalidLogoFiles = await _taxcomService.GetLienIdsForInvalidLogoFiles(batch.Id);

                    await _taxcomService.UpdateBillRecordsWithoutLienRecords(batch.Id);


                    bool isEmailValidationReq = batch.FileType == BatchFileType.Pdf_FedEx ? false : true;
                    if (isEmailValidationReq)
                    {
                        await _taxcomService.UpdateLienRecordsForEDelivery(batch.Id);
                    }

                    var lienData = await _taxcomService.GetLienDataByBatchId(batch.Id);

                    foreach (var lienInfo in lienData)
                    {
                        try
                        {
                            var errors = new List<string>();

                            foreach (var error in ValidateLienRecord(lienInfo, isEmailValidationReq, invalidDocTypes.ToHashSet<int>()))
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

                    var batchQualifiedCounts = await _taxcomService.GetLienQualifiedCountsByBatchId(batch.Id);
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

        private async Task GenerateLetters()
        {
            _logger.LogDebug("GenerateLetters Task Started");

            var batches = await _taxcomService.GetBatchesByPipelineStatus(BatchPipelineStatus.Validated);
            var dbFields = await _commonService.GetTemplateMergeFields();

            DelfusRequest delfusRequest = new DelfusRequest();
            DocGenResponse docGenResponse = new DocGenResponse();

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);

                    Directory.CreateDirectory(batchInfo.LettersPath);

                    var lienData = await _taxcomService.GetLienDataByBatchId(batch.Id);

                    var qualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.Qualified);

                    foreach (var lienRecord in qualifiedRecords)
                    {
                        try
                        {
                            var billRecords = await _taxcomService.GetBillDataByLienId(lienRecord.Id);
                            //code for generating letter
                            var templateId = $"{lienRecord.RD_CUST_ID}{lienRecord.RD_TMPLT_CD}";
                            string templateFileName = $"{templateId}.docx";

                            var base64Template = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_taxcomConfig.TaxComTemplatePath, templateFileName)));

                            delfusRequest.TransformData = null;

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
                                PropOwnerTxt = lienRecord.RD_CNTCT_NM,
                                LoanGroup = lienRecord.RD_CUST_LTR_CD,
                                TaxDetails = billRecords.Select(rc => new TaxDetails()
                                {
                                    BillTypeDesc = rc.RD_BILL_TYP_DESC,
                                    BillTaxAmount = double.Parse(rc.RD_TAX_AMT.Replace("$", string.Empty)),
                                    BillSearchDteUSA = rc.RD_BILL_SEARCH_DT,
                                    BillSearchDteYrUSA = DateTime.Parse(rc.RD_BILL_SEARCH_DT).Year.ToString(),
                                    AgencyName = lienRecord.RD_AGENCY_NM,
                                    SyiCodeDesc = rc.RD_SYI_CD_DESC
                                }).ToList(),
                            };
                            delfusRequest.Data.DueDte = delfusRequest.Data.TaxDetails[0]?.BillSearchDteUSA;
                            delfusRequest.Data.DueDteYr = delfusRequest.Data.TaxDetails[0]?.BillSearchDteYrUSA;

                            delfusRequest.Settings = new MergeSettings
                            {
                                MailMergeOptions = "RemoveEmptyParagraphs",
                                ReportEngineOptions = "RemoveEmptyParagraphs",
                                OutputDocType = "pdf",
                                Template = base64Template
                            };

                            using (var client = new HttpClient())
                            using (var stringContent = new StringContent(JsonConvert.SerializeObject(delfusRequest), Encoding.UTF8, "application/json"))
                            {
                                string docGenServerIP = Environment.GetEnvironmentVariable("SD_IP");
                                if (!docGenServerIP.IsNullOrWhiteSpace())
                                {
                                    _taxcomConfig.TaxCom_API = _taxcomConfig.TaxCom_API.Replace("{DocGenServerIP}", docGenServerIP);
                                }

                                HttpResponseMessage httpResponse = await client.PostAsync(_taxcomConfig.TaxCom_API, stringContent);
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
                            await _taxcomService.UpdateLienRecordPdfFileName(lienRecord.Id, pdfFileName);
                        }
                        catch (Exception ex)
                        {
                            LogAndSendUnexpectedEmailNotification(ex, $"Error occured while generating letter for lien Id {lienRecord.Id}");
                        }
                    }

                    var batchQualifiedCounts = await _taxcomService.GetLienQualifiedCountsByBatchId(batch.Id);
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
                    else if (letterGeneratedCount > 0 && letterGeneratedCount < batchQualifiedCounts.TotalCounts)
                    {
                        await _commonService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.PartiallyQualified);
                        //update Batch status
                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.LetterGenerated);
                    }
                    else
                    {
                        //update Batch status
                        await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.LetterGenerated);
                    }
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while generating letters for Batch {batch.Id}");
                }
            }
            _logger.LogDebug("GenerateLetters Task Completed");
        }

        private async Task SubmitToAuthoMail()
        {
            _logger.LogDebug("SubmitToAuthoMail Task Started");

            var batches = (await _taxcomService.GetBatchesByPipelineStatus(BatchPipelineStatus.LetterGenerated));
            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);
                    var lienData = await _taxcomService.GetLienDataByBatchId(batch.Id);
                    var qualifiedRecords = lienData.Where(s => s.RecordStatus == RecordStatus.LetterGenerated).ToList();
                    if (batch.FileType == BatchFileType.PdfOnly)
                    {
                        await PrepareAndSendPdfsToLenders(qualifiedRecords, batchInfo);
                    }
                    else
                    {
                        await PrepareAndSendPdfsToLenders(qualifiedRecords, batchInfo);
                        GenerateAMBatch(qualifiedRecords, batch);
                    }
                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToAuthoMail);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while submitting batch to Authomail for Batch {batch.Id}");
                }
            }
            _logger.LogDebug("SubmitToAuthoMail Task Completed");
        }

        private async Task Acknowledge()
        {
            _logger.LogDebug("Acknowledgement Task Started");

            var batches = await _taxcomService.GetBatchesByPipelineStatus(BatchPipelineStatus.SubmittedToAuthoMail);

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);

                    var lienData = await _taxcomService.GetLienDataByBatchId(batch.Id);

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

        private async Task SubmitToTaxCommercial()
        {
            _logger.LogDebug("SubmitToTaxCommercial Task Started");

            var batches = await _taxcomService.GetBatchesByPipelineStatus(BatchPipelineStatus.Acknowledged);

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);

                    var lienData = await _taxcomService.GetLienDataByBatchId(batch.Id);
                    var letterGeneratedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated);

                    List<string> groupedPdfsPath = Directory.GetFiles(batchInfo.LettersPath, "DLS*.pdf").ToList();
                    foreach (var groupedPdfPath in groupedPdfsPath)
                    {
                        string pdfName = Path.GetFileName(groupedPdfPath);
                        var destFilePath = Path.Combine(_taxcomConfig.TaxComPdfsStellentPath, pdfName.Split('_')[1].TrimStart('0'), Common.EstToday.ToString("yyyy"), Common.EstToday.ToString("MMMM"));
                        if (!Directory.Exists(destFilePath))
                            Directory.CreateDirectory(destFilePath);

                        var localFilePath = Path.Combine(batchInfo.LettersPath, pdfName);
                        File.Copy(localFilePath, Path.Combine(destFilePath, pdfName), true);
                    }

                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToStellent);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while submitting batch to Tax Commercial for Batch {batch.Id}");
                }
            }
            _logger.LogDebug("SubmitToTaxCommercial Task Completed");
        }

        private async Task SubmitToTaxCommercialGCP()
        {
            _logger.LogDebug("SubmitToTaxCommercialGCP Task Started");
            string appEnvironment = _taxcomConfig.AppEnvironment;

            if (appEnvironment == "T")
                appEnvironment = "F";
            var batches = await _taxcomService.GetBatchesByPipelineStatus(BatchPipelineStatus.SubmittedToStellent);

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);

                    var lienData = await _taxcomService.GetLienDataByBatchId(batch.Id);
                    var letterGeneratedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated);

                    List<string> generatedLettersPath = Directory.GetFiles(batchInfo.LettersPath, "*.pdf")
                        .Where(file => !file.Contains("DLS")).ToList();
                    var storageClient = await CreateGCSClientConnection(appEnvironment);
                    foreach (var generatedLetter in generatedLettersPath)
                    {
                        using (var f = File.OpenRead(generatedLetter))
                        {
                            string fileName = Path.GetFileName(generatedLetter);
                            string objectName = $"Inbound/{fileName.Split('_')[1].TrimStart('0')}/{batch.DownloadedOn.Value.Date.ToString("yyyy")}/{batch.DownloadedOn.Value.Date.ToString("MM")}/{batch.DownloadedOn.Value.Date.ToString("dd")}/{fileName}";
                            storageClient.UploadObject(_taxcomConfig.GoogleBucketName, objectName, null, f);
                        }
                    }
                    await _commonService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToStellentGCP);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while submitting batch to Tax Commercial for Batch {batch.Id}");
                }
            }
            _logger.LogDebug("SubmitToTaxCommercialGCP Task Completed");
        }

        private async Task CheckEdeliverySuppressionsStatus()
        {
            _logger.LogDebug("CheckEdeliverySuppressionsStatus Task Started");

            var liendataToCheckEDeliveryStatus = await _taxcomService.GetLienDataForEDeliveryStatus();
            int batchId = 0;
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    var token = await _taxcomService.GenerateToken(httpClient);
                    _logger.LogDebug("Token Generated");
                    // set default headers
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

                    var batchwiseDistinctEmails = liendataToCheckEDeliveryStatus.GroupBy(l => l.BatchId).ToDictionary(x => x.Key, x => x.Select(y => y.RD_DLVR_EMAIL_ADDR_TXT).Distinct().GroupBy(e => e.Count() / 10));
                    foreach (var batchwiseDistinctEmail in batchwiseDistinctEmails)
                    {
                        batchId = batchwiseDistinctEmail.Key;
                        foreach (var emailsToFetchStatus in batchwiseDistinctEmail.Value)
                        {
                            var responseResult = await _taxcomService.CheckEmailSuppressionsStatus(httpClient, string.Join(',', emailsToFetchStatus.ToArray()).Replace(" ", string.Empty));
                            if (!responseResult.IsSuccessStatusCode)
                            {
                                var msg = $"Error occured while fetching Email DeliveryStatus with Status Code : {responseResult.StatusCode},RequestMessage {responseResult.RequestMessage}, Error : {await responseResult?.Content?.ReadAsStringAsync()}";
                                _logger.LogError(msg);
                            }
                            var responseJson = await responseResult.Content.ReadAsStringAsync();
                            var emailStatuses = JsonConvert.DeserializeObject<EmailStatuses>(responseJson);
                            if (emailStatuses.Emails != null && emailStatuses.Emails.Count > 0)
                            {
                                foreach (var emailStatus in emailStatuses.Emails)
                                {
                                    await _taxcomService.UpdateLienRecordsEDeliveryStatusByBatchId(emailStatus.Status, Common.Now, batchId, emailStatus.EmailAddress);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, $"Error occured while getting email suppression status for the batch: {batchId}");
            }

            _logger.LogDebug("CheckEdeliverySuppressionsStatus Task Completed");
        }

        private async Task GetEdeliveryStatus()
        {
            _logger.LogDebug("GetEdeliveryStatus Task Started");

            var liendataToCheckEDeliveryStatus = await _taxcomService.GetLienDataForEDeliveryStatus();
            var emailIdsToCheckStatus = liendataToCheckEDeliveryStatus.Select(e => e.EmailId).Distinct().ToList();
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    var token = await _taxcomService.GenerateToken(httpClient);
                    _logger.LogDebug("Token Generated");
                    // set default headers
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

                    foreach (var emailId in emailIdsToCheckStatus)
                    {
                        var emailStatuses = await CheckEmailDeliveryStatus(emailId.ToString(), httpClient);
                        foreach (var emailStatus in emailStatuses.Emails)
                        {
                            await _taxcomService.UpdateLienRecordsEDeliveryStatus(emailStatus.EmailId, emailStatus.EmailAddress, emailStatus.Status, Common.Now);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, $"Error occured while checking email status.");
            }
            _logger.LogDebug("GetEdeliveryStatus Task Completed");
        }

        private void GenerateAMBatch(List<LienData> lienData, Batch batch)
        {
            BatchInfo batchInfo = new BatchInfo(batch.Id, _taxcomConfig.TaxComRootPath, batch.BatchName);

            var zipFileName = $"{batch.Id}_{batch.BatchName}.zip";
            var zipFilePath = Path.Combine(batchInfo.LettersPath, zipFileName);
            string xmlFileName = $"{batch.Id}_{batch.BatchName}.xml";
            var xmlFilePath = Path.Combine(batchInfo.LettersPath, xmlFileName);
            MailBatch mailBatch = new MailBatch()
            {
                Id = Guid.NewGuid(),
                ClientCode = "TAXCOM",
                Priority = Priority.Medium
            };

            if (File.Exists(zipFilePath))
                File.Delete(zipFilePath);
            if (File.Exists(xmlFilePath))
                File.Delete(xmlFilePath);

            using (ZipArchive newFile = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                foreach (var mp in lienData)
                {
                    try
                    {
                        MailPiece mailPiece = new MailPiece();
                        mailPiece.Id = Guid.NewGuid();
                        mailPiece.BusinessFriendlyIdName = "LoanId";
                        mailPiece.BusinessFriendlyIdValue = mp.RD_LOAN_ID;
                        mailPiece.DocumentOrPackage = new DocumentOrPackage() { Name = mp.PdfFileName };
                        mailPiece.DocumentOrPackageType = mp.DocumentType;

                        if (mp.RD_BRRW_FRST_NM.IsNullOrWhiteSpace() && mp.RD_BRRW_LAST_NM.IsNullOrWhiteSpace() || mp.RD_BRRW_ADDR1_TXT.IsNullOrWhiteSpace())
                        {
                            mailPiece.RecipientInfo = new RecipientInfo()
                            {
                                Recipient = new Recipient()
                                {
                                    FirstName = mp.RD_CNTCT_NM
                                },
                                Address = new Address()
                                {
                                    Street = mp.RD_SITUS_STR_NM,
                                    City = mp.RD_SITUS_CITY_NM,
                                    State = mp.RD_SITUS_STATE_ABBR,
                                    Zip = mp.RD_SITUS_ZIP_CD
                                }
                            };
                        }
                        else
                        {
                            mailPiece.RecipientInfo = new RecipientInfo()
                            {
                                Recipient = new Recipient()
                                {
                                    FirstName = mp.RD_BRRW_FRST_NM,
                                    LastName = mp.RD_BRRW_LAST_NM
                                },
                                Address = new Address()
                                {
                                    Street = mp.RD_BRRW_ADDR1_TXT,
                                    OptionalLine1BelowStreet = mp.RD_BRRW_ADDR2_TXT,
                                    City = mp.RD_BRRW_CITY_NM,
                                    State = mp.RD_BRRW_STATE_ABBR,
                                    Zip = mp.RD_BRRW_ZIP_CD
                                }
                            };
                        }

                        var customInfo = new List<Metadata>();

                        //if (mp.LogoFileName != "NA")
                        //{
                        //    customInfo.Add(new Metadata()
                        //    {
                        //        Key = "Base64_Logo",
                        //        Value = mp.LogoFileName,
                        //        DataType = DataType.String
                        //    });
                        //}

                        customInfo.Add(new Metadata()
                        {
                            Key = "CustomerId",
                            Value = mp.RD_CUST_ID,
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
                        throw;
                    }
                }

                //Generate xml
                var xmlString = mailBatch.ToXML();
                File.WriteAllText(xmlFilePath, xmlString);
                newFile.CreateEntryFromFile(xmlFilePath, Path.GetFileName(xmlFilePath));
            }
            File.Move(zipFilePath, Path.Combine(_taxcomConfig.TaxComAMBatchPath, zipFileName));
            File.WriteAllText(Path.Combine(_taxcomConfig.TaxComAMBatchPath, $"{batch.Id}_{batch.BatchName}.GO"), string.Empty);
        }


        private async Task PrepareAndSendPdfsToLenders(IEnumerable<LienData> letterGeneratedRecords, BatchInfo batch)
        {
            //TODO:: 
            // Merge pdfs ( group by RD-CUST-ID and RD-CUST-LTR-CD)
            // E-delivery (Store in tables)
            // Check email status and update record(create separate method)

            try
            {
                var groupedData = letterGeneratedRecords.GroupBy(c => new
                {
                    c.RD_CUST_ID,
                    c.RD_TMPLT_CD,
                    c.RD_CUST_LTR_CD
                });

                var groupedPdfs = new List<GroupPdfs>();

                //Merge Pdfs
                foreach (var data in groupedData)
                {
                    var custLetterCode = data.Key.RD_CUST_LTR_CD.IsNullOrWhiteSpace() ? "NOGRP" : data.Key.RD_CUST_LTR_CD;
                    var pdfFileList = data.Select(p => Path.Combine(batch.LettersPath, $"{p.Id.ToString().PadLeft(6, '0')}_{p.RD_CUST_ID}_{p.RD_LTR_PDF_DOC_ID}.pdf")).ToList();
                    string finalPdfPath = Path.Combine(batch.LettersPath, $"DLS_{data.Key.RD_CUST_ID}_{data.FirstOrDefault().RD_TMPLT_CD}_{custLetterCode}_{Common.EstToday:MM-dd-yyyy}.pdf");
                    pdfFileList.Add(finalPdfPath);
                    Extensions.MergePDFs(pdfFileList.ToArray());
                    groupedPdfs.AddRange(data.Select(c => new GroupPdfs() { CustId = data.Key.RD_CUST_ID, TemplateCode = data.FirstOrDefault().RD_TMPLT_CD, LienId = c.Id, PdfFilePath = finalPdfPath, EmailAddress = c.RD_DLVR_EMAIL_ADDR_TXT }));
                }

                var groupEmails = groupedPdfs.GroupBy(c => new { c.CustId });

                var emailList = groupEmails.Select(e => e.FirstOrDefault().EmailAddress);
                // Send email code
                using (HttpClient httpClient = new HttpClient())
                {
                    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    var token = await _taxcomService.GenerateToken(httpClient);
                    _logger.LogDebug("Token Generated");
                    foreach (var groupEmail in groupEmails)
                    {
                        //Check for error message in lien table before generating AuthoMail batch.
                        await SendEmailsToLender(groupEmail.ToList(), groupEmail.Key.CustId, httpClient, token);
                    }
                }
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, $"Error occured while preparing pdfs for the batch : {batch.BatchName}");
                throw;
            }

        }

        private async Task SendEmailsToLender(List<GroupPdfs> groupEmail, string custId, HttpClient httpClient, string token)
        {
            var emailId = groupEmail.FirstOrDefault().EmailAddress;
            var pdfs = groupEmail.Select(c => c.PdfFilePath).Distinct();
            EDeliveryRequest eDeliveryRequest = new EDeliveryRequest();

            // set default headers
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            eDeliveryRequest.To = new List<string>() { emailId };
            eDeliveryRequest.Cc = _clpConfing.CCEmailAddresses.Split(',').ToList();
            eDeliveryRequest.Bcc = new List<string>();
            eDeliveryRequest.Subject = $"DLS {custId} {groupEmail.FirstOrDefault().TemplateCode} {Common.EstToday:MM-dd-yyyy}";
            eDeliveryRequest.Body = "\r\nHello,<br/><br/>Enclosed you will find the DLS letter(s) for your use. <br/><br/>Thank you,<br/><br/><b>CoreLogic - Commercial Tax</b><br/><b>Customer Service Email:</b> ISCR-RA-TXJIRA@corelogic.onmicrosoft.com<br/><b>Customer Service Phone:</b> 888-988-0939";

            //eDeliveryRequest.Body = "Hello,<br/><br/>Enclosed you will find the DLS letter(s) for your use. <br/><br/>Thank you,<br/><br/>Halima Jarnagin<br/>Sr. Associate, Operation Svcs<br/>CoreLogic Commercial Real Estate Services<br/><br/><i>If you have any questions or comments, please call Customer Service at 888-988-0939 or send an email to facomtax_cs@corelogic.com.</i>";
            var attachments = new List<Attachment>();
            attachments.AddRange(pdfs.Select(f => new Attachment()
            {
                FileName = Path.GetFileName(f),
                Content = Convert.ToBase64String(File.ReadAllBytes(f)),
                Inline = false
            }).ToList());
            eDeliveryRequest.Attachments = attachments;

            try
            {
                HttpResponseMessage responseResult = await _taxcomService.SendAPIResponse(httpClient, eDeliveryRequest);
                if (!responseResult.IsSuccessStatusCode)
                {
                    var msg = $"Error occured while sending response to CLP endpoint with Status Code : {responseResult.StatusCode},RequestMessage {responseResult.RequestMessage}, Error : {await responseResult?.Content?.ReadAsStringAsync()}";
                    _logger.LogError(msg);
                    EDeliveryErrorResponse eDeliveryErrorResponse = JsonConvert.DeserializeObject<EDeliveryErrorResponse>(await responseResult?.Content?.ReadAsStringAsync());

                    //Return false and reject batch
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(eDeliveryErrorResponse.Message);
                    if (eDeliveryErrorResponse.Errors != null)
                    {
                        stringBuilder.AppendLine(string.Join(',', eDeliveryErrorResponse.Errors.Select(e => $"{e.Field}: {e.Message}")));
                    }

                    //update list<lienId> with error 
                    await _taxcomService.UpdateLienRecordsEDeliveryError(groupEmail.Select(i => i.LienId).ToList(), stringBuilder.ToString());
                    return;
                }
                var responseJson = await responseResult.Content.ReadAsStringAsync();
                EDeliveryResponse eDeliveryResponse = JsonConvert.DeserializeObject<EDeliveryResponse>(responseJson);
                DateTime submittedDatetime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                // update list<LienId> with EmailUniqueId and SubmittedDate               
                await _taxcomService.UpdateLienRecordsEDeliverySuccess(groupEmail.Select(i => i.LienId).ToList(), eDeliveryResponse.EmailId, submittedDatetime.AddMilliseconds(eDeliveryResponse.SubmittedDate));


                //Check and update email delivery Status
                var emailStatuses = await CheckEmailDeliveryStatus(eDeliveryResponse.EmailId, httpClient);
                foreach (var emailStatus in emailStatuses.Emails)
                {
                    await _taxcomService.UpdateLienRecordsEDeliveryStatus(emailStatus.EmailId, emailStatus.EmailAddress, emailStatus.Status, Common.Now);
                }
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, $"Error occured while sending email and checking staus for the LienIds : {string.Join(',', groupEmail.Select(i => i.LienId))} ");
            }
        }

        private async Task<EmailStatuses> CheckEmailDeliveryStatus(string emailId, HttpClient httpClient)
        {
            HttpResponseMessage responseResult = await _taxcomService.CheckEmailStatus(httpClient, emailId);
            if (!responseResult.IsSuccessStatusCode)
            {
                var msg = $"Error occured while fetching Email DeliveryStatus with Status Code : {responseResult.StatusCode},RequestMessage {responseResult.RequestMessage}, Error : {await responseResult?.Content?.ReadAsStringAsync()}";
                _logger.LogError(msg);
                return new EmailStatuses();
            }
            var responseJson = await responseResult.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<EmailStatuses>(responseJson);
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
                var subject = $"{_taxcomConfig.AppEnvironment} : TaxCommercial DLS Batch Processed {batchInfo.BatchName}";
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

        private IEnumerable<string> ValidateLienRecord(LienData lienInfo, bool isEmailValidationReq, HashSet<int> invalidDocTypes)
        {
            string templateId = $"{lienInfo.RD_CUST_ID}{lienInfo.RD_TMPLT_CD}.docx";

            if (!File.Exists(Path.Combine(_taxcomConfig.TaxComTemplatePath, templateId)))
            {
                yield return DisqualificationReasons.TemplateNotFound;
            }

            if (invalidDocTypes.Contains(lienInfo.Id))
            {
                yield return DisqualificationReasons.DoctypeNotConfigured;
            }
            //if (invalidLogoFiles.Contains(lienInfo.Id))
            //{
            //    yield return DisqualificationReasons.LogoNotConfigured;
            //}

            if (lienInfo.RD_BRRW_FOREIGN_ADDR_TXT.IsNullOrWhiteSpace())
            {
                if (lienInfo.RD_BRRW_FRST_NM.IsNullOrWhiteSpace() && lienInfo.RD_BRRW_LAST_NM.IsNullOrWhiteSpace() || lienInfo.RD_BRRW_ADDR1_TXT.IsNullOrWhiteSpace())
                {
                    if (!IsZIPValid(lienInfo.RD_SITUS_ZIP_CD))
                    {
                        _logger.LogInformation($"Original Zip Code : {lienInfo.RD_SITUS_ZIP_CD}");
                        lienInfo.RD_SITUS_ZIP_CD = lienInfo.RD_SITUS_ZIP_CD.Length > 5 ? lienInfo.RD_SITUS_ZIP_CD.Substring(0, 5) : lienInfo.RD_SITUS_ZIP_CD;
                        _logger.LogInformation($"New Zip Code : {lienInfo.RD_SITUS_ZIP_CD}");
                        if (!IsZIPValid(lienInfo.RD_SITUS_ZIP_CD))
                        {
                            yield return DisqualificationReasons.InvalidZip;
                        }
                        else
                        {
                            //update zip code
                            _taxcomService.UpdateLienRecordZipCode("RD_SITUS_ZIP_CD", lienInfo.RD_SITUS_ZIP_CD, lienInfo.Id);
                        }
                    }
                }
                else
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
                            _taxcomService.UpdateLienRecordZipCode("RD_BRRW_ZIP_CD", lienInfo.RD_BRRW_ZIP_CD, lienInfo.Id);
                        }
                    }
                }
            }

            if (isEmailValidationReq)
            {
                if (lienInfo.RD_DLVR_EMAIL_ADDR_TXT.IsNullOrWhiteSpace())
                {
                    yield return DisqualificationReasons.EmailAddressNotFound;
                }
                else
                {
                    if (!IsValidEmail(lienInfo.RD_DLVR_EMAIL_ADDR_TXT))
                    {
                        yield return DisqualificationReasons.InvalidEmailAddress;
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

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Normalize the domain
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                                      RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Examines the domain part of the email and normalizes it.
                string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    var domainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + domainName;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(email,
                    @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                    @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        private void LogAndSendUnexpectedEmailNotification(Exception exception, string errorMsg)
        {
            _logger.LogError(exception, errorMsg);

            var msg = $"{errorMsg} < br/> {exception}";

            SendEmailNotification(_unexpectedEmailConfig.To, _unexpectedEmailConfig.Cc, _unexpectedEmailConfig.Subject, msg, true, false);
        }

        public bool SendEmailNotification(string to, string cc, string subject, string body, bool isHTML, bool isAttachmentExist, string[] attachmentPaths = null)
        {
            try
            {
                using (SmtpClient smtpClient = new SmtpClient("smtp.corelogic.com"))
                {
                    using (MailMessage msg = new MailMessage())
                    {
                        msg.From = new MailAddress("donotreply@corelogic.com");
                        msg.IsBodyHtml = true;
                        msg.To.Add(to);
                        msg.CC.Add(cc);
                        msg.Subject = subject;
                        msg.Priority = MailPriority.High;
                        msg.Body = body;
                        msg.IsBodyHtml = isHTML;

                        if (isAttachmentExist)
                        {
                            foreach (var attachment in attachmentPaths)
                            {
                                if (File.Exists(attachment))
                                    msg.Attachments.Add(new System.Net.Mail.Attachment(attachment));
                            }
                        }
                        smtpClient.Send(msg);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public async Task<StorageClient> CreateGCSClientConnection(string appEnvironment)
        {
            GoogleCredential credential = null;
            var cts = new CancellationToken();
            string delfusJsonFilePath = _taxcomConfig.TaxComNTSFile;

            if (appEnvironment == "F")
            {
                delfusJsonFilePath = _taxcomConfig.TaxComNTSFile.Replace("prod", "uat");
            }
            //string delfusJsonFilePath = @"Resources\nts-app-delfus-sa-prod.json";
            //if (appEnvironment == "F")
            //{
            //    delfusJsonFilePath = @"Resources\nts-app-delfus-sa-uat.json";
            //}
            using (var jsonStream = new FileStream(delfusJsonFilePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
            {
                credential = await GoogleCredential.FromStreamAsync(jsonStream, cts);
            }
            var storageClient = StorageClient.Create(credential);
            return storageClient;
        }
    }
}
