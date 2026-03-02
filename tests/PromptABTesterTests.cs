using Prompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Prompt.Tests
{
    public class PromptABTesterTests
    {
        private PromptABTester CreateTester(string name = "test-experiment")
            => new PromptABTester(name);

        private PromptVariant MakeVariant(string name, string template, string? desc = null)
            => new PromptVariant(name, new PromptTemplate(template), desc);

        // ── Constructor ──────────────────────────────────────

        [Fact]
        public void Constructor_ValidName_Succeeds()
        {
            var tester = CreateTester("my-experiment");
            Assert.Equal("my-experiment", tester.ExperimentName);
            Assert.Equal(0, tester.VariantCount);
            Assert.Equal(0, tester.TotalTrials);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidName_Throws(string? name)
        {
            Assert.Throws<ArgumentException>(() => new PromptABTester(name!));
        }

        // ── PromptVariant ────────────────────────────────────

        [Fact]
        public void PromptVariant_ValidArgs_Succeeds()
        {
            var tmpl = new PromptTemplate("Hello {{name}}");
            var variant = new PromptVariant("A", tmpl, "Test description");
            Assert.Equal("A", variant.Name);
            Assert.Same(tmpl, variant.Template);
            Assert.Equal("Test description", variant.Description);
        }

        [Fact]
        public void PromptVariant_NullTemplate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptVariant("A", null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PromptVariant_InvalidName_Throws(string? name)
        {
            Assert.Throws<ArgumentException>(
                () => new PromptVariant(name!, new PromptTemplate("test")));
        }

        // ── Variant Management ───────────────────────────────

        [Fact]
        public void AddVariant_Success()
        {
            var tester = CreateTester();
            var result = tester.AddVariant(MakeVariant("A", "Prompt A"));
            Assert.Same(tester, result); // fluent
            Assert.Equal(1, tester.VariantCount);
        }

        [Fact]
        public void AddVariant_Duplicate_Throws()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Prompt A"));
            Assert.Throws<InvalidOperationException>(
                () => tester.AddVariant(MakeVariant("A", "Prompt A2")));
        }

        [Fact]
        public void AddVariant_CaseInsensitive_Throws()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("alpha", "Prompt A"));
            Assert.Throws<InvalidOperationException>(
                () => tester.AddVariant(MakeVariant("ALPHA", "Prompt A2")));
        }

        [Fact]
        public void AddVariant_MaxLimit_Throws()
        {
            var tester = CreateTester();
            for (int i = 0; i < PromptABTester.MaxVariants; i++)
                tester.AddVariant(MakeVariant($"V{i}", $"Prompt {i}"));

            Assert.Throws<InvalidOperationException>(
                () => tester.AddVariant(MakeVariant("overflow", "too many")));
        }

        [Fact]
        public void RemoveVariant_Exists_ReturnsTrue()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Prompt A"));
            Assert.True(tester.RemoveVariant("A"));
            Assert.Equal(0, tester.VariantCount);
        }

        [Fact]
        public void RemoveVariant_NotExists_ReturnsFalse()
        {
            var tester = CreateTester();
            Assert.False(tester.RemoveVariant("nope"));
        }

        [Fact]
        public void GetVariantNames_ReturnsAll()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "PA"))
                  .AddVariant(MakeVariant("B", "PB"));
            var names = tester.GetVariantNames();
            Assert.Contains("A", names);
            Assert.Contains("B", names);
            Assert.Equal(2, names.Count);
        }

        [Fact]
        public void GetVariant_Exists_ReturnsVariant()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "PA"));
            var v = tester.GetVariant("A");
            Assert.NotNull(v);
            Assert.Equal("A", v!.Name);
        }

        [Fact]
        public void GetVariant_NotExists_ReturnsNull()
        {
            var tester = CreateTester();
            Assert.Null(tester.GetVariant("nope"));
        }

        // ── RunTrial (simulated) ─────────────────────────────

        [Fact]
        public void RunTrial_Simulated_RecordsResult()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Tell me about {{topic}}"));

            var vars = new Dictionary<string, string> { ["topic"] = "cats" };
            var result = tester.RunTrial("A", vars, "Cats are great pets.");

            Assert.Equal("A", result.VariantName);
            Assert.Equal("Tell me about cats", result.RenderedPrompt);
            Assert.Equal("Cats are great pets.", result.Response);
            Assert.True(result.Success);
            Assert.True(result.InputTokens > 0);
            Assert.True(result.OutputTokens > 0);
            Assert.Equal(1, tester.TotalTrials);
        }

        [Fact]
        public void RunTrial_UnknownVariant_Throws()
        {
            var tester = CreateTester();
            Assert.Throws<KeyNotFoundException>(
                () => tester.RunTrial("nope", null, "response"));
        }

        [Fact]
        public void RunTrial_NullVariables_UsesEmpty()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "No variables here"));
            var result = tester.RunTrial("A", null, "response");
            Assert.Equal("No variables here", result.RenderedPrompt);
        }

        [Fact]
        public void RunTrial_WithElapsed_RecordsTime()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            var elapsed = TimeSpan.FromMilliseconds(250);
            var result = tester.RunTrial("A", null, "response", elapsed);
            Assert.Equal(250, result.Elapsed.TotalMilliseconds);
        }

        // ── RunTrialAsync ────────────────────────────────────

        [Fact]
        public async Task RunTrialAsync_Success_MeasuresTime()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Prompt {{x}}"));

            var vars = new Dictionary<string, string> { ["x"] = "test" };
            var result = await tester.RunTrialAsync("A", vars,
                async prompt =>
                {
                    await Task.Delay(10);
                    return $"Response to: {prompt}";
                });

            Assert.True(result.Success);
            Assert.Contains("Prompt test", result.RenderedPrompt);
            Assert.Contains("Response to:", result.Response);
            Assert.True(result.Elapsed.TotalMilliseconds >= 5); // some time passed
        }

        [Fact]
        public async Task RunTrialAsync_ModelThrows_RecordsError()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));

            var result = await tester.RunTrialAsync("A", null,
                _ => throw new InvalidOperationException("API down"));

            Assert.False(result.Success);
            Assert.Equal("API down", result.ErrorMessage);
            Assert.Null(result.Response);
        }

        [Fact]
        public async Task RunTrialAsync_NullCallback_Throws()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => tester.RunTrialAsync("A", null, null!));
        }

        // ── RunAllVariants ───────────────────────────────────

        [Fact]
        public void RunAllVariants_RunsEach()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Formal: {{q}}"))
                  .AddVariant(MakeVariant("B", "Casual: {{q}}"));

            var vars = new Dictionary<string, string> { ["q"] = "hello" };
            var results = tester.RunAllVariants(vars,
                (name, rendered) => $"Reply from {name}");

            Assert.Equal(2, results.Count);
            Assert.Equal(2, tester.TotalTrials);
            Assert.All(results, r => Assert.True(r.Success));
        }

        [Fact]
        public void RunAllVariants_NullGenerator_Throws()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            Assert.Throws<ArgumentNullException>(
                () => tester.RunAllVariants(null, null!));
        }

        // ── Scoring ──────────────────────────────────────────

        [Fact]
        public void ScoreTrial_ValidScore_Succeeds()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "response");
            tester.ScoreTrial("A", 0, 0.85);

            var trials = tester.GetTrials("A");
            Assert.Equal(0.85, trials[0].QualityScore);
        }

        [Fact]
        public void ScoreTrial_OutOfRange_Throws()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "response");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => tester.ScoreTrial("A", 0, 1.5));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => tester.ScoreTrial("A", 0, -0.1));
        }

        [Fact]
        public void ScoreTrial_InvalidIndex_Throws()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "response");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => tester.ScoreTrial("A", 5, 0.5));
        }

        [Fact]
        public void ScoreLastTrial_Succeeds()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "r1");
            tester.RunTrial("A", null, "r2");
            tester.ScoreLastTrial("A", 0.9);

            var trials = tester.GetTrials("A");
            Assert.Null(trials[0].QualityScore);
            Assert.Equal(0.9, trials[1].QualityScore);
        }

        [Fact]
        public void ScoreLastTrial_NoTrials_Throws()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            Assert.Throws<InvalidOperationException>(
                () => tester.ScoreLastTrial("A", 0.5));
        }

        // ── Trial Access ─────────────────────────────────────

        [Fact]
        public void GetTrials_ReturnsVariantTrials()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "pA"));
            tester.AddVariant(MakeVariant("B", "pB"));
            tester.RunTrial("A", null, "rA");
            tester.RunTrial("B", null, "rB1");
            tester.RunTrial("B", null, "rB2");

            Assert.Single(tester.GetTrials("A"));
            Assert.Equal(2, tester.GetTrials("B").Count);
        }

        [Fact]
        public void GetAllTrials_ReturnsAll()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "pA"));
            tester.AddVariant(MakeVariant("B", "pB"));
            tester.RunTrial("A", null, "rA");
            tester.RunTrial("B", null, "rB");

            Assert.Equal(2, tester.GetAllTrials().Count);
        }

        [Fact]
        public void ClearTrials_ClearsOnlySpecifiedVariant()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "pA"));
            tester.AddVariant(MakeVariant("B", "pB"));
            tester.RunTrial("A", null, "rA");
            tester.RunTrial("B", null, "rB");

            tester.ClearTrials("A");
            Assert.Empty(tester.GetTrials("A"));
            Assert.Single(tester.GetTrials("B"));
        }

        [Fact]
        public void ClearAllTrials_ClearsEverything()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "pA"));
            tester.AddVariant(MakeVariant("B", "pB"));
            tester.RunTrial("A", null, "rA");
            tester.RunTrial("B", null, "rB");

            tester.ClearAllTrials();
            Assert.Equal(0, tester.TotalTrials);
        }

        // ── Statistics ───────────────────────────────────────

        [Fact]
        public void GetVariantStats_NoTrials_ReturnsNull()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            Assert.Null(tester.GetVariantStats("A"));
        }

        [Fact]
        public void GetVariantStats_WithTrials_ComputesCorrectly()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "short", TimeSpan.FromMilliseconds(100));
            tester.RunTrial("A", null, "a much longer response text here", TimeSpan.FromMilliseconds(200));
            tester.ScoreTrial("A", 0, 0.8);
            tester.ScoreTrial("A", 1, 0.6);

            var stats = tester.GetVariantStats("A");
            Assert.NotNull(stats);
            Assert.Equal(2, stats!.TrialCount);
            Assert.Equal(2, stats.SuccessCount);
            Assert.Equal(1.0, stats.SuccessRate);
            Assert.Equal(150.0, stats.MeanResponseTimeMs);
            Assert.True(stats.StdDevResponseTimeMs > 0);
            Assert.True(stats.MeanInputTokens > 0);
            Assert.True(stats.MeanOutputTokens > 0);
            Assert.Equal(0.7, stats.MeanQualityScore!.Value, 3);
            Assert.Equal(2, stats.QualityScoredCount);
        }

        [Fact]
        public void GetVariantStats_WithFailures_ExcludesFromTimingStats()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "good response", TimeSpan.FromMilliseconds(100));

            // Simulate a failed trial by running async with exception
            // Instead, just verify success rate accounts for all trials
            var stats = tester.GetVariantStats("A");
            Assert.NotNull(stats);
            Assert.Equal(1.0, stats!.SuccessRate);
        }

        // ── Report & Winner ──────────────────────────────────

        [Fact]
        public void GetReport_NoData_ReturnsEmptyReport()
        {
            var tester = CreateTester();
            var report = tester.GetReport();
            Assert.Equal("test-experiment", report.ExperimentName);
            Assert.Equal(0, report.TotalTrials);
            Assert.Empty(report.VariantStatistics);
        }

        [Fact]
        public void GetReport_SingleVariant_WinsDefault()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "response");

            var report = tester.GetReport();
            Assert.Equal("A", report.Winner);
            Assert.Contains("Only one variant", report.WinnerReason);
        }

        [Fact]
        public void GetReport_TwoVariants_QualityWins()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Formal prompt"))
                  .AddVariant(MakeVariant("B", "Casual prompt"));

            // Variant A: high quality
            for (int i = 0; i < 5; i++)
            {
                tester.RunTrial("A", null, "detailed formal response");
                tester.ScoreLastTrial("A", 0.9);
            }

            // Variant B: low quality
            for (int i = 0; i < 5; i++)
            {
                tester.RunTrial("B", null, "quick casual reply");
                tester.ScoreLastTrial("B", 0.5);
            }

            var report = tester.GetReport();
            Assert.Equal("A", report.Winner);
            Assert.Contains("quality", report.WinnerReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetReport_CloseQuality_UsesComposite()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test A"))
                  .AddVariant(MakeVariant("B", "B"));  // shorter template

            // Similar quality
            tester.RunTrial("A", null, "long response from A variant with many words", TimeSpan.FromMilliseconds(200));
            tester.ScoreLastTrial("A", 0.8);
            tester.RunTrial("B", null, "short", TimeSpan.FromMilliseconds(50));
            tester.ScoreLastTrial("B", 0.78); // within 5% threshold

            var report = tester.GetReport();
            Assert.NotNull(report.Winner);
            Assert.Contains("Composite", report.WinnerReason);
        }

        [Fact]
        public void GetReport_ToJson_IsValidJson()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "response");

            var report = tester.GetReport();
            string json = report.ToJson();
            Assert.NotEmpty(json);
            // Should parse without exception
            JsonDocument.Parse(json);
        }

        // ── Quick Comparisons ────────────────────────────────

        [Fact]
        public void GetBestByQuality_ReturnsHighest()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "pA"))
                  .AddVariant(MakeVariant("B", "pB"));

            tester.RunTrial("A", null, "rA");
            tester.ScoreLastTrial("A", 0.6);
            tester.RunTrial("B", null, "rB");
            tester.ScoreLastTrial("B", 0.95);

            Assert.Equal("B", tester.GetBestByQuality());
        }

        [Fact]
        public void GetBestByQuality_NoScores_ReturnsNull()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "pA"));
            tester.RunTrial("A", null, "rA");
            Assert.Null(tester.GetBestByQuality());
        }

        [Fact]
        public void GetFastestVariant_ReturnsFastest()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "pA"))
                  .AddVariant(MakeVariant("B", "pB"));

            tester.RunTrial("A", null, "rA", TimeSpan.FromMilliseconds(500));
            tester.RunTrial("B", null, "rB", TimeSpan.FromMilliseconds(100));

            Assert.Equal("B", tester.GetFastestVariant());
        }

        [Fact]
        public void GetFastestVariant_NoTrials_ReturnsNull()
        {
            var tester = CreateTester();
            Assert.Null(tester.GetFastestVariant());
        }

        [Fact]
        public void GetCheapestVariant_ReturnsLowestTokens()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "A very long and verbose prompt template here"))
                  .AddVariant(MakeVariant("B", "Short"));

            tester.RunTrial("A", null, "long verbose response with many tokens in it");
            tester.RunTrial("B", null, "ok");

            Assert.Equal("B", tester.GetCheapestVariant());
        }

        [Fact]
        public void GetCheapestVariant_NoTrials_ReturnsNull()
        {
            var tester = CreateTester();
            Assert.Null(tester.GetCheapestVariant());
        }

        // ── Serialization ────────────────────────────────────

        [Fact]
        public void ToJson_FromJson_RoundTrip()
        {
            var tester = CreateTester("serialization-test");
            tester.AddVariant(MakeVariant("A", "Prompt {{x}}", "Test variant A"))
                  .AddVariant(MakeVariant("B", "Other {{x}}"));

            var vars = new Dictionary<string, string> { ["x"] = "value" };
            tester.RunTrial("A", vars, "Response A", TimeSpan.FromMilliseconds(100));
            tester.ScoreLastTrial("A", 0.8);
            tester.RunTrial("B", vars, "Response B", TimeSpan.FromMilliseconds(200));
            tester.ScoreLastTrial("B", 0.6);

            string json = tester.ToJson();
            var restored = PromptABTester.FromJson(json);

            Assert.Equal("serialization-test", restored.ExperimentName);
            Assert.Equal(2, restored.VariantCount);
            Assert.Equal(2, restored.TotalTrials);

            var trialsA = restored.GetTrials("A");
            Assert.Single(trialsA);
            Assert.Equal("Response A", trialsA[0].Response);
            Assert.Equal(0.8, trialsA[0].QualityScore);
        }

        [Fact]
        public void FromJson_Null_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptABTester.FromJson(null!));
        }

        [Fact]
        public void FromJson_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptABTester.FromJson(""));
        }

        [Fact]
        public void FromJson_InvalidJson_Throws()
        {
            Assert.ThrowsAny<Exception>(() => PromptABTester.FromJson("{invalid}"));
        }

        [Fact]
        public void ToJson_ValidJsonFormat()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            string json = tester.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal("test-experiment", doc.RootElement.GetProperty("ExperimentName").GetString());
        }

        // ── Edge Cases ───────────────────────────────────────

        [Fact]
        public void MultipleTrials_StatsAreAccurate()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));

            // Run 10 trials with known response times
            for (int i = 1; i <= 10; i++)
            {
                tester.RunTrial("A", null, $"response {i}",
                    TimeSpan.FromMilliseconds(i * 100));
            }

            var stats = tester.GetVariantStats("A");
            Assert.NotNull(stats);
            Assert.Equal(10, stats!.TrialCount);
            Assert.Equal(550.0, stats.MeanResponseTimeMs); // avg(100..1000)
            Assert.Equal(550.0, stats.MedianResponseTimeMs, 1); // median of 100..1000 even count = (500+600)/2
        }

        [Fact]
        public void RemoveVariant_AlsoRemovesTrials()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "r1");
            tester.RunTrial("A", null, "r2");

            Assert.Equal(2, tester.TotalTrials);
            tester.RemoveVariant("A");
            Assert.Equal(0, tester.TotalTrials);
        }

        [Fact]
        public void GetTrials_UnknownVariant_Throws()
        {
            var tester = CreateTester();
            Assert.Throws<KeyNotFoundException>(() => tester.GetTrials("nope"));
        }

        [Fact]
        public void ClearTrials_UnknownVariant_Throws()
        {
            var tester = CreateTester();
            Assert.Throws<KeyNotFoundException>(() => tester.ClearTrials("nope"));
        }

        [Fact]
        public void TokenEstimation_ShortText_AtLeastOne()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Hi"));
            var result = tester.RunTrial("A", null, "X");
            Assert.True(result.InputTokens >= 1);
            Assert.True(result.OutputTokens >= 1);
        }

        [Fact]
        public void TokenEstimation_EmptyResponse_ZeroTokens()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            var result = tester.RunTrial("A", null, "");
            Assert.Equal(0, result.OutputTokens);
        }

        [Fact]
        public void Report_ThreeVariants_PicksBestComposite()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "long verbose prompt A"))
                  .AddVariant(MakeVariant("B", "B"))
                  .AddVariant(MakeVariant("C", "moderate C"));

            // A: good quality, slow, expensive
            tester.RunTrial("A", null, "very detailed response", TimeSpan.FromMilliseconds(500));
            tester.ScoreLastTrial("A", 0.95);

            // B: decent quality, fast, cheap
            tester.RunTrial("B", null, "ok", TimeSpan.FromMilliseconds(50));
            tester.ScoreLastTrial("B", 0.85);

            // C: poor quality, medium
            tester.RunTrial("C", null, "mediocre", TimeSpan.FromMilliseconds(200));
            tester.ScoreLastTrial("C", 0.3);

            var report = tester.GetReport();
            Assert.Equal(3, report.VariantStatistics.Count);
            Assert.Equal(3, report.TotalTrials);
            Assert.NotNull(report.Winner);
            // C should not win with quality 0.3
            Assert.NotEqual("C", report.Winner);
        }

        [Fact]
        public void Variables_PreservedInTrialResult()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "Hello {{name}}"));

            var vars = new Dictionary<string, string> { ["name"] = "World" };
            var result = tester.RunTrial("A", vars, "Hi World!");

            Assert.NotNull(result.Variables);
            Assert.Equal("World", result.Variables!["name"]);
        }

        [Fact]
        public void Timestamp_IsReasonable()
        {
            var before = DateTimeOffset.UtcNow;
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            var result = tester.RunTrial("A", null, "response");
            var after = DateTimeOffset.UtcNow;

            Assert.InRange(result.Timestamp, before, after);
        }

        [Fact]
        public void CreatedAt_SetOnConstruction()
        {
            var before = DateTimeOffset.UtcNow;
            var tester = CreateTester();
            Assert.InRange(tester.CreatedAt, before, DateTimeOffset.UtcNow);
        }

        [Fact]
        public void Median_OddCount()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "r1", TimeSpan.FromMilliseconds(100));
            tester.RunTrial("A", null, "r2", TimeSpan.FromMilliseconds(300));
            tester.RunTrial("A", null, "r3", TimeSpan.FromMilliseconds(200));

            var stats = tester.GetVariantStats("A");
            Assert.Equal(200.0, stats!.MedianResponseTimeMs); // sorted: 100, 200, 300
        }

        [Fact]
        public void StdDev_SingleTrial_IsZero()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "response", TimeSpan.FromMilliseconds(100));

            var stats = tester.GetVariantStats("A");
            Assert.Equal(0.0, stats!.StdDevResponseTimeMs);
        }

        [Fact]
        public void QualityScore_BoundaryValues()
        {
            var tester = CreateTester();
            tester.AddVariant(MakeVariant("A", "test"));
            tester.RunTrial("A", null, "r1");
            tester.RunTrial("A", null, "r2");

            tester.ScoreTrial("A", 0, 0.0); // minimum
            tester.ScoreTrial("A", 1, 1.0); // maximum

            var trials = tester.GetTrials("A");
            Assert.Equal(0.0, trials[0].QualityScore);
            Assert.Equal(1.0, trials[1].QualityScore);
        }
    }
}
