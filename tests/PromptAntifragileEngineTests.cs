namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptAntifragileEngineTests
    {
        private static PromptAntifragileEngine CreateEngine(AntifragileConfig? config = null)
            => new(config ?? new AntifragileConfig());

        private static StressResponse MakeResponse(
            string promptId, StressorType type, int intensity, double quality,
            bool succeeded = true, double latency = 100, int tokens = 50)
            => new()
            {
                PromptId = promptId,
                StressorType = type,
                Intensity = intensity,
                QualityScore = quality,
                BaselineQuality = 1.0,
                Succeeded = succeeded,
                LatencyMs = latency,
                TokensUsed = tokens,
                Timestamp = DateTime.UtcNow
            };

        // ── StressorGenerator ───────────────────────────

        [Fact]
        public void GenerateStressors_ReturnsGraduatedSeries()
        {
            var engine = CreateEngine();
            var stressors = engine.GenerateStressors(StressorType.TokenCompression, 5);
            Assert.Equal(5, stressors.Count);
            Assert.Equal(1, stressors[0].Intensity);
            Assert.Equal(5, stressors[4].Intensity);
            Assert.All(stressors, s => Assert.Equal(StressorType.TokenCompression, s.Type));
        }

        [Fact]
        public void GenerateStressors_ThrowsOnInvalidIntensity()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.GenerateStressors(StressorType.InputNoise, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.GenerateStressors(StressorType.InputNoise, 11));
        }

        [Fact]
        public void GenerateStressors_HasDescriptions()
        {
            var engine = CreateEngine();
            var stressors = engine.GenerateStressors(StressorType.LatencyPressure, 3);
            Assert.All(stressors, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
        }

        [Fact]
        public void GenerateCompositeBattery_CoversAllTypes()
        {
            var engine = CreateEngine();
            var battery = engine.GenerateCompositeBattery(3);
            var types = battery.Select(s => s.Type).Distinct().ToList();
            // Should cover all types except Composite itself
            Assert.Contains(StressorType.TokenCompression, types);
            Assert.Contains(StressorType.InputNoise, types);
            Assert.Contains(StressorType.AdversarialInput, types);
        }

        [Fact]
        public void GenerateCompositeBattery_ThrowsOnInvalid()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.GenerateCompositeBattery(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.GenerateCompositeBattery(11));
        }

        // ── StressResponseTracker ───────────────────────

        [Fact]
        public void RecordResponse_StoresResponse()
        {
            var engine = CreateEngine();
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 3, 0.8));
            var responses = engine.GetResponses("p1");
            Assert.Single(responses);
            Assert.Single(responses[StressorType.InputNoise]);
        }

        [Fact]
        public void RecordResponse_ThrowsOnNullOrEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentNullException>(() => engine.RecordResponse(null!));
            Assert.Throws<ArgumentException>(() =>
                engine.RecordResponse(new StressResponse { PromptId = "" }));
        }

        [Fact]
        public void RecordResponse_TrimsToMaxHistory()
        {
            var config = new AntifragileConfig { MaxHistoryPerDimension = 3 };
            var engine = CreateEngine(config);
            for (int i = 0; i < 5; i++)
                engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, i + 1, 0.5));
            var responses = engine.GetResponses("p1");
            Assert.Equal(3, responses[StressorType.InputNoise].Count);
        }

        [Fact]
        public void GetResponses_EmptyForUnknownPrompt()
        {
            var engine = CreateEngine();
            var responses = engine.GetResponses("unknown");
            Assert.Empty(responses);
        }

        [Fact]
        public void GetResponses_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.GetResponses(""));
        }

        [Fact]
        public void RecordResponse_MultipleTypes()
        {
            var engine = CreateEngine();
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 3, 0.8));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 5, 0.6));
            var responses = engine.GetResponses("p1");
            Assert.Equal(2, responses.Count);
        }

        // ── RecoveryTracker ─────────────────────────────

        [Fact]
        public void RecordRecovery_StoresAndComputesRatio()
        {
            var engine = CreateEngine();
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1",
                StressorType = StressorType.InputNoise,
                StressedQuality = 0.5,
                RecoveredQuality = 0.95,
                BaselineQuality = 1.0
            });
            var recoveries = engine.GetRecoveries("p1");
            Assert.Single(recoveries);
            Assert.Equal(0.95, recoveries[0].RecoveryRatio);
        }

        [Fact]
        public void RecordRecovery_ThrowsOnNullOrEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentNullException>(() => engine.RecordRecovery(null!));
            Assert.Throws<ArgumentException>(() =>
                engine.RecordRecovery(new RecoveryObservation { PromptId = "" }));
        }

        [Fact]
        public void GetRecoveries_EmptyForUnknownPrompt()
        {
            var engine = CreateEngine();
            Assert.Empty(engine.GetRecoveries("unknown"));
        }

        [Fact]
        public void RecordRecovery_ZeroBaseline()
        {
            var engine = CreateEngine();
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1",
                StressorType = StressorType.InputNoise,
                StressedQuality = 0.5,
                RecoveredQuality = 0.8,
                BaselineQuality = 0.0
            });
            var recoveries = engine.GetRecoveries("p1");
            Assert.Equal(0.0, recoveries[0].RecoveryRatio);
        }

        // ── FragilityClassifier ─────────────────────────

        [Fact]
        public void ClassifyDimension_FragileWhenQualityDegrades()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // Quality drops significantly with intensity
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.4));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.1));
            Assert.Equal(FragilityClass.Fragile, engine.ClassifyDimension("p1", StressorType.InputNoise));
        }

        [Fact]
        public void ClassifyDimension_RobustWhenStable()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.85));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.84));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.83));
            Assert.Equal(FragilityClass.Robust, engine.ClassifyDimension("p1", StressorType.InputNoise));
        }

        [Fact]
        public void ClassifyDimension_AntifragileWhenImproves()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // Quality improves with intensity
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.6));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.7));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.85));
            Assert.Equal(FragilityClass.Antifragile, engine.ClassifyDimension("p1", StressorType.InputNoise));
        }

        [Fact]
        public void ClassifyDimension_AntifragileFromRecovery()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.8));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.7));
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1",
                StressorType = StressorType.InputNoise,
                StressedQuality = 0.7,
                RecoveredQuality = 1.1,
                BaselineQuality = 1.0
            });
            Assert.Equal(FragilityClass.Antifragile, engine.ClassifyDimension("p1", StressorType.InputNoise));
        }

        [Fact]
        public void ClassifyDimension_ResilientWhenRecoversWell()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.5));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.3));
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1",
                StressorType = StressorType.InputNoise,
                StressedQuality = 0.3,
                RecoveredQuality = 0.92,
                BaselineQuality = 1.0
            });
            Assert.Equal(FragilityClass.Resilient, engine.ClassifyDimension("p1", StressorType.InputNoise));
        }

        [Fact]
        public void ClassifyDimension_DefaultsWhenInsufficientData()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 5 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.1)); // only 1 test
            Assert.Equal(FragilityClass.Robust, engine.ClassifyDimension("p1", StressorType.InputNoise));
        }

        [Fact]
        public void ClassifyDimension_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.ClassifyDimension("", StressorType.InputNoise));
        }

        [Fact]
        public void ClassifyOverall_FragileIfAnyFragile()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // One robust dimension
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.85));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.84));
            // One fragile dimension
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 10, 0.1));
            Assert.Equal(FragilityClass.Fragile, engine.ClassifyOverall("p1"));
        }

        [Fact]
        public void ClassifyOverall_RobustWhenAllRobust()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.85));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.84));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 1, 0.80));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 10, 0.79));
            Assert.Equal(FragilityClass.Robust, engine.ClassifyOverall("p1"));
        }

        [Fact]
        public void ClassifyOverall_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.ClassifyOverall(""));
        }

        // ── BreakpointDetector ──────────────────────────

        [Fact]
        public void DetectBreakpoints_FindsCliff()
        {
            var engine = CreateEngine(new AntifragileConfig { BreakpointDropThreshold = 0.15 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 2, 0.88));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 3, 0.85));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 4, 0.3)); // cliff!

            var breakpoints = engine.DetectBreakpoints("p1");
            Assert.NotEmpty(breakpoints);
            Assert.Equal(4, breakpoints[0].BreakIntensity);
            Assert.True(breakpoints[0].DropMagnitude >= 0.15);
        }

        [Fact]
        public void DetectBreakpoints_EmptyWhenSmooth()
        {
            var engine = CreateEngine(new AntifragileConfig { BreakpointDropThreshold = 0.15 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 2, 0.88));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 3, 0.85));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 4, 0.82));

            var breakpoints = engine.DetectBreakpoints("p1");
            Assert.Empty(breakpoints);
        }

        [Fact]
        public void DetectBreakpoints_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.DetectBreakpoints(""));
        }

        [Fact]
        public void DetectBreakpoints_ClassifiesBreakType()
        {
            var engine = CreateEngine(new AntifragileConfig { BreakpointDropThreshold = 0.15 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 2, 0.3)); // cliff (0.6 drop)

            var breakpoints = engine.DetectBreakpoints("p1");
            Assert.NotEmpty(breakpoints);
            Assert.Equal("cliff", breakpoints[0].BreakType);
        }

        // ── RecoveryAnalyzer ────────────────────────────

        [Fact]
        public void AnalyzeRecovery_ComputesAverageRatio()
        {
            var engine = CreateEngine();
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1", StressorType = StressorType.InputNoise,
                StressedQuality = 0.5, RecoveredQuality = 0.9, BaselineQuality = 1.0
            });
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1", StressorType = StressorType.InputNoise,
                StressedQuality = 0.4, RecoveredQuality = 1.0, BaselineQuality = 1.0
            });

            var recovery = engine.AnalyzeRecovery("p1");
            Assert.True(recovery[StressorType.InputNoise] >= 0.9);
        }

        [Fact]
        public void AnalyzeRecovery_EmptyWhenNoData()
        {
            var engine = CreateEngine();
            Assert.Empty(engine.AnalyzeRecovery("p1"));
        }

        [Fact]
        public void AnalyzeRecovery_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.AnalyzeRecovery(""));
        }

        // ── HardeningRecommender ────────────────────────

        [Fact]
        public void GetRecommendations_PrioritizesFragileDimensions()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // Fragile in InputNoise
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.1));
            // Robust in LatencyPressure
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 1, 0.85));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 10, 0.83));

            var recs = engine.GetRecommendations("p1");
            Assert.NotEmpty(recs);
            // First rec should target the fragile dimension
            Assert.Equal(StressorType.InputNoise, recs[0].TargetStressor);
            Assert.Equal(1, recs[0].Priority);
        }

        [Fact]
        public void GetRecommendations_IncludesRobustUpgrade()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 1, 0.85));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 10, 0.84));

            var recs = engine.GetRecommendations("p1");
            Assert.NotEmpty(recs); // should get "push to antifragile" rec
            Assert.Contains(recs, r => r.Recommendation.Contains("Antifragile"));
        }

        [Fact]
        public void GetRecommendations_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.GetRecommendations(""));
        }

        // ── Snapshot ────────────────────────────────────

        [Fact]
        public void GetSnapshot_ReturnsFullAnalysis()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            for (int i = 1; i <= 5; i++)
                engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, i, 0.9 - i * 0.1));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal("p1", snap.PromptId);
            Assert.True(snap.AntifragileScore > 0);
            Assert.NotEmpty(snap.VulnerabilityProfiles);
            Assert.True(snap.TotalTests > 0);
        }

        [Fact]
        public void GetSnapshot_IncludesBreakpoints()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2, BreakpointDropThreshold = 0.15 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 2, 0.3));

            var snap = engine.GetSnapshot("p1");
            Assert.NotEmpty(snap.Breakpoints);
        }

        [Fact]
        public void GetSnapshot_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.GetSnapshot(""));
        }

        [Fact]
        public void GetSnapshot_HealthTierMatchesScore()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // High quality, antifragile behavior
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.7));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.8));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.95));
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1", StressorType = StressorType.InputNoise,
                StressedQuality = 0.7, RecoveredQuality = 1.1, BaselineQuality = 1.0
            });

            var snap = engine.GetSnapshot("p1");
            if (snap.AntifragileScore >= 85) Assert.Equal(AntifragileHealthTier.Thriving, snap.HealthTier);
            else if (snap.AntifragileScore >= 70) Assert.Equal(AntifragileHealthTier.Resilient, snap.HealthTier);
            else if (snap.AntifragileScore >= 50) Assert.Equal(AntifragileHealthTier.Stable, snap.HealthTier);
        }

        [Fact]
        public void GetSnapshot_WeakestAndStrongest()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // Weak on noise
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.3));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.2));
            // Strong on latency
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 1, 0.95));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 5, 0.93));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(StressorType.InputNoise, snap.WeakestDimension);
            Assert.Equal(StressorType.LatencyPressure, snap.StrongestDimension);
        }

        // ── Fleet Report ────────────────────────────────

        [Fact]
        public void GetFleetReport_AggregatesMultiplePrompts()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // p1: fragile
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.1));
            // p2: robust
            engine.RecordResponse(MakeResponse("p2", StressorType.InputNoise, 1, 0.85));
            engine.RecordResponse(MakeResponse("p2", StressorType.InputNoise, 10, 0.84));

            var report = engine.GetFleetReport();
            Assert.Equal(2, report.TotalPrompts);
            Assert.True(report.TotalTests > 0);
            Assert.True(report.ClassDistribution[FragilityClass.Fragile] >= 1);
            Assert.True(report.ClassDistribution[FragilityClass.Robust] >= 1);
        }

        [Fact]
        public void GetFleetReport_EmptyWhenNoData()
        {
            var engine = CreateEngine();
            var report = engine.GetFleetReport();
            Assert.Equal(0, report.TotalPrompts);
            Assert.Equal(0, report.TotalTests);
        }

        [Fact]
        public void GetFleetReport_IncludesInsights()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.1));
            var report = engine.GetFleetReport();
            Assert.NotEmpty(report.AutonomousInsights);
        }

        [Fact]
        public void GetFleetReport_IdentifiesFleetWeakness()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            // Both prompts weakest in InputNoise
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.5));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.2));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.LatencyPressure, 5, 0.88));
            engine.RecordResponse(MakeResponse("p2", StressorType.InputNoise, 1, 0.4));
            engine.RecordResponse(MakeResponse("p2", StressorType.InputNoise, 5, 0.15));
            engine.RecordResponse(MakeResponse("p2", StressorType.LatencyPressure, 1, 0.92));
            engine.RecordResponse(MakeResponse("p2", StressorType.LatencyPressure, 5, 0.9));

            var report = engine.GetFleetReport();
            Assert.Equal(StressorType.InputNoise, report.FleetWeakness);
        }

        [Fact]
        public void GetFleetReport_MostVulnerableAndAntifragile()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.1));
            engine.RecordResponse(MakeResponse("p2", StressorType.InputNoise, 1, 0.6));
            engine.RecordResponse(MakeResponse("p2", StressorType.InputNoise, 10, 0.9));

            var report = engine.GetFleetReport();
            Assert.NotEmpty(report.MostVulnerable);
            Assert.NotEmpty(report.MostAntifragile);
        }

        // ── TrackedPrompts ──────────────────────────────

        [Fact]
        public void GetTrackedPrompts_ReturnsList()
        {
            var engine = CreateEngine();
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.8));
            engine.RecordResponse(MakeResponse("p2", StressorType.InputNoise, 1, 0.7));
            var tracked = engine.GetTrackedPrompts();
            Assert.Equal(2, tracked.Count);
            Assert.Contains("p1", tracked);
            Assert.Contains("p2", tracked);
        }

        // ── HTML Dashboard ──────────────────────────────

        [Fact]
        public void GenerateHtmlDashboard_ProducesHtml()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.4));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.1));

            var html = engine.GenerateHtmlDashboard("p1");
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Antifragile Analysis", html);
            Assert.Contains("Vulnerability Profiles", html);
            Assert.Contains("Breakpoints", html);
        }

        [Fact]
        public void GenerateHtmlDashboard_ThrowsOnEmpty()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.GenerateHtmlDashboard(""));
        }

        [Fact]
        public void GenerateHtmlDashboard_IncludesRecoverySection()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.4));
            engine.RecordRecovery(new RecoveryObservation
            {
                PromptId = "p1", StressorType = StressorType.InputNoise,
                StressedQuality = 0.4, RecoveredQuality = 0.95, BaselineQuality = 1.0
            });

            var html = engine.GenerateHtmlDashboard("p1");
            Assert.Contains("Recovery Analysis", html);
        }

        // ── FailureRate ─────────────────────────────────

        [Fact]
        public void Snapshot_TracksFailureRate()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9, succeeded: true));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.5, succeeded: true));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 10, 0.0, succeeded: false));

            var snap = engine.GetSnapshot("p1");
            Assert.True(snap.OverallFailureRate > 0);
        }

        [Fact]
        public void Snapshot_ZeroFailureWhenAllSucceed()
        {
            var engine = CreateEngine(new AntifragileConfig { MinTestsPerStressor = 2 });
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 1, 0.9));
            engine.RecordResponse(MakeResponse("p1", StressorType.InputNoise, 5, 0.8));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(0, snap.OverallFailureRate);
        }
    }
}
