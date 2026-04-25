namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ────────────────────────────────────────────
    //  PromptEcosystem – Autonomous Prompt Ecosystem Health Analyzer
    //
    //  Examines an entire portfolio of prompts as a holistic system.
    //  Detects diversity gaps, staleness, redundancy clusters, coverage
    //  holes, complexity imbalance, consistency drift, and resilience
    //  weaknesses. Generates interactive HTML dashboards with SVG gauges,
    //  radar charts, and actionable recommendations.
    // ────────────────────────────────────────────

    #region Enums

    /// <summary>Health dimension for ecosystem analysis.</summary>
    public enum EcosystemHealthDimension
    {
        /// <summary>Category and topic diversity.</summary>
        Diversity,
        /// <summary>Capability area coverage.</summary>
        Coverage,
        /// <summary>Recency and usage freshness.</summary>
        Freshness,
        /// <summary>Redundancy and duplication level.</summary>
        Redundancy,
        /// <summary>Complexity distribution balance.</summary>
        Complexity,
        /// <summary>Formatting and style consistency.</summary>
        Consistency,
        /// <summary>Error handling and fallback resilience.</summary>
        Resilience
    }

    /// <summary>Severity level for ecosystem issues.</summary>
    public enum EcosystemIssueSeverity
    {
        /// <summary>Minor concern.</summary>
        Low,
        /// <summary>Notable issue worth addressing.</summary>
        Medium,
        /// <summary>Significant problem.</summary>
        High,
        /// <summary>Urgent issue requiring immediate attention.</summary>
        Critical
    }

    #endregion

    #region Records

    /// <summary>A prompt with ecosystem metadata.</summary>
    public record EcosystemPrompt
    {
        /// <summary>Prompt identifier.</summary>
        public string Name { get; init; } = "";
        /// <summary>Prompt text content.</summary>
        public string Text { get; init; } = "";
        /// <summary>Category or domain.</summary>
        public string Category { get; init; } = "Uncategorized";
        /// <summary>When this prompt was created.</summary>
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        /// <summary>Last usage timestamp.</summary>
        public DateTime? LastUsedUtc { get; init; }
        /// <summary>Total usage count.</summary>
        public int UsageCount { get; init; }
        /// <summary>Tags for classification.</summary>
        public List<string> Tags { get; init; } = new();
    }

    /// <summary>An issue detected in the ecosystem.</summary>
    public record EcosystemIssue
    {
        /// <summary>Which health dimension this affects.</summary>
        public EcosystemHealthDimension Dimension { get; init; }
        /// <summary>Issue severity.</summary>
        public EcosystemIssueSeverity Severity { get; init; }
        /// <summary>Human-readable description.</summary>
        public string Description { get; init; } = "";
        /// <summary>Names of affected prompts.</summary>
        public List<string> AffectedPrompts { get; init; } = new();
        /// <summary>Recommended fix.</summary>
        public string Recommendation { get; init; } = "";
    }

    /// <summary>A cluster of similar prompts.</summary>
    public record EcosystemCluster
    {
        /// <summary>Cluster label.</summary>
        public string Name { get; init; } = "";
        /// <summary>Member prompt names.</summary>
        public List<string> Members { get; init; } = new();
        /// <summary>Top keywords for this cluster.</summary>
        public List<string> CentroidKeywords { get; init; } = new();
        /// <summary>How redundant members are (0=unique, 1=identical).</summary>
        public double RedundancyScore { get; init; }
    }

    /// <summary>Full ecosystem analysis report.</summary>
    public record EcosystemReport
    {
        /// <summary>Overall health score (0-100).</summary>
        public double OverallHealthScore { get; init; }
        /// <summary>Per-dimension scores.</summary>
        public Dictionary<EcosystemHealthDimension, double> DimensionScores { get; init; } = new();
        /// <summary>Detected issues.</summary>
        public List<EcosystemIssue> Issues { get; init; } = new();
        /// <summary>Redundancy clusters.</summary>
        public List<EcosystemCluster> Clusters { get; init; } = new();
        /// <summary>Capability areas with no prompt coverage.</summary>
        public List<string> CoverageGaps { get; init; } = new();
        /// <summary>Prompts that haven't been used recently.</summary>
        public List<string> StalePrompts { get; init; } = new();
        /// <summary>Actionable recommendations.</summary>
        public List<string> Recommendations { get; init; } = new();
        /// <summary>When analysis was performed.</summary>
        public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
    }

    #endregion

    #region Main Class

    /// <summary>
    /// Autonomous prompt ecosystem health analyzer. Examines an entire
    /// portfolio for diversity, coverage, freshness, redundancy, complexity,
    /// consistency, and resilience. Produces multi-format reports with
    /// interactive HTML dashboards.
    /// </summary>
    public class PromptEcosystem
    {
        private readonly List<EcosystemPrompt> _prompts = new();
        private TimeSpan _stalenessThreshold = TimeSpan.FromDays(90);
        private double _redundancyThreshold = 0.7;
        private int _minDiversity = 3;
        private EcosystemReport? _report;

        private static readonly string[] CapabilityAreas = new[]
        {
            "Reasoning", "Creative", "Extraction", "Summary",
            "Classification", "Code", "Safety", "Conversation"
        };

        private static readonly Dictionary<string, string[]> CapabilityKeywords = new()
        {
            ["Reasoning"] = new[] { "reason", "logic", "analyze", "think", "deduc", "infer", "explain why", "step by step", "chain of thought" },
            ["Creative"] = new[] { "creat", "imagin", "story", "poem", "generat", "brainstorm", "invent", "write", "compose" },
            ["Extraction"] = new[] { "extract", "parse", "find", "identify", "detect", "locate", "pull out", "retrieve" },
            ["Summary"] = new[] { "summar", "condense", "brief", "overview", "tldr", "digest", "key points", "highlights" },
            ["Classification"] = new[] { "classif", "categoriz", "label", "sort", "group", "tag", "bucket", "type" },
            ["Code"] = new[] { "code", "program", "function", "implement", "debug", "refactor", "script", "algorithm" },
            ["Safety"] = new[] { "safe", "guard", "filter", "block", "reject", "moderate", "policy", "restrict", "harmful" },
            ["Conversation"] = new[] { "chat", "convers", "dialog", "respond", "reply", "discuss", "talk", "persona" }
        };

        private static readonly string[] ResilienceKeywords = new[]
        {
            "if error", "fallback", "otherwise", "in case", "handle",
            "edge case", "exception", "default", "try", "recover",
            "graceful", "alternative", "backup", "fail", "invalid"
        };

        #region Builder

        /// <summary>Add a prompt to the ecosystem.</summary>
        public PromptEcosystem AddPrompt(EcosystemPrompt prompt)
        {
            _prompts.Add(prompt);
            _report = null;
            return this;
        }

        /// <summary>Add multiple prompts.</summary>
        public PromptEcosystem AddPrompts(IEnumerable<EcosystemPrompt> prompts)
        {
            _prompts.AddRange(prompts);
            _report = null;
            return this;
        }

        /// <summary>Set staleness threshold (default 90 days).</summary>
        public PromptEcosystem WithStalenessThreshold(TimeSpan threshold)
        {
            _stalenessThreshold = threshold;
            _report = null;
            return this;
        }

        /// <summary>Set redundancy similarity threshold (default 0.7).</summary>
        public PromptEcosystem WithRedundancyThreshold(double threshold)
        {
            _redundancyThreshold = Math.Clamp(threshold, 0.0, 1.0);
            _report = null;
            return this;
        }

        /// <summary>Set minimum category diversity target.</summary>
        public PromptEcosystem WithMinDiversity(int count)
        {
            _minDiversity = Math.Max(1, count);
            _report = null;
            return this;
        }

        #endregion

        #region Analysis

        /// <summary>Run full ecosystem analysis.</summary>
        public EcosystemReport Analyze()
        {
            if (_prompts.Count == 0)
            {
                _report = new EcosystemReport
                {
                    OverallHealthScore = 0,
                    Recommendations = new List<string> { "Add prompts to the ecosystem before analyzing." }
                };
                return _report;
            }

            var issues = new List<EcosystemIssue>();
            var recommendations = new List<string>();

            double diversityScore = AnalyzeDiversity(issues, recommendations);
            double coverageScore = AnalyzeCoverage(issues, recommendations, out var gaps);
            double freshnessScore = AnalyzeFreshness(issues, recommendations, out var stale);
            var clusters = BuildClusters();
            double redundancyScore = AnalyzeRedundancy(clusters, issues, recommendations);
            double complexityScore = AnalyzeComplexity(issues, recommendations);
            double consistencyScore = AnalyzeConsistency(issues, recommendations);
            double resilienceScore = AnalyzeResilience(issues, recommendations);

            var dimScores = new Dictionary<EcosystemHealthDimension, double>
            {
                [EcosystemHealthDimension.Diversity] = diversityScore,
                [EcosystemHealthDimension.Coverage] = coverageScore,
                [EcosystemHealthDimension.Freshness] = freshnessScore,
                [EcosystemHealthDimension.Redundancy] = redundancyScore,
                [EcosystemHealthDimension.Complexity] = complexityScore,
                [EcosystemHealthDimension.Consistency] = consistencyScore,
                [EcosystemHealthDimension.Resilience] = resilienceScore
            };

            double overall = diversityScore * 0.15 + coverageScore * 0.20 +
                             freshnessScore * 0.15 + redundancyScore * 0.15 +
                             complexityScore * 0.10 + consistencyScore * 0.10 +
                             resilienceScore * 0.15;

            _report = new EcosystemReport
            {
                OverallHealthScore = Math.Round(overall, 1),
                DimensionScores = dimScores,
                Issues = issues.OrderByDescending(i => i.Severity).ToList(),
                Clusters = clusters,
                CoverageGaps = gaps,
                StalePrompts = stale,
                Recommendations = recommendations,
                AnalyzedAt = DateTime.UtcNow
            };
            return _report;
        }

        private double AnalyzeDiversity(List<EcosystemIssue> issues, List<string> recs)
        {
            var categories = _prompts.Select(p => p.Category).ToList();
            var groups = categories.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            int distinctCount = groups.Count;

            // Shannon entropy
            double entropy = 0;
            foreach (var kvp in groups)
            {
                double p = (double)kvp.Value / _prompts.Count;
                if (p > 0) entropy -= p * Math.Log2(p);
            }
            double maxEntropy = distinctCount > 1 ? Math.Log2(distinctCount) : 1;
            double evenness = maxEntropy > 0 ? entropy / maxEntropy : 0;

            double score = Math.Min(100, (distinctCount >= _minDiversity ? 50 : 50.0 * distinctCount / _minDiversity) + evenness * 50);

            if (distinctCount < _minDiversity)
            {
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Diversity,
                    Severity = distinctCount == 1 ? EcosystemIssueSeverity.High : EcosystemIssueSeverity.Medium,
                    Description = $"Only {distinctCount} category(ies) found; target is {_minDiversity}+.",
                    Recommendation = "Add prompts in new categories to diversify."
                });
                recs.Add($"Increase category diversity from {distinctCount} to at least {_minDiversity}.");
            }

            var dominant = groups.Where(g => (double)g.Value / _prompts.Count > 0.5).Select(g => g.Key).ToList();
            if (dominant.Count > 0)
            {
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Diversity,
                    Severity = EcosystemIssueSeverity.Medium,
                    Description = $"Category '{dominant[0]}' dominates with >{50}% of prompts.",
                    AffectedPrompts = _prompts.Where(p => p.Category == dominant[0]).Select(p => p.Name).ToList(),
                    Recommendation = $"Balance portfolio by adding prompts outside '{dominant[0]}'."
                });
            }

            return Math.Round(score, 1);
        }

        private double AnalyzeCoverage(List<EcosystemIssue> issues, List<string> recs, out List<string> gaps)
        {
            gaps = new List<string>();
            int covered = 0;

            foreach (var area in CapabilityAreas)
            {
                var keywords = CapabilityKeywords[area];
                bool found = _prompts.Any(p =>
                {
                    string lower = p.Text.ToLowerInvariant() + " " + p.Category.ToLowerInvariant() + " " + string.Join(" ", p.Tags).ToLowerInvariant();
                    return keywords.Any(k => lower.Contains(k));
                });
                if (found) covered++;
                else gaps.Add(area);
            }

            double score = 100.0 * covered / CapabilityAreas.Length;

            if (gaps.Count > 0)
            {
                var sev = gaps.Count > 4 ? EcosystemIssueSeverity.High :
                          gaps.Count > 2 ? EcosystemIssueSeverity.Medium : EcosystemIssueSeverity.Low;
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Coverage,
                    Severity = sev,
                    Description = $"Missing coverage in {gaps.Count} capability area(s): {string.Join(", ", gaps)}.",
                    Recommendation = "Create prompts targeting uncovered capabilities."
                });
                recs.Add($"Add prompts for: {string.Join(", ", gaps)}.");
            }

            return Math.Round(score, 1);
        }

        private double AnalyzeFreshness(List<EcosystemIssue> issues, List<string> recs, out List<string> stale)
        {
            var now = DateTime.UtcNow;
            stale = _prompts
                .Where(p =>
                {
                    var lastActive = p.LastUsedUtc ?? p.CreatedUtc;
                    return (now - lastActive) > _stalenessThreshold;
                })
                .Select(p => p.Name)
                .ToList();

            double score = 100.0 * (1.0 - (double)stale.Count / _prompts.Count);

            if (stale.Count > 0)
            {
                var sev = (double)stale.Count / _prompts.Count > 0.5 ? EcosystemIssueSeverity.High :
                          stale.Count > 3 ? EcosystemIssueSeverity.Medium : EcosystemIssueSeverity.Low;
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Freshness,
                    Severity = sev,
                    Description = $"{stale.Count} prompt(s) haven't been used in over {_stalenessThreshold.TotalDays} days.",
                    AffectedPrompts = stale,
                    Recommendation = "Review stale prompts — update, archive, or retire them."
                });
                recs.Add($"Review {stale.Count} stale prompt(s): {string.Join(", ", stale.Take(5))}{(stale.Count > 5 ? "..." : "")}.");
            }

            return Math.Round(Math.Max(0, score), 1);
        }

        private static HashSet<string> Tokenize(string text)
        {
            return text.ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToHashSet();
        }

        private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 && b.Count == 0) return 0;
            int intersection = a.Count(w => b.Contains(w));
            int union = a.Count + b.Count - intersection;
            return union > 0 ? (double)intersection / union : 0;
        }

        private List<EcosystemCluster> BuildClusters()
        {
            var tokens = _prompts.Select(p => Tokenize(p.Text)).ToList();
            var assigned = new bool[_prompts.Count];
            var clusters = new List<EcosystemCluster>();

            for (int i = 0; i < _prompts.Count; i++)
            {
                if (assigned[i]) continue;
                var members = new List<int> { i };
                assigned[i] = true;

                for (int j = i + 1; j < _prompts.Count; j++)
                {
                    if (assigned[j]) continue;
                    if (JaccardSimilarity(tokens[i], tokens[j]) >= _redundancyThreshold)
                    {
                        members.Add(j);
                        assigned[j] = true;
                    }
                }

                if (members.Count > 1)
                {
                    // Find centroid keywords (common across all members)
                    var commonWords = members.Select(m => tokens[m]).Aggregate((a, b) => { a.IntersectWith(b); return a; });
                    var avgSim = 0.0;
                    int pairs = 0;
                    for (int x = 0; x < members.Count; x++)
                        for (int y = x + 1; y < members.Count; y++)
                        {
                            avgSim += JaccardSimilarity(tokens[members[x]], tokens[members[y]]);
                            pairs++;
                        }
                    if (pairs > 0) avgSim /= pairs;

                    clusters.Add(new EcosystemCluster
                    {
                        Name = $"Cluster-{clusters.Count + 1} ({_prompts[members[0]].Category})",
                        Members = members.Select(m => _prompts[m].Name).ToList(),
                        CentroidKeywords = commonWords.Take(8).ToList(),
                        RedundancyScore = Math.Round(avgSim, 3)
                    });
                }
            }

            return clusters;
        }

        private double AnalyzeRedundancy(List<EcosystemCluster> clusters, List<EcosystemIssue> issues, List<string> recs)
        {
            int redundantPrompts = clusters.Sum(c => c.Members.Count - 1);
            double score = 100.0 * (1.0 - (double)redundantPrompts / Math.Max(1, _prompts.Count));

            foreach (var cluster in clusters.Where(c => c.RedundancyScore > 0.8))
            {
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Redundancy,
                    Severity = EcosystemIssueSeverity.High,
                    Description = $"High redundancy ({cluster.RedundancyScore:P0}) in {cluster.Name}: {cluster.Members.Count} near-identical prompts.",
                    AffectedPrompts = cluster.Members,
                    Recommendation = "Consolidate redundant prompts into a single parameterized template."
                });
            }

            if (redundantPrompts > 0)
                recs.Add($"Consolidate {redundantPrompts} redundant prompt(s) across {clusters.Count} cluster(s).");

            return Math.Round(Math.Max(0, score), 1);
        }

        private double AnalyzeComplexity(List<EcosystemIssue> issues, List<string> recs)
        {
            var lengths = _prompts.Select(p => (double)p.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length).ToList();
            double mean = lengths.Average();
            double stddev = lengths.Count > 1
                ? Math.Sqrt(lengths.Sum(l => (l - mean) * (l - mean)) / (lengths.Count - 1))
                : 0;
            double cv = mean > 0 ? stddev / mean : 0; // coefficient of variation

            // Good complexity = moderate variation (cv between 0.3 and 0.8)
            double score;
            if (cv < 0.1) score = 40; // all same length = boring
            else if (cv < 0.3) score = 60 + (cv - 0.1) / 0.2 * 40;
            else if (cv <= 0.8) score = 100;
            else score = Math.Max(40, 100 - (cv - 0.8) * 100);

            if (cv < 0.15)
            {
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Complexity,
                    Severity = EcosystemIssueSeverity.Low,
                    Description = $"Low complexity variation (CV={cv:F2}). All prompts are similar length (~{mean:F0} words).",
                    Recommendation = "Mix simple and complex prompts for different use cases."
                });
                recs.Add("Add both concise and detailed prompts for variety.");
            }

            var outliers = _prompts.Where(p =>
            {
                double wc = p.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                return stddev > 0 && Math.Abs(wc - mean) > 2.5 * stddev;
            }).Select(p => p.Name).ToList();

            if (outliers.Count > 0)
            {
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Complexity,
                    Severity = EcosystemIssueSeverity.Low,
                    Description = $"{outliers.Count} prompt(s) are complexity outliers (>2.5σ from mean).",
                    AffectedPrompts = outliers,
                    Recommendation = "Review outlier prompts — they may be too long or too short."
                });
            }

            return Math.Round(score, 1);
        }

        private double AnalyzeConsistency(List<EcosystemIssue> issues, List<string> recs)
        {
            int total = _prompts.Count;
            int startsUpper = _prompts.Count(p => p.Text.Length > 0 && char.IsUpper(p.Text[0]));
            int endsWithPeriod = _prompts.Count(p => p.Text.TrimEnd().EndsWith('.'));
            int usesYou = _prompts.Count(p => p.Text.Contains("you ", StringComparison.OrdinalIgnoreCase));
            int usesImperative = _prompts.Count(p =>
            {
                var first = p.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLower() ?? "";
                return new[] { "write", "create", "generate", "analyze", "summarize", "list", "explain", "describe", "find", "build" }.Contains(first);
            });

            // Score based on how consistent each pattern is (closer to all-or-nothing = more consistent)
            double ConsistencyOf(int count) => Math.Max((double)count / total, 1.0 - (double)count / total);

            double avg = (ConsistencyOf(startsUpper) + ConsistencyOf(endsWithPeriod) +
                         ConsistencyOf(usesYou) + ConsistencyOf(usesImperative)) / 4.0;
            double score = avg * 100;

            if (score < 60)
            {
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Consistency,
                    Severity = EcosystemIssueSeverity.Medium,
                    Description = $"Inconsistent prompt formatting (score: {score:F0}/100). Mixed capitalization, punctuation, and instruction styles.",
                    Recommendation = "Establish a prompt style guide and normalize formatting."
                });
                recs.Add("Create a prompt style guide for consistent formatting.");
            }

            return Math.Round(score, 1);
        }

        private double AnalyzeResilience(List<EcosystemIssue> issues, List<string> recs)
        {
            int resilient = _prompts.Count(p =>
            {
                string lower = p.Text.ToLowerInvariant();
                return ResilienceKeywords.Count(k => lower.Contains(k)) >= 2;
            });

            double score = 100.0 * resilient / _prompts.Count;

            if (score < 30)
            {
                issues.Add(new EcosystemIssue
                {
                    Dimension = EcosystemHealthDimension.Resilience,
                    Severity = score < 10 ? EcosystemIssueSeverity.High : EcosystemIssueSeverity.Medium,
                    Description = $"Only {resilient}/{_prompts.Count} prompt(s) include error handling or fallback instructions.",
                    Recommendation = "Add fallback behaviors, edge case handling, and graceful degradation to prompts."
                });
                recs.Add("Improve resilience by adding fallback instructions to more prompts.");
            }

            return Math.Round(score, 1);
        }

        #endregion

        #region Export

        /// <summary>Export report as plain text.</summary>
        public string ToText()
        {
            var r = _report ?? Analyze();
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("  PROMPT ECOSYSTEM HEALTH REPORT");
            sb.AppendLine($"  Analyzed: {r.AnalyzedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"  Portfolio: {_prompts.Count} prompt(s)");
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  OVERALL HEALTH: {r.OverallHealthScore}/100 {HealthEmoji(r.OverallHealthScore)}");
            sb.AppendLine();

            sb.AppendLine("  DIMENSION SCORES:");
            foreach (var kvp in r.DimensionScores)
                sb.AppendLine($"    {kvp.Key,-14} {kvp.Value,6:F1}/100 {HealthBar(kvp.Value)}");
            sb.AppendLine();

            if (r.Issues.Count > 0)
            {
                sb.AppendLine($"  ISSUES ({r.Issues.Count}):");
                foreach (var issue in r.Issues)
                {
                    sb.AppendLine($"    [{issue.Severity}] {issue.Description}");
                    if (issue.AffectedPrompts.Count > 0)
                        sb.AppendLine($"      Affected: {string.Join(", ", issue.AffectedPrompts.Take(5))}{(issue.AffectedPrompts.Count > 5 ? "..." : "")}");
                    sb.AppendLine($"      → {issue.Recommendation}");
                }
                sb.AppendLine();
            }

            if (r.Clusters.Count > 0)
            {
                sb.AppendLine($"  REDUNDANCY CLUSTERS ({r.Clusters.Count}):");
                foreach (var c in r.Clusters)
                    sb.AppendLine($"    {c.Name}: [{string.Join(", ", c.Members)}] (sim={c.RedundancyScore:P0})");
                sb.AppendLine();
            }

            if (r.CoverageGaps.Count > 0)
                sb.AppendLine($"  COVERAGE GAPS: {string.Join(", ", r.CoverageGaps)}");
            if (r.StalePrompts.Count > 0)
                sb.AppendLine($"  STALE PROMPTS: {string.Join(", ", r.StalePrompts)}");

            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  RECOMMENDATIONS:");
                foreach (var rec in r.Recommendations)
                    sb.AppendLine($"    • {rec}");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>Export report as Markdown.</summary>
        public string ToMarkdown()
        {
            var r = _report ?? Analyze();
            var sb = new StringBuilder();
            sb.AppendLine("# 🌿 Prompt Ecosystem Health Report");
            sb.AppendLine();
            sb.AppendLine($"**Analyzed:** {r.AnalyzedAt:yyyy-MM-dd HH:mm} UTC | **Portfolio:** {_prompts.Count} prompts");
            sb.AppendLine();
            sb.AppendLine($"## Overall Health: {r.OverallHealthScore}/100 {HealthEmoji(r.OverallHealthScore)}");
            sb.AppendLine();
            sb.AppendLine("| Dimension | Score | Status |");
            sb.AppendLine("|-----------|-------|--------|");
            foreach (var kvp in r.DimensionScores)
                sb.AppendLine($"| {kvp.Key} | {kvp.Value:F1}/100 | {HealthEmoji(kvp.Value)} |");
            sb.AppendLine();

            if (r.Issues.Count > 0)
            {
                sb.AppendLine($"## Issues ({r.Issues.Count})");
                sb.AppendLine();
                foreach (var issue in r.Issues)
                {
                    string badge = issue.Severity switch
                    {
                        EcosystemIssueSeverity.Critical => "🔴",
                        EcosystemIssueSeverity.High => "🟠",
                        EcosystemIssueSeverity.Medium => "🟡",
                        _ => "🟢"
                    };
                    sb.AppendLine($"- {badge} **{issue.Severity}** ({issue.Dimension}): {issue.Description}");
                    sb.AppendLine($"  - *{issue.Recommendation}*");
                }
                sb.AppendLine();
            }

            if (r.Clusters.Count > 0)
            {
                sb.AppendLine("## Redundancy Clusters");
                sb.AppendLine();
                foreach (var c in r.Clusters)
                    sb.AppendLine($"- **{c.Name}** ({c.RedundancyScore:P0} similarity): {string.Join(", ", c.Members)}");
                sb.AppendLine();
            }

            if (r.CoverageGaps.Count > 0)
                sb.AppendLine($"## Coverage Gaps\n\nMissing: {string.Join(", ", r.CoverageGaps)}\n");
            if (r.StalePrompts.Count > 0)
                sb.AppendLine($"## Stale Prompts\n\n{string.Join(", ", r.StalePrompts)}\n");

            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine("## Recommendations");
                sb.AppendLine();
                foreach (var rec in r.Recommendations)
                    sb.AppendLine($"1. {rec}");
            }

            return sb.ToString();
        }

        /// <summary>Export report as JSON.</summary>
        public string ToJson()
        {
            var r = _report ?? Analyze();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            return JsonSerializer.Serialize(r, options);
        }

        /// <summary>Export report as self-contained interactive HTML dashboard.</summary>
        public string ToHtml()
        {
            var r = _report ?? Analyze();
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.AppendLine("<title>Prompt Ecosystem Health Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI',system-ui,sans-serif;background:#0f1117;color:#e2e8f0;min-height:100vh;padding:24px}
.container{max-width:1200px;margin:0 auto}
h1{font-size:28px;margin-bottom:8px;color:#7dd3fc}
.subtitle{color:#94a3b8;margin-bottom:24px;font-size:14px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:16px;margin-bottom:24px}
.card{background:#1e2030;border-radius:12px;padding:20px;border:1px solid #2d3148}
.card h2{font-size:16px;color:#94a3b8;margin-bottom:12px;text-transform:uppercase;letter-spacing:1px;font-weight:600}
.gauge-container{display:flex;justify-content:center;align-items:center;margin:16px 0}
.score-big{font-size:48px;font-weight:700}
.score-label{font-size:13px;color:#64748b;text-align:center}
.bar{height:8px;border-radius:4px;background:#1a1d2e;margin:4px 0;overflow:hidden}
.bar-fill{height:100%;border-radius:4px;transition:width .3s}
.dim-row{display:flex;align-items:center;gap:8px;margin-bottom:8px}
.dim-name{width:100px;font-size:13px;color:#94a3b8}
.dim-score{width:50px;text-align:right;font-size:13px;font-weight:600}
.issue{padding:10px 12px;border-radius:8px;margin-bottom:8px;border-left:4px solid}
.issue.Critical{background:#3b111d;border-color:#ef4444}
.issue.High{background:#3b2a11;border-color:#f97316}
.issue.Medium{background:#3b3511;border-color:#eab308}
.issue.Low{background:#113b1a;border-color:#22c55e}
.issue-header{font-size:13px;font-weight:600;margin-bottom:4px}
.issue-desc{font-size:12px;color:#94a3b8}
.issue-rec{font-size:11px;color:#7dd3fc;margin-top:4px;font-style:italic}
.cluster{background:#1a1d2e;border-radius:8px;padding:12px;margin-bottom:8px}
.cluster-name{font-weight:600;font-size:14px;color:#a78bfa}
.cluster-members{font-size:12px;color:#94a3b8;margin-top:4px}
.tag{display:inline-block;background:#2d3148;border-radius:4px;padding:2px 8px;font-size:11px;margin:2px;color:#e2e8f0}
.rec-list{list-style:none;counter-reset:rec}
.rec-list li{counter-increment:rec;padding:8px 0 8px 32px;position:relative;border-bottom:1px solid #1a1d2e;font-size:13px}
.rec-list li::before{content:counter(rec);position:absolute;left:0;width:22px;height:22px;background:#7dd3fc;color:#0f1117;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700}
.gap-tag{display:inline-block;background:#3b111d;color:#fca5a5;border-radius:4px;padding:3px 10px;margin:3px;font-size:12px}
.stale-tag{display:inline-block;background:#3b3511;color:#fde68a;border-radius:4px;padding:3px 10px;margin:3px;font-size:12px}
svg text{font-family:'Segoe UI',system-ui,sans-serif}
");
            sb.AppendLine("</style></head><body><div class=\"container\">");
            sb.AppendLine($"<h1>🌿 Prompt Ecosystem Health</h1>");
            sb.AppendLine($"<div class=\"subtitle\">{_prompts.Count} prompts analyzed · {r.AnalyzedAt:yyyy-MM-dd HH:mm} UTC</div>");

            sb.AppendLine("<div class=\"grid\">");

            // Overall health gauge (SVG circular gauge)
            string gaugeColor = r.OverallHealthScore >= 70 ? "#22c55e" : r.OverallHealthScore >= 40 ? "#eab308" : "#ef4444";
            double circumference = 2 * Math.PI * 54;
            double dashOffset = circumference * (1 - r.OverallHealthScore / 100.0);
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<h2>Overall Health</h2>");
            sb.AppendLine("<div class=\"gauge-container\">");
            sb.AppendLine($"<svg width=\"160\" height=\"160\" viewBox=\"0 0 120 120\">");
            sb.AppendLine($"<circle cx=\"60\" cy=\"60\" r=\"54\" fill=\"none\" stroke=\"#1a1d2e\" stroke-width=\"8\"/>");
            sb.AppendLine($"<circle cx=\"60\" cy=\"60\" r=\"54\" fill=\"none\" stroke=\"{gaugeColor}\" stroke-width=\"8\" stroke-linecap=\"round\" stroke-dasharray=\"{circumference:F1}\" stroke-dashoffset=\"{dashOffset:F1}\" transform=\"rotate(-90 60 60)\"/>");
            sb.AppendLine($"<text x=\"60\" y=\"56\" text-anchor=\"middle\" fill=\"{gaugeColor}\" font-size=\"28\" font-weight=\"700\">{r.OverallHealthScore:F0}</text>");
            sb.AppendLine($"<text x=\"60\" y=\"72\" text-anchor=\"middle\" fill=\"#64748b\" font-size=\"10\">/ 100</text>");
            sb.AppendLine("</svg></div>");
            sb.AppendLine("<div class=\"score-label\">" + (r.OverallHealthScore >= 80 ? "Thriving" : r.OverallHealthScore >= 60 ? "Healthy" : r.OverallHealthScore >= 40 ? "Needs Attention" : "Critical") + "</div>");
            sb.AppendLine("</div>");

            // Dimension scores
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<h2>Dimensions</h2>");
            foreach (var kvp in r.DimensionScores)
            {
                string c = kvp.Value >= 70 ? "#22c55e" : kvp.Value >= 40 ? "#eab308" : "#ef4444";
                sb.AppendLine($"<div class=\"dim-row\"><span class=\"dim-name\">{kvp.Key}</span><div class=\"bar\" style=\"flex:1\"><div class=\"bar-fill\" style=\"width:{kvp.Value}%;background:{c}\"></div></div><span class=\"dim-score\" style=\"color:{c}\">{kvp.Value:F0}</span></div>");
            }
            sb.AppendLine("</div>");

            // Radar chart (SVG)
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<h2>Radar</h2>");
            sb.AppendLine("<svg viewBox=\"0 0 300 300\" style=\"max-width:280px;margin:0 auto;display:block\">");
            var dims = r.DimensionScores.ToList();
            int n = dims.Count;
            double cx2 = 150, cy2 = 150, maxR = 120;
            // Grid circles
            for (int ring = 25; ring <= 100; ring += 25)
            {
                double rr = maxR * ring / 100.0;
                sb.AppendLine($"<circle cx=\"{cx2}\" cy=\"{cy2}\" r=\"{rr:F1}\" fill=\"none\" stroke=\"#2d3148\" stroke-width=\"0.5\"/>");
            }
            // Spokes and labels
            var points = new List<(double x, double y)>();
            for (int i = 0; i < n; i++)
            {
                double angle = -Math.PI / 2 + 2 * Math.PI * i / n;
                double ex = cx2 + maxR * Math.Cos(angle);
                double ey = cy2 + maxR * Math.Sin(angle);
                sb.AppendLine($"<line x1=\"{cx2}\" y1=\"{cy2}\" x2=\"{ex:F1}\" y2=\"{ey:F1}\" stroke=\"#2d3148\" stroke-width=\"0.5\"/>");
                double lx = cx2 + (maxR + 18) * Math.Cos(angle);
                double ly = cy2 + (maxR + 18) * Math.Sin(angle);
                sb.AppendLine($"<text x=\"{lx:F1}\" y=\"{ly:F1}\" text-anchor=\"middle\" fill=\"#94a3b8\" font-size=\"8\">{dims[i].Key}</text>");
                double val = dims[i].Value / 100.0;
                points.Add((cx2 + maxR * val * Math.Cos(angle), cy2 + maxR * val * Math.Sin(angle)));
            }
            string polyPoints = string.Join(" ", points.Select(p => $"{p.x:F1},{p.y:F1}"));
            sb.AppendLine($"<polygon points=\"{polyPoints}\" fill=\"rgba(125,211,252,0.15)\" stroke=\"#7dd3fc\" stroke-width=\"2\"/>");
            foreach (var p in points)
                sb.AppendLine($"<circle cx=\"{p.x:F1}\" cy=\"{p.y:F1}\" r=\"3\" fill=\"#7dd3fc\"/>");
            sb.AppendLine("</svg></div>");

            sb.AppendLine("</div>"); // grid

            // Issues
            if (r.Issues.Count > 0)
            {
                sb.AppendLine("<div class=\"card\" style=\"margin-bottom:16px\">");
                sb.AppendLine($"<h2>Issues ({r.Issues.Count})</h2>");
                foreach (var issue in r.Issues)
                {
                    sb.AppendLine($"<div class=\"issue {issue.Severity}\">");
                    sb.AppendLine($"<div class=\"issue-header\">{issue.Severity} · {issue.Dimension}</div>");
                    sb.AppendLine($"<div class=\"issue-desc\">{Esc(issue.Description)}</div>");
                    if (issue.AffectedPrompts.Count > 0)
                        sb.AppendLine($"<div class=\"issue-desc\">Affected: {Esc(string.Join(", ", issue.AffectedPrompts.Take(5)))}{(issue.AffectedPrompts.Count > 5 ? "..." : "")}</div>");
                    sb.AppendLine($"<div class=\"issue-rec\">→ {Esc(issue.Recommendation)}</div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            // Clusters + Gaps + Stale in grid
            sb.AppendLine("<div class=\"grid\">");

            if (r.Clusters.Count > 0)
            {
                sb.AppendLine("<div class=\"card\">");
                sb.AppendLine($"<h2>Redundancy Clusters ({r.Clusters.Count})</h2>");
                foreach (var c in r.Clusters)
                {
                    sb.AppendLine($"<div class=\"cluster\"><div class=\"cluster-name\">{Esc(c.Name)} ({c.RedundancyScore:P0})</div>");
                    sb.AppendLine($"<div class=\"cluster-members\">{string.Join(" ", c.Members.Select(m => $"<span class=\"tag\">{Esc(m)}</span>"))}</div>");
                    if (c.CentroidKeywords.Count > 0)
                        sb.AppendLine($"<div class=\"cluster-members\" style=\"margin-top:4px\">Keywords: {string.Join(", ", c.CentroidKeywords.Take(6))}</div>");
                    sb.AppendLine("</div>");
                }
                sb.AppendLine("</div>");
            }

            if (r.CoverageGaps.Count > 0 || r.StalePrompts.Count > 0)
            {
                sb.AppendLine("<div class=\"card\">");
                if (r.CoverageGaps.Count > 0)
                {
                    sb.AppendLine("<h2>Coverage Gaps</h2>");
                    sb.AppendLine("<div style=\"margin-bottom:16px\">" + string.Join("", r.CoverageGaps.Select(g => $"<span class=\"gap-tag\">{Esc(g)}</span>")) + "</div>");
                }
                if (r.StalePrompts.Count > 0)
                {
                    sb.AppendLine("<h2 style=\"margin-top:12px\">Stale Prompts</h2>");
                    sb.AppendLine("<div>" + string.Join("", r.StalePrompts.Select(s => $"<span class=\"stale-tag\">{Esc(s)}</span>")) + "</div>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>"); // grid

            // Recommendations
            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine("<div class=\"card\">");
                sb.AppendLine("<h2>Recommendations</h2>");
                sb.AppendLine("<ol class=\"rec-list\">");
                foreach (var rec in r.Recommendations)
                    sb.AppendLine($"<li>{Esc(rec)}</li>");
                sb.AppendLine("</ol></div>");
            }

            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }

        private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string HealthEmoji(double score) => score >= 80 ? "🟢" : score >= 60 ? "🟡" : score >= 40 ? "🟠" : "🔴";

        private static string HealthBar(double score)
        {
            int filled = (int)(score / 5);
            return "[" + new string('█', filled) + new string('░', 20 - filled) + "]";
        }

        #endregion
    }

    #endregion
}
