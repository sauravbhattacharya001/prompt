namespace Prompt
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Shared text analysis utilities for tokenization, similarity computation,
    /// and word-level operations used across multiple prompt analysis components.
    /// Eliminates duplicated Tokenize/Jaccard methods that existed in
    /// PromptTokenOptimizer, PromptBenchmarkSuite, PromptRegressionDetector,
    /// PromptSimilarityAnalyzer, PromptEnsemble, PromptMemoryIndex, and others.
    /// </summary>
    internal static class TextAnalysisHelpers
    {
        private static readonly Regex WordTokenizer =
            new(@"\b\w+\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Tokenizes text into a deduplicated, lowercased word set.
        /// Filters out single-character tokens to reduce noise.
        /// </summary>
        /// <param name="text">The text to tokenize.</param>
        /// <returns>A set of distinct lowercase words (length > 1).</returns>
        internal static HashSet<string> TokenizeToWordSet(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new HashSet<string>(StringComparer.Ordinal);

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in WordTokenizer.Matches(text.ToLowerInvariant()))
            {
                if (m.Value.Length > 1)
                    set.Add(m.Value);
            }
            return set;
        }

        /// <summary>
        /// Tokenizes text into a list of lowercased words (with duplicates preserved).
        /// Useful when word frequency matters.
        /// </summary>
        /// <param name="text">The text to tokenize.</param>
        /// <returns>A list of lowercase words (length > 1).</returns>
        internal static List<string> TokenizeToWordList(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var list = new List<string>();
            foreach (Match m in WordTokenizer.Matches(text.ToLowerInvariant()))
            {
                if (m.Value.Length > 1)
                    list.Add(m.Value);
            }
            return list;
        }

        /// <summary>
        /// Tokenizes text into a set including single-character words.
        /// Some callers (e.g., benchmark overlap) need all tokens.
        /// </summary>
        /// <param name="text">The text to tokenize.</param>
        /// <returns>A set of distinct lowercase words.</returns>
        internal static HashSet<string> TokenizeToWordSetUnfiltered(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>(
                Regex.Split(text.ToLowerInvariant(), @"\W+")
                    .Where(w => w.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Computes Jaccard similarity between two pre-tokenized word sets.
        /// Iterates over the smaller set for efficiency.
        /// </summary>
        /// <param name="setA">First word set.</param>
        /// <param name="setB">Second word set.</param>
        /// <returns>Similarity score between 0.0 and 1.0.</returns>
        internal static double JaccardSimilarity(HashSet<string> setA, HashSet<string> setB)
        {
            if (setA.Count == 0 || setB.Count == 0) return 0;

            var smaller = setA.Count <= setB.Count ? setA : setB;
            var larger = setA.Count <= setB.Count ? setB : setA;

            int intersection = 0;
            foreach (var word in smaller)
            {
                if (larger.Contains(word))
                    intersection++;
            }

            int union = setA.Count + setB.Count - intersection;
            return union > 0 ? (double)intersection / union : 0;
        }

        /// <summary>
        /// Convenience overload: tokenizes two strings and computes their
        /// Jaccard similarity in one call.
        /// </summary>
        /// <param name="a">First text.</param>
        /// <param name="b">Second text.</param>
        /// <returns>Similarity score between 0.0 and 1.0.</returns>
        internal static double JaccardSimilarity(string a, string b)
        {
            var setA = TokenizeToWordSet(a);
            var setB = TokenizeToWordSet(b);
            return JaccardSimilarity(setA, setB);
        }

        /// <summary>
        /// Computes word overlap ratio: what fraction of words in
        /// <paramref name="reference"/> appear in <paramref name="candidate"/>.
        /// </summary>
        /// <param name="candidate">The text to check against.</param>
        /// <param name="reference">The reference text whose words we look for.</param>
        /// <returns>Overlap ratio between 0.0 and 1.0.</returns>
        internal static double WordOverlap(string candidate, string reference)
        {
            var wordsA = TokenizeToWordSetUnfiltered(candidate);
            var wordsB = TokenizeToWordSetUnfiltered(reference);
            if (wordsB.Count == 0) return 0.0;

            int hits = 0;
            foreach (var w in wordsB)
            {
                if (wordsA.Contains(w))
                    hits++;
            }
            return (double)hits / wordsB.Count;
        }
    }
}
