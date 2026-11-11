using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DELFUS.TemplateVerifier
{
    class Program
    {
        static void Main(string[] args)
        {
            //FieldTypeReport.GenerateFieldTypeReport();
            MergeFieldReport.GenerateMergeFieldReport();
            MergeFieldReport.GenerateTemplateVerifierReport();
        }
    }
}
