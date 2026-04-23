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
        /// Extracts character n-grams from text with their frequency counts.
        /// Used for bigram/trigram similarity computations across multiple analyzers.
        /// </summary>
        /// <param name="text">The text to extract n-grams from (should be pre-lowercased).</param>
        /// <param name="n">The n-gram size (e.g., 2 for bigrams, 3 for trigrams).</param>
        /// <returns>A dictionary mapping each n-gram to its occurrence count.</returns>
        internal static Dictionary<string, int> GetNgrams(string text, int n)
        {
            var ngrams = new Dictionary<string, int>();
            for (int i = 0; i <= text.Length - n; i++)
            {
                var gram = text.Substring(i, n);
                ngrams[gram] = ngrams.GetValueOrDefault(gram) + 1;
            }
            return ngrams;
        }

        /// <summary>
        /// Computes cosine similarity between two strings using character n-gram vectors.
        /// </summary>
        /// <param name="a">First text.</param>
        /// <param name="b">Second text.</param>
        /// <param name="n">The n-gram size (default 2 for bigrams).</param>
        /// <returns>Cosine similarity between 0.0 and 1.0.</returns>
        internal static double NgramCosineSimilarity(string a, string b, int n = 2)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0.0;

            var ngramsA = GetNgrams(a.ToLowerInvariant(), n);
            var ngramsB = GetNgrams(b.ToLowerInvariant(), n);

            var allKeys = new HashSet<string>(ngramsA.Keys);
            allKeys.UnionWith(ngramsB.Keys);

            double dot = 0, magA = 0, magB = 0;
            foreach (var key in allKeys)
            {
                ngramsA.TryGetValue(key, out int countA);
                ngramsB.TryGetValue(key, out int countB);
                dot += countA * countB;
                magA += countA * countA;
                magB += countB * countB;
            }

            if (magA == 0 || magB == 0) return 0.0;
            return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
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

        /// <summary>
        /// Pre-compiled regex for sentence boundary splitting.
        /// Splits on sentence-ending punctuation (.!?) optionally followed by
        /// whitespace, or on newlines followed by whitespace.
        /// </summary>
        private static readonly Regex SentenceBoundary =
            new(@"(?<=[.!?\n])\s+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Splits text into sentences on punctuation boundaries (.!?) and
        /// optional newline boundaries. Returns a list of non-empty trimmed strings.
        /// </summary>
        /// <param name="text">Text to split into sentences.</param>
        /// <param name="splitOnNewlines">If true, also splits on newline boundaries.</param>
        /// <returns>List of sentence strings.</returns>
        internal static List<string> SplitSentences(string text, bool splitOnNewlines = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var pattern = splitOnNewlines ? SentenceBoundary : PunctuationOnlySentenceBoundary;
            return pattern.Split(text)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
        }

        /// <summary>
        /// Estimates token count for a text string using the ~4 chars/token
        /// approximation common to GPT-family tokenizers.
        /// Replaces duplicated EstimateTokens methods across
        /// PromptConversationSimulator, PromptMemoryIndex, and PromptStyleTransformer.
        /// </summary>
        /// <param name="text">The text to estimate tokens for.</param>
        /// <returns>Estimated token count (0 for null/empty input).</returns>
        internal static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        /// <summary>
        /// Pre-compiled regex splitting only on punctuation boundaries (no newlines).
        /// </summary>
        private static readonly Regex PunctuationOnlySentenceBoundary =
            new(@"(?<=[.!?])\s+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    }
}
