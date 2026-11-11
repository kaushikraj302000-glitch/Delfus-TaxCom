using System;
using System.Collections.Generic;
using System.Text;

namespace DELFUS.Entities
{
    public class ClientConsistency
    {
        public int Id { get; set; }
        public string CustId { get; set; }
        public string FieldName { get; set; }
        public string SearchText { get; set; }
        public string ReplaceText { get; set; }
    }

    public class AMClient
    {
        public int Id { get; set; }
        public string CustId { get; set; }
        public string ClientCode { get; set; }
    }
}
