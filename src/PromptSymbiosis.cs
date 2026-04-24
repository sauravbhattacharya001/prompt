namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ────────────────────────────────────────────
    //  PromptSymbiosis – Autonomous Prompt Synergy Detector
    //
    //  Analyzes collections of prompts to discover synergistic pairs
    //  that work better together than alone. Detects complementary
    //  coverage, co-dependency patterns, capability gaps, and
    //  recommends symbiotic chains. Includes health scoring, gap
    //  analysis, and multi-format reporting (Text, Markdown, JSON, HTML).
    // ────────────────────────────────────────────

    #region Enums

    /// <summary>Type of symbiotic relationship between prompts.</summary>
    public enum SymbiosisType
    {
        /// <summary>Both prompts benefit from being used together.</summary>
        Mutualism,
        /// <summary>One prompt enhances the other without detriment.</summary>
        Commensalism,
        /// <summary>Prompts compete for the same task space.</summary>
        Competition,
        /// <summary>One prompt depends on the other's output.</summary>
        Parasitism,
        /// <summary>Prompts are independent with no interaction.</summary>
        Neutral
    }

    /// <summary>Dimension of prompt capability for coverage analysis.</summary>
    public enum CapabilityDimension
    {
        /// <summary>Reasoning and logic tasks.</summary>
        Reasoning,
        /// <summary>Creative and generative tasks.</summary>
        Creativity,
        /// <summary>Data extraction and parsing.</summary>
        Extraction,
        /// <summary>Summarization and compression.</summary>
        Summarization,
        /// <summary>Classification and categorization.</summary>
        Classification,
        /// <summary>Code generation and analysis.</summary>
        CodeGeneration,
        /// <summary>Conversation and dialogue.</summary>
        Conversation,
        /// <summary>Translation and localization.</summary>
        Translation,
        /// <summary>Validation and checking.</summary>
        Validation,
        /// <summary>Planning and orchestration.</summary>
        Planning
    }

    /// <summary>Report output format for symbiosis analysis.</summary>
    public enum SymbiosisReportFormat
    {
        /// <summary>ASCII text.</summary>
        Text,
        /// <summary>Markdown with tables.</summary>
        Markdown,
        /// <summary>Structured JSON.</summary>
        Json,
        /// <summary>Interactive HTML dashboard.</summary>
        Html
    }

    #endregion

    #region Data Models

    /// <summary>A prompt's capability profile across dimensions.</summary>
    public sealed class PromptCapabilityProfile
    {
        /// <summary>Prompt name/identifier.</summary>
        public string Name { get; set; } = "";

        /// <summary>The prompt text.</summary>
        public string PromptText { get; set; } = "";

        /// <summary>Capability scores (0.0–1.0) per dimension.</summary>
        public Dictionary<CapabilityDimension, double> Capabilities { get; set; } = new();

        /// <summary>Tags/categories for this prompt.</summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>Expected input format description.</summary>
        public string InputFormat { get; set; } = "";

        /// <summary>Expected output format description.</summary>
        public string OutputFormat { get; set; } = "";

        /// <summary>Overall capability coverage (fraction of dimensions with score > 0.3).</summary>
        public double CoverageScore => Capabilities.Count > 0
            ? (double)Capabilities.Count(kv => kv.Value > 0.3) / PromptSymbiosis.DimensionCount
            : 0;
    }

    /// <summary>A detected symbiotic relationship between two prompts.</summary>
    public sealed class SymbioticRelationship
    {
        /// <summary>First prompt in the pair.</summary>
        public string PromptA { get; set; } = "";
        /// <summary>Second prompt in the pair.</summary>
        public string PromptB { get; set; } = "";
        /// <summary>Type of symbiosis.</summary>
        public SymbiosisType Type { get; set; }
        /// <summary>Synergy score (0–1, higher = stronger relationship).</summary>
        public double SynergyScore { get; set; }
        /// <summary>Complementarity score — how well they fill each other's gaps.</summary>
        public double ComplementarityScore { get; set; }
        /// <summary>Overlap score — how much capability they share (competition indicator).</summary>
        public double OverlapScore { get; set; }
        /// <summary>Whether A's output can feed B's input.</summary>
        public bool HasDataFlow { get; set; }
        /// <summary>Human-readable explanation.</summary>
        public string Explanation { get; set; } = "";
        /// <summary>Recommended usage pattern.</summary>
        public string Recommendation { get; set; } = "";
    }

    /// <summary>A recommended chain of prompts for optimal coverage.</summary>
    public sealed class SymbioticChain
    {
        /// <summary>Ordered list of prompt names in the chain.</summary>
        public List<string> Steps { get; set; } = new();
        /// <summary>Combined capability coverage (0–1).</summary>
        public double CombinedCoverage { get; set; }
        /// <summary>Average synergy between consecutive steps.</summary>
        public double AverageSynergy { get; set; }
        /// <summary>What this chain is optimized for.</summary>
        public string Purpose { get; set; } = "";
        /// <summary>Capability dimensions this chain covers.</summary>
        public List<CapabilityDimension> CoveredDimensions { get; set; } = new();
        /// <summary>Capability gaps remaining after this chain.</summary>
        public List<CapabilityDimension> GapDimensions { get; set; } = new();
    }

    /// <summary>Gap analysis identifying uncovered capability areas.</summary>
    public sealed class CapabilityGap
    {
        /// <summary>The uncovered dimension.</summary>
        public CapabilityDimension Dimension { get; set; }
        /// <summary>Best available score across all prompts.</summary>
        public double BestAvailableScore { get; set; }
        /// <summary>Severity (0–1, higher = more critical gap).</summary>
        public double Severity { get; set; }
        /// <summary>Suggested prompt type to fill this gap.</summary>
        public string SuggestedFill { get; set; } = "";
    }

    /// <summary>Full symbiosis analysis report.</summary>
    public sealed class SymbiosisReport
    {
        /// <summary>Analysis name.</summary>
        public string Name { get; set; } = "";
        /// <summary>Number of prompts analyzed.</summary>
        public int PromptCount { get; set; }
        /// <summary>All detected relationships.</summary>
        public List<SymbioticRelationship> Relationships { get; set; } = new();
        /// <summary>Recommended symbiotic chains.</summary>
        public List<SymbioticChain> RecommendedChains { get; set; } = new();
        /// <summary>Capability gaps in the prompt portfolio.</summary>
        public List<CapabilityGap> Gaps { get; set; } = new();
        /// <summary>Ecosystem health score (0–100).</summary>
        public int HealthScore { get; set; }
        /// <summary>Health grade.</summary>
        public string HealthGrade { get; set; } = "";
        /// <summary>Proactive recommendations.</summary>
        public List<string> Recommendations { get; set; } = new();
        /// <summary>Relationship type distribution.</summary>
        public Dictionary<SymbiosisType, int> TypeDistribution { get; set; } = new();
    }

    #endregion

    #region PromptSymbiosis (main class)

    /// <summary>
    /// Autonomous prompt synergy detector. Analyzes prompt collections to find
    /// complementary pairs, detect competition, recommend chains, and identify
    /// capability gaps. Proactive recommendations for portfolio optimization.
    /// </summary>
    public sealed class PromptSymbiosis
    {
        private static readonly CapabilityDimension[] AllDimensions = Enum.GetValues<CapabilityDimension>();
        internal static readonly int DimensionCount = AllDimensions.Length;

        private readonly List<PromptCapabilityProfile> _profiles = new();

        /// <summary>All registered prompt profiles.</summary>
        public IReadOnlyList<PromptCapabilityProfile> Profiles => _profiles;

        /// <summary>Add a prompt profile for analysis.</summary>
        public PromptSymbiosis AddProfile(PromptCapabilityProfile profile)
        {
            _profiles.Add(profile);
            return this;
        }

        /// <summary>Add a prompt with auto-detected capabilities from text analysis.</summary>
        public PromptSymbiosis AddPrompt(string name, string promptText, params string[] tags)
        {
            var profile = new PromptCapabilityProfile
            {
                Name = name,
                PromptText = promptText,
                Tags = tags.ToList(),
                Capabilities = AutoDetectCapabilities(promptText)
            };
            _profiles.Add(profile);
            return this;
        }

        /// <summary>Run full symbiosis analysis.</summary>
        public SymbiosisReport Analyze(string name = "Symbiosis Analysis")
        {
            var relationships = DetectRelationships();
            var gaps = DetectGaps();
            var chains = RecommendChains(relationships);
            var typeDist = relationships.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count());
            var health = ComputeHealth(relationships, gaps);
            var recs = GenerateRecommendations(relationships, gaps, chains);

            return new SymbiosisReport
            {
                Name = name,
                PromptCount = _profiles.Count,
                Relationships = relationships,
                RecommendedChains = chains,
                Gaps = gaps,
                HealthScore = health,
                HealthGrade = health >= 90 ? "A" : health >= 80 ? "B" : health >= 70 ? "C" : health >= 60 ? "D" : "F",
                Recommendations = recs,
                TypeDistribution = typeDist
            };
        }

        /// <summary>Find the best symbiotic partner for a given prompt.</summary>
        public SymbioticRelationship? FindBestPartner(string promptName)
        {
            var relationships = DetectRelationships();
            return relationships
                .Where(r => (r.PromptA == promptName || r.PromptB == promptName)
                          && r.Type == SymbiosisType.Mutualism)
                .OrderByDescending(r => r.SynergyScore)
                .FirstOrDefault();
        }

        /// <summary>Generate a formatted report.</summary>
        public string GenerateReport(SymbiosisReportFormat format = SymbiosisReportFormat.Text, string name = "Symbiosis Analysis")
        {
            var report = Analyze(name);
            return format switch
            {
                SymbiosisReportFormat.Text => RenderText(report),
                SymbiosisReportFormat.Markdown => RenderMarkdown(report),
                SymbiosisReportFormat.Json => RenderJson(report),
                SymbiosisReportFormat.Html => RenderHtml(report),
                _ => RenderText(report)
            };
        }

        #region Auto-Detection

        private static Dictionary<CapabilityDimension, double> AutoDetectCapabilities(string text)
        {
            var lower = text.ToLowerInvariant();
            var caps = new Dictionary<CapabilityDimension, double>();

            var signals = new Dictionary<CapabilityDimension, string[]>
            {
                [CapabilityDimension.Reasoning] = new[] { "reason", "logic", "think", "analyze", "deduc", "infer", "step by step", "chain of thought", "why", "because", "therefore" },
                [CapabilityDimension.Creativity] = new[] { "creat", "imagin", "story", "poem", "write", "generat", "brainstorm", "invent", "novel", "original" },
                [CapabilityDimension.Extraction] = new[] { "extract", "parse", "find", "identify", "detect", "locate", "pull out", "pick out", "recognize", "ner" },
                [CapabilityDimension.Summarization] = new[] { "summar", "condensed", "brief", "tldr", "key points", "overview", "digest", "abstract", "shorten" },
                [CapabilityDimension.Classification] = new[] { "classif", "categoriz", "label", "tag", "sort", "group", "bucket", "type", "assign", "sentiment" },
                [CapabilityDimension.CodeGeneration] = new[] { "code", "program", "function", "implement", "debug", "refactor", "algorithm", "script", "syntax", "compile" },
                [CapabilityDimension.Conversation] = new[] { "chat", "convers", "dialog", "respond", "reply", "discuss", "talk", "assist", "help", "user" },
                [CapabilityDimension.Translation] = new[] { "translat", "localiz", "language", "multilingual", "i18n", "convert to", "render in" },
                [CapabilityDimension.Validation] = new[] { "valid", "check", "verify", "ensure", "confirm", "test", "assert", "correct", "lint", "review" },
                [CapabilityDimension.Planning] = new[] { "plan", "schedul", "organiz", "priorit", "roadmap", "strategy", "workflow", "orchestrat", "coordinate", "pipeline" }
            };

            foreach (var (dim, keywords) in signals)
            {
                var hits = keywords.Count(k => lower.Contains(k));
                var score = Math.Min(1.0, hits / 3.0);
                caps[dim] = Math.Round(score, 2);
            }

            return caps;
        }

        #endregion

        #region Relationship Detection

        private List<SymbioticRelationship> DetectRelationships()
        {
            var results = new List<SymbioticRelationship>();
            for (int i = 0; i < _profiles.Count; i++)
            {
                for (int j = i + 1; j < _profiles.Count; j++)
                {
                    results.Add(AnalyzePair(_profiles[i], _profiles[j]));
                }
            }
            return results.OrderByDescending(r => r.SynergyScore).ToList();
        }

        private static SymbioticRelationship AnalyzePair(PromptCapabilityProfile a, PromptCapabilityProfile b)
        {
            double complementarity = 0, overlap = 0, count = 0;

            foreach (var dim in AllDimensions)
            {
                var sa = a.Capabilities.GetValueOrDefault(dim);
                var sb = b.Capabilities.GetValueOrDefault(dim);
                // Complementarity: one is strong where other is weak
                complementarity += Math.Abs(sa - sb) * Math.Max(sa, sb);
                // Overlap: both are strong
                overlap += Math.Min(sa, sb);
                count++;
            }

            complementarity = count > 0 ? Math.Round(complementarity / count, 3) : 0;
            overlap = count > 0 ? Math.Round(overlap / count, 3) : 0;

            // Data flow: check if output format hints match input format hints
            var hasDataFlow = !string.IsNullOrEmpty(a.OutputFormat) && !string.IsNullOrEmpty(b.InputFormat)
                && (a.OutputFormat.Contains(b.InputFormat, StringComparison.OrdinalIgnoreCase)
                    || b.OutputFormat.Contains(a.InputFormat, StringComparison.OrdinalIgnoreCase));

            // Tag overlap
            var sharedTags = a.Tags.Intersect(b.Tags, StringComparer.OrdinalIgnoreCase).Count();
            var tagBonus = sharedTags > 0 ? 0.1 * Math.Min(sharedTags, 3) : 0;

            var synergy = Math.Round(Math.Min(1.0, complementarity * 0.6 + (hasDataFlow ? 0.2 : 0) + tagBonus + overlap * 0.1), 3);

            // Classify relationship type
            SymbiosisType type;
            string explanation, recommendation;

            if (overlap > 0.5 && complementarity < 0.15)
            {
                type = SymbiosisType.Competition;
                explanation = $"High overlap ({overlap:F2}) with low complementarity — these prompts compete for the same tasks";
                recommendation = "Consider consolidating into a single prompt or using A/B testing to pick the best";
            }
            else if (complementarity > 0.3 && hasDataFlow)
            {
                type = SymbiosisType.Mutualism;
                explanation = $"Strong complementarity ({complementarity:F2}) with data flow — excellent symbiotic pair";
                recommendation = "Chain these prompts together: one's output feeds the other's strength";
            }
            else if (complementarity > 0.2)
            {
                type = SymbiosisType.Commensalism;
                explanation = $"Good complementarity ({complementarity:F2}) — they cover different capability areas";
                recommendation = "Use together for broader task coverage";
            }
            else if (complementarity < 0.1 && overlap < 0.2)
            {
                type = SymbiosisType.Neutral;
                explanation = "Minimal interaction — these prompts operate in different domains";
                recommendation = "No action needed — these are independent tools";
            }
            else
            {
                type = SymbiosisType.Parasitism;
                explanation = $"Asymmetric relationship — one prompt may depend on the other without reciprocal benefit";
                recommendation = "Review whether the dependent prompt can be strengthened independently";
            }

            return new SymbioticRelationship
            {
                PromptA = a.Name,
                PromptB = b.Name,
                Type = type,
                SynergyScore = synergy,
                ComplementarityScore = complementarity,
                OverlapScore = overlap,
                HasDataFlow = hasDataFlow,
                Explanation = explanation,
                Recommendation = recommendation
            };
        }

        #endregion

        #region Gap Detection

        private List<CapabilityGap> DetectGaps()
        {
            var gaps = new List<CapabilityGap>();
            var fills = new Dictionary<CapabilityDimension, string>
            {
                [CapabilityDimension.Reasoning] = "Add a chain-of-thought reasoning prompt",
                [CapabilityDimension.Creativity] = "Add a creative writing/brainstorming prompt",
                [CapabilityDimension.Extraction] = "Add a data extraction/NER prompt",
                [CapabilityDimension.Summarization] = "Add a summarization/distillation prompt",
                [CapabilityDimension.Classification] = "Add a classification/labeling prompt",
                [CapabilityDimension.CodeGeneration] = "Add a code generation/review prompt",
                [CapabilityDimension.Conversation] = "Add a conversational/assistant prompt",
                [CapabilityDimension.Translation] = "Add a translation/localization prompt",
                [CapabilityDimension.Validation] = "Add a validation/quality-check prompt",
                [CapabilityDimension.Planning] = "Add a planning/orchestration prompt"
            };

            foreach (var dim in AllDimensions)
            {
                var bestScore = _profiles.Count > 0
                    ? _profiles.Max(p => p.Capabilities.GetValueOrDefault(dim))
                    : 0;

                if (bestScore < 0.3)
                {
                    gaps.Add(new CapabilityGap
                    {
                        Dimension = dim,
                        BestAvailableScore = bestScore,
                        Severity = Math.Round(1.0 - bestScore, 2),
                        SuggestedFill = fills.GetValueOrDefault(dim, "Add a specialized prompt for this area")
                    });
                }
            }

            return gaps.OrderByDescending(g => g.Severity).ToList();
        }

        #endregion

        #region Chain Recommendation

        private List<SymbioticChain> RecommendChains(List<SymbioticRelationship> relationships)
        {
            var chains = new List<SymbioticChain>();
            if (_profiles.Count < 2) return chains;

            // Strategy 1: Maximum coverage chain (greedy)
            var coverageChain = BuildCoverageChain();
            if (coverageChain.Steps.Count >= 2) chains.Add(coverageChain);

            // Strategy 2: Highest synergy pairs as 2-step chains
            var topPairs = relationships
                .Where(r => r.Type == SymbiosisType.Mutualism || r.Type == SymbiosisType.Commensalism)
                .Take(3);

            foreach (var pair in topPairs)
            {
                var steps = new List<string> { pair.PromptA, pair.PromptB };
                var combined = ComputeCombinedCoverage(steps);
                var covered = GetCoveredDimensions(steps);
                var gapped = AllDimensions.Except(covered).ToList();
                chains.Add(new SymbioticChain
                {
                    Steps = steps,
                    CombinedCoverage = combined,
                    AverageSynergy = pair.SynergyScore,
                    Purpose = $"Synergistic pair ({pair.Type})",
                    CoveredDimensions = covered,
                    GapDimensions = gapped
                });
            }

            return chains.OrderByDescending(c => c.CombinedCoverage).ToList();
        }

        private SymbioticChain BuildCoverageChain()
        {
            var selected = new List<string>();
            var coveredScores = new Dictionary<CapabilityDimension, double>(DimensionCount);
            foreach (var d in AllDimensions) coveredScores[d] = 0;

            var remaining = _profiles.ToList();

            // Greedy: pick prompt that adds most new coverage
            while (remaining.Count > 0 && selected.Count < 5)
            {
                PromptCapabilityProfile? best = null;
                double bestGain = -1;

                foreach (var p in remaining)
                {
                    double gain = 0;
                    foreach (var dim in AllDimensions)
                    {
                        var current = coveredScores[dim];
                        var offered = p.Capabilities.GetValueOrDefault(dim);
                        if (offered > current)
                            gain += offered - current;
                    }
                    if (gain > bestGain) { bestGain = gain; best = p; }
                }

                if (best == null || bestGain < 0.05) break;

                selected.Add(best.Name);
                foreach (var dim in AllDimensions)
                {
                    var offered = best.Capabilities.GetValueOrDefault(dim);
                    if (offered > coveredScores[dim])
                        coveredScores[dim] = offered;
                }
                remaining.Remove(best);
            }

            var covered = GetCoveredDimensions(selected);
            var gapped = AllDimensions.Except(covered).ToList();

            return new SymbioticChain
            {
                Steps = selected,
                CombinedCoverage = ComputeCombinedCoverage(selected),
                AverageSynergy = 0,
                Purpose = "Maximum capability coverage chain",
                CoveredDimensions = covered,
                GapDimensions = gapped
            };
        }

        private double ComputeCombinedCoverage(List<string> steps)
        {
            var profileIndex = BuildProfileIndex();
            var profiles = steps.Select(s => profileIndex.GetValueOrDefault(s)).Where(p => p != null).ToList();
            if (profiles.Count == 0) return 0;
            int covered = 0;
            foreach (var dim in AllDimensions)
            {
                var best = profiles.Max(p => p!.Capabilities.GetValueOrDefault(dim));
                if (best > 0.3) covered++;
            }
            return Math.Round((double)covered / DimensionCount, 3);
        }

        private Dictionary<string, PromptCapabilityProfile> BuildProfileIndex()
        {
            var index = new Dictionary<string, PromptCapabilityProfile>(_profiles.Count);
            foreach (var p in _profiles)
                index[p.Name] = p;
            return index;
        }

        private List<CapabilityDimension> GetCoveredDimensions(List<string> steps)
        {
            var profileIndex = BuildProfileIndex();
            var profiles = steps.Select(s => profileIndex.GetValueOrDefault(s)).Where(p => p != null).ToList();
            var result = new List<CapabilityDimension>();
            foreach (var dim in AllDimensions)
            {
                var best = profiles.Count > 0 ? profiles.Max(p => p!.Capabilities.GetValueOrDefault(dim)) : 0;
                if (best > 0.3) result.Add(dim);
            }
            return result;
        }

        #endregion

        #region Health & Recommendations

        private int ComputeHealth(List<SymbioticRelationship> relationships, List<CapabilityGap> gaps)
        {
            double score = 100;

            // Gap penalties
            score -= gaps.Count * 8;

            // Competition penalty
            var competitions = relationships.Count(r => r.Type == SymbiosisType.Competition);
            score -= competitions * 5;

            // Bonus for mutualism
            var mutualisms = relationships.Count(r => r.Type == SymbiosisType.Mutualism);
            score += Math.Min(15, mutualisms * 3);

            // Low portfolio penalty
            if (_profiles.Count < 3) score -= 10;

            return Math.Max(0, Math.Min(100, (int)Math.Round(score)));
        }

        private List<string> GenerateRecommendations(
            List<SymbioticRelationship> relationships, List<CapabilityGap> gaps, List<SymbioticChain> chains)
        {
            var recs = new List<string>();

            // Gap-based recommendations
            foreach (var gap in gaps.Take(3))
                recs.Add($"🔍 Gap in {gap.Dimension} (best score: {gap.BestAvailableScore:F1}) — {gap.SuggestedFill}");

            // Competition warnings
            var competitions = relationships.Where(r => r.Type == SymbiosisType.Competition).ToList();
            foreach (var comp in competitions.Take(2))
                recs.Add($"⚔️ '{comp.PromptA}' and '{comp.PromptB}' compete — consider consolidating or differentiating them");

            // Mutualism opportunities
            var topMutual = relationships.Where(r => r.Type == SymbiosisType.Mutualism).Take(2).ToList();
            foreach (var m in topMutual)
                recs.Add($"🤝 Strong synergy: '{m.PromptA}' + '{m.PromptB}' (score: {m.SynergyScore:F2}) — chain them for better results");

            // Coverage chain
            var bestChain = chains.FirstOrDefault();
            if (bestChain != null && bestChain.Steps.Count >= 2)
                recs.Add($"🔗 Best coverage chain: {string.Join(" → ", bestChain.Steps)} ({bestChain.CombinedCoverage:P0} coverage)");

            if (recs.Count == 0)
                recs.Add("✅ Prompt ecosystem looks balanced — no immediate action needed");

            return recs;
        }

        #endregion

        #region Renderers

        private static string RenderText(SymbiosisReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════════╗");
            sb.AppendLine($"║  PROMPT SYMBIOSIS REPORT: {r.Name,-27}║");
            sb.AppendLine("╚══════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"  Health: {r.HealthGrade} ({r.HealthScore}/100)  |  Prompts: {r.PromptCount}  |  Relationships: {r.Relationships.Count}");
            sb.AppendLine();

            // Type distribution
            sb.AppendLine("  RELATIONSHIP TYPES:");
            foreach (var (type, count) in r.TypeDistribution.OrderByDescending(kv => kv.Value))
            {
                var icon = type switch
                {
                    SymbiosisType.Mutualism => "🤝",
                    SymbiosisType.Commensalism => "🌿",
                    SymbiosisType.Competition => "⚔️",
                    SymbiosisType.Parasitism => "🔗",
                    _ => "➖"
                };
                sb.AppendLine($"  {icon} {type}: {count}");
            }
            sb.AppendLine();

            // Top relationships
            if (r.Relationships.Count > 0)
            {
                sb.AppendLine("  TOP RELATIONSHIPS:");
                foreach (var rel in r.Relationships.Take(5))
                    sb.AppendLine($"  • [{rel.Type}] {rel.PromptA} ↔ {rel.PromptB} (synergy: {rel.SynergyScore:F2}) — {rel.Explanation}");
                sb.AppendLine();
            }

            // Chains
            if (r.RecommendedChains.Count > 0)
            {
                sb.AppendLine("  RECOMMENDED CHAINS:");
                foreach (var chain in r.RecommendedChains)
                    sb.AppendLine($"  🔗 {string.Join(" → ", chain.Steps)} ({chain.CombinedCoverage:P0} coverage) — {chain.Purpose}");
                sb.AppendLine();
            }

            // Gaps
            if (r.Gaps.Count > 0)
            {
                sb.AppendLine("  CAPABILITY GAPS:");
                foreach (var gap in r.Gaps)
                    sb.AppendLine($"  ⚠️ {gap.Dimension}: best={gap.BestAvailableScore:F1}, severity={gap.Severity:F1} — {gap.SuggestedFill}");
                sb.AppendLine();
            }

            // Recommendations
            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine("  RECOMMENDATIONS:");
                foreach (var rec in r.Recommendations)
                    sb.AppendLine($"  • {rec}");
            }

            return sb.ToString();
        }

        private static string RenderMarkdown(SymbiosisReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# 🧬 Prompt Symbiosis Report: {r.Name}");
            sb.AppendLine();
            sb.AppendLine($"**Health:** {r.HealthGrade} ({r.HealthScore}/100) | **Prompts:** {r.PromptCount} | **Relationships:** {r.Relationships.Count}");
            sb.AppendLine();

            sb.AppendLine("## Relationship Distribution");
            sb.AppendLine("| Type | Count |");
            sb.AppendLine("|------|-------|");
            foreach (var (type, count) in r.TypeDistribution.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"| {type} | {count} |");
            sb.AppendLine();

            if (r.Relationships.Count > 0)
            {
                sb.AppendLine("## Top Relationships");
                sb.AppendLine("| Prompt A | Prompt B | Type | Synergy | Complementarity |");
                sb.AppendLine("|----------|----------|------|---------|-----------------|");
                foreach (var rel in r.Relationships.Take(10))
                    sb.AppendLine($"| {rel.PromptA} | {rel.PromptB} | {rel.Type} | {rel.SynergyScore:F2} | {rel.ComplementarityScore:F2} |");
                sb.AppendLine();
            }

            if (r.RecommendedChains.Count > 0)
            {
                sb.AppendLine("## Recommended Chains");
                foreach (var chain in r.RecommendedChains)
                {
                    sb.AppendLine($"- **{chain.Purpose}**: {string.Join(" → ", chain.Steps)} ({chain.CombinedCoverage:P0} coverage)");
                    if (chain.GapDimensions.Count > 0)
                        sb.AppendLine($"  - Gaps: {string.Join(", ", chain.GapDimensions)}");
                }
                sb.AppendLine();
            }

            if (r.Gaps.Count > 0)
            {
                sb.AppendLine("## Capability Gaps");
                foreach (var gap in r.Gaps)
                    sb.AppendLine($"- ⚠️ **{gap.Dimension}** (severity: {gap.Severity:F1}) — {gap.SuggestedFill}");
                sb.AppendLine();
            }

            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine("## Recommendations");
                foreach (var rec in r.Recommendations)
                    sb.AppendLine($"- {rec}");
            }

            return sb.ToString();
        }

        private static string RenderJson(SymbiosisReport r)
        {
            return JsonSerializer.Serialize(r, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        private static string RenderHtml(SymbiosisReport r)
        {
            var hc = r.HealthScore >= 80 ? "#22c55e" : r.HealthScore >= 60 ? "#eab308" : "#ef4444";
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.AppendLine($"<title>Prompt Symbiosis — {E(r.Name)}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:system-ui,-apple-system,sans-serif;background:#0f172a;color:#e2e8f0;padding:2rem}");
            sb.AppendLine(".card{background:#1e293b;border-radius:12px;padding:1.5rem;margin-bottom:1.5rem}");
            sb.AppendLine("h1{font-size:1.8rem;margin-bottom:1rem}h2{font-size:1.2rem;margin-bottom:0.8rem;color:#94a3b8}");
            sb.AppendLine(".grade{display:inline-block;font-size:3rem;font-weight:bold;border-radius:50%;width:80px;height:80px;line-height:80px;text-align:center}");
            sb.AppendLine(".stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:1rem;margin:1rem 0}");
            sb.AppendLine(".stat{text-align:center}.stat .val{font-size:1.5rem;font-weight:bold}.stat .lbl{font-size:0.8rem;color:#94a3b8}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin-top:0.5rem}");
            sb.AppendLine("th,td{padding:8px 12px;text-align:left;border-bottom:1px solid #334155}");
            sb.AppendLine("th{color:#94a3b8;font-size:0.85rem;text-transform:uppercase}");
            sb.AppendLine(".tag{display:inline-block;padding:2px 8px;border-radius:4px;font-size:0.75rem;margin:2px}");
            sb.AppendLine(".rec{background:#1a2332;border-left:3px solid #3b82f6;padding:0.6rem 1rem;margin:0.4rem 0;border-radius:4px}");
            sb.AppendLine(".chain{background:#172033;border:1px solid #334155;border-radius:8px;padding:1rem;margin:0.5rem 0}");
            sb.AppendLine(".arrow{color:#3b82f6;font-weight:bold;margin:0 0.3rem}");
            sb.AppendLine(".mutualism{color:#22c55e}.competition{color:#ef4444}.commensalism{color:#3b82f6}.parasitism{color:#eab308}.neutral{color:#64748b}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine($"<h1>🧬 Prompt Symbiosis — {E(r.Name)}</h1>");

            // Health
            sb.AppendLine("<div class=\"card\" style=\"display:flex;align-items:center;gap:1.5rem\">");
            sb.AppendLine($"<div class=\"grade\" style=\"background:{hc};color:#fff\">{E(r.HealthGrade)}</div>");
            sb.AppendLine($"<div><div style=\"font-size:1.4rem;font-weight:bold\">Ecosystem Health: {r.HealthScore}/100</div>");
            sb.AppendLine($"<div style=\"color:#94a3b8\">{r.PromptCount} prompts, {r.Relationships.Count} relationships, {r.Gaps.Count} gaps</div></div></div>");

            // Distribution
            sb.AppendLine("<div class=\"card\"><h2>Relationship Distribution</h2><div class=\"stats\">");
            foreach (var (type, count) in r.TypeDistribution.OrderByDescending(kv => kv.Value))
            {
                var cls = type.ToString().ToLowerInvariant();
                sb.AppendLine($"<div class=\"stat\"><div class=\"val {cls}\">{count}</div><div class=\"lbl\">{type}</div></div>");
            }
            sb.AppendLine("</div></div>");

            // Relationships table
            if (r.Relationships.Count > 0)
            {
                sb.AppendLine("<div class=\"card\"><h2>Relationships</h2><table>");
                sb.AppendLine("<tr><th>Prompt A</th><th>Prompt B</th><th>Type</th><th>Synergy</th><th>Complement.</th><th>Overlap</th></tr>");
                foreach (var rel in r.Relationships.Take(15))
                {
                    var cls = rel.Type.ToString().ToLowerInvariant();
                    sb.AppendLine($"<tr><td>{E(rel.PromptA)}</td><td>{E(rel.PromptB)}</td><td class=\"{cls}\">{rel.Type}</td><td>{rel.SynergyScore:F2}</td><td>{rel.ComplementarityScore:F2}</td><td>{rel.OverlapScore:F2}</td></tr>");
                }
                sb.AppendLine("</table></div>");
            }

            // Chains
            if (r.RecommendedChains.Count > 0)
            {
                sb.AppendLine("<div class=\"card\"><h2>Recommended Chains</h2>");
                foreach (var chain in r.RecommendedChains)
                {
                    sb.Append("<div class=\"chain\"><div style=\"font-size:1.1rem;margin-bottom:0.5rem\">");
                    sb.Append(string.Join("<span class=\"arrow\">→</span>", chain.Steps.Select(s => $"<strong>{E(s)}</strong>")));
                    sb.AppendLine($"</div><div style=\"color:#94a3b8\">{E(chain.Purpose)} — {chain.CombinedCoverage:P0} coverage</div>");
                    if (chain.GapDimensions.Count > 0)
                        sb.AppendLine($"<div style=\"color:#eab308;font-size:0.85rem;margin-top:0.3rem\">Gaps: {string.Join(", ", chain.GapDimensions)}</div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            // Gaps
            if (r.Gaps.Count > 0)
            {
                sb.AppendLine("<div class=\"card\"><h2>Capability Gaps</h2>");
                foreach (var gap in r.Gaps)
                    sb.AppendLine($"<div class=\"rec\"><strong style=\"color:#eab308\">{gap.Dimension}</strong> (severity: {gap.Severity:F1}) — {E(gap.SuggestedFill)}</div>");
                sb.AppendLine("</div>");
            }

            // Recommendations
            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine("<div class=\"card\"><h2>Recommendations</h2>");
                foreach (var rec in r.Recommendations)
                    sb.AppendLine($"<div class=\"rec\">{E(rec)}</div>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine($"<div style=\"text-align:center;color:#64748b;margin-top:2rem;font-size:0.8rem\">Generated by PromptSymbiosis — {DateTime.UtcNow:u}</div>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string E(string s) => System.Net.WebUtility.HtmlEncode(s);

        #endregion
    }

    #endregion
}
