using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prompt
{
    /// <summary>
    /// Describes the relationship between a prompt and its ancestor(s).
    /// </summary>
    public enum GenealogyRelation
    {
        /// <summary>Direct parent relationship.</summary>
        Parent,
        /// <summary>Direct child relationship.</summary>
        Child,
        /// <summary>Exact copy of another prompt.</summary>
        Clone,
        /// <summary>Modified version of a single parent.</summary>
        Mutation,
        /// <summary>Created by combining two parent prompts.</summary>
        Crossover,
        /// <summary>Merged from multiple sources.</summary>
        Merge,
        /// <summary>Divergent copy intended as a separate lineage.</summary>
        Fork
    }

    /// <summary>
    /// Represents a single prompt in the genealogy tree.
    /// </summary>
    public sealed record PromptAncestor(
        string Id,
        string Text,
        DateTime CreatedAt,
        string? ParentId,
        string? SecondParentId,
        GenealogyRelation Relation,
        Dictionary<string, string> Metadata);

    /// <summary>
    /// A node in a prompt lineage tree with children and depth info.
    /// </summary>
    public sealed class LineageNode
    {
        /// <summary>The ancestor data for this node.</summary>
        public PromptAncestor Ancestor { get; }

        /// <summary>Child nodes in the lineage.</summary>
        public List<LineageNode> Children { get; } = new();

        /// <summary>Depth from the subtree root (0-based).</summary>
        public int Depth { get; set; }

        /// <summary>Generation number (0 = root ancestor).</summary>
        public int Generation { get; set; }

        /// <summary>Creates a new lineage node.</summary>
        public LineageNode(PromptAncestor ancestor) => Ancestor = ancestor;

        /// <summary>Total number of descendants (recursive).</summary>
        public int DescendantCount() => Children.Sum(c => 1 + c.DescendantCount());

        /// <summary>Maximum depth of the subtree rooted at this node.</summary>
        public int MaxDepth() => Children.Count == 0 ? 0 : 1 + Children.Max(c => c.MaxDepth());
    }

    /// <summary>
    /// An alert raised by diversity or lineage analysis.
    /// </summary>
    public sealed record DiversityAlert(
        string AlertType,
        string Message,
        double Severity,
        DateTime DetectedAt,
        List<string> AffectedIds);

    /// <summary>
    /// Comprehensive genealogy analysis report.
    /// </summary>
    public sealed class GenealogyReport
    {
        /// <summary>Total registered prompts.</summary>
        public int TotalPrompts { get; init; }
        /// <summary>Number of distinct generations.</summary>
        public int TotalGenerations { get; init; }
        /// <summary>Longest ancestor chain length.</summary>
        public int LongestLineage { get; init; }
        /// <summary>Average number of children per non-leaf node.</summary>
        public double AverageBranchingFactor { get; init; }
        /// <summary>Portfolio diversity score (0–1).</summary>
        public double DiversityScore { get; init; }
        /// <summary>All detected alerts.</summary>
        public List<DiversityAlert> Alerts { get; init; } = new();
        /// <summary>Count of each relation type.</summary>
        public Dictionary<string, int> RelationCounts { get; init; } = new();
        /// <summary>Top 5 ancestors by descendant count.</summary>
        public List<string> MostProlificAncestors { get; init; } = new();
        /// <summary>When this report was generated.</summary>
        public DateTime GeneratedAt { get; init; }
    }

    /// <summary>
    /// Autonomous prompt lineage and ancestry tracking system.
    /// Builds family trees, tracks evolution, detects convergent evolution,
    /// and alerts on diversity loss in a prompt portfolio.
    /// </summary>
    public sealed class PromptGenealogyTracker
    {
        private readonly Dictionary<string, PromptAncestor> _ancestors = new();
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Number of registered prompts.</summary>
        public int Count => _ancestors.Count;

        /// <summary>
        /// Register a prompt in the genealogy.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when id already exists or parentId is not found.</exception>
        public void Register(string id, string text, string? parentId = null,
            GenealogyRelation relation = GenealogyRelation.Child,
            string? secondParentId = null, Dictionary<string, string>? metadata = null)
        {
            if (_ancestors.ContainsKey(id))
                throw new ArgumentException($"Prompt '{id}' is already registered.", nameof(id));
            if (parentId != null && !_ancestors.ContainsKey(parentId))
                throw new ArgumentException($"Parent '{parentId}' not found.", nameof(parentId));
            if (secondParentId != null && !_ancestors.ContainsKey(secondParentId))
                throw new ArgumentException($"Second parent '{secondParentId}' not found.", nameof(secondParentId));

            _ancestors[id] = new PromptAncestor(id, text, DateTime.UtcNow, parentId, secondParentId,
                parentId == null ? GenealogyRelation.Parent : relation, metadata ?? new());
        }

        /// <summary>Register a root prompt with no parent.</summary>
        public void RegisterRoot(string id, string text, Dictionary<string, string>? metadata = null)
            => Register(id, text, null, GenealogyRelation.Parent, null, metadata);

        /// <summary>Register a mutation of an existing prompt.</summary>
        public void RegisterMutation(string id, string text, string parentId, Dictionary<string, string>? metadata = null)
            => Register(id, text, parentId, GenealogyRelation.Mutation, null, metadata);

        /// <summary>Register a crossover of two parent prompts.</summary>
        public void RegisterCrossover(string id, string text, string parent1Id, string parent2Id, Dictionary<string, string>? metadata = null)
            => Register(id, text, parent1Id, GenealogyRelation.Crossover, parent2Id, metadata);

        /// <summary>Build the lineage subtree from a given root.</summary>
        public LineageNode BuildTree(string rootId)
        {
            if (!_ancestors.TryGetValue(rootId, out var root))
                throw new ArgumentException($"Prompt '{rootId}' not found.", nameof(rootId));

            var node = new LineageNode(root) { Depth = 0, Generation = GetGeneration(rootId) };
            BuildTreeRecursive(node, rootId);
            return node;
        }

        private void BuildTreeRecursive(LineageNode parent, string parentId)
        {
            foreach (var (id, a) in _ancestors)
            {
                if (a.ParentId == parentId || a.SecondParentId == parentId)
                {
                    // Avoid self-reference and only primary parent drives tree structure
                    if (a.ParentId != parentId) continue;
                    var child = new LineageNode(a) { Depth = parent.Depth + 1, Generation = parent.Generation + 1 };
                    parent.Children.Add(child);
                    BuildTreeRecursive(child, id);
                }
            }
        }

        /// <summary>Get the ancestry chain from a prompt up to its root.</summary>
        public List<PromptAncestor> GetAncestry(string id)
        {
            var chain = new List<PromptAncestor>();
            var current = id;
            var visited = new HashSet<string>();
            while (current != null && _ancestors.TryGetValue(current, out var ancestor) && visited.Add(current))
            {
                chain.Add(ancestor);
                current = ancestor.ParentId;
            }
            return chain;
        }

        /// <summary>Get all siblings (same parent) of a prompt.</summary>
        public List<PromptAncestor> GetSiblings(string id)
        {
            if (!_ancestors.TryGetValue(id, out var prompt)) return new();
            if (prompt.ParentId == null) return new();
            return _ancestors.Values
                .Where(a => a.ParentId == prompt.ParentId && a.Id != id)
                .ToList();
        }

        /// <summary>Compute Jaccard similarity on word trigrams between two texts.</summary>
        public double ComputeSimilarity(string text1, string text2)
        {
            var t1 = GetTrigrams(text1);
            var t2 = GetTrigrams(text2);
            if (t1.Count == 0 && t2.Count == 0) return 1.0;
            if (t1.Count == 0 || t2.Count == 0) return 0.0;
            var intersection = t1.Intersect(t2).Count();
            var union = t1.Union(t2).Count();
            return union == 0 ? 0.0 : (double)intersection / union;
        }

        private static HashSet<string> GetTrigrams(string text)
        {
            var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var trigrams = new HashSet<string>();
            for (int i = 0; i <= words.Length - 3; i++)
                trigrams.Add($"{words[i].ToLowerInvariant()} {words[i + 1].ToLowerInvariant()} {words[i + 2].ToLowerInvariant()}");
            return trigrams;
        }

        /// <summary>Find the root ancestor of a given prompt.</summary>
        private string FindRoot(string id)
        {
            var current = id;
            var visited = new HashSet<string>();
            while (_ancestors.TryGetValue(current, out var a) && a.ParentId != null && visited.Add(current))
                current = a.ParentId;
            return current;
        }

        /// <summary>
        /// Detect convergent evolution: unrelated prompts (different root lineages)
        /// that have become similar above the threshold.
        /// </summary>
        public List<DiversityAlert> DetectConvergentEvolution(double threshold = 0.6)
        {
            var alerts = new List<DiversityAlert>();
            var prompts = _ancestors.Values.ToList();
            for (int i = 0; i < prompts.Count; i++)
            {
                for (int j = i + 1; j < prompts.Count; j++)
                {
                    var root1 = FindRoot(prompts[i].Id);
                    var root2 = FindRoot(prompts[j].Id);
                    if (root1 == root2) continue;

                    var sim = ComputeSimilarity(prompts[i].Text, prompts[j].Text);
                    if (sim >= threshold)
                    {
                        alerts.Add(new DiversityAlert(
                            "ConvergentEvolution",
                            $"Prompts '{prompts[i].Id}' and '{prompts[j].Id}' from different lineages have {sim:P0} similarity",
                            Math.Min(1.0, sim),
                            DateTime.UtcNow,
                            new List<string> { prompts[i].Id, prompts[j].Id }));
                    }
                }
            }
            return alerts;
        }

        /// <summary>Compute overall portfolio diversity (average pairwise dissimilarity).</summary>
        public double ComputeDiversityScore()
        {
            var prompts = _ancestors.Values.ToList();
            if (prompts.Count < 2) return 1.0;
            double totalSim = 0;
            int pairs = 0;
            for (int i = 0; i < prompts.Count; i++)
            {
                for (int j = i + 1; j < prompts.Count; j++)
                {
                    totalSim += ComputeSimilarity(prompts[i].Text, prompts[j].Text);
                    pairs++;
                }
            }
            return 1.0 - (totalSim / pairs);
        }

        /// <summary>
        /// Detect diversity loss when overall or per-generation diversity drops below threshold.
        /// </summary>
        public List<DiversityAlert> DetectDiversityLoss(double minDiversity = 0.3)
        {
            var alerts = new List<DiversityAlert>();
            var overall = ComputeDiversityScore();
            if (overall < minDiversity)
            {
                alerts.Add(new DiversityAlert(
                    "DiversityLoss",
                    $"Overall diversity {overall:F2} is below threshold {minDiversity:F2}",
                    Math.Min(1.0, (minDiversity - overall) / minDiversity),
                    DateTime.UtcNow,
                    _ancestors.Keys.ToList()));
            }

            // Per-generation analysis
            var byGen = _ancestors.Values.GroupBy(a => GetGeneration(a.Id));
            foreach (var gen in byGen)
            {
                var genList = gen.ToList();
                if (genList.Count < 2) continue;
                double sim = 0; int p = 0;
                for (int i = 0; i < genList.Count; i++)
                    for (int j = i + 1; j < genList.Count; j++)
                    { sim += ComputeSimilarity(genList[i].Text, genList[j].Text); p++; }
                var genDiv = 1.0 - (sim / p);
                if (genDiv < minDiversity)
                {
                    alerts.Add(new DiversityAlert(
                        "MonocultureWarning",
                        $"Generation {gen.Key} diversity {genDiv:F2} is below threshold {minDiversity:F2}",
                        Math.Min(1.0, (minDiversity - genDiv) / minDiversity),
                        DateTime.UtcNow,
                        genList.Select(a => a.Id).ToList()));
                }
            }
            return alerts;
        }

        /// <summary>
        /// Detect inbreeding risk: crossover prompts whose parents share a common
        /// ancestor within 2 generations.
        /// </summary>
        public List<DiversityAlert> DetectInbreedingRisk()
        {
            var alerts = new List<DiversityAlert>();
            foreach (var a in _ancestors.Values.Where(a => a.Relation == GenealogyRelation.Crossover && a.SecondParentId != null))
            {
                var lineage1 = GetAncestry(a.ParentId!).Take(3).Select(x => x.Id).ToHashSet();
                var lineage2 = GetAncestry(a.SecondParentId!).Take(3).Select(x => x.Id).ToHashSet();
                var shared = lineage1.Intersect(lineage2).ToList();
                if (shared.Count > 0)
                {
                    alerts.Add(new DiversityAlert(
                        "InbreedingRisk",
                        $"Crossover '{a.Id}' parents share ancestor(s): {string.Join(", ", shared)}",
                        Math.Min(1.0, shared.Count * 0.4),
                        DateTime.UtcNow,
                        new List<string> { a.Id, a.ParentId!, a.SecondParentId! }));
                }
            }
            return alerts;
        }

        /// <summary>Generate a comprehensive genealogy report.</summary>
        public GenealogyReport GenerateReport()
        {
            var allAlerts = new List<DiversityAlert>();
            allAlerts.AddRange(DetectConvergentEvolution());
            allAlerts.AddRange(DetectDiversityLoss());
            allAlerts.AddRange(DetectInbreedingRisk());

            // Check extinction risk (roots with no descendants)
            foreach (var root in GetAllRoots())
            {
                var hasChildren = _ancestors.Values.Any(a => a.ParentId == root.Id);
                if (!hasChildren && _ancestors.Count > 1)
                {
                    allAlerts.Add(new DiversityAlert(
                        "ExtinctionRisk",
                        $"Root lineage '{root.Id}' has no descendants",
                        0.5,
                        DateTime.UtcNow,
                        new List<string> { root.Id }));
                }
            }

            var relationCounts = _ancestors.Values
                .GroupBy(a => a.Relation.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            // Branching factor
            var childCounts = _ancestors.Keys
                .Select(id => _ancestors.Values.Count(a => a.ParentId == id))
                .Where(c => c > 0)
                .ToList();
            var avgBranching = childCounts.Count > 0 ? childCounts.Average() : 0.0;

            // Most prolific ancestors
            var prolific = _ancestors.Keys
                .Select(id => (id, count: CountDescendants(id)))
                .OrderByDescending(x => x.count)
                .Take(5)
                .Select(x => x.id)
                .ToList();

            var generations = _ancestors.Keys.Select(GetGeneration).Distinct().Count();
            var longestLineage = _ancestors.Keys.Select(id => GetAncestry(id).Count).DefaultIfEmpty(0).Max();

            return new GenealogyReport
            {
                TotalPrompts = _ancestors.Count,
                TotalGenerations = generations,
                LongestLineage = longestLineage,
                AverageBranchingFactor = Math.Round(avgBranching, 2),
                DiversityScore = Math.Round(ComputeDiversityScore(), 4),
                Alerts = allAlerts,
                RelationCounts = relationCounts,
                MostProlificAncestors = prolific,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private int CountDescendants(string id)
        {
            var children = _ancestors.Values.Where(a => a.ParentId == id).ToList();
            return children.Count + children.Sum(c => CountDescendants(c.Id));
        }

        /// <summary>Get all root prompts (no parent).</summary>
        public List<PromptAncestor> GetAllRoots()
            => _ancestors.Values.Where(a => a.ParentId == null).ToList();

        /// <summary>Compute the generation number of a prompt (root = 0).</summary>
        public int GetGeneration(string id)
        {
            int gen = 0;
            var current = id;
            var visited = new HashSet<string>();
            while (_ancestors.TryGetValue(current, out var a) && a.ParentId != null && visited.Add(current))
            { gen++; current = a.ParentId; }
            return gen;
        }

        /// <summary>Serialize all ancestors as a JSON array.</summary>
        public string ExportJson()
            => JsonSerializer.Serialize(_ancestors.Values.ToList(), s_jsonOptions);

        /// <summary>Export the genealogy as a Graphviz DOT graph.</summary>
        public string ExportDot()
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph PromptGenealogy {");
            sb.AppendLine("  rankdir=TB;");
            sb.AppendLine("  node [shape=box, style=filled, fontsize=10];");

            var roots = new HashSet<string>(GetAllRoots().Select(r => r.Id));
            var leaves = new HashSet<string>(_ancestors.Keys.Where(id => !_ancestors.Values.Any(a => a.ParentId == id)));

            foreach (var (id, a) in _ancestors)
            {
                var label = a.Text.Length > 30 ? a.Text[..30] + "..." : a.Text;
                label = label.Replace("\"", "\\\"").Replace("\n", " ");
                var color = roots.Contains(id) ? "lightblue" : leaves.Contains(id) ? "lightyellow" : "lightgray";
                sb.AppendLine($"  \"{id}\" [label=\"{id}\\n{label}\", fillcolor={color}];");
            }

            foreach (var a in _ancestors.Values)
            {
                if (a.ParentId != null)
                    sb.AppendLine($"  \"{a.ParentId}\" -> \"{a.Id}\" [label=\"{a.Relation}\"];");
                if (a.SecondParentId != null)
                    sb.AppendLine($"  \"{a.SecondParentId}\" -> \"{a.Id}\" [label=\"{a.Relation}\", style=dashed];");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Export an ASCII family tree with report summary in Markdown.</summary>
        public string ExportMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Prompt Genealogy Report");
            sb.AppendLine();

            var report = GenerateReport();
            sb.AppendLine($"- **Total Prompts:** {report.TotalPrompts}");
            sb.AppendLine($"- **Generations:** {report.TotalGenerations}");
            sb.AppendLine($"- **Longest Lineage:** {report.LongestLineage}");
            sb.AppendLine($"- **Avg Branching Factor:** {report.AverageBranchingFactor:F2}");
            sb.AppendLine($"- **Diversity Score:** {report.DiversityScore:F4}");
            sb.AppendLine();

            if (report.Alerts.Count > 0)
            {
                sb.AppendLine("## Alerts");
                foreach (var alert in report.Alerts)
                    sb.AppendLine($"- ⚠️ [{alert.AlertType}] {alert.Message} (severity: {alert.Severity:F2})");
                sb.AppendLine();
            }

            sb.AppendLine("## Family Trees");
            foreach (var root in GetAllRoots())
            {
                var tree = BuildTree(root.Id);
                RenderTree(sb, tree, "");
                sb.AppendLine();
            }

            if (report.MostProlificAncestors.Count > 0)
            {
                sb.AppendLine("## Most Prolific Ancestors");
                foreach (var id in report.MostProlificAncestors)
                    sb.AppendLine($"- {id} ({CountDescendants(id)} descendants)");
            }

            return sb.ToString();
        }

        private void RenderTree(StringBuilder sb, LineageNode node, string prefix)
        {
            var label = node.Ancestor.Text.Length > 40 ? node.Ancestor.Text[..40] + "..." : node.Ancestor.Text;
            sb.AppendLine($"{prefix}├── {node.Ancestor.Id} [{node.Ancestor.Relation}] \"{label}\"");
            for (int i = 0; i < node.Children.Count; i++)
                RenderTree(sb, node.Children[i], prefix + "│   ");
        }
    }
}
