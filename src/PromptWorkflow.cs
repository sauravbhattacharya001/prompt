namespace Prompt
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    // ── Enums ────────────────────────────────────────────────────────

    /// <summary>
    /// The execution status of a workflow node.
    /// </summary>
    public enum WorkflowNodeStatus
    {
        /// <summary>Not yet started.</summary>
        Pending,
        /// <summary>Currently executing.</summary>
        Running,
        /// <summary>Completed successfully.</summary>
        Completed,
        /// <summary>Skipped due to a condition or branch not taken.</summary>
        Skipped,
        /// <summary>Failed with an error.</summary>
        Failed
    }

    /// <summary>
    /// How a merge node combines inputs from multiple parent nodes.
    /// </summary>
    public enum MergeStrategy
    {
        /// <summary>Wait for all parents and concatenate outputs with newlines.</summary>
        ConcatenateAll,
        /// <summary>Wait for all parents and join with a custom separator.</summary>
        JoinWithSeparator,
        /// <summary>Take only the first parent that completes.</summary>
        FirstCompleted,
        /// <summary>Take the longest output.</summary>
        LongestOutput,
        /// <summary>Take the shortest output.</summary>
        ShortestOutput,
        /// <summary>Use a custom merge template that references parent outputs.</summary>
        CustomTemplate
    }

    // ── Node configuration ───────────────────────────────────────────

    /// <summary>
    /// Configuration for a single node in a <see cref="PromptWorkflow"/>.
    /// Nodes can be prompt steps, branch points, or merge points.
    /// </summary>
    public class WorkflowNode
    {
        /// <summary>Unique identifier for this node.</summary>
        public string Id { get; }

        /// <summary>Human-readable name for display and logging.</summary>
        public string Name { get; set; }

        /// <summary>
        /// The prompt template to execute at this node.
        /// Null for merge-only nodes that just combine parent outputs.
        /// </summary>
        public PromptTemplate? Template { get; set; }

        /// <summary>
        /// Variable name where this node's output is stored in the
        /// workflow context. Referenced by downstream nodes as
        /// <c>{{outputVariable}}</c>.
        /// </summary>
        public string OutputVariable { get; set; }

        /// <summary>
        /// IDs of nodes that must complete before this node can run.
        /// Empty for root/start nodes.
        /// </summary>
        public List<string> DependsOn { get; } = new();

        /// <summary>
        /// Optional condition function. If set and returns false, the node
        /// is skipped (and any nodes that depend solely on it are also skipped).
        /// Receives the current workflow context.
        /// </summary>
        [JsonIgnore]
        public Func<WorkflowContext, bool>? Condition { get; set; }

        /// <summary>
        /// For merge nodes: how to combine outputs from multiple parents.
        /// </summary>
        public MergeStrategy MergeStrategy { get; set; } = MergeStrategy.ConcatenateAll;

        /// <summary>
        /// Separator used when <see cref="MergeStrategy"/> is
        /// <see cref="Prompt.MergeStrategy.JoinWithSeparator"/>.
        /// </summary>
        public string MergeSeparator { get; set; } = "\n\n---\n\n";

        /// <summary>
        /// Maximum execution time for this node. Null means use the
        /// workflow-level default.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Number of retries on failure. 0 = no retries.
        /// </summary>
        public int MaxRetries { get; set; } = 0;

        /// <summary>
        /// Creates a new workflow node.
        /// </summary>
        /// <param name="id">Unique node identifier.</param>
        /// <param name="name">Human-readable name.</param>
        /// <param name="outputVariable">Variable name for the output.</param>
        /// <param name="template">Optional prompt template.</param>
        public WorkflowNode(string id, string name, string outputVariable,
            PromptTemplate? template = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Node name cannot be null or empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(outputVariable))
                throw new ArgumentException("Output variable cannot be null or empty.", nameof(outputVariable));

            Id = id;
            Name = name;
            OutputVariable = outputVariable;
            Template = template;
        }
    }

    // ── Execution context ────────────────────────────────────────────

    /// <summary>
    /// Holds the state of a workflow execution: variables, node results,
    /// timing, and metadata.
    /// </summary>
    public class WorkflowContext
    {
        /// <summary>Unique execution identifier.</summary>
        public string ExecutionId { get; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>When execution started.</summary>
        public DateTimeOffset StartedAt { get; internal set; }

        /// <summary>When execution completed.</summary>
        public DateTimeOffset? CompletedAt { get; internal set; }

        /// <summary>
        /// All variables available to node templates. Populated with
        /// initial variables and updated as nodes complete.
        /// </summary>
        public ConcurrentDictionary<string, string> Variables { get; } = new();

        /// <summary>Per-node execution results.</summary>
        public ConcurrentDictionary<string, NodeResult> NodeResults { get; } = new();

        /// <summary>Arbitrary metadata for middleware or hooks.</summary>
        public ConcurrentDictionary<string, object> Metadata { get; } = new();

        /// <summary>Total elapsed time.</summary>
        public TimeSpan Elapsed => (CompletedAt ?? DateTimeOffset.UtcNow) - StartedAt;

        /// <summary>Whether all nodes completed successfully or were skipped.</summary>
        public bool IsSuccess => NodeResults.Values.All(
            r => r.Status == WorkflowNodeStatus.Completed ||
                 r.Status == WorkflowNodeStatus.Skipped);

        /// <summary>Nodes that failed.</summary>
        public IReadOnlyList<NodeResult> FailedNodes =>
            NodeResults.Values.Where(r => r.Status == WorkflowNodeStatus.Failed).ToList();

        /// <summary>Nodes that were skipped.</summary>
        public IReadOnlyList<NodeResult> SkippedNodes =>
            NodeResults.Values.Where(r => r.Status == WorkflowNodeStatus.Skipped).ToList();

        /// <summary>Nodes that completed successfully.</summary>
        public IReadOnlyList<NodeResult> CompletedNodes =>
            NodeResults.Values.Where(r => r.Status == WorkflowNodeStatus.Completed).ToList();
    }

    /// <summary>
    /// Result of executing a single workflow node.
    /// </summary>
    public class NodeResult
    {
        /// <summary>The node ID.</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>The node name.</summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>Execution status.</summary>
        public WorkflowNodeStatus Status { get; set; } = WorkflowNodeStatus.Pending;

        /// <summary>Output text (null if skipped or failed).</summary>
        public string? Output { get; set; }

        /// <summary>The rendered prompt sent to the model (null if skipped).</summary>
        public string? RenderedPrompt { get; set; }

        /// <summary>Error message if failed.</summary>
        public string? Error { get; set; }

        /// <summary>Execution duration for this node.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>When this node started executing.</summary>
        public DateTimeOffset? StartedAt { get; set; }

        /// <summary>When this node finished.</summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>Number of retries attempted.</summary>
        public int RetriesUsed { get; set; }
    }

    // ── Workflow engine ──────────────────────────────────────────────

    /// <summary>
    /// A DAG-based prompt workflow engine that supports branching,
    /// parallel execution, conditional nodes, and merge points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="PromptChain"/> (strictly linear, A→B→C) or
    /// <see cref="PromptPipeline"/> (middleware around a single call),
    /// <c>PromptWorkflow</c> models complex multi-step prompt pipelines
    /// as a directed acyclic graph (DAG). Nodes with no dependencies
    /// between each other run in parallel automatically.
    /// </para>
    /// <para>
    /// Key capabilities:
    /// <list type="bullet">
    ///   <item>Parallel execution — independent branches run concurrently</item>
    ///   <item>Conditional nodes — skip nodes based on runtime conditions</item>
    ///   <item>Merge strategies — combine parallel branch outputs (concat,
    ///         first-completed, longest, shortest, custom template)</item>
    ///   <item>Per-node retries and timeouts</item>
    ///   <item>Full execution tracing with per-node timing</item>
    ///   <item>Cycle detection at build time</item>
    ///   <item>Visualization via DOT graph export</item>
    /// </list>
    /// </para>
    /// <para>
    /// Example: parallel summarization + translation, then merge:
    /// <code>
    /// var wf = new PromptWorkflow();
    /// wf.AddNode(new WorkflowNode("summarize", "Summarize", "summary",
    ///     new PromptTemplate("Summarize: {{input}}")));
    /// wf.AddNode(new WorkflowNode("translate", "Translate", "translation",
    ///     new PromptTemplate("Translate to French: {{input}}")));
    /// var merge = new WorkflowNode("merge", "Combine", "final",
    ///     new PromptTemplate("Summary: {{summary}}\nTranslation: {{translation}}"));
    /// merge.DependsOn.Add("summarize");
    /// merge.DependsOn.Add("translate");
    /// wf.AddNode(merge);
    ///
    /// var ctx = await wf.ExecuteAsync(modelFunc,
    ///     new Dictionary&lt;string, string&gt; { ["input"] = "Hello world" });
    /// Console.WriteLine(ctx.Variables["final"]);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptWorkflow
    {
        private readonly Dictionary<string, WorkflowNode> _nodes = new();
        private readonly List<string> _executionOrder = new();
        private bool _validated = false;
        private TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);
        private int _maxParallelism = 8;

        /// <summary>
        /// Maximum number of nodes that can execute in parallel.
        /// Default: 8.
        /// </summary>
        public int MaxParallelism
        {
            get => _maxParallelism;
            set
            {
                if (value < 1) throw new ArgumentException("MaxParallelism must be >= 1.");
                _maxParallelism = value;
            }
        }

        /// <summary>
        /// Default timeout for nodes that don't specify their own.
        /// </summary>
        public TimeSpan DefaultTimeout
        {
            get => _defaultTimeout;
            set
            {
                if (value <= TimeSpan.Zero) throw new ArgumentException("Timeout must be positive.");
                _defaultTimeout = value;
            }
        }

        /// <summary>All registered nodes.</summary>
        public IReadOnlyDictionary<string, WorkflowNode> Nodes => _nodes;

        /// <summary>Number of registered nodes.</summary>
        public int NodeCount => _nodes.Count;

        // ── Building ─────────────────────────────────────────────────

        /// <summary>
        /// Add a node to the workflow.
        /// </summary>
        /// <param name="node">The node to add.</param>
        /// <returns>This workflow (for chaining).</returns>
        /// <exception cref="ArgumentException">Thrown if a node with the same ID already exists.</exception>
        public PromptWorkflow AddNode(WorkflowNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_nodes.ContainsKey(node.Id))
                throw new ArgumentException($"Node '{node.Id}' already exists.");
            _nodes[node.Id] = node;
            _validated = false;
            return this;
        }

        /// <summary>
        /// Add a dependency edge: <paramref name="nodeId"/> depends on
        /// <paramref name="dependsOnId"/>.
        /// </summary>
        public PromptWorkflow AddEdge(string dependsOnId, string nodeId)
        {
            if (!_nodes.ContainsKey(nodeId))
                throw new ArgumentException($"Node '{nodeId}' not found.");
            if (!_nodes.ContainsKey(dependsOnId))
                throw new ArgumentException($"Node '{dependsOnId}' not found.");
            if (!_nodes[nodeId].DependsOn.Contains(dependsOnId))
                _nodes[nodeId].DependsOn.Add(dependsOnId);
            _validated = false;
            return this;
        }

        // ── Validation ───────────────────────────────────────────────

        /// <summary>
        /// Validate the workflow: check for cycles, missing dependencies,
        /// and compute execution order (topological sort).
        /// </summary>
        /// <returns>List of validation errors (empty if valid).</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (_nodes.Count == 0)
            {
                errors.Add("Workflow has no nodes.");
                return errors;
            }

            // Check for missing dependencies
            foreach (var node in _nodes.Values)
            {
                foreach (var dep in node.DependsOn)
                {
                    if (!_nodes.ContainsKey(dep))
                        errors.Add($"Node '{node.Id}' depends on unknown node '{dep}'.");
                }
            }
            if (errors.Count > 0) return errors;

            // Cycle detection via topological sort (Kahn's algorithm)
            var inDegree = _nodes.Keys.ToDictionary(k => k, _ => 0);
            foreach (var node in _nodes.Values)
            {
                foreach (var dep in node.DependsOn)
                {
                    if (inDegree.ContainsKey(node.Id))
                        inDegree[node.Id]++;
                }
            }

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var sorted = new List<string>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                sorted.Add(current);

                foreach (var node in _nodes.Values)
                {
                    if (node.DependsOn.Contains(current))
                    {
                        inDegree[node.Id]--;
                        if (inDegree[node.Id] == 0)
                            queue.Enqueue(node.Id);
                    }
                }
            }

            if (sorted.Count != _nodes.Count)
            {
                var cycleNodes = _nodes.Keys.Except(sorted).ToList();
                errors.Add($"Cycle detected involving nodes: {string.Join(", ", cycleNodes)}");
            }
            else
            {
                _executionOrder.Clear();
                _executionOrder.AddRange(sorted);
                _validated = true;
            }

            // Check for duplicate output variables
            var outputVars = _nodes.Values
                .GroupBy(n => n.OutputVariable, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);
            foreach (var group in outputVars)
            {
                errors.Add($"Duplicate output variable '{group.Key}' in nodes: " +
                           string.Join(", ", group.Select(n => n.Id)));
            }

            return errors;
        }

        // ── Execution ────────────────────────────────────────────────

        /// <summary>
        /// Execute the workflow using the provided model function.
        /// </summary>
        /// <param name="modelFunc">
        /// Function that takes a rendered prompt and returns the model response.
        /// </param>
        /// <param name="initialVariables">
        /// Variables available to all nodes (e.g., the user input).
        /// </param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns>Workflow context with all results.</returns>
        public async Task<WorkflowContext> ExecuteAsync(
            Func<string, Task<string>> modelFunc,
            Dictionary<string, string>? initialVariables = null,
            CancellationToken cancellation = default)
        {
            if (modelFunc == null) throw new ArgumentNullException(nameof(modelFunc));

            // Validate if not already done
            if (!_validated)
            {
                var errors = Validate();
                if (errors.Count > 0)
                    throw new InvalidOperationException(
                        "Workflow validation failed:\n" + string.Join("\n", errors));
            }

            var context = new WorkflowContext { StartedAt = DateTimeOffset.UtcNow };

            // Populate initial variables
            if (initialVariables != null)
            {
                foreach (var kv in initialVariables)
                    context.Variables[kv.Key] = kv.Value;
            }

            // Initialize all node results
            foreach (var node in _nodes.Values)
            {
                context.NodeResults[node.Id] = new NodeResult
                {
                    NodeId = node.Id,
                    NodeName = node.Name,
                    Status = WorkflowNodeStatus.Pending
                };
            }

            // Execute using topological order with parallelism
            var semaphore = new SemaphoreSlim(_maxParallelism);
            var completedNodes = new ConcurrentDictionary<string, bool>();

            // Group nodes by "wave" — nodes in the same wave have no dependencies
            // between each other and can run in parallel
            var waves = ComputeWaves();

            foreach (var wave in waves)
            {
                cancellation.ThrowIfCancellationRequested();

                var tasks = wave.Select(nodeId => Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellation);
                    try
                    {
                        await ExecuteNodeAsync(nodeId, modelFunc, context,
                            completedNodes, cancellation);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellation)).ToArray();

                await Task.WhenAll(tasks);
            }

            context.CompletedAt = DateTimeOffset.UtcNow;
            return context;
        }

        /// <summary>
        /// Execute a single workflow using a synchronous model function.
        /// Convenience wrapper around <see cref="ExecuteAsync"/>.
        /// </summary>
        public WorkflowContext Execute(
            Func<string, string> modelFunc,
            Dictionary<string, string>? initialVariables = null)
        {
            if (modelFunc == null) throw new ArgumentNullException(nameof(modelFunc));
            return ExecuteAsync(
                prompt => Task.FromResult(modelFunc(prompt)),
                initialVariables).GetAwaiter().GetResult();
        }

        private async Task ExecuteNodeAsync(
            string nodeId,
            Func<string, Task<string>> modelFunc,
            WorkflowContext context,
            ConcurrentDictionary<string, bool> completedNodes,
            CancellationToken cancellation)
        {
            var node = _nodes[nodeId];
            var result = context.NodeResults[nodeId];

            // Check if any dependency was skipped or failed
            bool allDepsOk = true;
            foreach (var dep in node.DependsOn)
            {
                var depResult = context.NodeResults[dep];
                if (depResult.Status == WorkflowNodeStatus.Failed ||
                    depResult.Status == WorkflowNodeStatus.Skipped)
                {
                    allDepsOk = false;
                    break;
                }
            }

            if (!allDepsOk)
            {
                result.Status = WorkflowNodeStatus.Skipped;
                result.Error = "Skipped because a dependency was skipped or failed.";
                completedNodes[nodeId] = true;
                return;
            }

            // Check condition
            if (node.Condition != null)
            {
                try
                {
                    if (!node.Condition(context))
                    {
                        result.Status = WorkflowNodeStatus.Skipped;
                        result.Error = "Condition returned false.";
                        completedNodes[nodeId] = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    result.Status = WorkflowNodeStatus.Failed;
                    result.Error = $"Condition evaluation failed: {ex.Message}";
                    completedNodes[nodeId] = true;
                    return;
                }
            }

            var sw = Stopwatch.StartNew();
            result.StartedAt = DateTimeOffset.UtcNow;
            result.Status = WorkflowNodeStatus.Running;

            // If this is a merge-only node (no template), combine parent outputs
            if (node.Template == null)
            {
                try
                {
                    var mergedOutput = MergeParentOutputs(node, context);
                    context.Variables[node.OutputVariable] = mergedOutput;
                    result.Output = mergedOutput;
                    result.Status = WorkflowNodeStatus.Completed;
                }
                catch (Exception ex)
                {
                    result.Status = WorkflowNodeStatus.Failed;
                    result.Error = $"Merge failed: {ex.Message}";
                }
                sw.Stop();
                result.Duration = sw.Elapsed;
                result.CompletedAt = DateTimeOffset.UtcNow;
                completedNodes[nodeId] = true;
                return;
            }

            // Render template with current variables
            var timeout = node.Timeout ?? _defaultTimeout;
            int attempts = 0;
            int maxAttempts = 1 + node.MaxRetries;

            while (attempts < maxAttempts)
            {
                attempts++;
                try
                {
                    cancellation.ThrowIfCancellationRequested();

                    var vars = new Dictionary<string, string>(
                        context.Variables, StringComparer.OrdinalIgnoreCase);
                    var rendered = node.Template.Render(vars);
                    result.RenderedPrompt = rendered;

                    // Execute with timeout
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    cts.CancelAfter(timeout);

                    var response = await modelFunc(rendered);

                    context.Variables[node.OutputVariable] = response;
                    result.Output = response;
                    result.Status = WorkflowNodeStatus.Completed;
                    result.RetriesUsed = attempts - 1;
                    break;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    result.Status = WorkflowNodeStatus.Failed;
                    result.Error = "Workflow cancelled.";
                    break;
                }
                catch (Exception ex)
                {
                    if (attempts >= maxAttempts)
                    {
                        result.Status = WorkflowNodeStatus.Failed;
                        result.Error = $"Failed after {attempts} attempt(s): {ex.Message}";
                        result.RetriesUsed = attempts - 1;
                    }
                    // else retry
                }
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            result.CompletedAt = DateTimeOffset.UtcNow;
            completedNodes[nodeId] = true;
        }

        private string MergeParentOutputs(WorkflowNode node, WorkflowContext context)
        {
            var parentOutputs = new List<KeyValuePair<string, string>>();
            foreach (var dep in node.DependsOn)
            {
                var depNode = _nodes[dep];
                if (context.Variables.TryGetValue(depNode.OutputVariable, out var val))
                {
                    parentOutputs.Add(new KeyValuePair<string, string>(depNode.OutputVariable, val));
                }
            }

            return node.MergeStrategy switch
            {
                MergeStrategy.ConcatenateAll =>
                    string.Join("\n\n", parentOutputs.Select(p => p.Value)),
                MergeStrategy.JoinWithSeparator =>
                    string.Join(node.MergeSeparator, parentOutputs.Select(p => p.Value)),
                MergeStrategy.FirstCompleted =>
                    parentOutputs.FirstOrDefault().Value ?? string.Empty,
                MergeStrategy.LongestOutput =>
                    parentOutputs.OrderByDescending(p => p.Value.Length)
                        .FirstOrDefault().Value ?? string.Empty,
                MergeStrategy.ShortestOutput =>
                    parentOutputs.OrderBy(p => p.Value.Length)
                        .FirstOrDefault().Value ?? string.Empty,
                MergeStrategy.CustomTemplate =>
                    throw new InvalidOperationException(
                        "CustomTemplate merge requires a Template on the node."),
                _ => string.Join("\n\n", parentOutputs.Select(p => p.Value))
            };
        }

        // ── Wave computation ─────────────────────────────────────────

        private List<List<string>> ComputeWaves()
        {
            var waves = new List<List<string>>();
            var remaining = new HashSet<string>(_executionOrder);
            var completed = new HashSet<string>();

            while (remaining.Count > 0)
            {
                var wave = new List<string>();
                foreach (var nodeId in remaining.ToList())
                {
                    var node = _nodes[nodeId];
                    if (node.DependsOn.All(d => completed.Contains(d)))
                    {
                        wave.Add(nodeId);
                    }
                }

                if (wave.Count == 0)
                    throw new InvalidOperationException("Deadlock detected in workflow.");

                waves.Add(wave);
                foreach (var id in wave)
                {
                    remaining.Remove(id);
                    completed.Add(id);
                }
            }

            return waves;
        }

        // ── Visualization ────────────────────────────────────────────

        /// <summary>
        /// Export the workflow as a Graphviz DOT string for visualization.
        /// </summary>
        /// <param name="context">
        /// Optional execution context to color nodes by status.
        /// </param>
        /// <returns>DOT-format directed graph string.</returns>
        public string ToDot(WorkflowContext? context = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("digraph workflow {");
            sb.AppendLine("  rankdir=TB;");
            sb.AppendLine("  node [shape=box, style=rounded, fontname=\"Helvetica\"];");
            sb.AppendLine();

            foreach (var node in _nodes.Values)
            {
                var label = $"{node.Name}\\n({node.OutputVariable})";
                var color = "lightblue";
                var penwidth = "1";

                if (context != null && context.NodeResults.TryGetValue(node.Id, out var result))
                {
                    color = result.Status switch
                    {
                        WorkflowNodeStatus.Completed => "palegreen",
                        WorkflowNodeStatus.Failed => "salmon",
                        WorkflowNodeStatus.Skipped => "lightyellow",
                        WorkflowNodeStatus.Running => "lightskyblue",
                        _ => "lightgray"
                    };
                    var ms = result.Duration.TotalMilliseconds;
                    if (ms > 0) label += $"\\n{ms:F0}ms";
                    if (result.Status == WorkflowNodeStatus.Failed)
                        penwidth = "2";
                }

                if (node.Condition != null)
                    label += "\\n[conditional]";
                if (node.Template == null && node.DependsOn.Count > 0)
                    label += $"\\n[merge: {node.MergeStrategy}]";

                sb.AppendLine($"  \"{node.Id}\" [label=\"{label}\", fillcolor=\"{color}\", style=\"filled,rounded\", penwidth={penwidth}];");
            }

            sb.AppendLine();

            foreach (var node in _nodes.Values)
            {
                foreach (var dep in node.DependsOn)
                {
                    var style = "solid";
                    if (node.Condition != null) style = "dashed";
                    sb.AppendLine($"  \"{dep}\" -> \"{node.Id}\" [style={style}];");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        // ── Report ───────────────────────────────────────────────────

        /// <summary>
        /// Generate a human-readable execution report.
        /// </summary>
        public static string GenerateReport(WorkflowContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══ Workflow Execution Report ═══");
            sb.AppendLine();
            sb.AppendLine($"Execution ID: {context.ExecutionId}");
            sb.AppendLine($"Started:      {context.StartedAt:O}");
            sb.AppendLine($"Completed:    {context.CompletedAt:O}");
            sb.AppendLine($"Elapsed:      {context.Elapsed.TotalMilliseconds:F0}ms");
            sb.AppendLine($"Status:       {(context.IsSuccess ? "SUCCESS" : "FAILED")}");
            sb.AppendLine();

            var completed = context.CompletedNodes;
            var skipped = context.SkippedNodes;
            var failed = context.FailedNodes;

            sb.AppendLine($"Nodes: {completed.Count} completed, {skipped.Count} skipped, {failed.Count} failed");
            sb.AppendLine();

            sb.AppendLine("── Node Results ──");
            foreach (var result in context.NodeResults.Values.OrderBy(r => r.StartedAt))
            {
                var icon = result.Status switch
                {
                    WorkflowNodeStatus.Completed => "✓",
                    WorkflowNodeStatus.Skipped => "⊘",
                    WorkflowNodeStatus.Failed => "✗",
                    _ => "?"
                };
                sb.AppendLine($"  {icon} {result.NodeName} ({result.NodeId})");
                sb.AppendLine($"    Status:   {result.Status}");
                sb.AppendLine($"    Duration: {result.Duration.TotalMilliseconds:F0}ms");
                if (result.RetriesUsed > 0)
                    sb.AppendLine($"    Retries:  {result.RetriesUsed}");
                if (result.Output != null)
                {
                    var preview = result.Output.Length > 100
                        ? result.Output[..100] + "…"
                        : result.Output;
                    sb.AppendLine($"    Output:   {preview}");
                }
                if (result.Error != null)
                    sb.AppendLine($"    Error:    {result.Error}");
                sb.AppendLine();
            }

            sb.AppendLine("── Final Variables ──");
            foreach (var kv in context.Variables.OrderBy(kv => kv.Key))
            {
                var val = kv.Value.Length > 80
                    ? kv.Value[..80] + "…"
                    : kv.Value;
                sb.AppendLine($"  {kv.Key} = {val}");
            }

            return sb.ToString();
        }
    }
}
