namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Scoring strategy for comparing actual output against expected output.
    /// </summary>
    public enum BenchmarkScoring
    {
        /// <summary>Exact string match (case-insensitive). Score is 0 or 1.</summary>
        ExactMatch,
        /// <summary>Fraction of expected keywords found in actual output.</summary>
        KeywordOverlap,
        /// <summary>Character n-gram cosine similarity (default n=3).</summary>
        NgramSimilarity,
        /// <summary>Longest common subsequence ratio.</summary>
        LcsRatio,
        /// <summary>Average of all available scoring methods.</summary>
        Composite
    }

    /// <summary>
    /// A single benchmark scenario: a set of variable inputs, an expected
    /// reference output, and optional keywords that should appear.
    /// </summary>
    public class BenchmarkScenario
    {
        /// <summary>Descriptive name for this scenario.</summary>
        public string Name { get; set; } = "";

        /// <summary>Variable values to render the prompt template with.</summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        /// <summary>Expected reference output for similarity scoring.</summary>
        public string ExpectedOutput { get; set; } = "";

        /// <summary>Keywords that should appear in the output (for keyword scoring).</summary>
        public List<string> ExpectedKeywords { get; set; } = new();

        /// <summary>Optional tags for filtering scenarios.</summary>
        public HashSet<string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Score result for a single scenario evaluated against a single prompt variant.
    /// </summary>
    public class ScenarioScore
    {
        /// <summary>Scenario name.</summary>
        public string ScenarioName { get; internal set; } = "";

        /// <summary>Prompt variant name.</summary>
        public string VariantName { get; internal set; } = "";

        /// <summary>The rendered prompt text.</summary>
        public string RenderedPrompt { get; internal set; } = "";

        /// <summary>Individual metric scores (metric name → 0.0–1.0).</summary>
        public Dictionary<string, double> Metrics { get; internal set; } = new();

        /// <summary>Final composite score (0.0–1.0).</summary>
        public double FinalScore { get; internal set; }

        /// <summary>Scoring strategy used.</summary>
        public BenchmarkScoring Scoring { get; internal set; }
    }

    /// <summary>
    /// Aggregated result for a prompt variant across all scenarios.
    /// </summary>
    public class VariantResult
    {
        /// <summary>Variant name.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Per-scenario scores.</summary>
        public List<ScenarioScore> Scores { get; internal set; } = new();

        /// <summary>Average score across all scenarios.</summary>
        public double AverageScore => Scores.Count > 0
            ? Scores.Average(s => s.FinalScore)
            : 0.0;

        /// <summary>Minimum score across scenarios.</summary>
        public double MinScore => Scores.Count > 0
            ? Scores.Min(s => s.FinalScore)
            : 0.0;

        /// <summary>Maximum score across scenarios.</summary>
        public double MaxScore => Scores.Count > 0
            ? Scores.Max(s => s.FinalScore)
            : 0.0;

        /// <summary>Standard deviation of scores.</summary>
        public double StdDev
        {
            get
            {
                if (Scores.Count < 2) return 0.0;
                var avg = AverageScore;
                var sumSq = Scores.Sum(s => (s.FinalScore - avg) * (s.FinalScore - avg));
                return Math.Sqrt(sumSq / (Scores.Count - 1));
            }
        }
    }

    /// <summary>
    /// Full benchmark report with all variant results and a winner.
    /// </summary>
    public class BenchmarkReport
    {
        /// <summary>All variant results, sorted by average score descending.</summary>
        public List<VariantResult> Results { get; internal set; } = new();

        /// <summary>Name of the winning variant.</summary>
        public string Winner => Results.Count > 0 ? Results[0].Name : "";

        /// <summary>Scoring strategy used.</summary>
        public BenchmarkScoring Scoring { get; internal set; }

        /// <summary>Total scenarios evaluated.</summary>
        public int ScenarioCount { get; internal set; }

        /// <summary>Total variants evaluated.</summary>
        public int VariantCount => Results.Count;

        /// <summary>When the benchmark was run.</summary>
        public DateTimeOffset Timestamp { get; internal set; }

        /// <summary>
        /// Formats the report as a human-readable text table.
        /// </summary>
        public string ToTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Prompt Benchmark Report — {Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Scoring: {Scoring} | Scenarios: {ScenarioCount} | Variants: {VariantCount}");
            sb.AppendLine(new string('─', 72));
            sb.AppendLine($"{"Rank",-5} {"Variant",-25} {"Avg",7} {"Min",7} {"Max",7} {"StdDev",7}");
            sb.AppendLine(new string('─', 72));

            for (int i = 0; i < Results.Count; i++)
            {
                var r = Results[i];
                var marker = i == 0 ? " ★" : "";
                sb.AppendLine(
                    $"#{i + 1,-4} {(r.Name + marker),-25} {r.AverageScore,7:F3} {r.MinScore,7:F3} {r.MaxScore,7:F3} {r.StdDev,7:F3}");
            }

            sb.AppendLine(new string('─', 72));
            sb.AppendLine($"Winner: {Winner}");
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the report to JSON.
        /// </summary>
        public string ToJson()
        {
            var data = new
            {
                timestamp = Timestamp,
                scoring = Scoring.ToString(),
                scenarioCount = ScenarioCount,
                variantCount = VariantCount,
                winner = Winner,
                results = Results.Select(r => new
                {
                    name = r.Name,
                    averageScore = Math.Round(r.AverageScore, 4),
                    minScore = Math.Round(r.MinScore, 4),
                    maxScore = Math.Round(r.MaxScore, 4),
                    stdDev = Math.Round(r.StdDev, 4),
                    scenarios = r.Scores.Select(s => new
                    {
                        scenario = s.ScenarioName,
                        score = Math.Round(s.FinalScore, 4),
                        metrics = s.Metrics.ToDictionary(
                            kv => kv.Key,
                            kv => Math.Round(kv.Value, 4))
                    })
                })
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Returns a CSV string of the results.
        /// </summary>
        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Rank,Variant,AverageScore,MinScore,MaxScore,StdDev");
            for (int i = 0; i < Results.Count; i++)
            {
                var r = Results[i];
                sb.AppendLine($"{i + 1},{Escape(r.Name)},{r.AverageScore:F4},{r.MinScore:F4},{r.MaxScore:F4},{r.StdDev:F4}");
            }
            return sb.ToString();

            static string Escape(string s) =>
                s.Contains(',') || s.Contains('"')
                    ? $"\"{s.Replace("\"", "\"\"")}\""
                    : s;
        }
    }

    /// <summary>
    /// Benchmarks multiple prompt template variants against a suite of
    /// test scenarios, scoring each variant on how well its rendered output
    /// matches expected results. Useful for systematically evaluating which
    /// prompt phrasing performs best before deploying to production.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var suite = new PromptBenchmarkSuite();
    ///
    /// // Add prompt variants to compare
    /// suite.AddVariant("concise",
    ///     new PromptTemplate("Summarize in one sentence: {{text}}"));
    /// suite.AddVariant("detailed",
    ///     new PromptTemplate("Provide a detailed summary of: {{text}}"));
    /// suite.AddVariant("bullet",
    ///     new PromptTemplate("Summarize as bullet points: {{text}}"));
    ///
    /// // Add test scenarios with expected outputs
    /// suite.AddScenario(new BenchmarkScenario
    /// {
    ///     Name = "short-article",
    ///     Variables = new() { ["text"] = "The quick brown fox..." },
    ///     ExpectedOutput = "A fox jumps over a lazy dog.",
    ///     ExpectedKeywords = new() { "fox", "dog", "jumps" }
    /// });
    ///
    /// // Run benchmark and get report
    /// var report = suite.Run(BenchmarkScoring.Composite);
    /// Console.WriteLine(report.ToTable());
    /// Console.WriteLine($"Best variant: {report.Winner}");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptBenchmarkSuite
    {
        private readonly Dictionary<string, PromptTemplate> _variants = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<BenchmarkScenario> _scenarios = new();

        /// <summary>Number of registered variants.</summary>
        public int VariantCount => _variants.Count;

        /// <summary>Number of registered scenarios.</summary>
        public int ScenarioCount => _scenarios.Count;

        /// <summary>
        /// Registers a named prompt variant for benchmarking.
        /// </summary>
        /// <param name="name">Unique name for this variant.</param>
        /// <param name="template">The prompt template to evaluate.</param>
        /// <exception cref="ArgumentException">Name is empty or already registered.</exception>
        /// <exception cref="ArgumentNullException">Template is null.</exception>
        public void AddVariant(string name, PromptTemplate template)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variant name cannot be empty.", nameof(name));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (_variants.ContainsKey(name))
                throw new ArgumentException($"Variant '{name}' already registered.", nameof(name));

            _variants[name] = template;
        }

        /// <summary>
        /// Removes a variant by name.
        /// </summary>
        public bool RemoveVariant(string name) => _variants.Remove(name);

        /// <summary>
        /// Adds a benchmark scenario.
        /// </summary>
        /// <exception cref="ArgumentNullException">Scenario is null.</exception>
        /// <exception cref="ArgumentException">Scenario name is empty or duplicate.</exception>
        public void AddScenario(BenchmarkScenario scenario)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (string.IsNullOrWhiteSpace(scenario.Name))
                throw new ArgumentException("Scenario name cannot be empty.");
            if (_scenarios.Any(s => s.Name.Equals(scenario.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Scenario '{scenario.Name}' already exists.");

            _scenarios.Add(scenario);
        }

        /// <summary>
        /// Adds multiple scenarios at once.
        /// </summary>
        public void AddScenarios(IEnumerable<BenchmarkScenario> scenarios)
        {
            foreach (var s in scenarios)
                AddScenario(s);
        }

        /// <summary>
        /// Removes a scenario by name.
        /// </summary>
        public bool RemoveScenario(string name)
        {
            var idx = _scenarios.FindIndex(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return false;
            _scenarios.RemoveAt(idx);
            return true;
        }

        /// <summary>
        /// Runs the benchmark, scoring all variants against all scenarios.
        /// </summary>
        /// <param name="scoring">Scoring strategy to use.</param>
        /// <param name="tagFilter">Optional: only run scenarios with this tag.</param>
        /// <returns>A <see cref="BenchmarkReport"/> with ranked results.</returns>
        /// <exception cref="InvalidOperationException">No variants or scenarios registered.</exception>
        public BenchmarkReport Run(
            BenchmarkScoring scoring = BenchmarkScoring.Composite,
            string? tagFilter = null)
        {
            if (_variants.Count == 0)
                throw new InvalidOperationException("No variants registered. Add at least one variant.");
            if (_scenarios.Count == 0)
                throw new InvalidOperationException("No scenarios registered. Add at least one scenario.");

            var scenarios = tagFilter != null
                ? _scenarios.Where(s => s.Tags.Contains(tagFilter)).ToList()
                : _scenarios;

            if (scenarios.Count == 0)
                throw new InvalidOperationException($"No scenarios match tag '{tagFilter}'.");

            var variantResults = new List<VariantResult>();

            foreach (var (variantName, template) in _variants)
            {
                var scores = new List<ScenarioScore>();

                foreach (var scenario in scenarios)
                {
                    string rendered;
                    try
                    {
                        rendered = template.Render(scenario.Variables);
                    }
                    catch
                    {
                        rendered = "";
                    }

                    var metrics = ComputeMetrics(rendered, scenario);
                    double finalScore = ComputeFinalScore(metrics, scoring);

                    scores.Add(new ScenarioScore
                    {
                        ScenarioName = scenario.Name,
                        VariantName = variantName,
                        RenderedPrompt = rendered,
                        Metrics = metrics,
                        FinalScore = finalScore,
                        Scoring = scoring
                    });
                }

                variantResults.Add(new VariantResult
                {
                    Name = variantName,
                    Scores = scores
                });
            }

            variantResults.Sort((a, b) => b.AverageScore.CompareTo(a.AverageScore));

            return new BenchmarkReport
            {
                Results = variantResults,
                Scoring = scoring,
                ScenarioCount = scenarios.Count,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// Runs a quick head-to-head comparison of two variants and returns
        /// which one wins and by how much.
        /// </summary>
        public (string winner, double margin, BenchmarkReport report) HeadToHead(
            string variantA,
            string variantB,
            BenchmarkScoring scoring = BenchmarkScoring.Composite)
        {
            if (!_variants.ContainsKey(variantA))
                throw new ArgumentException($"Variant '{variantA}' not found.");
            if (!_variants.ContainsKey(variantB))
                throw new ArgumentException($"Variant '{variantB}' not found.");

            var report = Run(scoring);
            var resultA = report.Results.First(r =>
                r.Name.Equals(variantA, StringComparison.OrdinalIgnoreCase));
            var resultB = report.Results.First(r =>
                r.Name.Equals(variantB, StringComparison.OrdinalIgnoreCase));

            var margin = Math.Abs(resultA.AverageScore - resultB.AverageScore);
            var winner = resultA.AverageScore >= resultB.AverageScore ? variantA : variantB;

            return (winner, Math.Round(margin, 4), report);
        }

        // --- Scoring internals ---

        private static Dictionary<string, double> ComputeMetrics(
            string rendered, BenchmarkScenario scenario)
        {
            var metrics = new Dictionary<string, double>();

            // Exact match
            metrics["exactMatch"] = rendered.Equals(
                scenario.ExpectedOutput, StringComparison.OrdinalIgnoreCase)
                ? 1.0 : 0.0;

            // Keyword overlap
            if (scenario.ExpectedKeywords.Count > 0)
            {
                var lower = rendered.ToLowerInvariant();
                int hits = scenario.ExpectedKeywords
                    .Count(k => lower.Contains(k.ToLowerInvariant()));
                metrics["keywordOverlap"] = (double)hits / scenario.ExpectedKeywords.Count;
            }
            else
            {
                metrics["keywordOverlap"] = scenario.ExpectedOutput.Length > 0
                    ? ComputeWordOverlap(rendered, scenario.ExpectedOutput)
                    : 0.0;
            }

            // N-gram similarity
            metrics["ngramSimilarity"] = ComputeNgramCosineSimilarity(
                rendered, scenario.ExpectedOutput, 3);

            // LCS ratio
            metrics["lcsRatio"] = ComputeLcsRatio(rendered, scenario.ExpectedOutput);

            return metrics;
        }

        private static double ComputeFinalScore(
            Dictionary<string, double> metrics, BenchmarkScoring scoring)
        {
            return scoring switch
            {
                BenchmarkScoring.ExactMatch => metrics.GetValueOrDefault("exactMatch"),
                BenchmarkScoring.KeywordOverlap => metrics.GetValueOrDefault("keywordOverlap"),
                BenchmarkScoring.NgramSimilarity => metrics.GetValueOrDefault("ngramSimilarity"),
                BenchmarkScoring.LcsRatio => metrics.GetValueOrDefault("lcsRatio"),
                BenchmarkScoring.Composite => metrics.Values.Count > 0
                    ? metrics.Values.Average()
                    : 0.0,
                _ => 0.0
            };
        }

        private static double ComputeWordOverlap(string a, string b)
        {
            var wordsA = Tokenize(a);
            var wordsB = Tokenize(b);
            if (wordsB.Count == 0) return 0.0;

            int hits = wordsB.Count(w => wordsA.Contains(w));
            return (double)hits / wordsB.Count;
        }

        private static HashSet<string> Tokenize(string text)
        {
            return new HashSet<string>(
                Regex.Split(text.ToLowerInvariant(), @"\W+")
                    .Where(w => w.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        private static double ComputeNgramCosineSimilarity(string a, string b, int n)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0.0;

            var ngramsA = GetNgrams(a.ToLowerInvariant(), n);
            var ngramsB = GetNgrams(b.ToLowerInvariant(), n);

            var allKeys = new HashSet<string>(ngramsA.Keys);
            allKeys.UnionWith(ngramsB.Keys);

            double dot = 0, magA = 0, magB = 0;
            foreach (var key in allKeys)
            {
                ngramsA.TryGetValue(key, out int countA);
                ngramsB.TryGetValue(key, out int countB);
                dot += countA * countB;
                magA += countA * countA;
                magB += countB * countB;
            }

            if (magA == 0 || magB == 0) return 0.0;
            return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
        }

        private static Dictionary<string, int> GetNgrams(string text, int n)
        {
            var ngrams = new Dictionary<string, int>();
            for (int i = 0; i <= text.Length - n; i++)
            {
                var gram = text.Substring(i, n);
                ngrams[gram] = ngrams.GetValueOrDefault(gram) + 1;
            }
            return ngrams;
        }

        private static double ComputeLcsRatio(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0.0;

            var la = a.ToLowerInvariant();
            var lb = b.ToLowerInvariant();

            // Space-optimized LCS length
            int m = la.Length, n = lb.Length;

            // Limit to prevent excessive memory/time on very long strings
            if (m > 5000 || n > 5000)
            {
                // Fall back to word-level LCS for very long strings
                return ComputeWordLcsRatio(la, lb);
            }

            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (la[i - 1] == lb[j - 1])
                        curr[j] = prev[j - 1] + 1;
                    else
                        curr[j] = Math.Max(prev[j], curr[j - 1]);
                }
                (prev, curr) = (curr, prev);
                Array.Clear(curr, 0, curr.Length);
            }

            int lcsLen = prev[n];
            return (2.0 * lcsLen) / (m + n);
        }

        private static double ComputeWordLcsRatio(string a, string b)
        {
            var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int m = wordsA.Length, n = wordsB.Length;
            if (m == 0 || n == 0) return 0.0;

            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (wordsA[i - 1] == wordsB[j - 1])
                        curr[j] = prev[j - 1] + 1;
                    else
                        curr[j] = Math.Max(prev[j], curr[j - 1]);
                }
                (prev, curr) = (curr, prev);
                Array.Clear(curr, 0, curr.Length);
            }

            int lcsLen = prev[n];
            return (2.0 * lcsLen) / (m + n);
        }
    }
}
