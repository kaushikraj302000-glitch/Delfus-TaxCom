using CsvHelper.Expressions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TPV.DocGen
{
    public class App
    {
        private ILogger<App> _logger;
        private IHostApplicationLifetime _appLifeTime;

        private TPVService _tpvService;
        private TPVConfig _tpvConfig;

        public App(IHostApplicationLifetime appLifeTime, IConfiguration config, ILogger<App> logger, TPVService tpvService, IOptionsMonitor<TPVConfig> tpvConfig)
        {
            _logger = logger;
            _appLifeTime = appLifeTime;
            _tpvService = tpvService;
            _tpvConfig = tpvConfig.CurrentValue;
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

            _appLifeTime.StopApplication();
        }

        private async Task Download()
        {
            _logger.LogDebug("Downloading Task Started");


            var files = Directory.EnumerateFiles(_tpvConfig.TPVSourcePath, "*.csv");

            if (!files.Any())
                return;

            _logger.LogInformation($"Found Total {files.Count()} Names :{String.Join(",", files.Select(f => Path.GetFileName(f)))}");

            var rootAgencyFilePath = files.SingleOrDefault(f => Path.GetFileName(f).Contains("Agency"));
            var rootPropertyFilePath = files.SingleOrDefault(f => Path.GetFileName(f).Contains("Property"));

            if (string.IsNullOrWhiteSpace(rootAgencyFilePath) || string.IsNullOrWhiteSpace(rootPropertyFilePath))
            {
                SendEmailNotification(_tpvConfig.ClientEmailConfig.To, _tpvConfig.ClientEmailConfig.Cc, $"Files Not Found For TPV Process On {Common.Now.UtcToEst()} EST", $"System didn't find file name which contains 'Agency' or 'Property' in file names ", true, false);
                return;
            }

            try
            {
                _logger.LogInformation($"File downloading started : {Path.GetFileNameWithoutExtension(rootAgencyFilePath)}");

                string batchName = $"TPVProcess_{Common.Now.UtcToEst():yyyyMMddhhmmss}";

                Batch batch = new Batch()
                {
                    BatchName = batchName
                };

                batch.Id = await _tpvService.InsertBatchInfo(batch);

                BatchInfo batchInfo = new BatchInfo(batch.Id, _tpvConfig.TPVRootPath, batch.BatchName);

                Directory.CreateDirectory(batchInfo.RequestPath);

                string agencyFilePath = Path.Combine(batchInfo.RequestPath, Path.GetFileName(rootAgencyFilePath));
                string propertyFilePath = Path.Combine(batchInfo.RequestPath, Path.GetFileName(rootPropertyFilePath));

                File.Copy(rootAgencyFilePath, agencyFilePath, true);
                File.Copy(rootPropertyFilePath, propertyFilePath, true);


                if (File.Exists(agencyFilePath))
                {
                    File.Move(rootAgencyFilePath, Path.Combine(_tpvConfig.TPVSourceArchivePath, Path.GetFileName(agencyFilePath)), true);
                }

                if (File.Exists(propertyFilePath))
                {
                    File.Move(rootPropertyFilePath, Path.Combine(_tpvConfig.TPVSourceArchivePath, Path.GetFileName(propertyFilePath)), true);
                }

                await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Downloaded);

                _logger.LogInformation($"File downloading completed : {Path.GetFileNameWithoutExtension(agencyFilePath)}");
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, $"Error occured while downloading Batch Name {Path.GetFileNameWithoutExtension(rootAgencyFilePath)}");
            }
            _logger.LogDebug("Downloading Task Completed");
        }

        private async Task Parse()
        {
            _logger.LogDebug("Parsing Task Started");


            var batches = await _tpvService.GetBatchesByPipelineStatus(BatchPipelineStatus.Downloaded);

            foreach (var batch in batches)
            {
                try
                {
                    await _tpvService.InsertBatchData(batch.Id, _tpvConfig.TPVRootPath, batch.BatchName);
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

            var batches = await _tpvService.GetBatchesByPipelineStatus(BatchPipelineStatus.Parsed);

            foreach (var batch in batches)
            {
                try
                {
                    var agenciesWithoutProperties = await _tpvService.GetPropertiesInfoByBatchId(batch.Id);

                    if (agenciesWithoutProperties.Where(p => p.AgencyId == null).Any())
                    {
                        await _tpvService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.PartiallyQualified, "Some of the properties does not have any matching agency number in Mailing Address list.");
                    }

                    var agencies = await _tpvService.GetAgenciesDataByBatchId(batch.Id);

                    foreach (var agencyInfo in agencies)
                    {
                        try
                        {
                            var errors = new List<string>();

                            foreach (var error in ValidateAgencyRecord(agencyInfo))
                            {
                                errors.Add(error);
                            }

                            if (errors.Count == 0)
                            {
                                await _tpvService.UpdateAgencyRecordStatus(agencyInfo.Id, RecordStatus.Qualified);
                            }
                            else
                            {
                                await _tpvService.UpdateAgencyDisqualifyReason(agencyInfo.Id, String.Join(" | ", errors));
                            }
                        }
                        catch (Exception ex)
                        {
                            LogAndSendUnexpectedEmailNotification(ex, $"Error occured while processing Lien Record {agencyInfo.Id}");
                        }
                    }

                    var batchQualifiedCounts = await _tpvService.GetAgencyQualifiedCountsByBatchId(batch.Id);
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
                        await _tpvService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Qualified);
                    }
                    else if (qualifiedRecordCount > 0 && qualifiedRecordCount < totalRecordCount)
                    {
                        await _tpvService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.PartiallyQualified);
                    }
                    else
                    {
                        var msg = "All letters are disqualified due to some invalid configurations or inputs.";
                        SendEmailNotification(_tpvConfig.UnexpectedEmailConfig.To, _tpvConfig.UnexpectedEmailConfig.Cc, $"Disqualified Batch For TPV Process On {Common.Now.UtcToEst()} EST", $"{msg} for batch-id {batch.Id}", true, false);

                        await _tpvService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Disqualified, msg);
                    }

                    await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Validated);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while validating batch {batch.Id}");
                }
            }
            _logger.LogDebug("Validation Task Completed");
        }

        private IEnumerable<string> ValidateAgencyRecord(AgencyInfo agencyInfo)
        {
            if (string.IsNullOrWhiteSpace(agencyInfo.TaxCollectorName) || string.IsNullOrWhiteSpace(agencyInfo.TaxCollectorAddress) || string.IsNullOrWhiteSpace(agencyInfo.TaxCollectorCity) || string.IsNullOrWhiteSpace(agencyInfo.TaxCollectorState) || string.IsNullOrWhiteSpace(agencyInfo.TaxCollectorZip))
            {
                yield return DisqualificationReasons.InvalidTaxAddress;
            }

            if (!string.IsNullOrWhiteSpace(agencyInfo.TaxCollectorZip))
            {
                if (!IsZIPValid(agencyInfo.TaxCollectorZip))
                {
                    _logger.LogInformation($"Original Tax Collector Zip Code : {agencyInfo.TaxCollectorZip}");
                    agencyInfo.TaxCollectorZip = agencyInfo.TaxCollectorZip.Length > 5 ? agencyInfo.TaxCollectorZip.Substring(0, 5) : agencyInfo.TaxCollectorZip;
                    _logger.LogInformation($"New Tax Collector Zip Code : {agencyInfo.TaxCollectorZip}");
                    if (!IsZIPValid(agencyInfo.TaxCollectorZip))
                    {
                        yield return DisqualificationReasons.InvalidZip;
                    }
                    else
                    {
                        //update zip code
                        _tpvService.UpdateAgencyTaxCollectorZipCode("TaxCollectorZip", agencyInfo.TaxCollectorZip, agencyInfo.Id).Wait();
                    }
                }
            }
        }

        private bool IsZIPValid(string zipCode)
        {
            Regex regEx = new Regex(@"^(?(^00000(|-0000))|\d{5}(?:|[-\s]+\d{4}))$");
            var param = regEx.Match(zipCode);

            return param.Success;
        }

        private async Task Acknowledge()
        {
            _logger.LogDebug("Acknowledgement Task Started");

            var batches = await _tpvService.GetBatchesByPipelineStatus(BatchPipelineStatus.Validated);

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _tpvConfig.TPVRootPath, batch.BatchName);

                    var lienData = await _tpvService.GetAgenciesDataByBatchId(batch.Id);

                    var qualifiedRecords = lienData.Where(rd => rd.RecordStatus == RecordStatus.Qualified).ToList();

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
                        await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Rejected);
                    }
                    else if (batch.ValidationStatus == BatchValidationStatus.PartiallyQualified)
                    {
                        //Send Email Notification with disqualified record Report
                        SendAcknowledmentEmail(batch, lienData.ToList(), disqualifiedReportPath);
                        // Batch status Report Prepped
                        await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Acknowledged);
                    }
                    else
                    {
                        //Send Email Notification with counts
                        SendAcknowledmentEmail(batch, lienData.ToList(), "");
                        await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Acknowledged);
                    }
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while acknowledging batch {batch.Id}");
                }
            }
            _logger.LogDebug("Acknowledgement Task Completed");
        }

        private async Task GenerateLetters()
        {
            _logger.LogDebug("GenerateLetters Task Started");

            var batches = await _tpvService.GetBatchesByPipelineStatus(BatchPipelineStatus.Acknowledged);

            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _tpvConfig.TPVRootPath, batch.BatchName);

                    Directory.CreateDirectory(batchInfo.LettersPath);

                    var agencies = await _tpvService.GetAgenciesDataByBatchId(batch.Id);

                    var qualifiedRecords = agencies.Where(rd => rd.RecordStatus == RecordStatus.Qualified);

                    foreach (var agency in qualifiedRecords)
                    {
                        try
                        {
                            DocGenResponse docGenResponse = new DocGenResponse();
                            var agencyProperties = (await _tpvService.GetPropertiesInfoByAgencyId(agency.Id)).AsList();

                            var base64Template = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_tpvConfig.TPVTemplatePath)));

                            dynamic objTpv = new ExpandoObject();
                            objTpv.Data = new
                            {
                                CurrentDate = Common.EstToday.ToString("MMMM d, yyyy"),
                                CurrentYear = Common.EstToday.Year,
                                agency.AgencyNumber,
                                agency.TaxCollectorName,
                                agency.TaxCollectorAddress,
                                agency.TaxCollectorCityStateZip,
                                agency.ELD,
                                agency.DueDate,
                                agency.AgencyName,
                                Properties = agencyProperties
                            };

                            objTpv.Settings = new
                            {
                                MailMergeOptions = "RemoveEmptyParagraphs",
                                ReportEngineOptions = "RemoveEmptyParagraphs",
                                OutputDocType = "pdf",
                                Template = base64Template
                            };

                            objTpv.TransformData = null;

                            using (var client = new HttpClient())
                            using (var stringContent = new StringContent(JsonConvert.SerializeObject(objTpv), Encoding.UTF8, "application/json"))
                            {
                                HttpResponseMessage httpResponse = await client.PostAsync(_tpvConfig.TPV_API, stringContent);
                                var responseString = await httpResponse.Content.ReadAsStringAsync();
                                if (!httpResponse.IsSuccessStatusCode || httpResponse.StatusCode != HttpStatusCode.OK)
                                {
                                    await _tpvService.UpdateAgencyDisqualifyReason(agency.Id, responseString);
                                    continue;
                                }
                                docGenResponse = JsonConvert.DeserializeObject<DocGenResponse>(responseString);
                            }

                            var pdfFileName = $"{agency.Id.ToString().PadLeft(6, '0')}_{agency.AgencyNumber}.pdf";
                            string finalPdfPath = Path.Combine(batchInfo.LettersPath, pdfFileName);

                            File.WriteAllBytes(finalPdfPath, docGenResponse.DocumentData);

                            //update lien record status
                            await _tpvService.UpdateAgencyRecordStatus(agency.Id, RecordStatus.LetterGenerated);
                            //update lien filename 
                            await _tpvService.UpdateAgencyRecordPdfFileName(agency.Id, pdfFileName);
                        }
                        catch (Exception ex)
                        {
                            LogAndSendUnexpectedEmailNotification(ex, $"Error occured while generating letter for lien Id {agency.Id}");
                        }
                    }

                    var batchQualifiedCounts = await _tpvService.GetAgencyQualifiedCountsByBatchId(batch.Id);
                    var letterGeneratedCount = batchQualifiedCounts.LetterGeneratedCount;
                    var qualifiedRecordCount = batchQualifiedCounts.QualifedCount;

                    if (qualifiedRecordCount > 0)
                    {
                        continue;
                    }

                    if (letterGeneratedCount == 0)
                    {
                        await _tpvService.UpdateBatchValidationStatus(batch.Id, BatchValidationStatus.Disqualified, "All letters are disqualified due to unexpected errors in GenerateLetters method.");

                        await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.Rejected);

                        continue;
                    }

                    //update Batch status
                    await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.LetterGenerated);
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

            var batches = (await _tpvService.GetBatchesByPipelineStatus(BatchPipelineStatus.LetterGenerated));
            foreach (var batch in batches)
            {
                try
                {
                    BatchInfo batchInfo = new BatchInfo(batch.Id, _tpvConfig.TPVRootPath, batch.BatchName);

                    var mgData = await _tpvService.GetAgenciesDataByBatchId(batch.Id);

                    var qualifiedRecords = mgData.Where(rd => rd.RecordStatus == RecordStatus.LetterGenerated);

                    var zipFileName = $"{batch.Id}_{batch.BatchName}.zip";
                    var zipFilePath = Path.Combine(batchInfo.LettersPath, zipFileName);
                    string xmlFileName = $"{batch.Id}_{batch.BatchName}.xml";
                    var xmlFilePath = Path.Combine(batchInfo.LettersPath, xmlFileName);
                    MailBatch mailBatch = new MailBatch()
                    {
                        Id = Guid.NewGuid(),
                        ClientCode = "TPV",
                        Priority = Priority.Medium
                    };

                    if (File.Exists(zipFilePath))
                        File.Delete(zipFilePath);
                    if (File.Exists(xmlFilePath))
                        File.Delete(xmlFilePath);

                    using (ZipArchive newFile = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                    {
                        foreach (var mp in qualifiedRecords)
                        {
                            try
                            {
                                MailPiece mailPiece = new MailPiece();
                                mailPiece.Id = Guid.NewGuid();
                                mailPiece.BusinessFriendlyIdName = "Agency Number";
                                mailPiece.BusinessFriendlyIdValue = mp.AgencyNumber;
                                mailPiece.DocumentOrPackage = new DocumentOrPackage() { Name = mp.PdfFileName };
                                mailPiece.DocumentOrPackageType = _tpvConfig.TPVDocumentType;

                                mailPiece.RecipientInfo = new RecipientInfo()
                                {
                                    Recipient = new Recipient()
                                    {
                                        FirstName = mp.TaxCollectorName
                                    },
                                    Address = new Address()
                                    {
                                        Street = mp.TaxCollectorAddress,
                                        City = mp.TaxCollectorCity,
                                        State = mp.TaxCollectorState,
                                        Zip = mp.TaxCollectorZip
                                    }
                                };

                                var customInfo = new List<Metadata>();

                                customInfo.Add(new Metadata()
                                {
                                    Key = "CustomerId",
                                    Value = mp.AgencyNumber,
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
                    File.Move(zipFilePath, Path.Combine(_tpvConfig.TPVAMBatchPath, zipFileName));
                    File.WriteAllText(Path.Combine(_tpvConfig.TPVAMBatchPath, $"{batch.Id}_{batchInfo.BatchName}.GO"), string.Empty);
                    //update Batch status
                    await _tpvService.UpdateBatchPipelineStatus(batch.Id, BatchPipelineStatus.SubmittedToAuthoMail);
                }
                catch (Exception ex)
                {
                    LogAndSendUnexpectedEmailNotification(ex, $"Error occured while submitting batch to Authomail for Batch {batch.Id}");
                }
            }
            _logger.LogDebug("SubmitToAuthoMail Task Completed");
        }

        private static StringBuilder GenerateDisqualifiedReport(BatchInfo batchInfo, List<AgencyInfo> agencyRecords)
        {
            if (agencyRecords.Count == 0)
            {
                return null;
            }

            var columnName = String.Join(",", "BatchName", "AgencyName", "AgencyNumber", "TaxCollectorName", "Disqualification Reason");

            StringBuilder errorReport = new StringBuilder(columnName);

            errorReport.AppendLine();
            foreach (var agencyInfo in agencyRecords)
            {
                var line = String.Join(",", batchInfo.BatchName, agencyInfo.AgencyName.ToCsvString(), agencyInfo.AgencyNumber.ToCsvString(), agencyInfo.TaxCollectorName.ToCsvString(), agencyInfo.DisqualifyReason);
                errorReport.AppendLine(line);
            }
            return errorReport;
        }

        private bool SendAcknowledmentEmail(Batch batchInfo, List<AgencyInfo> agencyData, string errorReportPath)
        {
            try
            {
                var subject = $"TPV Batch Processed {batchInfo.BatchName}";
                string body = string.Empty;
                bool hasAttachment = false;
                if (agencyData.Count > 0)
                {
                    #region Prepare email body

                    string downloadEmail_TD_SrNoStyle = "style='width:10%;border:solid windowtext 1.0pt;border-top:none;padding:0in 5.4pt 0in 5.4pt' valign='top' width='213'><p>";
                    string downloadEmail_TD_DescriptionStyle = "style='width:80%;border-top:none;border-left:none;border-bottom:solid windowtext 1.0pt;border-right:solid windowtext 1.0pt;padding:0in 5.4pt 0in 5.4pt'' valign='top' width='213'><p>";
                    string downloadEmail_TD_CountStyle = "style='width:10%;border-top:none;border-left:none;border-bottom:solid windowtext 1.0pt;border-right:solid windowtext 1.0pt;padding:0in 5.4pt 0in 5.4pt' valign='top' width='213'><p>";

                    StringBuilder sb = new StringBuilder(File.ReadAllText(_tpvConfig.ClientEmailConfig.TemplatePath));
                    sb = sb.Replace("<%FileName%>", batchInfo.BatchName);
                    sb = sb.Replace("<%yyyymmdd%>", Common.Now.UtcToEst().ToString("MM/dd/yyyy hh:mm tt"));

                    StringBuilder data = new StringBuilder();
                    data.AppendLine(string.Format("<tr> <td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_SrNoStyle, "1"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_DescriptionStyle, "TOTAL RECORDS"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td> </tr>", downloadEmail_TD_CountStyle, agencyData.Count));
                    data.AppendLine(string.Format("<tr> <td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_SrNoStyle, "2"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p>  </td>", downloadEmail_TD_DescriptionStyle, "VALID RECORDS"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td> </tr>", downloadEmail_TD_CountStyle, agencyData.Where(l => l.RecordStatus == RecordStatus.Qualified).Count()));
                    data.AppendLine(string.Format("<tr> <td {0} {1} <u></u><u></u></p> </td>", downloadEmail_TD_SrNoStyle, "3"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p>  </td>", downloadEmail_TD_DescriptionStyle, "INVALID RECORDS"));
                    data.AppendLine(string.Format("<td {0} {1} <u></u><u></u></p> </td> </tr>", downloadEmail_TD_CountStyle, agencyData.Where(l => l.RecordStatus == RecordStatus.Disqualified).Count()));

                    sb = sb.Replace("<%trdata%>", data.ToString());
                    body = sb.ToString();

                    #endregion
                }
                else if (agencyData.Count == 0)
                {
                    body = string.Format("No records available in the batch file {0}.", batchInfo.BatchName);
                }

                if (!string.IsNullOrWhiteSpace(errorReportPath))
                    hasAttachment = true;


                SendEmailNotification(_tpvConfig.ClientEmailConfig.To, _tpvConfig.ClientEmailConfig.Cc, subject, body, true, hasAttachment, new string[] { errorReportPath });
            }
            catch (Exception ex)
            {
                LogAndSendUnexpectedEmailNotification(ex, "Error occured while sending achnowlegement email.");
            }

            return true;
        }

        private void LogAndSendUnexpectedEmailNotification(Exception exception, string errorMsg)
        {
            _logger.LogError(exception, errorMsg);

            var msg = $"{errorMsg} < br/> {exception}";

            SendEmailNotification(_tpvConfig.UnexpectedEmailConfig.To, _tpvConfig.UnexpectedEmailConfig.Cc, _tpvConfig.UnexpectedEmailConfig.Subject, msg, true, false);
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
                                    msg.Attachments.Add(new Attachment(attachment));
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
    }
}
