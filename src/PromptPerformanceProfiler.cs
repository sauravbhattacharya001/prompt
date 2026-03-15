namespace Prompt
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ── Data Types ───────────────────────────────────────────

    /// <summary>
    /// A single recorded execution sample from prompt profiling.
    /// </summary>
    public class ProfileSample
    {
        /// <summary>Name/label of the prompt or variant that was profiled.</summary>
        public string Label { get; }

        /// <summary>Wall-clock duration of the execution.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Number of input tokens (if known).</summary>
        public int? InputTokens { get; }

        /// <summary>Number of output tokens (if known).</summary>
        public int? OutputTokens { get; }

        /// <summary>Timestamp when the sample was recorded.</summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>Optional metadata tags attached to this sample.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }

        /// <summary>Whether this execution was considered a success.</summary>
        public bool Success { get; }

        /// <summary>Error message if the execution failed.</summary>
        public string? Error { get; }

        public ProfileSample(string label, TimeSpan duration, int? inputTokens = null,
            int? outputTokens = null, bool success = true, string? error = null,
            Dictionary<string, string>? tags = null, DateTimeOffset? timestamp = null)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Duration = duration;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
            Success = success;
            Error = error;
            Tags = tags != null ? new Dictionary<string, string>(tags) : new Dictionary<string, string>();
            Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        }

        /// <summary>Total tokens (input + output) if both are known.</summary>
        public int? TotalTokens => (InputTokens.HasValue && OutputTokens.HasValue)
            ? InputTokens.Value + OutputTokens.Value : null;
    }

    /// <summary>
    /// Aggregated statistics for a set of profile samples.
    /// </summary>
    public class ProfileStats
    {
        public string Label { get; set; } = "";
        public int SampleCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double SuccessRate { get; set; }

        // Latency stats (milliseconds)
        public double MeanLatencyMs { get; set; }
        public double MedianLatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }
        public double MinLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double StdDevLatencyMs { get; set; }

        // Token stats
        public double? MeanInputTokens { get; set; }
        public double? MeanOutputTokens { get; set; }
        public double? MeanTotalTokens { get; set; }
        public double? TokensPerSecond { get; set; }

        // Time range
        public DateTimeOffset? FirstSample { get; set; }
        public DateTimeOffset? LastSample { get; set; }
    }

    /// <summary>
    /// Comparison result between two profiled prompt variants.
    /// </summary>
    public class ProfileComparison
    {
        public string LabelA { get; set; } = "";
        public string LabelB { get; set; } = "";
        public ProfileStats StatsA { get; set; } = new();
        public ProfileStats StatsB { get; set; } = new();

        /// <summary>Percentage speedup of B over A (positive = B is faster).</summary>
        public double LatencyDiffPercent { get; set; }

        /// <summary>Percentage token savings of B over A (positive = B uses fewer).</summary>
        public double? TokenDiffPercent { get; set; }

        /// <summary>Which label is the recommended winner.</summary>
        public string? Winner { get; set; }

        /// <summary>Reason for the recommendation.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Profiles prompt execution performance — records samples, computes statistics
    /// (mean, median, p95, p99, std dev), compares variants, and generates reports.
    /// Thread-safe for concurrent recording.
    /// </summary>
    /// <example>
    /// <code>
    /// var profiler = new PromptPerformanceProfiler();
    ///
    /// // Record samples using the timing helper
    /// using (var ctx = profiler.StartSample("my-prompt"))
    /// {
    ///     // ... call LLM ...
    ///     ctx.SetTokens(inputTokens: 150, outputTokens: 80);
    /// }
    ///
    /// // Or record manually
    /// profiler.Record("my-prompt", TimeSpan.FromMilliseconds(320), inputTokens: 100, outputTokens: 50);
    ///
    /// // Get stats
    /// var stats = profiler.GetStats("my-prompt");
    /// Console.WriteLine($"p95: {stats.P95LatencyMs}ms");
    ///
    /// // Compare two variants
    /// var cmp = profiler.Compare("variant-a", "variant-b");
    /// Console.WriteLine($"Winner: {cmp.Winner} ({cmp.Reason})");
    ///
    /// // Export report
    /// string report = profiler.GenerateReport();
    /// </code>
    /// </example>
    public class PromptPerformanceProfiler
    {
        private readonly ConcurrentDictionary<string, List<ProfileSample>> _samples = new();
        private readonly object _lock = new();

        /// <summary>
        /// Records a profile sample.
        /// </summary>
        public void Record(string label, TimeSpan duration, int? inputTokens = null,
            int? outputTokens = null, bool success = true, string? error = null,
            Dictionary<string, string>? tags = null)
        {
            var sample = new ProfileSample(label, duration, inputTokens, outputTokens,
                success, error, tags);
            AddSample(label, sample);
        }

        /// <summary>
        /// Starts a timed sample. Dispose the returned context to finish recording.
        /// </summary>
        public ProfileSampleContext StartSample(string label, Dictionary<string, string>? tags = null)
        {
            return new ProfileSampleContext(this, label, tags);
        }

        internal void AddSample(string label, ProfileSample sample)
        {
            _samples.AddOrUpdate(label,
                _ => new List<ProfileSample> { sample },
                (_, list) => { lock (_lock) { list.Add(sample); } return list; });
        }

        /// <summary>
        /// Returns all labels that have been profiled.
        /// </summary>
        public IReadOnlyList<string> GetLabels()
        {
            return _samples.Keys.OrderBy(k => k).ToList();
        }

        /// <summary>
        /// Returns all samples for a label.
        /// </summary>
        public IReadOnlyList<ProfileSample> GetSamples(string label)
        {
            if (_samples.TryGetValue(label, out var list))
            {
                lock (_lock) { return list.ToList(); }
            }
            return Array.Empty<ProfileSample>();
        }

        /// <summary>
        /// Computes aggregate statistics for a given label.
        /// </summary>
        public ProfileStats GetStats(string label)
        {
            var samples = GetSamples(label);
            if (samples.Count == 0)
                return new ProfileStats { Label = label };

            var latencies = samples.Select(s => s.Duration.TotalMilliseconds).OrderBy(x => x).ToList();
            var successes = samples.Count(s => s.Success);

            var stats = new ProfileStats
            {
                Label = label,
                SampleCount = samples.Count,
                SuccessCount = successes,
                FailureCount = samples.Count - successes,
                SuccessRate = (double)successes / samples.Count,
                MeanLatencyMs = latencies.Average(),
                MedianLatencyMs = Percentile(latencies, 0.5),
                P95LatencyMs = Percentile(latencies, 0.95),
                P99LatencyMs = Percentile(latencies, 0.99),
                MinLatencyMs = latencies.First(),
                MaxLatencyMs = latencies.Last(),
                StdDevLatencyMs = StdDev(latencies),
                FirstSample = samples.Min(s => s.Timestamp),
                LastSample = samples.Max(s => s.Timestamp)
            };

            var withInput = samples.Where(s => s.InputTokens.HasValue).ToList();
            var withOutput = samples.Where(s => s.OutputTokens.HasValue).ToList();
            var withTotal = samples.Where(s => s.TotalTokens.HasValue).ToList();

            if (withInput.Count > 0)
                stats.MeanInputTokens = withInput.Average(s => s.InputTokens!.Value);
            if (withOutput.Count > 0)
                stats.MeanOutputTokens = withOutput.Average(s => s.OutputTokens!.Value);
            if (withTotal.Count > 0)
            {
                stats.MeanTotalTokens = withTotal.Average(s => s.TotalTokens!.Value);
                var avgTokens = stats.MeanTotalTokens.Value;
                var avgSeconds = latencies.Average() / 1000.0;
                if (avgSeconds > 0)
                    stats.TokensPerSecond = avgTokens / avgSeconds;
            }

            return stats;
        }

        /// <summary>
        /// Compares two profiled labels and recommends a winner.
        /// </summary>
        public ProfileComparison Compare(string labelA, string labelB)
        {
            var statsA = GetStats(labelA);
            var statsB = GetStats(labelB);

            if (statsA.SampleCount == 0)
                throw new InvalidOperationException($"No samples found for '{labelA}'.");
            if (statsB.SampleCount == 0)
                throw new InvalidOperationException($"No samples found for '{labelB}'.");

            var cmp = new ProfileComparison
            {
                LabelA = labelA,
                LabelB = labelB,
                StatsA = statsA,
                StatsB = statsB
            };

            // Latency comparison (positive = B is faster)
            if (statsA.MeanLatencyMs > 0)
                cmp.LatencyDiffPercent = ((statsA.MeanLatencyMs - statsB.MeanLatencyMs) / statsA.MeanLatencyMs) * 100;

            // Token comparison (positive = B uses fewer)
            if (statsA.MeanTotalTokens.HasValue && statsB.MeanTotalTokens.HasValue && statsA.MeanTotalTokens > 0)
                cmp.TokenDiffPercent = ((statsA.MeanTotalTokens.Value - statsB.MeanTotalTokens.Value) / statsA.MeanTotalTokens.Value) * 100;

            // Determine winner based on combined score
            double scoreA = 0, scoreB = 0;

            // Latency (lower is better, weight: 40%)
            if (statsA.P95LatencyMs < statsB.P95LatencyMs) scoreA += 40; else scoreB += 40;

            // Success rate (higher is better, weight: 35%)
            if (statsA.SuccessRate >= statsB.SuccessRate) scoreA += 35; else scoreB += 35;

            // Token efficiency (lower is better, weight: 25%)
            if (statsA.MeanTotalTokens.HasValue && statsB.MeanTotalTokens.HasValue)
            {
                if (statsA.MeanTotalTokens <= statsB.MeanTotalTokens) scoreA += 25; else scoreB += 25;
            }

            if (scoreA > scoreB)
            {
                cmp.Winner = labelA;
                cmp.Reason = $"{labelA} wins with score {scoreA} vs {scoreB} (latency p95: {statsA.P95LatencyMs:F1}ms vs {statsB.P95LatencyMs:F1}ms, success: {statsA.SuccessRate:P0} vs {statsB.SuccessRate:P0})";
            }
            else if (scoreB > scoreA)
            {
                cmp.Winner = labelB;
                cmp.Reason = $"{labelB} wins with score {scoreB} vs {scoreA} (latency p95: {statsB.P95LatencyMs:F1}ms vs {statsA.P95LatencyMs:F1}ms, success: {statsB.SuccessRate:P0} vs {statsA.SuccessRate:P0})";
            }
            else
            {
                cmp.Winner = null;
                cmp.Reason = "Tie — both variants perform similarly.";
            }

            return cmp;
        }

        /// <summary>
        /// Generates a human-readable text report of all profiled labels.
        /// </summary>
        public string GenerateReport(string? title = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title ?? "Prompt Performance Profile Report");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:u}");
            sb.AppendLine();

            var labels = GetLabels();
            if (labels.Count == 0)
            {
                sb.AppendLine("No samples recorded.");
                return sb.ToString();
            }

            foreach (var label in labels)
            {
                var stats = GetStats(label);
                sb.AppendLine($"── {label} ({stats.SampleCount} samples) ──");
                sb.AppendLine($"  Success Rate : {stats.SuccessRate:P1}");
                sb.AppendLine($"  Latency (ms) : mean={stats.MeanLatencyMs:F1}  median={stats.MedianLatencyMs:F1}  p95={stats.P95LatencyMs:F1}  p99={stats.P99LatencyMs:F1}");
                sb.AppendLine($"                 min={stats.MinLatencyMs:F1}  max={stats.MaxLatencyMs:F1}  σ={stats.StdDevLatencyMs:F1}");
                if (stats.MeanInputTokens.HasValue)
                    sb.AppendLine($"  Tokens (avg) : in={stats.MeanInputTokens:F0}  out={stats.MeanOutputTokens:F0}  total={stats.MeanTotalTokens:F0}");
                if (stats.TokensPerSecond.HasValue)
                    sb.AppendLine($"  Throughput   : {stats.TokensPerSecond:F1} tokens/sec");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports all profiled data as JSON.
        /// </summary>
        public string ExportJson(bool indented = true)
        {
            var data = new Dictionary<string, object>();
            foreach (var label in GetLabels())
            {
                data[label] = new
                {
                    stats = GetStats(label),
                    samples = GetSamples(label).Select(s => new
                    {
                        duration_ms = s.Duration.TotalMilliseconds,
                        input_tokens = s.InputTokens,
                        output_tokens = s.OutputTokens,
                        success = s.Success,
                        error = s.Error,
                        timestamp = s.Timestamp,
                        tags = s.Tags
                    })
                };
            }

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Clears all recorded samples.
        /// </summary>
        public void Clear() => _samples.Clear();

        /// <summary>
        /// Clears samples for a specific label.
        /// </summary>
        public void Clear(string label) => _samples.TryRemove(label, out _);

        // ── Helpers ──────────────────────────────────────────

        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            if (sorted.Count == 1) return sorted[0];
            double rank = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(rank);
            int upper = (int)Math.Ceiling(rank);
            if (lower == upper) return sorted[lower];
            double frac = rank - lower;
            return sorted[lower] * (1 - frac) + sorted[upper] * frac;
        }

        private static double StdDev(List<double> values)
        {
            if (values.Count < 2) return 0;
            double mean = values.Average();
            double sumSqDiff = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSqDiff / (values.Count - 1));
        }
    }

    /// <summary>
    /// Context object returned by <see cref="PromptPerformanceProfiler.StartSample"/>.
    /// Dispose to finalize the timing and record the sample.
    /// </summary>
    public class ProfileSampleContext : IDisposable
    {
        private readonly PromptPerformanceProfiler _profiler;
        private readonly string _label;
        private readonly Dictionary<string, string>? _tags;
        private readonly Stopwatch _sw;
        private int? _inputTokens;
        private int? _outputTokens;
        private bool _success = true;
        private string? _error;
        private bool _disposed;

        internal ProfileSampleContext(PromptPerformanceProfiler profiler, string label,
            Dictionary<string, string>? tags)
        {
            _profiler = profiler;
            _label = label;
            _tags = tags;
            _sw = Stopwatch.StartNew();
        }

        /// <summary>Sets token counts for this sample.</summary>
        public void SetTokens(int inputTokens, int outputTokens)
        {
            _inputTokens = inputTokens;
            _outputTokens = outputTokens;
        }

        /// <summary>Marks this sample as failed.</summary>
        public void SetError(string error)
        {
            _success = false;
            _error = error;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sw.Stop();
            var sample = new ProfileSample(_label, _sw.Elapsed, _inputTokens, _outputTokens,
                _success, _error, _tags);
            _profiler.AddSample(_label, sample);
        }
    }
}
