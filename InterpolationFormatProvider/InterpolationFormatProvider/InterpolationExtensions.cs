namespace Grax32
{
    public static class InterpolationExtensions
    {
        public static string ToInterpolatedString(this string pattern, params object[] args)
        {
            return string.Format(new InterpolationFormatProvider(), pattern, args);
        }
    }
}
