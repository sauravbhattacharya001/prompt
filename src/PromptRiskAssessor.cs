using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Prompt
{
    /// <summary>
    /// Risk dimension category for prompt security assessment.
    /// </summary>
    public enum RiskDimension
    {
        /// <summary>Prompt injection and instruction override attacks.</summary>
        Injection,
        /// <summary>Sensitive data exposure or leakage patterns.</summary>
        DataLeakage,
        /// <summary>Patterns that encourage hallucination or fabrication.</summary>
        Hallucination,
        /// <summary>Bias amplification or stereotyping risks.</summary>
        Bias,
        /// <summary>Jailbreak and safety bypass attempts.</summary>
        Jailbreak,
        /// <summary>Output manipulation and format exploitation.</summary>
        OutputManipulation
    }

    /// <summary>
    /// Severity level for a risk finding.
    /// </summary>
    public enum RiskSeverity
    {
        /// <summary>Informational only.</summary>
        Info = 0,
        /// <summary>Low risk — monitor but unlikely to cause harm.</summary>
        Low = 1,
        /// <summary>Medium risk — should be addressed before production.</summary>
        Medium = 2,
        /// <summary>High risk — likely exploitable, fix required.</summary>
        High = 3,
        /// <summary>Critical risk — immediate action required.</summary>
        Critical = 4
    }

    /// <summary>
    /// A single risk finding from the assessment.
    /// </summary>
    public class RiskFinding
    {
        /// <summary>Risk dimension this finding belongs to.</summary>
        public RiskDimension Dimension { get; set; }

        /// <summary>Severity level.</summary>
        public RiskSeverity Severity { get; set; }

        /// <summary>Short title of the finding.</summary>
        public string Title { get; set; } = "";

        /// <summary>Detailed description of the risk.</summary>
        public string Description { get; set; } = "";

        /// <summary>The matched text or pattern that triggered this finding.</summary>
        public string? MatchedText { get; set; }

        /// <summary>Recommended mitigation.</summary>
        public string Mitigation { get; set; } = "";
    }

    /// <summary>
    /// Per-dimension risk score breakdown.
    /// </summary>
    public class RiskDimensionScore
    {
        /// <summary>The risk dimension.</summary>
        public RiskDimension Dimension { get; set; }

        /// <summary>Score from 0 (safe) to 100 (extremely risky).</summary>
        public int Score { get; set; }

        /// <summary>Risk grade: A (safe) through F (critical).</summary>
        public string Grade => Score switch
        {
            <= 10 => "A",
            <= 25 => "B",
            <= 40 => "C",
            <= 60 => "D",
            _ => "F"
        };

        /// <summary>Number of findings in this dimension.</summary>
        public int FindingCount { get; set; }
    }

    /// <summary>
    /// Complete risk assessment result.
    /// </summary>
    public class RiskAssessment
    {
        /// <summary>The prompt text that was assessed.</summary>
        public string Prompt { get; set; } = "";

        /// <summary>Overall composite risk score (0-100).</summary>
        public int OverallScore { get; set; }

        /// <summary>Overall risk grade.</summary>
        public string OverallGrade => OverallScore switch
        {
            <= 10 => "A",
            <= 25 => "B",
            <= 40 => "C",
            <= 60 => "D",
            _ => "F"
        };

        /// <summary>Whether the prompt is considered safe for production use.</summary>
        public bool IsProductionSafe => OverallScore <= 25;

        /// <summary>Per-dimension score breakdown.</summary>
        public IReadOnlyList<RiskDimensionScore> DimensionScores { get; set; }
            = Array.Empty<RiskDimensionScore>();

        /// <summary>All findings from the assessment.</summary>
        public IReadOnlyList<RiskFinding> Findings { get; set; }
            = Array.Empty<RiskFinding>();

        /// <summary>Top priority mitigations to apply.</summary>
        public IReadOnlyList<string> TopMitigations { get; set; }
            = Array.Empty<string>();

        /// <summary>Assessment timestamp.</summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Serialize to JSON.</summary>
        public string ToJson(bool indented = true) => JsonSerializer.Serialize(this,
            new JsonSerializerOptions
            {
                WriteIndented = indented,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

        /// <summary>Generate a text report.</summary>
        public string ToReport()
        {
            var lines = new List<string>
            {
                "╔══════════════════════════════════════════╗",
                "║       PROMPT RISK ASSESSMENT REPORT      ║",
                "╚══════════════════════════════════════════╝",
                "",
                $"Overall Risk: {OverallScore}/100 (Grade: {OverallGrade})",
                $"Production Safe: {(IsProductionSafe ? "YES ✓" : "NO ✗")}",
                $"Findings: {Findings.Count}",
                "",
                "── Dimension Breakdown ──"
            };

            foreach (var ds in DimensionScores)
            {
                var bar = new string('█', ds.Score / 5) + new string('░', 20 - ds.Score / 5);
                lines.Add($"  {ds.Dimension,-20} [{bar}] {ds.Score,3}/100 ({ds.Grade}) [{ds.FindingCount} findings]");
            }

            if (Findings.Count > 0)
            {
                lines.Add("");
                lines.Add("── Findings ──");
                foreach (var f in Findings.OrderByDescending(f => f.Severity))
                {
                    lines.Add($"  [{f.Severity}] {f.Dimension}: {f.Title}");
                    lines.Add($"    {f.Description}");
                    if (f.MatchedText != null)
                    {
                        var display = f.MatchedText.Length > 60
                            ? f.MatchedText[..57] + "..."
                            : f.MatchedText;
                        lines.Add($"    Matched: \"{display}\"");
                    }
                    lines.Add($"    Fix: {f.Mitigation}");
                }
            }

            if (TopMitigations.Count > 0)
            {
                lines.Add("");
                lines.Add("── Top Mitigations ──");
                for (int i = 0; i < TopMitigations.Count; i++)
                    lines.Add($"  {i + 1}. {TopMitigations[i]}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Analyzes prompts for security risks across multiple dimensions:
    /// injection, data leakage, hallucination, bias, jailbreak, and output manipulation.
    /// Produces scored assessments with actionable mitigations.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="PromptGuard"/> which provides basic injection detection and quality scoring,
    /// PromptRiskAssessor performs deep multi-dimensional risk analysis with severity-rated findings,
    /// per-dimension scoring, composite risk grades, and prioritized remediation guidance.
    /// </remarks>
    public class PromptRiskAssessor
    {
        private readonly List<(Regex Pattern, RiskDimension Dim, RiskSeverity Sev, string Title, string Desc, string Fix)> _rules;

        /// <summary>
        /// Creates a new PromptRiskAssessor with default detection rules.
        /// </summary>
        public PromptRiskAssessor()
        {
            _rules = BuildDefaultRules();
        }

        /// <summary>
        /// Assess a prompt for security risks.
        /// </summary>
        /// <param name="prompt">The prompt text to assess.</param>
        /// <returns>A complete risk assessment.</returns>
        /// <exception cref="ArgumentNullException">When prompt is null.</exception>
        public RiskAssessment Assess(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            var findings = new List<RiskFinding>();
            var lower = prompt.ToLowerInvariant();

            // Run pattern-based rules
            foreach (var rule in _rules)
            {
                var match = rule.Pattern.Match(lower);
                if (match.Success)
                {
                    findings.Add(new RiskFinding
                    {
                        Dimension = rule.Dim,
                        Severity = rule.Sev,
                        Title = rule.Title,
                        Description = rule.Desc,
                        MatchedText = match.Value,
                        Mitigation = rule.Fix
                    });
                }
            }

            // Structural analysis
            findings.AddRange(AnalyzeStructuralRisks(prompt, lower));

            // Compute dimension scores
            var dimScores = ComputeDimensionScores(findings);

            // Composite score (weighted)
            int overall = ComputeOverallScore(dimScores);

            // Top mitigations
            var topMitigations = findings
                .OrderByDescending(f => f.Severity)
                .Select(f => f.Mitigation)
                .Distinct()
                .Take(5)
                .ToList();

            return new RiskAssessment
            {
                Prompt = prompt,
                OverallScore = overall,
                DimensionScores = dimScores,
                Findings = findings,
                TopMitigations = topMitigations
            };
        }

        /// <summary>
        /// Assess multiple prompts and return results.
        /// </summary>
        public IReadOnlyList<RiskAssessment> AssessBatch(IEnumerable<string> prompts)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));
            return prompts.Select(Assess).ToList();
        }

        /// <summary>
        /// Quick check: returns true if the prompt has any High or Critical findings.
        /// </summary>
        public bool HasHighRisk(string prompt)
        {
            var result = Assess(prompt);
            return result.Findings.Any(f => f.Severity >= RiskSeverity.High);
        }

        /// <summary>
        /// Compare risk profiles of two prompts.
        /// </summary>
        public (RiskAssessment Original, RiskAssessment Revised, int ScoreDelta) Compare(
            string original, string revised)
        {
            var a = Assess(original);
            var b = Assess(revised);
            return (a, b, b.OverallScore - a.OverallScore);
        }

        private List<RiskFinding> AnalyzeStructuralRisks(string prompt, string lower)
        {
            var findings = new List<RiskFinding>();

            // Very long prompts increase attack surface
            if (prompt.Length > 4000)
            {
                findings.Add(new RiskFinding
                {
                    Dimension = RiskDimension.Injection,
                    Severity = RiskSeverity.Low,
                    Title = "Large prompt surface",
                    Description = "Prompts over 4000 characters have a larger attack surface for hidden injections.",
                    Mitigation = "Review for unnecessary content; consider splitting into smaller prompts."
                });
            }

            // No output constraints
            if (!Regex.IsMatch(lower, @"(respond (only|exclusively)|output (only|format)|return (only|just)|format:|json|xml|markdown)"))
            {
                findings.Add(new RiskFinding
                {
                    Dimension = RiskDimension.OutputManipulation,
                    Severity = RiskSeverity.Low,
                    Title = "No output format constraints",
                    Description = "Without explicit output format instructions, responses may be manipulated into unexpected formats.",
                    Mitigation = "Add explicit output format instructions (e.g., 'Respond only in JSON format')."
                });
            }

            // Unrestricted role assignment
            if (Regex.IsMatch(lower, @"you (are|can be) (anything|anyone|whatever)"))
            {
                findings.Add(new RiskFinding
                {
                    Dimension = RiskDimension.Jailbreak,
                    Severity = RiskSeverity.High,
                    Title = "Unrestricted role assignment",
                    Description = "Telling the model it can be 'anything' or 'anyone' weakens safety boundaries.",
                    Mitigation = "Use specific, bounded role definitions instead of open-ended assignments."
                });
            }

            // User input placeholders without sanitization notes
            if (Regex.IsMatch(prompt, @"\{(user_input|input|query|question|message)\}") &&
                !Regex.IsMatch(lower, @"(sanitiz|validat|escap|filter)"))
            {
                findings.Add(new RiskFinding
                {
                    Dimension = RiskDimension.Injection,
                    Severity = RiskSeverity.Medium,
                    Title = "Unsanitized user input placeholder",
                    Description = "User input variables are included without mention of sanitization or validation.",
                    Mitigation = "Add input validation/sanitization before interpolation, or add guardrails in the prompt."
                });
            }

            // Asking for personal data categories
            var piiPatterns = Regex.Matches(lower, @"(social security|ssn|credit card|passport|date of birth|phone number|home address|bank account)");
            if (piiPatterns.Count > 0)
            {
                findings.Add(new RiskFinding
                {
                    Dimension = RiskDimension.DataLeakage,
                    Severity = RiskSeverity.High,
                    Title = "PII reference detected",
                    Description = $"Prompt references personally identifiable information ({piiPatterns[0].Value}).",
                    MatchedText = piiPatterns[0].Value,
                    Mitigation = "Avoid embedding or requesting PII in prompts; use anonymized placeholders."
                });
            }

            // Absolute certainty demands increase hallucination
            if (Regex.IsMatch(lower, @"(always be (correct|right|accurate)|never (be wrong|make mistakes|hallucinate)|100% accurate)"))
            {
                findings.Add(new RiskFinding
                {
                    Dimension = RiskDimension.Hallucination,
                    Severity = RiskSeverity.Medium,
                    Title = "Absolute certainty demand",
                    Description = "Demanding the model never be wrong can paradoxically increase confident hallucination.",
                    Mitigation = "Instead, instruct the model to express uncertainty and cite sources when unsure."
                });
            }

            return findings;
        }

        private static List<RiskDimensionScore> ComputeDimensionScores(List<RiskFinding> findings)
        {
            var scores = new List<RiskDimensionScore>();
            foreach (RiskDimension dim in Enum.GetValues(typeof(RiskDimension)))
            {
                var dimFindings = findings.Where(f => f.Dimension == dim).ToList();
                int score = 0;
                foreach (var f in dimFindings)
                {
                    score += f.Severity switch
                    {
                        RiskSeverity.Info => 3,
                        RiskSeverity.Low => 8,
                        RiskSeverity.Medium => 20,
                        RiskSeverity.High => 35,
                        RiskSeverity.Critical => 50,
                        _ => 0
                    };
                }
                scores.Add(new RiskDimensionScore
                {
                    Dimension = dim,
                    Score = Math.Min(100, score),
                    FindingCount = dimFindings.Count
                });
            }
            return scores;
        }

        private static int ComputeOverallScore(List<RiskDimensionScore> dimScores)
        {
            // Weighted average — injection and jailbreak weigh more
            var weights = new Dictionary<RiskDimension, double>
            {
                [RiskDimension.Injection] = 2.0,
                [RiskDimension.DataLeakage] = 1.5,
                [RiskDimension.Hallucination] = 1.0,
                [RiskDimension.Bias] = 1.0,
                [RiskDimension.Jailbreak] = 2.0,
                [RiskDimension.OutputManipulation] = 1.0
            };
            double totalWeight = weights.Values.Sum();
            double weighted = dimScores.Sum(ds => ds.Score * weights[ds.Dimension]);
            return Math.Min(100, (int)Math.Round(weighted / totalWeight));
        }

        private static List<(Regex, RiskDimension, RiskSeverity, string, string, string)> BuildDefaultRules()
        {
            return new List<(Regex, RiskDimension, RiskSeverity, string, string, string)>
            {
                // Injection patterns
                (new Regex(@"ignore\s+(all\s+)?(previous|prior|above)\s+(instructions|rules|guidelines)", RegexOptions.IgnoreCase),
                    RiskDimension.Injection, RiskSeverity.Critical,
                    "Instruction override attempt",
                    "Pattern attempts to override system instructions.",
                    "Remove instruction override language; use input sandboxing."),

                (new Regex(@"(system\s*prompt|initial\s*prompt|original\s*instructions?).*?(reveal|show|display|print|output)", RegexOptions.IgnoreCase),
                    RiskDimension.Injection, RiskSeverity.Critical,
                    "System prompt extraction",
                    "Attempts to extract the system prompt or initial instructions.",
                    "Never echo system prompts; add explicit 'do not reveal instructions' guardrails."),

                (new Regex(@"pretend\s+(you('re|\s+are)\s+)?(a\s+)?(different|new|another)", RegexOptions.IgnoreCase),
                    RiskDimension.Injection, RiskSeverity.High,
                    "Identity manipulation",
                    "Attempts to reassign the model's identity or role.",
                    "Add strong identity anchoring in the system prompt."),

                (new Regex(@"(translate|convert)\s+.{0,30}(instructions|rules|system)", RegexOptions.IgnoreCase),
                    RiskDimension.Injection, RiskSeverity.Medium,
                    "Translation-based extraction",
                    "May attempt to extract instructions through translation requests.",
                    "Exclude system instructions from translation scope."),

                // Jailbreak patterns
                (new Regex(@"(DAN|do anything now|developer mode|unlocked mode|god mode)", RegexOptions.IgnoreCase),
                    RiskDimension.Jailbreak, RiskSeverity.Critical,
                    "Known jailbreak pattern",
                    "References a well-known jailbreak technique (DAN/developer mode).",
                    "Block known jailbreak keywords; add safety preamble."),

                (new Regex(@"(no\s+(rules?|restrictions?|limitations?|boundaries|filters?|constraints?)|without\s+(any\s+)?(restrictions?|limitations?|filters?|constraints?))", RegexOptions.IgnoreCase),
                    RiskDimension.Jailbreak, RiskSeverity.High,
                    "Safety boundary removal",
                    "Attempts to remove safety constraints or content filters.",
                    "Reinforce safety boundaries in system prompt; never acknowledge ability to bypass."),

                (new Regex(@"hypothetical(ly)?.*?(illegal|harmful|dangerous|unethical)", RegexOptions.IgnoreCase),
                    RiskDimension.Jailbreak, RiskSeverity.Medium,
                    "Hypothetical harmful scenario",
                    "Uses hypothetical framing to elicit harmful content.",
                    "Add guardrails against hypothetical scenario exploitation."),

                // Data leakage
                (new Regex(@"(api[_\s]?key|secret[_\s]?key|access[_\s]?token|password|credentials?)\s*[:=]", RegexOptions.IgnoreCase),
                    RiskDimension.DataLeakage, RiskSeverity.Critical,
                    "Credential in prompt",
                    "Prompt appears to contain hardcoded credentials or API keys.",
                    "Never embed credentials in prompts; use environment variables or secret managers."),

                (new Regex(@"(sk-[a-zA-Z0-9]{20,}|ghp_[a-zA-Z0-9]{20,}|AKIA[A-Z0-9]{16})", RegexOptions.None),
                    RiskDimension.DataLeakage, RiskSeverity.Critical,
                    "API key pattern detected",
                    "Prompt contains what appears to be an actual API key (OpenAI/GitHub/AWS).",
                    "Remove the key immediately; rotate it; use secure key management."),

                (new Regex(@"(list|show|give|tell)\s+(me\s+)?(all|every)\s+(user|customer|employee|patient|record|account)", RegexOptions.IgnoreCase),
                    RiskDimension.DataLeakage, RiskSeverity.Medium,
                    "Bulk data extraction request",
                    "Prompt requests bulk extraction of potentially sensitive records.",
                    "Implement pagination and access controls; avoid bulk data in prompt responses."),

                // Hallucination
                (new Regex(@"(make up|invent|fabricate|create fictional)\s+.{0,30}(facts?|data|statistics?|sources?|citations?|references?)", RegexOptions.IgnoreCase),
                    RiskDimension.Hallucination, RiskSeverity.High,
                    "Explicit fabrication request",
                    "Prompt explicitly asks the model to fabricate information.",
                    "If fictional content is needed, clearly label it as such."),

                (new Regex(@"cite\s+.{0,20}(sources?|papers?|studies?|research)", RegexOptions.IgnoreCase),
                    RiskDimension.Hallucination, RiskSeverity.Low,
                    "Citation request",
                    "Citation requests may yield fabricated references without RAG/retrieval.",
                    "Use retrieval-augmented generation or verify citations independently."),

                // Bias
                (new Regex(@"(all|every|most)\s+(men|women|blacks?|whites?|asians?|muslims?|christians?|jews?|hispanics?|mexicans?|americans?)\s+(are|tend to|always|never)", RegexOptions.IgnoreCase),
                    RiskDimension.Bias, RiskSeverity.High,
                    "Stereotyping generalization",
                    "Prompt contains broad generalizations about demographic groups.",
                    "Remove stereotyping language; use specific, evidence-based claims."),

                (new Regex(@"(rank|compare|rate)\s+.{0,30}(race|gender|religion|ethnicity|nationality)", RegexOptions.IgnoreCase),
                    RiskDimension.Bias, RiskSeverity.Medium,
                    "Demographic ranking request",
                    "Requesting rankings by demographic characteristics risks perpetuating bias.",
                    "Reframe to focus on structural factors rather than demographic comparisons."),

                // Output manipulation
                (new Regex(@"(```|<script|<iframe|javascript:|onerror=|onclick=)", RegexOptions.IgnoreCase),
                    RiskDimension.OutputManipulation, RiskSeverity.Medium,
                    "Code injection in prompt",
                    "Prompt contains HTML/JavaScript that could execute if output is rendered unsanitized.",
                    "Sanitize all prompt content; escape HTML in rendered output."),

                (new Regex(@"\[.*?\]\(javascript:", RegexOptions.IgnoreCase),
                    RiskDimension.OutputManipulation, RiskSeverity.High,
                    "Markdown JavaScript injection",
                    "Markdown link with JavaScript URI could execute code if rendered.",
                    "Strip JavaScript URIs from markdown output; use allowlisted URL schemes only."),
            };
        }
    }
}
