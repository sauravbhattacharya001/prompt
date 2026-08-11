namespace Prompt
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;

    /// <summary>
    /// Shared string utility methods used across multiple prompt classes.
    /// Consolidates duplicated helpers (Levenshtein, Truncate, ComputeSimilarity, etc.)
    /// into a single internal utility to reduce code duplication.
    /// Character-level similarity (a Levenshtein-based ratio) lives here in
    /// <see cref="ComputeSimilarity"/>. Token-count estimation lives in
    /// <see cref="TextAnalysisHelpers"/>. Set-based Jaccard similarity is not a
    /// shared primitive; it is computed inline over tag sets in
    /// <c>PromptTagManager</c> where it is the only consumer.
    /// </summary>
    internal static class StringHelpers
    {
        /// <summary>
        /// Standard Levenshtein distance with two-row optimization.
        /// Uses ArrayPool to avoid GC pressure when called in tight loops
        /// (e.g. O(n²) pairwise similarity comparisons).
        /// </summary>
        internal static int LevenshteinDistance(string a, string b)
        {
            return LevenshteinDistance(a, b, int.MaxValue);
        }

        /// <summary>
        /// Bounded Levenshtein distance — returns early when the minimum
        /// possible distance exceeds <paramref name="maxDistance"/>,
        /// returning maxDistance + 1. This avoids wasted computation when
        /// callers only need to know if strings are within a threshold.
        /// Uses ArrayPool to eliminate per-call heap allocations.
        /// </summary>
        internal static int LevenshteinDistance(string a, string b, int maxDistance)
        {
            int m = a.Length;
            int n = b.Length;

            // Quick length-difference check for bounded mode
            if (Math.Abs(m - n) > maxDistance)
                return maxDistance + 1;

            // Ensure 'a' is the shorter string for less memory usage
            if (m > n)
            {
                (a, b) = (b, a);
                (m, n) = (n, m);
            }

            int rowSize = m + 1;
            var pool = ArrayPool<int>.Shared;
            var prev = pool.Rent(rowSize);
            var curr = pool.Rent(rowSize);

            try
            {
                for (int j = 0; j <= m; j++)
                    prev[j] = j;

                for (int i = 1; i <= n; i++)
                {
                    curr[0] = i;
                    int rowMin = i;
                    char bi = b[i - 1];

                    for (int j = 1; j <= m; j++)
                    {
                        int cost = a[j - 1] == bi ? 0 : 1;
                        int val = Math.Min(
                            Math.Min(curr[j - 1] + 1, prev[j] + 1),
                            prev[j - 1] + cost);
                        curr[j] = val;
                        if (val < rowMin) rowMin = val;
                    }

                    // Early exit: if every cell in this row exceeds the
                    // bound, the final result will too
                    if (rowMin > maxDistance)
                    {
                        return maxDistance + 1;
                    }

                    (prev, curr) = (curr, prev);
                }

                return prev[m];
            }
            finally
            {
                pool.Return(prev);
                pool.Return(curr);
            }
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
