namespace Prompt.Tests
{
    using Xunit;
    using System.Text.Json;

    public class PromptRegressionDetectorTests
    {
        // ──────────── Constructor ────────────

        [Fact]
        public void Constructor_Default_ZeroBaselines()
        {
            var detector = new PromptRegressionDetector();
            Assert.Equal(0, detector.BaselineCount);
        }

        // ──────────── RecordBaseline ────────────

        [Fact]
        public void RecordBaseline_AddsBaseline()
        {
            var detector = new PromptRegressionDetector();
            var template = new PromptTemplate("Hello, {{name}}!");
            var inputs = new Dictionary<string, string> { ["name"] = "World" };

            var baseline = detector.RecordBaseline("greeting", "v1", template, inputs);

            Assert.Equal("greeting", baseline.PromptId);
            Assert.Equal("v1", baseline.Version);
            Assert.Equal("Hello, World!", baseline.Output);
            Assert.Equal(1, detector.BaselineCount);
        }

        [Fact]
        public void RecordBaselineRaw_StoresOutput()
        {
            var detector = new PromptRegressionDetector();
            var inputs = new Dictionary<string, string> { ["q"] = "test" };

            var baseline = detector.RecordBaselineRaw("llm", "v1", inputs, "LLM output here");

            Assert.Equal("LLM output here", baseline.Output);
            Assert.Equal(1, detector.BaselineCount);
        }

        // ──────────── Check (identical) ────────────

        [Fact]
        public void Check_IdenticalTemplate_PassesWithNoRegression()
        {
            var detector = new PromptRegressionDetector();
            var template = new PromptTemplate("Summarize: {{text}}");
            var inputs = new Dictionary<string, string> { ["text"] = "AI is amazing" };
            detector.RecordBaseline("summarizer", "v1", template, inputs);

            var report = detector.Check("summarizer", "v2", template);

            Assert.True(report.Passed);
            Assert.Equal(0, report.RegressionsFound);
            Assert.Equal(RegressionSeverity.None, report.MaxSeverity);
        }

        // ──────────── Check (changed) ────────────

        [Fact]
        public void Check_ChangedTemplate_DetectsRegression()
        {
            var detector = new PromptRegressionDetector();
            var original = new PromptTemplate("Summarize: {{text}}");
            var inputs = new Dictionary<string, string> { ["text"] = "AI is amazing" };
            detector.RecordBaseline("summarizer", "v1", original, inputs);

            var changed = new PromptTemplate("Please write a detailed analysis of: {{text}} and include references.");
            var report = detector.Check("summarizer", "v2", changed);

            Assert.True(report.RegressionsFound > 0);
            Assert.True(report.Findings[0].Similarity < 1.0);
        }

        // ──────────── Check (no baselines) ────────────

        [Fact]
        public void Check_NoBaselines_EmptyReport()
        {
            var detector = new PromptRegressionDetector();
            var template = new PromptTemplate("Hello");
            var report = detector.Check("missing", "v1", template);

            Assert.True(report.Passed);
            Assert.Equal(0, report.TotalChecked);
        }

        // ──────────── CheckRaw ────────────

        [Fact]
        public void CheckRaw_DetectsChanges()
        {
            var detector = new PromptRegressionDetector();
            var inputs = new Dictionary<string, string> { ["q"] = "test" };
            detector.RecordBaselineRaw("llm", "v1", inputs, "The answer is 42.");

            var report = detector.CheckRaw("llm", "v2", inputs, "Something completely different and unrelated.");

            Assert.True(report.RegressionsFound > 0);
        }

        // ──────────── Severity classification ────────────

        [Fact]
        public void Severity_IdenticalOutputs_None()
        {
            var sim = PromptRegressionDetector.ComputeSimilarity("hello world", "hello world");
            Assert.Equal(1.0, sim);
        }

        [Fact]
        public void Severity_CompletelyDifferent_Low_Similarity()
        {
            var sim = PromptRegressionDetector.ComputeSimilarity(
                "The quick brown fox jumps over the lazy dog",
                "12345 67890 abcde fghij klmno pqrst uvwxyz");
            Assert.True(sim < 0.5);
        }

        // ──────────── FindDifferences ────────────

        [Fact]
        public void FindDifferences_ReportsLengthChange()
        {
            var diffs = PromptRegressionDetector.FindDifferences("short", "this is much longer text");
            Assert.Contains(diffs, d => d.Contains("Length changed"));
        }

        // ──────────── Export / Import ────────────

        [Fact]
        public void ExportImport_RoundTrip()
        {
            var detector = new PromptRegressionDetector();
            var template = new PromptTemplate("Test: {{x}}");
            detector.RecordBaseline("test", "v1", template, new Dictionary<string, string> { ["x"] = "a" });
            detector.RecordBaseline("test", "v1", template, new Dictionary<string, string> { ["x"] = "b" });

            var json = detector.ExportBaselines();
            var detector2 = new PromptRegressionDetector();
            var count = detector2.ImportBaselines(json);

            Assert.Equal(2, count);
            Assert.Equal(2, detector2.BaselineCount);
        }

        // ──────────── ListPromptIds / ClearBaselines ────────────

        [Fact]
        public void ListPromptIds_ReturnsAll()
        {
            var detector = new PromptRegressionDetector();
            detector.RecordBaselineRaw("a", "v1", new(), "out1");
            detector.RecordBaselineRaw("b", "v1", new(), "out2");

            var ids = detector.ListPromptIds();
            Assert.Equal(2, ids.Count);
            Assert.Contains("a", ids);
            Assert.Contains("b", ids);
        }

        [Fact]
        public void ClearBaselines_RemovesPrompt()
        {
            var detector = new PromptRegressionDetector();
            detector.RecordBaselineRaw("a", "v1", new(), "out");
            Assert.True(detector.ClearBaselines("a"));
            Assert.Equal(0, detector.BaselineCount);
        }

        // ──────────── Report Output ────────────

        [Fact]
        public void Report_ToText_ContainsHeader()
        {
            var report = new RegressionReport(new List<RegressionFinding>(), RegressionSeverity.Medium);
            var text = report.ToText();
            Assert.Contains("REGRESSION REPORT", text);
            Assert.Contains("PASS", text);
        }

        [Fact]
        public void Report_ToJson_ValidJson()
        {
            var report = new RegressionReport(new List<RegressionFinding>(), RegressionSeverity.Medium);
            var json = report.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal("true", doc.RootElement.GetProperty("passed").ToString().ToLower());
        }

        // ──────────── Threshold override ────────────

        [Fact]
        public void Check_CustomThreshold_AffectsVerdict()
        {
            var detector = new PromptRegressionDetector();
            var original = new PromptTemplate("Say: {{msg}}");
            detector.RecordBaseline("t", "v1", original, new Dictionary<string, string> { ["msg"] = "hi" });

            var changed = new PromptTemplate("Tell me: {{msg}} please");
            // Low threshold should fail even minor changes
            var report = detector.Check("t", "v2", changed, RegressionSeverity.Low);
            // Strict threshold makes it harder to pass
            Assert.True(report.Findings.Any());
        }
    }
}
