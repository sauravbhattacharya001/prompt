namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptSimilarityAnalyzerTests
    {
        // ── Constructor ──

        [Fact]
        public void Constructor_DefaultValues()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("hello", "hello");
            Assert.Equal(1.0, r.Score);
        }

        [Fact]
        public void Constructor_InvalidThreshold_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptSimilarityAnalyzer(defaultThreshold: 1.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptSimilarityAnalyzer(defaultThreshold: -0.1));
        }

        // ── Compare ──

        [Fact]
        public void Compare_IdenticalStrings_ScoreOne()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("Summarize this article", "Summarize this article");
            Assert.Equal(1.0, r.Score);
            Assert.True(r.IsDuplicate);
        }

        [Fact]
        public void Compare_CompletelyDifferent_LowScore()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("abc", "xyz");
            Assert.True(r.Score < 0.5);
            Assert.False(r.IsDuplicate);
        }

        [Fact]
        public void Compare_SimilarPrompts_HighScore()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("Summarize this article", "Summarize the article");
            Assert.True(r.Score > 0.7);
        }

        [Fact]
        public void Compare_NullA_Throws()
        {
            var a = new PromptSimilarityAnalyzer();
            Assert.Throws<ArgumentNullException>(() => a.Compare(null!, "b"));
        }

        [Fact]
        public void Compare_NullB_Throws()
        {
            var a = new PromptSimilarityAnalyzer();
            Assert.Throws<ArgumentNullException>(() => a.Compare("a", null!));
        }

        [Fact]
        public void Compare_EmptyStrings_ScoreOne()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("", "");
            Assert.Equal(1.0, r.Score);
        }

        [Fact]
        public void Compare_OneEmpty_ScoreZero()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("hello world", "");
            Assert.Equal(0.0, r.Score);
        }

        [Fact]
        public void Compare_CaseInsensitive_ByDefault()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("HELLO", "hello");
            Assert.Equal(1.0, r.Score);
        }

        [Fact]
        public void Compare_CaseSensitive_WhenConfigured()
        {
            var a = new PromptSimilarityAnalyzer(caseSensitive: true);
            var r = a.Compare("HELLO", "hello");
            Assert.True(r.Score < 1.0);
        }

        [Fact]
        public void Compare_SharedTokens_Populated()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("translate to french", "translate into french");
            Assert.Contains("translate", r.SharedTokens);
            Assert.Contains("french", r.SharedTokens);
        }

        [Fact]
        public void Compare_UniqueTokens_Populated()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("translate to french", "translate into french");
            Assert.Contains("to", r.UniqueToA);
            Assert.Contains("into", r.UniqueToB);
        }

        [Fact]
        public void Compare_CustomThreshold()
        {
            var a = new PromptSimilarityAnalyzer();
            var r1 = a.Compare("abc", "abd", threshold: 0.5);
            Assert.True(r1.IsDuplicate); // Levenshtein 2/3 = 0.67 > 0.5

            var r2 = a.Compare("abc", "abd", threshold: 0.9);
            Assert.False(r2.IsDuplicate);
        }

        // ── Metrics ──

        [Theory]
        [InlineData(SimilarityMetric.Levenshtein)]
        [InlineData(SimilarityMetric.Jaccard)]
        [InlineData(SimilarityMetric.Cosine)]
        [InlineData(SimilarityMetric.Dice)]
        [InlineData(SimilarityMetric.LCS)]
        public void AllMetrics_IdenticalStrings_ReturnOne(SimilarityMetric metric)
        {
            var a = new PromptSimilarityAnalyzer(metric);
            var r = a.Compare("test prompt", "test prompt");
            Assert.Equal(1.0, r.Score);
        }

        [Theory]
        [InlineData(SimilarityMetric.Levenshtein)]
        [InlineData(SimilarityMetric.Jaccard)]
        [InlineData(SimilarityMetric.Cosine)]
        [InlineData(SimilarityMetric.Dice)]
        [InlineData(SimilarityMetric.LCS)]
        public void AllMetrics_ScoreBetween0And1(SimilarityMetric metric)
        {
            var a = new PromptSimilarityAnalyzer(metric);
            var r = a.Compare("summarize article", "translate document");
            Assert.InRange(r.Score, 0.0, 1.0);
        }

        [Fact]
        public void JaccardMetric_SharedWords()
        {
            var a = new PromptSimilarityAnalyzer(SimilarityMetric.Jaccard);
            var r = a.Compare("the quick brown fox", "the slow brown fox");
            // Jaccard: {the, brown, fox} / {the, quick, slow, brown, fox} = 3/5
            Assert.Equal(0.6, r.Score, 2);
        }

        [Fact]
        public void CosineMetric_Similarity()
        {
            var a = new PromptSimilarityAnalyzer(SimilarityMetric.Cosine);
            var r = a.Compare("hello world hello", "hello world");
            Assert.True(r.Score > 0.8);
        }

        [Fact]
        public void DiceMetric_Similarity()
        {
            var a = new PromptSimilarityAnalyzer(SimilarityMetric.Dice);
            var r = a.Compare("night", "nacht");
            Assert.True(r.Score >= 0.0 && r.Score <= 1.0);
        }

        [Fact]
        public void LCSMetric_Similarity()
        {
            var a = new PromptSimilarityAnalyzer(SimilarityMetric.LCS);
            var r = a.Compare("abcdef", "abcxyz");
            // LCS = "abc" (3), maxLen = 6, similarity = 0.5
            Assert.Equal(0.5, r.Score, 2);
        }

        // ── CompareAllMetrics ──

        [Fact]
        public void CompareAllMetrics_ReturnsAllMetrics()
        {
            var a = new PromptSimilarityAnalyzer();
            var results = a.CompareAllMetrics("hello", "world");
            Assert.Equal(5, results.Count);
            Assert.Contains(results, r => r.Metric == SimilarityMetric.Levenshtein);
            Assert.Contains(results, r => r.Metric == SimilarityMetric.Jaccard);
            Assert.Contains(results, r => r.Metric == SimilarityMetric.Cosine);
            Assert.Contains(results, r => r.Metric == SimilarityMetric.Dice);
            Assert.Contains(results, r => r.Metric == SimilarityMetric.LCS);
        }

        // ── FindMostSimilar ──

        [Fact]
        public void FindMostSimilar_ReturnsBestMatch()
        {
            var a = new PromptSimilarityAnalyzer();
            var candidates = new[] { "Translate to Spanish", "Summarize the text", "Summarize this document" };
            var best = a.FindMostSimilar("Summarize this article", candidates);
            Assert.NotNull(best);
            Assert.Equal("Summarize this document", best!.PromptB);
        }

        [Fact]
        public void FindMostSimilar_NoCandidates_ReturnsNull()
        {
            var a = new PromptSimilarityAnalyzer();
            var best = a.FindMostSimilar("hello", Array.Empty<string>());
            Assert.Null(best);
        }

        [Fact]
        public void FindMostSimilar_SkipsSelf()
        {
            var a = new PromptSimilarityAnalyzer();
            var candidates = new[] { "hello", "world" };
            var best = a.FindMostSimilar("hello", candidates);
            Assert.NotNull(best);
            Assert.Equal("world", best!.PromptB);
        }

        [Fact]
        public void FindMostSimilar_NullPrompt_Throws()
        {
            var a = new PromptSimilarityAnalyzer();
            Assert.Throws<ArgumentNullException>(() => a.FindMostSimilar(null!, new[] { "a" }));
        }

        // ── FindDuplicates ──

        [Fact]
        public void FindDuplicates_DetectsNearDuplicates()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new[] { "Translate to French", "Translate into French", "Summarize article" };
            var report = a.FindDuplicates(prompts, threshold: 0.7);
            Assert.True(report.DuplicatePairs.Count > 0);
        }

        [Fact]
        public void FindDuplicates_NoDuplicates_EmptyPairs()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new[] { "abc", "xyz", "123" };
            var report = a.FindDuplicates(prompts, threshold: 0.99);
            Assert.Empty(report.DuplicatePairs);
        }

        [Fact]
        public void FindDuplicates_ReportFields()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new[] { "hello", "hello world", "goodbye" };
            var report = a.FindDuplicates(prompts);
            Assert.Equal(3, report.TotalPrompts);
            Assert.True(report.DuplicateRate >= 0.0 && report.DuplicateRate <= 1.0);
        }

        [Fact]
        public void FindDuplicates_ClusteringWorks()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new[]
            {
                "Summarize this article",
                "Summarize the article",
                "Summarize this document",
                "Translate to French"
            };
            var report = a.FindDuplicates(prompts, threshold: 0.7);
            // The summarize prompts should cluster
            if (report.Clusters.Count > 0)
            {
                Assert.True(report.Clusters[0].Members.Count >= 2);
                Assert.True(report.Clusters[0].AverageSimilarity > 0.5);
            }
        }

        [Fact]
        public void FindDuplicates_NullPrompts_Throws()
        {
            var a = new PromptSimilarityAnalyzer();
            Assert.Throws<ArgumentNullException>(() => a.FindDuplicates(null!));
        }

        [Fact]
        public void FindDuplicates_EmptyCollection_EmptyReport()
        {
            var a = new PromptSimilarityAnalyzer();
            var report = a.FindDuplicates(Array.Empty<string>());
            Assert.Equal(0, report.TotalPrompts);
            Assert.Empty(report.DuplicatePairs);
        }

        // ── RankBySimilarity ──

        [Fact]
        public void RankBySimilarity_OrderedDescending()
        {
            var a = new PromptSimilarityAnalyzer();
            var candidates = new[] { "xyz completely different", "summarize text", "summarize this article now" };
            var ranked = a.RankBySimilarity("summarize this article", candidates);
            Assert.True(ranked[0].Score >= ranked[1].Score);
            Assert.True(ranked[1].Score >= ranked[2].Score);
        }

        [Fact]
        public void RankBySimilarity_ExcludesSelf()
        {
            var a = new PromptSimilarityAnalyzer();
            var candidates = new[] { "summarize this article", "other prompt" };
            var ranked = a.RankBySimilarity("summarize this article", candidates);
            Assert.Single(ranked);
        }

        [Fact]
        public void RankBySimilarity_NullReference_Throws()
        {
            var a = new PromptSimilarityAnalyzer();
            Assert.Throws<ArgumentNullException>(() => a.RankBySimilarity(null!, new[] { "a" }));
        }

        // ── SimilarityMatrix ──

        [Fact]
        public void SimilarityMatrix_DiagonalIsOne()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new List<string> { "hello", "world", "test" };
            var matrix = a.SimilarityMatrix(prompts);
            for (int i = 0; i < prompts.Count; i++)
                Assert.Equal(1.0, matrix[i, i]);
        }

        [Fact]
        public void SimilarityMatrix_Symmetric()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new List<string> { "hello", "world" };
            var matrix = a.SimilarityMatrix(prompts);
            Assert.Equal(matrix[0, 1], matrix[1, 0]);
        }

        [Fact]
        public void SimilarityMatrix_NullPrompts_Throws()
        {
            var a = new PromptSimilarityAnalyzer();
            Assert.Throws<ArgumentNullException>(() => a.SimilarityMatrix(null!));
        }

        // ── MatrixToJson ──

        [Fact]
        public void MatrixToJson_ValidJson()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new List<string> { "hello", "world" };
            var json = a.MatrixToJson(prompts);
            Assert.Contains("hello", json);
            Assert.Contains("sim_0", json);
        }

        // ── DuplicateReport ──

        [Fact]
        public void DuplicateReport_ToTextReport_ContainsInfo()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new[] { "Translate to French", "Translate into French", "Summarize" };
            var report = a.FindDuplicates(prompts, threshold: 0.7);
            var text = report.ToTextReport();
            Assert.Contains("Duplicate Report", text);
            Assert.Contains("Total prompts", text);
        }

        [Fact]
        public void DuplicateReport_ToJson_ValidJson()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new[] { "hello", "hello world" };
            var report = a.FindDuplicates(prompts);
            var json = report.ToJson();
            Assert.Contains("totalPrompts", json);
            Assert.Contains("duplicateRate", json);
        }

        // ── Edge Cases ──

        [Fact]
        public void Compare_WhitespaceOnly_TreatedAsEmpty()
        {
            var a = new PromptSimilarityAnalyzer();
            var r = a.Compare("   ", "   ");
            Assert.Equal(1.0, r.Score);
        }

        [Fact]
        public void Compare_LongPrompts()
        {
            var a = new PromptSimilarityAnalyzer(SimilarityMetric.Jaccard);
            var long1 = string.Join(" ", Enumerable.Repeat("word", 100));
            var long2 = string.Join(" ", Enumerable.Repeat("word", 100));
            var r = a.Compare(long1, long2);
            Assert.Equal(1.0, r.Score);
        }

        [Fact]
        public void DefaultMetric_UsedWhenNotSpecified()
        {
            var a = new PromptSimilarityAnalyzer(SimilarityMetric.Cosine);
            var r = a.Compare("hello world", "hello earth");
            Assert.Equal(SimilarityMetric.Cosine, r.Metric);
        }

        [Fact]
        public void Compare_OverrideMetric()
        {
            var a = new PromptSimilarityAnalyzer(SimilarityMetric.Levenshtein);
            var r = a.Compare("hello", "world", SimilarityMetric.Jaccard);
            Assert.Equal(SimilarityMetric.Jaccard, r.Metric);
        }

        [Fact]
        public void FindDuplicates_PairsOrderedByScore()
        {
            var a = new PromptSimilarityAnalyzer();
            var prompts = new[]
            {
                "Summarize this article",
                "Summarize the article",
                "Summarize article",
                "Summarize this long article"
            };
            var report = a.FindDuplicates(prompts, threshold: 0.6);
            for (int i = 0; i < report.DuplicatePairs.Count - 1; i++)
                Assert.True(report.DuplicatePairs[i].Score >= report.DuplicatePairs[i + 1].Score);
        }
    }
}
