namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptChaosEngineTests
    {
        private static PromptChaosEngine CreateEngine(ChaosConfig? config = null)
            => new(config ?? new ChaosConfig());

        private static SteadyStateMetrics MakeBaseline(double latency = 200, double successRate = 0.95, double quality = 85, double tokens = 500, double errorRate = 0.05)
            => new()
            {
                AvgLatencyMs = latency,
                SuccessRate = successRate,
                AvgQuality = quality,
                AvgTokens = tokens,
                ErrorRate = errorRate,
                SampleCount = 10
            };

        private static SteadyStateMetrics MakeDegraded(double latency = 800, double successRate = 0.6, double quality = 50, double tokens = 600, double errorRate = 0.3)
            => new()
            {
                AvgLatencyMs = latency,
                SuccessRate = successRate,
                AvgQuality = quality,
                AvgTokens = tokens,
                ErrorRate = errorRate,
                SampleCount = 10
            };

        // ── Experiment Design ───────────────────────────

        [Fact]
        public void DesignExperiment_CreatesWithCorrectFields()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("test-1", ChaosInjectionType.LatencySpike, new List<string> { "p1" }, "System handles latency");
            Assert.Equal("test-1", exp.Name);
            Assert.Equal(ChaosInjectionType.LatencySpike, exp.InjectionType);
            Assert.Single(exp.TargetPromptIds);
            Assert.Equal("System handles latency", exp.Hypothesis);
            Assert.Equal(ExperimentPhase.Baseline, exp.Phase);
        }

        [Fact]
        public void DesignExperiment_ThrowsOnEmptyName()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.DesignExperiment("", ChaosInjectionType.TokenCorruption, new List<string> { "p1" }));
        }

        [Fact]
        public void DesignExperiment_ThrowsOnEmptyTargets()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.DesignExperiment("test", ChaosInjectionType.TokenCorruption, new List<string>()));
        }

        [Fact]
        public void DesignExperiment_ThrowsOnNullTargets()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.DesignExperiment("test", ChaosInjectionType.TokenCorruption, null!));
        }

        [Fact]
        public void DesignExperiment_GeneratesUniqueIds()
        {
            var engine = CreateEngine();
            var e1 = engine.DesignExperiment("a", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            var e2 = engine.DesignExperiment("b", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.NotEqual(e1.Id, e2.Id);
        }

        [Fact]
        public void GetExperiments_ReturnsAll()
        {
            var engine = CreateEngine();
            engine.DesignExperiment("a", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.DesignExperiment("b", ChaosInjectionType.TokenCorruption, new List<string> { "p2" });
            Assert.Equal(2, engine.GetExperiments().Count);
        }

        [Fact]
        public void GetExperiment_ReturnsNullForUnknown()
        {
            var engine = CreateEngine();
            Assert.Null(engine.GetExperiment("nonexistent"));
        }

        [Fact]
        public void GetExperiment_ReturnsNullForEmpty()
        {
            var engine = CreateEngine();
            Assert.Null(engine.GetExperiment(""));
        }

        [Fact]
        public void GetExperiment_FindsById()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("find-me", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            var found = engine.GetExperiment(exp.Id);
            Assert.NotNull(found);
            Assert.Equal("find-me", found!.Name);
        }

        // ── Baseline & Fault Injection ──────────────────

        [Fact]
        public void RecordBaseline_SetsBaselineAndPhase()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("bl", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            var updated = engine.GetExperiment(exp.Id)!;
            Assert.NotNull(updated.Baseline);
            Assert.Equal(200, updated.Baseline!.AvgLatencyMs);
        }

        [Fact]
        public void RecordBaseline_ThrowsOnNullMetrics()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("bl", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Throws<ArgumentNullException>(() => engine.RecordBaseline(exp.Id, null!));
        }

        [Fact]
        public void RecordBaseline_ThrowsOnUnknownExperiment()
        {
            var engine = CreateEngine();
            Assert.Throws<KeyNotFoundException>(() => engine.RecordBaseline("nope", MakeBaseline()));
        }

        [Fact]
        public void InjectFault_RequiresBaseline()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("inj", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Throws<InvalidOperationException>(() => engine.InjectFault(exp.Id));
        }

        [Fact]
        public void InjectFault_CreatesInjectionRecord()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("inj", ChaosInjectionType.TokenCorruption, new List<string> { "p1", "p2" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            var inj = engine.InjectFault(exp.Id);
            Assert.Equal(ChaosInjectionType.TokenCorruption, inj.Type);
            Assert.Equal(2, inj.TargetPromptIds.Count);
            Assert.True(inj.Active);
            Assert.True(inj.Parameters.ContainsKey("corruption_rate"));
        }

        [Fact]
        public void InjectFault_SetsPhaseToInjection()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("inj", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            Assert.Equal(ExperimentPhase.Injection, engine.GetExperiment(exp.Id)!.Phase);
        }

        [Fact]
        public void InjectFault_ThrowsOnCompletedExperiment()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("done", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.CompleteExperiment(exp.Id);
            Assert.Throws<InvalidOperationException>(() => engine.InjectFault(exp.Id));
        }

        [Fact]
        public void InjectFault_AllInjectionTypesHaveParams()
        {
            var engine = CreateEngine();
            foreach (ChaosInjectionType t in Enum.GetValues(typeof(ChaosInjectionType)))
            {
                var exp = engine.DesignExperiment($"test-{t}", t, new List<string> { "p1" });
                engine.RecordBaseline(exp.Id, MakeBaseline());
                var inj = engine.InjectFault(exp.Id);
                Assert.True(inj.Parameters.Count > 0, $"Injection type {t} should have parameters");
            }
        }

        // ── Observation ─────────────────────────────────

        [Fact]
        public void RecordObservation_AddsToList()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("obs", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.RecordObservation(exp.Id, MakeDegraded(700));
            Assert.Equal(2, engine.GetExperiment(exp.Id)!.Observations.Count);
        }

        [Fact]
        public void RecordObservation_MovesToObservationPhase()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("obs", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            Assert.Equal(ExperimentPhase.Observation, engine.GetExperiment(exp.Id)!.Phase);
        }

        [Fact]
        public void RecordObservation_ThrowsOnNull()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("obs", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Throws<ArgumentNullException>(() => engine.RecordObservation(exp.Id, null!));
        }

        // ── Blast Radius ────────────────────────────────

        [Fact]
        public void EstimateBlastRadius_IsolatedWhenNoDeps()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("br", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            var map = engine.EstimateBlastRadius(exp.Id);
            Assert.Equal(BlastRadiusLevel.Isolated, map.Level);
            Assert.Empty(map.AffectedPromptIds);
            Assert.Single(map.DirectTargets);
        }

        [Fact]
        public void EstimateBlastRadius_ContainedWithDirectDeps()
        {
            var engine = CreateEngine();
            engine.RegisterDependency("p2", "p1"); // p2 depends on p1
            var exp = engine.DesignExperiment("br", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            var map = engine.EstimateBlastRadius(exp.Id);
            Assert.Contains("p2", map.AffectedPromptIds);
            Assert.Equal(1, map.MaxDepth);
        }

        [Fact]
        public void EstimateBlastRadius_BfsTraversesChains()
        {
            var engine = CreateEngine();
            engine.RegisterDependency("p2", "p1");
            engine.RegisterDependency("p3", "p2");
            engine.RegisterDependency("p4", "p3");
            var exp = engine.DesignExperiment("chain", ChaosInjectionType.DependencyFailure, new List<string> { "p1" });
            var map = engine.EstimateBlastRadius(exp.Id);
            Assert.Equal(3, map.MaxDepth);
            Assert.Contains("p4", map.AffectedPromptIds);
        }

        [Fact]
        public void EstimateBlastRadius_ClassifiesSystemic()
        {
            var engine = CreateEngine(new ChaosConfig { SystemicThreshold = 0.8 });
            // Create a wide dependency graph: p1 is depended on by p2..p10
            for (int i = 2; i <= 10; i++)
                engine.RegisterDependency($"p{i}", "p1");
            var exp = engine.DesignExperiment("systemic", ChaosInjectionType.DependencyFailure, new List<string> { "p1" });
            var map = engine.EstimateBlastRadius(exp.Id);
            Assert.True(map.TotalAffected >= 10); // p1 + 9 dependents
            Assert.Equal(BlastRadiusLevel.Systemic, map.Level);
        }

        [Fact]
        public void RegisterDependency_ThrowsOnEmptyArgs()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.RegisterDependency("", "p2"));
            Assert.Throws<ArgumentException>(() => engine.RegisterDependency("p1", ""));
        }

        [Fact]
        public void GetDependencies_EmptyForUnknown()
        {
            var engine = CreateEngine();
            Assert.Empty(engine.GetDependencies("unknown"));
            Assert.Empty(engine.GetDependencies(""));
        }

        [Fact]
        public void GetDependencies_ReturnsDeps()
        {
            var engine = CreateEngine();
            engine.RegisterDependency("p1", "p2");
            engine.RegisterDependency("p1", "p3");
            var deps = engine.GetDependencies("p1");
            Assert.Equal(2, deps.Count);
            Assert.Contains("p2", deps);
        }

        // ── Resilience Scoring ──────────────────────────

        [Fact]
        public void ScoreResilience_ReturnsZeroWithoutBaseline()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("s", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Equal(0, engine.ScoreResilience(exp.Id));
        }

        [Fact]
        public void ScoreResilience_Returns100WithNoObservations()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("s", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            Assert.Equal(100, engine.ScoreResilience(exp.Id));
        }

        [Fact]
        public void ScoreResilience_DecreasesWithDegradation()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("deg", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            double score = engine.ScoreResilience(exp.Id);
            Assert.True(score < 100, $"Score {score} should be < 100 with degradation");
        }

        [Fact]
        public void ScoreResilience_HighWithMinimalDegradation()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("mild", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeBaseline(210, 0.93, 83, 510, 0.06)); // slight degradation
            double score = engine.ScoreResilience(exp.Id);
            Assert.True(score > 70, $"Score {score} should be > 70 for mild degradation");
        }

        [Fact]
        public void ScoreResilience_LowWithSevereDegradation()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("severe", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded(2000, 0.1, 10, 1000, 0.8));
            double score = engine.ScoreResilience(exp.Id);
            Assert.True(score < 30, $"Score {score} should be < 30 for severe degradation");
        }

        [Fact]
        public void ScoreResilience_ThrowsOnUnknownExperiment()
        {
            var engine = CreateEngine();
            Assert.Throws<KeyNotFoundException>(() => engine.ScoreResilience("nope"));
        }

        // ── Complete Experiment ──────────────────────────

        [Fact]
        public void CompleteExperiment_GeneratesReport()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("complete", ChaosInjectionType.TokenCorruption, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            var report = engine.CompleteExperiment(exp.Id);
            Assert.NotNull(report);
            Assert.True(report.CompositeScore >= 0 && report.CompositeScore <= 100);
            Assert.NotNull(report.BlastRadius);
            Assert.True(report.Recommendations.Count > 0);
        }

        [Fact]
        public void CompleteExperiment_SetsPhaseToCompleted()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("done", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.CompleteExperiment(exp.Id);
            Assert.Equal(ExperimentPhase.Completed, engine.GetExperiment(exp.Id)!.Phase);
        }

        [Fact]
        public void CompleteExperiment_ThrowsIfAlreadyCompleted()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("done", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.CompleteExperiment(exp.Id);
            Assert.Throws<InvalidOperationException>(() => engine.CompleteExperiment(exp.Id));
        }

        [Fact]
        public void CompleteExperiment_VerdictPassed_ForHighResilience()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("pass", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeBaseline(210, 0.93, 83, 510, 0.06));
            var report = engine.CompleteExperiment(exp.Id);
            Assert.Equal(ExperimentVerdict.Passed, report.Verdict);
        }

        [Fact]
        public void CompleteExperiment_VerdictDegraded_ForModerateImpact()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("deg", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded(500, 0.7, 60, 550, 0.2));
            var report = engine.CompleteExperiment(exp.Id);
            Assert.True(report.Verdict == ExperimentVerdict.Passed || report.Verdict == ExperimentVerdict.Degraded);
        }

        [Fact]
        public void CompleteExperiment_SetsVerdictOnExperiment()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("v", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.CompleteExperiment(exp.Id);
            Assert.NotNull(engine.GetExperiment(exp.Id)!.Verdict);
        }

        [Fact]
        public void CompleteExperiment_TierClassification()
        {
            var engine = CreateEngine();
            // High resilience
            var e1 = engine.DesignExperiment("t1", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(e1.Id, MakeBaseline());
            engine.InjectFault(e1.Id);
            engine.RecordObservation(e1.Id, MakeBaseline()); // no degradation
            var r1 = engine.CompleteExperiment(e1.Id);
            Assert.True(r1.Tier == ResilienceTier.Antifragile || r1.Tier == ResilienceTier.Resilient);

            // Low resilience
            var e2 = engine.DesignExperiment("t2", ChaosInjectionType.TokenCorruption, new List<string> { "p1" });
            engine.RecordBaseline(e2.Id, MakeBaseline());
            engine.InjectFault(e2.Id);
            engine.RecordObservation(e2.Id, MakeDegraded(2000, 0.1, 10, 1000, 0.8));
            var r2 = engine.CompleteExperiment(e2.Id);
            Assert.True(r2.Tier == ResilienceTier.Fragile || r2.Tier == ResilienceTier.Brittle);
        }

        // ── Recovery Verification ───────────────────────

        [Fact]
        public void VerifyRecovery_TrueWhenWithinTolerance()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("rec", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline(200, 0.95, 85, 500, 0.05));
            bool ok = engine.VerifyRecovery(exp.Id, MakeBaseline(210, 0.94, 84, 510, 0.06));
            Assert.True(ok);
        }

        [Fact]
        public void VerifyRecovery_FalseWhenBeyondTolerance()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("rec", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline(200, 0.95, 85, 500, 0.05));
            bool ok = engine.VerifyRecovery(exp.Id, MakeDegraded(800, 0.5, 40, 600, 0.4));
            Assert.False(ok);
        }

        [Fact]
        public void VerifyRecovery_TrueWhenNoBaseline()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("rec", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.True(engine.VerifyRecovery(exp.Id, MakeBaseline()));
        }

        [Fact]
        public void VerifyRecovery_ThrowsOnNullMetrics()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("rec", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Throws<ArgumentNullException>(() => engine.VerifyRecovery(exp.Id, null!));
        }

        [Fact]
        public void VerifyRecovery_SetsVerificationPhase()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("rec", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.VerifyRecovery(exp.Id, MakeBaseline());
            Assert.Equal(ExperimentPhase.Verification, engine.GetExperiment(exp.Id)!.Phase);
        }

        // ── Steady State Validation ─────────────────────

        [Fact]
        public void ValidateSteadyState_TrueWithinTolerance()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("ss", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            Assert.True(engine.ValidateSteadyState(exp.Id, MakeBaseline(210, 0.94, 84)));
        }

        [Fact]
        public void ValidateSteadyState_FalseOutsideTolerance()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("ss", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            Assert.False(engine.ValidateSteadyState(exp.Id, MakeDegraded()));
        }

        [Fact]
        public void ValidateSteadyState_TrueWithNoBaseline()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("ss", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.True(engine.ValidateSteadyState(exp.Id, MakeDegraded()));
        }

        [Fact]
        public void ValidateSteadyState_ThrowsOnNull()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("ss", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Throws<ArgumentNullException>(() => engine.ValidateSteadyState(exp.Id, null!));
        }

        [Fact]
        public void GetBaseline_ReturnsNullBeforeRecording()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("gb", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Null(engine.GetBaseline(exp.Id));
        }

        [Fact]
        public void GetBaseline_ReturnsAfterRecording()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("gb", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline(300));
            Assert.Equal(300, engine.GetBaseline(exp.Id)!.AvgLatencyMs);
        }

        // ── Insight Generation ──────────────────────────

        [Fact]
        public void GenerateInsights_EmptyWithNoExperiments()
        {
            var engine = CreateEngine();
            Assert.Empty(engine.GenerateInsights());
        }

        [Fact]
        public void GenerateInsights_DetectsCoverageGaps()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("cov", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.CompleteExperiment(exp.Id);
            var insights = engine.GenerateInsights();
            Assert.True(insights.Any(i => i.Type == ChaosInsightType.CoverageGap));
        }

        [Fact]
        public void GenerateInsights_DetectsWeakSpots()
        {
            var engine = CreateEngine();
            // Run 3 experiments of same type with bad results
            for (int i = 0; i < 3; i++)
            {
                var exp = engine.DesignExperiment($"weak-{i}", ChaosInjectionType.TokenCorruption, new List<string> { "p1" });
                engine.RecordBaseline(exp.Id, MakeBaseline());
                engine.InjectFault(exp.Id);
                engine.RecordObservation(exp.Id, MakeDegraded(2000, 0.1, 10, 1000, 0.8));
                engine.CompleteExperiment(exp.Id);
            }
            var insights = engine.GenerateInsights();
            Assert.True(insights.Any(i => i.Type == ChaosInsightType.WeakSpot));
        }

        [Fact]
        public void GenerateInsights_DetectsStrength()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("strong", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeBaseline()); // no degradation
            engine.CompleteExperiment(exp.Id);
            var insights = engine.GenerateInsights();
            Assert.True(insights.Any(i => i.Type == ChaosInsightType.StrengthFound));
        }

        [Fact]
        public void GenerateInsights_OrderedByPriority()
        {
            var engine = CreateEngine();
            // Mix of weak and strong
            var e1 = engine.DesignExperiment("w", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(e1.Id, MakeBaseline());
            engine.InjectFault(e1.Id);
            engine.RecordObservation(e1.Id, MakeDegraded(2000, 0.1, 10, 1000, 0.8));
            engine.CompleteExperiment(e1.Id);

            var e2 = engine.DesignExperiment("s", ChaosInjectionType.TokenCorruption, new List<string> { "p1" });
            engine.RecordBaseline(e2.Id, MakeBaseline());
            engine.InjectFault(e2.Id);
            engine.RecordObservation(e2.Id, MakeBaseline());
            engine.CompleteExperiment(e2.Id);

            var insights = engine.GenerateInsights();
            for (int i = 1; i < insights.Count; i++)
                Assert.True(insights[i].Priority >= insights[i - 1].Priority);
        }

        [Fact]
        public void GenerateInsights_DetectsCascadeRisk()
        {
            var engine = CreateEngine();
            // Create wide dependency: many prompts depend on p1
            for (int i = 2; i <= 10; i++)
                engine.RegisterDependency($"p{i}", "p1");
            var exp = engine.DesignExperiment("cascade", ChaosInjectionType.DependencyFailure, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.CompleteExperiment(exp.Id);
            var insights = engine.GenerateInsights();
            Assert.True(insights.Any(i => i.Type == ChaosInsightType.CascadeRisk));
        }

        // ── Fleet Resilience ────────────────────────────

        [Fact]
        public void FleetResilience_ZeroWithNoCompleted()
        {
            var engine = CreateEngine();
            Assert.Equal(0, engine.GetFleetResilienceScore());
        }

        [Fact]
        public void FleetResilience_AveragesCompletedExperiments()
        {
            var engine = CreateEngine();
            var e1 = engine.DesignExperiment("f1", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(e1.Id, MakeBaseline());
            engine.InjectFault(e1.Id);
            engine.RecordObservation(e1.Id, MakeBaseline()); // no degradation
            engine.CompleteExperiment(e1.Id);

            var e2 = engine.DesignExperiment("f2", ChaosInjectionType.TokenCorruption, new List<string> { "p1" });
            engine.RecordBaseline(e2.Id, MakeBaseline());
            engine.InjectFault(e2.Id);
            engine.RecordObservation(e2.Id, MakeDegraded(2000, 0.1, 10, 1000, 0.8));
            engine.CompleteExperiment(e2.Id);

            double fleet = engine.GetFleetResilienceScore();
            Assert.True(fleet > 0 && fleet < 100);
        }

        // ── LRU ─────────────────────────────────────────

        [Fact]
        public void Lru_EvictsOldestExperiments()
        {
            var engine = CreateEngine(new ChaosConfig { MaxExperiments = 3 });
            var e1 = engine.DesignExperiment("a", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.DesignExperiment("b", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.DesignExperiment("c", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            // Adding a 4th should evict the 1st
            engine.DesignExperiment("d", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Null(engine.GetExperiment(e1.Id));
            Assert.Equal(3, engine.GetExperiments().Count);
        }

        [Fact]
        public void Lru_RespectConfiguredCapacity()
        {
            var engine = CreateEngine(new ChaosConfig { MaxExperiments = 2 });
            engine.DesignExperiment("a", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.DesignExperiment("b", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.DesignExperiment("c", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            Assert.Equal(2, engine.GetExperiments().Count);
        }

        // ── Dashboard ───────────────────────────────────

        [Fact]
        public void RenderDashboard_ReturnsHtml()
        {
            var engine = CreateEngine();
            var html = engine.RenderDashboard();
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Prompt Chaos Engine", html);
        }

        [Fact]
        public void RenderDashboard_IncludesExperimentData()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("dash-test", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded());
            engine.CompleteExperiment(exp.Id);
            var html = engine.RenderDashboard();
            Assert.Contains("dash-test", html);
            Assert.Contains("LatencySpike", html);
        }

        [Fact]
        public void RenderDashboard_IncludesCoverageTab()
        {
            var engine = CreateEngine();
            var html = engine.RenderDashboard();
            Assert.Contains("Injection Type Coverage", html);
        }

        // ── Edge Cases ──────────────────────────────────

        [Fact]
        public void GetOrThrow_EmptyIdThrows()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.ScoreResilience(""));
        }

        [Fact]
        public void MultipleObservations_AreAveraged()
        {
            var engine = CreateEngine();
            var exp = engine.DesignExperiment("avg", ChaosInjectionType.LatencySpike, new List<string> { "p1" });
            engine.RecordBaseline(exp.Id, MakeBaseline());
            engine.InjectFault(exp.Id);
            engine.RecordObservation(exp.Id, MakeDegraded(400, 0.8, 70, 550, 0.15));
            engine.RecordObservation(exp.Id, MakeDegraded(600, 0.6, 50, 600, 0.3));
            double score = engine.ScoreResilience(exp.Id);
            // Should reflect aggregated observation
            Assert.True(score > 0 && score < 100);
        }

        [Fact]
        public void FullLifecycle_DesignThroughVerification()
        {
            var engine = CreateEngine();
            engine.RegisterDependency("p2", "p1");

            var exp = engine.DesignExperiment("lifecycle", ChaosInjectionType.LatencySpike, new List<string> { "p1" }, "Latency spike won't cascade");
            Assert.Equal(ExperimentPhase.Baseline, exp.Phase);

            engine.RecordBaseline(exp.Id, MakeBaseline());
            var inj = engine.InjectFault(exp.Id);
            Assert.Equal(ExperimentPhase.Injection, engine.GetExperiment(exp.Id)!.Phase);

            engine.RecordObservation(exp.Id, MakeDegraded(500, 0.7, 60, 550, 0.2));
            Assert.Equal(ExperimentPhase.Observation, engine.GetExperiment(exp.Id)!.Phase);

            var report = engine.CompleteExperiment(exp.Id);
            Assert.Equal(ExperimentPhase.Completed, engine.GetExperiment(exp.Id)!.Phase);
            Assert.NotNull(report.BlastRadius);
            Assert.Contains("p2", report.BlastRadius!.AffectedPromptIds);

            bool recovered = engine.VerifyRecovery(exp.Id, MakeBaseline(210, 0.94, 84, 510, 0.06));
            Assert.True(recovered);
        }
    }
}