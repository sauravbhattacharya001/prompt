namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Maturity level for prompt engineering capability (1-5 scale).
    /// </summary>
    public enum MaturityLevel
    {
        /// <summary>Level 1: Ad-hoc, unstructured prompts.</summary>
        Initial = 1,
        /// <summary>Level 2: Some structure but inconsistent.</summary>
        Developing = 2,
        /// <summary>Level 3: Defined practices with repeatable patterns.</summary>
        Defined = 3,
        /// <summary>Level 4: Measured and optimized systematically.</summary>
        Managed = 4,
        /// <summary>Level 5: Continuously improving with advanced techniques.</summary>
        Optimizing = 5
    }

    /// <summary>
    /// Capability dimension assessed in the maturity model.
    /// </summary>
    public enum MaturityDimension
    {
        /// <summary>Role/persona definition and system instruction quality.</summary>
        RoleClarity,
        /// <summary>Task specification precision and decomposition.</summary>
        TaskSpecification,
        /// <summary>Output format control and constraints.</summary>
        OutputControl,
        /// <summary>Context management and grounding.</summary>
        ContextManagement,
        /// <summary>Error handling, edge cases, and guardrails.</summary>
        Robustness,
        /// <summary>Few-shot examples and demonstration quality.</summary>
        ExampleUsage,
        /// <summary>Token efficiency and cost awareness.</summary>
        Efficiency,
        /// <summary>Safety, bias mitigation, and ethical considerations.</summary>
        SafetyEthics
    }

    /// <summary>
    /// Assessment result for a single dimension.
    /// </summary>
    public class DimensionAssessment
    {
        /// <summary>The dimension assessed.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MaturityDimension Dimension { get; set; }

        /// <summary>Achieved maturity level (1-5).</summary>
        public int Level { get; set; }

        /// <summary>Score within the level (0.0-1.0).</summary>
        public double Score { get; set; }

        /// <summary>Evidence found supporting the assessment.</summary>
        public List<string> Evidence { get; set; } = new();

        /// <summary>Specific recommendations to reach the next level.</summary>
        public List<string> Recommendations { get; set; } = new();

        /// <summary>Weight of this dimension in overall score (default 1.0).</summary>
        public double Weight { get; set; } = 1.0;
    }

    /// <summary>
    /// Complete maturity assessment result.
    /// </summary>
    public class MaturityAssessment
    {
        /// <summary>Overall maturity level.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MaturityLevel OverallLevel { get; set; }

        /// <summary>Overall weighted score (0.0-5.0).</summary>
        public double OverallScore { get; set; }

        /// <summary>Per-dimension assessment details.</summary>
        public List<DimensionAssessment> Dimensions { get; set; } = new();

        /// <summary>Top 3 priority recommendations.</summary>
        public List<string> TopRecommendations { get; set; } = new();

        /// <summary>Strengths identified.</summary>
        public List<string> Strengths { get; set; } = new();

        /// <summary>Maturity profile label (e.g. "Safety-First", "Efficiency-Focused").</summary>
        public string ProfileLabel { get; set; } = "";

        /// <summary>Timestamp of assessment.</summary>
        public DateTime AssessedAt { get; set; }

        /// <summary>Render a human-readable maturity report.</summary>
        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════╗");
            sb.AppendLine("║     PROMPT MATURITY ASSESSMENT           ║");
            sb.AppendLine("╚══════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"  Overall Level:  {(int)OverallLevel} - {OverallLevel}");
            sb.AppendLine($"  Overall Score:  {OverallScore:F2} / 5.00");
            sb.AppendLine($"  Profile:        {ProfileLabel}");
            sb.AppendLine($"  Assessed:       {AssessedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // Bar chart
            sb.AppendLine("  ── Dimension Scores ──");
            foreach (var d in Dimensions.OrderByDescending(x => x.Level * 1.0 + x.Score))
            {
                var label = d.Dimension.ToString().PadRight(20);
                var filled = (int)Math.Round(d.Level * 2 + d.Score * 2);
                var bar = new string('█', Math.Min(filled, 10)).PadRight(10, '░');
                sb.AppendLine($"  {label} [{bar}] L{d.Level} ({d.Score:F1})");
            }
            sb.AppendLine();

            if (Strengths.Count > 0)
            {
                sb.AppendLine("  ✅ Strengths:");
                foreach (var s in Strengths)
                    sb.AppendLine($"    • {s}");
                sb.AppendLine();
            }

            if (TopRecommendations.Count > 0)
            {
                sb.AppendLine("  🚀 Top Recommendations:");
                for (int i = 0; i < TopRecommendations.Count; i++)
                    sb.AppendLine($"    {i + 1}. {TopRecommendations[i]}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>Serialize to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        /// <summary>Compare two assessments and show progress.</summary>
        public static string CompareProgress(MaturityAssessment before, MaturityAssessment after)
        {
            var sb = new StringBuilder();
            sb.AppendLine("── Maturity Progress ──");
            sb.AppendLine($"  Overall: {before.OverallScore:F2} → {after.OverallScore:F2} ({(after.OverallScore - before.OverallScore >= 0 ? "+" : "")}{after.OverallScore - before.OverallScore:F2})");
            sb.AppendLine($"  Level:   {before.OverallLevel} → {after.OverallLevel}");
            sb.AppendLine();

            var beforeDims = before.Dimensions.ToDictionary(d => d.Dimension);
            foreach (var ad in after.Dimensions)
            {
                if (beforeDims.TryGetValue(ad.Dimension, out var bd))
                {
                    var delta = (ad.Level + ad.Score) - (bd.Level + bd.Score);
                    var arrow = delta > 0.1 ? "↑" : delta < -0.1 ? "↓" : "→";
                    sb.AppendLine($"  {arrow} {ad.Dimension}: L{bd.Level} → L{ad.Level}");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Evaluates prompt engineering maturity across 8 capability dimensions,
    /// providing a structured assessment with levels, scores, evidence, and
    /// actionable recommendations for improvement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inspired by capability maturity models (CMMI), this analyzer assesses
    /// prompts on a 5-level scale across dimensions like role clarity, task
    /// specification, output control, and safety. Each dimension is independently
    /// scored with specific evidence and next-level recommendations.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var model = new PromptMaturityModel();
    /// var assessment = model.Assess("You are a senior code reviewer...");
    /// Console.WriteLine(assessment.ToReport());
    /// // Shows overall level, dimension breakdown, strengths, recommendations
    ///
    /// // Compare before/after improvement
    /// var improved = model.Assess(improvedPrompt);
    /// Console.WriteLine(MaturityAssessment.CompareProgress(assessment, improved));
    ///
    /// // Custom dimension weights
    /// model.SetWeight(MaturityDimension.SafetyEthics, 2.0);
    /// model.SetWeight(MaturityDimension.Efficiency, 0.5);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptMaturityModel
    {
        private readonly Dictionary<MaturityDimension, double> _weights = new();

        /// <summary>
        /// Creates a new maturity model with equal dimension weights.
        /// </summary>
        public PromptMaturityModel()
        {
            foreach (MaturityDimension dim in Enum.GetValues(typeof(MaturityDimension)))
                _weights[dim] = 1.0;
        }

        /// <summary>
        /// Set the weight for a dimension (default 1.0). Higher = more impact on overall score.
        /// </summary>
        public PromptMaturityModel SetWeight(MaturityDimension dimension, double weight)
        {
            if (weight < 0) throw new ArgumentException("Weight must be non-negative.", nameof(weight));
            _weights[dimension] = weight;
            return this;
        }

        /// <summary>
        /// Assess a prompt's maturity across all dimensions.
        /// </summary>
        public MaturityAssessment Assess(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

            var dimensions = new List<DimensionAssessment>();
            foreach (MaturityDimension dim in Enum.GetValues(typeof(MaturityDimension)))
            {
                var assessment = AssessDimension(dim, prompt);
                assessment.Weight = _weights[dim];
                dimensions.Add(assessment);
            }

            var totalWeight = dimensions.Sum(d => d.Weight);
            var weightedScore = totalWeight > 0
                ? dimensions.Sum(d => (d.Level + d.Score * 0.9) * d.Weight) / totalWeight
                : 0;

            var overallLevel = weightedScore switch
            {
                >= 4.5 => MaturityLevel.Optimizing,
                >= 3.5 => MaturityLevel.Managed,
                >= 2.5 => MaturityLevel.Defined,
                >= 1.5 => MaturityLevel.Developing,
                _ => MaturityLevel.Initial
            };

            // Identify strengths (top dimensions at L3+)
            var strengths = dimensions
                .Where(d => d.Level >= 3)
                .OrderByDescending(d => d.Level + d.Score)
                .Take(3)
                .Select(d => $"{d.Dimension}: Level {d.Level}")
                .ToList();

            // Top recommendations from weakest dimensions
            var topRecs = dimensions
                .Where(d => d.Level < 4)
                .OrderBy(d => d.Level + d.Score)
                .Take(3)
                .SelectMany(d => d.Recommendations.Take(1))
                .ToList();

            // Profile label based on highest-scoring dimensions
            var profileLabel = DetermineProfile(dimensions);

            return new MaturityAssessment
            {
                OverallLevel = overallLevel,
                OverallScore = Math.Round(weightedScore, 2),
                Dimensions = dimensions,
                TopRecommendations = topRecs,
                Strengths = strengths,
                ProfileLabel = profileLabel,
                AssessedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Assess multiple prompts and return a portfolio-level summary.
        /// </summary>
        public (MaturityAssessment Average, MaturityAssessment Weakest, MaturityAssessment Strongest) AssessPortfolio(IEnumerable<string> prompts)
        {
            var assessments = prompts.Select(Assess).ToList();
            if (assessments.Count == 0)
                throw new ArgumentException("At least one prompt required.");

            var weakest = assessments.MinBy(a => a.OverallScore)!;
            var strongest = assessments.MaxBy(a => a.OverallScore)!;

            // Build average assessment
            var avgDimensions = new List<DimensionAssessment>();
            foreach (MaturityDimension dim in Enum.GetValues(typeof(MaturityDimension)))
            {
                var dimAssessments = assessments.SelectMany(a => a.Dimensions).Where(d => d.Dimension == dim).ToList();
                var avgLevel = (int)Math.Round(dimAssessments.Average(d => d.Level));
                var avgScore = dimAssessments.Average(d => d.Score);
                avgDimensions.Add(new DimensionAssessment
                {
                    Dimension = dim,
                    Level = avgLevel,
                    Score = Math.Round(avgScore, 2),
                    Evidence = new List<string> { $"Averaged across {assessments.Count} prompts" },
                    Recommendations = dimAssessments.SelectMany(d => d.Recommendations).Distinct().Take(2).ToList(),
                    Weight = _weights[dim]
                });
            }

            var totalWeight = avgDimensions.Sum(d => d.Weight);
            var avgWeightedScore = totalWeight > 0
                ? avgDimensions.Sum(d => (d.Level + d.Score * 0.9) * d.Weight) / totalWeight
                : 0;

            var avg = new MaturityAssessment
            {
                OverallLevel = (MaturityLevel)Math.Max(1, Math.Min(5, (int)Math.Round(avgWeightedScore))),
                OverallScore = Math.Round(avgWeightedScore, 2),
                Dimensions = avgDimensions,
                TopRecommendations = avgDimensions.OrderBy(d => d.Level).Take(3).SelectMany(d => d.Recommendations.Take(1)).ToList(),
                Strengths = avgDimensions.Where(d => d.Level >= 3).OrderByDescending(d => d.Level).Take(3).Select(d => $"{d.Dimension}: Level {d.Level}").ToList(),
                ProfileLabel = $"Portfolio Average ({assessments.Count} prompts)",
                AssessedAt = DateTime.UtcNow
            };

            return (avg, weakest, strongest);
        }

        private DimensionAssessment AssessDimension(MaturityDimension dimension, string prompt)
        {
            return dimension switch
            {
                MaturityDimension.RoleClarity => AssessRoleClarity(prompt),
                MaturityDimension.TaskSpecification => AssessTaskSpecification(prompt),
                MaturityDimension.OutputControl => AssessOutputControl(prompt),
                MaturityDimension.ContextManagement => AssessContextManagement(prompt),
                MaturityDimension.Robustness => AssessRobustness(prompt),
                MaturityDimension.ExampleUsage => AssessExampleUsage(prompt),
                MaturityDimension.Efficiency => AssessEfficiency(prompt),
                MaturityDimension.SafetyEthics => AssessSafetyEthics(prompt),
                _ => new DimensionAssessment { Dimension = dimension, Level = 1, Score = 0 }
            };
        }

        private DimensionAssessment AssessRoleClarity(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var lower = prompt.ToLowerInvariant();

            // L1: Any text at all
            score++;

            // L2: Has role/persona keywords
            var rolePatterns = new[] { "you are", "act as", "role:", "persona:", "system:", "as a", "you're a", "behave as" };
            if (rolePatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Contains role/persona definition");
            }
            else recs.Add("Add a clear role definition (e.g., 'You are a senior data analyst...')");

            // L3: Specific expertise/domain
            var expertiseWords = new[] { "expert", "specialist", "senior", "experienced", "professional", "proficient", "skilled" };
            if (expertiseWords.Any(w => lower.Contains(w)))
            {
                score++;
                evidence.Add("Specifies expertise level");
            }
            else recs.Add("Define expertise level and domain (e.g., 'expert in Python web security')");

            // L4: Behavioral constraints
            var behaviorPatterns = new[] { "always", "never", "must", "do not", "don't", "avoid", "ensure", "make sure", "refrain" };
            if (behaviorPatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Includes behavioral constraints");
            }
            else recs.Add("Add behavioral guidelines (what the role should always/never do)");

            // L5: Audience awareness + tone
            var audiencePatterns = new[] { "audience", "reader", "user", "beginner", "technical", "non-technical", "tone", "style", "voice", "formal", "casual" };
            if (audiencePatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Defines audience and communication style");
            }
            else recs.Add("Specify target audience and desired tone/style");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.RoleClarity,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private DimensionAssessment AssessTaskSpecification(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var lower = prompt.ToLowerInvariant();

            score++; // L1 base

            // L2: Has an imperative/action verb
            var actionVerbs = new[] { "analyze", "create", "generate", "write", "build", "design", "explain", "summarize", "review", "evaluate", "compare", "list", "extract", "translate", "implement", "calculate", "classify", "convert" };
            if (actionVerbs.Any(v => lower.Contains(v)))
            {
                score++;
                evidence.Add("Contains clear action verbs");
            }
            else recs.Add("Start with a clear action verb (analyze, create, summarize, etc.)");

            // L3: Has numbered steps or structured breakdown
            var hasSteps = lower.Contains("step 1") || lower.Contains("1.") || lower.Contains("first,") || lower.Contains("then,") || lower.Contains("finally,") || lower.Contains("step-by-step");
            if (hasSteps)
            {
                score++;
                evidence.Add("Uses structured step-by-step instructions");
            }
            else recs.Add("Break complex tasks into numbered steps or phases");

            // L4: Scope boundaries
            var scopePatterns = new[] { "scope", "limit", "only", "focus on", "specifically", "do not include", "exclude", "within", "between", "no more than", "at most", "at least" };
            if (scopePatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Defines scope and boundaries");
            }
            else recs.Add("Add explicit scope boundaries (what to include/exclude, limits)");

            // L5: Success criteria or evaluation
            var criteriaPatterns = new[] { "success", "criteria", "quality", "evaluate", "metric", "measure", "acceptable", "expected outcome", "goal", "objective" };
            if (criteriaPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Includes success criteria or goals");
            }
            else recs.Add("Define what a successful response looks like (acceptance criteria)");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.TaskSpecification,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private DimensionAssessment AssessOutputControl(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var lower = prompt.ToLowerInvariant();

            score++; // L1 base

            // L2: Mentions format
            var formatPatterns = new[] { "format", "json", "xml", "csv", "markdown", "html", "yaml", "table", "list", "bullet", "numbered" };
            if (formatPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Specifies output format");
            }
            else recs.Add("Specify desired output format (JSON, markdown, table, etc.)");

            // L3: Structure template or schema
            var schemaPatterns = new[] { "schema", "template", "structure", "field", "column", "header", "section", "```", "{", "}" };
            if (schemaPatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Provides output structure/template");
            }
            else recs.Add("Include an output template or schema showing exact expected structure");

            // L4: Length/detail constraints
            var lengthPatterns = new[] { "word", "sentence", "paragraph", "brief", "concise", "detailed", "comprehensive", "maximum", "minimum", "length", "short", "long" };
            if (lengthPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Constrains output length/detail level");
            }
            else recs.Add("Add length or detail constraints (e.g., 'in 3-5 sentences', 'maximum 200 words')");

            // L5: Multiple format options or conditional formatting
            var advancedPatterns = new[] { "if the", "otherwise", "when", "depending on", "for each", "per item", "alternatively" };
            var formatCount = formatPatterns.Count(p => lower.Contains(p));
            if (formatCount >= 2 || advancedPatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Uses conditional or multi-format output control");
            }
            else recs.Add("Add conditional formatting rules or support multiple output modes");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.OutputControl,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private DimensionAssessment AssessContextManagement(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var lower = prompt.ToLowerInvariant();

            score++; // L1 base

            // L2: Provides any context/background
            var contextPatterns = new[] { "context:", "background:", "given", "based on", "consider", "the following", "here is", "below is", "provided" };
            if (contextPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Provides context/background information");
            }
            else recs.Add("Add context section explaining the situation and available information");

            // L3: References or data
            var dataPatterns = new[] { "data:", "input:", "source:", "reference", "document", "text:", "content:", "example:", "sample:" };
            if (dataPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Includes reference data or inputs");
            }
            else recs.Add("Include specific data, documents, or references the model should use");

            // L4: Context boundaries
            var boundaryPatterns = new[] { "only use", "do not assume", "don't assume", "based solely", "from the provided", "ignore", "disregard", "stick to" };
            if (boundaryPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Sets context boundaries (what to use/ignore)");
            }
            else recs.Add("Set explicit context boundaries (e.g., 'only use the provided data, do not assume')");

            // L5: Dynamic context / variable slots
            var dynamicPatterns = new[] { "{{", "}}", "{", "}", "[insert", "[your", "<input>", "<context>", "variable", "placeholder", "parameter" };
            if (dynamicPatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Uses dynamic context with variable placeholders");
            }
            else recs.Add("Use variable placeholders for reusable prompts (e.g., '{{topic}}', '{{user_input}}')");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.ContextManagement,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private DimensionAssessment AssessRobustness(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var lower = prompt.ToLowerInvariant();

            score++; // L1 base

            // L2: Any error/edge handling mention
            var errorPatterns = new[] { "error", "invalid", "missing", "empty", "null", "edge case", "exception", "fail", "wrong" };
            if (errorPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Addresses error/edge cases");
            }
            else recs.Add("Add instructions for handling errors, invalid inputs, or edge cases");

            // L3: Fallback behavior
            var fallbackPatterns = new[] { "otherwise", "if not", "if no", "fallback", "default", "instead", "alternatively", "in case" };
            if (fallbackPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Defines fallback/default behavior");
            }
            else recs.Add("Specify fallback behavior when ideal conditions aren't met");

            // L4: Confidence/uncertainty handling
            var uncertaintyPatterns = new[] { "confident", "uncertain", "unsure", "not sure", "ambiguous", "unclear", "if you don't know", "if unsure", "confidence", "certainty" };
            if (uncertaintyPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Handles uncertainty and confidence levels");
            }
            else recs.Add("Add instructions for handling uncertainty (e.g., 'if unsure, say so and explain why')");

            // L5: Validation + self-check
            var validationPatterns = new[] { "verify", "validate", "double-check", "double check", "review your", "check your", "self-check", "proofread", "confirm" };
            if (validationPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Includes self-validation instructions");
            }
            else recs.Add("Ask the model to verify/validate its own output before finalizing");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.Robustness,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private DimensionAssessment AssessExampleUsage(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var lower = prompt.ToLowerInvariant();

            score++; // L1 base

            // L2: Any example present
            var examplePatterns = new[] { "example:", "for example", "e.g.", "such as", "like this:", "sample:", "instance:" };
            if (examplePatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Includes examples");
            }
            else recs.Add("Add at least one input/output example to demonstrate expected behavior");

            // L3: Input-output paired examples
            var pairedPatterns = new[] { "input:", "output:", "question:", "answer:", "prompt:", "response:", "before:", "after:" };
            if (pairedPatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Uses paired input/output examples (few-shot)");
            }
            else recs.Add("Use paired input→output examples for few-shot learning");

            // L4: Multiple examples
            var exampleCount = examplePatterns.Sum(p => CountOccurrences(lower, p));
            if (exampleCount >= 3)
            {
                score++;
                evidence.Add($"Multiple examples provided ({exampleCount} markers found)");
            }
            else recs.Add("Add 3+ diverse examples covering different scenarios");

            // L5: Counter-examples or edge case examples
            var counterPatterns = new[] { "bad example", "incorrect", "wrong example", "do not", "negative example", "anti-pattern", "avoid this", "not like this" };
            if (counterPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Includes counter-examples or negative demonstrations");
            }
            else recs.Add("Add counter-examples showing what NOT to do for clearer boundaries");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.ExampleUsage,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private DimensionAssessment AssessEfficiency(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var words = prompt.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var wordCount = words.Length;
            var lower = prompt.ToLowerInvariant();

            score++; // L1 base

            // L2: Reasonable length (not excessively wordy)
            if (wordCount <= 500)
            {
                score++;
                evidence.Add($"Reasonable length ({wordCount} words)");
            }
            else recs.Add($"Consider reducing prompt length ({wordCount} words). Aim for <500 words while preserving clarity");

            // L3: No significant repetition
            var sentences = prompt.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var uniqueSentences = sentences.Select(s => s.Trim().ToLower()).Distinct().Count();
            var repetitionRatio = sentences.Length > 0 ? (double)uniqueSentences / sentences.Length : 1.0;
            if (repetitionRatio > 0.85)
            {
                score++;
                evidence.Add("Low repetition detected");
            }
            else recs.Add("Remove redundant or repeated instructions to save tokens");

            // L4: Uses concise patterns
            var concisePatterns = new[] { "concise", "brief", "succinct", "terse", "efficient", "minimal" };
            var verboseWarnings = new[] { "please note that", "it is important to note", "it should be noted", "keep in mind that", "as mentioned earlier" };
            bool isConcise = concisePatterns.Any(p => lower.Contains(p)) || !verboseWarnings.Any(p => lower.Contains(p));
            if (isConcise && wordCount < 300)
            {
                score++;
                evidence.Add("Uses concise language patterns");
            }
            else recs.Add("Eliminate filler phrases ('please note that', 'it is important to') for token efficiency");

            // L5: Structured for caching / reuse
            var reusePatterns = new[] { "{{", "}}", "template", "reusable", "variable", "parameter", "slot" };
            if (reusePatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Designed for reuse/caching with templates");
            }
            else recs.Add("Structure prompt with template variables for reusability and prefix caching");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.Efficiency,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private DimensionAssessment AssessSafetyEthics(string prompt)
        {
            var evidence = new List<string>();
            var recs = new List<string>();
            int score = 0;
            var lower = prompt.ToLowerInvariant();

            score++; // L1 base

            // L2: Any safety awareness
            var safetyPatterns = new[] { "safe", "appropriate", "respectful", "responsible", "ethical", "harm", "offensive", "sensitive" };
            if (safetyPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Shows safety/ethics awareness");
            }
            else recs.Add("Add basic safety guidelines (e.g., 'provide safe and appropriate responses')");

            // L3: Content boundaries
            var boundaryPatterns = new[] { "do not generate", "never", "refuse", "decline", "prohibited", "forbidden", "not allowed", "restricted", "must not" };
            if (boundaryPatterns.Count(p => lower.Contains(p)) >= 2)
            {
                score++;
                evidence.Add("Defines content boundaries and restrictions");
            }
            else recs.Add("Set explicit content boundaries (what topics/outputs are off-limits)");

            // L4: Bias awareness
            var biasPatterns = new[] { "bias", "fair", "balanced", "neutral", "objective", "inclusive", "diverse", "stereotype", "discrimination" };
            if (biasPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Addresses bias and fairness");
            }
            else recs.Add("Add bias awareness instructions (e.g., 'avoid stereotypes, present balanced perspectives')");

            // L5: Privacy + attribution
            var privacyPatterns = new[] { "privacy", "personal information", "pii", "confidential", "attribution", "source", "cite", "credit", "copyright", "consent" };
            if (privacyPatterns.Any(p => lower.Contains(p)))
            {
                score++;
                evidence.Add("Considers privacy, attribution, or IP concerns");
            }
            else recs.Add("Address privacy (PII handling), attribution, and intellectual property considerations");

            return new DimensionAssessment
            {
                Dimension = MaturityDimension.SafetyEthics,
                Level = Math.Min(score, 5),
                Score = score > 5 ? 1.0 : 0.0,
                Evidence = evidence,
                Recommendations = recs
            };
        }

        private string DetermineProfile(List<DimensionAssessment> dimensions)
        {
            var topDims = dimensions.OrderByDescending(d => d.Level + d.Score).Take(2).Select(d => d.Dimension).ToList();

            if (topDims.Contains(MaturityDimension.SafetyEthics))
                return "Safety-First";
            if (topDims.Contains(MaturityDimension.Efficiency))
                return "Efficiency-Focused";
            if (topDims.Contains(MaturityDimension.RoleClarity) && topDims.Contains(MaturityDimension.TaskSpecification))
                return "Well-Structured";
            if (topDims.Contains(MaturityDimension.ExampleUsage))
                return "Example-Driven";
            if (topDims.Contains(MaturityDimension.OutputControl))
                return "Output-Oriented";
            if (topDims.Contains(MaturityDimension.Robustness))
                return "Defensive";
            if (topDims.Contains(MaturityDimension.ContextManagement))
                return "Context-Rich";

            return "Balanced";
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
            {
                count++;
                i += pattern.Length;
            }
            return count;
        }
    }
}
