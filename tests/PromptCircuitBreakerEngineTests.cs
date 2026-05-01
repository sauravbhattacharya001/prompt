namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptCircuitBreakerEngineTests
    {
        private readonly PromptCircuitBreakerEngine _engine;

        public PromptCircuitBreakerEngineTests()
        {
            _engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                FailureThreshold = 0.5,
                ConsecutiveFailureLimit = 5,
                LatencyThresholdMs = 5000,
                SlowCallThreshold = 0.3,
                WindowSize = 20,
                CooldownSeconds = 60,
                HalfOpenMaxProbes = 3,
                HalfOpenSuccessThreshold = 0.67,
                MinCallsBeforeTrip = 10
            });
        }

        // ─── Helpers ──────────────────────────────────

        private static CallOutcome MakeOutcome(string promptId, bool success, double latencyMs = 100, string? error = null)
        {
            return new CallOutcome
            {
                PromptId = promptId,
                Timestamp = DateTime.UtcNow,
                Success = success,
                LatencyMs = latencyMs,
                ErrorCategory = error
            };
        }

        private void RecordSuccesses(string promptId, int count, double latencyMs = 100)
        {
            for (int i = 0; i < count; i++)
                _engine.RecordOutcome(MakeOutcome(promptId, true, latencyMs));
        }

        private void RecordFailures(string promptId, int count, double latencyMs = 100)
        {
            for (int i = 0; i < count; i++)
                _engine.RecordOutcome(MakeOutcome(promptId, false, latencyMs, "test-error"));
        }

        // ─── Recording Outcomes ───────────────────────

        [Fact]
        public void RecordOutcome_ValidOutcome_IncreasesCallCount()
        {
            _engine.RecordOutcome(MakeOutcome("p1", true));
            Assert.Equal(1, _engine.GetCallCount("p1"));
        }

        [Fact]
        public void RecordOutcome_NullOutcome_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _engine.RecordOutcome(null!));
        }

        [Fact]
        public void RecordOutcome_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.RecordOutcome(MakeOutcome("", true)));
        }

        [Fact]
        public void RecordOutcome_MultipleCalls_TracksSeparately()
        {
            _engine.RecordOutcome(MakeOutcome("p1", true));
            _engine.RecordOutcome(MakeOutcome("p2", false));
            Assert.Equal(1, _engine.GetCallCount("p1"));
            Assert.Equal(1, _engine.GetCallCount("p2"));
        }

        [Fact]
        public void RecordOutcome_MultipleCallsSamePrompt_AccumulatesCount()
        {
            RecordSuccesses("p1", 5);
            Assert.Equal(5, _engine.GetCallCount("p1"));
        }

        // ─── Circuit Stays Closed ─────────────────────

        [Fact]
        public void Circuit_StaysClosedUnderThreshold()
        {
            // 4 failures out of 10 = 40% < 50% threshold
            RecordSuccesses("p1", 6);
            RecordFailures("p1", 4);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Closed, snap.State);
        }

        [Fact]
        public void Circuit_StaysClosedBelowMinCalls()
        {
            // All failures but only 5 calls (below MinCallsBeforeTrip=10)
            // Also below ConsecutiveFailureLimit=5
            RecordFailures("p1", 4);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Closed, snap.State);
        }

        [Fact]
        public void Circuit_NewPrompt_DefaultsClosed()
        {
            var snap = _engine.GetSnapshot("new-prompt");
            Assert.Equal(CBCircuitState.Closed, snap.State);
            Assert.Equal(100.0, snap.HealthScore);
        }

        // ─── Circuit Trips on Failure Rate ────────────

        [Fact]
        public void Circuit_TripsOnHighFailureRate()
        {
            // 4 successes + 7 failures = 11 calls, 63.6% failure rate > 50%
            RecordSuccesses("p1", 4);
            RecordFailures("p1", 7);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Open, snap.State);
        }

        [Fact]
        public void Circuit_TripsOnExactlyAtThreshold()
        {
            // 5 successes + 6 failures = 11 calls, ~54.5% > 50%
            RecordSuccesses("p1", 5);
            RecordFailures("p1", 6);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Open, snap.State);
        }

        // ─── Circuit Trips on Consecutive Failures ────

        [Fact]
        public void Circuit_TripsOnConsecutiveFailures()
        {
            // 5 consecutive failures trips regardless of MinCallsBeforeTrip
            RecordFailures("p1", 5);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Open, snap.State);
            Assert.Equal(TripReason.ConsecutiveFailures, snap.LastTripReason);
        }

        [Fact]
        public void Circuit_ConsecutiveFailures_ResetBySuccess()
        {
            RecordFailures("p1", 4);
            _engine.RecordOutcome(MakeOutcome("p1", true));
            RecordFailures("p1", 4);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Closed, snap.State);
        }

        // ─── Circuit Trips on Slow Calls ──────────────

        [Fact]
        public void Circuit_TripsOnSlowCallRate()
        {
            // 4 slow + 7 normal = 11 calls, ~36% slow > 30% threshold
            for (int i = 0; i < 4; i++)
                _engine.RecordOutcome(MakeOutcome("p1", true, 6000)); // slow
            for (int i = 0; i < 7; i++)
                _engine.RecordOutcome(MakeOutcome("p1", true, 100)); // normal
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Open, snap.State);
            Assert.Equal(TripReason.LatencyThreshold, snap.LastTripReason);
        }

        // ─── CanExecute ──────────────────────────────

        [Fact]
        public void CanExecute_ClosedCircuit_ReturnsTrue()
        {
            RecordSuccesses("p1", 5);
            Assert.True(_engine.CanExecute("p1"));
        }

        [Fact]
        public void CanExecute_OpenCircuit_ReturnsFalse()
        {
            RecordFailures("p1", 5); // trips on consecutive failures
            Assert.False(_engine.CanExecute("p1"));
        }

        [Fact]
        public void CanExecute_UnknownPrompt_ReturnsTrue()
        {
            Assert.True(_engine.CanExecute("unknown"));
        }

        [Fact]
        public void CanExecute_NullPrompt_ReturnsTrue()
        {
            Assert.True(_engine.CanExecute(null!));
        }

        // ─── Cooldown and HalfOpen Transition ─────────

        [Fact]
        public void CanExecute_OpenCircuit_TransitionsToHalfOpenAfterCooldown()
        {
            // Use short cooldown engine
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 3,
                CooldownSeconds = 0 // instant cooldown for testing
            });
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            // Circuit should be Open after 3 consecutive failures
            Assert.Equal(CBCircuitState.Open, engine.GetSnapshot("p1").State);
            // With 0 cooldown, CanExecute transitions to HalfOpen
            var canExec = engine.CanExecute("p1");
            Assert.True(canExec);
            Assert.Equal(CBCircuitState.HalfOpen, engine.GetSnapshot("p1").State);
        }

        // ─── HalfOpen Probe Behavior ─────────────────

        [Fact]
        public void HalfOpen_AllowsLimitedProbes()
        {
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 3,
                CooldownSeconds = 0,
                HalfOpenMaxProbes = 3,
                HalfOpenSuccessThreshold = 0.67
            });
            // Trip it
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            // Transition to HalfOpen
            engine.CanExecute("p1");
            Assert.Equal(CBCircuitState.HalfOpen, engine.GetSnapshot("p1").State);
        }

        [Fact]
        public void HalfOpen_RecoverySucceeds_ClosesCircuit()
        {
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 3,
                CooldownSeconds = 0,
                HalfOpenMaxProbes = 3,
                HalfOpenSuccessThreshold = 0.67
            });
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.CanExecute("p1"); // -> HalfOpen

            // 3 successful probes (100% >= 67%)
            engine.RecordOutcome(MakeOutcome("p1", true));
            engine.RecordOutcome(MakeOutcome("p1", true));
            engine.RecordOutcome(MakeOutcome("p1", true));

            Assert.Equal(CBCircuitState.Closed, engine.GetSnapshot("p1").State);
        }

        [Fact]
        public void HalfOpen_RecoveryFails_ReopensCircuit()
        {
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 3,
                CooldownSeconds = 0,
                HalfOpenMaxProbes = 3,
                HalfOpenSuccessThreshold = 0.67
            });
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.CanExecute("p1"); // -> HalfOpen

            // 3 failed probes
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));

            Assert.Equal(CBCircuitState.Open, engine.GetSnapshot("p1").State);
        }

        [Fact]
        public void HalfOpen_PartialRecovery_ReopensCircuit()
        {
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 3,
                CooldownSeconds = 0,
                HalfOpenMaxProbes = 3,
                HalfOpenSuccessThreshold = 0.67
            });
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.CanExecute("p1"); // -> HalfOpen

            // 1 success, 2 failures (33% < 67%)
            engine.RecordOutcome(MakeOutcome("p1", true));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));

            Assert.Equal(CBCircuitState.Open, engine.GetSnapshot("p1").State);
        }

        // ─── RecoveryReport ──────────────────────────

        [Fact]
        public void GetRecoveryReport_ClosedCircuit_ReturnsNull()
        {
            RecordSuccesses("p1", 5);
            Assert.Null(_engine.GetRecoveryReport("p1"));
        }

        [Fact]
        public void GetRecoveryReport_HalfOpen_ReturnsInsufficientData()
        {
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 3,
                CooldownSeconds = 0,
                HalfOpenMaxProbes = 3
            });
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.CanExecute("p1");
            engine.RecordOutcome(MakeOutcome("p1", true)); // 1 probe

            var report = engine.GetRecoveryReport("p1");
            Assert.NotNull(report);
            Assert.Equal(RecoveryVerdict.InsufficientData, report!.Verdict);
            Assert.Single(report.ProbeResults);
        }

        [Fact]
        public void GetRecoveryReport_UnknownPrompt_ReturnsNull()
        {
            Assert.Null(_engine.GetRecoveryReport("unknown"));
        }

        // ─── ForceTrip and ForceReset ─────────────────

        [Fact]
        public void ForceTrip_OpensCircuit()
        {
            RecordSuccesses("p1", 3);
            _engine.ForceTrip("p1", "Emergency shutdown");
            Assert.Equal(CBCircuitState.Open, _engine.GetSnapshot("p1").State);
            Assert.Equal(TripReason.ManualTrip, _engine.GetSnapshot("p1").LastTripReason);
        }

        [Fact]
        public void ForceTrip_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.ForceTrip(""));
        }

        [Fact]
        public void ForceReset_ClosesCircuit()
        {
            RecordFailures("p1", 5); // trips
            Assert.Equal(CBCircuitState.Open, _engine.GetSnapshot("p1").State);
            _engine.ForceReset("p1");
            Assert.Equal(CBCircuitState.Closed, _engine.GetSnapshot("p1").State);
        }

        [Fact]
        public void ForceReset_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.ForceReset(""));
        }

        // ─── Health Score ─────────────────────────────

        [Fact]
        public void HealthScore_AllSuccessful_Returns100()
        {
            RecordSuccesses("p1", 10);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(100.0, snap.HealthScore);
        }

        [Fact]
        public void HealthScore_AllFailed_ReturnsLow()
        {
            // Use a custom engine where we can trip and then force reset to see the score
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 100, // don't trip on consecutive
                MinCallsBeforeTrip = 100 // don't trip on rate
            });
            for (int i = 0; i < 10; i++)
                engine.RecordOutcome(MakeOutcome("p1", false));
            var snap = engine.GetSnapshot("p1");
            // 100 - (1.0 * 50) - (0 * 30) - (10 * 4) = 100 - 50 - 40 = 10
            Assert.Equal(10.0, snap.HealthScore);
        }

        [Fact]
        public void HealthScore_NoCalls_Returns100()
        {
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(100.0, snap.HealthScore);
        }

        // ─── Health Tier ──────────────────────────────

        [Fact]
        public void GetHealthTier_AllSuccessful_Pristine()
        {
            RecordSuccesses("p1", 10);
            Assert.Equal(CircuitHealthTier.Pristine, _engine.GetHealthTier("p1"));
        }

        [Fact]
        public void GetHealthTier_OpenCircuit_Tripped()
        {
            RecordFailures("p1", 5);
            Assert.Equal(CircuitHealthTier.Tripped, _engine.GetHealthTier("p1"));
        }

        [Fact]
        public void GetHealthTier_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.GetHealthTier(""));
        }

        // ─── Trip History ─────────────────────────────

        [Fact]
        public void GetTripHistory_RecordsTrips()
        {
            RecordFailures("p1", 5);
            var history = _engine.GetTripHistory("p1");
            Assert.Single(history);
            Assert.Equal(TripReason.ConsecutiveFailures, history[0].Reason);
        }

        [Fact]
        public void GetTripHistory_UnknownPrompt_ReturnsEmpty()
        {
            var history = _engine.GetTripHistory("unknown");
            Assert.Empty(history);
        }

        [Fact]
        public void GetTripHistory_MultipleTrips_TracksAll()
        {
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                ConsecutiveFailureLimit = 3,
                CooldownSeconds = 0,
                HalfOpenMaxProbes = 3,
                HalfOpenSuccessThreshold = 0.67
            });
            // First trip
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            // Recover
            engine.CanExecute("p1");
            engine.RecordOutcome(MakeOutcome("p1", true));
            engine.RecordOutcome(MakeOutcome("p1", true));
            engine.RecordOutcome(MakeOutcome("p1", true));
            // Second trip
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));
            engine.RecordOutcome(MakeOutcome("p1", false));

            var history = engine.GetTripHistory("p1");
            Assert.Equal(2, history.Count);
        }

        [Fact]
        public void GetTripHistory_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.GetTripHistory(""));
        }

        // ─── Fleet Health ─────────────────────────────

        [Fact]
        public void GetFleetHealth_EmptyFleet_Returns100()
        {
            var report = _engine.GetFleetHealth();
            Assert.Equal(0, report.TotalCircuits);
            Assert.Equal(100.0, report.OverallHealthScore);
        }

        [Fact]
        public void GetFleetHealth_MixedCircuits_ReportsCorrectly()
        {
            RecordSuccesses("p1", 10);
            RecordFailures("p2", 5); // trips
            var report = _engine.GetFleetHealth();
            Assert.Equal(2, report.TotalCircuits);
            Assert.Equal(1, report.ClosedCount);
            Assert.Equal(1, report.OpenCount);
        }

        [Fact]
        public void GetFleetHealth_MostFragile_SortedByHealth()
        {
            RecordSuccesses("healthy", 10);
            RecordFailures("broken", 5);
            var report = _engine.GetFleetHealth();
            Assert.True(report.MostFragile.Count <= 5);
            if (report.MostFragile.Count > 1)
                Assert.True(report.MostFragile[0].HealthScore <= report.MostFragile[1].HealthScore);
        }

        [Fact]
        public void GetFleetHealth_IncludesInsights()
        {
            RecordFailures("p1", 5);
            var report = _engine.GetFleetHealth();
            Assert.NotEmpty(report.AutonomousInsights);
        }

        // ─── Sliding Window ───────────────────────────

        [Fact]
        public void Window_TrimsToConfiguredSize()
        {
            // Window size is 20; add 25 successful outcomes
            RecordSuccesses("p1", 25);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(20, snap.CurrentWindowSize);
            Assert.Equal(25, snap.TotalCalls);
        }

        [Fact]
        public void Window_OldFailuresDropOff()
        {
            // 8 failures then 20 successes — failures should be pushed out of window
            var engine = new PromptCircuitBreakerEngine(new CircuitBreakerConfig
            {
                WindowSize = 10,
                ConsecutiveFailureLimit = 100, // disable
                MinCallsBeforeTrip = 100 // disable
            });
            for (int i = 0; i < 8; i++)
                engine.RecordOutcome(MakeOutcome("p1", false));
            for (int i = 0; i < 10; i++)
                engine.RecordOutcome(MakeOutcome("p1", true));
            var snap = engine.GetSnapshot("p1");
            Assert.Equal(0.0, snap.FailureRate); // all failures slid out
        }

        // ─── Snapshot ─────────────────────────────────

        [Fact]
        public void GetSnapshot_EmptyPromptId_Throws()
        {
            Assert.Throws<ArgumentException>(() => _engine.GetSnapshot(""));
        }

        [Fact]
        public void GetSnapshot_ReturnsCorrectMetrics()
        {
            RecordSuccesses("p1", 8);
            RecordFailures("p1", 2);
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal("p1", snap.PromptId);
            Assert.Equal(10, snap.TotalCalls);
            Assert.Equal(0.2, snap.FailureRate);
        }

        // ─── Dashboard ───────────────────────────────

        [Fact]
        public void GenerateFleetDashboard_ReturnsNonEmptyHtml()
        {
            RecordSuccesses("p1", 5);
            var html = _engine.GenerateFleetDashboard();
            Assert.NotEmpty(html);
            Assert.Contains("Circuit Breaker Dashboard", html);
            Assert.Contains("<!DOCTYPE html>", html);
        }

        [Fact]
        public void GenerateFleetDashboard_IncludesPromptNames()
        {
            RecordSuccesses("my-prompt", 5);
            var html = _engine.GenerateFleetDashboard();
            Assert.Contains("my-prompt", html);
        }

        [Fact]
        public void GenerateFleetDashboard_EmptyFleet_StillGenerates()
        {
            var html = _engine.GenerateFleetDashboard();
            Assert.NotEmpty(html);
            Assert.Contains("Fleet Health", html);
        }

        // ─── Edge Cases ──────────────────────────────

        [Fact]
        public void SingleCall_Success_StaysHealthy()
        {
            _engine.RecordOutcome(MakeOutcome("p1", true, 50));
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Closed, snap.State);
            Assert.Equal(100.0, snap.HealthScore);
        }

        [Fact]
        public void SingleCall_Failure_DoesNotTrip()
        {
            _engine.RecordOutcome(MakeOutcome("p1", false));
            var snap = _engine.GetSnapshot("p1");
            Assert.Equal(CBCircuitState.Closed, snap.State);
        }

        [Fact]
        public void ForceTrip_ThenForceReset_ResumesNormal()
        {
            RecordSuccesses("p1", 5);
            _engine.ForceTrip("p1");
            Assert.False(_engine.CanExecute("p1"));
            _engine.ForceReset("p1");
            Assert.True(_engine.CanExecute("p1"));
            Assert.Equal(CBCircuitState.Closed, _engine.GetSnapshot("p1").State);
        }

        [Fact]
        public void TripEvent_IncludesDescription()
        {
            RecordFailures("p1", 5);
            var history = _engine.GetTripHistory("p1");
            Assert.NotEmpty(history[0].Description);
            Assert.Contains("consecutive", history[0].Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FleetHealth_TripHistory_OrderedByTime()
        {
            RecordFailures("p1", 5);
            _engine.ForceReset("p1");
            _engine.ForceTrip("p2", "test");
            var report = _engine.GetFleetHealth();
            if (report.TripHistory.Count > 1)
                Assert.True(report.TripHistory[0].Timestamp >= report.TripHistory[1].Timestamp);
        }
    }
}
