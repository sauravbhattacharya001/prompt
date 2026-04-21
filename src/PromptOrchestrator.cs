namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    // ── Enums ────────────────────────────────────────────────

    /// <summary>Type of orchestrator node.</summary>
    public enum OrchestratorNodeType
    {
        /// <summary>Standard prompt execution node.</summary>
        Standard,
        /// <summary>Gate node that blocks downstream if confidence is below threshold.</summary>
        Gate,
        /// <summary>Aggregator node that combines results from multiple upstream nodes.</summary>
        Aggregator,
        /// <summary>Router node that selects which branch to follow based on prior results.</summary>
        Router,
        /// <summary>Fallback node that tries alternatives on failure.</summary>
        Fallback
    }

    /// <summary>Overall orchestration execution status.</summary>
    public enum OrchestratorStatus
    {
        /// <summary>Execution has not started.</summary>
        Pending,
        /// <summary>Execution is in progress.</summary>
        Running,
        /// <summary>All nodes completed successfully.</summary>
        Completed,
        /// <summary>One or more nodes failed and no fallback recovered.</summary>
        Failed,
        /// <summary>Some nodes succeeded, some failed or were skipped.</summary>
        PartialSuccess
    }

    /// <summary>Types of orchestrator execution events.</summary>
    public enum OrchestratorEventType
    {
        /// <summary>Node execution started.</summary>
        NodeStarted,
        /// <summary>Node execution completed successfully.</summary>
        NodeCompleted,
        /// <summary>Node execution failed.</summary>
        NodeFailed,
        /// <summary>Node is being retried.</summary>
        NodeRetrying,
        /// <summary>Node was skipped (gate blocked or router bypassed).</summary>
        NodeSkipped,
        /// <summary>Gate blocked downstream execution.</summary>
        GateBlocked,
        /// <summary>Router selected a branch.</summary>
        RouteSelected,
        /// <summary>Fallback chain triggered.</summary>
        FallbackTriggered,
        /// <summary>Overall execution started.</summary>
        ExecutionStarted,
        /// <summary>Overall execution completed.</summary>
        ExecutionCompleted
    }

    // ── Data Types ───────────────────────────────────────────

    /// <summary>
    /// Defines a single node in an orchestration plan. Each node contains a prompt
    /// template, dependency information, and execution behavior settings.
    /// </summary>
    public class OrchestratorNode
    {
        /// <summary>Unique identifier for this node.</summary>
        public string Id { get; }

        /// <summary>Prompt template text. Use {nodeId} placeholders to inject upstream outputs.</summary>
        public string PromptText { get; set; }

        /// <summary>IDs of nodes that must complete before this node runs.</summary>
        public List<string> DependsOn { get; set; } = new();

        /// <summary>Behavioral type of this node.</summary>
        public OrchestratorNodeType Type { get; set; } = OrchestratorNodeType.Standard;

        /// <summary>Maximum retry attempts on failure (default 1 = no retry).</summary>
        public int MaxRetries { get; set; } = 1;

        /// <summary>Timeout for a single execution attempt.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>For Gate nodes: minimum confidence to allow downstream execution.</summary>
        public double? ConfidenceThreshold { get; set; }

        /// <summary>For Router nodes: function that receives all results so far and returns the next node ID to execute.</summary>
        [JsonIgnore]
        public Func<Dictionary<string, OrchestratorResult>, string>? RouterFunc { get; set; }

        /// <summary>For Fallback nodes: ordered list of alternative node IDs to try on failure.</summary>
        public List<string>? FallbackNodeIds { get; set; }

        /// <summary>Create a new orchestrator node.</summary>
        /// <param name="id">Unique node identifier.</param>
        /// <param name="promptText">Prompt template with optional {nodeId} placeholders.</param>
        public OrchestratorNode(string id, string promptText)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            PromptText = promptText ?? throw new ArgumentNullException(nameof(promptText));
        }
    }

    /// <summary>Result of executing a single orchestrator node.</summary>
    public class OrchestratorResult
    {
        /// <summary>Node that produced this result.</summary>
        public string NodeId { get; set; } = "";

        /// <summary>Output text from the prompt execution.</summary>
        public string Output { get; set; } = "";

        /// <summary>Whether the node completed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Wall-clock duration of the execution.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Which attempt produced this result (1-based).</summary>
        public int Attempt { get; set; }

        /// <summary>Optional confidence/quality score (0.0–1.0).</summary>
        public double? Confidence { get; set; }

        /// <summary>Error message if the node failed.</summary>
        public string? Error { get; set; }

        /// <summary>When this result was produced.</summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>Timestamped event during orchestration execution.</summary>
    public class OrchestratorEvent
    {
        /// <summary>When the event occurred.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Related node ID (empty for execution-level events).</summary>
        public string NodeId { get; set; } = "";

        /// <summary>Event type.</summary>
        public OrchestratorEventType Type { get; set; }

        /// <summary>Human-readable event description.</summary>
        public string Message { get; set; } = "";
    }

    // ── Plan ─────────────────────────────────────────────────

    /// <summary>
    /// An orchestration plan defining nodes, their dependencies, and the entry point.
    /// Supports validation, topological sorting, and critical-path analysis.
    /// </summary>
    public class OrchestratorPlan
    {
        /// <summary>All nodes in the plan.</summary>
        public List<OrchestratorNode> Nodes { get; set; } = new();

        /// <summary>ID of the node where execution begins.</summary>
        public string EntryNodeId { get; set; } = "";

        /// <summary>
        /// Validate the plan: checks entry node exists, all dependencies reference
        /// existing nodes, and the dependency graph is acyclic.
        /// </summary>
        /// <returns>List of validation error messages (empty = valid).</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(Nodes.Select(n => n.Id));

            if (!ids.Contains(EntryNodeId))
                errors.Add($"Entry node '{EntryNodeId}' not found in plan.");

            foreach (var node in Nodes)
            {
                foreach (var dep in node.DependsOn)
                {
                    if (!ids.Contains(dep))
                        errors.Add($"Node '{node.Id}' depends on unknown node '{dep}'.");
                }
            }

            // Cycle detection via Kahn's algorithm
            var inDegree = Nodes.ToDictionary(n => n.Id, _ => 0);
            foreach (var node in Nodes)
                foreach (var dep in node.DependsOn)
                    if (inDegree.ContainsKey(node.Id))
                        inDegree[node.Id]++;

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            int visited = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                visited++;
                foreach (var node in Nodes.Where(n => n.DependsOn.Contains(current)))
                {
                    inDegree[node.Id]--;
                    if (inDegree[node.Id] == 0) queue.Enqueue(node.Id);
                }
            }

            if (visited < Nodes.Count)
                errors.Add("Dependency graph contains a cycle.");

            return errors;
        }

        /// <summary>
        /// Topological sort into parallelizable execution layers.
        /// Layer 0 = nodes with no dependencies, Layer 1 = depends only on Layer 0, etc.
        /// </summary>
        public List<List<OrchestratorNode>> GetExecutionLayers()
        {
            var layers = new List<List<OrchestratorNode>>();
            var assigned = new HashSet<string>();
            var remaining = new List<OrchestratorNode>(Nodes);

            while (remaining.Count > 0)
            {
                var layer = remaining
                    .Where(n => n.DependsOn.All(d => assigned.Contains(d)))
                    .ToList();

                if (layer.Count == 0)
                    break; // cycle guard

                layers.Add(layer);
                foreach (var n in layer)
                {
                    assigned.Add(n.Id);
                    remaining.Remove(n);
                }
            }

            return layers;
        }

        /// <summary>
        /// Compute the critical path — the longest dependency chain by node count.
        /// </summary>
        /// <returns>Ordered list of node IDs on the critical path.</returns>
        public List<string> GetCriticalPath()
        {
            var nodeMap = Nodes.ToDictionary(n => n.Id);
            var memo = new Dictionary<string, List<string>>();

            List<string> Longest(string id)
            {
                if (memo.TryGetValue(id, out var cached)) return cached;
                var node = nodeMap[id];
                List<string> best = new();
                foreach (var dep in node.DependsOn)
                {
                    if (nodeMap.ContainsKey(dep))
                    {
                        var path = Longest(dep);
                        if (path.Count > best.Count) best = path;
                    }
                }
                var result = new List<string>(best) { id };
                memo[id] = result;
                return result;
            }

            List<string> criticalPath = new();
            foreach (var node in Nodes)
            {
                var path = Longest(node.Id);
                if (path.Count > criticalPath.Count) criticalPath = path;
            }
            return criticalPath;
        }
    }

    // ── Execution ────────────────────────────────────────────

    /// <summary>
    /// Represents a single execution run of an orchestration plan, including
    /// all results, timing, and event log.
    /// </summary>
    public class OrchestratorExecution
    {
        /// <summary>Unique execution identifier.</summary>
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>The plan being executed.</summary>
        [JsonIgnore]
        public OrchestratorPlan Plan { get; set; } = new();

        /// <summary>Results keyed by node ID.</summary>
        public Dictionary<string, OrchestratorResult> Results { get; set; } = new();

        /// <summary>Overall execution status.</summary>
        public OrchestratorStatus Status { get; set; } = OrchestratorStatus.Pending;

        /// <summary>When execution started.</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>When execution completed (null if still running).</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Total wall-clock duration.</summary>
        public TimeSpan TotalDuration => CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : DateTime.UtcNow - StartedAt;

        /// <summary>Timestamped event log.</summary>
        public List<OrchestratorEvent> EventLog { get; set; } = new();
    }

    // ── Orchestrator Engine ──────────────────────────────────

    /// <summary>
    /// Autonomous multi-prompt coordination engine. Manages complex prompt workflows
    /// with dependency resolution, parallel execution, result aggregation, gate
    /// evaluation, routing, fallback chains, and adaptive retry.
    /// </summary>
    public class PromptOrchestrator
    {
        private readonly Func<string, Task<string>> _promptExecutor;

        /// <summary>
        /// Create a new orchestrator with the given prompt execution function.
        /// </summary>
        /// <param name="promptExecutor">
        /// Async function that takes a resolved prompt string and returns the LLM output.
        /// </param>
        public PromptOrchestrator(Func<string, Task<string>> promptExecutor)
        {
            _promptExecutor = promptExecutor ?? throw new ArgumentNullException(nameof(promptExecutor));
        }

        /// <summary>
        /// Execute an orchestration plan. Nodes run in topological layers with
        /// parallelism within each layer. Supports gates, routers, fallbacks, and retries.
        /// </summary>
        /// <param name="plan">The orchestration plan to execute.</param>
        /// <param name="initialVariables">Optional seed variables available as {key} placeholders.</param>
        /// <param name="useAdaptiveRetry">Use exponential backoff on retries.</param>
        /// <returns>The completed execution with all results and event log.</returns>
        public async Task<OrchestratorExecution> ExecuteAsync(
            OrchestratorPlan plan,
            Dictionary<string, string>? initialVariables = null,
            bool useAdaptiveRetry = false)
        {
            var errors = plan.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    "Plan validation failed: " + string.Join("; ", errors));

            var execution = new OrchestratorExecution
            {
                Plan = plan,
                Status = OrchestratorStatus.Running,
                StartedAt = DateTime.UtcNow
            };

            LogEvent(execution, "", OrchestratorEventType.ExecutionStarted, "Orchestration started");

            var variables = new Dictionary<string, string>(
                initialVariables ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);

            var skippedNodes = new HashSet<string>();
            var routerSelectedNodes = new HashSet<string>();
            var hasRouters = plan.Nodes.Any(n => n.Type == OrchestratorNodeType.Router);

            var layers = plan.GetExecutionLayers();

            foreach (var layer in layers)
            {
                var tasks = new List<Task>();

                foreach (var node in layer)
                {
                    if (skippedNodes.Contains(node.Id))
                    {
                        LogEvent(execution, node.Id, OrchestratorEventType.NodeSkipped,
                            $"Node '{node.Id}' skipped (upstream gate blocked or router bypassed)");
                        continue;
                    }

                    tasks.Add(ExecuteNodeAsync(execution, node, variables, skippedNodes,
                        routerSelectedNodes, useAdaptiveRetry));
                }

                await Task.WhenAll(tasks);
            }

            // Determine final status
            var results = execution.Results.Values.ToList();
            bool anyFailed = results.Any(r => !r.Success);
            bool allFailed = results.All(r => !r.Success);

            execution.Status = allFailed && results.Count > 0
                ? OrchestratorStatus.Failed
                : anyFailed
                    ? OrchestratorStatus.PartialSuccess
                    : OrchestratorStatus.Completed;

            execution.CompletedAt = DateTime.UtcNow;
            LogEvent(execution, "", OrchestratorEventType.ExecutionCompleted,
                $"Orchestration {execution.Status} — {results.Count(r => r.Success)}/{results.Count} nodes succeeded");

            return execution;
        }

        /// <summary>
        /// Execute with adaptive exponential backoff on retries.
        /// </summary>
        public Task<OrchestratorExecution> ExecuteWithAdaptiveRetryAsync(
            OrchestratorPlan plan,
            Dictionary<string, string>? initialVariables = null)
        {
            return ExecuteAsync(plan, initialVariables, useAdaptiveRetry: true);
        }

        private async Task ExecuteNodeAsync(
            OrchestratorExecution execution,
            OrchestratorNode node,
            Dictionary<string, string> variables,
            HashSet<string> skippedNodes,
            HashSet<string> routerSelectedNodes,
            bool useAdaptiveRetry)
        {
            var sw = Stopwatch.StartNew();

            for (int attempt = 1; attempt <= node.MaxRetries; attempt++)
            {
                if (attempt > 1)
                {
                    LogEvent(execution, node.Id, OrchestratorEventType.NodeRetrying,
                        $"Retry attempt {attempt}/{node.MaxRetries}");

                    if (useAdaptiveRetry)
                    {
                        var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));
                        await Task.Delay(delay);
                    }
                }

                LogEvent(execution, node.Id, OrchestratorEventType.NodeStarted,
                    $"Executing node '{node.Id}' (attempt {attempt})");

                try
                {
                    // Resolve prompt template
                    string resolvedPrompt = ResolveTemplate(node.PromptText, variables);

                    // Execute with timeout
                    using var cts = new CancellationTokenSource(node.Timeout);
                    var outputTask = _promptExecutor(resolvedPrompt);
                    var completedTask = await Task.WhenAny(outputTask, Task.Delay(node.Timeout));

                    if (completedTask != outputTask)
                        throw new TimeoutException(
                            $"Node '{node.Id}' exceeded timeout of {node.Timeout.TotalSeconds}s");

                    string output = await outputTask;
                    sw.Stop();

                    // Try to extract confidence from output (pattern: [confidence:0.XX])
                    double? confidence = ExtractConfidence(output);

                    var result = new OrchestratorResult
                    {
                        NodeId = node.Id,
                        Output = output,
                        Success = true,
                        Duration = sw.Elapsed,
                        Attempt = attempt,
                        Confidence = confidence,
                        CompletedAt = DateTime.UtcNow
                    };

                    lock (execution.Results) { execution.Results[node.Id] = result; }
                    lock (variables) { variables[node.Id] = output; }

                    // Handle Gate nodes
                    if (node.Type == OrchestratorNodeType.Gate && node.ConfidenceThreshold.HasValue)
                    {
                        double conf = confidence ?? 0.0;
                        if (conf < node.ConfidenceThreshold.Value)
                        {
                            LogEvent(execution, node.Id, OrchestratorEventType.GateBlocked,
                                $"Gate blocked: confidence {conf:F2} < threshold {node.ConfidenceThreshold.Value:F2}");

                            // Skip all downstream nodes
                            SkipDownstream(node.Id, execution.Plan, skippedNodes);
                            return;
                        }
                    }

                    // Handle Router nodes
                    if (node.Type == OrchestratorNodeType.Router && node.RouterFunc != null)
                    {
                        Dictionary<string, OrchestratorResult> snapshot;
                        lock (execution.Results) { snapshot = new(execution.Results); }

                        string selectedId = node.RouterFunc(snapshot);
                        routerSelectedNodes.Add(selectedId);

                        LogEvent(execution, node.Id, OrchestratorEventType.RouteSelected,
                            $"Router selected branch: '{selectedId}'");

                        // Skip sibling branches that were not selected
                        var downstreamAll = execution.Plan.Nodes
                            .Where(n => n.DependsOn.Contains(node.Id) && n.Id != selectedId)
                            .ToList();
                        foreach (var skip in downstreamAll)
                            SkipDownstream(skip.Id, execution.Plan, skippedNodes);
                    }

                    LogEvent(execution, node.Id, OrchestratorEventType.NodeCompleted,
                        $"Node '{node.Id}' completed in {sw.Elapsed.TotalMilliseconds:F0}ms");
                    return;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    LogEvent(execution, node.Id, OrchestratorEventType.NodeFailed,
                        $"Node '{node.Id}' failed: {ex.Message}");

                    if (attempt == node.MaxRetries)
                    {
                        var failResult = new OrchestratorResult
                        {
                            NodeId = node.Id,
                            Output = "",
                            Success = false,
                            Duration = sw.Elapsed,
                            Attempt = attempt,
                            Error = ex.Message,
                            CompletedAt = DateTime.UtcNow
                        };

                        lock (execution.Results) { execution.Results[node.Id] = failResult; }

                        // Try fallback chain
                        if (node.Type == OrchestratorNodeType.Fallback && node.FallbackNodeIds?.Count > 0)
                        {
                            LogEvent(execution, node.Id, OrchestratorEventType.FallbackTriggered,
                                $"Triggering fallback chain for '{node.Id}'");

                            var nodeMap = execution.Plan.Nodes.ToDictionary(n => n.Id);
                            foreach (var fbId in node.FallbackNodeIds)
                            {
                                if (nodeMap.TryGetValue(fbId, out var fbNode))
                                {
                                    await ExecuteNodeAsync(execution, fbNode, variables,
                                        skippedNodes, routerSelectedNodes, useAdaptiveRetry);

                                    if (execution.Results.TryGetValue(fbId, out var fbResult) && fbResult.Success)
                                    {
                                        lock (variables) { variables[node.Id] = fbResult.Output; }
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static string ResolveTemplate(string template, Dictionary<string, string> variables)
        {
            return Regex.Replace(template, @"\{(\w+)\}", match =>
            {
                var key = match.Groups[1].Value;
                return variables.TryGetValue(key, out var val) ? val : match.Value;
            });
        }

        private static double? ExtractConfidence(string output)
        {
            var match = Regex.Match(output, @"\[confidence:\s*([\d.]+)\]", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double val))
                return Math.Clamp(val, 0.0, 1.0);
            return null;
        }

        private static void SkipDownstream(string nodeId, OrchestratorPlan plan, HashSet<string> skipped)
        {
            if (!skipped.Add(nodeId)) return;
            foreach (var child in plan.Nodes.Where(n => n.DependsOn.Contains(nodeId)))
                SkipDownstream(child.Id, plan, skipped);
        }

        private static void LogEvent(OrchestratorExecution execution, string nodeId,
            OrchestratorEventType type, string message)
        {
            lock (execution.EventLog)
            {
                execution.EventLog.Add(new OrchestratorEvent
                {
                    Timestamp = DateTime.UtcNow,
                    NodeId = nodeId,
                    Type = type,
                    Message = message
                });
            }
        }

        // ── Static Builder Helpers ───────────────────────────

        /// <summary>
        /// Build a simple linear chain of prompts executed sequentially.
        /// Each prompt can reference the previous node's output via {step_N}.
        /// </summary>
        public static OrchestratorPlan BuildLinearChain(params string[] prompts)
        {
            var plan = new OrchestratorPlan();
            OrchestratorNode? prev = null;

            for (int i = 0; i < prompts.Length; i++)
            {
                var node = new OrchestratorNode($"step_{i}", prompts[i]);
                if (prev != null) node.DependsOn.Add(prev.Id);
                plan.Nodes.Add(node);
                prev = node;
            }

            plan.EntryNodeId = "step_0";
            return plan;
        }

        /// <summary>
        /// Build a fan-out/fan-in pattern: one input prompt fans out to N parallel
        /// prompts, then an aggregator prompt combines their outputs.
        /// </summary>
        public static OrchestratorPlan BuildFanOutFanIn(
            string inputPrompt,
            string[] parallelPrompts,
            string aggregatorPrompt)
        {
            var plan = new OrchestratorPlan { EntryNodeId = "input" };
            plan.Nodes.Add(new OrchestratorNode("input", inputPrompt));

            var parallelIds = new List<string>();
            for (int i = 0; i < parallelPrompts.Length; i++)
            {
                var id = $"parallel_{i}";
                var node = new OrchestratorNode(id, parallelPrompts[i])
                {
                    DependsOn = { "input" }
                };
                plan.Nodes.Add(node);
                parallelIds.Add(id);
            }

            var agg = new OrchestratorNode("aggregator", aggregatorPrompt)
            {
                Type = OrchestratorNodeType.Aggregator,
                DependsOn = new List<string>(parallelIds)
            };
            plan.Nodes.Add(agg);

            return plan;
        }

        /// <summary>
        /// Build a conditional pipeline: a classifier prompt feeds a router that
        /// selects among branch prompts based on the router function.
        /// </summary>
        public static OrchestratorPlan BuildConditionalPipeline(
            string classifierPrompt,
            Dictionary<string, string> branchPrompts,
            Func<Dictionary<string, OrchestratorResult>, string> routerFunc)
        {
            var plan = new OrchestratorPlan { EntryNodeId = "classifier" };
            plan.Nodes.Add(new OrchestratorNode("classifier", classifierPrompt));

            var router = new OrchestratorNode("router", "Route based on: {classifier}")
            {
                Type = OrchestratorNodeType.Router,
                DependsOn = { "classifier" },
                RouterFunc = routerFunc
            };
            plan.Nodes.Add(router);

            foreach (var (branchId, branchPrompt) in branchPrompts)
            {
                plan.Nodes.Add(new OrchestratorNode(branchId, branchPrompt)
                {
                    DependsOn = { "router" }
                });
            }

            return plan;
        }
    }

    // ── Report Generator ─────────────────────────────────────

    /// <summary>
    /// Generates execution reports in multiple formats: text, Markdown, JSON, and Mermaid.
    /// </summary>
    public static class OrchestratorReport
    {
        /// <summary>Generate a plain text execution summary.</summary>
        public static string GenerateText(OrchestratorExecution execution)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"═══ Orchestration Report ═══");
            sb.AppendLine($"Execution: {execution.ExecutionId}");
            sb.AppendLine($"Status:    {execution.Status}");
            sb.AppendLine($"Duration:  {execution.TotalDuration.TotalMilliseconds:F0}ms");
            sb.AppendLine($"Nodes:     {execution.Results.Count}");
            sb.AppendLine();

            var criticalPath = execution.Plan.GetCriticalPath();
            sb.AppendLine($"Critical Path: {string.Join(" → ", criticalPath)}");
            sb.AppendLine();

            sb.AppendLine("── Node Results ──");
            foreach (var result in execution.Results.Values.OrderBy(r => r.CompletedAt))
            {
                var status = result.Success ? "✓" : "✗";
                var conf = result.Confidence.HasValue ? $" (conf: {result.Confidence:F2})" : "";
                sb.AppendLine($"  {status} {result.NodeId,-20} {result.Duration.TotalMilliseconds,6:F0}ms  attempt {result.Attempt}{conf}");
                if (!result.Success && result.Error != null)
                    sb.AppendLine($"    Error: {result.Error}");
            }

            sb.AppendLine();
            sb.AppendLine($"── Event Log ({execution.EventLog.Count} events) ──");
            foreach (var evt in execution.EventLog)
            {
                sb.AppendLine($"  [{evt.Timestamp:HH:mm:ss.fff}] {evt.Type,-20} {evt.Message}");
            }

            return sb.ToString();
        }

        /// <summary>Generate a Markdown execution report with tables and status badges.</summary>
        public static string GenerateMarkdown(OrchestratorExecution execution)
        {
            var sb = new StringBuilder();
            var statusEmoji = execution.Status switch
            {
                OrchestratorStatus.Completed => "✅",
                OrchestratorStatus.Failed => "❌",
                OrchestratorStatus.PartialSuccess => "⚠️",
                _ => "🔄"
            };

            sb.AppendLine($"# {statusEmoji} Orchestration Report");
            sb.AppendLine();
            sb.AppendLine($"| Field | Value |");
            sb.AppendLine($"|-------|-------|");
            sb.AppendLine($"| Execution ID | `{execution.ExecutionId}` |");
            sb.AppendLine($"| Status | **{execution.Status}** |");
            sb.AppendLine($"| Duration | {execution.TotalDuration.TotalMilliseconds:F0}ms |");
            sb.AppendLine($"| Nodes | {execution.Results.Count} |");
            sb.AppendLine();

            var cp = execution.Plan.GetCriticalPath();
            sb.AppendLine($"**Critical Path:** `{string.Join(" → ", cp)}`");
            sb.AppendLine();

            sb.AppendLine("## Node Results");
            sb.AppendLine();
            sb.AppendLine("| Node | Status | Duration | Attempt | Confidence |");
            sb.AppendLine("|------|--------|----------|---------|------------|");
            foreach (var r in execution.Results.Values.OrderBy(r => r.CompletedAt))
            {
                var s = r.Success ? "✅" : "❌";
                var c = r.Confidence.HasValue ? $"{r.Confidence:F2}" : "—";
                sb.AppendLine($"| {r.NodeId} | {s} | {r.Duration.TotalMilliseconds:F0}ms | {r.Attempt} | {c} |");
            }

            return sb.ToString();
        }

        /// <summary>Generate a JSON export of the execution.</summary>
        public static string GenerateJson(OrchestratorExecution execution)
        {
            var export = new
            {
                execution.ExecutionId,
                Status = execution.Status.ToString(),
                execution.StartedAt,
                execution.CompletedAt,
                TotalDurationMs = execution.TotalDuration.TotalMilliseconds,
                Results = execution.Results.Values.Select(r => new
                {
                    r.NodeId,
                    r.Output,
                    r.Success,
                    DurationMs = r.Duration.TotalMilliseconds,
                    r.Attempt,
                    r.Confidence,
                    r.Error,
                    r.CompletedAt
                }),
                Events = execution.EventLog.Select(e => new
                {
                    e.Timestamp,
                    e.NodeId,
                    Type = e.Type.ToString(),
                    e.Message
                })
            };

            return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Generate a Mermaid flowchart with color-coded node status.</summary>
        public static string GenerateMermaid(OrchestratorExecution execution)
        {
            var sb = new StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine("flowchart TD");

            foreach (var node in execution.Plan.Nodes)
            {
                var label = node.Id;
                if (node.Type != OrchestratorNodeType.Standard)
                    label += $"\\n({node.Type})";

                sb.AppendLine($"    {node.Id}[\"{label}\"]");
            }

            sb.AppendLine();

            foreach (var node in execution.Plan.Nodes)
                foreach (var dep in node.DependsOn)
                    sb.AppendLine($"    {dep} --> {node.Id}");

            sb.AppendLine();

            // Color coding
            foreach (var result in execution.Results.Values)
            {
                var style = result.Success
                    ? "fill:#2d6a2d,stroke:#333,color:#fff"
                    : "fill:#8b2222,stroke:#333,color:#fff";
                sb.AppendLine($"    style {result.NodeId} {style}");
            }

            // Skipped nodes (not in results)
            var resultIds = new HashSet<string>(execution.Results.Keys);
            foreach (var node in execution.Plan.Nodes.Where(n => !resultIds.Contains(n.Id)))
            {
                sb.AppendLine($"    style {node.Id} fill:#555,stroke:#333,color:#aaa");
            }

            sb.AppendLine("```");
            return sb.ToString();
        }
    }
}
