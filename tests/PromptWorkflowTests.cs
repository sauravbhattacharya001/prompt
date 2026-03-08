namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class PromptWorkflowTests
    {
        // ── Helpers ──────────────────────────────────────────────────

        private static Func<string, Task<string>> Echo =>
            prompt => Task.FromResult($"[echo:{prompt}]");

        private static Func<string, Task<string>> Upper =>
            prompt => Task.FromResult(prompt.ToUpperInvariant());

        private static Func<string, string> EchoSync =>
            prompt => $"[sync:{prompt}]";

        private static PromptTemplate T(string template) =>
            new PromptTemplate(template);

        // ── Construction ─────────────────────────────────────────────

        [Fact]
        public void AddNode_Basic()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Step A", "out_a", T("Hello")));
            Assert.Equal(1, wf.NodeCount);
            Assert.True(wf.Nodes.ContainsKey("a"));
        }

        [Fact]
        public void AddNode_DuplicateThrows()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a"));
            Assert.Throws<ArgumentException>(() =>
                wf.AddNode(new WorkflowNode("a", "A2", "out_a2")));
        }

        [Fact]
        public void AddNode_NullThrows()
        {
            var wf = new PromptWorkflow();
            Assert.Throws<ArgumentNullException>(() => wf.AddNode(null!));
        }

        [Fact]
        public void WorkflowNode_EmptyIdThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorkflowNode("", "Name", "out"));
        }

        [Fact]
        public void WorkflowNode_EmptyNameThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorkflowNode("id", "", "out"));
        }

        [Fact]
        public void WorkflowNode_EmptyOutputVarThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorkflowNode("id", "Name", ""));
        }

        // ── AddEdge ──────────────────────────────────────────────────

        [Fact]
        public void AddEdge_CreatesDepependency()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("Hi")));
            wf.AddNode(new WorkflowNode("b", "B", "out_b", T("{{out_a}}")));
            wf.AddEdge("a", "b");
            Assert.Contains("a", wf.Nodes["b"].DependsOn);
        }

        [Fact]
        public void AddEdge_UnknownNodeThrows()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a"));
            Assert.Throws<ArgumentException>(() => wf.AddEdge("a", "missing"));
            Assert.Throws<ArgumentException>(() => wf.AddEdge("missing", "a"));
        }

        [Fact]
        public void AddEdge_NoDuplicates()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a"));
            wf.AddNode(new WorkflowNode("b", "B", "out_b"));
            wf.AddEdge("a", "b");
            wf.AddEdge("a", "b"); // duplicate
            Assert.Single(wf.Nodes["b"].DependsOn);
        }

        // ── Validation ───────────────────────────────────────────────

        [Fact]
        public void Validate_EmptyWorkflow()
        {
            var wf = new PromptWorkflow();
            var errors = wf.Validate();
            Assert.Contains(errors, e => e.Contains("no nodes"));
        }

        [Fact]
        public void Validate_CycleDetected()
        {
            var wf = new PromptWorkflow();
            var a = new WorkflowNode("a", "A", "out_a", T("Hi"));
            var b = new WorkflowNode("b", "B", "out_b", T("Hi"));
            a.DependsOn.Add("b");
            b.DependsOn.Add("a");
            wf.AddNode(a);
            wf.AddNode(b);
            var errors = wf.Validate();
            Assert.Contains(errors, e => e.Contains("Cycle"));
        }

        [Fact]
        public void Validate_MissingDependency()
        {
            var wf = new PromptWorkflow();
            var a = new WorkflowNode("a", "A", "out_a", T("Hi"));
            a.DependsOn.Add("missing");
            wf.AddNode(a);
            var errors = wf.Validate();
            Assert.Contains(errors, e => e.Contains("unknown node 'missing'"));
        }

        [Fact]
        public void Validate_DuplicateOutputVariables()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "shared", T("Hi")));
            wf.AddNode(new WorkflowNode("b", "B", "shared", T("Hi")));
            var errors = wf.Validate();
            Assert.Contains(errors, e => e.Contains("Duplicate output variable"));
        }

        [Fact]
        public void Validate_ValidWorkflow()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("Hi")));
            wf.AddNode(new WorkflowNode("b", "B", "out_b", T("{{out_a}}")));
            wf.AddEdge("a", "b");
            var errors = wf.Validate();
            Assert.Empty(errors);
        }

        // ── Single node execution ────────────────────────────────────

        [Fact]
        public async Task Execute_SingleNode()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Step A", "out_a", T("Hello")));

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Equal("[echo:Hello]", ctx.Variables["out_a"]);
            Assert.Single(ctx.CompletedNodes);
            Assert.Empty(ctx.FailedNodes);
        }

        [Fact]
        public void Execute_Sync()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("Hello")));

            var ctx = wf.Execute(EchoSync);

            Assert.True(ctx.IsSuccess);
            Assert.Equal("[sync:Hello]", ctx.Variables["out_a"]);
        }

        // ── Linear chain ─────────────────────────────────────────────

        [Fact]
        public async Task Execute_LinearChain()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("start")));
            var b = new WorkflowNode("b", "B", "out_b", T("{{out_a}} + more"));
            b.DependsOn.Add("a");
            wf.AddNode(b);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Equal("[echo:start]", ctx.Variables["out_a"]);
            Assert.Equal("[echo:[echo:start] + more]", ctx.Variables["out_b"]);
        }

        // ── Parallel execution ───────────────────────────────────────

        [Fact]
        public async Task Execute_Parallel()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("task-a")));
            wf.AddNode(new WorkflowNode("b", "B", "out_b", T("task-b")));
            wf.AddNode(new WorkflowNode("c", "C", "out_c", T("task-c")));

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Equal(3, ctx.CompletedNodes.Count);
            Assert.Equal("[echo:task-a]", ctx.Variables["out_a"]);
            Assert.Equal("[echo:task-b]", ctx.Variables["out_b"]);
            Assert.Equal("[echo:task-c]", ctx.Variables["out_c"]);
        }

        [Fact]
        public async Task Execute_DiamondPattern()
        {
            // A → B, A → C, B+C → D
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Root", "root", T("input")));

            var b = new WorkflowNode("b", "Branch1", "b1", T("left: {{root}}"));
            b.DependsOn.Add("a");
            wf.AddNode(b);

            var c = new WorkflowNode("c", "Branch2", "b2", T("right: {{root}}"));
            c.DependsOn.Add("a");
            wf.AddNode(c);

            var d = new WorkflowNode("d", "Merge", "final", T("{{b1}} + {{b2}}"));
            d.DependsOn.Add("b");
            d.DependsOn.Add("c");
            wf.AddNode(d);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Equal(4, ctx.CompletedNodes.Count);
            Assert.Contains("left:", ctx.Variables["final"]);
            Assert.Contains("right:", ctx.Variables["final"]);
        }

        // ── Initial variables ────────────────────────────────────────

        [Fact]
        public async Task Execute_WithInitialVariables()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a",
                T("Hello {{name}}, you are {{age}}")));

            var vars = new Dictionary<string, string>
            {
                ["name"] = "Alice",
                ["age"] = "30"
            };

            var ctx = await wf.ExecuteAsync(Echo, vars);

            Assert.True(ctx.IsSuccess);
            Assert.Contains("Alice", ctx.Variables["out_a"]);
            Assert.Contains("30", ctx.Variables["out_a"]);
        }

        // ── Conditional nodes ────────────────────────────────────────

        [Fact]
        public async Task Execute_ConditionalSkipped()
        {
            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("Hello"));
            node.Condition = ctx => false; // always skip
            wf.AddNode(node);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Single(ctx.SkippedNodes);
            Assert.Empty(ctx.CompletedNodes);
        }

        [Fact]
        public async Task Execute_ConditionalExecuted()
        {
            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("Hello"));
            node.Condition = ctx => true;
            wf.AddNode(node);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Single(ctx.CompletedNodes);
        }

        [Fact]
        public async Task Execute_ConditionalBasedOnVariable()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("data")));

            var b = new WorkflowNode("b", "B", "out_b", T("process: {{out_a}}"));
            b.DependsOn.Add("a");
            b.Condition = ctx => ctx.Variables.ContainsKey("out_a")
                              && ctx.Variables["out_a"].Contains("data");
            wf.AddNode(b);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Equal(2, ctx.CompletedNodes.Count);
        }

        [Fact]
        public async Task Execute_ConditionalCascadeSkip()
        {
            // A → B(conditional:false) → C
            // C should be skipped because B was skipped
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("start")));

            var b = new WorkflowNode("b", "B", "out_b", T("middle"));
            b.DependsOn.Add("a");
            b.Condition = ctx => false;
            wf.AddNode(b);

            var c = new WorkflowNode("c", "C", "out_c", T("end"));
            c.DependsOn.Add("b");
            wf.AddNode(c);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Single(ctx.CompletedNodes); // only A
            Assert.Equal(2, ctx.SkippedNodes.Count); // B and C
        }

        [Fact]
        public async Task Execute_ConditionExceptionFails()
        {
            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("Hello"));
            node.Condition = ctx => throw new InvalidOperationException("boom");
            wf.AddNode(node);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.Single(ctx.FailedNodes);
            Assert.Contains("boom", ctx.FailedNodes[0].Error);
        }

        // ── Merge strategies ─────────────────────────────────────────

        [Fact]
        public async Task Merge_ConcatenateAll()
        {
            var wf = BuildParallelMergeWorkflow(MergeStrategy.ConcatenateAll);
            var ctx = await wf.ExecuteAsync(s => Task.FromResult(s));

            Assert.True(ctx.IsSuccess);
            var merged = ctx.Variables["merged"];
            Assert.Contains("left", merged);
            Assert.Contains("right", merged);
        }

        [Fact]
        public async Task Merge_JoinWithSeparator()
        {
            var wf = BuildParallelMergeWorkflow(MergeStrategy.JoinWithSeparator, " | ");
            var ctx = await wf.ExecuteAsync(s => Task.FromResult(s));

            var merged = ctx.Variables["merged"];
            Assert.Contains(" | ", merged);
        }

        [Fact]
        public async Task Merge_LongestOutput()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Short", "short", T("hi")));
            wf.AddNode(new WorkflowNode("b", "Long", "long", T("hello world this is longer")));

            var merge = new WorkflowNode("m", "Merge", "result");
            merge.DependsOn.Add("a");
            merge.DependsOn.Add("b");
            merge.MergeStrategy = MergeStrategy.LongestOutput;
            wf.AddNode(merge);

            var ctx = await wf.ExecuteAsync(s => Task.FromResult(s));

            Assert.Equal(ctx.Variables["long"], ctx.Variables["result"]);
        }

        [Fact]
        public async Task Merge_ShortestOutput()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Short", "short", T("hi")));
            wf.AddNode(new WorkflowNode("b", "Long", "long", T("hello world this is longer")));

            var merge = new WorkflowNode("m", "Merge", "result");
            merge.DependsOn.Add("a");
            merge.DependsOn.Add("b");
            merge.MergeStrategy = MergeStrategy.ShortestOutput;
            wf.AddNode(merge);

            var ctx = await wf.ExecuteAsync(s => Task.FromResult(s));

            Assert.Equal(ctx.Variables["short"], ctx.Variables["result"]);
        }

        [Fact]
        public async Task Merge_FirstCompleted()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("first")));
            wf.AddNode(new WorkflowNode("b", "B", "out_b", T("second")));

            var merge = new WorkflowNode("m", "Merge", "result");
            merge.DependsOn.Add("a");
            merge.DependsOn.Add("b");
            merge.MergeStrategy = MergeStrategy.FirstCompleted;
            wf.AddNode(merge);

            var ctx = await wf.ExecuteAsync(s => Task.FromResult(s));

            Assert.True(ctx.IsSuccess);
            Assert.True(ctx.Variables.ContainsKey("result"));
        }

        [Fact]
        public async Task Merge_CustomTemplateThrowsWithoutTemplate()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("data")));

            var merge = new WorkflowNode("m", "Merge", "result");
            merge.DependsOn.Add("a");
            merge.MergeStrategy = MergeStrategy.CustomTemplate;
            wf.AddNode(merge);

            var ctx = await wf.ExecuteAsync(s => Task.FromResult(s));

            Assert.Single(ctx.FailedNodes);
        }

        private PromptWorkflow BuildParallelMergeWorkflow(
            MergeStrategy strategy, string? separator = null)
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Left", "left", T("left")));
            wf.AddNode(new WorkflowNode("b", "Right", "right", T("right")));

            var merge = new WorkflowNode("m", "Merge", "merged");
            merge.DependsOn.Add("a");
            merge.DependsOn.Add("b");
            merge.MergeStrategy = strategy;
            if (separator != null) merge.MergeSeparator = separator;
            wf.AddNode(merge);

            return wf;
        }

        // ── Error handling ───────────────────────────────────────────

        [Fact]
        public async Task Execute_FailurePropagates()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("Hello")));
            var b = new WorkflowNode("b", "B", "out_b", T("{{out_a}}"));
            b.DependsOn.Add("a");
            wf.AddNode(b);

            Func<string, Task<string>> failingModel = async prompt =>
            {
                if (prompt == "Hello") throw new Exception("Model error");
                return await Task.FromResult("ok");
            };

            var ctx = await wf.ExecuteAsync(failingModel);

            Assert.False(ctx.IsSuccess);
            Assert.Single(ctx.FailedNodes);
            Assert.Single(ctx.SkippedNodes); // B skipped because A failed
        }

        [Fact]
        public async Task Execute_Retries()
        {
            var callCount = 0;
            Func<string, Task<string>> flakyModel = prompt =>
            {
                callCount++;
                if (callCount < 3) throw new Exception("transient error");
                return Task.FromResult("success");
            };

            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("test"));
            node.MaxRetries = 3;
            wf.AddNode(node);

            var ctx = await wf.ExecuteAsync(flakyModel);

            Assert.True(ctx.IsSuccess);
            Assert.Equal("success", ctx.Variables["out_a"]);
            Assert.Equal(2, ctx.NodeResults["a"].RetriesUsed);
        }

        [Fact]
        public async Task Execute_RetriesExhausted()
        {
            Func<string, Task<string>> alwaysFail = prompt =>
                throw new Exception("always fails");

            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("test"));
            node.MaxRetries = 2;
            wf.AddNode(node);

            var ctx = await wf.ExecuteAsync(alwaysFail);

            Assert.False(ctx.IsSuccess);
            Assert.Single(ctx.FailedNodes);
            Assert.Contains("3 attempt", ctx.FailedNodes[0].Error);
        }

        // ── Cancellation ─────────────────────────────────────────────

        [Fact]
        public async Task Execute_CancellationRespected()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("test")));

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                wf.ExecuteAsync(Echo, null, cts.Token));
        }

        // ── Validation on execute ────────────────────────────────────

        [Fact]
        public async Task Execute_InvalidWorkflowThrows()
        {
            var wf = new PromptWorkflow();
            var a = new WorkflowNode("a", "A", "out_a", T("Hi"));
            a.DependsOn.Add("missing");
            wf.AddNode(a);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                wf.ExecuteAsync(Echo));
        }

        [Fact]
        public async Task Execute_NullModelFuncThrows()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("test")));

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                wf.ExecuteAsync(null!));
        }

        [Fact]
        public void Execute_SyncNullModelFuncThrows()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("test")));

            Assert.Throws<ArgumentNullException>(() =>
                wf.Execute((Func<string, string>)null!));
        }

        // ── MaxParallelism ───────────────────────────────────────────

        [Fact]
        public void MaxParallelism_InvalidThrows()
        {
            var wf = new PromptWorkflow();
            Assert.Throws<ArgumentException>(() => wf.MaxParallelism = 0);
        }

        [Fact]
        public async Task MaxParallelism_Respected()
        {
            var concurrency = 0;
            var maxConcurrency = 0;

            Func<string, Task<string>> trackingModel = async prompt =>
            {
                var c = Interlocked.Increment(ref concurrency);
                var prev = maxConcurrency;
                while (c > prev && Interlocked.CompareExchange(
                    ref maxConcurrency, c, prev) != prev)
                {
                    prev = maxConcurrency;
                }
                await Task.Delay(50);
                Interlocked.Decrement(ref concurrency);
                return "ok";
            };

            var wf = new PromptWorkflow { MaxParallelism = 2 };
            for (int i = 0; i < 6; i++)
                wf.AddNode(new WorkflowNode($"n{i}", $"N{i}", $"out_{i}", T("test")));

            var ctx = await wf.ExecuteAsync(trackingModel);

            Assert.True(ctx.IsSuccess);
            Assert.True(maxConcurrency <= 2,
                $"Max concurrency was {maxConcurrency}, expected <= 2");
        }

        // ── DefaultTimeout ───────────────────────────────────────────

        [Fact]
        public void DefaultTimeout_InvalidThrows()
        {
            var wf = new PromptWorkflow();
            Assert.Throws<ArgumentException>(() =>
                wf.DefaultTimeout = TimeSpan.Zero);
        }

        // ── ToDot ────────────────────────────────────────────────────

        [Fact]
        public void ToDot_Basic()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Start", "out_a", T("Hi")));
            var b = new WorkflowNode("b", "End", "out_b", T("Bye"));
            b.DependsOn.Add("a");
            wf.AddNode(b);

            var dot = wf.ToDot();

            Assert.Contains("digraph workflow", dot);
            Assert.Contains("\"a\"", dot);
            Assert.Contains("\"b\"", dot);
            Assert.Contains("\"a\" -> \"b\"", dot);
        }

        [Fact]
        public async Task ToDot_WithContext()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("Hi")));

            var ctx = await wf.ExecuteAsync(Echo);

            var dot = wf.ToDot(ctx);

            Assert.Contains("palegreen", dot); // completed color
        }

        [Fact]
        public void ToDot_ConditionalNode()
        {
            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("Hi"));
            node.Condition = ctx => true;
            wf.AddNode(node);

            var dot = wf.ToDot();
            Assert.Contains("[conditional]", dot);
        }

        [Fact]
        public void ToDot_MergeNode()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("Hi")));
            var merge = new WorkflowNode("m", "M", "merged");
            merge.DependsOn.Add("a");
            wf.AddNode(merge);

            var dot = wf.ToDot();
            Assert.Contains("merge:", dot);
        }

        // ── GenerateReport ───────────────────────────────────────────

        [Fact]
        public async Task GenerateReport_Basic()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Step A", "out_a", T("test")));

            var ctx = await wf.ExecuteAsync(Echo);

            var report = PromptWorkflow.GenerateReport(ctx);

            Assert.Contains("Workflow Execution Report", report);
            Assert.Contains("SUCCESS", report);
            Assert.Contains("Step A", report);
            Assert.Contains("Execution ID", report);
        }

        [Fact]
        public async Task GenerateReport_WithFailure()
        {
            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("test"));
            wf.AddNode(node);

            var ctx = await wf.ExecuteAsync(
                prompt => throw new Exception("oops"));

            var report = PromptWorkflow.GenerateReport(ctx);

            Assert.Contains("FAILED", report);
            Assert.Contains("oops", report);
        }

        [Fact]
        public void GenerateReport_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PromptWorkflow.GenerateReport(null!));
        }

        // ── WorkflowContext ──────────────────────────────────────────

        [Fact]
        public async Task Context_ExecutionId()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("test")));

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.NotNull(ctx.ExecutionId);
            Assert.Equal(12, ctx.ExecutionId.Length);
        }

        [Fact]
        public async Task Context_Timing()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("test")));

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.CompletedAt > ctx.StartedAt);
            Assert.True(ctx.Elapsed >= TimeSpan.Zero);
        }

        [Fact]
        public async Task Context_Metadata()
        {
            var wf = new PromptWorkflow();
            var node = new WorkflowNode("a", "A", "out_a", T("test"));
            node.Condition = ctx =>
            {
                ctx.Metadata["checked"] = true;
                return true;
            };
            wf.AddNode(node);

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True((bool)ctx.Metadata["checked"]);
        }

        // ── NodeResult ───────────────────────────────────────────────

        [Fact]
        public async Task NodeResult_RenderedPrompt()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a",
                T("Hello {{name}}")));

            var ctx = await wf.ExecuteAsync(Echo,
                new Dictionary<string, string> { ["name"] = "World" });

            Assert.Equal("Hello World", ctx.NodeResults["a"].RenderedPrompt);
        }

        [Fact]
        public async Task NodeResult_Timing()
        {
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "A", "out_a", T("test")));

            var ctx = await wf.ExecuteAsync(async prompt =>
            {
                await Task.Delay(20);
                return "ok";
            });

            var result = ctx.NodeResults["a"];
            Assert.True(result.Duration >= TimeSpan.FromMilliseconds(10));
            Assert.NotNull(result.StartedAt);
            Assert.NotNull(result.CompletedAt);
        }

        // ── Complex workflows ────────────────────────────────────────

        [Fact]
        public async Task Execute_WideFanOut()
        {
            // Root → 10 parallel nodes
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("root", "Root", "root_out", T("go")));

            for (int i = 0; i < 10; i++)
            {
                var child = new WorkflowNode($"c{i}", $"Child{i}", $"c{i}_out",
                    T($"child-{i}: {{{{root_out}}}}"));
                child.DependsOn.Add("root");
                wf.AddNode(child);
            }

            var ctx = await wf.ExecuteAsync(Echo);

            Assert.True(ctx.IsSuccess);
            Assert.Equal(11, ctx.CompletedNodes.Count);
        }

        [Fact]
        public async Task Execute_ThreeLevelPipeline()
        {
            // L1: A  →  L2: B, C (parallel)  →  L3: D (merge)
            var wf = new PromptWorkflow();
            wf.AddNode(new WorkflowNode("a", "Input", "input", T("raw data")));

            var b = new WorkflowNode("b", "Summarize", "summary", T("summarize: {{input}}"));
            b.DependsOn.Add("a");
            wf.AddNode(b);

            var c = new WorkflowNode("c", "Analyze", "analysis", T("analyze: {{input}}"));
            c.DependsOn.Add("a");
            wf.AddNode(c);

            var d = new WorkflowNode("d", "Report", "report",
                T("Report:\n{{summary}}\n{{analysis}}"));
            d.DependsOn.Add("b");
            d.DependsOn.Add("c");
            wf.AddNode(d);

            var ctx = await wf.ExecuteAsync(Upper);

            Assert.True(ctx.IsSuccess);
            Assert.Equal(4, ctx.CompletedNodes.Count);
            Assert.Contains("SUMMARIZE", ctx.Variables["report"]);
            Assert.Contains("ANALYZE", ctx.Variables["report"]);
        }

        // ── Chaining ─────────────────────────────────────────────────

        [Fact]
        public void FluentChaining()
        {
            var wf = new PromptWorkflow()
                .AddNode(new WorkflowNode("a", "A", "out_a", T("Hi")))
                .AddNode(new WorkflowNode("b", "B", "out_b", T("Bye")))
                .AddEdge("a", "b");

            Assert.Equal(2, wf.NodeCount);
            Assert.Contains("a", wf.Nodes["b"].DependsOn);
        }
    }
}
