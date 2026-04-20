namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Memory entry importance tier, controlling retention priority.
    /// </summary>
    public enum ConvMemoryTier
    {
        /// <summary>Critical context that should never be evicted.</summary>
        Pinned,
        /// <summary>High-value context (decisions, goals, constraints).</summary>
        Core,
        /// <summary>Normal conversational context.</summary>
        Standard,
        /// <summary>Low-value context (greetings, acknowledgments).</summary>
        Ephemeral
    }

    /// <summary>
    /// A single memory entry tracked by <see cref="PromptMemory"/>.
    /// </summary>
    public class ConvMemoryEntry
    {
        /// <summary>Gets the unique identifier.</summary>
        public string Id { get; internal set; } = "";

        /// <summary>Gets the role (system, user, assistant).</summary>
        public string Role { get; internal set; } = "";

        /// <summary>Gets the original content.</summary>
        public string Content { get; internal set; } = "";

        /// <summary>Gets the summarized content (null if not yet summarized).</summary>
        public string? Summary { get; internal set; }

        /// <summary>Gets the importance tier.</summary>
        public ConvMemoryTier Tier { get; internal set; } = ConvMemoryTier.Standard;

        /// <summary>Gets the relevance score (0.0–1.0).</summary>
        public double Relevance { get; internal set; } = 0.5;

        /// <summary>Gets the estimated token count.</summary>
        public int TokenEstimate { get; internal set; }

        /// <summary>Gets the timestamp when added.</summary>
        public DateTime AddedAt { get; internal set; }

        /// <summary>Gets how many times this entry was referenced.</summary>
        public int ReferenceCount { get; internal set; }

        /// <summary>Gets the last time this entry was accessed.</summary>
        public DateTime LastAccessed { get; internal set; }

        /// <summary>Gets the topic tags extracted from the content.</summary>
        public List<string> Topics { get; internal set; } = new();

        /// <summary>Gets the active content (summary if available, else original).</summary>
        public string ActiveContent => Summary ?? Content;

        /// <summary>Gets the active token estimate.</summary>
        public int ActiveTokens => Summary != null ? EstimateTokens(Summary) : TokenEstimate;

        internal static int EstimateTokens(string text) =>
            string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 3.5);
    }

    /// <summary>
    /// Eviction event raised when entries are removed from memory.
    /// </summary>
    public class ConvMemoryEvictionEvent
    {
        /// <summary>Gets the evicted entry.</summary>
        public ConvMemoryEntry Entry { get; internal set; } = new();

        /// <summary>Gets the reason for eviction.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Gets when the eviction occurred.</summary>
        public DateTime EvictedAt { get; internal set; }
    }

    /// <summary>
    /// Memory health report from <see cref="PromptMemory.GetHealthReport"/>.
    /// </summary>
    public class ConvMemoryHealthReport
    {
        /// <summary>Total entries in memory.</summary>
        public int TotalEntries { get; internal set; }

        /// <summary>Total estimated tokens used.</summary>
        public int TotalTokens { get; internal set; }

        /// <summary>Token budget capacity.</summary>
        public int TokenBudget { get; internal set; }

        /// <summary>Utilization percentage (0–100).</summary>
        public double UtilizationPercent { get; internal set; }

        /// <summary>Number of summarized entries.</summary>
        public int SummarizedCount { get; internal set; }

        /// <summary>Number of evicted entries since creation.</summary>
        public int EvictedCount { get; internal set; }

        /// <summary>Entries per tier.</summary>
        public Dictionary<ConvMemoryTier, int> TierBreakdown { get; internal set; } = new();

        /// <summary>Top topics by frequency.</summary>
        public List<KeyValuePair<string, int>> TopTopics { get; internal set; } = new();

        /// <summary>Average relevance score.</summary>
        public double AverageRelevance { get; internal set; }

        /// <summary>Health status: Healthy, Warning, Critical.</summary>
        public string Status { get; internal set; } = "Healthy";

        /// <summary>Proactive recommendations.</summary>
        public List<string> Recommendations { get; internal set; } = new();
    }

    /// <summary>
    /// Strategy for memory compaction when approaching token budget.
    /// </summary>
    public enum CompactionStrategy
    {
        /// <summary>Evict lowest-relevance entries first.</summary>
        RelevanceBased,
        /// <summary>Summarize older entries, evict only if needed.</summary>
        SummarizeFirst,
        /// <summary>Evict oldest entries first (FIFO).</summary>
        OldestFirst,
        /// <summary>Evict least-recently-accessed entries.</summary>
        LRU
    }

    /// <summary>
    /// Adaptive conversation memory manager that autonomously manages context
    /// within token budgets using relevance scoring, auto-summarization,
    /// topic tracking, and intelligent eviction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Agentic behavior:</b> PromptMemory proactively monitors token usage,
    /// auto-classifies entry importance, detects topic drift, and autonomously
    /// compacts context to stay within budget — all without user intervention.
    /// </para>
    /// <para>
    /// Usage:
    /// <code>
    /// var memory = new PromptMemory(tokenBudget: 4000);
    /// memory.Add("user", "Build me a REST API for todo items");
    /// memory.Add("assistant", "I'll create a REST API with CRUD endpoints...");
    /// memory.Add("user", "Add authentication with JWT");
    /// 
    /// // Get optimized context for next API call
    /// var context = memory.BuildContext();
    /// 
    /// // Check memory health
    /// var health = memory.GetHealthReport();
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptMemory
    {
        private readonly List<ConvMemoryEntry> _entries = new();
        private readonly List<ConvMemoryEvictionEvent> _evictionLog = new();
        private readonly int _tokenBudget;
        private readonly CompactionStrategy _strategy;
        private readonly double _compactionThreshold;
        private int _nextId;

        // Topic extraction patterns
        private static readonly Regex TopicPattern = new(
            @"\b(?:build|create|implement|add|fix|update|remove|delete|test|deploy|configure|setup|design|refactor)\s+(\w+(?:\s+\w+){0,2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TechPattern = new(
            @"\b(API|REST|GraphQL|SQL|JWT|OAuth|Docker|Kubernetes|Redis|MongoDB|PostgreSQL|MySQL|React|Vue|Angular|Node\.?js|Python|C#|\.NET|TypeScript|JavaScript|HTML|CSS|JSON|YAML|XML|HTTP|HTTPS|WebSocket|gRPC|CRUD|CI/?CD|AWS|Azure|GCP)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EphemeralPattern = new(
            @"^\s*(ok|okay|sure|thanks|thank you|got it|sounds good|great|yes|no|right|understood|perfect|awesome|cool|nice)\s*[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DecisionPattern = new(
            @"\b(decided?|chose|chosen|will use|going with|let'?s go with|picking|selected?|must|should|require|constraint|rule|always|never)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Creates a new PromptMemory with the given token budget and compaction strategy.
        /// </summary>
        /// <param name="tokenBudget">Maximum tokens for context (default 4000).</param>
        /// <param name="strategy">Compaction strategy (default SummarizeFirst).</param>
        /// <param name="compactionThreshold">Utilization % that triggers compaction (default 0.85).</param>
        public PromptMemory(int tokenBudget = 4000, CompactionStrategy strategy = CompactionStrategy.SummarizeFirst, double compactionThreshold = 0.85)
        {
            _tokenBudget = Math.Max(100, tokenBudget);
            _strategy = strategy;
            _compactionThreshold = Math.Clamp(compactionThreshold, 0.5, 0.99);
        }

        /// <summary>Gets the current token budget.</summary>
        public int TokenBudget => _tokenBudget;

        /// <summary>Gets the current total token usage.</summary>
        public int CurrentTokens => _entries.Sum(e => e.ActiveTokens);

        /// <summary>Gets the number of entries in memory.</summary>
        public int Count => _entries.Count;

        /// <summary>Gets the eviction log.</summary>
        public IReadOnlyList<ConvMemoryEvictionEvent> EvictionLog => _evictionLog.AsReadOnly();

        /// <summary>
        /// Add a message to memory with automatic importance classification and topic extraction.
        /// </summary>
        /// <param name="role">Message role (system, user, assistant).</param>
        /// <param name="content">Message content.</param>
        /// <param name="tier">Override tier (null for auto-classification).</param>
        /// <returns>The created memory entry.</returns>
        public ConvMemoryEntry Add(string role, string content, ConvMemoryTier? tier = null)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be empty.", nameof(content));

            var entry = new ConvMemoryEntry
            {
                Id = $"mem_{_nextId++:D4}",
                Role = role?.ToLowerInvariant() ?? "user",
                Content = content,
                Tier = tier ?? ClassifyTier(role, content),
                Relevance = ScoreRelevance(content),
                TokenEstimate = ConvMemoryEntry.EstimateTokens(content),
                AddedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                Topics = ExtractTopics(content)
            };

            // Boost relevance for system messages
            if (entry.Role == "system")
                entry.Relevance = Math.Min(1.0, entry.Relevance + 0.3);

            _entries.Add(entry);

            // Proactive compaction check
            if (CurrentTokens > _tokenBudget * _compactionThreshold)
                Compact();

            return entry;
        }

        /// <summary>
        /// Pin an entry so it's never evicted.
        /// </summary>
        public void Pin(string entryId)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
                entry.Tier = ConvMemoryTier.Pinned;
        }

        /// <summary>
        /// Mark an entry as referenced (boosts relevance and access time).
        /// </summary>
        public void Touch(string entryId)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
            {
                entry.ReferenceCount++;
                entry.LastAccessed = DateTime.UtcNow;
                entry.Relevance = Math.Min(1.0, entry.Relevance + 0.05);
            }
        }

        /// <summary>
        /// Search memory entries by topic or content keyword.
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="maxResults">Maximum results to return.</param>
        /// <returns>Matching entries sorted by relevance.</returns>
        public List<ConvMemoryEntry> Search(string query, int maxResults = 10)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<ConvMemoryEntry>();

            var queryWords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var queryTopics = new HashSet<string>(queryWords);

            return _entries
                .Select(e =>
                {
                    double score = 0;
                    var lower = e.Content.ToLowerInvariant();

                    // Word match scoring
                    foreach (var w in queryWords)
                        if (lower.Contains(w)) score += 0.3;

                    // Topic match scoring
                    foreach (var t in e.Topics)
                        if (queryTopics.Contains(t.ToLowerInvariant())) score += 0.5;

                    // Relevance boost
                    score += e.Relevance * 0.2;

                    return (entry: e, score);
                })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Take(maxResults)
                .Select(x =>
                {
                    x.entry.LastAccessed = DateTime.UtcNow;
                    return x.entry;
                })
                .ToList();
        }

        /// <summary>
        /// Build optimized context for the next API call, respecting token budget.
        /// Prioritizes pinned → core → recent relevant → summarized older entries.
        /// </summary>
        /// <returns>Ordered list of entries for context.</returns>
        public List<ConvMemoryEntry> BuildContext()
        {
            var result = new List<ConvMemoryEntry>();
            var remaining = _tokenBudget;

            // Phase 1: Always include pinned entries
            foreach (var e in _entries.Where(e => e.Tier == ConvMemoryTier.Pinned))
            {
                if (e.ActiveTokens <= remaining)
                {
                    result.Add(e);
                    remaining -= e.ActiveTokens;
                    e.LastAccessed = DateTime.UtcNow;
                }
            }

            // Phase 2: Core entries by relevance
            foreach (var e in _entries.Where(e => e.Tier == ConvMemoryTier.Core)
                .OrderByDescending(e => e.Relevance))
            {
                if (e.ActiveTokens <= remaining && !result.Contains(e))
                {
                    result.Add(e);
                    remaining -= e.ActiveTokens;
                    e.LastAccessed = DateTime.UtcNow;
                }
            }

            // Phase 3: Standard entries, recent first with relevance tiebreaker
            foreach (var e in _entries.Where(e => e.Tier == ConvMemoryTier.Standard)
                .OrderByDescending(e => e.AddedAt)
                .ThenByDescending(e => e.Relevance))
            {
                if (e.ActiveTokens <= remaining && !result.Contains(e))
                {
                    result.Add(e);
                    remaining -= e.ActiveTokens;
                    e.LastAccessed = DateTime.UtcNow;
                }
            }

            // Phase 4: Ephemeral only if budget allows
            foreach (var e in _entries.Where(e => e.Tier == ConvMemoryTier.Ephemeral)
                .OrderByDescending(e => e.AddedAt))
            {
                if (e.ActiveTokens <= remaining && !result.Contains(e))
                {
                    result.Add(e);
                    remaining -= e.ActiveTokens;
                }
            }

            // Re-sort by original order for coherent conversation flow
            return result.OrderBy(e => e.AddedAt).ToList();
        }

        /// <summary>
        /// Compact memory to stay within token budget using the configured strategy.
        /// Returns the number of entries affected (summarized + evicted).
        /// </summary>
        public int Compact()
        {
            int affected = 0;

            switch (_strategy)
            {
                case CompactionStrategy.SummarizeFirst:
                    affected += SummarizeOldEntries();
                    if (CurrentTokens > _tokenBudget)
                        affected += EvictByRelevance();
                    break;

                case CompactionStrategy.RelevanceBased:
                    affected += EvictByRelevance();
                    break;

                case CompactionStrategy.OldestFirst:
                    affected += EvictOldest();
                    break;

                case CompactionStrategy.LRU:
                    affected += EvictLRU();
                    break;
            }

            return affected;
        }

        /// <summary>
        /// Detect if the conversation has drifted to new topics.
        /// </summary>
        /// <returns>New topics that appeared in recent entries but not earlier.</returns>
        public List<string> DetectTopicDrift()
        {
            if (_entries.Count < 4) return new List<string>();

            int midpoint = _entries.Count / 2;
            var earlyTopics = new HashSet<string>(
                _entries.Take(midpoint).SelectMany(e => e.Topics),
                StringComparer.OrdinalIgnoreCase);
            var recentTopics = _entries.Skip(midpoint)
                .SelectMany(e => e.Topics)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(t => !earlyTopics.Contains(t))
                .ToList();

            return recentTopics;
        }

        /// <summary>
        /// Get a comprehensive health report with proactive recommendations.
        /// </summary>
        public ConvMemoryHealthReport GetHealthReport()
        {
            var utilization = _tokenBudget > 0 ? (double)CurrentTokens / _tokenBudget * 100 : 0;
            var topicCounts = _entries.SelectMany(e => e.Topics)
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .ToList();

            var report = new ConvMemoryHealthReport
            {
                TotalEntries = _entries.Count,
                TotalTokens = CurrentTokens,
                TokenBudget = _tokenBudget,
                UtilizationPercent = Math.Round(utilization, 1),
                SummarizedCount = _entries.Count(e => e.Summary != null),
                EvictedCount = _evictionLog.Count,
                TierBreakdown = Enum.GetValues<ConvMemoryTier>()
                    .ToDictionary(t => t, t => _entries.Count(e => e.Tier == t)),
                TopTopics = topicCounts,
                AverageRelevance = _entries.Count > 0 ? Math.Round(_entries.Average(e => e.Relevance), 3) : 0,
                Status = utilization > 95 ? "Critical" : utilization > 80 ? "Warning" : "Healthy"
            };

            // Proactive recommendations
            if (utilization > 90)
                report.Recommendations.Add("Memory near capacity — consider increasing token budget or switching to SummarizeFirst strategy.");

            if (_entries.Count(e => e.Tier == ConvMemoryTier.Ephemeral) > _entries.Count * 0.4)
                report.Recommendations.Add("High ratio of ephemeral entries — conversation may lack substantive content.");

            var drift = DetectTopicDrift();
            if (drift.Count > 3)
                report.Recommendations.Add($"Topic drift detected: {string.Join(", ", drift.Take(5))}. Consider pinning key context entries.");

            if (_entries.Count(e => e.Tier == ConvMemoryTier.Pinned) > _entries.Count * 0.3)
                report.Recommendations.Add("Too many pinned entries — this limits memory flexibility. Review pins.");

            var staleCount = _entries.Count(e =>
                e.Tier != ConvMemoryTier.Pinned &&
                (DateTime.UtcNow - e.LastAccessed).TotalMinutes > 30 &&
                e.ReferenceCount == 0);
            if (staleCount > 5)
                report.Recommendations.Add($"{staleCount} unreferenced entries may be stale — compaction would reclaim tokens.");

            if (report.Recommendations.Count == 0)
                report.Recommendations.Add("Memory is healthy. No action needed.");

            return report;
        }

        /// <summary>
        /// Export memory state as a structured dictionary (for serialization or inspection).
        /// </summary>
        public Dictionary<string, object> Export()
        {
            return new Dictionary<string, object>
            {
                ["tokenBudget"] = _tokenBudget,
                ["currentTokens"] = CurrentTokens,
                ["strategy"] = _strategy.ToString(),
                ["entryCount"] = _entries.Count,
                ["entries"] = _entries.Select(e => new Dictionary<string, object>
                {
                    ["id"] = e.Id,
                    ["role"] = e.Role,
                    ["content"] = e.Content,
                    ["summary"] = (object?)e.Summary ?? "",
                    ["tier"] = e.Tier.ToString(),
                    ["relevance"] = e.Relevance,
                    ["tokenEstimate"] = e.TokenEstimate,
                    ["topics"] = e.Topics,
                    ["referenceCount"] = e.ReferenceCount
                }).ToList(),
                ["evictionCount"] = _evictionLog.Count
            };
        }

        // --- Private helpers ---

        private ConvMemoryTier ClassifyTier(string? role, string content)
        {
            if (role?.ToLowerInvariant() == "system")
                return ConvMemoryTier.Core;

            if (EphemeralPattern.IsMatch(content))
                return ConvMemoryTier.Ephemeral;

            if (DecisionPattern.IsMatch(content))
                return ConvMemoryTier.Core;

            if (content.Length > 200)
                return ConvMemoryTier.Standard;

            return ConvMemoryTier.Standard;
        }

        private double ScoreRelevance(string content)
        {
            double score = 0.5;

            // Length contributes to relevance (more substance = more relevant)
            if (content.Length > 100) score += 0.1;
            if (content.Length > 300) score += 0.1;

            // Technical terms boost relevance
            var techMatches = TechPattern.Matches(content).Count;
            score += Math.Min(0.2, techMatches * 0.05);

            // Questions are high-relevance
            if (content.Contains('?')) score += 0.1;

            // Decisions are high-relevance
            if (DecisionPattern.IsMatch(content)) score += 0.15;

            // Code blocks are high-relevance
            if (content.Contains("```") || content.Contains("    ")) score += 0.1;

            return Math.Min(1.0, score);
        }

        private List<string> ExtractTopics(string content)
        {
            var topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in TechPattern.Matches(content))
                topics.Add(m.Value.ToUpperInvariant());

            foreach (Match m in TopicPattern.Matches(content))
            {
                var topic = m.Groups[1].Value.Trim();
                if (topic.Length > 2 && topic.Length < 40)
                    topics.Add(topic.ToLowerInvariant());
            }

            return topics.Take(10).ToList();
        }

        private int SummarizeOldEntries()
        {
            int count = 0;
            // Summarize non-pinned entries from oldest, only if not already summarized
            var candidates = _entries
                .Where(e => e.Tier != ConvMemoryTier.Pinned && e.Summary == null && e.TokenEstimate > 30)
                .OrderBy(e => e.AddedAt)
                .Take(_entries.Count / 3 + 1);

            foreach (var entry in candidates)
            {
                entry.Summary = GenerateSummary(entry.Content, entry.Role);
                count++;
            }
            return count;
        }

        private string GenerateSummary(string content, string role)
        {
            // Extractive summarization: take first sentence + key phrases
            var sentences = Regex.Split(content, @"(?<=[.!?])\s+")
                .Where(s => s.Length > 10)
                .ToList();

            if (sentences.Count <= 1)
                return content.Length > 80 ? content[..77] + "..." : content;

            var summary = sentences[0];

            // Append any sentences with decisions or tech terms
            foreach (var s in sentences.Skip(1).Take(2))
            {
                if (DecisionPattern.IsMatch(s) || TechPattern.IsMatch(s))
                    summary += " " + s;
            }

            if (summary.Length > content.Length * 0.7)
                return content; // Summary isn't much shorter, keep original

            return $"[{role}] {summary}";
        }

        private int EvictByRelevance()
        {
            int count = 0;
            while (CurrentTokens > _tokenBudget)
            {
                var victim = _entries
                    .Where(e => e.Tier != ConvMemoryTier.Pinned)
                    .OrderBy(e => e.Tier == ConvMemoryTier.Ephemeral ? 0 : 1)
                    .ThenBy(e => e.Relevance)
                    .ThenBy(e => e.ReferenceCount)
                    .FirstOrDefault();

                if (victim == null) break;
                Evict(victim, "Low relevance score");
                count++;
            }
            return count;
        }

        private int EvictOldest()
        {
            int count = 0;
            while (CurrentTokens > _tokenBudget)
            {
                var victim = _entries
                    .Where(e => e.Tier != ConvMemoryTier.Pinned)
                    .OrderBy(e => e.AddedAt)
                    .FirstOrDefault();

                if (victim == null) break;
                Evict(victim, "Oldest entry (FIFO)");
                count++;
            }
            return count;
        }

        private int EvictLRU()
        {
            int count = 0;
            while (CurrentTokens > _tokenBudget)
            {
                var victim = _entries
                    .Where(e => e.Tier != ConvMemoryTier.Pinned)
                    .OrderBy(e => e.LastAccessed)
                    .FirstOrDefault();

                if (victim == null) break;
                Evict(victim, "Least recently used");
                count++;
            }
            return count;
        }

        private void Evict(ConvMemoryEntry entry, string reason)
        {
            _evictionLog.Add(new ConvMemoryEvictionEvent
            {
                Entry = entry,
                Reason = reason,
                EvictedAt = DateTime.UtcNow
            });
            _entries.Remove(entry);
        }
    }
}
