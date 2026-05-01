namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptCircuitBreakerEngine – Autonomous Circuit Breaker for Prompts
    //
    //  Implements the circuit breaker pattern for prompt execution.
    //  Tracks success/failure outcomes per prompt, autonomously
    //  transitions between Closed→Open→HalfOpen states, detects
    //  failure cascades, blocks failing prompts during cooldown,
    //  and self-tests recovery with limited probe calls.
    //
    //  Unlike PromptRiskForecaster (forward-looking risk) or
    //  PromptMetabolismEngine (token efficiency), this engine
    //  focuses on real-time execution health and autonomous
    //  failure isolation to protect downstream systems.
    // ────────────────────────────────────────────

    /// <summary>Current state of a prompt's circuit breaker.</summary>
    public enum CBCircuitState
    {
        /// <summary>Normal operation — calls flow through, failures are counted.</summary>
        Closed,
        /// <summary>Circuit tripped — calls are blocked during cooldown.</summary>
        Open,
        /// <summary>Probing recovery — limited test calls allowed.</summary>
        HalfOpen
    }

    /// <summary>Reason a circuit was tripped.</summary>
    public enum TripReason
    {
        /// <summary>Failure rate exceeded threshold.</summary>
        FailureThreshold,
        /// <summary>Average latency exceeded threshold.</summary>
        LatencyThreshold,
        /// <summary>Too many consecutive failures.</summary>
        ConsecutiveFailures,
        /// <summary>Burst of errors in a short window.</summary>
        ErrorBurstRate,
        /// <summary>Health score dropped below critical level.</summary>
        HealthScoreDrop,
        /// <summary>Manually forced by external kill switch.</summary>
        ManualTrip
    }

    /// <summary>Result of recovery probing in HalfOpen state.</summary>
    public enum RecoveryVerdict
    {
        /// <summary>All probes succeeded — circuit can close.</summary>
        FullyRecovered,
        /// <summary>Some probes succeeded but below threshold.</summary>
        PartialRecovery,
        /// <summary>All probes failed — circuit re-opens.</summary>
        StillFailing,
        /// <summary>Not enough probe results yet.</summary>
        InsufficientData
    }

    /// <summary>Health classification for a circuit.</summary>
    public enum CircuitHealthTier
    {
        /// <summary>Health 90–100 — excellent condition.</summary>
        Pristine,
        /// <summary>Health 70–89 — good, minor issues.</summary>
        Healthy,
        /// <summary>Health 50–69 — noticeable degradation.</summary>
        Degraded,
        /// <summary>Health 20–49 — serious problems.</summary>
        Critical,
        /// <summary>Circuit is in Open state.</summary>
        Tripped
    }

    /// <summary>A single call outcome for a prompt.</summary>
    public class CallOutcome
    {
        public string PromptId { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; }
        public double LatencyMs { get; set; }
        public string? ErrorCategory { get; set; }
    }

    /// <summary>Point-in-time snapshot of a circuit's state.</summary>
    public class CircuitSnapshot
    {
        public string PromptId { get; set; } = "";
        public CBCircuitState State { get; set; }
        public double HealthScore { get; set; }
        public double FailureRate { get; set; }
        public double AvgLatencyMs { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int TotalCalls { get; set; }
        public int TripCount { get; set; }
        public DateTime? LastTripTime { get; set; }
        public TripReason? LastTripReason { get; set; }
        public int CurrentWindowSize { get; set; }
    }

    /// <summary>Record of a circuit trip event.</summary>
    public class TripEvent
    {
        public string PromptId { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public TripReason Reason { get; set; }
        public double FailureRate { get; set; }
        public int ConsecutiveFailures { get; set; }
        public double AvgLatencyMs { get; set; }
        public string Description { get; set; } = "";
    }

    /// <summary>Recovery assessment for a HalfOpen circuit.</summary>
    public class RecoveryReport
    {
        public string PromptId { get; set; } = "";
        public RecoveryVerdict Verdict { get; set; }
        public List<bool> ProbeResults { get; set; } = new();
        public double ProbeSuccessRate { get; set; }
        public TimeSpan TimeSinceTrip { get; set; }
        public string Recommendation { get; set; } = "";
    }

    /// <summary>Fleet-wide circuit breaker health report.</summary>
    public class FleetHealthReport
    {
        public int TotalCircuits { get; set; }
        public int ClosedCount { get; set; }
        public int OpenCount { get; set; }
        public int HalfOpenCount { get; set; }
        public double OverallHealthScore { get; set; }
        public List<CircuitSnapshot> MostFragile { get; set; } = new();
        public List<TripEvent> TripHistory { get; set; } = new();
        public List<string> AutonomousInsights { get; set; } = new();
    }

    /// <summary>Configuration for the circuit breaker engine.</summary>
    public class CircuitBreakerConfig
    {
        public double FailureThreshold { get; set; } = 0.5;
        public int ConsecutiveFailureLimit { get; set; } = 5;
        public double LatencyThresholdMs { get; set; } = 5000;
        public double SlowCallThreshold { get; set; } = 0.3;
        public int WindowSize { get; set; } = 20;
        public double CooldownSeconds { get; set; } = 60;
        public int HalfOpenMaxProbes { get; set; } = 3;
        public double HalfOpenSuccessThreshold { get; set; } = 0.67;
        public int MinCallsBeforeTrip { get; set; } = 10;
    }

    /// <summary>Internal per-prompt circuit state.</summary>
    internal class CircuitData
    {
        public string PromptId { get; set; } = "";
        public CBCircuitState State { get; set; } = CBCircuitState.Closed;
        public List<CallOutcome> Window { get; set; } = new();
        public int ConsecutiveFailures { get; set; }
        public int TotalCalls { get; set; }
        public int TripCount { get; set; }
        public DateTime? LastTripTime { get; set; }
        public TripReason? LastTripReason { get; set; }
        public List<TripEvent> TripHistory { get; set; } = new();
        public List<bool> HalfOpenProbes { get; set; } = new();
        public DateTime? HalfOpenStartTime { get; set; }
    }

    /// <summary>
    /// Autonomous circuit breaker engine for prompt execution.
    /// Monitors call outcomes, detects failure cascades, and manages
    /// Closed→Open→HalfOpen state transitions per prompt.
    /// </summary>
    public class PromptCircuitBreakerEngine
    {
        private readonly CircuitBreakerConfig _config;
        private readonly Dictionary<string, CircuitData> _circuits = new();

        public PromptCircuitBreakerEngine(CircuitBreakerConfig? config = null)
        {
            _config = config ?? new CircuitBreakerConfig();
        }

        /// <summary>Record a call outcome and evaluate circuit state.</summary>
        public void RecordOutcome(CallOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (string.IsNullOrWhiteSpace(outcome.PromptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(outcome));

            var circuit = GetOrCreate(outcome.PromptId);
            circuit.TotalCalls++;

            // In HalfOpen, record as probe
            if (circuit.State == CBCircuitState.HalfOpen)
            {
                circuit.HalfOpenProbes.Add(outcome.Success);
                circuit.Window.Add(outcome);
                TrimWindow(circuit);

                if (outcome.Success)
                    circuit.ConsecutiveFailures = 0;
                else
                    circuit.ConsecutiveFailures++;

                // Evaluate recovery after enough probes
                if (circuit.HalfOpenProbes.Count >= _config.HalfOpenMaxProbes)
                {
                    var successRate = circuit.HalfOpenProbes.Count(p => p) / (double)circuit.HalfOpenProbes.Count;
                    if (successRate >= _config.HalfOpenSuccessThreshold)
                    {
                        // Recovery succeeded — close circuit
                        circuit.State = CBCircuitState.Closed;
                        circuit.HalfOpenProbes.Clear();
                        circuit.HalfOpenStartTime = null;
                    }
                    else
                    {
                        // Recovery failed — re-open
                        TripCircuit(circuit, TripReason.FailureThreshold,
                            $"Recovery failed: probe success rate {successRate:P0} below threshold {_config.HalfOpenSuccessThreshold:P0}");
                    }
                }
                return;
            }

            // Normal (Closed) recording
            circuit.Window.Add(outcome);
            TrimWindow(circuit);

            if (outcome.Success)
                circuit.ConsecutiveFailures = 0;
            else
                circuit.ConsecutiveFailures++;

            // Evaluate trip conditions
            EvaluateTrip(circuit);
        }

        /// <summary>Check if a prompt can execute (not blocked by open circuit).</summary>
        public bool CanExecute(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId)) return true;
            if (!_circuits.ContainsKey(promptId)) return true;

            var circuit = _circuits[promptId];

            if (circuit.State == CBCircuitState.Closed) return true;

            if (circuit.State == CBCircuitState.Open)
            {
                // Check cooldown
                if (circuit.LastTripTime.HasValue)
                {
                    var elapsed = (DateTime.UtcNow - circuit.LastTripTime.Value).TotalSeconds;
                    if (elapsed >= _config.CooldownSeconds)
                    {
                        // Transition to HalfOpen
                        circuit.State = CBCircuitState.HalfOpen;
                        circuit.HalfOpenProbes.Clear();
                        circuit.HalfOpenStartTime = DateTime.UtcNow;
                        return true;
                    }
                }
                return false;
            }

            // HalfOpen — allow if probes not exhausted
            if (circuit.State == CBCircuitState.HalfOpen)
            {
                return circuit.HalfOpenProbes.Count < _config.HalfOpenMaxProbes;
            }

            return false;
        }

        /// <summary>Get a snapshot of a prompt's circuit state.</summary>
        public CircuitSnapshot GetSnapshot(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var circuit = GetOrCreate(promptId);
            return BuildSnapshot(circuit);
        }

        /// <summary>Get fleet-wide health report.</summary>
        public FleetHealthReport GetFleetHealth()
        {
            var report = new FleetHealthReport
            {
                TotalCircuits = _circuits.Count,
                ClosedCount = _circuits.Values.Count(c => c.State == CBCircuitState.Closed),
                OpenCount = _circuits.Values.Count(c => c.State == CBCircuitState.Open),
                HalfOpenCount = _circuits.Values.Count(c => c.State == CBCircuitState.HalfOpen),
            };

            var snapshots = _circuits.Values.Select(BuildSnapshot).ToList();

            report.OverallHealthScore = snapshots.Count > 0
                ? Math.Round(snapshots.Average(s => s.HealthScore), 1)
                : 100.0;

            report.MostFragile = snapshots
                .OrderBy(s => s.HealthScore)
                .Take(5)
                .ToList();

            report.TripHistory = _circuits.Values
                .SelectMany(c => c.TripHistory)
                .OrderByDescending(t => t.Timestamp)
                .Take(50)
                .ToList();

            report.AutonomousInsights = GenerateInsights(report, snapshots);

            return report;
        }

        /// <summary>Get trip history for a prompt.</summary>
        public List<TripEvent> GetTripHistory(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            if (!_circuits.ContainsKey(promptId)) return new List<TripEvent>();
            return new List<TripEvent>(_circuits[promptId].TripHistory);
        }

        /// <summary>Get recovery report for a HalfOpen circuit.</summary>
        public RecoveryReport? GetRecoveryReport(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId)) return null;
            if (!_circuits.ContainsKey(promptId)) return null;

            var circuit = _circuits[promptId];
            if (circuit.State != CBCircuitState.HalfOpen) return null;

            var probeCount = circuit.HalfOpenProbes.Count;
            var successCount = circuit.HalfOpenProbes.Count(p => p);
            var successRate = probeCount > 0 ? successCount / (double)probeCount : 0;
            var timeSinceTrip = circuit.LastTripTime.HasValue
                ? DateTime.UtcNow - circuit.LastTripTime.Value
                : TimeSpan.Zero;

            RecoveryVerdict verdict;
            string recommendation;

            if (probeCount < _config.HalfOpenMaxProbes)
            {
                verdict = RecoveryVerdict.InsufficientData;
                recommendation = $"Need {_config.HalfOpenMaxProbes - probeCount} more probe(s) to evaluate recovery.";
            }
            else if (successRate >= _config.HalfOpenSuccessThreshold)
            {
                verdict = RecoveryVerdict.FullyRecovered;
                recommendation = "Circuit recovered — safe to resume normal operation.";
            }
            else if (successCount > 0)
            {
                verdict = RecoveryVerdict.PartialRecovery;
                recommendation = $"Partial recovery ({successRate:P0}) — investigate intermittent failures before resuming.";
            }
            else
            {
                verdict = RecoveryVerdict.StillFailing;
                recommendation = "No recovery detected — root cause likely unresolved. Consider prompt revision.";
            }

            return new RecoveryReport
            {
                PromptId = promptId,
                Verdict = verdict,
                ProbeResults = new List<bool>(circuit.HalfOpenProbes),
                ProbeSuccessRate = Math.Round(successRate, 3),
                TimeSinceTrip = timeSinceTrip,
                Recommendation = recommendation
            };
        }

        /// <summary>Manually trip a circuit (external kill switch).</summary>
        public void ForceTrip(string promptId, string reason = "Manual trip")
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var circuit = GetOrCreate(promptId);
            TripCircuit(circuit, TripReason.ManualTrip, reason);
        }

        /// <summary>Manually reset a circuit to Closed state.</summary>
        public void ForceReset(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var circuit = GetOrCreate(promptId);
            circuit.State = CBCircuitState.Closed;
            circuit.ConsecutiveFailures = 0;
            circuit.HalfOpenProbes.Clear();
            circuit.HalfOpenStartTime = null;
        }

        /// <summary>Get health tier for a prompt's circuit.</summary>
        public CircuitHealthTier GetHealthTier(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var circuit = GetOrCreate(promptId);
            if (circuit.State == CBCircuitState.Open) return CircuitHealthTier.Tripped;

            var score = ComputeHealthScore(circuit);
            if (score >= 90) return CircuitHealthTier.Pristine;
            if (score >= 70) return CircuitHealthTier.Healthy;
            if (score >= 50) return CircuitHealthTier.Degraded;
            return CircuitHealthTier.Critical;
        }

        /// <summary>Get total recorded calls for a prompt.</summary>
        public int GetCallCount(string promptId)
        {
            if (!_circuits.ContainsKey(promptId)) return 0;
            return _circuits[promptId].TotalCalls;
        }

        /// <summary>Generate an interactive HTML dashboard.</summary>
        public string GenerateFleetDashboard()
        {
            var fleet = GetFleetHealth();
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
            sb.AppendLine("<title>Prompt Circuit Breaker Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:'Segoe UI',system-ui,sans-serif;background:#0f172a;color:#e2e8f0;padding:24px}");
            sb.AppendLine("h1{font-size:1.8rem;margin-bottom:8px;color:#f8fafc}");
            sb.AppendLine(".subtitle{color:#94a3b8;margin-bottom:24px}");
            sb.AppendLine(".cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:16px;margin-bottom:32px}");
            sb.AppendLine(".card{background:#1e293b;border-radius:12px;padding:20px;text-align:center}");
            sb.AppendLine(".card .value{font-size:2rem;font-weight:700;margin:8px 0}");
            sb.AppendLine(".card .label{color:#94a3b8;font-size:0.85rem;text-transform:uppercase;letter-spacing:1px}");
            sb.AppendLine(".green{color:#22c55e}.red{color:#ef4444}.yellow{color:#eab308}.blue{color:#3b82f6}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:32px}");
            sb.AppendLine("th{background:#1e293b;padding:12px 16px;text-align:left;font-size:0.8rem;text-transform:uppercase;letter-spacing:1px;color:#94a3b8}");
            sb.AppendLine("td{padding:12px 16px;border-bottom:1px solid #1e293b}");
            sb.AppendLine("tr:hover{background:#1e293b44}");
            sb.AppendLine(".badge{padding:4px 12px;border-radius:999px;font-size:0.75rem;font-weight:600}");
            sb.AppendLine(".badge-closed{background:#16a34a22;color:#22c55e;border:1px solid #22c55e44}");
            sb.AppendLine(".badge-open{background:#dc262622;color:#ef4444;border:1px solid #ef444444}");
            sb.AppendLine(".badge-halfopen{background:#ca8a0422;color:#eab308;border:1px solid #eab30844}");
            sb.AppendLine(".health-bar{background:#334155;border-radius:8px;height:20px;overflow:hidden;min-width:120px}");
            sb.AppendLine(".health-fill{height:100%;border-radius:8px;transition:width 0.3s}");
            sb.AppendLine(".insight{background:#1e293b;border-left:4px solid #3b82f6;padding:12px 16px;margin:8px 0;border-radius:0 8px 8px 0}");
            sb.AppendLine(".section{margin-bottom:32px}");
            sb.AppendLine(".section h2{font-size:1.2rem;margin-bottom:16px;color:#f8fafc}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<h1>⚡ Prompt Circuit Breaker Dashboard</h1>");
            sb.AppendLine("<p class='subtitle'>Autonomous failure isolation and recovery monitoring</p>");

            // Fleet overview cards
            sb.AppendLine("<div class='cards'>");
            sb.AppendLine($"<div class='card'><div class='label'>Total Circuits</div><div class='value blue'>{fleet.TotalCircuits}</div></div>");
            sb.AppendLine($"<div class='card'><div class='label'>Closed</div><div class='value green'>{fleet.ClosedCount}</div></div>");
            sb.AppendLine($"<div class='card'><div class='label'>Open</div><div class='value red'>{fleet.OpenCount}</div></div>");
            sb.AppendLine($"<div class='card'><div class='label'>Half-Open</div><div class='value yellow'>{fleet.HalfOpenCount}</div></div>");
            var healthColor = fleet.OverallHealthScore >= 70 ? "green" : fleet.OverallHealthScore >= 50 ? "yellow" : "red";
            sb.AppendLine($"<div class='card'><div class='label'>Fleet Health</div><div class='value {healthColor}'>{fleet.OverallHealthScore:F0}</div></div>");
            sb.AppendLine("</div>");

            // Circuit table
            if (_circuits.Count > 0)
            {
                sb.AppendLine("<div class='section'><h2>Circuit Status</h2>");
                sb.AppendLine("<table><tr><th>Prompt</th><th>State</th><th>Health</th><th>Failure Rate</th><th>Avg Latency</th><th>Trips</th><th>Calls</th></tr>");
                foreach (var snapshot in _circuits.Values.Select(BuildSnapshot).OrderBy(s => s.HealthScore))
                {
                    var badgeClass = snapshot.State switch
                    {
                        CBCircuitState.Closed => "badge-closed",
                        CBCircuitState.Open => "badge-open",
                        _ => "badge-halfopen"
                    };
                    var fillColor = snapshot.HealthScore >= 70 ? "#22c55e" : snapshot.HealthScore >= 50 ? "#eab308" : "#ef4444";
                    sb.AppendLine($"<tr><td>{Esc(snapshot.PromptId)}</td>");
                    sb.AppendLine($"<td><span class='badge {badgeClass}'>{snapshot.State}</span></td>");
                    sb.AppendLine($"<td><div class='health-bar'><div class='health-fill' style='width:{snapshot.HealthScore:F0}%;background:{fillColor}'></div></div> {snapshot.HealthScore:F0}</td>");
                    sb.AppendLine($"<td>{snapshot.FailureRate:P0}</td><td>{snapshot.AvgLatencyMs:F0}ms</td><td>{snapshot.TripCount}</td><td>{snapshot.TotalCalls}</td></tr>");
                }
                sb.AppendLine("</table></div>");
            }

            // Trip history
            if (fleet.TripHistory.Count > 0)
            {
                sb.AppendLine("<div class='section'><h2>Recent Trip Events</h2><table>");
                sb.AppendLine("<tr><th>Time</th><th>Prompt</th><th>Reason</th><th>Description</th></tr>");
                foreach (var trip in fleet.TripHistory.Take(20))
                {
                    sb.AppendLine($"<tr><td>{trip.Timestamp:u}</td><td>{Esc(trip.PromptId)}</td><td>{trip.Reason}</td><td>{Esc(trip.Description)}</td></tr>");
                }
                sb.AppendLine("</table></div>");
            }

            // Insights
            if (fleet.AutonomousInsights.Count > 0)
            {
                sb.AppendLine("<div class='section'><h2>🤖 Autonomous Insights</h2>");
                foreach (var insight in fleet.AutonomousInsights)
                    sb.AppendLine($"<div class='insight'>{Esc(insight)}</div>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ─── Internal helpers ─────────────────────────────────

        private CircuitData GetOrCreate(string promptId)
        {
            if (!_circuits.ContainsKey(promptId))
                _circuits[promptId] = new CircuitData { PromptId = promptId };
            return _circuits[promptId];
        }

        private void TrimWindow(CircuitData circuit)
        {
            while (circuit.Window.Count > _config.WindowSize)
                circuit.Window.RemoveAt(0);
        }

        private void EvaluateTrip(CircuitData circuit)
        {
            if (circuit.State != CBCircuitState.Closed) return;

            // Consecutive failure check (no minimum call requirement)
            if (circuit.ConsecutiveFailures >= _config.ConsecutiveFailureLimit)
            {
                TripCircuit(circuit, TripReason.ConsecutiveFailures,
                    $"{circuit.ConsecutiveFailures} consecutive failures (limit: {_config.ConsecutiveFailureLimit})");
                return;
            }

            // Need minimum calls for rate-based checks
            if (circuit.Window.Count < _config.MinCallsBeforeTrip) return;

            // Failure rate check
            var failureRate = circuit.Window.Count(o => !o.Success) / (double)circuit.Window.Count;
            if (failureRate > _config.FailureThreshold)
            {
                TripCircuit(circuit, TripReason.FailureThreshold,
                    $"Failure rate {failureRate:P0} exceeds threshold {_config.FailureThreshold:P0}");
                return;
            }

            // Slow call rate check
            var slowRate = circuit.Window.Count(o => o.LatencyMs > _config.LatencyThresholdMs) / (double)circuit.Window.Count;
            if (slowRate > _config.SlowCallThreshold)
            {
                TripCircuit(circuit, TripReason.LatencyThreshold,
                    $"Slow call rate {slowRate:P0} exceeds threshold {_config.SlowCallThreshold:P0}");
                return;
            }
        }

        private void TripCircuit(CircuitData circuit, TripReason reason, string description)
        {
            circuit.State = CBCircuitState.Open;
            circuit.TripCount++;
            circuit.LastTripTime = DateTime.UtcNow;
            circuit.LastTripReason = reason;
            circuit.HalfOpenProbes.Clear();
            circuit.HalfOpenStartTime = null;

            var failureRate = circuit.Window.Count > 0
                ? circuit.Window.Count(o => !o.Success) / (double)circuit.Window.Count
                : 0;
            var avgLatency = circuit.Window.Count > 0
                ? circuit.Window.Average(o => o.LatencyMs)
                : 0;

            circuit.TripHistory.Add(new TripEvent
            {
                PromptId = circuit.PromptId,
                Timestamp = DateTime.UtcNow,
                Reason = reason,
                FailureRate = Math.Round(failureRate, 3),
                ConsecutiveFailures = circuit.ConsecutiveFailures,
                AvgLatencyMs = Math.Round(avgLatency, 1),
                Description = description
            });
        }

        private double ComputeHealthScore(CircuitData circuit)
        {
            if (circuit.Window.Count == 0) return 100.0;

            var failureRate = circuit.Window.Count(o => !o.Success) / (double)circuit.Window.Count;
            var slowRate = circuit.Window.Count(o => o.LatencyMs > _config.LatencyThresholdMs) / (double)circuit.Window.Count;
            var score = 100.0 - (failureRate * 50) - (slowRate * 30) - (circuit.ConsecutiveFailures * 4);
            return Math.Round(Math.Clamp(score, 0, 100), 1);
        }

        private CircuitSnapshot BuildSnapshot(CircuitData circuit)
        {
            var failureRate = circuit.Window.Count > 0
                ? circuit.Window.Count(o => !o.Success) / (double)circuit.Window.Count
                : 0;
            var avgLatency = circuit.Window.Count > 0
                ? circuit.Window.Average(o => o.LatencyMs)
                : 0;

            return new CircuitSnapshot
            {
                PromptId = circuit.PromptId,
                State = circuit.State,
                HealthScore = ComputeHealthScore(circuit),
                FailureRate = Math.Round(failureRate, 3),
                AvgLatencyMs = Math.Round(avgLatency, 1),
                ConsecutiveFailures = circuit.ConsecutiveFailures,
                TotalCalls = circuit.TotalCalls,
                TripCount = circuit.TripCount,
                LastTripTime = circuit.LastTripTime,
                LastTripReason = circuit.LastTripReason,
                CurrentWindowSize = circuit.Window.Count
            };
        }

        private List<string> GenerateInsights(FleetHealthReport report, List<CircuitSnapshot> snapshots)
        {
            var insights = new List<string>();

            // Systemic issues
            if (report.TotalCircuits > 0)
            {
                var openPct = report.OpenCount / (double)report.TotalCircuits;
                if (openPct > 0.3)
                    insights.Add($"{report.OpenCount} of {report.TotalCircuits} circuits are Open — possible systemic failure affecting multiple prompts.");
            }

            // Fleet health assessment
            if (report.OverallHealthScore < 50)
                insights.Add($"Fleet health score is {report.OverallHealthScore:F0}/100 — Critical status. Immediate investigation recommended.");
            else if (report.OverallHealthScore < 70)
                insights.Add($"Fleet health score is {report.OverallHealthScore:F0}/100 — Degraded status. Monitor closely.");

            // Frequent trippers
            foreach (var snap in snapshots.Where(s => s.TripCount >= 3))
                insights.Add($"Circuit '{snap.PromptId}' has tripped {snap.TripCount} times — investigate root cause or consider prompt revision.");

            // Recovery observations
            foreach (var snap in snapshots.Where(s => s.State == CBCircuitState.HalfOpen))
                insights.Add($"Circuit '{snap.PromptId}' is probing recovery — monitor probe outcomes before resuming full traffic.");

            // High failure rate but not tripped (below MinCallsBeforeTrip)
            foreach (var snap in snapshots.Where(s => s.State == CBCircuitState.Closed && s.FailureRate > 0.4 && s.TotalCalls < _config.MinCallsBeforeTrip))
                insights.Add($"Circuit '{snap.PromptId}' has {snap.FailureRate:P0} failure rate but insufficient calls ({snap.TotalCalls}) to trip — early warning.");

            if (insights.Count == 0 && report.TotalCircuits > 0)
                insights.Add("All circuits operating normally. No anomalies detected.");

            return insights;
        }

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
