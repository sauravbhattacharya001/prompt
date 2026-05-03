namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    // ────────────────────────────────────────────
    //  PromptSelfHealer – Autonomous Prompt Repair Engine
    //
    //  Monitors prompt execution results, detects failure patterns
    //  (refusals, hallucinations, format violations, truncations,
    //  repetition loops, off-topic drift), diagnoses root causes,
    //  and auto-generates corrective patches. Maintains a healing
    //  journal with before/after comparisons and success tracking.
    //  Supports configurable healing policies and escalation.
    // ────────────────────────────────────────────

    /// <summary>Category of prompt failure detected.</summary>
    public enum HealerFailureMode
    {
        /// <summary>Model refused to answer (safety filter, policy).</summary>
        Refusal,
        /// <summary>Output contains fabricated facts or citations.</summary>
        Hallucination,
        /// <summary>Output doesn't match expected format (JSON, list, etc.).</summary>
        FormatViolation,
        /// <summary>Output was cut off mid-sentence.</summary>
        Truncation,
        /// <summary>Output contains repeated phrases or loops.</summary>
        RepetitionLoop,
        /// <summary>Response drifted away from the original topic.</summary>
        OffTopicDrift,
        /// <summary>Output is too vague or generic to be useful.</summary>
        LowSpecificity,
        /// <summary>Output contradicts prior context or instructions.</summary>
        Contradiction,
        /// <summary>Model ignored explicit constraints.</summary>
        ConstraintViolation,
        /// <summary>Response latency or token usage exceeded threshold.</summary>
        PerformanceDegradation
    }

    /// <summary>Severity of the failure.</summary>
    public enum FailureSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>Status of a healing attempt.</summary>
    public enum HealingStatus
    {
        Pending,
        Applied,
        Verified,
        Failed,
        Reverted
    }

    /// <summary>Policy controlling how aggressive healing should be.</summary>
    public enum HealingPolicy
    {
        /// <summary>Only suggest patches; never auto-apply.</summary>
        SuggestOnly,
        /// <summary>Auto-apply low-risk patches; suggest high-risk ones.</summary>
        Conservative,
        /// <summary>Auto-apply all patches with revert capability.</summary>
        Aggressive,
        /// <summary>Auto-apply and escalate repeated failures.</summary>
        Adaptive
    }

    /// <summary>Type of patch operation applied to the prompt.</summary>
    public enum PatchOperation
    {
        /// <summary>Prepend text to the prompt.</summary>
        Prepend,
        /// <summary>Append text to the prompt.</summary>
        Append,
        /// <summary>Replace a matched section.</summary>
        Replace,
        /// <summary>Insert a constraint clause.</summary>
        InsertConstraint,
        /// <summary>Wrap the prompt in a structured template.</summary>
        WrapTemplate,
        /// <summary>Remove a problematic section.</summary>
        Remove
    }

    /// <summary>A detected failure in a prompt execution.</summary>
    public class PromptFailure
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public HealerFailureMode Mode { get; set; }
        public FailureSeverity Severity { get; set; }
        public string PromptText { get; set; } = "";
        public string OutputText { get; set; } = "";
        public string Evidence { get; set; } = "";
        public double Confidence { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>A corrective patch to fix a prompt failure.</summary>
    public class HealingPatch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string FailureId { get; set; } = "";
        public HealerFailureMode TargetMode { get; set; }
        public PatchOperation Operation { get; set; }
        public string Description { get; set; } = "";
        public string OriginalText { get; set; } = "";
        public string PatchedText { get; set; } = "";
        public string PatchContent { get; set; } = "";
        public double RiskScore { get; set; }
        public HealingStatus Status { get; set; } = HealingStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AppliedAt { get; set; }
        public bool? VerifiedSuccess { get; set; }
    }

    /// <summary>Record in the healing journal tracking a full heal cycle.</summary>
    public class HealingRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public PromptFailure Failure { get; set; } = new();
        public HealingPatch Patch { get; set; } = new();
        public string PromptBefore { get; set; } = "";
        public string PromptAfter { get; set; } = "";
        public int AttemptNumber { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Escalation event when repeated failures can't be healed.</summary>
    public class EscalationEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public HealerFailureMode Mode { get; set; }
        public int FailureCount { get; set; }
        public int HealAttempts { get; set; }
        public string Recommendation { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Health summary for a monitored prompt.</summary>
    public class PromptHealthSummary
    {
        public int TotalExecutions { get; set; }
        public int TotalFailures { get; set; }
        public int TotalHealed { get; set; }
        public int TotalEscalations { get; set; }
        public double SuccessRate => TotalExecutions == 0 ? 1.0 : 1.0 - (double)TotalFailures / TotalExecutions;
        public double HealRate => TotalFailures == 0 ? 1.0 : (double)TotalHealed / TotalFailures;
        public string HealthGrade => SuccessRate >= 0.95 ? "A" : SuccessRate >= 0.85 ? "B" : SuccessRate >= 0.70 ? "C" : SuccessRate >= 0.50 ? "D" : "F";
        public Dictionary<HealerFailureMode, int> FailureBreakdown { get; set; } = new();
        public List<string> ProactiveRecommendations { get; set; } = new();
    }

    /// <summary>
    /// Autonomous prompt repair engine. Detects failures in prompt outputs,
    /// diagnoses root causes, generates corrective patches, and maintains
    /// a healing journal. Supports configurable policies and escalation.
    /// </summary>
    public class PromptSelfHealer
    {
        private readonly List<PromptFailure> _failures = new();
        private readonly List<HealingRecord> _journal = new();
        private readonly List<EscalationEvent> _escalations = new();
        private readonly Dictionary<string, int> _promptExecutionCount = new();
        private readonly Dictionary<string, int> _promptFailureCount = new();
        private readonly Random _rng = new();

        public HealingPolicy Policy { get; set; } = HealingPolicy.Conservative;
        public int EscalationThreshold { get; set; } = 3;
        public int MaxHealAttempts { get; set; } = 5;

        public IReadOnlyList<PromptFailure> Failures => _failures;
        public IReadOnlyList<HealingRecord> Journal => _journal;
        public IReadOnlyList<EscalationEvent> Escalations => _escalations;

        // ── Failure Detection ──────────────────────────

        private static readonly Regex RefusalPattern = new(
            @"(?i)(I\s+can(?:'t|not)|I\s+am\s+unable|I\s+apologize|as\s+an\s+AI|I\s+cannot\s+help|I\s+must\s+decline|not\s+appropriate)",
            RegexOptions.Compiled);

        private static readonly Regex RepetitionPattern = new(
            @"(.{20,}?)\1{2,}",
            RegexOptions.Compiled);

        private static readonly Regex TruncationPattern = new(
            @"[^.!?\s]\s*$",
            RegexOptions.Compiled);

        private static readonly Regex HallucinationMarkers = new(
            @"(?i)(according\s+to\s+(?:my|internal)|as\s+of\s+my\s+(?:last|training)|I\s+(?:recall|remember)\s+that|published\s+in\s+\d{4}(?!\s*[\.\)]))",
            RegexOptions.Compiled);

        /// <summary>
        /// Analyze a prompt execution and detect any failure modes.
        /// Delegates to individual detector methods for each failure category.
        /// </summary>
        public List<PromptFailure> Detect(string prompt, string output, Dictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt text is required.", nameof(prompt));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            var key = ComputePromptKey(prompt);
            _promptExecutionCount[key] = _promptExecutionCount.GetValueOrDefault(key) + 1;

            var meta = metadata ?? new Dictionary<string, string>();
            var detected = new List<PromptFailure>();

            DetectRefusal(prompt, output, meta, detected);
            DetectRepetitionLoop(prompt, output, meta, detected);
            DetectTruncation(prompt, output, meta, detected);
            DetectHallucination(prompt, output, meta, detected);
            DetectFormatViolation(prompt, output, meta, detected);
            DetectLowSpecificity(prompt, output, meta, detected);
            DetectConstraintViolation(prompt, output, meta, detected);
            DetectOffTopicDrift(prompt, output, meta, detected);

            if (detected.Count > 0)
                _promptFailureCount[key] = _promptFailureCount.GetValueOrDefault(key) + detected.Count;
            _failures.AddRange(detected);

            return detected;
        }

        // ── Individual Detectors ───────────────────────

        private void DetectRefusal(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            if (RefusalPattern.IsMatch(output))
                detected.Add(CreateFailure(HealerFailureMode.Refusal, FailureSeverity.High, prompt, output,
                    "Output contains refusal language", 0.85, meta));
        }

        private void DetectRepetitionLoop(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            if (RepetitionPattern.IsMatch(output))
                detected.Add(CreateFailure(HealerFailureMode.RepetitionLoop, FailureSeverity.Medium, prompt, output,
                    "Output contains repeated text blocks", 0.90, meta));
        }

        private static void DetectTruncation(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            if (output.Length > 100 && TruncationPattern.IsMatch(output) && !output.TrimEnd().EndsWith("..."))
                detected.Add(CreateFailure(HealerFailureMode.Truncation, FailureSeverity.Medium, prompt, output,
                    "Output appears to end mid-sentence", 0.70, meta));
        }

        private static void DetectHallucination(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            if (HallucinationMarkers.IsMatch(output))
                detected.Add(CreateFailure(HealerFailureMode.Hallucination, FailureSeverity.High, prompt, output,
                    "Output contains hallucination markers (unverifiable claims)", 0.65, meta));
        }

        private static readonly Regex JsonFormatRequest = new(
            @"(?i)(respond\s+(?:in|with)\s+JSON|JSON\s+format|output.*JSON|```json)",
            RegexOptions.Compiled);

        private static readonly Regex ListFormatRequest = new(
            @"(?i)(list\s+(?:the|all|each)|bullet\s+points?|numbered\s+list)",
            RegexOptions.Compiled);

        private static readonly Regex ListMarkerPattern = new(
            @"(?m)^[\s]*[-•*\d]+[.)\s]",
            RegexOptions.Compiled);

        private static void DetectFormatViolation(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            if (JsonFormatRequest.IsMatch(prompt))
            {
                var trimmed = output.Trim();
                if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
                    detected.Add(CreateFailure(HealerFailureMode.FormatViolation, FailureSeverity.High, prompt, output,
                        "Prompt requests JSON but output is not JSON", 0.90, meta));
            }

            if (ListFormatRequest.IsMatch(prompt) && !ListMarkerPattern.IsMatch(output))
                detected.Add(CreateFailure(HealerFailureMode.FormatViolation, FailureSeverity.Medium, prompt, output,
                    "Prompt requests list format but output lacks list markers", 0.70, meta));
        }

        private static void DetectLowSpecificity(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            var promptWords = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var outputWords = output.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (promptWords > 30 && outputWords < 15 && output.Length < 100)
                detected.Add(CreateFailure(HealerFailureMode.LowSpecificity, FailureSeverity.Medium, prompt, output,
                    $"Output ({outputWords} words) is disproportionately short for prompt ({promptWords} words)", 0.75, meta));
        }

        private static readonly Regex ConstraintPattern = new(
            @"(?i)(?:do\s+not|must\s+not|never|avoid)\s+(.{5,50?})(?=[.,;!]|$)",
            RegexOptions.Compiled);

        private static void DetectConstraintViolation(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            var outputLower = output.ToLowerInvariant();
            foreach (Match m in ConstraintPattern.Matches(prompt))
            {
                var forbidden = m.Groups[1].Value.Trim().ToLowerInvariant();
                if (forbidden.Length > 5 && outputLower.Contains(forbidden))
                {
                    detected.Add(CreateFailure(HealerFailureMode.ConstraintViolation, FailureSeverity.High, prompt, output,
                        $"Output violates constraint: '{m.Value.Trim()}'", 0.80, meta));
                    break;
                }
            }
        }

        private static void DetectOffTopicDrift(string prompt, string output, Dictionary<string, string> meta, List<PromptFailure> detected)
        {
            var promptKeywords = ExtractKeywords(prompt);
            var outputKeywords = ExtractKeywords(output);
            if (promptKeywords.Count >= 5 && outputKeywords.Count >= 5)
            {
                var overlap = promptKeywords.Intersect(outputKeywords).Count();
                var overlapRatio = (double)overlap / promptKeywords.Count;
                if (overlapRatio < 0.1)
                    detected.Add(CreateFailure(HealerFailureMode.OffTopicDrift, FailureSeverity.Medium, prompt, output,
                        $"Keyword overlap is only {overlapRatio:P0} — output may be off-topic", 0.60, meta));
            }
        }

        // ── Patch Generation ───────────────────────────

        /// <summary>
        /// Generate a healing patch for a detected failure.
        /// </summary>
        public HealingPatch GeneratePatch(PromptFailure failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));

            return failure.Mode switch
            {
                HealerFailureMode.Refusal => GenerateRefusalPatch(failure),
                HealerFailureMode.Hallucination => GenerateHallucinationPatch(failure),
                HealerFailureMode.FormatViolation => GenerateFormatPatch(failure),
                HealerFailureMode.Truncation => GenerateTruncationPatch(failure),
                HealerFailureMode.RepetitionLoop => GenerateRepetitionPatch(failure),
                HealerFailureMode.OffTopicDrift => GenerateDriftPatch(failure),
                HealerFailureMode.LowSpecificity => GenerateSpecificityPatch(failure),
                HealerFailureMode.Contradiction => GenerateContradictionPatch(failure),
                HealerFailureMode.ConstraintViolation => GenerateConstraintPatch(failure),
                HealerFailureMode.PerformanceDegradation => GeneratePerformancePatch(failure),
                _ => GenerateGenericPatch(failure)
            };
        }

        /// <summary>
        /// Detect failures and auto-generate patches in one call.
        /// </summary>
        public List<(PromptFailure Failure, HealingPatch Patch)> DetectAndHeal(string prompt, string output, Dictionary<string, string>? metadata = null)
        {
            var failures = Detect(prompt, output, metadata);
            var results = new List<(PromptFailure, HealingPatch)>();

            foreach (var f in failures)
            {
                var patch = GeneratePatch(f);
                results.Add((f, patch));
            }

            return results;
        }

        /// <summary>
        /// Apply a patch to produce a healed prompt text.
        /// </summary>
        public string ApplyPatch(string originalPrompt, HealingPatch patch)
        {
            if (string.IsNullOrEmpty(originalPrompt)) throw new ArgumentException("Prompt required.", nameof(originalPrompt));
            if (patch == null) throw new ArgumentNullException(nameof(patch));

            var result = patch.Operation switch
            {
                PatchOperation.Prepend => patch.PatchContent + "\n\n" + originalPrompt,
                PatchOperation.Append => originalPrompt + "\n\n" + patch.PatchContent,
                PatchOperation.InsertConstraint => InsertConstraintIntoPrompt(originalPrompt, patch.PatchContent),
                PatchOperation.WrapTemplate => patch.PatchContent.Replace("{{PROMPT}}", originalPrompt),
                PatchOperation.Remove => originalPrompt.Replace(patch.PatchContent, "").Trim(),
                PatchOperation.Replace when !string.IsNullOrEmpty(patch.OriginalText) =>
                    originalPrompt.Replace(patch.OriginalText, patch.PatchContent),
                _ => originalPrompt + "\n\n" + patch.PatchContent
            };

            patch.PatchedText = result;
            patch.Status = HealingStatus.Applied;
            patch.AppliedAt = DateTime.UtcNow;

            _journal.Add(new HealingRecord
            {
                Failure = _failures.FirstOrDefault(f => f.Id == patch.FailureId) ?? new PromptFailure(),
                Patch = patch,
                PromptBefore = originalPrompt,
                PromptAfter = result,
                AttemptNumber = _journal.Count(j => j.Failure.Id == patch.FailureId) + 1
            });

            return result;
        }

        /// <summary>
        /// Full autonomous heal cycle: detect → patch → apply → return healed prompt.
        /// Returns the original prompt if no failures detected.
        /// </summary>
        public string AutoHeal(string prompt, string output, Dictionary<string, string>? metadata = null)
        {
            var pairs = DetectAndHeal(prompt, output, metadata);
            if (pairs.Count == 0) return prompt;

            // Sort by severity (critical first) then confidence
            pairs.Sort((a, b) =>
            {
                var sev = b.Failure.Severity.CompareTo(a.Failure.Severity);
                return sev != 0 ? sev : b.Failure.Confidence.CompareTo(a.Failure.Confidence);
            });

            var current = prompt;
            foreach (var (failure, patch) in pairs)
            {
                if (Policy == HealingPolicy.SuggestOnly) continue;

                if (Policy == HealingPolicy.Conservative && patch.RiskScore > 0.5)
                    continue;

                current = ApplyPatch(current, patch);
            }

            // Check for escalation
            var key = ComputePromptKey(prompt);
            var failCount = _promptFailureCount.GetValueOrDefault(key);
            if (failCount >= EscalationThreshold)
            {
                var topMode = pairs[0].Failure.Mode;
                if (!_escalations.Any(e => e.Mode == topMode && (DateTime.UtcNow - e.Timestamp).TotalMinutes < 30))
                {
                    _escalations.Add(new EscalationEvent
                    {
                        Mode = topMode,
                        FailureCount = failCount,
                        HealAttempts = _journal.Count(j => j.Failure.PromptText == prompt),
                        Recommendation = GetEscalationRecommendation(topMode, failCount)
                    });
                }
            }

            return current;
        }

        /// <summary>
        /// Verify whether a healed prompt produced a successful output.
        /// </summary>
        public bool VerifyHeal(string healedPrompt, string newOutput, string patchId)
        {
            var newFailures = Detect(healedPrompt, newOutput);
            var record = _journal.LastOrDefault(j => j.Patch.Id == patchId);
            if (record != null)
            {
                record.Patch.VerifiedSuccess = newFailures.Count == 0;
                record.Patch.Status = newFailures.Count == 0 ? HealingStatus.Verified : HealingStatus.Failed;
            }
            return newFailures.Count == 0;
        }

        // ── Health Summary ─────────────────────────────

        /// <summary>Get overall health summary for all monitored prompts.</summary>
        public PromptHealthSummary GetHealthSummary()
        {
            var totalExec = _promptExecutionCount.Values.Sum();
            var totalFail = _failures.Count;
            var totalHealed = _journal.Count(j => j.Patch.Status == HealingStatus.Verified);

            var breakdown = _failures
                .GroupBy(f => f.Mode)
                .ToDictionary(g => g.Key, g => g.Count());

            var summary = new PromptHealthSummary
            {
                TotalExecutions = totalExec,
                TotalFailures = totalFail,
                TotalHealed = totalHealed,
                TotalEscalations = _escalations.Count,
                FailureBreakdown = breakdown,
                ProactiveRecommendations = GenerateProactiveRecommendations()
            };

            return summary;
        }

        // ── Export ─────────────────────────────────────

        /// <summary>Export healing journal as Markdown.</summary>
        public string ExportMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Prompt Self-Healer Report");
            sb.AppendLine();

            var summary = GetHealthSummary();
            sb.AppendLine($"**Health Grade:** {summary.HealthGrade} | **Success Rate:** {summary.SuccessRate:P1} | **Heal Rate:** {summary.HealRate:P1}");
            sb.AppendLine($"**Executions:** {summary.TotalExecutions} | **Failures:** {summary.TotalFailures} | **Healed:** {summary.TotalHealed} | **Escalations:** {summary.TotalEscalations}");
            sb.AppendLine();

            if (summary.FailureBreakdown.Count > 0)
            {
                sb.AppendLine("## Failure Breakdown");
                sb.AppendLine();
                foreach (var kvp in summary.FailureBreakdown.OrderByDescending(k => k.Value))
                    sb.AppendLine($"- **{kvp.Key}**: {kvp.Value}");
                sb.AppendLine();
            }

            if (_journal.Count > 0)
            {
                sb.AppendLine("## Healing Journal");
                sb.AppendLine();
                foreach (var rec in _journal.TakeLast(20))
                {
                    sb.AppendLine($"### Heal {rec.Id} ({rec.Timestamp:yyyy-MM-dd HH:mm})");
                    sb.AppendLine($"- **Failure:** {rec.Failure.Mode} ({rec.Failure.Severity})");
                    sb.AppendLine($"- **Patch:** {rec.Patch.Operation} — {rec.Patch.Description}");
                    sb.AppendLine($"- **Status:** {rec.Patch.Status} | Risk: {rec.Patch.RiskScore:F2}");
                    sb.AppendLine();
                }
            }

            if (_escalations.Count > 0)
            {
                sb.AppendLine("## Escalations");
                sb.AppendLine();
                foreach (var esc in _escalations)
                {
                    sb.AppendLine($"- ⚠️ **{esc.Mode}** — {esc.FailureCount} failures, {esc.HealAttempts} heal attempts");
                    sb.AppendLine($"  Recommendation: {esc.Recommendation}");
                }
                sb.AppendLine();
            }

            if (summary.ProactiveRecommendations.Count > 0)
            {
                sb.AppendLine("## Proactive Recommendations");
                sb.AppendLine();
                foreach (var rec in summary.ProactiveRecommendations)
                    sb.AppendLine($"- 💡 {rec}");
            }

            return sb.ToString();
        }

        /// <summary>Export healing journal as JSON string.</summary>
        public string ExportJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            var summary = GetHealthSummary();
            sb.AppendLine($"  \"healthGrade\": \"{summary.HealthGrade}\",");
            sb.AppendLine($"  \"successRate\": {summary.SuccessRate:F4},");
            sb.AppendLine($"  \"healRate\": {summary.HealRate:F4},");
            sb.AppendLine($"  \"totalExecutions\": {summary.TotalExecutions},");
            sb.AppendLine($"  \"totalFailures\": {summary.TotalFailures},");
            sb.AppendLine($"  \"totalHealed\": {summary.TotalHealed},");
            sb.AppendLine($"  \"totalEscalations\": {summary.TotalEscalations},");

            sb.AppendLine("  \"failureBreakdown\": {");
            var bdEntries = summary.FailureBreakdown.ToList();
            for (int i = 0; i < bdEntries.Count; i++)
            {
                var comma = i < bdEntries.Count - 1 ? "," : "";
                sb.AppendLine($"    \"{bdEntries[i].Key}\": {bdEntries[i].Value}{comma}");
            }
            sb.AppendLine("  },");

            sb.AppendLine($"  \"journalEntries\": {_journal.Count},");
            sb.AppendLine($"  \"escalationCount\": {_escalations.Count},");

            sb.AppendLine("  \"recommendations\": [");
            for (int i = 0; i < summary.ProactiveRecommendations.Count; i++)
            {
                var comma = i < summary.ProactiveRecommendations.Count - 1 ? "," : "";
                sb.AppendLine($"    \"{EscapeJson(summary.ProactiveRecommendations[i])}\"{comma}");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── Private Helpers ────────────────────────────

        private static PromptFailure CreateFailure(HealerFailureMode mode, FailureSeverity severity, string prompt,
            string output, string evidence, double confidence, Dictionary<string, string> meta)
        {
            return new PromptFailure
            {
                Mode = mode,
                Severity = severity,
                PromptText = prompt,
                OutputText = output.Length > 500 ? output[..500] + "…" : output,
                Evidence = evidence,
                Confidence = confidence,
                Metadata = new Dictionary<string, string>(meta)
            };
        }

        private static string ComputePromptKey(string prompt)
        {
            // Simple hash for grouping identical prompts
            var hash = 0;
            foreach (var c in prompt) hash = (hash * 31 + c) & 0x7FFFFFFF;
            return hash.ToString("X8");
        }

        private static HashSet<string> ExtractKeywords(string text)
        {
            var stopwords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "be", "been",
                "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should",
                "may", "might", "shall", "can", "to", "of", "in", "for", "on", "with", "at", "by",
                "from", "as", "into", "through", "during", "before", "after", "and", "but", "or",
                "not", "no", "nor", "if", "then", "than", "that", "this", "it", "its", "i", "you",
                "he", "she", "we", "they", "my", "your", "his", "her", "our", "their" };
            return text.ToLowerInvariant()
                .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopwords.Contains(w))
                .ToHashSet();
        }

        private HealingPatch GenerateRefusalPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.Refusal,
            Operation = PatchOperation.WrapTemplate,
            Description = "Reframe prompt to avoid safety-filter triggers while preserving intent",
            PatchContent = "You are a helpful expert assistant. The following is an educational/professional request.\n\n{{PROMPT}}\n\nProvide a thorough, factual response.",
            RiskScore = 0.3
        };

        private HealingPatch GenerateHallucinationPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.Hallucination,
            Operation = PatchOperation.Append,
            Description = "Add grounding constraint to reduce hallucination",
            PatchContent = "\n\nIMPORTANT: Only state facts you are certain about. If you are unsure, say \"I'm not certain about this.\" Do not invent citations, dates, or statistics. Distinguish clearly between established facts and your reasoning.",
            RiskScore = 0.15
        };

        private HealingPatch GenerateFormatPatch(PromptFailure f)
        {
            var isJson = Regex.IsMatch(f.PromptText, @"(?i)JSON");
            return new HealingPatch
            {
                FailureId = f.Id,
                TargetMode = HealerFailureMode.FormatViolation,
                Operation = PatchOperation.Append,
                Description = isJson ? "Reinforce JSON format requirement" : "Reinforce list format requirement",
                PatchContent = isJson
                    ? "\n\nYou MUST respond with valid JSON only. No markdown code fences. No explanatory text. Start with { or [."
                    : "\n\nYou MUST format your response as a bulleted or numbered list. Each item on its own line starting with - or a number.",
                RiskScore = 0.1
            };
        }

        private HealingPatch GenerateTruncationPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.Truncation,
            Operation = PatchOperation.Append,
            Description = "Add completion instruction to prevent truncation",
            PatchContent = "\n\nKeep your response concise and complete. Finish all sentences. If the topic is too large, provide a focused subset rather than trailing off.",
            RiskScore = 0.1
        };

        private HealingPatch GenerateRepetitionPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.RepetitionLoop,
            Operation = PatchOperation.Append,
            Description = "Add anti-repetition instruction",
            PatchContent = "\n\nDo not repeat yourself. Each sentence should add new information. If you've covered a point, move on to the next.",
            RiskScore = 0.1
        };

        private HealingPatch GenerateDriftPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.OffTopicDrift,
            Operation = PatchOperation.WrapTemplate,
            Description = "Add focus guardrails to prevent topic drift",
            PatchContent = "STAY ON TOPIC. Answer ONLY what is asked. Do not go on tangents or introduce unrelated topics.\n\n{{PROMPT}}\n\nRemember: Stay focused on the specific question above.",
            RiskScore = 0.25
        };

        private HealingPatch GenerateSpecificityPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.LowSpecificity,
            Operation = PatchOperation.Append,
            Description = "Request more detailed and specific output",
            PatchContent = "\n\nProvide a detailed, specific response. Include concrete examples, numbers, or actionable steps. Avoid vague generalities.",
            RiskScore = 0.15
        };

        private HealingPatch GenerateContradictionPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.Contradiction,
            Operation = PatchOperation.Prepend,
            Description = "Add consistency instruction",
            PatchContent = "IMPORTANT: Ensure internal consistency. Do not contradict yourself or the context provided. If information conflicts, acknowledge the conflict explicitly.\n",
            RiskScore = 0.2
        };

        private HealingPatch GenerateConstraintPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.ConstraintViolation,
            Operation = PatchOperation.Prepend,
            Description = "Elevate constraint visibility",
            PatchContent = $"⚠️ CRITICAL CONSTRAINTS — You MUST follow these rules. Violations are unacceptable:\n{ExtractConstraints(f.PromptText)}\n\n",
            RiskScore = 0.2
        };

        private HealingPatch GeneratePerformancePatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = HealerFailureMode.PerformanceDegradation,
            Operation = PatchOperation.Append,
            Description = "Add brevity constraint for performance",
            PatchContent = "\n\nBe concise. Aim for the shortest adequate response. Prioritize the most important information first.",
            RiskScore = 0.15
        };

        private HealingPatch GenerateGenericPatch(PromptFailure f) => new()
        {
            FailureId = f.Id,
            TargetMode = f.Mode,
            Operation = PatchOperation.Append,
            Description = "Generic quality improvement patch",
            PatchContent = "\n\nPlease ensure your response is accurate, well-structured, complete, and directly addresses the request.",
            RiskScore = 0.1
        };

        private static string InsertConstraintIntoPrompt(string prompt, string constraint)
        {
            // Insert constraint after the first sentence or paragraph break
            var firstBreak = prompt.IndexOf('\n');
            if (firstBreak > 0 && firstBreak < prompt.Length - 1)
                return prompt[..(firstBreak + 1)] + "\n" + constraint + "\n" + prompt[(firstBreak + 1)..];
            return constraint + "\n\n" + prompt;
        }

        private static string ExtractConstraints(string prompt)
        {
            var sb = new StringBuilder();
            var matches = Regex.Matches(prompt, @"(?i)(?:do\s+not|must\s+not|never|avoid|must|always|required)\s+(.{5,80}?)(?=[.,;!]|$)");
            foreach (Match m in matches)
                sb.AppendLine($"  • {m.Value.Trim()}");
            return sb.Length > 0 ? sb.ToString() : "  • Follow all instructions precisely.";
        }

        /// <summary>Threshold-based recommendation rules keyed by failure mode.</summary>
        private static readonly (HealerFailureMode Mode, int MinCount, string Advice)[] RecommendationRules =
        {
            (HealerFailureMode.Refusal, 2, "Recurring refusals detected. Consider reframing sensitive prompts with professional/educational context framing."),
            (HealerFailureMode.Hallucination, 2, "Multiple hallucination events. Add explicit grounding instructions and 'cite sources or say unsure' directives."),
            (HealerFailureMode.FormatViolation, 2, "Format violations recurring. Use stronger format enforcement (e.g., 'Output ONLY valid JSON, nothing else')."),
            (HealerFailureMode.Truncation, 2, "Truncation is common. Reduce prompt complexity or add 'be concise but complete' instructions."),
            (HealerFailureMode.RepetitionLoop, 1, "Repetition loops detected. Lower temperature or add diversity instructions."),
            (HealerFailureMode.OffTopicDrift, 2, "Off-topic drift recurring. Add explicit scope boundaries and 'stay on topic' guardrails."),
            (HealerFailureMode.ConstraintViolation, 2, "Constraints being ignored. Move critical constraints to the beginning of the prompt and use ALL CAPS or bullet formatting."),
            (HealerFailureMode.LowSpecificity, 2, "Low specificity outputs. Add 'provide concrete examples' or 'include specific numbers/steps' to prompts."),
        };

        private List<string> GenerateProactiveRecommendations()
        {
            var recs = new List<string>();

            // Build per-mode counts once
            var modeCounts = new Dictionary<HealerFailureMode, int>();
            foreach (var f in _failures)
                modeCounts[f.Mode] = modeCounts.GetValueOrDefault(f.Mode) + 1;

            // Evaluate declarative threshold rules
            foreach (var (mode, minCount, advice) in RecommendationRules)
            {
                if (modeCounts.GetValueOrDefault(mode) >= minCount)
                    recs.Add(advice);
            }

            // Systemic health checks
            var totalExec = _promptExecutionCount.Values.Sum();
            if (totalExec > 10 && _failures.Count > totalExec * 0.3)
                recs.Add("Overall failure rate exceeds 30%. Consider a fundamental prompt redesign rather than incremental patches.");

            var verifiedHeals = _journal.Count(j => j.Patch.VerifiedSuccess == true);
            var failedHeals = _journal.Count(j => j.Patch.VerifiedSuccess == false);
            if (failedHeals > verifiedHeals && _journal.Count >= 3)
                recs.Add("More heal attempts are failing than succeeding. The prompt may need manual expert review.");

            if (_escalations.Count >= 3)
                recs.Add("Multiple escalation events. This prompt has systemic issues — consider a complete rewrite with different approach.");

            if (recs.Count == 0 && _failures.Count == 0)
                recs.Add("All prompts are healthy. Continue monitoring for early drift detection.");

            return recs;
        }

        private string GetEscalationRecommendation(HealerFailureMode mode, int failCount) => mode switch
        {
            HealerFailureMode.Refusal => $"Prompt has triggered {failCount} refusals. Rewrite to avoid safety-filter patterns while preserving intent.",
            HealerFailureMode.Hallucination => $"Prompt has {failCount} hallucination events. Add retrieval-augmented grounding or strict factual constraints.",
            HealerFailureMode.FormatViolation => $"Format compliance failed {failCount} times. Switch to function-calling or structured output mode if available.",
            HealerFailureMode.RepetitionLoop => $"Repetition detected {failCount} times. Reduce temperature and max_tokens, or restructure the prompt.",
            HealerFailureMode.Truncation => $"Output truncated {failCount} times. Split into smaller sub-prompts or increase token budget.",
            _ => $"Failure mode {mode} has occurred {failCount} times. Manual review recommended."
        };

        private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
