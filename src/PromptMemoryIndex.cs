namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>A single memory entry representing a conversation exchange.</summary>
    public class MemoryEntry
    {
        /// <summary>Unique identifier for this memory.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
        /// <summary>The role that produced this content (user, assistant, system).</summary>
        public string Role { get; set; } = "user";
        /// <summary>The text content of the memory.</summary>
        public string Content { get; set; } = "";
        /// <summary>When this memory was created.</summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>Optional tags for categorical filtering.</summary>
        public List<string> Tags { get; set; } = new();
        /// <summary>Optional metadata key-value pairs.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
        /// <summary>Number of times this memory has been recalled.</summary>
        public int RecallCount { get; set; }
        /// <summary>When this memory was last recalled.</summary>
        public DateTimeOffset? LastRecalled { get; set; }
        /// <summary>Importance score (0.0-1.0). Default: 0.5.</summary>
        public double Importance { get; set; } = 0.5;
    }

    /// <summary>Result of a memory retrieval query.</summary>
    public class MemoryResult
    {
        /// <summary>The matching memory entry.</summary>
        public MemoryEntry Entry { get; set; } = null!;
        /// <summary>BM25 relevance score.</summary>
        public double RelevanceScore { get; set; }
        /// <summary>Recency score (0-1).</summary>
        public double RecencyScore { get; set; }
        /// <summary>Combined final score.</summary>
        public double FinalScore { get; set; }
        /// <summary>Matched query terms.</summary>
        public List<string> MatchedTerms { get; set; } = new();
    }

    /// <summary>Options for memory retrieval scoring.</summary>
    public class MemoryRetrievalOptions
    {
        /// <summary>Max results. Default: 5.</summary>
        public int MaxResults { get; set; } = 5;
        /// <summary>BM25 relevance weight. Default: 0.5.</summary>
        public double RelevanceWeight { get; set; } = 0.5;
        /// <summary>Recency weight. Default: 0.3.</summary>
        public double RecencyWeight { get; set; } = 0.3;
        /// <summary>Importance weight. Default: 0.2.</summary>
        public double ImportanceWeight { get; set; } = 0.2;
        /// <summary>Recency half-life in hours. Default: 24.</summary>
        public double RecencyHalfLifeHours { get; set; } = 24.0;
        /// <summary>Min score threshold. Default: 0.01.</summary>
        public double MinScore { get; set; } = 0.01;
        /// <summary>Role filter (null = all).</summary>
        public HashSet<string>? RoleFilter { get; set; }
        /// <summary>Tag filter (null = all).</summary>
        public HashSet<string>? TagFilter { get; set; }
        /// <summary>Boost less-recalled entries. Default: false.</summary>
        public bool NoveltyBoost { get; set; }
    }

    /// <summary>Memory index statistics.</summary>
    public class MemoryIndexStats
    {
        /// <summary>Total entries.</summary>
        public int TotalEntries { get; set; }
        /// <summary>Entries by role.</summary>
        public Dictionary<string, int> EntriesByRole { get; set; } = new();
        /// <summary>Total estimated tokens.</summary>
        public int TotalTokens { get; set; }
        /// <summary>Average tokens per entry.</summary>
        public double AvgTokensPerEntry { get; set; }
        /// <summary>Unique tag count.</summary>
        public int UniqueTags { get; set; }
        /// <summary>Oldest entry timestamp.</summary>
        public DateTimeOffset? OldestEntry { get; set; }
        /// <summary>Newest entry timestamp.</summary>
        public DateTimeOffset? NewestEntry { get; set; }
        /// <summary>Time span covered.</summary>
        public TimeSpan? TimeSpan => OldestEntry.HasValue && NewestEntry.HasValue
            ? NewestEntry.Value - OldestEntry.Value : null;
    }

    /// <summary>
    /// In-memory retrieval index for conversation messages. Enables RAG by
    /// indexing past exchanges and retrieving the most relevant ones via
    /// BM25 text relevance + recency decay + importance weighting.
    /// </summary>
    /// <remarks>
    /// <code>
    /// var memory = new PromptMemoryIndex();
    /// memory.Add("user", "My favorite color is blue.");
    /// memory.Add("assistant", "Got it! Blue is a great color.");
    /// memory.Add("user", "I'm working on a Python web scraper.");
    ///
    /// var results = memory.Retrieve("What color theme should I use?");
    /// string context = memory.FormatAsContext(results);
    /// </code>
    /// </remarks>
    public class PromptMemoryIndex
    {
        private readonly List<MemoryEntry> _entries = new();
        private readonly Dictionary<string, double> _idf = new();
        private readonly Dictionary<string, Dictionary<string, int>> _termFreqs = new();
        private bool _idfDirty = true;

        private const double K1 = 1.2;
        private const double B = 0.75;

        private static readonly Regex TokenPattern = new(@"[a-zA-Z0-9]+", RegexOptions.Compiled);
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a","an","the","is","are","was","were","be","been","being",
            "have","has","had","do","does","did","will","would","could",
            "should","may","might","shall","can","to","of","in","for",
            "on","with","at","by","from","as","into","through","during",
            "before","after","and","but","or","nor","not","so","yet",
            "both","either","neither","each","every","all","any","few",
            "more","most","other","some","such","no","only","own","same",
            "than","too","very","just","because","if","when","where",
            "how","what","which","who","whom","this","that","these",
            "those","i","me","my","myself","we","our","you","your",
            "he","him","his","she","her","it","its","they","them",
            "their","up","out","about"
        };

        /// <summary>Max entries allowed. Default: 10000.</summary>
        public int MaxEntries { get; set; } = 10000;
        /// <summary>Auto-evict oldest when exceeding MaxEntries. Default: true.</summary>
        public bool AutoEvict { get; set; } = true;
        /// <summary>Current entry count.</summary>
        public int Count => _entries.Count;

        /// <summary>Add a memory from a conversation message.</summary>
        public MemoryEntry Add(string role, string content, IEnumerable<string>? tags = null, double importance = 0.5)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be empty.", nameof(content));
            return Add(new MemoryEntry
            {
                Role = role ?? "user", Content = content,
                Importance = Math.Clamp(importance, 0.0, 1.0),
                Tags = tags?.ToList() ?? new List<string>()
            });
        }

        /// <summary>Add a pre-built MemoryEntry.</summary>
        public MemoryEntry Add(MemoryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.Content))
                throw new ArgumentException("Entry content cannot be empty.");
            _entries.Add(entry);
            IndexEntry(entry);
            _idfDirty = true;
            if (AutoEvict && _entries.Count > MaxEntries)
                Evict(_entries.Count - MaxEntries);
            return entry;
        }

        /// <summary>Index a batch of conversation messages.</summary>
        public void AddConversation(IEnumerable<(string role, string content)> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            foreach (var (role, content) in messages)
                if (!string.IsNullOrWhiteSpace(content)) Add(role, content);
        }

        /// <summary>Retrieve the most relevant memories for a query.</summary>
        public List<MemoryResult> Retrieve(string query, MemoryRetrievalOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<MemoryResult>();
            options ??= new MemoryRetrievalOptions();
            var queryTerms = Tokenize(query);
            if (queryTerms.Count == 0) return new List<MemoryResult>();

            RebuildIdfIfNeeded();
            var now = DateTimeOffset.UtcNow;
            var avgDocLen = _termFreqs.Count > 0
                ? _termFreqs.Values.Average(tf => tf.Values.Sum()) : 1.0;

            var results = new List<MemoryResult>();
            foreach (var entry in _entries)
            {
                if (options.RoleFilter != null && !options.RoleFilter.Contains(entry.Role)) continue;
                if (options.TagFilter != null && !entry.Tags.Any(t => options.TagFilter.Contains(t))) continue;

                var (bm25, matched) = ComputeBM25(entry.Id, queryTerms, avgDocLen);
                if (matched.Count == 0) continue;

                double age = Math.Max(0, (now - entry.Timestamp).TotalHours);
                double recency = Math.Pow(0.5, age / options.RecencyHalfLifeHours);
                double imp = entry.Importance;
                if (options.NoveltyBoost && entry.RecallCount > 0)
                    imp *= 1.0 / (1.0 + 0.1 * entry.RecallCount);

                double final_ = options.RelevanceWeight * bm25
                    + options.RecencyWeight * recency + options.ImportanceWeight * imp;

                if (final_ >= options.MinScore)
                    results.Add(new MemoryResult
                    {
                        Entry = entry, RelevanceScore = bm25,
                        RecencyScore = recency, FinalScore = final_,
                        MatchedTerms = matched
                    });
            }

            var top = results.OrderByDescending(r => r.FinalScore)
                .Take(options.MaxResults).ToList();
            foreach (var r in top) { r.Entry.RecallCount++; r.Entry.LastRecalled = now; }
            return top;
        }

        /// <summary>Remove a memory by ID.</summary>
        public bool Remove(string id)
        {
            var e = _entries.FirstOrDefault(x => x.Id == id);
            if (e == null) return false;
            _entries.Remove(e); _termFreqs.Remove(e.Id); _idfDirty = true;
            return true;
        }

        /// <summary>Remove entries matching a predicate.</summary>
        public int RemoveWhere(Func<MemoryEntry, bool> pred)
        {
            var rem = _entries.Where(pred).ToList();
            foreach (var e in rem) { _entries.Remove(e); _termFreqs.Remove(e.Id); }
            if (rem.Count > 0) _idfDirty = true;
            return rem.Count;
        }

        /// <summary>Format memories as a context string for prompt injection.</summary>
        public string FormatAsContext(List<MemoryResult> results,
            bool includeMetadata = false, int? maxTokens = null)
        {
            if (results == null || results.Count == 0) return "";
            var lines = new List<string> { "--- Relevant Context from Memory ---" };
            int est = 0;
            foreach (var r in results)
            {
                var line = includeMetadata
                    ? $"[{r.Entry.Role}] (score: {r.FinalScore:F2}, {r.Entry.Timestamp:yyyy-MM-dd HH:mm}) {r.Entry.Content}"
                    : $"[{r.Entry.Role}] {r.Entry.Content}";
                int t = EstimateTokens(line);
                if (maxTokens.HasValue && est + t > maxTokens.Value) break;
                lines.Add(line); est += t;
            }
            lines.Add("--- End Memory Context ---");
            return string.Join("\n", lines);
        }

        /// <summary>Get index statistics.</summary>
        public MemoryIndexStats GetStats()
        {
            var s = new MemoryIndexStats
            {
                TotalEntries = _entries.Count,
                EntriesByRole = _entries.GroupBy(e => e.Role).ToDictionary(g => g.Key, g => g.Count()),
                UniqueTags = _entries.SelectMany(e => e.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            };
            if (_entries.Count > 0)
            {
                s.TotalTokens = _entries.Sum(e => EstimateTokens(e.Content));
                s.AvgTokensPerEntry = (double)s.TotalTokens / _entries.Count;
                s.OldestEntry = _entries.Min(e => e.Timestamp);
                s.NewestEntry = _entries.Max(e => e.Timestamp);
            }
            return s;
        }

        /// <summary>Export index as JSON.</summary>
        public string ExportJson() => JsonSerializer.Serialize(_entries,
            new JsonSerializerOptions { WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });

        /// <summary>Import entries from JSON (appends).</summary>
        public int ImportJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty.", nameof(json));
            if (json.Length > SerializationGuards.MaxJsonPayloadBytes)
                throw new InvalidOperationException("JSON payload too large.");
            var entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json)
                ?? throw new InvalidOperationException("Deserialization failed.");
            if (entries.Count > MaxEntries)
                throw new InvalidOperationException($"Too many entries ({entries.Count} > {MaxEntries}).");
            foreach (var e in entries)
                if (!string.IsNullOrWhiteSpace(e.Content)) Add(e);
            return entries.Count;
        }

        /// <summary>Clear all entries.</summary>
        public void Clear()
        {
            _entries.Clear(); _termFreqs.Clear(); _idf.Clear(); _idfDirty = false;
        }

        /// <summary>Get all entries, optionally filtered, by timestamp descending.</summary>
        public List<MemoryEntry> GetAll(string? roleFilter = null, string? tagFilter = null)
        {
            IEnumerable<MemoryEntry> q = _entries;
            if (roleFilter != null)
                q = q.Where(e => e.Role.Equals(roleFilter, StringComparison.OrdinalIgnoreCase));
            if (tagFilter != null)
                q = q.Where(e => e.Tags.Contains(tagFilter, StringComparer.OrdinalIgnoreCase));
            return q.OrderByDescending(e => e.Timestamp).ToList();
        }

        /// <summary>Merge another index (deduplicates by ID).</summary>
        public int Merge(PromptMemoryIndex other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            var ids = new HashSet<string>(_entries.Select(e => e.Id));
            int added = 0;
            foreach (var e in other._entries)
                if (!ids.Contains(e.Id)) { Add(e); added++; }
            return added;
        }

        private void IndexEntry(MemoryEntry entry)
        {
            var tf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in Tokenize(entry.Content))
            { tf.TryGetValue(t, out int c); tf[t] = c + 1; }
            _termFreqs[entry.Id] = tf;
        }

        private void RebuildIdfIfNeeded()
        {
            if (!_idfDirty) return;
            _idf.Clear();
            int n = _termFreqs.Count; if (n == 0) return;
            var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in _termFreqs.Values)
                foreach (var t in doc.Keys)
                { df.TryGetValue(t, out int c); df[t] = c + 1; }
            foreach (var (t, f) in df)
                _idf[t] = Math.Log((n - f + 0.5) / (f + 0.5) + 1.0);
            _idfDirty = false;
        }

        private (double, List<string>) ComputeBM25(string docId, List<string> qTerms, double avgDl)
        {
            if (!_termFreqs.TryGetValue(docId, out var tf)) return (0, new());
            double dl = tf.Values.Sum(), score = 0;
            var matched = new List<string>();
            foreach (var t in qTerms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!tf.TryGetValue(t, out int freq) || !_idf.TryGetValue(t, out double idf)) continue;
                score += idf * (freq * (K1 + 1)) / (freq + K1 * (1 - B + B * dl / avgDl));
                matched.Add(t);
            }
            if (score > 0) score = Math.Min(1.0, score / (qTerms.Count * 2.0));
            return (score, matched);
        }

        private static List<string> Tokenize(string text) =>
            TokenPattern.Matches(text.ToLowerInvariant())
                .Select(m => m.Value).Where(t => t.Length > 1 && !StopWords.Contains(t)).ToList();

        private static int EstimateTokens(string text) =>
            string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);

        private void Evict(int count)
        {
            foreach (var e in _entries.OrderBy(e => e.Timestamp).Take(count).ToList())
            { _entries.Remove(e); _termFreqs.Remove(e.Id); }
            _idfDirty = true;
        }
    }
}
