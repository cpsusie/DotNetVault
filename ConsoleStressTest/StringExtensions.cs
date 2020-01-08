using System;

namespace ConsoleStressTest
{
    public static class StringExtensions
    {
        public static string ThrowIfNullOrEmpty(this string s, string paramName)
        {
            if (s == null) throw new ArgumentNullException(paramName ?? nameof(paramName));
            if (string.IsNullOrEmpty(s))
                throw new ArgumentException(@"The string may not be empty.", paramName ?? nameof(paramName));
            return s;
        }

        public static string ThrowIfNullEmptyOrWhitespace(this string s, string paramName)
        {
            if (s == null) throw new ArgumentNullException(paramName ?? nameof(paramName));
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException(@"The string may not be empty or just whitespace.",
                    paramName ?? nameof(paramName));
            return s;
        }
    }
}