namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Xunit;

    public class PromptSituationRoomTests
    {
        private static SituationSignal MakeSignal(SignalDomain domain, string prompt, double severity, string desc = "test signal")
            => new SituationSignal(domain, prompt, severity, desc, DateTimeOffset.UtcNow);

        // ── Empty / Green State ─────────────────────────

        [Fact]
        public void EmptyRoom_StatusIsGreen()
        {
            var room = new PromptSituationRoom();
            Assert.Equal(OperationalStatus.Green, room.GetCurrentStatus());
        }

        [Fact]
        public void EmptyRoom_SituationsContainQuietPeriod()
        {
            var room = new PromptSituationRoom();
            var situations = room.GetActiveSituations();
            Assert.Single(situations);
            Assert.Equal(SituationType.QuietPeriod, situations[0].Type);
            Assert.Equal(UrgencyLevel.Routine, situations[0].Urgency);
        }

        [Fact]
        public void EmptyRoom_SitrepHasZeroSignals()
        {
            var room = new PromptSituationRoom();
            var report = room.GenerateSitrep();
            Assert.Equal(0, report.TotalSignals);
            Assert.Equal(0, report.AffectedPromptCount);
        }

        // ── Single Signal ───────────────────────────────

        [Fact]
        public void SingleLowSignal_StatusIsYellow()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "prompt-a", 0.5));
            Assert.Equal(OperationalStatus.Yellow, room.GetCurrentStatus());
        }

        [Fact]
        public void SingleSignal_NotEnoughForCrisis()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "prompt-a", 0.8));
            var situations = room.GetActiveSituations();
            Assert.Single(situations);
            Assert.Equal(SituationType.QuietPeriod, situations[0].Type);
        }

        // ── Portfolio Health Crisis ─────────────────────

        [Fact]
        public void ThreeHealthSignals_TriggersPortfolioHealthCrisis()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p1", 0.6));
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p2", 0.7));
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p3", 0.5));

            var situations = room.GetActiveSituations();
            Assert.Contains(situations, s => s.Type == SituationType.PortfolioHealthCrisis);
        }

        [Fact]
        public void HealthCrisis_ListsAffectedPrompts()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "alpha", 0.6));
            room.IngestSignal(MakeSignal(SignalDomain.Health, "beta", 0.7));
            room.IngestSignal(MakeSignal(SignalDomain.Health, "gamma", 0.5));

            var crisis = room.GetActiveSituations().First(s => s.Type == SituationType.PortfolioHealthCrisis);
            Assert.Contains("alpha", crisis.AffectedPrompts);
            Assert.Contains("beta", crisis.AffectedPrompts);
            Assert.Contains("gamma", crisis.AffectedPrompts);
        }

        // ── Drift Storm ─────────────────────────────────

        [Fact]
        public void ThreeDriftSignals_TriggersDriftStorm()
        {
            var room = new PromptSituationRoom();
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Drift, "d1", 0.6),
                MakeSignal(SignalDomain.Drift, "d2", 0.5),
                MakeSignal(SignalDomain.Drift, "d3", 0.7)
            });

            var situations = room.GetActiveSituations();
            Assert.Contains(situations, s => s.Type == SituationType.DriftStorm);
        }

        [Fact]
        public void DriftStorm_HasRecommendedActions()
        {
            var room = new PromptSituationRoom();
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Drift, "d1", 0.6),
                MakeSignal(SignalDomain.Drift, "d2", 0.5),
                MakeSignal(SignalDomain.Drift, "d3", 0.7)
            });

            var storm = room.GetActiveSituations().First(s => s.Type == SituationType.DriftStorm);
            Assert.True(storm.RecommendedActions.Count >= 3);
            Assert.Contains(storm.RecommendedActions, a => a.Category == "Immediate");
            Assert.Contains(storm.RecommendedActions, a => a.Category == "Investigate");
            Assert.Contains(storm.RecommendedActions, a => a.Category == "Prevent");
        }

        // ── Cost Spike ──────────────────────────────────

        [Fact]
        public void TwoCostSignals_TriggersCostSpike()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Cost, "expensive-1", 0.8));
            room.IngestSignal(MakeSignal(SignalDomain.Cost, "expensive-2", 0.6));

            var situations = room.GetActiveSituations();
            Assert.Contains(situations, s => s.Type == SituationType.CostSpike);
        }

        // ── Security Breach ─────────────────────────────

        [Fact]
        public void TwoRiskSignals_TriggersSecurityBreach()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Risk, "vuln-1", 0.9));
            room.IngestSignal(MakeSignal(SignalDomain.Risk, "vuln-2", 0.8));

            var situations = room.GetActiveSituations();
            Assert.Contains(situations, s => s.Type == SituationType.SecurityBreach);
        }

        [Fact]
        public void SecurityBreach_EscalatesUrgencyAboveBaseSeverity()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Risk, "vuln-1", 0.85));
            room.IngestSignal(MakeSignal(SignalDomain.Risk, "vuln-2", 0.85));

            var breach = room.GetActiveSituations().First(s => s.Type == SituationType.SecurityBreach);
            // Risk signals get +0.2 severity boost -> should be High or Critical
            Assert.True(breach.Urgency >= UrgencyLevel.High);
        }

        // ── Staleness Cascade ───────────────────────────

        [Fact]
        public void ThreeStaleSignals_TriggersStalenessCascade()
        {
            var room = new PromptSituationRoom();
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Staleness, "old-1", 0.5),
                MakeSignal(SignalDomain.Staleness, "old-2", 0.6),
                MakeSignal(SignalDomain.Staleness, "old-3", 0.4)
            });

            Assert.Contains(room.GetActiveSituations(), s => s.Type == SituationType.StalenessCascade);
        }

        // ── Complexity Creep ────────────────────────────

        [Fact]
        public void ThreeComplexitySignals_TriggersComplexityCreep()
        {
            var room = new PromptSituationRoom();
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Complexity, "complex-1", 0.7),
                MakeSignal(SignalDomain.Complexity, "complex-2", 0.6),
                MakeSignal(SignalDomain.Complexity, "complex-3", 0.8)
            });

            Assert.Contains(room.GetActiveSituations(), s => s.Type == SituationType.ComplexityCreep);
        }

        // ── Urgency Escalation ──────────────────────────

        [Fact]
        public void HighSeveritySignals_EscalateToHighUrgency()
        {
            var room = new PromptSituationRoom();
            for (int i = 0; i < 5; i++)
                room.IngestSignal(MakeSignal(SignalDomain.Health, $"p{i}", 0.8));

            var crisis = room.GetActiveSituations().First(s => s.Type == SituationType.PortfolioHealthCrisis);
            Assert.True(crisis.Urgency >= UrgencyLevel.High);
        }

        [Fact]
        public void CriticalSeverity_EscalatesToCritical()
        {
            var room = new PromptSituationRoom();
            for (int i = 0; i < 8; i++)
                room.IngestSignal(MakeSignal(SignalDomain.Health, $"p{i}", 0.95));

            var crisis = room.GetActiveSituations().First(s => s.Type == SituationType.PortfolioHealthCrisis);
            Assert.Equal(UrgencyLevel.Critical, crisis.Urgency);
        }

        // ── Operational Status Mapping ──────────────────

        [Fact]
        public void MultipleCriticalSignals_StatusIsRed()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p1", 0.95));
            room.IngestSignal(MakeSignal(SignalDomain.Drift, "p2", 0.95));

            Assert.Equal(OperationalStatus.Red, room.GetCurrentStatus());
        }

        [Fact]
        public void ThreeDomains_HighSeverity_StatusIsOrange()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p1", 0.5));
            room.IngestSignal(MakeSignal(SignalDomain.Drift, "p2", 0.5));
            room.IngestSignal(MakeSignal(SignalDomain.Cost, "p3", 0.5));

            Assert.Equal(OperationalStatus.Orange, room.GetCurrentStatus());
        }

        // ── Watch List ──────────────────────────────────

        [Fact]
        public void WatchList_AddAndRetrieve()
        {
            var room = new PromptSituationRoom();
            room.AddToWatchList("prompt-x", "Needs monitoring after update");

            var list = room.GetWatchList();
            Assert.Single(list);
            Assert.Equal("prompt-x", list[0].PromptName);
            Assert.Equal("Needs monitoring after update", list[0].Reason);
        }

        [Fact]
        public void WatchList_Remove()
        {
            var room = new PromptSituationRoom();
            room.AddToWatchList("prompt-x", "reason");
            room.RemoveFromWatchList("prompt-x");
            Assert.Empty(room.GetWatchList());
        }

        [Fact]
        public void WatchList_DuplicateAddIgnored()
        {
            var room = new PromptSituationRoom();
            room.AddToWatchList("prompt-x", "first reason");
            room.AddToWatchList("prompt-x", "second reason");
            Assert.Single(room.GetWatchList());
            Assert.Equal("first reason", room.GetWatchList()[0].Reason);
        }

        [Fact]
        public void WatchList_SignalCountIncrementsForWatchedPrompts()
        {
            var room = new PromptSituationRoom();
            room.AddToWatchList("prompt-x", "under observation");
            room.IngestSignal(MakeSignal(SignalDomain.Health, "prompt-x", 0.5));
            room.IngestSignal(MakeSignal(SignalDomain.Drift, "prompt-x", 0.6));

            Assert.Equal(2, room.GetWatchList()[0].SignalCount);
        }

        [Fact]
        public void WatchList_NullNameThrows()
        {
            var room = new PromptSituationRoom();
            Assert.Throws<ArgumentException>(() => room.AddToWatchList("", "reason"));
            Assert.Throws<ArgumentException>(() => room.AddToWatchList("name", ""));
        }

        // ── SITREP ──────────────────────────────────────

        [Fact]
        public void Sitrep_ContainsAllFields()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p1", 0.5));
            var report = room.GenerateSitrep();

            Assert.NotNull(report);
            Assert.NotNull(report.Situations);
            Assert.NotNull(report.WatchList);
            Assert.NotNull(report.Recommendations);
            Assert.Equal(1, report.TotalSignals);
            Assert.Equal(1, report.AffectedPromptCount);
        }

        [Fact]
        public void Sitrep_AutoWatchesHighSeverityPrompts()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Risk, "risky-prompt", 0.85));
            var report = room.GenerateSitrep();

            Assert.Contains(report.WatchList, w => w.PromptName == "risky-prompt");
        }

        [Fact]
        public void Sitrep_RecommendationsSortedByPriority()
        {
            var room = new PromptSituationRoom();
            for (int i = 0; i < 3; i++)
                room.IngestSignal(MakeSignal(SignalDomain.Health, $"p{i}", 0.7));

            var report = room.GenerateSitrep();
            for (int i = 1; i < report.Recommendations.Count; i++)
            {
                Assert.True(report.Recommendations[i].Priority >= report.Recommendations[i - 1].Priority);
            }
        }

        // ── Mixed Domains ───────────────────────────────

        [Fact]
        public void MixedDomains_ProduceMultipleSituations()
        {
            var room = new PromptSituationRoom();
            // Health crisis (3)
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Health, "h1", 0.6),
                MakeSignal(SignalDomain.Health, "h2", 0.7),
                MakeSignal(SignalDomain.Health, "h3", 0.5)
            });
            // Cost spike (2)
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Cost, "c1", 0.8),
                MakeSignal(SignalDomain.Cost, "c2", 0.6)
            });

            var situations = room.GetActiveSituations();
            Assert.Contains(situations, s => s.Type == SituationType.PortfolioHealthCrisis);
            Assert.Contains(situations, s => s.Type == SituationType.CostSpike);
            Assert.DoesNotContain(situations, s => s.Type == SituationType.QuietPeriod);
        }

        // ── Clear Signals ───────────────────────────────

        [Fact]
        public void ClearSignals_ResetsToGreen()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p1", 0.8));
            room.IngestSignal(MakeSignal(SignalDomain.Drift, "p2", 0.9));

            room.ClearSignals();

            Assert.Equal(OperationalStatus.Green, room.GetCurrentStatus());
            Assert.Single(room.GetActiveSituations());
            Assert.Equal(SituationType.QuietPeriod, room.GetActiveSituations()[0].Type);
        }

        // ── Text Export ─────────────────────────────────

        [Fact]
        public void TextExport_ContainsStatusAndSituations()
        {
            var room = new PromptSituationRoom();
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Risk, "vuln-1", 0.9),
                MakeSignal(SignalDomain.Risk, "vuln-2", 0.8)
            });

            var text = room.ExportSitrepText();
            Assert.Contains("SITUATION ROOM", text);
            Assert.Contains("Status:", text);
            Assert.Contains("SecurityBreach", text);
        }

        [Fact]
        public void TextExport_EmptyRoom_ShowsGreen()
        {
            var room = new PromptSituationRoom();
            var text = room.ExportSitrepText();
            Assert.Contains("GREEN", text);
            Assert.Contains("QuietPeriod", text);
        }

        // ── JSON Export ─────────────────────────────────

        [Fact]
        public void JsonExport_IsValidJson()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p1", 0.5));
            var json = room.ExportSitrepJson();

            var doc = JsonDocument.Parse(json);
            Assert.NotNull(doc);
            Assert.True(doc.RootElement.TryGetProperty("status", out _));
            Assert.True(doc.RootElement.TryGetProperty("situations", out _));
            Assert.True(doc.RootElement.TryGetProperty("trend", out _));
        }

        [Fact]
        public void JsonExport_RoundTrip_PreservesStatus()
        {
            var room = new PromptSituationRoom();
            room.IngestSignals(new[]
            {
                MakeSignal(SignalDomain.Drift, "d1", 0.7),
                MakeSignal(SignalDomain.Drift, "d2", 0.7),
                MakeSignal(SignalDomain.Drift, "d3", 0.7)
            });

            var json = room.ExportSitrepJson();
            var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("status").GetString();
            Assert.NotNull(status);
            Assert.NotEqual("Green", status); // Should be Yellow or higher
        }

        // ── Trend Direction ─────────────────────────────

        [Fact]
        public void Trend_UnknownWhenNoHistory()
        {
            var room = new PromptSituationRoom();
            var report = room.GenerateSitrep();
            Assert.Equal(TrendDirection.Insufficient, report.Trend);
        }

        // ── Edge Cases ──────────────────────────────────

        [Fact]
        public void NullSignal_Throws()
        {
            var room = new PromptSituationRoom();
            Assert.Throws<ArgumentNullException>(() => room.IngestSignal(null!));
        }

        [Fact]
        public void NullSignals_Throws()
        {
            var room = new PromptSituationRoom();
            Assert.Throws<ArgumentNullException>(() => room.IngestSignals(null!));
        }

        [Fact]
        public void Signal_SeverityClampedTo0_1()
        {
            var s1 = new SituationSignal(SignalDomain.Health, "p1", -0.5, "low");
            var s2 = new SituationSignal(SignalDomain.Health, "p1", 1.5, "high");
            Assert.Equal(0.0, s1.Severity);
            Assert.Equal(1.0, s2.Severity);
        }

        [Fact]
        public void Signal_NullPromptNameThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new SituationSignal(SignalDomain.Health, null!, 0.5, "desc"));
        }

        [Fact]
        public void Signal_NullDescriptionThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new SituationSignal(SignalDomain.Health, "p1", 0.5, null!));
        }

        [Fact]
        public void RemoveFromWatchList_NonExistent_NoError()
        {
            var room = new PromptSituationRoom();
            room.RemoveFromWatchList("nonexistent"); // should not throw
        }

        [Fact]
        public void WatchList_CaseInsensitive()
        {
            var room = new PromptSituationRoom();
            room.AddToWatchList("PromptX", "reason");
            room.AddToWatchList("promptx", "different reason"); // should be ignored (duplicate)
            Assert.Single(room.GetWatchList());
        }

        // ── TimeSinceAllClear ───────────────────────────

        [Fact]
        public void TimeSinceAllClear_NullWhenGreen()
        {
            var room = new PromptSituationRoom();
            var report = room.GenerateSitrep();
            Assert.Null(report.TimeSinceAllClear);
        }

        [Fact]
        public void TimeSinceAllClear_HasValueWhenNotGreen()
        {
            var room = new PromptSituationRoom();
            room.IngestSignal(MakeSignal(SignalDomain.Health, "p1", 0.95));
            room.IngestSignal(MakeSignal(SignalDomain.Drift, "p2", 0.95));
            var report = room.GenerateSitrep();
            // Status is Red -> should have timeSinceAllClear
            Assert.NotNull(report.TimeSinceAllClear);
        }
    }
}
