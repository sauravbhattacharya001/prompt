namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>A single few-shot example used by <see cref="PromptExampleQualityAdvisor"/>. This is a lightweight DTO distinct from <c>FewShotBuilder.FewShotExample</c>.</summary>
    public sealed class QualityExample
    {
        /// <summary>Optional stable id. Falls back to <c>ex-{index}</c> when empty.</summary>
        public string Id { get; init; } = "";
        /// <summary>Example input.</summary>
        public string Input { get; init; } = "";
        /// <summary>Example output / demonstration.</summary>
        public string Output { get; init; } = "";
        /// <summary>Optional category/class label.</summary>
        public string? Label { get; init; }
    }

    /// <summary>Verdict bucket for a single example.</summary>
    public enum ExampleVerdict
    {
        /// <summary>Example is healthy as-is.</summary>
        Keep,
        /// <summary>Example should be rewritten to fix quality issues.</summary>
        Revise,
        /// <summary>Example should be replaced (e.g. contradicts another).</summary>
        Replace,
        /// <summary>Example should be dropped.</summary>
        Remove,
        /// <summary>Example should be repositioned within the set.</summary>
        Reorder,
    }

    /// <summary>Priority bucket for example assessments and playbook actions.</summary>
    public enum ExamplePriority
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

    /// <summary>Aggregate health band for the example set.</summary>
    public enum ExampleQualityRisk
    {
        /// <summary>No meaningful issues.</summary>
        Healthy,
        /// <summary>Minor issues worth watching.</summary>
        Watch,
        /// <summary>Notable issues affecting quality.</summary>
        Degraded,
        /// <summary>Several severe issues; curate before use.</summary>
        AtRisk,
        /// <summary>Set should not be used as-is.</summary>
        Broken,
    }

    /// <summary>Risk appetite knob mirroring sibling advisors.</summary>
    public enum ExampleRiskAppetite
    {
        /// <summary>Stricter scoring and extra reviewer step on borderline grades.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient scoring and trims P2 from playbook.</summary>
        Aggressive,
    }

    /// <summary>One detected quality issue in the example set.</summary>
    public sealed class ExampleFinding
    {
        /// <summary>Detector code (e.g. NEAR_DUPLICATE).</summary>
        public string Code { get; internal set; } = "";
        /// <summary>Id of the affected example, or <c>*</c> for set-level findings.</summary>
        public string ExampleId { get; internal set; } = "";
        /// <summary>Human-readable label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Severity 0-100.</summary>
        public int Severity { get; internal set; }
        /// <summary>Human-readable detail / reason.</summary>
        public string Detail { get; internal set; } = "";
        /// <summary>Optional related example ids (e.g. the duplicate keeper).</summary>
        public IReadOnlyList<string> RelatedExampleIds { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>Per-example verdict synthesised from its findings.</summary>
    public sealed class ExampleAssessment
    {
        /// <summary>Affected example id.</summary>
        public string ExampleId { get; internal set; } = "";
        /// <summary>Recommended verdict.</summary>
        public ExampleVerdict Verdict { get; internal set; }
        /// <summary>Priority bucket.</summary>
        public ExamplePriority Priority { get; internal set; }
        /// <summary>Aggregated risk score 0-100.</summary>
        public int RiskScore { get; internal set; }
        /// <summary>Short rationale.</summary>
        public string Rationale { get; internal set; } = "";
        /// <summary>Underlying detector codes.</summary>
        public IReadOnlyList<string> Reasons { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>One playbook action recommended for the set as a whole.</summary>
    public sealed class ExampleAction
    {
        /// <summary>Action code.</summary>
        public string Code { get; internal set; } = "";
        /// <summary>Human-readable label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Priority.</summary>
        public ExamplePriority Priority { get; internal set; }
        /// <summary>Owning role (prompt_author / data_curator / reviewer).</summary>
        public string Owner { get; internal set; } = "";
        /// <summary>Blast radius 1-5.</summary>
        public int BlastRadius { get; internal set; }
        /// <summary>Reversibility (low/medium/high).</summary>
        public string Reversibility { get; internal set; } = "";
        /// <summary>Human-readable reason.</summary>
        public string Reason { get; internal set; } = "";
        /// <summary>Affected example ids.</summary>
        public IReadOnlyList<string> ExampleIds { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>A cleaned, paste-ready few-shot example set.</summary>
    public sealed class CuratedExampleSet
    {
        /// <summary>Surviving examples after curation.</summary>
        public IReadOnlyList<QualityExample> Examples { get; internal set; } = Array.Empty<QualityExample>();
        /// <summary>Ids dropped during curation.</summary>
        public IReadOnlyList<string> RemovedExampleIds { get; internal set; } = Array.Empty<string>();
        /// <summary>Ids flagged for human revision.</summary>
        public IReadOnlyList<string> RevisedExampleIds { get; internal set; } = Array.Empty<string>();
        /// <summary>Short note summarising what changed.</summary>
        public string Note { get; internal set; } = "";
    }

    /// <summary>The full advisor report.</summary>
    public sealed class ExampleQualityReport
    {
        /// <summary>Aggregate score 0-100 (higher = worse).</summary>
        public int OverallScore { get; internal set; }
        /// <summary>A-F grade.</summary>
        public char Grade { get; internal set; }
        /// <summary>Aggregate verdict band.</summary>
        public ExampleQualityRisk Verdict { get; internal set; }
        /// <summary>All detected findings.</summary>
        public IReadOnlyList<ExampleFinding> Findings { get; internal set; } = Array.Empty<ExampleFinding>();
        /// <summary>Per-example assessments.</summary>
        public IReadOnlyList<ExampleAssessment> Assessments { get; internal set; } = Array.Empty<ExampleAssessment>();
        /// <summary>Recommended cross-set actions.</summary>
        public IReadOnlyList<ExampleAction> Playbook { get; internal set; } = Array.Empty<ExampleAction>();
        /// <summary>Cross-set insights.</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();
        /// <summary>Curated example set ready for use.</summary>
        public CuratedExampleSet Curated { get; internal set; } = new();
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
            sb.Append("Example quality: ").Append(Grade).Append(" - ").Append(OverallScore).Append("/100 (").Append(Verdict).AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("Top examples by risk:");
            foreach (var a in Assessments.OrderByDescending(x => x.RiskScore).Take(10))
            {
                sb.Append("  ").Append(a.ExampleId).Append(" [").Append(a.Verdict).Append('/').Append(a.Priority).Append("] score=").Append(a.RiskScore).Append(' ').AppendLine(a.Rationale);
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
            sb.AppendLine("## PromptExampleQualityAdvisor");
            sb.Append("**Grade:** ").Append(Grade).Append("  **Score:** ").Append(OverallScore).Append("/100  **Verdict:** ").AppendLine(Verdict.ToString());
            sb.AppendLine();
            sb.AppendLine("### Assessments");
            sb.AppendLine("| Id | Verdict | Priority | Score | Reasons |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var a in Assessments.OrderByDescending(x => x.RiskScore))
            {
                sb.Append("| ").Append(a.ExampleId).Append(" | ").Append(a.Verdict).Append(" | ").Append(a.Priority).Append(" | ").Append(a.RiskScore).Append(" | ").Append(string.Join(", ", a.Reasons)).AppendLine(" |");
            }
            sb.AppendLine();
            sb.AppendLine("### Findings");
            foreach (var f in Findings)
            {
                sb.Append("- **").Append(f.Code).Append("** (").Append(f.ExampleId).Append(", sev=").Append(f.Severity).Append("): ").AppendLine(f.Detail);
            }
            sb.AppendLine();
            sb.AppendLine("### Playbook");
            foreach (var p in Playbook)
            {
                sb.Append("- [").Append(p.Priority).Append("] **").Append(p.Code).Append("** — ").Append(p.Label).Append(" _(owner: ").Append(p.Owner).Append(", blast: ").Append(p.BlastRadius).Append(", rev: ").Append(p.Reversibility).AppendLine(")_");
            }
            if (Insights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Insights");
                foreach (var i in Insights) { sb.Append("- ").AppendLine(i); }
            }
            sb.AppendLine();
            sb.Append("### Curated set (").Append(Curated.Examples.Count).AppendLine(" examples)");
            sb.AppendLine(Curated.Note);
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
                    exampleId = f.ExampleId,
                    label = f.Label,
                    severity = f.Severity,
                    detail = f.Detail,
                    relatedExampleIds = f.RelatedExampleIds,
                }).ToArray(),
                assessments = Assessments.Select(a => new
                {
                    exampleId = a.ExampleId,
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
                    exampleIds = p.ExampleIds,
                }).ToArray(),
                insights = Insights,
                curated = new
                {
                    note = Curated.Note,
                    removed = Curated.RemovedExampleIds,
                    revised = Curated.RevisedExampleIds,
                    examples = Curated.Examples.Select(e => new
                    {
                        id = e.Id,
                        input = e.Input,
                        output = e.Output,
                        label = e.Label,
                    }).ToArray(),
                },
            };
            return JsonSerializer.Serialize(dto, JsonOpts);
        }
    }

    /// <summary>
    /// Agentic few-shot example curator. Detects quality issues across a set of examples
    /// (duplicates, contradictions, leakage, format inconsistency, label imbalance, etc.),
    /// scores them, and produces a paste-ready cleaned <see cref="CuratedExampleSet"/>.
    /// Sibling to <see cref="PromptHallucinationRiskScorer"/>, <see cref="PromptDefenseAdvisor"/>,
    /// and <see cref="PromptKnowledgeFreshnessAdvisor"/>.
    /// </summary>
    public sealed class PromptExampleQualityAdvisor
    {
        private readonly Func<DateTime> _now;

        /// <summary>Create a new advisor with an optional injectable clock.</summary>
        public PromptExampleQualityAdvisor(Func<DateTime>? nowProvider = null)
        {
            _now = nowProvider ?? (() => DateTime.UtcNow);
        }

        /// <summary>Risk appetite knob; mutates scoring and playbook composition.</summary>
        public ExampleRiskAppetite RiskAppetite { get; set; } = ExampleRiskAppetite.Balanced;

        private static readonly Regex TokenRe = new(@"\w+", RegexOptions.Compiled);

        /// <summary>Analyse a set of few-shot examples.</summary>
        public ExampleQualityReport Analyze(IEnumerable<QualityExample>? examples)
        {
            var list = (examples ?? Array.Empty<QualityExample>()).ToList();
            var report = new ExampleQualityReport { GeneratedAtUtc = _now() };

            // assign stable ids
            var ids = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ids[i] = string.IsNullOrWhiteSpace(list[i].Id) ? $"ex-{i}" : list[i].Id;
            }

            if (list.Count == 0)
            {
                report.OverallScore = 0;
                report.Grade = 'A';
                report.Verdict = ExampleQualityRisk.Healthy;
                report.Curated = new CuratedExampleSet { Note = "removed 0, revised 0, reordered=false" };
                return report;
            }

            var findings = new List<ExampleFinding>();

            // Pre-compute token sets, formats, lengths
            var tokenSets = list.Select(e => Tokenize(e.Input + " " + e.Output)).ToList();
            var formats = list.Select(e => ClassifyFormat(e.Output)).ToList();
            var tokenCounts = list.Select(e => TokenCount(e.Input) + TokenCount(e.Output)).ToList();
            var inputTokens = list.Select(e => Tokenize(e.Input)).ToList();
            var outputTokens = list.Select(e => Tokenize(e.Output)).ToList();

            // 1. EMPTY_FIELD
            for (int i = 0; i < list.Count; i++)
            {
                bool emptyIn = string.IsNullOrWhiteSpace(list[i].Input);
                bool emptyOut = string.IsNullOrWhiteSpace(list[i].Output);
                if (emptyIn || emptyOut)
                {
                    findings.Add(new ExampleFinding
                    {
                        Code = "EMPTY_FIELD", ExampleId = ids[i], Label = "Empty field",
                        Severity = 90,
                        Detail = emptyIn && emptyOut ? "Both input and output are blank." : emptyIn ? "Input is blank." : "Output is blank.",
                    });
                }
            }

            // 2. NEAR_DUPLICATE
            var dupKeeper = new Dictionary<int, int>(); // dup index -> keeper index
            for (int i = 0; i < list.Count; i++)
            {
                if (tokenSets[i].Count == 0) continue;
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (tokenSets[j].Count == 0) continue;
                    double jac = Jaccard(tokenSets[i], tokenSets[j]);
                    if (jac >= 0.85)
                    {
                        int sev = (int)Math.Min(100, 60 + (jac - 0.85) * 100);
                        findings.Add(new ExampleFinding
                        {
                            Code = "NEAR_DUPLICATE", ExampleId = ids[j], Label = "Near-duplicate example",
                            Severity = sev,
                            Detail = $"Highly similar to {ids[i]} (Jaccard {jac:F2}).",
                            RelatedExampleIds = new[] { ids[i] },
                        });
                        if (!dupKeeper.ContainsKey(j)) dupKeeper[j] = i;
                    }
                }
            }

            // 3. CONTRADICTORY_EXAMPLE
            var contradictions = new HashSet<int>();
            var inputGroups = list
                .Select((e, idx) => new { e, idx })
                .GroupBy(x => NormalizeText(x.e.Input))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key));
            foreach (var g in inputGroups)
            {
                var outs = g.Select(x => new { x.idx, norm = NormalizeText(x.e.Output) }).ToList();
                var distinct = outs.Select(o => o.norm).Distinct().ToList();
                if (distinct.Count > 1)
                {
                    var allIds = g.Select(x => ids[x.idx]).ToArray();
                    foreach (var x in g)
                    {
                        contradictions.Add(x.idx);
                        findings.Add(new ExampleFinding
                        {
                            Code = "CONTRADICTORY_EXAMPLE", ExampleId = ids[x.idx], Label = "Contradictory example",
                            Severity = 95,
                            Detail = $"Same input as {string.Join(", ", allIds.Where(a => a != ids[x.idx]))} but different output.",
                            RelatedExampleIds = allIds.Where(a => a != ids[x.idx]).ToArray(),
                        });
                    }
                }
            }

            // 4. LABEL_LEAKAGE
            for (int i = 0; i < list.Count; i++)
            {
                var output = list[i].Output ?? "";
                var input = list[i].Input ?? "";
                var trimmed = output.Trim();
                if (trimmed.Length >= 4 && !string.IsNullOrWhiteSpace(input))
                {
                    if (input.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        findings.Add(new ExampleFinding
                        {
                            Code = "LABEL_LEAKAGE", ExampleId = ids[i], Label = "Label leakage",
                            Severity = 70,
                            Detail = "Output text appears verbatim inside the input.",
                        });
                    }
                }
            }

            // 5. FORMAT_INCONSISTENCY
            var bucketCounts = formats.Where(f => f != null).GroupBy(f => f!).ToDictionary(g => g.Key, g => g.Count());
            int totalFormat = bucketCounts.Values.Sum();
            string? dominantBucket = null;
            int dominantBucketCount = 0;
            double dominantBucketShare = 0;
            if (totalFormat > 0)
            {
                var top = bucketCounts.MaxBy(kv => kv.Value)!;
                dominantBucket = top.Key;
                dominantBucketCount = top.Value;
                dominantBucketShare = (double)top.Value / totalFormat;
            }
            bool formatInconsistent = bucketCounts.Count >= 2 && dominantBucketShare < 0.80;
            if (formatInconsistent)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (formats[i] != null && formats[i] != dominantBucket)
                    {
                        findings.Add(new ExampleFinding
                        {
                            Code = "FORMAT_INCONSISTENCY", ExampleId = ids[i], Label = "Output format inconsistent",
                            Severity = 50,
                            Detail = $"Output is {formats[i]} but dominant format is {dominantBucket} ({(int)(dominantBucketShare * 100)}%).",
                        });
                    }
                }
            }

            // 6. LABEL_IMBALANCE
            string? dominantLabel = null;
            double dominantLabelShare = 0;
            int labeledCount = list.Count(e => !string.IsNullOrWhiteSpace(e.Label));
            bool labelImbalance = false;
            if (list.Count > 0 && (double)labeledCount / list.Count >= 0.70)
            {
                var groups = list.Where(e => !string.IsNullOrWhiteSpace(e.Label))
                    .GroupBy(e => e.Label!).ToList();
                var top = groups.OrderByDescending(g => g.Count()).First();
                dominantLabel = top.Key;
                dominantLabelShare = (double)top.Count() / labeledCount;
                double threshold = RiskAppetite == ExampleRiskAppetite.Cautious ? 0.60 : 0.70;
                if (dominantLabelShare >= threshold && groups.Count >= 1)
                {
                    labelImbalance = true;
                    findings.Add(new ExampleFinding
                    {
                        Code = "LABEL_IMBALANCE", ExampleId = "*", Label = "Label imbalance",
                        Severity = 55,
                        Detail = $"{(int)(dominantLabelShare * 100)}% of labeled examples carry label '{dominantLabel}'.",
                    });
                }
            }

            // 7. LENGTH_VARIANCE
            if (list.Count >= 2)
            {
                var nonZero = tokenCounts.Where(c => c > 0).ToList();
                if (nonZero.Count >= 2)
                {
                    double max = nonZero.Max();
                    double min = Math.Max(1, nonZero.Min());
                    double mean = nonZero.Average();
                    double sd = Math.Sqrt(nonZero.Select(c => (c - mean) * (c - mean)).Sum() / nonZero.Count);
                    double cv = mean > 0 ? sd / mean : 0;
                    if (max / min >= 5 && cv >= 0.6)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            double tc = tokenCounts[i];
                            if (tc > 2 * mean || (tc > 0 && tc < 0.5 * mean))
                            {
                                findings.Add(new ExampleFinding
                                {
                                    Code = "LENGTH_VARIANCE", ExampleId = ids[i], Label = "Length variance",
                                    Severity = 40,
                                    Detail = $"Example length {(int)tc} tokens vs mean {(int)mean}.",
                                });
                            }
                        }
                    }
                }
            }

            // 8. AMBIGUOUS_DEMONSTRATION
            for (int i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(list[i].Input) || string.IsNullOrWhiteSpace(list[i].Output)) continue;
                if (formats[i] != "sentence") continue;
                if (outputTokens[i].Count < 4) continue;
                var inT = inputTokens[i];
                if (inT.Count == 0) continue;
                int shared = outputTokens[i].Intersect(inT).Count();
                double share = (double)shared / outputTokens[i].Count;
                if (share <= 0.10)
                {
                    findings.Add(new ExampleFinding
                    {
                        Code = "AMBIGUOUS_DEMONSTRATION", ExampleId = ids[i], Label = "Ambiguous demonstration",
                        Severity = 45,
                        Detail = $"Output shares only {(int)(share * 100)}% of tokens with input.",
                    });
                }
            }

            // 9. ORDER_RECENCY_BIAS
            var recencyBiasTailIdx = new List<int>();
            if (list.Count >= 4 && labeledCount > 0 && (double)labeledCount / list.Count >= 0.70)
            {
                int tailN = list.Count < 8 ? 2 : Math.Max(2, list.Count / 4);
                var tail = Enumerable.Range(list.Count - tailN, tailN).ToList();
                var tailLabels = tail.Select(i => list[i].Label).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
                if (tailLabels.Count == 1)
                {
                    findings.Add(new ExampleFinding
                    {
                        Code = "ORDER_RECENCY_BIAS", ExampleId = "*", Label = "Order recency bias",
                        Severity = 50,
                        Detail = $"Last {tailN} examples all share label '{tailLabels[0]}'.",
                        RelatedExampleIds = tail.Select(i => ids[i]).ToArray(),
                    });
                    recencyBiasTailIdx = tail;
                }
            }

            // 10. LOW_DIVERSITY
            bool lowDiversity = false;
            if (list.Count >= 3)
            {
                double sumJ = 0; int pairs = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (tokenSets[i].Count == 0 || tokenSets[j].Count == 0) continue;
                        sumJ += Jaccard(tokenSets[i], tokenSets[j]);
                        pairs++;
                    }
                }
                if (pairs > 0)
                {
                    double avg = sumJ / pairs;
                    if (avg >= 0.45)
                    {
                        lowDiversity = true;
                        findings.Add(new ExampleFinding
                        {
                            Code = "LOW_DIVERSITY", ExampleId = "*", Label = "Low diversity",
                            Severity = 60,
                            Detail = $"Average pairwise Jaccard {avg:F2} >= 0.45.",
                        });
                    }
                }
            }

            report.Findings = findings.ToArray();

            // Per-example assessments
            var assessments = new List<ExampleAssessment>();
            for (int i = 0; i < list.Count; i++)
            {
                var own = findings.Where(f => f.ExampleId == ids[i]).ToList();
                ExampleVerdict verdict = ExampleVerdict.Keep;
                ExamplePriority priority = ExamplePriority.P3;
                int score = 0;
                string rationale = "no issues";
                var codes = own.Select(f => f.Code).Distinct().ToList();
                if (own.Count > 0)
                {
                    var topSev = own.Max(f => f.Severity);
                    score = topSev;
                    if (own.Any(f => f.Code == "EMPTY_FIELD" || f.Code == "NEAR_DUPLICATE"))
                    {
                        verdict = ExampleVerdict.Remove;
                        priority = ExamplePriority.P0;
                    }
                    else if (own.Any(f => f.Code == "CONTRADICTORY_EXAMPLE"))
                    {
                        verdict = ExampleVerdict.Replace;
                        priority = ExamplePriority.P0;
                    }
                    else if (own.Any(f => f.Code == "LABEL_LEAKAGE" || f.Code == "FORMAT_INCONSISTENCY"
                                       || f.Code == "AMBIGUOUS_DEMONSTRATION" || f.Code == "LENGTH_VARIANCE"))
                    {
                        verdict = ExampleVerdict.Revise;
                        priority = topSev >= 60 ? ExamplePriority.P1 : ExamplePriority.P2;
                    }
                    rationale = string.Join("; ", own.Select(f => f.Detail));
                }

                // Reorder verdict applies to recency-biased tail (overrides Keep)
                if (recencyBiasTailIdx.Contains(i) && verdict == ExampleVerdict.Keep)
                {
                    verdict = ExampleVerdict.Reorder;
                    priority = ExamplePriority.P2;
                    score = Math.Max(score, 35);
                    rationale = "Tail-of-set recency bias.";
                    codes.Add("ORDER_RECENCY_BIAS");
                }

                assessments.Add(new ExampleAssessment
                {
                    ExampleId = ids[i],
                    Verdict = verdict,
                    Priority = priority,
                    RiskScore = score,
                    Rationale = rationale,
                    Reasons = codes,
                });
            }
            report.Assessments = assessments.ToArray();

            // OverallScore
            int overall = 0;
            if (findings.Count > 0)
            {
                var sevs = findings.Select(f => f.Severity).OrderByDescending(x => x).ToList();
                double meanTop3 = sevs.Take(3).Average();
                double maxSev = sevs[0];
                overall = (int)Math.Round(meanTop3 * 0.6 + maxSev * 0.4);
            }
            if (RiskAppetite == ExampleRiskAppetite.Cautious) overall = Math.Min(100, overall + 8);
            else if (RiskAppetite == ExampleRiskAppetite.Aggressive) overall = Math.Max(0, overall - 8);
            report.OverallScore = overall;

            // Verdict band
            ExampleQualityRisk band;
            if (overall <= 15) band = ExampleQualityRisk.Healthy;
            else if (overall <= 30) band = ExampleQualityRisk.Watch;
            else if (overall <= 50) band = ExampleQualityRisk.Degraded;
            else if (overall <= 70) band = ExampleQualityRisk.AtRisk;
            else band = ExampleQualityRisk.Broken;
            report.Verdict = band;

            // Grade
            char grade = overall switch
            {
                <= 15 => 'A',
                <= 30 => 'B',
                <= 50 => 'C',
                <= 70 => 'D',
                _ => 'F',
            };
            int dupCount = findings.Count(f => f.Code == "NEAR_DUPLICATE");
            int leakCount = findings.Count(f => f.Code == "LABEL_LEAKAGE");
            bool hasContradiction = findings.Any(f => f.Code == "CONTRADICTORY_EXAMPLE");
            int forceFThreshold = (int)Math.Ceiling(list.Count / 2.0);
            if (hasContradiction || (dupCount + leakCount) >= forceFThreshold && (dupCount + leakCount) > 0)
            {
                grade = 'F';
            }
            report.Grade = grade;

            // Playbook
            var playbook = new List<ExampleAction>();
            void Add(string code, string label, ExamplePriority pri, string owner, int blast, string rev, string reason, IEnumerable<string> exIds)
            {
                playbook.Add(new ExampleAction
                {
                    Code = code, Label = label, Priority = pri, Owner = owner,
                    BlastRadius = blast, Reversibility = rev, Reason = reason,
                    ExampleIds = exIds.Distinct().ToArray(),
                });
            }
            var dupIds = findings.Where(f => f.Code == "NEAR_DUPLICATE").Select(f => f.ExampleId);
            if (dupCount > 0) Add("REMOVE_DUPLICATES", "Drop near-duplicate examples", ExamplePriority.P0, "data_curator", 2, "high", "Near-duplicate examples bias the model toward repeated patterns.", dupIds);
            var contraIds = findings.Where(f => f.Code == "CONTRADICTORY_EXAMPLE").Select(f => f.ExampleId);
            if (hasContradiction) Add("RESOLVE_CONTRADICTIONS", "Resolve contradictory examples", ExamplePriority.P0, "prompt_author", 4, "medium", "Contradictory examples make the desired behavior ambiguous.", contraIds);
            var emptyIds = findings.Where(f => f.Code == "EMPTY_FIELD").Select(f => f.ExampleId);
            if (findings.Any(f => f.Code == "EMPTY_FIELD")) Add("BACKFILL_REQUIRED_FIELDS", "Backfill empty input/output fields", ExamplePriority.P0, "data_curator", 3, "high", "Examples with empty fields cannot teach the pattern.", emptyIds);
            var leakIds = findings.Where(f => f.Code == "LABEL_LEAKAGE").Select(f => f.ExampleId);
            if (leakCount > 0) Add("REWRITE_LEAKED_OUTPUTS", "Rewrite outputs that leak into the input", ExamplePriority.P1, "prompt_author", 2, "high", "Output text appears verbatim inside the input.", leakIds);
            var fmtIds = findings.Where(f => f.Code == "FORMAT_INCONSISTENCY").Select(f => f.ExampleId);
            if (formatInconsistent) Add("NORMALIZE_OUTPUT_FORMAT", "Normalise output formats", ExamplePriority.P1, "prompt_author", 3, "high", $"Output formats are mixed; dominant is {dominantBucket}.", fmtIds);
            if (labelImbalance) Add("ADD_MINORITY_EXAMPLES", "Add minority-class examples", ExamplePriority.P1, "data_curator", 2, "high", $"Set is dominated by '{dominantLabel}'.", Array.Empty<string>());
            if (recencyBiasTailIdx.Count > 0) Add("REORDER_EXAMPLES", "Reorder examples to break recency bias", ExamplePriority.P1, "prompt_author", 1, "high", "Tail of the set shares a single label.", recencyBiasTailIdx.Select(i => ids[i]));
            if (lowDiversity) Add("ADD_DIVERSE_EXAMPLES", "Add more diverse examples", ExamplePriority.P1, "data_curator", 2, "high", "Average pairwise similarity is high.", Array.Empty<string>());
            var lenIds = findings.Where(f => f.Code == "LENGTH_VARIANCE").Select(f => f.ExampleId);
            if (findings.Any(f => f.Code == "LENGTH_VARIANCE")) Add("TIGHTEN_LENGTH_RANGE", "Tighten the example length range", ExamplePriority.P2, "prompt_author", 1, "high", "Extreme length variance can teach inconsistent behaviour.", lenIds);
            var ambIds = findings.Where(f => f.Code == "AMBIGUOUS_DEMONSTRATION").Select(f => f.ExampleId);
            if (findings.Any(f => f.Code == "AMBIGUOUS_DEMONSTRATION")) Add("CLARIFY_DEMONSTRATIONS", "Clarify ambiguous demonstrations", ExamplePriority.P2, "prompt_author", 2, "high", "Output appears unrelated to the input vocabulary.", ambIds);

            if (RiskAppetite == ExampleRiskAppetite.Aggressive)
            {
                playbook = playbook.Where(p => p.Priority != ExamplePriority.P2 && p.Priority != ExamplePriority.P3).ToList();
            }
            if (RiskAppetite == ExampleRiskAppetite.Cautious && (grade == 'C' || grade == 'D' || grade == 'F'))
            {
                Add("SOLICIT_SECOND_REVIEWER", "Bring in a second reviewer", ExamplePriority.P2, "reviewer", 1, "high", "Cautious appetite escalates borderline sets to a second reviewer.", Array.Empty<string>());
            }
            // Dedup by code, then sort P0-first then code asc
            var dedup = playbook
                .GroupBy(p => p.Code)
                .Select(g => g.First())
                .OrderBy(p => (int)p.Priority)
                .ThenBy(p => p.Code, StringComparer.Ordinal)
                .ToList();
            report.Playbook = dedup.ToArray();

            // Insights
            var insights = new List<string>();
            int contraCount = findings.Count(f => f.Code == "CONTRADICTORY_EXAMPLE");
            if (contraCount >= 2) insights.Add($"high_contradiction_count: {contraCount}");
            if (dupCount >= 1) insights.Add($"near_duplicate_cluster: {dupCount} pair(s)");
            if (labelImbalance) insights.Add($"label_dominant: {dominantLabel} ({(int)(dominantLabelShare * 100)}%)");
            if (formatInconsistent) insights.Add($"format_dominant: {dominantBucket} ({(int)(dominantBucketShare * 100)}%)");
            if (leakCount >= 1)
            {
                double leakPct = (double)leakCount / list.Count * 100;
                insights.Add($"leakage_pct: {(int)leakPct}%");
            }
            if (list.Count == 1 || list.Count == 2) insights.Add($"set_too_small: {list.Count} (<3)");
            report.Insights = insights.ToArray();

            // Curated set
            var removedIds = new HashSet<string>();
            var revisedIds = new List<string>();
            // EMPTY_FIELD drop
            for (int i = 0; i < list.Count; i++)
            {
                if (findings.Any(f => f.ExampleId == ids[i] && f.Code == "EMPTY_FIELD")) removedIds.Add(ids[i]);
            }
            // Drop later-indexed duplicates / contradictions
            foreach (var kv in dupKeeper) removedIds.Add(ids[kv.Key]);
            if (contradictions.Count > 0)
            {
                // keep earliest per input-group, drop the rest
                var groups2 = list.Select((e, idx) => new { e, idx })
                    .Where(x => contradictions.Contains(x.idx))
                    .GroupBy(x => NormalizeText(x.e.Input));
                foreach (var g in groups2)
                {
                    var sorted = g.OrderBy(x => x.idx).ToList();
                    foreach (var x in sorted.Skip(1)) removedIds.Add(ids[x.idx]);
                }
            }
            foreach (var a in assessments)
            {
                if (a.Verdict == ExampleVerdict.Revise || a.Verdict == ExampleVerdict.Replace) revisedIds.Add(a.ExampleId);
            }

            var survivors = new List<(int origIdx, QualityExample ex)>();
            for (int i = 0; i < list.Count; i++)
            {
                if (!removedIds.Contains(ids[i])) survivors.Add((i, list[i]));
            }

            bool didReorder = false;
            if (recencyBiasTailIdx.Count > 0 && survivors.Count >= 4)
            {
                var reordered = RoundRobinByLabel(survivors);
                if (!reordered.Select(x => x.origIdx).SequenceEqual(survivors.Select(x => x.origIdx)))
                {
                    survivors = reordered;
                    didReorder = true;
                }
            }

            report.Curated = new CuratedExampleSet
            {
                Examples = survivors.Select(s => s.ex).ToArray(),
                RemovedExampleIds = removedIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                RevisedExampleIds = revisedIds.Distinct().ToArray(),
                Note = $"removed {removedIds.Count}, revised {revisedIds.Distinct().Count()}, reordered={(didReorder ? "true" : "false")}",
            };

            return report;
        }

        private static List<(int origIdx, QualityExample ex)> RoundRobinByLabel(List<(int origIdx, QualityExample ex)> items)
        {
            // bucket by label, then interleave so no 3 consecutive share a label
            var buckets = items
                .GroupBy(x => string.IsNullOrWhiteSpace(x.ex.Label) ? "" : x.ex.Label!)
                .OrderByDescending(g => g.Count())
                .Select(g => new Queue<(int, QualityExample)>(g))
                .ToList();
            var result = new List<(int, QualityExample)>(items.Count);
            string? lastLabel = null; int run = 0;
            while (buckets.Any(b => b.Count > 0))
            {
                // pick bucket with most remaining that doesn't continue a run of 2
                var candidate = buckets
                    .Where(b => b.Count > 0)
                    .Select(b => new { b, label = b.Peek().Item2.Label ?? "" })
                    .OrderByDescending(x => x.b.Count)
                    .ToList();
                var pick = candidate.FirstOrDefault(x => !(run >= 2 && x.label == lastLabel)) ?? candidate.First();
                var item = pick.b.Dequeue();
                result.Add(item);
                var lbl = item.Item2.Label ?? "";
                if (lbl == lastLabel) run++; else { lastLabel = lbl; run = 1; }
            }
            return result;
        }

        private static HashSet<string> Tokenize(string s)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(s)) return set;
            foreach (Match m in TokenRe.Matches(s.ToLowerInvariant())) set.Add(m.Value);
            return set;
        }

        private static int TokenCount(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            foreach (Match _ in TokenRe.Matches(s)) n++;
            return n;
        }

        private static double Jaccard(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 && b.Count == 0) return 1;
            int inter = a.Intersect(b).Count();
            int union = a.Count + b.Count - inter;
            return union == 0 ? 0 : (double)inter / union;
        }

        private static string ClassifyFormat(string s)
        {
            var t = (s ?? "").Trim();
            if (string.IsNullOrEmpty(t)) return "empty";
            if (t.StartsWith("```")) return "code_block";
            if (t.StartsWith("{") && t.EndsWith("}")) return "json_object";
            if (t.StartsWith("[") && t.EndsWith("]")) return "json_array";
            if (t.StartsWith("- ") || t.StartsWith("* ")) return "bullet_list";
            if (Regex.IsMatch(t, @"^\d+\."))  return "numbered_list";
            if (!t.Contains(' ') && !t.Contains('\n')) return "single_word";
            return "sentence";
        }

        private static string NormalizeText(string? s) => (s ?? "").Trim().ToLowerInvariant();
    }
}
