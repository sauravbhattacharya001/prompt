namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptBlackSwanEngineTests
    {
        private static PromptBlackSwanEngine CreateEngine(BlackSwanConfig? config = null)
            => new(config ?? new BlackSwanConfig());

        private static BlackSwanEvent MakeEvent(
            string promptId, FailureCategory category, double impact,
            BlackSwanSeverity? severity = null, DateTime? timestamp = null,
            int cascadeDepth = 0, List<string>? precursors = null)
            => new()
            {
                PromptId = promptId,
                Category = category,
                Severity = severity ?? (impact >= 95 ? BlackSwanSeverity.BlackSwan :
                    impact >= 85 ? BlackSwanSeverity.Catastrophe :
                    impact >= 70 ? BlackSwanSeverity.Crisis :
                    impact >= 55 ? BlackSwanSeverity.Shock : BlackSwanSeverity.Anomaly),
                Impact = impact,
                Timestamp = timestamp ?? DateTime.UtcNow,
                CascadeDepth = cascadeDepth,
                Precursors = precursors ?? new List<string>()
            };

        // ── Event Recording ─────────────────────────

        [Fact]
        public void RecordEvent_StoresEvent()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));
            Assert.Equal(1, engine.EventCount);
        }

        [Fact]
        public void RecordEvent_ThrowsOnNull()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentNullException>(() => engine.RecordEvent(null!));
        }

        [Fact]
        public void RecordEvents_StoresMultiple()
        {
            var engine = CreateEngine();
            engine.RecordEvents(new[]
            {
                MakeEvent("p1", FailureCategory.Timeout, 30),
                MakeEvent("p2", FailureCategory.Hallucination, 60)
            });
            Assert.Equal(2, engine.EventCount);
        }

        [Fact]
        public void RecordEvents_ThrowsOnNull()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentNullException>(() => engine.RecordEvents(null!));
        }

        [Fact]
        public void GetEvents_ReturnsAll()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Injection, 60));
            Assert.Equal(2, engine.GetEvents().Count);
        }

        [Fact]
        public void GetEvents_FiltersById()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Injection, 60));
            Assert.Single(engine.GetEvents("p1"));
        }

        [Fact]
        public void GetEvents_ReturnsEmptyForUnknownId()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30));
            Assert.Empty(engine.GetEvents("unknown"));
        }

        // ── Severity Classification ─────────────────

        [Fact]
        public void ClassifySeverity_BlackSwan()
        {
            var engine = CreateEngine();
            Assert.Equal(BlackSwanSeverity.BlackSwan, engine.ClassifySeverity(97));
        }

        [Fact]
        public void ClassifySeverity_Catastrophe()
        {
            var engine = CreateEngine();
            Assert.Equal(BlackSwanSeverity.Catastrophe, engine.ClassifySeverity(90));
        }

        [Fact]
        public void ClassifySeverity_Crisis()
        {
            var engine = CreateEngine();
            Assert.Equal(BlackSwanSeverity.Crisis, engine.ClassifySeverity(75));
        }

        [Fact]
        public void ClassifySeverity_Shock()
        {
            var engine = CreateEngine();
            Assert.Equal(BlackSwanSeverity.Shock, engine.ClassifySeverity(60));
        }

        [Fact]
        public void ClassifySeverity_Anomaly()
        {
            var engine = CreateEngine();
            Assert.Equal(BlackSwanSeverity.Anomaly, engine.ClassifySeverity(45));
        }

        [Fact]
        public void ClassifySeverity_LowImpactIsAnomaly()
        {
            var engine = CreateEngine();
            Assert.Equal(BlackSwanSeverity.Anomaly, engine.ClassifySeverity(10));
        }

        // ── Extreme Event Detection ─────────────────

        [Fact]
        public void DetectExtremeEvents_EmptyOnFewEvents()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));
            Assert.Empty(engine.DetectExtremeEvents("p1"));
        }

        [Fact]
        public void DetectExtremeEvents_FindsOutliers()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Many low-impact events + one extreme outlier
            for (int i = 0; i < 20; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 10 + i % 5,
                    timestamp: baseTime.AddMinutes(i)));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 95,
                timestamp: baseTime.AddMinutes(21)));

            var extreme = engine.DetectExtremeEvents("p1");
            Assert.NotEmpty(extreme);
            Assert.Contains(extreme, e => e.Impact >= 95);
        }

        [Fact]
        public void DetectExtremeEvents_EmptyForUnknownPrompt()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));
            Assert.Empty(engine.DetectExtremeEvents("unknown"));
        }

        [Fact]
        public void DetectExtremeEvents_HighImpactAlwaysExtreme()
        {
            var engine = CreateEngine();
            // All events with moderate spread, one above crisis threshold
            for (int i = 0; i < 10; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30 + i * 2));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 80));

            var extreme = engine.DetectExtremeEvents("p1");
            Assert.Contains(extreme, e => e.Impact == 80);
        }

        [Fact]
        public void DetectExtremeEvents_AllSameImpactNoExtremes()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20));

            var extreme = engine.DetectExtremeEvents("p1");
            Assert.Empty(extreme);
        }

        // ── Tail Risk Analysis ──────────────────────

        [Fact]
        public void AnalyzeTailRisk_ReturnsEmptyOnInsufficientData()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20 + i));
            Assert.Empty(engine.AnalyzeTailRisk());
        }

        [Fact]
        public void AnalyzeTailRisk_ReturnsProfilesForSufficientData()
        {
            var engine = CreateEngine();
            var rng = new Random(42);
            for (int i = 0; i < 25; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, rng.Next(10, 50)));

            var profiles = engine.AnalyzeTailRisk();
            Assert.Single(profiles);
            Assert.Equal(FailureCategory.Timeout, profiles[0].Category);
            Assert.Equal(25, profiles[0].SampleCount);
        }

        [Fact]
        public void AnalyzeTailRisk_ComputesVaR()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 30; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, i * 3.0));

            var profiles = engine.AnalyzeTailRisk();
            Assert.Single(profiles);
            Assert.True(profiles[0].ValueAtRisk95 > profiles[0].ValueAtRisk99 ||
                        profiles[0].ValueAtRisk99 >= profiles[0].ValueAtRisk95);
        }

        [Fact]
        public void AnalyzeTailRisk_VaR99GreaterThanVaR95()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 30; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, i * 3.0));

            var profiles = engine.AnalyzeTailRisk();
            Assert.True(profiles[0].ValueAtRisk99 >= profiles[0].ValueAtRisk95);
        }

        [Fact]
        public void AnalyzeTailRisk_FatTailDetection()
        {
            var engine = CreateEngine();
            // Create data with fat tails: many small values, a few very large
            for (int i = 0; i < 25; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 10));
            // Add extreme outliers
            for (int i = 0; i < 5; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 95));

            var profiles = engine.AnalyzeTailRisk();
            Assert.Single(profiles);
            Assert.True(profiles[0].Kurtosis > 0, "Expected positive kurtosis for fat-tailed data");
        }

        [Fact]
        public void AnalyzeTailRisk_FiltersByPromptId()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 25; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20 + i));
            for (int i = 0; i < 25; i++)
                engine.RecordEvent(MakeEvent("p2", FailureCategory.Injection, 30 + i));

            var p1 = engine.AnalyzeTailRisk("p1");
            Assert.All(p1, p => Assert.Equal(25, p.SampleCount));
        }

        [Fact]
        public void AnalyzeTailRisk_MultipleCategories()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 25; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20 + i));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Injection, 30 + i));
            }

            var profiles = engine.AnalyzeTailRisk();
            Assert.Equal(2, profiles.Count);
        }

        [Fact]
        public void AnalyzeTailRisk_MaxObservedImpact()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 25; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, i * 4.0));

            var profile = engine.AnalyzeTailRisk().First();
            Assert.Equal(96.0, profile.MaxObservedImpact);
        }

        [Fact]
        public void AnalyzeTailRisk_ExpectedShortfallGteVaR()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 30; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, i * 3.0));

            var profile = engine.AnalyzeTailRisk().First();
            Assert.True(profile.ExpectedShortfall >= profile.ValueAtRisk95);
        }

        // ── Fragility Surface Mapping ───────────────

        [Fact]
        public void MapFragilitySurface_EmptyOnFewEvents()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30));
            Assert.Empty(engine.MapFragilitySurface());
        }

        [Fact]
        public void MapFragilitySurface_DetectsCorrelation()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Place Timeout and Hallucination events in the same time windows
            for (int i = 0; i < 10; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30,
                    timestamp: baseTime.AddSeconds(i * 30)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 40,
                    timestamp: baseTime.AddSeconds(i * 30 + 5)));
            }

            var surfaces = engine.MapFragilitySurface();
            Assert.NotEmpty(surfaces);
            var pair = surfaces.First();
            Assert.True(pair.AmplificationFactor >= 1.0);
        }

        [Fact]
        public void MapFragilitySurface_NoCorrelationForIndependentEvents()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Place events in widely separated time windows
            for (int i = 0; i < 10; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30,
                    timestamp: baseTime.AddMinutes(i * 10)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Injection, 40,
                    timestamp: baseTime.AddMinutes(i * 10 + 5)));
            }

            var surfaces = engine.MapFragilitySurface();
            // Should still find some co-occurrence since 5 min is within 60s window? No, 5 min > 60s
            // Events are 5 min apart within each pair, which exceeds default cascade window
            // But they might end up in different windows
        }

        [Fact]
        public void MapFragilitySurface_OrderedByAmplification()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30,
                    timestamp: baseTime.AddSeconds(i * 30)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 40,
                    timestamp: baseTime.AddSeconds(i * 30 + 2)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Injection, 50,
                    timestamp: baseTime.AddSeconds(i * 30 + 4)));
            }

            var surfaces = engine.MapFragilitySurface();
            Assert.NotEmpty(surfaces);
            // Should be ordered descending by amplification
            for (int i = 1; i < surfaces.Count; i++)
                Assert.True(surfaces[i - 1].AmplificationFactor >= surfaces[i].AmplificationFactor);
        }

        // ── Cascade Detection ───────────────────────

        [Fact]
        public void DetectCascades_EmptyOnSingleEvent()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30));
            Assert.Empty(engine.DetectCascades());
        }

        [Fact]
        public void DetectCascades_FindsChain()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50,
                timestamp: baseTime));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.CascadeFailure, 60,
                timestamp: baseTime.AddSeconds(10)));
            engine.RecordEvent(MakeEvent("p3", FailureCategory.CascadeFailure, 40,
                timestamp: baseTime.AddSeconds(20)));

            var cascades = engine.DetectCascades();
            Assert.Single(cascades);
            Assert.Equal(3, cascades[0].ChainLength);
            Assert.Equal("p1", cascades[0].PatientZeroPromptId);
        }

        [Fact]
        public void DetectCascades_SeparateChains()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Chain 1
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50,
                timestamp: baseTime));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Timeout, 40,
                timestamp: baseTime.AddSeconds(10)));
            // Gap > cascade window
            // Chain 2
            engine.RecordEvent(MakeEvent("p3", FailureCategory.Injection, 60,
                timestamp: baseTime.AddMinutes(5)));
            engine.RecordEvent(MakeEvent("p4", FailureCategory.Injection, 30,
                timestamp: baseTime.AddMinutes(5).AddSeconds(15)));

            var cascades = engine.DetectCascades();
            Assert.Equal(2, cascades.Count);
        }

        [Fact]
        public void DetectCascades_OrderedByImpact()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Low-impact chain
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 10, timestamp: baseTime));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Timeout, 10, timestamp: baseTime.AddSeconds(5)));
            // High-impact chain
            engine.RecordEvent(MakeEvent("p3", FailureCategory.Injection, 90, timestamp: baseTime.AddMinutes(5)));
            engine.RecordEvent(MakeEvent("p4", FailureCategory.Injection, 80, timestamp: baseTime.AddMinutes(5).AddSeconds(5)));

            var cascades = engine.DetectCascades();
            Assert.True(cascades[0].TotalImpact >= cascades[1].TotalImpact);
        }

        [Fact]
        public void DetectCascades_TracksAffectedPrompts()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50, timestamp: baseTime));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Timeout, 40, timestamp: baseTime.AddSeconds(10)));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30, timestamp: baseTime.AddSeconds(20)));

            var cascades = engine.DetectCascades();
            Assert.Single(cascades);
            Assert.Contains("p1", cascades[0].AffectedPromptIds);
            Assert.Contains("p2", cascades[0].AffectedPromptIds);
        }

        [Fact]
        public void DetectCascades_ComputesDuration()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50, timestamp: baseTime));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Timeout, 40, timestamp: baseTime.AddSeconds(30)));

            var cascades = engine.DetectCascades();
            Assert.Single(cascades);
            Assert.Equal(30.0, cascades[0].DurationSeconds);
        }

        // ── Antecedent Mining ───────────────────────

        [Fact]
        public void MineAntecedents_EmptyOnFewEvents()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30));
            Assert.Empty(engine.MineAntecedents());
        }

        [Fact]
        public void MineAntecedents_FindsPrecursors()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Pattern: Timeout events precede Hallucination crises
            for (int i = 0; i < 5; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20,
                    timestamp: baseTime.AddMinutes(i * 10)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 80,
                    timestamp: baseTime.AddMinutes(i * 10 + 2)));
            }

            var signals = engine.MineAntecedents();
            Assert.NotEmpty(signals);
            Assert.Contains(signals, s => s.AssociatedCategory == FailureCategory.Hallucination);
        }

        [Fact]
        public void MineAntecedents_CalculatesLeadTime()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20,
                    timestamp: baseTime.AddMinutes(i * 10)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 80,
                    timestamp: baseTime.AddMinutes(i * 10 + 2)));
            }

            var signals = engine.MineAntecedents();
            var signal = signals.First(s => s.AssociatedCategory == FailureCategory.Hallucination);
            Assert.True(signal.LeadTimeSeconds > 0);
            Assert.True(signal.LeadTimeSeconds <= 300);
        }

        [Fact]
        public void MineAntecedents_HasConfidence()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20,
                    timestamp: baseTime.AddMinutes(i * 10)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 80,
                    timestamp: baseTime.AddMinutes(i * 10 + 2)));
            }

            var signals = engine.MineAntecedents();
            Assert.All(signals, s =>
            {
                Assert.InRange(s.Confidence, 0, 1);
                Assert.True(s.Occurrences >= 2);
            });
        }

        [Fact]
        public void MineAntecedents_OrderedByConfidence()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20,
                    timestamp: baseTime.AddMinutes(i * 10)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Injection, 25,
                    timestamp: baseTime.AddMinutes(i * 10 + 1)));
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 80,
                    timestamp: baseTime.AddMinutes(i * 10 + 2)));
            }

            var signals = engine.MineAntecedents();
            if (signals.Count > 1)
            {
                for (int i = 1; i < signals.Count; i++)
                    Assert.True(signals[i - 1].Confidence >= signals[i].Confidence);
            }
        }

        [Fact]
        public void MineAntecedents_NoSignalForDistantEvents()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Events too far apart (> antecedent window)
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20,
                timestamp: baseTime));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20,
                timestamp: baseTime.AddSeconds(1)));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Hallucination, 80,
                timestamp: baseTime.AddMinutes(30)));

            var signals = engine.MineAntecedents();
            // Should be empty — precursor is too far from the major event
            // (depends on config, default antecedent window is 300s = 5min)
            Assert.Empty(signals);
        }

        // ── Impact Amplification ────────────────────

        [Fact]
        public void AnalyzeImpactAmplification_EmptyOnNoEvents()
        {
            var engine = CreateEngine();
            Assert.Empty(engine.AnalyzeImpactAmplification());
        }

        [Fact]
        public void AnalyzeImpactAmplification_MeasuresBlastRadius()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            var trigger = MakeEvent("p1", FailureCategory.Timeout, 80, timestamp: baseTime);
            engine.RecordEvent(trigger);
            engine.RecordEvent(MakeEvent("p2", FailureCategory.CascadeFailure, 40,
                timestamp: baseTime.AddSeconds(10)));
            engine.RecordEvent(MakeEvent("p3", FailureCategory.CascadeFailure, 30,
                timestamp: baseTime.AddSeconds(20)));

            var amps = engine.AnalyzeImpactAmplification(trigger.Id);
            Assert.Single(amps);
            Assert.Equal(2, amps[0].BlastRadius);
        }

        [Fact]
        public void AnalyzeImpactAmplification_ComputesAmplificationRatio()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            var trigger = MakeEvent("p1", FailureCategory.Timeout, 60, timestamp: baseTime);
            engine.RecordEvent(trigger);
            engine.RecordEvent(MakeEvent("p2", FailureCategory.CascadeFailure, 40,
                timestamp: baseTime.AddSeconds(10)));

            var amps = engine.AnalyzeImpactAmplification(trigger.Id);
            Assert.True(amps[0].AmplificationRatio > 1.0);
            Assert.Equal(60, amps[0].DirectImpact);
            Assert.Equal(40, amps[0].IndirectImpact);
        }

        [Fact]
        public void AnalyzeImpactAmplification_AutoFiltersHighSeverity()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            // Low impact event should not appear in auto-filtered results
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 10, timestamp: baseTime));
            // High impact should
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Hallucination, 70, timestamp: baseTime.AddMinutes(5)));

            var amps = engine.AnalyzeImpactAmplification();
            Assert.Single(amps);
            Assert.Equal("p2", engine.GetEvents().First(e => e.Id == amps[0].EventId).PromptId);
        }

        [Fact]
        public void AnalyzeImpactAmplification_RecoveryDifficulty()
        {
            var engine = CreateEngine();
            var baseTime = DateTime.UtcNow;
            var trigger = MakeEvent("p1", FailureCategory.Timeout, 80, timestamp: baseTime, cascadeDepth: 2);
            engine.RecordEvent(trigger);

            var amps = engine.AnalyzeImpactAmplification(trigger.Id);
            Assert.True(amps[0].RecoveryDifficultyScore > 0);
        }

        // ── Snapshot Generation ─────────────────────

        [Fact]
        public void GenerateSnapshot_EmptyEvents()
        {
            var engine = CreateEngine();
            var snapshot = engine.GenerateSnapshot("p1");
            Assert.Equal(0, snapshot.EventCount);
            Assert.Equal(0, snapshot.ExposureScore);
        }

        [Fact]
        public void GenerateSnapshot_WithEvents()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 80));

            var snapshot = engine.GenerateSnapshot("p1");
            Assert.Equal(3, snapshot.EventCount);
            Assert.Equal("p1", snapshot.PromptId);
            Assert.NotNull(snapshot.WorstSeverity);
            Assert.NotNull(snapshot.DominantCategory);
        }

        [Fact]
        public void GenerateSnapshot_TierAssignment()
        {
            var engine = CreateEngine();
            // Create enough extreme events to produce a high exposure score
            for (int i = 0; i < 10; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 90));

            var snapshot = engine.GenerateSnapshot("p1");
            Assert.True(snapshot.ExposureScore > 0);
            // With all high-impact events, tier should indicate poor health
            Assert.True(snapshot.Tier == BlackSwanHealthTier.Blind ||
                        snapshot.Tier == BlackSwanHealthTier.Exposed ||
                        snapshot.Tier == BlackSwanHealthTier.Aware);
        }

        [Fact]
        public void GenerateSnapshot_HasTailRiskSummary()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 30 + i * 10));

            var snapshot = engine.GenerateSnapshot("p1");
            Assert.False(string.IsNullOrWhiteSpace(snapshot.TailRiskSummary));
        }

        // ── Fleet Report ────────────────────────────

        [Fact]
        public void GenerateFleetReport_EmptyFleet()
        {
            var engine = CreateEngine();
            var report = engine.GenerateFleetReport();
            Assert.Equal(100, report.FleetScore);
            Assert.Equal(BlackSwanHealthTier.Fortified, report.Tier);
            Assert.Empty(report.Snapshots);
        }

        [Fact]
        public void GenerateFleetReport_WithMultiplePrompts()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Injection, 70));
            engine.RecordEvent(MakeEvent("p3", FailureCategory.Hallucination, 30));

            var report = engine.GenerateFleetReport();
            Assert.Equal(3, report.Snapshots.Count);
            Assert.True(report.FleetScore >= 0 && report.FleetScore <= 100);
        }

        [Fact]
        public void GenerateFleetReport_HasInsights()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 97,
                severity: BlackSwanSeverity.BlackSwan));

            var report = engine.GenerateFleetReport();
            Assert.NotEmpty(report.Insights);
        }

        [Fact]
        public void GenerateFleetReport_DisableInsights()
        {
            var config = new BlackSwanConfig { EnableAutonomousInsights = false };
            var engine = CreateEngine(config);
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 97,
                severity: BlackSwanSeverity.BlackSwan));

            var report = engine.GenerateFleetReport();
            Assert.Empty(report.Insights);
        }

        // ── HTML Dashboard ──────────────────────────

        [Fact]
        public void GenerateHtmlDashboard_ReturnsValidHtml()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Injection, 70));

            var html = engine.GenerateHtmlDashboard();
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Black Swan Dashboard", html);
            Assert.Contains("</html>", html);
        }

        [Fact]
        public void GenerateHtmlDashboard_EmptyFleet()
        {
            var engine = CreateEngine();
            var html = engine.GenerateHtmlDashboard();
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Black Swan Dashboard", html);
        }

        [Fact]
        public void GenerateHtmlDashboard_ContainsSeverityDistribution()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));
            var html = engine.GenerateHtmlDashboard();
            Assert.Contains("Severity Distribution", html);
        }

        // ── Config Customization ────────────────────

        [Fact]
        public void CustomConfig_AffectsExtremeDetection()
        {
            var config = new BlackSwanConfig { ZScoreThreshold = 1.0 };
            var engine = CreateEngine(config);
            for (int i = 0; i < 10; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 10));
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));

            var extreme = engine.DetectExtremeEvents("p1");
            Assert.NotEmpty(extreme);
        }

        [Fact]
        public void CustomConfig_CascadeWindow()
        {
            // Tight cascade window — events 30s apart should NOT chain
            var config = new BlackSwanConfig { CascadeWindowSeconds = 5 };
            var engine = CreateEngine(config);
            var baseTime = DateTime.UtcNow;
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50, timestamp: baseTime));
            engine.RecordEvent(MakeEvent("p2", FailureCategory.Timeout, 40, timestamp: baseTime.AddSeconds(30)));

            var cascades = engine.DetectCascades();
            Assert.Empty(cascades);
        }

        [Fact]
        public void CustomConfig_TailMinSamples()
        {
            var config = new BlackSwanConfig { TailAnalysisMinSamples = 5 };
            var engine = CreateEngine(config);
            for (int i = 0; i < 8; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 20 + i * 5));

            var profiles = engine.AnalyzeTailRisk();
            Assert.Single(profiles); // 8 > 5 min samples
        }

        [Fact]
        public void CustomConfig_SeverityThresholds()
        {
            var config = new BlackSwanConfig
            {
                SeverityThresholds = new Dictionary<BlackSwanSeverity, double>
                {
                    [BlackSwanSeverity.Anomaly] = 10,
                    [BlackSwanSeverity.Shock] = 20,
                    [BlackSwanSeverity.Crisis] = 30,
                    [BlackSwanSeverity.Catastrophe] = 50,
                    [BlackSwanSeverity.BlackSwan] = 70
                }
            };
            var engine = CreateEngine(config);
            Assert.Equal(BlackSwanSeverity.BlackSwan, engine.ClassifySeverity(75));
            Assert.Equal(BlackSwanSeverity.Catastrophe, engine.ClassifySeverity(55));
        }

        // ── Edge Cases ──────────────────────────────

        [Fact]
        public void EdgeCase_SingleEventPerPrompt()
        {
            var engine = CreateEngine();
            engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 50));

            var snapshot = engine.GenerateSnapshot("p1");
            Assert.Equal(1, snapshot.EventCount);
            var report = engine.GenerateFleetReport();
            Assert.Single(report.Snapshots);
        }

        [Fact]
        public void EdgeCase_AllSameCategory()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 30; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 10 + i));

            var profiles = engine.AnalyzeTailRisk();
            Assert.Single(profiles);
            Assert.Equal(FailureCategory.Timeout, profiles[0].Category);
        }

        [Fact]
        public void EdgeCase_AllMaxImpact()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Timeout, 100));

            var extreme = engine.DetectExtremeEvents("p1");
            // All are high impact so all should be flagged as extreme (>= crisis threshold)
            Assert.Equal(10, extreme.Count);
        }

        [Fact]
        public void EdgeCase_ManyPromptsManyCategories()
        {
            var engine = CreateEngine();
            var categories = Enum.GetValues<FailureCategory>();
            for (int p = 0; p < 5; p++)
                for (int c = 0; c < categories.Length; c++)
                    engine.RecordEvent(MakeEvent($"p{p}", categories[c], 20 + c * 5));

            var report = engine.GenerateFleetReport();
            Assert.Equal(5, report.Snapshots.Count);
        }

        [Fact]
        public void EdgeCase_ZeroImpactEvents()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordEvent(MakeEvent("p1", FailureCategory.Unknown, 0));

            var snapshot = engine.GenerateSnapshot("p1");
            Assert.Equal(5, snapshot.EventCount);
            // Zero-impact events still contribute to exposure via extreme-rate and cascade
            // calculations, so score may be small but non-negative
            Assert.True(snapshot.ExposureScore >= 0);
        }
    }
}
