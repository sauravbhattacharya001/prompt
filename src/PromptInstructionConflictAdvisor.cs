namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>A single instruction line/sentence in a system prompt.</summary>
    public sealed class InstructionItem
    {
        /// <summary>Optional stable id. Falls back to <c>ins-{index}</c>.</summary>
        public string Id { get; init; } = "";
        /// <summary>The raw instruction text.</summary>
        public string Text { get; init; } = "";
        /// <summary>Optional 1-based section number this instruction belongs to (e.g. for nested rule blocks).</summary>
        public int? Section { get; init; }
    }

    /// <summary>Per-instruction verdict.</summary>
    public enum InstructionVerdict
    {
        /// <summary>No issues - keep as-is.</summary>
        Keep,
        /// <summary>Rewrite to remove ambiguity / weak language.</summary>
        Clarify,
        /// <summary>Merge with a sibling that overlaps semantically.</summary>
        Merge,
        /// <summary>Remove because it duplicates or contradicts a higher-priority instruction.</summary>
        Remove,
        /// <summary>Move to an earlier / explicitly prioritised section.</summary>
        Promote,
    }

    /// <summary>Priority bucket for instruction-level findings and playbook actions.</summary>
    public enum InstructionPriority
    {
        /// <summary>Immediate / blocking action.</summary>
        P0,
        /// <summary>High-priority action.</summary>
        P1,
        /// <summary>Medium-priority action.</summary>
        P2,
        /// <summary>Advisory action.</summary>
        P3,
    }

    /// <summary>Aggregate health band for the prompt's instruction set.</summary>
    public enum InstructionConflictRisk
    {
        /// <summary>No meaningful conflicts.</summary>
        Healthy,
        /// <summary>Minor overlaps or weak language.</summary>
        Watch,
        /// <summary>Notable conflicts affecting model behaviour.</summary>
        Degraded,
        /// <summary>Multiple high-severity conflicts.</summary>
        AtRisk,
        /// <summary>Prompt is internally contradictory; do not ship.</summary>
        Broken,
    }

    /// <summary>Risk appetite knob.</summary>
    public enum InstructionRiskAppetite
    {
        /// <summary>Stricter scoring and richer playbook.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient scoring and trims P2/P3 from playbook.</summary>
        Aggressive,
    }

    /// <summary>One detected issue in the instruction set.</summary>
    public sealed class InstructionFinding
    {
        /// <summary>Detector code (e.g. DIRECT_CONTRADICTION).</summary>
        public string Code { get; internal set; } = "";
        /// <summary>Id of the affected instruction, or <c>*</c> for set-level findings.</summary>
        public string InstructionId { get; internal set; } = "";
        /// <summary>Human-readable label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Severity 0-100.</summary>
        public int Severity { get; internal set; }
        /// <summary>Human-readable detail.</summary>
        public string Detail { get; internal set; } = "";
        /// <summary>Other instruction ids this one conflicts / overlaps with.</summary>
        public IReadOnlyList<string> RelatedInstructionIds { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>Per-instruction synthesised verdict.</summary>
    public sealed class InstructionAssessment
    {
        /// <summary>Affected instruction id.</summary>
        public string InstructionId { get; internal set; } = "";
        /// <summary>Recommended verdict.</summary>
        public InstructionVerdict Verdict { get; internal set; }
        /// <summary>Priority bucket.</summary>
        public InstructionPriority Priority { get; internal set; }
        /// <summary>Aggregated risk score 0-100.</summary>
        public int RiskScore { get; internal set; }
        /// <summary>Short rationale.</summary>
        public string Rationale { get; internal set; } = "";
        /// <summary>Underlying detector codes.</summary>
        public IReadOnlyList<string> Reasons { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>One playbook action for the prompt as a whole.</summary>
    public sealed class InstructionAction
    {
        /// <summary>Action code.</summary>
        public string Code { get; internal set; } = "";
        /// <summary>Human-readable label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Priority.</summary>
        public InstructionPriority Priority { get; internal set; }
        /// <summary>Owning role.</summary>
        public string Owner { get; internal set; } = "";
        /// <summary>Blast radius 1-5.</summary>
        public int BlastRadius { get; internal set; }
        /// <summary>Reversibility (low/medium/high).</summary>
        public string Reversibility { get; internal set; } = "";
        /// <summary>Human-readable reason.</summary>
        public string Reason { get; internal set; } = "";
        /// <summary>Affected instruction ids.</summary>
        public IReadOnlyList<string> InstructionIds { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>A deconflicted, paste-ready prompt draft.</summary>
    public sealed class DeconflictedPromptDraft
    {
        /// <summary>Surviving instructions after curation, in recommended order.</summary>
        public IReadOnlyList<InstructionItem> Instructions { get; internal set; } = Array.Empty<InstructionItem>();
        /// <summary>Ids dropped during curation.</summary>
        public IReadOnlyList<string> RemovedInstructionIds { get; internal set; } = Array.Empty<string>();
        /// <summary>Ids flagged for human revision (clarify / merge).</summary>
        public IReadOnlyList<string> RevisedInstructionIds { get; internal set; } = Array.Empty<string>();
        /// <summary>Ids that were re-ordered (e.g. promoted).</summary>
        public IReadOnlyList<string> ReorderedInstructionIds { get; internal set; } = Array.Empty<string>();
        /// <summary>Ready-to-paste rebuilt prompt text with a numbered priority preamble.</summary>
        public string PromptText { get; internal set; } = "";
        /// <summary>Short note summarising what changed.</summary>
        public string Note { get; internal set; } = "";
    }

    /// <summary>The full advisor report.</summary>
    public sealed class InstructionConflictReport
    {
        /// <summary>Aggregate score 0-100 (higher = worse).</summary>
        public int OverallScore { get; internal set; }
        /// <summary>A-F grade.</summary>
        public char Grade { get; internal set; }
        /// <summary>Aggregate verdict band.</summary>
        public InstructionConflictRisk Verdict { get; internal set; }
        /// <summary>All detected findings.</summary>
        public IReadOnlyList<InstructionFinding> Findings { get; internal set; } = Array.Empty<InstructionFinding>();
        /// <summary>Per-instruction assessments.</summary>
        public IReadOnlyList<InstructionAssessment> Assessments { get; internal set; } = Array.Empty<InstructionAssessment>();
        /// <summary>Recommended cross-prompt actions.</summary>
        public IReadOnlyList<InstructionAction> Playbook { get; internal set; } = Array.Empty<InstructionAction>();
        /// <summary>Cross-prompt insights.</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();
        /// <summary>Deconflicted draft ready for use.</summary>
        public DeconflictedPromptDraft Draft { get; internal set; } = new();
        /// <summary>Generation timestamp (from injected clock).</summary>
        public DateTime GeneratedAtUtc { get; internal set; }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>Plain-text rendering.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("Instruction conflict: ").Append(Grade).Append(" - ").Append(OverallScore).Append("/100 (").Append(Verdict).AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("Top instructions by risk:");
            foreach (var a in Assessments.OrderByDescending(x => x.RiskScore).Take(10))
            {
                sb.Append("  ").Append(a.InstructionId).Append(" [").Append(a.Verdict).Append('/').Append(a.Priority).Append("] score=").Append(a.RiskScore).Append(' ').AppendLine(a.Rationale);
            }
            sb.AppendLine();
            sb.AppendLine("Playbook:");
            foreach (var p in Playbook)
            {
                sb.Append("  [").Append(p.Priority).Append("] ").Append(p.Code).Append(" - ").Append(p.Label).Append(" (").Append(p.Owner).Append(", blast=").Append(p.BlastRadius).Append(", rev=").Append(p.Reversibility).AppendLine(")");
            }
            if (Insights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Insights:");
                foreach (var i in Insights) { sb.Append("  - ").AppendLine(i); }
            }
            return sb.ToString();
        }

        /// <summary>Markdown rendering.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## PromptInstructionConflictAdvisor");
            sb.Append("**Grade:** ").Append(Grade).Append("  **Score:** ").Append(OverallScore).Append("/100  **Verdict:** ").AppendLine(Verdict.ToString());
            sb.AppendLine();
            sb.AppendLine("### Assessments");
            sb.AppendLine("| Id | Verdict | Priority | Score | Reasons |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var a in Assessments.OrderByDescending(x => x.RiskScore))
            {
                sb.Append("| ").Append(a.InstructionId).Append(" | ").Append(a.Verdict).Append(" | ").Append(a.Priority).Append(" | ").Append(a.RiskScore).Append(" | ").Append(string.Join(", ", a.Reasons)).AppendLine(" |");
            }
            sb.AppendLine();
            sb.AppendLine("### Findings");
            foreach (var f in Findings)
            {
                sb.Append("- **").Append(f.Code).Append("** (").Append(f.InstructionId).Append(", sev=").Append(f.Severity).Append("): ").AppendLine(f.Detail);
            }
            sb.AppendLine();
            sb.AppendLine("### Playbook");
            foreach (var p in Playbook)
            {
                sb.Append("- [").Append(p.Priority).Append("] **").Append(p.Code).Append("** - ").Append(p.Label).Append(" _(owner: ").Append(p.Owner).Append(", blast: ").Append(p.BlastRadius).Append(", rev: ").Append(p.Reversibility).AppendLine(")_");
            }
            if (Insights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Insights");
                foreach (var i in Insights) { sb.Append("- ").AppendLine(i); }
            }
            sb.AppendLine();
            sb.Append("### Deconflicted draft (").Append(Draft.Instructions.Count).AppendLine(" instructions)");
            sb.AppendLine(Draft.Note);
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(Draft.PromptText);
            sb.AppendLine("```");
            return sb.ToString();
        }

        /// <summary>Deterministic JSON rendering.</summary>
        public string ToJson()
        {
            var dto = new
            {
                overallScore = OverallScore,
                grade = Grade.ToString(),
                verdict = Verdict.ToString(),
                generatedAtUtc = GeneratedAtUtc.ToString("o"),
                findings = Findings.Select(f => new
                {
                    code = f.Code,
                    instructionId = f.InstructionId,
                    label = f.Label,
                    severity = f.Severity,
                    detail = f.Detail,
                    relatedInstructionIds = f.RelatedInstructionIds,
                }).ToArray(),
                assessments = Assessments.Select(a => new
                {
                    instructionId = a.InstructionId,
                    verdict = a.Verdict.ToString(),
                    priority = a.Priority.ToString(),
                    riskScore = a.RiskScore,
                    rationale = a.Rationale,
                    reasons = a.Reasons,
                }).ToArray(),
                playbook = Playbook.Select(p => new
                {
                    code = p.Code,
                    label = p.Label,
                    priority = p.Priority.ToString(),
                    owner = p.Owner,
                    blastRadius = p.BlastRadius,
                    reversibility = p.Reversibility,
                    reason = p.Reason,
                    instructionIds = p.InstructionIds,
                }).ToArray(),
                insights = Insights,
                draft = new
                {
                    note = Draft.Note,
                    removed = Draft.RemovedInstructionIds,
                    revised = Draft.RevisedInstructionIds,
                    reordered = Draft.ReorderedInstructionIds,
                    promptText = Draft.PromptText,
                    instructions = Draft.Instructions.Select(e => new
                    {
                        id = e.Id,
                        text = e.Text,
                        section = e.Section,
                    }).ToArray(),
                },
            };
            return JsonSerializer.Serialize(dto, JsonOpts);
        }
    }

    /// <summary>
    /// Agentic instruction-conflict analyser for system prompts. Splits a long prompt into
    /// individual instructions, detects contradictions, overlaps, ambiguous priority, weak
    /// language, escape-hatch conflicts, format/tone/audience/language/tool-permission
    /// conflicts, and buried critical rules. Produces a per-instruction verdict, a P0-P3
    /// playbook, and a deconflicted paste-ready draft prompt. Fifth sibling to
    /// <see cref="PromptHallucinationRiskScorer"/>, <see cref="PromptDefenseAdvisor"/>,
    /// <see cref="PromptKnowledgeFreshnessAdvisor"/>, and
    /// <see cref="PromptExampleQualityAdvisor"/>.
    /// </summary>
    public sealed class PromptInstructionConflictAdvisor
    {
        private readonly Func<DateTime> _now;

        /// <summary>Create a new advisor with an optional injectable clock.</summary>
        public PromptInstructionConflictAdvisor(Func<DateTime>? nowProvider = null)
        {
            _now = nowProvider ?? (() => DateTime.UtcNow);
        }

        /// <summary>Risk appetite knob.</summary>
        public InstructionRiskAppetite RiskAppetite { get; set; } = InstructionRiskAppetite.Balanced;

        private static readonly Regex TokenRe = new(@"[a-z0-9]+", RegexOptions.Compiled);
        private static readonly Regex SplitRe = new(@"(?:\r?\n|(?<=[.!?])\s+(?=[A-Z]))", RegexOptions.Compiled);
        private static readonly Regex StripBulletRe = new(@"^(?:[-*•]|\d+[.)])\s+", RegexOptions.Compiled);
        private static readonly Regex YearRe = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);

        // Antonym groups: instructions tagged with both members of any group conflict directly.
        private static readonly (string A, string B, string Code, string Label)[] AntonymPairs = new[]
        {
            ("formal", "casual", "TONE_CONFLICT", "Formal vs casual tone"),
            ("brief", "detailed", "LENGTH_CONFLICT", "Brief vs detailed length"),
            ("short", "long", "LENGTH_CONFLICT", "Short vs long length"),
            ("concise", "verbose", "LENGTH_CONFLICT", "Concise vs verbose length"),
            ("polite", "blunt", "TONE_CONFLICT", "Polite vs blunt tone"),
            ("technical", "non-technical", "AUDIENCE_CONFLICT", "Technical vs non-technical audience"),
            ("english", "spanish", "LANGUAGE_CONFLICT", "English vs Spanish output language"),
            ("english", "french", "LANGUAGE_CONFLICT", "English vs French output language"),
            ("english", "german", "LANGUAGE_CONFLICT", "English vs German output language"),
            ("json", "markdown", "FORMAT_CONFLICT", "JSON vs Markdown output format"),
            ("json", "plaintext", "FORMAT_CONFLICT", "JSON vs plaintext output format"),
            ("markdown", "plaintext", "FORMAT_CONFLICT", "Markdown vs plaintext output format"),
            ("bullets", "prose", "FORMAT_CONFLICT", "Bullets vs prose output format"),
            ("use_tools", "no_tools", "TOOL_PERMISSION_CONFLICT", "Tool use required vs forbidden"),
            ("cite", "no_citation", "CITATION_CONFLICT", "Citations required vs forbidden"),
            ("emoji", "no_emoji", "STYLE_CONFLICT", "Emoji encouraged vs forbidden"),
        };

        /// <summary>Analyse a list of instructions.</summary>
        public InstructionConflictReport Analyze(IEnumerable<InstructionItem>? instructions)
        {
            var list = (instructions ?? Array.Empty<InstructionItem>()).ToList();
            return AnalyseInternal(list);
        }

        /// <summary>Convenience overload: split a raw prompt string into instructions and analyse.</summary>
        public InstructionConflictReport Analyze(string? promptText)
        {
            return AnalyseInternal(SplitInstructions(promptText ?? ""));
        }

        /// <summary>Split a prompt string into <see cref="InstructionItem"/>s (bullets / numbered list / sentence boundary).</summary>
        public static List<InstructionItem> SplitInstructions(string promptText)
        {
            var result = new List<InstructionItem>();
            if (string.IsNullOrWhiteSpace(promptText)) return result;
            int section = 0;
            foreach (var raw in SplitRe.Split(promptText))
            {
                var line = raw?.Trim() ?? "";
                if (line.Length == 0) continue;
                if (line.StartsWith("#"))
                {
                    section++;
                    continue;
                }
                line = StripBulletRe.Replace(line, "");
                if (line.Length == 0) continue;
                result.Add(new InstructionItem { Id = $"ins-{result.Count}", Text = line, Section = section > 0 ? section : null });
            }
            return result;
        }

        private InstructionConflictReport AnalyseInternal(List<InstructionItem> list)
        {
            var report = new InstructionConflictReport { GeneratedAtUtc = _now() };
            var ids = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ids[i] = string.IsNullOrWhiteSpace(list[i].Id) ? $"ins-{i}" : list[i].Id;
            }

            if (list.Count == 0)
            {
                report.OverallScore = 0;
                report.Grade = 'A';
                report.Verdict = InstructionConflictRisk.Healthy;
                report.Draft = new DeconflictedPromptDraft { Note = "removed 0, revised 0, reordered=false", PromptText = "" };
                return report;
            }

            var findings = new List<InstructionFinding>();
            var tokenSets = list.Select(e => Tokenize(e.Text)).ToList();
            var tags = list.Select(e => ClassifyTags(e.Text)).ToList();

            // 1. EMPTY_INSTRUCTION
            for (int i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(list[i].Text))
                {
                    findings.Add(new InstructionFinding
                    {
                        Code = "EMPTY_INSTRUCTION", InstructionId = ids[i], Label = "Empty instruction",
                        Severity = 70, Detail = "Instruction has no body text.",
                    });
                }
            }

            // 2. NEAR_DUPLICATE
            for (int i = 0; i < list.Count; i++)
            {
                if (tokenSets[i].Count == 0) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (tokenSets[j].Count == 0) continue;
                    double jac = Jaccard(tokenSets[i], tokenSets[j]);
                    if (jac >= 0.80)
                    {
                        int sev = (int)Math.Min(100, 50 + (jac - 0.80) * 200);
                        findings.Add(new InstructionFinding
                        {
                            Code = "NEAR_DUPLICATE", InstructionId = ids[j], Label = "Near-duplicate instruction",
                            Severity = sev,
                            Detail = $"Highly similar to {ids[i]} (Jaccard {jac:F2}).",
                            RelatedInstructionIds = new[] { ids[i] },
                        });
                    }
                    else if (jac >= 0.55)
                    {
                        findings.Add(new InstructionFinding
                        {
                            Code = "REDUNDANT_OVERLAP", InstructionId = ids[j], Label = "Redundant overlap",
                            Severity = 35,
                            Detail = $"Overlaps with {ids[i]} (Jaccard {jac:F2}); consider merging.",
                            RelatedInstructionIds = new[] { ids[i] },
                        });
                    }
                }
            }

            // 3. DIRECT_CONTRADICTION & antonym-based conflicts
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    foreach (var (a, b, code, label) in AntonymPairs)
                    {
                        bool ia = tags[i].Contains(a), ib = tags[i].Contains(b);
                        bool ja = tags[j].Contains(a), jb = tags[j].Contains(b);
                        // Conflict when one side picks A and the other picks B.
                        if ((ia && jb) || (ib && ja))
                        {
                            findings.Add(new InstructionFinding
                            {
                                Code = code, InstructionId = ids[j], Label = label,
                                Severity = 85,
                                Detail = $"Conflicts with {ids[i]} ({label.ToLowerInvariant()}).",
                                RelatedInstructionIds = new[] { ids[i] },
                            });
                        }
                    }
                }
            }

            // 4. NEGATION_FLIP: "always X" vs "never X" or "do X" vs "do not X" on shared lemma
            for (int i = 0; i < list.Count; i++)
            {
                var (iPolarity, iLemma) = ExtractDirective(list[i].Text);
                if (iLemma == null) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    var (jPolarity, jLemma) = ExtractDirective(list[j].Text);
                    if (jLemma == null) continue;
                    if (iLemma == jLemma && iPolarity != jPolarity)
                    {
                        findings.Add(new InstructionFinding
                        {
                            Code = "DIRECT_CONTRADICTION", InstructionId = ids[j], Label = "Direct contradiction",
                            Severity = 95,
                            Detail = $"\"{Trunc(list[j].Text)}\" directly negates {ids[i]} (\"{Trunc(list[i].Text)}\").",
                            RelatedInstructionIds = new[] { ids[i] },
                        });
                    }
                }
            }

            // 5. AMBIGUOUS_PRIORITY: multiple "most important" / "above all" markers.
            var priorityIdxs = list
                .Select((e, idx) => new { e, idx })
                .Where(x => HasAny(x.e.Text, "most important", "above all", "highest priority", "always prioritize", "always prioritise", "first and foremost"))
                .Select(x => x.idx).ToList();
            if (priorityIdxs.Count >= 2)
            {
                foreach (var idx in priorityIdxs)
                {
                    findings.Add(new InstructionFinding
                    {
                        Code = "AMBIGUOUS_PRIORITY", InstructionId = ids[idx], Label = "Ambiguous top priority",
                        Severity = 60,
                        Detail = $"{priorityIdxs.Count} instructions claim top priority; only one rule can win.",
                        RelatedInstructionIds = priorityIdxs.Where(x => x != idx).Select(x => ids[x]).ToList(),
                    });
                }
            }

            // 6. WEAK_LANGUAGE: hedging that undermines hard rules.
            for (int i = 0; i < list.Count; i++)
            {
                if (HasAny(list[i].Text, "try to", "if possible", "ideally", "preferably", "as much as you can", "where appropriate", "when feasible"))
                {
                    findings.Add(new InstructionFinding
                    {
                        Code = "WEAK_LANGUAGE", InstructionId = ids[i], Label = "Weak / hedged language",
                        Severity = 30,
                        Detail = "Hedge phrase makes this instruction easy to ignore.",
                    });
                }
            }

            // 7. ESCAPE_HATCH_CONFLICT: "never X unless Y" without specified Y near the rule.
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i].Text;
                if ((t.Contains("never", StringComparison.OrdinalIgnoreCase) ||
                     t.Contains("do not", StringComparison.OrdinalIgnoreCase)) &&
                    (t.Contains(" unless", StringComparison.OrdinalIgnoreCase) ||
                     t.Contains(" except", StringComparison.OrdinalIgnoreCase)))
                {
                    // require the escape clause to actually have content after the keyword
                    var idx = t.IndexOf(" unless", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) idx = t.IndexOf(" except", StringComparison.OrdinalIgnoreCase);
                    var tail = idx >= 0 ? t.Substring(idx).Trim().TrimEnd('.', ',', ';') : "";
                    if (tail.Length < 12 || tail.Split(' ').Length < 3)
                    {
                        findings.Add(new InstructionFinding
                        {
                            Code = "ESCAPE_HATCH_CONFLICT", InstructionId = ids[i], Label = "Vague escape hatch",
                            Severity = 55,
                            Detail = "Rule has 'unless/except' clause but the exception is not specified clearly.",
                        });
                    }
                }
            }

            // 8. UNDEFINED_REFERENCE: refers to "the above rules" / "as stated" / "section N" with no section data.
            for (int i = 0; i < list.Count; i++)
            {
                if (HasAny(list[i].Text, "as stated above", "the above rules", "as mentioned", "per the previous", "see above", "as noted earlier"))
                {
                    findings.Add(new InstructionFinding
                    {
                        Code = "UNDEFINED_REFERENCE", InstructionId = ids[i], Label = "Vague back-reference",
                        Severity = 40,
                        Detail = "Refers to prior content; models will struggle to resolve this without explicit anchors.",
                    });
                }
            }

            // 9. BURIED_CRITICAL: "never" / "always" / "critical" / "must" appearing in the bottom 30% of the prompt.
            int total = list.Count;
            int buriedStart = (int)Math.Ceiling(total * 0.7);
            for (int i = buriedStart; i < total; i++)
            {
                if (HasAny(list[i].Text, "never", "must", "critical", "do not", "always"))
                {
                    findings.Add(new InstructionFinding
                    {
                        Code = "BURIED_CRITICAL", InstructionId = ids[i], Label = "Critical rule buried at the end",
                        Severity = 50,
                        Detail = "Hard rule appears late in the prompt; models weight earlier instructions more strongly.",
                    });
                }
            }

            // 10. OVERLOADED_INSTRUCTION: a single instruction with >=4 distinct directive verbs.
            for (int i = 0; i < list.Count; i++)
            {
                int verbs = CountDirectiveVerbs(list[i].Text);
                if (verbs >= 4)
                {
                    findings.Add(new InstructionFinding
                    {
                        Code = "OVERLOADED_INSTRUCTION", InstructionId = ids[i], Label = "Overloaded instruction",
                        Severity = 45,
                        Detail = $"Instruction packs {verbs} directives into one line; split for clarity.",
                    });
                }
            }

            // 11. ABSOLUTE_VS_CONDITIONAL: any "always X" later conditioned by "sometimes X" / "you may X".
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i].Text;
                if (!t.Contains("always", StringComparison.OrdinalIgnoreCase)) continue;
                var (_, iLemma) = ExtractDirective(t);
                if (iLemma == null) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    var u = list[j].Text;
                    if (!HasAny(u, "sometimes", "you may", "optionally", "occasionally", "in some cases")) continue;
                    var (_, jLemma) = ExtractDirective(u);
                    if (jLemma != null && jLemma == iLemma)
                    {
                        findings.Add(new InstructionFinding
                        {
                            Code = "ABSOLUTE_VS_CONDITIONAL", InstructionId = ids[j], Label = "Conditional softens an absolute rule",
                            Severity = 55,
                            Detail = $"{ids[j]} loosens the 'always' rule in {ids[i]} on the same action.",
                            RelatedInstructionIds = new[] { ids[i] },
                        });
                    }
                }
            }

            // ----- scoring -----
            int appetiteShift = RiskAppetite switch
            {
                InstructionRiskAppetite.Cautious => +8,
                InstructionRiskAppetite.Aggressive => -8,
                _ => 0,
            };
            // Sort by severity desc for top-3 mean
            var sevList = findings.Select(f => f.Severity).OrderByDescending(x => x).ToList();
            double mean = sevList.Count == 0 ? 0 : sevList.Take(3).Average();
            int max = sevList.Count == 0 ? 0 : sevList[0];
            int score = Clamp((int)Math.Round(mean * 0.6 + max * 0.4) + appetiteShift, 0, 100);

            bool hasContradiction = findings.Any(f => f.Code == "DIRECT_CONTRADICTION");
            int contradictionCount = findings.Count(f => f.Code == "DIRECT_CONTRADICTION");
            int dupCount = findings.Count(f => f.Code == "NEAR_DUPLICATE");
            int formatConflict = findings.Count(f => f.Code == "FORMAT_CONFLICT" || f.Code == "TOOL_PERMISSION_CONFLICT" || f.Code == "LANGUAGE_CONFLICT");

            // Verdict band
            InstructionConflictRisk verdict;
            if (hasContradiction && contradictionCount >= 2) verdict = InstructionConflictRisk.Broken;
            else if (score >= 70 || hasContradiction) verdict = InstructionConflictRisk.AtRisk;
            else if (score >= 50) verdict = InstructionConflictRisk.Degraded;
            else if (score >= 20) verdict = InstructionConflictRisk.Watch;
            else verdict = InstructionConflictRisk.Healthy;

            // Grade
            char grade =
                verdict == InstructionConflictRisk.Broken ? 'F' :
                score <= 15 ? 'A' :
                score <= 30 ? 'B' :
                score <= 50 ? 'C' :
                score <= 70 ? 'D' : 'F';

            // ----- per-instruction assessments -----
            var byTarget = findings
                .Where(f => !string.IsNullOrEmpty(f.InstructionId) && f.InstructionId != "*")
                .GroupBy(f => f.InstructionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var assessments = new List<InstructionAssessment>();
            foreach (var id in ids)
            {
                byTarget.TryGetValue(id, out var fs);
                fs ??= new List<InstructionFinding>();
                int rs = fs.Count == 0 ? 0 : (int)Math.Round(fs.Max(x => x.Severity) * 0.6 + fs.Average(x => (double)x.Severity) * 0.4);

                InstructionVerdict v;
                InstructionPriority p;
                string rationale;
                if (fs.Any(x => x.Code == "DIRECT_CONTRADICTION"))
                {
                    v = InstructionVerdict.Remove; p = InstructionPriority.P0;
                    rationale = "Directly contradicts another instruction; remove or replace.";
                }
                else if (fs.Any(x => x.Code == "NEAR_DUPLICATE"))
                {
                    v = InstructionVerdict.Remove; p = InstructionPriority.P0;
                    rationale = "Near-duplicate of an earlier instruction.";
                }
                else if (fs.Any(x => x.Code == "TONE_CONFLICT" || x.Code == "FORMAT_CONFLICT" ||
                                     x.Code == "LANGUAGE_CONFLICT" || x.Code == "TOOL_PERMISSION_CONFLICT" ||
                                     x.Code == "AUDIENCE_CONFLICT" || x.Code == "LENGTH_CONFLICT" ||
                                     x.Code == "CITATION_CONFLICT" || x.Code == "STYLE_CONFLICT" ||
                                     x.Code == "ABSOLUTE_VS_CONDITIONAL"))
                {
                    v = InstructionVerdict.Clarify; p = InstructionPriority.P1;
                    rationale = "Conflicts with a sibling instruction; clarify which one wins.";
                }
                else if (fs.Any(x => x.Code == "REDUNDANT_OVERLAP"))
                {
                    v = InstructionVerdict.Merge; p = InstructionPriority.P1;
                    rationale = "Overlaps with another instruction; merge into one.";
                }
                else if (fs.Any(x => x.Code == "BURIED_CRITICAL"))
                {
                    v = InstructionVerdict.Promote; p = InstructionPriority.P1;
                    rationale = "Critical rule buried at the end; promote to the top.";
                }
                else if (fs.Any(x => x.Code == "WEAK_LANGUAGE" || x.Code == "ESCAPE_HATCH_CONFLICT" ||
                                     x.Code == "UNDEFINED_REFERENCE" || x.Code == "OVERLOADED_INSTRUCTION" ||
                                     x.Code == "AMBIGUOUS_PRIORITY"))
                {
                    v = InstructionVerdict.Clarify; p = InstructionPriority.P2;
                    rationale = "Ambiguous or overloaded; rewrite for precision.";
                }
                else if (fs.Any(x => x.Code == "EMPTY_INSTRUCTION"))
                {
                    v = InstructionVerdict.Remove; p = InstructionPriority.P1;
                    rationale = "Empty instruction.";
                }
                else
                {
                    v = InstructionVerdict.Keep; p = InstructionPriority.P3;
                    rationale = "Clear, self-contained instruction.";
                }
                assessments.Add(new InstructionAssessment
                {
                    InstructionId = id, Verdict = v, Priority = p,
                    RiskScore = rs, Rationale = rationale,
                    Reasons = fs.Select(x => x.Code).Distinct().OrderBy(x => x).ToList(),
                });
            }

            // ----- playbook -----
            var playbook = new List<InstructionAction>();
            if (contradictionCount > 0)
            {
                playbook.Add(new InstructionAction
                {
                    Code = "RESOLVE_CONTRADICTIONS", Label = "Resolve direct contradictions",
                    Priority = InstructionPriority.P0, Owner = "prompt_author",
                    BlastRadius = 4, Reversibility = "medium",
                    Reason = $"{contradictionCount} contradiction(s) detected; pick one rule and remove the other.",
                    InstructionIds = findings.Where(f => f.Code == "DIRECT_CONTRADICTION").Select(f => f.InstructionId).Distinct().ToList(),
                });
            }
            if (dupCount > 0)
            {
                playbook.Add(new InstructionAction
                {
                    Code = "REMOVE_DUPLICATES", Label = "Remove near-duplicate instructions",
                    Priority = InstructionPriority.P0, Owner = "prompt_author",
                    BlastRadius = 2, Reversibility = "high",
                    Reason = $"{dupCount} near-duplicate(s); keep one canonical version.",
                    InstructionIds = findings.Where(f => f.Code == "NEAR_DUPLICATE").Select(f => f.InstructionId).Distinct().ToList(),
                });
            }
            if (formatConflict > 0)
            {
                playbook.Add(new InstructionAction
                {
                    Code = "RECONCILE_OUTPUT_CONTRACT", Label = "Reconcile output / tool / language contract",
                    Priority = InstructionPriority.P1, Owner = "prompt_author",
                    BlastRadius = 3, Reversibility = "high",
                    Reason = "Output format, language, or tool-permission rules disagree.",
                    InstructionIds = findings.Where(f => f.Code == "FORMAT_CONFLICT" || f.Code == "LANGUAGE_CONFLICT" || f.Code == "TOOL_PERMISSION_CONFLICT").Select(f => f.InstructionId).Distinct().ToList(),
                });
            }
            if (findings.Any(f => f.Code == "AMBIGUOUS_PRIORITY"))
            {
                playbook.Add(new InstructionAction
                {
                    Code = "DEFINE_PRIORITY_ORDER", Label = "Establish a single priority order",
                    Priority = InstructionPriority.P1, Owner = "prompt_author",
                    BlastRadius = 2, Reversibility = "high",
                    Reason = "Multiple instructions claim top priority; number them 1, 2, 3.",
                });
            }
            if (findings.Any(f => f.Code == "BURIED_CRITICAL"))
            {
                playbook.Add(new InstructionAction
                {
                    Code = "PROMOTE_BURIED_RULES", Label = "Move critical rules to the top",
                    Priority = InstructionPriority.P1, Owner = "prompt_author",
                    BlastRadius = 2, Reversibility = "high",
                    Reason = "Hard rules at the end of the prompt are weighted less by models.",
                    InstructionIds = findings.Where(f => f.Code == "BURIED_CRITICAL").Select(f => f.InstructionId).ToList(),
                });
            }
            if (findings.Any(f => f.Code == "WEAK_LANGUAGE"))
            {
                playbook.Add(new InstructionAction
                {
                    Code = "TIGHTEN_HEDGED_LANGUAGE", Label = "Replace hedges with hard verbs",
                    Priority = InstructionPriority.P2, Owner = "prompt_author",
                    BlastRadius = 1, Reversibility = "high",
                    Reason = "'try to', 'if possible', etc. make rules easy for models to skip.",
                });
            }
            if (findings.Any(f => f.Code == "ESCAPE_HATCH_CONFLICT"))
            {
                playbook.Add(new InstructionAction
                {
                    Code = "SPECIFY_ESCAPE_HATCHES", Label = "Specify each exception explicitly",
                    Priority = InstructionPriority.P2, Owner = "prompt_author",
                    BlastRadius = 1, Reversibility = "high",
                    Reason = "'unless'/'except' clauses without an explicit clause leak ambiguity.",
                });
            }
            if (findings.Any(f => f.Code == "OVERLOADED_INSTRUCTION"))
            {
                playbook.Add(new InstructionAction
                {
                    Code = "SPLIT_OVERLOADED", Label = "Split overloaded instructions",
                    Priority = InstructionPriority.P2, Owner = "prompt_author",
                    BlastRadius = 1, Reversibility = "high",
                    Reason = "Single lines carrying 4+ directives bury rules.",
                });
            }
            if (findings.Any(f => f.Code == "UNDEFINED_REFERENCE"))
            {
                playbook.Add(new InstructionAction
                {
                    Code = "ANCHOR_REFERENCES", Label = "Anchor back-references explicitly",
                    Priority = InstructionPriority.P2, Owner = "prompt_author",
                    BlastRadius = 1, Reversibility = "high",
                    Reason = "Replace 'as stated above' with explicit rule names or numbers.",
                });
            }
            if (RiskAppetite == InstructionRiskAppetite.Cautious && grade is 'C' or 'D' or 'F')
            {
                playbook.Add(new InstructionAction
                {
                    Code = "SOLICIT_SECOND_REVIEWER", Label = "Have a second reviewer check the prompt",
                    Priority = InstructionPriority.P2, Owner = "reviewer",
                    BlastRadius = 1, Reversibility = "high",
                    Reason = "Cautious mode + grade " + grade + " → ask a second pair of eyes.",
                });
            }
            if (playbook.Count == 0)
            {
                playbook.Add(new InstructionAction
                {
                    Code = "PROMPT_OK", Label = "Prompt is internally consistent",
                    Priority = InstructionPriority.P3, Owner = "prompt_author",
                    BlastRadius = 1, Reversibility = "high",
                    Reason = "No conflicts detected.",
                });
            }
            if (RiskAppetite == InstructionRiskAppetite.Aggressive)
            {
                playbook = playbook.Where(a => a.Priority != InstructionPriority.P3 || playbook.Count == 1).ToList();
                playbook = playbook.Where(a => a.Priority != InstructionPriority.P2).Concat(playbook.Where(a => a.Priority == InstructionPriority.P2).Take(0)).ToList();
            }
            playbook = playbook
                .OrderBy(a => (int)a.Priority)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .ToList();

            // ----- insights -----
            var insights = new List<string>();
            if (contradictionCount > 0) insights.Add($"contradiction_count: {contradictionCount}");
            if (dupCount > 0) insights.Add($"near_duplicate_count: {dupCount}");
            if (formatConflict > 0) insights.Add($"output_contract_conflict_count: {formatConflict}");
            int weakCount = findings.Count(f => f.Code == "WEAK_LANGUAGE");
            if (weakCount > 0) insights.Add($"weak_language_count: {weakCount}");
            if (findings.Any(f => f.Code == "BURIED_CRITICAL"))
            {
                insights.Add($"buried_critical_rules: {findings.Count(f => f.Code == "BURIED_CRITICAL")}");
            }
            if (list.Count <= 3) insights.Add("prompt_too_small_for_meaningful_split");
            if (list.Count >= 25) insights.Add("prompt_very_long_consider_modularising");

            // ----- deconflicted draft -----
            var removed = new HashSet<string>();
            var revised = new HashSet<string>();
            var reordered = new HashSet<string>();

            // Remove DIRECT_CONTRADICTION targets (later one) and NEAR_DUPLICATE targets (later one).
            foreach (var a in assessments.Where(a => a.Verdict == InstructionVerdict.Remove))
            {
                removed.Add(a.InstructionId);
            }
            foreach (var a in assessments.Where(a => a.Verdict is InstructionVerdict.Clarify or InstructionVerdict.Merge))
            {
                revised.Add(a.InstructionId);
            }
            // Promote: pull buried critical rules to the front, keep their order amongst themselves.
            var promoteIds = assessments.Where(a => a.Verdict == InstructionVerdict.Promote).Select(a => a.InstructionId).ToHashSet();
            var indexed = list.Select((e, idx) => new { e, idx, id = ids[idx] }).ToList();
            var promoted = indexed.Where(x => promoteIds.Contains(x.id) && !removed.Contains(x.id)).Select(x => x.e).ToList();
            var rest = indexed.Where(x => !promoteIds.Contains(x.id) && !removed.Contains(x.id)).Select(x => x.e).ToList();
            foreach (var p in promoted) reordered.Add(p.Id);
            var ordered = promoted.Concat(rest).ToList();

            var draftSb = new StringBuilder();
            draftSb.AppendLine("# Priority order (highest first)");
            for (int i = 0; i < ordered.Count; i++)
            {
                draftSb.Append(i + 1).Append(". ").AppendLine(ordered[i].Text.Trim());
            }
            if (revised.Count > 0)
            {
                draftSb.AppendLine();
                draftSb.AppendLine("# Clarifications needed");
                foreach (var rid in revised.OrderBy(x => x, StringComparer.Ordinal))
                {
                    var ass = assessments.First(a => a.InstructionId == rid);
                    draftSb.Append("- [ ] ").Append(rid).Append(": ").AppendLine(ass.Rationale);
                }
            }
            var draft = new DeconflictedPromptDraft
            {
                Instructions = ordered,
                RemovedInstructionIds = removed.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                RevisedInstructionIds = revised.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                ReorderedInstructionIds = reordered.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                PromptText = draftSb.ToString().TrimEnd(),
                Note = $"removed {removed.Count}, revised {revised.Count}, reordered={(reordered.Count > 0 ? "true" : "false")}",
            };

            report.OverallScore = score;
            report.Grade = grade;
            report.Verdict = verdict;
            report.Findings = findings
                .OrderByDescending(f => f.Severity)
                .ThenBy(f => f.Code, StringComparer.Ordinal)
                .ThenBy(f => f.InstructionId, StringComparer.Ordinal)
                .ToList();
            report.Assessments = assessments;
            report.Playbook = playbook;
            report.Insights = insights;
            report.Draft = draft;
            return report;
        }

        // ---- helpers ----
        private static HashSet<string> Tokenize(string s)
            => TokenRe.Matches(s.ToLowerInvariant()).Select(m => m.Value).ToHashSet();

        private static double Jaccard(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0.0;
            int inter = 0;
            foreach (var t in a) if (b.Contains(t)) inter++;
            int union = a.Count + b.Count - inter;
            return union == 0 ? 0.0 : (double)inter / union;
        }

        private static HashSet<string> ClassifyTags(string text)
        {
            var t = text.ToLowerInvariant();
            var tags = new HashSet<string>();
            void Add(string tag) => tags.Add(tag);

            if (Has(t, "formal")) Add("formal");
            if (Has(t, "casual") || Has(t, "informal") || Has(t, "friendly tone")) Add("casual");
            if (Has(t, "brief") || Has(t, "be brief")) Add("brief");
            if (Has(t, "detailed") || Has(t, "in detail") || Has(t, "exhaustive")) Add("detailed");
            if (Has(t, "short")) Add("short");
            if (Has(t, "long")) Add("long");
            if (Has(t, "concise")) Add("concise");
            if (Has(t, "verbose") || Has(t, "elaborate")) Add("verbose");
            if (Has(t, "polite")) Add("polite");
            if (Has(t, "blunt") || Has(t, "direct, no")) Add("blunt");
            if (Has(t, "technical")) Add("technical");
            if (Has(t, "non-technical") || Has(t, "for beginners") || Has(t, "for a layperson") || Has(t, "plain english")) Add("non-technical");
            if (Has(t, "in english") || Has(t, "respond in english")) Add("english");
            if (Has(t, "in spanish") || Has(t, "respond in spanish") || Has(t, "en español")) Add("spanish");
            if (Has(t, "in french") || Has(t, "respond in french")) Add("french");
            if (Has(t, "in german") || Has(t, "respond in german")) Add("german");
            if (Has(t, "json") || Has(t, "as json")) Add("json");
            if (Has(t, "markdown")) Add("markdown");
            if (Has(t, "plaintext") || Has(t, "plain text") || Has(t, "no markdown")) Add("plaintext");
            if (Has(t, "bullets") || Has(t, "bulleted list") || Has(t, "use bullets")) Add("bullets");
            if (Has(t, "prose") || Has(t, "in paragraphs") || Has(t, "no bullets")) Add("prose");
            if (Has(t, "use tools") || Has(t, "call tools") || Has(t, "invoke tools") || Has(t, "use the calculator") || Has(t, "use search")) Add("use_tools");
            if (Has(t, "do not call any tools") || Has(t, "no tool calls") || Has(t, "do not use tools") || Has(t, "without tools")) Add("no_tools");
            if (Has(t, "cite sources") || Has(t, "include citations") || Has(t, "with citations")) Add("cite");
            if (Has(t, "no citations") || Has(t, "do not cite") || Has(t, "without citations")) Add("no_citation");
            if (Has(t, "use emoji") || Has(t, "with emoji")) Add("emoji");
            if (Has(t, "no emoji") || Has(t, "without emoji") || Has(t, "do not use emoji")) Add("no_emoji");
            return tags;
        }

        private static bool Has(string text, string needle)
            => text.Contains(needle, StringComparison.OrdinalIgnoreCase);

        private static bool HasAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

        // Returns (polarity, lemma) where polarity is +1 for affirmative directive and -1 for negative,
        // or (0, null) if no clear directive can be extracted.
        private static (int Polarity, string? Lemma) ExtractDirective(string text)
        {
            var t = text.ToLowerInvariant().Trim();
            // negative markers
            string[] neg = { "never ", "do not ", "don't ", "must not ", "avoid ", "refrain from " };
            string[] pos = { "always ", "must ", "should ", "you must ", "ensure you ", "make sure to " };
            int polarity = 0;
            string? rest = null;
            foreach (var n in neg)
            {
                int i = t.IndexOf(n, StringComparison.Ordinal);
                if (i >= 0) { polarity = -1; rest = t.Substring(i + n.Length); break; }
            }
            if (polarity == 0)
            {
                foreach (var p in pos)
                {
                    int i = t.IndexOf(p, StringComparison.Ordinal);
                    if (i >= 0) { polarity = +1; rest = t.Substring(i + p.Length); break; }
                }
            }
            if (rest == null) return (0, null);
            // first significant token (skip very common stopwords)
            var toks = TokenRe.Matches(rest).Select(m => m.Value).ToList();
            var skip = new HashSet<string> { "the", "a", "an", "to", "be", "in", "on", "of", "any", "all", "every", "this", "that", "user", "users", "you", "your", "their", "ever", "always", "never" };
            var lemma = toks.FirstOrDefault(x => !skip.Contains(x) && x.Length > 2);
            return (polarity, lemma);
        }

        private static int CountDirectiveVerbs(string text)
        {
            string[] verbs = { "always", "never", "must", "should", "ensure", "include", "avoid", "use", "do not", "respond", "answer", "explain", "summarize", "summarise", "cite", "ask" };
            int n = 0;
            foreach (var v in verbs)
            {
                if (text.Contains(v, StringComparison.OrdinalIgnoreCase)) n++;
            }
            return n;
        }

        private static string NormalizeText(string s)
        {
            var toks = TokenRe.Matches(s.ToLowerInvariant()).Select(m => m.Value);
            return string.Join(" ", toks);
        }

        private static string Trunc(string s) => s.Length <= 60 ? s : s.Substring(0, 57) + "...";

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
