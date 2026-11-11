using System;
using System.Collections.Generic;
using System.Text;

namespace TPV.DocGen
{
    public static class Extentions
    {
        public static string ToCsvString(this string value)
        {
            return "\"{0}\"".Fill(value ?? "");
        }

        public static string Fill(this string format, params object[] args)
        {
            return String.Format(format, args);
        }
    }
}
