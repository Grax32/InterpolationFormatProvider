using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grax.Text
{
    public static class InterpolationExtensions
    {

        public static string ToInterpolatedString(this string pattern, params object[] args)
        {
            return string.Format(new InterpolationFormatProvider(), pattern, args);
        }
    }
}
