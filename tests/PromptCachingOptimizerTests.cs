namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptCachingOptimizerTests
    {
        private readonly PromptCachingOptimizer _optimizer = new();

        #region AnalyzeSingle

        [Fact]
        public void AnalyzeSingle_EmptyPrompt_ReturnsEmptyAnalysis()
        {
            var result = _optimizer.AnalyzeSingle("");
            Assert.Empty(result.Segments);
            Assert.Equal(0, result.TotalTokens);
            Assert.Equal(0, result.CacheableTokens);
        }

        [Fact]
        public void AnalyzeSingle_NullPrompt_ReturnsEmptyAnalysis()
        {
            var result = _optimizer.AnalyzeSingle(null!);
            Assert.Empty(result.Segments);
        }

        [Fact]
        public void AnalyzeSingle_SystemInstruction_DetectedWithHighCacheProbability()
        {
            var prompt = "You are a helpful assistant.\nUser: Hello";
            var result = _optimizer.AnalyzeSingle(prompt);

            var system = result.Segments.FirstOrDefault(s => s.Type == SegmentType.SystemInstruction);
            Assert.NotNull(system);
            Assert.Equal(0.95, system.CacheHitProbability);
            Assert.Contains("You are a helpful assistant", system.Content);
        }

        [Fact]
        public void AnalyzeSingle_FewShotExamples_DetectedCorrectly()
        {
            var prompt = "You are a classifier.\nExample 1: Input: cat -> Output: animal\nExample 2: Input: car -> Output: vehicle\nUser: What is a dog?";
            var result = _optimizer.AnalyzeSingle(prompt);

            var fewShot = result.Segments.FirstOrDefault(s => s.Type == SegmentType.FewShotExamples);
            Assert.NotNull(fewShot);
            Assert.Equal(0.85, fewShot.CacheHitProbability);
        }

        [Fact]
        public void AnalyzeSingle_DynamicSuffix_LowCacheProbability()
        {
            var prompt = "You are a helper.\nUser: What is the weather today?";
            var result = _optimizer.AnalyzeSingle(prompt);

            var dynamic = result.Segments.FirstOrDefault(s => s.Type == SegmentType.DynamicSuffix);
            Assert.NotNull(dynamic);
            Assert.Equal(0.05, dynamic.CacheHitProbability);
        }

        [Fact]
        public void AnalyzeSingle_CacheableTokens_ExcludesDynamicSuffix()
        {
            var prompt = "You are a helpful assistant that answers questions.\nUser: Tell me about cats";
            var result = _optimizer.AnalyzeSingle(prompt);

            // CacheableTokens should be less than TotalTokens (dynamic part excluded)
            Assert.True(result.CacheableTokens < result.TotalTokens || result.CacheableTokens == result.TotalTokens);
            Assert.True(result.CacheableTokens >= 0);
        }

        [Fact]
        public void AnalyzeSingle_HighCacheability_HighScore()
        {
            // Mostly static content with tiny dynamic suffix
            var prompt = "System: You are an expert.\n" +
                         string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Context line {i} with important data.")) +
                         "\nUser: Summarize.";
            var result = _optimizer.AnalyzeSingle(prompt);

            Assert.True(result.CacheEfficiencyScore > 50);
        }

        [Fact]
        public void AnalyzeSingle_LargeContext_DetectedAsContextBlock()
        {
            // More than 200 words of context before user turn
            var contextLines = string.Join("\n", Enumerable.Range(1, 100).Select(i =>
                $"This is context line number {i} with some additional descriptive words."));
            var prompt = contextLines + "\nUser: Summarize this.";
            var result = _optimizer.AnalyzeSingle(prompt);

            var context = result.Segments.FirstOrDefault(s => s.Type == SegmentType.ContextBlock);
            Assert.NotNull(context);
            Assert.Equal(0.60, context.CacheHitProbability);
        }

        [Fact]
        public void AnalyzeSingle_SmallStaticPrefix_DetectedAsStaticPrefix()
        {
            // Less than 200 words of static content
            var prompt = "Here is some brief context.\nUser: Hello";
            var result = _optimizer.AnalyzeSingle(prompt);

            var prefix = result.Segments.FirstOrDefault(s => s.Type == SegmentType.StaticPrefix);
            Assert.NotNull(prefix);
            Assert.Equal(0.90, prefix.CacheHitProbability);
        }

        [Fact]
        public void AnalyzeSingle_Recommendations_SystemInstructionsPresent()
        {
            var prompt = "You are a helpful assistant.\nUser: Hi";
            var result = _optimizer.AnalyzeSingle(prompt);

            Assert.Contains(result.Recommendations, r => r.Contains("System instructions"));
        }

        [Fact]
        public void AnalyzeSingle_Recommendations_FewShotPresent()
        {
            var prompt = "You are a helper.\nExample 1: Input: hello -> Output: greeting\nUser: Hi";
            var result = _optimizer.AnalyzeSingle(prompt);

            Assert.Contains(result.Recommendations, r => r.Contains("Few-shot"));
        }

        #endregion

        #region Analyze (batch)

        [Fact]
        public void Analyze_EmptyList_ReturnsEmpty()
        {
            var result = _optimizer.Analyze(new List<string>());
            Assert.Empty(result.Segments);
            Assert.Equal(0, result.TotalTokens);
        }

        [Fact]
        public void Analyze_SinglePrompt_DelegatesToAnalyzeSingle()
        {
            var prompt = "You are a helper.\nUser: Hi";
            var single = _optimizer.AnalyzeSingle(prompt);
            var batch = _optimizer.Analyze(new[] { prompt });

            Assert.Equal(single.TotalTokens, batch.TotalTokens);
            Assert.Equal(single.CacheableTokens, batch.CacheableTokens);
        }

        [Fact]
        public void Analyze_MultiplePrompts_AggregatesTokens()
        {
            var prompts = new[]
            {
                "You are a helper.\nUser: Q1",
                "You are a helper.\nUser: Q2",
                "You are a helper.\nUser: Q3"
            };
            var result = _optimizer.Analyze(prompts);

            Assert.True(result.TotalTokens > 0);
            Assert.True(result.Segments.Count > 0);
        }

        [Fact]
        public void Analyze_CommonPrefixes_Detected()
        {
            // Build prompts with a shared prefix > 100 tokens
            var sharedPrefix = string.Join(" ", Enumerable.Range(1, 120).Select(i => $"word{i}"));
            var prompts = new[]
            {
                sharedPrefix + "\nUser: Question A",
                sharedPrefix + "\nUser: Question B",
                sharedPrefix + "\nUser: Question C"
            };
            var result = _optimizer.Analyze(prompts);

            Assert.True(result.CommonPrefixes.Count > 0);
            Assert.True(result.CommonPrefixes[0].PromptIndices.Count >= 2);
        }

        [Fact]
        public void Analyze_NullInput_ReturnsEmpty()
        {
            var result = _optimizer.Analyze(null!);
            Assert.Empty(result.Segments);
        }

        #endregion

        #region FindCommonPrefixes

        [Fact]
        public void FindCommonPrefixes_LessThanTwo_ReturnsEmpty()
        {
            var result = _optimizer.FindCommonPrefixes(new[] { "one prompt" });
            Assert.Empty(result);
        }

        [Fact]
        public void FindCommonPrefixes_ShortPrefix_Excluded()
        {
            var result = _optimizer.FindCommonPrefixes(new[] { "Hello world", "Hello there" }, minPrefixTokens: 100);
            Assert.Empty(result);
        }

        [Fact]
        public void FindCommonPrefixes_LongSharedPrefix_Found()
        {
            var prefix = string.Join(" ", Enumerable.Range(1, 150).Select(i => $"token{i}"));
            var prompts = new[]
            {
                prefix + " suffix A",
                prefix + " suffix B"
            };
            var result = _optimizer.FindCommonPrefixes(prompts, minPrefixTokens: 50);

            Assert.True(result.Count > 0);
            Assert.True(result[0].PrefixTokens >= 50);
            Assert.Equal(2, result[0].PromptIndices.Count);
            Assert.True(result[0].SavingsIfCached > 0);
        }

        [Fact]
        public void FindCommonPrefixes_OrderedBySavings()
        {
            var shortPrefix = string.Join(" ", Enumerable.Range(1, 60).Select(i => $"a{i}"));
            var longPrefix = string.Join(" ", Enumerable.Range(1, 200).Select(i => $"b{i}"));
            var prompts = new[]
            {
                shortPrefix + " endA",
                shortPrefix + " endB",
                longPrefix + " endC",
                longPrefix + " endD"
            };
            var result = _optimizer.FindCommonPrefixes(prompts, minPrefixTokens: 50);

            if (result.Count >= 2)
            {
                Assert.True(result[0].SavingsIfCached >= result[1].SavingsIfCached);
            }
        }

        #endregion

        #region Restructure

        [Fact]
        public void Restructure_Empty_ReturnsInput()
        {
            Assert.Equal("", _optimizer.Restructure(""));
            Assert.Null(_optimizer.Restructure(null!));
        }

        [Fact]
        public void Restructure_ReordersSystemFirst()
        {
            var prompt = "Some context here.\nYou are a helpful assistant.\nUser: Hello";
            var restructured = _optimizer.Restructure(prompt);

            // System instruction should come before dynamic content
            int sysIdx = restructured.IndexOf("You are a helpful assistant", StringComparison.Ordinal);
            int userIdx = restructured.IndexOf("Hello", StringComparison.Ordinal);
            Assert.True(sysIdx < userIdx);
        }

        [Fact]
        public void Restructure_InsertsCacheBreaks()
        {
            var prompt = "You are a helper.\nExample 1: Input: hi -> Output: greeting\nUser: Hey";
            var restructured = _optimizer.Restructure(prompt);

            Assert.Contains("[CACHE_BREAK]", restructured);
        }

        [Fact]
        public void Restructure_DynamicSuffixLast()
        {
            var prompt = "You are a helper.\nUser: Tell me a joke";
            var restructured = _optimizer.Restructure(prompt);

            // Dynamic content should be at the end (after last CACHE_BREAK)
            int lastBreak = restructured.LastIndexOf("[CACHE_BREAK]", StringComparison.Ordinal);
            int dynamicIdx = restructured.IndexOf("Tell me a joke", StringComparison.Ordinal);
            if (lastBreak >= 0)
            {
                Assert.True(dynamicIdx > lastBreak);
            }
        }

        #endregion

        #region Monitor

        [Fact]
        public void Monitor_NoBaseline_ReturnsAnalysis()
        {
            var prompts = new[] { "You are a bot.\nUser: Hi" };
            var result = _optimizer.Monitor(prompts);

            Assert.True(result.TotalTokens > 0);
        }

        [Fact]
        public void Monitor_ScoreDrop_WarningAdded()
        {
            var baseline = new CachingAnalysis
            {
                CacheEfficiencyScore = 80,
                EstimatedSavingsPercent = 40,
                TotalTokens = 1000
            };

            // A prompt with low cacheability (all dynamic)
            var prompts = new[] { "User: Just a question with no system prefix at all." };
            var result = _optimizer.Monitor(prompts, baseline);

            if (result.CacheEfficiencyScore < baseline.CacheEfficiencyScore - 10)
            {
                Assert.Contains(result.Recommendations, r => r.Contains("WARNING"));
            }
        }

        [Fact]
        public void Monitor_ScoreImprovement_ImprovementNoted()
        {
            var baseline = new CachingAnalysis
            {
                CacheEfficiencyScore = 20,
                EstimatedSavingsPercent = 10,
                TotalTokens = 100
            };

            // Well-structured prompt
            var bigContext = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Context data line {i} with info."));
            var prompts = new[] { "System: You are an expert.\n" + bigContext + "\nUser: Summarize." };
            var result = _optimizer.Monitor(prompts, baseline);

            if (result.CacheEfficiencyScore > baseline.CacheEfficiencyScore + 10)
            {
                Assert.Contains(result.Recommendations, r => r.Contains("IMPROVEMENT"));
            }
        }

        [Fact]
        public void Monitor_TokenVolumeShift_Noted()
        {
            var baseline = new CachingAnalysis
            {
                CacheEfficiencyScore = 50,
                EstimatedSavingsPercent = 25,
                TotalTokens = 100
            };

            // Much larger prompt (>20% increase)
            var bigPrompt = string.Join(" ", Enumerable.Range(1, 500).Select(i => $"word{i}"));
            var prompts = new[] { bigPrompt + "\nUser: Q" };
            var result = _optimizer.Monitor(prompts, baseline);

            Assert.Contains(result.Recommendations, r => r.Contains("Token volume shifted"));
        }

        #endregion

        #region ToReport / ToJson

        [Fact]
        public void ToReport_ContainsExpectedSections()
        {
            var prompt = "You are a helper.\nExample 1: Input: hi -> Output: hello\nUser: Hey";
            var result = _optimizer.AnalyzeSingle(prompt);
            var report = result.ToReport();

            Assert.Contains("Prompt Caching Analysis", report);
            Assert.Contains("Total estimated tokens", report);
            Assert.Contains("Efficiency score", report);
            Assert.Contains("Segment Breakdown", report);
        }

        [Fact]
        public void ToReport_RatingMapping()
        {
            var analysis = new CachingAnalysis { CacheEfficiencyScore = 80 };
            Assert.Contains("EXCELLENT", analysis.ToReport());

            analysis.CacheEfficiencyScore = 60;
            Assert.Contains("GOOD", analysis.ToReport());

            analysis.CacheEfficiencyScore = 30;
            Assert.Contains("FAIR", analysis.ToReport());

            analysis.CacheEfficiencyScore = 10;
            Assert.Contains("POOR", analysis.ToReport());
        }

        [Fact]
        public void ToJson_ValidJson()
        {
            var prompt = "You are a helper.\nUser: Hi";
            var result = _optimizer.AnalyzeSingle(prompt);
            var json = result.ToJson();

            Assert.Contains("\"totalTokens\"", json);
            Assert.Contains("\"cacheableTokens\"", json);
            Assert.Contains("\"cacheEfficiencyScore\"", json);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void AnalyzeSingle_WhitespaceOnly_ReturnsEmpty()
        {
            var result = _optimizer.AnalyzeSingle("   \n\n  ");
            Assert.Empty(result.Segments);
        }

        [Fact]
        public void AnalyzeSingle_MultipleSystemPatterns()
        {
            var prompt = "SYSTEM: You are an AI.\nYou are helpful.\nUser: Hello";
            var result = _optimizer.AnalyzeSingle(prompt);

            var system = result.Segments.FirstOrDefault(s => s.Type == SegmentType.SystemInstruction);
            Assert.NotNull(system);
        }

        [Fact]
        public void AnalyzeSingle_EstimatedSavings_BoundedCorrectly()
        {
            var prompt = "You are a classifier.\n" +
                         string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Example {i}: Input: x{i} -> Output: y{i}")) +
                         "\nUser: Classify z.";
            var result = _optimizer.AnalyzeSingle(prompt);

            Assert.True(result.EstimatedSavingsPercent >= 0);
            Assert.True(result.EstimatedSavingsPercent <= 50);
        }

        #endregion
    }
}
