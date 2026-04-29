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
    //  PromptMutationLab – Autonomous Mutation Testing Engine
    //
    //  Inspired by code mutation testing, this engine systematically
    //  segments prompts into semantic zones, applies targeted mutation
    //  operators per zone, evaluates resilience heuristically, and
    //  generates a full resilience map with hardening recommendations.
    //  Unlike PromptFuzzer (random perturbations), this performs
    //  structured, zone-aware analysis to pinpoint fragile sections.
    // ────────────────────────────────────────────

    /// <summary>Semantic zone type within a prompt.</summary>
    public enum MutationZoneType
    {
        /// <summary>Imperative instructions telling the model what to do.</summary>
        Instruction,
        /// <summary>Constraints using must/always/never language.</summary>
        Constraint,
        /// <summary>Examples or demonstrations.</summary>
        Example,
        /// <summary>Background context or information.</summary>
        Context,
        /// <summary>Role or persona definition (You are...).</summary>
        RoleDefinition,
        /// <summary>Output format specifications.</summary>
        OutputFormat,
        /// <summary>Important notes, warnings, guardrails.</summary>
        Guardrail,
        /// <summary>Unclassified freeform text.</summary>
        Freeform
    }

    /// <summary>Mutation operator type applied to a zone.</summary>
    public enum MutationOperator
    {
        /// <summary>Remove the zone entirely.</summary>
        Deletion,
        /// <summary>Replace key words with synonyms or antonyms.</summary>
        Substitution,
        /// <summary>Shuffle sentences within the zone.</summary>
        Reordering,
        /// <summary>Flip polarity (not, never↔always).</summary>
        Negation,
        /// <summary>Replace strong words with weak equivalents.</summary>
        Weakening,
        /// <summary>Replace weak words with strong equivalents.</summary>
        Strengthening,
        /// <summary>Repeat the zone text.</summary>
        Duplication,
        /// <summary>Add a contradicting statement.</summary>
        Contradiction,
        /// <summary>Replace specific terms with vague ones.</summary>
        Ambiguation,
        /// <summary>Cut the zone at 50%.</summary>
        Truncation
    }

    /// <summary>Resilience grade based on score thresholds.</summary>
    public enum MutationResilienceGrade
    {
        /// <summary>Score 90–100: extremely resilient.</summary>
        Fortified,
        /// <summary>Score 70–89: strong resilience.</summary>
        Robust,
        /// <summary>Score 50–69: acceptable but improvable.</summary>
        Moderate,
        /// <summary>Score 30–49: significant vulnerabilities.</summary>
        Fragile,
        /// <summary>Score 0–29: critical weaknesses requiring immediate attention.</summary>
        Critical
    }

    /// <summary>Priority level for hardening recommendations.</summary>
    public enum HardeningPriority
    {
        /// <summary>Must fix now — critical vulnerability.</summary>
        Immediate,
        /// <summary>Fix soon — high risk.</summary>
        High,
        /// <summary>Plan to fix — moderate risk.</summary>
        Medium,
        /// <summary>Nice to have — low risk.</summary>
        Low,
        /// <summary>Optional improvement.</summary>
        Optional
    }

    /// <summary>A detected semantic zone within a prompt.</summary>
    public class PromptZone
    {
        /// <summary>Start character index in the original prompt.</summary>
        public int StartIndex { get; set; }

        /// <summary>End character index (exclusive) in the original prompt.</summary>
        public int EndIndex { get; set; }

        /// <summary>The text content of this zone.</summary>
        public string Text { get; set; } = "";

        /// <summary>Detected or assigned zone type.</summary>
        public MutationZoneType ZoneType { get; set; }

        /// <summary>Human-readable label for this zone.</summary>
        public string Label { get; set; } = "";
    }

    /// <summary>A single mutation test case targeting a specific zone.</summary>
    public class MutationCase
    {
        /// <summary>Unique identifier for this case.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>The zone being mutated.</summary>
        public PromptZone TargetZone { get; set; } = new();

        /// <summary>The mutation operator applied.</summary>
        public MutationOperator Operator { get; set; }

        /// <summary>Original text before mutation.</summary>
        public string OriginalText { get; set; } = "";

        /// <summary>Text after mutation.</summary>
        public string MutatedText { get; set; } = "";

        /// <summary>Human-readable description of what was mutated.</summary>
        public string Description { get; set; } = "";
    }

    /// <summary>Outcome of evaluating a mutation case.</summary>
    public class MutationOutcome
    {
        /// <summary>The mutation case ID this outcome belongs to.</summary>
        public string CaseId { get; set; } = "";

        /// <summary>Semantic drift from original (0.0 = identical, 1.0 = completely different).</summary>
        public double SemanticDrift { get; set; }

        /// <summary>Whether the core intent of the prompt is preserved.</summary>
        public bool IntentPreserved { get; set; }

        /// <summary>List of constraint violations introduced by this mutation.</summary>
        public List<string> ConstraintViolations { get; set; } = new();

        /// <summary>Quality delta from -100 (catastrophic) to +100 (improved).</summary>
        public double QualityDelta { get; set; }
    }

    /// <summary>Resilience report for a single zone.</summary>
    public class ZoneResilienceReport
    {
        /// <summary>The zone analyzed.</summary>
        public PromptZone Zone { get; set; } = new();

        /// <summary>Resilience grade.</summary>
        public MutationResilienceGrade Grade { get; set; }

        /// <summary>Resilience score 0–100.</summary>
        public double Score { get; set; }

        /// <summary>The mutation operator that caused the most damage.</summary>
        public MutationOperator WeakestOperator { get; set; }

        /// <summary>The mutation operator that had the least impact.</summary>
        public MutationOperator StrongestOperator { get; set; }

        /// <summary>Number of mutations the zone survived (intent preserved).</summary>
        public int MutationsSurvived { get; set; }

        /// <summary>Total mutations tested on this zone.</summary>
        public int MutationsTotal { get; set; }

        /// <summary>Specific vulnerabilities identified.</summary>
        public List<string> Vulnerabilities { get; set; } = new();
    }

    /// <summary>A hardening recommendation for improving prompt resilience.</summary>
    public class HardeningRecommendation
    {
        /// <summary>The zone this recommendation targets.</summary>
        public PromptZone Zone { get; set; } = new();

        /// <summary>Priority of this recommendation.</summary>
        public HardeningPriority Priority { get; set; }

        /// <summary>Short title.</summary>
        public string Title { get; set; } = "";

        /// <summary>Detailed description of the vulnerability.</summary>
        public string Description { get; set; } = "";

        /// <summary>Suggested fix or improvement.</summary>
        public string SuggestedFix { get; set; } = "";

        /// <summary>Estimated impact of applying this fix (0–100).</summary>
        public int EstimatedImpact { get; set; }
    }

    /// <summary>A mutation campaign containing all cases and outcomes for a prompt.</summary>
    public class MutationCampaign
    {
        /// <summary>Campaign identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>The original prompt text.</summary>
        public string PromptText { get; set; } = "";

        /// <summary>Detected zones.</summary>
        public List<PromptZone> Zones { get; set; } = new();

        /// <summary>Generated mutation cases.</summary>
        public List<MutationCase> Cases { get; set; } = new();

        /// <summary>Evaluated outcomes.</summary>
        public List<MutationOutcome> Outcomes { get; set; } = new();
    }

    /// <summary>Full mutation lab report with resilience analysis and recommendations.</summary>
    public class MutationLabReport
    {
        /// <summary>The campaign that generated this report.</summary>
        public MutationCampaign Campaign { get; set; } = new();

        /// <summary>Per-zone resilience reports.</summary>
        public List<ZoneResilienceReport> ZoneReports { get; set; } = new();

        /// <summary>Hardening recommendations sorted by priority.</summary>
        public List<HardeningRecommendation> Recommendations { get; set; } = new();

        /// <summary>Overall resilience score (0–100).</summary>
        public double OverallResilienceScore { get; set; }

        /// <summary>Overall resilience grade.</summary>
        public MutationResilienceGrade OverallGrade { get; set; }

        /// <summary>Zones identified as fragility hotspots.</summary>
        public List<string> FragilityHotspots { get; set; } = new();

        /// <summary>Human-readable executive summary.</summary>
        public string Summary { get; set; } = "";
    }

    /// <summary>Configuration for a mutation lab run.</summary>
    public class MutationLabConfig
    {
        /// <summary>Number of mutations to generate per zone. Default: 8.</summary>
        public int MutationsPerZone { get; set; } = 8;

        /// <summary>Which operators to use. Empty = all.</summary>
        public List<MutationOperator> Operators { get; set; } = new();

        /// <summary>Minimum character length for a zone to be tested. Default: 10.</summary>
        public int MinZoneLength { get; set; } = 10;

        /// <summary>Whether to auto-detect zones. Default: true.</summary>
        public bool EnableAutoZoneDetection { get; set; } = true;

        /// <summary>Random seed for reproducible results. Null for random.</summary>
        public int? Seed { get; set; }
    }

    /// <summary>
    /// Autonomous mutation testing engine for prompts. Systematically mutates
    /// prompt zones to identify fragile sections and generate hardening plans.
    /// </summary>
    public class PromptMutationLab
    {
        private readonly MutationLabConfig _config;
        private readonly Random _rng;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Word replacement maps for mutation operators
        private static readonly Dictionary<string, string> StrongToWeak = new(StringComparer.OrdinalIgnoreCase)
        {
            ["must"] = "should", ["always"] = "sometimes", ["never"] = "rarely",
            ["required"] = "recommended", ["mandatory"] = "optional", ["exactly"] = "approximately",
            ["critical"] = "helpful", ["essential"] = "useful", ["guarantee"] = "attempt",
            ["strictly"] = "preferably", ["immediately"] = "eventually", ["absolutely"] = "ideally"
        };

        private static readonly Dictionary<string, string> WeakToStrong = new(StringComparer.OrdinalIgnoreCase)
        {
            ["should"] = "must", ["sometimes"] = "always", ["rarely"] = "never",
            ["recommended"] = "required", ["optional"] = "mandatory", ["approximately"] = "exactly",
            ["helpful"] = "critical", ["useful"] = "essential", ["attempt"] = "guarantee",
            ["preferably"] = "strictly", ["eventually"] = "immediately", ["ideally"] = "absolutely"
        };

        private static readonly Dictionary<string, string> SpecificToVague = new(StringComparer.OrdinalIgnoreCase)
        {
            ["JSON"] = "structured format", ["XML"] = "markup", ["CSV"] = "tabular data",
            ["integer"] = "number", ["boolean"] = "value", ["array"] = "collection",
            ["paragraph"] = "text block", ["bullet points"] = "list format",
            ["three"] = "several", ["five"] = "some", ["ten"] = "many",
            ["English"] = "natural language", ["Python"] = "programming language",
            ["Monday"] = "a day", ["2024"] = "recently"
        };

        /// <summary>Create a new mutation lab with optional configuration.</summary>
        public PromptMutationLab(MutationLabConfig? config = null)
        {
            _config = config ?? new MutationLabConfig();
            _rng = _config.Seed.HasValue ? new Random(_config.Seed.Value) : new Random();
        }

        /// <summary>
        /// Detect semantic zones in a prompt using regex heuristics.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>List of detected zones with their types and positions.</returns>
        public List<PromptZone> DetectZones(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new List<PromptZone>();

            var zones = new List<PromptZone>();
            var lines = prompt.Split('\n');
            int currentIndex = 0;

            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (trimmed.Length < _config.MinZoneLength)
                {
                    currentIndex += line.Length + 1;
                    continue;
                }

                var zoneType = ClassifyLine(trimmed);
                zones.Add(new PromptZone
                {
                    StartIndex = currentIndex,
                    EndIndex = currentIndex + line.Length,
                    Text = line,
                    ZoneType = zoneType,
                    Label = $"{zoneType}@{currentIndex}"
                });

                currentIndex += line.Length + 1;
            }

            // Merge adjacent zones of the same type
            return MergeAdjacentZones(zones);
        }

        /// <summary>
        /// Generate mutation cases for a prompt. Auto-detects zones if not provided.
        /// </summary>
        public MutationCampaign GenerateMutations(string prompt, List<PromptZone>? zones = null)
        {
            zones ??= DetectZones(prompt);
            var operators = _config.Operators.Count > 0
                ? _config.Operators
                : Enum.GetValues<MutationOperator>().ToList();

            var campaign = new MutationCampaign
            {
                PromptText = prompt,
                Zones = zones
            };

            foreach (var zone in zones)
            {
                int count = Math.Min(_config.MutationsPerZone, operators.Count);
                var selectedOps = operators.OrderBy(_ => _rng.Next()).Take(count).ToList();

                foreach (var op in selectedOps)
                {
                    var mutatedText = ApplyMutation(zone.Text, op);
                    campaign.Cases.Add(new MutationCase
                    {
                        TargetZone = zone,
                        Operator = op,
                        OriginalText = zone.Text,
                        MutatedText = mutatedText,
                        Description = $"{op} applied to {zone.ZoneType} zone '{zone.Label}'"
                    });
                }
            }

            return campaign;
        }

        /// <summary>
        /// Evaluate outcomes for all cases in a campaign using heuristic scoring.
        /// </summary>
        public MutationCampaign EvaluateOutcomes(MutationCampaign campaign)
        {
            var coreVerbs = ExtractCoreVerbs(campaign.PromptText);
            var constraintKeywords = ExtractConstraintKeywords(campaign.PromptText);

            foreach (var c in campaign.Cases)
            {
                var fullMutated = campaign.PromptText.Replace(c.OriginalText, c.MutatedText);
                var drift = ComputeNormalizedEditDistance(campaign.PromptText, fullMutated);
                var intentPreserved = CheckIntentPreserved(fullMutated, coreVerbs);
                var violations = CheckConstraintViolations(fullMutated, constraintKeywords);
                var qualityDelta = ComputeQualityDelta(drift, intentPreserved, violations.Count);

                campaign.Outcomes.Add(new MutationOutcome
                {
                    CaseId = c.Id,
                    SemanticDrift = drift,
                    IntentPreserved = intentPreserved,
                    ConstraintViolations = violations,
                    QualityDelta = qualityDelta
                });
            }

            return campaign;
        }

        /// <summary>
        /// Run the full analysis pipeline: detect zones → generate mutations → evaluate → report.
        /// </summary>
        /// <param name="prompt">The prompt to analyze.</param>
        /// <returns>Complete mutation lab report with resilience scores and recommendations.</returns>
        public MutationLabReport Analyze(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new MutationLabReport
                {
                    OverallResilienceScore = 0,
                    OverallGrade = MutationResilienceGrade.Critical,
                    Summary = "Empty prompt — no analysis possible."
                };
            }

            var zones = _config.EnableAutoZoneDetection ? DetectZones(prompt) : new List<PromptZone>();
            if (zones.Count == 0)
            {
                // Treat entire prompt as one freeform zone
                zones.Add(new PromptZone
                {
                    StartIndex = 0,
                    EndIndex = prompt.Length,
                    Text = prompt,
                    ZoneType = MutationZoneType.Freeform,
                    Label = "entire-prompt"
                });
            }

            var campaign = GenerateMutations(prompt, zones);
            campaign = EvaluateOutcomes(campaign);

            var zoneReports = GenerateZoneReports(campaign);
            var recommendations = GenerateRecommendations(zoneReports);
            var overallScore = zoneReports.Count > 0
                ? zoneReports.Average(z => z.Score)
                : 0;
            var hotspots = zoneReports
                .Where(z => z.Grade == MutationResilienceGrade.Critical || z.Grade == MutationResilienceGrade.Fragile)
                .Select(z => z.Zone.Label)
                .ToList();

            return new MutationLabReport
            {
                Campaign = campaign,
                ZoneReports = zoneReports,
                Recommendations = recommendations,
                OverallResilienceScore = Math.Round(overallScore, 1),
                OverallGrade = ScoreToGrade(overallScore),
                FragilityHotspots = hotspots,
                Summary = GenerateSummary(zoneReports, overallScore, hotspots, recommendations)
            };
        }

        /// <summary>Serialize a report to JSON.</summary>
        public string ToJson(MutationLabReport report) =>
            JsonSerializer.Serialize(report, JsonOpts);

        /// <summary>Render a report as a readable Markdown document.</summary>
        public string ToMarkdown(MutationLabReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 🧬 Prompt Mutation Lab Report");
            sb.AppendLine();
            sb.AppendLine($"**Overall Resilience:** {report.OverallResilienceScore}/100 ({report.OverallGrade})");
            sb.AppendLine($"**Zones Analyzed:** {report.ZoneReports.Count}");
            sb.AppendLine($"**Mutations Tested:** {report.Campaign.Cases.Count}");
            sb.AppendLine($"**Hotspots:** {report.FragilityHotspots.Count}");
            sb.AppendLine();

            sb.AppendLine("## Summary");
            sb.AppendLine(report.Summary);
            sb.AppendLine();

            sb.AppendLine("## Zone Map");
            sb.AppendLine("| Zone | Type | Score | Grade |");
            sb.AppendLine("|------|------|-------|-------|");
            foreach (var zr in report.ZoneReports)
            {
                var preview = zr.Zone.Text.Length > 40
                    ? zr.Zone.Text[..40] + "..."
                    : zr.Zone.Text;
                preview = preview.Replace("|", "\\|").Replace("\n", " ");
                sb.AppendLine($"| {preview} | {zr.Zone.ZoneType} | {zr.Score:F1} | {zr.Grade} |");
            }
            sb.AppendLine();

            sb.AppendLine("## Per-Zone Resilience");
            foreach (var zr in report.ZoneReports)
            {
                sb.AppendLine($"### {zr.Zone.Label} ({zr.Grade})");
                sb.AppendLine($"- Score: {zr.Score:F1}/100");
                sb.AppendLine($"- Survived: {zr.MutationsSurvived}/{zr.MutationsTotal}");
                sb.AppendLine($"- Weakest against: {zr.WeakestOperator}");
                sb.AppendLine($"- Strongest against: {zr.StrongestOperator}");
                if (zr.Vulnerabilities.Count > 0)
                {
                    sb.AppendLine("- Vulnerabilities:");
                    foreach (var v in zr.Vulnerabilities)
                        sb.AppendLine($"  - {v}");
                }
                sb.AppendLine();
            }

            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine("## Hardening Recommendations");
                sb.AppendLine("| Priority | Title | Impact | Zone |");
                sb.AppendLine("|----------|-------|--------|------|");
                foreach (var rec in report.Recommendations)
                {
                    sb.AppendLine($"| {rec.Priority} | {rec.Title} | {rec.EstimatedImpact}% | {rec.Zone.Label} |");
                }
                sb.AppendLine();

                foreach (var rec in report.Recommendations.Take(5))
                {
                    sb.AppendLine($"### {rec.Title}");
                    sb.AppendLine($"**Priority:** {rec.Priority} | **Impact:** {rec.EstimatedImpact}%");
                    sb.AppendLine(rec.Description);
                    sb.AppendLine($"> **Fix:** {rec.SuggestedFix}");
                    sb.AppendLine();
                }
            }

            if (report.FragilityHotspots.Count > 0)
            {
                sb.AppendLine("## ⚠️ Fragility Hotspots");
                foreach (var h in report.FragilityHotspots)
                    sb.AppendLine($"- {h}");
            }

            return sb.ToString();
        }

        // ──────────── Private Helpers ────────────

        private MutationZoneType ClassifyLine(string line)
        {
            var lower = line.ToLowerInvariant();

            if (Regex.IsMatch(lower, @"^(you are|act as|you're a|your role|as a|imagine you)"))
                return MutationZoneType.RoleDefinition;

            if (Regex.IsMatch(lower, @"\b(must|always|never|do not|don't|shall not|cannot|forbidden|prohibited)\b"))
                return MutationZoneType.Constraint;

            if (Regex.IsMatch(lower, @"(^example|^e\.g\.|for instance|for example|sample:|input:|output:)"))
                return MutationZoneType.Example;

            if (Regex.IsMatch(lower, @"\b(output|format|respond with|return|reply in|structured as|render as)\b"))
                return MutationZoneType.OutputFormat;

            if (Regex.IsMatch(lower, @"^(important|note|warning|caution|reminder|remember):?"))
                return MutationZoneType.Guardrail;

            if (Regex.IsMatch(lower, @"^(list|explain|summarize|describe|analyze|generate|create|write|find|extract|translate|compare|evaluate)"))
                return MutationZoneType.Instruction;

            return MutationZoneType.Context;
        }

        private List<PromptZone> MergeAdjacentZones(List<PromptZone> zones)
        {
            if (zones.Count <= 1) return zones;
            var merged = new List<PromptZone> { zones[0] };

            for (int i = 1; i < zones.Count; i++)
            {
                var prev = merged[^1];
                if (prev.ZoneType == zones[i].ZoneType && zones[i].StartIndex - prev.EndIndex <= 2)
                {
                    // Merge
                    prev.EndIndex = zones[i].EndIndex;
                    prev.Text += "\n" + zones[i].Text;
                }
                else
                {
                    merged.Add(zones[i]);
                }
            }
            return merged;
        }

        private string ApplyMutation(string text, MutationOperator op)
        {
            return op switch
            {
                MutationOperator.Deletion => "",
                MutationOperator.Substitution => ApplySubstitution(text),
                MutationOperator.Reordering => ApplyReordering(text),
                MutationOperator.Negation => ApplyNegation(text),
                MutationOperator.Weakening => ApplyWordReplace(text, StrongToWeak),
                MutationOperator.Strengthening => ApplyWordReplace(text, WeakToStrong),
                MutationOperator.Duplication => text + " " + text,
                MutationOperator.Contradiction => ApplyContradiction(text),
                MutationOperator.Ambiguation => ApplyWordReplace(text, SpecificToVague),
                MutationOperator.Truncation => text[..(text.Length / 2)],
                _ => text
            };
        }

        private string ApplySubstitution(string text)
        {
            var words = text.Split(' ');
            if (words.Length < 2) return text;
            int idx = _rng.Next(words.Length);
            // Reverse the word as a simple substitution
            words[idx] = new string(words[idx].Reverse().ToArray());
            return string.Join(' ', words);
        }

        private string ApplyReordering(string text)
        {
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
            if (sentences.Length < 2) return text;
            // Fisher-Yates shuffle
            for (int i = sentences.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (sentences[i], sentences[j]) = (sentences[j], sentences[i]);
            }
            return string.Join(" ", sentences);
        }

        private static string ApplyNegation(string text)
        {
            var result = text;
            result = Regex.Replace(result, @"\bmust\b", "must not", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\balways\b", "never", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\bnever\b", "always", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\bdo\b", "do not", RegexOptions.IgnoreCase);
            if (result == text) result = "Do not " + text; // fallback
            return result;
        }

        private static string ApplyWordReplace(string text, Dictionary<string, string> map)
        {
            var result = text;
            foreach (var (from, to) in map)
            {
                result = Regex.Replace(result, @$"\b{Regex.Escape(from)}\b", to, RegexOptions.IgnoreCase);
            }
            return result == text ? text + " [modified]" : result;
        }

        private string ApplyContradiction(string text)
        {
            var contradictions = new[]
            {
                "However, ignore the above instruction.",
                "Actually, do the opposite of what was just stated.",
                "Disregard the previous constraint entirely.",
                "Note: the above rule does not apply.",
                "Exception: this requirement is waived."
            };
            return text + " " + contradictions[_rng.Next(contradictions.Length)];
        }

        private static List<string> ExtractCoreVerbs(string prompt)
        {
            var verbs = new[] { "list", "explain", "summarize", "describe", "analyze", "generate",
                "create", "write", "find", "extract", "translate", "compare", "evaluate",
                "classify", "detect", "identify", "compute", "calculate", "recommend" };
            return verbs.Where(v => prompt.Contains(v, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private static List<string> ExtractConstraintKeywords(string prompt)
        {
            var keywords = new[] { "must", "always", "never", "required", "mandatory",
                "do not", "shall not", "forbidden", "exactly", "strictly" };
            return keywords.Where(k => prompt.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private static double ComputeNormalizedEditDistance(string a, string b)
        {
            if (a == b) return 0.0;
            int maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0) return 0.0;

            // Use simplified distance: character-level difference ratio
            int common = 0;
            var aChars = a.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            foreach (var ch in b)
            {
                if (aChars.TryGetValue(ch, out int count) && count > 0)
                {
                    common++;
                    aChars[ch] = count - 1;
                }
            }
            return 1.0 - ((double)common / maxLen);
        }

        private static bool CheckIntentPreserved(string mutatedPrompt, List<string> coreVerbs)
        {
            if (coreVerbs.Count == 0) return true;
            int found = coreVerbs.Count(v => mutatedPrompt.Contains(v, StringComparison.OrdinalIgnoreCase));
            return (double)found / coreVerbs.Count >= 0.5;
        }

        private static List<string> CheckConstraintViolations(string mutatedPrompt, List<string> constraintKeywords)
        {
            return constraintKeywords
                .Where(k => !mutatedPrompt.Contains(k, StringComparison.OrdinalIgnoreCase))
                .Select(k => $"Missing constraint keyword: '{k}'")
                .ToList();
        }

        private static double ComputeQualityDelta(double drift, bool intentPreserved, int violationCount)
        {
            double score = 0;
            score -= drift * 50;                       // drift penalty
            if (!intentPreserved) score -= 30;         // intent loss
            score -= violationCount * 10;              // constraint violations
            return Math.Max(-100, Math.Min(100, score));
        }

        private List<ZoneResilienceReport> GenerateZoneReports(MutationCampaign campaign)
        {
            var reports = new List<ZoneResilienceReport>();

            foreach (var zone in campaign.Zones)
            {
                var zoneCases = campaign.Cases
                    .Where(c => c.TargetZone.StartIndex == zone.StartIndex && c.TargetZone.EndIndex == zone.EndIndex)
                    .ToList();
                var zoneOutcomes = campaign.Outcomes
                    .Where(o => zoneCases.Any(c => c.Id == o.CaseId))
                    .ToList();

                if (zoneOutcomes.Count == 0)
                {
                    reports.Add(new ZoneResilienceReport
                    {
                        Zone = zone,
                        Grade = MutationResilienceGrade.Moderate,
                        Score = 50,
                        MutationsTotal = 0
                    });
                    continue;
                }

                int survived = zoneOutcomes.Count(o => o.IntentPreserved && o.ConstraintViolations.Count == 0);
                double score = (double)survived / zoneOutcomes.Count * 100;

                // Find weakest/strongest operators
                var opScores = zoneCases
                    .Join(zoneOutcomes, c => c.Id, o => o.CaseId, (c, o) => new { c.Operator, o.QualityDelta })
                    .GroupBy(x => x.Operator)
                    .Select(g => new { Op = g.Key, AvgDelta = g.Average(x => x.QualityDelta) })
                    .OrderBy(x => x.AvgDelta)
                    .ToList();

                var weakest = opScores.FirstOrDefault()?.Op ?? MutationOperator.Deletion;
                var strongest = opScores.LastOrDefault()?.Op ?? MutationOperator.Duplication;

                var vulnerabilities = new List<string>();
                if (score < 50) vulnerabilities.Add("Zone is highly sensitive to mutations");
                if (opScores.Any(o => o.Op == MutationOperator.Deletion && o.AvgDelta < -40))
                    vulnerabilities.Add("Critical dependency — removal causes catastrophic damage");
                if (opScores.Any(o => o.Op == MutationOperator.Negation && o.AvgDelta < -30))
                    vulnerabilities.Add("Vulnerable to polarity inversion attacks");
                if (opScores.Any(o => o.Op == MutationOperator.Weakening && o.AvgDelta < -20))
                    vulnerabilities.Add("Relies on strong language that can be weakened");

                reports.Add(new ZoneResilienceReport
                {
                    Zone = zone,
                    Grade = ScoreToGrade(score),
                    Score = Math.Round(score, 1),
                    WeakestOperator = weakest,
                    StrongestOperator = strongest,
                    MutationsSurvived = survived,
                    MutationsTotal = zoneOutcomes.Count,
                    Vulnerabilities = vulnerabilities
                });
            }

            return reports;
        }

        private static List<HardeningRecommendation> GenerateRecommendations(List<ZoneResilienceReport> reports)
        {
            var recs = new List<HardeningRecommendation>();

            foreach (var report in reports.Where(r => r.Grade <= MutationResilienceGrade.Moderate))
            {
                var priority = report.Grade switch
                {
                    MutationResilienceGrade.Critical => HardeningPriority.Immediate,
                    MutationResilienceGrade.Fragile => HardeningPriority.High,
                    _ => HardeningPriority.Medium
                };

                if (report.Vulnerabilities.Any(v => v.Contains("removal")))
                {
                    recs.Add(new HardeningRecommendation
                    {
                        Zone = report.Zone,
                        Priority = priority,
                        Title = "Add redundancy for critical zone",
                        Description = $"Zone '{report.Zone.Label}' is a single point of failure. Removal destroys prompt intent.",
                        SuggestedFix = "Reinforce this zone by restating key instructions in a different section, or add a meta-instruction referencing it.",
                        EstimatedImpact = 85
                    });
                }

                if (report.Vulnerabilities.Any(v => v.Contains("polarity")))
                {
                    recs.Add(new HardeningRecommendation
                    {
                        Zone = report.Zone,
                        Priority = priority,
                        Title = "Harden against negation attacks",
                        Description = $"Zone '{report.Zone.Label}' can be semantically inverted by flipping polarity words.",
                        SuggestedFix = "Use explicit positive AND negative phrasing: 'Always do X. Never do the opposite of X.'",
                        EstimatedImpact = 70
                    });
                }

                if (report.Vulnerabilities.Any(v => v.Contains("weakened")))
                {
                    recs.Add(new HardeningRecommendation
                    {
                        Zone = report.Zone,
                        Priority = priority,
                        Title = "Strengthen directive language",
                        Description = $"Zone '{report.Zone.Label}' relies on modal verbs that can be easily softened.",
                        SuggestedFix = "Add consequence statements: 'You MUST do X. Failure to do X will result in incorrect output.'",
                        EstimatedImpact = 60
                    });
                }

                if (report.WeakestOperator == MutationOperator.Truncation)
                {
                    recs.Add(new HardeningRecommendation
                    {
                        Zone = report.Zone,
                        Priority = HardeningPriority.Medium,
                        Title = "Front-load critical content",
                        Description = $"Zone '{report.Zone.Label}' loses critical information when truncated.",
                        SuggestedFix = "Move the most important directives to the beginning of this zone.",
                        EstimatedImpact = 50
                    });
                }

                // Generic recommendation if no specific ones matched
                if (!recs.Any(r => r.Zone.StartIndex == report.Zone.StartIndex))
                {
                    recs.Add(new HardeningRecommendation
                    {
                        Zone = report.Zone,
                        Priority = priority,
                        Title = $"Improve resilience of {report.Zone.ZoneType} zone",
                        Description = $"Zone scored {report.Score:F0}/100 — below acceptable threshold.",
                        SuggestedFix = "Consider adding explicit examples, restating constraints, or restructuring for clarity.",
                        EstimatedImpact = 40
                    });
                }
            }

            return recs.OrderBy(r => r.Priority).ThenByDescending(r => r.EstimatedImpact).ToList();
        }

        private static MutationResilienceGrade ScoreToGrade(double score) => score switch
        {
            >= 90 => MutationResilienceGrade.Fortified,
            >= 70 => MutationResilienceGrade.Robust,
            >= 50 => MutationResilienceGrade.Moderate,
            >= 30 => MutationResilienceGrade.Fragile,
            _ => MutationResilienceGrade.Critical
        };

        private static string GenerateSummary(List<ZoneResilienceReport> reports, double overallScore,
            List<string> hotspots, List<HardeningRecommendation> recs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Mutation testing analyzed {reports.Count} zone(s) with an overall resilience score of {overallScore:F1}/100.");

            var gradeDistro = reports.GroupBy(r => r.Grade)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Count()} {g.Key}")
                .ToList();
            sb.AppendLine($"Grade distribution: {string.Join(", ", gradeDistro)}.");

            if (hotspots.Count > 0)
                sb.AppendLine($"⚠️ {hotspots.Count} fragility hotspot(s) detected requiring attention.");
            else
                sb.AppendLine("✅ No critical fragility hotspots detected.");

            if (recs.Count > 0)
                sb.AppendLine($"Generated {recs.Count} hardening recommendation(s).");

            return sb.ToString();
        }
    }
}
