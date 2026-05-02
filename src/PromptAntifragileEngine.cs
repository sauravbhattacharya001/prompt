namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptAntifragileEngine – Autonomous Antifragility Analysis
    //
    //  Inspired by Nassim Taleb's antifragility concept, this engine
    //  stress-tests prompts under progressive stressor intensity and
    //  classifies them on the Fragile → Robust → Resilient → Antifragile
    //  spectrum.  Antifragile prompts actually improve under stress;
    //  fragile ones collapse.
    //
    //  7 engines: StressorGenerator, StressResponseTracker,
    //  FragilityClassifier, BreakpointDetector, RecoveryAnalyzer,
    //  HardeningRecommender, InsightGenerator.
    //
    //  Unlike PromptMutationLab (mutation testing for resilience zones)
    //  or PromptFuzzer (random input fuzzing), this engine applies
    //  graduated, multi-dimensional stress with dose-response curve
    //  analysis — prompts that gain from disorder.
    // ────────────────────────────────────────────

    /// <summary>Classification on the Fragile–Antifragile spectrum.</summary>
    public enum FragilityClass
    {
        /// <summary>Performance degrades disproportionately under stress.</summary>
        Fragile,
        /// <summary>Performance stays roughly constant under stress.</summary>
        Robust,
        /// <summary>Performance degrades but recovers after stress removal.</summary>
        Resilient,
        /// <summary>Performance actually improves under moderate stress.</summary>
        Antifragile
    }

    /// <summary>Category of stressor applied to a prompt.</summary>
    public enum StressorType
    {
        /// <summary>Token budget progressively squeezed.</summary>
        TokenCompression,
        /// <summary>Ambiguous, contradictory, or noisy input.</summary>
        InputNoise,
        /// <summary>Latency/timeout constraints tightened.</summary>
        LatencyPressure,
        /// <summary>Context window approaching capacity.</summary>
        ContextOverload,
        /// <summary>Model downgrade (GPT-4 → GPT-3.5 → smaller).</summary>
        ModelDegradation,
        /// <summary>Adversarial phrasing or injection attempts.</summary>
        AdversarialInput,
        /// <summary>Rapid sequential executions (rate stress).</summary>
        ThroughputFlood,
        /// <summary>Combined multi-dimensional stress.</summary>
        Composite
    }

    /// <summary>Intensity level of a stressor (1–10 scale).</summary>
    public enum StressIntensity
    {
        /// <summary>Minimal stress — near normal conditions.</summary>
        Minimal = 1,
        /// <summary>Light stress.</summary>
        Light = 2,
        /// <summary>Moderate stress.</summary>
        Moderate = 4,
        /// <summary>Heavy stress.</summary>
        Heavy = 6,
        /// <summary>Severe stress.</summary>
        Severe = 8,
        /// <summary>Extreme — maximum possible stress.</summary>
        Extreme = 10
    }

    /// <summary>Health tier for antifragility analysis.</summary>
    public enum AntifragileHealthTier
    {
        /// <summary>Score 85–100: highly antifragile.</summary>
        Thriving,
        /// <summary>Score 70–84: resilient, gains from some stress.</summary>
        Resilient,
        /// <summary>Score 50–69: robust but doesn't improve.</summary>
        Stable,
        /// <summary>Score 30–49: fragile under moderate stress.</summary>
        Vulnerable,
        /// <summary>Score 0–29: collapses under minimal stress.</summary>
        Brittle
    }

    /// <summary>A single stressor applied to a prompt during testing.</summary>
    public class Stressor
    {
        /// <summary>Unique stressor identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];

        /// <summary>Type of stress applied.</summary>
        public StressorType Type { get; set; }

        /// <summary>Intensity on 1–10 scale.</summary>
        public int Intensity { get; set; } = 1;

        /// <summary>Human-readable description of what was done.</summary>
        public string Description { get; set; } = "";

        /// <summary>When the stressor was applied.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Observed response to a stressor.</summary>
    public class StressResponse
    {
        /// <summary>Prompt being tested.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Stressor that was applied.</summary>
        public string StressorId { get; set; } = "";

        /// <summary>Stressor type for convenience.</summary>
        public StressorType StressorType { get; set; }

        /// <summary>Stress intensity level.</summary>
        public int Intensity { get; set; }

        /// <summary>Quality score of the response under stress (0.0–1.0).</summary>
        public double QualityScore { get; set; }

        /// <summary>Baseline quality score without stress (0.0–1.0).</summary>
        public double BaselineQuality { get; set; } = 1.0;

        /// <summary>Whether the prompt execution succeeded at all.</summary>
        public bool Succeeded { get; set; } = true;

        /// <summary>Latency in milliseconds under stress.</summary>
        public double LatencyMs { get; set; }

        /// <summary>Token usage under stress.</summary>
        public int TokensUsed { get; set; }

        /// <summary>When observed.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Breakpoint where prompt behavior changes significantly.</summary>
    public class StressBreakpoint
    {
        /// <summary>Stressor type at the breakpoint.</summary>
        public StressorType StressorType { get; set; }

        /// <summary>Intensity at which the break occurs.</summary>
        public int BreakIntensity { get; set; }

        /// <summary>Quality before break.</summary>
        public double QualityBefore { get; set; }

        /// <summary>Quality after break.</summary>
        public double QualityAfter { get; set; }

        /// <summary>Magnitude of quality drop (positive = degradation).</summary>
        public double DropMagnitude { get; set; }

        /// <summary>Classification: cliff, gradual, or threshold.</summary>
        public string BreakType { get; set; } = "cliff";
    }

    /// <summary>Recovery observation after stress is removed.</summary>
    public class RecoveryObservation
    {
        /// <summary>Prompt tested.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Stressor type that was removed.</summary>
        public StressorType StressorType { get; set; }

        /// <summary>Quality during peak stress.</summary>
        public double StressedQuality { get; set; }

        /// <summary>Quality after stress removal.</summary>
        public double RecoveredQuality { get; set; }

        /// <summary>Original baseline quality.</summary>
        public double BaselineQuality { get; set; }

        /// <summary>Recovery ratio: 1.0 = full recovery, >1.0 = antifragile gain.</summary>
        public double RecoveryRatio { get; set; }

        /// <summary>Time taken to recover in seconds.</summary>
        public double RecoveryTimeSeconds { get; set; }
    }

    /// <summary>Per-stressor-type vulnerability profile for a prompt.</summary>
    public class VulnerabilityProfile
    {
        /// <summary>Stressor type.</summary>
        public StressorType StressorType { get; set; }

        /// <summary>Number of stress tests conducted.</summary>
        public int TestCount { get; set; }

        /// <summary>Mean quality under stress (0.0–1.0).</summary>
        public double MeanStressQuality { get; set; }

        /// <summary>Quality degradation slope per intensity unit.</summary>
        public double DegradationSlope { get; set; }

        /// <summary>Classification for this stressor dimension.</summary>
        public FragilityClass Classification { get; set; }

        /// <summary>Breakpoint intensity (null if none found).</summary>
        public int? BreakpointIntensity { get; set; }

        /// <summary>Failure rate under stress.</summary>
        public double FailureRate { get; set; }
    }

    /// <summary>Hardening recommendation to improve antifragility.</summary>
    public class AntifragileRecommendation
    {
        /// <summary>Target stressor dimension.</summary>
        public StressorType TargetStressor { get; set; }

        /// <summary>Priority: 1 = most urgent.</summary>
        public int Priority { get; set; }

        /// <summary>Recommendation description.</summary>
        public string Recommendation { get; set; } = "";

        /// <summary>Expected improvement in antifragility score.</summary>
        public double ExpectedImprovement { get; set; }

        /// <summary>Effort estimate: Low, Medium, High.</summary>
        public string Effort { get; set; } = "Medium";
    }

    /// <summary>Configuration for the antifragile engine.</summary>
    public class AntifragileConfig
    {
        /// <summary>Minimum stress tests per stressor type before classification.</summary>
        public int MinTestsPerStressor { get; set; } = 3;

        /// <summary>Quality drop threshold (fraction) to detect a breakpoint.</summary>
        public double BreakpointDropThreshold { get; set; } = 0.15;

        /// <summary>Slope threshold to separate Robust from Fragile.</summary>
        public double RobustSlopeThreshold { get; set; } = 0.02;

        /// <summary>Recovery ratio above which we classify as Antifragile.</summary>
        public double AntifragileRecoveryThreshold { get; set; } = 1.05;

        /// <summary>Maximum history entries per prompt per stressor type.</summary>
        public int MaxHistoryPerDimension { get; set; } = 100;
    }

    /// <summary>Full antifragility snapshot for a prompt.</summary>
    public class AntifragileSnapshot
    {
        /// <summary>Prompt identifier.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Overall antifragility class.</summary>
        public FragilityClass OverallClass { get; set; }

        /// <summary>Composite antifragility score 0–100.</summary>
        public double AntifragileScore { get; set; }

        /// <summary>Health tier derived from score.</summary>
        public AntifragileHealthTier HealthTier { get; set; }

        /// <summary>Per-stressor vulnerability profiles.</summary>
        public List<VulnerabilityProfile> VulnerabilityProfiles { get; set; } = new();

        /// <summary>Detected breakpoints.</summary>
        public List<StressBreakpoint> Breakpoints { get; set; } = new();

        /// <summary>Recovery observations.</summary>
        public List<RecoveryObservation> Recoveries { get; set; } = new();

        /// <summary>Prioritized hardening recommendations.</summary>
        public List<AntifragileRecommendation> Recommendations { get; set; } = new();

        /// <summary>Total stress tests conducted.</summary>
        public int TotalTests { get; set; }

        /// <summary>Overall failure rate under stress.</summary>
        public double OverallFailureRate { get; set; }

        /// <summary>Weakest stressor dimension.</summary>
        public StressorType? WeakestDimension { get; set; }

        /// <summary>Strongest stressor dimension.</summary>
        public StressorType? StrongestDimension { get; set; }
    }

    /// <summary>Fleet-wide antifragility report across all prompts.</summary>
    public class AntifragileFleetReport
    {
        /// <summary>Total prompts analyzed.</summary>
        public int TotalPrompts { get; set; }

        /// <summary>Total stress tests conducted.</summary>
        public int TotalTests { get; set; }

        /// <summary>Distribution of fragility classes across fleet.</summary>
        public Dictionary<FragilityClass, int> ClassDistribution { get; set; } = new();

        /// <summary>Distribution of health tiers.</summary>
        public Dictionary<AntifragileHealthTier, int> TierDistribution { get; set; } = new();

        /// <summary>Mean antifragility score across fleet.</summary>
        public double MeanScore { get; set; }

        /// <summary>Most common weakness across the fleet.</summary>
        public StressorType? FleetWeakness { get; set; }

        /// <summary>Top vulnerable prompts.</summary>
        public List<AntifragileSnapshot> MostVulnerable { get; set; } = new();

        /// <summary>Top antifragile prompts.</summary>
        public List<AntifragileSnapshot> MostAntifragile { get; set; } = new();

        /// <summary>Fleet health score 0–100.</summary>
        public double FleetHealthScore { get; set; }

        /// <summary>Autonomous insights about fleet antifragility.</summary>
        public List<string> AutonomousInsights { get; set; } = new();
    }

    /// <summary>
    /// Autonomous antifragility analysis engine for prompts.
    /// Applies graduated stress tests across multiple dimensions, tracks
    /// dose-response curves, detects breakpoints, measures recovery,
    /// and classifies prompts on the Fragile–Antifragile spectrum.
    /// </summary>
    public class PromptAntifragileEngine
    {
        private readonly AntifragileConfig _config;

        // promptId → stressorType → list of responses
        private readonly Dictionary<string, Dictionary<StressorType, List<StressResponse>>> _responses = new();

        // promptId → list of recovery observations
        private readonly Dictionary<string, List<RecoveryObservation>> _recoveries = new();

        /// <summary>Create an antifragile engine with optional configuration.</summary>
        public PromptAntifragileEngine(AntifragileConfig? config = null)
        {
            _config = config ?? new AntifragileConfig();
        }

        // ── StressorGenerator ───────────────────────────

        /// <summary>Generate a graduated series of stressors for a given type.</summary>
        public List<Stressor> GenerateStressors(StressorType type, int maxIntensity = 10)
        {
            if (maxIntensity < 1 || maxIntensity > 10)
                throw new ArgumentOutOfRangeException(nameof(maxIntensity), "Must be 1–10.");

            var stressors = new List<Stressor>();
            for (int i = 1; i <= maxIntensity; i++)
            {
                stressors.Add(new Stressor
                {
                    Type = type,
                    Intensity = i,
                    Description = GetStressorDescription(type, i),
                    Timestamp = DateTime.UtcNow
                });
            }
            return stressors;
        }

        /// <summary>Generate a composite stress battery across all dimensions.</summary>
        public List<Stressor> GenerateCompositeBattery(int intensityLevels = 5)
        {
            if (intensityLevels < 1 || intensityLevels > 10)
                throw new ArgumentOutOfRangeException(nameof(intensityLevels), "Must be 1–10.");

            var battery = new List<Stressor>();
            var types = Enum.GetValues<StressorType>().Where(t => t != StressorType.Composite);
            foreach (var type in types)
            {
                for (int i = 1; i <= intensityLevels; i++)
                {
                    battery.Add(new Stressor
                    {
                        Type = type,
                        Intensity = i,
                        Description = GetStressorDescription(type, i)
                    });
                }
            }
            return battery;
        }

        // ── StressResponseTracker ───────────────────────

        /// <summary>Record a stress response observation.</summary>
        public void RecordResponse(StressResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (string.IsNullOrWhiteSpace(response.PromptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(response));

            if (!_responses.ContainsKey(response.PromptId))
                _responses[response.PromptId] = new Dictionary<StressorType, List<StressResponse>>();

            var byType = _responses[response.PromptId];
            if (!byType.ContainsKey(response.StressorType))
                byType[response.StressorType] = new List<StressResponse>();

            var list = byType[response.StressorType];
            list.Add(response);

            // Trim to max history
            while (list.Count > _config.MaxHistoryPerDimension)
                list.RemoveAt(0);
        }

        /// <summary>Record a recovery observation after stress removal.</summary>
        public void RecordRecovery(RecoveryObservation recovery)
        {
            if (recovery == null) throw new ArgumentNullException(nameof(recovery));
            if (string.IsNullOrWhiteSpace(recovery.PromptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(recovery));

            if (!_recoveries.ContainsKey(recovery.PromptId))
                _recoveries[recovery.PromptId] = new List<RecoveryObservation>();

            recovery.RecoveryRatio = recovery.BaselineQuality > 0
                ? recovery.RecoveredQuality / recovery.BaselineQuality
                : 0;

            _recoveries[recovery.PromptId].Add(recovery);
        }

        /// <summary>Get all recorded responses for a prompt.</summary>
        public Dictionary<StressorType, List<StressResponse>> GetResponses(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            return _responses.TryGetValue(promptId, out var byType)
                ? byType.ToDictionary(kv => kv.Key, kv => kv.Value.ToList())
                : new Dictionary<StressorType, List<StressResponse>>();
        }

        /// <summary>Get all recovery observations for a prompt.</summary>
        public List<RecoveryObservation> GetRecoveries(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            return _recoveries.TryGetValue(promptId, out var list) ? list.ToList() : new();
        }

        // ── FragilityClassifier ─────────────────────────

        /// <summary>Classify a prompt's fragility for a specific stressor type.</summary>
        public FragilityClass ClassifyDimension(string promptId, StressorType type)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            if (!_responses.TryGetValue(promptId, out var byType) ||
                !byType.TryGetValue(type, out var responses) ||
                responses.Count < _config.MinTestsPerStressor)
                return FragilityClass.Robust; // default when insufficient data

            // Check recovery data for antifragile signal
            if (_recoveries.TryGetValue(promptId, out var recoveries))
            {
                var typeRecoveries = recoveries.Where(r => r.StressorType == type).ToList();
                if (typeRecoveries.Any(r => r.RecoveryRatio >= _config.AntifragileRecoveryThreshold))
                    return FragilityClass.Antifragile;
            }

            // Compute degradation slope via least-squares on intensity vs quality
            double slope = ComputeDegradationSlope(responses);

            // Check for quality improvement under stress (antifragile)
            if (slope > _config.RobustSlopeThreshold)
                return FragilityClass.Antifragile;

            // Robust: slope near zero
            if (Math.Abs(slope) <= _config.RobustSlopeThreshold)
                return FragilityClass.Robust;

            // Resilient vs Fragile: check recovery
            if (_recoveries.TryGetValue(promptId, out var recov))
            {
                var typeRecov = recov.Where(r => r.StressorType == type).ToList();
                if (typeRecov.Count > 0 && typeRecov.Average(r => r.RecoveryRatio) >= 0.9)
                    return FragilityClass.Resilient;
            }

            return FragilityClass.Fragile;
        }

        /// <summary>Classify overall fragility across all tested dimensions.</summary>
        public FragilityClass ClassifyOverall(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            if (!_responses.TryGetValue(promptId, out var byType) || byType.Count == 0)
                return FragilityClass.Robust;

            var classifications = byType.Keys
                .Select(t => ClassifyDimension(promptId, t))
                .ToList();

            // Worst class wins for overall assessment
            if (classifications.Contains(FragilityClass.Fragile))
                return FragilityClass.Fragile;
            if (classifications.Contains(FragilityClass.Resilient))
                return FragilityClass.Resilient;
            if (classifications.Contains(FragilityClass.Antifragile))
                return FragilityClass.Antifragile;
            return FragilityClass.Robust;
        }

        // ── BreakpointDetector ──────────────────────────

        /// <summary>Detect breakpoints where quality drops sharply.</summary>
        public List<StressBreakpoint> DetectBreakpoints(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var breakpoints = new List<StressBreakpoint>();

            if (!_responses.TryGetValue(promptId, out var byType))
                return breakpoints;

            foreach (var (type, responses) in byType)
            {
                if (responses.Count < 2) continue;

                // Sort by intensity, group, and compute average quality at each level
                var byIntensity = responses
                    .GroupBy(r => r.Intensity)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Intensity = g.Key, AvgQuality = g.Average(r => r.QualityScore) })
                    .ToList();

                for (int i = 1; i < byIntensity.Count; i++)
                {
                    double drop = byIntensity[i - 1].AvgQuality - byIntensity[i].AvgQuality;
                    if (drop >= _config.BreakpointDropThreshold)
                    {
                        string breakType = drop >= 0.4 ? "cliff" : drop >= 0.25 ? "threshold" : "gradual";
                        breakpoints.Add(new StressBreakpoint
                        {
                            StressorType = type,
                            BreakIntensity = byIntensity[i].Intensity,
                            QualityBefore = Math.Round(byIntensity[i - 1].AvgQuality, 3),
                            QualityAfter = Math.Round(byIntensity[i].AvgQuality, 3),
                            DropMagnitude = Math.Round(drop, 3),
                            BreakType = breakType
                        });
                    }
                }
            }

            return breakpoints;
        }

        // ── RecoveryAnalyzer ────────────────────────────

        /// <summary>Analyze recovery patterns for a prompt.</summary>
        public Dictionary<StressorType, double> AnalyzeRecovery(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var result = new Dictionary<StressorType, double>();

            if (!_recoveries.TryGetValue(promptId, out var recoveries) || recoveries.Count == 0)
                return result;

            foreach (var group in recoveries.GroupBy(r => r.StressorType))
            {
                result[group.Key] = Math.Round(group.Average(r => r.RecoveryRatio), 3);
            }

            return result;
        }

        // ── HardeningRecommender ────────────────────────

        /// <summary>Generate prioritized hardening recommendations.</summary>
        public List<AntifragileRecommendation> GetRecommendations(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var recommendations = new List<AntifragileRecommendation>();
            var profiles = BuildVulnerabilityProfiles(promptId);

            int priority = 0;
            foreach (var profile in profiles
                .Where(p => p.Classification == FragilityClass.Fragile)
                .OrderBy(p => p.MeanStressQuality))
            {
                priority++;
                var (rec, effort, improvement) = GetHardeningAdvice(profile.StressorType, profile);
                recommendations.Add(new AntifragileRecommendation
                {
                    TargetStressor = profile.StressorType,
                    Priority = priority,
                    Recommendation = rec,
                    ExpectedImprovement = improvement,
                    Effort = effort
                });
            }

            // Add recommendations for Robust prompts that could become Antifragile
            foreach (var profile in profiles
                .Where(p => p.Classification == FragilityClass.Robust)
                .OrderByDescending(p => p.TestCount))
            {
                priority++;
                recommendations.Add(new AntifragileRecommendation
                {
                    TargetStressor = profile.StressorType,
                    Priority = priority,
                    Recommendation = $"Consider adding adaptive fallbacks for {profile.StressorType} stress to push from Robust toward Antifragile.",
                    ExpectedImprovement = 5,
                    Effort = "Medium"
                });
            }

            return recommendations;
        }

        // ── Snapshot & Fleet ────────────────────────────

        /// <summary>Get a full antifragility snapshot for a prompt.</summary>
        public AntifragileSnapshot GetSnapshot(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var profiles = BuildVulnerabilityProfiles(promptId);
            var breakpoints = DetectBreakpoints(promptId);
            var recoveries = GetRecoveries(promptId);
            var overallClass = ClassifyOverall(promptId);
            var score = ComputeAntifragileScore(promptId, profiles);

            int totalTests = profiles.Sum(p => p.TestCount);
            double overallFailure = totalTests > 0
                ? profiles.Sum(p => p.FailureRate * p.TestCount) / totalTests
                : 0;

            var weakest = profiles.OrderBy(p => p.MeanStressQuality).FirstOrDefault();
            var strongest = profiles.OrderByDescending(p => p.MeanStressQuality).FirstOrDefault();

            return new AntifragileSnapshot
            {
                PromptId = promptId,
                OverallClass = overallClass,
                AntifragileScore = score,
                HealthTier = ScoreToTier(score),
                VulnerabilityProfiles = profiles,
                Breakpoints = breakpoints,
                Recoveries = recoveries,
                Recommendations = GetRecommendations(promptId),
                TotalTests = totalTests,
                OverallFailureRate = Math.Round(overallFailure, 3),
                WeakestDimension = weakest?.StressorType,
                StrongestDimension = strongest?.StressorType
            };
        }

        /// <summary>Get fleet-wide antifragility report.</summary>
        public AntifragileFleetReport GetFleetReport()
        {
            var snapshots = _responses.Keys.Select(GetSnapshot).ToList();

            var classDist = new Dictionary<FragilityClass, int>();
            var tierDist = new Dictionary<AntifragileHealthTier, int>();
            foreach (var fc in Enum.GetValues<FragilityClass>()) classDist[fc] = 0;
            foreach (var t in Enum.GetValues<AntifragileHealthTier>()) tierDist[t] = 0;

            foreach (var snap in snapshots)
            {
                classDist[snap.OverallClass]++;
                tierDist[snap.HealthTier]++;
            }

            // Find fleet-wide weakness
            var allWeaknesses = snapshots
                .Where(s => s.WeakestDimension.HasValue)
                .GroupBy(s => s.WeakestDimension!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            double meanScore = snapshots.Count > 0 ? Math.Round(snapshots.Average(s => s.AntifragileScore), 1) : 0;

            var report = new AntifragileFleetReport
            {
                TotalPrompts = snapshots.Count,
                TotalTests = snapshots.Sum(s => s.TotalTests),
                ClassDistribution = classDist,
                TierDistribution = tierDist,
                MeanScore = meanScore,
                FleetWeakness = allWeaknesses?.Key,
                MostVulnerable = snapshots.OrderBy(s => s.AntifragileScore).Take(5).ToList(),
                MostAntifragile = snapshots.OrderByDescending(s => s.AntifragileScore).Take(5).ToList(),
                FleetHealthScore = meanScore,
                AutonomousInsights = GenerateFleetInsights(snapshots, classDist)
            };

            return report;
        }

        /// <summary>Get all tracked prompt IDs.</summary>
        public List<string> GetTrackedPrompts()
        {
            return _responses.Keys.ToList();
        }

        // ── HTML Dashboard ──────────────────────────────

        /// <summary>Generate an interactive HTML dashboard for a prompt's antifragility analysis.</summary>
        public string GenerateHtmlDashboard(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("PromptId cannot be empty.", nameof(promptId));

            var snapshot = GetSnapshot(promptId);
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
            sb.AppendLine("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            sb.AppendLine($"<title>Antifragile Analysis — {EscapeHtml(promptId)}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:system-ui,-apple-system,sans-serif;background:#0f172a;color:#e2e8f0;padding:2rem}");
            sb.AppendLine(".header{text-align:center;margin-bottom:2rem}");
            sb.AppendLine(".header h1{font-size:1.8rem;color:#f8fafc}");
            sb.AppendLine(".score-badge{display:inline-block;padding:.5rem 1.5rem;border-radius:2rem;font-size:1.2rem;font-weight:700;margin:.5rem}");
            sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(350px,1fr));gap:1.5rem}");
            sb.AppendLine(".card{background:#1e293b;border-radius:12px;padding:1.5rem;border:1px solid #334155}");
            sb.AppendLine(".card h2{font-size:1.1rem;color:#94a3b8;margin-bottom:1rem;border-bottom:1px solid #334155;padding-bottom:.5rem}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;font-size:.85rem}");
            sb.AppendLine("th,td{padding:.4rem .6rem;text-align:left;border-bottom:1px solid #334155}");
            sb.AppendLine("th{color:#64748b;font-weight:600}");
            sb.AppendLine(".fragile{color:#ef4444}.robust{color:#3b82f6}.resilient{color:#f59e0b}.antifragile{color:#22c55e}");
            sb.AppendLine(".bar{height:8px;border-radius:4px;margin-top:4px}");
            sb.AppendLine(".insight{background:#1a2332;border-left:3px solid #3b82f6;padding:.6rem 1rem;margin:.4rem 0;font-size:.85rem;border-radius:0 6px 6px 0}");
            sb.AppendLine("</style></head><body>");

            // Header
            string tierColor = snapshot.HealthTier switch
            {
                AntifragileHealthTier.Thriving => "#22c55e",
                AntifragileHealthTier.Resilient => "#f59e0b",
                AntifragileHealthTier.Stable => "#3b82f6",
                AntifragileHealthTier.Vulnerable => "#f97316",
                _ => "#ef4444"
            };
            sb.AppendLine("<div class='header'>");
            sb.AppendLine($"<h1>⚡ Antifragile Analysis — {EscapeHtml(promptId)}</h1>");
            sb.AppendLine($"<span class='score-badge' style='background:{tierColor}20;color:{tierColor};border:2px solid {tierColor}'>");
            sb.AppendLine($"Score: {snapshot.AntifragileScore:F1} — {snapshot.HealthTier}</span>");
            sb.AppendLine($"<br><span class='score-badge {snapshot.OverallClass.ToString().ToLower()}'>{snapshot.OverallClass}</span>");
            sb.AppendLine($"<br><span style='color:#94a3b8'>Tests: {snapshot.TotalTests} | Failure rate: {snapshot.OverallFailureRate:P0}</span>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='grid'>");

            // Vulnerability Profiles
            sb.AppendLine("<div class='card'><h2>🛡️ Vulnerability Profiles</h2><table>");
            sb.AppendLine("<tr><th>Stressor</th><th>Class</th><th>Avg Quality</th><th>Slope</th><th>Fail%</th></tr>");
            foreach (var p in snapshot.VulnerabilityProfiles)
            {
                string cls = p.Classification.ToString().ToLower();
                sb.AppendLine($"<tr><td>{p.StressorType}</td><td class='{cls}'>{p.Classification}</td>");
                sb.AppendLine($"<td>{p.MeanStressQuality:F3}</td><td>{p.DegradationSlope:F4}</td>");
                sb.AppendLine($"<td>{p.FailureRate:P0}</td></tr>");
            }
            sb.AppendLine("</table></div>");

            // Breakpoints
            sb.AppendLine("<div class='card'><h2>💥 Breakpoints</h2>");
            if (snapshot.Breakpoints.Count == 0)
            {
                sb.AppendLine("<p style='color:#64748b'>No breakpoints detected — prompt handles graduated stress smoothly.</p>");
            }
            else
            {
                sb.AppendLine("<table><tr><th>Stressor</th><th>Intensity</th><th>Before</th><th>After</th><th>Drop</th><th>Type</th></tr>");
                foreach (var bp in snapshot.Breakpoints)
                {
                    sb.AppendLine($"<tr><td>{bp.StressorType}</td><td>{bp.BreakIntensity}</td>");
                    sb.AppendLine($"<td>{bp.QualityBefore:F3}</td><td>{bp.QualityAfter:F3}</td>");
                    sb.AppendLine($"<td style='color:#ef4444'>-{bp.DropMagnitude:F3}</td><td>{bp.BreakType}</td></tr>");
                }
                sb.AppendLine("</table>");
            }
            sb.AppendLine("</div>");

            // Recoveries
            sb.AppendLine("<div class='card'><h2>🔄 Recovery Analysis</h2>");
            if (snapshot.Recoveries.Count == 0)
            {
                sb.AppendLine("<p style='color:#64748b'>No recovery observations recorded yet.</p>");
            }
            else
            {
                sb.AppendLine("<table><tr><th>Stressor</th><th>Stressed</th><th>Recovered</th><th>Ratio</th></tr>");
                foreach (var r in snapshot.Recoveries)
                {
                    string ratioColor = r.RecoveryRatio >= 1.05 ? "#22c55e" : r.RecoveryRatio >= 0.9 ? "#f59e0b" : "#ef4444";
                    sb.AppendLine($"<tr><td>{r.StressorType}</td><td>{r.StressedQuality:F3}</td>");
                    sb.AppendLine($"<td>{r.RecoveredQuality:F3}</td>");
                    sb.AppendLine($"<td style='color:{ratioColor}'>{r.RecoveryRatio:F3}</td></tr>");
                }
                sb.AppendLine("</table>");
            }
            sb.AppendLine("</div>");

            // Recommendations
            sb.AppendLine("<div class='card'><h2>🔧 Hardening Recommendations</h2>");
            if (snapshot.Recommendations.Count == 0)
            {
                sb.AppendLine("<p style='color:#64748b'>No recommendations — prompt is performing well under stress.</p>");
            }
            else
            {
                foreach (var rec in snapshot.Recommendations.Take(8))
                {
                    sb.AppendLine($"<div class='insight'><strong>#{rec.Priority} [{rec.TargetStressor}]</strong> (Effort: {rec.Effort}, +{rec.ExpectedImprovement:F0} pts)<br>{EscapeHtml(rec.Recommendation)}</div>");
                }
            }
            sb.AppendLine("</div>");

            sb.AppendLine("</div>"); // grid
            sb.AppendLine($"<p style='text-align:center;color:#475569;margin-top:2rem;font-size:.75rem'>Generated by PromptAntifragileEngine — {DateTime.UtcNow:u}</p>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        // ── Private Helpers ─────────────────────────────

        private List<VulnerabilityProfile> BuildVulnerabilityProfiles(string promptId)
        {
            var profiles = new List<VulnerabilityProfile>();

            if (!_responses.TryGetValue(promptId, out var byType))
                return profiles;

            foreach (var (type, responses) in byType)
            {
                double meanQ = responses.Average(r => r.QualityScore);
                double slope = ComputeDegradationSlope(responses);
                double failRate = responses.Count > 0
                    ? (double)responses.Count(r => !r.Succeeded) / responses.Count
                    : 0;

                var breakpoints = DetectBreakpointsForType(responses, type);

                profiles.Add(new VulnerabilityProfile
                {
                    StressorType = type,
                    TestCount = responses.Count,
                    MeanStressQuality = Math.Round(meanQ, 3),
                    DegradationSlope = Math.Round(slope, 4),
                    Classification = ClassifyDimension(promptId, type),
                    BreakpointIntensity = breakpoints.FirstOrDefault()?.BreakIntensity,
                    FailureRate = Math.Round(failRate, 3)
                });
            }

            return profiles.OrderBy(p => p.MeanStressQuality).ToList();
        }

        private List<StressBreakpoint> DetectBreakpointsForType(List<StressResponse> responses, StressorType type)
        {
            var breakpoints = new List<StressBreakpoint>();
            if (responses.Count < 2) return breakpoints;

            var byIntensity = responses
                .GroupBy(r => r.Intensity)
                .OrderBy(g => g.Key)
                .Select(g => new { Intensity = g.Key, AvgQuality = g.Average(r => r.QualityScore) })
                .ToList();

            for (int i = 1; i < byIntensity.Count; i++)
            {
                double drop = byIntensity[i - 1].AvgQuality - byIntensity[i].AvgQuality;
                if (drop >= _config.BreakpointDropThreshold)
                {
                    string breakType = drop >= 0.4 ? "cliff" : drop >= 0.25 ? "threshold" : "gradual";
                    breakpoints.Add(new StressBreakpoint
                    {
                        StressorType = type,
                        BreakIntensity = byIntensity[i].Intensity,
                        QualityBefore = Math.Round(byIntensity[i - 1].AvgQuality, 3),
                        QualityAfter = Math.Round(byIntensity[i].AvgQuality, 3),
                        DropMagnitude = Math.Round(drop, 3),
                        BreakType = breakType
                    });
                }
            }

            return breakpoints;
        }

        private double ComputeDegradationSlope(List<StressResponse> responses)
        {
            if (responses.Count < 2) return 0;

            // Least-squares regression: quality = slope * intensity + intercept
            double n = responses.Count;
            double sumX = responses.Sum(r => (double)r.Intensity);
            double sumY = responses.Sum(r => r.QualityScore);
            double sumXY = responses.Sum(r => r.Intensity * r.QualityScore);
            double sumX2 = responses.Sum(r => (double)r.Intensity * r.Intensity);

            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-12) return 0;

            return (n * sumXY - sumX * sumY) / denom;
        }

        private double ComputeAntifragileScore(string promptId, List<VulnerabilityProfile> profiles)
        {
            if (profiles.Count == 0) return 50; // neutral when no data

            double score = 0;

            // Base score from classification distribution (40 points)
            double classPoints = profiles.Average(p => p.Classification switch
            {
                FragilityClass.Antifragile => 100,
                FragilityClass.Robust => 70,
                FragilityClass.Resilient => 50,
                FragilityClass.Fragile => 20,
                _ => 50
            });
            score += classPoints * 0.40;

            // Mean stress quality (25 points)
            double meanQ = profiles.Average(p => p.MeanStressQuality);
            score += meanQ * 100 * 0.25;

            // Recovery bonus (20 points)
            var recoveryRatios = AnalyzeRecovery(promptId);
            if (recoveryRatios.Count > 0)
            {
                double avgRecovery = recoveryRatios.Values.Average();
                double recoveryScore = Math.Min(avgRecovery / 1.1 * 100, 100); // 1.1 ratio = 100
                score += recoveryScore * 0.20;
            }
            else
            {
                score += 50 * 0.20; // neutral when no recovery data
            }

            // Low failure rate bonus (15 points)
            double avgFailRate = profiles.Average(p => p.FailureRate);
            score += (1 - avgFailRate) * 100 * 0.15;

            return Math.Round(Math.Clamp(score, 0, 100), 1);
        }

        private static AntifragileHealthTier ScoreToTier(double score) => score switch
        {
            >= 85 => AntifragileHealthTier.Thriving,
            >= 70 => AntifragileHealthTier.Resilient,
            >= 50 => AntifragileHealthTier.Stable,
            >= 30 => AntifragileHealthTier.Vulnerable,
            _ => AntifragileHealthTier.Brittle
        };

        private static string GetStressorDescription(StressorType type, int intensity) => type switch
        {
            StressorType.TokenCompression => $"Squeeze token budget to {100 - intensity * 10}% of normal",
            StressorType.InputNoise => $"Inject {intensity * 10}% noise/ambiguity into input",
            StressorType.LatencyPressure => $"Reduce timeout to {100 - intensity * 8}% of baseline",
            StressorType.ContextOverload => $"Fill context window to {50 + intensity * 5}% capacity",
            StressorType.ModelDegradation => $"Downgrade model capability by {intensity} tiers",
            StressorType.AdversarialInput => $"Apply level-{intensity} adversarial perturbation",
            StressorType.ThroughputFlood => $"Send {intensity * 10} concurrent requests",
            StressorType.Composite => $"Apply {intensity}-dimension combined stress",
            _ => $"Stress level {intensity}"
        };

        private static (string rec, string effort, double improvement) GetHardeningAdvice(
            StressorType type, VulnerabilityProfile profile)
        {
            return type switch
            {
                StressorType.TokenCompression => (
                    "Add progressive summarization fallback: when token budget shrinks, auto-compress context while preserving key information.",
                    "Medium", 12),
                StressorType.InputNoise => (
                    "Add input normalization and clarification prompts: detect ambiguity, request clarification, or apply noise-tolerant parsing.",
                    "High", 15),
                StressorType.LatencyPressure => (
                    "Implement tiered response strategy: provide quick partial answers under time pressure, with full answers when time allows.",
                    "Medium", 10),
                StressorType.ContextOverload => (
                    "Add context prioritization: score context relevance and evict lowest-value content when window fills.",
                    "High", 14),
                StressorType.ModelDegradation => (
                    "Design model-agnostic prompts: reduce reliance on advanced reasoning, add explicit step-by-step instructions.",
                    "Medium", 11),
                StressorType.AdversarialInput => (
                    "Strengthen injection guards and add adversarial robustness layers: input validation, output verification, guardrails.",
                    "High", 16),
                StressorType.ThroughputFlood => (
                    "Add request deduplication and intelligent batching to handle throughput spikes gracefully.",
                    "Low", 8),
                StressorType.Composite => (
                    "Add graceful degradation modes: detect multi-dimensional stress and activate survival-mode with essential functions only.",
                    "High", 13),
                _ => ("Review and harden this stress dimension.", "Medium", 8)
            };
        }

        private static List<string> GenerateFleetInsights(
            List<AntifragileSnapshot> snapshots,
            Dictionary<FragilityClass, int> classDist)
        {
            var insights = new List<string>();
            if (snapshots.Count == 0)
            {
                insights.Add("No prompts tracked yet — run stress tests to begin antifragility analysis.");
                return insights;
            }

            int total = snapshots.Count;
            int fragileCount = classDist.GetValueOrDefault(FragilityClass.Fragile);
            int antifragileCount = classDist.GetValueOrDefault(FragilityClass.Antifragile);

            if (fragileCount > total / 2)
                insights.Add($"⚠️ {fragileCount}/{total} prompts are Fragile — fleet is vulnerable to stress. Prioritize hardening.");
            else if (antifragileCount > total / 2)
                insights.Add($"✅ {antifragileCount}/{total} prompts are Antifragile — fleet gains from disorder.");

            // Common weakness
            var weaknesses = snapshots
                .Where(s => s.WeakestDimension.HasValue)
                .GroupBy(s => s.WeakestDimension!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (weaknesses != null && weaknesses.Count() >= 2)
                insights.Add($"🎯 Common fleet weakness: {weaknesses.Key} ({weaknesses.Count()} prompts). Consider fleet-wide hardening for this dimension.");

            double avgScore = snapshots.Average(s => s.AntifragileScore);
            if (avgScore < 40)
                insights.Add($"📉 Fleet average score {avgScore:F1} is below 40 — significant fragility risk across the portfolio.");
            else if (avgScore >= 75)
                insights.Add($"📈 Fleet average score {avgScore:F1} indicates strong overall resilience.");

            // Breakpoint analysis
            int breakpointPrompts = snapshots.Count(s => s.Breakpoints.Count > 0);
            if (breakpointPrompts > 0)
                insights.Add($"💥 {breakpointPrompts} prompt(s) have stress breakpoints — sudden quality cliffs under pressure.");

            if (insights.Count == 0)
                insights.Add("Fleet antifragility is within normal parameters.");

            return insights;
        }

        private static string EscapeHtml(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
