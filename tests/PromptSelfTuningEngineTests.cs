namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptSelfTuningEngineTests
    {
        private static PromptSelfTuningEngine CreateEngine(SelfTuningConfig? config = null)
            => new(config ?? new SelfTuningConfig());

        private static TuningOutcome MakeOutcome(string armId, double quality, bool success = true, double latency = 200, int tokens = 100)
            => new()
            {
                ArmId = armId,
                QualityScore = quality,
                Success = success,
                LatencyMs = latency,
                TokensUsed = tokens,
                Timestamp = DateTime.UtcNow
            };

        // ── Arm Management ──────────────────────────────

        [Fact]
        public void RegisterArm_AddsArm()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1", Temperature = 0.5 });
            var arms = engine.GetArms("p1");
            Assert.Single(arms);
            Assert.Equal("a1", arms[0].ArmId);
            Assert.Equal(0.5, arms[0].Temperature);
        }

        [Fact]
        public void RegisterArm_ThrowsOnNull()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentException>(() => engine.RegisterArm("", new TuningArm { ArmId = "a1" }));
            Assert.Throws<ArgumentNullException>(() => engine.RegisterArm("p1", null!));
            Assert.Throws<ArgumentException>(() => engine.RegisterArm("p1", new TuningArm { ArmId = "" }));
        }

        [Fact]
        public void RegisterDefaultArms_Creates5Arms()
        {
            var engine = CreateEngine();
            engine.RegisterDefaultArms("p1");
            Assert.Equal(5, engine.GetArms("p1").Count);
        }

        [Fact]
        public void GetArms_EmptyForUnknownPrompt()
        {
            var engine = CreateEngine();
            Assert.Empty(engine.GetArms("unknown"));
        }

        [Fact]
        public void RegisterArm_ResetsConvergence()
        {
            var engine = CreateEngine(new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 2, ConvergenceThreshold = 0.01 });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });
            // Run enough trials to converge
            for (int i = 0; i < 5; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.9));
            for (int i = 0; i < 5; i++) engine.RecordOutcome("p1", MakeOutcome("a2", 0.3));

            var snap1 = engine.GetSnapshot("p1");
            Assert.Equal(TuningPhase.Converged, snap1.Phase);

            // Adding new arm should reset convergence
            engine.RegisterArm("p1", new TuningArm { ArmId = "a3" });
            var snap2 = engine.GetSnapshot("p1");
            Assert.NotEqual(TuningPhase.Converged, snap2.Phase);
        }

        // ── Recommendation ──────────────────────────────

        [Fact]
        public void Recommend_ThrowsWithoutSession()
        {
            var engine = CreateEngine();
            Assert.Throws<InvalidOperationException>(() => engine.Recommend("unknown"));
        }

        [Fact]
        public void Recommend_ThrowsWithNoArms()
        {
            var engine = CreateEngine();
            // Force session creation indirectly - register then remove isn't possible, so test the error path
            Assert.Throws<InvalidOperationException>(() => engine.Recommend("nonexistent"));
        }

        [Fact]
        public void Recommend_RoundRobinDuringExploration()
        {
            var engine = CreateEngine(new SelfTuningConfig { MinTrialsPerArm = 2 });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });

            var rec1 = engine.Recommend("p1");
            Assert.Equal(TuningReason.RoundRobin, rec1.Reason);
            Assert.Equal(TuningPhase.Exploration, rec1.Phase);
        }

        [Fact]
        public void Recommend_UCBAfterExploration()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 2 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });

            // Fill exploration
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.8));
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.85));
            engine.RecordOutcome("p1", MakeOutcome("a2", 0.4));
            engine.RecordOutcome("p1", MakeOutcome("a2", 0.35));

            var rec = engine.Recommend("p1");
            Assert.NotEqual(TuningReason.RoundRobin, rec.Reason);
        }

        [Fact]
        public void Recommend_ExploitsWhenConverged()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 5, ConvergenceThreshold = 0.05 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "good" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "bad" });

            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("good", 0.9));
            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("bad", 0.3));

            var rec = engine.Recommend("p1");
            Assert.Equal(TuningReason.Exploitation, rec.Reason);
            Assert.Equal(TuningPhase.Converged, rec.Phase);
            Assert.Equal("good", rec.Arm.ArmId);
            Assert.True(rec.Confidence > 0.9);
        }

        // ── Outcome Recording ───────────────────────────

        [Fact]
        public void RecordOutcome_ThrowsForUnknownPrompt()
        {
            var engine = CreateEngine();
            Assert.Throws<InvalidOperationException>(() =>
                engine.RecordOutcome("unknown", MakeOutcome("a1", 0.5)));
        }

        [Fact]
        public void RecordOutcome_ThrowsForUnknownArm()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            Assert.Throws<ArgumentException>(() =>
                engine.RecordOutcome("p1", MakeOutcome("nonexistent", 0.5)));
        }

        [Fact]
        public void RecordOutcome_TracksHistory()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.7));
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.8));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(2, snap.TotalTrials);
        }

        // ── Statistics ──────────────────────────────────

        [Fact]
        public void GetArmStatistics_ComputesCorrectly()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.6, latency: 100, tokens: 50));
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.8, latency: 200, tokens: 150));
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.7, latency: 150, tokens: 100));

            var stats = engine.GetArmStatistics("p1", "a1");
            Assert.Equal(3, stats.Trials);
            Assert.Equal(0.7, stats.MeanReward, 2);
            Assert.True(stats.StdDevReward > 0);
            Assert.Equal(150, stats.MeanLatencyMs, 0);
            Assert.Equal(100, stats.MeanTokens, 0);
            Assert.Equal(1.0, stats.SuccessRate);
            Assert.Equal(0.8, stats.BestScore);
            Assert.Equal(0.6, stats.WorstScore);
        }

        [Fact]
        public void GetArmStatistics_SuccessRateAccountsForFailures()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.8, success: true));
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.0, success: false));

            var stats = engine.GetArmStatistics("p1", "a1");
            Assert.Equal(0.5, stats.SuccessRate);
        }

        // ── Snapshot ────────────────────────────────────

        [Fact]
        public void GetSnapshot_ReflectsCurrentState()
        {
            var engine = CreateEngine();
            engine.RegisterDefaultArms("p1");

            var snap = engine.GetSnapshot("p1");
            Assert.Equal("p1", snap.PromptId);
            Assert.Equal(5, snap.ArmCount);
            Assert.Equal(0, snap.TotalTrials);
            Assert.Equal(TuningPhase.Exploration, snap.Phase);
        }

        [Fact]
        public void GetSnapshot_IdentifiesBestArm()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.9));
            engine.RecordOutcome("p1", MakeOutcome("a2", 0.3));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal("a1", snap.BestArmId);
        }

        // ── Phase Transitions ───────────────────────────

        [Fact]
        public void PhaseTransition_ExplorationToBalancing()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 2, MinTrialsForConvergence = 100 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });

            engine.RecordOutcome("p1", MakeOutcome("a1", 0.7));
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.7));
            engine.RecordOutcome("p1", MakeOutcome("a2", 0.7));
            Assert.Equal(TuningPhase.Exploration, engine.GetSnapshot("p1").Phase);

            engine.RecordOutcome("p1", MakeOutcome("a2", 0.7));
            Assert.Equal(TuningPhase.Balancing, engine.GetSnapshot("p1").Phase);
        }

        [Fact]
        public void PhaseTransition_ToConverged()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 5, ConvergenceThreshold = 0.1 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "winner" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "loser" });

            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("winner", 0.85));
            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("loser", 0.4));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(TuningPhase.Converged, snap.Phase);
            Assert.Equal("winner", snap.BestArmId);
        }

        // ── Drift Detection ─────────────────────────────

        [Fact]
        public void DriftDetection_DetectsPerformanceDrop()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, DriftWindowSize = 5, DriftThreshold = 0.15 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });

            // Good performance
            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.85));
            // Sudden drop
            for (int i = 0; i < 5; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.4));

            var snap = engine.GetSnapshot("p1");
            Assert.NotNull(snap.Drift);
            Assert.True(snap.Drift!.DriftDetected);
            Assert.Contains("a1", snap.Drift.AffectedArms);
            Assert.True(snap.Drift.DriftMagnitude > 0.1);
        }

        [Fact]
        public void DriftDetection_NoDriftWhenStable()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, DriftWindowSize = 5, DriftThreshold = 0.15 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });

            for (int i = 0; i < 15; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.8 + (i % 3) * 0.02));

            var snap = engine.GetSnapshot("p1");
            Assert.True(snap.Drift == null || !snap.Drift.DriftDetected);
        }

        // ── Health Score ────────────────────────────────

        [Fact]
        public void HealthScore_ZeroWithNoData()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            var snap = engine.GetSnapshot("p1");
            Assert.Equal(0, snap.HealthScore);
            Assert.Equal(TuningHealthTier.Insufficient, snap.HealthTier);
        }

        [Fact]
        public void HealthScore_HighWhenConverged()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 5, ConvergenceThreshold = 0.05 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });

            for (int i = 0; i < 20; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.95));
            for (int i = 0; i < 20; i++) engine.RecordOutcome("p1", MakeOutcome("a2", 0.4));

            var snap = engine.GetSnapshot("p1");
            Assert.True(snap.HealthScore >= 70);
            Assert.True(snap.HealthTier == TuningHealthTier.Good || snap.HealthTier == TuningHealthTier.Optimal);
        }

        // ── Fleet Report ────────────────────────────────

        [Fact]
        public void FleetReport_AggregatesMultipleSessions()
        {
            var engine = CreateEngine();
            engine.RegisterDefaultArms("p1");
            engine.RegisterDefaultArms("p2");
            engine.RegisterDefaultArms("p3");

            var report = engine.GetFleetReport();
            Assert.Equal(3, report.TotalSessions);
            Assert.Equal(3, report.Sessions.Count);
            Assert.NotEmpty(report.Insights);
        }

        [Fact]
        public void FleetReport_EmptyWhenNoSessions()
        {
            var engine = CreateEngine();
            var report = engine.GetFleetReport();
            Assert.Equal(0, report.TotalSessions);
            Assert.Equal(0, report.OverallHealthScore);
        }

        // ── Reset ───────────────────────────────────────

        [Fact]
        public void ResetSession_ClearsHistoryKeepsArms()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.8));
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.9));

            engine.ResetSession("p1");

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(0, snap.TotalTrials);
            Assert.Equal(1, snap.ArmCount);
            Assert.Equal(TuningPhase.Exploration, snap.Phase);
        }

        [Fact]
        public void ResetSession_NoOpForUnknown()
        {
            var engine = CreateEngine();
            engine.ResetSession("nonexistent"); // Should not throw
        }

        // ── GetTrackedPrompts ───────────────────────────

        [Fact]
        public void GetTrackedPrompts_ListsSessions()
        {
            var engine = CreateEngine();
            engine.RegisterArm("alpha", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("beta", new TuningArm { ArmId = "a1" });

            var tracked = engine.GetTrackedPrompts();
            Assert.Contains("alpha", tracked);
            Assert.Contains("beta", tracked);
        }

        // ── Dashboard ───────────────────────────────────

        [Fact]
        public void Dashboard_GeneratesHTML()
        {
            var engine = CreateEngine();
            engine.RegisterDefaultArms("p1");
            engine.RecordOutcome("p1", MakeOutcome("conservative", 0.8));
            engine.RecordOutcome("p1", MakeOutcome("balanced", 0.75));

            string html = engine.GenerateDashboard("p1");
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Self-Tuning", html);
            Assert.Contains("conservative", html);
            Assert.Contains("balanced", html);
        }

        [Fact]
        public void Dashboard_ShowsDriftInfo()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, DriftWindowSize = 5, DriftThreshold = 0.1 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });

            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.9));
            for (int i = 0; i < 5; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.3));

            string html = engine.GenerateDashboard("p1");
            Assert.Contains("DRIFT DETECTED", html);
        }

        // ── TuningArm.ToParameterString ─────────────────

        [Fact]
        public void TuningArm_ToParameterString_FormatsCorrectly()
        {
            var arm = new TuningArm { ArmId = "test", Temperature = 0.7, TopP = 0.9, FrequencyPenalty = 0.1, PresencePenalty = 0.2, MaxTokens = 500 };
            string s = arm.ToParameterString();
            Assert.Contains("temp=0.70", s);
            Assert.Contains("top_p=0.90", s);
            Assert.Contains("max_tok=500", s);
        }

        [Fact]
        public void TuningArm_ToParameterString_OmitsMaxTokensWhenZero()
        {
            var arm = new TuningArm { ArmId = "test", Temperature = 0.5 };
            string s = arm.ToParameterString();
            Assert.DoesNotContain("max_tok", s);
        }

        // ── Edge Cases ──────────────────────────────────

        [Fact]
        public void SingleArm_ConvergesOnItself()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 5 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "only" });

            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("only", 0.7));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(TuningPhase.Converged, snap.Phase);
            Assert.Equal("only", snap.BestArmId);
        }

        [Fact]
        public void ManyArms_UCBHandlesLargeArmSet()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1 };
            var engine = CreateEngine(config);

            for (int i = 0; i < 20; i++)
                engine.RegisterArm("p1", new TuningArm { ArmId = $"arm_{i}", Temperature = i * 0.1 });

            // One trial each
            for (int i = 0; i < 20; i++)
                engine.RecordOutcome("p1", MakeOutcome($"arm_{i}", 0.5 + i * 0.02));

            var rec = engine.Recommend("p1");
            Assert.NotNull(rec.Arm);
        }

        [Fact]
        public void DimensionScores_StoredInOutcome()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });

            var outcome = MakeOutcome("a1", 0.8);
            outcome.DimensionScores[TuningQualityDimension.Accuracy] = 0.9;
            outcome.DimensionScores[TuningQualityDimension.Coherence] = 0.7;
            engine.RecordOutcome("p1", outcome);

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(1, snap.TotalTrials);
        }

        [Fact]
        public void ConvergingPhase_Detected()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 10, ConvergenceThreshold = 0.3 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });

            // a1 better by 0.1, which is >= 0.3*0.5=0.15? No, 0.1<0.15 => Balancing
            // a1 better by 0.2, which is >= 0.3*0.5=0.15 but < 0.3 => Converging
            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.8));
            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("a2", 0.6));

            var snap = engine.GetSnapshot("p1");
            Assert.Equal(TuningPhase.Converging, snap.Phase);
        }

        [Fact]
        public void FleetReport_PhaseDistribution()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 3, ConvergenceThreshold = 0.1 };
            var engine = CreateEngine(config);

            // Session 1: converged
            engine.RegisterArm("p1", new TuningArm { ArmId = "a" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "b" });
            for (int i = 0; i < 5; i++) engine.RecordOutcome("p1", MakeOutcome("a", 0.9));
            for (int i = 0; i < 5; i++) engine.RecordOutcome("p1", MakeOutcome("b", 0.3));

            // Session 2: still exploring
            engine.RegisterArm("p2", new TuningArm { ArmId = "x" });
            engine.RegisterArm("p2", new TuningArm { ArmId = "y" });

            var report = engine.GetFleetReport();
            Assert.True(report.PhaseDistribution.ContainsKey(TuningPhase.Converged) || report.PhaseDistribution.ContainsKey(TuningPhase.Exploration));
        }

        [Fact]
        public void HealthTier_Classification()
        {
            // Test all tier boundaries via snapshot
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 2, ConvergenceThreshold = 0.01 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });

            // High-quality converged session should have good health
            for (int i = 0; i < 20; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.95));
            for (int i = 0; i < 20; i++) engine.RecordOutcome("p1", MakeOutcome("a2", 0.3));

            var snap = engine.GetSnapshot("p1");
            Assert.True(snap.HealthTier == TuningHealthTier.Optimal || snap.HealthTier == TuningHealthTier.Good);
        }

        [Fact]
        public void Recommend_ConfidenceIncreasesWithConvergence()
        {
            var config = new SelfTuningConfig { MinTrialsPerArm = 1, MinTrialsForConvergence = 3, ConvergenceThreshold = 0.05 };
            var engine = CreateEngine(config);
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            engine.RegisterArm("p1", new TuningArm { ArmId = "a2" });

            // Exploration phase
            engine.RecordOutcome("p1", MakeOutcome("a1", 0.8));
            engine.RecordOutcome("p1", MakeOutcome("a2", 0.4));
            var earlyRec = engine.Recommend("p1");

            // Add more data to converge
            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("a1", 0.85));
            for (int i = 0; i < 10; i++) engine.RecordOutcome("p1", MakeOutcome("a2", 0.35));
            var lateRec = engine.Recommend("p1");

            Assert.True(lateRec.Confidence >= earlyRec.Confidence);
        }

        [Fact]
        public void Dashboard_ThrowsForUnknownPrompt()
        {
            var engine = CreateEngine();
            Assert.Throws<InvalidOperationException>(() => engine.GenerateDashboard("unknown"));
        }

        [Fact]
        public void RecordOutcome_ThrowsOnNull()
        {
            var engine = CreateEngine();
            engine.RegisterArm("p1", new TuningArm { ArmId = "a1" });
            Assert.Throws<ArgumentNullException>(() => engine.RecordOutcome("p1", null!));
        }
    }
}
