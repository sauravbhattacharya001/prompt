namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using Xunit;

    public class PromptPerformanceProfilerTests
    {
        [Fact]
        public void Record_StoresSample()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("test", TimeSpan.FromMilliseconds(100), 50, 30);

            var samples = profiler.GetSamples("test");
            Assert.Single(samples);
            Assert.Equal(100, samples[0].Duration.TotalMilliseconds);
            Assert.Equal(50, samples[0].InputTokens);
            Assert.Equal(30, samples[0].OutputTokens);
            Assert.Equal(80, samples[0].TotalTokens);
            Assert.True(samples[0].Success);
        }

        [Fact]
        public void Record_MultipleSamples()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("a", TimeSpan.FromMilliseconds(100));
            profiler.Record("a", TimeSpan.FromMilliseconds(200));
            profiler.Record("b", TimeSpan.FromMilliseconds(300));

            Assert.Equal(2, profiler.GetSamples("a").Count);
            Assert.Single(profiler.GetSamples("b"));
            Assert.Equal(new[] { "a", "b" }, profiler.GetLabels());
        }

        [Fact]
        public void GetSamples_UnknownLabel_ReturnsEmpty()
        {
            var profiler = new PromptPerformanceProfiler();
            Assert.Empty(profiler.GetSamples("nope"));
        }

        [Fact]
        public void GetStats_ComputesLatencyPercentiles()
        {
            var profiler = new PromptPerformanceProfiler();
            var values = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            foreach (var v in values)
                profiler.Record("x", TimeSpan.FromMilliseconds(v));

            var stats = profiler.GetStats("x");
            Assert.Equal(10, stats.SampleCount);
            Assert.Equal(55, stats.MeanLatencyMs);
            Assert.Equal(10, stats.MinLatencyMs);
            Assert.Equal(100, stats.MaxLatencyMs);
            Assert.True(stats.P95LatencyMs > 80);
            Assert.True(stats.P99LatencyMs > 90);
        }

        [Fact]
        public void GetStats_ComputesSuccessRate()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("x", TimeSpan.FromMilliseconds(100), success: true);
            profiler.Record("x", TimeSpan.FromMilliseconds(200), success: true);
            profiler.Record("x", TimeSpan.FromMilliseconds(300), success: false, error: "timeout");

            var stats = profiler.GetStats("x");
            Assert.Equal(2, stats.SuccessCount);
            Assert.Equal(1, stats.FailureCount);
            Assert.InRange(stats.SuccessRate, 0.66, 0.67);
        }

        [Fact]
        public void GetStats_ComputesTokenStats()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("x", TimeSpan.FromMilliseconds(1000), 100, 50);
            profiler.Record("x", TimeSpan.FromMilliseconds(1000), 200, 100);

            var stats = profiler.GetStats("x");
            Assert.Equal(150, stats.MeanInputTokens);
            Assert.Equal(75, stats.MeanOutputTokens);
            Assert.Equal(225, stats.MeanTotalTokens);
            Assert.NotNull(stats.TokensPerSecond);
            Assert.Equal(225, stats.TokensPerSecond!.Value, 1);
        }

        [Fact]
        public void GetStats_EmptyLabel_ReturnsZeroStats()
        {
            var profiler = new PromptPerformanceProfiler();
            var stats = profiler.GetStats("empty");
            Assert.Equal(0, stats.SampleCount);
        }

        [Fact]
        public void StartSample_TimesExecution()
        {
            var profiler = new PromptPerformanceProfiler();
            using (var ctx = profiler.StartSample("timed"))
            {
                Thread.Sleep(50);
                ctx.SetTokens(10, 5);
            }

            var samples = profiler.GetSamples("timed");
            Assert.Single(samples);
            Assert.True(samples[0].Duration.TotalMilliseconds >= 40);
            Assert.Equal(10, samples[0].InputTokens);
            Assert.Equal(5, samples[0].OutputTokens);
        }

        [Fact]
        public void StartSample_RecordsError()
        {
            var profiler = new PromptPerformanceProfiler();
            using (var ctx = profiler.StartSample("err"))
            {
                ctx.SetError("boom");
            }

            var samples = profiler.GetSamples("err");
            Assert.False(samples[0].Success);
            Assert.Equal("boom", samples[0].Error);
        }

        [Fact]
        public void Compare_ReturnsWinner()
        {
            var profiler = new PromptPerformanceProfiler();
            // A is slower
            for (int i = 0; i < 10; i++)
                profiler.Record("slow", TimeSpan.FromMilliseconds(500), 200, 100);
            // B is faster
            for (int i = 0; i < 10; i++)
                profiler.Record("fast", TimeSpan.FromMilliseconds(100), 100, 50);

            var cmp = profiler.Compare("slow", "fast");
            Assert.Equal("fast", cmp.Winner);
            Assert.True(cmp.LatencyDiffPercent > 0); // positive = B faster
            Assert.NotNull(cmp.TokenDiffPercent);
            Assert.True(cmp.TokenDiffPercent > 0); // positive = B uses fewer
        }

        [Fact]
        public void Compare_ThrowsOnMissingSamples()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("a", TimeSpan.FromMilliseconds(100));

            Assert.Throws<InvalidOperationException>(() => profiler.Compare("a", "missing"));
            Assert.Throws<InvalidOperationException>(() => profiler.Compare("missing", "a"));
        }

        [Fact]
        public void GenerateReport_IncludesAllLabels()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("alpha", TimeSpan.FromMilliseconds(100), 50, 25);
            profiler.Record("beta", TimeSpan.FromMilliseconds(200));

            var report = profiler.GenerateReport("Test Report");
            Assert.Contains("Test Report", report);
            Assert.Contains("alpha", report);
            Assert.Contains("beta", report);
            Assert.Contains("p95=", report);
        }

        [Fact]
        public void GenerateReport_EmptyProfiler()
        {
            var profiler = new PromptPerformanceProfiler();
            var report = profiler.GenerateReport();
            Assert.Contains("No samples recorded", report);
        }

        [Fact]
        public void ExportJson_ValidJson()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("j", TimeSpan.FromMilliseconds(42), 10, 5);

            var json = profiler.ExportJson();
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("j", out var jElem));
            Assert.True(jElem.TryGetProperty("stats", out _));
            Assert.True(jElem.TryGetProperty("samples", out _));
        }

        [Fact]
        public void Clear_RemovesAllSamples()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("a", TimeSpan.FromMilliseconds(100));
            profiler.Record("b", TimeSpan.FromMilliseconds(200));
            profiler.Clear();

            Assert.Empty(profiler.GetLabels());
        }

        [Fact]
        public void Clear_SpecificLabel()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("a", TimeSpan.FromMilliseconds(100));
            profiler.Record("b", TimeSpan.FromMilliseconds(200));
            profiler.Clear("a");

            Assert.Single(profiler.GetLabels());
            Assert.Equal("b", profiler.GetLabels()[0]);
        }

        [Fact]
        public void Record_WithTags()
        {
            var profiler = new PromptPerformanceProfiler();
            var tags = new Dictionary<string, string> { { "model", "gpt-4" }, { "temp", "0.7" } };
            profiler.Record("tagged", TimeSpan.FromMilliseconds(100), tags: tags);

            var sample = profiler.GetSamples("tagged")[0];
            Assert.Equal("gpt-4", sample.Tags["model"]);
            Assert.Equal("0.7", sample.Tags["temp"]);
        }

        [Fact]
        public void StdDev_SingleSample_ReturnsZero()
        {
            var profiler = new PromptPerformanceProfiler();
            profiler.Record("one", TimeSpan.FromMilliseconds(100));

            var stats = profiler.GetStats("one");
            Assert.Equal(0, stats.StdDevLatencyMs);
        }
    }
}
