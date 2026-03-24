namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Staleness severity indicating how urgently a prompt needs review.
    /// </summary>
    public enum StalenessSeverity
    {
        /// <summary>Prompt is fresh — no action needed.</summary>
        Fresh = 0,

        /// <summary>Prompt is aging — consider reviewing soon.</summary>
        Aging = 1,

        /// <summary>Prompt is stale — should be reviewed.</summary>
        Stale = 2,

        /// <summary>Prompt is critically stale — likely outdated.</summary>
        Critical = 3
    }

    /// <summary>
    /// Records when a prompt was last updated and against which model version.
    /// </summary>
    public class PromptAge
    {
        /// <summary>Gets or sets the prompt identifier.</summary>
        [JsonPropertyName("promptId")]
        public string PromptId { get; set; } = "";

        /// <summary>Gets or sets the prompt name or label.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>Gets or sets when the prompt was last modified.</summary>
        [JsonPropertyName("lastModified")]
        public DateTimeOffset LastModified { get; set; }

        /// <summary>Gets or sets the model version the prompt was last tested/tuned against.</summary>
        [JsonPropertyName("targetModel")]
        public string TargetModel { get; set; } = "";

        /// <summary>Gets or sets optional tags for categorization.</summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// The result of a staleness check for a single prompt.
    /// </summary>
    public class StalenessReport
    {
        /// <summary>Gets the prompt that was analyzed.</summary>
        [JsonPropertyName("prompt")]
        public PromptAge Prompt { get; init; } = new();

        /// <summary>Gets the severity level.</summary>
        [JsonPropertyName("severity")]
        public StalenessSeverity Severity { get; init; }

        /// <summary>Gets how many days since the prompt was last modified.</summary>
        [JsonPropertyName("daysSinceUpdate")]
        public int DaysSinceUpdate { get; init; }

        /// <summary>Gets whether the target model is behind the current model.</summary>
        [JsonPropertyName("modelDrift")]
        public bool ModelDrift { get; init; }

        /// <summary>Gets a human-readable recommendation.</summary>
        [JsonPropertyName("recommendation")]
        public string Recommendation { get; init; } = "";
    }

    /// <summary>
    /// A bulk staleness scan result.
    /// </summary>
    public class StalenessScan
    {
        /// <summary>Gets when the scan was performed.</summary>
        [JsonPropertyName("scannedAt")]
        public DateTimeOffset ScannedAt { get; init; }

        /// <summary>Gets the current model used as the baseline.</summary>
        [JsonPropertyName("currentModel")]
        public string CurrentModel { get; init; } = "";

        /// <summary>Gets the total number of prompts scanned.</summary>
        [JsonPropertyName("totalPrompts")]
        public int TotalPrompts { get; init; }

        /// <summary>Gets the number of stale prompts (Stale or Critical).</summary>
        [JsonPropertyName("staleCount")]
        public int StaleCount { get; init; }

        /// <summary>Gets individual reports sorted by severity (worst first).</summary>
        [JsonPropertyName("reports")]
        public List<StalenessReport> Reports { get; init; } = new();

        /// <summary>
        /// Serializes the scan to indented JSON.
        /// </summary>
        public string ToJson() =>
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Configuration for staleness thresholds.
    /// </summary>
    public class StalenessThresholds
    {
        /// <summary>Days before a prompt is considered Aging (default 30).</summary>
        public int AgingDays { get; set; } = 30;

        /// <summary>Days before a prompt is considered Stale (default 90).</summary>
        public int StaleDays { get; set; } = 90;

        /// <summary>Days before a prompt is considered Critical (default 180).</summary>
        public int CriticalDays { get; set; } = 180;
    }

    /// <summary>
    /// Detects prompts that are potentially stale based on age and model drift.
    /// Helps teams maintain prompt hygiene by identifying prompts that haven't been
    /// updated as models evolve, which often leads to degraded performance.
    /// </summary>
    /// <example>
    /// <code>
    /// var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
    /// detector.Register(new PromptAge
    /// {
    ///     PromptId = "summarizer-v2",
    ///     Name = "Article Summarizer",
    ///     LastModified = DateTimeOffset.Now.AddDays(-120),
    ///     TargetModel = "gpt-4-0613"
    /// });
    /// var scan = detector.Scan();
    /// Console.WriteLine($"Found {scan.StaleCount} stale prompts");
    /// </code>
    /// </example>
    public class PromptStaleDetector
    {
        private readonly string _currentModel;
        private readonly StalenessThresholds _thresholds;
        private readonly List<PromptAge> _prompts = new();
        private readonly Dictionary<string, List<string>> _modelFamilies = new()
        {
            ["gpt-4o"] = new() { "gpt-4o-2024-05-13", "gpt-4o-2024-08-06", "gpt-4o-2024-11-20" },
            ["gpt-4"] = new() { "gpt-4-0314", "gpt-4-0613", "gpt-4-1106-preview", "gpt-4-turbo-2024-04-09" },
            ["gpt-3.5"] = new() { "gpt-3.5-turbo-0301", "gpt-3.5-turbo-0613", "gpt-3.5-turbo-1106" },
            ["claude-3"] = new() { "claude-3-haiku", "claude-3-sonnet", "claude-3-opus", "claude-3.5-sonnet", "claude-3.5-haiku" },
            ["gemini"] = new() { "gemini-1.0-pro", "gemini-1.5-flash", "gemini-1.5-pro", "gemini-2.0-flash" }
        };

        /// <summary>
        /// Creates a new stale detector with the given current model as baseline.
        /// </summary>
        /// <param name="currentModel">The model version currently in use.</param>
        /// <param name="thresholds">Optional custom thresholds.</param>
        public PromptStaleDetector(string currentModel, StalenessThresholds? thresholds = null)
        {
            _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
            _thresholds = thresholds ?? new StalenessThresholds();
        }

        /// <summary>
        /// Registers a prompt for staleness tracking.
        /// </summary>
        public PromptStaleDetector Register(PromptAge prompt)
        {
            _prompts.Add(prompt ?? throw new ArgumentNullException(nameof(prompt)));
            return this;
        }

        /// <summary>
        /// Registers multiple prompts at once.
        /// </summary>
        public PromptStaleDetector RegisterAll(IEnumerable<PromptAge> prompts)
        {
            foreach (var p in prompts) Register(p);
            return this;
        }

        /// <summary>
        /// Adds or replaces a model family for drift detection.
        /// Versions should be ordered oldest to newest.
        /// </summary>
        public PromptStaleDetector AddModelFamily(string family, List<string> versions)
        {
            _modelFamilies[family] = versions;
            return this;
        }

        /// <summary>
        /// Scans all registered prompts and returns a staleness report.
        /// </summary>
        public StalenessScan Scan(DateTimeOffset? asOf = null)
        {
            var now = asOf ?? DateTimeOffset.UtcNow;
            var reports = new List<StalenessReport>();

            foreach (var prompt in _prompts)
            {
                var days = (int)(now - prompt.LastModified).TotalDays;
                var drift = HasModelDrift(prompt.TargetModel);

                var severity = StalenessSeverity.Fresh;
                if (days >= _thresholds.CriticalDays || (days >= _thresholds.StaleDays && drift))
                    severity = StalenessSeverity.Critical;
                else if (days >= _thresholds.StaleDays)
                    severity = StalenessSeverity.Stale;
                else if (days >= _thresholds.AgingDays || drift)
                    severity = StalenessSeverity.Aging;

                var recommendation = severity switch
                {
                    StalenessSeverity.Fresh => "No action needed.",
                    StalenessSeverity.Aging when drift =>
                        $"Model has evolved from {prompt.TargetModel} → {_currentModel}. Consider re-testing.",
                    StalenessSeverity.Aging =>
                        $"Last updated {days} days ago. Schedule a review.",
                    StalenessSeverity.Stale =>
                        $"Not updated in {days} days. Performance may have drifted. Review recommended.",
                    StalenessSeverity.Critical =>
                        $"Critically stale ({days} days, model drift: {drift}). Immediate review required.",
                    _ => ""
                };

                reports.Add(new StalenessReport
                {
                    Prompt = prompt,
                    Severity = severity,
                    DaysSinceUpdate = days,
                    ModelDrift = drift,
                    Recommendation = recommendation
                });
            }

            reports.Sort((a, b) =>
            {
                var s = b.Severity.CompareTo(a.Severity);
                return s != 0 ? s : b.DaysSinceUpdate.CompareTo(a.DaysSinceUpdate);
            });

            return new StalenessScan
            {
                ScannedAt = now,
                CurrentModel = _currentModel,
                TotalPrompts = _prompts.Count,
                StaleCount = reports.Count(r => r.Severity >= StalenessSeverity.Stale),
                Reports = reports
            };
        }

        /// <summary>
        /// Returns only prompts at or above the given severity.
        /// </summary>
        public List<StalenessReport> FindStale(StalenessSeverity minSeverity = StalenessSeverity.Stale, DateTimeOffset? asOf = null)
        {
            return Scan(asOf).Reports.Where(r => r.Severity >= minSeverity).ToList();
        }

        /// <summary>
        /// Filters to prompts matching any of the given tags, then scans.
        /// </summary>
        public StalenessScan ScanByTags(IEnumerable<string> tags, DateTimeOffset? asOf = null)
        {
            var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            var filtered = _prompts.Where(p => p.Tags.Any(t => tagSet.Contains(t))).ToList();

            var tempDetector = new PromptStaleDetector(_currentModel, _thresholds);
            tempDetector.RegisterAll(filtered);
            foreach (var kv in _modelFamilies) tempDetector.AddModelFamily(kv.Key, kv.Value);
            return tempDetector.Scan(asOf);
        }

        /// <summary>
        /// Returns a quick text summary suitable for logging or CLI output.
        /// </summary>
        public string Summarize(DateTimeOffset? asOf = null)
        {
            var scan = Scan(asOf);
            var lines = new List<string>
            {
                $"Prompt Staleness Scan — {scan.ScannedAt:yyyy-MM-dd HH:mm} UTC",
                $"Current Model: {scan.CurrentModel}",
                $"Total Prompts: {scan.TotalPrompts} | Stale: {scan.StaleCount}",
                new string('─', 60)
            };

            foreach (var r in scan.Reports)
            {
                var icon = r.Severity switch
                {
                    StalenessSeverity.Fresh => "✅",
                    StalenessSeverity.Aging => "🟡",
                    StalenessSeverity.Stale => "🟠",
                    StalenessSeverity.Critical => "🔴",
                    _ => "  "
                };
                lines.Add($"{icon} [{r.Severity}] {r.Prompt.Name} ({r.Prompt.PromptId})");
                lines.Add($"   Last updated: {r.DaysSinceUpdate}d ago | Model: {r.Prompt.TargetModel} | Drift: {r.ModelDrift}");
                lines.Add($"   → {r.Recommendation}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private bool HasModelDrift(string targetModel)
        {
            if (string.IsNullOrEmpty(targetModel) || targetModel == _currentModel)
                return false;

            // Check if both are in the same family but target is older
            foreach (var (_, versions) in _modelFamilies)
            {
                var targetIdx = versions.IndexOf(targetModel);
                var currentIdx = versions.IndexOf(_currentModel);
                if (targetIdx >= 0 && currentIdx >= 0)
                    return targetIdx < currentIdx;
            }

            // Different families or unknown models — assume drift if names differ
            return true;
        }
    }
}
