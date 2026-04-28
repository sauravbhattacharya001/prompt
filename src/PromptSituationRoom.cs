namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ────────────────────────────────────────────
    //  PromptSituationRoom – Autonomous Operational Command Center
    //
    //  Aggregates signals from multiple prompt monitoring dimensions
    //  (health, drift, cost, risk, staleness, complexity) into a
    //  unified situational awareness picture.  Auto-detects situations
    //  requiring attention, classifies urgency, maintains a watch list,
    //  and generates structured SITREPs with recommended actions.
    // ────────────────────────────────────────────

    /// <summary>Domain from which a monitoring signal originates.</summary>
    public enum SignalDomain
    {
        /// <summary>Overall prompt health and template quality.</summary>
        Health,
        /// <summary>Performance degradation over time.</summary>
        Drift,
        /// <summary>Cost and spending anomalies.</summary>
        Cost,
        /// <summary>Injection threats, compliance violations.</summary>
        Risk,
        /// <summary>Prompts overdue for refresh.</summary>
        Staleness,
        /// <summary>Overly complex or fragile prompts.</summary>
        Complexity
    }

    /// <summary>Type of detected situation.</summary>
    public enum SituationType
    {
        /// <summary>Multiple prompts failing health checks simultaneously.</summary>
        PortfolioHealthCrisis,
        /// <summary>Several prompts drifting at once (possible model update).</summary>
        DriftStorm,
        /// <summary>Sudden cost increase across the portfolio.</summary>
        CostSpike,
        /// <summary>Multiple injection or risk signals firing.</summary>
        SecurityBreach,
        /// <summary>Many prompts overdue for refresh.</summary>
        StalenessCascade,
        /// <summary>Average complexity trending upward.</summary>
        ComplexityCreep,
        /// <summary>Everything nominal — no action needed.</summary>
        QuietPeriod
    }

    /// <summary>Urgency level for a detected situation.</summary>
    public enum UrgencyLevel
    {
        /// <summary>Can wait for next review cycle.</summary>
        Routine,
        /// <summary>Should address within the day.</summary>
        Elevated,
        /// <summary>Needs attention within hours.</summary>
        High,
        /// <summary>Drop everything and handle now.</summary>
        Critical
    }

    /// <summary>Overall operational status of the prompt portfolio.</summary>
    public enum OperationalStatus
    {
        /// <summary>All systems nominal.</summary>
        Green,
        /// <summary>Minor issues detected.</summary>
        Yellow,
        /// <summary>Significant issues requiring attention.</summary>
        Orange,
        /// <summary>Critical situation — immediate action required.</summary>
        Red
    }

    // TrendDirection enum is defined in PromptUsageReport.cs and reused here.

    /// <summary>A single monitoring signal from any domain.</summary>
    public class SituationSignal
    {
        /// <summary>Gets the domain this signal belongs to.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SignalDomain Domain { get; }

        /// <summary>Gets the name of the prompt this signal is about.</summary>
        public string PromptName { get; }

        /// <summary>Gets the severity score (0.0 = informational, 1.0 = critical).</summary>
        public double Severity { get; }

        /// <summary>Gets the human-readable description of the signal.</summary>
        public string Description { get; }

        /// <summary>Gets the time the signal was observed.</summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>Creates a new situation signal.</summary>
        public SituationSignal(SignalDomain domain, string promptName, double severity, string description, DateTimeOffset? timestamp = null)
        {
            Domain = domain;
            PromptName = promptName ?? throw new ArgumentNullException(nameof(promptName));
            Severity = Math.Clamp(severity, 0.0, 1.0);
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        }
    }

    /// <summary>A recommended action to address a situation.</summary>
    public class SituationAction
    {
        /// <summary>Gets the priority (1 = highest).</summary>
        public int Priority { get; }

        /// <summary>Gets the action category (Immediate / Investigate / Prevent).</summary>
        public string Category { get; }

        /// <summary>Gets the action description.</summary>
        public string Description { get; }

        /// <summary>Creates a new action recommendation.</summary>
        public SituationAction(int priority, string category, string description)
        {
            Priority = priority;
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }

    /// <summary>A situation detected by the situation room.</summary>
    public class DetectedSituation
    {
        /// <summary>Gets the situation type.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SituationType Type { get; }

        /// <summary>Gets the urgency level.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UrgencyLevel Urgency { get; }

        /// <summary>Gets the prompts affected by this situation.</summary>
        public IReadOnlyList<string> AffectedPrompts { get; }

        /// <summary>Gets a human-readable situation description.</summary>
        public string Description { get; }

        /// <summary>Gets when this situation was detected.</summary>
        public DateTimeOffset DetectedAt { get; }

        /// <summary>Gets recommended actions for this situation.</summary>
        public IReadOnlyList<SituationAction> RecommendedActions { get; }

        /// <summary>Creates a new detected situation.</summary>
        public DetectedSituation(SituationType type, UrgencyLevel urgency, IReadOnlyList<string> affectedPrompts,
            string description, DateTimeOffset detectedAt, IReadOnlyList<SituationAction> recommendedActions)
        {
            Type = type;
            Urgency = urgency;
            AffectedPrompts = affectedPrompts ?? Array.Empty<string>();
            Description = description ?? throw new ArgumentNullException(nameof(description));
            DetectedAt = detectedAt;
            RecommendedActions = recommendedActions ?? Array.Empty<SituationAction>();
        }
    }

    /// <summary>An entry on the watch list for elevated monitoring.</summary>
    public class WatchListEntry
    {
        /// <summary>Gets the prompt name being watched.</summary>
        public string PromptName { get; }

        /// <summary>Gets the reason for watching.</summary>
        public string Reason { get; }

        /// <summary>Gets when this entry was added.</summary>
        public DateTimeOffset AddedAt { get; }

        /// <summary>Gets or sets the number of signals received while on the watch list.</summary>
        public int SignalCount { get; internal set; }

        /// <summary>Creates a new watch list entry.</summary>
        public WatchListEntry(string promptName, string reason, DateTimeOffset? addedAt = null)
        {
            PromptName = promptName ?? throw new ArgumentNullException(nameof(promptName));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            AddedAt = addedAt ?? DateTimeOffset.UtcNow;
            SignalCount = 0;
        }
    }

    /// <summary>A structured situation report (SITREP).</summary>
    public class SituationReport
    {
        /// <summary>Gets the current operational status.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OperationalStatus Status { get; }

        /// <summary>Gets all active situations.</summary>
        public IReadOnlyList<DetectedSituation> Situations { get; }

        /// <summary>Gets the overall trend direction.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TrendDirection Trend { get; }

        /// <summary>Gets the current watch list.</summary>
        public IReadOnlyList<WatchListEntry> WatchList { get; }

        /// <summary>Gets all recommended actions across situations, priority-sorted.</summary>
        public IReadOnlyList<SituationAction> Recommendations { get; }

        /// <summary>Gets when this report was generated.</summary>
        public DateTimeOffset GeneratedAt { get; }

        /// <summary>Gets the duration since the last all-clear state, or null if currently all-clear.</summary>
        public TimeSpan? TimeSinceAllClear { get; }

        /// <summary>Gets the total number of signals in the current assessment window.</summary>
        public int TotalSignals { get; }

        /// <summary>Gets the number of distinct prompts with signals.</summary>
        public int AffectedPromptCount { get; }

        /// <summary>Creates a new situation report.</summary>
        public SituationReport(OperationalStatus status, IReadOnlyList<DetectedSituation> situations,
            TrendDirection trend, IReadOnlyList<WatchListEntry> watchList,
            IReadOnlyList<SituationAction> recommendations, DateTimeOffset generatedAt,
            TimeSpan? timeSinceAllClear, int totalSignals, int affectedPromptCount)
        {
            Status = status;
            Situations = situations ?? Array.Empty<DetectedSituation>();
            Trend = trend;
            WatchList = watchList ?? Array.Empty<WatchListEntry>();
            Recommendations = recommendations ?? Array.Empty<SituationAction>();
            GeneratedAt = generatedAt;
            TimeSinceAllClear = timeSinceAllClear;
            TotalSignals = totalSignals;
            AffectedPromptCount = affectedPromptCount;
        }
    }

    /// <summary>
    /// Autonomous operational command center for prompt portfolio management.
    /// Aggregates signals, detects situations, classifies urgency, and generates SITREPs.
    /// </summary>
    public class PromptSituationRoom
    {
        private readonly List<SituationSignal> _signals = new();
        private readonly Dictionary<string, WatchListEntry> _watchList = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SituationSignal> _historicalSignals = new();
        private DateTimeOffset? _lastAllClear;
        private int _previousSignalCount;

        // ── Thresholds ──────────────────────────────────

        private const int CrisisThreshold = 3;           // min signals in a domain to trigger crisis
        private const double HighSeverityThreshold = 0.7; // severity above this is "high"
        private const double CriticalSeverityThreshold = 0.9;
        private const int MaxHistoricalSignals = 500;

        /// <summary>Creates a new situation room instance.</summary>
        public PromptSituationRoom()
        {
            _lastAllClear = DateTimeOffset.UtcNow;
        }

        // ── Signal Ingestion ────────────────────────────

        /// <summary>Ingest a single monitoring signal.</summary>
        public void IngestSignal(SituationSignal signal)
        {
            if (signal == null) throw new ArgumentNullException(nameof(signal));
            _signals.Add(signal);

            // Track watched prompts
            if (_watchList.TryGetValue(signal.PromptName, out var entry))
            {
                entry.SignalCount++;
            }
        }

        /// <summary>Ingest multiple monitoring signals.</summary>
        public void IngestSignals(IEnumerable<SituationSignal> signals)
        {
            if (signals == null) throw new ArgumentNullException(nameof(signals));
            foreach (var s in signals)
            {
                IngestSignal(s);
            }
        }

        // ── Situation Detection ─────────────────────────

        /// <summary>Gets all currently active situations based on ingested signals.</summary>
        public IReadOnlyList<DetectedSituation> GetActiveSituations()
        {
            var situations = new List<DetectedSituation>();
            var now = DateTimeOffset.UtcNow;

            var byDomain = _signals.GroupBy(s => s.Domain).ToDictionary(g => g.Key, g => g.ToList());

            // Portfolio Health Crisis
            if (byDomain.TryGetValue(SignalDomain.Health, out var healthSignals) && healthSignals.Count >= CrisisThreshold)
            {
                var prompts = healthSignals.Select(s => s.PromptName).Distinct().ToList();
                var avgSev = healthSignals.Average(s => s.Severity);
                situations.Add(new DetectedSituation(
                    SituationType.PortfolioHealthCrisis,
                    ClassifyUrgency(avgSev, healthSignals.Count),
                    prompts,
                    $"Portfolio health crisis: {healthSignals.Count} health signals across {prompts.Count} prompts (avg severity {avgSev:F2})",
                    now,
                    GenerateActions(SituationType.PortfolioHealthCrisis, prompts)));
            }

            // Drift Storm
            if (byDomain.TryGetValue(SignalDomain.Drift, out var driftSignals) && driftSignals.Count >= CrisisThreshold)
            {
                var prompts = driftSignals.Select(s => s.PromptName).Distinct().ToList();
                var avgSev = driftSignals.Average(s => s.Severity);
                situations.Add(new DetectedSituation(
                    SituationType.DriftStorm,
                    ClassifyUrgency(avgSev, driftSignals.Count),
                    prompts,
                    $"Drift storm detected: {prompts.Count} prompts drifting simultaneously — possible model update or data shift",
                    now,
                    GenerateActions(SituationType.DriftStorm, prompts)));
            }

            // Cost Spike
            if (byDomain.TryGetValue(SignalDomain.Cost, out var costSignals) && costSignals.Count >= 2)
            {
                var prompts = costSignals.Select(s => s.PromptName).Distinct().ToList();
                var maxSev = costSignals.Max(s => s.Severity);
                situations.Add(new DetectedSituation(
                    SituationType.CostSpike,
                    ClassifyUrgency(maxSev, costSignals.Count),
                    prompts,
                    $"Cost spike: {costSignals.Count} cost anomalies across {prompts.Count} prompts (max severity {maxSev:F2})",
                    now,
                    GenerateActions(SituationType.CostSpike, prompts)));
            }

            // Security Breach
            if (byDomain.TryGetValue(SignalDomain.Risk, out var riskSignals) && riskSignals.Count >= 2)
            {
                var prompts = riskSignals.Select(s => s.PromptName).Distinct().ToList();
                var maxSev = riskSignals.Max(s => s.Severity);
                situations.Add(new DetectedSituation(
                    SituationType.SecurityBreach,
                    ClassifyUrgency(Math.Min(maxSev + 0.2, 1.0), riskSignals.Count),
                    prompts,
                    $"Security alert: {riskSignals.Count} risk signals — possible injection attempts or compliance violations",
                    now,
                    GenerateActions(SituationType.SecurityBreach, prompts)));
            }

            // Staleness Cascade
            if (byDomain.TryGetValue(SignalDomain.Staleness, out var staleSignals) && staleSignals.Count >= CrisisThreshold)
            {
                var prompts = staleSignals.Select(s => s.PromptName).Distinct().ToList();
                var avgSev = staleSignals.Average(s => s.Severity);
                situations.Add(new DetectedSituation(
                    SituationType.StalenessCascade,
                    ClassifyUrgency(avgSev, staleSignals.Count),
                    prompts,
                    $"Staleness cascade: {prompts.Count} prompts overdue for refresh",
                    now,
                    GenerateActions(SituationType.StalenessCascade, prompts)));
            }

            // Complexity Creep
            if (byDomain.TryGetValue(SignalDomain.Complexity, out var complexSignals) && complexSignals.Count >= CrisisThreshold)
            {
                var prompts = complexSignals.Select(s => s.PromptName).Distinct().ToList();
                var avgSev = complexSignals.Average(s => s.Severity);
                situations.Add(new DetectedSituation(
                    SituationType.ComplexityCreep,
                    ClassifyUrgency(avgSev, complexSignals.Count),
                    prompts,
                    $"Complexity creep: average complexity rising across {prompts.Count} prompts",
                    now,
                    GenerateActions(SituationType.ComplexityCreep, prompts)));
            }

            // Quiet Period — no situations
            if (situations.Count == 0)
            {
                _lastAllClear = now;
                situations.Add(new DetectedSituation(
                    SituationType.QuietPeriod,
                    UrgencyLevel.Routine,
                    Array.Empty<string>(),
                    "All systems nominal — no situations detected",
                    now,
                    Array.Empty<SituationAction>()));
            }

            return situations;
        }

        // ── Status ──────────────────────────────────────

        /// <summary>Gets the current operational status based on ingested signals.</summary>
        public OperationalStatus GetCurrentStatus()
        {
            if (_signals.Count == 0) return OperationalStatus.Green;

            var maxSeverity = _signals.Max(s => s.Severity);
            var domainCount = _signals.Select(s => s.Domain).Distinct().Count();
            var criticalCount = _signals.Count(s => s.Severity >= CriticalSeverityThreshold);

            if (criticalCount >= 2 || (maxSeverity >= CriticalSeverityThreshold && domainCount >= 3))
                return OperationalStatus.Red;
            if (maxSeverity >= HighSeverityThreshold || domainCount >= 3)
                return OperationalStatus.Orange;
            if (_signals.Count >= 2 || maxSeverity >= 0.4)
                return OperationalStatus.Yellow;

            return OperationalStatus.Green;
        }

        // ── Watch List ──────────────────────────────────

        /// <summary>Add a prompt to the watch list for elevated monitoring.</summary>
        public void AddToWatchList(string promptName, string reason)
        {
            if (string.IsNullOrWhiteSpace(promptName)) throw new ArgumentException("Prompt name required.", nameof(promptName));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required.", nameof(reason));

            if (!_watchList.ContainsKey(promptName))
            {
                _watchList[promptName] = new WatchListEntry(promptName, reason);
            }
        }

        /// <summary>Remove a prompt from the watch list.</summary>
        public void RemoveFromWatchList(string promptName)
        {
            if (promptName != null)
                _watchList.Remove(promptName);
        }

        /// <summary>Gets the current watch list.</summary>
        public IReadOnlyList<WatchListEntry> GetWatchList()
        {
            return _watchList.Values.ToList();
        }

        // ── SITREP Generation ───────────────────────────

        /// <summary>Generate a full situation report.</summary>
        public SituationReport GenerateSitrep()
        {
            var now = DateTimeOffset.UtcNow;
            var situations = GetActiveSituations();
            var status = GetCurrentStatus();
            var trend = ComputeTrend();
            var watchList = GetWatchList();

            // Auto-add prompts with signals to watch list
            foreach (var signal in _signals)
            {
                if (!_watchList.ContainsKey(signal.PromptName) && signal.Severity >= HighSeverityThreshold)
                {
                    AddToWatchList(signal.PromptName, $"Auto-watch: {signal.Domain} signal (severity {signal.Severity:F2})");
                }
            }

            // Collect all recommendations, sorted by priority
            var allActions = situations
                .Where(s => s.Type != SituationType.QuietPeriod)
                .SelectMany(s => s.RecommendedActions)
                .OrderBy(a => a.Priority)
                .ToList();

            var timeSinceAllClear = (status == OperationalStatus.Green)
                ? (TimeSpan?)null
                : (_lastAllClear.HasValue ? now - _lastAllClear.Value : (TimeSpan?)null);

            var affectedCount = _signals.Select(s => s.PromptName).Distinct().Count();

            // Archive current signals for trend computation
            _historicalSignals.AddRange(_signals);
            if (_historicalSignals.Count > MaxHistoricalSignals)
            {
                _historicalSignals.RemoveRange(0, _historicalSignals.Count - MaxHistoricalSignals);
            }
            _previousSignalCount = _signals.Count;

            return new SituationReport(
                status, situations, trend,
                GetWatchList(), allActions, now,
                timeSinceAllClear, _signals.Count, affectedCount);
        }

        /// <summary>Clear all current signals for a new assessment cycle.</summary>
        public void ClearSignals()
        {
            _historicalSignals.AddRange(_signals);
            if (_historicalSignals.Count > MaxHistoricalSignals)
            {
                _historicalSignals.RemoveRange(0, _historicalSignals.Count - MaxHistoricalSignals);
            }
            _previousSignalCount = _signals.Count;
            _signals.Clear();
        }

        // ── Export ──────────────────────────────────────

        /// <summary>Export a human-readable text SITREP.</summary>
        public string ExportSitrepText()
        {
            var report = GenerateSitrep();
            var sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════╗");
            sb.AppendLine("║          PROMPT SITUATION ROOM — SITREP         ║");
            sb.AppendLine("╚══════════════════════════════════════════════════╝");
            sb.AppendLine();

            // Status banner
            var statusIcon = report.Status switch
            {
                OperationalStatus.Green => "🟢",
                OperationalStatus.Yellow => "🟡",
                OperationalStatus.Orange => "🟠",
                OperationalStatus.Red => "🔴",
                _ => "⚪"
            };
            sb.AppendLine($"  Status:  {statusIcon} {report.Status.ToString().ToUpperInvariant()}");
            sb.AppendLine($"  Trend:   {FormatTrend(report.Trend)}");
            sb.AppendLine($"  Signals: {report.TotalSignals} across {report.AffectedPromptCount} prompt(s)");
            if (report.TimeSinceAllClear.HasValue)
                sb.AppendLine($"  Time since all-clear: {FormatDuration(report.TimeSinceAllClear.Value)}");
            sb.AppendLine($"  Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine();

            // Situations
            sb.AppendLine("── Active Situations ─────────────────────────────");
            foreach (var sit in report.Situations)
            {
                var urgencyTag = sit.Urgency switch
                {
                    UrgencyLevel.Critical => "[CRITICAL]",
                    UrgencyLevel.High => "[HIGH]",
                    UrgencyLevel.Elevated => "[ELEVATED]",
                    _ => "[ROUTINE]"
                };
                sb.AppendLine($"  {urgencyTag} {sit.Type}: {sit.Description}");
                if (sit.AffectedPrompts.Count > 0)
                    sb.AppendLine($"    Affected: {string.Join(", ", sit.AffectedPrompts)}");
            }
            sb.AppendLine();

            // Watch list
            if (report.WatchList.Count > 0)
            {
                sb.AppendLine("── Watch List ────────────────────────────────────");
                foreach (var w in report.WatchList)
                {
                    sb.AppendLine($"  👁 {w.PromptName} — {w.Reason} (signals: {w.SignalCount})");
                }
                sb.AppendLine();
            }

            // Recommendations
            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine("── Recommended Actions ───────────────────────────");
                foreach (var action in report.Recommendations)
                {
                    sb.AppendLine($"  [{action.Priority}] ({action.Category}) {action.Description}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("══════════════════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>Export the SITREP as a JSON string.</summary>
        public string ExportSitrepJson()
        {
            var report = GenerateSitrep();
            return JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        // ── Private Helpers ─────────────────────────────

        private static UrgencyLevel ClassifyUrgency(double avgSeverity, int signalCount)
        {
            if (avgSeverity >= CriticalSeverityThreshold || signalCount >= 8)
                return UrgencyLevel.Critical;
            if (avgSeverity >= HighSeverityThreshold || signalCount >= 5)
                return UrgencyLevel.High;
            if (avgSeverity >= 0.4 || signalCount >= 3)
                return UrgencyLevel.Elevated;
            return UrgencyLevel.Routine;
        }

        private TrendDirection ComputeTrend()
        {
            if (_historicalSignals.Count == 0 && _signals.Count == 0)
                return TrendDirection.Insufficient;
            if (_historicalSignals.Count == 0)
                return TrendDirection.Insufficient;

            var currentAvg = _signals.Count > 0 ? _signals.Average(s => s.Severity) : 0.0;
            var historicalAvg = _historicalSignals.Average(s => s.Severity);

            if (_signals.Count < _previousSignalCount && currentAvg < historicalAvg - 0.1)
                return TrendDirection.Increasing;
            if (_signals.Count > _previousSignalCount && currentAvg > historicalAvg + 0.1)
                return TrendDirection.Decreasing;
            return TrendDirection.Stable;
        }

        private static IReadOnlyList<SituationAction> GenerateActions(SituationType type, IReadOnlyList<string> prompts)
        {
            var actions = new List<SituationAction>();
            var promptList = prompts.Count <= 3
                ? string.Join(", ", prompts)
                : $"{string.Join(", ", prompts.Take(3))} (+{prompts.Count - 3} more)";

            switch (type)
            {
                case SituationType.PortfolioHealthCrisis:
                    actions.Add(new SituationAction(1, "Immediate", $"Run full health check on affected prompts: {promptList}"));
                    actions.Add(new SituationAction(2, "Investigate", "Check for recent template changes or dependency updates"));
                    actions.Add(new SituationAction(3, "Prevent", "Set up automated health checks on CI pipeline"));
                    break;

                case SituationType.DriftStorm:
                    actions.Add(new SituationAction(1, "Immediate", "Check for recent model version changes or API updates"));
                    actions.Add(new SituationAction(2, "Investigate", $"Compare baseline vs current performance for: {promptList}"));
                    actions.Add(new SituationAction(3, "Prevent", "Implement model version pinning and drift baselines"));
                    break;

                case SituationType.CostSpike:
                    actions.Add(new SituationAction(1, "Immediate", $"Review token usage for: {promptList}"));
                    actions.Add(new SituationAction(2, "Investigate", "Check for prompt expansion, retry storms, or loop conditions"));
                    actions.Add(new SituationAction(3, "Prevent", "Set per-prompt cost budgets and alerts"));
                    break;

                case SituationType.SecurityBreach:
                    actions.Add(new SituationAction(1, "Immediate", $"Quarantine affected prompts and review inputs: {promptList}"));
                    actions.Add(new SituationAction(2, "Investigate", "Analyze injection patterns and attack vectors"));
                    actions.Add(new SituationAction(3, "Prevent", "Strengthen input sanitization and add injection detection"));
                    break;

                case SituationType.StalenessCascade:
                    actions.Add(new SituationAction(1, "Immediate", $"Schedule prompt refresh for: {promptList}"));
                    actions.Add(new SituationAction(2, "Investigate", "Determine if stale prompts are still effective"));
                    actions.Add(new SituationAction(3, "Prevent", "Implement automatic freshness tracking with refresh reminders"));
                    break;

                case SituationType.ComplexityCreep:
                    actions.Add(new SituationAction(1, "Immediate", $"Review and simplify: {promptList}"));
                    actions.Add(new SituationAction(2, "Investigate", "Identify unnecessary complexity added in recent changes"));
                    actions.Add(new SituationAction(3, "Prevent", "Set complexity budgets and require simplification reviews"));
                    break;
            }

            return actions;
        }

        private static string FormatTrend(TrendDirection trend) => trend switch
        {
            TrendDirection.Increasing => "↗ Improving",
            TrendDirection.Stable => "→ Stable",
            TrendDirection.Decreasing => "↘ Degrading",
            _ => "? Unknown"
        };

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalDays >= 1) return $"{ts.Days}d {ts.Hours}h";
            if (ts.TotalHours >= 1) return $"{ts.Hours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }
    }
}
