namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Shared string utility methods used across multiple prompt classes.
    /// Consolidates duplicated helpers (Levenshtein, Truncate, Similarity, etc.)
    /// into a single internal utility to reduce code duplication.
    /// </summary>
    internal static class StringHelpers
    {
        /// <summary>
        /// Standard Levenshtein distance with two-row optimization.
        /// </summary>
        internal static int LevenshteinDistance(string a, string b)
        {
            int m = a.Length;
            int n = b.Length;

            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int j = 0; j <= n; j++)
                prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= n; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }

            return prev[n];
        }

        /// <summary>
        /// Truncates a string with ellipsis if it exceeds the max length.
        /// </summary>
        internal static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
                return text ?? string.Empty;
            return maxLen > 3 ? text[..(maxLen - 3)] + "..." : text[..maxLen];
        }

        /// <summary>
        /// Computes similarity (0.0–1.0) via normalized Levenshtein distance.
        /// Falls back to line-based comparison for texts longer than 5000 chars.
        /// </summary>
        internal static double ComputeSimilarity(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal))
                return 1.0;

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0.0;

            if (a.Length > 5000 || b.Length > 5000)
            {
                var aLines = new HashSet<string>(a.Split('\n').Select(l => l.TrimEnd()));
                var bLines = new HashSet<string>(b.Split('\n').Select(l => l.TrimEnd()));
                int common = aLines.Intersect(bLines).Count();
                int total = Math.Max(aLines.Count, bLines.Count);
                return total > 0 ? (double)common / total : 1.0;
            }

            int distance = LevenshteinDistance(a, b);
            int maxLen = Math.Max(a.Length, b.Length);
            return 1.0 - ((double)distance / maxLen);
        }

        /// <summary>
        /// Jaccard similarity between two sets.
        /// </summary>
        internal static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 && b.Count == 0) return 1.0;
            int intersection = a.Intersect(b).Count();
            int union = a.Union(b).Count();
            return union > 0 ? (double)intersection / union : 0.0;
        }

        /// <summary>
        /// CSV-safe escape: wraps value in quotes if it contains commas, quotes, or newlines.
        /// </summary>
        internal static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        /// <summary>
        /// Counts non-overlapping occurrences of a pattern in text.
        /// </summary>
        internal static int CountOccurrences(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return 0;
            int count = 0, index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        /// <summary>
        /// Basic HTML encoding for &amp;, &lt;, &gt;, and &quot;.
        /// </summary>
        internal static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        /// <summary>
        /// Standard deviation of a list of doubles.
        /// </summary>
        internal static double StdDev(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            double avg = values.Average();
            double sumSq = values.Sum(v => (v - avg) * (v - avg));
            return Math.Sqrt(sumSq / (values.Count - 1));
        }

        /// <summary>
        /// Computes a percentile value from a sorted array.
        /// </summary>
        internal static double Percentile(double[] values, double percentile)
        {
            if (values.Length == 0) return 0.0;
            Array.Sort(values);
            double index = (percentile / 100.0) * (values.Length - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return values[lower];
            double frac = index - lower;
            return values[lower] * (1.0 - frac) + values[upper] * frac;
        }

        /// <summary>
        /// Safe regex match that doesn't throw on invalid patterns.
        /// </summary>
        internal static bool SafeRegexMatch(string input, string pattern)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(
                    input, pattern, System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromSeconds(1));
            }
            catch
            {
                return false;
            }
        }
    }
}
