namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    // ── Data Types ───────────────────────────────────────────

    /// <summary>
    /// Result of a single shadow execution (primary or shadow model).
    /// </summary>
    public class ShadowExecutionResult
    {
        /// <summary>Label identifying the model ("primary" or the shadow name).</summary>
        public string Label { get; set; } = "";

        /// <summary>The response text returned by this model.</summary>
        public string? Response { get; set; }

        /// <summary>Whether the execution succeeded.</summary>
        public bool Succeeded { get; set; }

        /// <summary>Error message if execution failed.</summary>
        public string? Error { get; set; }

        /// <summary>Wall-clock latency in milliseconds.</summary>
        public long LatencyMs { get; set; }

        /// <summary>Estimated token count of the response (if available).</summary>
        public int? ResponseTokens { get; set; }

        /// <summary>Arbitrary metadata attached by the executor.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Comparison between primary and shadow execution results.
    /// </summary>
    public class ShadowComparison
    {
        /// <summary>Unique run identifier.</summary>
        public string RunId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>UTC timestamp when the comparison was created.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>The prompt text that was executed.</summary>
        public string Prompt { get; set; } = "";

        /// <summary>Result from the primary model.</summary>
        public ShadowExecutionResult Primary { get; set; } = new();

        /// <summary>Results from shadow models, keyed by label.</summary>
        public Dictionary<string, ShadowExecutionResult> Shadows { get; set; } = new();

        /// <summary>Latency ratio (shadow / primary). Values > 1 mean shadow was slower.</summary>
        public Dictionary<string, double> LatencyRatios { get; set; } = new();

        /// <summary>Whether primary and shadow responses matched (via custom or default comparison).</summary>
        public Dictionary<string, bool?> Matches { get; set; } = new();

        /// <summary>Tags for filtering and grouping comparisons.</summary>
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Summary statistics for a batch of shadow comparisons.
    /// </summary>
    public class ShadowRunSummary
    {
        /// <summary>Total number of comparisons.</summary>
        public int TotalRuns { get; set; }

        /// <summary>Number of runs where primary succeeded.</summary>
        public int PrimarySuccesses { get; set; }

        /// <summary>Per-shadow statistics.</summary>
        public Dictionary<string, ShadowModelStats> ShadowStats { get; set; } = new();

        /// <summary>Average primary latency in milliseconds.</summary>
        public double AvgPrimaryLatencyMs { get; set; }

        /// <summary>When the summary was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics for a single shadow model across a batch of runs.
    /// </summary>
    public class ShadowModelStats
    {
        /// <summary>Shadow model label.</summary>
        public string Label { get; set; } = "";

        /// <summary>Number of successful executions.</summary>
        public int Successes { get; set; }

        /// <summary>Number of failures.</summary>
        public int Failures { get; set; }

        /// <summary>Average latency in milliseconds.</summary>
        public double AvgLatencyMs { get; set; }

        /// <summary>Average latency ratio compared to primary.</summary>
        public double AvgLatencyRatio { get; set; }

        /// <summary>Number of runs where output matched primary.</summary>
        public int MatchCount { get; set; }

        /// <summary>Match rate as a percentage (0–100).</summary>
        public double MatchRate { get; set; }
    }

    /// <summary>
    /// Configuration for a shadow model.
    /// </summary>
    public class ShadowModelConfig
    {
        /// <summary>Label for this shadow model.</summary>
        public string Label { get; set; } = "";

        /// <summary>The executor function: takes prompt text, returns response text.</summary>
        [JsonIgnore]
        public Func<string, CancellationToken, Task<string>>? Executor { get; set; }

        /// <summary>Maximum time to wait for shadow execution before cancelling.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Sampling rate (0.0–1.0). 1.0 means every request gets shadowed.</summary>
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>Whether to continue even if the shadow throws.</summary>
        public bool SwallowErrors { get; set; } = true;
    }

    /// <summary>
    /// Runs prompts against a primary model and one or more shadow models in parallel.
    /// The primary response is always returned to the caller — shadow results are captured
    /// asynchronously for comparison without affecting production latency.
    /// </summary>
    /// <remarks>
    /// <para><b>Usage:</b></para>
    /// <code>
    /// var runner = new PromptShadowRunner(
    ///     primaryExecutor: (prompt, ct) => callGpt4(prompt, ct),
    ///     shadows: new[]
    ///     {
    ///         new ShadowModelConfig
    ///         {
    ///             Label = "gpt-4o-mini",
    ///             Executor = (prompt, ct) => callGpt4oMini(prompt, ct),
    ///             SamplingRate = 0.5
    ///         }
    ///     });
    ///
    /// // Returns primary response immediately; shadow runs in background
    /// string response = await runner.ExecuteAsync("Summarize this article...");
    ///
    /// // Later, inspect comparisons
    /// var summary = runner.GetSummary();
    /// </code>
    /// </remarks>
    public class PromptShadowRunner
    {
        private readonly Func<string, CancellationToken, Task<string>> _primaryExecutor;
        private readonly List<ShadowModelConfig> _shadows = new();
        private readonly List<ShadowComparison> _comparisons = new();
        private readonly object _lock = new();
        private readonly Random _rng = new();
        private Func<string, string, bool>? _matchFunction;
        private Action<ShadowComparison>? _onComparison;
        private int _maxStoredComparisons = 10_000;

        /// <summary>
        /// Creates a new shadow runner.
        /// </summary>
        /// <param name="primaryExecutor">Function to call the primary model.</param>
        /// <param name="shadows">Shadow model configurations.</param>
        public PromptShadowRunner(
            Func<string, CancellationToken, Task<string>> primaryExecutor,
            IEnumerable<ShadowModelConfig>? shadows = null)
        {
            _primaryExecutor = primaryExecutor ?? throw new ArgumentNullException(nameof(primaryExecutor));
            if (shadows != null)
                _shadows.AddRange(shadows);
        }

        /// <summary>
        /// Adds a shadow model configuration.
        /// </summary>
        public PromptShadowRunner AddShadow(ShadowModelConfig config)
        {
            _shadows.Add(config ?? throw new ArgumentNullException(nameof(config)));
            return this;
        }

        /// <summary>
        /// Sets a custom function to determine if two responses "match".
        /// Default: case-insensitive trimmed equality.
        /// </summary>
        public PromptShadowRunner WithMatchFunction(Func<string, string, bool> matchFn)
        {
            _matchFunction = matchFn;
            return this;
        }

        /// <summary>
        /// Registers a callback invoked after each comparison is recorded.
        /// Use for logging, metrics emission, or alerting.
        /// </summary>
        public PromptShadowRunner OnComparison(Action<ShadowComparison> callback)
        {
            _onComparison = callback;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of comparisons to keep in memory (default 10,000).
        /// Oldest comparisons are evicted when the limit is exceeded.
        /// </summary>
        public PromptShadowRunner WithMaxStoredComparisons(int max)
        {
            _maxStoredComparisons = Math.Max(1, max);
            return this;
        }

        /// <summary>
        /// Executes the prompt against the primary model and shadows in parallel.
        /// Returns the primary response. Shadow results are captured asynchronously.
        /// </summary>
        /// <param name="prompt">The prompt text to execute.</param>
        /// <param name="tags">Optional tags for filtering comparisons.</param>
        /// <param name="cancellationToken">Cancellation token for the primary execution.</param>
        /// <returns>The primary model's response.</returns>
        public async Task<string> ExecuteAsync(
            string prompt,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            var comparison = new ShadowComparison
            {
                Prompt = prompt,
                Tags = tags?.ToList() ?? new List<string>()
            };

            // Run primary
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _primaryExecutor(prompt, cancellationToken);
                sw.Stop();
                comparison.Primary = new ShadowExecutionResult
                {
                    Label = "primary",
                    Response = response,
                    Succeeded = true,
                    LatencyMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                comparison.Primary = new ShadowExecutionResult
                {
                    Label = "primary",
                    Succeeded = false,
                    Error = ex.Message,
                    LatencyMs = sw.ElapsedMilliseconds
                };
                throw; // Always propagate primary failures
            }

            // Fire-and-forget shadow executions
            var primaryResponse = comparison.Primary.Response ?? "";
            _ = Task.Run(async () =>
            {
                var shadowTasks = _shadows
                    .Where(s => _rng.NextDouble() < s.SamplingRate)
                    .Select(s => RunShadowAsync(s, prompt, primaryResponse, comparison));
                await Task.WhenAll(shadowTasks);

                // Store comparison
                lock (_lock)
                {
                    _comparisons.Add(comparison);
                    while (_comparisons.Count > _maxStoredComparisons)
                        _comparisons.RemoveAt(0);
                }

                try { _onComparison?.Invoke(comparison); } catch { /* swallow callback errors */ }
            });

            return primaryResponse;
        }

        private async Task RunShadowAsync(
            ShadowModelConfig config,
            string prompt,
            string primaryResponse,
            ShadowComparison comparison)
        {
            var result = new ShadowExecutionResult { Label = config.Label };
            var sw = Stopwatch.StartNew();

            try
            {
                using var cts = new CancellationTokenSource(config.Timeout);
                var response = await config.Executor!(prompt, cts.Token);
                sw.Stop();
                result.Response = response;
                result.Succeeded = true;
                result.LatencyMs = sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Succeeded = false;
                result.Error = ex.Message;
                result.LatencyMs = sw.ElapsedMilliseconds;
                if (!config.SwallowErrors) throw;
            }

            lock (_lock)
            {
                comparison.Shadows[config.Label] = result;

                // Compute latency ratio
                if (comparison.Primary.LatencyMs > 0)
                    comparison.LatencyRatios[config.Label] =
                        (double)result.LatencyMs / comparison.Primary.LatencyMs;

                // Compute match
                if (result.Succeeded && comparison.Primary.Succeeded)
                {
                    var match = _matchFunction != null
                        ? _matchFunction(primaryResponse, result.Response ?? "")
                        : string.Equals(
                            primaryResponse.Trim(),
                            (result.Response ?? "").Trim(),
                            StringComparison.OrdinalIgnoreCase);
                    comparison.Matches[config.Label] = match;
                }
                else
                {
                    comparison.Matches[config.Label] = null;
                }
            }
        }

        /// <summary>
        /// Returns all stored comparisons, optionally filtered by tag.
        /// </summary>
        public List<ShadowComparison> GetComparisons(string? tag = null)
        {
            lock (_lock)
            {
                var query = _comparisons.AsEnumerable();
                if (tag != null)
                    query = query.Where(c => c.Tags.Contains(tag));
                return query.ToList();
            }
        }

        /// <summary>
        /// Returns a summary of all stored shadow comparisons.
        /// </summary>
        public ShadowRunSummary GetSummary()
        {
            lock (_lock)
            {
                var summary = new ShadowRunSummary
                {
                    TotalRuns = _comparisons.Count,
                    PrimarySuccesses = _comparisons.Count(c => c.Primary.Succeeded),
                    AvgPrimaryLatencyMs = _comparisons.Count > 0
                        ? _comparisons.Average(c => c.Primary.LatencyMs)
                        : 0
                };

                // Gather per-shadow stats
                var shadowLabels = _comparisons
                    .SelectMany(c => c.Shadows.Keys)
                    .Distinct();

                foreach (var label in shadowLabels)
                {
                    var shadowResults = _comparisons
                        .Where(c => c.Shadows.ContainsKey(label))
                        .Select(c => c.Shadows[label])
                        .ToList();

                    var matchResults = _comparisons
                        .Where(c => c.Matches.ContainsKey(label) && c.Matches[label].HasValue)
                        .Select(c => c.Matches[label]!.Value)
                        .ToList();

                    var ratios = _comparisons
                        .Where(c => c.LatencyRatios.ContainsKey(label))
                        .Select(c => c.LatencyRatios[label])
                        .ToList();

                    summary.ShadowStats[label] = new ShadowModelStats
                    {
                        Label = label,
                        Successes = shadowResults.Count(r => r.Succeeded),
                        Failures = shadowResults.Count(r => !r.Succeeded),
                        AvgLatencyMs = shadowResults.Count > 0
                            ? shadowResults.Average(r => r.LatencyMs)
                            : 0,
                        AvgLatencyRatio = ratios.Count > 0
                            ? ratios.Average()
                            : 0,
                        MatchCount = matchResults.Count(m => m),
                        MatchRate = matchResults.Count > 0
                            ? matchResults.Count(m => m) * 100.0 / matchResults.Count
                            : 0
                    };
                }

                return summary;
            }
        }

        /// <summary>
        /// Clears all stored comparisons.
        /// </summary>
        public void ClearComparisons()
        {
            lock (_lock) { _comparisons.Clear(); }
        }

        /// <summary>
        /// Exports all comparisons as a JSON string.
        /// </summary>
        public string ExportJson(bool indented = true)
        {
            lock (_lock)
            {
                return JsonSerializer.Serialize(_comparisons, new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
        }
    }
}
