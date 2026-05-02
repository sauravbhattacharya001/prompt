namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Severity level for a detected prompt regression.
    /// </summary>
    public enum RegressionSeverity
    {
        /// <summary>No regression detected.</summary>
        None,
        /// <summary>Minor change, likely cosmetic.</summary>
        Low,
        /// <summary>Moderate change that may affect behavior.</summary>
        Medium,
        /// <summary>Major change that likely breaks expected behavior.</summary>
        High,
        /// <summary>Critical change - output is completely different.</summary>
        Critical
    }

    /// <summary>
    /// A recorded baseline output for a prompt with specific inputs.
    /// </summary>
    public class PromptBaseline
    {
        /// <summary>Gets the prompt identifier.</summary>
        [JsonPropertyName("promptId")]
        public string PromptId { get; }

        /// <summary>Gets the version tag for this baseline.</summary>
        [JsonPropertyName("version")]
        public string Version { get; }

        /// <summary>Gets the input variables used to produce this output.</summary>
        [JsonPropertyName("inputs")]
        public Dictionary<string, string> Inputs { get; }

        /// <summary>Gets the recorded output.</summary>
        [JsonPropertyName("output")]
        public string Output { get; }

        /// <summary>Gets the timestamp when this baseline was recorded.</summary>
        [JsonPropertyName("recordedAt")]
        public DateTime RecordedAt { get; }

        /// <summary>Gets optional tags for categorizing baselines.</summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; }

        /// <summary>
        /// Creates a new baseline record.
        /// </summary>
        public PromptBaseline(string promptId, string version, Dictionary<string, string> inputs,
            string output, List<string>? tags = null)
        {
            PromptId = promptId ?? throw new ArgumentNullException(nameof(promptId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Inputs = inputs ?? new Dictionary<string, string>();
            Output = output ?? throw new ArgumentNullException(nameof(output));
            RecordedAt = DateTime.UtcNow;
            Tags = tags ?? new List<string>();
        }

        [JsonConstructor]
        public PromptBaseline(string promptId, string version, Dictionary<string, string> inputs,
            string output, DateTime recordedAt, List<string> tags)
        {
            PromptId = promptId;
            Version = version;
            Inputs = inputs ?? new Dictionary<string, string>();
            Output = output;
            RecordedAt = recordedAt;
            Tags = tags ?? new List<string>();
        }
    }

    /// <summary>
    /// A single regression finding between baseline and current output.
    /// </summary>
    public class RegressionFinding
    {
        /// <summary>Gets the prompt identifier.</summary>
        [JsonPropertyName("promptId")]
        public string PromptId { get; }

        /// <summary>Gets the baseline version.</summary>
        [JsonPropertyName("baselineVersion")]
        public string BaselineVersion { get; }

        /// <summary>Gets the current version being tested.</summary>
        [JsonPropertyName("currentVersion")]
        public string CurrentVersion { get; }

        /// <summary>Gets the severity of the regression.</summary>
        [JsonPropertyName("severity")]
        public RegressionSeverity Severity { get; }

        /// <summary>Gets the similarity score (0.0 to 1.0).</summary>
        [JsonPropertyName("similarity")]
        public double Similarity { get; }

        /// <summary>Gets specific differences found.</summary>
        [JsonPropertyName("differences")]
        public List<string> Differences { get; }

        /// <summary>Gets the baseline output.</summary>
        [JsonPropertyName("baselineOutput")]
        public string BaselineOutput { get; }

        /// <summary>Gets the current output.</summary>
        [JsonPropertyName("currentOutput")]
        public string CurrentOutput { get; }

        /// <summary>Gets the inputs used for comparison.</summary>
        [JsonPropertyName("inputs")]
        public Dictionary<string, string> Inputs { get; }

        public RegressionFinding(string promptId, string baselineVersion, string currentVersion,
            RegressionSeverity severity, double similarity, List<string> differences,
            string baselineOutput, string currentOutput, Dictionary<string, string> inputs)
        {
            PromptId = promptId;
            BaselineVersion = baselineVersion;
            CurrentVersion = currentVersion;
            Severity = severity;
            Similarity = similarity;
            Differences = differences;
            BaselineOutput = baselineOutput;
            CurrentOutput = currentOutput;
            Inputs = inputs;
        }
    }

    /// <summary>
    /// Summary report of a regression detection run.
    /// </summary>
    public class RegressionReport
    {
        /// <summary>Gets when the report was generated.</summary>
        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; }

        /// <summary>Gets total baselines checked.</summary>
        [JsonPropertyName("totalChecked")]
        public int TotalChecked { get; }

        /// <summary>Gets count of regressions found.</summary>
        [JsonPropertyName("regressionsFound")]
        public int RegressionsFound { get; }

        /// <summary>Gets the highest severity found.</summary>
        [JsonPropertyName("maxSeverity")]
        public RegressionSeverity MaxSeverity { get; }

        /// <summary>Gets all findings.</summary>
        [JsonPropertyName("findings")]
        public List<RegressionFinding> Findings { get; }

        /// <summary>Gets the pass/fail verdict.</summary>
        [JsonPropertyName("passed")]
        public bool Passed { get; }

        /// <summary>Gets the threshold used for pass/fail.</summary>
        [JsonPropertyName("threshold")]
        public RegressionSeverity Threshold { get; }

        public RegressionReport(List<RegressionFinding> findings, RegressionSeverity threshold)
        {
            GeneratedAt = DateTime.UtcNow;
            Findings = findings;
            TotalChecked = findings.Count;
            RegressionsFound = findings.Count(f => f.Severity > RegressionSeverity.None);
            MaxSeverity = findings.Any() ? findings.Max(f => f.Severity) : RegressionSeverity.None;
            Threshold = threshold;
            Passed = MaxSeverity < threshold;
        }

        /// <summary>
        /// Generates a human-readable text report.
        /// </summary>
        public string ToText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("       PROMPT REGRESSION REPORT");
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine($"  Generated: {GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"  Checked:   {TotalChecked} baseline(s)");
            sb.AppendLine($"  Regressed: {RegressionsFound}");
            sb.AppendLine($"  Max Severity: {MaxSeverity}");
            sb.AppendLine($"  Threshold: {Threshold}");
            sb.AppendLine($"  Verdict:   {(Passed ? "✅ PASS" : "❌ FAIL")}");
            sb.AppendLine("───────────────────────────────────────────");

            foreach (var f in Findings.Where(f => f.Severity > RegressionSeverity.None))
            {
                sb.AppendLine();
                sb.AppendLine($"  [{f.Severity}] {f.PromptId}");
                sb.AppendLine($"    Versions: {f.BaselineVersion} → {f.CurrentVersion}");
                sb.AppendLine($"    Similarity: {f.Similarity:P1}");
                foreach (var d in f.Differences.Take(5))
                    sb.AppendLine($"    • {d}");
            }

            if (RegressionsFound == 0)
            {
                sb.AppendLine();
                sb.AppendLine("  No regressions detected. All outputs match baselines.");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the report to JSON.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }
    }

    /// <summary>
    /// Detects regressions in prompt outputs by comparing current results against
    /// recorded baselines. Uses multiple similarity metrics (token overlap, character
    /// edit distance, structural comparison) to identify when prompt changes cause
    /// unexpected behavioral shifts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Workflow: record baselines for known-good prompt versions, then run regression
    /// checks whenever prompts are modified. The detector scores similarity and flags
    /// findings above configurable severity thresholds.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var detector = new PromptRegressionDetector();
    ///
    /// // Record a baseline
    /// var template = new PromptTemplate("Summarize: {{text}}");
    /// var inputs = new Dictionary&lt;string, string&gt; { ["text"] = "AI is transforming..." };
    /// detector.RecordBaseline("summarizer", "v1", template, inputs);
    ///
    /// // Later, check a new version against baselines
    /// var newTemplate = new PromptTemplate("Please summarize the following: {{text}}");
    /// var report = detector.Check("summarizer", "v2", newTemplate);
    /// Console.WriteLine(report.ToText());
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptRegressionDetector
    {
        private readonly Dictionary<string, List<PromptBaseline>> _baselines = new();
        private readonly RegressionSeverity _defaultThreshold;

        /// <summary>
        /// Gets the total number of recorded baselines.
        /// </summary>
        public int BaselineCount => _baselines.Values.Sum(l => l.Count);

        /// <summary>
        /// Creates a new regression detector.
        /// </summary>
        /// <param name="defaultThreshold">Minimum severity to fail a check (default: Medium).</param>
        public PromptRegressionDetector(RegressionSeverity defaultThreshold = RegressionSeverity.Medium)
        {
            _defaultThreshold = defaultThreshold;
        }

        /// <summary>
        /// Records a baseline output for a prompt.
        /// </summary>
        public PromptBaseline RecordBaseline(string promptId, string version,
            PromptTemplate template, Dictionary<string, string> inputs, List<string>? tags = null)
        {
            if (promptId == null) throw new ArgumentNullException(nameof(promptId));
            if (template == null) throw new ArgumentNullException(nameof(template));

            var output = template.Render(inputs);
            var baseline = new PromptBaseline(promptId, version, inputs, output, tags);

            if (!_baselines.ContainsKey(promptId))
                _baselines[promptId] = new List<PromptBaseline>();

            _baselines[promptId].Add(baseline);
            return baseline;
        }

        /// <summary>
        /// Records a baseline from a raw output string (useful when output comes from an LLM).
        /// </summary>
        public PromptBaseline RecordBaselineRaw(string promptId, string version,
            Dictionary<string, string> inputs, string output, List<string>? tags = null)
        {
            if (promptId == null) throw new ArgumentNullException(nameof(promptId));
            if (output == null) throw new ArgumentNullException(nameof(output));

            var baseline = new PromptBaseline(promptId, version, inputs, output, tags);

            if (!_baselines.ContainsKey(promptId))
                _baselines[promptId] = new List<PromptBaseline>();

            _baselines[promptId].Add(baseline);
            return baseline;
        }

        /// <summary>
        /// Checks a new prompt version against all recorded baselines for that prompt.
        /// </summary>
        public RegressionReport Check(string promptId, string currentVersion,
            PromptTemplate currentTemplate, RegressionSeverity? threshold = null)
        {
            if (!_baselines.ContainsKey(promptId))
                return new RegressionReport(new List<RegressionFinding>(), threshold ?? _defaultThreshold);

            var findings = new List<RegressionFinding>();

            foreach (var baseline in _baselines[promptId])
            {
                var currentOutput = currentTemplate.Render(baseline.Inputs);
                var finding = Compare(promptId, baseline, currentVersion, currentOutput);
                findings.Add(finding);
            }

            return new RegressionReport(findings, threshold ?? _defaultThreshold);
        }

        /// <summary>
        /// Checks raw output against baselines (for LLM-generated outputs).
        /// </summary>
        public RegressionReport CheckRaw(string promptId, string currentVersion,
            Dictionary<string, string> inputs, string currentOutput, RegressionSeverity? threshold = null)
        {
            if (!_baselines.ContainsKey(promptId))
                return new RegressionReport(new List<RegressionFinding>(), threshold ?? _defaultThreshold);

            var findings = new List<RegressionFinding>();
            var matchingBaselines = _baselines[promptId]
                .Where(b => InputsMatch(b.Inputs, inputs))
                .ToList();

            if (!matchingBaselines.Any())
                matchingBaselines = _baselines[promptId]; // fall back to all baselines

            foreach (var baseline in matchingBaselines)
            {
                var finding = Compare(promptId, baseline, currentVersion, currentOutput);
                findings.Add(finding);
            }

            return new RegressionReport(findings, threshold ?? _defaultThreshold);
        }

        /// <summary>
        /// Gets all baselines for a specific prompt.
        /// </summary>
        public IReadOnlyList<PromptBaseline> GetBaselines(string promptId)
        {
            return _baselines.TryGetValue(promptId, out var list)
                ? list.AsReadOnly()
                : new List<PromptBaseline>().AsReadOnly();
        }

        /// <summary>
        /// Lists all prompt IDs that have baselines.
        /// </summary>
        public IReadOnlyList<string> ListPromptIds()
        {
            return _baselines.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Removes all baselines for a prompt.
        /// </summary>
        public bool ClearBaselines(string promptId)
        {
            return _baselines.Remove(promptId);
        }

        /// <summary>
        /// Exports all baselines to JSON.
        /// </summary>
        public string ExportBaselines()
        {
            return JsonSerializer.Serialize(_baselines, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        /// <summary>
        /// Imports baselines from JSON.
        /// </summary>
        public int ImportBaselines(string json)
        {
            SerializationGuards.ValidateJsonInput(json, nameof(json));
            var data = JsonSerializer.Deserialize<Dictionary<string, List<PromptBaseline>>>(json,
                SerializationGuards.ReadWithEnums);

            if (data == null) return 0;

            int count = 0;
            foreach (var (key, baselines) in data)
            {
                if (!_baselines.ContainsKey(key))
                    _baselines[key] = new List<PromptBaseline>();

                _baselines[key].AddRange(baselines);
                count += baselines.Count;
            }
            return count;
        }

        private RegressionFinding Compare(string promptId, PromptBaseline baseline,
            string currentVersion, string currentOutput)
        {
            var similarity = ComputeSimilarity(baseline.Output, currentOutput);
            var differences = FindDifferences(baseline.Output, currentOutput);
            var severity = ClassifySeverity(similarity, differences);

            return new RegressionFinding(
                promptId, baseline.Version, currentVersion,
                severity, similarity, differences,
                baseline.Output, currentOutput, baseline.Inputs);
        }

        /// <summary>
        /// Computes similarity between two strings using a combination of
        /// token overlap (Jaccard) and character-level similarity.
        /// </summary>
        internal static double ComputeSimilarity(string a, string b)
        {
            if (a == b) return 1.0;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

            // Token-level Jaccard similarity (60% weight)
            var tokensA = TextAnalysisHelpers.TokenizeToWordSet(a);
            var tokensB = TextAnalysisHelpers.TokenizeToWordSet(b);
            var jaccard = TextAnalysisHelpers.JaccardSimilarity(tokensA, tokensB);

            // Character-level: 1 - (normalized edit distance) (40% weight)
            var maxLen = Math.Max(a.Length, b.Length);
            var editDist = StringHelpers.LevenshteinDistance(a, b);
            var charSim = 1.0 - ((double)editDist / maxLen);

            return (jaccard * 0.6) + (charSim * 0.4);
        }

        internal static List<string> FindDifferences(string baseline, string current)
        {
            var diffs = new List<string>();

            // Length change
            var lenDiff = Math.Abs(baseline.Length - current.Length);
            if (lenDiff > 0)
            {
                var pct = (double)lenDiff / Math.Max(baseline.Length, 1) * 100;
                diffs.Add($"Length changed by {lenDiff} chars ({pct:F0}% {(current.Length > baseline.Length ? "longer" : "shorter")})");
            }

            // Line count change
            var baseLines = baseline.Split('\n').Length;
            var currLines = current.Split('\n').Length;
            if (baseLines != currLines)
                diffs.Add($"Line count: {baseLines} → {currLines}");

            // Token diff
            var baseTokens = TextAnalysisHelpers.TokenizeToWordSet(baseline);
            var currTokens = TextAnalysisHelpers.TokenizeToWordSet(current);
            var added = currTokens.Except(baseTokens).Take(5).ToList();
            var removed = baseTokens.Except(currTokens).Take(5).ToList();
            if (added.Any())
                diffs.Add($"New tokens: {string.Join(", ", added)}");
            if (removed.Any())
                diffs.Add($"Removed tokens: {string.Join(", ", removed)}");

            // Structure check (placeholder presence)
            var basePlaceholders = ExtractPlaceholders(baseline);
            var currPlaceholders = ExtractPlaceholders(current);
            var addedPh = currPlaceholders.Except(basePlaceholders).ToList();
            var removedPh = basePlaceholders.Except(currPlaceholders).ToList();
            if (addedPh.Any())
                diffs.Add($"New placeholders: {string.Join(", ", addedPh)}");
            if (removedPh.Any())
                diffs.Add($"Removed placeholders: {string.Join(", ", removedPh)}");

            return diffs;
        }

        private static HashSet<string> ExtractPlaceholders(string text)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\{\{(\w+)\}\}");
            return new HashSet<string>(matches.Select(m => m.Groups[1].Value));
        }

        private static RegressionSeverity ClassifySeverity(double similarity, List<string> differences)
        {
            if (similarity >= 0.99) return RegressionSeverity.None;
            if (similarity >= 0.90) return RegressionSeverity.Low;
            if (similarity >= 0.70) return RegressionSeverity.Medium;
            if (similarity >= 0.40) return RegressionSeverity.High;
            return RegressionSeverity.Critical;
        }

        private static bool InputsMatch(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var (key, val) in a)
            {
                if (!b.TryGetValue(key, out var bVal) || val != bVal) return false;
            }
            return true;
        }
    }
}
