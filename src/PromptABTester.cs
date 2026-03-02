namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    // ── Data Types ───────────────────────────────────────────

    /// <summary>
    /// Defines a prompt variant in an A/B test experiment.
    /// </summary>
    public class PromptVariant
    {
        /// <summary>Short identifier for this variant (e.g., "A", "B", "concise").</summary>
        public string Name { get; }

        /// <summary>The prompt template associated with this variant.</summary>
        public PromptTemplate Template { get; }

        /// <summary>Optional human-readable description of what this variant tests.</summary>
        public string? Description { get; }

        /// <summary>
        /// Creates a new prompt variant.
        /// </summary>
        /// <param name="name">Unique name for this variant.</param>
        /// <param name="template">The prompt template to test.</param>
        /// <param name="description">Optional description of the hypothesis.</param>
        public PromptVariant(string name, PromptTemplate template, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variant name cannot be null or empty.", nameof(name));
            Name = name;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Description = description;
        }
    }

    /// <summary>
    /// Records the outcome of a single trial run for a variant.
    /// </summary>
    public class TrialResult
    {
        /// <summary>The variant name this trial was run for.</summary>
        public string VariantName { get; set; } = "";

        /// <summary>The rendered prompt that was sent.</summary>
        public string RenderedPrompt { get; set; } = "";

        /// <summary>The model's response text.</summary>
        public string? Response { get; set; }

        /// <summary>Wall-clock time for the model call.</summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>Estimated input token count (chars / 4 heuristic).</summary>
        public int InputTokens { get; set; }

        /// <summary>Estimated output token count (chars / 4 heuristic).</summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Optional quality score assigned by a human or automated evaluator (0.0 - 1.0).
        /// </summary>
        public double? QualityScore { get; set; }

        /// <summary>When this trial was executed.</summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>The variable values used to render the prompt.</summary>
        public Dictionary<string, string>? Variables { get; set; }

        /// <summary>Whether the trial completed without errors.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error message if the trial failed.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Statistical summary for a single variant across all its trials.
    /// </summary>
    public class VariantStats
    {
        /// <summary>The variant name.</summary>
        public string VariantName { get; set; } = "";

        /// <summary>Total number of trials run.</summary>
        public int TrialCount { get; set; }

        /// <summary>Number of successful trials.</summary>
        public int SuccessCount { get; set; }

        /// <summary>Success rate (0.0 - 1.0).</summary>
        public double SuccessRate { get; set; }

        /// <summary>Mean response time in milliseconds.</summary>
        public double MeanResponseTimeMs { get; set; }

        /// <summary>Standard deviation of response time in milliseconds.</summary>
        public double StdDevResponseTimeMs { get; set; }

        /// <summary>Median response time in milliseconds.</summary>
        public double MedianResponseTimeMs { get; set; }

        /// <summary>Mean input token count across trials.</summary>
        public double MeanInputTokens { get; set; }

        /// <summary>Mean output token count across trials.</summary>
        public double MeanOutputTokens { get; set; }

        /// <summary>Mean response character length.</summary>
        public double MeanResponseLength { get; set; }

        /// <summary>Mean quality score (if scores were provided).</summary>
        public double? MeanQualityScore { get; set; }

        /// <summary>Standard deviation of quality scores (if available).</summary>
        public double? StdDevQualityScore { get; set; }

        /// <summary>Number of trials with quality scores.</summary>
        public int QualityScoredCount { get; set; }

        /// <summary>Estimated mean total tokens (input + output) per trial.</summary>
        public double MeanTotalTokens { get; set; }
    }

    /// <summary>
    /// Complete report of an A/B test experiment.
    /// </summary>
    public class ABTestReport
    {
        /// <summary>Name of the experiment.</summary>
        public string ExperimentName { get; set; } = "";

        /// <summary>When the experiment was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>When the report was generated.</summary>
        public DateTimeOffset ReportGeneratedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Total trials across all variants.</summary>
        public int TotalTrials { get; set; }

        /// <summary>Per-variant statistical summaries.</summary>
        public List<VariantStats> VariantStatistics { get; set; } = new();

        /// <summary>
        /// The recommended winner variant name, or null if insufficient data
        /// or no clear winner.
        /// </summary>
        public string? Winner { get; set; }

        /// <summary>How the winner was determined.</summary>
        public string? WinnerReason { get; set; }

        /// <summary>All individual trial results.</summary>
        public List<TrialResult> Trials { get; set; } = new();

        /// <summary>
        /// Serializes the report to a JSON string.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    // ── Main Tester Class ────────────────────────────────────

    /// <summary>
    /// A/B testing framework for comparing prompt template variants. Register
    /// variants, run trials with model calls (or simulated responses), collect
    /// results with timing and quality metrics, and generate statistical reports
    /// to determine which prompt variant performs best.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var tester = new PromptABTester("tone-experiment");
    ///
    /// tester.AddVariant(new PromptVariant("formal",
    ///     new PromptTemplate("Please provide a formal summary of {{topic}}.")));
    /// tester.AddVariant(new PromptVariant("casual",
    ///     new PromptTemplate("Give me a quick summary of {{topic}}.")));
    ///
    /// var variables = new Dictionary&lt;string, string&gt; { ["topic"] = "quantum computing" };
    ///
    /// // Run with simulated responses for offline testing:
    /// tester.RunTrial("formal", variables, "Quantum computing is a paradigm...");
    /// tester.RunTrial("casual", variables, "So basically quantum computers...");
    ///
    /// // Score the results:
    /// tester.ScoreTrial("formal", 0, 0.9);
    /// tester.ScoreTrial("casual", 0, 0.7);
    ///
    /// // Get the report:
    /// ABTestReport report = tester.GetReport();
    /// Console.WriteLine(report.Winner); // "formal"
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptABTester
    {
        /// <summary>Maximum number of variants per experiment.</summary>
        public const int MaxVariants = 26;

        /// <summary>Maximum trials per variant to prevent unbounded growth.</summary>
        public const int MaxTrialsPerVariant = 10_000;

        /// <summary>Maximum JSON payload bytes for deserialization.</summary>
        internal const int MaxJsonPayloadBytes = SerializationGuards.MaxJsonPayloadBytes;

        private readonly object _lock = new();
        private readonly Dictionary<string, PromptVariant> _variants = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<TrialResult>> _trials = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the experiment name.</summary>
        public string ExperimentName { get; }

        /// <summary>When the experiment was created.</summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>Number of registered variants.</summary>
        public int VariantCount
        {
            get { lock (_lock) return _variants.Count; }
        }

        /// <summary>Total number of trials across all variants.</summary>
        public int TotalTrials
        {
            get { lock (_lock) return _trials.Values.Sum(t => t.Count); }
        }

        /// <summary>
        /// Creates a new A/B test experiment.
        /// </summary>
        /// <param name="experimentName">Descriptive name for this experiment.</param>
        public PromptABTester(string experimentName)
        {
            if (string.IsNullOrWhiteSpace(experimentName))
                throw new ArgumentException("Experiment name cannot be null or empty.",
                    nameof(experimentName));
            ExperimentName = experimentName;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        // ── Variant Management ───────────────────────────────

        /// <summary>
        /// Registers a prompt variant for testing.
        /// </summary>
        /// <param name="variant">The variant to add.</param>
        /// <returns>This tester for fluent chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a variant with the same name already exists or
        /// the maximum variant limit is reached.
        /// </exception>
        public PromptABTester AddVariant(PromptVariant variant)
        {
            ArgumentNullException.ThrowIfNull(variant);
            lock (_lock)
            {
                if (_variants.Count >= MaxVariants)
                    throw new InvalidOperationException(
                        $"Maximum of {MaxVariants} variants per experiment.");
                if (_variants.ContainsKey(variant.Name))
                    throw new InvalidOperationException(
                        $"Variant '{variant.Name}' already exists.");
                _variants[variant.Name] = variant;
                _trials[variant.Name] = new List<TrialResult>();
            }
            return this;
        }

        /// <summary>
        /// Removes a variant and all its trial data.
        /// </summary>
        /// <param name="variantName">Name of the variant to remove.</param>
        /// <returns>True if the variant was found and removed.</returns>
        public bool RemoveVariant(string variantName)
        {
            lock (_lock)
            {
                bool removed = _variants.Remove(variantName);
                _trials.Remove(variantName);
                return removed;
            }
        }

        /// <summary>Gets all registered variant names.</summary>
        public IReadOnlyList<string> GetVariantNames()
        {
            lock (_lock) return _variants.Keys.ToList().AsReadOnly();
        }

        /// <summary>Gets a variant by name, or null if not found.</summary>
        public PromptVariant? GetVariant(string name)
        {
            lock (_lock) return _variants.TryGetValue(name, out var v) ? v : null;
        }

        // ── Trial Execution ──────────────────────────────────

        /// <summary>
        /// Runs a trial for the specified variant with a simulated response.
        /// Useful for offline evaluation when you already have model outputs.
        /// </summary>
        /// <param name="variantName">Which variant to test.</param>
        /// <param name="variables">Variables to render the template with.</param>
        /// <param name="simulatedResponse">The pre-collected model response.</param>
        /// <param name="simulatedElapsed">Optional simulated response time.</param>
        /// <returns>The trial result.</returns>
        public TrialResult RunTrial(
            string variantName,
            Dictionary<string, string>? variables,
            string simulatedResponse,
            TimeSpan? simulatedElapsed = null)
        {
            lock (_lock)
            {
                ValidateVariantExists(variantName);
                ValidateTrialLimit(variantName);

                var variant = _variants[variantName];
                string rendered = variant.Template.Render(variables ?? new());

                var result = new TrialResult
                {
                    VariantName = variantName,
                    RenderedPrompt = rendered,
                    Response = simulatedResponse,
                    Elapsed = simulatedElapsed ?? TimeSpan.Zero,
                    InputTokens = EstimateTokens(rendered),
                    OutputTokens = EstimateTokens(simulatedResponse),
                    Variables = variables != null ? new Dictionary<string, string>(variables) : null,
                    Success = true,
                    Timestamp = DateTimeOffset.UtcNow
                };

                _trials[variantName].Add(result);
                return result;
            }
        }

        /// <summary>
        /// Runs a trial for the specified variant using a real async model call.
        /// The provided function receives the rendered prompt and returns the response.
        /// Elapsed time is measured automatically.
        /// </summary>
        /// <param name="variantName">Which variant to test.</param>
        /// <param name="variables">Variables to render the template with.</param>
        /// <param name="modelCall">
        /// Async function that takes the rendered prompt and returns the model's response.
        /// </param>
        /// <returns>The trial result.</returns>
        public async Task<TrialResult> RunTrialAsync(
            string variantName,
            Dictionary<string, string>? variables,
            Func<string, Task<string>> modelCall)
        {
            ArgumentNullException.ThrowIfNull(modelCall);

            string rendered;
            lock (_lock)
            {
                ValidateVariantExists(variantName);
                ValidateTrialLimit(variantName);
                rendered = _variants[variantName].Template.Render(variables ?? new());
            }

            var result = new TrialResult
            {
                VariantName = variantName,
                RenderedPrompt = rendered,
                Variables = variables != null ? new Dictionary<string, string>(variables) : null,
                Timestamp = DateTimeOffset.UtcNow
            };

            var sw = Stopwatch.StartNew();
            try
            {
                result.Response = await modelCall(rendered).ConfigureAwait(false);
                sw.Stop();
                result.Elapsed = sw.Elapsed;
                result.InputTokens = EstimateTokens(rendered);
                result.OutputTokens = EstimateTokens(result.Response ?? "");
                result.Success = true;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Elapsed = sw.Elapsed;
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            lock (_lock)
            {
                _trials[variantName].Add(result);
            }
            return result;
        }

        /// <summary>
        /// Runs trials for all registered variants with the same variables and
        /// simulated responses provided by a response generator function.
        /// </summary>
        /// <param name="variables">Variables to render each variant's template.</param>
        /// <param name="responseGenerator">
        /// Function that takes (variantName, renderedPrompt) and returns a simulated response.
        /// </param>
        /// <returns>List of trial results, one per variant.</returns>
        public List<TrialResult> RunAllVariants(
            Dictionary<string, string>? variables,
            Func<string, string, string> responseGenerator)
        {
            ArgumentNullException.ThrowIfNull(responseGenerator);
            var results = new List<TrialResult>();
            List<string> names;

            lock (_lock)
            {
                names = _variants.Keys.ToList();
            }

            foreach (var name in names)
            {
                string rendered;
                lock (_lock)
                {
                    rendered = _variants[name].Template.Render(variables ?? new());
                }
                string response = responseGenerator(name, rendered);
                results.Add(RunTrial(name, variables, response));
            }
            return results;
        }

        // ── Scoring ──────────────────────────────────────────

        /// <summary>
        /// Assigns a quality score to a specific trial.
        /// </summary>
        /// <param name="variantName">The variant name.</param>
        /// <param name="trialIndex">Zero-based index into the variant's trial list.</param>
        /// <param name="score">Quality score between 0.0 and 1.0.</param>
        public void ScoreTrial(string variantName, int trialIndex, double score)
        {
            if (score < 0.0 || score > 1.0)
                throw new ArgumentOutOfRangeException(nameof(score),
                    "Quality score must be between 0.0 and 1.0.");

            lock (_lock)
            {
                ValidateVariantExists(variantName);
                var trials = _trials[variantName];
                if (trialIndex < 0 || trialIndex >= trials.Count)
                    throw new ArgumentOutOfRangeException(nameof(trialIndex),
                        $"Trial index must be between 0 and {trials.Count - 1}.");
                trials[trialIndex].QualityScore = score;
            }
        }

        /// <summary>
        /// Assigns a quality score to the most recent trial of a variant.
        /// </summary>
        /// <param name="variantName">The variant name.</param>
        /// <param name="score">Quality score between 0.0 and 1.0.</param>
        public void ScoreLastTrial(string variantName, double score)
        {
            lock (_lock)
            {
                ValidateVariantExists(variantName);
                var trials = _trials[variantName];
                if (trials.Count == 0)
                    throw new InvalidOperationException(
                        $"Variant '{variantName}' has no trials to score.");
                ScoreTrial(variantName, trials.Count - 1, score);
            }
        }

        // ── Trial Access ─────────────────────────────────────

        /// <summary>Gets all trials for a specific variant.</summary>
        public IReadOnlyList<TrialResult> GetTrials(string variantName)
        {
            lock (_lock)
            {
                ValidateVariantExists(variantName);
                return _trials[variantName].ToList().AsReadOnly();
            }
        }

        /// <summary>Gets all trials across all variants.</summary>
        public IReadOnlyList<TrialResult> GetAllTrials()
        {
            lock (_lock)
            {
                return _trials.Values.SelectMany(t => t).ToList().AsReadOnly();
            }
        }

        /// <summary>Clears all trial data for a specific variant.</summary>
        public void ClearTrials(string variantName)
        {
            lock (_lock)
            {
                ValidateVariantExists(variantName);
                _trials[variantName].Clear();
            }
        }

        /// <summary>Clears all trial data for all variants.</summary>
        public void ClearAllTrials()
        {
            lock (_lock)
            {
                foreach (var key in _trials.Keys.ToList())
                    _trials[key].Clear();
            }
        }

        // ── Statistics & Reporting ───────────────────────────

        /// <summary>
        /// Computes statistics for a single variant.
        /// </summary>
        /// <param name="variantName">The variant name.</param>
        /// <returns>Statistical summary, or null if no trials exist.</returns>
        public VariantStats? GetVariantStats(string variantName)
        {
            lock (_lock)
            {
                ValidateVariantExists(variantName);
                var trials = _trials[variantName];
                if (trials.Count == 0) return null;
                return ComputeStats(variantName, trials);
            }
        }

        /// <summary>
        /// Generates a complete A/B test report with statistics for all variants,
        /// winner determination, and all trial data.
        /// </summary>
        /// <returns>The experiment report.</returns>
        public ABTestReport GetReport()
        {
            lock (_lock)
            {
                var report = new ABTestReport
                {
                    ExperimentName = ExperimentName,
                    CreatedAt = CreatedAt,
                    ReportGeneratedAt = DateTimeOffset.UtcNow,
                    TotalTrials = _trials.Values.Sum(t => t.Count),
                    Trials = _trials.Values.SelectMany(t => t).ToList()
                };

                var allStats = new List<VariantStats>();
                foreach (var (name, trials) in _trials)
                {
                    if (trials.Count > 0)
                        allStats.Add(ComputeStats(name, trials));
                }
                report.VariantStatistics = allStats;

                DetermineWinner(report);
                return report;
            }
        }

        /// <summary>
        /// Quick comparison: returns the name of the best variant based on
        /// quality scores. Returns null if no quality scores are available.
        /// </summary>
        public string? GetBestByQuality()
        {
            lock (_lock)
            {
                string? best = null;
                double bestScore = -1;

                foreach (var (name, trials) in _trials)
                {
                    var scored = trials.Where(t => t.QualityScore.HasValue).ToList();
                    if (scored.Count == 0) continue;
                    double mean = scored.Average(t => t.QualityScore!.Value);
                    if (mean > bestScore)
                    {
                        bestScore = mean;
                        best = name;
                    }
                }
                return best;
            }
        }

        /// <summary>
        /// Quick comparison: returns the name of the fastest variant by mean
        /// response time. Returns null if no successful trials exist.
        /// </summary>
        public string? GetFastestVariant()
        {
            lock (_lock)
            {
                string? fastest = null;
                double bestMs = double.MaxValue;

                foreach (var (name, trials) in _trials)
                {
                    var successful = trials.Where(t => t.Success).ToList();
                    if (successful.Count == 0) continue;
                    double mean = successful.Average(t => t.Elapsed.TotalMilliseconds);
                    if (mean < bestMs)
                    {
                        bestMs = mean;
                        fastest = name;
                    }
                }
                return fastest;
            }
        }

        /// <summary>
        /// Quick comparison: returns the name of the cheapest variant by mean
        /// total token count. Returns null if no successful trials exist.
        /// </summary>
        public string? GetCheapestVariant()
        {
            lock (_lock)
            {
                string? cheapest = null;
                double bestTokens = double.MaxValue;

                foreach (var (name, trials) in _trials)
                {
                    var successful = trials.Where(t => t.Success).ToList();
                    if (successful.Count == 0) continue;
                    double mean = successful.Average(t => t.InputTokens + t.OutputTokens);
                    if (mean < bestTokens)
                    {
                        bestTokens = mean;
                        cheapest = name;
                    }
                }
                return cheapest;
            }
        }

        // ── Serialization ────────────────────────────────────

        /// <summary>
        /// Serializes the full experiment state (variants, trials) to JSON.
        /// </summary>
        public string ToJson()
        {
            lock (_lock)
            {
                var data = new ExperimentData
                {
                    ExperimentName = ExperimentName,
                    CreatedAt = CreatedAt,
                    Variants = _variants.Values.Select(v => new VariantData
                    {
                        Name = v.Name,
                        TemplateText = v.Template.Template,
                        Description = v.Description
                    }).ToList(),
                    Trials = _trials.Values.SelectMany(t => t).ToList()
                };

                return JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
        }

        /// <summary>
        /// Deserializes an experiment from JSON. Trial data is restored,
        /// variant templates are rebuilt from saved template text.
        /// </summary>
        /// <param name="json">JSON string from <see cref="ToJson"/>.</param>
        /// <returns>A fully restored PromptABTester.</returns>
        public static PromptABTester FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or empty.", nameof(json));

            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var data = JsonSerializer.Deserialize<ExperimentData>(json)
                ?? throw new JsonException("Failed to deserialize experiment data.");

            var tester = new PromptABTester(data.ExperimentName);

            foreach (var vd in data.Variants)
            {
                var template = new PromptTemplate(vd.TemplateText);
                tester.AddVariant(new PromptVariant(vd.Name, template, vd.Description));
            }

            // Restore trials
            foreach (var trial in data.Trials)
            {
                if (tester._trials.ContainsKey(trial.VariantName))
                    tester._trials[trial.VariantName].Add(trial);
            }

            return tester;
        }

        // ── Private Helpers ──────────────────────────────────

        private void ValidateVariantExists(string variantName)
        {
            if (!_variants.ContainsKey(variantName))
                throw new KeyNotFoundException(
                    $"Variant '{variantName}' is not registered.");
        }

        private void ValidateTrialLimit(string variantName)
        {
            if (_trials[variantName].Count >= MaxTrialsPerVariant)
                throw new InvalidOperationException(
                    $"Maximum of {MaxTrialsPerVariant} trials per variant reached.");
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Standard heuristic: ~4 chars per token for English text
            return Math.Max(1, (text.Length + 3) / 4);
        }

        private static VariantStats ComputeStats(string name, List<TrialResult> trials)
        {
            var successful = trials.Where(t => t.Success).ToList();

            var stats = new VariantStats
            {
                VariantName = name,
                TrialCount = trials.Count,
                SuccessCount = successful.Count,
                SuccessRate = trials.Count > 0 ? (double)successful.Count / trials.Count : 0
            };

            if (successful.Count > 0)
            {
                var times = successful.Select(t => t.Elapsed.TotalMilliseconds).ToList();
                stats.MeanResponseTimeMs = times.Average();
                stats.StdDevResponseTimeMs = StdDev(times);
                stats.MedianResponseTimeMs = Median(times);
                stats.MeanInputTokens = successful.Average(t => (double)t.InputTokens);
                stats.MeanOutputTokens = successful.Average(t => (double)t.OutputTokens);
                stats.MeanTotalTokens = successful.Average(t => (double)(t.InputTokens + t.OutputTokens));
                stats.MeanResponseLength = successful
                    .Where(t => t.Response != null)
                    .Select(t => (double)t.Response!.Length)
                    .DefaultIfEmpty(0)
                    .Average();
            }

            var scored = trials.Where(t => t.QualityScore.HasValue).ToList();
            stats.QualityScoredCount = scored.Count;
            if (scored.Count > 0)
            {
                var scores = scored.Select(t => t.QualityScore!.Value).ToList();
                stats.MeanQualityScore = scores.Average();
                stats.StdDevQualityScore = StdDev(scores);
            }

            return stats;
        }

        private static void DetermineWinner(ABTestReport report)
        {
            var stats = report.VariantStatistics;
            if (stats.Count < 2)
            {
                report.Winner = stats.Count == 1 ? stats[0].VariantName : null;
                report.WinnerReason = stats.Count == 1
                    ? "Only one variant with data."
                    : "No variant data available.";
                return;
            }

            // Priority 1: quality scores if available for all variants
            if (stats.All(s => s.QualityScoredCount > 0))
            {
                var bestQuality = stats.OrderByDescending(s => s.MeanQualityScore ?? 0).First();
                var secondBest = stats.OrderByDescending(s => s.MeanQualityScore ?? 0).Skip(1).First();
                double diff = (bestQuality.MeanQualityScore ?? 0) - (secondBest.MeanQualityScore ?? 0);

                if (diff >= 0.05) // 5% quality difference threshold
                {
                    report.Winner = bestQuality.VariantName;
                    report.WinnerReason = $"Highest mean quality score ({bestQuality.MeanQualityScore:F3}) " +
                        $"with {diff:F3} advantage over next best ({secondBest.VariantName}).";
                    return;
                }

                // Quality scores too close — fall through to composite
            }

            // Priority 2: composite score (quality weight 0.6, efficiency 0.2, speed 0.2)
            var maxTokens = stats.Max(s => s.MeanTotalTokens);
            var maxTime = stats.Max(s => s.MeanResponseTimeMs);
            maxTokens = maxTokens > 0 ? maxTokens : 1;
            maxTime = maxTime > 0 ? maxTime : 1;

            string? compositeBest = null;
            double bestComposite = -1;
            var reasons = new List<string>();

            foreach (var s in stats)
            {
                double qualityPart = s.MeanQualityScore ?? 0.5; // neutral if no scores
                double efficiencyPart = 1.0 - (s.MeanTotalTokens / maxTokens); // lower tokens = better
                double speedPart = 1.0 - (s.MeanResponseTimeMs / maxTime); // lower time = better

                double composite = qualityPart * 0.6 + efficiencyPart * 0.2 + speedPart * 0.2;

                if (composite > bestComposite)
                {
                    bestComposite = composite;
                    compositeBest = s.VariantName;
                }
            }

            report.Winner = compositeBest;
            report.WinnerReason = stats.Any(s => s.QualityScoredCount > 0)
                ? $"Composite score (quality 60%, efficiency 20%, speed 20%). " +
                  $"Winner: {compositeBest} with score {bestComposite:F3}."
                : $"Composite score (efficiency 50%, speed 50% — no quality data). " +
                  $"Winner: {compositeBest}.";
        }

        private static double StdDev(List<double> values)
        {
            if (values.Count < 2) return 0;
            double mean = values.Average();
            double sumSq = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / (values.Count - 1)); // sample std dev
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }

        // ── Serialization DTOs ───────────────────────────────

        private class ExperimentData
        {
            public string ExperimentName { get; set; } = "";
            public DateTimeOffset CreatedAt { get; set; }
            public List<VariantData> Variants { get; set; } = new();
            public List<TrialResult> Trials { get; set; } = new();
        }

        private class VariantData
        {
            public string Name { get; set; } = "";
            public string TemplateText { get; set; } = "";
            public string? Description { get; set; }
        }
    }
}
