namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ────────────────────────────────────────────
    //  PromptRiskForecaster – Autonomous Risk Prediction Engine
    //
    //  Tracks risk observations across prompt portfolios over time,
    //  applies linear regression to detect rising trends, forecasts
    //  when risk thresholds will be breached, and generates early
    //  warnings with intervention plans.  Unlike PromptRiskAssessor
    //  (point-in-time), this engine is forward-looking and predictive.
    // ────────────────────────────────────────────

    /// <summary>Directional trend of a risk dimension over time.</summary>
    public enum RiskTrend
    {
        /// <summary>Risk scores increasing over time.</summary>
        Rising,
        /// <summary>Risk scores relatively constant.</summary>
        Stable,
        /// <summary>Risk scores decreasing over time.</summary>
        Falling,
        /// <summary>Risk scores fluctuating without clear direction.</summary>
        Volatile,
        /// <summary>Not enough data points for trend analysis.</summary>
        Insufficient
    }

    /// <summary>Confidence in a forecast based on regression fit.</summary>
    public enum ForecastConfidence
    {
        /// <summary>R² below 0.3 — very unreliable.</summary>
        VeryLow,
        /// <summary>R² 0.3–0.5 — weak signal.</summary>
        Low,
        /// <summary>R² 0.5–0.7 — moderate signal.</summary>
        Medium,
        /// <summary>R² 0.7–0.85 — strong signal.</summary>
        High,
        /// <summary>R² above 0.85 — very strong signal.</summary>
        VeryHigh
    }

    /// <summary>Severity of an early-warning alert.</summary>
    public enum AlertSeverity
    {
        /// <summary>Breach possible but distant (&gt;30 days).</summary>
        Watch,
        /// <summary>Breach projected within 14–30 days.</summary>
        Advisory,
        /// <summary>Breach projected within 7–14 days.</summary>
        Warning,
        /// <summary>Breach projected within 3–7 days.</summary>
        Critical,
        /// <summary>Breach projected within 3 days or already breached.</summary>
        Imminent
    }

    /// <summary>Estimated effort for an intervention step.</summary>
    public enum InterventionEffort
    {
        /// <summary>Under 1 hour of work.</summary>
        Quick,
        /// <summary>1–4 hours of work.</summary>
        Moderate,
        /// <summary>Half-day to full day.</summary>
        Significant,
        /// <summary>Multi-day effort.</summary>
        Major
    }

    /// <summary>A single risk measurement for a prompt dimension at a point in time.</summary>
    public class RiskObservation
    {
        /// <summary>Unique observation identifier.</summary>
        public string ObservationId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>Prompt this observation relates to.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Risk dimension being measured (e.g. "Injection", "DataLeakage").</summary>
        public string Dimension { get; set; } = "";

        /// <summary>Risk score from 0 (safe) to 100 (critical).</summary>
        public double Score { get; set; }

        /// <summary>Optional metadata tags.</summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>When this observation was recorded.</summary>
        public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Forecast for a single risk dimension.</summary>
    public class DimensionForecast
    {
        /// <summary>Risk dimension name.</summary>
        public string Dimension { get; set; } = "";

        /// <summary>Most recent average score for this dimension.</summary>
        public double CurrentScore { get; set; }

        /// <summary>Detected trend direction.</summary>
        public RiskTrend TrendDirection { get; set; }

        /// <summary>Rate of change in points per day.</summary>
        public double Velocity { get; set; }

        /// <summary>Projected score 7 days from now.</summary>
        public double ProjectedScore7d { get; set; }

        /// <summary>Projected score 30 days from now.</summary>
        public double ProjectedScore30d { get; set; }

        /// <summary>Threshold at which a breach is declared.</summary>
        public double BreachThreshold { get; set; }

        /// <summary>Estimated days until breach (null if not projected).</summary>
        public double? DaysToBreachEstimate { get; set; }

        /// <summary>Confidence in this forecast.</summary>
        public ForecastConfidence Confidence { get; set; }

        /// <summary>R² goodness-of-fit for the trend line.</summary>
        public double TrendR2 { get; set; }

        /// <summary>Number of observations used.</summary>
        public int ObservationCount { get; set; }
    }

    /// <summary>An early-warning alert for an approaching breach.</summary>
    public class EarlyWarning
    {
        /// <summary>Unique warning identifier.</summary>
        public string WarningId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>Risk dimension triggering the warning.</summary>
        public string Dimension { get; set; } = "";

        /// <summary>Alert severity based on proximity to breach.</summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>Human-readable warning message.</summary>
        public string Message { get; set; } = "";

        /// <summary>Estimated days until threshold breach.</summary>
        public double DaysToBreach { get; set; }

        /// <summary>Prompt ids contributing to this risk.</summary>
        public List<string> AffectedPromptIds { get; set; } = new();

        /// <summary>Recommended immediate action.</summary>
        public string RecommendedAction { get; set; } = "";

        /// <summary>When this warning was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A planned intervention to prevent a forecasted breach.</summary>
    public class InterventionPlan
    {
        /// <summary>Dimension this plan addresses.</summary>
        public string Dimension { get; set; } = "";

        /// <summary>Projected breach date (null if no breach projected).</summary>
        public DateTime? BreachDate { get; set; }

        /// <summary>Ordered intervention steps.</summary>
        public List<InterventionStep> Interventions { get; set; } = new();
    }

    /// <summary>A single step in an intervention plan.</summary>
    public class InterventionStep
    {
        /// <summary>Execution priority (1 = highest).</summary>
        public int Priority { get; set; }

        /// <summary>Action to take.</summary>
        public string Action { get; set; } = "";

        /// <summary>Why this action helps.</summary>
        public string Rationale { get; set; } = "";

        /// <summary>Estimated effort.</summary>
        public InterventionEffort Effort { get; set; }

        /// <summary>Expected risk-score reduction.</summary>
        public double EstimatedRiskReduction { get; set; }

        /// <summary>Prompts that should be addressed.</summary>
        public List<string> AffectedPromptIds { get; set; } = new();
    }

    /// <summary>A cell in the portfolio risk heatmap.</summary>
    public class HeatmapCell
    {
        /// <summary>Prompt identifier.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Risk dimension.</summary>
        public string Dimension { get; set; } = "";

        /// <summary>Latest risk score.</summary>
        public double CurrentScore { get; set; }

        /// <summary>Trend direction for this prompt×dimension.</summary>
        public RiskTrend Trend { get; set; }

        /// <summary>Rate of change in points per day.</summary>
        public double Velocity { get; set; }
    }

    /// <summary>Complete forecast report for the prompt portfolio.</summary>
    public class ForecastReport
    {
        /// <summary>Per-dimension forecasts.</summary>
        public List<DimensionForecast> Forecasts { get; set; } = new();

        /// <summary>Active early warnings.</summary>
        public List<EarlyWarning> Warnings { get; set; } = new();

        /// <summary>Portfolio-wide risk heatmap.</summary>
        public List<HeatmapCell> Heatmap { get; set; } = new();

        /// <summary>Intervention plans for at-risk dimensions.</summary>
        public List<InterventionPlan> InterventionPlans { get; set; } = new();

        /// <summary>Aggregate portfolio risk score (0–100).</summary>
        public double PortfolioRiskScore { get; set; }

        /// <summary>Overall portfolio trend.</summary>
        public RiskTrend PortfolioTrend { get; set; }

        /// <summary>When this report was generated.</summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Total observations analyzed.</summary>
        public int ObservationCount { get; set; }

        /// <summary>Distinct prompts in the portfolio.</summary>
        public int PromptCount { get; set; }

        /// <summary>Distinct risk dimensions tracked.</summary>
        public int DimensionCount { get; set; }
    }

    /// <summary>
    /// Autonomous forward-looking risk forecasting engine.  Tracks risk
    /// observations across prompt portfolios, detects rising trends via
    /// linear regression, forecasts threshold breaches, and generates
    /// early warnings with actionable intervention plans.
    /// </summary>
    public class PromptRiskForecaster
    {
        private readonly List<RiskObservation> _observations = new();
        private readonly double _breachThreshold;
        private readonly int _warningHorizonDays;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Creates a new risk forecaster.
        /// </summary>
        /// <param name="breachThreshold">Risk score at which a breach is declared (default 75).</param>
        /// <param name="warningHorizonDays">Days ahead to scan for potential breaches (default 7).</param>
        public PromptRiskForecaster(double breachThreshold = 75.0, int warningHorizonDays = 7)
        {
            _breachThreshold = breachThreshold;
            _warningHorizonDays = warningHorizonDays;
        }

        /// <summary>Number of recorded observations.</summary>
        public int ObservationCount => _observations.Count;

        /// <summary>Record a single risk observation.</summary>
        public void RecordObservation(RiskObservation obs)
        {
            if (obs == null) throw new ArgumentNullException(nameof(obs));
            if (obs.Score < 0 || obs.Score > 100)
                throw new ArgumentOutOfRangeException(nameof(obs), "Score must be between 0 and 100.");
            if (string.IsNullOrWhiteSpace(obs.Dimension))
                throw new ArgumentException("Dimension must not be empty.", nameof(obs));
            _observations.Add(obs);
        }

        /// <summary>Record multiple risk observations at once.</summary>
        public void RecordObservations(IEnumerable<RiskObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            foreach (var obs in observations) RecordObservation(obs);
        }

        /// <summary>
        /// Generates a complete forecast report for the portfolio.
        /// Analyzes trends, projects breaches, and builds intervention plans.
        /// </summary>
        public ForecastReport GenerateForecast()
        {
            var report = new ForecastReport
            {
                ObservationCount = _observations.Count,
                PromptCount = _observations.Select(o => o.PromptId).Distinct().Count(),
                DimensionCount = _observations.Select(o => o.Dimension).Distinct().Count()
            };

            if (_observations.Count == 0) return report;

            var now = _observations.Max(o => o.ObservedAt);

            // ── Per-dimension forecasts ─────────────────────────
            var byDimension = _observations.GroupBy(o => o.Dimension);
            foreach (var group in byDimension)
            {
                var sorted = group.OrderBy(o => o.ObservedAt).ToList();
                var forecast = BuildDimensionForecast(sorted, group.Key, now);
                report.Forecasts.Add(forecast);

                // Early warning
                if (forecast.DaysToBreachEstimate.HasValue &&
                    forecast.DaysToBreachEstimate.Value <= _warningHorizonDays &&
                    forecast.TrendDirection == RiskTrend.Rising)
                {
                    var promptIds = group.Select(o => o.PromptId).Distinct().ToList();
                    report.Warnings.Add(new EarlyWarning
                    {
                        Dimension = group.Key,
                        Severity = MapAlertSeverity(forecast.DaysToBreachEstimate.Value),
                        Message = $"{group.Key} risk projected to breach threshold " +
                                  $"({_breachThreshold}) in {forecast.DaysToBreachEstimate.Value:F1} days " +
                                  $"(velocity: {forecast.Velocity:+0.00;-0.00} pts/day)",
                        DaysToBreach = forecast.DaysToBreachEstimate.Value,
                        AffectedPromptIds = promptIds,
                        RecommendedAction = forecast.Velocity > 2.0
                            ? "Immediate review of high-risk prompts"
                            : "Schedule prompt audit within the warning window"
                    });
                }

                // Intervention plan for at-risk dimensions
                if (forecast.TrendDirection == RiskTrend.Rising && forecast.CurrentScore > 40)
                {
                    var promptIds = group.Select(o => o.PromptId).Distinct().ToList();
                    report.InterventionPlans.Add(new InterventionPlan
                    {
                        Dimension = group.Key,
                        BreachDate = forecast.DaysToBreachEstimate.HasValue
                            ? now.AddDays(forecast.DaysToBreachEstimate.Value)
                            : null,
                        Interventions = GenerateInterventions(
                            group.Key, forecast.CurrentScore, forecast.Velocity, promptIds)
                    });
                }
            }

            // ── Heatmap ─────────────────────────────────────────
            var promptDimGroups = _observations
                .GroupBy(o => (o.PromptId, o.Dimension));
            foreach (var pg in promptDimGroups)
            {
                var sorted = pg.OrderBy(o => o.ObservedAt).ToList();
                var latest = sorted.Last();
                var (vel, _, r2) = sorted.Count >= 2
                    ? ComputeRegression(sorted, sorted.First().ObservedAt)
                    : (0.0, latest.Score, 0.0);

                report.Heatmap.Add(new HeatmapCell
                {
                    PromptId = pg.Key.PromptId,
                    Dimension = pg.Key.Dimension,
                    CurrentScore = latest.Score,
                    Trend = ClassifyTrend(vel, r2, sorted.Count),
                    Velocity = Math.Round(vel, 4)
                });
            }

            // ── Portfolio aggregates ────────────────────────────
            if (report.Forecasts.Count > 0)
            {
                report.PortfolioRiskScore = Math.Round(
                    report.Forecasts.Average(f => f.CurrentScore), 2);

                var avgVelocity = report.Forecasts
                    .Where(f => f.TrendDirection != RiskTrend.Insufficient)
                    .Select(f => f.Velocity)
                    .DefaultIfEmpty(0)
                    .Average();
                var avgR2 = report.Forecasts
                    .Where(f => f.TrendDirection != RiskTrend.Insufficient)
                    .Select(f => f.TrendR2)
                    .DefaultIfEmpty(0)
                    .Average();
                report.PortfolioTrend = ClassifyTrend(avgVelocity, avgR2,
                    report.Forecasts.Max(f => f.ObservationCount));
            }

            return report;
        }

        /// <summary>Returns all observations for a given dimension.</summary>
        public List<RiskObservation> GetDimensionHistory(string dimension)
        {
            return _observations
                .Where(o => o.Dimension.Equals(dimension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(o => o.ObservedAt)
                .ToList();
        }

        /// <summary>Returns the latest risk score per dimension for a prompt.</summary>
        public Dictionary<string, double> GetPromptRiskProfile(string promptId)
        {
            return _observations
                .Where(o => o.PromptId == promptId)
                .GroupBy(o => o.Dimension)
                .ToDictionary(
                    g => g.Key,
                    g => g.MaxBy(o => o.ObservedAt)!.Score);
        }

        /// <summary>Exports the current forecast as JSON.</summary>
        public string ExportJson()
        {
            var report = GenerateForecast();
            return JsonSerializer.Serialize(report, JsonOpts);
        }

        // ── Private helpers ─────────────────────────────────────

        private DimensionForecast BuildDimensionForecast(
            List<RiskObservation> sorted, string dimension, DateTime now)
        {
            var latest = sorted.Last();
            var count = sorted.Count;

            if (count < 3)
            {
                return new DimensionForecast
                {
                    Dimension = dimension,
                    CurrentScore = Math.Round(latest.Score, 2),
                    TrendDirection = RiskTrend.Insufficient,
                    Velocity = 0,
                    ProjectedScore7d = latest.Score,
                    ProjectedScore30d = latest.Score,
                    BreachThreshold = _breachThreshold,
                    DaysToBreachEstimate = null,
                    Confidence = ForecastConfidence.VeryLow,
                    TrendR2 = 0,
                    ObservationCount = count
                };
            }

            var epoch = sorted.First().ObservedAt;
            var (slope, intercept, r2) = ComputeRegression(sorted, epoch);

            var nowDays = (now - epoch).TotalDays;
            var currentFitted = intercept + slope * nowDays;

            var proj7 = Clamp(intercept + slope * (nowDays + 7), 0, 100);
            var proj30 = Clamp(intercept + slope * (nowDays + 30), 0, 100);

            double? daysToBreach = null;
            if (slope > 0 && currentFitted < _breachThreshold)
            {
                daysToBreach = (_breachThreshold - currentFitted) / slope;
            }
            else if (currentFitted >= _breachThreshold)
            {
                daysToBreach = 0;
            }

            var trend = ClassifyTrend(slope, r2, count);

            return new DimensionForecast
            {
                Dimension = dimension,
                CurrentScore = Math.Round(latest.Score, 2),
                TrendDirection = trend,
                Velocity = Math.Round(slope, 4),
                ProjectedScore7d = Math.Round(proj7, 2),
                ProjectedScore30d = Math.Round(proj30, 2),
                BreachThreshold = _breachThreshold,
                DaysToBreachEstimate = daysToBreach.HasValue ? Math.Round(daysToBreach.Value, 2) : null,
                Confidence = MapConfidence(r2),
                TrendR2 = Math.Round(r2, 4),
                ObservationCount = count
            };
        }

        private static (double slope, double intercept, double r2) ComputeRegression(
            List<RiskObservation> sorted, DateTime epoch)
        {
            var points = sorted
                .Select(o => ((o.ObservedAt - epoch).TotalDays, o.Score))
                .ToList();
            return LinearRegression(points);
        }

        internal static (double slope, double intercept, double r2) LinearRegression(
            List<(double x, double y)> points)
        {
            int n = points.Count;
            if (n < 2) return (0, points.Count == 1 ? points[0].y : 0, 0);

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            foreach (var (x, y) in points)
            {
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-12)
                return (0, sumY / n, 0);

            double slope = (n * sumXY - sumX * sumY) / denom;
            double intercept = (sumY - slope * sumX) / n;

            // R²
            double meanY = sumY / n;
            double ssTot = 0, ssRes = 0;
            foreach (var (x, y) in points)
            {
                double predicted = intercept + slope * x;
                ssRes += (y - predicted) * (y - predicted);
                ssTot += (y - meanY) * (y - meanY);
            }

            double r2 = ssTot < 1e-12 ? 1.0 : 1.0 - ssRes / ssTot;
            if (r2 < 0) r2 = 0;

            return (slope, intercept, r2);
        }

        private static RiskTrend ClassifyTrend(double velocity, double r2, int count)
        {
            if (count < 3) return RiskTrend.Insufficient;
            if (r2 < 0.3 && count >= 5) return RiskTrend.Volatile;
            if (r2 < 0.3) return RiskTrend.Insufficient;
            if (Math.Abs(velocity) < 0.5) return RiskTrend.Stable;
            return velocity > 0 ? RiskTrend.Rising : RiskTrend.Falling;
        }

        private static ForecastConfidence MapConfidence(double r2)
        {
            if (r2 >= 0.85) return ForecastConfidence.VeryHigh;
            if (r2 >= 0.7) return ForecastConfidence.High;
            if (r2 >= 0.5) return ForecastConfidence.Medium;
            if (r2 >= 0.3) return ForecastConfidence.Low;
            return ForecastConfidence.VeryLow;
        }

        private static AlertSeverity MapAlertSeverity(double daysToBreach)
        {
            if (daysToBreach <= 0) return AlertSeverity.Imminent;
            if (daysToBreach <= 3) return AlertSeverity.Imminent;
            if (daysToBreach <= 7) return AlertSeverity.Critical;
            if (daysToBreach <= 14) return AlertSeverity.Warning;
            if (daysToBreach <= 30) return AlertSeverity.Advisory;
            return AlertSeverity.Watch;
        }

        private static List<InterventionStep> GenerateInterventions(
            string dimension, double currentScore, double velocity, List<string> promptIds)
        {
            var steps = new List<InterventionStep>();
            int priority = 1;

            if (velocity > 2.0)
            {
                steps.Add(new InterventionStep
                {
                    Priority = priority++,
                    Action = $"Immediate review of {dimension} risk in affected prompts",
                    Rationale = $"Velocity is {velocity:F2} pts/day — risk is accelerating rapidly",
                    Effort = InterventionEffort.Quick,
                    EstimatedRiskReduction = 12.5,
                    AffectedPromptIds = promptIds
                });
            }

            if (velocity > 1.0)
            {
                steps.Add(new InterventionStep
                {
                    Priority = priority++,
                    Action = $"Schedule {dimension} prompt refactoring sprint",
                    Rationale = $"Steady risk increase at {velocity:F2} pts/day requires structural changes",
                    Effort = InterventionEffort.Moderate,
                    EstimatedRiskReduction = 20.0,
                    AffectedPromptIds = promptIds
                });
            }

            if (currentScore > 60)
            {
                steps.Add(new InterventionStep
                {
                    Priority = priority++,
                    Action = $"Add guardrails and validation for {dimension}",
                    Rationale = $"Current score ({currentScore:F1}) is elevated — defensive measures needed",
                    Effort = InterventionEffort.Moderate,
                    EstimatedRiskReduction = 15.0,
                    AffectedPromptIds = promptIds
                });
            }

            if (currentScore > 50)
            {
                steps.Add(new InterventionStep
                {
                    Priority = priority++,
                    Action = $"Implement real-time monitoring alerts for {dimension}",
                    Rationale = $"Score above 50 warrants continuous observation",
                    Effort = InterventionEffort.Quick,
                    EstimatedRiskReduction = 7.5,
                    AffectedPromptIds = promptIds
                });
            }

            steps.Add(new InterventionStep
            {
                Priority = priority,
                Action = $"Review and update prompt templates addressing {dimension}",
                Rationale = "Periodic template refresh reduces accumulated risk drift",
                Effort = InterventionEffort.Significant,
                EstimatedRiskReduction = 25.0,
                AffectedPromptIds = promptIds
            });

            return steps;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
