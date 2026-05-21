namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ────────────────────────────────────────────
    //  PromptAutoImprover – Autonomous Prompt Improvement Engine
    //
    //  Takes a raw prompt and produces concrete improved versions through
    //  multiple analysis passes: clarity, specificity, structure, token
    //  efficiency, instruction completeness, and anti-pattern removal.
    //  Each pass rewrites the prompt text and explains what changed and why.
    //  The engine scores before/after quality and produces a diff summary.
    // ────────────────────────────────────────────

    #region Enums

    /// <summary>Category of improvement applied to a prompt.</summary>
    public enum ImprovementCategory
    {
        /// <summary>Replace vague language with precise instructions.</summary>
        Specificity,
        /// <summary>Simplify wording for better model comprehension.</summary>
        Clarity,
        /// <summary>Add missing structural elements (role, format, constraints).</summary>
        Structure,
        /// <summary>Reduce token count without losing meaning.</summary>
        TokenEfficiency,
        /// <summary>Add missing instructions the model likely needs.</summary>
        Completeness,
        /// <summary>Remove known anti-patterns that degrade output quality.</summary>
        AntiPattern,
        /// <summary>Improve output format specification.</summary>
        FormatSpec,
        /// <summary>Add or improve examples for few-shot clarity.</summary>
        ExampleQuality,
        /// <summary>Strengthen guardrails and safety constraints.</summary>
        SafetyGuardrail
    }

    /// <summary>How aggressive the improvement should be.</summary>
    public enum ImprovementIntensity
    {
        /// <summary>Conservative: fix clear issues only.</summary>
        Light,
        /// <summary>Balanced: fix issues and improve where confident.</summary>
        Moderate,
        /// <summary>Aggressive: maximize quality even with significant rewrites.</summary>
        Deep
    }

    /// <summary>Dimension scored during quality assessment.</summary>
    public enum QualityDimension
    {
        /// <summary>How specific and unambiguous the instructions are.</summary>
        Specificity,
        /// <summary>How clearly the prompt communicates intent.</summary>
        Clarity,
        /// <summary>Whether role, task, format, and constraints are present.</summary>
        Structure,
        /// <summary>Token efficiency relative to information content.</summary>
        Efficiency,
        /// <summary>Whether all necessary instructions are included.</summary>
        Completeness,
        /// <summary>Absence of known anti-patterns.</summary>
        AntiPatternFree,
        /// <summary>Overall estimated effectiveness.</summary>
        OverallEffectiveness
    }

    #endregion

    #region Records

    /// <summary>A single improvement applied to the prompt.</summary>
    public record ImprovementAction
    {
        /// <summary>What category of improvement this is.</summary>
        [JsonPropertyName("category")]
        public ImprovementCategory Category { get; init; }

        /// <summary>Human-readable description of what was changed.</summary>
        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        /// <summary>The original text segment that was changed.</summary>
        [JsonPropertyName("before")]
        public string Before { get; init; } = "";

        /// <summary>The improved text segment.</summary>
        [JsonPropertyName("after")]
        public string After { get; init; } = "";

        /// <summary>Why this change improves the prompt.</summary>
        [JsonPropertyName("rationale")]
        public string Rationale { get; init; } = "";

        /// <summary>Confidence in this improvement (0.0–1.0).</summary>
        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }

    /// <summary>Quality score across multiple dimensions.</summary>
    public record QualityScore
    {
        /// <summary>Scores per dimension (0–100).</summary>
        [JsonPropertyName("dimensions")]
        public Dictionary<QualityDimension, int> Dimensions { get; init; } = new();

        /// <summary>Weighted overall score (0–100).</summary>
        [JsonPropertyName("overall")]
        public int Overall { get; init; }

        /// <summary>Letter grade (A+ through F).</summary>
        [JsonPropertyName("grade")]
        public string Grade { get; init; } = "C";
    }

    /// <summary>Complete result of an auto-improvement run.</summary>
    public record ImprovementResult
    {
        /// <summary>The original prompt text.</summary>
        [JsonPropertyName("original")]
        public string Original { get; init; } = "";

        /// <summary>The improved prompt text.</summary>
        [JsonPropertyName("improved")]
        public string Improved { get; init; } = "";

        /// <summary>Quality score of the original prompt.</summary>
        [JsonPropertyName("originalScore")]
        public QualityScore OriginalScore { get; init; } = new();

        /// <summary>Quality score of the improved prompt.</summary>
        [JsonPropertyName("improvedScore")]
        public QualityScore ImprovedScore { get; init; } = new();

        /// <summary>All improvements applied in order.</summary>
        [JsonPropertyName("improvements")]
        public List<ImprovementAction> Improvements { get; init; } = new();

        /// <summary>Token count of the original prompt (estimated).</summary>
        [JsonPropertyName("originalTokenEstimate")]
        public int OriginalTokenEstimate { get; init; }

        /// <summary>Token count of the improved prompt (estimated).</summary>
        [JsonPropertyName("improvedTokenEstimate")]
        public int ImprovedTokenEstimate { get; init; }

        /// <summary>Net score improvement (positive = better).</summary>
        [JsonPropertyName("scoreImprovement")]
        public int ScoreImprovement { get; init; }

        /// <summary>Improvement intensity used.</summary>
        [JsonPropertyName("intensity")]
        public ImprovementIntensity Intensity { get; init; }

        /// <summary>Categories that were improved.</summary>
        [JsonPropertyName("categoriesImproved")]
        public List<ImprovementCategory> CategoriesImproved { get; init; } = new();
    }

    /// <summary>Configuration for the auto-improver.</summary>
    public record ImproverConfig
    {
        /// <summary>How aggressively to improve.</summary>
        public ImprovementIntensity Intensity { get; init; } = ImprovementIntensity.Moderate;

        /// <summary>Categories to focus on (null = all).</summary>
        public HashSet<ImprovementCategory>? FocusCategories { get; init; }

        /// <summary>Minimum confidence threshold to apply an improvement (0.0–1.0).</summary>
        public double MinConfidence { get; init; } = 0.5;

        /// <summary>Maximum token budget for the improved prompt (0 = no limit).</summary>
        public int MaxTokenBudget { get; init; } = 0;

        /// <summary>Whether to preserve the exact original structure where possible.</summary>
        public bool PreserveStructure { get; init; } = false;
    }

    #endregion

    /// <summary>
    /// Autonomous prompt improvement engine. Analyzes a prompt across multiple
    /// quality dimensions and produces a concrete rewritten version with
    /// tracked changes and quality score comparisons.
    /// </summary>
    public class PromptAutoImprover
    {
        private readonly ImproverConfig _config;

        // ── Anti-patterns ──
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

        // ── Pre-compiled regexes for scoring / passes (eliminates per-call Regex allocations) ──
        private static readonly Regex RxDigits = new(@"\b\d+\b", RegexOptions.None, RegexTimeout);
        private static readonly Regex RxQuotedExamples = new(@"""[^""]+""", RegexOptions.None, RegexTimeout);
        private static readonly Regex RxImperativeStart = new(@"^(?:Analyze|Write|Create|Explain|List|Describe|Use|Include|Provide|Ensure|Generate|Return|Format)", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxPassiveVoice = new(@"\b(?:is|are|was|were|be|been|being)\s+\w+ed\b", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxMarkdownHeading = new(@"^#+\s", RegexOptions.Multiline, RegexTimeout);
        private static readonly Regex RxExcessiveNewlines = new(@"\n{3,}", RegexOptions.None, RegexTimeout);
        private static readonly Regex RxActionVerbs = new(@"\b(?:analyze|write|create|explain|summarize|compare|list|describe|evaluate|generate|translate|review|classify|extract|calculate|design|implement|optimize)\b", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxOutputGuidance = new(@"\b(?:format|output|respond|return|provide)\b", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxLengthGuidance = new(@"\b(?:\d+\s*(?:words?|sentences?|paragraphs?|lines?|pages?|tokens?)|brief|concise|detailed|comprehensive|short|long)\b", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxExamples = new(@"(?:example|e\.g\.|such as|for instance)", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxSentenceSplit = new(@"(?<=[.!?])\s+(?=[A-Z])", RegexOptions.None, RegexTimeout);
        private static readonly Regex RxNormalizeSpaces = new(@"\s+", RegexOptions.None, RegexTimeout);
        private static readonly Regex RxTrailingLineSpaces = new(@"[ \t]+\n", RegexOptions.None, RegexTimeout);
        private static readonly Regex RxExcessiveSpaces = new(@"[ \t]{2,}", RegexOptions.None, RegexTimeout);
        private static readonly Regex RxConjunction = new(@"\s+(and|but|however|additionally|furthermore|moreover)\s+", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxListRequest = new(@"\b(?:list|enumerate|give me|provide)\s+(?:the|all|some|a few)?\s*\w+", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxFormatSpec = new(@"(?:numbered|bulleted|bullet|markdown|json|csv|table|format)", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxGenerationIndicator = new(@"\b(?:write|create|generate|compose|draft|produce)\b", RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex RxHasGuardrails = new(@"\b(?:do not|don't|never|avoid|refrain|must not)\b", RegexOptions.IgnoreCase, RegexTimeout);

        private static readonly List<(string Name, Regex Pattern, string Replacement, string Rationale, ImprovementCategory Category)> AntiPatterns = new()
        {
            ("Politeness filler", new Regex(@"\b(please|kindly|if you could|would you mind|I'd appreciate if you)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "", "Politeness tokens consume budget without improving output quality.", ImprovementCategory.AntiPattern),

            ("Begging/pleading", new Regex(@"\b(I really need you to|it's very important that you|you must absolutely|please please)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "", "Emotional appeals don't improve model output; direct instructions do.", ImprovementCategory.AntiPattern),

            ("Meta-commentary", new Regex(@"(?:^|\n)\s*(?:I want you to|I need you to|I'm looking for you to|What I need is for you to)\s*", RegexOptions.IgnoreCase | RegexOptions.Multiline, RegexTimeout),
             "", "Remove meta-commentary; state instructions directly.", ImprovementCategory.AntiPattern),

            ("Unnecessary hedging", new Regex(@"\b(maybe you could|perhaps try to|if possible,?\s*(?:try to|attempt to))\b", RegexOptions.IgnoreCase, RegexTimeout),
             "", "Hedging weakens instructions. State what you want directly.", ImprovementCategory.AntiPattern),

            ("Repetitive emphasis", new Regex(@"(!{2,})", RegexOptions.None, RegexTimeout),
             ".", "Multiple exclamation marks don't strengthen instructions.", ImprovementCategory.AntiPattern),
        };

        // ── Vague phrases → specific replacements ──
        private static readonly List<(Regex Pattern, string Replacement, string Rationale)> VagueToSpecific = new()
        {
            (new Regex(@"\bgood (?:response|answer|output)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "accurate, well-structured response", "Replace vague 'good' with measurable qualities."),

            (new Regex(@"\bbe (?:detailed|thorough)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "include specific examples, data points, and step-by-step reasoning", "Specify what 'detailed' means concretely."),

            (new Regex(@"\bkeep it (?:short|brief|concise)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "limit response to 2-3 paragraphs", "Give a concrete length constraint."),

            (new Regex(@"\bwrite (?:something|a thing) about\b", RegexOptions.IgnoreCase, RegexTimeout),
             "write an analysis of", "Replace vague 'something about' with a specific task."),

            (new Regex(@"\bmake (?:it )?(?:better|improve it)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "revise for clarity, conciseness, and logical flow", "Define what 'better' means."),

            (new Regex(@"\bdo (?:your |the )?best\b", RegexOptions.IgnoreCase, RegexTimeout),
             "optimize for accuracy and completeness", "Replace motivational filler with evaluation criteria."),

            (new Regex(@"\bin (?:a|an) (?:nice|good) (?:way|format|style)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "using clear headings, bullet points, and logical grouping", "Specify the desired format explicitly."),
        };

        // ── Structural elements to check for ──
        private static readonly List<(string Name, Regex Detector, string Template, double Weight)> StructuralElements = new()
        {
            ("Role/persona", new Regex(@"(?:you are|act as|role:|persona:|as a)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "\nYou are a knowledgeable assistant specialized in this domain.\n", 0.15),

            ("Output format", new Regex(@"(?:format:|output:|respond (?:in|with|using)|use (?:the following )?format)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "\nFormat your response as: [structured output specification]\n", 0.20),

            ("Constraints", new Regex(@"(?:do not|don't|never|avoid|constraint|limit|must not|refrain)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "", 0.10), // Only flag absence, don't auto-add constraints

            ("Task statement", new Regex(@"(?:your (?:task|job|goal) is|instructions?:|task:|objective:)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "", 0.25),

            ("Context/background", new Regex(@"(?:context:|background:|given (?:that|the following)|here is (?:the|some) (?:context|background))\b", RegexOptions.IgnoreCase, RegexTimeout),
             "", 0.10),

            ("Examples", new Regex(@"(?:example:|for example|e\.g\.|such as|here (?:is|are) (?:an? )?example)\b", RegexOptions.IgnoreCase, RegexTimeout),
             "", 0.20),
        };

        /// <summary>
        /// Creates a new PromptAutoImprover with the specified configuration.
        /// </summary>
        /// <param name="config">Configuration settings. Uses defaults if null.</param>
        public PromptAutoImprover(ImproverConfig? config = null)
        {
            _config = config ?? new ImproverConfig();
        }

        /// <summary>
        /// Analyzes and improves the given prompt text.
        /// </summary>
        /// <param name="prompt">The prompt text to improve.</param>
        /// <returns>Complete improvement result with before/after comparison.</returns>
        /// <exception cref="ArgumentException">If prompt is null or whitespace.</exception>
        public ImprovementResult Improve(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var originalScore = ScoreQuality(prompt);
            var improvements = new List<ImprovementAction>();
            var working = prompt;

            // Pass 1: Remove anti-patterns
            if (ShouldRunCategory(ImprovementCategory.AntiPattern))
                working = RunAntiPatternPass(working, improvements);

            // Pass 2: Improve specificity (vague → precise)
            if (ShouldRunCategory(ImprovementCategory.Specificity))
                working = RunSpecificityPass(working, improvements);

            // Pass 3: Improve clarity (simplify complex sentences)
            if (ShouldRunCategory(ImprovementCategory.Clarity))
                working = RunClarityPass(working, improvements);

            // Pass 4: Add missing structural elements
            if (ShouldRunCategory(ImprovementCategory.Structure))
                working = RunStructurePass(working, improvements);

            // Pass 5: Improve format specifications
            if (ShouldRunCategory(ImprovementCategory.FormatSpec))
                working = RunFormatSpecPass(working, improvements);

            // Pass 6: Add safety guardrails if missing
            if (ShouldRunCategory(ImprovementCategory.SafetyGuardrail))
                working = RunSafetyPass(working, improvements);

            // Pass 7: Token efficiency (only in Moderate/Deep)
            if (_config.Intensity != ImprovementIntensity.Light &&
                ShouldRunCategory(ImprovementCategory.TokenEfficiency))
                working = RunTokenEfficiencyPass(working, improvements);

            // Pass 8: Completeness check (only in Deep)
            if (_config.Intensity == ImprovementIntensity.Deep &&
                ShouldRunCategory(ImprovementCategory.Completeness))
                working = RunCompletenessPass(working, improvements);

            // Normalize whitespace in final output
            working = NormalizeWhitespace(working);

            // Apply token budget if configured
            if (_config.MaxTokenBudget > 0)
                working = EnforceTokenBudget(working, _config.MaxTokenBudget);

            var improvedScore = ScoreQuality(working);

            // Only apply changes if they actually improved the score
            var finalText = improvedScore.Overall >= originalScore.Overall ? working : prompt;
            var finalScore = improvedScore.Overall >= originalScore.Overall ? improvedScore : originalScore;

            return new ImprovementResult
            {
                Original = prompt,
                Improved = finalText,
                OriginalScore = originalScore,
                ImprovedScore = finalScore,
                Improvements = improvements,
                OriginalTokenEstimate = EstimateTokens(prompt),
                ImprovedTokenEstimate = EstimateTokens(finalText),
                ScoreImprovement = finalScore.Overall - originalScore.Overall,
                Intensity = _config.Intensity,
                CategoriesImproved = improvements
                    .Select(i => i.Category)
                    .Distinct()
                    .ToList()
            };
        }

        /// <summary>
        /// Scores prompt quality across multiple dimensions.
        /// </summary>
        /// <param name="prompt">The prompt text to evaluate.</param>
        /// <returns>A quality score breakdown.</returns>
        public QualityScore ScoreQuality(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new QualityScore { Overall = 0, Grade = "F" };

            var dims = new Dictionary<QualityDimension, int>();

            dims[QualityDimension.Specificity] = ScoreSpecificity(prompt);
            dims[QualityDimension.Clarity] = ScoreClarity(prompt);
            dims[QualityDimension.Structure] = ScoreStructure(prompt);
            dims[QualityDimension.Efficiency] = ScoreEfficiency(prompt);
            dims[QualityDimension.Completeness] = ScoreCompleteness(prompt);
            dims[QualityDimension.AntiPatternFree] = ScoreAntiPatternFree(prompt);

            // Weighted overall
            var weights = new Dictionary<QualityDimension, double>
            {
                [QualityDimension.Specificity] = 0.20,
                [QualityDimension.Clarity] = 0.20,
                [QualityDimension.Structure] = 0.20,
                [QualityDimension.Efficiency] = 0.10,
                [QualityDimension.Completeness] = 0.15,
                [QualityDimension.AntiPatternFree] = 0.15,
            };

            double overall = 0;
            foreach (var kvp in weights)
            {
                if (dims.TryGetValue(kvp.Key, out var score))
                    overall += score * kvp.Value;
            }

            var overallInt = (int)Math.Round(overall);
            dims[QualityDimension.OverallEffectiveness] = overallInt;

            return new QualityScore
            {
                Dimensions = dims,
                Overall = overallInt,
                Grade = ScoreToGrade(overallInt)
            };
        }

        /// <summary>
        /// Generates an HTML report for an improvement result.
        /// </summary>
        /// <param name="result">The improvement result to visualize.</param>
        /// <returns>Complete HTML document string.</returns>
        public string GenerateHtmlReport(ImprovementResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
            sb.AppendLine("<title>Prompt Auto-Improvement Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#e2e8f0;padding:2rem}");
            sb.AppendLine(".container{max-width:960px;margin:0 auto}");
            sb.AppendLine("h1{font-size:1.8rem;margin-bottom:0.5rem;background:linear-gradient(135deg,#60a5fa,#a78bfa);-webkit-background-clip:text;-webkit-text-fill-color:transparent}");
            sb.AppendLine(".subtitle{color:#94a3b8;margin-bottom:2rem}");
            sb.AppendLine(".card{background:#1e293b;border-radius:12px;padding:1.5rem;margin-bottom:1.5rem;border:1px solid #334155}");
            sb.AppendLine(".score-grid{display:grid;grid-template-columns:1fr 1fr;gap:1.5rem;margin-bottom:1.5rem}");
            sb.AppendLine(".score-box{text-align:center;padding:1.5rem}");
            sb.AppendLine(".score-big{font-size:3rem;font-weight:700}");
            sb.AppendLine(".score-original .score-big{color:#f87171}");
            sb.AppendLine(".score-improved .score-big{color:#34d399}");
            sb.AppendLine(".grade{font-size:1.5rem;font-weight:600;margin-left:0.5rem;opacity:0.8}");
            sb.AppendLine(".dim-bar{display:flex;align-items:center;margin:0.4rem 0}");
            sb.AppendLine(".dim-label{width:140px;font-size:0.85rem;color:#94a3b8}");
            sb.AppendLine(".dim-track{flex:1;height:8px;background:#334155;border-radius:4px;overflow:hidden;position:relative}");
            sb.AppendLine(".dim-fill-orig{height:100%;border-radius:4px;position:absolute;top:0;left:0;opacity:0.4}");
            sb.AppendLine(".dim-fill-new{height:100%;border-radius:4px;position:absolute;top:0;left:0}");
            sb.AppendLine(".dim-val{width:45px;text-align:right;font-size:0.85rem;font-weight:600;margin-left:0.5rem}");
            sb.AppendLine(".improvement{border-left:3px solid #60a5fa;padding:0.75rem 1rem;margin:0.75rem 0;background:#1a2332;border-radius:0 8px 8px 0}");
            sb.AppendLine(".imp-cat{font-size:0.75rem;font-weight:600;text-transform:uppercase;color:#60a5fa;margin-bottom:0.3rem}");
            sb.AppendLine(".imp-desc{font-size:0.9rem;margin-bottom:0.4rem}");
            sb.AppendLine(".imp-rationale{font-size:0.8rem;color:#94a3b8;font-style:italic}");
            sb.AppendLine(".diff{display:grid;grid-template-columns:1fr 1fr;gap:0.5rem;margin-top:0.5rem;font-size:0.8rem}");
            sb.AppendLine(".diff-before{background:#3b1a1a;padding:0.5rem;border-radius:6px;color:#fca5a5}");
            sb.AppendLine(".diff-after{background:#1a3b2a;padding:0.5rem;border-radius:6px;color:#86efac}");
            sb.AppendLine(".prompt-box{background:#0f172a;border:1px solid #334155;border-radius:8px;padding:1rem;font-family:'Fira Code',monospace;font-size:0.85rem;white-space:pre-wrap;line-height:1.6;max-height:300px;overflow-y:auto}");
            sb.AppendLine(".tokens{display:flex;justify-content:center;gap:2rem;margin-top:1rem;font-size:0.9rem;color:#94a3b8}");
            sb.AppendLine(".token-change{color:#34d399;font-weight:600}");
            sb.AppendLine(".badge{display:inline-block;padding:0.15rem 0.5rem;border-radius:999px;font-size:0.7rem;font-weight:600;margin:0.1rem}");
            sb.AppendLine(".badge-spec{background:#1e3a5f;color:#60a5fa}");
            sb.AppendLine(".badge-clar{background:#1e3a5f;color:#38bdf8}");
            sb.AppendLine(".badge-struct{background:#2d1f5e;color:#a78bfa}");
            sb.AppendLine(".badge-eff{background:#1a3b2a;color:#34d399}");
            sb.AppendLine(".badge-anti{background:#3b1a1a;color:#fca5a5}");
            sb.AppendLine(".badge-safe{background:#3b2f1a;color:#fbbf24}");
            sb.AppendLine(".confidence{float:right;font-size:0.75rem;color:#64748b}");
            sb.AppendLine("</style></head><body><div class=\"container\">");

            sb.AppendLine($"<h1>⚡ Prompt Auto-Improvement Report</h1>");
            sb.AppendLine($"<p class=\"subtitle\">Intensity: {result.Intensity} · {result.Improvements.Count} improvements applied · {result.CategoriesImproved.Count} categories</p>");

            // Score comparison
            sb.AppendLine("<div class=\"score-grid\">");
            sb.AppendLine($"<div class=\"card score-box score-original\"><div>Original</div><div class=\"score-big\">{result.OriginalScore.Overall}<span class=\"grade\">{result.OriginalScore.Grade}</span></div></div>");
            sb.AppendLine($"<div class=\"card score-box score-improved\"><div>Improved</div><div class=\"score-big\">{result.ImprovedScore.Overall}<span class=\"grade\">{result.ImprovedScore.Grade}</span></div></div>");
            sb.AppendLine("</div>");

            // Dimension comparison bars
            sb.AppendLine("<div class=\"card\"><h3 style=\"margin-bottom:1rem\">Quality Dimensions</h3>");
            foreach (QualityDimension dim in Enum.GetValues(typeof(QualityDimension)))
            {
                if (dim == QualityDimension.OverallEffectiveness) continue;
                var origVal = result.OriginalScore.Dimensions.GetValueOrDefault(dim, 0);
                var newVal = result.ImprovedScore.Dimensions.GetValueOrDefault(dim, 0);
                var color = DimensionColor(dim);
                sb.AppendLine("<div class=\"dim-bar\">");
                sb.AppendLine($"  <span class=\"dim-label\">{dim}</span>");
                sb.AppendLine($"  <div class=\"dim-track\"><div class=\"dim-fill-orig\" style=\"width:{origVal}%;background:{color}\"></div><div class=\"dim-fill-new\" style=\"width:{newVal}%;background:{color}\"></div></div>");
                sb.AppendLine($"  <span class=\"dim-val\" style=\"color:{color}\">{newVal}</span>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");

            // Improvements list
            if (result.Improvements.Count > 0)
            {
                sb.AppendLine("<div class=\"card\"><h3 style=\"margin-bottom:1rem\">Improvements Applied</h3>");
                foreach (var imp in result.Improvements)
                {
                    var badgeClass = imp.Category switch
                    {
                        ImprovementCategory.Specificity => "badge-spec",
                        ImprovementCategory.Clarity => "badge-clar",
                        ImprovementCategory.Structure => "badge-struct",
                        ImprovementCategory.TokenEfficiency => "badge-eff",
                        ImprovementCategory.AntiPattern => "badge-anti",
                        ImprovementCategory.SafetyGuardrail => "badge-safe",
                        _ => "badge-spec"
                    };
                    sb.AppendLine("<div class=\"improvement\">");
                    sb.AppendLine($"  <div class=\"imp-cat\"><span class=\"badge {badgeClass}\">{imp.Category}</span><span class=\"confidence\">{imp.Confidence:P0}</span></div>");
                    sb.AppendLine($"  <div class=\"imp-desc\">{Escape(imp.Description)}</div>");
                    if (!string.IsNullOrEmpty(imp.Before) || !string.IsNullOrEmpty(imp.After))
                    {
                        sb.AppendLine("  <div class=\"diff\">");
                        if (!string.IsNullOrEmpty(imp.Before))
                            sb.AppendLine($"    <div class=\"diff-before\">− {Escape(imp.Before)}</div>");
                        else
                            sb.AppendLine("    <div class=\"diff-before\">−</div>");
                        if (!string.IsNullOrEmpty(imp.After))
                            sb.AppendLine($"    <div class=\"diff-after\">+ {Escape(imp.After)}</div>");
                        else
                            sb.AppendLine("    <div class=\"diff-after\">+ (removed)</div>");
                    }
                    sb.AppendLine($"  <div class=\"imp-rationale\">{Escape(imp.Rationale)}</div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            // Prompt comparison
            sb.AppendLine("<div class=\"card\"><h3 style=\"margin-bottom:1rem\">Final Prompt</h3>");
            sb.AppendLine($"<div class=\"prompt-box\">{Escape(result.Improved)}</div>");
            sb.AppendLine("<div class=\"tokens\">");
            sb.AppendLine($"  <span>Original: ~{result.OriginalTokenEstimate} tokens</span>");
            var tokenDiff = result.ImprovedTokenEstimate - result.OriginalTokenEstimate;
            var tokenSign = tokenDiff > 0 ? "+" : "";
            sb.AppendLine($"  <span>Improved: ~{result.ImprovedTokenEstimate} tokens <span class=\"token-change\">({tokenSign}{tokenDiff})</span></span>");
            sb.AppendLine("</div></div>");

            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the improvement result to JSON.
        /// </summary>
        public string ToJson(ImprovementResult result)
        {
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            });
        }

        // ═══════════════════════════════════════
        //  Improvement Passes
        // ═══════════════════════════════════════

        private string RunAntiPatternPass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;
            foreach (var (name, pattern, replacement, rationale, category) in AntiPatterns)
            {
                var matches = pattern.Matches(working);
                if (matches.Count == 0) continue;

                var confidence = _config.Intensity == ImprovementIntensity.Light ? 0.6 : 0.85;
                if (confidence < _config.MinConfidence) continue;

                var matchText = matches[0].Value;
                var before = working;
                working = pattern.Replace(working, replacement);

                if (working != before)
                {
                    improvements.Add(new ImprovementAction
                    {
                        Category = category,
                        Description = $"Removed {name.ToLower()}: \"{matchText}\"",
                        Before = matchText,
                        After = string.IsNullOrEmpty(replacement) ? "(removed)" : replacement,
                        Rationale = rationale,
                        Confidence = confidence
                    });
                }
            }
            return working;
        }

        private string RunSpecificityPass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;
            foreach (var (pattern, replacement, rationale) in VagueToSpecific)
            {
                var match = pattern.Match(working);
                if (!match.Success) continue;

                var confidence = 0.7;
                if (confidence < _config.MinConfidence) continue;

                var before = match.Value;
                working = pattern.Replace(working, replacement, 1); // Replace only first occurrence

                improvements.Add(new ImprovementAction
                {
                    Category = ImprovementCategory.Specificity,
                    Description = $"Replaced vague language with specific instruction",
                    Before = before,
                    After = replacement,
                    Rationale = rationale,
                    Confidence = confidence
                });
            }
            return working;
        }

        private string RunClarityPass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;

            // Detect run-on sentences (very long without proper punctuation)
            var sentences = SplitSentences(working);
            var longSentences = sentences.Where(s => s.Split(' ').Length > 40).ToList();

            foreach (var longSentence in longSentences)
            {
                if (_config.Intensity == ImprovementIntensity.Light) break;

                var confidence = 0.6;
                if (confidence < _config.MinConfidence) continue;

                // Split at conjunctions
                var splitResult = RxConjunction.Replace(longSentence, m =>
                    $".\n{char.ToUpper(m.Groups[1].Value[0])}{m.Groups[1].Value.Substring(1)} ");

                if (splitResult != longSentence)
                {
                    working = working.Replace(longSentence, splitResult);
                    improvements.Add(new ImprovementAction
                    {
                        Category = ImprovementCategory.Clarity,
                        Description = "Split long run-on sentence into shorter, clearer statements",
                        Before = longSentence.Length > 100 ? longSentence.Substring(0, 100) + "..." : longSentence,
                        After = splitResult.Length > 100 ? splitResult.Substring(0, 100) + "..." : splitResult,
                        Rationale = "Shorter sentences are easier for models to parse and follow accurately.",
                        Confidence = confidence
                    });
                }
            }

            // Detect passive voice and suggest active
            var passiveMatches = RxPassiveVoice.Matches(working);
            if (passiveMatches.Count > 2 && _config.Intensity != ImprovementIntensity.Light)
            {
                improvements.Add(new ImprovementAction
                {
                    Category = ImprovementCategory.Clarity,
                    Description = $"Prompt uses passive voice {passiveMatches.Count} times — consider active voice for directness",
                    Before = passiveMatches[0].Value,
                    After = "(use active voice equivalent)",
                    Rationale = "Active voice instructions are more direct and reduce ambiguity.",
                    Confidence = 0.5
                });
            }

            return working;
        }

        private string RunStructurePass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;
            var missingElements = new List<string>();

            foreach (var (name, detector, template, weight) in StructuralElements)
            {
                if (detector.IsMatch(working)) continue;
                missingElements.Add(name);

                if (!string.IsNullOrEmpty(template) && _config.Intensity != ImprovementIntensity.Light)
                {
                    var confidence = weight; // Use the element's weight as confidence
                    if (confidence < _config.MinConfidence) continue;

                    // For role/persona, prepend it
                    if (name == "Role/persona" && !_config.PreserveStructure)
                    {
                        working = template.Trim() + "\n\n" + working;
                        improvements.Add(new ImprovementAction
                        {
                            Category = ImprovementCategory.Structure,
                            Description = $"Added missing {name} definition",
                            Before = "(not present)",
                            After = template.Trim(),
                            Rationale = $"A {name.ToLower()} helps the model understand the expected behavior and expertise level.",
                            Confidence = confidence
                        });
                    }
                    // For output format, append it
                    else if (name == "Output format" && !_config.PreserveStructure)
                    {
                        working = working.TrimEnd() + "\n\n" + template.Trim();
                        improvements.Add(new ImprovementAction
                        {
                            Category = ImprovementCategory.FormatSpec,
                            Description = $"Added missing {name} specification",
                            Before = "(not present)",
                            After = template.Trim(),
                            Rationale = "Specifying output format reduces ambiguity and increases response consistency.",
                            Confidence = confidence
                        });
                    }
                }
            }

            if (missingElements.Count > 0 && !improvements.Any(i => i.Category == ImprovementCategory.Structure))
            {
                improvements.Add(new ImprovementAction
                {
                    Category = ImprovementCategory.Completeness,
                    Description = $"Missing structural elements: {string.Join(", ", missingElements)}",
                    Before = "(not present)",
                    After = "(consider adding these elements)",
                    Rationale = "Well-structured prompts with role, task, format, and constraints produce more consistent results.",
                    Confidence = 0.5
                });
            }

            return working;
        }

        private string RunFormatSpecPass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;

            // Detect if user asks for a list but doesn't specify format
            if (RxListRequest.IsMatch(working) && !RxFormatSpec.IsMatch(working) && _config.Intensity != ImprovementIntensity.Light)
            {
                var confidence = 0.6;
                if (confidence >= _config.MinConfidence)
                {
                    var addendum = "\nPresent items as a numbered list with brief descriptions.";
                    working = working.TrimEnd() + addendum;
                    improvements.Add(new ImprovementAction
                    {
                        Category = ImprovementCategory.FormatSpec,
                        Description = "Added list format specification for enumeration request",
                        Before = "(no format specified for list)",
                        After = addendum.Trim(),
                        Rationale = "Specifying list format ensures consistent, parseable output.",
                        Confidence = confidence
                    });
                }
            }

            return working;
        }

        private string RunSafetyPass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;

            // Only add guardrails in Deep mode when generating content
            if (_config.Intensity != ImprovementIntensity.Deep) return working;

            if (RxGenerationIndicator.IsMatch(working) && !RxHasGuardrails.IsMatch(working))
            {
                var confidence = 0.55;
                if (confidence >= _config.MinConfidence)
                {
                    var guardrail = "\n\nIf you are unsure about any aspect, state your uncertainty rather than guessing.";
                    working = working.TrimEnd() + guardrail;
                    improvements.Add(new ImprovementAction
                    {
                        Category = ImprovementCategory.SafetyGuardrail,
                        Description = "Added uncertainty disclosure guardrail",
                        Before = "(no guardrails present)",
                        After = guardrail.Trim(),
                        Rationale = "Guardrails improve output reliability by encouraging the model to flag uncertainty.",
                        Confidence = confidence
                    });
                }
            }

            return working;
        }

        private string RunTokenEfficiencyPass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;

            // Remove redundant whitespace and normalize
            var beforeLen = working.Length;
            working = RxExcessiveNewlines.Replace(working, "\n\n");
            working = RxExcessiveSpaces.Replace(working, " ");

            if (working.Length < beforeLen)
            {
                improvements.Add(new ImprovementAction
                {
                    Category = ImprovementCategory.TokenEfficiency,
                    Description = $"Removed {beforeLen - working.Length} redundant whitespace characters",
                    Before = $"({beforeLen} chars)",
                    After = $"({working.Length} chars)",
                    Rationale = "Unnecessary whitespace consumes tokens without adding information.",
                    Confidence = 0.95
                });
            }

            // Detect repeated instructions
            var sentences = SplitSentences(working);
            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            foreach (var s in sentences)
            {
                var normalized = NormalizeSentence(s);
                if (normalized.Length < 10) continue;
                if (!seen.Add(normalized))
                    duplicates.Add(s);
            }

            if (duplicates.Count > 0)
            {
                foreach (var dup in duplicates)
                {
                    working = RemoveFirstOccurrence(working, dup);
                }
                improvements.Add(new ImprovementAction
                {
                    Category = ImprovementCategory.TokenEfficiency,
                    Description = $"Removed {duplicates.Count} duplicate instruction(s)",
                    Before = duplicates.First(),
                    After = "(removed duplicate)",
                    Rationale = "Duplicate instructions waste tokens and can confuse priority.",
                    Confidence = 0.9
                });
            }

            return working;
        }

        private string RunCompletenessPass(string prompt, List<ImprovementAction> improvements)
        {
            var working = prompt;

            // Check if there's a clear task/action verb
            if (!RxActionVerbs.IsMatch(working))
            {
                improvements.Add(new ImprovementAction
                {
                    Category = ImprovementCategory.Completeness,
                    Description = "No clear action verb detected — prompt may be ambiguous about what to do",
                    Before = "(no action verb found)",
                    After = "(consider starting with a clear action verb: analyze, write, explain, etc.)",
                    Rationale = "A clear action verb anchors the task and reduces interpretation ambiguity.",
                    Confidence = 0.65
                });
            }

            // Check for output length guidance
            if (!RxLengthGuidance.IsMatch(working) && working.Length > 50)
            {
                improvements.Add(new ImprovementAction
                {
                    Category = ImprovementCategory.Completeness,
                    Description = "No output length guidance provided",
                    Before = "(no length specification)",
                    After = "(consider specifying expected response length)",
                    Rationale = "Length guidance helps the model calibrate response detail and prevents overly verbose or terse outputs.",
                    Confidence = 0.5
                });
            }

            return working;
        }

        // ═══════════════════════════════════════
        //  Scoring Functions
        // ═══════════════════════════════════════

        private int ScoreSpecificity(string prompt)
        {
            var score = 50; // Base

            // Positive: contains numbers, measurements, specific terms
            score += Math.Min(20, RxDigits.Matches(prompt).Count * 5);

            // Positive: contains quoted examples or specific names
            score += Math.Min(15, RxQuotedExamples.Matches(prompt).Count * 5);

            // Negative: vague terms
            foreach (var (pattern, _, _) in VagueToSpecific)
            {
                if (pattern.IsMatch(prompt)) score -= 8;
            }

            return Math.Clamp(score, 0, 100);
        }

        private int ScoreClarity(string prompt)
        {
            var score = 60;
            var sentences = SplitSentences(prompt);
            if (sentences.Count == 0) return 30;

            // Average sentence length
            var avgWords = sentences.Average(s => s.Split(' ').Length);
            if (avgWords < 25) score += 15;
            else if (avgWords > 40) score -= 15;
            else if (avgWords > 30) score -= 5;

            // Consistent use of imperative mood (good for instructions)
            var imperativeCount = sentences.Count(s => RxImperativeStart.IsMatch(s.TrimStart()));
            if (sentences.Count > 0 && (double)imperativeCount / sentences.Count > 0.3)
                score += 10;

            // Penalty for passive voice
            var passiveCount = RxPassiveVoice.Matches(prompt).Count;
            score -= Math.Min(15, passiveCount * 3);

            return Math.Clamp(score, 0, 100);
        }

        private int ScoreStructure(string prompt)
        {
            var score = 20; // Start low, reward presence of elements
            foreach (var (name, detector, _, weight) in StructuralElements)
            {
                if (detector.IsMatch(prompt))
                    score += (int)(weight * 100);
            }

            // Bonus for markdown structure
            if (RxMarkdownHeading.IsMatch(prompt)) score += 5;
            if (prompt.Contains('\n')) score += 5; // Multi-line is structured

            return Math.Clamp(score, 0, 100);
        }

        private int ScoreEfficiency(string prompt)
        {
            var score = 70;

            // Penalty for excessive whitespace
            score -= Math.Min(15, RxExcessiveNewlines.Matches(prompt).Count * 5);

            // Penalty for duplicate sentences
            var sentences = SplitSentences(prompt);
            var unique = sentences.Select(NormalizeSentence).Distinct().Count();
            if (sentences.Count > 0)
            {
                var dupRatio = 1.0 - ((double)unique / sentences.Count);
                score -= (int)(dupRatio * 30);
            }

            // Penalty for overly long prompts with little structure
            var tokens = EstimateTokens(prompt);
            if (tokens > 500 && !prompt.Contains('\n'))
                score -= 15;

            return Math.Clamp(score, 0, 100);
        }

        private int ScoreCompleteness(string prompt)
        {
            var score = 40;

            // Has action verb
            if (RxActionVerbs.IsMatch(prompt)) score += 20;

            // Has output guidance
            if (RxOutputGuidance.IsMatch(prompt)) score += 15;

            // Has length guidance
            if (RxLengthGuidance.IsMatch(prompt)) score += 15;

            // Has examples
            if (RxExamples.IsMatch(prompt)) score += 10;

            return Math.Clamp(score, 0, 100);
        }

        private int ScoreAntiPatternFree(string prompt)
        {
            var score = 100;
            foreach (var (_, pattern, _, _, _) in AntiPatterns)
            {
                if (pattern.IsMatch(prompt)) score -= 15;
            }
            return Math.Clamp(score, 0, 100);
        }

        // ═══════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════

        private bool ShouldRunCategory(ImprovementCategory category)
        {
            return _config.FocusCategories == null || _config.FocusCategories.Contains(category);
        }

        // Delegates to the canonical char-based estimator in TextAnalysisHelpers
        // (issue #191: converge duplicated ~4-chars-per-token implementations).
        private static int EstimateTokens(string text) =>
            TextAnalysisHelpers.EstimateTokens(text);

        private static List<string> SplitSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            return RxSentenceSplit.Split(text)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static string NormalizeSentence(string sentence)
        {
            return RxNormalizeSpaces.Replace(sentence.ToLowerInvariant().Trim(), " ");
        }

        private static string RemoveFirstOccurrence(string text, string sentence)
        {
            var idx = text.IndexOf(sentence, StringComparison.Ordinal);
            if (idx < 0) return text;
            return text.Remove(idx, sentence.Length).Trim();
        }

        private static string NormalizeWhitespace(string text)
        {
            // Collapse 3+ newlines to 2, trim trailing whitespace per line
            text = RxExcessiveNewlines.Replace(text, "\n\n");
            text = RxTrailingLineSpaces.Replace(text, "\n");
            return text.Trim();
        }

        private static string EnforceTokenBudget(string text, int maxTokens)
        {
            var estimated = EstimateTokens(text);
            if (estimated <= maxTokens) return text;

            // Truncate to approximate token budget (4 chars per token)
            var maxChars = maxTokens * 4;
            if (text.Length > maxChars)
            {
                text = text.Substring(0, maxChars);
                // Try to end at a sentence boundary
                var lastPeriod = text.LastIndexOf('.');
                if (lastPeriod > maxChars * 0.7)
                    text = text.Substring(0, lastPeriod + 1);
            }
            return text;
        }

        private static string ScoreToGrade(int score) => score switch
        {
            >= 95 => "A+",
            >= 90 => "A",
            >= 85 => "A-",
            >= 80 => "B+",
            >= 75 => "B",
            >= 70 => "B-",
            >= 65 => "C+",
            >= 60 => "C",
            >= 55 => "C-",
            >= 50 => "D+",
            >= 45 => "D",
            >= 40 => "D-",
            _ => "F"
        };

        private static string DimensionColor(QualityDimension dim) => dim switch
        {
            QualityDimension.Specificity => "#60a5fa",
            QualityDimension.Clarity => "#38bdf8",
            QualityDimension.Structure => "#a78bfa",
            QualityDimension.Efficiency => "#34d399",
            QualityDimension.Completeness => "#fbbf24",
            QualityDimension.AntiPatternFree => "#f87171",
            _ => "#94a3b8"
        };

        private static string Escape(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
