namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Defines the visual direction of a Mermaid flowchart.
    /// </summary>
    public enum FlowDirection
    {
        /// <summary>Top to bottom (default).</summary>
        TopToBottom,
        /// <summary>Left to right.</summary>
        LeftToRight,
        /// <summary>Bottom to top.</summary>
        BottomToTop,
        /// <summary>Right to left.</summary>
        RightToLeft
    }

    /// <summary>
    /// The shape of a node in the flowchart.
    /// </summary>
    public enum FlowNodeShape
    {
        /// <summary>Rectangle with square corners: [text]</summary>
        Rectangle,
        /// <summary>Rounded rectangle: (text)</summary>
        Rounded,
        /// <summary>Stadium/pill shape: ([text])</summary>
        Stadium,
        /// <summary>Diamond for decisions: {text}</summary>
        Diamond,
        /// <summary>Hexagon: {{text}}</summary>
        Hexagon,
        /// <summary>Circle: ((text))</summary>
        Circle,
        /// <summary>Trapezoid: [/text\]</summary>
        Trapezoid
    }

    /// <summary>
    /// Represents a node in a <see cref="PromptFlowChart"/>.
    /// </summary>
    public class FlowChartNode
    {
        /// <summary>Unique node identifier (alphanumeric).</summary>
        public string Id { get; }

        /// <summary>Display label for the node.</summary>
        public string Label { get; set; }

        /// <summary>Visual shape of the node.</summary>
        public FlowNodeShape Shape { get; set; }

        /// <summary>Optional CSS class for styling.</summary>
        public string? CssClass { get; set; }

        /// <summary>Creates a new flowchart node.</summary>
        public FlowChartNode(string id, string label, FlowNodeShape shape = FlowNodeShape.Rectangle)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Node id cannot be null or empty.", nameof(id));
            Id = id;
            Label = label ?? id;
            Shape = shape;
        }
    }

    /// <summary>
    /// Represents a directed edge between two nodes.
    /// </summary>
    public class FlowChartEdge
    {
        /// <summary>Source node id.</summary>
        public string From { get; }

        /// <summary>Target node id.</summary>
        public string To { get; }

        /// <summary>Optional label on the edge.</summary>
        public string? Label { get; set; }

        /// <summary>If true, renders as a dotted line.</summary>
        public bool Dotted { get; set; }

        /// <summary>Creates a new edge.</summary>
        public FlowChartEdge(string from, string to, string? label = null)
        {
            From = from ?? throw new ArgumentNullException(nameof(from));
            To = to ?? throw new ArgumentNullException(nameof(to));
            Label = label;
        }
    }

    /// <summary>
    /// Generates Mermaid flowchart diagrams from prompt chains, workflows,
    /// or custom node/edge definitions. Useful for visualizing prompt
    /// execution flow, documenting architectures, and debugging complex
    /// multi-step pipelines.
    /// </summary>
    /// <example>
    /// <code>
    /// // From a PromptChain:
    /// var chart = PromptFlowChart.FromChain(myChain);
    /// Console.WriteLine(chart.Render());
    ///
    /// // From a PromptWorkflow:
    /// var chart = PromptFlowChart.FromWorkflow(myWorkflow);
    /// File.WriteAllText("flow.md", chart.RenderMarkdown());
    ///
    /// // Build manually:
    /// var chart = new PromptFlowChart("My Flow", FlowDirection.LeftToRight);
    /// chart.AddNode("a", "Start", FlowNodeShape.Stadium);
    /// chart.AddNode("b", "Process", FlowNodeShape.Rectangle);
    /// chart.AddNode("c", "Decision?", FlowNodeShape.Diamond);
    /// chart.AddNode("d", "End", FlowNodeShape.Stadium);
    /// chart.AddEdge("a", "b");
    /// chart.AddEdge("b", "c");
    /// chart.AddEdge("c", "d", "yes");
    /// chart.AddEdge("c", "b", "no", dotted: true);
    /// Console.WriteLine(chart.Render());
    /// </code>
    /// </example>
    public class PromptFlowChart
    {
        private readonly List<FlowChartNode> _nodes = new();
        private readonly List<FlowChartEdge> _edges = new();
        private readonly Dictionary<string, string> _styles = new();
        private readonly List<(string Name, List<string> NodeIds)> _subgraphs = new();

        /// <summary>Title of the flowchart.</summary>
        public string Title { get; set; }

        /// <summary>Layout direction.</summary>
        public FlowDirection Direction { get; set; }

        /// <summary>Read-only list of nodes.</summary>
        public IReadOnlyList<FlowChartNode> Nodes => _nodes;

        /// <summary>Read-only list of edges.</summary>
        public IReadOnlyList<FlowChartEdge> Edges => _edges;

        /// <summary>
        /// Creates a new flowchart builder.
        /// </summary>
        /// <param name="title">Chart title (used in markdown output).</param>
        /// <param name="direction">Layout direction.</param>
        public PromptFlowChart(string title = "Prompt Flow",
                               FlowDirection direction = FlowDirection.TopToBottom)
        {
            Title = title;
            Direction = direction;
        }

        /// <summary>Adds a node to the chart.</summary>
        /// <returns>This instance for chaining.</returns>
        public PromptFlowChart AddNode(string id, string label,
                                        FlowNodeShape shape = FlowNodeShape.Rectangle,
                                        string? cssClass = null)
        {
            if (_nodes.Any(n => n.Id == id))
                throw new InvalidOperationException($"Node '{id}' already exists.");
            _nodes.Add(new FlowChartNode(id, label, shape) { CssClass = cssClass });
            return this;
        }

        /// <summary>Adds a directed edge between two nodes.</summary>
        /// <returns>This instance for chaining.</returns>
        public PromptFlowChart AddEdge(string from, string to,
                                        string? label = null, bool dotted = false)
        {
            _edges.Add(new FlowChartEdge(from, to, label) { Dotted = dotted });
            return this;
        }

        /// <summary>
        /// Defines a CSS class style for nodes.
        /// </summary>
        /// <param name="className">The class name (referenced by <see cref="FlowChartNode.CssClass"/>).</param>
        /// <param name="cssProperties">Mermaid CSS properties, e.g. "fill:#f9f,stroke:#333".</param>
        /// <returns>This instance for chaining.</returns>
        public PromptFlowChart AddStyle(string className, string cssProperties)
        {
            _styles[className] = cssProperties;
            return this;
        }

        /// <summary>
        /// Groups nodes into a labeled subgraph.
        /// </summary>
        /// <param name="name">Subgraph label.</param>
        /// <param name="nodeIds">IDs of nodes to include.</param>
        /// <returns>This instance for chaining.</returns>
        public PromptFlowChart AddSubgraph(string name, params string[] nodeIds)
        {
            _subgraphs.Add((name, nodeIds.ToList()));
            return this;
        }

        /// <summary>
        /// Creates a flowchart from a <see cref="PromptChain"/>.
        /// Each step becomes a node, connected sequentially.
        /// </summary>
        public static PromptFlowChart FromChain(PromptChain chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            var chart = new PromptFlowChart("Chain");
            var steps = chain.Steps;

            if (steps.Count == 0) return chart;

            // Start node
            chart.AddNode("start", "Start", FlowNodeShape.Stadium, "startEnd");

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var nodeId = $"step{i}";
                var label = $"{step.Name}\\n→ {step.OutputVariable}";
                chart.AddNode(nodeId, label, FlowNodeShape.Rectangle, "step");
            }

            // End node
            chart.AddNode("end", "End", FlowNodeShape.Stadium, "startEnd");

            // Edges
            chart.AddEdge("start", "step0");
            for (int i = 0; i < steps.Count - 1; i++)
            {
                chart.AddEdge($"step{i}", $"step{i + 1}", steps[i].OutputVariable);
            }
            chart.AddEdge($"step{steps.Count - 1}", "end");

            chart.AddStyle("startEnd", "fill:#4CAF50,stroke:#388E3C,color:#fff");
            chart.AddStyle("step", "fill:#E3F2FD,stroke:#1976D2");

            return chart;
        }

        /// <summary>
        /// Creates a flowchart from a <see cref="PromptWorkflow"/>.
        /// Respects dependencies, branch points, and merge nodes.
        /// </summary>
        public static PromptFlowChart FromWorkflow(PromptWorkflow workflow)
        {
            if (workflow == null) throw new ArgumentNullException(nameof(workflow));

            var chart = new PromptFlowChart(
                "Workflow",
                FlowDirection.TopToBottom);

            var nodes = workflow.Nodes.Values.ToList();
            if (nodes.Count == 0) return chart;

            foreach (var node in nodes)
            {
                var shape = FlowNodeShape.Rectangle;
                var cssClass = "step";

                // Detect merge nodes (multiple dependencies)
                if (node.DependsOn.Count > 1)
                {
                    shape = FlowNodeShape.Hexagon;
                    cssClass = "merge";
                }

                // Detect likely branch/decision nodes (depended on by multiple)
                int childCount = nodes.Count(n => n.DependsOn.Contains(node.Id));
                if (childCount > 1 && node.Template != null)
                {
                    shape = FlowNodeShape.Diamond;
                    cssClass = "branch";
                }

                // Root nodes (no dependencies) with template are start points
                if (node.DependsOn.Count == 0)
                {
                    cssClass = "root";
                }

                var label = node.Name ?? node.Id;
                if (!string.IsNullOrEmpty(node.OutputVariable))
                    label += $"\\n→ {node.OutputVariable}";

                chart.AddNode(node.Id, label, shape, cssClass);
            }

            // Add dependency edges
            foreach (var node in nodes)
            {
                foreach (var dep in node.DependsOn)
                {
                    chart.AddEdge(dep, node.Id);
                }
            }

            chart.AddStyle("root", "fill:#4CAF50,stroke:#388E3C,color:#fff");
            chart.AddStyle("step", "fill:#E3F2FD,stroke:#1976D2");
            chart.AddStyle("branch", "fill:#FFF3E0,stroke:#F57C00");
            chart.AddStyle("merge", "fill:#F3E5F5,stroke:#7B1FA2");

            return chart;
        }

        /// <summary>
        /// Renders the flowchart as a Mermaid diagram string.
        /// </summary>
        public string Render()
        {
            var sb = new StringBuilder();

            var dir = Direction switch
            {
                FlowDirection.TopToBottom => "TB",
                FlowDirection.LeftToRight => "LR",
                FlowDirection.BottomToTop => "BT",
                FlowDirection.RightToLeft => "RL",
                _ => "TB"
            };

            sb.AppendLine($"flowchart {dir}");

            // Collect nodes that belong to subgraphs
            var subgraphNodeIds = new HashSet<string>(
                _subgraphs.SelectMany(sg => sg.NodeIds));

            // Render nodes not in subgraphs
            foreach (var node in _nodes.Where(n => !subgraphNodeIds.Contains(n.Id)))
            {
                sb.AppendLine($"    {RenderNode(node)}");
            }

            // Render subgraphs
            foreach (var (name, nodeIds) in _subgraphs)
            {
                sb.AppendLine($"    subgraph {EscapeLabel(name)}");
                foreach (var nodeId in nodeIds)
                {
                    var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
                    if (node != null)
                        sb.AppendLine($"        {RenderNode(node)}");
                }
                sb.AppendLine("    end");
            }

            // Render edges
            foreach (var edge in _edges)
            {
                sb.AppendLine($"    {RenderEdge(edge)}");
            }

            // Render class definitions
            foreach (var (className, css) in _styles)
            {
                var nodesWithClass = _nodes
                    .Where(n => n.CssClass == className)
                    .Select(n => n.Id);
                var ids = string.Join(",", nodesWithClass);
                if (!string.IsNullOrEmpty(ids))
                {
                    sb.AppendLine($"    classDef {className} {css}");
                    sb.AppendLine($"    class {ids} {className}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders as a complete Markdown document with the diagram in a
        /// fenced code block.
        /// </summary>
        public string RenderMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {Title}");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine(Render());
            sb.AppendLine("```");

            // Add a legend
            sb.AppendLine();
            sb.AppendLine("## Legend");
            sb.AppendLine();
            sb.AppendLine($"- **Nodes:** {_nodes.Count}");
            sb.AppendLine($"- **Edges:** {_edges.Count}");
            sb.AppendLine($"- **Direction:** {Direction}");

            if (_subgraphs.Count > 0)
                sb.AppendLine($"- **Subgraphs:** {_subgraphs.Count}");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders as an HTML page with Mermaid.js included for live rendering.
        /// </summary>
        public string RenderHtml()
        {
            var mermaid = EscapeHtml(Render()).Replace("\\n", "<br/>");
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(Title)}</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
               max-width: 1200px; margin: 2rem auto; padding: 0 1rem;
               background: #fafafa; color: #333; }}
        h1 {{ color: #1976D2; border-bottom: 2px solid #E3F2FD; padding-bottom: .5rem; }}
        .mermaid {{ background: #fff; padding: 2rem; border-radius: 8px;
                    box-shadow: 0 2px 8px rgba(0,0,0,.1); }}
        .stats {{ margin-top: 1rem; color: #666; font-size: .9rem; }}
    </style>
</head>
<body>
    <h1>{EscapeHtml(Title)}</h1>
    <div class=""mermaid"">
{Render()}
    </div>
    <div class=""stats"">
        {_nodes.Count} nodes &middot; {_edges.Count} edges &middot; {Direction}
    </div>
    <script type=""module"">
        import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
        mermaid.initialize({{ startOnLoad: true, theme: 'default' }});
    </script>
</body>
</html>";
        }

        /// <summary>
        /// Validates the chart: checks for missing node references in edges,
        /// detects cycles, and returns any issues found.
        /// </summary>
        /// <returns>List of validation messages (empty if valid).</returns>
        public List<string> Validate()
        {
            var issues = new List<string>();
            var nodeIds = new HashSet<string>(_nodes.Select(n => n.Id));

            foreach (var edge in _edges)
            {
                if (!nodeIds.Contains(edge.From))
                    issues.Add($"Edge references unknown source node '{edge.From}'.");
                if (!nodeIds.Contains(edge.To))
                    issues.Add($"Edge references unknown target node '{edge.To}'.");
            }

            // Cycle detection via DFS
            if (HasCycle(nodeIds))
                issues.Add("Flowchart contains a cycle.");

            return issues;
        }

        private bool HasCycle(HashSet<string> nodeIds)
        {
            var visited = new HashSet<string>();
            var stack = new HashSet<string>();
            var adj = new Dictionary<string, List<string>>();

            foreach (var id in nodeIds)
                adj[id] = new List<string>();
            foreach (var edge in _edges)
            {
                if (adj.ContainsKey(edge.From))
                    adj[edge.From].Add(edge.To);
            }

            bool Dfs(string node)
            {
                visited.Add(node);
                stack.Add(node);
                foreach (var next in adj[node])
                {
                    if (!visited.Contains(next) && Dfs(next)) return true;
                    if (stack.Contains(next)) return true;
                }
                stack.Remove(node);
                return false;
            }

            foreach (var id in nodeIds)
            {
                if (!visited.Contains(id) && Dfs(id)) return true;
            }
            return false;
        }

        private static string RenderNode(FlowChartNode node)
        {
            var label = EscapeLabel(node.Label);
            return node.Shape switch
            {
                FlowNodeShape.Rectangle => $"{node.Id}[{label}]",
                FlowNodeShape.Rounded => $"{node.Id}({label})",
                FlowNodeShape.Stadium => $"{node.Id}([{label}])",
                FlowNodeShape.Diamond => $"{node.Id}{{{label}}}",
                FlowNodeShape.Hexagon => $"{node.Id}{{{{{label}}}}}",
                FlowNodeShape.Circle => $"{node.Id}(({label}))",
                FlowNodeShape.Trapezoid => $"{node.Id}[/{label}\\]",
                _ => $"{node.Id}[{label}]"
            };
        }

        private static string RenderEdge(FlowChartEdge edge)
        {
            var arrow = edge.Dotted ? "-.->" : "-->";
            if (!string.IsNullOrEmpty(edge.Label))
            {
                var label = EscapeLabel(edge.Label);
                return edge.Dotted
                    ? $"{edge.From} -.->|{label}| {edge.To}"
                    : $"{edge.From} -->|{label}| {edge.To}";
            }
            return $"{edge.From} {arrow} {edge.To}";
        }

        private static string EscapeLabel(string text)
        {
            return text.Replace("&", "#amp;")
                       .Replace("<", "#lt;")
                       .Replace(">", "#gt;")
                       .Replace("\"", "#quot;");
        }

        private static string EscapeHtml(string text)
        {
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;");
        }
    }
}
