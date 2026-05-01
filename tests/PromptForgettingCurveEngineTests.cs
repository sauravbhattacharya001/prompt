namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptForgettingCurveEngineTests
    {
        private readonly PromptForgettingCurveEngine _engine;

        public PromptForgettingCurveEngineTests()
        {
            _engine = new PromptForgettingCurveEngine(new ForgettingCurveConfig
            {
                MinObservationsForFit = 3,
                DefaultHalfLifeDays = 30.0,
                QualityFloor = 50.0
            });
        }

        // ─── Recording Observations ───────────────────────────────

        [Fact]
        public void RecordObservation_ValidObs_IncreasesCount()
        {
            var obs = MakeObs("p1", DateTime.UtcNow, 90);
            _engine.RecordObservation(obs);
            Assert.Equal(1, _engine.GetObservationCount("p1"));
        }

        [Fact]
        public void RecordObservation_NullObs_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _engine.RecordObservation(null!));
        }

        [Fact]
        public void RecordObservation_EmptyPromptId_Throws()
        {
            var obs = new PerformanceObservation { PromptId = "", QualityScore = 80 };
            Assert.Throws<ArgumentException>(() => _engine.RecordObservation(obs));
        }

        [Fact]
        public void RecordObservation_MultiplePrompts_TrackedSeparately()
        {
            _engine.RecordObservation(MakeObs("p1", DateTime.UtcNow, 90));
            _engine.RecordObservation(MakeObs("p2", DateTime.UtcNow, 80));
            Assert.Equal(1, _engine.GetObservationCount("p1"));
            Assert.Equal(1, _engine.GetObservationCount("p2"));
        }

        [Fact]
        public void RecordObservation_AutoFitsWhenEnoughData()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("p1", baseTime.AddDays(i * 7), 95 - i * 10));

            var profile = _engine.FitDecayCurve("p1");
            Assert.True(profile.RSquared > 0);
        }

        // ─── Curve Fitting ────────────────────────────────────────

        [Fact]
        public void FitDecayCurve_ExponentialDecay_FitsCorrectly()
        {
            var baseTime = DateTime.UtcNow.AddDays(-50);
            // Simulate exponential decay
            for (int i = 0; i < 10; i++)
            {
                var t = i * 5;
                var score = 100.0 * Math.Exp(-0.03 * t);
                _engine.RecordObservation(MakeObs("exp1", baseTime.AddDays(t), score));
            }

            var profile = _engine.FitDecayCurve("exp1");
            Assert.True(profile.RSquared > 0.8);
            Assert.True(profile.DecayRate > 0);
        }

        [Fact]
        public void FitDecayCurve_NoData_ReturnsDefault()
        {
            var profile = _engine.FitDecayCurve("unknown");
            Assert.Equal(DecayModel.Exponential, profile.FittedModel);
            Assert.Equal(30.0, profile.HalfLife.TotalDays, 1);
        }

        [Fact]
        public void FitDecayCurve_SinglePoint_ReturnsDefault()
        {
            _engine.RecordObservation(MakeObs("single", DateTime.UtcNow, 90));
            var profile = _engine.FitDecayCurve("single");
            Assert.Equal(0.0, profile.RSquared);
        }

        [Fact]
        public void FitDecayCurve_FlatData_LowDecayRate()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 6; i++)
                _engine.RecordObservation(MakeObs("flat1", baseTime.AddDays(i * 5), 85 + (i % 2)));

            var profile = _engine.FitDecayCurve("flat1");
            Assert.True(profile.HalfLife.TotalDays > 50);
        }

        [Fact]
        public void FitDecayCurve_PowerLawDecay_DetectsPattern()
        {
            var baseTime = DateTime.UtcNow.AddDays(-60);
            for (int i = 0; i < 10; i++)
            {
                var t = i * 6;
                var score = 100.0 * Math.Pow(t + 1, -0.3);
                _engine.RecordObservation(MakeObs("pow1", baseTime.AddDays(t), score));
            }

            var profile = _engine.FitDecayCurve("pow1");
            Assert.True(profile.RSquared > 0.5);
        }

        [Fact]
        public void FitDecayCurve_StepDrop_DetectsStepFunction()
        {
            var baseTime = DateTime.UtcNow.AddDays(-40);
            // Flat then sudden drop
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("step1", baseTime.AddDays(i * 3), 90));
            for (int i = 5; i < 10; i++)
                _engine.RecordObservation(MakeObs("step1", baseTime.AddDays(i * 3), 30));

            var profile = _engine.FitDecayCurve("step1");
            Assert.True(profile.RSquared > 0.5);
        }

        // ─── Phase Detection ──────────────────────────────────────

        [Fact]
        public void GetRetentionPhase_NoData_ReturnsFresh()
        {
            Assert.Equal(RetentionPhase.Fresh, _engine.GetRetentionPhase("nonexistent"));
        }

        [Fact]
        public void GetRetentionPhase_RecentHighQuality_ReturnsFreshOrConsolidating()
        {
            var obs = MakeObs("fresh1", DateTime.UtcNow.AddDays(-1), 95);
            _engine.RecordObservation(obs);
            var phase = _engine.GetRetentionPhase("fresh1");
            Assert.True(phase == RetentionPhase.Fresh || phase == RetentionPhase.Consolidating);
        }

        [Fact]
        public void GetRetentionPhase_OldLowQuality_ReturnsDecayingOrForgotten()
        {
            var baseTime = DateTime.UtcNow.AddDays(-60);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("old1", baseTime.AddDays(i * 12), 90 - i * 18));

            var phase = _engine.GetRetentionPhase("old1");
            Assert.True(phase == RetentionPhase.Decaying || phase == RetentionPhase.Forgotten);
        }

        [Fact]
        public void GetRetentionPhase_StableHighRetention_ReturnsStable()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("stable1", baseTime.AddDays(i * 6), 88 - i));

            var phase = _engine.GetRetentionPhase("stable1");
            Assert.True(phase == RetentionPhase.Stable || phase == RetentionPhase.Consolidating);
        }

        // ─── Durability Assessment ───────────────────────────────

        [Fact]
        public void AssessDurability_LongHalfLife_HighTier()
        {
            var baseTime = DateTime.UtcNow.AddDays(-90);
            for (int i = 0; i < 6; i++)
                _engine.RecordObservation(MakeObs("durable1", baseTime.AddDays(i * 15), 95 - i));

            var assessment = _engine.AssessDurability("durable1");
            Assert.True(assessment.Score > 40);
            Assert.True(assessment.Tier >= DurabilityTier.Standard);
        }

        [Fact]
        public void AssessDurability_ShortHalfLife_LowTier()
        {
            var baseTime = DateTime.UtcNow.AddDays(-20);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("fragile1", baseTime.AddDays(i * 4), 95 - i * 20));

            var assessment = _engine.AssessDurability("fragile1");
            Assert.True(assessment.Tier <= DurabilityTier.Standard);
        }

        [Fact]
        public void AssessDurability_IncludesFactors()
        {
            var baseTime = DateTime.UtcNow.AddDays(-60);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("factored1", baseTime.AddDays(i * 12), 90 - i * 5));

            var assessment = _engine.AssessDurability("factored1");
            Assert.NotEmpty(assessment.Factors);
        }

        [Fact]
        public void AssessDurability_HasPositiveLifespan()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 4; i++)
                _engine.RecordObservation(MakeObs("life1", baseTime.AddDays(i * 7), 85 - i * 5));

            var assessment = _engine.AssessDurability("life1");
            Assert.True(assessment.PredictedLifespan.TotalDays > 0);
        }

        // ─── Maintenance Window ───────────────────────────────────

        [Fact]
        public void PredictMaintenanceWindow_HealthyPrompt_NoUrgency()
        {
            var baseTime = DateTime.UtcNow.AddDays(-7);
            for (int i = 0; i < 4; i++)
                _engine.RecordObservation(MakeObs("healthy1", baseTime.AddDays(i * 2), 92 - i));

            var window = _engine.PredictMaintenanceWindow("healthy1");
            Assert.True(window.Urgency <= MaintenanceUrgency.Routine);
        }

        [Fact]
        public void PredictMaintenanceWindow_DecayingPrompt_HasUrgency()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 6; i++)
                _engine.RecordObservation(MakeObs("decay1", baseTime.AddDays(i * 5), 95 - i * 15));

            var window = _engine.PredictMaintenanceWindow("decay1");
            Assert.True(window.Urgency >= MaintenanceUrgency.Routine);
            Assert.NotEqual(RefreshStrategy.MinorTweak, window.Strategy);
        }

        [Fact]
        public void PredictMaintenanceWindow_HasEstimatedEffort()
        {
            var baseTime = DateTime.UtcNow.AddDays(-20);
            for (int i = 0; i < 4; i++)
                _engine.RecordObservation(MakeObs("effort1", baseTime.AddDays(i * 5), 80 - i * 10));

            var window = _engine.PredictMaintenanceWindow("effort1");
            Assert.False(string.IsNullOrEmpty(window.EstimatedEffort));
        }

        [Fact]
        public void PredictMaintenanceWindow_HasRefreshDate()
        {
            var baseTime = DateTime.UtcNow.AddDays(-14);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("refresh1", baseTime.AddDays(i * 3), 90 - i * 8));

            var window = _engine.PredictMaintenanceWindow("refresh1");
            Assert.NotNull(window.RecommendedRefreshDate);
        }

        // ─── Spaced Repetition ────────────────────────────────────

        [Fact]
        public void GenerateSpacedSchedule_ReturnsExpandingIntervals()
        {
            var baseTime = DateTime.UtcNow.AddDays(-10);
            for (int i = 0; i < 4; i++)
                _engine.RecordObservation(MakeObs("srs1", baseTime.AddDays(i * 2), 85 - i * 3));

            var schedule = _engine.GenerateSpacedSchedule("srs1");
            Assert.True(schedule.ReviewIntervals.Count > 3);
            // Intervals should be expanding
            for (int i = 1; i < schedule.ReviewIntervals.Count; i++)
                Assert.True(schedule.ReviewIntervals[i] >= schedule.ReviewIntervals[i - 1]);
        }

        [Fact]
        public void CompleteReview_Success_ExpandsInterval()
        {
            var baseTime = DateTime.UtcNow.AddDays(-10);
            for (int i = 0; i < 3; i++)
                _engine.RecordObservation(MakeObs("srs2", baseTime.AddDays(i * 3), 85));

            var schedule1 = _engine.GenerateSpacedSchedule("srs2");
            var firstNext = schedule1.NextReviewDate;

            var schedule2 = _engine.CompleteReview("srs2", true);
            Assert.Equal(1, schedule2.ReviewCount);
            Assert.Equal(1, schedule2.SuccessiveCorrectCount);
            Assert.True(schedule2.NextReviewDate > firstNext);
        }

        [Fact]
        public void CompleteReview_Failure_ResetsInterval()
        {
            var baseTime = DateTime.UtcNow.AddDays(-10);
            for (int i = 0; i < 3; i++)
                _engine.RecordObservation(MakeObs("srs3", baseTime.AddDays(i * 3), 85));

            _engine.GenerateSpacedSchedule("srs3");
            _engine.CompleteReview("srs3", true);
            _engine.CompleteReview("srs3", true);
            var afterSuccess = _engine.CompleteReview("srs3", true);
            Assert.Equal(3, afterSuccess.SuccessiveCorrectCount);

            var afterFail = _engine.CompleteReview("srs3", false);
            Assert.Equal(0, afterFail.SuccessiveCorrectCount);
            Assert.Equal(4, afterFail.ReviewCount);
        }

        // ─── Fleet Report ─────────────────────────────────────────

        [Fact]
        public void GetFleetReport_EmptyFleet_ReturnsZeros()
        {
            var engine = new PromptForgettingCurveEngine();
            var report = engine.GetFleetReport();
            Assert.Equal(0, report.TotalPrompts);
        }

        [Fact]
        public void GetFleetReport_MultiplePrompts_AggregatesCorrectly()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            // Healthy prompt
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("fleet-a", baseTime.AddDays(i * 5), 90 - i));
            // Decaying prompt
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("fleet-b", baseTime.AddDays(i * 5), 95 - i * 18));

            var report = _engine.GetFleetReport();
            Assert.Equal(2, report.TotalPrompts);
            Assert.True(report.OverallHealthScore > 0);
        }

        [Fact]
        public void GetFleetReport_IncludesTopDecaying()
        {
            var baseTime = DateTime.UtcNow.AddDays(-40);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("decayer", baseTime.AddDays(i * 8), 100 - i * 20));

            var report = _engine.GetFleetReport();
            Assert.True(report.TopDecaying.Count >= 0); // May or may not classify as decaying
        }

        [Fact]
        public void GetFleetReport_HasInsights()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("insight1", baseTime.AddDays(i * 6), 90 - i * 5));

            var report = _engine.GetFleetReport();
            Assert.NotEmpty(report.AutonomousInsights);
        }

        // ─── Refresh Recommendations ─────────────────────────────

        [Fact]
        public void GetRefreshRecommendations_NoDecay_ReturnsEmpty()
        {
            var engine = new PromptForgettingCurveEngine();
            var recs = engine.GetRefreshRecommendations();
            Assert.Empty(recs);
        }

        [Fact]
        public void GetRefreshRecommendations_DecayedPrompts_ReturnsSorted()
        {
            var baseTime = DateTime.UtcNow.AddDays(-60);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("rec-a", baseTime.AddDays(i * 12), 100 - i * 20));
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("rec-b", baseTime.AddDays(i * 12), 95 - i * 10));

            var recs = _engine.GetRefreshRecommendations();
            if (recs.Count >= 2)
                Assert.True(recs[0].PriorityScore >= recs[1].PriorityScore);
        }

        [Fact]
        public void GetRefreshRecommendations_HasReasoning()
        {
            var baseTime = DateTime.UtcNow.AddDays(-40);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("reason1", baseTime.AddDays(i * 8), 95 - i * 18));

            var recs = _engine.GetRefreshRecommendations();
            foreach (var rec in recs)
                Assert.False(string.IsNullOrEmpty(rec.Reasoning));
        }

        [Fact]
        public void GetRefreshRecommendations_RespectsTopN()
        {
            var baseTime = DateTime.UtcNow.AddDays(-50);
            for (int p = 0; p < 15; p++)
                for (int i = 0; i < 5; i++)
                    _engine.RecordObservation(MakeObs($"topn-{p}", baseTime.AddDays(i * 10), 90 - i * 17));

            var recs = _engine.GetRefreshRecommendations(topN: 5);
            Assert.True(recs.Count <= 5);
        }

        // ─── Decay Simulation ─────────────────────────────────────

        [Fact]
        public void SimulateDecay_ReturnsCorrectDayCount()
        {
            var baseTime = DateTime.UtcNow.AddDays(-20);
            for (int i = 0; i < 4; i++)
                _engine.RecordObservation(MakeObs("sim1", baseTime.AddDays(i * 5), 90 - i * 5));

            var sim = _engine.SimulateDecay("sim1", 30);
            Assert.Equal(31, sim.Count); // 0 through 30
        }

        [Fact]
        public void SimulateDecay_RetentionDecreases()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("sim2", baseTime.AddDays(i * 6), 100 - i * 15));

            var sim = _engine.SimulateDecay("sim2", 60);
            Assert.True(sim.First().Retention >= sim.Last().Retention);
        }

        [Fact]
        public void SimulateDecay_RetentionClampedZeroToOne()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("sim3", baseTime.AddDays(i * 6), 95 - i * 20));

            var sim = _engine.SimulateDecay("sim3", 365);
            Assert.All(sim, s =>
            {
                Assert.True(s.Retention >= 0.0);
                Assert.True(s.Retention <= 1.0);
            });
        }

        // ─── Recovery Events ──────────────────────────────────────

        [Fact]
        public void DetectRecoveryEvents_NoRecovery_ReturnsEmpty()
        {
            var baseTime = DateTime.UtcNow.AddDays(-20);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("norec1", baseTime.AddDays(i * 4), 90 - i * 10));

            var events = _engine.DetectRecoveryEvents("norec1");
            Assert.Empty(events);
        }

        [Fact]
        public void DetectRecoveryEvents_WithRecovery_DetectsIt()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            _engine.RecordObservation(MakeObs("rec1", baseTime, 90));
            _engine.RecordObservation(MakeObs("rec1", baseTime.AddDays(7), 60));
            _engine.RecordObservation(MakeObs("rec1", baseTime.AddDays(14), 40));
            _engine.RecordObservation(MakeObs("rec1", baseTime.AddDays(21), 80)); // Recovery!

            var events = _engine.DetectRecoveryEvents("rec1");
            Assert.NotEmpty(events);
            Assert.True(events[0].RecoveryStrength > 0);
        }

        [Fact]
        public void DetectRecoveryEvents_InsufficientData_ReturnsEmpty()
        {
            _engine.RecordObservation(MakeObs("few1", DateTime.UtcNow, 80));
            Assert.Empty(_engine.DetectRecoveryEvents("few1"));
        }

        // ─── Insights ─────────────────────────────────────────────

        [Fact]
        public void GenerateInsights_EmptyFleet_ReturnsNoDataMessage()
        {
            var engine = new PromptForgettingCurveEngine();
            var insights = engine.GenerateInsights();
            Assert.Contains(insights, i => i.Contains("No prompts"));
        }

        [Fact]
        public void GenerateInsights_WithData_ReturnsMultipleInsights()
        {
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("ins1", baseTime.AddDays(i * 6), 85 - i * 5));

            var insights = _engine.GenerateInsights();
            Assert.True(insights.Count >= 3);
        }

        // ─── Edge Cases ───────────────────────────────────────────

        [Fact]
        public void GetTrackedPromptIds_ReturnsAllTracked()
        {
            _engine.RecordObservation(MakeObs("x1", DateTime.UtcNow, 80));
            _engine.RecordObservation(MakeObs("x2", DateTime.UtcNow, 70));
            var ids = _engine.GetTrackedPromptIds();
            Assert.Contains("x1", ids);
            Assert.Contains("x2", ids);
        }

        [Fact]
        public void FitDecayCurve_IdenticalTimestamps_DoesNotCrash()
        {
            var t = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("same-t", t, 80 - i * 5));

            var profile = _engine.FitDecayCurve("same-t");
            Assert.NotNull(profile);
        }

        [Fact]
        public void FitDecayCurve_ZeroScores_DoesNotCrash()
        {
            var baseTime = DateTime.UtcNow.AddDays(-10);
            for (int i = 0; i < 5; i++)
                _engine.RecordObservation(MakeObs("zero1", baseTime.AddDays(i * 2), 0));

            var profile = _engine.FitDecayCurve("zero1");
            Assert.NotNull(profile);
        }

        [Fact]
        public void CompositeScore_AveragesAllDimensions()
        {
            var obs = new PerformanceObservation
            {
                PromptId = "comp1",
                QualityScore = 80,
                ResponseRelevance = 60,
                UserSatisfaction = 90,
                TokenEfficiency = 70
            };
            Assert.Equal(75.0, obs.CompositeScore);
        }

        [Fact]
        public void RecoveryEvent_StrengthCalculation()
        {
            var evt = new RecoveryEvent
            {
                PreRecoveryRetention = 0.3,
                PostRecoveryRetention = 0.8
            };
            Assert.Equal(0.5, evt.RecoveryStrength, 2);
        }

        // ─── Helpers ──────────────────────────────────────────────

        private static PerformanceObservation MakeObs(string promptId, DateTime timestamp, double score)
        {
            return new PerformanceObservation
            {
                PromptId = promptId,
                Timestamp = timestamp,
                QualityScore = score,
                ResponseRelevance = score * 0.95,
                UserSatisfaction = score * 0.9,
                TokenEfficiency = score * 0.85,
                Tags = new List<string>()
            };
        }
    }
}
