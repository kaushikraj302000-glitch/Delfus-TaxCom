using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public class EDeliveryRequest
    {
        [JsonProperty("to")]
        public List<string> To { get; set; }
        [JsonProperty("from")]
        public string From { get; set; }
        [JsonProperty("fromname")]
        public string FromName { get; set; }
        [JsonProperty("cc")]
        public List<string> Cc { get; set; }
        [JsonProperty("bcc")]
        public List<string> Bcc { get; set; }
        [JsonProperty("subject")]
        public string Subject { get; set; }
        [JsonProperty("body")]
        public string Body { get; set; }
        [JsonProperty("attachments")]
        public List<Attachment> Attachments { get; set; }
    }

    public class Attachment
    {
        [JsonProperty("filename")]
        public string FileName { get; set; }
        [JsonProperty("content")]
        public string Content { get; set; }
        [JsonProperty("inline")]
        public bool Inline { get; set; }
    }
}
