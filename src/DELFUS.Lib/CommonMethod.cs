using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.Lib
{
    public static class Common
    {
        public static DateTime Now
        {
            get { return DateTime.UtcNow; }
        }

        public static DateTime Today
        {
            get { return DateTime.UtcNow.Date; }
        }

        public static DateTime EstToday
        {
            get { return DateTime.UtcNow.UtcToEst().Date; }
        }

        public static bool SendEmailNotification(string to, string cc, string subject, string body, bool isHTML, bool isAttachmentExist, string[] attachmentPaths = null)
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

        public static async Task<string> SendApiRequest(string apiUrl, object obj)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(apiUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var stringContent = new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");
                HttpResponseMessage httpResponse = client.PostAsync(client.BaseAddress, stringContent).Result;
                return await httpResponse.Content.ReadAsStringAsync();
            }
        }
    }

    public static class Env
    {
        public static string Prod = "Prod";
        public static string Staging = "Staging";
        public static string QA = "QA";
        public static string Internal = "Internal";
    }
}
