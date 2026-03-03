namespace Prompt.Tests
{
    using Xunit;

    public class PromptAnalyticsTests
    {
        [Fact]
        public void StartRender_Complete_TracksRenderCount()
        {
            var analytics = new PromptAnalytics();
            var tracker = analytics.StartRender("test-template");
            tracker.Complete(new[] { "name", "role" });

            var stats = analytics.GetStats("test-template");
            Assert.NotNull(stats);
            Assert.Equal(1, stats.RenderCount);
            Assert.Equal(0, stats.ErrorCount);
            Assert.Equal(1, stats.TotalRenders);
        }

        [Fact]
        public void StartRender_Fail_TracksErrorCount()
        {
            var analytics = new PromptAnalytics();
            var tracker = analytics.StartRender("bad-template");
            tracker.Fail("Missing variable");

            var stats = analytics.GetStats("bad-template");
            Assert.NotNull(stats);
            Assert.Equal(0, stats.RenderCount);
            Assert.Equal(1, stats.ErrorCount);
            Assert.Equal(1, stats.TotalRenders);
        }

        [Fact]
        public void StartRender_NullName_Throws()
        {
            var analytics = new PromptAnalytics();
            Assert.Throws<ArgumentException>(() => analytics.StartRender(""));
            Assert.Throws<ArgumentException>(() => analytics.StartRender("  "));
        }

        [Fact]
        public void RecordRender_TracksWithoutTiming()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("simple", new[] { "x" });
            analytics.RecordRender("simple", new[] { "x", "y" });

            var stats = analytics.GetStats("simple");
            Assert.NotNull(stats);
            Assert.Equal(2, stats.RenderCount);
            Assert.Equal(0, stats.AverageRenderMs);
        }

        [Fact]
        public void RecordError_TracksErrors()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordError("err-template", "oops");
            analytics.RecordError("err-template", "oops again");

            var stats = analytics.GetStats("err-template");
            Assert.NotNull(stats);
            Assert.Equal(2, stats.ErrorCount);
            Assert.Equal(1.0, stats.ErrorRate);
            Assert.Equal(2, stats.RecentErrors.Count);
        }

        [Fact]
        public void GetStats_UnknownTemplate_ReturnsNull()
        {
            var analytics = new PromptAnalytics();
            Assert.Null(analytics.GetStats("nonexistent"));
        }

        [Fact]
        public void GetAllStats_OrdersByRenderCountDescending()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("low");
            analytics.RecordRender("high");
            analytics.RecordRender("high");
            analytics.RecordRender("high");
            analytics.RecordRender("mid");
            analytics.RecordRender("mid");

            var all = analytics.GetAllStats();
            Assert.Equal(3, all.Count);
            Assert.Equal("high", all[0].TemplateName);
            Assert.Equal("mid", all[1].TemplateName);
            Assert.Equal("low", all[2].TemplateName);
        }

        [Fact]
        public void GetTopTemplates_ReturnsLimitedResults()
        {
            var analytics = new PromptAnalytics();
            for (int i = 0; i < 15; i++)
                analytics.RecordRender($"template-{i}");

            var top5 = analytics.GetTopTemplates(5);
            Assert.Equal(5, top5.Count);
        }

        [Fact]
        public void GetErrorProne_FiltersAndSortsByErrorRate()
        {
            var analytics = new PromptAnalytics();

            // 50% error rate
            analytics.RecordRender("half-broken");
            analytics.RecordError("half-broken", "fail");

            // 100% error rate
            analytics.RecordError("fully-broken", "fail");

            // 0% error rate
            analytics.RecordRender("fine");

            var errorProne = analytics.GetErrorProne();
            Assert.Equal(2, errorProne.Count);
            Assert.Equal("fully-broken", errorProne[0].TemplateName);
            Assert.Equal("half-broken", errorProne[1].TemplateName);
        }

        [Fact]
        public void GetTopVariables_AggregatesAcrossTemplates()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("t1", new[] { "name", "role" });
            analytics.RecordRender("t2", new[] { "name", "topic" });
            analytics.RecordRender("t3", new[] { "name" });

            var topVars = analytics.GetTopVariables();
            Assert.True(topVars.Count >= 3);
            Assert.Equal("name", topVars[0].Key);
            Assert.Equal(3, topVars[0].Value);
        }

        [Fact]
        public void GenerateReport_EmptyAnalytics_ReturnsMessage()
        {
            var analytics = new PromptAnalytics();
            var report = analytics.GenerateReport();
            Assert.Equal("No analytics data recorded.", report);
        }

        [Fact]
        public void GenerateReport_WithData_ContainsSections()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("template-a", new[] { "x" });
            analytics.RecordRender("template-a", new[] { "x" });
            analytics.RecordError("template-b", "broken");

            var report = analytics.GenerateReport();
            Assert.Contains("Prompt Analytics Report", report);
            Assert.Contains("Templates tracked: 2", report);
            Assert.Contains("Total renders: 3", report);
            Assert.Contains("template-a", report);
            Assert.Contains("Error-Prone", report);
        }

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("json-test", new[] { "a", "b" });
            analytics.RecordError("json-test", "err");

            var json = analytics.ToJson();
            Assert.Contains("\"json-test\"", json);
            Assert.Contains("\"renderCount\"", json);
            Assert.Contains("\"errorCount\"", json);
        }

        [Fact]
        public void FromJson_RoundTrips()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("rt-test", new[] { "x" });
            analytics.RecordRender("rt-test", new[] { "x" });
            analytics.RecordError("rt-test", "e1");

            var json = analytics.ToJson();
            var restored = PromptAnalytics.FromJson(json);

            var stats = restored.GetStats("rt-test");
            Assert.NotNull(stats);
            Assert.Equal(2, stats.RenderCount);
            Assert.Equal(1, stats.ErrorCount);
        }

        [Fact]
        public void FromJson_EmptyString_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptAnalytics.FromJson(""));
        }

        [Fact]
        public void Clear_RemovesAllData()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("a");
            analytics.RecordRender("b");
            Assert.Equal(2, analytics.TrackedTemplateCount);

            analytics.Clear();
            Assert.Equal(0, analytics.TrackedTemplateCount);
        }

        [Fact]
        public void Remove_RemovesSpecificTemplate()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("keep");
            analytics.RecordRender("remove");

            Assert.True(analytics.Remove("remove"));
            Assert.Equal(1, analytics.TrackedTemplateCount);
            Assert.Null(analytics.GetStats("remove"));
            Assert.NotNull(analytics.GetStats("keep"));
        }

        [Fact]
        public void RenderTracker_DoubleComplete_IgnoresSecond()
        {
            var analytics = new PromptAnalytics();
            var tracker = analytics.StartRender("double");
            tracker.Complete();
            tracker.Complete(); // should be ignored

            var stats = analytics.GetStats("double");
            Assert.Equal(1, stats!.RenderCount);
        }

        [Fact]
        public void RenderTracker_DoubleFail_IgnoresSecond()
        {
            var analytics = new PromptAnalytics();
            var tracker = analytics.StartRender("double-fail");
            tracker.Fail("first");
            tracker.Fail("second"); // should be ignored

            var stats = analytics.GetStats("double-fail");
            Assert.Equal(1, stats!.ErrorCount);
        }

        [Fact]
        public void ErrorRate_Calculation()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("rate-test");
            analytics.RecordRender("rate-test");
            analytics.RecordRender("rate-test");
            analytics.RecordError("rate-test", "fail");

            var stats = analytics.GetStats("rate-test");
            Assert.Equal(0.25, stats!.ErrorRate, 2);
        }

        [Fact]
        public void RecentErrors_CapsAt10()
        {
            var analytics = new PromptAnalytics();
            for (int i = 0; i < 15; i++)
                analytics.RecordError("capped", $"error-{i}");

            var stats = analytics.GetStats("capped");
            Assert.Equal(10, stats!.RecentErrors.Count);
            Assert.Equal("error-5", stats.RecentErrors[0]); // oldest kept
            Assert.Equal("error-14", stats.RecentErrors[9]); // newest
        }

        [Fact]
        public void CaseInsensitive_TemplateNames()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("MyTemplate");
            analytics.RecordRender("mytemplate");
            analytics.RecordRender("MYTEMPLATE");

            Assert.Equal(1, analytics.TrackedTemplateCount);
            var stats = analytics.GetStats("mytemplate");
            Assert.Equal(3, stats!.RenderCount);
        }

        [Fact]
        public void CaseInsensitive_Variables()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("t", new[] { "Name" });
            analytics.RecordRender("t", new[] { "name" });
            analytics.RecordRender("t", new[] { "NAME" });

            var stats = analytics.GetStats("t");
            Assert.Single(stats!.TopVariables);
            Assert.Equal(3, stats.TopVariables[0].Value);
        }

        [Fact]
        public void Timestamps_AreTracked()
        {
            var analytics = new PromptAnalytics();
            analytics.RecordRender("time-test");

            var stats = analytics.GetStats("time-test");
            Assert.True(stats!.FirstUsed <= DateTimeOffset.UtcNow);
            Assert.True(stats.LastUsed <= DateTimeOffset.UtcNow);
        }
    }
}
