namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptTriageEngine – Autonomous Incident Triage for Prompts
    //
    //  Ingests failure/quality signals, deduplicates similar
    //  incidents, prioritizes by blast radius and urgency,
    //  generates investigation plans, and tracks resolution.
    //  Think of it as a mini incident-response system for
    //  prompt engineering operations.
    //
    //  Unlike PromptCircuitBreakerEngine (real-time execution
    //  gating) or PromptRiskForecaster (forward-looking risk),
    //  this engine focuses on post-hoc triage, investigation
    //  planning, and resolution tracking across the prompt fleet.
    // ────────────────────────────────────────────

    /// <summary>Severity level of a triage incident.</summary>
    public enum TriageSeverity
    {
        /// <summary>Service-impacting, requires immediate action.</summary>
        Critical,
        /// <summary>Significant issue, needs prompt attention.</summary>
        High,
        /// <summary>Moderate issue, should be scheduled.</summary>
        Medium,
        /// <summary>Minor issue, can be batched.</summary>
        Low,
        /// <summary>Informational signal, no action required.</summary>
        Info
    }

    /// <summary>Category of the triage signal/incident.</summary>
    public enum TriageCategory
    {
        /// <summary>Sudden spike in failure rate.</summary>
        FailureSpike,
        /// <summary>Latency exceeding acceptable thresholds.</summary>
        LatencyDegradation,
        /// <summary>Output quality drifting from baseline.</summary>
        QualityDrift,
        /// <summary>Unexpected cost increase.</summary>
        CostAnomaly,
        /// <summary>Token usage exceeding budget/limits.</summary>
        TokenOverflow,
        /// <summary>Outputs showing hallucination patterns.</summary>
        HallucinationSignal,
        /// <summary>Policy or compliance rule violated.</summary>
        ComplianceViolation,
        /// <summary>Upstream dependency unavailable or degraded.</summary>
        DependencyFailure
    }

    /// <summary>Current status of a triage incident.</summary>
    public enum IncidentStatus
    {
        /// <summary>Newly created, awaiting investigation.</summary>
        Open,
        /// <summary>Actively being investigated.</summary>
        Investigating,
        /// <summary>Root cause found, mitigation in progress.</summary>
        Mitigating,
        /// <summary>Issue resolved and verified.</summary>
        Resolved,
        /// <summary>Determined to be non-actionable.</summary>
        Dismissed
    }

    /// <summary>Urgency classification for scheduling response.</summary>
    public enum TriageUrgency
    {
        /// <summary>Drop everything — respond now.</summary>
        Immediate,
        /// <summary>Respond within the hour.</summary>
        Urgent,
        /// <summary>Schedule for next available slot.</summary>
        Scheduled,
        /// <summary>Address when convenient.</summary>
        Backlog
    }

    /// <summary>An input signal that may create or update an incident.</summary>
    public class TriageSignal
    {
        /// <summary>Identifier of the prompt that emitted the signal.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>When the signal was observed.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Category of the signal.</summary>
        public TriageCategory Category { get; set; }

        /// <summary>Human-readable description of the signal.</summary>
        public string Description { get; set; } = "";

        /// <summary>Initial severity assessment.</summary>
        public TriageSeverity Severity { get; set; } = TriageSeverity.Medium;

        /// <summary>Optional numeric measurement (latency ms, cost $, etc.).</summary>
        public double? NumericValue { get; set; }

        /// <summary>Free-form tags for correlation.</summary>
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>A deduplicated, tracked incident.</summary>
    public class TriageIncident
    {
        /// <summary>Unique incident identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>Primary prompt associated with this incident.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Incident category.</summary>
        public TriageCategory Category { get; set; }

        /// <summary>Current severity level.</summary>
        public TriageSeverity Severity { get; set; }

        /// <summary>Current lifecycle status.</summary>
        public IncidentStatus Status { get; set; } = IncidentStatus.Open;

        /// <summary>Response urgency.</summary>
        public TriageUrgency Urgency { get; set; } = TriageUrgency.Scheduled;

        /// <summary>Number of signals folded into this incident.</summary>
        public int SignalCount { get; set; } = 1;

        /// <summary>When the first signal was observed.</summary>
        public DateTime FirstSeen { get; set; }

        /// <summary>When the most recent signal was observed.</summary>
        public DateTime LastSeen { get; set; }

        /// <summary>Blast radius score 0–100.</summary>
        public double BlastRadius { get; set; }

        /// <summary>Consolidated description.</summary>
        public string Description { get; set; } = "";

        /// <summary>Generated investigation steps.</summary>
        public List<string> InvestigationPlan { get; set; } = new();

        /// <summary>Other prompts affected by this incident.</summary>
        public List<string> AffectedPrompts { get; set; } = new();

        /// <summary>Notes recorded during resolution.</summary>
        public string? ResolutionNotes { get; set; }

        /// <summary>When the incident was resolved.</summary>
        public DateTime? ResolvedAt { get; set; }
    }

    /// <summary>Configuration for the triage engine.</summary>
    public class TriageConfig
    {
        /// <summary>Window for deduplicating signals (minutes).</summary>
        public int DeduplicationWindowMinutes { get; set; } = 30;

        /// <summary>Signal count threshold for auto-escalation.</summary>
        public int AutoEscalateThreshold { get; set; } = 5;

        /// <summary>Whether urgency rules are active.</summary>
        public bool UrgencyRulesEnabled { get; set; } = true;
    }

    /// <summary>Fleet-wide triage report.</summary>
    public class TriageReport
    {
        /// <summary>Total incident count.</summary>
        public int TotalIncidents { get; set; }

        /// <summary>Count of Open incidents.</summary>
        public int OpenCount { get; set; }

        /// <summary>Count of Investigating incidents.</summary>
        public int InvestigatingCount { get; set; }

        /// <summary>Count of Resolved incidents.</summary>
        public int ResolvedCount { get; set; }

        /// <summary>Count of Dismissed incidents.</summary>
        public int DismissedCount { get; set; }

        /// <summary>Incident counts by category.</summary>
        public Dictionary<TriageCategory, int> ByCategory { get; set; } = new();

        /// <summary>Incident counts by severity.</summary>
        public Dictionary<TriageSeverity, int> BySeverity { get; set; } = new();

        /// <summary>Top incidents ranked by blast radius.</summary>
        public List<TriageIncident> TopIncidents { get; set; } = new();

        /// <summary>Mean time to resolve in minutes (resolved incidents only).</summary>
        public double MeanTimeToResolveMinutes { get; set; }

        /// <summary>Overall fleet health score 0–100.</summary>
        public double HealthScore { get; set; }

        /// <summary>Autonomous insights about fleet triage state.</summary>
        public List<string> AutonomousInsights { get; set; } = new();
    }

    /// <summary>
    /// Autonomous incident triage engine for prompt operations.
    /// Ingests signals, deduplicates into incidents, computes blast radius,
    /// generates investigation plans, and tracks resolution lifecycle.
    /// </summary>
    public class PromptTriageEngine
    {
        private readonly TriageConfig _config;
        private readonly List<TriageIncident> _incidents = new();

        /// <summary>Create a triage engine with optional configuration.</summary>
        public PromptTriageEngine(TriageConfig? config = null)
        {
            _config = config ?? new TriageConfig();
        }

        /// <summary>Ingest a signal, creating or updating an incident.</summary>
        public TriageIncident IngestSignal(TriageSignal signal)
        {
            if (signal == null) throw new ArgumentNullException(nameof(signal));
            if (string.IsNullOrWhiteSpace(signal.PromptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(signal));

            // Deduplication: match by PromptId + Category within window
            var cutoff = signal.Timestamp.AddMinutes(-_config.DeduplicationWindowMinutes);
            var existing = _incidents.FirstOrDefault(i =>
                i.PromptId == signal.PromptId &&
                i.Category == signal.Category &&
                i.Status != IncidentStatus.Resolved &&
                i.Status != IncidentStatus.Dismissed &&
                i.LastSeen >= cutoff);

            if (existing != null)
            {
                existing.SignalCount++;
                existing.LastSeen = signal.Timestamp;
                if (signal.Severity < existing.Severity) // lower enum = higher severity
                    existing.Severity = signal.Severity;
                existing.BlastRadius = ComputeBlastRadius(existing);
                existing.Urgency = DeriveUrgency(existing);
                return existing;
            }

            // New incident
            var incident = new TriageIncident
            {
                PromptId = signal.PromptId,
                Category = signal.Category,
                Severity = signal.Severity,
                FirstSeen = signal.Timestamp,
                LastSeen = signal.Timestamp,
                Description = signal.Description,
                InvestigationPlan = GenerateInvestigationPlan(signal.Category),
            };
            incident.BlastRadius = ComputeBlastRadius(incident);
            incident.Urgency = DeriveUrgency(incident);
            _incidents.Add(incident);
            return incident;
        }

        /// <summary>Get an incident by its unique identifier.</summary>
        public TriageIncident? GetIncident(string incidentId)
        {
            if (string.IsNullOrWhiteSpace(incidentId))
                throw new ArgumentException("Incident ID cannot be empty.", nameof(incidentId));
            return _incidents.FirstOrDefault(i => i.Id == incidentId);
        }

        /// <summary>Get all incidents for a specific prompt.</summary>
        public List<TriageIncident> GetIncidentsByPrompt(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));
            return _incidents.Where(i => i.PromptId == promptId).ToList();
        }

        /// <summary>Get all open (non-Resolved, non-Dismissed) incidents sorted by blast radius descending.</summary>
        public List<TriageIncident> GetOpenIncidents()
        {
            return _incidents
                .Where(i => i.Status != IncidentStatus.Resolved && i.Status != IncidentStatus.Dismissed)
                .OrderByDescending(i => i.BlastRadius)
                .ToList();
        }

        /// <summary>Transition an incident to a new status.</summary>
        public void UpdateStatus(string incidentId, IncidentStatus newStatus, string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(incidentId))
                throw new ArgumentException("Incident ID cannot be empty.", nameof(incidentId));

            var incident = _incidents.FirstOrDefault(i => i.Id == incidentId)
                ?? throw new KeyNotFoundException($"Incident '{incidentId}' not found.");

            incident.Status = newStatus;
            if (notes != null) incident.ResolutionNotes = notes;
            if (newStatus == IncidentStatus.Resolved)
                incident.ResolvedAt = DateTime.UtcNow;
        }

        /// <summary>Dismiss an incident as non-actionable.</summary>
        public void DismissIncident(string incidentId, string reason)
        {
            if (string.IsNullOrWhiteSpace(incidentId))
                throw new ArgumentException("Incident ID cannot be empty.", nameof(incidentId));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be empty.", nameof(reason));

            var incident = _incidents.FirstOrDefault(i => i.Id == incidentId)
                ?? throw new KeyNotFoundException($"Incident '{incidentId}' not found.");

            incident.Status = IncidentStatus.Dismissed;
            incident.ResolutionNotes = reason;
        }

        /// <summary>Escalate an incident's severity by one level.</summary>
        public void EscalateIncident(string incidentId)
        {
            if (string.IsNullOrWhiteSpace(incidentId))
                throw new ArgumentException("Incident ID cannot be empty.", nameof(incidentId));

            var incident = _incidents.FirstOrDefault(i => i.Id == incidentId)
                ?? throw new KeyNotFoundException($"Incident '{incidentId}' not found.");

            if (incident.Severity > TriageSeverity.Critical)
            {
                incident.Severity = (TriageSeverity)((int)incident.Severity - 1);
                incident.BlastRadius = ComputeBlastRadius(incident);
                incident.Urgency = DeriveUrgency(incident);
            }
        }

        /// <summary>Compute blast radius score 0–100 for an incident.</summary>
        public double ComputeBlastRadius(TriageIncident incident)
        {
            if (incident == null) throw new ArgumentNullException(nameof(incident));

            // Severity weight: Critical=40, High=30, Medium=20, Low=10, Info=5
            double severityWeight = incident.Severity switch
            {
                TriageSeverity.Critical => 40,
                TriageSeverity.High => 30,
                TriageSeverity.Medium => 20,
                TriageSeverity.Low => 10,
                TriageSeverity.Info => 5,
                _ => 10
            };

            // Signal frequency: SignalCount * 3, capped at 30
            double frequencyScore = Math.Min(incident.SignalCount * 3.0, 30);

            // Category weight
            double categoryWeight = incident.Category switch
            {
                TriageCategory.FailureSpike => 15,
                TriageCategory.ComplianceViolation => 15,
                TriageCategory.HallucinationSignal => 12,
                TriageCategory.CostAnomaly => 10,
                _ => 8
            };

            // Duration factor: minutes since first seen / 60, capped at 15
            double durationMinutes = (incident.LastSeen - incident.FirstSeen).TotalMinutes;
            double durationFactor = Math.Min(durationMinutes / 60.0 * 15.0, 15);

            double total = severityWeight + frequencyScore + categoryWeight + durationFactor;
            return Math.Round(Math.Min(total, 100), 1);
        }

        /// <summary>Run autonomous triage: auto-escalate, recalculate, generate insights.</summary>
        public TriageReport AutoTriage()
        {
            foreach (var incident in _incidents.Where(i =>
                i.Status != IncidentStatus.Resolved && i.Status != IncidentStatus.Dismissed))
            {
                // Auto-escalate if signal count exceeds threshold
                if (incident.SignalCount >= _config.AutoEscalateThreshold &&
                    incident.Severity > TriageSeverity.Critical)
                {
                    incident.Severity = (TriageSeverity)((int)incident.Severity - 1);
                }

                incident.BlastRadius = ComputeBlastRadius(incident);
                incident.Urgency = DeriveUrgency(incident);
            }

            return GetTriageReport();
        }

        /// <summary>Build a fleet-wide triage report with autonomous insights.</summary>
        public TriageReport GetTriageReport()
        {
            var report = new TriageReport
            {
                TotalIncidents = _incidents.Count,
                OpenCount = _incidents.Count(i => i.Status == IncidentStatus.Open),
                InvestigatingCount = _incidents.Count(i => i.Status == IncidentStatus.Investigating),
                ResolvedCount = _incidents.Count(i => i.Status == IncidentStatus.Resolved),
                DismissedCount = _incidents.Count(i => i.Status == IncidentStatus.Dismissed),
            };

            // By category
            foreach (TriageCategory cat in Enum.GetValues(typeof(TriageCategory)))
            {
                var count = _incidents.Count(i => i.Category == cat);
                if (count > 0) report.ByCategory[cat] = count;
            }

            // By severity
            foreach (TriageSeverity sev in Enum.GetValues(typeof(TriageSeverity)))
            {
                var count = _incidents.Count(i => i.Severity == sev);
                if (count > 0) report.BySeverity[sev] = count;
            }

            // Top incidents by blast radius
            report.TopIncidents = _incidents
                .OrderByDescending(i => i.BlastRadius)
                .Take(10)
                .ToList();

            // MTTR
            var resolved = _incidents.Where(i =>
                i.Status == IncidentStatus.Resolved && i.ResolvedAt.HasValue).ToList();
            if (resolved.Count > 0)
            {
                report.MeanTimeToResolveMinutes = Math.Round(
                    resolved.Average(i => (i.ResolvedAt!.Value - i.FirstSeen).TotalMinutes), 1);
            }

            // Health score: start at 100, deduct per open incident by severity
            double deductions = 0;
            foreach (var i in _incidents.Where(i =>
                i.Status != IncidentStatus.Resolved && i.Status != IncidentStatus.Dismissed))
            {
                deductions += i.Severity switch
                {
                    TriageSeverity.Critical => 20,
                    TriageSeverity.High => 12,
                    TriageSeverity.Medium => 6,
                    TriageSeverity.Low => 3,
                    TriageSeverity.Info => 1,
                    _ => 3
                };
            }
            report.HealthScore = Math.Round(Math.Max(0, 100 - deductions), 1);

            // Autonomous insights
            report.AutonomousInsights = GenerateInsights(report);

            return report;
        }

        /// <summary>Generate an investigation plan for a given category.</summary>
        public List<string> GenerateInvestigationPlan(TriageCategory category)
        {
            return category switch
            {
                TriageCategory.FailureSpike => new List<string>
                {
                    "Check error logs for the affected prompt",
                    "Review recent prompt template changes",
                    "Verify upstream API availability",
                    "Check rate limit status",
                    "Compare with baseline failure rate",
                    "Test prompt in isolation with known-good inputs"
                },
                TriageCategory.LatencyDegradation => new List<string>
                {
                    "Profile token count of recent requests",
                    "Check model endpoint response times",
                    "Review context window utilization",
                    "Inspect prompt chain for sequential bottlenecks",
                    "Compare latency across model versions"
                },
                TriageCategory.QualityDrift => new List<string>
                {
                    "Run quality evaluation suite against recent outputs",
                    "Compare output distributions with golden baseline",
                    "Check for model version changes upstream",
                    "Review system prompt for unintended modifications",
                    "Audit few-shot examples for staleness"
                },
                TriageCategory.CostAnomaly => new List<string>
                {
                    "Audit token usage per request",
                    "Check for prompt expansion or context bloat",
                    "Review model routing decisions",
                    "Identify retry storms inflating costs",
                    "Compare cost-per-call with historical average"
                },
                TriageCategory.TokenOverflow => new List<string>
                {
                    "Measure average input and output token counts",
                    "Identify prompts exceeding context window limits",
                    "Review context compression effectiveness",
                    "Check for recursive or self-referential expansions",
                    "Evaluate chunking strategy for long inputs"
                },
                TriageCategory.HallucinationSignal => new List<string>
                {
                    "Review output validator results",
                    "Check context window utilization",
                    "Verify grounding data freshness",
                    "Test with reduced temperature setting",
                    "Compare outputs across model versions",
                    "Inspect retrieval-augmented context for relevance"
                },
                TriageCategory.ComplianceViolation => new List<string>
                {
                    "Identify which compliance rule was triggered",
                    "Review output sanitizer configuration",
                    "Check if policy rules were recently modified",
                    "Audit prompt for injection vulnerability",
                    "Verify content filter thresholds"
                },
                TriageCategory.DependencyFailure => new List<string>
                {
                    "Check upstream service health endpoints",
                    "Review dependency timeout configurations",
                    "Verify API key validity and quotas",
                    "Test fallback chain activation",
                    "Check network connectivity to external services"
                },
                _ => new List<string> { "Investigate the reported signal" }
            };
        }

        /// <summary>Generate an interactive HTML dashboard for triage state.</summary>
        public string GenerateDashboard()
        {
            var report = GetTriageReport();
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
            sb.AppendLine("<title>Prompt Triage Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:'Segoe UI',system-ui,sans-serif;background:#0f172a;color:#e2e8f0;padding:24px}");
            sb.AppendLine("h1{font-size:1.8rem;margin-bottom:8px;color:#f8fafc}");
            sb.AppendLine(".subtitle{color:#94a3b8;margin-bottom:24px}");
            sb.AppendLine(".cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:16px;margin-bottom:32px}");
            sb.AppendLine(".card{background:#1e293b;border-radius:12px;padding:20px;text-align:center}");
            sb.AppendLine(".card .value{font-size:2rem;font-weight:700;margin:8px 0}");
            sb.AppendLine(".card .label{color:#94a3b8;font-size:0.85rem;text-transform:uppercase;letter-spacing:1px}");
            sb.AppendLine(".green{color:#22c55e}.red{color:#ef4444}.yellow{color:#eab308}.blue{color:#3b82f6}.orange{color:#f97316}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:32px}");
            sb.AppendLine("th{background:#1e293b;padding:12px 16px;text-align:left;font-size:0.8rem;text-transform:uppercase;letter-spacing:1px;color:#94a3b8}");
            sb.AppendLine("td{padding:12px 16px;border-bottom:1px solid #1e293b}");
            sb.AppendLine("tr:hover{background:#1e293b55}");
            sb.AppendLine(".badge{display:inline-block;padding:4px 10px;border-radius:9999px;font-size:0.75rem;font-weight:600}");
            sb.AppendLine(".badge-critical{background:#7f1d1d;color:#fca5a5}.badge-high{background:#78350f;color:#fbbf24}");
            sb.AppendLine(".badge-medium{background:#1e3a5f;color:#93c5fd}.badge-low{background:#14532d;color:#86efac}");
            sb.AppendLine(".badge-info{background:#334155;color:#94a3b8}");
            sb.AppendLine(".badge-open{background:#1e3a5f;color:#60a5fa}.badge-investigating{background:#78350f;color:#fbbf24}");
            sb.AppendLine(".badge-mitigating{background:#4c1d95;color:#c4b5fd}.badge-resolved{background:#14532d;color:#86efac}");
            sb.AppendLine(".badge-dismissed{background:#334155;color:#94a3b8}");
            sb.AppendLine(".section{margin-bottom:32px}");
            sb.AppendLine(".section h2{font-size:1.2rem;margin-bottom:16px;color:#f8fafc}");
            sb.AppendLine(".insight{background:#1e293b;border-left:4px solid #3b82f6;padding:12px 16px;margin-bottom:8px;border-radius:0 8px 8px 0}");
            sb.AppendLine(".bar-container{display:flex;height:24px;border-radius:8px;overflow:hidden;margin-bottom:16px}");
            sb.AppendLine(".bar-segment{display:flex;align-items:center;justify-content:center;font-size:0.7rem;font-weight:600;min-width:30px}");
            sb.AppendLine("</style></head><body>");

            // Header
            sb.AppendLine("<h1>🚨 Prompt Triage Dashboard</h1>");
            sb.AppendLine($"<p class='subtitle'>Fleet Health: {report.HealthScore}/100 · {report.TotalIncidents} total incidents · Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>");

            // Summary cards
            sb.AppendLine("<div class='cards'>");
            AppendCard(sb, report.TotalIncidents.ToString(), "Total", "blue");
            AppendCard(sb, report.OpenCount.ToString(), "Open", "red");
            AppendCard(sb, report.InvestigatingCount.ToString(), "Investigating", "yellow");
            AppendCard(sb, report.ResolvedCount.ToString(), "Resolved", "green");
            string healthColor = report.HealthScore >= 80 ? "green" : report.HealthScore >= 50 ? "yellow" : "red";
            AppendCard(sb, report.HealthScore.ToString("F0"), "Health", healthColor);
            if (report.MeanTimeToResolveMinutes > 0)
                AppendCard(sb, $"{report.MeanTimeToResolveMinutes:F0}m", "MTTR", "orange");
            sb.AppendLine("</div>");

            // Severity breakdown bar
            if (report.BySeverity.Count > 0)
            {
                sb.AppendLine("<div class='section'><h2>Severity Distribution</h2>");
                sb.AppendLine("<div class='bar-container'>");
                var sevColors = new Dictionary<TriageSeverity, string>
                {
                    { TriageSeverity.Critical, "#ef4444" },
                    { TriageSeverity.High, "#f97316" },
                    { TriageSeverity.Medium, "#3b82f6" },
                    { TriageSeverity.Low, "#22c55e" },
                    { TriageSeverity.Info, "#64748b" }
                };
                int total = report.BySeverity.Values.Sum();
                foreach (var kvp in report.BySeverity.OrderBy(k => k.Key))
                {
                    double pct = total > 0 ? kvp.Value * 100.0 / total : 0;
                    string color = sevColors.GetValueOrDefault(kvp.Key, "#64748b");
                    sb.AppendLine($"<div class='bar-segment' style='width:{pct:F1}%;background:{color}'>{kvp.Key} ({kvp.Value})</div>");
                }
                sb.AppendLine("</div></div>");
            }

            // Top incidents table
            if (report.TopIncidents.Count > 0)
            {
                sb.AppendLine("<div class='section'><h2>Top Incidents (by Blast Radius)</h2>");
                sb.AppendLine("<table><thead><tr><th>Prompt</th><th>Category</th><th>Severity</th><th>Status</th><th>Signals</th><th>Blast Radius</th><th>Urgency</th></tr></thead><tbody>");
                foreach (var inc in report.TopIncidents)
                {
                    string sevBadge = $"badge-{inc.Severity.ToString().ToLowerInvariant()}";
                    string statusBadge = $"badge-{inc.Status.ToString().ToLowerInvariant()}";
                    sb.AppendLine($"<tr><td>{HtmlEncode(inc.PromptId)}</td><td>{inc.Category}</td>");
                    sb.AppendLine($"<td><span class='badge {sevBadge}'>{inc.Severity}</span></td>");
                    sb.AppendLine($"<td><span class='badge {statusBadge}'>{inc.Status}</span></td>");
                    sb.AppendLine($"<td>{inc.SignalCount}</td><td>{inc.BlastRadius:F1}</td><td>{inc.Urgency}</td></tr>");
                }
                sb.AppendLine("</tbody></table></div>");
            }

            // Category distribution
            if (report.ByCategory.Count > 0)
            {
                sb.AppendLine("<div class='section'><h2>Category Distribution</h2>");
                sb.AppendLine("<div class='cards'>");
                foreach (var kvp in report.ByCategory.OrderByDescending(k => k.Value))
                {
                    AppendCard(sb, kvp.Value.ToString(), kvp.Key.ToString(), "blue");
                }
                sb.AppendLine("</div></div>");
            }

            // Insights
            if (report.AutonomousInsights.Count > 0)
            {
                sb.AppendLine("<div class='section'><h2>🤖 Autonomous Insights</h2>");
                foreach (var insight in report.AutonomousInsights)
                {
                    sb.AppendLine($"<div class='insight'>{HtmlEncode(insight)}</div>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ── Private helpers ──────────────────────────────

        private TriageUrgency DeriveUrgency(TriageIncident incident)
        {
            if (!_config.UrgencyRulesEnabled) return TriageUrgency.Scheduled;

            return incident.Severity switch
            {
                TriageSeverity.Critical => TriageUrgency.Immediate,
                TriageSeverity.High => TriageUrgency.Urgent,
                TriageSeverity.Medium => TriageUrgency.Scheduled,
                _ => TriageUrgency.Backlog
            };
        }

        private List<string> GenerateInsights(TriageReport report)
        {
            var insights = new List<string>();
            int total = report.TotalIncidents;
            if (total == 0)
            {
                insights.Add("✅ No incidents recorded — fleet is clean.");
                return insights;
            }

            // Dominant category
            if (report.ByCategory.Count > 0)
            {
                var top = report.ByCategory.OrderByDescending(k => k.Value).First();
                if (top.Value > total * 0.5)
                {
                    insights.Add($"⚠️ {top.Key} dominates — {top.Value}/{total} incidents. Systemic root cause likely.");
                }
            }

            // Critical blast radius
            var critical = _incidents.Where(i => i.BlastRadius > 80).ToList();
            if (critical.Count > 0)
            {
                foreach (var c in critical.Take(3))
                {
                    insights.Add($"🔴 Critical blast radius ({c.BlastRadius:F0}) on '{c.PromptId}' — immediate attention required.");
                }
            }

            // MTTR warning
            if (report.MeanTimeToResolveMinutes > 60)
            {
                insights.Add($"⏱️ Mean time to resolve is {report.MeanTimeToResolveMinutes:F0} min — consider automated remediation.");
            }

            // Open incident volume
            int openActive = report.OpenCount + report.InvestigatingCount;
            if (openActive > 10)
            {
                insights.Add($"📈 {openActive} active incidents — triage team may be overwhelmed.");
            }

            // Health commentary
            if (report.HealthScore >= 90)
                insights.Add("💚 Fleet health is excellent — proactive posture maintained.");
            else if (report.HealthScore >= 70)
                insights.Add("💛 Fleet health is good but has room for improvement.");
            else if (report.HealthScore >= 50)
                insights.Add("🟠 Fleet health is degraded — prioritize critical incidents.");
            else
                insights.Add("🔴 Fleet health is critical — immediate intervention recommended.");

            // Severity distribution
            if (report.BySeverity.ContainsKey(TriageSeverity.Critical))
            {
                int critCount = report.BySeverity[TriageSeverity.Critical];
                if (critCount > 1)
                    insights.Add($"🚨 {critCount} critical-severity incidents active — escalation procedures should be engaged.");
            }

            // Unresolved duration
            var longRunning = _incidents.Where(i =>
                i.Status != IncidentStatus.Resolved &&
                i.Status != IncidentStatus.Dismissed &&
                (DateTime.UtcNow - i.FirstSeen).TotalHours > 2).ToList();
            if (longRunning.Count > 0)
            {
                insights.Add($"⏳ {longRunning.Count} incident(s) open for >2 hours — review for stale investigations.");
            }

            return insights;
        }

        private static void AppendCard(StringBuilder sb, string value, string label, string colorClass)
        {
            sb.AppendLine($"<div class='card'><div class='label'>{label}</div><div class='value {colorClass}'>{value}</div></div>");
        }

        private static string HtmlEncode(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
