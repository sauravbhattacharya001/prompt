namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Represents a node in a prompt dependency graph — a prompt template
    /// or processing step that may depend on outputs from other nodes.
    /// </summary>
    public sealed class PromptNode
    {
        /// <summary>Unique identifier for this node.</summary>
        public string Id { get; }

        /// <summary>Optional human-readable label.</summary>
        public string? Label { get; set; }

        /// <summary>Estimated execution cost/time (arbitrary units).</summary>
        public double Weight { get; set; } = 1.0;

        /// <summary>IDs of nodes this node depends on (inputs).</summary>
        public IReadOnlyList<string> Dependencies => _dependencies.AsReadOnly();

        /// <summary>Optional metadata dictionary.</summary>
        public Dictionary<string, string> Metadata { get; } = new();

        private readonly List<string> _dependencies = new();

        /// <summary>Create a prompt node with the given ID.</summary>
        public PromptNode(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Node id cannot be null or whitespace.", nameof(id));
            Id = id;
        }

        /// <summary>Add a dependency on another node.</summary>
        public PromptNode DependsOn(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Dependency id cannot be null or whitespace.", nameof(nodeId));
            if (nodeId == Id)
                throw new ArgumentException("A node cannot depend on itself.", nameof(nodeId));
            if (!_dependencies.Contains(nodeId))
                _dependencies.Add(nodeId);
            return this;
        }

        /// <summary>Remove a dependency.</summary>
        public bool RemoveDependency(string nodeId)
            => _dependencies.Remove(nodeId);
    }

    /// <summary>
    /// Represents a cycle detected in the dependency graph.
    /// </summary>
    public sealed class DependencyCycle
    {
        /// <summary>Ordered list of node IDs forming the cycle.</summary>
        public IReadOnlyList<string> Path { get; }

        /// <summary>Total weight of all nodes in the cycle.</summary>
        public double TotalWeight { get; }

        public DependencyCycle(IReadOnlyList<string> path, double totalWeight)
        {
            Path = path;
            TotalWeight = totalWeight;
        }

        public override string ToString()
            => string.Join(" → ", Path) + " → " + Path[0];
    }

    /// <summary>
    /// Result of a critical path analysis.
    /// </summary>
    public sealed class CriticalPathResult
    {
        /// <summary>Ordered node IDs on the critical (longest) path.</summary>
        public IReadOnlyList<string> Path { get; }

        /// <summary>Total weight along the critical path.</summary>
        public double TotalWeight { get; }

        /// <summary>Per-node earliest start times.</summary>
        public IReadOnlyDictionary<string, double> EarliestStart { get; }

        /// <summary>Per-node latest start times.</summary>
        public IReadOnlyDictionary<string, double> LatestStart { get; }

        /// <summary>Per-node slack (latest - earliest start). Zero = critical.</summary>
        public IReadOnlyDictionary<string, double> Slack { get; }

        public CriticalPathResult(
            IReadOnlyList<string> path,
            double totalWeight,
            IReadOnlyDictionary<string, double> earliestStart,
            IReadOnlyDictionary<string, double> latestStart,
            IReadOnlyDictionary<string, double> slack)
        {
            Path = path;
            TotalWeight = totalWeight;
            EarliestStart = earliestStart;
            LatestStart = latestStart;
            Slack = slack;
        }
    }

    /// <summary>
    /// Result of graph analysis.
    /// </summary>
    public sealed class GraphAnalysis
    {
        /// <summary>Total number of nodes.</summary>
        public int NodeCount { get; init; }

        /// <summary>Total number of edges (dependencies).</summary>
        public int EdgeCount { get; init; }

        /// <summary>Nodes with no dependencies (entry points).</summary>
        public IReadOnlyList<string> RootNodes { get; init; } = Array.Empty<string>();

        /// <summary>Nodes with no dependents (final outputs).</summary>
        public IReadOnlyList<string> LeafNodes { get; init; } = Array.Empty<string>();

        /// <summary>Maximum depth from any root to any leaf.</summary>
        public int MaxDepth { get; init; }

        /// <summary>Average dependencies per node.</summary>
        public double AverageFanIn { get; init; }

        /// <summary>Average dependents per node.</summary>
        public double AverageFanOut { get; init; }

        /// <summary>Nodes that are single points of failure (many dependents).</summary>
        public IReadOnlyList<string> Bottlenecks { get; init; } = Array.Empty<string>();

        /// <summary>Execution layers (parallelizable groups).</summary>
        public IReadOnlyList<IReadOnlyList<string>> ExecutionLayers { get; init; } = Array.Empty<IReadOnlyList<string>>();

        /// <summary>Critical path result (null if graph has cycles).</summary>
        public CriticalPathResult? CriticalPath { get; init; }

        /// <summary>Detected cycles (empty if DAG).</summary>
        public IReadOnlyList<DependencyCycle> Cycles { get; init; } = Array.Empty<DependencyCycle>();

        /// <summary>Whether the graph is a valid DAG (no cycles).</summary>
        public bool IsDAG => Cycles.Count == 0;
    }

    /// <summary>
    /// Builds and analyzes prompt dependency graphs. Detects cycles,
    /// computes execution order, critical paths, parallelization layers,
    /// and bottlenecks. Exports to DOT, JSON, and Mermaid formats.
    /// </summary>
    /// <example>
    /// <code>
    /// var graph = new PromptDependencyGraph()
    ///     .AddNode("context", label: "Gather Context")
    ///     .AddNode("draft", label: "Draft Response")
    ///     .AddNode("review", label: "Review &amp; Edit")
    ///     .AddEdge("draft", "context")  // draft depends on context
    ///     .AddEdge("review", "draft");  // review depends on draft
    ///
    /// var analysis = graph.Analyze();
    /// // analysis.RootNodes = ["context"]
    /// // analysis.LeafNodes = ["review"]
    /// // analysis.ExecutionLayers = [["context"], ["draft"], ["review"]]
    /// // analysis.IsDAG = true
    ///
    /// string dot = graph.ToDot();     // Graphviz DOT
    /// string mermaid = graph.ToMermaid(); // Mermaid diagram
    /// </code>
    /// </example>
    public sealed class PromptDependencyGraph
    {
        private readonly Dictionary<string, PromptNode> _nodes = new();

        /// <summary>All nodes in the graph.</summary>
        public IReadOnlyDictionary<string, PromptNode> Nodes => _nodes;

        /// <summary>Number of nodes.</summary>
        public int Count => _nodes.Count;

        /// <summary>Add a node to the graph.</summary>
        public PromptDependencyGraph AddNode(string id, string? label = null, double weight = 1.0)
        {
            if (_nodes.ContainsKey(id))
                throw new ArgumentException($"Node '{id}' already exists.", nameof(id));
            var node = new PromptNode(id) { Label = label, Weight = weight };
            _nodes[id] = node;
            return this;
        }

        /// <summary>Add a node object directly.</summary>
        public PromptDependencyGraph AddNode(PromptNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_nodes.ContainsKey(node.Id))
                throw new ArgumentException($"Node '{node.Id}' already exists.", nameof(node));
            _nodes[node.Id] = node;
            return this;
        }

        /// <summary>Remove a node and all edges referencing it.</summary>
        public bool RemoveNode(string id)
        {
            if (!_nodes.Remove(id)) return false;
            foreach (var node in _nodes.Values)
                node.RemoveDependency(id);
            return true;
        }

        /// <summary>Add a dependency edge: <paramref name="fromId"/> depends on <paramref name="toId"/>.</summary>
        public PromptDependencyGraph AddEdge(string fromId, string toId)
        {
            if (!_nodes.ContainsKey(fromId))
                throw new ArgumentException($"Node '{fromId}' not found.", nameof(fromId));
            if (!_nodes.ContainsKey(toId))
                throw new ArgumentException($"Node '{toId}' not found.", nameof(toId));
            _nodes[fromId].DependsOn(toId);
            return this;
        }

        /// <summary>Remove a dependency edge.</summary>
        public bool RemoveEdge(string fromId, string toId)
        {
            if (!_nodes.TryGetValue(fromId, out var node)) return false;
            return node.RemoveDependency(toId);
        }

        /// <summary>Get all nodes that directly depend on the given node.</summary>
        public IReadOnlyList<string> GetDependents(string nodeId)
        {
            return _nodes.Values
                .Where(n => n.Dependencies.Contains(nodeId))
                .Select(n => n.Id)
                .ToList();
        }

        /// <summary>Get transitive dependencies (all ancestors).</summary>
        public IReadOnlySet<string> GetTransitiveDependencies(string nodeId)
        {
            var result = new HashSet<string>();
            var stack = new Stack<string>();
            if (!_nodes.TryGetValue(nodeId, out var node)) return result;
            foreach (var dep in node.Dependencies) stack.Push(dep);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!result.Add(current)) continue;
                if (_nodes.TryGetValue(current, out var n))
                    foreach (var dep in n.Dependencies)
                        stack.Push(dep);
            }
            return result;
        }

        /// <summary>Get transitive dependents (all descendants).</summary>
        public IReadOnlySet<string> GetTransitiveDependents(string nodeId)
        {
            var result = new HashSet<string>();
            var stack = new Stack<string>();
            foreach (var dep in GetDependents(nodeId)) stack.Push(dep);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!result.Add(current)) continue;
                foreach (var dep in GetDependents(current))
                    stack.Push(dep);
            }
            return result;
        }

        /// <summary>Detect all cycles in the graph.</summary>
        public IReadOnlyList<DependencyCycle> DetectCycles()
        {
            var cycles = new List<DependencyCycle>();
            var visited = new HashSet<string>();
            var onStack = new HashSet<string>();
            var path = new List<string>();

            foreach (var id in _nodes.Keys)
            {
                if (!visited.Contains(id))
                    DfsCycles(id, visited, onStack, path, cycles);
            }
            return cycles;
        }

        private void DfsCycles(string nodeId, HashSet<string> visited,
            HashSet<string> onStack, List<string> path, List<DependencyCycle> cycles)
        {
            visited.Add(nodeId);
            onStack.Add(nodeId);
            path.Add(nodeId);

            if (_nodes.TryGetValue(nodeId, out var node))
            {
                foreach (var dep in node.Dependencies)
                {
                    if (!_nodes.ContainsKey(dep)) continue;
                    if (onStack.Contains(dep))
                    {
                        int start = path.IndexOf(dep);
                        var cyclePath = path.Skip(start).ToList();
                        double weight = cyclePath.Sum(id =>
                            _nodes.TryGetValue(id, out var n) ? n.Weight : 0);
                        cycles.Add(new DependencyCycle(cyclePath, weight));
                    }
                    else if (!visited.Contains(dep))
                    {
                        DfsCycles(dep, visited, onStack, path, cycles);
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            onStack.Remove(nodeId);
        }

        /// <summary>Topological sort (Kahn's algorithm). Throws if cycles exist.</summary>
        public IReadOnlyList<string> TopologicalSort()
        {
            var inDegree = new Dictionary<string, int>();
            foreach (var id in _nodes.Keys) inDegree[id] = 0;
            foreach (var node in _nodes.Values)
                foreach (var dep in node.Dependencies)
                    if (_nodes.ContainsKey(dep))
                        if (inDegree.ContainsKey(dep)) { } // dep has dependents; track fromId
            // Recompute: inDegree[X] = number of dependencies X has
            // Actually for execution order: inDegree = number of unresolved deps
            foreach (var id in _nodes.Keys) inDegree[id] = 0;
            foreach (var node in _nodes.Values)
                foreach (var dep in node.Dependencies)
                    if (_nodes.ContainsKey(dep))
                        ; // edge: node depends on dep → dep must come first
            // Use reverse: inDegree[node] = count of its dependencies
            foreach (var node in _nodes.Values)
                inDegree[node.Id] = node.Dependencies.Count(d => _nodes.ContainsKey(d));

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var result = new List<string>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                foreach (var dependent in GetDependents(current))
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        queue.Enqueue(dependent);
                }
            }

            if (result.Count != _nodes.Count)
                throw new InvalidOperationException("Graph contains cycles — topological sort not possible.");

            return result;
        }

        /// <summary>Compute execution layers (parallelizable groups).</summary>
        public IReadOnlyList<IReadOnlyList<string>> ComputeExecutionLayers()
        {
            var layers = new List<IReadOnlyList<string>>();
            var nodeLayer = new Dictionary<string, int>();

            // Assign each node to the layer after all its dependencies
            var sorted = TopologicalSort();
            foreach (var id in sorted)
            {
                int layer = 0;
                if (_nodes.TryGetValue(id, out var node))
                {
                    foreach (var dep in node.Dependencies)
                        if (nodeLayer.TryGetValue(dep, out int depLayer))
                            layer = Math.Max(layer, depLayer + 1);
                }
                nodeLayer[id] = layer;
            }

            int maxLayer = nodeLayer.Count > 0 ? nodeLayer.Values.Max() : -1;
            for (int i = 0; i <= maxLayer; i++)
                layers.Add(nodeLayer.Where(kv => kv.Value == i).Select(kv => kv.Key).ToList());

            return layers;
        }

        /// <summary>Compute the critical path (longest weighted path through the DAG).</summary>
        public CriticalPathResult ComputeCriticalPath()
        {
            var sorted = TopologicalSort();
            var earliest = new Dictionary<string, double>();
            var predecessor = new Dictionary<string, string?>();

            foreach (var id in sorted)
            {
                double maxPre = 0;
                string? pred = null;
                if (_nodes.TryGetValue(id, out var node))
                {
                    foreach (var dep in node.Dependencies)
                    {
                        if (earliest.TryGetValue(dep, out double depEnd))
                        {
                            double end = depEnd + (_nodes.TryGetValue(dep, out var dn) ? dn.Weight : 0);
                            if (end > maxPre) { maxPre = end; pred = dep; }
                        }
                    }
                }
                earliest[id] = maxPre;
                predecessor[id] = pred;
            }

            // Find the node with latest finish time
            string? lastNode = null;
            double maxFinish = 0;
            foreach (var id in sorted)
            {
                double finish = earliest[id] + _nodes[id].Weight;
                if (finish >= maxFinish) { maxFinish = finish; lastNode = id; }
            }

            // Trace back the critical path
            var path = new List<string>();
            var cur = lastNode;
            while (cur != null)
            {
                path.Add(cur);
                cur = predecessor[cur];
            }
            path.Reverse();

            // Compute latest start times
            var latest = new Dictionary<string, double>();
            foreach (var id in sorted.Reverse())
            {
                var dependents = GetDependents(id);
                if (dependents.Count == 0)
                    latest[id] = maxFinish - _nodes[id].Weight;
                else
                    latest[id] = dependents.Min(d => latest[d]) - _nodes[id].Weight;
            }

            var slack = new Dictionary<string, double>();
            foreach (var id in sorted)
                slack[id] = Math.Round(latest[id] - earliest[id], 10);

            return new CriticalPathResult(path, maxFinish, earliest, latest, slack);
        }

        /// <summary>Run full analysis on the graph.</summary>
        public GraphAnalysis Analyze()
        {
            int edgeCount = _nodes.Values.Sum(n => n.Dependencies.Count(d => _nodes.ContainsKey(d)));
            var roots = _nodes.Values.Where(n => n.Dependencies.All(d => !_nodes.ContainsKey(d)) || n.Dependencies.Count == 0)
                .Select(n => n.Id).ToList();
            var dependentCounts = new Dictionary<string, int>();
            foreach (var id in _nodes.Keys) dependentCounts[id] = 0;
            foreach (var node in _nodes.Values)
                foreach (var dep in node.Dependencies)
                    if (dependentCounts.ContainsKey(dep))
                        dependentCounts[dep]++;
            var leaves = dependentCounts.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();

            double avgFanIn = _nodes.Count > 0 ? _nodes.Values.Average(n => n.Dependencies.Count(d => _nodes.ContainsKey(d))) : 0;
            double avgFanOut = _nodes.Count > 0 ? dependentCounts.Values.Average() : 0;

            // Bottlenecks: nodes with dependents > 2 * average fan-out
            var bottlenecks = dependentCounts
                .Where(kv => kv.Value > Math.Max(avgFanOut * 2, 1))
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key).ToList();

            var cycles = DetectCycles();

            IReadOnlyList<IReadOnlyList<string>>? layers = null;
            CriticalPathResult? criticalPath = null;
            int maxDepth = 0;

            if (cycles.Count == 0)
            {
                layers = ComputeExecutionLayers();
                maxDepth = layers.Count;
                criticalPath = ComputeCriticalPath();
            }

            return new GraphAnalysis
            {
                NodeCount = _nodes.Count,
                EdgeCount = edgeCount,
                RootNodes = roots,
                LeafNodes = leaves,
                MaxDepth = maxDepth,
                AverageFanIn = Math.Round(avgFanIn, 2),
                AverageFanOut = Math.Round(avgFanOut, 2),
                Bottlenecks = bottlenecks,
                ExecutionLayers = layers ?? Array.Empty<IReadOnlyList<string>>(),
                CriticalPath = criticalPath,
                Cycles = cycles
            };
        }

        /// <summary>Export to Graphviz DOT format.</summary>
        public string ToDot(string graphName = "PromptDependencies")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"digraph \"{graphName}\" {{");
            sb.AppendLine("  rankdir=TB;");
            sb.AppendLine("  node [shape=box, style=rounded];");
            sb.AppendLine();

            foreach (var node in _nodes.Values)
            {
                var label = node.Label ?? node.Id;
                sb.AppendLine($"  \"{node.Id}\" [label=\"{EscapeDot(label)}\\nw={node.Weight}\"];");
            }
            sb.AppendLine();

            foreach (var node in _nodes.Values)
                foreach (var dep in node.Dependencies)
                    if (_nodes.ContainsKey(dep))
                        sb.AppendLine($"  \"{dep}\" -> \"{node.Id}\";");

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Export to Mermaid diagram format.</summary>
        public string ToMermaid()
        {
            var sb = new StringBuilder();
            sb.AppendLine("graph TD");

            foreach (var node in _nodes.Values)
            {
                var label = node.Label ?? node.Id;
                sb.AppendLine($"  {node.Id}[\"{EscapeMermaid(label)}\"]");
            }

            foreach (var node in _nodes.Values)
                foreach (var dep in node.Dependencies)
                    if (_nodes.ContainsKey(dep))
                        sb.AppendLine($"  {dep} --> {node.Id}");

            return sb.ToString();
        }

        /// <summary>Export graph to JSON.</summary>
        public string ToJson(bool indented = true)
        {
            var data = new
            {
                nodes = _nodes.Values.Select(n => new
                {
                    id = n.Id,
                    label = n.Label,
                    weight = n.Weight,
                    dependencies = n.Dependencies,
                    metadata = n.Metadata
                }),
                analysis = Analyze()
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>Generate a text report of the graph analysis.</summary>
        public string GenerateReport()
        {
            var analysis = Analyze();
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  PROMPT DEPENDENCY GRAPH REPORT");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  Nodes: {analysis.NodeCount}");
            sb.AppendLine($"  Edges: {analysis.EdgeCount}");
            sb.AppendLine($"  DAG: {(analysis.IsDAG ? "Yes" : "No")}");
            sb.AppendLine($"  Max Depth: {analysis.MaxDepth}");
            sb.AppendLine($"  Avg Fan-In: {analysis.AverageFanIn:F2}");
            sb.AppendLine($"  Avg Fan-Out: {analysis.AverageFanOut:F2}");
            sb.AppendLine();

            sb.AppendLine("  Root Nodes (entry points):");
            foreach (var r in analysis.RootNodes) sb.AppendLine($"    • {r}");
            sb.AppendLine();

            sb.AppendLine("  Leaf Nodes (final outputs):");
            foreach (var l in analysis.LeafNodes) sb.AppendLine($"    • {l}");
            sb.AppendLine();

            if (analysis.Bottlenecks.Count > 0)
            {
                sb.AppendLine("  ⚠ Bottlenecks:");
                foreach (var b in analysis.Bottlenecks)
                    sb.AppendLine($"    • {b} ({GetDependents(b).Count} dependents)");
                sb.AppendLine();
            }

            if (!analysis.IsDAG)
            {
                sb.AppendLine("  ❌ CYCLES DETECTED:");
                foreach (var c in analysis.Cycles)
                    sb.AppendLine($"    • {c}");
                sb.AppendLine();
            }

            if (analysis.ExecutionLayers.Count > 0)
            {
                sb.AppendLine("  Execution Layers (parallelizable):");
                for (int i = 0; i < analysis.ExecutionLayers.Count; i++)
                    sb.AppendLine($"    Layer {i}: [{string.Join(", ", analysis.ExecutionLayers[i])}]");
                sb.AppendLine();
            }

            if (analysis.CriticalPath != null)
            {
                sb.AppendLine($"  Critical Path (weight={analysis.CriticalPath.TotalWeight:F1}):");
                sb.AppendLine($"    {string.Join(" → ", analysis.CriticalPath.Path)}");
                sb.AppendLine();
                sb.AppendLine("  Slack Analysis:");
                foreach (var kv in analysis.CriticalPath.Slack.OrderBy(kv => kv.Value))
                    sb.AppendLine($"    {kv.Key}: slack={kv.Value:F1}{(kv.Value == 0 ? " ← CRITICAL" : "")}");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>
        /// Auto-detect dependencies from prompt template variable references.
        /// Scans template text for {{nodeId}} patterns and adds edges.
        /// </summary>
        public PromptDependencyGraph AutoDetectDependencies(string nodeId, string templateText)
        {
            if (!_nodes.ContainsKey(nodeId))
                throw new ArgumentException($"Node '{nodeId}' not found.", nameof(nodeId));

            var matches = Regex.Matches(templateText, @"\{\{(\w+)\}\}");
            foreach (Match match in matches)
            {
                string refId = match.Groups[1].Value;
                if (_nodes.ContainsKey(refId) && refId != nodeId)
                    _nodes[nodeId].DependsOn(refId);
            }
            return this;
        }

        /// <summary>Merge another graph into this one.</summary>
        public PromptDependencyGraph Merge(PromptDependencyGraph other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var node in other._nodes.Values)
            {
                if (!_nodes.ContainsKey(node.Id))
                    _nodes[node.Id] = node;
            }
            return this;
        }

        /// <summary>Create a subgraph containing only the specified nodes and edges between them.</summary>
        public PromptDependencyGraph Subgraph(IEnumerable<string> nodeIds)
        {
            var ids = new HashSet<string>(nodeIds);
            var sub = new PromptDependencyGraph();
            foreach (var id in ids)
            {
                if (_nodes.TryGetValue(id, out var node))
                {
                    sub.AddNode(id, node.Label, node.Weight);
                    foreach (var dep in node.Dependencies)
                        if (ids.Contains(dep))
                            sub._nodes[id].DependsOn(dep);
                }
            }
            return sub;
        }

        /// <summary>Get the impact set: all nodes affected if the given node changes.</summary>
        public IReadOnlySet<string> GetImpactSet(string nodeId)
            => GetTransitiveDependents(nodeId);

        /// <summary>Get the requirement set: all nodes needed for the given node to execute.</summary>
        public IReadOnlySet<string> GetRequirementSet(string nodeId)
            => GetTransitiveDependencies(nodeId);

        private static string EscapeDot(string s)
            => s.Replace("\"", "\\\"").Replace("\n", "\\n");

        private static string EscapeMermaid(string s)
            => s.Replace("\"", "'");
    }
}
