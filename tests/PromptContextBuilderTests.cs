namespace Prompt.Tests
{
    using Xunit;
    using System.Linq;

    public class PromptContextBuilderTests
    {
        // ── Constructor ────────────────────────────────────────────────

        [Fact]
        public void Constructor_ValidBudget_Creates()
        {
            var builder = new PromptContextBuilder(4000);
            Assert.Equal(4000, builder.MaxTokens);
            Assert.Equal(0, builder.BlockCount);
        }

        [Fact]
        public void Constructor_MinBudget_Creates()
        {
            var builder = new PromptContextBuilder(1);
            Assert.Equal(1, builder.MaxTokens);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Constructor_InvalidBudget_Throws(int budget)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PromptContextBuilder(budget));
        }

        // ── AddBlock ───────────────────────────────────────────────────

        [Fact]
        public void AddBlock_ReturnsBuilder_ForChaining()
        {
            var builder = new PromptContextBuilder(1000);
            var result = builder.AddBlock("a", "content");
            Assert.Same(builder, result);
        }

        [Fact]
        public void AddBlock_IncrementsCount()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "hello");
            builder.AddBlock("b", "world");
            Assert.Equal(2, builder.BlockCount);
        }

        [Fact]
        public void AddBlock_DuplicateId_Throws()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("system", "instructions");
            Assert.Throws<ArgumentException>(() =>
                builder.AddBlock("system", "other"));
        }

        [Fact]
        public void AddBlock_DuplicateId_CaseInsensitive()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("System", "a");
            Assert.Throws<ArgumentException>(() =>
                builder.AddBlock("system", "b"));
        }

        [Fact]
        public void AddBlock_NullId_Throws()
        {
            var builder = new PromptContextBuilder(1000);
            Assert.Throws<ArgumentException>(() =>
                builder.AddBlock(null!, "content"));
        }

        [Fact]
        public void AddBlock_EmptyContent_Allowed()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("empty", "");
            Assert.Equal(1, builder.BlockCount);
        }

        [Fact]
        public void AddBlock_NullContent_TreatedAsEmpty()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("null", null!);
            var result = builder.Build();
            Assert.Equal(1, result.IncludedCount);
        }

        [Fact]
        public void AddBlock_PriorityClampedTo0_100()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("low", "a", priority: -50);
            builder.AddBlock("high", "b", priority: 200);
            var blocks = builder.GetBlocks();
            Assert.Equal(0, blocks.First(b => b.Id == "low").Priority);
            Assert.Equal(100, blocks.First(b => b.Id == "high").Priority);
        }

        [Fact]
        public void AddBlock_WithTags()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("docs", "content", tags: new[] { "reference", "rag" });
            var block = builder.GetBlocks().First();
            Assert.Contains("reference", block.Tags);
            Assert.Contains("rag", block.Tags);
        }

        // ── AddBlock (ContextBlock overload) ───────────────────────────

        [Fact]
        public void AddBlock_ContextBlock_Works()
        {
            var builder = new PromptContextBuilder(1000);
            var block = new ContextBlock("sys", "instructions", priority: 100);
            builder.AddBlock(block);
            Assert.Equal(1, builder.BlockCount);
        }

        [Fact]
        public void AddBlock_ContextBlock_Null_Throws()
        {
            var builder = new PromptContextBuilder(1000);
            Assert.Throws<ArgumentNullException>(() =>
                builder.AddBlock((ContextBlock)null!));
        }

        // ── RemoveBlock ────────────────────────────────────────────────

        [Fact]
        public void RemoveBlock_Existing_ReturnsTrue()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "content");
            Assert.True(builder.RemoveBlock("a"));
            Assert.Equal(0, builder.BlockCount);
        }

        [Fact]
        public void RemoveBlock_NonExistent_ReturnsFalse()
        {
            var builder = new PromptContextBuilder(1000);
            Assert.False(builder.RemoveBlock("missing"));
        }

        [Fact]
        public void RemoveBlock_AllowsReAdd()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "first");
            builder.RemoveBlock("a");
            builder.AddBlock("a", "second"); // should not throw
            Assert.Equal(1, builder.BlockCount);
        }

        // ── GetBlocks ──────────────────────────────────────────────────

        [Fact]
        public void GetBlocks_ReturnsAll()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "x");
            builder.AddBlock("b", "y");
            Assert.Equal(2, builder.GetBlocks().Count);
        }

        [Fact]
        public void GetBlocks_FilterByTag()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "x", tags: new[] { "rag" });
            builder.AddBlock("b", "y", tags: new[] { "system" });
            builder.AddBlock("c", "z", tags: new[] { "rag", "system" });

            var rag = builder.GetBlocks("rag");
            Assert.Equal(2, rag.Count);
            Assert.Contains(rag, b => b.Id == "a");
            Assert.Contains(rag, b => b.Id == "c");
        }

        // ── Build — empty ──────────────────────────────────────────────

        [Fact]
        public void Build_NoBlocks_ReturnsEmpty()
        {
            var builder = new PromptContextBuilder(1000);
            var result = builder.Build();
            Assert.Equal(string.Empty, result.Text);
            Assert.Equal(0, result.TotalTokens);
            Assert.Equal(0, result.IncludedCount);
            Assert.Equal(0, result.DroppedCount);
            Assert.Equal(0, result.TruncatedCount);
            Assert.Equal(1000, result.Budget);
        }

        // ── Build — single block ───────────────────────────────────────

        [Fact]
        public void Build_SingleBlock_IncludesContent()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("sys", "You are a helpful assistant.");
            var result = builder.Build();
            Assert.Contains("You are a helpful assistant.", result.Text);
            Assert.Equal(1, result.IncludedCount);
            Assert.Equal(0, result.DroppedCount);
        }

        [Fact]
        public void Build_SingleBlock_WithLabel()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("sys", "Be helpful.", label: "## System");
            var result = builder.Build();
            Assert.Contains("## System", result.Text);
            Assert.Contains("Be helpful.", result.Text);
            Assert.True(result.Text.IndexOf("## System") <
                         result.Text.IndexOf("Be helpful."));
        }

        // ── Build — priority ordering ──────────────────────────────────

        [Fact]
        public void Build_PreservesInsertionOrder_NotPriority()
        {
            var builder = new PromptContextBuilder(10000);
            builder.AddBlock("second", "BBB", priority: 100);
            builder.AddBlock("first", "AAA", priority: 50);
            var result = builder.Build();
            int posA = result.Text.IndexOf("AAA");
            int posB = result.Text.IndexOf("BBB");
            // Insertion order: second (BBB) then first (AAA)
            Assert.True(posB < posA, "Should preserve insertion order");
        }

        [Fact]
        public void Build_HighPriorityGetsTokensFirst()
        {
            // Very tight budget — only room for one block
            var builder = new PromptContextBuilder(10);
            builder.AddBlock("low", "This is low priority content",
                priority: 10, truncation: TruncationStrategy.DropIfOverBudget);
            builder.AddBlock("high", "High priority",
                priority: 90);
            var result = builder.Build();
            // High priority should be included
            Assert.Contains(result.Blocks, b => b.Id == "high");
        }

        // ── Build — truncation strategies ──────────────────────────────

        [Fact]
        public void Build_TruncationKeepStart_KeepsBeginning()
        {
            var builder = new PromptContextBuilder(20);
            string longContent = string.Join(" ", Enumerable.Repeat("word", 200));
            builder.AddBlock("doc", longContent,
                truncation: TruncationStrategy.KeepStart);
            var result = builder.Build();
            Assert.True(result.Text.StartsWith("word"));
            Assert.Contains("truncated", result.Text);
            Assert.Equal(1, result.TruncatedCount);
        }

        [Fact]
        public void Build_TruncationKeepEnd_KeepsEnding()
        {
            var builder = new PromptContextBuilder(20);
            string content = "START " + string.Join(" ", Enumerable.Repeat("mid", 200)) + " END";
            builder.AddBlock("log", content,
                truncation: TruncationStrategy.KeepEnd);
            var result = builder.Build();
            Assert.Contains("END", result.Text);
            Assert.Contains("truncated", result.Text);
        }

        [Fact]
        public void Build_TruncationKeepStartAndEnd()
        {
            var builder = new PromptContextBuilder(30);
            string content = "BEGINNING " +
                string.Join(" ", Enumerable.Repeat("filler", 200)) + " ENDING";
            builder.AddBlock("doc", content,
                truncation: TruncationStrategy.KeepStartAndEnd);
            var result = builder.Build();
            Assert.Contains("BEGINNING", result.Text);
            Assert.Contains("ENDING", result.Text);
            Assert.Contains("truncated", result.Text);
        }

        [Fact]
        public void Build_TruncationDrop_DropsWhenOverBudget()
        {
            var builder = new PromptContextBuilder(10);
            string longContent = string.Join(" ", Enumerable.Repeat("word", 200));
            builder.AddBlock("doc", longContent,
                truncation: TruncationStrategy.DropIfOverBudget);
            var result = builder.Build();
            Assert.Equal(0, result.IncludedCount);
            Assert.Equal(1, result.DroppedCount);
        }

        [Fact]
        public void Build_TruncationNever_IncludesFull()
        {
            var builder = new PromptContextBuilder(5);
            builder.AddBlock("sys", "You are a helpful AI assistant that always responds accurately.",
                truncation: TruncationStrategy.Never);
            var result = builder.Build();
            Assert.Contains("You are a helpful AI assistant", result.Text);
            Assert.False(result.Blocks[0].WasTruncated);
        }

        // ── Build — minTokens ─────────────────────────────────────────

        [Fact]
        public void Build_MinTokens_DropsWhenBelowMinimum()
        {
            var builder = new PromptContextBuilder(10);
            // High-priority block takes most of the budget
            builder.AddBlock("main", "The main important content here.", priority: 90);
            // This block needs at least 500 tokens but won't get them
            builder.AddBlock("docs", "Some docs",
                priority: 10, minTokens: 500);
            var result = builder.Build();
            Assert.Contains(result.Dropped, d => d.Id == "docs");
        }

        // ── Build — maxTokens per block ────────────────────────────────

        [Fact]
        public void Build_MaxTokens_CapsAllocation()
        {
            var builder = new PromptContextBuilder(10000);
            string longContent = string.Join(" ", Enumerable.Repeat("word", 500));
            builder.AddBlock("limited", longContent, maxTokens: 20);
            var result = builder.Build();
            // Should have been truncated to ~20 tokens
            Assert.True(result.Blocks[0].FinalTokens <= 30); // some overhead
        }

        // ── Build — multiple blocks, tight budget ──────────────────────

        [Fact]
        public void Build_MultipleBlocks_DropsLowPriority()
        {
            var builder = new PromptContextBuilder(15);
            builder.AddBlock("sys", "System instructions.", priority: 100,
                truncation: TruncationStrategy.Never);
            builder.AddBlock("profile", "User profile data.", priority: 80);
            builder.AddBlock("lowpri", string.Join(" ", Enumerable.Repeat("extra", 200)),
                priority: 10, truncation: TruncationStrategy.DropIfOverBudget);
            var result = builder.Build();
            // System should be included
            Assert.Contains(result.Blocks, b => b.Id == "sys");
            // Low priority long block should be dropped
            Assert.Contains(result.Dropped, d => d.Id == "lowpri");
        }

        // ── Build — separator ──────────────────────────────────────────

        [Fact]
        public void Build_CustomSeparator()
        {
            var builder = new PromptContextBuilder(1000);
            builder.WithSeparator("\n---\n");
            builder.AddBlock("a", "Block A");
            builder.AddBlock("b", "Block B");
            var result = builder.Build();
            Assert.Contains("---", result.Text);
        }

        [Fact]
        public void Build_EmptySeparator()
        {
            var builder = new PromptContextBuilder(1000);
            builder.WithSeparator("");
            builder.AddBlock("a", "AAA");
            builder.AddBlock("b", "BBB");
            var result = builder.Build();
            Assert.Contains("AAABBB", result.Text);
        }

        // ── Build — overflow marker ────────────────────────────────────

        [Fact]
        public void Build_CustomOverflowMarker()
        {
            var builder = new PromptContextBuilder(15);
            builder.WithOverflowMarker("\n[SNIP]\n");
            string longContent = string.Join(" ", Enumerable.Repeat("word", 200));
            builder.AddBlock("doc", longContent,
                truncation: TruncationStrategy.KeepStart);
            var result = builder.Build();
            Assert.Contains("[SNIP]", result.Text);
        }

        // ── ContextBuildResult properties ──────────────────────────────

        [Fact]
        public void Result_UsagePercent_Calculated()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("sys", "Hello world.");
            var result = builder.Build();
            Assert.True(result.UsagePercent > 0);
            Assert.True(result.UsagePercent < 100);
        }

        [Fact]
        public void Result_RemainingTokens_Correct()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("sys", "Hi.");
            var result = builder.Build();
            Assert.Equal(1000 - result.TotalTokens, result.RemainingTokens);
        }

        [Fact]
        public void Result_ZeroBudget_UsagePercent_Zero()
        {
            // Empty build returns 0% usage
            var builder = new PromptContextBuilder(1000);
            var result = builder.Build();
            Assert.Equal(0, result.UsagePercent);
        }

        // ── BuildText ──────────────────────────────────────────────────

        [Fact]
        public void BuildText_ReturnsSameAsResult()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "Hello");
            string text = builder.BuildText();
            var result = builder.Build();
            Assert.Equal(result.Text, text);
        }

        // ── EstimateTotalTokens ────────────────────────────────────────

        [Fact]
        public void EstimateTotalTokens_ReturnsPositive()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "Some content here.");
            Assert.True(builder.EstimateTotalTokens() > 0);
        }

        [Fact]
        public void EstimateTotalTokens_Empty_ReturnsZero()
        {
            var builder = new PromptContextBuilder(1000);
            Assert.Equal(0, builder.EstimateTotalTokens());
        }

        [Fact]
        public void EstimateTotalTokens_IncludesLabels()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("a", "Content", label: "## Header");
            int withLabel = builder.EstimateTotalTokens();

            var builder2 = new PromptContextBuilder(1000);
            builder2.AddBlock("a", "Content");
            int withoutLabel = builder2.EstimateTotalTokens();

            Assert.True(withLabel > withoutLabel);
        }

        // ── GetAllocationSummary ───────────────────────────────────────

        [Fact]
        public void GetAllocationSummary_ContainsKey_Info()
        {
            var builder = new PromptContextBuilder(1000);
            builder.AddBlock("sys", "Instructions", priority: 100, label: "System");
            builder.AddBlock("docs", "Documents", priority: 50);
            var summary = builder.GetAllocationSummary();
            Assert.Contains("Budget:", summary);
            Assert.Contains("Used:", summary);
            Assert.Contains("sys", summary);
            Assert.Contains("docs", summary);
            Assert.Contains("Included", summary);
        }

        [Fact]
        public void GetAllocationSummary_ShowsDropped()
        {
            var builder = new PromptContextBuilder(5);
            builder.AddBlock("main", "Important.", priority: 100);
            builder.AddBlock("extra", string.Join(" ", Enumerable.Repeat("filler", 200)),
                priority: 10, truncation: TruncationStrategy.DropIfOverBudget);
            var summary = builder.GetAllocationSummary();
            Assert.Contains("Dropped", summary);
            Assert.Contains("extra", summary);
        }

        // ── WithSeparator / WithOverflowMarker chaining ───────────────

        [Fact]
        public void WithSeparator_ReturnsBuilder()
        {
            var builder = new PromptContextBuilder(1000);
            var result = builder.WithSeparator("---");
            Assert.Same(builder, result);
        }

        [Fact]
        public void WithOverflowMarker_ReturnsBuilder()
        {
            var builder = new PromptContextBuilder(1000);
            var result = builder.WithOverflowMarker("[...]");
            Assert.Same(builder, result);
        }

        // ── ContextBlock ───────────────────────────────────────────────

        [Fact]
        public void ContextBlock_EstimatesTokens()
        {
            var block = new ContextBlock("test", "Hello world, this is a test.");
            Assert.True(block.EstimatedTokens > 0);
        }

        [Fact]
        public void ContextBlock_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ContextBlock("", "content"));
        }

        [Fact]
        public void ContextBlock_WhitespaceId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ContextBlock("   ", "content"));
        }

        [Fact]
        public void ContextBlock_MaxTokens_ClampsToMin1()
        {
            var block = new ContextBlock("test", "content", maxTokens: 0);
            Assert.Equal(1, block.MaxTokens);
        }

        [Fact]
        public void ContextBlock_MinTokens_ClampsToZero()
        {
            var block = new ContextBlock("test", "content", minTokens: -10);
            Assert.Equal(0, block.MinTokens);
        }

        [Fact]
        public void ContextBlock_DefaultValues()
        {
            var block = new ContextBlock("test", "content");
            Assert.Equal(50, block.Priority);
            Assert.Null(block.Label);
            Assert.Null(block.MaxTokens);
            Assert.Equal(0, block.MinTokens);
            Assert.Equal(TruncationStrategy.KeepStart, block.Truncation);
            Assert.Empty(block.Tags);
        }

        // ── Integration: realistic prompt assembly ─────────────────────

        [Fact]
        public void Integration_RealisticPromptAssembly()
        {
            var builder = new PromptContextBuilder(500);

            builder.AddBlock("system",
                "You are a helpful AI coding assistant.",
                priority: 100, label: "## System Instructions",
                truncation: TruncationStrategy.Never);

            builder.AddBlock("profile",
                "User: Senior Python developer, works on data pipelines.",
                priority: 80, label: "## User Profile",
                maxTokens: 100);

            builder.AddBlock("history",
                "User: How do I read a CSV file?\nAssistant: Use pandas.read_csv().\n" +
                "User: What about large files?\nAssistant: Use chunks parameter.",
                priority: 60, label: "## Recent Conversation",
                truncation: TruncationStrategy.KeepEnd);

            builder.AddBlock("docs",
                "pandas.read_csv() documentation: Reads a CSV file into a DataFrame...",
                priority: 40, label: "## Retrieved Documents",
                truncation: TruncationStrategy.KeepStart,
                tags: new[] { "rag" });

            var result = builder.Build();

            Assert.True(result.TotalTokens <= 500);
            Assert.True(result.IncludedCount >= 2); // at least system + profile
            Assert.Contains("## System Instructions", result.Text);
            Assert.Contains("helpful AI coding assistant", result.Text);

            // System should come before profile in output (insertion order)
            Assert.True(result.Text.IndexOf("System Instructions") <
                         result.Text.IndexOf("User Profile"));
        }

        [Fact]
        public void Integration_TightBudget_GracefulDegradation()
        {
            var builder = new PromptContextBuilder(30);

            builder.AddBlock("system",
                "You are a helpful assistant.",
                priority: 100, truncation: TruncationStrategy.Never);

            builder.AddBlock("context",
                string.Join("\n", Enumerable.Repeat("Line of important context.", 50)),
                priority: 50, truncation: TruncationStrategy.KeepStart,
                minTokens: 5);

            builder.AddBlock("optional",
                string.Join("\n", Enumerable.Repeat("Optional reference material.", 100)),
                priority: 10, truncation: TruncationStrategy.DropIfOverBudget);

            var result = builder.Build();

            // System always included (Never truncation)
            Assert.Contains(result.Blocks, b => b.Id == "system" && !b.WasTruncated);
            // Optional likely dropped (DropIfOverBudget with tight budget)
            // Context may or may not fit depending on token estimation
            Assert.True(result.IncludedCount >= 1);
        }

        // ── BlockResult properties ─────────────────────────────────────

        [Fact]
        public void BlockResult_NonTruncated_OriginalEqualsFinal()
        {
            var builder = new PromptContextBuilder(10000);
            builder.AddBlock("small", "Short text.");
            var result = builder.Build();
            var block = result.Blocks[0];
            Assert.False(block.WasTruncated);
            Assert.Equal(block.OriginalTokens, block.FinalTokens);
        }

        [Fact]
        public void BlockResult_Truncated_FinalLessThanOriginal()
        {
            var builder = new PromptContextBuilder(20);
            string longContent = string.Join(" ", Enumerable.Repeat("word", 500));
            builder.AddBlock("long", longContent);
            var result = builder.Build();
            var block = result.Blocks[0];
            Assert.True(block.WasTruncated);
            Assert.True(block.FinalTokens < block.OriginalTokens);
        }

        // ── DroppedBlock properties ────────────────────────────────────

        [Fact]
        public void DroppedBlock_HasReasonAndTokens()
        {
            var builder = new PromptContextBuilder(5);
            builder.AddBlock("big", string.Join(" ", Enumerable.Repeat("word", 200)),
                truncation: TruncationStrategy.DropIfOverBudget);
            var result = builder.Build();
            Assert.Equal(1, result.Dropped.Count);
            Assert.Equal("big", result.Dropped[0].Id);
            Assert.True(result.Dropped[0].RequestedTokens > 0);
            Assert.False(string.IsNullOrEmpty(result.Dropped[0].Reason));
        }
    }
}
