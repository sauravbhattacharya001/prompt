namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Represents a single cost-saving suggestion for a prompt.
    /// </summary>
    public class CostSuggestion
    {
        /// <summary>Gets the suggestion category.</summary>
        public string Category { get; internal set; } = "";

        /// <summary>Gets a human-readable description of the suggestion.</summary>
        public string Description { get; internal set; } = "";

        /// <summary>Gets the estimated token savings.</summary>
        public int EstimatedTokenSavings { get; internal set; }

        /// <summary>Gets the estimated cost savings in USD (based on configured rate).</summary>
        public double EstimatedCostSavings { get; internal set; }

        /// <summary>Gets the severity: Low, Medium, High.</summary>
        public string Severity { get; internal set; } = "Low";

        /// <summary>Gets the original text fragment (if applicable).</summary>
        public string? OriginalFragment { get; internal set; }

        /// <summary>Gets the suggested replacement (if applicable).</summary>
        public string? SuggestedReplacement { get; internal set; }
    }

    /// <summary>
    /// Pricing tier configuration for cost estimation.
    /// </summary>
    public class OptimizerModelPricing
    {
        /// <summary>Gets or sets the model name.</summary>
        public string ModelName { get; set; } = "";

        /// <summary>Gets or sets the cost per 1K input tokens in USD.</summary>
        public double CostPer1KInputTokens { get; set; }

        /// <summary>Gets or sets the cost per 1K output tokens in USD.</summary>
        public double CostPer1KOutputTokens { get; set; }

        /// <summary>Gets or sets the maximum context window size.</summary>
        public int MaxContextTokens { get; set; }

        /// <summary>Gets or sets the model capability tier (1=basic, 2=mid, 3=advanced).</summary>
        public int Tier { get; set; } = 2;
    }

    /// <summary>
    /// Full cost optimization report for a prompt.
    /// </summary>
    public class CostOptimizationReport
    {
        /// <summary>Gets the original token count.</summary>
        public int OriginalTokens { get; internal set; }

        /// <summary>Gets the estimated optimized token count.</summary>
        public int OptimizedTokens { get; internal set; }

        /// <summary>Gets the total potential token savings.</summary>
        public int TotalTokenSavings => OriginalTokens - OptimizedTokens;

        /// <summary>Gets the savings as a percentage.</summary>
        public double SavingsPercent => OriginalTokens > 0
            ? (double)TotalTokenSavings / OriginalTokens * 100 : 0;

        /// <summary>Gets the current estimated cost in USD.</summary>
        public double CurrentCost { get; internal set; }

        /// <summary>Gets the optimized estimated cost in USD.</summary>
        public double OptimizedCost { get; internal set; }

        /// <summary>Gets cost savings in USD.</summary>
        public double CostSavings => CurrentCost - OptimizedCost;

        /// <summary>Gets the recommended model tier.</summary>
        public string? RecommendedModel { get; internal set; }

        /// <summary>Gets the list of optimization suggestions.</summary>
        public List<CostSuggestion> Suggestions { get; internal set; } = new();

        /// <summary>Gets the optimized prompt text (if auto-optimization was applied).</summary>
        public string? OptimizedPrompt { get; internal set; }

        /// <summary>Returns a human-readable summary.</summary>
        public string Summary()
        {
            var lines = new List<string>
            {
                $"=== Cost Optimization Report ===",
                $"Original tokens:  {OriginalTokens:N0}",
                $"Optimized tokens: {OptimizedTokens:N0}",
                $"Token savings:    {TotalTokenSavings:N0} ({SavingsPercent:F1}%)",
                $"Current cost:     ${CurrentCost:F6}",
                $"Optimized cost:   ${OptimizedCost:F6}",
                $"Cost savings:     ${CostSavings:F6}",
            };

            if (RecommendedModel != null)
                lines.Add($"Recommended model: {RecommendedModel}");

            if (Suggestions.Count > 0)
            {
                lines.Add($"");
                lines.Add($"Suggestions ({Suggestions.Count}):");
                foreach (var s in Suggestions)
                {
                    lines.Add($"  [{s.Severity}] {s.Category}: {s.Description}");
                    if (s.EstimatedTokenSavings > 0)
                        lines.Add($"         Saves ~{s.EstimatedTokenSavings} tokens (${s.EstimatedCostSavings:F6})");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Analyzes prompts and suggests cost-saving optimizations including
    /// redundancy removal, verbose phrase shortening, model tier recommendations,
    /// and structural improvements.
    /// </summary>
    /// <example>
    /// <code>
    /// var optimizer = new PromptCostOptimizer();
    /// var report = optimizer.Analyze("You are a helpful assistant. Please help me...");
    /// Console.WriteLine(report.Summary());
    /// 
    /// // Auto-optimize
    /// var optimized = optimizer.Optimize("I would like you to please help me...");
    /// Console.WriteLine(optimized);
    /// </code>
    /// </example>
    public class PromptCostOptimizer
    {
        private readonly List<OptimizerModelPricing> _models;
        private readonly OptimizerModelPricing _currentModel;

        /// <summary>Well-known verbose phrases and their concise replacements.</summary>
        private static readonly Dictionary<string, string> VerboseReplacements = new(StringComparer.OrdinalIgnoreCase)
        {
            { "I would like you to", "" },
            { "Could you please", "" },
            { "I want you to", "" },
            { "Please make sure to", "" },
            { "It is important that you", "" },
            { "Please note that", "Note:" },
            { "In order to", "To" },
            { "Due to the fact that", "Because" },
            { "At this point in time", "Now" },
            { "In the event that", "If" },
            { "For the purpose of", "To" },
            { "With regard to", "About" },
            { "In light of the fact that", "Since" },
            { "On the other hand", "However" },
            { "As a matter of fact", "In fact" },
            { "In the near future", "Soon" },
            { "A large number of", "Many" },
            { "In spite of the fact that", "Although" },
            { "Is able to", "Can" },
            { "Has the ability to", "Can" },
            { "It is necessary that", "Must" },
            { "Make sure that you", "" },
            { "Be sure to", "" },
            { "Please be advised that", "" },
            { "I need you to", "" },
        };

        /// <summary>
        /// Creates a new PromptCostOptimizer with default model pricing.
        /// </summary>
        /// <param name="currentModelName">Name of the model currently in use. Defaults to "gpt-4".</param>
        public PromptCostOptimizer(string currentModelName = "gpt-4")
        {
            _models = new List<OptimizerModelPricing>
            {
                new() { ModelName = "gpt-4o-mini", CostPer1KInputTokens = 0.00015, CostPer1KOutputTokens = 0.0006, MaxContextTokens = 128000, Tier = 1 },
                new() { ModelName = "gpt-4o", CostPer1KInputTokens = 0.0025, CostPer1KOutputTokens = 0.01, MaxContextTokens = 128000, Tier = 2 },
                new() { ModelName = "gpt-4", CostPer1KInputTokens = 0.03, CostPer1KOutputTokens = 0.06, MaxContextTokens = 8192, Tier = 3 },
                new() { ModelName = "claude-3-haiku", CostPer1KInputTokens = 0.00025, CostPer1KOutputTokens = 0.00125, MaxContextTokens = 200000, Tier = 1 },
                new() { ModelName = "claude-3.5-sonnet", CostPer1KInputTokens = 0.003, CostPer1KOutputTokens = 0.015, MaxContextTokens = 200000, Tier = 2 },
            };

            _currentModel = _models.FirstOrDefault(m => m.ModelName.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
                            ?? _models[2]; // default to gpt-4
        }

        /// <summary>
        /// Creates a new PromptCostOptimizer with custom model pricing.
        /// </summary>
        public PromptCostOptimizer(List<OptimizerModelPricing> models, string currentModelName)
        {
            _models = models ?? throw new ArgumentNullException(nameof(models));
            _currentModel = _models.FirstOrDefault(m => m.ModelName.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
                            ?? _models.First();
        }

        /// <summary>
        /// Analyzes a prompt and returns a cost optimization report.
        /// </summary>
        public CostOptimizationReport Analyze(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new CostOptimizationReport();

            var suggestions = new List<CostSuggestion>();
            int originalTokens = PromptGuard.EstimateTokens(prompt);
            int totalSavings = 0;

            // 1. Detect verbose phrases
            foreach (var (verbose, concise) in VerboseReplacements)
            {
                var matches = Regex.Matches(prompt, Regex.Escape(verbose), RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    int saving = matches.Count * PromptGuard.EstimateTokens(verbose);
                    if (!string.IsNullOrEmpty(concise))
                        saving -= matches.Count * PromptGuard.EstimateTokens(concise);

                    if (saving > 0)
                    {
                        totalSavings += saving;
                        suggestions.Add(new CostSuggestion
                        {
                            Category = "Verbose Phrase",
                            Description = string.IsNullOrEmpty(concise)
                                ? $"Remove filler phrase \"{verbose}\" ({matches.Count}x)"
                                : $"Replace \"{verbose}\" with \"{concise}\" ({matches.Count}x)",
                            EstimatedTokenSavings = saving,
                            EstimatedCostSavings = saving / 1000.0 * _currentModel.CostPer1KInputTokens,
                            Severity = saving > 20 ? "Medium" : "Low",
                            OriginalFragment = verbose,
                            SuggestedReplacement = concise,
                        });
                    }
                }
            }

            // 2. Detect repeated sentences
            var sentences = Regex.Split(prompt, @"(?<=[.!?])\s+")
                .Where(s => s.Length > 10)
                .ToList();
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var sentence in sentences)
            {
                var normalized = Regex.Replace(sentence.Trim(), @"\s+", " ");
                seen[normalized] = seen.GetValueOrDefault(normalized) + 1;
            }
            foreach (var (sentence, count) in seen.Where(kv => kv.Value > 1))
            {
                int saving = (count - 1) * PromptGuard.EstimateTokens(sentence);
                totalSavings += saving;
                suggestions.Add(new CostSuggestion
                {
                    Category = "Duplicate Content",
                    Description = $"Sentence repeated {count}x: \"{StringHelpers.Truncate(sentence, 60)}\"",
                    EstimatedTokenSavings = saving,
                    EstimatedCostSavings = saving / 1000.0 * _currentModel.CostPer1KInputTokens,
                    Severity = "High",
                    OriginalFragment = sentence,
                });
            }

            // 3. Detect excessive whitespace
            var whitespaceMatches = Regex.Matches(prompt, @"[ \t]{3,}");
            if (whitespaceMatches.Count > 0)
            {
                int extraChars = whitespaceMatches.Sum(m => m.Length - 1);
                int saving = Math.Max(1, extraChars / 4);
                totalSavings += saving;
                suggestions.Add(new CostSuggestion
                {
                    Category = "Whitespace",
                    Description = $"Excessive whitespace detected ({whitespaceMatches.Count} occurrences, ~{extraChars} extra chars)",
                    EstimatedTokenSavings = saving,
                    EstimatedCostSavings = saving / 1000.0 * _currentModel.CostPer1KInputTokens,
                    Severity = "Low",
                });
            }

            // 4. Detect excessive newlines
            var newlineMatches = Regex.Matches(prompt, @"\n{3,}");
            if (newlineMatches.Count > 0)
            {
                int saving = newlineMatches.Sum(m => m.Length - 2);
                totalSavings += saving;
                suggestions.Add(new CostSuggestion
                {
                    Category = "Whitespace",
                    Description = $"Excessive blank lines ({newlineMatches.Count} occurrences)",
                    EstimatedTokenSavings = saving,
                    EstimatedCostSavings = saving / 1000.0 * _currentModel.CostPer1KInputTokens,
                    Severity = "Low",
                });
            }

            // 5. Model tier recommendation
            string? recommendedModel = null;
            var complexity = EstimateComplexity(prompt);
            var cheaperModels = _models
                .Where(m => m.CostPer1KInputTokens < _currentModel.CostPer1KInputTokens && m.Tier >= complexity)
                .OrderByDescending(m => m.CostPer1KInputTokens)
                .ToList();

            if (cheaperModels.Count > 0)
            {
                var rec = cheaperModels.First();
                recommendedModel = rec.ModelName;
                double currentCostEst = originalTokens / 1000.0 * _currentModel.CostPer1KInputTokens;
                double recCostEst = originalTokens / 1000.0 * rec.CostPer1KInputTokens;
                suggestions.Add(new CostSuggestion
                {
                    Category = "Model Tier",
                    Description = $"Prompt complexity is {ComplexityLabel(complexity)}; consider using {rec.ModelName} instead of {_currentModel.ModelName}",
                    EstimatedTokenSavings = 0,
                    EstimatedCostSavings = currentCostEst - recCostEst,
                    Severity = "High",
                });
            }

            // 6. Detect long system-style preambles
            var preambleMatch = Regex.Match(prompt, @"^(You are .+?[.!]\s)", RegexOptions.Singleline);
            if (preambleMatch.Success && preambleMatch.Length > 200)
            {
                int saving = PromptGuard.EstimateTokens(preambleMatch.Value) / 3;
                totalSavings += saving;
                suggestions.Add(new CostSuggestion
                {
                    Category = "Preamble",
                    Description = $"Long system preamble ({preambleMatch.Length} chars). Consider shortening the role description.",
                    EstimatedTokenSavings = saving,
                    EstimatedCostSavings = saving / 1000.0 * _currentModel.CostPer1KInputTokens,
                    Severity = "Medium",
                    OriginalFragment = StringHelpers.Truncate(preambleMatch.Value, 100),
                });
            }

            int optimizedTokens = Math.Max(1, originalTokens - totalSavings);
            double currentCost = originalTokens / 1000.0 * _currentModel.CostPer1KInputTokens;
            double optimizedCost = optimizedTokens / 1000.0 * _currentModel.CostPer1KInputTokens;

            return new CostOptimizationReport
            {
                OriginalTokens = originalTokens,
                OptimizedTokens = optimizedTokens,
                CurrentCost = currentCost,
                OptimizedCost = optimizedCost,
                RecommendedModel = recommendedModel,
                Suggestions = suggestions.OrderByDescending(s => s.EstimatedCostSavings).ToList(),
            };
        }

        /// <summary>
        /// Applies automatic optimizations to a prompt and returns the optimized text.
        /// </summary>
        public string Optimize(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return prompt;

            var result = prompt;

            // Remove filler phrases
            foreach (var (verbose, concise) in VerboseReplacements)
            {
                result = Regex.Replace(result, Regex.Escape(verbose), concise, RegexOptions.IgnoreCase);
            }

            // Collapse excessive whitespace
            result = Regex.Replace(result, @"[ \t]{2,}", " ");

            // Collapse excessive newlines
            result = Regex.Replace(result, @"\n{3,}", "\n\n");

            // Remove duplicate sentences
            var sentences = Regex.Split(result, @"(?<=[.!?])\s+").ToList();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<string>();
            foreach (var sentence in sentences)
            {
                var normalized = Regex.Replace(sentence.Trim(), @"\s+", " ");
                if (unique.Add(normalized))
                    deduped.Add(sentence);
            }
            result = string.Join(" ", deduped);

            // Clean up any double spaces left from removals
            result = Regex.Replace(result, @"  +", " ").Trim();

            return result;
        }

        /// <summary>
        /// Analyzes and auto-optimizes, returning a full report with the optimized prompt.
        /// </summary>
        public CostOptimizationReport AnalyzeAndOptimize(string prompt)
        {
            var report = Analyze(prompt);
            report.OptimizedPrompt = Optimize(prompt);
            report.OptimizedTokens = PromptGuard.EstimateTokens(report.OptimizedPrompt);
            return report;
        }

        /// <summary>
        /// Estimates prompt complexity as a tier (1=basic, 2=mid, 3=advanced).
        /// </summary>
        private int EstimateComplexity(string prompt)
        {
            int score = 0;

            // Length-based
            int tokens = PromptGuard.EstimateTokens(prompt);
            if (tokens > 2000) score += 2;
            else if (tokens > 500) score += 1;

            // Structural indicators of complexity
            if (Regex.IsMatch(prompt, @"(step\s*\d|chain|multi.?step|reason|think|analyze)", RegexOptions.IgnoreCase))
                score += 1;
            if (Regex.IsMatch(prompt, @"(code|function|class|implement|algorithm|debug)", RegexOptions.IgnoreCase))
                score += 1;
            if (Regex.IsMatch(prompt, @"(creative|story|poem|write|compose|narrative)", RegexOptions.IgnoreCase))
                score += 1;
            if (Regex.IsMatch(prompt, @"(translate|multilingual|language)", RegexOptions.IgnoreCase))
                score += 1;

            if (score >= 4) return 3;
            if (score >= 2) return 2;
            return 1;
        }

        private static string ComplexityLabel(int tier) => tier switch
        {
            1 => "basic",
            2 => "moderate",
            3 => "advanced",
            _ => "unknown",
        };

    }
}