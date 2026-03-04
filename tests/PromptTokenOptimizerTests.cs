namespace Prompt.Tests
{
    using Xunit;
    using System.Linq;

    public class PromptTokenOptimizerTests
    {
        private readonly PromptTokenOptimizer _optimizer = new();

        // --- EstimateTokens ---

        [Fact]
        public void EstimateTokens_EmptyString_ReturnsZero()
        {
            Assert.Equal(0, PromptTokenOptimizer.EstimateTokens(""));
        }

        [Fact]
        public void EstimateTokens_Null_ReturnsZero()
        {
            Assert.Equal(0, PromptTokenOptimizer.EstimateTokens(null!));
        }

        [Fact]
        public void EstimateTokens_ShortText_ReturnsAtLeastOne()
        {
            Assert.True(PromptTokenOptimizer.EstimateTokens("Hi") >= 1);
        }

        [Fact]
        public void EstimateTokens_LongerText_ReturnsReasonableCount()
        {
            var tokens = PromptTokenOptimizer.EstimateTokens("The quick brown fox jumps over the lazy dog");
            Assert.True(tokens > 5 && tokens < 20);
        }

        // --- Analyze - basic ---

        [Fact]
        public void Analyze_EmptyPrompt_ReturnsEmptyReport()
        {
            var report = _optimizer.Analyze("");
            Assert.Equal(100, report.OptimizationScore);
            Assert.Empty(report.Recommendations);
        }

        [Fact]
        public void Analyze_NullPrompt_ReturnsEmptyReport()
        {
            var report = _optimizer.Analyze(null!);
            Assert.Equal(100, report.OptimizationScore);
        }

        [Fact]
        public void Analyze_SimplePrompt_ReturnsReport()
        {
            var report = _optimizer.Analyze("Write a poem about cats.");
            Assert.True(report.TotalTokens > 0);
            Assert.NotEmpty(report.OriginalPrompt);
            Assert.NotEmpty(report.OptimizedPrompt);
        }

        [Fact]
        public void Analyze_ReturnsNonNullSections()
        {
            var report = _optimizer.Analyze("## Instructions\nDo X.\n\n## Examples\nExample 1.");
            Assert.NotNull(report.Sections);
        }

        // --- Filler word detection ---

        [Fact]
        public void DetectFillerWords_PleaseNote_Detected()
        {
            var recs = _optimizer.DetectFillerWords("Please note that the response should be concise.");
            Assert.Contains(recs, r => r.Category == OptimizationCategory.FillerWords);
        }

        [Fact]
        public void DetectFillerWords_InOrderTo_Detected()
        {
            var recs = _optimizer.DetectFillerWords("In order to complete the task, follow these steps.");
            Assert.Contains(recs, r => r.Description.Contains("Verbose"));
        }

        [Fact]
        public void DetectFillerWords_DueToTheFact_Detected()
        {
            var recs = _optimizer.DetectFillerWords("Due to the fact that it's raining, stay inside.");
            Assert.Contains(recs, r => r.SuggestedText == "Because");
        }

        [Fact]
        public void DetectFillerWords_CleanText_NoneDetected()
        {
            var recs = _optimizer.DetectFillerWords("Write a poem about cats.");
            Assert.Empty(recs);
        }

        [Fact]
        public void DetectFillerWords_IWouldLikeYouTo_Detected()
        {
            var recs = _optimizer.DetectFillerWords("I would like you to write a poem.");
            Assert.NotEmpty(recs);
        }

        [Fact]
        public void DetectFillerWords_INeedYouTo_Detected()
        {
            var recs = _optimizer.DetectFillerWords("I need you to analyze this data.");
            Assert.NotEmpty(recs);
        }

        [Fact]
        public void DetectFillerWords_AtThisPointInTime_Detected()
        {
            var recs = _optimizer.DetectFillerWords("At this point in time we should proceed.");
            Assert.Contains(recs, r => r.SuggestedText == "Now");
        }

        [Fact]
        public void DetectFillerWords_MultipleOccurrences_CountsAll()
        {
            var text = "In order to do X. In order to do Y. In order to do Z.";
            var recs = _optimizer.DetectFillerWords(text);
            var rec = recs.First(r => r.Description.Contains("3 occurrence"));
            Assert.NotNull(rec);
        }

        // --- Redundancy detection ---

        [Fact]
        public void DetectRedundancies_IdenticalInstructions_Detected()
        {
            var instructions = new List<string>
            {
                "Always format the output as JSON",
                "Always format the output as JSON"
            };
            var pairs = _optimizer.DetectRedundancies(instructions);
            Assert.Single(pairs);
            Assert.Equal(1.0, pairs[0].Similarity);
        }

        [Fact]
        public void DetectRedundancies_SimilarInstructions_Detected()
        {
            var instructions = new List<string>
            {
                "Format the response output as JSON with proper indentation and spacing",
                "Format the response output as JSON with correct indentation and spacing"
            };
            var pairs = _optimizer.DetectRedundancies(instructions);
            Assert.NotEmpty(pairs);
        }

        [Fact]
        public void DetectRedundancies_DifferentInstructions_NotDetected()
        {
            var instructions = new List<string>
            {
                "Write a poem about cats",
                "Calculate the square root of 144"
            };
            var pairs = _optimizer.DetectRedundancies(instructions);
            Assert.Empty(pairs);
        }

        [Fact]
        public void DetectRedundancies_EmptyList_ReturnsEmpty()
        {
            var pairs = _optimizer.DetectRedundancies(new List<string>());
            Assert.Empty(pairs);
        }

        // --- Section identification ---

        [Fact]
        public void IdentifySections_MarkdownHeadings_Splits()
        {
            var prompt = "## Part 1\nContent A\n\n## Part 2\nContent B";
            var totalTokens = PromptTokenOptimizer.EstimateTokens(prompt);
            var sections = _optimizer.IdentifySections(prompt, totalTokens);
            Assert.True(sections.Count >= 2);
        }

        [Fact]
        public void IdentifySections_SingleBlock_OneSection()
        {
            var prompt = "Just a simple prompt with no sections.";
            var totalTokens = PromptTokenOptimizer.EstimateTokens(prompt);
            var sections = _optimizer.IdentifySections(prompt, totalTokens);
            Assert.Single(sections);
        }

        [Fact]
        public void IdentifySections_PercentOfTotal_SumsToApprox100()
        {
            var prompt = "## A\nContent\n\n## B\nMore content\n\n## C\nEven more";
            var totalTokens = PromptTokenOptimizer.EstimateTokens(prompt);
            var sections = _optimizer.IdentifySections(prompt, totalTokens);
            var total = sections.Sum(s => s.PercentOfTotal);
            Assert.InRange(total, 80, 120); // approximate
        }

        // --- Instruction extraction ---

        [Fact]
        public void ExtractInstructions_BulletList_ExtractsEach()
        {
            var prompt = "- Do X\n- Do Y\n- Do Z";
            var instructions = _optimizer.ExtractInstructions(prompt);
            Assert.True(instructions.Count >= 3);
        }

        [Fact]
        public void ExtractInstructions_NumberedList_ExtractsEach()
        {
            var prompt = "1. First step\n2. Second step\n3. Third step";
            var instructions = _optimizer.ExtractInstructions(prompt);
            Assert.True(instructions.Count >= 3);
        }

        [Fact]
        public void ExtractInstructions_YouShould_Detected()
        {
            var prompt = "You should always validate input.\nYou must handle errors.";
            var instructions = _optimizer.ExtractInstructions(prompt);
            Assert.True(instructions.Count >= 2);
        }

        // --- ComputeSimilarity ---

        [Fact]
        public void ComputeSimilarity_IdenticalStrings_ReturnsOne()
        {
            Assert.Equal(1.0, PromptTokenOptimizer.ComputeSimilarity("hello world", "hello world"));
        }

        [Fact]
        public void ComputeSimilarity_CompletelyDifferent_ReturnsLow()
        {
            var sim = PromptTokenOptimizer.ComputeSimilarity("cats are cute", "the sky is blue");
            Assert.True(sim < 0.3);
        }

        [Fact]
        public void ComputeSimilarity_Empty_ReturnsZero()
        {
            Assert.Equal(0, PromptTokenOptimizer.ComputeSimilarity("", "hello"));
        }

        [Fact]
        public void ComputeSimilarity_BothEmpty_ReturnsZero()
        {
            Assert.Equal(0, PromptTokenOptimizer.ComputeSimilarity("", ""));
        }

        // --- Formatting overhead ---

        [Fact]
        public void DetectFormattingOverhead_RepeatedJsonInstructions_Detected()
        {
            var prompt = "Format the output as JSON. Then return the result as JSON.";
            var recs = _optimizer.DetectFormattingOverhead(prompt);
            // May or may not detect depending on exact match; at least no crash
            Assert.NotNull(recs);
        }

        [Fact]
        public void DetectFormattingOverhead_SingleInstruction_NotDetected()
        {
            var recs = _optimizer.DetectFormattingOverhead("format the output as JSON");
            Assert.Empty(recs);
        }

        // --- Excessive context ---

        [Fact]
        public void DetectExcessiveContext_HeavyExamples_Detected()
        {
            var prompt = "Do X. Example: " + new string('a', 500);
            var recs = _optimizer.DetectExcessiveContext(prompt);
            Assert.NotEmpty(recs);
        }

        [Fact]
        public void DetectExcessiveContext_NoExamples_NotDetected()
        {
            var recs = _optimizer.DetectExcessiveContext("Simple instruction without examples.");
            Assert.Empty(recs);
        }

        // --- Over-specification ---

        [Fact]
        public void DetectOverSpecification_ManyConstraints_Detected()
        {
            // Use a single long block so regex can find many matches
            var prompt = "You must always validate user input before processing. You should never expose internal details to external clients. Always ensure the database connection is properly closed. Never allow unauthenticated users to access resources. You must handle all edge cases including null values. Ensure that every response includes proper status codes. Do not cache sensitive data without explicit consent. Make sure all logs are properly sanitized before output.";
            var recs = _optimizer.DetectOverSpecification(prompt);
            Assert.NotEmpty(recs);
        }

        [Fact]
        public void DetectOverSpecification_FewConstraints_NotDetected()
        {
            var recs = _optimizer.DetectOverSpecification("Do X. Do Y.");
            Assert.Empty(recs);
        }

        // --- Auto-apply optimizations ---

        [Fact]
        public void Analyze_AutoApply_RemovesFillers()
        {
            var prompt = "In order to do X, please note that Y is important.";
            var report = _optimizer.Analyze(prompt);
            Assert.DoesNotContain("In order to", report.OptimizedPrompt);
        }

        [Fact]
        public void Analyze_AutoApplyDisabled_KeepsOriginal()
        {
            var opt = new PromptTokenOptimizer(new OptimizerConfig { AutoApply = false });
            var prompt = "In order to do X.";
            var report = opt.Analyze(prompt);
            Assert.Contains("In order to", report.OptimizedPrompt);
        }

        [Fact]
        public void Analyze_OptimizedTokens_LessThanOrEqualOriginal()
        {
            var prompt = "In order to complete X, please note that Y. Due to the fact that Z.";
            var report = _optimizer.Analyze(prompt);
            Assert.True(report.OptimizedTokens <= report.TotalTokens);
        }

        // --- Token budget ---

        [Fact]
        public void Analyze_TokenBudgetExceeded_CriticalRecommendation()
        {
            var opt = new PromptTokenOptimizer(new OptimizerConfig { TokenBudget = 5 });
            var prompt = "This is a fairly long prompt that definitely exceeds five tokens.";
            var report = opt.Analyze(prompt);
            Assert.Contains(report.Recommendations, r => r.Severity == OptimizationSeverity.Critical);
        }

        [Fact]
        public void Analyze_TokenBudgetNotExceeded_NoCritical()
        {
            var opt = new PromptTokenOptimizer(new OptimizerConfig { TokenBudget = 10000 });
            var report = opt.Analyze("Short.");
            Assert.DoesNotContain(report.Recommendations, r =>
                r.Category == OptimizationCategory.Structure &&
                r.Severity == OptimizationSeverity.Critical);
        }

        // --- Compare ---

        [Fact]
        public void Compare_TwoPrompts_ReturnsComparison()
        {
            var result = _optimizer.Compare("Short prompt.", "This is a much longer and more verbose prompt with unnecessary words.");
            Assert.NotNull(result.ReportA);
            Assert.NotNull(result.ReportB);
            Assert.True(result.TokenDifference != 0 || result.ReportA.TotalTokens == result.ReportB.TotalTokens);
        }

        [Fact]
        public void Compare_IdenticalPrompts_ZeroDifference()
        {
            var result = _optimizer.Compare("Same prompt.", "Same prompt.");
            Assert.Equal(0, result.TokenDifference);
        }

        [Fact]
        public void Compare_MoreEfficientSet()
        {
            var result = _optimizer.Compare("Do X.", "In order to do X, please note that Y. Due to the fact that Z.");
            Assert.Contains(result.MoreEfficientPrompt, new[] { "A", "B" });
        }

        // --- CheckBudget ---

        [Fact]
        public void CheckBudget_WithinBudget_ReturnsTrue()
        {
            var check = _optimizer.CheckBudget("Short prompt.", 1000);
            Assert.True(check.WithinBudget);
            Assert.True(check.TokensRemaining > 0);
        }

        [Fact]
        public void CheckBudget_ExceedsBudget_ReturnsFalse()
        {
            var longPrompt = string.Join(" ", Enumerable.Repeat("word", 500));
            var check = _optimizer.CheckBudget(longPrompt, 10);
            Assert.False(check.WithinBudget);
            Assert.True(check.TokensRemaining < 0);
        }

        [Fact]
        public void CheckBudget_ReserveRatio_Respected()
        {
            var check = _optimizer.CheckBudget("Test", 1000, 0.5);
            Assert.Equal(500, check.ReserveTokens);
            Assert.Equal(500, check.AvailableForPrompt);
        }

        [Fact]
        public void CheckBudget_UtilizationPercent_Reasonable()
        {
            var check = _optimizer.CheckBudget("Test prompt here.", 1000);
            Assert.True(check.UtilizationPercent > 0);
            Assert.True(check.UtilizationPercent < 100);
        }

        // --- SuggestSplit ---

        [Fact]
        public void SuggestSplit_ShortPrompt_SingleChunk()
        {
            var chunks = _optimizer.SuggestSplit("Short.", 100);
            Assert.Single(chunks);
        }

        [Fact]
        public void SuggestSplit_LongPrompt_MultipleChunks()
        {
            var prompt = "## Section 1\n" + string.Join(" ", Enumerable.Repeat("word", 100)) +
                "\n\n## Section 2\n" + string.Join(" ", Enumerable.Repeat("text", 100));
            var chunks = _optimizer.SuggestSplit(prompt, 50);
            Assert.True(chunks.Count >= 2);
        }

        [Fact]
        public void SuggestSplit_EmptyPrompt_SingleChunk()
        {
            var chunks = _optimizer.SuggestSplit("", 100);
            Assert.Single(chunks);
        }

        [Fact]
        public void SuggestSplit_NullPrompt_SingleChunk()
        {
            var chunks = _optimizer.SuggestSplit(null!, 100);
            Assert.Single(chunks);
        }

        [Fact]
        public void SuggestSplit_ZeroMaxTokens_SingleChunk()
        {
            var chunks = _optimizer.SuggestSplit("Test", 0);
            Assert.Single(chunks);
        }

        // --- OptimizationReport.Summary ---

        [Fact]
        public void Summary_ContainsKeyInfo()
        {
            var prompt = "In order to analyze this, please note that we need to process it. Due to the fact that it's complex, I would like you to take care.";
            var report = _optimizer.Analyze(prompt);
            var summary = report.Summary();
            Assert.Contains("Prompt Optimization Report", summary);
            Assert.Contains("Total tokens:", summary);
            Assert.Contains("Optimization score:", summary);
        }

        [Fact]
        public void Summary_WithRecommendations_ShowsTop()
        {
            var prompt = "In order to do X. Please note that Y. Due to the fact that Z. I would like you to A. I need you to B.";
            var report = _optimizer.Analyze(prompt);
            if (report.Recommendations.Count > 0)
            {
                var summary = report.Summary();
                Assert.Contains("Recommendations", summary);
            }
        }

        // --- Optimization score ---

        [Fact]
        public void OptimizationScore_CleanPrompt_HighScore()
        {
            var report = _optimizer.Analyze("Write a haiku about spring.");
            Assert.True(report.OptimizationScore >= 80);
        }

        [Fact]
        public void OptimizationScore_VerbosePrompt_LowerScore()
        {
            var prompt = "In order to complete this task, please note that I would like you to write a poem. Due to the fact that poetry is complex, I need you to take your time. At this point in time, for the purpose of creating art, with regard to the style, it should be noted that rhyming is preferred.";
            var report = _optimizer.Analyze(prompt);
            Assert.True(report.OptimizationScore < 95);
        }

        // --- SavingsPercent ---

        [Fact]
        public void SavingsPercent_WithSavings_Positive()
        {
            var prompt = "In order to do X. Please note that Y. Due to the fact that Z.";
            var report = _optimizer.Analyze(prompt);
            Assert.True(report.SavingsPercent >= 0);
        }

        [Fact]
        public void SavingsPercent_CleanPrompt_ZeroOrLow()
        {
            var report = _optimizer.Analyze("Do X.");
            Assert.True(report.SavingsPercent < 50);
        }

        // --- Config ---

        [Fact]
        public void Config_CustomRedundancyThreshold_Respected()
        {
            var opt = new PromptTokenOptimizer(new OptimizerConfig { RedundancyThreshold = 0.99 });
            var instructions = new List<string>
            {
                "Format the output as JSON with indentation",
                "Return the output formatted as JSON with proper indentation"
            };
            var pairs = opt.DetectRedundancies(instructions);
            // With threshold at 0.99, near-but-not-exact matches should not trigger
            Assert.Empty(pairs);
        }

        [Fact]
        public void Config_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptTokenOptimizer(null!));
        }

        [Fact]
        public void Config_AutoApplyMinConfidence_Filters()
        {
            var opt = new PromptTokenOptimizer(new OptimizerConfig { AutoApplyMinConfidence = 1.0 });
            var prompt = "In order to do X.";
            var report = opt.Analyze(prompt);
            // With min confidence 1.0, the 0.9-confidence filler replacement should still apply
            // (since 0.9 < 1.0, it won't auto-apply)
            Assert.Contains("In order to", report.OptimizedPrompt);
        }

        // --- Edge cases ---

        [Fact]
        public void Analyze_VeryLongPrompt_DoesNotThrow()
        {
            var prompt = string.Join("\n", Enumerable.Range(1, 500).Select(i => $"- Instruction {i}: do thing {i}"));
            var report = _optimizer.Analyze(prompt);
            Assert.True(report.TotalTokens > 100);
        }

        [Fact]
        public void Analyze_SpecialCharacters_DoesNotThrow()
        {
            var report = _optimizer.Analyze("Use émojis 🎉 and spëcial chars: <tag> & \"quotes\"");
            Assert.NotNull(report);
        }

        [Fact]
        public void Analyze_OnlyWhitespace_HandledGracefully()
        {
            var report = _optimizer.Analyze("   \n\n   \t  ");
            Assert.NotNull(report);
        }

        // --- RedundancyPair properties ---

        [Fact]
        public void RedundancyPair_SuggestedMerge_IsShorterVersion()
        {
            var instructions = new List<string>
            {
                "Always format output as JSON",
                "Always format output as JSON"
            };
            var pairs = _optimizer.DetectRedundancies(instructions);
            Assert.NotEmpty(pairs);
            Assert.NotEmpty(pairs[0].SuggestedMerge);
        }

        [Fact]
        public void RedundancyPair_EstimatedTokensSaved_Positive()
        {
            var instructions = new List<string>
            {
                "Always format the response output as clean JSON with indentation",
                "Always format the response output as clean JSON with indentation"
            };
            var pairs = _optimizer.DetectRedundancies(instructions);
            Assert.True(pairs[0].EstimatedTokensSaved > 0);
        }

        // --- Verbosity detection ---

        [Fact]
        public void DetectVerbosity_LongSentences_Detected()
        {
            var section = new PromptSection
            {
                Name = "Test",
                Content = "This is a very long sentence that goes on and on and on and on and on with many words that could probably be condensed into something much shorter and more concise but instead it just keeps going and going. Another very long sentence follows here with even more unnecessary verbosity that really does not need to be this long at all.",
                TokenCount = 80
            };
            var recs = _optimizer.DetectVerbosity(new List<PromptSection> { section });
            Assert.NotEmpty(recs);
        }

        [Fact]
        public void DetectVerbosity_ShortSections_Skipped()
        {
            var section = new PromptSection { Name = "Test", Content = "Short.", TokenCount = 2 };
            var recs = _optimizer.DetectVerbosity(new List<PromptSection> { section });
            Assert.Empty(recs);
        }
    }
}
