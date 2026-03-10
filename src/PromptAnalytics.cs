namespace Prompt
{
    using System.Collections.Concurrent;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Records and reports usage statistics for prompt template renders.
    /// Tracks render counts, timing, error rates, variable popularity,
    /// and produces usage reports.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var analytics = new PromptAnalytics();
    /// var template = new PromptTemplate("Hello {{name}}!");
    ///
    /// // Track a render
    /// var tracker = analytics.StartRender("greet");
    /// string result = template.Render(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["name"] = "World"
    /// });
    /// tracker.Complete(new[] { "name" });
    ///
    /// // Get stats
    /// var stats = analytics.GetStats("greet");
    /// Console.WriteLine($"Renders: {stats.RenderCount}, Avg: {stats.AverageRenderMs:F1}ms");
    ///
    /// // Generate report
    /// string report = analytics.GenerateReport();
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptAnalytics
    {
        private readonly ConcurrentDictionary<string, TemplateStats> _stats = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the number of templates being tracked.
        /// </summary>
        public int TrackedTemplateCount => _stats.Count;

        /// <summary>
        /// Starts tracking a render for the given template name.
        /// Returns a <see cref="RenderTracker"/> that should be completed
        /// or failed when the render finishes.
        /// </summary>
        /// <param name="templateName">Name of the template being rendered.</param>
        /// <returns>A tracker to complete or fail.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="templateName"/> is null or empty.
        /// </exception>
        public RenderTracker StartRender(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException("Template name cannot be null or empty.", nameof(templateName));

            var stats = _stats.GetOrAdd(templateName.Trim(), _ => new TemplateStats(templateName.Trim()));
            return new RenderTracker(stats);
        }

        /// <summary>
        /// Records a simple render event without timing. Useful for
        /// bulk-importing historical data.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <param name="variables">Variables used in the render.</param>
        public void RecordRender(string templateName, IEnumerable<string>? variables = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException("Template name cannot be null or empty.", nameof(templateName));

            var stats = _stats.GetOrAdd(templateName.Trim(), _ => new TemplateStats(templateName.Trim()));
            stats.RecordSuccess(0, variables);
        }

        /// <summary>
        /// Records a render error.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <param name="errorMessage">Description of the error.</param>
        public void RecordError(string templateName, string? errorMessage = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException("Template name cannot be null or empty.", nameof(templateName));

            var stats = _stats.GetOrAdd(templateName.Trim(), _ => new TemplateStats(templateName.Trim()));
            stats.RecordError(errorMessage);
        }

        /// <summary>
        /// Gets usage statistics for a specific template.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <returns>Statistics snapshot, or null if not tracked.</returns>
        public TemplateStatsSnapshot? GetStats(string templateName)
        {
            if (_stats.TryGetValue(templateName, out var stats))
                return stats.ToSnapshot();
            return null;
        }

        /// <summary>
        /// Gets usage statistics for all tracked templates, ordered by
        /// render count descending.
        /// </summary>
        public IReadOnlyList<TemplateStatsSnapshot> GetAllStats()
        {
            return _stats.Values
                .Select(s => s.ToSnapshot())
                .OrderByDescending(s => s.TotalRenders)
                .ToList();
        }

        /// <summary>
        /// Gets the top N most-used templates.
        /// </summary>
        /// <param name="count">Number of templates to return.</param>
        public IReadOnlyList<TemplateStatsSnapshot> GetTopTemplates(int count = 10)
        {
            return GetAllStats().Take(Math.Max(1, count)).ToList();
        }

        /// <summary>
        /// Gets templates with the highest error rates (at least 1 error).
        /// </summary>
        /// <param name="count">Number of templates to return.</param>
        public IReadOnlyList<TemplateStatsSnapshot> GetErrorProne(int count = 10)
        {
            return _stats.Values
                .Select(s => s.ToSnapshot())
                .Where(s => s.ErrorCount > 0)
                .OrderByDescending(s => s.ErrorRate)
                .Take(Math.Max(1, count))
                .ToList();
        }

        /// <summary>
        /// Gets the most frequently used variables across all templates.
        /// </summary>
        /// <param name="count">Number of variables to return.</param>
        public IReadOnlyList<KeyValuePair<string, int>> GetTopVariables(int count = 20)
        {
            var variableCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var stats in _stats.Values)
            {
                foreach (var kvp in stats.GetVariableCounts())
                {
                    if (variableCounts.ContainsKey(kvp.Key))
                        variableCounts[kvp.Key] += kvp.Value;
                    else
                        variableCounts[kvp.Key] = kvp.Value;
                }
            }

            return variableCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(Math.Max(1, count))
                .ToList();
        }

        /// <summary>
        /// Generates a human-readable usage report.
        /// </summary>
        public string GenerateReport()
        {
            var all = GetAllStats();
            if (all.Count == 0)
                return "No analytics data recorded.";

            var lines = new List<string>
            {
                "═══ Prompt Analytics Report ═══",
                $"Templates tracked: {all.Count}",
                $"Total renders: {all.Sum(s => s.TotalRenders)}",
                $"Total errors: {all.Sum(s => s.ErrorCount)}",
                ""
            };

            // Top templates
            lines.Add("── Top Templates by Usage ──");
            foreach (var s in all.Take(10))
            {
                lines.Add($"  {s.TemplateName}: {s.RenderCount} renders, {s.ErrorCount} errors" +
                    (s.AverageRenderMs > 0 ? $", avg {s.AverageRenderMs:F1}ms" : ""));
            }

            // Error-prone templates
            var errorProne = all.Where(s => s.ErrorCount > 0).OrderByDescending(s => s.ErrorRate).Take(5).ToList();
            if (errorProne.Count > 0)
            {
                lines.Add("");
                lines.Add("── Error-Prone Templates ──");
                foreach (var s in errorProne)
                {
                    lines.Add($"  {s.TemplateName}: {s.ErrorRate:P1} error rate ({s.ErrorCount}/{s.TotalRenders})");
                }
            }

            // Top variables
            var topVars = GetTopVariables(10);
            if (topVars.Count > 0)
            {
                lines.Add("");
                lines.Add("── Most Used Variables ──");
                foreach (var kvp in topVars)
                {
                    lines.Add($"  {{{{{kvp.Key}}}}}: {kvp.Value} uses");
                }
            }

            // Slowest templates
            var slowest = all.Where(s => s.AverageRenderMs > 0).OrderByDescending(s => s.AverageRenderMs).Take(5).ToList();
            if (slowest.Count > 0)
            {
                lines.Add("");
                lines.Add("── Slowest Templates ──");
                foreach (var s in slowest)
                {
                    lines.Add($"  {s.TemplateName}: avg {s.AverageRenderMs:F1}ms, max {s.MaxRenderMs:F1}ms");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Exports all analytics data to JSON.
        /// </summary>
        /// <param name="indented">Whether to format with indentation.</param>
        public string ToJson(bool indented = true)
        {
            var data = new AnalyticsData
            {
                ExportedAt = DateTimeOffset.UtcNow,
                Templates = GetAllStats().Select(s => new AnalyticsTemplateData
                {
                    Name = s.TemplateName,
                    RenderCount = s.RenderCount,
                    ErrorCount = s.ErrorCount,
                    TotalRenders = s.TotalRenders,
                    AverageRenderMs = Math.Round(s.AverageRenderMs, 2),
                    MaxRenderMs = Math.Round(s.MaxRenderMs, 2),
                    MinRenderMs = Math.Round(s.MinRenderMs, 2),
                    FirstUsed = s.FirstUsed,
                    LastUsed = s.LastUsed,
                    TopVariables = s.TopVariables.Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    RecentErrors = s.RecentErrors.ToList()
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(data, options);
        }

        /// <summary>
        /// Imports analytics data from JSON, merging with existing data.
        /// </summary>
        /// <param name="json">JSON string from <see cref="ToJson"/>.</param>
        /// <returns>Number of templates imported.</returns>
        public static PromptAnalytics FromJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json);

            var data = SerializationGuards.SafeDeserialize<AnalyticsData>(json);

            var analytics = new PromptAnalytics();
            foreach (var t in data.Templates)
            {
                for (int i = 0; i < t.RenderCount; i++)
                    analytics.RecordRender(t.Name, t.TopVariables?.Keys);
                for (int i = 0; i < t.ErrorCount; i++)
                    analytics.RecordError(t.Name, t.RecentErrors?.ElementAtOrDefault(i));
            }
            return analytics;
        }

        /// <summary>
        /// Clears all analytics data.
        /// </summary>
        public void Clear() => _stats.Clear();

        /// <summary>
        /// Removes analytics for a specific template.
        /// </summary>
        /// <param name="templateName">Template to remove.</param>
        /// <returns>True if removed.</returns>
        public bool Remove(string templateName) => _stats.TryRemove(templateName, out _);
    }

    /// <summary>
    /// Tracks the timing of a single render operation. Call
    /// <see cref="Complete"/> on success or <see cref="Fail"/> on error.
    /// </summary>
    public class RenderTracker
    {
        private readonly TemplateStats _stats;
        private readonly long _startTicks;
        private bool _finished;

        internal RenderTracker(TemplateStats stats)
        {
            _stats = stats;
            _startTicks = Environment.TickCount64;
        }

        /// <summary>
        /// Marks the render as successfully completed.
        /// </summary>
        /// <param name="variables">Variables used in the render.</param>
        public void Complete(IEnumerable<string>? variables = null)
        {
            if (_finished) return;
            _finished = true;
            double ms = Environment.TickCount64 - _startTicks;
            _stats.RecordSuccess(ms, variables);
        }

        /// <summary>
        /// Marks the render as failed.
        /// </summary>
        /// <param name="errorMessage">Description of the error.</param>
        public void Fail(string? errorMessage = null)
        {
            if (_finished) return;
            _finished = true;
            _stats.RecordError(errorMessage);
        }
    }

    /// <summary>
    /// Thread-safe mutable statistics for a single template.
    /// </summary>
    internal class TemplateStats
    {
        private readonly object _lock = new();
        private readonly string _name;
        private int _renderCount;
        private int _errorCount;
        private double _totalMs;
        private double _maxMs;
        private double _minMs = double.MaxValue;
        private DateTimeOffset _firstUsed = DateTimeOffset.MaxValue;
        private DateTimeOffset _lastUsed = DateTimeOffset.MinValue;
        private readonly Dictionary<string, int> _variableCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _recentErrors = new();
        private const int MaxRecentErrors = 10;

        internal TemplateStats(string name) => _name = name;

        internal void RecordSuccess(double ms, IEnumerable<string>? variables)
        {
            lock (_lock)
            {
                _renderCount++;
                _totalMs += ms;
                if (ms > _maxMs) _maxMs = ms;
                if (ms < _minMs) _minMs = ms;

                var now = DateTimeOffset.UtcNow;
                if (now < _firstUsed) _firstUsed = now;
                if (now > _lastUsed) _lastUsed = now;

                if (variables != null)
                {
                    foreach (var v in variables)
                    {
                        var key = v.Trim();
                        if (string.IsNullOrEmpty(key)) continue;
                        _variableCounts.TryGetValue(key, out int count);
                        _variableCounts[key] = count + 1;
                    }
                }
            }
        }

        internal void RecordError(string? errorMessage)
        {
            lock (_lock)
            {
                _errorCount++;
                var now = DateTimeOffset.UtcNow;
                if (now < _firstUsed) _firstUsed = now;
                if (now > _lastUsed) _lastUsed = now;

                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    _recentErrors.Enqueue(errorMessage);
                    while (_recentErrors.Count > MaxRecentErrors)
                        _recentErrors.Dequeue();
                }
            }
        }

        internal Dictionary<string, int> GetVariableCounts()
        {
            lock (_lock)
                return new Dictionary<string, int>(_variableCounts, StringComparer.OrdinalIgnoreCase);
        }

        internal TemplateStatsSnapshot ToSnapshot()
        {
            lock (_lock)
            {
                return new TemplateStatsSnapshot
                {
                    TemplateName = _name,
                    RenderCount = _renderCount,
                    ErrorCount = _errorCount,
                    TotalRenders = _renderCount + _errorCount,
                    TotalRenderMs = _totalMs,
                    AverageRenderMs = _renderCount > 0 ? _totalMs / _renderCount : 0,
                    MaxRenderMs = _maxMs,
                    MinRenderMs = _minMs == double.MaxValue ? 0 : _minMs,
                    FirstUsed = _firstUsed == DateTimeOffset.MaxValue ? DateTimeOffset.UtcNow : _firstUsed,
                    LastUsed = _lastUsed == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : _lastUsed,
                    TopVariables = _variableCounts
                        .OrderByDescending(kvp => kvp.Value)
                        .ToList(),
                    RecentErrors = _recentErrors.ToList()
                };
            }
        }
    }

    /// <summary>
    /// Immutable snapshot of usage statistics for a single template.
    /// </summary>
    public class TemplateStatsSnapshot
    {
        /// <summary>Template name.</summary>
        public string TemplateName { get; init; } = "";

        /// <summary>Number of successful renders.</summary>
        public int RenderCount { get; init; }

        /// <summary>Number of failed renders.</summary>
        public int ErrorCount { get; init; }

        /// <summary>Total renders (success + error).</summary>
        public int TotalRenders { get; init; }

        /// <summary>Total render time in milliseconds (successes only).</summary>
        public double TotalRenderMs { get; init; }

        /// <summary>Average render time in milliseconds.</summary>
        public double AverageRenderMs { get; init; }

        /// <summary>Maximum render time in milliseconds.</summary>
        public double MaxRenderMs { get; init; }

        /// <summary>Minimum render time in milliseconds.</summary>
        public double MinRenderMs { get; init; }

        /// <summary>Error rate (errors / total).</summary>
        public double ErrorRate => TotalRenders > 0 ? (double)ErrorCount / TotalRenders : 0;

        /// <summary>When this template was first used.</summary>
        public DateTimeOffset FirstUsed { get; init; }

        /// <summary>When this template was last used.</summary>
        public DateTimeOffset LastUsed { get; init; }

        /// <summary>Most frequently used variables, sorted by count.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> TopVariables { get; init; } = Array.Empty<KeyValuePair<string, int>>();

        /// <summary>Recent error messages (up to 10).</summary>
        public IReadOnlyList<string> RecentErrors { get; init; } = Array.Empty<string>();
    }

    // ──────────────── Serialization DTOs ────────────────

    internal class AnalyticsData
    {
        [JsonPropertyName("exportedAt")]
        public DateTimeOffset ExportedAt { get; set; }

        [JsonPropertyName("templates")]
        public List<AnalyticsTemplateData> Templates { get; set; } = new();
    }

    internal class AnalyticsTemplateData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("renderCount")]
        public int RenderCount { get; set; }

        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; set; }

        [JsonPropertyName("totalRenders")]
        public int TotalRenders { get; set; }

        [JsonPropertyName("averageRenderMs")]
        public double AverageRenderMs { get; set; }

        [JsonPropertyName("maxRenderMs")]
        public double MaxRenderMs { get; set; }

        [JsonPropertyName("minRenderMs")]
        public double MinRenderMs { get; set; }

        [JsonPropertyName("firstUsed")]
        public DateTimeOffset FirstUsed { get; set; }

        [JsonPropertyName("lastUsed")]
        public DateTimeOffset LastUsed { get; set; }

        [JsonPropertyName("topVariables")]
        public Dictionary<string, int>? TopVariables { get; set; }

        [JsonPropertyName("recentErrors")]
        public List<string>? RecentErrors { get; set; }
    }
}
