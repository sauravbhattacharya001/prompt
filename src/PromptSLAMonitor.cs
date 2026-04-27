namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptSLAMonitor – Autonomous SLA Compliance Engine
    //
    //  Define service-level agreements (SLAs) for prompt quality,
    //  latency, cost, and error rate. The monitor autonomously tracks
    //  compliance, detects breaches, classifies violation patterns,
    //  forecasts SLA budget exhaustion, and recommends remediation.
    //
    //  Key capabilities:
    //  • Multi-metric SLA definitions with per-prompt overrides
    //  • Rolling-window compliance percentage tracking
    //  • Breach severity escalation (warning → violation → critical)
    //  • Error budget tracking with burn-rate calculation
    //  • Autonomous pattern detection (time-of-day, model-specific)
    //  • SLA forecast: predicts when error budget will exhaust
    //  • Multi-format export (JSON, Markdown, HTML dashboard)
    // ────────────────────────────────────────────

    /// <summary>Which metric an SLA target applies to.</summary>
    public enum SLAMetric
    {
        /// <summary>Quality/effectiveness score (0.0–1.0, higher is better).</summary>
        Quality,
        /// <summary>Response latency in milliseconds (lower is better).</summary>
        Latency,
        /// <summary>Cost per invocation in USD (lower is better).</summary>
        Cost,
        /// <summary>Error rate as fraction 0.0–1.0 (lower is better).</summary>
        ErrorRate,
        /// <summary>Token usage count (lower is better).</summary>
        TokenUsage
    }

    /// <summary>How the SLA threshold is evaluated.</summary>
    public enum SLADirection
    {
        /// <summary>Actual value must be ≥ threshold (quality).</summary>
        AtLeast,
        /// <summary>Actual value must be ≤ threshold (latency, cost).</summary>
        AtMost
    }

    /// <summary>Severity of an SLA breach.</summary>
    public enum SLABreachSeverity
    {
        /// <summary>Within target — no breach.</summary>
        Compliant,
        /// <summary>Within 10% of threshold — early warning.</summary>
        Warning,
        /// <summary>Threshold exceeded.</summary>
        Violation,
        /// <summary>Threshold exceeded by &gt;50% of tolerance.</summary>
        Critical
    }

    /// <summary>Export format for SLA reports.</summary>
    public enum SLAExportFormat
    {
        Json,
        Markdown,
        Html
    }

    /// <summary>Defines a single SLA target for a metric.</summary>
    public sealed class SLATarget
    {
        /// <summary>Human-readable name for this SLA.</summary>
        public string Name { get; set; } = "";
        /// <summary>The metric being constrained.</summary>
        public SLAMetric Metric { get; set; }
        /// <summary>Threshold direction.</summary>
        public SLADirection Direction { get; set; }
        /// <summary>The threshold value.</summary>
        public double Threshold { get; set; }
        /// <summary>Target compliance percentage (e.g. 99.5 = 99.5%).</summary>
        public double TargetCompliancePercent { get; set; } = 99.0;
        /// <summary>Optional: only apply to this prompt ID. Null = global.</summary>
        public string? PromptId { get; set; }
    }

    /// <summary>A single invocation record for SLA tracking.</summary>
    public sealed class SLAObservation
    {
        public string PromptId { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double QualityScore { get; set; } = 1.0;
        public double LatencyMs { get; set; }
        public double CostUsd { get; set; }
        public double ErrorRate { get; set; }
        public int TokenCount { get; set; }
        public string ModelVersion { get; set; } = "";
        public bool IsError { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>Result of evaluating one SLA target against observations.</summary>
    public sealed class SLAComplianceResult
    {
        public SLATarget Target { get; set; } = new();
        public double CompliancePercent { get; set; }
        public SLABreachSeverity Severity { get; set; }
        public int TotalObservations { get; set; }
        public int CompliantCount { get; set; }
        public int ViolationCount { get; set; }
        public double ErrorBudgetRemainingPercent { get; set; }
        public double BurnRatePerDay { get; set; }
        public DateTime? EstimatedBudgetExhaustion { get; set; }
        public List<string> Patterns { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>Overall SLA health report across all targets.</summary>
    public sealed class SLAReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int WindowDays { get; set; }
        public int TotalObservations { get; set; }
        public string OverallHealth { get; set; } = "Healthy";
        public double OverallCompliancePercent { get; set; }
        public List<SLAComplianceResult> Results { get; set; } = new();
        public List<string> TopRecommendations { get; set; } = new();
    }

    /// <summary>
    /// Autonomous SLA compliance monitor for prompt portfolios.
    /// Records observations and evaluates them against defined SLA targets
    /// with breach detection, error-budget tracking, pattern analysis,
    /// and proactive recommendations.
    /// </summary>
    public sealed class PromptSLAMonitor
    {
        private readonly List<SLATarget> _targets = new();
        private readonly List<SLAObservation> _observations = new();
        private int _windowDays = 30;

        // ── Configuration ──────────────────────────

        /// <summary>Set the rolling window size in days (default 30).</summary>
        public PromptSLAMonitor WithWindow(int days)
        {
            if (days < 1) throw new ArgumentOutOfRangeException(nameof(days));
            _windowDays = days;
            return this;
        }

        /// <summary>Add an SLA target to monitor.</summary>
        public PromptSLAMonitor AddTarget(SLATarget target)
        {
            _targets.Add(target ?? throw new ArgumentNullException(nameof(target)));
            return this;
        }

        /// <summary>Add a quality SLA (score must be ≥ threshold).</summary>
        public PromptSLAMonitor AddQualitySLA(string name, double minScore, double compliancePct = 99.0, string? promptId = null) =>
            AddTarget(new SLATarget
            {
                Name = name,
                Metric = SLAMetric.Quality,
                Direction = SLADirection.AtLeast,
                Threshold = minScore,
                TargetCompliancePercent = compliancePct,
                PromptId = promptId
            });

        /// <summary>Add a latency SLA (must be ≤ threshold ms).</summary>
        public PromptSLAMonitor AddLatencySLA(string name, double maxMs, double compliancePct = 99.0, string? promptId = null) =>
            AddTarget(new SLATarget
            {
                Name = name,
                Metric = SLAMetric.Latency,
                Direction = SLADirection.AtMost,
                Threshold = maxMs,
                TargetCompliancePercent = compliancePct,
                PromptId = promptId
            });

        /// <summary>Add a cost SLA (must be ≤ threshold USD).</summary>
        public PromptSLAMonitor AddCostSLA(string name, double maxCost, double compliancePct = 99.0, string? promptId = null) =>
            AddTarget(new SLATarget
            {
                Name = name,
                Metric = SLAMetric.Cost,
                Direction = SLADirection.AtMost,
                Threshold = maxCost,
                TargetCompliancePercent = compliancePct,
                PromptId = promptId
            });

        /// <summary>Add an error-rate SLA (must be ≤ threshold fraction).</summary>
        public PromptSLAMonitor AddErrorRateSLA(string name, double maxRate, double compliancePct = 99.5, string? promptId = null) =>
            AddTarget(new SLATarget
            {
                Name = name,
                Metric = SLAMetric.ErrorRate,
                Direction = SLADirection.AtMost,
                Threshold = maxRate,
                TargetCompliancePercent = compliancePct,
                PromptId = promptId
            });

        // ── Observation Recording ──────────────────

        /// <summary>Record a prompt invocation observation.</summary>
        public PromptSLAMonitor Record(SLAObservation obs)
        {
            _observations.Add(obs ?? throw new ArgumentNullException(nameof(obs)));
            return this;
        }

        /// <summary>Record multiple observations.</summary>
        public PromptSLAMonitor RecordBatch(IEnumerable<SLAObservation> observations)
        {
            foreach (var obs in observations) Record(obs);
            return this;
        }

        /// <summary>Quick-record a prompt result.</summary>
        public PromptSLAMonitor Record(string promptId, double quality, double latencyMs,
            double costUsd = 0, double errorRate = 0, int tokens = 0, string model = "")
        {
            return Record(new SLAObservation
            {
                PromptId = promptId,
                QualityScore = quality,
                LatencyMs = latencyMs,
                CostUsd = costUsd,
                ErrorRate = errorRate,
                TokenCount = tokens,
                ModelVersion = model,
                IsError = errorRate > 0.5
            });
        }

        // ── Evaluation ─────────────────────────────

        /// <summary>Evaluate all SLA targets and produce a compliance report.</summary>
        public SLAReport Evaluate()
        {
            var cutoff = DateTime.UtcNow.AddDays(-_windowDays);
            var windowObs = _observations.Where(o => o.Timestamp >= cutoff).ToList();
            var report = new SLAReport
            {
                WindowDays = _windowDays,
                TotalObservations = windowObs.Count
            };

            if (windowObs.Count == 0)
            {
                report.OverallHealth = "No Data";
                report.TopRecommendations.Add("Record observations before evaluating SLAs.");
                return report;
            }

            foreach (var target in _targets)
            {
                var result = EvaluateTarget(target, windowObs);
                report.Results.Add(result);
            }

            // Overall compliance = average across all targets
            if (report.Results.Count > 0)
            {
                report.OverallCompliancePercent = report.Results.Average(r => r.CompliancePercent);
                var worstSeverity = report.Results.Max(r => r.Severity);
                report.OverallHealth = worstSeverity switch
                {
                    SLABreachSeverity.Compliant => "Healthy",
                    SLABreachSeverity.Warning => "At Risk",
                    SLABreachSeverity.Violation => "Degraded",
                    SLABreachSeverity.Critical => "Critical",
                    _ => "Unknown"
                };
            }

            // Aggregate top recommendations
            report.TopRecommendations = report.Results
                .Where(r => r.Severity >= SLABreachSeverity.Warning)
                .SelectMany(r => r.Recommendations)
                .Distinct()
                .Take(10)
                .ToList();

            if (report.TopRecommendations.Count == 0)
                report.TopRecommendations.Add("All SLAs compliant. Continue monitoring.");

            return report;
        }

        private SLAComplianceResult EvaluateTarget(SLATarget target, List<SLAObservation> observations)
        {
            // Filter by prompt ID if scoped
            var relevant = target.PromptId != null
                ? observations.Where(o => o.PromptId == target.PromptId).ToList()
                : observations;

            var result = new SLAComplianceResult
            {
                Target = target,
                TotalObservations = relevant.Count
            };

            if (relevant.Count == 0)
            {
                result.CompliancePercent = 100;
                result.Severity = SLABreachSeverity.Compliant;
                result.ErrorBudgetRemainingPercent = 100;
                return result;
            }

            // Check each observation against threshold
            var compliant = new List<SLAObservation>();
            var violating = new List<SLAObservation>();

            foreach (var obs in relevant)
            {
                double value = GetMetricValue(obs, target.Metric);
                bool passes = target.Direction == SLADirection.AtLeast
                    ? value >= target.Threshold
                    : value <= target.Threshold;

                if (passes) compliant.Add(obs);
                else violating.Add(obs);
            }

            result.CompliantCount = compliant.Count;
            result.ViolationCount = violating.Count;
            result.CompliancePercent = (double)compliant.Count / relevant.Count * 100.0;

            // Error budget: how much of the allowed failure budget remains
            double allowedFailurePercent = 100.0 - target.TargetCompliancePercent;
            double actualFailurePercent = 100.0 - result.CompliancePercent;
            result.ErrorBudgetRemainingPercent = allowedFailurePercent > 0
                ? Math.Max(0, (1.0 - actualFailurePercent / allowedFailurePercent) * 100.0)
                : (actualFailurePercent > 0 ? 0 : 100);

            // Burn rate: error budget consumed per day
            if (relevant.Count >= 2)
            {
                var span = (relevant.Max(o => o.Timestamp) - relevant.Min(o => o.Timestamp)).TotalDays;
                if (span > 0)
                {
                    double budgetConsumedPercent = 100.0 - result.ErrorBudgetRemainingPercent;
                    result.BurnRatePerDay = budgetConsumedPercent / span;

                    // Forecast exhaustion
                    if (result.BurnRatePerDay > 0 && result.ErrorBudgetRemainingPercent > 0)
                    {
                        double daysLeft = result.ErrorBudgetRemainingPercent / result.BurnRatePerDay;
                        result.EstimatedBudgetExhaustion = DateTime.UtcNow.AddDays(daysLeft);
                    }
                }
            }

            // Severity classification
            result.Severity = ClassifySeverity(result, target);

            // Pattern detection
            DetectPatterns(result, violating, relevant);

            // Recommendations
            GenerateRecommendations(result, target, violating);

            return result;
        }

        private static double GetMetricValue(SLAObservation obs, SLAMetric metric) => metric switch
        {
            SLAMetric.Quality => obs.QualityScore,
            SLAMetric.Latency => obs.LatencyMs,
            SLAMetric.Cost => obs.CostUsd,
            SLAMetric.ErrorRate => obs.ErrorRate,
            SLAMetric.TokenUsage => obs.TokenCount,
            _ => 0
        };

        private static SLABreachSeverity ClassifySeverity(SLAComplianceResult result, SLATarget target)
        {
            if (result.CompliancePercent >= target.TargetCompliancePercent)
            {
                // Check if we're close to the edge (within 10% of allowed failures)
                double margin = result.CompliancePercent - target.TargetCompliancePercent;
                double allowedFailure = 100.0 - target.TargetCompliancePercent;
                if (allowedFailure > 0 && margin < allowedFailure * 0.5)
                    return SLABreachSeverity.Warning;
                return SLABreachSeverity.Compliant;
            }

            double breach = target.TargetCompliancePercent - result.CompliancePercent;
            double allowedRange = 100.0 - target.TargetCompliancePercent;
            if (allowedRange > 0 && breach > allowedRange * 1.5)
                return SLABreachSeverity.Critical;

            return SLABreachSeverity.Violation;
        }

        private static void DetectPatterns(SLAComplianceResult result,
            List<SLAObservation> violating, List<SLAObservation> all)
        {
            if (violating.Count == 0) return;

            // Time-of-day pattern: check if violations cluster in certain hours
            var hourGroups = violating.GroupBy(v => v.Timestamp.Hour).OrderByDescending(g => g.Count()).ToList();
            if (hourGroups.Count > 0 && hourGroups[0].Count() >= 3)
            {
                var peakHours = hourGroups.TakeWhile(g => g.Count() >= hourGroups[0].Count() * 0.5)
                    .Select(g => $"{g.Key:00}:00").ToList();
                result.Patterns.Add($"Time-of-day cluster: violations peak at {string.Join(", ", peakHours)}");
            }

            // Model-specific pattern
            var modelGroups = violating.Where(v => !string.IsNullOrEmpty(v.ModelVersion))
                .GroupBy(v => v.ModelVersion).ToList();
            if (modelGroups.Count > 1)
            {
                var worstModel = modelGroups.OrderByDescending(g => g.Count()).First();
                double worstPct = (double)worstModel.Count() / violating.Count * 100;
                if (worstPct > 60)
                    result.Patterns.Add($"Model-specific: {worstPct:F0}% of violations from model '{worstModel.Key}'");
            }

            // Prompt-specific pattern
            var promptGroups = violating.GroupBy(v => v.PromptId).OrderByDescending(g => g.Count()).ToList();
            if (promptGroups.Count > 1 && promptGroups[0].Count() > violating.Count * 0.5)
            {
                result.Patterns.Add($"Prompt hotspot: '{promptGroups[0].Key}' accounts for {promptGroups[0].Count()}/{violating.Count} violations");
            }

            // Trend pattern: are violations accelerating?
            if (violating.Count >= 4)
            {
                var sorted = violating.OrderBy(v => v.Timestamp).ToList();
                int halfIdx = sorted.Count / 2;
                int firstHalf = halfIdx;
                int secondHalf = sorted.Count - halfIdx;
                if (secondHalf > firstHalf * 1.5)
                    result.Patterns.Add("Accelerating: violations increasing over time — possible drift");
            }

            // Burst pattern: multiple violations in a short period
            var sortedAll = violating.OrderBy(v => v.Timestamp).ToList();
            for (int i = 0; i < sortedAll.Count - 2; i++)
            {
                var window = sortedAll.Skip(i).TakeWhile(v =>
                    (v.Timestamp - sortedAll[i].Timestamp).TotalMinutes <= 30).ToList();
                if (window.Count >= 3)
                {
                    result.Patterns.Add($"Burst detected: {window.Count} violations within 30 min around {sortedAll[i].Timestamp:u}");
                    break;
                }
            }
        }

        private static void GenerateRecommendations(SLAComplianceResult result,
            SLATarget target, List<SLAObservation> violating)
        {
            if (result.Severity == SLABreachSeverity.Compliant && result.Severity != SLABreachSeverity.Warning)
                return;

            string metricName = target.Metric.ToString().ToLower();

            if (result.ErrorBudgetRemainingPercent < 20)
                result.Recommendations.Add($"⚠ Error budget nearly exhausted ({result.ErrorBudgetRemainingPercent:F1}% remaining) for {target.Name}. Immediate attention needed.");

            if (result.EstimatedBudgetExhaustion.HasValue)
            {
                var daysLeft = (result.EstimatedBudgetExhaustion.Value - DateTime.UtcNow).TotalDays;
                if (daysLeft < 7)
                    result.Recommendations.Add($"🔥 At current burn rate, error budget for '{target.Name}' exhausts in {daysLeft:F1} days.");
            }

            if (result.Patterns.Any(p => p.Contains("Model-specific")))
                result.Recommendations.Add($"Consider routing {metricName}-sensitive traffic away from the underperforming model variant.");

            if (result.Patterns.Any(p => p.Contains("Time-of-day")))
                result.Recommendations.Add($"Investigate infrastructure load or model provider issues during peak violation hours.");

            if (result.Patterns.Any(p => p.Contains("Burst")))
                result.Recommendations.Add("Burst violations suggest transient outages — add retry/fallback logic.");

            if (result.Patterns.Any(p => p.Contains("Accelerating")))
                result.Recommendations.Add($"Violation trend is accelerating for '{target.Name}'. Proactively tune prompts or adjust SLA thresholds.");

            if (target.Metric == SLAMetric.Latency && result.Severity >= SLABreachSeverity.Violation)
                result.Recommendations.Add("Latency SLA breach: consider prompt minification, caching, or switching to a faster model tier.");

            if (target.Metric == SLAMetric.Quality && result.Severity >= SLABreachSeverity.Violation)
                result.Recommendations.Add("Quality SLA breach: run prompt regression tests and review recent prompt edits.");

            if (target.Metric == SLAMetric.Cost && result.Severity >= SLABreachSeverity.Violation)
                result.Recommendations.Add("Cost SLA breach: audit token usage with PromptTokenOptimizer and consider context compression.");

            if (target.Metric == SLAMetric.ErrorRate && result.Severity >= SLABreachSeverity.Violation)
                result.Recommendations.Add("Error rate SLA breach: check PromptSelfHealer for auto-repair and review PromptFallbackChain config.");
        }

        // ── Presets ────────────────────────────────

        /// <summary>Production preset: strict quality + latency + cost SLAs.</summary>
        public static PromptSLAMonitor Production() => new PromptSLAMonitor()
            .AddQualitySLA("Prod Quality", 0.85, 99.0)
            .AddLatencySLA("Prod Latency (P99)", 3000, 99.0)
            .AddCostSLA("Prod Cost Cap", 0.05, 95.0)
            .AddErrorRateSLA("Prod Error Rate", 0.02, 99.5)
            .WithWindow(30);

        /// <summary>Startup/prototype preset: relaxed thresholds.</summary>
        public static PromptSLAMonitor Relaxed() => new PromptSLAMonitor()
            .AddQualitySLA("Quality Baseline", 0.70, 95.0)
            .AddLatencySLA("Latency Ceiling", 5000, 95.0)
            .AddErrorRateSLA("Error Budget", 0.10, 90.0)
            .WithWindow(14);

        /// <summary>Enterprise preset: tight multi-metric SLAs.</summary>
        public static PromptSLAMonitor Enterprise() => new PromptSLAMonitor()
            .AddQualitySLA("Enterprise Quality", 0.90, 99.5)
            .AddLatencySLA("Enterprise P99 Latency", 2000, 99.5)
            .AddCostSLA("Enterprise Cost Control", 0.03, 99.0)
            .AddErrorRateSLA("Enterprise Availability", 0.005, 99.9)
            .WithWindow(30);

        // ── Export ─────────────────────────────────

        /// <summary>Export the SLA report in the specified format.</summary>
        public string Export(SLAReport report, SLAExportFormat format) => format switch
        {
            SLAExportFormat.Json => ExportJson(report),
            SLAExportFormat.Markdown => ExportMarkdown(report),
            SLAExportFormat.Html => ExportHtml(report),
            _ => ExportMarkdown(report)
        };

        private static string ExportJson(SLAReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"generatedAt\": \"{report.GeneratedAt:o}\",");
            sb.AppendLine($"  \"windowDays\": {report.WindowDays},");
            sb.AppendLine($"  \"totalObservations\": {report.TotalObservations},");
            sb.AppendLine($"  \"overallHealth\": \"{report.OverallHealth}\",");
            sb.AppendLine($"  \"overallCompliancePercent\": {report.OverallCompliancePercent:F2},");
            sb.AppendLine("  \"results\": [");

            for (int i = 0; i < report.Results.Count; i++)
            {
                var r = report.Results[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"sla\": \"{Esc(r.Target.Name)}\",");
                sb.AppendLine($"      \"metric\": \"{r.Target.Metric}\",");
                sb.AppendLine($"      \"threshold\": {r.Target.Threshold},");
                sb.AppendLine($"      \"targetCompliance\": {r.Target.TargetCompliancePercent:F1},");
                sb.AppendLine($"      \"actualCompliance\": {r.CompliancePercent:F2},");
                sb.AppendLine($"      \"severity\": \"{r.Severity}\",");
                sb.AppendLine($"      \"observations\": {r.TotalObservations},");
                sb.AppendLine($"      \"violations\": {r.ViolationCount},");
                sb.AppendLine($"      \"errorBudgetRemaining\": {r.ErrorBudgetRemainingPercent:F1},");
                sb.AppendLine($"      \"burnRatePerDay\": {r.BurnRatePerDay:F2},");
                sb.AppendLine($"      \"estimatedExhaustion\": {(r.EstimatedBudgetExhaustion.HasValue ? $"\"{r.EstimatedBudgetExhaustion.Value:o}\"" : "null")},");
                sb.AppendLine($"      \"patterns\": [{string.Join(", ", r.Patterns.Select(p => $"\"{Esc(p)}\""))}],");
                sb.AppendLine($"      \"recommendations\": [{string.Join(", ", r.Recommendations.Select(p => $"\"{Esc(p)}\""))}]");
                sb.AppendLine(i < report.Results.Count - 1 ? "    }," : "    }");
            }

            sb.AppendLine("  ],");
            sb.AppendLine($"  \"topRecommendations\": [{string.Join(", ", report.TopRecommendations.Select(r => $"\"{Esc(r)}\""))}]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ExportMarkdown(SLAReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 📊 SLA Compliance Report");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {report.GeneratedAt:u}  ");
            sb.AppendLine($"**Window:** {report.WindowDays} days | **Observations:** {report.TotalObservations}  ");
            sb.AppendLine($"**Overall Health:** {HealthEmoji(report.OverallHealth)} {report.OverallHealth} ({report.OverallCompliancePercent:F1}%)");
            sb.AppendLine();

            foreach (var r in report.Results)
            {
                string icon = r.Severity switch
                {
                    SLABreachSeverity.Compliant => "✅",
                    SLABreachSeverity.Warning => "⚠️",
                    SLABreachSeverity.Violation => "❌",
                    SLABreachSeverity.Critical => "🔥",
                    _ => "❓"
                };
                sb.AppendLine($"## {icon} {r.Target.Name}");
                sb.AppendLine();
                sb.AppendLine($"- **Metric:** {r.Target.Metric} ({r.Target.Direction} {r.Target.Threshold})");
                sb.AppendLine($"- **Compliance:** {r.CompliancePercent:F1}% (target: {r.Target.TargetCompliancePercent}%)");
                sb.AppendLine($"- **Status:** {r.Severity} | Violations: {r.ViolationCount}/{r.TotalObservations}");
                sb.AppendLine($"- **Error Budget:** {r.ErrorBudgetRemainingPercent:F1}% remaining (burn: {r.BurnRatePerDay:F2}%/day)");
                if (r.EstimatedBudgetExhaustion.HasValue)
                    sb.AppendLine($"- **Forecast:** Budget exhausts ~{r.EstimatedBudgetExhaustion.Value:yyyy-MM-dd}");

                if (r.Patterns.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Patterns:**");
                    foreach (var p in r.Patterns) sb.AppendLine($"  - {p}");
                }
                if (r.Recommendations.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Recommendations:**");
                    foreach (var rec in r.Recommendations) sb.AppendLine($"  - {rec}");
                }
                sb.AppendLine();
            }

            if (report.TopRecommendations.Count > 0)
            {
                sb.AppendLine("---");
                sb.AppendLine("## 🎯 Top Recommendations");
                foreach (var rec in report.TopRecommendations) sb.AppendLine($"- {rec}");
            }

            return sb.ToString();
        }

        private static string ExportHtml(SLAReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'/>");
            sb.AppendLine("<title>SLA Compliance Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:'Segoe UI',system-ui,sans-serif;background:#0f172a;color:#e2e8f0;padding:24px}");
            sb.AppendLine("h1{font-size:1.8rem;margin-bottom:8px;color:#f8fafc}");
            sb.AppendLine(".meta{color:#94a3b8;margin-bottom:20px;font-size:.9rem}");
            sb.AppendLine(".health-bar{display:inline-block;padding:6px 16px;border-radius:6px;font-weight:600;font-size:1rem;margin-bottom:20px}");
            sb.AppendLine(".health-healthy{background:#065f46;color:#6ee7b7}");
            sb.AppendLine(".health-atrisk{background:#78350f;color:#fbbf24}");
            sb.AppendLine(".health-degraded{background:#7c2d12;color:#fb923c}");
            sb.AppendLine(".health-critical{background:#7f1d1d;color:#fca5a5}");
            sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(420px,1fr));gap:16px;margin-bottom:24px}");
            sb.AppendLine(".card{background:#1e293b;border-radius:10px;padding:20px;border:1px solid #334155}");
            sb.AppendLine(".card h2{font-size:1.1rem;margin-bottom:12px;display:flex;align-items:center;gap:8px}");
            sb.AppendLine(".badge{display:inline-block;padding:2px 10px;border-radius:4px;font-size:.75rem;font-weight:600}");
            sb.AppendLine(".badge-compliant{background:#065f46;color:#6ee7b7}");
            sb.AppendLine(".badge-warning{background:#78350f;color:#fbbf24}");
            sb.AppendLine(".badge-violation{background:#7c2d12;color:#fb923c}");
            sb.AppendLine(".badge-critical{background:#7f1d1d;color:#fca5a5}");
            sb.AppendLine(".meter{height:8px;background:#334155;border-radius:4px;margin:8px 0;overflow:hidden}");
            sb.AppendLine(".meter-fill{height:100%;border-radius:4px;transition:width .3s}");
            sb.AppendLine(".stat{display:flex;justify-content:space-between;margin:4px 0;font-size:.85rem}");
            sb.AppendLine(".stat-label{color:#94a3b8} .stat-value{color:#f1f5f9;font-weight:500}");
            sb.AppendLine(".patterns,.recs{margin-top:10px;font-size:.82rem;color:#cbd5e1}");
            sb.AppendLine(".patterns li,.recs li{margin:3px 0 3px 16px}");
            sb.AppendLine(".recs-section{background:#1e293b;border-radius:10px;padding:20px;border:1px solid #334155;margin-top:16px}");
            sb.AppendLine(".recs-section h2{font-size:1.1rem;margin-bottom:10px}");
            sb.AppendLine(".recs-section li{margin:6px 0 6px 16px;font-size:.9rem}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h1>📊 SLA Compliance Dashboard</h1>");
            sb.AppendLine($"<div class='meta'>{report.GeneratedAt:u} · {report.WindowDays}-day window · {report.TotalObservations} observations</div>");

            string healthCls = report.OverallHealth.Replace(" ", "").ToLower();
            sb.AppendLine($"<div class='health-bar health-{healthCls}'>{HealthEmoji(report.OverallHealth)} {report.OverallHealth} — {report.OverallCompliancePercent:F1}% overall compliance</div>");

            sb.AppendLine("<div class='grid'>");
            foreach (var r in report.Results)
            {
                string sevCls = r.Severity.ToString().ToLower();
                string meterColor = r.Severity switch
                {
                    SLABreachSeverity.Compliant => "#10b981",
                    SLABreachSeverity.Warning => "#f59e0b",
                    SLABreachSeverity.Violation => "#f97316",
                    _ => "#ef4444"
                };

                sb.AppendLine("<div class='card'>");
                sb.AppendLine($"<h2>{H(r.Target.Name)} <span class='badge badge-{sevCls}'>{r.Severity}</span></h2>");
                sb.AppendLine($"<div class='stat'><span class='stat-label'>Metric</span><span class='stat-value'>{r.Target.Metric} ({r.Target.Direction} {r.Target.Threshold})</span></div>");
                sb.AppendLine($"<div class='stat'><span class='stat-label'>Compliance</span><span class='stat-value'>{r.CompliancePercent:F1}% (target {r.Target.TargetCompliancePercent}%)</span></div>");
                sb.AppendLine($"<div class='meter'><div class='meter-fill' style='width:{Math.Min(100, r.CompliancePercent):F0}%;background:{meterColor}'></div></div>");
                sb.AppendLine($"<div class='stat'><span class='stat-label'>Violations</span><span class='stat-value'>{r.ViolationCount}/{r.TotalObservations}</span></div>");
                sb.AppendLine($"<div class='stat'><span class='stat-label'>Error Budget</span><span class='stat-value'>{r.ErrorBudgetRemainingPercent:F1}% remaining</span></div>");
                sb.AppendLine($"<div class='stat'><span class='stat-label'>Burn Rate</span><span class='stat-value'>{r.BurnRatePerDay:F2}%/day</span></div>");
                if (r.EstimatedBudgetExhaustion.HasValue)
                    sb.AppendLine($"<div class='stat'><span class='stat-label'>Forecast</span><span class='stat-value'>Exhausts ~{r.EstimatedBudgetExhaustion.Value:yyyy-MM-dd}</span></div>");

                if (r.Patterns.Count > 0)
                {
                    sb.AppendLine("<ul class='patterns'>");
                    foreach (var p in r.Patterns) sb.AppendLine($"<li>🔍 {H(p)}</li>");
                    sb.AppendLine("</ul>");
                }
                if (r.Recommendations.Count > 0)
                {
                    sb.AppendLine("<ul class='recs'>");
                    foreach (var rec in r.Recommendations) sb.AppendLine($"<li>{H(rec)}</li>");
                    sb.AppendLine("</ul>");
                }
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");

            if (report.TopRecommendations.Count > 0)
            {
                sb.AppendLine("<div class='recs-section'><h2>🎯 Top Recommendations</h2><ul>");
                foreach (var rec in report.TopRecommendations)
                    sb.AppendLine($"<li>{H(rec)}</li>");
                sb.AppendLine("</ul></div>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string HealthEmoji(string health) => health switch
        {
            "Healthy" => "💚",
            "At Risk" => "💛",
            "Degraded" => "🟠",
            "Critical" => "🔴",
            _ => "⚪"
        };

        private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string H(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
