namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="PromptOrchestrator"/> and the supporting plan / execution /
    /// report types. This module was previously untested; these cover plan validation,
    /// topological layering, critical-path analysis, the execution engine (linear chains,
    /// fan-out/fan-in, gates, routers, fallbacks, retries) and the report generators.
    /// </summary>
    public class PromptOrchestratorTests
    {
        // ── helpers ──────────────────────────────────────────

        /// <summary>An executor that echoes the (already-resolved) prompt it receives.</summary>
        private static PromptOrchestrator EchoOrchestrator() =>
            new PromptOrchestrator(prompt => Task.FromResult($"echo:{prompt}"));

        // ── OrchestratorPlan.Validate ────────────────────────

        [Fact]
        public void Validate_ValidLinearPlan_NoErrors()
        {
            var plan = PromptOrchestrator.BuildLinearChain("a", "b", "c");
            Assert.Empty(plan.Validate());
        }

        [Fact]
        public void Validate_MissingEntryNode_ReportsError()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "ghost" };
            plan.Nodes.Add(new OrchestratorNode("real", "p"));

            var errors = plan.Validate();
            Assert.Contains(errors, e => e.Contains("ghost") && e.Contains("not found"));
        }

        [Fact]
        public void Validate_UnknownDependency_ReportsError()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "a" };
            plan.Nodes.Add(new OrchestratorNode("a", "p") { DependsOn = { "missing" } });

            var errors = plan.Validate();
            Assert.Contains(errors, e => e.Contains("'a'") && e.Contains("missing"));
        }

        [Fact]
        public void Validate_Cycle_IsDetected()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "a" };
            plan.Nodes.Add(new OrchestratorNode("a", "p") { DependsOn = { "b" } });
            plan.Nodes.Add(new OrchestratorNode("b", "p") { DependsOn = { "a" } });

            Assert.Contains(plan.Validate(), e => e.Contains("cycle"));
        }

        // ── GetExecutionLayers ───────────────────────────────

        [Fact]
        public void GetExecutionLayers_DiamondGraph_LayeredCorrectly()
        {
            // a → {b, c} → d
            var plan = new OrchestratorPlan { EntryNodeId = "a" };
            plan.Nodes.Add(new OrchestratorNode("a", "p"));
            plan.Nodes.Add(new OrchestratorNode("b", "p") { DependsOn = { "a" } });
            plan.Nodes.Add(new OrchestratorNode("c", "p") { DependsOn = { "a" } });
            plan.Nodes.Add(new OrchestratorNode("d", "p") { DependsOn = { "b", "c" } });

            var layers = plan.GetExecutionLayers();

            Assert.Equal(3, layers.Count);
            Assert.Equal(new[] { "a" }, layers[0].Select(n => n.Id));
            Assert.Equal(new[] { "b", "c" }, layers[1].Select(n => n.Id).OrderBy(x => x));
            Assert.Equal(new[] { "d" }, layers[2].Select(n => n.Id));
        }

        [Fact]
        public void GetExecutionLayers_Cycle_StopsWithoutInfiniteLoop()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "a" };
            plan.Nodes.Add(new OrchestratorNode("a", "p") { DependsOn = { "b" } });
            plan.Nodes.Add(new OrchestratorNode("b", "p") { DependsOn = { "a" } });

            // Neither node can ever enter a layer; the guard breaks out.
            var layers = plan.GetExecutionLayers();
            Assert.Empty(layers);
        }

        // ── GetCriticalPath ──────────────────────────────────

        [Fact]
        public void GetCriticalPath_ReturnsLongestChain()
        {
            // a → b → d   and   a → c   ⇒ critical path a,b,d
            var plan = new OrchestratorPlan { EntryNodeId = "a" };
            plan.Nodes.Add(new OrchestratorNode("a", "p"));
            plan.Nodes.Add(new OrchestratorNode("b", "p") { DependsOn = { "a" } });
            plan.Nodes.Add(new OrchestratorNode("c", "p") { DependsOn = { "a" } });
            plan.Nodes.Add(new OrchestratorNode("d", "p") { DependsOn = { "b" } });

            Assert.Equal(new[] { "a", "b", "d" }, plan.GetCriticalPath());
        }

        // ── ExecuteAsync: basic linear chain ─────────────────

        [Fact]
        public async Task ExecuteAsync_LinearChain_RunsAllNodesAndResolvesUpstream()
        {
            // step_1 references {step_0}; the orchestrator must inject step_0's output.
            var plan = PromptOrchestrator.BuildLinearChain("hello", "use {step_0}");
            var orch = EchoOrchestrator();

            var exec = await orch.ExecuteAsync(plan);

            Assert.Equal(OrchestratorStatus.Completed, exec.Status);
            Assert.Equal(2, exec.Results.Count);
            Assert.True(exec.Results["step_0"].Success);
            Assert.Equal("echo:hello", exec.Results["step_0"].Output);
            // {step_0} placeholder was substituted before execution
            Assert.Equal("echo:use echo:hello", exec.Results["step_1"].Output);
            Assert.NotNull(exec.CompletedAt);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidPlan_Throws()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "nope" };
            plan.Nodes.Add(new OrchestratorNode("a", "p") { DependsOn = { "missing" } });
            var orch = EchoOrchestrator();

            await Assert.ThrowsAsync<InvalidOperationException>(() => orch.ExecuteAsync(plan));
        }

        [Fact]
        public async Task ExecuteAsync_InitialVariables_AreInjected()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "n" };
            plan.Nodes.Add(new OrchestratorNode("n", "Hi {name}"));
            var orch = EchoOrchestrator();

            var exec = await orch.ExecuteAsync(plan,
                new Dictionary<string, string> { ["name"] = "Sam" });

            Assert.Equal("echo:Hi Sam", exec.Results["n"].Output);
        }

        // ── ExecuteAsync: failure / status ───────────────────

        [Fact]
        public async Task ExecuteAsync_AllNodesFail_StatusFailed()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "n" };
            plan.Nodes.Add(new OrchestratorNode("n", "p"));
            var orch = new PromptOrchestrator(_ => throw new InvalidOperationException("boom"));

            var exec = await orch.ExecuteAsync(plan);

            Assert.Equal(OrchestratorStatus.Failed, exec.Status);
            Assert.False(exec.Results["n"].Success);
            Assert.Equal("boom", exec.Results["n"].Error);
        }

        [Fact]
        public async Task ExecuteAsync_SomeFailSomeSucceed_StatusPartialSuccess()
        {
            // Two independent (entry-layer) nodes; the first call succeeds, the second throws.
            var plan = new OrchestratorPlan { EntryNodeId = "ok" };
            plan.Nodes.Add(new OrchestratorNode("ok", "p"));
            plan.Nodes.Add(new OrchestratorNode("bad", "p"));

            int call = 0;
            var orch = new PromptOrchestrator(_ =>
                Interlocked.Increment(ref call) == 1
                    ? Task.FromResult("ok-output")
                    : throw new Exception("second fails"));

            var exec = await orch.ExecuteAsync(plan);

            Assert.Equal(OrchestratorStatus.PartialSuccess, exec.Status);
            Assert.Contains(exec.Results.Values, r => r.Success);
            Assert.Contains(exec.Results.Values, r => !r.Success);
        }

        // ── ExecuteAsync: retry-timing regression ────────────

        /// <summary>
        /// Regression: the per-node stopwatch is started once before the retry loop and must
        /// keep running across attempts. Previously it was Stop()'d on the first (failed)
        /// attempt and never restarted, so a node that only succeeded after a retry reported
        /// just the first attempt's elapsed time as Duration. Here attempt 1 sleeps then throws
        /// and attempt 2 sleeps then succeeds; the recorded Duration must cover BOTH attempts.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_RetrySucceeds_DurationCoversAllAttempts()
        {
            const int perAttemptMs = 60;
            int attempts = 0;
            var orch = new PromptOrchestrator(async _ =>
            {
                int n = Interlocked.Increment(ref attempts);
                await Task.Delay(perAttemptMs);
                if (n == 1) throw new Exception("transient");
                return "recovered";
            });

            var plan = new OrchestratorPlan { EntryNodeId = "n" };
            plan.Nodes.Add(new OrchestratorNode("n", "p") { MaxRetries = 2 });

            var exec = await orch.ExecuteAsync(plan);

            var result = exec.Results["n"];
            Assert.True(result.Success);
            Assert.Equal(2, result.Attempt);
            // Both attempts each slept perAttemptMs, so a correctly-running stopwatch reports
            // roughly 2×perAttemptMs (~120ms). The old frozen-stopwatch bug stopped timing after
            // the first attempt and reported only ~1×perAttemptMs (~60ms). Assert comfortably
            // above the single-attempt figure but below the two-attempt figure so the check is a
            // genuine regression guard without being timer-jitter flaky.
            Assert.True(result.Duration.TotalMilliseconds >= perAttemptMs + 25,
                $"Duration {result.Duration.TotalMilliseconds:F0}ms should cover both attempts " +
                $"(~{perAttemptMs * 2}ms); the frozen-stopwatch bug reports only ~{perAttemptMs}ms.");
        }

        [Fact]
        public async Task ExecuteAsync_RetryThenSucceed_LogsRetryEvent()
        {
            int attempts = 0;
            var orch = new PromptOrchestrator(_ =>
                Interlocked.Increment(ref attempts) == 1
                    ? throw new Exception("transient")
                    : Task.FromResult("ok"));

            var plan = new OrchestratorPlan { EntryNodeId = "n" };
            plan.Nodes.Add(new OrchestratorNode("n", "p") { MaxRetries = 3 });

            var exec = await orch.ExecuteAsync(plan);

            Assert.Equal(OrchestratorStatus.Completed, exec.Status);
            Assert.Contains(exec.EventLog, e => e.Type == OrchestratorEventType.NodeRetrying);
        }

        // ── ExecuteAsync: gate ───────────────────────────────

        [Fact]
        public async Task ExecuteAsync_GateBelowThreshold_SkipsDownstream()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "gate" };
            plan.Nodes.Add(new OrchestratorNode("gate", "p")
            {
                Type = OrchestratorNodeType.Gate,
                ConfidenceThreshold = 0.8
            });
            plan.Nodes.Add(new OrchestratorNode("after", "p") { DependsOn = { "gate" } });

            // gate output carries a low confidence marker
            var orch = new PromptOrchestrator(_ => Task.FromResult("result [confidence:0.30]"));
            var exec = await orch.ExecuteAsync(plan);

            Assert.True(exec.Results.ContainsKey("gate"));
            Assert.False(exec.Results.ContainsKey("after")); // skipped
            Assert.Contains(exec.EventLog, e => e.Type == OrchestratorEventType.GateBlocked);
        }

        [Fact]
        public async Task ExecuteAsync_GateAboveThreshold_RunsDownstream()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "gate" };
            plan.Nodes.Add(new OrchestratorNode("gate", "p")
            {
                Type = OrchestratorNodeType.Gate,
                ConfidenceThreshold = 0.5
            });
            plan.Nodes.Add(new OrchestratorNode("after", "p") { DependsOn = { "gate" } });

            var orch = new PromptOrchestrator(_ => Task.FromResult("ok [confidence:0.90]"));
            var exec = await orch.ExecuteAsync(plan);

            Assert.True(exec.Results["gate"].Success);
            Assert.True(exec.Results.ContainsKey("after"));
            Assert.True(exec.Results["after"].Success);
        }

        // ── ExecuteAsync: router ─────────────────────────────

        [Fact]
        public async Task ExecuteAsync_Router_SelectsBranchAndSkipsSiblings()
        {
            var plan = PromptOrchestrator.BuildConditionalPipeline(
                "classify this",
                new Dictionary<string, string>
                {
                    ["branch_a"] = "do A",
                    ["branch_b"] = "do B"
                },
                _ => "branch_a");

            var orch = EchoOrchestrator();
            var exec = await orch.ExecuteAsync(plan);

            Assert.True(exec.Results.ContainsKey("classifier"));
            Assert.True(exec.Results.ContainsKey("router"));
            Assert.True(exec.Results.ContainsKey("branch_a"));   // selected
            Assert.False(exec.Results.ContainsKey("branch_b"));  // sibling skipped
            Assert.Contains(exec.EventLog, e => e.Type == OrchestratorEventType.RouteSelected);
        }

        // ── ExecuteAsync: fallback ───────────────────────────

        [Fact]
        public async Task ExecuteAsync_FallbackNode_RecoversViaAlternative()
        {
            var plan = new OrchestratorPlan { EntryNodeId = "primary" };
            plan.Nodes.Add(new OrchestratorNode("primary", "PRIMARY")
            {
                Type = OrchestratorNodeType.Fallback,
                FallbackNodeIds = new List<string> { "alt" }
            });
            // 'alt' is a standalone node (no dependency) tried only when primary fails
            plan.Nodes.Add(new OrchestratorNode("alt", "ALT"));

            var orch = new PromptOrchestrator(prompt =>
                prompt.Contains("PRIMARY")
                    ? throw new Exception("primary down")
                    : Task.FromResult("alt-output"));

            var exec = await orch.ExecuteAsync(plan);

            Assert.False(exec.Results["primary"].Success);
            Assert.True(exec.Results["alt"].Success);
            Assert.Equal("alt-output", exec.Results["alt"].Output);
            Assert.Contains(exec.EventLog, e => e.Type == OrchestratorEventType.FallbackTriggered);
        }

        // ── Builders ─────────────────────────────────────────

        [Fact]
        public void BuildLinearChain_WiresSequentialDependencies()
        {
            var plan = PromptOrchestrator.BuildLinearChain("p0", "p1", "p2");

            Assert.Equal("step_0", plan.EntryNodeId);
            Assert.Empty(plan.Nodes[0].DependsOn);
            Assert.Equal(new[] { "step_0" }, plan.Nodes[1].DependsOn);
            Assert.Equal(new[] { "step_1" }, plan.Nodes[2].DependsOn);
        }

        [Fact]
        public void BuildFanOutFanIn_AggregatorDependsOnAllParallel()
        {
            var plan = PromptOrchestrator.BuildFanOutFanIn(
                "input", new[] { "p0", "p1", "p2" }, "combine");

            Assert.Equal("input", plan.EntryNodeId);
            var agg = plan.Nodes.Single(n => n.Id == "aggregator");
            Assert.Equal(OrchestratorNodeType.Aggregator, agg.Type);
            Assert.Equal(new[] { "parallel_0", "parallel_1", "parallel_2" },
                agg.DependsOn.OrderBy(x => x));
            Assert.Empty(plan.Validate());
        }

        // ── Reports ──────────────────────────────────────────

        [Fact]
        public async Task OrchestratorReport_AllFormats_RenderWithoutError()
        {
            var plan = PromptOrchestrator.BuildLinearChain("a", "b");
            var exec = await EchoOrchestrator().ExecuteAsync(plan);

            var text = OrchestratorReport.GenerateText(exec);
            var md = OrchestratorReport.GenerateMarkdown(exec);
            var json = OrchestratorReport.GenerateJson(exec);
            var mermaid = OrchestratorReport.GenerateMermaid(exec);

            Assert.Contains("Orchestration Report", text);
            Assert.Contains("Critical Path", text);
            Assert.Contains("| Node | Status |", md);
            Assert.Contains("\"Status\": \"Completed\"", json);
            Assert.Contains("flowchart TD", mermaid);
            // each node appears as an edge target / style entry
            Assert.Contains("step_0 --> step_1", mermaid);
        }

        [Fact]
        public async Task OrchestratorReport_Mermaid_StylesSkippedNodesSeparately()
        {
            // Gate blocks the downstream node so it is absent from Results → styled as skipped.
            var plan = new OrchestratorPlan { EntryNodeId = "gate" };
            plan.Nodes.Add(new OrchestratorNode("gate", "p")
            {
                Type = OrchestratorNodeType.Gate,
                ConfidenceThreshold = 0.9
            });
            plan.Nodes.Add(new OrchestratorNode("after", "p") { DependsOn = { "gate" } });

            var orch = new PromptOrchestrator(_ => Task.FromResult("x [confidence:0.10]"));
            var exec = await orch.ExecuteAsync(plan);

            var mermaid = OrchestratorReport.GenerateMermaid(exec);
            Assert.Contains("style after fill:#555", mermaid); // skipped style
        }
    }
}
