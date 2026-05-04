namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptEntanglementEngine – Autonomous Hidden Dependency Detector
    //
    //  Detects and maps hidden "entanglements" between prompts in a
    //  fleet — shared variables, template dependencies, semantic
    //  overlap, behavioral correlations, cascade risk chains, and
    //  resource contention. Like quantum entanglement, changing one
    //  prompt can unexpectedly affect others.
    //
    //  7 detection engines: Variable Entanglement, Template Dependency,
    //  Semantic Overlap, Behavioral Correlation, Cascade Risk, Cluster
    //  Detection (union-find), and Insight Generation.
    // ────────────────────────────────────────────

    /// <summary>Type of entanglement between two prompts.</summary>
    public enum EntanglementType
    {
        /// <summary>Prompts share one or more variable names.</summary>
        SharedVariable,
        /// <summary>Prompts reference the same or chained templates.</summary>
        TemplateDependency,
        /// <summary>Prompts have significant keyword/content overlap.</summary>
        SemanticOverlap,
        /// <summary>Prompt outcomes are statistically correlated.</summary>
        BehavioralCorrelation,
        /// <summary>Changing one prompt could cascade to affect the other.</summary>
        CascadeRisk,
        /// <summary>Prompts must execute in a specific order.</summary>
        OrderDependency,
        /// <summary>Prompts compete for the same limited resource.</summary>
        ResourceContention
    }

    /// <summary>Severity tier based on entanglement strength.</summary>
    public enum EntanglementSeverity
    {
        /// <summary>Strength 0–20.</summary>
        Negligible,
        /// <summary>Strength 21–40.</summary>
        Low,
        /// <summary>Strength 41–60.</summary>
        Moderate,
        /// <summary>Strength 61–80.</summary>
        High,
        /// <summary>Strength 81–100.</summary>
        Critical
    }

    /// <summary>Health tier for the overall entanglement score.</summary>
    public enum EntanglementHealthTier
    {
        /// <summary>Score 90–100 — minimal coupling.</summary>
        Decoupled,
        /// <summary>Score 70–89 — acceptable coupling.</summary>
        Manageable,
        /// <summary>Score 50–69 — noticeable coupling.</summary>
        Tangled,
        /// <summary>Score 20–49 — problematic coupling.</summary>
        Knotted,
        /// <summary>Score 0–19 — severe coupling everywhere.</summary>
        Spaghetti
    }

    /// <summary>Registration data for a prompt in the fleet.</summary>
    public class PromptRegistration
    {
        /// <summary>Unique identifier for this prompt.</summary>
        public string PromptId { get; set; } = "";
        /// <summary>The prompt content/text.</summary>
        public string Content { get; set; } = "";
        /// <summary>Variable names used in this prompt.</summary>
        public List<string> Variables { get; set; } = new();
        /// <summary>Tags or categories for this prompt.</summary>
        public List<string> Tags { get; set; } = new();
        /// <summary>Template references this prompt depends on.</summary>
        public List<string> TemplateRefs { get; set; } = new();
        /// <summary>Explicit dependency prompt IDs.</summary>
        public List<string> Dependencies { get; set; } = new();
        /// <summary>When this prompt was registered.</summary>
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A detected entanglement between two prompts.</summary>
    public class Entanglement
    {
        /// <summary>First prompt in the entangled pair.</summary>
        public string PromptA { get; set; } = "";
        /// <summary>Second prompt in the entangled pair.</summary>
        public string PromptB { get; set; } = "";
        /// <summary>Type of entanglement detected.</summary>
        public EntanglementType Type { get; set; }
        /// <summary>Strength of entanglement 0–100.</summary>
        public double Strength { get; set; }
        /// <summary>Evidence supporting this entanglement.</summary>
        public List<string> Evidence { get; set; } = new();
        /// <summary>When this entanglement was detected.</summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Severity tier based on strength.</summary>
        public EntanglementSeverity Severity =>
            Strength switch
            {
                <= 20 => EntanglementSeverity.Negligible,
                <= 40 => EntanglementSeverity.Low,
                <= 60 => EntanglementSeverity.Moderate,
                <= 80 => EntanglementSeverity.High,
                _ => EntanglementSeverity.Critical
            };
    }

    /// <summary>A cluster of entangled prompts.</summary>
    public class EntanglementCluster
    {
        /// <summary>Unique identifier for this cluster.</summary>
        public string ClusterId { get; set; } = "";
        /// <summary>Prompt IDs in this cluster.</summary>
        public List<string> PromptIds { get; set; } = new();
        /// <summary>Most common entanglement type in the cluster.</summary>
        public EntanglementType DominantType { get; set; }
        /// <summary>Average entanglement strength within the cluster.</summary>
        public double AvgStrength { get; set; }
        /// <summary>Risk score for this cluster 0–100.</summary>
        public double RiskScore { get; set; }
    }

    /// <summary>A cascade chain from a source prompt through entangled neighbors.</summary>
    public class EntanglementCascadeChain
    {
        /// <summary>The prompt that initiates the cascade.</summary>
        public string SourcePromptId { get; set; } = "";
        /// <summary>Prompts affected by changes to the source.</summary>
        public List<string> AffectedPromptIds { get; set; } = new();
        /// <summary>Length of the cascade chain.</summary>
        public int ChainLength { get; set; }
        /// <summary>Maximum entanglement strength in the chain.</summary>
        public double MaxStrength { get; set; }
        /// <summary>Description of the cascade path.</summary>
        public string Description { get; set; } = "";
    }

    /// <summary>Comprehensive entanglement report for the fleet.</summary>
    public class EntanglementReport
    {
        /// <summary>Total registered prompts.</summary>
        public int TotalPrompts { get; set; }
        /// <summary>Total entanglements detected.</summary>
        public int TotalEntanglements { get; set; }
        /// <summary>Entanglement clusters.</summary>
        public List<EntanglementCluster> Clusters { get; set; } = new();
        /// <summary>Strongest entangled pairs.</summary>
        public List<Entanglement> StrongestPairs { get; set; } = new();
        /// <summary>Cascade chains detected.</summary>
        public List<EntanglementCascadeChain> CascadeChains { get; set; } = new();
        /// <summary>Overall health score 0–100.</summary>
        public double HealthScore { get; set; }
        /// <summary>Health tier classification.</summary>
        public EntanglementHealthTier HealthTier { get; set; }
        /// <summary>Autonomous insights about the fleet.</summary>
        public List<string> Insights { get; set; } = new();
    }

    /// <summary>Configuration for the entanglement engine.</summary>
    public class EntanglementConfig
    {
        /// <summary>Jaccard threshold for variable overlap detection (0–1).</summary>
        public double VariableOverlapThreshold { get; set; } = 0.3;
        /// <summary>Keyword similarity threshold for semantic overlap (0–1).</summary>
        public double SemanticSimilarityThreshold { get; set; } = 0.4;
        /// <summary>Maximum depth for cascade chain BFS.</summary>
        public int CascadeDepthLimit { get; set; } = 5;
        /// <summary>Minimum strength to include in reports.</summary>
        public double MinStrengthToReport { get; set; } = 20;
        /// <summary>Maximum clusters to report.</summary>
        public int MaxClustersToReport { get; set; } = 10;
        /// <summary>Minimum outcomes to compute behavioral correlation.</summary>
        public int MinOutcomesForCorrelation { get; set; } = 5;
        /// <summary>Pearson correlation threshold for behavioral detection.</summary>
        public double CorrelationThreshold { get; set; } = 0.5;
    }

    /// <summary>Execution outcome for behavioral correlation tracking.</summary>
    internal class EntanglementOutcome
    {
        public string PromptId { get; set; } = "";
        public bool Success { get; set; }
        public double LatencyMs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Autonomous entanglement detection engine for prompt fleets.
    /// Discovers hidden dependencies, shared variables, template chains,
    /// semantic overlap, behavioral correlations, and cascade risks.
    /// </summary>
    public class PromptEntanglementEngine
    {
        private readonly EntanglementConfig _config;
        private readonly Dictionary<string, PromptRegistration> _prompts = new();
        private readonly List<EntanglementOutcome> _outcomes = new();

        /// <summary>Create a new entanglement engine with optional configuration.</summary>
        public PromptEntanglementEngine(EntanglementConfig? config = null)
        {
            _config = config ?? new EntanglementConfig();
        }

        // ─── Registration ───────────────────────────

        /// <summary>Register a prompt for entanglement analysis.</summary>
        public void RegisterPrompt(PromptRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            if (string.IsNullOrWhiteSpace(registration.PromptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(registration));

            _prompts[registration.PromptId] = registration;
        }

        /// <summary>Get the number of registered prompts.</summary>
        public int PromptCount => _prompts.Count;

        /// <summary>Check if a prompt is registered.</summary>
        public bool IsRegistered(string promptId) => _prompts.ContainsKey(promptId);

        // ─── Outcome Tracking ───────────────────────

        /// <summary>Record an execution outcome for behavioral correlation.</summary>
        public void RecordOutcome(string promptId, bool success, double latencyMs)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));
            if (latencyMs < 0)
                throw new ArgumentException("LatencyMs cannot be negative.", nameof(latencyMs));

            _outcomes.Add(new EntanglementOutcome
            {
                PromptId = promptId,
                Success = success,
                LatencyMs = latencyMs
            });
        }

        /// <summary>Get outcome count for a prompt.</summary>
        public int GetOutcomeCount(string promptId) =>
            _outcomes.Count(o => o.PromptId == promptId);

        // ─── Engine 1: Variable Entanglement ────────

        internal List<Entanglement> DetectVariableEntanglements()
        {
            var result = new List<Entanglement>();
            var ids = _prompts.Keys.ToList();

            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var a = _prompts[ids[i]];
                    var b = _prompts[ids[j]];
                    if (a.Variables.Count == 0 || b.Variables.Count == 0) continue;

                    var setA = new HashSet<string>(a.Variables, StringComparer.OrdinalIgnoreCase);
                    var setB = new HashSet<string>(b.Variables, StringComparer.OrdinalIgnoreCase);
                    var intersection = setA.Intersect(setB, StringComparer.OrdinalIgnoreCase).ToList();
                    var union = setA.Union(setB, StringComparer.OrdinalIgnoreCase).Count();

                    if (union == 0) continue;
                    double jaccard = (double)intersection.Count / union;

                    if (jaccard >= _config.VariableOverlapThreshold)
                    {
                        result.Add(new Entanglement
                        {
                            PromptA = ids[i],
                            PromptB = ids[j],
                            Type = EntanglementType.SharedVariable,
                            Strength = Math.Round(jaccard * 100, 1),
                            Evidence = intersection.Select(v => $"Shared variable: {v}").ToList()
                        });
                    }
                }
            }
            return result;
        }

        // ─── Engine 2: Template Dependency Mapper ───

        internal List<Entanglement> DetectTemplateDependencies()
        {
            var result = new List<Entanglement>();
            var ids = _prompts.Keys.ToList();

            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var a = _prompts[ids[i]];
                    var b = _prompts[ids[j]];
                    if (a.TemplateRefs.Count == 0 || b.TemplateRefs.Count == 0) continue;

                    var setA = new HashSet<string>(a.TemplateRefs, StringComparer.OrdinalIgnoreCase);
                    var setB = new HashSet<string>(b.TemplateRefs, StringComparer.OrdinalIgnoreCase);
                    var shared = setA.Intersect(setB, StringComparer.OrdinalIgnoreCase).ToList();

                    if (shared.Count > 0)
                    {
                        double strength = Math.Min(100, (double)shared.Count / Math.Max(setA.Count, setB.Count) * 100);
                        result.Add(new Entanglement
                        {
                            PromptA = ids[i],
                            PromptB = ids[j],
                            Type = EntanglementType.TemplateDependency,
                            Strength = Math.Round(strength, 1),
                            Evidence = shared.Select(t => $"Shared template: {t}").ToList()
                        });
                    }
                }
            }

            // Also detect dependency chain entanglements
            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var a = _prompts[ids[i]];
                    var b = _prompts[ids[j]];

                    bool aDepB = a.Dependencies.Contains(ids[j]);
                    bool bDepA = b.Dependencies.Contains(ids[i]);

                    if (aDepB || bDepA)
                    {
                        // Check if we already have an entanglement for this pair
                        bool exists = result.Any(e =>
                            (e.PromptA == ids[i] && e.PromptB == ids[j]) ||
                            (e.PromptA == ids[j] && e.PromptB == ids[i]));
                        if (exists) continue;

                        double strength = aDepB && bDepA ? 95 : 75;
                        var evidence = new List<string>();
                        if (aDepB) evidence.Add($"{ids[i]} depends on {ids[j]}");
                        if (bDepA) evidence.Add($"{ids[j]} depends on {ids[i]}");

                        result.Add(new Entanglement
                        {
                            PromptA = ids[i],
                            PromptB = ids[j],
                            Type = EntanglementType.OrderDependency,
                            Strength = strength,
                            Evidence = evidence
                        });
                    }
                }
            }

            return result;
        }

        // ─── Engine 3: Semantic Overlap Analyzer ────

        internal List<Entanglement> DetectSemanticOverlap()
        {
            var result = new List<Entanglement>();
            var ids = _prompts.Keys.ToList();

            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var a = _prompts[ids[i]];
                    var b = _prompts[ids[j]];
                    if (string.IsNullOrWhiteSpace(a.Content) || string.IsNullOrWhiteSpace(b.Content))
                        continue;

                    var wordsA = TokenizeContent(a.Content);
                    var wordsB = TokenizeContent(b.Content);
                    if (wordsA.Count == 0 || wordsB.Count == 0) continue;

                    var intersection = wordsA.Intersect(wordsB, StringComparer.OrdinalIgnoreCase).Count();
                    var union = wordsA.Union(wordsB, StringComparer.OrdinalIgnoreCase).Count();
                    if (union == 0) continue;

                    double jaccard = (double)intersection / union;
                    if (jaccard >= _config.SemanticSimilarityThreshold)
                    {
                        var sharedWords = wordsA.Intersect(wordsB, StringComparer.OrdinalIgnoreCase)
                            .Take(5).ToList();
                        result.Add(new Entanglement
                        {
                            PromptA = ids[i],
                            PromptB = ids[j],
                            Type = EntanglementType.SemanticOverlap,
                            Strength = Math.Round(jaccard * 100, 1),
                            Evidence = new List<string>
                            {
                                $"Jaccard similarity: {jaccard:F3}",
                                $"Shared keywords: {string.Join(", ", sharedWords)}"
                            }
                        });
                    }
                }
            }
            return result;
        }

        private static HashSet<string> TokenizeContent(string content)
        {
            var words = content.ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return words;
        }

        // ─── Engine 4: Behavioral Correlation ───────

        internal List<Entanglement> DetectBehavioralCorrelations()
        {
            var result = new List<Entanglement>();
            var promptIds = _outcomes.Select(o => o.PromptId).Distinct().ToList();

            for (int i = 0; i < promptIds.Count; i++)
            {
                for (int j = i + 1; j < promptIds.Count; j++)
                {
                    var outA = _outcomes.Where(o => o.PromptId == promptIds[i]).ToList();
                    var outB = _outcomes.Where(o => o.PromptId == promptIds[j]).ToList();

                    if (outA.Count < _config.MinOutcomesForCorrelation ||
                        outB.Count < _config.MinOutcomesForCorrelation)
                        continue;

                    // Align by taking the min count
                    int n = Math.Min(outA.Count, outB.Count);
                    var valA = outA.Take(n).Select(o => o.Success ? 1.0 : 0.0).ToList();
                    var valB = outB.Take(n).Select(o => o.Success ? 1.0 : 0.0).ToList();

                    double corr = PearsonCorrelation(valA, valB);
                    double absCorr = Math.Abs(corr);

                    if (absCorr >= _config.CorrelationThreshold)
                    {
                        string direction = corr > 0 ? "positive" : "negative";
                        result.Add(new Entanglement
                        {
                            PromptA = promptIds[i],
                            PromptB = promptIds[j],
                            Type = EntanglementType.BehavioralCorrelation,
                            Strength = Math.Round(absCorr * 100, 1),
                            Evidence = new List<string>
                            {
                                $"Pearson r = {corr:F3} ({direction} correlation)",
                                $"Based on {n} aligned outcomes"
                            }
                        });
                    }
                }
            }
            return result;
        }

        private static double PearsonCorrelation(List<double> x, List<double> y)
        {
            int n = x.Count;
            if (n < 2) return 0;

            double meanX = x.Average();
            double meanY = y.Average();
            double sumXY = 0, sumX2 = 0, sumY2 = 0;

            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - meanX;
                double dy = y[i] - meanY;
                sumXY += dx * dy;
                sumX2 += dx * dx;
                sumY2 += dy * dy;
            }

            double denom = Math.Sqrt(sumX2 * sumY2);
            return denom < 1e-10 ? 0 : sumXY / denom;
        }

        // ─── Engine 5: Cascade Risk Analyzer ────────

        /// <summary>Get cascade chains originating from a specific prompt.</summary>
        public List<EntanglementCascadeChain> GetCascadeChains(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var allEntanglements = DetectEntanglements();
            return BuildCascadeChains(promptId, allEntanglements);
        }

        internal List<EntanglementCascadeChain> BuildCascadeChains(string sourceId, List<Entanglement> entanglements)
        {
            // BFS to find all reachable prompts with depth tracking
            var adjacency = new Dictionary<string, List<(string neighbor, double strength)>>();
            foreach (var e in entanglements)
            {
                if (!adjacency.ContainsKey(e.PromptA))
                    adjacency[e.PromptA] = new();
                if (!adjacency.ContainsKey(e.PromptB))
                    adjacency[e.PromptB] = new();
                adjacency[e.PromptA].Add((e.PromptB, e.Strength));
                adjacency[e.PromptB].Add((e.PromptA, e.Strength));
            }

            if (!adjacency.ContainsKey(sourceId))
                return new List<EntanglementCascadeChain>();

            var chains = new List<EntanglementCascadeChain>();
            var visited = new HashSet<string> { sourceId };
            var queue = new Queue<(string id, List<string> path, double maxStr)>();

            foreach (var (neighbor, strength) in adjacency[sourceId])
            {
                if (!visited.Contains(neighbor))
                {
                    queue.Enqueue((neighbor, new List<string> { neighbor }, strength));
                    visited.Add(neighbor);
                }
            }

            while (queue.Count > 0)
            {
                var (current, path, maxStr) = queue.Dequeue();

                chains.Add(new EntanglementCascadeChain
                {
                    SourcePromptId = sourceId,
                    AffectedPromptIds = new List<string>(path),
                    ChainLength = path.Count,
                    MaxStrength = maxStr,
                    Description = $"{sourceId} → {string.Join(" → ", path)}"
                });

                if (path.Count >= _config.CascadeDepthLimit) continue;

                if (adjacency.ContainsKey(current))
                {
                    foreach (var (neighbor, strength) in adjacency[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            var newPath = new List<string>(path) { neighbor };
                            queue.Enqueue((neighbor, newPath, Math.Max(maxStr, strength)));
                        }
                    }
                }
            }

            return chains.OrderByDescending(c => c.MaxStrength).ToList();
        }

        // ─── Engine 6: Cluster Detector (Union-Find) ─

        /// <summary>Get entanglement clusters using union-find.</summary>
        public List<EntanglementCluster> GetClusters()
        {
            var entanglements = DetectEntanglements();
            return BuildClusters(entanglements);
        }

        internal List<EntanglementCluster> BuildClusters(List<Entanglement> entanglements)
        {
            if (entanglements.Count == 0) return new List<EntanglementCluster>();

            // Union-Find
            var parent = new Dictionary<string, string>();
            var rank = new Dictionary<string, int>();

            string Find(string x)
            {
                if (!parent.ContainsKey(x)) { parent[x] = x; rank[x] = 0; }
                if (parent[x] != x) parent[x] = Find(parent[x]);
                return parent[x];
            }

            void Union(string a, string b)
            {
                string ra = Find(a), rb = Find(b);
                if (ra == rb) return;
                if (rank[ra] < rank[rb]) parent[ra] = rb;
                else if (rank[ra] > rank[rb]) parent[rb] = ra;
                else { parent[rb] = ra; rank[ra]++; }
            }

            foreach (var e in entanglements)
            {
                Union(e.PromptA, e.PromptB);
            }

            // Group by root
            var groups = new Dictionary<string, List<string>>();
            foreach (var id in parent.Keys)
            {
                string root = Find(id);
                if (!groups.ContainsKey(root)) groups[root] = new();
                if (!groups[root].Contains(id)) groups[root].Add(id);
            }

            // Build clusters
            int clusterIdx = 0;
            var clusters = new List<EntanglementCluster>();
            foreach (var (root, members) in groups.OrderByDescending(g => g.Value.Count))
            {
                if (members.Count < 2) continue;
                if (clusters.Count >= _config.MaxClustersToReport) break;

                var clusterEntanglements = entanglements.Where(e =>
                    members.Contains(e.PromptA) && members.Contains(e.PromptB)).ToList();

                var dominantType = clusterEntanglements
                    .GroupBy(e => e.Type)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? EntanglementType.SharedVariable;

                double avgStrength = clusterEntanglements.Count > 0
                    ? clusterEntanglements.Average(e => e.Strength)
                    : 0;

                // Risk = f(size, avgStrength, max strength)
                double maxStr = clusterEntanglements.Count > 0
                    ? clusterEntanglements.Max(e => e.Strength)
                    : 0;
                double riskScore = Math.Min(100, avgStrength * 0.5 + maxStr * 0.3 + members.Count * 3);

                clusters.Add(new EntanglementCluster
                {
                    ClusterId = $"C{++clusterIdx:D3}",
                    PromptIds = members.OrderBy(m => m).ToList(),
                    DominantType = dominantType,
                    AvgStrength = Math.Round(avgStrength, 1),
                    RiskScore = Math.Round(riskScore, 1)
                });
            }

            return clusters;
        }

        // ─── Engine 7: Insight Generator ────────────

        internal List<string> GenerateInsights(List<Entanglement> entanglements, List<EntanglementCluster> clusters)
        {
            var insights = new List<string>();

            if (entanglements.Count == 0 && _prompts.Count > 0)
            {
                insights.Add("Fleet is fully decoupled — no entanglements detected between any prompts.");
                return insights;
            }

            // Hub detection
            var connectionCounts = new Dictionary<string, int>();
            foreach (var e in entanglements)
            {
                connectionCounts[e.PromptA] = connectionCounts.GetValueOrDefault(e.PromptA) + 1;
                connectionCounts[e.PromptB] = connectionCounts.GetValueOrDefault(e.PromptB) + 1;
            }

            var hubs = connectionCounts.Where(kv => kv.Value > 3)
                .OrderByDescending(kv => kv.Value).ToList();
            foreach (var hub in hubs.Take(3))
            {
                insights.Add($"Hub prompt '{hub.Key}' has {hub.Value} entanglements — changes here cascade widely.");
            }

            // Isolated prompts
            var entangledIds = entanglements.SelectMany(e => new[] { e.PromptA, e.PromptB }).ToHashSet();
            var isolated = _prompts.Keys.Where(id => !entangledIds.Contains(id)).ToList();
            if (isolated.Count > 0 && isolated.Count <= 5)
            {
                insights.Add($"Isolated prompts (no entanglements): {string.Join(", ", isolated)}.");
            }
            else if (isolated.Count > 5)
            {
                insights.Add($"{isolated.Count} prompts are fully isolated with no detected entanglements.");
            }

            // Critical entanglements
            var critical = entanglements.Where(e => e.Severity == EntanglementSeverity.Critical).ToList();
            if (critical.Count > 0)
            {
                insights.Add($"{critical.Count} critical-strength entanglement(s) detected — review these pairs urgently.");
            }

            // Cluster insights
            var riskyCluster = clusters.Where(c => c.RiskScore > 70).ToList();
            foreach (var c in riskyCluster.Take(3))
            {
                insights.Add($"Cluster {c.ClusterId} ({c.PromptIds.Count} prompts) has risk score {c.RiskScore:F0} — consider decoupling.");
            }

            // Type distribution
            var typeDist = entanglements.GroupBy(e => e.Type)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();
            if (typeDist.Count > 0)
            {
                insights.Add($"Entanglement type distribution — {string.Join(", ", typeDist)}.");
            }

            // Behavioral correlation warning
            var behavioral = entanglements.Where(e => e.Type == EntanglementType.BehavioralCorrelation).ToList();
            if (behavioral.Count > 0)
            {
                insights.Add($"{behavioral.Count} behavioral correlation(s) found — these prompts succeed/fail together.");
            }

            return insights;
        }

        // ─── Public API ─────────────────────────────

        /// <summary>Run all detection engines and return all entanglements.</summary>
        public List<Entanglement> DetectEntanglements()
        {
            var all = new List<Entanglement>();
            all.AddRange(DetectVariableEntanglements());
            all.AddRange(DetectTemplateDependencies());
            all.AddRange(DetectSemanticOverlap());
            all.AddRange(DetectBehavioralCorrelations());

            // Filter by minimum strength
            return all.Where(e => e.Strength >= _config.MinStrengthToReport)
                .OrderByDescending(e => e.Strength)
                .ToList();
        }

        /// <summary>Get all entanglements involving a specific prompt.</summary>
        public List<Entanglement> GetEntanglementsFor(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            return DetectEntanglements()
                .Where(e => e.PromptA == promptId || e.PromptB == promptId)
                .ToList();
        }

        /// <summary>Compute overall fleet health score 0–100.</summary>
        public double GetHealthScore()
        {
            var entanglements = DetectEntanglements();
            var clusters = BuildClusters(entanglements);
            return ComputeHealthScore(entanglements, clusters);
        }

        internal double ComputeHealthScore(List<Entanglement> entanglements, List<EntanglementCluster> clusters)
        {
            if (_prompts.Count == 0) return 100;

            double score = 100;

            // Deduct for high-strength entanglements
            foreach (var e in entanglements.Where(e => e.Strength > 60))
                score -= 2;

            // Deduct for large clusters
            foreach (var c in clusters.Where(c => c.PromptIds.Count > 3))
                score -= 5;

            // Deduct for hub prompts (>5 connections)
            var connectionCounts = new Dictionary<string, int>();
            foreach (var e in entanglements)
            {
                connectionCounts[e.PromptA] = connectionCounts.GetValueOrDefault(e.PromptA) + 1;
                connectionCounts[e.PromptB] = connectionCounts.GetValueOrDefault(e.PromptB) + 1;
            }
            foreach (var kv in connectionCounts.Where(kv => kv.Value > 5))
                score -= 3;

            return Math.Max(0, Math.Min(100, Math.Round(score, 1)));
        }

        /// <summary>Classify health score into a tier.</summary>
        public static EntanglementHealthTier ClassifyHealthTier(double score) =>
            score switch
            {
                >= 90 => EntanglementHealthTier.Decoupled,
                >= 70 => EntanglementHealthTier.Manageable,
                >= 50 => EntanglementHealthTier.Tangled,
                >= 20 => EntanglementHealthTier.Knotted,
                _ => EntanglementHealthTier.Spaghetti
            };

        /// <summary>Generate a comprehensive entanglement report.</summary>
        public EntanglementReport GenerateReport()
        {
            var entanglements = DetectEntanglements();
            var clusters = BuildClusters(entanglements);
            var healthScore = ComputeHealthScore(entanglements, clusters);
            var insights = GenerateInsights(entanglements, clusters);

            // Cascade chains for all registered prompts
            var cascadeChains = new List<EntanglementCascadeChain>();
            foreach (var id in _prompts.Keys)
            {
                var chains = BuildCascadeChains(id, entanglements);
                var longChains = chains.Where(c => c.ChainLength > 1).ToList();
                cascadeChains.AddRange(longChains);
            }

            // Deduplicate cascade chains (same set of affected prompts)
            var seenChains = new HashSet<string>();
            var uniqueChains = new List<EntanglementCascadeChain>();
            foreach (var c in cascadeChains.OrderByDescending(c => c.MaxStrength))
            {
                var key = $"{c.SourcePromptId}:{string.Join(",", c.AffectedPromptIds)}";
                if (seenChains.Add(key))
                    uniqueChains.Add(c);
            }

            return new EntanglementReport
            {
                TotalPrompts = _prompts.Count,
                TotalEntanglements = entanglements.Count,
                Clusters = clusters,
                StrongestPairs = entanglements.Take(10).ToList(),
                CascadeChains = uniqueChains.Take(20).ToList(),
                HealthScore = healthScore,
                HealthTier = ClassifyHealthTier(healthScore),
                Insights = insights
            };
        }

        // ─── Dashboard ──────────────────────────────

        /// <summary>Generate an interactive HTML dashboard.</summary>
        public string RenderDashboard()
        {
            var report = GenerateReport();
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>Prompt Entanglement Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("* { margin: 0; padding: 0; box-sizing: border-box; }");
            sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #0f0f23; color: #e0e0e0; padding: 20px; }");
            sb.AppendLine("h1 { color: #9b59b6; margin-bottom: 10px; }");
            sb.AppendLine("h2 { color: #8e44ad; margin: 20px 0 10px; border-bottom: 1px solid #333; padding-bottom: 5px; }");
            sb.AppendLine(".gauge { display: inline-flex; align-items: center; gap: 15px; background: #1a1a2e; padding: 15px 25px; border-radius: 12px; margin: 10px 0; }");
            sb.AppendLine(".score { font-size: 48px; font-weight: bold; }");
            sb.AppendLine(".tier { font-size: 18px; opacity: 0.8; }");
            sb.AppendLine(".grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 15px; }");
            sb.AppendLine(".card { background: #1a1a2e; padding: 15px; border-radius: 8px; border: 1px solid #333; }");
            sb.AppendLine(".card h3 { color: #bb86fc; margin-bottom: 8px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 10px 0; }");
            sb.AppendLine("th, td { padding: 8px; text-align: left; border-bottom: 1px solid #333; }");
            sb.AppendLine("th { color: #bb86fc; }");
            sb.AppendLine(".badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 12px; }");
            sb.AppendLine(".critical { background: #e74c3c; color: white; }");
            sb.AppendLine(".high { background: #e67e22; color: white; }");
            sb.AppendLine(".moderate { background: #f39c12; color: black; }");
            sb.AppendLine(".low { background: #27ae60; color: white; }");
            sb.AppendLine(".insight { background: #16213e; padding: 10px; border-left: 3px solid #9b59b6; margin: 5px 0; border-radius: 4px; }");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<h1>🔮 Prompt Entanglement Dashboard</h1>");
            sb.AppendLine($"<p>{report.TotalPrompts} prompts · {report.TotalEntanglements} entanglements · {report.Clusters.Count} clusters</p>");

            // Health gauge
            string gaugeColor = report.HealthScore >= 70 ? "#27ae60" : report.HealthScore >= 50 ? "#f39c12" : "#e74c3c";
            sb.AppendLine($"<div class=\"gauge\"><span class=\"score\" style=\"color:{gaugeColor}\">{report.HealthScore:F0}</span>");
            sb.AppendLine($"<div><div class=\"tier\">{report.HealthTier}</div><div style=\"opacity:0.6\">Health Score</div></div></div>");

            // Strongest pairs
            if (report.StrongestPairs.Count > 0)
            {
                sb.AppendLine("<h2>🔗 Strongest Entanglements</h2>");
                sb.AppendLine("<table><tr><th>Pair</th><th>Type</th><th>Strength</th><th>Severity</th></tr>");
                foreach (var e in report.StrongestPairs.Take(10))
                {
                    string sevClass = e.Severity.ToString().ToLower();
                    sb.AppendLine($"<tr><td>{Esc(e.PromptA)} ↔ {Esc(e.PromptB)}</td><td>{e.Type}</td><td>{e.Strength:F1}</td><td><span class=\"badge {sevClass}\">{e.Severity}</span></td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // Clusters
            if (report.Clusters.Count > 0)
            {
                sb.AppendLine("<h2>🧩 Entanglement Clusters</h2>");
                sb.AppendLine("<div class=\"grid\">");
                foreach (var c in report.Clusters)
                {
                    sb.AppendLine($"<div class=\"card\"><h3>{Esc(c.ClusterId)} — {c.PromptIds.Count} prompts</h3>");
                    sb.AppendLine($"<p>Dominant: {c.DominantType} · Avg Strength: {c.AvgStrength:F1} · Risk: {c.RiskScore:F0}</p>");
                    sb.AppendLine($"<p style=\"opacity:0.7;font-size:13px\">{string.Join(", ", c.PromptIds.Select(Esc))}</p></div>");
                }
                sb.AppendLine("</div>");
            }

            // Cascade chains
            if (report.CascadeChains.Count > 0)
            {
                sb.AppendLine("<h2>⚡ Cascade Chains</h2>");
                sb.AppendLine("<table><tr><th>Chain</th><th>Length</th><th>Max Strength</th></tr>");
                foreach (var c in report.CascadeChains.Take(10))
                {
                    sb.AppendLine($"<tr><td>{Esc(c.Description)}</td><td>{c.ChainLength}</td><td>{c.MaxStrength:F1}</td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // Insights
            if (report.Insights.Count > 0)
            {
                sb.AppendLine("<h2>💡 Autonomous Insights</h2>");
                foreach (var insight in report.Insights)
                {
                    sb.AppendLine($"<div class=\"insight\">{Esc(insight)}</div>");
                }
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
