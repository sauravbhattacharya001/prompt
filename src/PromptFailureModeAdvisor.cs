namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>Risk appetite for <see cref="PromptFailureModeAdvisor"/>.</summary>
    public enum FailureRiskAppetite
    {
        /// <summary>Stricter scoring; appends SchedulePromptReview at C/D/F.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient; trims P3 actions when P0/P1 present.</summary>
        Aggressive,
    }

    /// <summary>Audience hint for failure-mode detection.</summary>
    public enum FailureAudience
    {
        /// <summary>General/consumer audience.</summary>
        General,
        /// <summary>Technical / engineering audience.</summary>
        Technical,
        /// <summary>Safety-critical / regulated audience.</summary>
        SafetyCritical,
    }

    /// <summary>Priority bucket for failure-mode findings and playbook actions.</summary>
    public enum FailurePriority
    {
        /// <summary>Blocking / immediate.</summary>
        P0,
        /// <summary>High priority.</summary>
        P1,
        /// <summary>Medium priority.</summary>
        P2,
        /// <summary>Advisory / fallback.</summary>
        P3,
    }

    /// <summary>Verdict ladder.</summary>
    public enum FailureVerdict
    {
        /// <summary>No findings worth acting on.</summary>
        Healthy,
        /// <summary>Minor risks; safe to send.</summary>
        MinorRisk,
        /// <summary>Moderate risks; consider revisions.</summary>
        ModerateRisk,
        /// <summary>High risks; revise before sending.</summary>
        HighRisk,
        /// <summary>Critical: do not send as-is.</summary>
        CriticalRisk,
    }

    /// <summary>Enumerated failure modes detected by <see cref="PromptFailureModeAdvisor"/>.</summary>
    public enum PromptFailureMode
    {
        /// <summary>Output likely vague / under-constrained.</summary>
        VagueOutput,
        /// <summary>Likely refusal due to flagged topic with no disclaimer policy.</summary>
        RefusalRisk,
        /// <summary>JSON requested but no schema or JSON instruction.</summary>
        FormatBreak,
        /// <summary>Fact claims without source / retrieval grounding.</summary>
        Hallucination,
        /// <summary>Open-ended prompt likely to produce verbose output.</summary>
        OverVerbosity,
        /// <summary>System-prompt leak risk or untrusted input without sanitizer.</summary>
        PromptLeak,
        /// <summary>Too many or contradictory instructions.</summary>
        InstructionDrift,
        /// <summary>Tools available but no tool-use contract.</summary>
        ToolMisuse,
        /// <summary>Long context with no truncation guidance.</summary>
        ContextOverflow,
        /// <summary>Conflicting persona declarations.</summary>
        AmbiguousPersona,
        /// <summary>Loop directives without termination condition.</summary>
        UnboundedRecursion,
        /// <summary>Streaming without error/fallback guidance.</summary>
        SilentFailure,
    }

    /// <summary>Optional environment context for failure-mode analysis.</summary>
    public sealed class FailureModeContext
    {
        /// <summary>Whether tools are available to the LLM.</summary>
        public bool Tools { get; init; }

        /// <summary>Whether a JSON response is required.</summary>
        public bool JsonRequired { get; init; }

        /// <summary>Whether the prompt feeds a long-context model.</summary>
        public bool LongContext { get; init; }

        /// <summary>Whether the prompt incorporates untrusted user/document input.</summary>
        public bool UntrustedInputs { get; init; }

        /// <summary>Whether the response will be streamed.</summary>
        public bool Streaming { get; init; }

        /// <summary>Audience hint.</summary>
        public FailureAudience Audience { get; init; } = FailureAudience.General;

        /// <summary>Risk appetite knob.</summary>
        public FailureRiskAppetite RiskAppetite { get; init; } = FailureRiskAppetite.Balanced;
    }

    /// <summary>A single failure-mode finding.</summary>
    public sealed class FailureModeFinding
    {
        /// <summary>Failure-mode code.</summary>
        public PromptFailureMode Mode { get; internal set; }

        /// <summary>Severity 0..100 (post appetite modulation).</summary>
        public int Severity { get; internal set; }

        /// <summary>Priority bucket.</summary>
        public FailurePriority Priority { get; internal set; }

        /// <summary>Short reason text.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Optional preview / snippet from the prompt.</summary>
        public string Snippet { get; internal set; } = "";
    }

    /// <summary>A single playbook action.</summary>
    public sealed class FailurePlaybookAction
    {
        /// <summary>Stable id.</summary>
        public string Id { get; internal set; } = "";

        /// <summary>Priority bucket.</summary>
        public FailurePriority Priority { get; internal set; }

        /// <summary>Short label.</summary>
        public string Label { get; internal set; } = "";

        /// <summary>Reason / rationale.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Owner role.</summary>
        public string Owner { get; internal set; } = "prompt_author";

        /// <summary>Blast radius 1-5.</summary>
        public int BlastRadius { get; internal set; } = 1;

        /// <summary>Reversibility tier (low/medium/high).</summary>
        public string Reversibility { get; internal set; } = "high";

        /// <summary>Failure modes this action addresses (sorted).</summary>
        public IReadOnlyList<string> RelatedFindings { get; internal set; } = Array.Empty<string>();

        /// <summary>Suggested concrete value or replacement.</summary>
        public string? SuggestedValue { get; internal set; }
    }

    /// <summary>Result of <see cref="PromptFailureModeAdvisor.Analyze"/>.</summary>
    public sealed class FailureModeReport
    {
        /// <summary>Verdict.</summary>
        public FailureVerdict Verdict { get; internal set; }

        /// <summary>Letter grade.</summary>
        public string Grade { get; internal set; } = "A";

        /// <summary>Overall risk 0..100.</summary>
        public int OverallRisk { get; internal set; }

        /// <summary>Findings (ordered by priority asc then severity desc).</summary>
        public IReadOnlyList<FailureModeFinding> Findings { get; internal set; } = Array.Empty<FailureModeFinding>();

        /// <summary>Playbook actions (priority asc then id asc).</summary>
        public IReadOnlyList<FailurePlaybookAction> Playbook { get; internal set; } = Array.Empty<FailurePlaybookAction>();

        /// <summary>Insights (sorted).</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();

        /// <summary>Original prompt augmented with a TODO safeguards block (P0/P1 actions only).</summary>
        public string HardenedDraft { get; internal set; } = "";

        /// <summary>One-line headline.</summary>
        public string Headline { get; internal set; } = "";
    }

    /// <summary>
    /// 10th agentic sibling. Pre-flight failure-mode advisor: predicts likely failure modes
    /// for a prompt given an optional <see cref="FailureModeContext"/>, scores them, and
    /// emits a deduplicated playbook referencing companion advisors. Deterministic, no I/O.
    /// </summary>
    public sealed class PromptFailureModeAdvisor
    {
        private static readonly string[] GenerativeVerbs = {
            "generate", "write", "produce", "explain", "describe", "compose", "draft",
        };
        private static readonly string[] FormatHints = {
            "json", "yaml", "xml", "csv", "markdown", "bullet", "table", "schema", "format:",
            "respond with", "<=", "words", "characters",
        };
        private static readonly string[] LengthCaps = {
            "brief", "concise", "short", "<=", "no more than", "at most", "limit", "max ",
            "maximum", " words", " bullets", "tl;dr",
        };
        private static readonly string[] FlaggedTopics = {
            "medical advice", "legal advice", "financial advice", "weapons", "exploit",
            "jailbreak", "malware",
        };
        private static readonly string[] DisclaimerLanguage = {
            "not a substitute", "consult a professional", "for informational purposes",
            "i cannot provide", "refuse politely", "decline", "policy",
        };
        private static readonly string[] FactClaimMarkers = {
            "list ", "name ", "who is", "who was", "when did", "where is", "history of",
            "what year", "how many",
        };
        private static readonly string[] SourceMarkers = {
            "source", "citation", "cite", "reference", "retriev", "according to",
            "ground truth", "provided document", "based on the context",
        };
        private static readonly string[] PersonaMarkers = {
            "you are ", "act as ", "you act as", "you're a ", "you're an ",
        };
        private static readonly string[] RecursionMarkers = {
            "keep going until", "repeat until", "again and again",
            "until satisfied", "loop until",
        };
        private static readonly string[] TerminationMarkers = {
            "stop when", "until you reach", "max iterations", "at most",
            "iterations", "until done", "terminate when",
        };
        private static readonly string[] ToolMarkers = {
            "tool", "function call", "invoke", "use the api", "call the",
        };
        private static readonly string[] TruncationGuidance = {
            "summarize first", "if you cannot fit", "if context overflows",
            "truncate", "chunk", "if too long",
        };
        private static readonly string[] StreamingFallback = {
            "if you fail", "on error", "fallback", "retry", "graceful",
        };
        private static readonly Regex ImperativeRegex = new(@"(^|[\.\!\?\n])\s*(do|write|generate|list|return|provide|use|avoid|ensure|include|exclude|format|respond|output|reply)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex JsonFenceRegex = new(@"```json\b|""\s*schema""\s*:|\bjson\s+schema\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Analyze a prompt and emit a <see cref="FailureModeReport"/>.</summary>
        public FailureModeReport Analyze(string prompt, FailureModeContext? context = null)
        {
            if (prompt is null) throw new ArgumentNullException(nameof(prompt));
            context ??= new FailureModeContext();
            var lower = prompt.ToLowerInvariant();
            var raw = new List<FailureModeFinding>();

            // 1. VagueOutput
            if (Contains(lower, GenerativeVerbs)
                && !Contains(lower, FormatHints)
                && !Contains(lower, LengthCaps)
                && !lower.Contains("example")
                && !lower.Contains("for instance"))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.VagueOutput,
                    Severity = 55,
                    Reason = "Generative verb present without format/length/example hint.",
                    Snippet = Preview(prompt, FirstMatch(lower, GenerativeVerbs)),
                });
            }

            // 2. RefusalRisk
            if (Contains(lower, FlaggedTopics)
                && context.Audience != FailureAudience.SafetyCritical
                && !Contains(lower, DisclaimerLanguage))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.RefusalRisk,
                    Severity = 60,
                    Reason = "Flagged topic without disclaimer / refusal-policy language.",
                    Snippet = Preview(prompt, FirstMatch(lower, FlaggedTopics)),
                });
            }

            // 3. FormatBreak (always evaluated; suppressed by explicit JSON fence/schema)
            var schemaExplicit = JsonFenceRegex.IsMatch(prompt) || lower.Contains("schema:") || lower.Contains("respond with valid json");
            if (!schemaExplicit && (context.JsonRequired || lower.Contains("json")))
            {
                var hasJsonInstr = lower.Contains("respond with") && lower.Contains("json");
                if (context.JsonRequired && !hasJsonInstr)
                {
                    raw.Add(new FailureModeFinding
                    {
                        Mode = PromptFailureMode.FormatBreak,
                        Severity = 70,
                        Reason = "JSON output required but no schema / explicit 'respond with valid JSON' instruction.",
                        Snippet = Preview(prompt, 0),
                    });
                }
            }

            // 4. Hallucination
            if (Contains(lower, FactClaimMarkers) && !Contains(lower, SourceMarkers))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.Hallucination,
                    Severity = 65,
                    Reason = "Fact-claim verbs without source/retrieval grounding.",
                    Snippet = Preview(prompt, FirstMatch(lower, FactClaimMarkers)),
                });
            }

            // 5. OverVerbosity
            if (context.Audience == FailureAudience.General
                && !Contains(lower, LengthCaps)
                && Contains(lower, GenerativeVerbs))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.OverVerbosity,
                    Severity = 35,
                    Reason = "Open-ended generative prompt with no length cap for a general audience.",
                });
            }

            // 6. PromptLeak
            var hasSystemMarker = lower.Contains("do not reveal") || lower.Contains("never share these instructions") || lower.Contains("do not disclose your instructions");
            if ((hasSystemMarker && !lower.Contains("defense") && !lower.Contains("guard"))
                || (context.UntrustedInputs && !lower.Contains("sanitiz") && !lower.Contains("strip ") && !lower.Contains("escape ")))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.PromptLeak,
                    Severity = 75,
                    Reason = context.UntrustedInputs
                        ? "Untrusted inputs present with no sanitizer / escape mention."
                        : "System-prompt protective markers present but no defense/guard reference.",
                    Snippet = Preview(prompt, 0),
                });
            }

            // 7. InstructionDrift
            var imperativeCount = ImperativeRegex.Matches(prompt).Count;
            var contradictoryPairs = new[]
            {
                ("formal", "casual"),
                ("brief", "detailed"),
                ("only", "also include"),
                ("never", "always"),
            };
            var contradictions = contradictoryPairs.Where(p => lower.Contains(p.Item1) && lower.Contains(p.Item2)).ToList();
            if (imperativeCount >= 8 || contradictions.Count > 0)
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.InstructionDrift,
                    Severity = 50,
                    Reason = contradictions.Count > 0
                        ? $"Contradictory instruction pair: '{contradictions[0].Item1}' vs '{contradictions[0].Item2}'."
                        : $"{imperativeCount} imperative sentences detected.",
                });
            }

            // 8. ToolMisuse
            if (context.Tools
                && !Contains(lower, ToolMarkers)
                && !lower.Contains("when to call")
                && !lower.Contains("only when"))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.ToolMisuse,
                    Severity = 60,
                    Reason = "Tools enabled but no tool-use contract / invocation guidance.",
                });
            }

            // 9. ContextOverflow
            if (context.LongContext
                && !Contains(lower, TruncationGuidance))
            {
                var endsTruncated = prompt.TrimEnd().EndsWith("...", StringComparison.Ordinal) || prompt.TrimEnd().EndsWith("[truncated]", StringComparison.OrdinalIgnoreCase);
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.ContextOverflow,
                    Severity = endsTruncated ? 50 : 40,
                    Reason = endsTruncated
                        ? "Prompt ends with truncation indicator and no overflow guidance."
                        : "Long-context model with no 'summarize first' / overflow guidance.",
                });
            }

            // 10. AmbiguousPersona
            int personaCount = 0;
            foreach (var marker in PersonaMarkers)
            {
                int idx = 0;
                while ((idx = lower.IndexOf(marker, idx, StringComparison.Ordinal)) >= 0) { personaCount++; idx += marker.Length; }
            }
            if (personaCount >= 2 || (lower.Contains("act as both ") && lower.Contains(" and ")))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.AmbiguousPersona,
                    Severity = 45,
                    Reason = $"{personaCount} persona-declaration markers detected.",
                });
            }

            // 11. UnboundedRecursion
            if (Contains(lower, RecursionMarkers) && !Contains(lower, TerminationMarkers))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.UnboundedRecursion,
                    Severity = 55,
                    Reason = "Loop directive without termination condition.",
                    Snippet = Preview(prompt, FirstMatch(lower, RecursionMarkers)),
                });
            }

            // 12. SilentFailure
            if (context.Streaming && !Contains(lower, StreamingFallback))
            {
                raw.Add(new FailureModeFinding
                {
                    Mode = PromptFailureMode.SilentFailure,
                    Severity = 30,
                    Reason = "Streaming response with no error/fallback handling guidance.",
                });
            }

            // Priorities
            foreach (var f in raw)
            {
                f.Priority = ComputePriority(f, context);
            }

            // Score
            double appetiteMult = context.RiskAppetite switch
            {
                FailureRiskAppetite.Cautious => 1.15,
                FailureRiskAppetite.Aggressive => 0.85,
                _ => 1.0,
            };
            int overall = 0;
            if (raw.Count > 0)
            {
                var topSev = raw.Max(f => f.Severity);
                var restSum = raw.Sum(f => f.Severity) - topSev;
                overall = (int)Math.Round((topSev + 0.4 * Math.Min(restSum, 60)) * appetiteMult);
                overall = Math.Clamp(overall, 0, 100);
            }

            // Verdict
            FailureVerdict verdict;
            if (overall < 25) verdict = FailureVerdict.Healthy;
            else if (overall < 45) verdict = FailureVerdict.MinorRisk;
            else if (overall < 65) verdict = FailureVerdict.ModerateRisk;
            else if (overall < 80) verdict = FailureVerdict.HighRisk;
            else verdict = FailureVerdict.CriticalRisk;

            // Appetite shift +/- 5 on score boundaries
            if (context.RiskAppetite == FailureRiskAppetite.Cautious && verdict != FailureVerdict.CriticalRisk)
            {
                if (overall >= 20 && verdict == FailureVerdict.Healthy) verdict = FailureVerdict.MinorRisk;
                else if (overall >= 40 && verdict == FailureVerdict.MinorRisk) verdict = FailureVerdict.ModerateRisk;
                else if (overall >= 60 && verdict == FailureVerdict.ModerateRisk) verdict = FailureVerdict.HighRisk;
                else if (overall >= 75 && verdict == FailureVerdict.HighRisk) verdict = FailureVerdict.CriticalRisk;
            }
            else if (context.RiskAppetite == FailureRiskAppetite.Aggressive)
            {
                if (overall < 30 && verdict == FailureVerdict.MinorRisk) verdict = FailureVerdict.Healthy;
                else if (overall < 50 && verdict == FailureVerdict.ModerateRisk) verdict = FailureVerdict.MinorRisk;
                else if (overall < 70 && verdict == FailureVerdict.HighRisk) verdict = FailureVerdict.ModerateRisk;
            }

            // Force CriticalRisk on hard fails
            var forceCritical = raw.Any(f =>
                (f.Mode == PromptFailureMode.FormatBreak && context.JsonRequired)
                || (f.Mode == PromptFailureMode.PromptLeak && context.UntrustedInputs));
            if (forceCritical) verdict = FailureVerdict.CriticalRisk;

            // Grade
            string grade = verdict == FailureVerdict.CriticalRisk
                ? "F"
                : overall <= 20 ? "A"
                : overall <= 40 ? "B"
                : overall <= 60 ? "C"
                : overall <= 80 ? "D"
                : "F";

            // Sort findings priority asc then severity desc then mode name
            var findings = raw
                .OrderBy(f => (int)f.Priority)
                .ThenByDescending(f => f.Severity)
                .ThenBy(f => f.Mode.ToString(), StringComparer.Ordinal)
                .ToList();

            // Playbook
            var playbook = BuildPlaybook(findings, context, grade);

            // Insights
            var insights = BuildInsights(findings, context);

            // Hardened draft
            var hardened = BuildHardenedDraft(prompt, playbook);

            var headline = $"{verdict} (Grade {grade}, Risk {overall})";

            return new FailureModeReport
            {
                Verdict = verdict,
                Grade = grade,
                OverallRisk = overall,
                Findings = findings,
                Playbook = playbook,
                Insights = insights,
                HardenedDraft = hardened,
                Headline = headline,
            };
        }

        /// <summary>Render the report as plain text.</summary>
        public string ToText(FailureModeReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var sb = new StringBuilder();
            sb.AppendLine(report.Headline);
            sb.AppendLine();
            sb.AppendLine($"Findings ({report.Findings.Count}):");
            foreach (var f in report.Findings)
            {
                sb.AppendLine($"  - [{f.Priority}] {f.Mode} (sev {f.Severity}): {f.Reason}");
            }
            sb.AppendLine();
            sb.AppendLine($"Playbook ({report.Playbook.Count}):");
            foreach (var a in report.Playbook)
            {
                sb.AppendLine($"  - [{a.Priority}] {a.Id}: {a.Label} — {a.Reason}");
                if (!string.IsNullOrEmpty(a.SuggestedValue))
                    sb.AppendLine($"      suggestion: {a.SuggestedValue}");
            }
            sb.AppendLine();
            sb.AppendLine($"Insights ({report.Insights.Count}):");
            foreach (var i in report.Insights) sb.AppendLine($"  - {i}");
            return sb.ToString();
        }

        /// <summary>Render the report as Markdown with 4 sections.</summary>
        public string ToMarkdown(FailureModeReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var sb = new StringBuilder();
            sb.AppendLine($"# PromptFailureModeAdvisor — {report.Headline}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Verdict: **{report.Verdict}**");
            sb.AppendLine($"- Grade: **{report.Grade}**");
            sb.AppendLine($"- OverallRisk: **{report.OverallRisk}**");
            sb.AppendLine();
            sb.AppendLine("## Findings");
            if (report.Findings.Count == 0) sb.AppendLine("_None_");
            foreach (var f in report.Findings)
                sb.AppendLine($"- **{f.Mode}** ({f.Priority}, sev {f.Severity}): {f.Reason}");
            sb.AppendLine();
            sb.AppendLine("## Playbook");
            if (report.Playbook.Count == 0) sb.AppendLine("_None_");
            foreach (var a in report.Playbook)
            {
                sb.AppendLine($"- **{a.Id}** ({a.Priority}, owner: {a.Owner}, blast: {a.BlastRadius}, reversibility: {a.Reversibility}) — {a.Label}");
                sb.AppendLine($"  - Reason: {a.Reason}");
                if (!string.IsNullOrEmpty(a.SuggestedValue))
                    sb.AppendLine($"  - Suggestion: {a.SuggestedValue}");
            }
            sb.AppendLine();
            sb.AppendLine("## Insights");
            foreach (var i in report.Insights) sb.AppendLine($"- {i}");
            return sb.ToString();
        }

        /// <summary>Render the report as deterministic JSON.</summary>
        public string ToJson(FailureModeReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var doc = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["findings"] = report.Findings.Select(f => (object)new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode"] = f.Mode.ToString(),
                    ["priority"] = f.Priority.ToString(),
                    ["reason"] = f.Reason,
                    ["severity"] = f.Severity,
                    ["snippet"] = f.Snippet ?? "",
                }).ToList(),
                ["grade"] = report.Grade,
                ["hardenedDraft"] = report.HardenedDraft,
                ["headline"] = report.Headline,
                ["insights"] = report.Insights.ToList(),
                ["overallRisk"] = report.OverallRisk,
                ["playbook"] = report.Playbook.Select(a => (object)new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["blastRadius"] = a.BlastRadius,
                    ["id"] = a.Id,
                    ["label"] = a.Label,
                    ["owner"] = a.Owner,
                    ["priority"] = a.Priority.ToString(),
                    ["reason"] = a.Reason,
                    ["relatedFindings"] = a.RelatedFindings.ToList(),
                    ["reversibility"] = a.Reversibility,
                    ["suggestedValue"] = a.SuggestedValue,
                }).ToList(),
                ["verdict"] = report.Verdict.ToString(),
            };
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }

        private static FailurePriority ComputePriority(FailureModeFinding f, FailureModeContext ctx)
        {
            if (f.Mode == PromptFailureMode.PromptLeak && ctx.UntrustedInputs) return FailurePriority.P0;
            if (f.Mode == PromptFailureMode.FormatBreak && ctx.JsonRequired) return FailurePriority.P0;
            if (f.Mode == PromptFailureMode.ToolMisuse && ctx.Tools && f.Severity >= 60) return FailurePriority.P0;
            if (f.Severity >= 55) return FailurePriority.P1;
            if (f.Severity >= 35) return FailurePriority.P2;
            return FailurePriority.P3;
        }

        private static IReadOnlyList<FailurePlaybookAction> BuildPlaybook(
            IReadOnlyList<FailureModeFinding> findings,
            FailureModeContext ctx,
            string grade)
        {
            var byMode = findings.ToLookup(f => f.Mode);
            var actions = new List<FailurePlaybookAction>();

            void Add(string id, FailurePriority p, string label, string reason, string owner,
                     int blast, string rev, IEnumerable<PromptFailureMode> related, string? suggested = null)
            {
                actions.Add(new FailurePlaybookAction
                {
                    Id = id,
                    Priority = p,
                    Label = label,
                    Reason = reason,
                    Owner = owner,
                    BlastRadius = blast,
                    Reversibility = rev,
                    RelatedFindings = related.Select(r => r.ToString()).OrderBy(s => s, StringComparer.Ordinal).ToList(),
                    SuggestedValue = suggested,
                });
            }

            if (byMode[PromptFailureMode.PromptLeak].Any() || ctx.UntrustedInputs)
            {
                Add("RunSanitizerBeforePrompt", FailurePriority.P0, "Run PromptSanitizer before prompt",
                    "Untrusted input must be sanitized before being included in the prompt.",
                    "security_reviewer", 3, "high", new[] { PromptFailureMode.PromptLeak },
                    "PromptSanitizer.Sanitize(userInput)");
            }
            if (byMode[PromptFailureMode.FormatBreak].Any() && ctx.JsonRequired)
            {
                Add("EnforceJsonSchema", FailurePriority.P0, "Enforce JSON schema via PromptOutputValidator",
                    "JSON output is required; attach a schema or grammar contract.",
                    "platform", 3, "high", new[] { PromptFailureMode.FormatBreak },
                    "PromptOutputValidator.WithSchema(schema) + PromptGrammarValidator");
            }
            if (byMode[PromptFailureMode.ToolMisuse].Any() && ctx.Tools)
            {
                Add("AttachToolContract", FailurePriority.P0, "Attach a tool-use contract",
                    "Tools are available but no invocation contract is provided.",
                    "prompt_author", 2, "high", new[] { PromptFailureMode.ToolMisuse },
                    "See PromptToolUseContractAdvisor");
            }
            if (byMode[PromptFailureMode.Hallucination].Any())
            {
                Add("AddCitationRequirement", FailurePriority.P1, "Require citations for fact claims",
                    "Fact-claim verbs without source hints raise hallucination risk.",
                    "prompt_author", 2, "high", new[] { PromptFailureMode.Hallucination },
                    "Add: 'Cite every fact with a source URL or say UNKNOWN.'");
            }
            if (byMode[PromptFailureMode.RefusalRisk].Any())
            {
                Add("ClarifyRefusalPolicy", FailurePriority.P1, "Clarify refusal / disclaimer policy",
                    "Flagged topics without disclaimer policy may trigger refusal.",
                    "prompt_author", 2, "high", new[] { PromptFailureMode.RefusalRisk },
                    "Add a 'When you cannot answer, respond with {reason, alternative_action}' clause.");
            }
            if (byMode[PromptFailureMode.InstructionDrift].Any())
            {
                Add("ResolveContradictoryInstructions", FailurePriority.P1, "Resolve contradictory instructions",
                    "Too many or contradictory imperatives reduce instruction-following reliability.",
                    "prompt_author", 2, "high", new[] { PromptFailureMode.InstructionDrift },
                    "Run PromptInstructionConflictAdvisor and consolidate to <=6 imperatives.");
            }
            if (byMode[PromptFailureMode.VagueOutput].Any())
            {
                Add("AddOutputContract", FailurePriority.P1, "Add an explicit output contract",
                    "Generative verb without format/length hint will produce vague output.",
                    "prompt_author", 1, "high", new[] { PromptFailureMode.VagueOutput },
                    "Specify format, length cap, and one example.");
            }
            if (byMode[PromptFailureMode.OverVerbosity].Any())
            {
                Add("AddLengthCap", FailurePriority.P2, "Add a length cap",
                    "Open-ended prompt for general audience tends to produce long outputs.",
                    "prompt_author", 1, "high", new[] { PromptFailureMode.OverVerbosity },
                    "Limit to 5 bullet points or <=120 words.");
            }
            if (byMode[PromptFailureMode.FormatBreak].Any() && !ctx.JsonRequired)
            {
                Add("AddSchemaExample", FailurePriority.P2, "Add a schema example",
                    "Mentions JSON without an explicit schema or example.",
                    "prompt_author", 1, "high", new[] { PromptFailureMode.FormatBreak });
            }
            if (byMode[PromptFailureMode.UnboundedRecursion].Any())
            {
                Add("AddTerminationCondition", FailurePriority.P2, "Add a termination condition",
                    "Loop directive must specify a stop condition or iteration cap.",
                    "prompt_author", 1, "high", new[] { PromptFailureMode.UnboundedRecursion },
                    "Add: 'Stop after at most N iterations or when X is reached.'");
            }
            if (byMode[PromptFailureMode.AmbiguousPersona].Any())
            {
                Add("ConsolidatePersona", FailurePriority.P2, "Consolidate persona declarations",
                    "Multiple persona declarations confuse role adherence.",
                    "prompt_author", 1, "high", new[] { PromptFailureMode.AmbiguousPersona });
            }
            if (byMode[PromptFailureMode.ContextOverflow].Any())
            {
                Add("AddTruncationGuidance", FailurePriority.P2, "Add truncation guidance",
                    "Long context with no overflow strategy may lead to silent loss.",
                    "prompt_author", 1, "high", new[] { PromptFailureMode.ContextOverflow },
                    "Add: 'If you cannot fit, summarize older sections first.'");
            }
            if (byMode[PromptFailureMode.SilentFailure].Any())
            {
                Add("AddStreamingFallback", FailurePriority.P3, "Add streaming fallback guidance",
                    "Streaming responses should specify a fallback or retry behaviour.",
                    "platform", 1, "high", new[] { PromptFailureMode.SilentFailure });
            }

            // Cautious second-review
            if (ctx.RiskAppetite == FailureRiskAppetite.Cautious && (grade == "C" || grade == "D" || grade == "F"))
            {
                Add("SchedulePromptReview", FailurePriority.P2, "Schedule a security/prompt review",
                    "Cautious appetite + non-passing grade triggers a peer review.",
                    "security_reviewer", 2, "high", Array.Empty<PromptFailureMode>());
            }

            // Aggressive trims P3 when P0/P1 exist
            if (ctx.RiskAppetite == FailureRiskAppetite.Aggressive
                && actions.Any(a => a.Priority == FailurePriority.P0 || a.Priority == FailurePriority.P1))
            {
                actions.RemoveAll(a => a.Priority == FailurePriority.P3);
            }

            // Fallback
            if (actions.Count == 0)
            {
                Add("PromptReady", FailurePriority.P3, "Prompt looks ready",
                    "No actionable failure modes detected.",
                    "prompt_author", 1, "high", Array.Empty<PromptFailureMode>());
            }

            return actions
                .OrderBy(a => (int)a.Priority)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static IReadOnlyList<string> BuildInsights(IReadOnlyList<FailureModeFinding> findings, FailureModeContext ctx)
        {
            var insights = new SortedSet<string>(StringComparer.Ordinal);
            var modes = findings.Select(f => f.Mode).ToHashSet();

            if (modes.Contains(PromptFailureMode.FormatBreak) && ctx.JsonRequired) insights.Add("JSON_FORMAT_GUARANTEE_MISSING");
            if (modes.Contains(PromptFailureMode.ToolMisuse) && ctx.Tools) insights.Add("TOOL_CONTRACT_MISSING");
            if (modes.Contains(PromptFailureMode.PromptLeak) && ctx.UntrustedInputs) insights.Add("UNTRUSTED_INPUT_UNPROTECTED");
            if (findings.Count(f => f.Severity >= 55) >= 3) insights.Add("MULTIPLE_HIGH_RISK_MODES");
            if (modes.Contains(PromptFailureMode.RefusalRisk)) insights.Add("LIKELY_REFUSAL_PATTERNS");
            if (modes.Contains(PromptFailureMode.Hallucination)) insights.Add("HALLUCINATION_PRONE");
            if (findings.Count == 0) insights.Add("HEALTHY_PROMPT");

            return insights.Count == 0 ? new[] { "REVIEW_RECOMMENDED" } : insights.ToList();
        }

        private static string BuildHardenedDraft(string prompt, IReadOnlyList<FailurePlaybookAction> playbook)
        {
            var critical = playbook
                .Where(a => a.Priority == FailurePriority.P0 || a.Priority == FailurePriority.P1)
                .ToList();
            if (critical.Count == 0) return prompt;

            var sb = new StringBuilder(prompt);
            if (!prompt.EndsWith("\n", StringComparison.Ordinal)) sb.Append('\n');
            sb.AppendLine();
            sb.AppendLine("# Failure-mode safeguards");
            foreach (var a in critical)
            {
                var sv = string.IsNullOrEmpty(a.SuggestedValue) ? a.Label : $"{a.Label}: {a.SuggestedValue}";
                sb.AppendLine($"- [ ] {sv}");
            }
            return sb.ToString();
        }

        private static bool Contains(string haystack, IEnumerable<string> needles)
        {
            foreach (var n in needles)
                if (haystack.IndexOf(n, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static int FirstMatch(string haystack, IEnumerable<string> needles)
        {
            int best = -1;
            foreach (var n in needles)
            {
                var idx = haystack.IndexOf(n, StringComparison.Ordinal);
                if (idx >= 0 && (best == -1 || idx < best)) best = idx;
            }
            return Math.Max(best, 0);
        }

        private static string Preview(string source, int start)
        {
            if (string.IsNullOrEmpty(source)) return "";
            start = Math.Clamp(start, 0, Math.Max(source.Length - 1, 0));
            var len = Math.Min(80, source.Length - start);
            return source.Substring(start, len).Replace('\n', ' ').Trim();
        }
    }
}
