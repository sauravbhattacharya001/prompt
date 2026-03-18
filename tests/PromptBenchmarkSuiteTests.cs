namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptBenchmarkSuiteTests
    {
        // ================================================================
        // AddVariant
        // ================================================================

        [Fact]
        public void AddVariant_ValidInput_IncrementsCount()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello {{name}}"));
            Assert.Equal(1, suite.VariantCount);
        }

        [Fact]
        public void AddVariant_DuplicateName_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello"));
            Assert.Throws<ArgumentException>(() =>
                suite.AddVariant("v1", new PromptTemplate("World")));
        }

        [Fact]
        public void AddVariant_EmptyName_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            Assert.Throws<ArgumentException>(() =>
                suite.AddVariant("", new PromptTemplate("Hello")));
        }

        [Fact]
        public void AddVariant_NullTemplate_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            Assert.Throws<ArgumentNullException>(() =>
                suite.AddVariant("v1", null!));
        }

        [Fact]
        public void AddVariant_CaseInsensitiveDuplicate_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("MyVariant", new PromptTemplate("Hello"));
            Assert.Throws<ArgumentException>(() =>
                suite.AddVariant("myvariant", new PromptTemplate("World")));
        }

        // ================================================================
        // RemoveVariant
        // ================================================================

        [Fact]
        public void RemoveVariant_ExistingVariant_ReturnsTrue()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello"));
            Assert.True(suite.RemoveVariant("v1"));
            Assert.Equal(0, suite.VariantCount);
        }

        [Fact]
        public void RemoveVariant_NonExistent_ReturnsFalse()
        {
            var suite = new PromptBenchmarkSuite();
            Assert.False(suite.RemoveVariant("nope"));
        }

        // ================================================================
        // AddScenario
        // ================================================================

        [Fact]
        public void AddScenario_ValidInput_IncrementsCount()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "test1",
                Variables = new() { ["name"] = "World" },
                ExpectedOutput = "Hello World"
            });
            Assert.Equal(1, suite.ScenarioCount);
        }

        [Fact]
        public void AddScenario_Null_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            Assert.Throws<ArgumentNullException>(() => suite.AddScenario(null!));
        }

        [Fact]
        public void AddScenario_EmptyName_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            Assert.Throws<ArgumentException>(() =>
                suite.AddScenario(new BenchmarkScenario { Name = "" }));
        }

        [Fact]
        public void AddScenario_DuplicateName_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddScenario(new BenchmarkScenario { Name = "s1", ExpectedOutput = "a" });
            Assert.Throws<ArgumentException>(() =>
                suite.AddScenario(new BenchmarkScenario { Name = "s1", ExpectedOutput = "b" }));
        }

        [Fact]
        public void AddScenarios_MultipleScenariosAdded()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddScenarios(new[]
            {
                new BenchmarkScenario { Name = "s1", ExpectedOutput = "a" },
                new BenchmarkScenario { Name = "s2", ExpectedOutput = "b" }
            });
            Assert.Equal(2, suite.ScenarioCount);
        }

        // ================================================================
        // RemoveScenario
        // ================================================================

        [Fact]
        public void RemoveScenario_Existing_ReturnsTrue()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddScenario(new BenchmarkScenario { Name = "s1", ExpectedOutput = "a" });
            Assert.True(suite.RemoveScenario("s1"));
            Assert.Equal(0, suite.ScenarioCount);
        }

        [Fact]
        public void RemoveScenario_NonExistent_ReturnsFalse()
        {
            var suite = new PromptBenchmarkSuite();
            Assert.False(suite.RemoveScenario("nope"));
        }

        // ================================================================
        // Run - validation
        // ================================================================

        [Fact]
        public void Run_NoVariants_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddScenario(new BenchmarkScenario { Name = "s1", ExpectedOutput = "x" });
            Assert.Throws<InvalidOperationException>(() => suite.Run());
        }

        [Fact]
        public void Run_NoScenarios_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello"));
            Assert.Throws<InvalidOperationException>(() => suite.Run());
        }

        [Fact]
        public void Run_NoMatchingTagFilter_Throws()
        {
            var suite = CreateSuiteWithOneVariantOneScenario();
            Assert.Throws<InvalidOperationException>(() =>
                suite.Run(tagFilter: "nonexistent-tag"));
        }

        // ================================================================
        // Run - scoring modes
        // ================================================================

        [Fact]
        public void Run_ExactMatch_PerfectScore()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("exact", new PromptTemplate("Hello World"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "Hello World"
            });

            var report = suite.Run(BenchmarkScoring.ExactMatch);
            Assert.Equal(1.0, report.Results[0].AverageScore);
        }

        [Fact]
        public void Run_ExactMatch_NoMatch_ZeroScore()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("mismatch", new PromptTemplate("Goodbye"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "Hello World"
            });

            var report = suite.Run(BenchmarkScoring.ExactMatch);
            Assert.Equal(0.0, report.Results[0].AverageScore);
        }

        [Fact]
        public void Run_KeywordOverlap_WithKeywords()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("The quick brown fox jumps"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "anything",
                ExpectedKeywords = new List<string> { "quick", "fox", "missing" }
            });

            var report = suite.Run(BenchmarkScoring.KeywordOverlap);
            // 2 out of 3 keywords found
            Assert.InRange(report.Results[0].AverageScore, 0.6, 0.7);
        }

        [Fact]
        public void Run_NgramSimilarity_IdenticalStrings_PerfectScore()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello World"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "Hello World"
            });

            var report = suite.Run(BenchmarkScoring.NgramSimilarity);
            Assert.Equal(1.0, report.Results[0].AverageScore, 4);
        }

        [Fact]
        public void Run_LcsRatio_IdenticalStrings_PerfectScore()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello World"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "Hello World"
            });

            var report = suite.Run(BenchmarkScoring.LcsRatio);
            Assert.Equal(1.0, report.Results[0].AverageScore, 4);
        }

        [Fact]
        public void Run_Composite_AveragesAllMetrics()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello World"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "Hello World",
                ExpectedKeywords = new List<string> { "Hello", "World" }
            });

            var report = suite.Run(BenchmarkScoring.Composite);
            // All metrics should be 1.0 for identical strings
            Assert.Equal(1.0, report.Results[0].AverageScore, 4);
        }

        // ================================================================
        // Run - multiple variants ranked correctly
        // ================================================================

        [Fact]
        public void Run_MultipleVariants_BestRankedFirst()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("perfect", new PromptTemplate("The answer is 42"));
            suite.AddVariant("close", new PromptTemplate("The answer is 43"));
            suite.AddVariant("wrong", new PromptTemplate("Completely different text"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "The answer is 42"
            });

            var report = suite.Run(BenchmarkScoring.Composite);
            Assert.Equal("perfect", report.Winner);
            Assert.Equal("perfect", report.Results[0].Name);
            Assert.True(report.Results[0].AverageScore >= report.Results[1].AverageScore);
            Assert.True(report.Results[1].AverageScore >= report.Results[2].AverageScore);
        }

        // ================================================================
        // Run - tag filtering
        // ================================================================

        [Fact]
        public void Run_TagFilter_OnlyMatchingScenarios()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "tagged",
                ExpectedOutput = "Hello",
                Tags = new HashSet<string> { "fast" }
            });
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "untagged",
                ExpectedOutput = "other"
            });

            var report = suite.Run(tagFilter: "fast");
            Assert.Equal(1, report.ScenarioCount);
        }

        // ================================================================
        // Run - template render failure handled gracefully
        // ================================================================

        [Fact]
        public void Run_TemplateRenderFailure_DoesNotThrow()
        {
            var suite = new PromptBenchmarkSuite();
            // Template expects {{name}} but scenario doesn't provide it
            suite.AddVariant("v1", new PromptTemplate("Hello {{name}}"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                Variables = new Dictionary<string, string>(),
                ExpectedOutput = "Hello World"
            });

            // Should not throw; render failure produces empty string
            var report = suite.Run();
            Assert.Single(report.Results);
        }

        // ================================================================
        // BenchmarkReport
        // ================================================================

        [Fact]
        public void Report_Metadata_IsCorrect()
        {
            var suite = CreateSuiteWithOneVariantOneScenario();
            var report = suite.Run();

            Assert.Equal(1, report.ScenarioCount);
            Assert.Equal(1, report.VariantCount);
            Assert.NotEqual(default, report.Timestamp);
        }

        [Fact]
        public void Report_ToTable_ContainsVariantName()
        {
            var suite = CreateSuiteWithOneVariantOneScenario();
            var report = suite.Run();
            var table = report.ToTable();

            Assert.Contains("v1", table);
            Assert.Contains("Prompt Benchmark Report", table);
            Assert.Contains("Winner: v1", table);
        }

        [Fact]
        public void Report_ToJson_IsValidJson()
        {
            var suite = CreateSuiteWithOneVariantOneScenario();
            var report = suite.Run();
            var json = report.ToJson();

            Assert.Contains("\"winner\"", json);
            Assert.Contains("\"v1\"", json);
            // Should parse without error
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.NotNull(doc);
        }

        [Fact]
        public void Report_ToCsv_HasHeaderAndData()
        {
            var suite = CreateSuiteWithOneVariantOneScenario();
            var report = suite.Run();
            var csv = report.ToCsv();

            Assert.Contains("Rank,Variant,AverageScore", csv);
            Assert.Contains("v1", csv);
        }

        // ================================================================
        // VariantResult statistics
        // ================================================================

        [Fact]
        public void VariantResult_StdDev_SingleScore_IsZero()
        {
            var suite = CreateSuiteWithOneVariantOneScenario();
            var report = suite.Run();
            Assert.Equal(0.0, report.Results[0].StdDev);
        }

        [Fact]
        public void VariantResult_MultipleScenarios_StatsCorrect()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello World"));
            suite.AddScenario(new BenchmarkScenario { Name = "s1", ExpectedOutput = "Hello World" });
            suite.AddScenario(new BenchmarkScenario { Name = "s2", ExpectedOutput = "Something Else" });

            var report = suite.Run(BenchmarkScoring.ExactMatch);
            var result = report.Results[0];

            Assert.Equal(1.0, result.MaxScore);
            Assert.Equal(0.0, result.MinScore);
            Assert.Equal(0.5, result.AverageScore);
            Assert.True(result.StdDev > 0);
        }

        // ================================================================
        // HeadToHead
        // ================================================================

        [Fact]
        public void HeadToHead_ReturnsWinnerAndMargin()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("good", new PromptTemplate("The answer is 42"));
            suite.AddVariant("bad", new PromptTemplate("I have no idea"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "The answer is 42"
            });

            var (winner, margin, report) = suite.HeadToHead("good", "bad");
            Assert.Equal("good", winner);
            Assert.True(margin > 0);
            Assert.NotNull(report);
        }

        [Fact]
        public void HeadToHead_UnknownVariant_Throws()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello"));
            suite.AddScenario(new BenchmarkScenario { Name = "s1", ExpectedOutput = "Hello" });

            Assert.Throws<ArgumentException>(() =>
                suite.HeadToHead("v1", "nonexistent"));
        }

        // ================================================================
        // Edge cases - empty expected output / keywords
        // ================================================================

        [Fact]
        public void Run_EmptyExpectedOutput_DoesNotThrow()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Some output"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = ""
            });

            var report = suite.Run();
            Assert.Single(report.Results);
        }

        [Fact]
        public void Run_KeywordOverlap_NoKeywords_UsesWordOverlap()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("quick brown fox"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "quick brown fox jumps",
                ExpectedKeywords = new List<string>() // empty
            });

            var report = suite.Run(BenchmarkScoring.KeywordOverlap);
            // Should use word overlap fallback, score > 0
            Assert.True(report.Results[0].AverageScore > 0);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static PromptBenchmarkSuite CreateSuiteWithOneVariantOneScenario()
        {
            var suite = new PromptBenchmarkSuite();
            suite.AddVariant("v1", new PromptTemplate("Hello World"));
            suite.AddScenario(new BenchmarkScenario
            {
                Name = "s1",
                ExpectedOutput = "Hello World"
            });
            return suite;
        }
    }
}
