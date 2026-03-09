namespace Prompt.Tests
{
    using Xunit;
    using Prompt;
    using System;
    using System.Linq;

    public class PromptDependencyGraphTests
    {
        // ─── Node Management ───

        [Fact]
        public void AddNode_BasicNode_Succeeds()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a", "Node A");
            Assert.Equal(1, graph.Count);
            Assert.Equal("Node A", graph.Nodes["a"].Label);
        }

        [Fact]
        public void AddNode_WithWeight_SetsWeight()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a", weight: 5.0);
            Assert.Equal(5.0, graph.Nodes["a"].Weight);
        }

        [Fact]
        public void AddNode_DuplicateId_Throws()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a");
            Assert.Throws<ArgumentException>(() => graph.AddNode("a"));
        }

        [Fact]
        public void AddNode_NullId_Throws()
        {
            var node = Assert.Throws<ArgumentException>(() => new PromptNode(null!));
        }

        [Fact]
        public void AddNode_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PromptNode(""));
        }

        [Fact]
        public void AddNode_ObjectOverload_Works()
        {
            var graph = new PromptDependencyGraph();
            var node = new PromptNode("x") { Label = "Test" };
            graph.AddNode(node);
            Assert.Same(node, graph.Nodes["x"]);
        }

        [Fact]
        public void RemoveNode_Existing_ReturnsTrue()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a");
            Assert.True(graph.RemoveNode("a"));
            Assert.Equal(1, graph.Count);
        }

        [Fact]
        public void RemoveNode_RemovesDanglingEdges()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a");
            graph.RemoveNode("a");
            Assert.Empty(graph.Nodes["b"].Dependencies);
        }

        [Fact]
        public void RemoveNode_NonExisting_ReturnsFalse()
        {
            var graph = new PromptDependencyGraph();
            Assert.False(graph.RemoveNode("nope"));
        }

        // ─── Edge Management ───

        [Fact]
        public void AddEdge_ValidNodes_CreatesDependency()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a");
            Assert.Contains("a", graph.Nodes["b"].Dependencies);
        }

        [Fact]
        public void AddEdge_FromNotFound_Throws()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a");
            Assert.Throws<ArgumentException>(() => graph.AddEdge("nope", "a"));
        }

        [Fact]
        public void AddEdge_ToNotFound_Throws()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a");
            Assert.Throws<ArgumentException>(() => graph.AddEdge("a", "nope"));
        }

        [Fact]
        public void AddEdge_SelfDependency_Throws()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a");
            Assert.Throws<ArgumentException>(() => graph.AddEdge("a", "a"));
        }

        [Fact]
        public void AddEdge_DuplicateIsIdempotent()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a").AddEdge("b", "a");
            Assert.Single(graph.Nodes["b"].Dependencies);
        }

        [Fact]
        public void RemoveEdge_Existing_ReturnsTrue()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a");
            Assert.True(graph.RemoveEdge("b", "a"));
            Assert.Empty(graph.Nodes["b"].Dependencies);
        }

        [Fact]
        public void RemoveEdge_NonExisting_ReturnsFalse()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b");
            Assert.False(graph.RemoveEdge("b", "a"));
        }

        // ─── Queries ───

        [Fact]
        public void GetDependents_ReturnsCorrectNodes()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c")
                .AddEdge("b", "a").AddEdge("c", "a");
            var deps = graph.GetDependents("a");
            Assert.Equal(2, deps.Count);
            Assert.Contains("b", deps);
            Assert.Contains("c", deps);
        }

        [Fact]
        public void GetTransitiveDependencies_ReturnsAll()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c")
                .AddEdge("b", "a").AddEdge("c", "b");
            var trans = graph.GetTransitiveDependencies("c");
            Assert.Contains("b", trans);
            Assert.Contains("a", trans);
        }

        [Fact]
        public void GetTransitiveDependents_ReturnsAll()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c")
                .AddEdge("b", "a").AddEdge("c", "b");
            var trans = graph.GetTransitiveDependents("a");
            Assert.Contains("b", trans);
            Assert.Contains("c", trans);
        }

        // ─── Cycle Detection ───

        [Fact]
        public void DetectCycles_NoCycles_ReturnsEmpty()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a");
            Assert.Empty(graph.DetectCycles());
        }

        [Fact]
        public void DetectCycles_WithCycle_DetectsIt()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c");
            graph.AddEdge("b", "a").AddEdge("c", "b");
            // Manually create cycle: a depends on c
            graph.AddEdge("a", "c");
            var cycles = graph.DetectCycles();
            Assert.NotEmpty(cycles);
        }

        [Fact]
        public void DetectCycles_TwoNodeCycle()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b");
            graph.AddEdge("a", "b").AddEdge("b", "a");
            Assert.NotEmpty(graph.DetectCycles());
        }

        // ─── Topological Sort ───

        [Fact]
        public void TopologicalSort_LinearChain()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c")
                .AddEdge("b", "a").AddEdge("c", "b");
            var sorted = graph.TopologicalSort();
            Assert.Equal(new[] { "a", "b", "c" }, sorted);
        }

        [Fact]
        public void TopologicalSort_Diamond()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c").AddNode("d");
            graph.AddEdge("b", "a").AddEdge("c", "a").AddEdge("d", "b").AddEdge("d", "c");
            var sorted = graph.TopologicalSort();
            Assert.Equal("a", sorted[0]);
            Assert.Equal("d", sorted[3]);
        }

        [Fact]
        public void TopologicalSort_WithCycle_Throws()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b");
            graph.AddEdge("a", "b").AddEdge("b", "a");
            Assert.Throws<InvalidOperationException>(() => graph.TopologicalSort());
        }

        // ─── Execution Layers ───

        [Fact]
        public void ExecutionLayers_ParallelNodes()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c").AddNode("d");
            graph.AddEdge("c", "a").AddEdge("d", "b");
            var layers = graph.ComputeExecutionLayers();
            Assert.Equal(2, layers.Count);
            Assert.Equal(2, layers[0].Count); // a, b in parallel
            Assert.Equal(2, layers[1].Count); // c, d in parallel
        }

        [Fact]
        public void ExecutionLayers_AllIndependent()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c");
            var layers = graph.ComputeExecutionLayers();
            Assert.Single(layers);
            Assert.Equal(3, layers[0].Count);
        }

        // ─── Critical Path ───

        [Fact]
        public void CriticalPath_LinearChain()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a", weight: 2).AddNode("b", weight: 3).AddNode("c", weight: 1);
            graph.AddEdge("b", "a").AddEdge("c", "b");
            var cp = graph.ComputeCriticalPath();
            Assert.Equal(new[] { "a", "b", "c" }, cp.Path);
            Assert.Equal(6.0, cp.TotalWeight);
        }

        [Fact]
        public void CriticalPath_ChoosesHeavierBranch()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("start", weight: 1);
            graph.AddNode("fast", weight: 1);
            graph.AddNode("slow", weight: 10);
            graph.AddNode("end", weight: 1);
            graph.AddEdge("fast", "start").AddEdge("slow", "start");
            graph.AddEdge("end", "fast").AddEdge("end", "slow");
            var cp = graph.ComputeCriticalPath();
            Assert.Contains("slow", cp.Path);
            Assert.Equal(12.0, cp.TotalWeight);
        }

        [Fact]
        public void CriticalPath_SlackIsZeroOnCritical()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a", weight: 1).AddNode("b", weight: 1);
            graph.AddEdge("b", "a");
            var cp = graph.ComputeCriticalPath();
            Assert.Equal(0, cp.Slack["a"]);
            Assert.Equal(0, cp.Slack["b"]);
        }

        [Fact]
        public void CriticalPath_NonCriticalHasPositiveSlack()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("start", weight: 1);
            graph.AddNode("fast", weight: 1);
            graph.AddNode("slow", weight: 5);
            graph.AddNode("end", weight: 1);
            graph.AddEdge("fast", "start").AddEdge("slow", "start");
            graph.AddEdge("end", "fast").AddEdge("end", "slow");
            var cp = graph.ComputeCriticalPath();
            Assert.True(cp.Slack["fast"] > 0);
        }

        // ─── Full Analysis ───

        [Fact]
        public void Analyze_EmptyGraph()
        {
            var graph = new PromptDependencyGraph();
            var a = graph.Analyze();
            Assert.Equal(0, a.NodeCount);
            Assert.True(a.IsDAG);
        }

        [Fact]
        public void Analyze_IdentifiesRootsAndLeaves()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c")
                .AddEdge("b", "a").AddEdge("c", "b");
            var a = graph.Analyze();
            Assert.Contains("a", a.RootNodes);
            Assert.Contains("c", a.LeafNodes);
        }

        [Fact]
        public void Analyze_IdentifiesBottlenecks()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("hub");
            for (int i = 0; i < 5; i++)
            {
                graph.AddNode($"d{i}");
                graph.AddEdge($"d{i}", "hub");
            }
            var a = graph.Analyze();
            Assert.Contains("hub", a.Bottlenecks);
        }

        [Fact]
        public void Analyze_CyclicGraph_ReportsCycles()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b");
            graph.AddEdge("a", "b").AddEdge("b", "a");
            var a = graph.Analyze();
            Assert.False(a.IsDAG);
            Assert.NotEmpty(a.Cycles);
        }

        // ─── Export Formats ───

        [Fact]
        public void ToDot_ContainsNodesAndEdges()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a", "Alpha").AddNode("b", "Beta").AddEdge("b", "a");
            var dot = graph.ToDot();
            Assert.Contains("digraph", dot);
            Assert.Contains("Alpha", dot);
            Assert.Contains("\"a\" -> \"b\"", dot);
        }

        [Fact]
        public void ToMermaid_ContainsNodesAndEdges()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a", "Alpha").AddNode("b", "Beta").AddEdge("b", "a");
            var mermaid = graph.ToMermaid();
            Assert.Contains("graph TD", mermaid);
            Assert.Contains("a --> b", mermaid);
        }

        [Fact]
        public void ToJson_IsValidJson()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a");
            var json = graph.ToJson();
            Assert.Contains("\"nodes\"", json);
            Assert.Contains("\"analysis\"", json);
        }

        [Fact]
        public void GenerateReport_ContainsKeyInfo()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddEdge("b", "a");
            var report = graph.GenerateReport();
            Assert.Contains("PROMPT DEPENDENCY GRAPH REPORT", report);
            Assert.Contains("Nodes: 2", report);
            Assert.Contains("DAG: Yes", report);
        }

        // ─── Auto-Detect ───

        [Fact]
        public void AutoDetect_FindsTemplateReferences()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("context").AddNode("draft");
            graph.AutoDetectDependencies("draft", "Based on {{context}}, write a draft.");
            Assert.Contains("context", graph.Nodes["draft"].Dependencies);
        }

        [Fact]
        public void AutoDetect_IgnoresUnknownReferences()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("draft");
            graph.AutoDetectDependencies("draft", "Based on {{nonexistent}}, write.");
            Assert.Empty(graph.Nodes["draft"].Dependencies);
        }

        [Fact]
        public void AutoDetect_IgnoresSelfReference()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("draft");
            graph.AutoDetectDependencies("draft", "Improve {{draft}} iteratively.");
            Assert.Empty(graph.Nodes["draft"].Dependencies);
        }

        // ─── Merge & Subgraph ───

        [Fact]
        public void Merge_CombinesGraphs()
        {
            var g1 = new PromptDependencyGraph();
            g1.AddNode("a");
            var g2 = new PromptDependencyGraph();
            g2.AddNode("b");
            g1.Merge(g2);
            Assert.Equal(2, g1.Count);
        }

        [Fact]
        public void Merge_SkipsDuplicates()
        {
            var g1 = new PromptDependencyGraph();
            g1.AddNode("a");
            var g2 = new PromptDependencyGraph();
            g2.AddNode("a");
            g1.Merge(g2);
            Assert.Equal(1, g1.Count);
        }

        [Fact]
        public void Subgraph_ExtractsSubset()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c")
                .AddEdge("b", "a").AddEdge("c", "b");
            var sub = graph.Subgraph(new[] { "a", "b" });
            Assert.Equal(2, sub.Count);
            Assert.Contains("a", sub.Nodes["b"].Dependencies);
        }

        [Fact]
        public void Subgraph_ExcludesExternalEdges()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c")
                .AddEdge("b", "a").AddEdge("c", "b");
            var sub = graph.Subgraph(new[] { "b", "c" });
            // b's dependency on a should be excluded since a isn't in subgraph
            Assert.Empty(sub.Nodes["b"].Dependencies);
        }

        // ─── Impact & Requirements ───

        [Fact]
        public void GetImpactSet_ReturnsAllDownstream()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c").AddNode("d");
            graph.AddEdge("b", "a").AddEdge("c", "b").AddEdge("d", "c");
            var impact = graph.GetImpactSet("a");
            Assert.Equal(3, impact.Count);
        }

        [Fact]
        public void GetRequirementSet_ReturnsAllUpstream()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c").AddNode("d");
            graph.AddEdge("b", "a").AddEdge("c", "b").AddEdge("d", "c");
            var reqs = graph.GetRequirementSet("d");
            Assert.Equal(3, reqs.Count);
        }

        // ─── PromptNode Tests ───

        [Fact]
        public void PromptNode_Metadata_Works()
        {
            var node = new PromptNode("test");
            node.Metadata["model"] = "gpt-4";
            Assert.Equal("gpt-4", node.Metadata["model"]);
        }

        [Fact]
        public void PromptNode_DependsOn_Chainable()
        {
            var node = new PromptNode("test");
            var result = node.DependsOn("a").DependsOn("b");
            Assert.Same(node, result);
            Assert.Equal(2, node.Dependencies.Count);
        }

        [Fact]
        public void PromptNode_DependsOnSelf_Throws()
        {
            var node = new PromptNode("test");
            Assert.Throws<ArgumentException>(() => node.DependsOn("test"));
        }

        // ─── DependencyCycle ───

        [Fact]
        public void DependencyCycle_ToString_ShowsCycle()
        {
            var cycle = new DependencyCycle(new[] { "a", "b", "c" }, 3.0);
            Assert.Equal("a → b → c → a", cycle.ToString());
        }

        // ─── Complex Scenarios ───

        [Fact]
        public void ComplexPipeline_PromptWorkflow()
        {
            var graph = new PromptDependencyGraph()
                .AddNode("gather", "Gather Context", 2.0)
                .AddNode("classify", "Classify Intent", 1.0)
                .AddNode("retrieve", "Retrieve Knowledge", 3.0)
                .AddNode("draft", "Draft Response", 4.0)
                .AddNode("review", "Review Quality", 1.5)
                .AddNode("format", "Format Output", 0.5)
                .AddEdge("classify", "gather")
                .AddEdge("retrieve", "classify")
                .AddEdge("draft", "gather")
                .AddEdge("draft", "retrieve")
                .AddEdge("review", "draft")
                .AddEdge("format", "review");

            var analysis = graph.Analyze();
            Assert.True(analysis.IsDAG);
            Assert.Equal(6, analysis.NodeCount);
            Assert.Equal(6, analysis.EdgeCount);
            Assert.Contains("gather", analysis.RootNodes);
            Assert.Contains("format", analysis.LeafNodes);
            Assert.True(analysis.CriticalPath!.TotalWeight > 0);
            Assert.True(analysis.ExecutionLayers.Count >= 3);
        }

        [Fact]
        public void FluentChaining_Works()
        {
            var graph = new PromptDependencyGraph()
                .AddNode("a")
                .AddNode("b")
                .AddNode("c")
                .AddEdge("b", "a")
                .AddEdge("c", "b");
            Assert.Equal(3, graph.Count);
        }

        [Fact]
        public void SingleNode_AnalyzesCorrectly()
        {
            var graph = new PromptDependencyGraph().AddNode("solo");
            var a = graph.Analyze();
            Assert.Equal(1, a.NodeCount);
            Assert.Equal(0, a.EdgeCount);
            Assert.Contains("solo", a.RootNodes);
            Assert.Contains("solo", a.LeafNodes);
        }

        [Fact]
        public void WideGraph_ManyRoots()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("sink");
            for (int i = 0; i < 10; i++)
            {
                graph.AddNode($"r{i}");
                graph.AddEdge("sink", $"r{i}");
            }
            var a = graph.Analyze();
            Assert.Equal(10, a.RootNodes.Count);
            Assert.Single(a.LeafNodes);
        }

        [Fact]
        public void DiamondPattern_ExecutionLayers()
        {
            var graph = new PromptDependencyGraph();
            graph.AddNode("a").AddNode("b").AddNode("c").AddNode("d");
            graph.AddEdge("b", "a").AddEdge("c", "a").AddEdge("d", "b").AddEdge("d", "c");
            var layers = graph.ComputeExecutionLayers();
            Assert.Equal(3, layers.Count);
            Assert.Single(layers[0]); // a
            Assert.Equal(2, layers[1].Count); // b, c parallel
            Assert.Single(layers[2]); // d
        }
    }
}
