using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public class EDeliveryResponse
    {
        [JsonProperty("emailId")]
        public string EmailId { get; set; }
        [JsonProperty("submittedDate")]
        public double SubmittedDate { get; set; }       
    }

    public class EDeliveryErrorResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("errors")]
        public List<Error> Errors { get; set; }
    }

    public class Error
    {
        [JsonProperty("field")]
        public string Field { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class EmailStatuses
    {
        [JsonProperty("emails")]
        public List<Emails> Emails { get; set; }
    }

    public class Emails
    {
        [JsonProperty("emailId")]
        public string EmailId { get; set; }
        [JsonProperty("emailAddress")]
        public string EmailAddress { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("statusDate")]
        public double StatusDate { get; set; }
    }
}
