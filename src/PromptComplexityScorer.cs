namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A single complexity dimension score.
    /// </summary>
    public class ComplexityDimension
    {
        /// <summary>Gets the dimension name.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the raw score (0–10 scale).</summary>
        public double Score { get; internal set; }

        /// <summary>Gets the weight used in overall calculation.</summary>
        public double Weight { get; internal set; }

        /// <summary>Gets a human-readable explanation.</summary>
        public string Explanation { get; internal set; } = "";

        /// <summary>Gets specific evidence items.</summary>
        public List<string> Evidence { get; internal set; } = new();
    }

    /// <summary>
    /// Model tier recommendation based on complexity.
    /// </summary>
    public class ModelRecommendation
    {
        /// <summary>Gets the suggested model tier (e.g., "small", "medium", "large", "frontier").</summary>
        public string Tier { get; internal set; } = "";

        /// <summary>Gets the reasoning for the recommendation.</summary>
        public string Reasoning { get; internal set; } = "";

        /// <summary>Gets example models in this tier.</summary>
        public List<string> ExampleModels { get; internal set; } = new();
    }

    /// <summary>
    /// Full complexity analysis result.
    /// </summary>
    public class ComplexityResult
    {
        /// <summary>Gets the overall complexity score (0–10).</summary>
        public double OverallScore { get; internal set; }

        /// <summary>Gets the complexity level label.</summary>
        public string Level { get; internal set; } = "";

        /// <summary>Gets per-dimension breakdowns.</summary>
        public List<ComplexityDimension> Dimensions { get; internal set; } = new();

        /// <summary>Gets the model tier recommendation.</summary>
        public ModelRecommendation Recommendation { get; internal set; } = new();

        /// <summary>Gets estimated reasoning steps needed.</summary>
        public int EstimatedReasoningSteps { get; internal set; }

        /// <summary>Gets identified risk factors that may cause model failures.</summary>
        public List<string> RiskFactors { get; internal set; } = new();

        /// <summary>Gets a formatted summary string.</summary>
        public string Summary => $"Complexity: {OverallScore:F1}/10 ({Level}) — {Recommendation.Tier} model recommended";
    }

    /// <summary>
    /// Analyzes prompt complexity across multiple dimensions to help users
    /// understand whether a prompt is suitable for simpler/cheaper models
    /// or requires frontier-class reasoning.
    /// </summary>
    /// <remarks>
    /// <para>Dimensions scored:</para>
    /// <list type="bullet">
    /// <item><description><b>Instruction Density</b> — how many distinct instructions per 100 tokens</description></item>
    /// <item><description><b>Nesting Depth</b> — conditional/structural nesting complexity</description></item>
    /// <item><description><b>Variable Load</b> — template variables, placeholders, dynamic content</description></item>
    /// <item><description><b>Ambiguity</b> — vague language, hedging, underspecified constraints</description></item>
    /// <item><description><b>Domain Specificity</b> — specialized vocabulary and domain knowledge required</description></item>
    /// <item><description><b>Output Constraints</b> — format/structure requirements on the response</description></item>
    /// <item><description><b>Reasoning Depth</b> — multi-step logic, analysis, comparison required</description></item>
    /// <item><description><b>Context Dependency</b> — how much external knowledge/context is assumed</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var scorer = new PromptComplexityScorer();
    /// var result = scorer.Score("You are a helpful assistant. Answer the user's question.");
    /// Console.WriteLine(result.Summary);
    /// // Complexity: 1.2/10 (Trivial) — small model recommended
    ///
    /// var complex = scorer.Score(@"
    ///   You are a senior tax advisor. Given the user's W-2, 1099-DIV, and
    ///   Schedule K-1, compute their estimated tax liability under both
    ///   standard and itemized deductions. If married filing jointly and
    ///   AGI exceeds {{threshold}}, apply AMT calculations. Output as JSON
    ///   with fields: gross_income, deductions, taxable_income, tax_owed,
    ///   effective_rate. Show your reasoning step by step.");
    /// Console.WriteLine(complex.Summary);
    /// // Complexity: 7.8/10 (High) — frontier model recommended
    /// </code>
    /// </example>
    public class PromptComplexityScorer
    {
        private static readonly Regex InstructionPattern = new(
            @"(?:^|\.\s+)(?:you\s+(?:must|should|will|need\s+to|are\s+to)|please\s+\w+|do\s+not|don't|never|always|ensure|make\s+sure|remember\s+to|be\s+sure\s+to|include|exclude|avoid|use\s+only|output|return|respond|answer|list|explain|analyze|compare|summarize|compute|calculate|generate|create|write|format|provide|give\s+me|show)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex NestingPattern = new(
            @"\b(?:if\b|else\b|otherwise|unless|when\b|in\s+(?:that\s+)?case|except\s+(?:when|if)|provided\s+that|assuming|given\s+that|should\s+(?:the|this|it))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VariablePattern = new(
            @"(?:\{\{[\w.]+\}\}|\{[\w.]+\}|\[\[[\w.]+\]\]|<[\w.]+>|\$\{[\w.]+\}|<<[\w.]+>>)",
            RegexOptions.Compiled);

        private static readonly Regex AmbiguityPattern = new(
            @"\b(?:maybe|perhaps|possibly|might|could\s+be|some(?:what|how)|sort\s+of|kind\s+of|roughly|approximately|etc\.?|and\s+so\s+on|or\s+something|as\s+(?:needed|appropriate|necessary)|if\s+(?:possible|applicable)|optionally|you\s+(?:may|can)\s+also)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DomainPattern = new(
            @"\b(?:API|SDK|REST|GraphQL|SQL|HIPAA|GDPR|PCI|WCAG|OWASP|OAuth|JWT|SAML|CORS|CRUD|ETL|CI/CD|TCP|UDP|DNS|SSL|TLS|ACID|CAP|regex|AST|IR|FFT|PCA|SVD|LSTM|GAN|BERT|transformer|embeddings|eigenvalue|gradient|backprop|tokenizer|attention\s+mechanism|fine-?tun|K-?means|SVM|AMT|AGI|W-?2|1099|Schedule\s+K|10-?K|EBITDA|P/?E\s+ratio|yield\s+curve|alpha|beta|sharpe|drawdown|VaR)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex OutputFormatPattern = new(
            @"\b(?:JSON|XML|CSV|YAML|markdown|table|bullet\s*(?:point|list)|numbered\s+list|heading|format\s+(?:as|like|in)|schema|struct(?:ure)?d?\s+(?:output|response|format)|fields?:\s*\w+|columns?:\s*\w+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ReasoningPattern = new(
            @"\b(?:step\s+by\s+step|chain\s+of\s+thought|think\s+(?:through|about|carefully)|reason(?:ing)?|analy[sz]e|compare\s+and\s+contrast|evaluate|weigh\s+(?:the\s+)?(?:pros|options|trade-?offs)|consider\s+(?:all|each|multiple)|break\s+(?:it\s+)?down|first\s*[,\.]\s*(?:then|next|second)|multi-?step|cross-?reference|synthesize|infer|deduce|derive)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ContextDependencyPattern = new(
            @"\b(?:the\s+(?:above|previous|following|given|provided|attached|uploaded)|based\s+on|referring\s+to|as\s+(?:mentioned|described|shown)|context|background|prior\s+(?:knowledge|conversation)|history|earlier|recall\s+that|remember\s+that|you\s+(?:already\s+)?know)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Scores the complexity of the given prompt text.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>A <see cref="ComplexityResult"/> with dimension scores and recommendations.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompt"/> is null.</exception>
        public ComplexityResult Score(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new ComplexityResult
                {
                    OverallScore = 0,
                    Level = "Empty",
                    Dimensions = new List<ComplexityDimension>(),
                    Recommendation = new ModelRecommendation
                    {
                        Tier = "none",
                        Reasoning = "Prompt is empty.",
                        ExampleModels = new List<string>()
                    },
                    EstimatedReasoningSteps = 0,
                    RiskFactors = new List<string>()
                };
            }

            int estimatedTokens = EstimateTokens(prompt);
            var dimensions = new List<ComplexityDimension>
            {
                ScoreInstructionDensity(prompt, estimatedTokens),
                ScoreNestingDepth(prompt),
                ScoreVariableLoad(prompt),
                ScoreAmbiguity(prompt, estimatedTokens),
                ScoreDomainSpecificity(prompt),
                ScoreOutputConstraints(prompt),
                ScoreReasoningDepth(prompt),
                ScoreContextDependency(prompt)
            };

            double weightedSum = dimensions.Sum(d => d.Score * d.Weight);
            double totalWeight = dimensions.Sum(d => d.Weight);
            double overall = totalWeight > 0 ? Math.Round(weightedSum / totalWeight, 1) : 0;
            overall = Math.Min(10, Math.Max(0, overall));

            string level = overall switch
            {
                <= 1.5 => "Trivial",
                <= 3.0 => "Simple",
                <= 5.0 => "Moderate",
                <= 7.0 => "Complex",
                <= 8.5 => "High",
                _ => "Extreme"
            };

            var risks = IdentifyRiskFactors(dimensions, prompt, estimatedTokens);
            int reasoningSteps = EstimateReasoningSteps(dimensions);

            return new ComplexityResult
            {
                OverallScore = overall,
                Level = level,
                Dimensions = dimensions,
                Recommendation = RecommendModel(overall, dimensions),
                EstimatedReasoningSteps = reasoningSteps,
                RiskFactors = risks
            };
        }

        private static int EstimateTokens(string text)
        {
            // Rough approximation: ~4 chars per token for English
            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }

        private static int CountMatches(Regex pattern, string text)
        {
            return pattern.Matches(text).Count;
        }

        private ComplexityDimension ScoreInstructionDensity(string prompt, int tokens)
        {
            int count = CountMatches(InstructionPattern, prompt);
            double per100 = tokens > 0 ? (count * 100.0) / tokens : 0;
            double score = Math.Min(10, per100 * 3.0);
            if (count >= 10) score = Math.Max(score, 7);
            if (count >= 20) score = Math.Max(score, 9);

            return new ComplexityDimension
            {
                Name = "Instruction Density",
                Score = Math.Round(score, 1),
                Weight = 1.5,
                Explanation = $"Found {count} instruction markers (~{per100:F1} per 100 tokens).",
                Evidence = InstructionPattern.Matches(prompt).Cast<Match>()
                    .Take(5).Select(m => m.Value.Trim()).ToList()
            };
        }

        private ComplexityDimension ScoreNestingDepth(string prompt)
        {
            int count = CountMatches(NestingPattern, prompt);
            double score = Math.Min(10, count * 1.8);

            return new ComplexityDimension
            {
                Name = "Nesting Depth",
                Score = Math.Round(score, 1),
                Weight = 1.2,
                Explanation = $"Found {count} conditional/branching constructs.",
                Evidence = NestingPattern.Matches(prompt).Cast<Match>()
                    .Take(5).Select(m => m.Value.Trim()).ToList()
            };
        }

        private ComplexityDimension ScoreVariableLoad(string prompt)
        {
            int count = CountMatches(VariablePattern, prompt);
            double score = Math.Min(10, count * 2.0);

            return new ComplexityDimension
            {
                Name = "Variable Load",
                Score = Math.Round(score, 1),
                Weight = 0.8,
                Explanation = $"Found {count} template variable(s)/placeholder(s).",
                Evidence = VariablePattern.Matches(prompt).Cast<Match>()
                    .Take(5).Select(m => m.Value).ToList()
            };
        }

        private ComplexityDimension ScoreAmbiguity(string prompt, int tokens)
        {
            int count = CountMatches(AmbiguityPattern, prompt);
            double per100 = tokens > 0 ? (count * 100.0) / tokens : 0;
            double score = Math.Min(10, per100 * 5.0);

            return new ComplexityDimension
            {
                Name = "Ambiguity",
                Score = Math.Round(score, 1),
                Weight = 1.0,
                Explanation = $"Found {count} ambiguous/vague term(s) (~{per100:F1} per 100 tokens).",
                Evidence = AmbiguityPattern.Matches(prompt).Cast<Match>()
                    .Take(5).Select(m => m.Value.Trim()).ToList()
            };
        }

        private ComplexityDimension ScoreDomainSpecificity(string prompt)
        {
            var matches = DomainPattern.Matches(prompt);
            var unique = matches.Cast<Match>().Select(m => m.Value.ToUpperInvariant()).Distinct().ToList();
            double score = Math.Min(10, unique.Count * 1.5);

            return new ComplexityDimension
            {
                Name = "Domain Specificity",
                Score = Math.Round(score, 1),
                Weight = 1.0,
                Explanation = $"Found {unique.Count} domain-specific term(s).",
                Evidence = unique.Take(5).ToList()
            };
        }

        private ComplexityDimension ScoreOutputConstraints(string prompt)
        {
            int count = CountMatches(OutputFormatPattern, prompt);
            double score = Math.Min(10, count * 2.5);

            return new ComplexityDimension
            {
                Name = "Output Constraints",
                Score = Math.Round(score, 1),
                Weight = 0.8,
                Explanation = $"Found {count} output format requirement(s).",
                Evidence = OutputFormatPattern.Matches(prompt).Cast<Match>()
                    .Take(5).Select(m => m.Value.Trim()).ToList()
            };
        }

        private ComplexityDimension ScoreReasoningDepth(string prompt)
        {
            int count = CountMatches(ReasoningPattern, prompt);
            double score = Math.Min(10, count * 2.5);

            return new ComplexityDimension
            {
                Name = "Reasoning Depth",
                Score = Math.Round(score, 1),
                Weight = 1.5,
                Explanation = $"Found {count} reasoning/analysis requirement(s).",
                Evidence = ReasoningPattern.Matches(prompt).Cast<Match>()
                    .Take(5).Select(m => m.Value.Trim()).ToList()
            };
        }

        private ComplexityDimension ScoreContextDependency(string prompt)
        {
            int count = CountMatches(ContextDependencyPattern, prompt);
            double score = Math.Min(10, count * 2.0);

            return new ComplexityDimension
            {
                Name = "Context Dependency",
                Score = Math.Round(score, 1),
                Weight = 0.7,
                Explanation = $"Found {count} external context reference(s).",
                Evidence = ContextDependencyPattern.Matches(prompt).Cast<Match>()
                    .Take(5).Select(m => m.Value.Trim()).ToList()
            };
        }

        private static List<string> IdentifyRiskFactors(List<ComplexityDimension> dims, string prompt, int tokens)
        {
            var risks = new List<string>();
            var ambiguity = dims.FirstOrDefault(d => d.Name == "Ambiguity");
            var instructions = dims.FirstOrDefault(d => d.Name == "Instruction Density");
            var reasoning = dims.FirstOrDefault(d => d.Name == "Reasoning Depth");
            var nesting = dims.FirstOrDefault(d => d.Name == "Nesting Depth");

            if (ambiguity?.Score >= 5)
                risks.Add("High ambiguity may cause inconsistent outputs across runs.");
            if (instructions?.Score >= 7)
                risks.Add("Dense instructions increase the risk of the model skipping or conflating directives.");
            if (reasoning?.Score >= 7 && nesting?.Score >= 5)
                risks.Add("Complex reasoning with branching logic may exceed simpler models' capabilities.");
            if (tokens > 2000)
                risks.Add($"Long prompt (~{tokens} tokens) increases cost and may hit context limits on smaller models.");
            if (dims.Count(d => d.Score >= 6) >= 4)
                risks.Add("Multiple high-complexity dimensions — consider breaking into sub-prompts.");

            return risks;
        }

        private static int EstimateReasoningSteps(List<ComplexityDimension> dims)
        {
            var instructions = dims.FirstOrDefault(d => d.Name == "Instruction Density")?.Score ?? 0;
            var reasoning = dims.FirstOrDefault(d => d.Name == "Reasoning Depth")?.Score ?? 0;
            var nesting = dims.FirstOrDefault(d => d.Name == "Nesting Depth")?.Score ?? 0;

            return Math.Max(1, (int)Math.Ceiling((instructions + reasoning * 1.5 + nesting) / 2.5));
        }

        private static ModelRecommendation RecommendModel(double overall, List<ComplexityDimension> dims)
        {
            if (overall <= 2.0)
            {
                return new ModelRecommendation
                {
                    Tier = "small",
                    Reasoning = "Low complexity — a small, fast model handles this well.",
                    ExampleModels = new List<string> { "GPT-4o-mini", "Claude Haiku", "Gemini Flash" }
                };
            }
            if (overall <= 4.5)
            {
                return new ModelRecommendation
                {
                    Tier = "medium",
                    Reasoning = "Moderate complexity — a mid-tier model balances cost and quality.",
                    ExampleModels = new List<string> { "GPT-4o", "Claude Sonnet", "Gemini Pro" }
                };
            }
            if (overall <= 7.0)
            {
                return new ModelRecommendation
                {
                    Tier = "large",
                    Reasoning = "High complexity — a capable model is needed for reliable results.",
                    ExampleModels = new List<string> { "GPT-4o (high-reasoning)", "Claude Sonnet (extended thinking)", "Gemini Pro 2" }
                };
            }
            return new ModelRecommendation
            {
                Tier = "frontier",
                Reasoning = "Extreme complexity — use the most capable model available.",
                ExampleModels = new List<string> { "o1/o3", "Claude Opus", "Gemini Ultra" }
            };
        }
    }
}
