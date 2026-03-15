namespace Prompt
{
    using System.Collections.Concurrent;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Records prompt/response execution history with timestamps, durations,
    /// token estimates, and metadata. Supports search, filtering, statistics,
    /// and export to JSON/CSV.
    /// </summary>
    /// <remarks>
    /// <para>Thread-safe. Can be used as a singleton across the application.</para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var history = new PromptHistory(maxEntries: 1000);
    ///
    /// // Record manually
    /// history.Record("Tell me a joke", "Why did the chicken...", TimeSpan.FromSeconds(1.2));
    ///
    /// // Or wrap a call for automatic recording
    /// var response = await history.TrackAsync(() =>
    ///     Main.GetResponseAsync("Explain quantum computing"));
    ///
    /// // Search and filter
    /// var recent = history.GetRecent(10);
    /// var slow = history.Search(minDuration: TimeSpan.FromSeconds(5));
    /// var stats = history.GetStatistics();
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptHistory
    {
        private readonly ConcurrentQueue<HistoryEntry> _entries = new();
        private readonly int _maxEntries;
        private long _totalCount;

        /// <summary>
        /// Creates a new prompt history tracker.
        /// </summary>
        /// <param name="maxEntries">
        /// Maximum entries to keep in memory. Oldest entries are evicted
        /// when this limit is exceeded. Default: 500.
        /// </param>
        public PromptHistory(int maxEntries = 500)
        {
            if (maxEntries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxEntries),
                    maxEntries, "maxEntries must be at least 1.");
            _maxEntries = maxEntries;
        }

        /// <summary>Number of entries currently stored.</summary>
        public int Count => _entries.Count;

        /// <summary>Total entries ever recorded (including evicted).</summary>
        public long TotalCount => Interlocked.Read(ref _totalCount);

        /// <summary>
        /// Records a prompt execution in history.
        /// </summary>
        /// <param name="prompt">The prompt that was sent.</param>
        /// <param name="response">The response received (null if failed).</param>
        /// <param name="duration">How long the call took.</param>
        /// <param name="systemPrompt">Optional system prompt used.</param>
        /// <param name="model">Optional model identifier.</param>
        /// <param name="tags">Optional tags for categorization.</param>
        /// <param name="error">Optional error message if the call failed.</param>
        /// <param name="options">Optional prompt options that were used.</param>
        public void Record(
            string prompt,
            string? response,
            TimeSpan duration,
            string? systemPrompt = null,
            string? model = null,
            IEnumerable<string>? tags = null,
            string? error = null,
            PromptOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var entry = new HistoryEntry
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Timestamp = DateTimeOffset.UtcNow,
                Prompt = prompt,
                SystemPrompt = systemPrompt,
                Response = response,
                Duration = duration,
                Model = model,
                Tags = tags != null ? new List<string>(tags) : null,
                Error = error,
                Success = error == null && response != null,
                EstimatedPromptTokens = PromptGuard.EstimateTokens(prompt),
                EstimatedResponseTokens = response != null ? PromptGuard.EstimateTokens(response) : 0,
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens
            };

            _entries.Enqueue(entry);
            Interlocked.Increment(ref _totalCount);

            // Evict oldest entries if over capacity
            while (_entries.Count > _maxEntries)
                _entries.TryDequeue(out _);
        }

        /// <summary>
        /// Wraps an async prompt call and automatically records it in history.
        /// </summary>
        /// <param name="promptCall">The async function that sends the prompt.</param>
        /// <param name="prompt">The prompt text (for recording).</param>
        /// <param name="systemPrompt">Optional system prompt (for recording).</param>
        /// <param name="model">Optional model identifier.</param>
        /// <param name="tags">Optional tags.</param>
        /// <param name="options">Optional prompt options.</param>
        /// <returns>The response from the prompt call.</returns>
        public async Task<string?> TrackAsync(
            Func<Task<string?>> promptCall,
            string? prompt = null,
            string? systemPrompt = null,
            string? model = null,
            IEnumerable<string>? tags = null,
            PromptOptions? options = null)
        {
            if (promptCall == null)
                throw new ArgumentNullException(nameof(promptCall));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            string? response = null;
            string? error = null;

            try
            {
                response = await promptCall();
                return response;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                throw;
            }
            finally
            {
                sw.Stop();
                Record(
                    prompt ?? "(tracked call)",
                    response,
                    sw.Elapsed,
                    systemPrompt,
                    model,
                    tags,
                    error,
                    options);
            }
        }

        /// <summary>
        /// Gets the most recent entries.
        /// </summary>
        /// <param name="count">Number of entries to return.</param>
        /// <returns>Entries ordered newest-first.</returns>
        public IReadOnlyList<HistoryEntry> GetRecent(int count = 10)
        {
            return _entries.Reverse().Take(count).ToList();
        }

        /// <summary>
        /// Searches history with optional filters.
        /// </summary>
        /// <param name="query">Text to search in prompt/response (case-insensitive).</param>
        /// <param name="tag">Filter by tag.</param>
        /// <param name="successOnly">If true, only return successful entries.</param>
        /// <param name="failedOnly">If true, only return failed entries.</param>
        /// <param name="minDuration">Minimum duration filter.</param>
        /// <param name="maxDuration">Maximum duration filter.</param>
        /// <param name="after">Only entries after this timestamp.</param>
        /// <param name="before">Only entries before this timestamp.</param>
        /// <param name="limit">Maximum results to return.</param>
        /// <returns>Matching entries, newest-first.</returns>
        public IReadOnlyList<HistoryEntry> Search(
            string? query = null,
            string? tag = null,
            bool? successOnly = null,
            bool? failedOnly = null,
            TimeSpan? minDuration = null,
            TimeSpan? maxDuration = null,
            DateTimeOffset? after = null,
            DateTimeOffset? before = null,
            int limit = 50)
        {
            IEnumerable<HistoryEntry> results = _entries;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query!.ToLowerInvariant();
                results = results.Where(e =>
                    (e.Prompt?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Response?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.SystemPrompt?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (tag != null)
                results = results.Where(e =>
                    e.Tags != null && e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

            if (successOnly == true)
                results = results.Where(e => e.Success);

            if (failedOnly == true)
                results = results.Where(e => !e.Success);

            if (minDuration.HasValue)
                results = results.Where(e => e.Duration >= minDuration.Value);

            if (maxDuration.HasValue)
                results = results.Where(e => e.Duration <= maxDuration.Value);

            if (after.HasValue)
                results = results.Where(e => e.Timestamp >= after.Value);

            if (before.HasValue)
                results = results.Where(e => e.Timestamp <= before.Value);

            return results.Reverse().Take(limit).ToList();
        }

        /// <summary>
        /// Gets aggregate statistics across all stored entries.
        /// </summary>
        public HistoryStatistics GetStatistics()
        {
            var entries = _entries.ToArray();

            if (entries.Length == 0)
            {
                return new HistoryStatistics
                {
                    TotalEntries = 0,
                    TotalEverRecorded = TotalCount,
                    SuccessCount = 0,
                    FailureCount = 0,
                    SuccessRate = 0
                };
            }

            var successful = entries.Where(e => e.Success).ToArray();
            var failed = entries.Where(e => !e.Success).ToArray();
            var durations = entries.Select(e => e.Duration.TotalMilliseconds).ToArray();

            return new HistoryStatistics
            {
                TotalEntries = entries.Length,
                TotalEverRecorded = TotalCount,
                SuccessCount = successful.Length,
                FailureCount = failed.Length,
                SuccessRate = entries.Length > 0
                    ? Math.Round((double)successful.Length / entries.Length * 100, 1)
                    : 0,
                AverageDurationMs = Math.Round(durations.Average(), 1),
                MedianDurationMs = Math.Round(Median(durations), 1),
                MinDurationMs = Math.Round(durations.Min(), 1),
                MaxDurationMs = Math.Round(durations.Max(), 1),
                P95DurationMs = Math.Round(Percentile(durations, 95), 1),
                TotalEstimatedTokens = entries.Sum(e =>
                    e.EstimatedPromptTokens + e.EstimatedResponseTokens),
                AveragePromptTokens = (int)Math.Round(
                    entries.Average(e => e.EstimatedPromptTokens)),
                AverageResponseTokens = (int)Math.Round(
                    entries.Average(e => e.EstimatedResponseTokens)),
                OldestEntry = entries.Min(e => e.Timestamp),
                NewestEntry = entries.Max(e => e.Timestamp),
                TopTags = entries
                    .Where(e => e.Tags != null)
                    .SelectMany(e => e.Tags!)
                    .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        /// <summary>
        /// Clears all stored history entries.
        /// </summary>
        public void Clear()
        {
            while (_entries.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Exports history to JSON.
        /// </summary>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <returns>JSON string of all entries.</returns>
        public string ExportToJson(bool indented = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(_entries.ToArray(), options);
        }

        /// <summary>
        /// Exports history to CSV format.
        /// </summary>
        /// <returns>CSV string with headers.</returns>
        public string ExportToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Timestamp,Success,DurationMs,EstPromptTokens,EstResponseTokens,Model,Tags,PromptPreview,Error");

            foreach (var e in _entries)
            {
                var preview = (e.Prompt?.Length > 80
                    ? e.Prompt[..80] + "..."
                    : e.Prompt) ?? "";

                sb.AppendLine(string.Join(",",
                    CsvEscape(e.Id),
                    e.Timestamp.ToString("o"),
                    e.Success,
                    Math.Round(e.Duration.TotalMilliseconds, 1),
                    e.EstimatedPromptTokens,
                    e.EstimatedResponseTokens,
                    CsvEscape(e.Model),
                    CsvEscape(e.Tags != null ? string.Join(";", e.Tags) : null),
                    CsvEscape(preview),
                    CsvEscape(e.Error)));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Saves history to a JSON file.
        /// </summary>
        public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            var json = ExportToJson();
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        /// <summary>
        /// Loads history from a JSON file, appending to current entries.
        /// </summary>
        public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"History file not found: {filePath}", filePath);

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var entries = JsonSerializer.Deserialize<HistoryEntry[]>(json, options);

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    _entries.Enqueue(entry);
                    Interlocked.Increment(ref _totalCount);
                }

                while (_entries.Count > _maxEntries)
                    _entries.TryDequeue(out _);
            }
        }

        // ──────────── Helpers ────────────

        /// <summary>
        /// Rough token estimate (~4 chars per token for English text).
        /// </summary>

        private static double Median(double[] sorted)
        {
            var s = sorted.OrderBy(x => x).ToArray();
            int mid = s.Length / 2;
            return s.Length % 2 == 0 ? (s[mid - 1] + s[mid]) / 2.0 : s[mid];
        }

        private static double Percentile(double[] values, double percentile)
        {
            var s = values.OrderBy(x => x).ToArray();
            double index = (percentile / 100.0) * (s.Length - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return s[lower];
            double frac = index - lower;
            return s[lower] * (1 - frac) + s[upper] * frac;
        }

        private static string CsvEscape(string? value)
        {
            if (value == null) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }

    /// <summary>
    /// A single prompt execution record.
    /// </summary>
    public class HistoryEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("systemPrompt")]
        public string? SystemPrompt { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("duration")]
        public TimeSpan Duration { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("estimatedPromptTokens")]
        public int EstimatedPromptTokens { get; set; }

        [JsonPropertyName("estimatedResponseTokens")]
        public int EstimatedResponseTokens { get; set; }

        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }

        [JsonPropertyName("maxTokens")]
        public int? MaxTokens { get; set; }
    }

    /// <summary>
    /// Aggregate statistics across history entries.
    /// </summary>
    public class HistoryStatistics
    {
        public int TotalEntries { get; set; }
        public long TotalEverRecorded { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double SuccessRate { get; set; }
        public double AverageDurationMs { get; set; }
        public double MedianDurationMs { get; set; }
        public double MinDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
        public double P95DurationMs { get; set; }
        public long TotalEstimatedTokens { get; set; }
        public int AveragePromptTokens { get; set; }
        public int AverageResponseTokens { get; set; }
        public DateTimeOffset OldestEntry { get; set; }
        public DateTimeOffset NewestEntry { get; set; }
        public Dictionary<string, int> TopTags { get; set; } = new();
    }
}
