namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies the string similarity metric to use for prompt comparison.
    /// </summary>
    public enum SimilarityMetric
    {
        /// <summary>Normalized Levenshtein edit distance (character-level).</summary>
        Levenshtein,

        /// <summary>Jaccard index over word-level n-grams.</summary>
        Jaccard,

        /// <summary>Cosine similarity over word frequency vectors.</summary>
        Cosine,

        /// <summary>Dice/Sørensen coefficient over bigrams.</summary>
        Dice,

        /// <summary>Longest common subsequence ratio.</summary>
        LCS
    }

    /// <summary>
    /// Result of comparing two prompts.
    /// </summary>
    public class SimilarityResult
    {
        [JsonPropertyName("promptA")]
        public string PromptA { get; set; } = "";

        [JsonPropertyName("promptB")]
        public string PromptB { get; set; } = "";

        [JsonPropertyName("metric")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SimilarityMetric Metric { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("isDuplicate")]
        public bool IsDuplicate { get; set; }

        /// <summary>Shared words between the two prompts.</summary>
        [JsonPropertyName("sharedTokens")]
        public List<string> SharedTokens { get; set; } = new();

        /// <summary>Words unique to prompt A.</summary>
        [JsonPropertyName("uniqueToA")]
        public List<string> UniqueToA { get; set; } = new();

        /// <summary>Words unique to prompt B.</summary>
        [JsonPropertyName("uniqueToB")]
        public List<string> UniqueToB { get; set; } = new();
    }

    /// <summary>
    /// A cluster of similar prompts grouped by the analyzer.
    /// </summary>
    public class SimilarityCluster
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("representative")]
        public string Representative { get; set; } = "";

        [JsonPropertyName("members")]
        public List<string> Members { get; set; } = new();

        [JsonPropertyName("averageSimilarity")]
        public double AverageSimilarity { get; set; }
    }

    /// <summary>
    /// Report from a batch duplicate scan.
    /// </summary>
    public class DuplicateReport
    {
        [JsonPropertyName("totalPrompts")]
        public int TotalPrompts { get; set; }

        [JsonPropertyName("duplicatePairs")]
        public List<SimilarityResult> DuplicatePairs { get; set; } = new();

        [JsonPropertyName("clusters")]
        public List<SimilarityCluster> Clusters { get; set; } = new();

        [JsonPropertyName("uniqueCount")]
        public int UniqueCount { get; set; }

        [JsonPropertyName("duplicateRate")]
        public double DuplicateRate { get; set; }

        [JsonPropertyName("metric")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SimilarityMetric Metric { get; set; }

        [JsonPropertyName("threshold")]
        public double Threshold { get; set; }

        /// <summary>Generate a human-readable text report.</summary>
        public string ToTextReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Prompt Similarity / Duplicate Report ===");
            sb.AppendLine($"Total prompts:   {TotalPrompts}");
            sb.AppendLine($"Unique prompts:  {UniqueCount}");
            sb.AppendLine($"Duplicate rate:  {DuplicateRate:P1}");
            sb.AppendLine($"Metric:          {Metric}");
            sb.AppendLine($"Threshold:       {Threshold:F2}");
            sb.AppendLine();

            if (DuplicatePairs.Count > 0)
            {
                sb.AppendLine($"--- Duplicate Pairs ({DuplicatePairs.Count}) ---");
                foreach (var pair in DuplicatePairs)
                {
                    var a = Truncate(pair.PromptA, 50);
                    var b = Truncate(pair.PromptB, 50);
                    sb.AppendLine($"  [{pair.Score:F3}] \"{a}\" <-> \"{b}\"");
                }
                sb.AppendLine();
            }

            if (Clusters.Count > 0)
            {
                sb.AppendLine($"--- Clusters ({Clusters.Count}) ---");
                foreach (var c in Clusters)
                {
                    sb.AppendLine($"  Cluster {c.Id} ({c.Members.Count} members, avg={c.AverageSimilarity:F3}):");
                    sb.AppendLine($"    Representative: \"{Truncate(c.Representative, 60)}\"");
                    foreach (var m in c.Members.Take(5))
                        sb.AppendLine($"    - \"{Truncate(m, 60)}\"");
                    if (c.Members.Count > 5)
                        sb.AppendLine($"    ... and {c.Members.Count - 5} more");
                }
            }

            return sb.ToString();
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max] + "...";

        /// <summary>Serialize to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Compares prompts for similarity using multiple string distance metrics,
    /// detects near-duplicates in a collection, and clusters similar prompts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Useful for maintaining prompt libraries: find redundant prompts, identify
    /// clusters of similar templates, and measure how much two prompts overlap.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var analyzer = new PromptSimilarityAnalyzer();
    ///
    /// // Compare two prompts
    /// var result = analyzer.Compare("Summarize this article", "Summarize the article");
    /// // result.Score ~= 0.85 (Levenshtein)
    ///
    /// // Find duplicates in a library
    /// var prompts = new[] { "Translate to French", "Translate into French", "Summarize" };
    /// var report = analyzer.FindDuplicates(prompts, threshold: 0.8);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptSimilarityAnalyzer
    {
        private readonly SimilarityMetric _defaultMetric;
        private readonly double _defaultThreshold;
        private readonly bool _caseSensitive;

        /// <summary>
        /// Creates a new similarity analyzer.
        /// </summary>
        /// <param name="defaultMetric">Default metric for comparisons.</param>
        /// <param name="defaultThreshold">Default duplicate threshold (0.0–1.0).</param>
        /// <param name="caseSensitive">Whether comparisons are case-sensitive.</param>
        public PromptSimilarityAnalyzer(
            SimilarityMetric defaultMetric = SimilarityMetric.Levenshtein,
            double defaultThreshold = 0.8,
            bool caseSensitive = false)
        {
            if (defaultThreshold < 0.0 || defaultThreshold > 1.0)
                throw new ArgumentOutOfRangeException(nameof(defaultThreshold), "Threshold must be between 0.0 and 1.0.");

            _defaultMetric = defaultMetric;
            _defaultThreshold = defaultThreshold;
            _caseSensitive = caseSensitive;
        }

        /// <summary>
        /// Compare two prompts using a specific metric.
        /// </summary>
        public SimilarityResult Compare(string a, string b, SimilarityMetric? metric = null, double? threshold = null)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            var m = metric ?? _defaultMetric;
            var t = threshold ?? _defaultThreshold;
            var na = Normalize(a);
            var nb = Normalize(b);

            var score = ComputeSimilarity(na, nb, m);
            var tokensA = Tokenize(na);
            var tokensB = Tokenize(nb);
            var setA = new HashSet<string>(tokensA);
            var setB = new HashSet<string>(tokensB);

            return new SimilarityResult
            {
                PromptA = a,
                PromptB = b,
                Metric = m,
                Score = score,
                IsDuplicate = score >= t,
                SharedTokens = setA.Intersect(setB).OrderBy(x => x).ToList(),
                UniqueToA = setA.Except(setB).OrderBy(x => x).ToList(),
                UniqueToB = setB.Except(setA).OrderBy(x => x).ToList()
            };
        }

        /// <summary>
        /// Compare a prompt against all metrics and return results for each.
        /// </summary>
        public List<SimilarityResult> CompareAllMetrics(string a, string b, double? threshold = null)
        {
            return Enum.GetValues<SimilarityMetric>()
                .Select(m => Compare(a, b, m, threshold))
                .ToList();
        }

        /// <summary>
        /// Find the most similar prompt in a collection.
        /// </summary>
        public SimilarityResult? FindMostSimilar(string prompt, IEnumerable<string> candidates, SimilarityMetric? metric = null)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            SimilarityResult? best = null;
            foreach (var c in candidates)
            {
                if (c == prompt) continue;
                var result = Compare(prompt, c, metric);
                if (best == null || result.Score > best.Score)
                    best = result;
            }
            return best;
        }

        /// <summary>
        /// Find all near-duplicate pairs in a collection of prompts.
        /// </summary>
        public DuplicateReport FindDuplicates(IEnumerable<string> prompts, double? threshold = null, SimilarityMetric? metric = null)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));

            var list = prompts.ToList();
            var t = threshold ?? _defaultThreshold;
            var m = metric ?? _defaultMetric;
            var pairs = new List<SimilarityResult>();

            // Pre-normalize all strings once to avoid redundant work in O(n²) loop
            var normalized = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                normalized[i] = Normalize(list[i]);

            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var score = ComputeSimilarity(normalized[i], normalized[j], m);
                    if (score >= t)
                    {
                        var tokensA = Tokenize(normalized[i]);
                        var tokensB = Tokenize(normalized[j]);
                        var setA = new HashSet<string>(tokensA);
                        var setB = new HashSet<string>(tokensB);

                        pairs.Add(new SimilarityResult
                        {
                            PromptA = list[i],
                            PromptB = list[j],
                            Metric = m,
                            Score = score,
                            IsDuplicate = true,
                            SharedTokens = setA.Intersect(setB).OrderBy(x => x).ToList(),
                            UniqueToA = setA.Except(setB).OrderBy(x => x).ToList(),
                            UniqueToB = setB.Except(setA).OrderBy(x => x).ToList()
                        });
                    }
                }
            }

            var clusters = BuildClusters(list, pairs, t);
            var involvedInDuplicates = new HashSet<string>();
            foreach (var p in pairs)
            {
                involvedInDuplicates.Add(p.PromptA);
                involvedInDuplicates.Add(p.PromptB);
            }

            var uniqueCount = list.Count - involvedInDuplicates.Count + clusters.Count;

            return new DuplicateReport
            {
                TotalPrompts = list.Count,
                DuplicatePairs = pairs.OrderByDescending(p => p.Score).ToList(),
                Clusters = clusters,
                UniqueCount = Math.Max(uniqueCount, 0),
                DuplicateRate = list.Count > 0 ? (double)involvedInDuplicates.Count / list.Count : 0.0,
                Metric = m,
                Threshold = t
            };
        }

        /// <summary>
        /// Rank a collection of prompts by similarity to a reference prompt.
        /// </summary>
        public List<SimilarityResult> RankBySimilarity(string reference, IEnumerable<string> candidates, SimilarityMetric? metric = null)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            return candidates
                .Where(c => c != reference)
                .Select(c => Compare(reference, c, metric))
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        /// <summary>
        /// Compute pairwise similarity matrix for a collection of prompts.
        /// Returns a 2D array where matrix[i][j] is the similarity between prompts[i] and prompts[j].
        /// </summary>
        public double[,] SimilarityMatrix(IList<string> prompts, SimilarityMetric? metric = null)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));

            var n = prompts.Count;
            var matrix = new double[n, n];
            var m = metric ?? _defaultMetric;

            // Pre-normalize once to avoid O(n²) redundant Normalize() calls
            var normalized = new string[n];
            for (int i = 0; i < n; i++)
                normalized[i] = Normalize(prompts[i]);

            for (int i = 0; i < n; i++)
            {
                matrix[i, i] = 1.0;
                for (int j = i + 1; j < n; j++)
                {
                    var score = ComputeSimilarity(normalized[i], normalized[j], m);
                    matrix[i, j] = score;
                    matrix[j, i] = score;
                }
            }

            return matrix;
        }

        /// <summary>
        /// Export similarity matrix as JSON.
        /// </summary>
        public string MatrixToJson(IList<string> prompts, SimilarityMetric? metric = null)
        {
            var matrix = SimilarityMatrix(prompts, metric);
            var n = prompts.Count;
            var rows = new List<Dictionary<string, object>>();

            for (int i = 0; i < n; i++)
            {
                var row = new Dictionary<string, object> { ["prompt"] = prompts[i] };
                for (int j = 0; j < n; j++)
                    row[$"sim_{j}"] = Math.Round(matrix[i, j], 4);
                rows.Add(row);
            }

            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
        }

        // ── Similarity Algorithms ──

        internal double ComputeSimilarity(string a, string b, SimilarityMetric metric)
        {
            if (a == b) return 1.0;
            if (a.Length == 0 && b.Length == 0) return 1.0;
            if (a.Length == 0 || b.Length == 0) return 0.0;

            return metric switch
            {
                SimilarityMetric.Levenshtein => LevenshteinSimilarity(a, b),
                SimilarityMetric.Jaccard => JaccardSimilarity(a, b),
                SimilarityMetric.Cosine => CosineSimilarity(a, b),
                SimilarityMetric.Dice => DiceSimilarity(a, b),
                SimilarityMetric.LCS => LCSSimilarity(a, b),
                _ => throw new ArgumentOutOfRangeException(nameof(metric))
            };
        }

        private double LevenshteinSimilarity(string a, string b)
        {
            var maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0) return 1.0;
            return 1.0 - (double)LevenshteinDistance(a, b) / maxLen;
        }

        private static int LevenshteinDistance(string a, string b)
        {
            // Ensure we iterate over the shorter string in the inner loop
            // to minimize memory: O(min(m,n)) instead of O(m*n).
            if (a.Length < b.Length)
            {
                var tmp = a; a = b; b = tmp;
            }

            var m = a.Length;
            var n = b.Length;
            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int j = 0; j <= n; j++) prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= n; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(prev[j] + 1, curr[j - 1] + 1),
                        prev[j - 1] + cost);
                }
                var swap = prev; prev = curr; curr = swap;
            }

            return prev[n];
        }

        private double JaccardSimilarity(string a, string b)
        {
            var setA = new HashSet<string>(Tokenize(a));
            var setB = new HashSet<string>(Tokenize(b));

            if (setA.Count == 0 && setB.Count == 0) return 1.0;

            var intersection = setA.Intersect(setB).Count();
            var union = setA.Union(setB).Count();

            return union == 0 ? 1.0 : (double)intersection / union;
        }

        private double CosineSimilarity(string a, string b)
        {
            var freqA = WordFrequency(Tokenize(a));
            var freqB = WordFrequency(Tokenize(b));

            var allWords = new HashSet<string>(freqA.Keys);
            allWords.UnionWith(freqB.Keys);

            if (allWords.Count == 0) return 1.0;

            double dot = 0, magA = 0, magB = 0;
            foreach (var w in allWords)
            {
                freqA.TryGetValue(w, out var va);
                freqB.TryGetValue(w, out var vb);
                dot += va * vb;
                magA += va * va;
                magB += vb * vb;
            }

            var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
            return denom == 0 ? 0.0 : dot / denom;
        }

        private double DiceSimilarity(string a, string b)
        {
            var bigramsA = CharBigrams(a);
            var bigramsB = CharBigrams(b);

            if (bigramsA.Count == 0 && bigramsB.Count == 0) return 1.0;

            var intersection = bigramsA.Intersect(bigramsB).Count();
            var total = bigramsA.Count + bigramsB.Count;

            return total == 0 ? 1.0 : 2.0 * intersection / total;
        }

        private double LCSSimilarity(string a, string b)
        {
            var maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0) return 1.0;
            return (double)LCSLength(a, b) / maxLen;
        }

        private static int LCSLength(string a, string b)
        {
            // Two-row DP: O(min(m,n)) memory instead of O(m*n).
            if (a.Length < b.Length)
            {
                var tmp = a; a = b; b = tmp;
            }

            var m = a.Length;
            var n = b.Length;
            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    curr[j] = a[i - 1] == b[j - 1]
                        ? prev[j - 1] + 1
                        : Math.Max(prev[j], curr[j - 1]);
                }
                var swap = prev; prev = curr; curr = swap;
                Array.Clear(curr, 0, curr.Length);
            }

            return prev[n];
        }

        // ── Helpers ──

        private string Normalize(string s) =>
            _caseSensitive ? s.Trim() : s.Trim().ToLowerInvariant();

        private static string[] Tokenize(string s) =>
            s.Split(new[] { ' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries);

        private static Dictionary<string, int> WordFrequency(string[] tokens)
        {
            var freq = new Dictionary<string, int>();
            foreach (var t in tokens)
            {
                freq.TryGetValue(t, out var count);
                freq[t] = count + 1;
            }
            return freq;
        }

        private static HashSet<string> CharBigrams(string s)
        {
            var bigrams = new HashSet<string>();
            for (int i = 0; i < s.Length - 1; i++)
                bigrams.Add(s.Substring(i, 2));
            return bigrams;
        }

        private List<SimilarityCluster> BuildClusters(List<string> prompts, List<SimilarityResult> pairs, double threshold)
        {
            // Union-Find clustering
            var parent = new Dictionary<string, string>();

            string Find(string x)
            {
                if (!parent.ContainsKey(x)) parent[x] = x;
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]]; // path compression
                    x = parent[x];
                }
                return x;
            }

            void Union(string a, string b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }

            foreach (var p in pairs)
            {
                Union(p.PromptA, p.PromptB);
            }

            // Group by root
            var groups = new Dictionary<string, List<string>>();
            foreach (var p in pairs)
            {
                var rootA = Find(p.PromptA);
                if (!groups.ContainsKey(rootA)) groups[rootA] = new List<string>();
                if (!groups[rootA].Contains(p.PromptA)) groups[rootA].Add(p.PromptA);

                var rootB = Find(p.PromptB);
                // After union, rootA and rootB should be the same
                if (!groups[rootA].Contains(p.PromptB)) groups[rootA].Add(p.PromptB);
            }

            var clusters = new List<SimilarityCluster>();
            int id = 1;
            foreach (var (root, members) in groups)
            {
                // Average pairwise similarity within the cluster
                double totalSim = 0;
                int count = 0;
                for (int i = 0; i < members.Count; i++)
                {
                    for (int j = i + 1; j < members.Count; j++)
                    {
                        var na = Normalize(members[i]);
                        var nb = Normalize(members[j]);
                        totalSim += ComputeSimilarity(na, nb, _defaultMetric);
                        count++;
                    }
                }

                clusters.Add(new SimilarityCluster
                {
                    Id = id++,
                    Representative = members[0],
                    Members = members,
                    AverageSimilarity = count > 0 ? totalSim / count : 1.0
                });
            }

            return clusters.OrderByDescending(c => c.Members.Count).ToList();
        }
    }
}
