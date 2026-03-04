namespace Prompt
{
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Specifies how aggressively a prompt is normalized before fingerprinting.
    /// Higher levels produce fingerprints that are invariant to more transformations.
    /// </summary>
    public enum NormalizationLevel
    {
        /// <summary>No normalization — raw text is hashed as-is.</summary>
        None = 0,

        /// <summary>Collapse whitespace and trim.</summary>
        Whitespace = 1,

        /// <summary>Whitespace normalization + lowercase.</summary>
        CaseInsensitive = 2,

        /// <summary>Whitespace + lowercase + remove punctuation.</summary>
        Structural = 3,

        /// <summary>Whitespace + lowercase + remove punctuation + sort words (order-independent).</summary>
        Semantic = 4
    }

    /// <summary>
    /// Represents a content-addressable fingerprint of a prompt, useful for
    /// deduplication, cache keying, and change detection.
    /// </summary>
    public class PromptFingerprint
    {
        /// <summary>Gets the SHA-256 hash of the normalized prompt.</summary>
        [JsonPropertyName("hash")]
        public string Hash { get; init; } = "";

        /// <summary>Gets the normalization level used to produce this fingerprint.</summary>
        [JsonPropertyName("normalization")]
        public NormalizationLevel Normalization { get; init; }

        /// <summary>Gets the original character count before normalization.</summary>
        [JsonPropertyName("originalLength")]
        public int OriginalLength { get; init; }

        /// <summary>Gets the normalized character count.</summary>
        [JsonPropertyName("normalizedLength")]
        public int NormalizedLength { get; init; }

        /// <summary>Gets the word count of the normalized text.</summary>
        [JsonPropertyName("wordCount")]
        public int WordCount { get; init; }

        /// <summary>Gets the UTC timestamp when this fingerprint was created.</summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        /// <summary>Gets an optional tag/label for this fingerprint.</summary>
        [JsonPropertyName("tag")]
        public string? Tag { get; init; }

        /// <summary>
        /// Gets the n-gram shingle hashes used for similarity comparison.
        /// Only populated when <see cref="PromptFingerprintOptions.EnableSimilarity"/> is true.
        /// </summary>
        [JsonIgnore]
        public HashSet<int> ShingleHashes { get; init; } = new();

        /// <summary>
        /// Checks whether two fingerprints represent the same content.
        /// </summary>
        public bool Matches(PromptFingerprint other)
        {
            if (other is null) return false;
            return Hash == other.Hash;
        }

        /// <summary>
        /// Computes the Jaccard similarity (0.0–1.0) between this fingerprint
        /// and another, based on word-level shingle hashes. Requires both
        /// fingerprints to have been created with similarity enabled.
        /// </summary>
        /// <returns>Similarity score from 0.0 (completely different) to 1.0 (identical).</returns>
        public double SimilarityTo(PromptFingerprint other)
        {
            if (other is null) return 0.0;
            if (ShingleHashes.Count == 0 || other.ShingleHashes.Count == 0) return 0.0;

            int intersection = 0;
            foreach (var h in ShingleHashes)
            {
                if (other.ShingleHashes.Contains(h))
                    intersection++;
            }

            int union = ShingleHashes.Count + other.ShingleHashes.Count - intersection;
            return union == 0 ? 1.0 : (double)intersection / union;
        }

        /// <summary>Serializes this fingerprint to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

        /// <inheritdoc/>
        public override string ToString() => $"Fingerprint[{Hash[..12]}... | {Normalization} | {WordCount}w]";

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is PromptFingerprint pf && Hash == pf.Hash;

        /// <inheritdoc/>
        public override int GetHashCode() => Hash.GetHashCode();
    }

    /// <summary>
    /// Options for configuring prompt fingerprinting behavior.
    /// </summary>
    public class PromptFingerprintOptions
    {
        /// <summary>Gets or sets the normalization level. Default is <see cref="NormalizationLevel.CaseInsensitive"/>.</summary>
        public NormalizationLevel Normalization { get; set; } = NormalizationLevel.CaseInsensitive;

        /// <summary>Gets or sets whether to compute shingle hashes for similarity comparison.</summary>
        public bool EnableSimilarity { get; set; } = false;

        /// <summary>Gets or sets the shingle size (n-gram window) for similarity. Default is 3.</summary>
        public int ShingleSize { get; set; } = 3;

        /// <summary>Gets or sets an optional tag to attach to the fingerprint.</summary>
        public string? Tag { get; set; }

        /// <summary>Gets or sets stop words to remove during normalization (Structural+ levels).</summary>
        public HashSet<string>? StopWords { get; set; }
    }

    /// <summary>
    /// Produces content-addressable fingerprints for prompts, enabling deduplication,
    /// cache keying, change detection, and similarity comparison.
    /// </summary>
    /// <remarks>
    /// <para><b>Key capabilities:</b></para>
    /// <list type="bullet">
    ///   <item>SHA-256 hashing with configurable normalization levels</item>
    ///   <item>Jaccard similarity via word-level shingling</item>
    ///   <item>Batch fingerprinting with duplicate detection</item>
    ///   <item>Fingerprint diffing for change detection</item>
    ///   <item>Stop word removal for noise-invariant fingerprints</item>
    /// </list>
    /// </remarks>
    public class PromptFingerprintGenerator
    {
        private static readonly HashSet<string> DefaultStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "shall", "can", "to", "of", "in", "for",
            "on", "with", "at", "by", "from", "as", "into", "through", "during",
            "before", "after", "above", "below", "between", "and", "but", "or",
            "not", "no", "nor", "so", "yet", "both", "either", "neither", "each",
            "every", "all", "any", "few", "more", "most", "other", "some", "such",
            "than", "too", "very", "just", "it", "its", "this", "that", "these",
            "those", "i", "me", "my", "we", "our", "you", "your", "he", "him",
            "his", "she", "her", "they", "them", "their", "what", "which", "who"
        };

        /// <summary>
        /// Creates a fingerprint for the given prompt text.
        /// </summary>
        /// <param name="prompt">The prompt text to fingerprint.</param>
        /// <param name="options">Optional configuration. If null, defaults are used.</param>
        /// <returns>A <see cref="PromptFingerprint"/> representing the prompt's content signature.</returns>
        /// <exception cref="ArgumentNullException">Thrown when prompt is null.</exception>
        public PromptFingerprint Fingerprint(string prompt, PromptFingerprintOptions? options = null)
        {
            if (prompt is null) throw new ArgumentNullException(nameof(prompt));

            options ??= new PromptFingerprintOptions();
            var normalized = Normalize(prompt, options);
            var hash = ComputeHash(normalized);
            var words = GetWords(normalized);

            var shingles = new HashSet<int>();
            if (options.EnableSimilarity && words.Length > 0)
            {
                shingles = ComputeShingles(words, options.ShingleSize);
            }

            return new PromptFingerprint
            {
                Hash = hash,
                Normalization = options.Normalization,
                OriginalLength = prompt.Length,
                NormalizedLength = normalized.Length,
                WordCount = words.Length,
                Tag = options.Tag,
                ShingleHashes = shingles
            };
        }

        /// <summary>
        /// Fingerprints multiple prompts and identifies duplicates.
        /// </summary>
        /// <param name="prompts">The prompts to fingerprint.</param>
        /// <param name="options">Optional configuration.</param>
        /// <returns>A batch result containing fingerprints and duplicate groups.</returns>
        public BatchFingerprintResult FingerprintBatch(IEnumerable<string> prompts, PromptFingerprintOptions? options = null)
        {
            if (prompts is null) throw new ArgumentNullException(nameof(prompts));

            var results = new List<(int Index, string Prompt, PromptFingerprint Fingerprint)>();
            var hashGroups = new Dictionary<string, List<int>>();
            int index = 0;

            foreach (var prompt in prompts)
            {
                var fp = Fingerprint(prompt, options);
                results.Add((index, prompt, fp));

                if (!hashGroups.ContainsKey(fp.Hash))
                    hashGroups[fp.Hash] = new List<int>();
                hashGroups[fp.Hash].Add(index);

                index++;
            }

            var duplicates = hashGroups
                .Where(g => g.Value.Count > 1)
                .Select(g => new DuplicateGroup
                {
                    Hash = g.Key,
                    Indices = g.Value,
                    Count = g.Value.Count
                })
                .ToList();

            return new BatchFingerprintResult
            {
                Fingerprints = results.Select(r => r.Fingerprint).ToList(),
                TotalCount = results.Count,
                UniqueCount = hashGroups.Count,
                DuplicateGroups = duplicates,
                DuplicateRate = results.Count == 0 ? 0 : 1.0 - (double)hashGroups.Count / results.Count
            };
        }

        /// <summary>
        /// Finds prompts similar to a query prompt from a collection, using Jaccard similarity.
        /// Requires <see cref="PromptFingerprintOptions.EnableSimilarity"/> to be true.
        /// </summary>
        /// <param name="query">The query prompt.</param>
        /// <param name="candidates">Candidate prompts to compare against.</param>
        /// <param name="threshold">Minimum similarity score (0.0–1.0) to include. Default 0.3.</param>
        /// <param name="options">Optional configuration.</param>
        /// <returns>Candidates above the threshold, sorted by similarity descending.</returns>
        public List<SimilarityMatch> FindSimilar(string query, IEnumerable<string> candidates,
            double threshold = 0.3, PromptFingerprintOptions? options = null)
        {
            options ??= new PromptFingerprintOptions();
            options.EnableSimilarity = true;

            var queryFp = Fingerprint(query, options);
            var matches = new List<SimilarityMatch>();
            int index = 0;

            foreach (var candidate in candidates)
            {
                var candidateFp = Fingerprint(candidate, options);
                var similarity = queryFp.SimilarityTo(candidateFp);

                if (similarity >= threshold)
                {
                    matches.Add(new SimilarityMatch
                    {
                        Index = index,
                        Text = candidate,
                        Similarity = similarity,
                        Fingerprint = candidateFp,
                        IsExactMatch = queryFp.Matches(candidateFp)
                    });
                }

                index++;
            }

            return matches.OrderByDescending(m => m.Similarity).ToList();
        }

        /// <summary>
        /// Computes a diff between two fingerprints, highlighting what changed.
        /// </summary>
        public FingerprintDiff Diff(string before, string after, PromptFingerprintOptions? options = null)
        {
            options ??= new PromptFingerprintOptions { EnableSimilarity = true };
            options.EnableSimilarity = true;

            var fpBefore = Fingerprint(before, options);
            var fpAfter = Fingerprint(after, options);

            var wordsBefore = new HashSet<string>(GetWords(Normalize(before, options)));
            var wordsAfter = new HashSet<string>(GetWords(Normalize(after, options)));

            var added = wordsAfter.Except(wordsBefore).ToList();
            var removed = wordsBefore.Except(wordsAfter).ToList();
            var shared = wordsBefore.Intersect(wordsAfter).Count();

            return new FingerprintDiff
            {
                Before = fpBefore,
                After = fpAfter,
                IsChanged = !fpBefore.Matches(fpAfter),
                Similarity = fpBefore.SimilarityTo(fpAfter),
                AddedWords = added,
                RemovedWords = removed,
                SharedWordCount = shared,
                LengthDelta = fpAfter.NormalizedLength - fpBefore.NormalizedLength,
                WordCountDelta = fpAfter.WordCount - fpBefore.WordCount
            };
        }

        internal string Normalize(string text, PromptFingerprintOptions options)
        {
            var result = text;

            if (options.Normalization >= NormalizationLevel.Whitespace)
            {
                result = Regex.Replace(result, @"\s+", " ").Trim();
            }

            if (options.Normalization >= NormalizationLevel.CaseInsensitive)
            {
                result = result.ToLowerInvariant();
            }

            if (options.Normalization >= NormalizationLevel.Structural)
            {
                result = Regex.Replace(result, @"[^\w\s]", "");
                result = Regex.Replace(result, @"\s+", " ").Trim();

                var stopWords = options.StopWords ?? DefaultStopWords;
                if (stopWords.Count > 0)
                {
                    var words = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    result = string.Join(" ", words.Where(w => !stopWords.Contains(w)));
                }
            }

            if (options.Normalization >= NormalizationLevel.Semantic)
            {
                var words = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Array.Sort(words, StringComparer.Ordinal);
                result = string.Join(" ", words);
            }

            return result;
        }

        private static string ComputeHash(string text)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string[] GetWords(string text)
        {
            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static HashSet<int> ComputeShingles(string[] words, int shingleSize)
        {
            var shingles = new HashSet<int>();
            if (words.Length < shingleSize)
            {
                shingles.Add(string.Join(" ", words).GetHashCode());
                return shingles;
            }

            for (int i = 0; i <= words.Length - shingleSize; i++)
            {
                var shingle = string.Join(" ", words.Skip(i).Take(shingleSize));
                shingles.Add(shingle.GetHashCode());
            }

            return shingles;
        }
    }

    /// <summary>Result of batch fingerprinting.</summary>
    public class BatchFingerprintResult
    {
        /// <summary>Gets the fingerprints for each prompt.</summary>
        [JsonPropertyName("fingerprints")]
        public List<PromptFingerprint> Fingerprints { get; init; } = new();

        /// <summary>Gets the total number of prompts processed.</summary>
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; init; }

        /// <summary>Gets the number of unique prompts.</summary>
        [JsonPropertyName("uniqueCount")]
        public int UniqueCount { get; init; }

        /// <summary>Gets the duplicate groups found.</summary>
        [JsonPropertyName("duplicateGroups")]
        public List<DuplicateGroup> DuplicateGroups { get; init; } = new();

        /// <summary>Gets the fraction of prompts that are duplicates (0.0–1.0).</summary>
        [JsonPropertyName("duplicateRate")]
        public double DuplicateRate { get; init; }
    }

    /// <summary>A group of duplicate prompts sharing the same fingerprint hash.</summary>
    public class DuplicateGroup
    {
        /// <summary>Gets the shared hash.</summary>
        [JsonPropertyName("hash")]
        public string Hash { get; init; } = "";

        /// <summary>Gets the indices of duplicates in the original list.</summary>
        [JsonPropertyName("indices")]
        public List<int> Indices { get; init; } = new();

        /// <summary>Gets the number of duplicates.</summary>
        [JsonPropertyName("count")]
        public int Count { get; init; }
    }

    /// <summary>A similarity match result.</summary>
    public class SimilarityMatch
    {
        /// <summary>Gets the index in the candidates list.</summary>
        [JsonPropertyName("index")]
        public int Index { get; init; }

        /// <summary>Gets the candidate text.</summary>
        [JsonPropertyName("text")]
        public string Text { get; init; } = "";

        /// <summary>Gets the Jaccard similarity score (0.0–1.0).</summary>
        [JsonPropertyName("similarity")]
        public double Similarity { get; init; }

        /// <summary>Gets the candidate's fingerprint.</summary>
        [JsonIgnore]
        public PromptFingerprint Fingerprint { get; init; } = new();

        /// <summary>Gets whether this is an exact hash match.</summary>
        [JsonPropertyName("isExactMatch")]
        public bool IsExactMatch { get; init; }
    }

    /// <summary>Result of comparing two fingerprints.</summary>
    public class FingerprintDiff
    {
        /// <summary>Gets the before fingerprint.</summary>
        [JsonPropertyName("before")]
        public PromptFingerprint Before { get; init; } = new();

        /// <summary>Gets the after fingerprint.</summary>
        [JsonPropertyName("after")]
        public PromptFingerprint After { get; init; } = new();

        /// <summary>Gets whether the content changed.</summary>
        [JsonPropertyName("isChanged")]
        public bool IsChanged { get; init; }

        /// <summary>Gets the similarity between before and after.</summary>
        [JsonPropertyName("similarity")]
        public double Similarity { get; init; }

        /// <summary>Gets words added in the after version.</summary>
        [JsonPropertyName("addedWords")]
        public List<string> AddedWords { get; init; } = new();

        /// <summary>Gets words removed from the before version.</summary>
        [JsonPropertyName("removedWords")]
        public List<string> RemovedWords { get; init; } = new();

        /// <summary>Gets the count of words shared between versions.</summary>
        [JsonPropertyName("sharedWordCount")]
        public int SharedWordCount { get; init; }

        /// <summary>Gets the change in normalized length.</summary>
        [JsonPropertyName("lengthDelta")]
        public int LengthDelta { get; init; }

        /// <summary>Gets the change in word count.</summary>
        [JsonPropertyName("wordCountDelta")]
        public int WordCountDelta { get; init; }

        /// <summary>Gets a human-readable summary of the diff.</summary>
        public string Summary()
        {
            if (!IsChanged) return "No changes detected.";
            var parts = new List<string>();
            parts.Add($"Similarity: {Similarity:P1}");
            if (WordCountDelta != 0)
                parts.Add($"Word count: {(WordCountDelta > 0 ? "+" : "")}{WordCountDelta}");
            if (AddedWords.Count > 0)
                parts.Add($"Added: {string.Join(", ", AddedWords.Take(10))}{(AddedWords.Count > 10 ? "..." : "")}");
            if (RemovedWords.Count > 0)
                parts.Add($"Removed: {string.Join(", ", RemovedWords.Take(10))}{(RemovedWords.Count > 10 ? "..." : "")}");
            return string.Join(" | ", parts);
        }
    }
}
