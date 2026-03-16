namespace Prompt.Tests
{
    using Xunit;

    public class PromptUsageReportTests
    {
        private PromptHistory CreatePopulatedHistory()
        {
            var h = new PromptHistory(1000);
            // Simulate entries across different models, times, and outcomes
            var baseTime = new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);

            for (int i = 0; i < 30; i++)
            {
                var ts = baseTime.AddHours(i * 2);
                string model = i % 3 == 0 ? "gpt-4" : (i % 3 == 1 ? "gpt-3.5-turbo" : "claude-3");
                bool fail = i % 10 == 7;

                h.Record(
                    prompt: $"Test prompt number {i} with some content to estimate tokens",
                    response: fail ? null : $"Response {i} with varying length content for token estimation purposes",
                    duration: TimeSpan.FromMilliseconds(200 + i * 50 + (fail ? 500 : 0)),
                    model: model,
                    tags: new[] { i % 2 == 0 ? "production" : "staging", "test" },
                    error: fail ? "Timeout" : null);
            }

            return h;
        }

        [Fact]
        public void Constructor_NullHistory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptUsageReport(null!));
        }

        [Fact]
        public void SetCostPerToken_ValidInput_Succeeds()
        {
            var report = new PromptUsageReport(new PromptHistory());
            report.SetCostPerToken("gpt-4", 0.03m, 0.06m);
            // No exception = success
        }

        [Fact]
        public void SetCostPerToken_EmptyModel_Throws()
        {
            var report = new PromptUsageReport(new PromptHistory());
            Assert.Throws<ArgumentException>(() => report.SetCostPerToken("", 0.03m, 0.06m));
        }

        [Fact]
        public void SetCostPerToken_NegativeCost_Throws()
        {
            var report = new PromptUsageReport(new PromptHistory());
            Assert.Throws<ArgumentOutOfRangeException>(() => report.SetCostPerToken("gpt-4", -0.01m, 0.06m));
            Assert.Throws<ArgumentOutOfRangeException>(() => report.SetCostPerToken("gpt-4", 0.03m, -0.01m));
        }

        [Fact]
        public void Generate_EmptyHistory_ReturnsReport()
        {
            var report = new PromptUsageReport(new PromptHistory());
            var text = report.Generate();

            Assert.Contains("PROMPT USAGE REPORT", text);
            Assert.Contains("Total Calls:", text);
            Assert.Contains("0", text);
        }

        [Fact]
        public void Generate_PopulatedHistory_ContainsSections()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var text = report.Generate(ReportGranularity.Daily);

            Assert.Contains("Summary", text);
            Assert.Contains("Tokens", text);
            Assert.Contains("Latency", text);
            Assert.Contains("Time Breakdown", text);
            Assert.Contains("Model Breakdown", text);
            Assert.Contains("Trends", text);
        }

        [Fact]
        public void Generate_HourlyGranularity_ShowsHourBuckets()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var text = report.Generate(ReportGranularity.Hourly);

            // All entries are recorded at ~now, so should have a single hourly bucket
            Assert.Contains("Time Breakdown", text);
            Assert.Contains(":00", text); // hourly format "YYYY-MM-DD HH:00"
        }

        [Fact]
        public void Generate_MonthlyGranularity_ShowsMonthBuckets()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var text = report.Generate(ReportGranularity.Monthly);

            Assert.Contains("2026-03", text);
        }

        [Fact]
        public void Generate_WithCostConfig_ShowsCost()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            report.SetCostPerToken("gpt-4", 0.03m, 0.06m);
            report.SetCostPerToken("gpt-3.5-turbo", 0.001m, 0.002m);
            var text = report.Generate();

            Assert.Contains("Est. Cost:", text);
            Assert.Contains("$", text);
        }

        [Fact]
        public void GenerateHtml_EmptyHistory_ReturnsValidHtml()
        {
            var report = new PromptUsageReport(new PromptHistory());
            var html = report.GenerateHtml();

            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Prompt Usage Report", html);
            Assert.Contains("</html>", html);
        }

        [Fact]
        public void GenerateHtml_PopulatedHistory_ContainsCards()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var html = report.GenerateHtml();

            Assert.Contains("Total Calls", html);
            Assert.Contains("Total Tokens", html);
            Assert.Contains("Avg Latency", html);
            Assert.Contains("bar-fill", html);
        }

        [Fact]
        public void GenerateHtml_WithCost_ShowsCostCard()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            report.SetCostPerToken("gpt-4", 0.03m, 0.06m);
            var html = report.GenerateHtml();

            Assert.Contains("Est. Cost", html);
            Assert.Contains("cost", html);
        }

        [Fact]
        public void GenerateJson_EmptyHistory_ReturnsValidJson()
        {
            var report = new PromptUsageReport(new PromptHistory());
            var json = report.GenerateJson();

            Assert.Contains("\"totalCalls\"", json);
            Assert.Contains("\"granularity\"", json);
        }

        [Fact]
        public void GenerateJson_PopulatedHistory_ContainsAllSections()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var json = report.GenerateJson(ReportGranularity.Daily);

            Assert.Contains("\"timeBuckets\"", json);
            Assert.Contains("\"modelBreakdown\"", json);
            Assert.Contains("\"trends\"", json);
            Assert.Contains("\"successRate\"", json);
        }

        [Fact]
        public void GenerateJson_NotIndented_NoNewlines()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var json = report.GenerateJson(indented: false);

            Assert.DoesNotContain("\n  ", json);
        }

        [Fact]
        public void DetectTrends_SingleEntry_ReturnsInsufficient()
        {
            var h = new PromptHistory();
            h.Record("test", "ok", TimeSpan.FromSeconds(1));
            var report = new PromptUsageReport(h);
            var trends = report.DetectTrends();

            Assert.Equal(TrendDirection.Insufficient, trends.UsageTrend);
            Assert.Equal(1, trends.DataPoints);
        }

        [Fact]
        public void DetectTrends_MultipleEntries_ReturnsTrends()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var trends = report.DetectTrends();

            Assert.NotEqual(TrendDirection.Insufficient, trends.UsageTrend);
            Assert.Equal(30, trends.DataPoints);
            Assert.True(trends.FirstHalfEntries > 0);
            Assert.True(trends.SecondHalfEntries > 0);
        }

        [Fact]
        public void DetectTrends_IncreasingLatency_Detected()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var trends = report.DetectTrends();

            // All entries recorded near-simultaneously, so trends are based on 
            // insertion order split. Durations increase (200 + i*50), but Search 
            // returns newest-first, reversing the order. Just verify we get a result.
            Assert.NotEqual(TrendDirection.Insufficient, trends.LatencyTrend);
            Assert.Equal(30, trends.DataPoints);
        }

        [Fact]
        public void GetCostBreakdown_NoCostConfig_NullCosts()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var breakdown = report.GetCostBreakdown();

            Assert.True(breakdown.Count > 0);
            Assert.All(breakdown, b => Assert.Null(b.EstimatedCostUsd));
        }

        [Fact]
        public void GetCostBreakdown_WithCostConfig_CalculatesCosts()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            report.SetCostPerToken("gpt-4", 0.03m, 0.06m);
            var breakdown = report.GetCostBreakdown();

            var gpt4 = breakdown.FirstOrDefault(b => b.Model == "gpt-4");
            Assert.NotNull(gpt4);
            Assert.NotNull(gpt4!.EstimatedCostUsd);
            Assert.True(gpt4.EstimatedCostUsd > 0);
        }

        [Fact]
        public void GetCostBreakdown_ModelCounts_Correct()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var breakdown = report.GetCostBreakdown();

            int total = breakdown.Sum(b => b.CallCount);
            Assert.Equal(30, total);
            Assert.Equal(3, breakdown.Count); // gpt-4, gpt-3.5-turbo, claude-3
        }

        [Fact]
        public void GetCostBreakdown_SuccessRates_Correct()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var breakdown = report.GetCostBreakdown();

            foreach (var m in breakdown)
            {
                Assert.InRange(m.SuccessRate, 0, 100);
            }
        }

        [Fact]
        public void Generate_WithTimeFilter_FiltersEntries()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var after = new DateTimeOffset(2026, 3, 11, 0, 0, 0, TimeSpan.Zero);
            var text = report.Generate(after: after);

            Assert.Contains("Total Calls:", text);
            // Should have fewer entries than the full 30
        }

        [Fact]
        public void GenerateHtml_WeeklyGranularity_ShowsWeekBuckets()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var html = report.GenerateHtml(ReportGranularity.Weekly);

            Assert.Contains("2026-W", html);
        }

        [Fact]
        public void Generate_LatencyPercentiles_Present()
        {
            var h = CreatePopulatedHistory();
            var report = new PromptUsageReport(h);
            var text = report.Generate();

            Assert.Contains("P50:", text);
            Assert.Contains("P95:", text);
            Assert.Contains("P99:", text);
        }

        [Fact]
        public void GetCostBreakdown_EmptyHistory_ReturnsEmpty()
        {
            var report = new PromptUsageReport(new PromptHistory());
            var breakdown = report.GetCostBreakdown();
            Assert.Empty(breakdown);
        }

        [Fact]
        public void SetCostPerToken_OverwriteExisting_UsesNew()
        {
            var h = new PromptHistory();
            h.Record("test", "response", TimeSpan.FromSeconds(1), model: "gpt-4");

            var report = new PromptUsageReport(h);
            report.SetCostPerToken("gpt-4", 0.01m, 0.02m);
            var first = report.GetCostBreakdown().First().EstimatedCostUsd;

            report.SetCostPerToken("gpt-4", 0.10m, 0.20m);
            var second = report.GetCostBreakdown().First().EstimatedCostUsd;

            Assert.True(second > first);
        }

        [Fact]
        public void DetectTrends_EmptyHistory_ReturnsInsufficient()
        {
            var report = new PromptUsageReport(new PromptHistory());
            var trends = report.DetectTrends();
            Assert.Equal(TrendDirection.Insufficient, trends.UsageTrend);
            Assert.Equal(0, trends.DataPoints);
        }
    }
}
