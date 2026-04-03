namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Categories of bias that can be detected in prompts.
    /// </summary>
    public enum BiasCategory
    {
        /// <summary>Gender-related assumptions or stereotypes.</summary>
        Gender,
        /// <summary>Age-related assumptions or stereotypes.</summary>
        Age,
        /// <summary>Cultural or ethnic assumptions.</summary>
        Cultural,
        /// <summary>Confirmation bias — leading or loaded questions.</summary>
        Confirmation,
        /// <summary>Anchoring bias — over-reliance on specific values or examples.</summary>
        Anchoring,
        /// <summary>Authority bias — appeals to authority without justification.</summary>
        Authority,
        /// <summary>Framing bias — asymmetric positive/negative framing.</summary>
        Framing,
        /// <summary>Exclusion bias — language that excludes groups.</summary>
        Exclusion,
        /// <summary>Ability-related assumptions.</summary>
        Ability,
        /// <summary>Socioeconomic assumptions.</summary>
        Socioeconomic
    }

    /// <summary>
    /// Severity level for a detected bias finding.
    /// </summary>
    public enum BiasSeverity
    {
        /// <summary>Minor issue — worth noting but not critical.</summary>
        Low,
        /// <summary>Moderate concern — should be addressed.</summary>
        Medium,
        /// <summary>Significant bias — must be fixed before use.</summary>
        High
    }

    /// <summary>
    /// A single bias finding detected in a prompt.
    /// </summary>
    public class BiasFinding
    {
        /// <summary>Gets the bias category.</summary>
        public BiasCategory Category { get; internal set; }

        /// <summary>Gets the severity level.</summary>
        public BiasSeverity Severity { get; internal set; }

        /// <summary>Gets the matched text that triggered the finding.</summary>
        public string MatchedText { get; internal set; } = string.Empty;

        /// <summary>Gets a human-readable description of the bias.</summary>
        public string Description { get; internal set; } = string.Empty;

        /// <summary>Gets a suggested neutral alternative.</summary>
        public string Suggestion { get; internal set; } = string.Empty;

        /// <summary>Gets the character position of the match in the original text.</summary>
        public int Position { get; internal set; }
    }

    /// <summary>
    /// Result of a bias analysis on a prompt.
    /// </summary>
    public class BiasReport
    {
        /// <summary>Gets the original prompt text.</summary>
        public string OriginalText { get; internal set; } = string.Empty;

        /// <summary>Gets the list of detected bias findings.</summary>
        public List<BiasFinding> Findings { get; internal set; } = new List<BiasFinding>();

        /// <summary>Gets the overall bias score (0.0 = no bias, 1.0 = highly biased).</summary>
        public double BiasScore { get; internal set; }

        /// <summary>Gets the debiased version of the prompt with suggestions applied.</summary>
        public string DebiasedText { get; internal set; } = string.Empty;

        /// <summary>Gets a per-category breakdown of findings.</summary>
        public Dictionary<BiasCategory, int> CategoryBreakdown =>
            Findings.GroupBy(f => f.Category)
                    .ToDictionary(g => g.Key, g => g.Count());

        /// <summary>Returns true if no bias was detected.</summary>
        public bool IsClean => Findings.Count == 0;

        /// <summary>
        /// Renders the report as a human-readable string.
        /// </summary>
        public string Render()
        {
            if (IsClean)
                return "✅ No bias detected. Prompt looks clean.";

            var lines = new List<string>
            {
                $"Bias Report — Score: {BiasScore:F2}/1.00 ({Findings.Count} finding(s))",
                new string('─', 60)
            };

            foreach (var finding in Findings.OrderByDescending(f => f.Severity))
            {
                var icon = finding.Severity == BiasSeverity.High ? "🔴" :
                           finding.Severity == BiasSeverity.Medium ? "🟡" : "🔵";
                lines.Add($"{icon} [{finding.Category}] {finding.Description}");
                lines.Add($"   Matched: \"{finding.MatchedText}\"");
                if (!string.IsNullOrEmpty(finding.Suggestion))
                    lines.Add($"   Suggest: {finding.Suggestion}");
                lines.Add("");
            }

            lines.Add(new string('─', 60));
            lines.Add("Debiased version:");
            lines.Add(DebiasedText);

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Detects cognitive and demographic biases in prompts.
    /// Scans for gendered language, leading questions, anchoring,
    /// exclusionary phrasing, and other bias patterns, then suggests
    /// neutral alternatives.
    /// </summary>
    /// <example>
    /// <code>
    /// var detector = new PromptBiasDetector();
    /// var report = detector.Analyze("Ask the businessman to explain his strategy");
    /// Console.WriteLine(report.Render());
    /// // Flags gendered language, suggests "business professional" / "their"
    ///
    /// // Batch analysis
    /// var prompts = new[] { "He should fix this", "Obviously the answer is 42" };
    /// var reports = detector.AnalyzeBatch(prompts);
    ///
    /// // Custom rules
    /// detector.AddRule(new BiasRule
    /// {
    ///     Category = BiasCategory.Cultural,
    ///     Pattern = new Regex(@"\bnormal\s+people\b", RegexOptions.IgnoreCase),
    ///     Description = "Implies some people are abnormal",
    ///     Suggestion = "most people",
    ///     Severity = BiasSeverity.Medium
    /// });
    /// </code>
    /// </example>
    public class PromptBiasDetector
    {
        private readonly List<BiasRule> _rules;

        /// <summary>
        /// Initializes a new instance with built-in bias detection rules.
        /// </summary>
        public PromptBiasDetector()
        {
            _rules = BuildDefaultRules();
        }

        /// <summary>
        /// Adds a custom bias detection rule.
        /// </summary>
        public void AddRule(BiasRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            _rules.Add(rule);
        }

        /// <summary>
        /// Analyzes a prompt for bias.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>A <see cref="BiasReport"/> with findings and suggestions.</returns>
        public BiasReport Analyze(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new BiasReport { OriginalText = prompt ?? string.Empty, DebiasedText = prompt ?? string.Empty };

            var findings = new List<BiasFinding>();

            foreach (var rule in _rules)
            {
                var matches = rule.Pattern.Matches(prompt);
                foreach (Match match in matches)
                {
                    findings.Add(new BiasFinding
                    {
                        Category = rule.Category,
                        Severity = rule.Severity,
                        MatchedText = match.Value,
                        Description = rule.Description,
                        Suggestion = rule.Suggestion,
                        Position = match.Index
                    });
                }
            }

            // Build debiased text
            var debiased = prompt;
            foreach (var rule in _rules)
            {
                if (!string.IsNullOrEmpty(rule.Suggestion))
                {
                    debiased = rule.Pattern.Replace(debiased, rule.Suggestion);
                }
            }

            // Calculate bias score
            double score = 0;
            foreach (var f in findings)
            {
                score += f.Severity == BiasSeverity.High ? 0.3 :
                         f.Severity == BiasSeverity.Medium ? 0.15 : 0.05;
            }
            score = Math.Min(1.0, score);

            return new BiasReport
            {
                OriginalText = prompt,
                Findings = findings,
                BiasScore = Math.Round(score, 4),
                DebiasedText = debiased
            };
        }

        /// <summary>
        /// Analyzes multiple prompts in batch.
        /// </summary>
        /// <param name="prompts">The prompts to analyze.</param>
        /// <returns>A list of bias reports, one per prompt.</returns>
        public List<BiasReport> AnalyzeBatch(IEnumerable<string> prompts)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));
            return prompts.Select(Analyze).ToList();
        }

        /// <summary>
        /// Returns the count of currently registered rules.
        /// </summary>
        public int RuleCount => _rules.Count;

        private static List<BiasRule> BuildDefaultRules()
        {
            var rules = new List<BiasRule>();
            var ic = RegexOptions.IgnoreCase;

            // === Gender bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Gender, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(businessman|businessmen)\b", ic),
                Description = "Gendered professional term",
                Suggestion = "business professional"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Gender, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(chairman)\b", ic),
                Description = "Gendered leadership term",
                Suggestion = "chairperson"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Gender, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(fireman|firemen)\b", ic),
                Description = "Gendered occupation term",
                Suggestion = "firefighter"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Gender, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(policeman|policemen)\b", ic),
                Description = "Gendered occupation term",
                Suggestion = "police officer"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Gender, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(mankind)\b", ic),
                Description = "Gendered collective term",
                Suggestion = "humankind"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Gender, Severity = BiasSeverity.Low,
                Pattern = new Regex(@"\b(manpower)\b", ic),
                Description = "Gendered resource term",
                Suggestion = "workforce"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Gender, Severity = BiasSeverity.Low,
                Pattern = new Regex(@"\b(man-made)\b", ic),
                Description = "Gendered adjective",
                Suggestion = "synthetic"
            });

            // === Confirmation bias (leading questions) ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Confirmation, Severity = BiasSeverity.High,
                Pattern = new Regex(@"\b(obviously|clearly|undeniably|undoubtedly|of course)\b", ic),
                Description = "Presuppositional language that biases the response",
                Suggestion = ""
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Confirmation, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\bdon'?t you (?:think|agree)\b", ic),
                Description = "Leading question that presupposes agreement",
                Suggestion = "what do you think about"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Confirmation, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\bisn'?t it (?:true|obvious|clear) that\b", ic),
                Description = "Leading assertion disguised as question",
                Suggestion = "is it the case that"
            });

            // === Anchoring bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Anchoring, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(?:most (?:people|experts|studies)|everyone knows|it is well known)\b", ic),
                Description = "Anchoring to unsubstantiated consensus",
                Suggestion = ""
            });

            // === Authority bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Authority, Severity = BiasSeverity.Low,
                Pattern = new Regex(@"\b(?:experts say|studies show|research proves|science says)\b", ic),
                Description = "Vague appeal to authority without citation",
                Suggestion = ""
            });

            // === Framing bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Framing, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(only|merely|just)\s+\d+%", ic),
                Description = "Minimizing framing of statistics",
                Suggestion = ""
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Framing, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(as much as|a whopping|a staggering)\s+\d+", ic),
                Description = "Amplifying framing of statistics",
                Suggestion = ""
            });

            // === Exclusion bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Exclusion, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(normal people|regular people|ordinary people)\b", ic),
                Description = "Implies some groups are abnormal or irregular",
                Suggestion = "most people"
            });

            // === Age bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Age, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(elderly|old people|the aged)\b", ic),
                Description = "Potentially ageist terminology",
                Suggestion = "older adults"
            });
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Age, Severity = BiasSeverity.Low,
                Pattern = new Regex(@"\b(young people don'?t|kids these days|millennials are)\b", ic),
                Description = "Age-based generalization",
                Suggestion = ""
            });

            // === Ability bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Ability, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(crazy|insane|lame|dumb|blind(?:ed)?(?:\s+to))\b", ic),
                Description = "Ableist language used casually",
                Suggestion = ""
            });

            // === Socioeconomic bias ===
            rules.Add(new BiasRule
            {
                Category = BiasCategory.Socioeconomic, Severity = BiasSeverity.Medium,
                Pattern = new Regex(@"\b(underprivileged|the poor|low-?class)\b", ic),
                Description = "Stigmatizing socioeconomic language",
                Suggestion = "under-resourced communities"
            });

            return rules;
        }
    }

    /// <summary>
    /// A single bias detection rule with a regex pattern and metadata.
    /// </summary>
    public class BiasRule
    {
        /// <summary>Gets or sets the bias category.</summary>
        public BiasCategory Category { get; set; }

        /// <summary>Gets or sets the severity.</summary>
        public BiasSeverity Severity { get; set; }

        /// <summary>Gets or sets the regex pattern to match.</summary>
        public Regex Pattern { get; set; } = new Regex("(?!)");

        /// <summary>Gets or sets the human-readable description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the suggested replacement (empty = manual review).</summary>
        public string Suggestion { get; set; } = string.Empty;
    }
}
