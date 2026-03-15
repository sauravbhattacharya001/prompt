namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Strategy for compressing conversation context.
    /// </summary>
    public enum CompressionStrategy
    {
        /// <summary>Keep anchors (system + first N + last N turns), summarize middle.</summary>
        AnchorWindow,

        /// <summary>Score messages by importance and drop lowest-scoring ones.</summary>
        ImportanceScoring,

        /// <summary>Detect near-duplicate messages and merge them.</summary>
        Deduplication,

        /// <summary>Apply all strategies in sequence for maximum compression.</summary>
        Aggressive
    }

    /// <summary>
    /// Configuration for context compression.
    /// </summary>
    public class CompressionOptions
    {
        /// <summary>Target token budget for the compressed output.</summary>
        public int TargetTokens { get; set; } = 4000;

        /// <summary>Strategy to use for compression.</summary>
        public CompressionStrategy Strategy { get; set; } = CompressionStrategy.AnchorWindow;

        /// <summary>Number of initial turns to always keep (anchor window).</summary>
        public int KeepFirstTurns { get; set; } = 2;

        /// <summary>Number of recent turns to always keep (anchor window).</summary>
        public int KeepLastTurns { get; set; } = 5;

        /// <summary>Minimum importance score (0.0-1.0) to keep a message.</summary>
        public double ImportanceThreshold { get; set; } = 0.3;

        /// <summary>Similarity threshold (0.0-1.0) for deduplication. Higher = stricter.</summary>
        public double SimilarityThreshold { get; set; } = 0.7;

        /// <summary>Keywords that boost message importance when present.</summary>
        public List<string> ImportantKeywords { get; set; } = new();

        /// <summary>Roles that should never be removed.</summary>
        public List<string> ProtectedRoles { get; set; } = new() { "system" };

        /// <summary>Text to use when summarizing removed messages.</summary>
        public string SummaryPlaceholder { get; set; } = "[{count} message(s) compressed]";

        /// <summary>Whether to preserve messages containing code blocks.</summary>
        public bool PreserveCodeBlocks { get; set; } = true;

        /// <summary>Whether to preserve messages containing URLs.</summary>
        public bool PreserveUrls { get; set; } = false;

        /// <summary>Recency weight factor (0.0-1.0). Higher = recent messages score higher.</summary>
        public double RecencyWeight { get; set; } = 0.4;

        /// <summary>Length weight factor (0.0-1.0). Higher = longer messages score higher.</summary>
        public double LengthWeight { get; set; } = 0.2;

        /// <summary>Role weight factor (0.0-1.0). Higher = assistant/system messages score higher.</summary>
        public double RoleWeight { get; set; } = 0.2;

        /// <summary>Keyword weight factor (0.0-1.0). Higher = keyword-containing messages score higher.</summary>
        public double KeywordWeight { get; set; } = 0.2;

        /// <summary>
        /// Creates a deep copy of these options with an optional strategy override.
        /// </summary>
        /// <param name="strategy">If provided, overrides the Strategy on the clone.</param>
        /// <returns>A new <see cref="CompressionOptions"/> with all fields copied.</returns>
        public CompressionOptions Clone(CompressionStrategy? strategy = null)
        {
            return new CompressionOptions
            {
                TargetTokens = TargetTokens,
                Strategy = strategy ?? Strategy,
                KeepFirstTurns = KeepFirstTurns,
                KeepLastTurns = KeepLastTurns,
                ImportanceThreshold = ImportanceThreshold,
                SimilarityThreshold = SimilarityThreshold,
                ImportantKeywords = new List<string>(ImportantKeywords),
                ProtectedRoles = new List<string>(ProtectedRoles),
                SummaryPlaceholder = SummaryPlaceholder,
                PreserveCodeBlocks = PreserveCodeBlocks,
                PreserveUrls = PreserveUrls,
                RecencyWeight = RecencyWeight,
                LengthWeight = LengthWeight,
                RoleWeight = RoleWeight,
                KeywordWeight = KeywordWeight,
            };
        }
    }

    /// <summary>
    /// A scored message with its importance metadata.
    /// </summary>
    public class ScoredMessage
    {
        /// <summary>Original index in the conversation.</summary>
        public int Index { get; set; }

        /// <summary>The message role.</summary>
        public string Role { get; set; } = "";

        /// <summary>The message content.</summary>
        public string Content { get; set; } = "";

        /// <summary>Computed importance score (0.0-1.0).</summary>
        public double ImportanceScore { get; set; }

        /// <summary>Whether this message was kept in compression.</summary>
        public bool Kept { get; set; } = true;

        /// <summary>Reason the message was removed or kept.</summary>
        public string Reason { get; set; } = "";

        /// <summary>Estimated token count.</summary>
        public int EstimatedTokens { get; set; }
    }

    /// <summary>
    /// Result of a context compression operation.
    /// </summary>
    public class CompressionResult
    {
        /// <summary>The compressed messages.</summary>
        public List<(string Role, string Content)> Messages { get; set; } = new();

        /// <summary>Original message count.</summary>
        public int OriginalCount { get; set; }

        /// <summary>Compressed message count.</summary>
        public int CompressedCount { get; set; }

        /// <summary>Messages removed.</summary>
        public int RemovedCount => OriginalCount - CompressedCount;

        /// <summary>Estimated tokens before compression.</summary>
        public int OriginalTokens { get; set; }

        /// <summary>Estimated tokens after compression.</summary>
        public int CompressedTokens { get; set; }

        /// <summary>Tokens saved.</summary>
        public int TokensSaved => OriginalTokens - CompressedTokens;

        /// <summary>Compression ratio (0.0-1.0).</summary>
        public double CompressionRatio => OriginalTokens > 0
            ? Math.Round(1.0 - (double)CompressedTokens / OriginalTokens, 3)
            : 0;

        /// <summary>Strategy used.</summary>
        public CompressionStrategy Strategy { get; set; }

        /// <summary>Detailed per-message scoring information.</summary>
        public List<ScoredMessage> ScoringDetails { get; set; } = new();

        /// <summary>Number of duplicate groups found (deduplication strategy).</summary>
        public int DuplicateGroupsFound { get; set; }

        /// <summary>Whether the target budget was met.</summary>
        public bool BudgetMet { get; set; }

        /// <summary>Generates a human-readable summary.</summary>
        public string Summary()
        {
            var lines = new List<string>
            {
                $"Context Compression Report ({Strategy})",
                $"  Messages: {OriginalCount} → {CompressedCount} ({RemovedCount} removed)",
                $"  Tokens:   {OriginalTokens} → {CompressedTokens} ({TokensSaved} saved, {CompressionRatio * 100:F1}%)",
                $"  Budget:   {(BudgetMet ? "✓ Met" : "✗ Not met")}"
            };
            if (DuplicateGroupsFound > 0)
                lines.Add($"  Duplicates: {DuplicateGroupsFound} group(s) merged");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Exports the result to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// Intelligently compresses conversation context to fit within token budgets.
    /// Goes beyond simple truncation by scoring message importance, detecting
    /// duplicates, and preserving critical anchors (system prompts, recent turns).
    /// </summary>
    public class PromptContextCompressor
    {
        private readonly CompressionOptions _options;

        /// <summary>
        /// Creates a compressor with default options.
        /// </summary>
        public PromptContextCompressor() : this(new CompressionOptions()) { }

        /// <summary>
        /// Creates a compressor with the given options.
        /// </summary>
        public PromptContextCompressor(CompressionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Compresses a list of messages to fit within the target token budget.
        /// </summary>
        /// <param name="messages">Messages as (role, content) tuples.</param>
        /// <returns>Compression result with the compressed messages and metadata.</returns>
        public CompressionResult Compress(List<(string Role, string Content)> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            if (messages.Count == 0)
                return new CompressionResult
                {
                    Messages = new(),
                    Strategy = _options.Strategy,
                    BudgetMet = true
                };

            return _options.Strategy switch
            {
                CompressionStrategy.AnchorWindow => CompressAnchorWindow(messages),
                CompressionStrategy.ImportanceScoring => CompressImportanceScoring(messages),
                CompressionStrategy.Deduplication => CompressDeduplication(messages),
                CompressionStrategy.Aggressive => CompressAggressive(messages),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        /// <summary>
        /// Scores all messages by importance without removing any.
        /// Useful for inspection before compression.
        /// </summary>
        public List<ScoredMessage> ScoreMessages(List<(string Role, string Content)> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            return messages.Select((m, i) => ScoreMessage(m.Role, m.Content, i, messages.Count))
                           .ToList();
        }

        /// <summary>
        /// Finds groups of near-duplicate messages.
        /// </summary>
        public List<List<int>> FindDuplicates(List<(string Role, string Content)> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            return FindDuplicateGroups(messages);
        }

        /// <summary>
        /// Estimates whether the messages fit within the target budget.
        /// </summary>
        public bool FitsInBudget(List<(string Role, string Content)> messages)
        {
            if (messages == null) return true;
            int total = messages.Sum(m => PromptGuard.EstimateTokens(m.Content));
            return total <= _options.TargetTokens;
        }

        /// <summary>
        /// Creates a builder for fluent configuration.
        /// </summary>
        public static CompressorBuilder Builder() => new();

        // --- Strategies ---

        private CompressionResult CompressAnchorWindow(List<(string Role, string Content)> messages)
        {
            var scored = ScoreMessages(messages);
            int totalTokens = scored.Sum(s => s.EstimatedTokens);

            if (totalTokens <= _options.TargetTokens)
                return BuildResult(messages, scored, scored.Select(s => s.Index).ToHashSet(), totalTokens, 0);

            var keepIndices = new HashSet<int>();

            // Always keep protected roles
            for (int i = 0; i < messages.Count; i++)
            {
                if (_options.ProtectedRoles.Contains(messages[i].Role, StringComparer.OrdinalIgnoreCase))
                {
                    keepIndices.Add(i);
                    scored[i].Reason = "Protected role";
                }
            }

            // Keep first N turns
            for (int i = 0; i < Math.Min(_options.KeepFirstTurns, messages.Count); i++)
            {
                keepIndices.Add(i);
                if (string.IsNullOrEmpty(scored[i].Reason)) scored[i].Reason = "Anchor (first)";
            }

            // Keep last N turns
            for (int i = Math.Max(0, messages.Count - _options.KeepLastTurns); i < messages.Count; i++)
            {
                keepIndices.Add(i);
                if (string.IsNullOrEmpty(scored[i].Reason)) scored[i].Reason = "Anchor (last)";
            }

            // Mark removed messages
            int removedCount = 0;
            foreach (var s in scored)
            {
                if (!keepIndices.Contains(s.Index))
                {
                    s.Kept = false;
                    s.Reason = "Outside anchor window";
                    removedCount++;
                }
            }

            return BuildResultWithPlaceholders(messages, scored, keepIndices, totalTokens, removedCount);
        }

        private CompressionResult CompressImportanceScoring(List<(string Role, string Content)> messages)
        {
            var scored = ScoreMessages(messages);
            int totalTokens = scored.Sum(s => s.EstimatedTokens);

            if (totalTokens <= _options.TargetTokens)
                return BuildResult(messages, scored, scored.Select(s => s.Index).ToHashSet(), totalTokens, 0);

            var keepIndices = new HashSet<int>();

            // Always keep protected roles
            for (int i = 0; i < messages.Count; i++)
            {
                if (_options.ProtectedRoles.Contains(messages[i].Role, StringComparer.OrdinalIgnoreCase))
                {
                    keepIndices.Add(i);
                    scored[i].Reason = "Protected role";
                }
            }

            // Sort non-protected by importance, keep until budget filled
            var candidates = scored
                .Where(s => !keepIndices.Contains(s.Index))
                .OrderByDescending(s => s.ImportanceScore)
                .ToList();

            int currentTokens = scored.Where(s => keepIndices.Contains(s.Index)).Sum(s => s.EstimatedTokens);

            foreach (var c in candidates)
            {
                if (currentTokens + c.EstimatedTokens <= _options.TargetTokens)
                {
                    keepIndices.Add(c.Index);
                    currentTokens += c.EstimatedTokens;
                    c.Reason = $"Importance {c.ImportanceScore:F2} (kept)";
                }
                else
                {
                    c.Kept = false;
                    c.Reason = $"Importance {c.ImportanceScore:F2} (dropped, budget)";
                }
            }

            // Also drop anything below threshold even if budget allows
            foreach (var c in scored)
            {
                if (c.Kept && !_options.ProtectedRoles.Contains(c.Role, StringComparer.OrdinalIgnoreCase)
                    && c.ImportanceScore < _options.ImportanceThreshold)
                {
                    c.Kept = false;
                    keepIndices.Remove(c.Index);
                    c.Reason = $"Below threshold ({c.ImportanceScore:F2} < {_options.ImportanceThreshold})";
                }
            }

            return BuildResultWithPlaceholders(messages, scored, keepIndices, totalTokens, scored.Count(s => !s.Kept));
        }

        private CompressionResult CompressDeduplication(List<(string Role, string Content)> messages)
        {
            var scored = ScoreMessages(messages);
            int totalTokens = scored.Sum(s => s.EstimatedTokens);
            var duplicateGroups = FindDuplicateGroups(messages);
            var keepIndices = new HashSet<int>(Enumerable.Range(0, messages.Count));

            foreach (var group in duplicateGroups)
            {
                // Keep the last (most recent) duplicate, remove the rest
                var sortedGroup = group.OrderBy(i => i).ToList();
                for (int g = 0; g < sortedGroup.Count - 1; g++)
                {
                    keepIndices.Remove(sortedGroup[g]);
                    scored[sortedGroup[g]].Kept = false;
                    scored[sortedGroup[g]].Reason = $"Duplicate of message {sortedGroup.Last()}";
                }
                scored[sortedGroup.Last()].Reason = "Kept (latest in duplicate group)";
            }

            // If still over budget after dedup, do importance-based trimming
            int compressedTokens = scored.Where(s => keepIndices.Contains(s.Index)).Sum(s => s.EstimatedTokens);
            if (compressedTokens > _options.TargetTokens)
            {
                var removable = scored
                    .Where(s => keepIndices.Contains(s.Index))
                    .Where(s => !_options.ProtectedRoles.Contains(s.Role, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(s => s.ImportanceScore)
                    .ToList();

                foreach (var r in removable)
                {
                    if (compressedTokens <= _options.TargetTokens) break;
                    keepIndices.Remove(r.Index);
                    compressedTokens -= r.EstimatedTokens;
                    r.Kept = false;
                    r.Reason = "Dropped (post-dedup budget trim)";
                }
            }

            var result = BuildResultWithPlaceholders(messages, scored, keepIndices, totalTokens, scored.Count(s => !s.Kept));
            result.DuplicateGroupsFound = duplicateGroups.Count;
            return result;
        }

        private CompressionResult CompressAggressive(List<(string Role, string Content)> messages)
        {
            // Step 1: Deduplication
            var dedupResult = CompressDeduplication(messages);
            var current = dedupResult.Messages;

            // Step 2: If still over budget, apply anchor window
            if (!FitsInBudgetInternal(current))
            {
                var anchorCompressor = new PromptContextCompressor(
                    _options.Clone(CompressionStrategy.AnchorWindow));
                var anchorResult = anchorCompressor.Compress(current);
                current = anchorResult.Messages;
            }

            // Step 3: If still over budget, apply importance scoring
            if (!FitsInBudgetInternal(current))
            {
                var importCompressor = new PromptContextCompressor(
                    _options.Clone(CompressionStrategy.ImportanceScoring));
                var importResult = importCompressor.Compress(current);
                current = importResult.Messages;
            }

            int originalTokens = messages.Sum(m => PromptGuard.EstimateTokens(m.Content));
            int compressedTokens = current.Sum(m => PromptGuard.EstimateTokens(m.Content));

            return new CompressionResult
            {
                Messages = current,
                OriginalCount = messages.Count,
                CompressedCount = current.Count,
                OriginalTokens = originalTokens,
                CompressedTokens = compressedTokens,
                Strategy = CompressionStrategy.Aggressive,
                DuplicateGroupsFound = dedupResult.DuplicateGroupsFound,
                BudgetMet = compressedTokens <= _options.TargetTokens
            };
        }

        // --- Scoring ---

        private ScoredMessage ScoreMessage(string role, string content, int index, int totalCount)
        {
            double recency = totalCount > 1 ? (double)index / (totalCount - 1) : 1.0;
            double length = Math.Min(1.0, PromptGuard.EstimateTokens(content) / 500.0);
            double roleScore = role.ToLowerInvariant() switch
            {
                "system" => 1.0,
                "assistant" => 0.6,
                "user" => 0.5,
                "tool" => 0.4,
                _ => 0.3
            };

            double keywordScore = 0;
            if (_options.ImportantKeywords.Count > 0)
            {
                var contentLower = content.ToLowerInvariant();
                int hits = _options.ImportantKeywords.Count(k => contentLower.Contains(k.ToLowerInvariant()));
                keywordScore = Math.Min(1.0, (double)hits / Math.Max(1, _options.ImportantKeywords.Count));
            }

            // Bonus for code blocks
            bool hasCode = Regex.IsMatch(content, @"```[\s\S]*?```", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            double codeBonus = hasCode && _options.PreserveCodeBlocks ? 0.2 : 0;

            // Bonus for URLs
            bool hasUrl = Regex.IsMatch(content, @"https?://\S+", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            double urlBonus = hasUrl && _options.PreserveUrls ? 0.15 : 0;

            double score = _options.RecencyWeight * recency
                         + _options.LengthWeight * length
                         + _options.RoleWeight * roleScore
                         + _options.KeywordWeight * keywordScore
                         + codeBonus + urlBonus;

            // Clamp to 0-1
            score = Math.Max(0, Math.Min(1, score));

            return new ScoredMessage
            {
                Index = index,
                Role = role,
                Content = content,
                ImportanceScore = Math.Round(score, 3),
                EstimatedTokens = PromptGuard.EstimateTokens(content)
            };
        }

        // --- Deduplication ---

        private List<List<int>> FindDuplicateGroups(List<(string Role, string Content)> messages)
        {
            var groups = new List<List<int>>();
            var assigned = new HashSet<int>();

            for (int i = 0; i < messages.Count; i++)
            {
                if (assigned.Contains(i)) continue;
                var group = new List<int> { i };

                for (int j = i + 1; j < messages.Count; j++)
                {
                    if (assigned.Contains(j)) continue;
                    if (messages[i].Role != messages[j].Role) continue;

                    double sim = ComputeSimilarity(messages[i].Content, messages[j].Content);
                    if (sim >= _options.SimilarityThreshold)
                    {
                        group.Add(j);
                        assigned.Add(j);
                    }
                }

                if (group.Count > 1)
                {
                    assigned.Add(i);
                    groups.Add(group);
                }
            }

            return groups;
        }

        private static double ComputeSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

            // Normalize
            a = NormalizeForComparison(a);
            b = NormalizeForComparison(b);

            if (a == b) return 1.0;

            // Jaccard similarity on word n-grams
            var setA = GetWordNgrams(a, 2);
            var setB = GetWordNgrams(b, 2);

            if (setA.Count == 0 && setB.Count == 0) return 1.0;
            if (setA.Count == 0 || setB.Count == 0) return 0.0;

            int intersection = setA.Intersect(setB).Count();
            int union = setA.Union(setB).Count();

            return union > 0 ? (double)intersection / union : 0;
        }

        private static string NormalizeForComparison(string s)
        {
            s = s.ToLowerInvariant().Trim();
            s = Regex.Replace(s, @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            return s;
        }

        private static HashSet<string> GetWordNgrams(string text, int n)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ngrams = new HashSet<string>();
            for (int i = 0; i <= words.Length - n; i++)
            {
                ngrams.Add(string.Join(" ", words.Skip(i).Take(n)));
            }
            // Also add individual words for short texts
            if (words.Length < n)
            {
                foreach (var w in words) ngrams.Add(w);
            }
            return ngrams;
        }

        // --- Helpers ---

        private bool FitsInBudgetInternal(List<(string Role, string Content)> messages)
        {
            return messages.Sum(m => PromptGuard.EstimateTokens(m.Content)) <= _options.TargetTokens;
        }

        private CompressionResult BuildResult(
            List<(string Role, string Content)> original,
            List<ScoredMessage> scored,
            HashSet<int> keepIndices,
            int originalTokens,
            int removedCount)
        {
            var compressed = original
                .Where((m, i) => keepIndices.Contains(i))
                .ToList();

            int compressedTokens = compressed.Sum(m => PromptGuard.EstimateTokens(m.Content));

            return new CompressionResult
            {
                Messages = compressed,
                OriginalCount = original.Count,
                CompressedCount = compressed.Count,
                OriginalTokens = originalTokens,
                CompressedTokens = compressedTokens,
                Strategy = _options.Strategy,
                ScoringDetails = scored,
                BudgetMet = compressedTokens <= _options.TargetTokens
            };
        }

        private CompressionResult BuildResultWithPlaceholders(
            List<(string Role, string Content)> original,
            List<ScoredMessage> scored,
            HashSet<int> keepIndices,
            int originalTokens,
            int removedCount)
        {
            var compressed = new List<(string Role, string Content)>();
            int consecutiveRemoved = 0;

            for (int i = 0; i < original.Count; i++)
            {
                if (keepIndices.Contains(i))
                {
                    if (consecutiveRemoved > 0)
                    {
                        var placeholder = _options.SummaryPlaceholder
                            .Replace("{count}", consecutiveRemoved.ToString());
                        compressed.Add(("system", placeholder));
                        consecutiveRemoved = 0;
                    }
                    compressed.Add(original[i]);
                }
                else
                {
                    consecutiveRemoved++;
                }
            }

            // Trailing removed messages
            if (consecutiveRemoved > 0)
            {
                var placeholder = _options.SummaryPlaceholder
                    .Replace("{count}", consecutiveRemoved.ToString());
                compressed.Add(("system", placeholder));
            }

            int compressedTokens = compressed.Sum(m => PromptGuard.EstimateTokens(m.Content));

            return new CompressionResult
            {
                Messages = compressed,
                OriginalCount = original.Count,
                CompressedCount = compressed.Count,
                OriginalTokens = originalTokens,
                CompressedTokens = compressedTokens,
                Strategy = _options.Strategy,
                ScoringDetails = scored,
                BudgetMet = compressedTokens <= _options.TargetTokens
            };
        }
    }

    /// <summary>
    /// Fluent builder for creating a configured PromptContextCompressor.
    /// </summary>
    public class CompressorBuilder
    {
        private readonly CompressionOptions _options = new();

        /// <summary>Set the target token budget.</summary>
        public CompressorBuilder WithTargetTokens(int tokens) { _options.TargetTokens = tokens; return this; }

        /// <summary>Set the compression strategy.</summary>
        public CompressorBuilder WithStrategy(CompressionStrategy strategy) { _options.Strategy = strategy; return this; }

        /// <summary>Set anchor window sizes.</summary>
        public CompressorBuilder WithAnchorWindow(int first, int last)
        {
            _options.KeepFirstTurns = first;
            _options.KeepLastTurns = last;
            return this;
        }

        /// <summary>Set the importance threshold.</summary>
        public CompressorBuilder WithImportanceThreshold(double threshold) { _options.ImportanceThreshold = threshold; return this; }

        /// <summary>Set the similarity threshold for deduplication.</summary>
        public CompressorBuilder WithSimilarityThreshold(double threshold) { _options.SimilarityThreshold = threshold; return this; }

        /// <summary>Add keywords that boost message importance.</summary>
        public CompressorBuilder WithImportantKeywords(params string[] keywords)
        {
            _options.ImportantKeywords.AddRange(keywords);
            return this;
        }

        /// <summary>Set protected roles.</summary>
        public CompressorBuilder WithProtectedRoles(params string[] roles)
        {
            _options.ProtectedRoles = roles.ToList();
            return this;
        }

        /// <summary>Set the summary placeholder text.</summary>
        public CompressorBuilder WithSummaryPlaceholder(string placeholder) { _options.SummaryPlaceholder = placeholder; return this; }

        /// <summary>Set whether to preserve code blocks.</summary>
        public CompressorBuilder PreserveCodeBlocks(bool preserve = true) { _options.PreserveCodeBlocks = preserve; return this; }

        /// <summary>Set whether to preserve URLs.</summary>
        public CompressorBuilder PreserveUrls(bool preserve = true) { _options.PreserveUrls = preserve; return this; }

        /// <summary>Set scoring weights.</summary>
        public CompressorBuilder WithWeights(double recency = 0.4, double length = 0.2, double role = 0.2, double keyword = 0.2)
        {
            _options.RecencyWeight = recency;
            _options.LengthWeight = length;
            _options.RoleWeight = role;
            _options.KeywordWeight = keyword;
            return this;
        }

        /// <summary>Build the compressor.</summary>
        public PromptContextCompressor Build() => new(_options);
    }
}
