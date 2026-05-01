namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ────────────────────────────────────────────
    //  PromptForgettingCurveEngine – Autonomous Retention & Decay Analysis
    //
    //  Models how prompt effectiveness decays over time using
    //  Ebbinghaus-style forgetting curves.  Fits exponential and
    //  power-law decay models, predicts maintenance windows, classifies
    //  prompts by durability, generates spaced-repetition review
    //  schedules, and produces autonomous fleet-wide retention reports.
    //
    //  Unlike PromptRiskForecaster (risk trends) or PromptMetabolismEngine
    //  (token efficiency), this engine focuses specifically on temporal
    //  degradation patterns and optimal refresh timing.
    // ────────────────────────────────────────────

    /// <summary>Phase of prompt retention based on decay curve position.</summary>
    public enum RetentionPhase
    {
        /// <summary>Newly deployed, less than 3 days old.</summary>
        Fresh,
        /// <summary>3–14 days, retention above 0.8 — consolidating into stable use.</summary>
        Consolidating,
        /// <summary>Retention above 0.7 and steady — reliable prompt.</summary>
        Stable,
        /// <summary>Retention actively dropping — needs attention soon.</summary>
        Decaying,
        /// <summary>Retention below 0.3 — effectively lost usefulness.</summary>
        Forgotten
    }

    /// <summary>Mathematical model used to fit the decay curve.</summary>
    public enum DecayModel
    {
        /// <summary>R(t) = R₀ × e^(−λt) — classic Ebbinghaus model.</summary>
        Exponential,
        /// <summary>R(t) = R₀ × t^(−b) — slower initial decay, long tail.</summary>
        PowerLaw,
        /// <summary>Retention holds steady then drops sharply at a threshold.</summary>
        StepFunction,
        /// <summary>R(t) = R₀ − c × ln(t) — moderate logarithmic fade.</summary>
        Logarithmic
    }

    /// <summary>How naturally long-lasting a prompt is.</summary>
    public enum DurabilityTier
    {
        /// <summary>Half-life under 3 days — extremely short-lived.</summary>
        Ephemeral,
        /// <summary>Half-life 3–10 days — needs frequent refreshing.</summary>
        Fragile,
        /// <summary>Half-life 10–30 days — typical lifespan.</summary>
        Standard,
        /// <summary>Half-life 30–90 days — reliably long-lived.</summary>
        Durable,
        /// <summary>Half-life over 90 days — practically permanent.</summary>
        Evergreen
    }

    /// <summary>How urgently a prompt needs maintenance.</summary>
    public enum MaintenanceUrgency
    {
        /// <summary>No maintenance needed — retention healthy.</summary>
        None,
        /// <summary>Schedule routine review within 30 days.</summary>
        Routine,
        /// <summary>Quality approaching threshold — review within 14 days.</summary>
        Approaching,
        /// <summary>Quality near threshold — review within 7 days.</summary>
        Urgent,
        /// <summary>Quality already below acceptable threshold.</summary>
        Overdue
    }

    /// <summary>Recommended strategy for refreshing a decaying prompt.</summary>
    public enum RefreshStrategy
    {
        /// <summary>Small wording tweaks to restore clarity.</summary>
        MinorTweak,
        /// <summary>Rephrase key instructions while keeping structure.</summary>
        Rephrase,
        /// <summary>Reorganize structure and flow significantly.</summary>
        Restructure,
        /// <summary>Complete rewrite from scratch.</summary>
        FullRewrite,
        /// <summary>Prompt has outlived usefulness — retire it.</summary>
        Retire
    }

    /// <summary>A single performance measurement for a prompt at a point in time.</summary>
    public class PerformanceObservation
    {
        /// <summary>Unique identifier of the prompt being measured.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>When the observation was recorded.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Overall quality score 0–100.</summary>
        public double QualityScore { get; set; }

        /// <summary>How relevant the response was 0–100.</summary>
        public double ResponseRelevance { get; set; }

        /// <summary>User satisfaction rating 0–100.</summary>
        public double UserSatisfaction { get; set; }

        /// <summary>Token efficiency score 0–100.</summary>
        public double TokenEfficiency { get; set; }

        /// <summary>Optional categorization tags.</summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>Composite score averaging all dimensions.</summary>
        public double CompositeScore => (QualityScore + ResponseRelevance + UserSatisfaction + TokenEfficiency) / 4.0;
    }

    /// <summary>Fitted decay curve profile for a specific prompt.</summary>
    public class DecayCurveProfile
    {
        /// <summary>Prompt this curve belongs to.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>Best-fit mathematical model.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DecayModel FittedModel { get; set; }

        /// <summary>Time for retention to halve.</summary>
        public TimeSpan HalfLife { get; set; }

        /// <summary>Initial strength (normalized 0–1).</summary>
        public double InitialStrength { get; set; }

        /// <summary>Decay rate parameter (λ for exponential, b for power-law).</summary>
        public double DecayRate { get; set; }

        /// <summary>R² goodness of fit (0–1).</summary>
        public double RSquared { get; set; }

        /// <summary>Current estimated retention (0–1).</summary>
        public double CurrentRetention { get; set; }

        /// <summary>Current lifecycle phase.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RetentionPhase Phase { get; set; }

        /// <summary>When the curve was last fitted.</summary>
        public DateTime LastFitted { get; set; }
    }

    /// <summary>Assessment of how durable a prompt is over time.</summary>
    public class DurabilityAssessment
    {
        /// <summary>Prompt being assessed.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>Durability classification.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DurabilityTier Tier { get; set; }

        /// <summary>Numeric durability score 0–100.</summary>
        public double Score { get; set; }

        /// <summary>Factors contributing to durability.</summary>
        public List<string> Factors { get; set; } = new();

        /// <summary>Predicted useful lifespan before needing refresh.</summary>
        public TimeSpan PredictedLifespan { get; set; }
    }

    /// <summary>Predicted maintenance window for a prompt.</summary>
    public class MaintenanceWindow
    {
        /// <summary>Prompt needing maintenance.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>How urgent the maintenance is.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MaintenanceUrgency Urgency { get; set; }

        /// <summary>When retention will drop below 80%.</summary>
        public DateTime? PredictedDecayBelow80 { get; set; }

        /// <summary>When retention will drop below 50%.</summary>
        public DateTime? PredictedDecayBelow50 { get; set; }

        /// <summary>Recommended date to refresh the prompt.</summary>
        public DateTime? RecommendedRefreshDate { get; set; }

        /// <summary>Suggested refresh approach.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RefreshStrategy Strategy { get; set; }

        /// <summary>Estimated effort description.</summary>
        public string EstimatedEffort { get; set; } = string.Empty;
    }

    /// <summary>Prioritized recommendation to refresh a specific prompt.</summary>
    public class RefreshRecommendation
    {
        /// <summary>Prompt to refresh.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>Recommended strategy.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RefreshStrategy Strategy { get; set; }

        /// <summary>Why this refresh is recommended.</summary>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>Priority score (higher = more urgent).</summary>
        public double PriorityScore { get; set; }

        /// <summary>Expected retention gain from refreshing (0–1).</summary>
        public double ExpectedRetentionGain { get; set; }
    }

    /// <summary>Spaced repetition review schedule for maintaining a prompt.</summary>
    public class SpacedRepetitionSchedule
    {
        /// <summary>Prompt this schedule applies to.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>Intervals between reviews (expanding on success).</summary>
        public List<TimeSpan> ReviewIntervals { get; set; } = new();

        /// <summary>When the next review should happen.</summary>
        public DateTime NextReviewDate { get; set; }

        /// <summary>Total number of reviews completed.</summary>
        public int ReviewCount { get; set; }

        /// <summary>Consecutive successful reviews (retention stayed high).</summary>
        public int SuccessiveCorrectCount { get; set; }
    }

    /// <summary>A detected recovery event where a prompt's retention bounced back.</summary>
    public class RecoveryEvent
    {
        /// <summary>Prompt that recovered.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>When the recovery was detected.</summary>
        public DateTime RecoveryDate { get; set; }

        /// <summary>Retention level before recovery.</summary>
        public double PreRecoveryRetention { get; set; }

        /// <summary>Retention level after recovery.</summary>
        public double PostRecoveryRetention { get; set; }

        /// <summary>How strong the recovery was (post - pre).</summary>
        public double RecoveryStrength => PostRecoveryRetention - PreRecoveryRetention;
    }

    /// <summary>Fleet-wide retention health report.</summary>
    public class FleetRetentionReport
    {
        /// <summary>Overall fleet retention health 0–100.</summary>
        public double OverallHealthScore { get; set; }

        /// <summary>Total prompts being tracked.</summary>
        public int TotalPrompts { get; set; }

        /// <summary>Count of prompts in Fresh phase.</summary>
        public int FreshCount { get; set; }

        /// <summary>Count of prompts in Consolidating phase.</summary>
        public int ConsolidatingCount { get; set; }

        /// <summary>Count of prompts in Stable phase.</summary>
        public int StableCount { get; set; }

        /// <summary>Count of prompts in Decaying phase.</summary>
        public int DecayingCount { get; set; }

        /// <summary>Count of prompts in Forgotten phase.</summary>
        public int ForgottenCount { get; set; }

        /// <summary>Prompts with fastest decay rates.</summary>
        public List<DecayCurveProfile> TopDecaying { get; set; } = new();

        /// <summary>Most durable prompts in the fleet.</summary>
        public List<DecayCurveProfile> MostDurable { get; set; } = new();

        /// <summary>Prompts overdue or urgently needing maintenance.</summary>
        public List<MaintenanceWindow> MaintenanceBacklog { get; set; } = new();

        /// <summary>Autonomous insights about fleet retention patterns.</summary>
        public List<string> AutonomousInsights { get; set; } = new();
    }

    /// <summary>Configuration for the forgetting curve engine.</summary>
    public class ForgettingCurveConfig
    {
        /// <summary>Minimum observations required before fitting a curve.</summary>
        public int MinObservationsForFit { get; set; } = 5;

        /// <summary>Default assumed half-life when no fit is available (days).</summary>
        public double DefaultHalfLifeDays { get; set; } = 30.0;

        /// <summary>Quality floor below which a prompt is considered forgotten.</summary>
        public double QualityFloor { get; set; } = 50.0;

        /// <summary>Retention threshold for "healthy" status (0–1).</summary>
        public double HealthyRetentionThreshold { get; set; } = 0.7;

        /// <summary>Number of top items in fleet reports.</summary>
        public int FleetTopN { get; set; } = 5;
    }

    /// <summary>
    /// Autonomous engine that models prompt effectiveness decay over time,
    /// predicts maintenance windows, and generates spaced-repetition review schedules.
    /// </summary>
    public class PromptForgettingCurveEngine
    {
        private readonly Dictionary<string, List<PerformanceObservation>> _observations = new();
        private readonly Dictionary<string, DecayCurveProfile> _profiles = new();
        private readonly Dictionary<string, SpacedRepetitionSchedule> _schedules = new();
        private readonly ForgettingCurveConfig _config;

        /// <summary>Creates a new forgetting curve engine with optional configuration.</summary>
        public PromptForgettingCurveEngine(ForgettingCurveConfig? config = null)
        {
            _config = config ?? new ForgettingCurveConfig();
        }

        /// <summary>Records a performance observation and re-fits the curve if enough data exists.</summary>
        public void RecordObservation(PerformanceObservation obs)
        {
            if (obs == null) throw new ArgumentNullException(nameof(obs));
            if (string.IsNullOrWhiteSpace(obs.PromptId))
                throw new ArgumentException("PromptId is required.", nameof(obs));

            if (!_observations.ContainsKey(obs.PromptId))
                _observations[obs.PromptId] = new List<PerformanceObservation>();

            _observations[obs.PromptId].Add(obs);

            if (_observations[obs.PromptId].Count >= _config.MinObservationsForFit)
                FitDecayCurve(obs.PromptId);
        }

        /// <summary>Fits the best decay model to observed data for a prompt.</summary>
        public DecayCurveProfile FitDecayCurve(string promptId)
        {
            if (!_observations.ContainsKey(promptId) || _observations[promptId].Count < 2)
            {
                var defaultProfile = new DecayCurveProfile
                {
                    PromptId = promptId,
                    FittedModel = DecayModel.Exponential,
                    HalfLife = TimeSpan.FromDays(_config.DefaultHalfLifeDays),
                    InitialStrength = 1.0,
                    DecayRate = Math.Log(2) / _config.DefaultHalfLifeDays,
                    RSquared = 0.0,
                    CurrentRetention = 1.0,
                    Phase = RetentionPhase.Fresh,
                    LastFitted = DateTime.UtcNow
                };
                _profiles[promptId] = defaultProfile;
                return defaultProfile;
            }

            var sorted = _observations[promptId].OrderBy(o => o.Timestamp).ToList();
            var baseTime = sorted[0].Timestamp;
            var maxComposite = sorted.Max(o => o.CompositeScore);
            if (maxComposite < 1.0) maxComposite = 100.0;

            // Normalize observations to 0–1 retention values
            var points = sorted.Select(o => (
                t: (o.Timestamp - baseTime).TotalDays,
                r: Math.Clamp(o.CompositeScore / maxComposite, 0.0, 1.0)
            )).ToList();

            // Fit exponential: R(t) = R₀ × e^(−λt)
            var expFit = FitExponential(points);
            // Fit power law: R(t) = R₀ × (t+1)^(−b)
            var powFit = FitPowerLaw(points);
            // Fit logarithmic: R(t) = R₀ − c × ln(t+1)
            var logFit = FitLogarithmic(points);

            // Pick best R²
            var bestModel = DecayModel.Exponential;
            var bestR2 = expFit.r2;
            var bestRate = expFit.rate;
            var bestInitial = expFit.initial;

            if (powFit.r2 > bestR2)
            {
                bestModel = DecayModel.PowerLaw;
                bestR2 = powFit.r2;
                bestRate = powFit.rate;
                bestInitial = powFit.initial;
            }
            if (logFit.r2 > bestR2)
            {
                bestModel = DecayModel.Logarithmic;
                bestR2 = logFit.r2;
                bestRate = logFit.rate;
                bestInitial = logFit.initial;
            }

            // Check for step function pattern
            var stepFit = DetectStepFunction(points);
            if (stepFit.r2 > bestR2)
            {
                bestModel = DecayModel.StepFunction;
                bestR2 = stepFit.r2;
                bestRate = stepFit.rate;
                bestInitial = stepFit.initial;
            }

            // Calculate half-life
            double halfLifeDays;
            if (bestRate > 0.0001)
            {
                halfLifeDays = bestModel switch
                {
                    DecayModel.Exponential => Math.Log(2) / bestRate,
                    DecayModel.PowerLaw => Math.Pow(2, 1.0 / bestRate) - 1.0,
                    DecayModel.Logarithmic => Math.Exp((bestInitial * 0.5) / bestRate) - 1.0,
                    _ => _config.DefaultHalfLifeDays
                };
            }
            else
            {
                halfLifeDays = _config.DefaultHalfLifeDays * 10; // Very slow decay
            }

            halfLifeDays = Math.Clamp(halfLifeDays, 0.1, 3650.0);

            // Current retention estimate
            var daysSinceFirst = (DateTime.UtcNow - baseTime).TotalDays;
            var currentRetention = EstimateRetention(bestModel, bestInitial, bestRate, daysSinceFirst);

            var profile = new DecayCurveProfile
            {
                PromptId = promptId,
                FittedModel = bestModel,
                HalfLife = TimeSpan.FromDays(halfLifeDays),
                InitialStrength = bestInitial,
                DecayRate = bestRate,
                RSquared = Math.Clamp(bestR2, 0.0, 1.0),
                CurrentRetention = Math.Clamp(currentRetention, 0.0, 1.0),
                Phase = ClassifyPhase(currentRetention, daysSinceFirst),
                LastFitted = DateTime.UtcNow
            };

            _profiles[promptId] = profile;
            return profile;
        }

        /// <summary>Gets the current retention phase for a prompt.</summary>
        public RetentionPhase GetRetentionPhase(string promptId)
        {
            if (_profiles.TryGetValue(promptId, out var profile))
                return profile.Phase;

            if (!_observations.ContainsKey(promptId) || _observations[promptId].Count == 0)
                return RetentionPhase.Fresh;

            var sorted = _observations[promptId].OrderBy(o => o.Timestamp).ToList();
            var daysSinceFirst = (DateTime.UtcNow - sorted[0].Timestamp).TotalDays;
            var latestComposite = sorted.Last().CompositeScore / 100.0;

            return ClassifyPhase(latestComposite, daysSinceFirst);
        }

        /// <summary>Assesses how durable a prompt is based on its decay characteristics.</summary>
        public DurabilityAssessment AssessDurability(string promptId)
        {
            var profile = _profiles.ContainsKey(promptId) ? _profiles[promptId] : FitDecayCurve(promptId);
            var factors = new List<string>();
            double score = 50.0;

            // Half-life contribution (40%)
            var halfLifeDays = profile.HalfLife.TotalDays;
            var halfLifeScore = Math.Clamp(halfLifeDays / 90.0 * 100.0, 0, 100);
            score = halfLifeScore * 0.4;

            if (halfLifeDays > 60) factors.Add("Long half-life indicates stable domain");
            if (halfLifeDays < 7) factors.Add("Very short half-life suggests high context sensitivity");

            // Variance stability (25%)
            double varianceScore = 50.0;
            if (_observations.ContainsKey(promptId) && _observations[promptId].Count >= 3)
            {
                var composites = _observations[promptId].Select(o => o.CompositeScore).ToList();
                var mean = composites.Average();
                var variance = composites.Sum(c => (c - mean) * (c - mean)) / composites.Count;
                var cv = mean > 0 ? Math.Sqrt(variance) / mean : 1.0;
                varianceScore = Math.Clamp((1.0 - cv) * 100.0, 0, 100);

                if (cv < 0.1) factors.Add("Very consistent performance (low variance)");
                if (cv > 0.3) factors.Add("High performance variance indicates instability");
            }
            score += varianceScore * 0.25;

            // Recovery rate (20%)
            var recoveries = DetectRecoveryEvents(promptId);
            double recoveryScore = 50.0;
            if (recoveries.Count > 0)
            {
                var avgStrength = recoveries.Average(r => r.RecoveryStrength);
                recoveryScore = Math.Clamp(avgStrength * 200.0, 0, 100);
                factors.Add($"Shows recovery ability (avg strength: {avgStrength:F2})");
            }
            else if (_observations.ContainsKey(promptId) && _observations[promptId].Count >= 5)
            {
                factors.Add("No recovery events detected — monotonic decay");
                recoveryScore = 30.0;
            }
            score += recoveryScore * 0.20;

            // Context independence (15%) — measured by tag diversity stability
            double contextScore = 50.0;
            if (_observations.ContainsKey(promptId))
            {
                var taggedObs = _observations[promptId].Where(o => o.Tags.Count > 0).ToList();
                if (taggedObs.Count >= 3)
                {
                    var tagGroups = taggedObs.GroupBy(o => string.Join(",", o.Tags.OrderBy(t => t)));
                    var groupScores = tagGroups.Select(g => g.Average(o => o.CompositeScore)).ToList();
                    if (groupScores.Count >= 2)
                    {
                        var range = groupScores.Max() - groupScores.Min();
                        contextScore = Math.Clamp((1.0 - range / 100.0) * 100.0, 0, 100);
                        if (range < 10) factors.Add("Performs consistently across contexts");
                        if (range > 30) factors.Add("Performance varies significantly by context");
                    }
                }
            }
            score += contextScore * 0.15;

            score = Math.Clamp(score, 0, 100);

            var tier = score switch
            {
                >= 80 => DurabilityTier.Evergreen,
                >= 60 => DurabilityTier.Durable,
                >= 40 => DurabilityTier.Standard,
                >= 20 => DurabilityTier.Fragile,
                _ => DurabilityTier.Ephemeral
            };

            return new DurabilityAssessment
            {
                PromptId = promptId,
                Tier = tier,
                Score = Math.Round(score, 1),
                Factors = factors,
                PredictedLifespan = TimeSpan.FromDays(halfLifeDays * 2)
            };
        }

        /// <summary>Predicts when maintenance will be needed for a prompt.</summary>
        public MaintenanceWindow PredictMaintenanceWindow(string promptId)
        {
            var profile = _profiles.ContainsKey(promptId) ? _profiles[promptId] : FitDecayCurve(promptId);
            var baseTime = DateTime.UtcNow;

            if (_observations.ContainsKey(promptId) && _observations[promptId].Count > 0)
                baseTime = _observations[promptId].Min(o => o.Timestamp);

            var daysSinceStart = (DateTime.UtcNow - baseTime).TotalDays;

            // Find days until retention hits thresholds
            DateTime? below80 = FindThresholdDate(profile, daysSinceStart, 0.8, baseTime);
            DateTime? below50 = FindThresholdDate(profile, daysSinceStart, 0.5, baseTime);

            var urgency = DetermineUrgency(profile.CurrentRetention, below80);
            var strategy = DetermineStrategy(profile.CurrentRetention, profile.HalfLife.TotalDays);

            var effort = strategy switch
            {
                RefreshStrategy.MinorTweak => "15–30 minutes",
                RefreshStrategy.Rephrase => "30–60 minutes",
                RefreshStrategy.Restructure => "1–2 hours",
                RefreshStrategy.FullRewrite => "2–4 hours",
                RefreshStrategy.Retire => "30 minutes (deprecation)",
                _ => "Unknown"
            };

            return new MaintenanceWindow
            {
                PromptId = promptId,
                Urgency = urgency,
                PredictedDecayBelow80 = below80,
                PredictedDecayBelow50 = below50,
                RecommendedRefreshDate = below80?.AddDays(-7) ?? DateTime.UtcNow.AddDays(profile.HalfLife.TotalDays * 0.7),
                Strategy = strategy,
                EstimatedEffort = effort
            };
        }

        /// <summary>Generates a spaced repetition review schedule for maintaining a prompt.</summary>
        public SpacedRepetitionSchedule GenerateSpacedSchedule(string promptId)
        {
            var profile = _profiles.ContainsKey(promptId) ? _profiles[promptId] : FitDecayCurve(promptId);

            if (_schedules.TryGetValue(promptId, out var existing))
                return existing;

            // SM-2 inspired intervals, scaled by half-life
            var scaleFactor = Math.Max(profile.HalfLife.TotalDays / 30.0, 0.5);
            var baseIntervals = new[] { 1.0, 3.0, 7.0, 14.0, 30.0, 60.0, 120.0 };
            var scaledIntervals = baseIntervals.Select(d => TimeSpan.FromDays(d * scaleFactor)).ToList();

            var schedule = new SpacedRepetitionSchedule
            {
                PromptId = promptId,
                ReviewIntervals = scaledIntervals,
                NextReviewDate = DateTime.UtcNow.AddDays(scaledIntervals[0].TotalDays),
                ReviewCount = 0,
                SuccessiveCorrectCount = 0
            };

            _schedules[promptId] = schedule;
            return schedule;
        }

        /// <summary>Marks a review as completed (success or failure) and updates the schedule.</summary>
        public SpacedRepetitionSchedule CompleteReview(string promptId, bool success)
        {
            var schedule = _schedules.ContainsKey(promptId) ? _schedules[promptId] : GenerateSpacedSchedule(promptId);

            schedule.ReviewCount++;

            if (success)
            {
                schedule.SuccessiveCorrectCount++;
                var nextIndex = Math.Min(schedule.SuccessiveCorrectCount, schedule.ReviewIntervals.Count - 1);
                schedule.NextReviewDate = DateTime.UtcNow.Add(schedule.ReviewIntervals[nextIndex]);
            }
            else
            {
                schedule.SuccessiveCorrectCount = 0;
                schedule.NextReviewDate = DateTime.UtcNow.Add(schedule.ReviewIntervals[0]);
            }

            _schedules[promptId] = schedule;
            return schedule;
        }

        /// <summary>Gets a fleet-wide retention health report.</summary>
        public FleetRetentionReport GetFleetReport()
        {
            // Ensure all prompts have profiles
            foreach (var kvp in _observations)
            {
                if (!_profiles.ContainsKey(kvp.Key) && kvp.Value.Count >= _config.MinObservationsForFit)
                    FitDecayCurve(kvp.Key);
            }

            var profiles = _profiles.Values.ToList();
            var report = new FleetRetentionReport
            {
                TotalPrompts = _observations.Count,
                FreshCount = profiles.Count(p => p.Phase == RetentionPhase.Fresh),
                ConsolidatingCount = profiles.Count(p => p.Phase == RetentionPhase.Consolidating),
                StableCount = profiles.Count(p => p.Phase == RetentionPhase.Stable),
                DecayingCount = profiles.Count(p => p.Phase == RetentionPhase.Decaying),
                ForgottenCount = profiles.Count(p => p.Phase == RetentionPhase.Forgotten)
            };

            // Overall health: weighted by retention
            if (profiles.Count > 0)
                report.OverallHealthScore = Math.Round(profiles.Average(p => p.CurrentRetention) * 100.0, 1);

            // Top decaying
            report.TopDecaying = profiles
                .Where(p => p.Phase == RetentionPhase.Decaying || p.Phase == RetentionPhase.Forgotten)
                .OrderBy(p => p.CurrentRetention)
                .Take(_config.FleetTopN)
                .ToList();

            // Most durable
            report.MostDurable = profiles
                .OrderByDescending(p => p.HalfLife.TotalDays)
                .Take(_config.FleetTopN)
                .ToList();

            // Maintenance backlog
            report.MaintenanceBacklog = profiles
                .Select(p => PredictMaintenanceWindow(p.PromptId))
                .Where(m => m.Urgency >= MaintenanceUrgency.Approaching)
                .OrderByDescending(m => m.Urgency)
                .Take(_config.FleetTopN * 2)
                .ToList();

            // Generate insights
            report.AutonomousInsights = GenerateInsights();

            return report;
        }

        /// <summary>Gets priority-ranked recommendations for prompts needing refresh.</summary>
        public List<RefreshRecommendation> GetRefreshRecommendations(int topN = 10)
        {
            var recommendations = new List<RefreshRecommendation>();

            foreach (var kvp in _profiles)
            {
                var profile = kvp.Value;
                if (profile.CurrentRetention >= _config.HealthyRetentionThreshold)
                    continue;

                var strategy = DetermineStrategy(profile.CurrentRetention, profile.HalfLife.TotalDays);
                var priority = (1.0 - profile.CurrentRetention) * 100.0;

                // Boost priority for fast-decaying prompts
                if (profile.HalfLife.TotalDays < 7) priority *= 1.5;

                var expectedGain = strategy switch
                {
                    RefreshStrategy.MinorTweak => 0.15,
                    RefreshStrategy.Rephrase => 0.30,
                    RefreshStrategy.Restructure => 0.50,
                    RefreshStrategy.FullRewrite => 0.80,
                    RefreshStrategy.Retire => 0.0,
                    _ => 0.20
                };

                var reasoning = BuildRefreshReasoning(profile, strategy);

                recommendations.Add(new RefreshRecommendation
                {
                    PromptId = kvp.Key,
                    Strategy = strategy,
                    Reasoning = reasoning,
                    PriorityScore = Math.Round(priority, 1),
                    ExpectedRetentionGain = expectedGain
                });
            }

            return recommendations.OrderByDescending(r => r.PriorityScore).Take(topN).ToList();
        }

        /// <summary>Projects future retention values for the specified number of days.</summary>
        public List<(int Day, double Retention)> SimulateDecay(string promptId, int daysForward)
        {
            var profile = _profiles.ContainsKey(promptId) ? _profiles[promptId] : FitDecayCurve(promptId);
            var results = new List<(int Day, double Retention)>();

            var daysSinceStart = 0.0;
            if (_observations.ContainsKey(promptId) && _observations[promptId].Count > 0)
                daysSinceStart = (DateTime.UtcNow - _observations[promptId].Min(o => o.Timestamp)).TotalDays;

            for (int d = 0; d <= daysForward; d++)
            {
                var t = daysSinceStart + d;
                var retention = EstimateRetention(profile.FittedModel, profile.InitialStrength, profile.DecayRate, t);
                results.Add((d, Math.Clamp(Math.Round(retention, 4), 0.0, 1.0)));
            }

            return results;
        }

        /// <summary>Detects events where a prompt's performance recovered after decay.</summary>
        public List<RecoveryEvent> DetectRecoveryEvents(string promptId)
        {
            var events = new List<RecoveryEvent>();
            if (!_observations.ContainsKey(promptId) || _observations[promptId].Count < 3)
                return events;

            var sorted = _observations[promptId].OrderBy(o => o.Timestamp).ToList();
            var maxScore = sorted.Max(o => o.CompositeScore);
            if (maxScore < 1.0) return events;

            for (int i = 2; i < sorted.Count; i++)
            {
                var prev = sorted[i - 1].CompositeScore / maxScore;
                var curr = sorted[i].CompositeScore / maxScore;
                var prevPrev = sorted[i - 2].CompositeScore / maxScore;

                // Recovery: previous was declining but current jumped up significantly
                if (prevPrev > prev && curr > prev && (curr - prev) > 0.1)
                {
                    events.Add(new RecoveryEvent
                    {
                        PromptId = promptId,
                        RecoveryDate = sorted[i].Timestamp,
                        PreRecoveryRetention = prev,
                        PostRecoveryRetention = curr
                    });
                }
            }

            return events;
        }

        /// <summary>Generates autonomous insights about fleet retention patterns.</summary>
        public List<string> GenerateInsights()
        {
            var insights = new List<string>();
            var profiles = _profiles.Values.ToList();

            if (profiles.Count == 0)
            {
                insights.Add("No prompts have been tracked long enough for insights.");
                return insights;
            }

            // Fleet-level statistics
            var avgRetention = profiles.Average(p => p.CurrentRetention);
            var avgHalfLife = profiles.Average(p => p.HalfLife.TotalDays);

            insights.Add($"Fleet average retention: {avgRetention:P0} across {profiles.Count} prompts.");
            insights.Add($"Average half-life: {avgHalfLife:F1} days.");

            // Identify model distribution
            var modelCounts = profiles.GroupBy(p => p.FittedModel)
                .OrderByDescending(g => g.Count())
                .ToList();
            var dominantModel = modelCounts.First();
            insights.Add($"Dominant decay pattern: {dominantModel.Key} ({dominantModel.Count()}/{profiles.Count} prompts).");

            // Phase distribution concern
            var decayingPct = profiles.Count(p => p.Phase == RetentionPhase.Decaying || p.Phase == RetentionPhase.Forgotten)
                / (double)profiles.Count;
            if (decayingPct > 0.4)
                insights.Add($"⚠️ {decayingPct:P0} of prompts are in Decaying/Forgotten phase — fleet-wide refresh campaign recommended.");

            // Half-life outliers
            var shortLived = profiles.Where(p => p.HalfLife.TotalDays < 5).ToList();
            if (shortLived.Count > 0)
                insights.Add($"⚡ {shortLived.Count} prompt(s) have half-life under 5 days — consider restructuring or retirement.");

            var longLived = profiles.Where(p => p.HalfLife.TotalDays > 90).ToList();
            if (longLived.Count > 0)
                insights.Add($"🌿 {longLived.Count} prompt(s) are evergreen (half-life > 90 days) — study their patterns for best practices.");

            // Recovery patterns
            var allRecoveries = _observations.Keys.SelectMany(id => DetectRecoveryEvents(id)).ToList();
            if (allRecoveries.Count > 0)
            {
                var avgRecoveryStrength = allRecoveries.Average(r => r.RecoveryStrength);
                insights.Add($"🔄 {allRecoveries.Count} recovery event(s) detected, avg strength: {avgRecoveryStrength:F2}.");
            }

            return insights;
        }

        /// <summary>Gets the number of observations recorded for a prompt.</summary>
        public int GetObservationCount(string promptId)
        {
            return _observations.ContainsKey(promptId) ? _observations[promptId].Count : 0;
        }

        /// <summary>Gets all tracked prompt IDs.</summary>
        public List<string> GetTrackedPromptIds()
        {
            return _observations.Keys.ToList();
        }

        // ─── Private Helpers ───────────────────────────────────────

        private RetentionPhase ClassifyPhase(double retention, double daysSinceFirst)
        {
            if (daysSinceFirst < 3) return RetentionPhase.Fresh;
            if (retention < 0.3) return RetentionPhase.Forgotten;
            if (retention < 0.5) return RetentionPhase.Decaying;
            if (daysSinceFirst <= 14 && retention >= 0.8) return RetentionPhase.Consolidating;
            if (retention >= 0.7) return RetentionPhase.Stable;
            return RetentionPhase.Decaying;
        }

        private double EstimateRetention(DecayModel model, double initial, double rate, double t)
        {
            if (t <= 0) return initial;
            return model switch
            {
                DecayModel.Exponential => initial * Math.Exp(-rate * t),
                DecayModel.PowerLaw => initial * Math.Pow(t + 1, -rate),
                DecayModel.Logarithmic => Math.Max(0, initial - rate * Math.Log(t + 1)),
                DecayModel.StepFunction => t < (1.0 / (rate + 0.001)) ? initial : initial * 0.2,
                _ => initial * Math.Exp(-rate * t)
            };
        }

        private (double initial, double rate, double r2) FitExponential(List<(double t, double r)> points)
        {
            if (points.Count < 2) return (1.0, 0.01, 0.0);

            // Log-linear regression: ln(R) = ln(R₀) - λt
            var validPoints = points.Where(p => p.r > 0.01).ToList();
            if (validPoints.Count < 2) return (1.0, 0.01, 0.0);

            var n = validPoints.Count;
            var sumT = validPoints.Sum(p => p.t);
            var sumLnR = validPoints.Sum(p => Math.Log(p.r));
            var sumTLnR = validPoints.Sum(p => p.t * Math.Log(p.r));
            var sumT2 = validPoints.Sum(p => p.t * p.t);

            var denom = n * sumT2 - sumT * sumT;
            if (Math.Abs(denom) < 1e-10) return (1.0, 0.01, 0.0);

            var slope = (n * sumTLnR - sumT * sumLnR) / denom;
            var intercept = (sumLnR - slope * sumT) / n;

            var initial = Math.Clamp(Math.Exp(intercept), 0.1, 2.0);
            var rate = Math.Max(-slope, 0.0001);

            // Calculate R²
            var r2 = CalculateR2(points, t => initial * Math.Exp(-rate * t));
            return (initial, rate, r2);
        }

        private (double initial, double rate, double r2) FitPowerLaw(List<(double t, double r)> points)
        {
            if (points.Count < 2) return (1.0, 0.5, 0.0);

            // Log-log regression: ln(R) = ln(R₀) - b × ln(t+1)
            var validPoints = points.Where(p => p.r > 0.01).ToList();
            if (validPoints.Count < 2) return (1.0, 0.5, 0.0);

            var n = validPoints.Count;
            var sumLnT = validPoints.Sum(p => Math.Log(p.t + 1));
            var sumLnR = validPoints.Sum(p => Math.Log(p.r));
            var sumLnTLnR = validPoints.Sum(p => Math.Log(p.t + 1) * Math.Log(p.r));
            var sumLnT2 = validPoints.Sum(p => Math.Log(p.t + 1) * Math.Log(p.t + 1));

            var denom = n * sumLnT2 - sumLnT * sumLnT;
            if (Math.Abs(denom) < 1e-10) return (1.0, 0.5, 0.0);

            var slope = (n * sumLnTLnR - sumLnT * sumLnR) / denom;
            var intercept = (sumLnR - slope * sumLnT) / n;

            var initial = Math.Clamp(Math.Exp(intercept), 0.1, 2.0);
            var rate = Math.Max(-slope, 0.0001);

            var r2 = CalculateR2(points, t => initial * Math.Pow(t + 1, -rate));
            return (initial, rate, r2);
        }

        private (double initial, double rate, double r2) FitLogarithmic(List<(double t, double r)> points)
        {
            if (points.Count < 2) return (1.0, 0.1, 0.0);

            // Linear regression: R = R₀ - c × ln(t+1)
            var n = points.Count;
            var sumLnT = points.Sum(p => Math.Log(p.t + 1));
            var sumR = points.Sum(p => p.r);
            var sumLnTR = points.Sum(p => Math.Log(p.t + 1) * p.r);
            var sumLnT2 = points.Sum(p => Math.Log(p.t + 1) * Math.Log(p.t + 1));

            var denom = n * sumLnT2 - sumLnT * sumLnT;
            if (Math.Abs(denom) < 1e-10) return (1.0, 0.1, 0.0);

            var slope = (n * sumLnTR - sumLnT * sumR) / denom;
            var intercept = (sumR - slope * sumLnT) / n;

            var initial = Math.Clamp(intercept, 0.1, 2.0);
            var rate = Math.Max(-slope, 0.0001);

            var r2 = CalculateR2(points, t => Math.Max(0, initial - rate * Math.Log(t + 1)));
            return (initial, rate, r2);
        }

        private (double initial, double rate, double r2) DetectStepFunction(List<(double t, double r)> points)
        {
            if (points.Count < 4) return (1.0, 0.01, 0.0);

            // Find best split point
            double bestR2 = 0.0;
            double bestStepT = points[points.Count / 2].t;
            var initial = points.Take(points.Count / 3).Average(p => p.r);

            for (int split = 2; split < points.Count - 1; split++)
            {
                var preAvg = points.Take(split).Average(p => p.r);
                var postAvg = points.Skip(split).Average(p => p.r);

                if (preAvg - postAvg > 0.2) // Significant drop
                {
                    var splitT = points[split].t;
                    var r2 = CalculateR2(points, t => t < splitT ? preAvg : postAvg);
                    if (r2 > bestR2)
                    {
                        bestR2 = r2;
                        bestStepT = splitT;
                        initial = preAvg;
                    }
                }
            }

            var rate = bestStepT > 0 ? 1.0 / bestStepT : 0.01;
            return (initial, rate, bestR2);
        }

        private double CalculateR2(List<(double t, double r)> points, Func<double, double> model)
        {
            var mean = points.Average(p => p.r);
            var ssRes = points.Sum(p => Math.Pow(p.r - model(p.t), 2));
            var ssTot = points.Sum(p => Math.Pow(p.r - mean, 2));

            if (ssTot < 1e-10) return 0.0;
            return Math.Clamp(1.0 - ssRes / ssTot, 0.0, 1.0);
        }

        private DateTime? FindThresholdDate(DecayCurveProfile profile, double currentDays, double threshold, DateTime baseTime)
        {
            if (profile.CurrentRetention <= threshold)
                return DateTime.UtcNow; // Already below threshold

            // Binary search for the day when retention crosses threshold
            double low = currentDays;
            double high = currentDays + profile.HalfLife.TotalDays * 5;

            for (int i = 0; i < 50; i++)
            {
                var mid = (low + high) / 2.0;
                var retention = EstimateRetention(profile.FittedModel, profile.InitialStrength, profile.DecayRate, mid);
                if (retention > threshold)
                    low = mid;
                else
                    high = mid;
            }

            var daysFromNow = low - currentDays;
            if (daysFromNow < 0) return DateTime.UtcNow;
            if (daysFromNow > 3650) return null; // More than 10 years away

            return DateTime.UtcNow.AddDays(daysFromNow);
        }

        private MaintenanceUrgency DetermineUrgency(double currentRetention, DateTime? below80)
        {
            if (currentRetention < 0.5) return MaintenanceUrgency.Overdue;
            if (currentRetention < 0.6) return MaintenanceUrgency.Urgent;

            if (below80 == null) return MaintenanceUrgency.None;

            var daysUntil = (below80.Value - DateTime.UtcNow).TotalDays;
            if (daysUntil <= 0) return MaintenanceUrgency.Overdue;
            if (daysUntil <= 7) return MaintenanceUrgency.Urgent;
            if (daysUntil <= 14) return MaintenanceUrgency.Approaching;
            if (daysUntil <= 30) return MaintenanceUrgency.Routine;
            return MaintenanceUrgency.None;
        }

        private RefreshStrategy DetermineStrategy(double currentRetention, double halfLifeDays)
        {
            if (currentRetention < 0.2) return RefreshStrategy.Retire;
            if (currentRetention < 0.3) return RefreshStrategy.FullRewrite;
            if (currentRetention < 0.5) return RefreshStrategy.Restructure;
            if (currentRetention < 0.7) return RefreshStrategy.Rephrase;
            return RefreshStrategy.MinorTweak;
        }

        private string BuildRefreshReasoning(DecayCurveProfile profile, RefreshStrategy strategy)
        {
            var sb = new StringBuilder();
            sb.Append($"Retention at {profile.CurrentRetention:P0} (phase: {profile.Phase}). ");
            sb.Append($"Half-life: {profile.HalfLife.TotalDays:F0} days. ");
            sb.Append($"Decay model: {profile.FittedModel} (R²={profile.RSquared:F2}). ");

            switch (strategy)
            {
                case RefreshStrategy.Retire:
                    sb.Append("Prompt has decayed beyond recovery threshold — recommend retirement.");
                    break;
                case RefreshStrategy.FullRewrite:
                    sb.Append("Severe decay requires complete reimagining of the prompt.");
                    break;
                case RefreshStrategy.Restructure:
                    sb.Append("Significant decay indicates structural issues needing reorganization.");
                    break;
                case RefreshStrategy.Rephrase:
                    sb.Append("Moderate decay — rephrasing key instructions should restore effectiveness.");
                    break;
                case RefreshStrategy.MinorTweak:
                    sb.Append("Slight drift — small adjustments will maintain performance.");
                    break;
            }

            return sb.ToString();
        }
    }
}
