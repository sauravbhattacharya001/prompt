namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ── Enums ────────────────────────────────────────────────────────

    /// <summary>
    /// Execution status of a plan task.
    /// </summary>
    public enum PlanTaskStatus
    {
        /// <summary>Waiting for dependencies.</summary>
        Pending,
        /// <summary>All dependencies met, ready to execute.</summary>
        Ready,
        /// <summary>Currently executing.</summary>
        Running,
        /// <summary>Completed successfully.</summary>
        Done,
        /// <summary>Failed during execution.</summary>
        Failed,
        /// <summary>Skipped (dependency failed or user override).</summary>
        Skipped
    }

    /// <summary>
    /// Complexity estimate for a plan task.
    /// </summary>
    public enum TaskComplexity
    {
        /// <summary>Simple, single-turn prompt.</summary>
        Trivial,
        /// <summary>Moderate work, may need context.</summary>
        Moderate,
        /// <summary>Complex, multi-step reasoning.</summary>
        Complex,
        /// <summary>Very difficult, may need chain-of-thought or multiple attempts.</summary>
        Expert
    }

    /// <summary>
    /// Strategy for decomposing goals into sub-tasks.
    /// </summary>
    public enum DecompositionStrategy
    {
        /// <summary>Break down by sequential steps.</summary>
        Sequential,
        /// <summary>Maximize parallel independent sub-tasks.</summary>
        Parallel,
        /// <summary>Depth-first recursive decomposition.</summary>
        Recursive,
        /// <summary>Balanced mix of parallel and sequential.</summary>
        Balanced
    }

    // ── Models ───────────────────────────────────────────────────────

    /// <summary>
    /// A single task within an execution plan, including the generated prompt.
    /// </summary>
    public class PlanTask
    {
        /// <summary>Unique identifier for this task.</summary>
        public string Id { get; set; } = "";

        /// <summary>Human-readable task name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Detailed description of what this task accomplishes.</summary>
        public string Description { get; set; } = "";

        /// <summary>The generated prompt text for this task.</summary>
        public string Prompt { get; set; } = "";

        /// <summary>IDs of tasks that must complete before this one.</summary>
        public List<string> DependsOn { get; set; } = new List<string>();

        /// <summary>Estimated complexity.</summary>
        public TaskComplexity Complexity { get; set; } = TaskComplexity.Moderate;

        /// <summary>Current execution status.</summary>
        public PlanTaskStatus Status { get; set; } = PlanTaskStatus.Pending;

        /// <summary>Output/result text from execution (null if not executed).</summary>
        public string? Result { get; set; }

        /// <summary>Error message if failed.</summary>
        public string? Error { get; set; }

        /// <summary>Estimated token cost for this task's prompt.</summary>
        public int EstimatedTokens { get; set; }

        /// <summary>Execution order (0-based, tasks at same level can run in parallel).</summary>
        public int Level { get; set; }

        /// <summary>Tags for categorization.</summary>
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// A complete execution plan: a DAG of tasks derived from a goal.
    /// </summary>
    public class ExecutionPlan
    {
        /// <summary>Unique plan identifier.</summary>
        public string PlanId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>The original goal text.</summary>
        public string Goal { get; set; } = "";

        /// <summary>Strategy used for decomposition.</summary>
        public DecompositionStrategy Strategy { get; set; }

        /// <summary>All tasks in the plan.</summary>
        public List<PlanTask> Tasks { get; set; } = new List<PlanTask>();

        /// <summary>When the plan was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Total estimated tokens across all tasks.</summary>
        public int TotalEstimatedTokens => Tasks.Sum(t => t.EstimatedTokens);

        /// <summary>Number of execution levels (critical path length).</summary>
        public int CriticalPathLength => Tasks.Count > 0 ? Tasks.Max(t => t.Level) + 1 : 0;

        /// <summary>Maximum tasks that can run in parallel at any level.</summary>
        public int MaxParallelism => Tasks.Count > 0
            ? Tasks.GroupBy(t => t.Level).Max(g => g.Count())
            : 0;

        /// <summary>Fraction of tasks completed (0.0–1.0).</summary>
        public double Progress => Tasks.Count > 0
            ? (double)Tasks.Count(t => t.Status == PlanTaskStatus.Done) / Tasks.Count
            : 0;

        /// <summary>Whether the entire plan has finished (all done or failed/skipped).</summary>
        public bool IsComplete => Tasks.All(t =>
            t.Status == PlanTaskStatus.Done ||
            t.Status == PlanTaskStatus.Failed ||
            t.Status == PlanTaskStatus.Skipped);

        /// <summary>Tasks that are ready to execute right now.</summary>
        public IReadOnlyList<PlanTask> ReadyTasks => Tasks
            .Where(t => t.Status == PlanTaskStatus.Ready)
            .ToList();

        /// <summary>Tasks that failed.</summary>
        public IReadOnlyList<PlanTask> FailedTasks => Tasks
            .Where(t => t.Status == PlanTaskStatus.Failed)
            .ToList();
    }

    /// <summary>
    /// Snapshot of plan state changes after an advancement step.
    /// </summary>
    public class PlanAdvancement
    {
        /// <summary>Tasks that became ready in this step.</summary>
        public List<PlanTask> NewlyReady { get; set; } = new List<PlanTask>();

        /// <summary>Tasks that were skipped due to failed dependencies.</summary>
        public List<PlanTask> Skipped { get; set; } = new List<PlanTask>();

        /// <summary>Whether the plan is now complete.</summary>
        public bool PlanComplete { get; set; }

        /// <summary>Current overall progress (0.0–1.0).</summary>
        public double Progress { get; set; }
    }

    /// <summary>
    /// Validation result for a plan.
    /// </summary>
    public class PlanValidation
    {
        /// <summary>Whether the plan is valid.</summary>
        public bool IsValid { get; set; }

        /// <summary>Validation issues found.</summary>
        public List<string> Issues { get; set; } = new List<string>();
    }

    // ── Goal Planner ─────────────────────────────────────────────────

    /// <summary>
    /// Goal-oriented prompt decomposition engine. Takes a high-level objective
    /// and breaks it into a directed acyclic graph (DAG) of sub-task prompts
    /// with dependency management, parallel execution tracking, and adaptive
    /// re-planning on failure.
    ///
    /// <para><b>Agentic capabilities:</b></para>
    /// <list type="bullet">
    ///   <item>Autonomous decomposition — analyzes goals and generates sub-tasks</item>
    ///   <item>Dependency-aware scheduling — tracks ready/blocked tasks</item>
    ///   <item>Failure propagation — skips downstream tasks on failure</item>
    ///   <item>Adaptive re-planning — generates recovery plans for failed tasks</item>
    ///   <item>Progress monitoring — tracks completion and critical path</item>
    /// </list>
    ///
    /// <example>
    /// <code>
    /// var planner = new PromptGoalPlanner();
    /// var plan = planner.Decompose("Build a REST API for user management",
    ///     DecompositionStrategy.Balanced);
    ///
    /// // Get ready tasks
    /// var ready = plan.ReadyTasks;
    ///
    /// // Mark task complete
    /// planner.CompleteTask(plan, ready[0].Id, "Schema designed: users table with ...");
    ///
    /// // Advance to unlock dependent tasks
    /// var advancement = planner.Advance(plan);
    ///
    /// // Export plan as Markdown
    /// string md = planner.ExportMarkdown(plan);
    /// </code>
    /// </example>
    /// </summary>
    public class PromptGoalPlanner
    {
        private static readonly Random _rng = new Random();

        // ── Keyword → decomposition templates ───────────────────────

        private static readonly Dictionary<string, List<(string name, string desc, TaskComplexity complexity, string[] tags)>>
            _domainTemplates = new Dictionary<string, List<(string, string, TaskComplexity, string[])>>
            (StringComparer.OrdinalIgnoreCase)
        {
            ["api"] = new List<(string, string, TaskComplexity, string[])>
            {
                ("Define data models", "Design the data structures and schemas needed", TaskComplexity.Moderate, new[] { "design", "data" }),
                ("Design API endpoints", "Specify routes, methods, request/response shapes", TaskComplexity.Moderate, new[] { "design", "api" }),
                ("Implement core logic", "Build the business logic layer", TaskComplexity.Complex, new[] { "implement" }),
                ("Add input validation", "Validate all inputs with clear error messages", TaskComplexity.Moderate, new[] { "implement", "validation" }),
                ("Write error handling", "Handle edge cases and return proper status codes", TaskComplexity.Moderate, new[] { "implement", "errors" }),
                ("Create documentation", "Document endpoints, examples, and usage", TaskComplexity.Trivial, new[] { "docs" }),
            },
            ["analysis"] = new List<(string, string, TaskComplexity, string[])>
            {
                ("Gather requirements", "Clarify what needs to be analyzed and why", TaskComplexity.Trivial, new[] { "planning" }),
                ("Collect data sources", "Identify and describe available data", TaskComplexity.Moderate, new[] { "data" }),
                ("Perform analysis", "Run the core analytical work", TaskComplexity.Complex, new[] { "analysis" }),
                ("Validate findings", "Cross-check results for accuracy", TaskComplexity.Moderate, new[] { "validation" }),
                ("Synthesize report", "Combine findings into actionable recommendations", TaskComplexity.Moderate, new[] { "report" }),
            },
            ["app"] = new List<(string, string, TaskComplexity, string[])>
            {
                ("Define requirements", "Clarify features, constraints, and user needs", TaskComplexity.Trivial, new[] { "planning" }),
                ("Design architecture", "Plan components, data flow, and interfaces", TaskComplexity.Complex, new[] { "design" }),
                ("Build UI components", "Create the user interface elements", TaskComplexity.Complex, new[] { "implement", "ui" }),
                ("Implement business logic", "Build core functionality", TaskComplexity.Complex, new[] { "implement" }),
                ("Add state management", "Handle data flow and persistence", TaskComplexity.Moderate, new[] { "implement", "data" }),
                ("Test and refine", "Verify functionality and fix issues", TaskComplexity.Moderate, new[] { "test" }),
            },
            ["write"] = new List<(string, string, TaskComplexity, string[])>
            {
                ("Research topic", "Gather background information and sources", TaskComplexity.Moderate, new[] { "research" }),
                ("Create outline", "Structure the content with clear sections", TaskComplexity.Trivial, new[] { "planning" }),
                ("Write first draft", "Produce the initial content", TaskComplexity.Complex, new[] { "writing" }),
                ("Review and edit", "Refine clarity, tone, and accuracy", TaskComplexity.Moderate, new[] { "editing" }),
                ("Finalize", "Polish formatting and prepare for delivery", TaskComplexity.Trivial, new[] { "finishing" }),
            },
            ["debug"] = new List<(string, string, TaskComplexity, string[])>
            {
                ("Reproduce the issue", "Identify exact steps and conditions", TaskComplexity.Moderate, new[] { "investigation" }),
                ("Analyze root cause", "Trace the failure to its origin", TaskComplexity.Complex, new[] { "analysis" }),
                ("Develop fix", "Implement the correction", TaskComplexity.Moderate, new[] { "implement" }),
                ("Verify fix", "Confirm the issue is resolved without regressions", TaskComplexity.Moderate, new[] { "test" }),
            },
            ["learn"] = new List<(string, string, TaskComplexity, string[])>
            {
                ("Survey the topic", "Get a high-level overview of key concepts", TaskComplexity.Trivial, new[] { "research" }),
                ("Study core concepts", "Deep-dive into fundamental principles", TaskComplexity.Complex, new[] { "learning" }),
                ("Practice with examples", "Apply concepts to concrete problems", TaskComplexity.Complex, new[] { "practice" }),
                ("Build a project", "Create something that demonstrates understanding", TaskComplexity.Expert, new[] { "project" }),
                ("Review and solidify", "Summarize learnings and identify gaps", TaskComplexity.Moderate, new[] { "review" }),
            },
        };

        // Keyword detection for domain matching
        private static readonly Dictionary<string, string[]> _domainKeywords =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["api"] = new[] { "api", "rest", "endpoint", "server", "backend", "service", "microservice" },
            ["analysis"] = new[] { "analyze", "analysis", "evaluate", "assess", "investigate", "audit", "review data" },
            ["app"] = new[] { "app", "application", "build", "create", "develop", "implement", "tool", "system" },
            ["write"] = new[] { "write", "draft", "compose", "essay", "article", "blog", "report", "document" },
            ["debug"] = new[] { "debug", "fix", "bug", "error", "issue", "broken", "failing", "crash" },
            ["learn"] = new[] { "learn", "study", "understand", "tutorial", "course", "teach", "explain" },
        };

        // ── Decompose ───────────────────────────────────────────────

        /// <summary>
        /// Decomposes a high-level goal into an execution plan of sub-task prompts.
        /// Uses keyword analysis to detect the domain and applies appropriate templates,
        /// then generates contextual prompts for each task.
        /// </summary>
        /// <param name="goal">The high-level objective to decompose.</param>
        /// <param name="strategy">Decomposition strategy.</param>
        /// <param name="maxTasks">Maximum number of tasks (3–20). Default 8.</param>
        /// <returns>A complete execution plan.</returns>
        public ExecutionPlan Decompose(string goal, DecompositionStrategy strategy = DecompositionStrategy.Balanced, int maxTasks = 8)
        {
            if (string.IsNullOrWhiteSpace(goal))
                throw new ArgumentException("Goal cannot be empty.", nameof(goal));
            if (maxTasks < 3 || maxTasks > 20)
                throw new ArgumentOutOfRangeException(nameof(maxTasks), "Must be 3–20.");

            var plan = new ExecutionPlan
            {
                Goal = goal.Trim(),
                Strategy = strategy
            };

            // Detect domain
            var domain = DetectDomain(goal);
            var templates = _domainTemplates.ContainsKey(domain)
                ? _domainTemplates[domain]
                : _domainTemplates["app"]; // default fallback

            // Limit tasks
            var selectedTemplates = templates.Take(maxTasks).ToList();

            // Generate tasks with dependencies based on strategy
            for (int i = 0; i < selectedTemplates.Count; i++)
            {
                var (name, desc, complexity, tags) = selectedTemplates[i];
                var task = new PlanTask
                {
                    Id = $"task-{i + 1}",
                    Name = name,
                    Description = desc,
                    Complexity = complexity,
                    Tags = new List<string>(tags),
                    EstimatedTokens = EstimateTokensForComplexity(complexity, goal.Length)
                };

                // Assign dependencies based on strategy
                switch (strategy)
                {
                    case DecompositionStrategy.Sequential:
                        if (i > 0)
                            task.DependsOn.Add($"task-{i}");
                        task.Level = i;
                        break;

                    case DecompositionStrategy.Parallel:
                        // Everything independent except last task depends on all others
                        if (i == selectedTemplates.Count - 1 && i > 0)
                        {
                            for (int j = 0; j < i; j++)
                                task.DependsOn.Add($"task-{j + 1}");
                            task.Level = 1;
                        }
                        else
                        {
                            task.Level = 0;
                        }
                        break;

                    case DecompositionStrategy.Recursive:
                        // Each task depends on previous, but every 3rd task is a checkpoint
                        if (i > 0)
                        {
                            if (i % 3 == 0)
                            {
                                // Checkpoint: depends on all previous in this batch
                                for (int j = Math.Max(0, i - 3); j < i; j++)
                                    task.DependsOn.Add($"task-{j + 1}");
                            }
                            else
                            {
                                task.DependsOn.Add($"task-{i}");
                            }
                        }
                        task.Level = ComputeLevel(task, plan.Tasks);
                        break;

                    case DecompositionStrategy.Balanced:
                    default:
                        // First task independent; tasks 2-3 depend on 1; rest on prior
                        if (i == 0)
                        {
                            task.Level = 0;
                        }
                        else if (i <= 2)
                        {
                            task.DependsOn.Add("task-1");
                            task.Level = 1;
                        }
                        else
                        {
                            task.DependsOn.Add($"task-{i}");
                            task.Level = ComputeLevel(task, plan.Tasks);
                        }
                        break;
                }

                // Generate the actual prompt
                task.Prompt = GenerateTaskPrompt(goal, task, plan.Tasks);

                plan.Tasks.Add(task);
            }

            // Set initial ready tasks
            RefreshReadyTasks(plan);

            return plan;
        }

        // ── Task Lifecycle ──────────────────────────────────────────

        /// <summary>
        /// Marks a task as complete with its result and advances the plan.
        /// </summary>
        public PlanAdvancement CompleteTask(ExecutionPlan plan, string taskId, string result)
        {
            var task = FindTask(plan, taskId);
            if (task.Status != PlanTaskStatus.Ready && task.Status != PlanTaskStatus.Running)
                throw new InvalidOperationException(
                    $"Task '{taskId}' is {task.Status}, cannot complete.");

            task.Status = PlanTaskStatus.Done;
            task.Result = result ?? "";

            return Advance(plan);
        }

        /// <summary>
        /// Marks a task as failed and propagates failure to dependents.
        /// </summary>
        public PlanAdvancement FailTask(ExecutionPlan plan, string taskId, string error)
        {
            var task = FindTask(plan, taskId);
            if (task.Status != PlanTaskStatus.Ready && task.Status != PlanTaskStatus.Running)
                throw new InvalidOperationException(
                    $"Task '{taskId}' is {task.Status}, cannot fail.");

            task.Status = PlanTaskStatus.Failed;
            task.Error = error ?? "Unknown error";

            return Advance(plan);
        }

        /// <summary>
        /// Marks a task as running.
        /// </summary>
        public void StartTask(ExecutionPlan plan, string taskId)
        {
            var task = FindTask(plan, taskId);
            if (task.Status != PlanTaskStatus.Ready)
                throw new InvalidOperationException(
                    $"Task '{taskId}' is {task.Status}, must be Ready to start.");

            task.Status = PlanTaskStatus.Running;
        }

        /// <summary>
        /// Re-evaluates the plan: unlocks ready tasks, skips tasks with failed dependencies.
        /// </summary>
        public PlanAdvancement Advance(ExecutionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var advancement = new PlanAdvancement();
            var lookup = BuildTaskLookup(plan);

            // Skip tasks whose dependencies failed
            bool changed;
            do
            {
                changed = false;
                foreach (var task in plan.Tasks.Where(t => t.Status == PlanTaskStatus.Pending))
                {
                    if (AnyDependencyFailed(task, lookup))
                    {
                        task.Status = PlanTaskStatus.Skipped;
                        task.Error = "Skipped: dependency failed";
                        advancement.Skipped.Add(task);
                        changed = true;
                    }
                }
            } while (changed);

            // Unlock newly ready tasks
            foreach (var task in plan.Tasks.Where(t => t.Status == PlanTaskStatus.Pending))
            {
                if (AllDependenciesDone(task, lookup))
                {
                    task.Status = PlanTaskStatus.Ready;
                    advancement.NewlyReady.Add(task);
                }
            }

            advancement.PlanComplete = plan.IsComplete;
            advancement.Progress = plan.Progress;

            return advancement;
        }

        // ── Re-planning ─────────────────────────────────────────────

        /// <summary>
        /// Generates a recovery plan for a failed task by creating alternative
        /// sub-tasks that approach the problem differently.
        /// </summary>
        public List<PlanTask> GenerateRecoveryTasks(ExecutionPlan plan, string failedTaskId)
        {
            var failed = FindTask(plan, failedTaskId);
            if (failed.Status != PlanTaskStatus.Failed)
                throw new InvalidOperationException("Task must be Failed to generate recovery.");

            var recoveryTasks = new List<PlanTask>();
            var baseId = $"{failed.Id}-recovery";

            // Recovery strategy 1: Simplify the task
            recoveryTasks.Add(new PlanTask
            {
                Id = $"{baseId}-simplify",
                Name = $"Simplified: {failed.Name}",
                Description = $"A simpler approach to: {failed.Description}",
                Complexity = DowngradeComplexity(failed.Complexity),
                DependsOn = new List<string>(failed.DependsOn),
                Tags = new List<string>(failed.Tags) { "recovery", "simplified" },
                EstimatedTokens = failed.EstimatedTokens / 2,
                Level = failed.Level,
                Prompt = GenerateRecoveryPrompt(plan.Goal, failed, "simplify")
            });

            // Recovery strategy 2: Decompose further
            recoveryTasks.Add(new PlanTask
            {
                Id = $"{baseId}-part1",
                Name = $"{failed.Name} (Part A)",
                Description = $"First half of: {failed.Description}",
                Complexity = DowngradeComplexity(failed.Complexity),
                DependsOn = new List<string>(failed.DependsOn),
                Tags = new List<string>(failed.Tags) { "recovery", "decomposed" },
                EstimatedTokens = failed.EstimatedTokens * 2 / 3,
                Level = failed.Level,
                Prompt = GenerateRecoveryPrompt(plan.Goal, failed, "decompose-a")
            });

            recoveryTasks.Add(new PlanTask
            {
                Id = $"{baseId}-part2",
                Name = $"{failed.Name} (Part B)",
                Description = $"Second half of: {failed.Description}",
                Complexity = DowngradeComplexity(failed.Complexity),
                DependsOn = new List<string> { $"{baseId}-part1" },
                Tags = new List<string>(failed.Tags) { "recovery", "decomposed" },
                EstimatedTokens = failed.EstimatedTokens * 2 / 3,
                Level = failed.Level + 1,
                Prompt = GenerateRecoveryPrompt(plan.Goal, failed, "decompose-b")
            });

            return recoveryTasks;
        }

        /// <summary>
        /// Applies recovery tasks to the plan, replacing the failed task's
        /// role in the dependency graph.
        /// </summary>
        public void ApplyRecovery(ExecutionPlan plan, string failedTaskId, List<PlanTask> recoveryTasks)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (recoveryTasks == null || recoveryTasks.Count == 0)
                throw new ArgumentException("Recovery tasks cannot be empty.");

            var failed = FindTask(plan, failedTaskId);

            // Find last recovery task (the one downstream tasks should depend on)
            var lastRecovery = recoveryTasks.Last();

            // Rewire dependents: anything that depended on the failed task
            // now depends on the last recovery task
            foreach (var task in plan.Tasks)
            {
                var idx = task.DependsOn.IndexOf(failedTaskId);
                if (idx >= 0)
                {
                    task.DependsOn[idx] = lastRecovery.Id;
                }
            }

            // Add recovery tasks to plan
            plan.Tasks.AddRange(recoveryTasks);

            // Refresh ready states
            RefreshReadyTasks(plan);
        }

        // ── Validation ──────────────────────────────────────────────

        /// <summary>
        /// Validates a plan for structural issues (cycles, missing deps, etc.).
        /// </summary>
        public PlanValidation Validate(ExecutionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var result = new PlanValidation { IsValid = true };
            var ids = new HashSet<string>(plan.Tasks.Select(t => t.Id));

            // Check for duplicate IDs
            if (ids.Count != plan.Tasks.Count)
            {
                result.IsValid = false;
                result.Issues.Add("Duplicate task IDs detected.");
            }

            // Check for missing dependencies
            foreach (var task in plan.Tasks)
            {
                foreach (var dep in task.DependsOn)
                {
                    if (!ids.Contains(dep))
                    {
                        result.IsValid = false;
                        result.Issues.Add($"Task '{task.Id}' depends on missing task '{dep}'.");
                    }
                }
            }

            // Check for cycles (DFS)
            if (HasCycle(plan))
            {
                result.IsValid = false;
                result.Issues.Add("Dependency cycle detected.");
            }

            // Check for empty prompts
            foreach (var task in plan.Tasks.Where(t => string.IsNullOrWhiteSpace(t.Prompt)))
            {
                result.Issues.Add($"Task '{task.Id}' has an empty prompt.");
            }

            // Check no task depends on itself
            foreach (var task in plan.Tasks.Where(t => t.DependsOn.Contains(t.Id)))
            {
                result.IsValid = false;
                result.Issues.Add($"Task '{task.Id}' depends on itself.");
            }

            if (result.Issues.Count == 0)
                result.Issues.Add("No issues found.");

            return result;
        }

        // ── Export ───────────────────────────────────────────────────

        /// <summary>
        /// Exports the plan as a Markdown document with task details and dependency graph.
        /// </summary>
        public string ExportMarkdown(ExecutionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var sb = new StringBuilder();
            sb.AppendLine($"# Execution Plan: {plan.Goal}");
            sb.AppendLine();
            sb.AppendLine($"**Plan ID:** `{plan.PlanId}`  ");
            sb.AppendLine($"**Strategy:** {plan.Strategy}  ");
            sb.AppendLine($"**Tasks:** {plan.Tasks.Count}  ");
            sb.AppendLine($"**Critical Path:** {plan.CriticalPathLength} levels  ");
            sb.AppendLine($"**Max Parallelism:** {plan.MaxParallelism}  ");
            sb.AppendLine($"**Est. Total Tokens:** ~{plan.TotalEstimatedTokens:N0}  ");
            sb.AppendLine($"**Progress:** {plan.Progress:P0}  ");
            sb.AppendLine();

            // Summary bar
            var done = plan.Tasks.Count(t => t.Status == PlanTaskStatus.Done);
            var failed = plan.Tasks.Count(t => t.Status == PlanTaskStatus.Failed);
            var skipped = plan.Tasks.Count(t => t.Status == PlanTaskStatus.Skipped);
            var ready = plan.Tasks.Count(t => t.Status == PlanTaskStatus.Ready);
            var pending = plan.Tasks.Count(t => t.Status == PlanTaskStatus.Pending);
            sb.AppendLine($"✅ Done: {done} | 🔴 Failed: {failed} | ⏭ Skipped: {skipped} | 🟢 Ready: {ready} | ⏳ Pending: {pending}");
            sb.AppendLine();

            // Dependency graph (Mermaid)
            sb.AppendLine("## Dependency Graph");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");
            foreach (var task in plan.Tasks)
            {
                var statusIcon = task.Status switch
                {
                    PlanTaskStatus.Done => "✅",
                    PlanTaskStatus.Failed => "🔴",
                    PlanTaskStatus.Skipped => "⏭",
                    PlanTaskStatus.Ready => "🟢",
                    PlanTaskStatus.Running => "🔄",
                    _ => "⏳"
                };
                sb.AppendLine($"    {task.Id}[\"{statusIcon} {EscapeMermaid(task.Name)}\"]");
                foreach (var dep in task.DependsOn)
                    sb.AppendLine($"    {dep} --> {task.Id}");
            }
            sb.AppendLine("```");
            sb.AppendLine();

            // Task details
            sb.AppendLine("## Tasks");
            sb.AppendLine();
            foreach (var task in plan.Tasks.OrderBy(t => t.Level).ThenBy(t => t.Id))
            {
                sb.AppendLine($"### {task.Id}: {task.Name}");
                sb.AppendLine();
                sb.AppendLine($"- **Status:** {task.Status}");
                sb.AppendLine($"- **Complexity:** {task.Complexity}");
                sb.AppendLine($"- **Level:** {task.Level}");
                sb.AppendLine($"- **Est. Tokens:** ~{task.EstimatedTokens:N0}");
                if (task.DependsOn.Count > 0)
                    sb.AppendLine($"- **Depends On:** {string.Join(", ", task.DependsOn)}");
                if (task.Tags.Count > 0)
                    sb.AppendLine($"- **Tags:** {string.Join(", ", task.Tags)}");
                sb.AppendLine();
                sb.AppendLine("**Prompt:**");
                sb.AppendLine("```");
                sb.AppendLine(task.Prompt);
                sb.AppendLine("```");
                if (!string.IsNullOrEmpty(task.Result))
                {
                    sb.AppendLine();
                    sb.AppendLine("**Result:**");
                    sb.AppendLine($"> {task.Result.Replace("\n", "\n> ")}");
                }
                if (!string.IsNullOrEmpty(task.Error))
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Error:** {task.Error}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the plan as a JSON-like structured string.
        /// </summary>
        public string ExportJson(ExecutionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"planId\": \"{plan.PlanId}\",");
            sb.AppendLine($"  \"goal\": \"{EscapeJson(plan.Goal)}\",");
            sb.AppendLine($"  \"strategy\": \"{plan.Strategy}\",");
            sb.AppendLine($"  \"criticalPathLength\": {plan.CriticalPathLength},");
            sb.AppendLine($"  \"maxParallelism\": {plan.MaxParallelism},");
            sb.AppendLine($"  \"totalEstimatedTokens\": {plan.TotalEstimatedTokens},");
            sb.AppendLine($"  \"progress\": {plan.Progress:F2},");
            sb.AppendLine($"  \"isComplete\": {plan.IsComplete.ToString().ToLower()},");
            sb.AppendLine($"  \"tasks\": [");

            for (int i = 0; i < plan.Tasks.Count; i++)
            {
                var t = plan.Tasks[i];
                var comma = i < plan.Tasks.Count - 1 ? "," : "";
                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": \"{t.Id}\",");
                sb.AppendLine($"      \"name\": \"{EscapeJson(t.Name)}\",");
                sb.AppendLine($"      \"status\": \"{t.Status}\",");
                sb.AppendLine($"      \"complexity\": \"{t.Complexity}\",");
                sb.AppendLine($"      \"level\": {t.Level},");
                sb.AppendLine($"      \"estimatedTokens\": {t.EstimatedTokens},");
                sb.AppendLine($"      \"dependsOn\": [{string.Join(", ", t.DependsOn.Select(d => $"\"{d}\""))}],");
                sb.AppendLine($"      \"tags\": [{string.Join(", ", t.Tags.Select(tg => $"\"{EscapeJson(tg)}\""))}],");
                sb.AppendLine($"      \"prompt\": \"{EscapeJson(t.Prompt)}\"");
                sb.AppendLine($"    }}{comma}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── Private Helpers ─────────────────────────────────────────

        private string DetectDomain(string goal)
        {
            var lower = goal.ToLowerInvariant();
            string bestDomain = "app";
            int bestScore = 0;

            foreach (var kvp in _domainKeywords)
            {
                int score = kvp.Value.Count(kw => lower.Contains(kw));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDomain = kvp.Key;
                }
            }

            return bestDomain;
        }

        private string GenerateTaskPrompt(string goal, PlanTask task, List<PlanTask> existingTasks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Task: {task.Name}");
            sb.AppendLine();
            sb.AppendLine($"**Overall Goal:** {goal}");
            sb.AppendLine();
            sb.AppendLine($"**Your Assignment:** {task.Description}");
            sb.AppendLine();

            // Add context from dependencies
            if (task.DependsOn.Count > 0)
            {
                sb.AppendLine("**Context from prerequisite tasks:**");
                foreach (var depId in task.DependsOn)
                {
                    var dep = existingTasks.FirstOrDefault(t => t.Id == depId);
                    if (dep != null)
                    {
                        sb.AppendLine($"- *{dep.Name}*: {dep.Description}");
                        if (!string.IsNullOrEmpty(dep.Result))
                            sb.AppendLine($"  Result: {dep.Result}");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("**Instructions:**");
            sb.AppendLine($"1. Focus specifically on: {task.Description}");
            sb.AppendLine("2. Be thorough but concise.");
            sb.AppendLine("3. Output should be directly usable by the next task in the pipeline.");

            if (task.Complexity >= TaskComplexity.Complex)
            {
                sb.AppendLine("4. This is a complex task — break your thinking into clear steps.");
                sb.AppendLine("5. Consider edge cases and potential issues.");
            }

            return sb.ToString().Trim();
        }

        private string GenerateRecoveryPrompt(string goal, PlanTask failed, string recoveryType)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Recovery Task: {failed.Name}");
            sb.AppendLine();
            sb.AppendLine($"**Overall Goal:** {goal}");
            sb.AppendLine();
            sb.AppendLine($"**Previous attempt failed:** {failed.Error}");
            sb.AppendLine();

            switch (recoveryType)
            {
                case "simplify":
                    sb.AppendLine("**Strategy:** Simplify the approach. Use a more straightforward method.");
                    sb.AppendLine($"**Original task:** {failed.Description}");
                    sb.AppendLine();
                    sb.AppendLine("Try a simpler approach that avoids the issue encountered.");
                    sb.AppendLine("Prioritize getting a working result over perfection.");
                    break;
                case "decompose-a":
                    sb.AppendLine("**Strategy:** This is Part A of a decomposed retry.");
                    sb.AppendLine($"**Original task:** {failed.Description}");
                    sb.AppendLine();
                    sb.AppendLine("Handle only the first half of the original task.");
                    sb.AppendLine("Focus on the foundational work that Part B will build on.");
                    break;
                case "decompose-b":
                    sb.AppendLine("**Strategy:** This is Part B of a decomposed retry.");
                    sb.AppendLine($"**Original task:** {failed.Description}");
                    sb.AppendLine();
                    sb.AppendLine("Build on Part A's output to complete the remaining work.");
                    break;
            }

            return sb.ToString().Trim();
        }

        private static int EstimateTokensForComplexity(TaskComplexity complexity, int goalLength)
        {
            int baseTokens = complexity switch
            {
                TaskComplexity.Trivial => 200,
                TaskComplexity.Moderate => 500,
                TaskComplexity.Complex => 1000,
                TaskComplexity.Expert => 2000,
                _ => 500
            };

            // Longer goals produce more detailed prompts
            return baseTokens + Math.Min(goalLength, 200);
        }

        private static TaskComplexity DowngradeComplexity(TaskComplexity c)
        {
            return c switch
            {
                TaskComplexity.Expert => TaskComplexity.Complex,
                TaskComplexity.Complex => TaskComplexity.Moderate,
                TaskComplexity.Moderate => TaskComplexity.Trivial,
                _ => TaskComplexity.Trivial
            };
        }

        private int ComputeLevel(PlanTask task, List<PlanTask> existingTasks)
        {
            if (task.DependsOn.Count == 0) return 0;

            int maxDepLevel = 0;
            foreach (var depId in task.DependsOn)
            {
                var dep = existingTasks.FirstOrDefault(t => t.Id == depId);
                if (dep != null && dep.Level >= maxDepLevel)
                    maxDepLevel = dep.Level + 1;
            }
            return maxDepLevel;
        }

        private void RefreshReadyTasks(ExecutionPlan plan)
        {
            var lookup = BuildTaskLookup(plan);
            foreach (var task in plan.Tasks.Where(t => t.Status == PlanTaskStatus.Pending))
            {
                if (task.DependsOn.Count == 0)
                {
                    task.Status = PlanTaskStatus.Ready;
                    continue;
                }

                if (AllDependenciesDone(task, lookup))
                    task.Status = PlanTaskStatus.Ready;
            }
        }

        private PlanTask FindTask(ExecutionPlan plan, string taskId)
        {
            return plan.Tasks.FirstOrDefault(t => t.Id == taskId)
                ?? throw new ArgumentException($"Task '{taskId}' not found in plan.");
        }

        /// <summary>Build an O(1) lookup dictionary from task ID to PlanTask.</summary>
        private static Dictionary<string, PlanTask> BuildTaskLookup(ExecutionPlan plan)
            => plan.Tasks.ToDictionary(t => t.Id);

        /// <summary>Returns true if all dependencies are in Done state (O(D) with dictionary lookup).</summary>
        private static bool AllDependenciesDone(PlanTask task, Dictionary<string, PlanTask> lookup)
        {
            foreach (var depId in task.DependsOn)
            {
                if (!lookup.TryGetValue(depId, out var dep) || dep.Status != PlanTaskStatus.Done)
                    return false;
            }
            return true;
        }

        /// <summary>Returns true if any dependency is Failed or Skipped (O(D) with dictionary lookup).</summary>
        private static bool AnyDependencyFailed(PlanTask task, Dictionary<string, PlanTask> lookup)
        {
            foreach (var depId in task.DependsOn)
            {
                if (lookup.TryGetValue(depId, out var dep) &&
                    (dep.Status == PlanTaskStatus.Failed || dep.Status == PlanTaskStatus.Skipped))
                    return true;
            }
            return false;
        }

        private bool HasCycle(ExecutionPlan plan)
        {
            var visited = new HashSet<string>();
            var stack = new HashSet<string>();
            var lookup = plan.Tasks.ToDictionary(t => t.Id);

            foreach (var task in plan.Tasks)
            {
                if (DfsCycleCheck(task.Id, lookup, visited, stack))
                    return true;
            }
            return false;
        }

        private bool DfsCycleCheck(string id, Dictionary<string, PlanTask> lookup,
            HashSet<string> visited, HashSet<string> stack)
        {
            if (stack.Contains(id)) return true;
            if (visited.Contains(id)) return false;

            visited.Add(id);
            stack.Add(id);

            if (lookup.TryGetValue(id, out var task))
            {
                foreach (var dep in task.DependsOn)
                {
                    if (DfsCycleCheck(dep, lookup, visited, stack))
                        return true;
                }
            }

            stack.Remove(id);
            return false;
        }

        private static string EscapeMermaid(string s)
            => s.Replace("\"", "&quot;").Replace("[", "&#91;").Replace("]", "&#93;");

        private static string EscapeJson(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
