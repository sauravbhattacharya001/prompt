namespace Prompt.Tests
{
    using Xunit;

    public class PromptStaleDetectorTests
    {
        private static PromptAge MakePrompt(string id, string name, int daysAgo, string model = "gpt-4-0613") =>
            new()
            {
                PromptId = id,
                Name = name,
                LastModified = DateTimeOffset.UtcNow.AddDays(-daysAgo),
                TargetModel = model,
                Tags = new() { "test" }
            };

        [Fact]
        public void FreshPrompt_ReturnsNoStaleness()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p1", "Fresh Prompt", 5, "gpt-4o-2024-08-06"));
            var scan = detector.Scan();

            Assert.Equal(0, scan.StaleCount);
            Assert.Equal(StalenessSeverity.Fresh, scan.Reports[0].Severity);
        }

        [Fact]
        public void AgingPrompt_DetectedByAge()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p2", "Aging Prompt", 45, "gpt-4o-2024-08-06"));
            var scan = detector.Scan();

            Assert.Equal(StalenessSeverity.Aging, scan.Reports[0].Severity);
        }

        [Fact]
        public void ModelDrift_BumpsToAging()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p3", "Drifted Prompt", 10, "gpt-4-0613"));
            var scan = detector.Scan();

            Assert.True(scan.Reports[0].ModelDrift);
            Assert.Equal(StalenessSeverity.Aging, scan.Reports[0].Severity);
        }

        [Fact]
        public void StalePrompt_DetectedByAge()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p4", "Stale Prompt", 100, "gpt-4o-2024-08-06"));
            var scan = detector.Scan();

            Assert.Equal(StalenessSeverity.Stale, scan.Reports[0].Severity);
        }

        [Fact]
        public void CriticalPrompt_WithDriftAndAge()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p5", "Critical Prompt", 95, "gpt-4-0613"));
            var scan = detector.Scan();

            Assert.Equal(StalenessSeverity.Critical, scan.Reports[0].Severity);
        }

        [Fact]
        public void CriticalPrompt_ByAgeAlone()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p6", "Ancient Prompt", 200, "gpt-4o-2024-08-06"));
            var scan = detector.Scan();

            Assert.Equal(StalenessSeverity.Critical, scan.Reports[0].Severity);
        }

        [Fact]
        public void Scan_SortsBySeverityDescending()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("fresh", "Fresh", 1, "gpt-4o-2024-08-06"));
            detector.Register(MakePrompt("critical", "Critical", 200, "gpt-4-0613"));
            detector.Register(MakePrompt("aging", "Aging", 40, "gpt-4o-2024-08-06"));
            var scan = detector.Scan();

            Assert.Equal("critical", scan.Reports[0].Prompt.PromptId);
            Assert.Equal("fresh", scan.Reports[^1].Prompt.PromptId);
        }

        [Fact]
        public void FindStale_FiltersCorrectly()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("fresh", "Fresh", 1, "gpt-4o-2024-08-06"));
            detector.Register(MakePrompt("stale", "Stale", 100, "gpt-4o-2024-08-06"));
            var stale = detector.FindStale();

            Assert.Single(stale);
            Assert.Equal("stale", stale[0].Prompt.PromptId);
        }

        [Fact]
        public void ScanByTags_FiltersToMatchingTags()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(new PromptAge
            {
                PromptId = "tagged", Name = "Tagged",
                LastModified = DateTimeOffset.UtcNow.AddDays(-100),
                TargetModel = "gpt-4o-2024-08-06",
                Tags = new() { "production" }
            });
            detector.Register(new PromptAge
            {
                PromptId = "other", Name = "Other",
                LastModified = DateTimeOffset.UtcNow.AddDays(-100),
                TargetModel = "gpt-4o-2024-08-06",
                Tags = new() { "draft" }
            });

            var scan = detector.ScanByTags(new[] { "production" });
            Assert.Equal(1, scan.TotalPrompts);
            Assert.Equal("tagged", scan.Reports[0].Prompt.PromptId);
        }

        [Fact]
        public void Summarize_ProducesReadableOutput()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p1", "Test Prompt", 95, "gpt-4-0613"));
            var summary = detector.Summarize();

            Assert.Contains("Prompt Staleness Scan", summary);
            Assert.Contains("Test Prompt", summary);
            Assert.Contains("gpt-4o-2024-08-06", summary);
        }

        [Fact]
        public void CustomThresholds_AreRespected()
        {
            var thresholds = new StalenessThresholds { AgingDays = 5, StaleDays = 10, CriticalDays = 20 };
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06", thresholds);
            detector.Register(MakePrompt("p1", "Quick Stale", 12, "gpt-4o-2024-08-06"));
            var scan = detector.Scan();

            Assert.Equal(StalenessSeverity.Stale, scan.Reports[0].Severity);
        }

        [Fact]
        public void AddModelFamily_CustomFamily()
        {
            var detector = new PromptStaleDetector("llama-3.1");
            detector.AddModelFamily("llama", new() { "llama-2", "llama-3", "llama-3.1" });
            detector.Register(MakePrompt("p1", "Llama Prompt", 5, "llama-2"));
            var scan = detector.Scan();

            Assert.True(scan.Reports[0].ModelDrift);
        }

        [Fact]
        public void ToJson_ReturnsValidJson()
        {
            var detector = new PromptStaleDetector("gpt-4o-2024-08-06");
            detector.Register(MakePrompt("p1", "JSON Test", 50));
            var scan = detector.Scan();
            var json = scan.ToJson();

            Assert.Contains("\"totalPrompts\"", json);
            Assert.Contains("\"reports\"", json);
        }
    }
}
