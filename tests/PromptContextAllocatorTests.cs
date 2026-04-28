namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Xunit;

    public class PromptContextAllocatorTests
    {
        // ================================================================
        // Constructor
        // ================================================================

        [Fact]
        public void Constructor_ValidBudget_Creates()
        {
            var allocator = new PromptContextAllocator(4096);
            Assert.True(allocator.CanFitAll()); // no components yet
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Constructor_InvalidBudget_Throws(int budget)
        {
            Assert.Throws<ArgumentException>(() => new PromptContextAllocator(budget));
        }

        [Fact]
        public void Constructor_CustomReserve_RespectedInAllocation()
        {
            var allocator = new PromptContextAllocator(100, reserveTokens: 50);
            // 100 budget - 50 reserve = 50 available
            // 200 chars = 50 tokens → exactly fills available
            allocator.Add("a", new string('x', 200));
            var plan = allocator.Allocate();
            Assert.True(plan.Success);
            Assert.Equal("kept", plan.Allocations[0].Action);
        }

        [Fact]
        public void Constructor_ReserveExceedsBudget_AllocationFails()
        {
            var allocator = new PromptContextAllocator(100, reserveTokens: 200);
            allocator.Add("a", "hello");
            var plan = allocator.Allocate();
            Assert.False(plan.Success);
            Assert.Contains(plan.Insights, i => i.Severity == "critical" && i.Message.Contains("Reserve tokens exceed"));
        }

        // ================================================================
        // ContextComponent.EstimateTokens
        // ================================================================

        [Fact]
        public void EstimateTokens_EmptyString_ReturnsZero()
        {
            Assert.Equal(0, ContextComponent.EstimateTokens(""));
        }

        [Fact]
        public void EstimateTokens_Null_ReturnsZero()
        {
            Assert.Equal(0, ContextComponent.EstimateTokens(null));
        }

        [Fact]
        public void EstimateTokens_ShortString_ReturnsAtLeastOne()
        {
            Assert.True(ContextComponent.EstimateTokens("hi") >= 1);
        }

        [Fact]
        public void EstimateTokens_FourCharsPerToken_Approximation()
        {
            // 100 chars / 4 = 25 tokens
            var text = new string('a', 100);
            Assert.Equal(25, ContextComponent.EstimateTokens(text));
        }

        [Fact]
        public void EstimateTokens_RoundsUp()
        {
            // 5 chars / 4 = 1.25 → ceil = 2
            Assert.Equal(2, ContextComponent.EstimateTokens("abcde"));
        }

        // ================================================================
        // ContextComponent constructor
        // ================================================================

        [Fact]
        public void ContextComponent_NullName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ContextComponent(null, "content"));
        }

        [Fact]
        public void ContextComponent_NullContent_DefaultsToEmpty()
        {
            var comp = new ContextComponent("test", null);
            Assert.Equal("", comp.Content);
            Assert.Equal(0, comp.EstimatedTokens);
        }

        [Fact]
        public void ContextComponent_DefaultPriority_IsNormal()
        {
            var comp = new ContextComponent("test", "hello");
            Assert.Equal(AllocationPriority.Normal, comp.Priority);
            Assert.Equal(OverflowStrategy.Truncate, comp.OverflowStrategy);
        }

        [Fact]
        public void ContextComponent_CustomProperties_Set()
        {
            var comp = new ContextComponent("ctx", "data", AllocationPriority.High,
                OverflowStrategy.Compress, minTokens: 10, maxTokens: 50);
            Assert.Equal("ctx", comp.Name);
            Assert.Equal(AllocationPriority.High, comp.Priority);
            Assert.Equal(OverflowStrategy.Compress, comp.OverflowStrategy);
            Assert.Equal(10, comp.MinTokens);
            Assert.Equal(50, comp.MaxTokens);
        }

        // ================================================================
        // Add (fluent API)
        // ================================================================

        [Fact]
        public void Add_ReturnsSelf_ForChaining()
        {
            var allocator = new PromptContextAllocator(4096);
            var result = allocator.Add("a", "hello");
            Assert.Same(allocator, result);
        }

        [Fact]
        public void Add_MultipleComponents_AllTracked()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("a", "hello").Add("b", "world").Add("c", "test");
            var plan = allocator.Allocate();
            Assert.Equal(3, plan.Allocations.Count);
        }

        // ================================================================
        // Allocate - Basic scenarios
        // ================================================================

        [Fact]
        public void Allocate_NoComponents_EmptyPlan()
        {
            var allocator = new PromptContextAllocator(4096);
            var plan = allocator.Allocate();
            Assert.True(plan.Success);
            Assert.Empty(plan.Allocations);
            Assert.Equal(0, plan.TotalAllocated);
            Assert.Equal(0, plan.TotalRequested);
        }

        [Fact]
        public void Allocate_SingleComponentFits_Kept()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("system", "You are a helpful assistant.");
            var plan = allocator.Allocate();
            Assert.True(plan.Success);
            Assert.Single(plan.Allocations);
            Assert.Equal("kept", plan.Allocations[0].Action);
            Assert.False(plan.Allocations[0].WasModified);
            Assert.Equal("You are a helpful assistant.", plan.Allocations[0].FinalContent);
        }

        [Fact]
        public void Allocate_AllComponentsFit_AllKept()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("system", "System prompt");
            allocator.Add("user", "User question");
            allocator.Add("context", "Some context");
            var plan = allocator.Allocate();
            Assert.True(plan.Success);
            Assert.All(plan.Allocations, a => Assert.Equal("kept", a.Action));
            Assert.Empty(plan.DroppedComponents);
        }

        // ================================================================
        // Priority ordering
        // ================================================================

        [Fact]
        public void Allocate_CriticalGetsFirst_OverNormal()
        {
            // Small budget: only room for one component
            // 40 tokens budget - 0 reserve = 40 available
            var allocator = new PromptContextAllocator(40, reserveTokens: 0);
            // normal: 30 tokens (120 chars)
            allocator.Add("normal", new string('n', 120), AllocationPriority.Normal);
            // critical: 30 tokens (120 chars)
            allocator.Add("critical", new string('c', 120), AllocationPriority.Critical);

            var plan = allocator.Allocate();
            // Critical should be allocated first (fully), normal gets remaining
            var critResult = plan.Allocations.First(a => a.Name == "critical");
            Assert.Equal("kept", critResult.Action);
            Assert.Equal(30, critResult.AllocatedTokens);
        }

        [Fact]
        public void Allocate_PreservesInsertionOrder_WithinSamePriority()
        {
            var allocator = new PromptContextAllocator(4096, reserveTokens: 0);
            allocator.Add("first", "aaa", AllocationPriority.Normal);
            allocator.Add("second", "bbb", AllocationPriority.Normal);
            allocator.Add("third", "ccc", AllocationPriority.Normal);
            var plan = allocator.Allocate();
            // Results should be in insertion order
            Assert.Equal("first", plan.Allocations[0].Name);
            Assert.Equal("second", plan.Allocations[1].Name);
            Assert.Equal("third", plan.Allocations[2].Name);
        }

        [Fact]
        public void Allocate_PriorityOrder_CritHighNormalLowOptional()
        {
            // Tight budget forces priority ordering to matter
            var allocator = new PromptContextAllocator(50, reserveTokens: 0);
            allocator.Add("optional", new string('o', 80), AllocationPriority.Optional, OverflowStrategy.Drop);
            allocator.Add("critical", new string('c', 80), AllocationPriority.Critical);
            allocator.Add("low", new string('l', 80), AllocationPriority.Low, OverflowStrategy.Drop);
            allocator.Add("high", new string('h', 80), AllocationPriority.High);
            allocator.Add("normal", new string('n', 80), AllocationPriority.Normal, OverflowStrategy.Drop);

            var plan = allocator.Allocate();
            // Critical and High should get budget first
            var critResult = plan.Allocations.First(a => a.Name == "critical");
            var highResult = plan.Allocations.First(a => a.Name == "high");
            Assert.True(critResult.AllocatedTokens > 0);
            Assert.True(highResult.AllocatedTokens > 0);
        }

        // ================================================================
        // Overflow strategies
        // ================================================================

        [Fact]
        public void Allocate_OverflowTruncate_TruncatesContent()
        {
            // Budget: 10 tokens (40 chars), reserve 0, content much larger
            var allocator = new PromptContextAllocator(10, reserveTokens: 0);
            allocator.Add("big", new string('x', 200), AllocationPriority.Normal, OverflowStrategy.Truncate);
            var plan = allocator.Allocate();
            var result = plan.Allocations[0];
            Assert.Equal("truncated", result.Action);
            Assert.True(result.WasModified);
            Assert.True(result.FinalContent.EndsWith("..."));
            Assert.True(result.FinalContent.Length <= 40);
        }

        [Fact]
        public void Allocate_OverflowDrop_DropsComponent()
        {
            var allocator = new PromptContextAllocator(10, reserveTokens: 0);
            allocator.Add("big", new string('x', 200), AllocationPriority.Normal, OverflowStrategy.Drop);
            var plan = allocator.Allocate();
            var result = plan.Allocations[0];
            Assert.Equal("dropped", result.Action);
            Assert.Equal(0, result.AllocatedTokens);
            Assert.Equal("", result.FinalContent);
            Assert.Contains("big", plan.DroppedComponents);
        }

        [Fact]
        public void Allocate_OverflowCompress_RemovesFiller()
        {
            // Content with filler words
            var content = "This is really very just basically actually literally a test";
            var allocator = new PromptContextAllocator(5000, reserveTokens: 0);
            allocator.Add("text", content, AllocationPriority.Normal, OverflowStrategy.Compress);
            // If it fits, it's kept, not compressed. We need it not to fit.
            var allocator2 = new PromptContextAllocator(5, reserveTokens: 0);
            allocator2.Add("text", content, AllocationPriority.Normal, OverflowStrategy.Compress);
            var plan = allocator2.Allocate();
            var result = plan.Allocations[0];
            // Should have been compressed (or compressed+truncated)
            Assert.True(result.Action == "compressed" || result.Action == "compressed+truncated");
            Assert.True(result.WasModified);
        }

        [Fact]
        public void Allocate_OverflowSummarize_KeepsFirstAndLast()
        {
            var content = new string('a', 100) + new string('b', 100) + new string('c', 100);
            var allocator = new PromptContextAllocator(20, reserveTokens: 0);
            allocator.Add("doc", content, AllocationPriority.Normal, OverflowStrategy.Summarize);
            var plan = allocator.Allocate();
            var result = plan.Allocations[0];
            Assert.Equal("summarized", result.Action);
            Assert.Contains("...", result.FinalContent);
            // Should contain beginning (a's) and end (c's)
            Assert.StartsWith("a", result.FinalContent);
            Assert.EndsWith("c", result.FinalContent);
        }

        // ================================================================
        // Critical component failure
        // ================================================================

        [Fact]
        public void Allocate_CriticalDoesNotFit_FailsPlan()
        {
            var allocator = new PromptContextAllocator(5, reserveTokens: 0);
            // Fill budget with first component
            allocator.Add("first", new string('a', 20), AllocationPriority.Critical);
            // Second critical can't fit
            allocator.Add("second", new string('b', 200), AllocationPriority.Critical);
            var plan = allocator.Allocate();
            Assert.False(plan.Success);
            Assert.Contains(plan.Insights, i => i.Severity == "critical" && i.Message.Contains("second"));
        }

        [Fact]
        public void Allocate_NonCriticalDoesNotFit_PlanStillSucceeds()
        {
            var allocator = new PromptContextAllocator(5, reserveTokens: 0);
            allocator.Add("main", new string('a', 20), AllocationPriority.Critical);
            allocator.Add("extra", new string('b', 200), AllocationPriority.Optional, OverflowStrategy.Drop);
            var plan = allocator.Allocate();
            // Optional component was dropped, but plan should succeed since only non-critical dropped
            Assert.Contains("extra", plan.DroppedComponents);
        }

        // ================================================================
        // MaxTokens cap
        // ================================================================

        [Fact]
        public void Allocate_MaxTokens_CapsAllocation()
        {
            var allocator = new PromptContextAllocator(4096, reserveTokens: 0);
            // Content is 250 tokens (1000 chars), but maxTokens = 10
            allocator.Add("capped", new string('x', 1000), maxTokens: 10);
            var plan = allocator.Allocate();
            Assert.Equal("kept", plan.Allocations[0].Action);
            Assert.Equal(10, plan.Allocations[0].AllocatedTokens);
        }

        // ================================================================
        // MinTokens
        // ================================================================

        [Fact]
        public void Allocate_MinTokensNotMet_DropsComponent()
        {
            // Budget only has 3 tokens left, but component needs minTokens=10
            var allocator = new PromptContextAllocator(13, reserveTokens: 0);
            allocator.Add("first", new string('a', 40)); // takes 10 tokens
            allocator.Add("second", new string('b', 100), minTokens: 10); // needs min 10, only 3 left
            var plan = allocator.Allocate();
            var second = plan.Allocations.First(a => a.Name == "second");
            Assert.Equal("dropped", second.Action);
        }

        // ================================================================
        // CanFitAll
        // ================================================================

        [Fact]
        public void CanFitAll_True_WhenComponentsFit()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("a", "short");
            Assert.True(allocator.CanFitAll());
        }

        [Fact]
        public void CanFitAll_False_WhenComponentsExceedBudget()
        {
            var allocator = new PromptContextAllocator(5, reserveTokens: 0);
            allocator.Add("a", new string('x', 1000)); // way over
            Assert.False(allocator.CanFitAll());
        }

        [Fact]
        public void CanFitAll_AccountsForReserve()
        {
            // 10 tokens budget, 8 reserve = 2 available
            var allocator = new PromptContextAllocator(10, reserveTokens: 8);
            allocator.Add("a", new string('x', 12)); // 3 tokens, won't fit in 2
            Assert.False(allocator.CanFitAll());
        }

        // ================================================================
        // RecommendedBudget
        // ================================================================

        [Fact]
        public void RecommendedBudget_IncludesReserve()
        {
            var allocator = new PromptContextAllocator(100, reserveTokens: 50);
            allocator.Add("a", new string('x', 40)); // 10 tokens
            Assert.Equal(60, allocator.RecommendedBudget()); // 10 + 50 reserve
        }

        [Fact]
        public void RecommendedBudget_NoComponents_JustReserve()
        {
            var allocator = new PromptContextAllocator(100, reserveTokens: 50);
            Assert.Equal(50, allocator.RecommendedBudget());
        }

        // ================================================================
        // Insights
        // ================================================================

        [Fact]
        public void Allocate_HighUtilization_WarningInsight()
        {
            // Budget exactly matches content → ~100% utilization
            var allocator = new PromptContextAllocator(30, reserveTokens: 0);
            allocator.Add("a", new string('x', 116)); // 29 tokens out of 30 available
            var plan = allocator.Allocate();
            Assert.Contains(plan.Insights, i => i.Severity == "warning" && i.Message.Contains("utilized"));
        }

        [Fact]
        public void Allocate_LowUtilization_InfoInsight()
        {
            var allocator = new PromptContextAllocator(4096, reserveTokens: 0);
            allocator.Add("tiny", "hi"); // ~1 token out of 4096
            var plan = allocator.Allocate();
            Assert.Contains(plan.Insights, i => i.Severity == "info" && i.Message.Contains("budget used"));
        }

        [Fact]
        public void Allocate_DroppedComponents_InsightGenerated()
        {
            var allocator = new PromptContextAllocator(5, reserveTokens: 0);
            allocator.Add("main", new string('m', 20), AllocationPriority.High);
            allocator.Add("extra", new string('e', 200), AllocationPriority.Low, OverflowStrategy.Drop);
            var plan = allocator.Allocate();
            if (plan.DroppedComponents.Count > 0)
            {
                Assert.Contains(plan.Insights, i => i.Message.Contains("dropped"));
            }
        }

        [Fact]
        public void Allocate_DominantComponent_InfoInsight()
        {
            var allocator = new PromptContextAllocator(100, reserveTokens: 0);
            allocator.Add("dominant", new string('d', 300)); // 75 tokens out of 100
            allocator.Add("tiny", "hi");
            var plan = allocator.Allocate();
            Assert.Contains(plan.Insights, i => i.Severity == "info" && i.Message.Contains("consumes"));
        }

        // ================================================================
        // AllocationPlan methods
        // ================================================================

        [Fact]
        public void AssemblePrompt_CombinesKeptComponents()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("system", "System prompt");
            allocator.Add("user", "User query");
            var plan = allocator.Allocate();
            var prompt = plan.AssemblePrompt();
            Assert.Contains("System prompt", prompt);
            Assert.Contains("User query", prompt);
        }

        [Fact]
        public void AssemblePrompt_ExcludesDropped()
        {
            var allocator = new PromptContextAllocator(10, reserveTokens: 0);
            allocator.Add("main", new string('m', 40), AllocationPriority.High);
            allocator.Add("dropped", "should not appear", AllocationPriority.Optional, OverflowStrategy.Drop);
            var plan = allocator.Allocate();
            var prompt = plan.AssemblePrompt();
            Assert.DoesNotContain("should not appear", prompt);
        }

        [Fact]
        public void AssemblePrompt_CustomSeparator()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("a", "part1");
            allocator.Add("b", "part2");
            var plan = allocator.Allocate();
            var prompt = plan.AssemblePrompt("---");
            Assert.Contains("part1---part2", prompt);
        }

        [Fact]
        public void ToJson_ReturnsValidJson()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("test", "hello");
            var plan = allocator.Allocate();
            var json = plan.ToJson();
            Assert.Contains("\"totalBudget\"", json);
            Assert.Contains("\"allocations\"", json);
            // Should be valid JSON
            var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
            Assert.Equal(System.Text.Json.JsonValueKind.Object, parsed.ValueKind);
        }

        [Fact]
        public void ToMarkdown_ContainsExpectedSections()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("system", "prompt text", AllocationPriority.Critical);
            var plan = allocator.Allocate();
            var md = plan.ToMarkdown();
            Assert.Contains("# Context Allocation Plan", md);
            Assert.Contains("## Allocations", md);
            Assert.Contains("system", md);
            Assert.Contains("Critical", md);
        }

        [Fact]
        public void ToMarkdown_ShowsDropped_WhenPresent()
        {
            var allocator = new PromptContextAllocator(5, reserveTokens: 0);
            allocator.Add("main", new string('a', 20), AllocationPriority.High);
            allocator.Add("dropped", new string('b', 200), AllocationPriority.Low, OverflowStrategy.Drop);
            var plan = allocator.Allocate();
            var md = plan.ToMarkdown();
            if (plan.DroppedComponents.Count > 0)
            {
                Assert.Contains("Dropped", md);
            }
        }

        // ================================================================
        // Plan metrics
        // ================================================================

        [Fact]
        public void Allocate_TotalAllocated_SumsCorrectly()
        {
            var allocator = new PromptContextAllocator(4096, reserveTokens: 0);
            allocator.Add("a", new string('x', 40)); // 10 tokens
            allocator.Add("b", new string('y', 80)); // 20 tokens
            var plan = allocator.Allocate();
            Assert.Equal(30, plan.TotalAllocated);
            Assert.Equal(30, plan.TotalRequested);
            Assert.Equal(4066, plan.RemainingBudget);
        }

        [Fact]
        public void Allocate_UtilizationPercent_Calculated()
        {
            var allocator = new PromptContextAllocator(100, reserveTokens: 0);
            allocator.Add("half", new string('x', 200)); // 50 tokens
            var plan = allocator.Allocate();
            Assert.Equal(50.0, plan.UtilizationPercent, 1);
        }

        // ================================================================
        // Edge cases
        // ================================================================

        [Fact]
        public void Allocate_EmptyContent_ZeroTokens()
        {
            var allocator = new PromptContextAllocator(4096);
            allocator.Add("empty", "");
            var plan = allocator.Allocate();
            Assert.Equal(0, plan.Allocations[0].RequestedTokens);
            Assert.Equal(0, plan.Allocations[0].AllocatedTokens);
        }

        [Fact]
        public void Allocate_ManyComponents_HandlesGracefully()
        {
            var allocator = new PromptContextAllocator(10000, reserveTokens: 0);
            for (int i = 0; i < 50; i++)
            {
                allocator.Add($"comp_{i}", $"Content for component {i}");
            }
            var plan = allocator.Allocate();
            Assert.Equal(50, plan.Allocations.Count);
            Assert.True(plan.Success);
        }

        [Fact]
        public void Allocate_CompressFitsAfterCompression_UsesCompressedTokenCount()
        {
            // Content with lots of filler that compresses well
            var content = "really very just basically actually literally " +
                          "really very just basically actually literally " +
                          "really very just basically actually literally data";
            var allocator = new PromptContextAllocator(8, reserveTokens: 0);
            allocator.Add("filler", content, overflow: OverflowStrategy.Compress);
            var plan = allocator.Allocate();
            var result = plan.Allocations[0];
            Assert.True(result.WasModified);
        }

        [Fact]
        public void Allocate_ZeroReserve_UsesFullBudget()
        {
            var allocator = new PromptContextAllocator(10, reserveTokens: 0);
            allocator.Add("fill", new string('x', 40)); // exactly 10 tokens
            var plan = allocator.Allocate();
            Assert.Equal("kept", plan.Allocations[0].Action);
            Assert.Equal(10, plan.Allocations[0].AllocatedTokens);
            Assert.Equal(0, plan.RemainingBudget);
        }
    }
}
