namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptChaosEngine – Autonomous Chaos Engineering for Prompt Operations
    //
    //  Inspired by Netflix's Chaos Monkey: systematically inject controlled
    //  faults into prompt operations to measure resilience before real
    //  failures strike.  Designs chaos experiments, injects perturbations,
    //  measures blast radius through dependency graphs, scores resilience
    //  across 6 dimensions, and verifies recovery — all autonomously.
    //
    //  7 engines: ExperimentDesigner, FaultInjector, BlastRadiusEstimator,
    //  ResilienceScorer, RecoveryVerifier, SteadyStateValidator,
    //  InsightGenerator.
    //
    //  Unlike PromptAntifragileEngine (graduated stress testing) or
    //  PromptCircuitBreakerEngine (real-time failure isolation), this
    //  engine focuses on proactive controlled experimentation — breaking
    //  things on purpose so you know what happens before production does.
    // ────────────────────────────────────────────

    /// <summary>Type of fault to inject during a chaos experiment.</summary>
    public enum ChaosInjectionType
    {
        /// <summary>Corrupt random tokens in the prompt text.</summary>
        TokenCorruption,
        /// <summary>Simulate abnormally high response latency.</summary>
        LatencySpike,
        /// <summary>Return a truncated/partial response.</summary>
        PartialResponse,
        /// <summary>Simulate switching to a different model mid-operation.</summary>
        ModelSwitch,
        /// <summary>Shift temperature parameter to extreme values.</summary>
        TemperatureShift,
        /// <summary>Truncate context window to a fraction of normal.</summary>
        ContextTruncation,
        /// <summary>Introduce encoding errors (mojibake).</summary>
        EncodingCorruption,
        /// <summary>Simulate rate limit / throttling responses.</summary>
        RateLimitSimulation,
        /// <summary>Simulate concurrent request overload.</summary>
        ConcurrentOverload,
        /// <summary>Simulate failure of a downstream dependency.</summary>
        DependencyFailure
    }

    /// <summary>Phase of a chaos experiment lifecycle.</summary>
    public enum ExperimentPhase
    {
        /// <summary>Collecting baseline steady-state metrics.</summary>
        Baseline,
        /// <summary>Fault is actively being injected.</summary>
        Injection,
        /// <summary>Observing system behavior under fault.</summary>
        Observation,
        /// <summary>Fault removed, system recovering.</summary>
        Recovery,
        /// <summary>Verifying system returned to steady state.</summary>
        Verification,
        /// <summary>Experiment complete.</summary>
        Completed
    }

    /// <summary>Resilience tier classification.</summary>
    public enum ResilienceTier
    {
        /// <summary>Score 85–100: system improves under stress.</summary>
        Antifragile,
        /// <summary>Score 70–84: system handles faults gracefully.</summary>
        Resilient,
        /// <summary>Score 50–69: system degrades but survives.</summary>
        Tolerant,
        /// <summary>Score 30–49: system struggles under faults.</summary>
        Fragile,
        /// <summary>Score 0–29: system breaks under minor faults.</summary>
        Brittle
    }

    /// <summary>Overall experiment outcome.</summary>
    public enum ExperimentVerdict
    {
        /// <summary>System maintained acceptable behavior throughout.</summary>
        Passed,
        /// <summary>System degraded but recovered.</summary>
        Degraded,
        /// <summary>System failed to recover within tolerance.</summary>
        Failed,
        /// <summary>System experienced cascading/total failure.</summary>
        Catastrophic
    }

    /// <summary>How far a fault propagated.</summary>
    public enum BlastRadiusLevel
    {
        /// <summary>Fault affected only the target prompt.</summary>
        Isolated,
        /// <summary>Fault affected immediate dependencies only.</summary>
        Contained,
        /// <summary>Fault propagated 2+ hops through dependency graph.</summary>
        Spreading,
        /// <summary>Fault affected majority of connected prompts.</summary>
        Widespread,
        /// <summary>Fault affected entire prompt system.</summary>
        Systemic
    }

    /// <summary>Type of autonomous insight.</summary>
    public enum ChaosInsightType
    {
        /// <summary>A single point of failure was discovered.</summary>
        SinglePointOfFailure,
        /// <summary>Missing fallback mechanism detected.</summary>
        MissingFallback,
        /// <summary>Slow recovery pattern identified.</summary>
        SlowRecovery,
        /// <summary>Cascading failure risk found.</summary>
        CascadeRisk,
        /// <summary>Specific injection type is consistently problematic.</summary>
        WeakSpot,
        /// <summary>System showed improvement under stress.</summary>
        StrengthFound,
        /// <summary>Untested injection type / coverage gap.</summary>
        CoverageGap,
        /// <summary>General recommendation.</summary>
        Recommendation
    }

    /// <summary>Steady-state metrics for baseline and observation comparison.</summary>
    public class SteadyStateMetrics
    {
        /// <summary>Average response latency in milliseconds.</summary>
        public double AvgLatencyMs { get; set; }
        /// <summary>Success rate 0–1.</summary>
        public double SuccessRate { get; set; } = 1.0;
        /// <summary>Average quality score 0–100.</summary>
        public double AvgQuality { get; set; } = 80;
        /// <summary>Average tokens used per request.</summary>
        public double AvgTokens { get; set; }
        /// <summary>Error rate 0–1.</summary>
        public double ErrorRate { get; set; }
        /// <summary>Number of samples in this measurement.</summary>
        public int SampleCount { get; set; } = 1;
        /// <summary>When these metrics were recorded.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A single fault injection event within an experiment.</summary>
    public class ChaosInjection
    {
        /// <summary>Unique injection identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        /// <summary>Type of fault injected.</summary>
        public ChaosInjectionType Type { get; set; }
        /// <summary>Target prompt IDs.</summary>
        public List<string> TargetPromptIds { get; set; } = new();
        /// <summary>Injection-specific parameters.</summary>
        public Dictionary<string, string> Parameters { get; set; } = new();
        /// <summary>When injection started.</summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        /// <summary>Duration of the injection.</summary>
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(30);
        /// <summary>Whether the injection is currently active.</summary>
        public bool Active { get; set; } = true;
    }

    /// <summary>Blast radius analysis for a fault injection.</summary>
    public class BlastRadiusMap
    {
        /// <summary>Prompt IDs directly targeted.</summary>
        public List<string> DirectTargets { get; set; } = new();
        /// <summary>Prompt IDs affected through dependency propagation.</summary>
        public List<string> AffectedPromptIds { get; set; } = new();
        /// <summary>Maximum depth of propagation.</summary>
        public int MaxDepth { get; set; }
        /// <summary>Propagation paths (target → [affected chain]).</summary>
        public Dictionary<string, List<string>> PropagationPaths { get; set; } = new();
        /// <summary>Blast radius classification.</summary>
        public BlastRadiusLevel Level { get; set; } = BlastRadiusLevel.Isolated;
        /// <summary>Total number of affected prompts (direct + indirect).</summary>
        public int TotalAffected => DirectTargets.Count + AffectedPromptIds.Count;
    }

    /// <summary>Resilience measurement for a completed chaos experiment.</summary>
    public class ChaosResilienceReport
    {
        /// <summary>Experiment this report is for.</summary>
        public string ExperimentId { get; set; } = "";
        /// <summary>Recovery time score 0–100 (100 = instant recovery).</summary>
        public double RecoveryTimeScore { get; set; }
        /// <summary>Graceful degradation score 0–100.</summary>
        public double DegradationScore { get; set; }
        /// <summary>Error handling score 0–100.</summary>
        public double ErrorHandlingScore { get; set; }
        /// <summary>Fallback effectiveness score 0–100.</summary>
        public double FallbackScore { get; set; }
        /// <summary>State consistency score 0–100.</summary>
        public double StateConsistencyScore { get; set; }
        /// <summary>User impact score 0–100 (100 = no user impact).</summary>
        public double UserImpactScore { get; set; }
        /// <summary>Composite resilience score 0–100.</summary>
        public double CompositeScore { get; set; }
        /// <summary>Resilience tier classification.</summary>
        public ResilienceTier Tier { get; set; }
        /// <summary>Experiment verdict.</summary>
        public ExperimentVerdict Verdict { get; set; }
        /// <summary>Estimated recovery time in seconds.</summary>
        public double RecoveryTimeSec { get; set; }
        /// <summary>Autonomous recommendations.</summary>
        public List<string> Recommendations { get; set; } = new();
        /// <summary>Blast radius analysis.</summary>
        public BlastRadiusMap? BlastRadius { get; set; }
    }

    /// <summary>An autonomous insight generated from chaos experiment results.</summary>
    public class ChaosInsight
    {
        /// <summary>Type of insight.</summary>
        public ChaosInsightType Type { get; set; }
        /// <summary>Human-readable description.</summary>
        public string Description { get; set; } = "";
        /// <summary>Priority 1–5 (1 = critical).</summary>
        public int Priority { get; set; } = 3;
        /// <summary>Estimated effort to address (hours).</summary>
        public double EffortHours { get; set; }
        /// <summary>Related experiment IDs.</summary>
        public List<string> RelatedExperimentIds { get; set; } = new();
    }

    /// <summary>A chaos experiment definition and its results.</summary>
    public class ChaosExperiment
    {
        /// <summary>Unique experiment identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        /// <summary>Human-readable name.</summary>
        public string Name { get; set; } = "";
        /// <summary>What we expect to happen.</summary>
        public string Hypothesis { get; set; } = "";
        /// <summary>Type of fault to inject.</summary>
        public ChaosInjectionType InjectionType { get; set; }
        /// <summary>Target prompt IDs.</summary>
        public List<string> TargetPromptIds { get; set; } = new();
        /// <summary>Current phase.</summary>
        public ExperimentPhase Phase { get; set; } = ExperimentPhase.Baseline;
        /// <summary>When experiment was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>When experiment completed.</summary>
        public DateTime? CompletedAt { get; set; }
        /// <summary>Baseline steady-state metrics.</summary>
        public SteadyStateMetrics? Baseline { get; set; }
        /// <summary>Metrics observed during fault injection.</summary>
        public List<SteadyStateMetrics> Observations { get; set; } = new();
        /// <summary>Post-recovery metrics.</summary>
        public SteadyStateMetrics? PostRecovery { get; set; }
        /// <summary>Fault injections applied.</summary>
        public List<ChaosInjection> Injections { get; set; } = new();
        /// <summary>Resilience report (set after completion).</summary>
        public ChaosResilienceReport? Report { get; set; }
        /// <summary>Experiment verdict.</summary>
        public ExperimentVerdict? Verdict { get; set; }
    }

    /// <summary>Configuration for the chaos engine.</summary>
    public class ChaosConfig
    {
        /// <summary>Maximum experiments to retain (LRU eviction).</summary>
        public int MaxExperiments { get; set; } = 500;
        /// <summary>Default injection duration in seconds.</summary>
        public double DefaultInjectionDurationSec { get; set; } = 30;
        /// <summary>Recovery tolerance — how close to baseline post-metrics must be (0–1).</summary>
        public double RecoveryTolerance { get; set; } = 0.10;
        /// <summary>Minimum baseline samples before injection is allowed.</summary>
        public int MinBaselineSamples { get; set; } = 1;
        /// <summary>Blast radius threshold for "Systemic" classification (fraction of all known prompts).</summary>
        public double SystemicThreshold { get; set; } = 0.8;
        /// <summary>Blast radius threshold for "Widespread" (fraction).</summary>
        public double WidespreadThreshold { get; set; } = 0.5;
        /// <summary>Blast radius threshold for "Spreading" (fraction).</summary>
        public double SpreadingThreshold { get; set; } = 0.2;
    }

    /// <summary>
    /// Autonomous chaos engineering engine for prompt operations.
    /// Designs experiments, injects faults, measures blast radius,
    /// scores resilience, and generates actionable insights.
    /// </summary>
    public class PromptChaosEngine
    {
        private readonly ChaosConfig _config;
        private readonly Dictionary<string, ChaosExperiment> _experiments = new();
        private readonly List<string> _experimentOrder = new();  // for LRU
        private readonly Dictionary<string, HashSet<string>> _dependencies = new();  // promptId → depends on
        private readonly Dictionary<string, HashSet<string>> _dependents = new();   // promptId → depended on by

        /// <summary>Create a new chaos engine with optional configuration.</summary>
        public PromptChaosEngine(ChaosConfig? config = null)
        {
            _config = config ?? new ChaosConfig();
        }

        // ── Engine 1: Experiment Designer ───────────────

        /// <summary>Design a new chaos experiment.</summary>
        public ChaosExperiment DesignExperiment(string name, ChaosInjectionType type, List<string> targetPromptIds, string hypothesis = "")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Experiment name is required.", nameof(name));
            if (targetPromptIds == null || targetPromptIds.Count == 0)
                throw new ArgumentException("At least one target prompt ID is required.", nameof(targetPromptIds));

            var experiment = new ChaosExperiment
            {
                Name = name,
                InjectionType = type,
                TargetPromptIds = new List<string>(targetPromptIds),
                Hypothesis = hypothesis,
                Phase = ExperimentPhase.Baseline
            };

            EnforceLru();
            _experiments[experiment.Id] = experiment;
            _experimentOrder.Add(experiment.Id);

            return experiment;
        }

        /// <summary>Get all experiments.</summary>
        public List<ChaosExperiment> GetExperiments() => _experiments.Values.ToList();

        /// <summary>Get a specific experiment by ID.</summary>
        public ChaosExperiment? GetExperiment(string experimentId)
        {
            if (string.IsNullOrWhiteSpace(experimentId)) return null;
            return _experiments.TryGetValue(experimentId, out var e) ? e : null;
        }

        // ── Engine 2: Fault Injector ────────────────────

        /// <summary>Record baseline metrics for an experiment.</summary>
        public void RecordBaseline(string experimentId, SteadyStateMetrics metrics)
        {
            var exp = GetOrThrow(experimentId);
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            exp.Baseline = metrics;
            exp.Phase = ExperimentPhase.Baseline;
        }

        /// <summary>Inject a fault for the given experiment. Requires baseline to be recorded first.</summary>
        public ChaosInjection InjectFault(string experimentId)
        {
            var exp = GetOrThrow(experimentId);
            if (exp.Baseline == null)
                throw new InvalidOperationException("Baseline must be recorded before injecting faults.");
            if (exp.Phase == ExperimentPhase.Completed)
                throw new InvalidOperationException("Experiment is already completed.");

            var injection = new ChaosInjection
            {
                Type = exp.InjectionType,
                TargetPromptIds = new List<string>(exp.TargetPromptIds),
                Duration = TimeSpan.FromSeconds(_config.DefaultInjectionDurationSec),
                Parameters = BuildInjectionParams(exp.InjectionType)
            };

            exp.Injections.Add(injection);
            exp.Phase = ExperimentPhase.Injection;
            return injection;
        }

        /// <summary>Record an observation during or after fault injection.</summary>
        public void RecordObservation(string experimentId, SteadyStateMetrics metrics)
        {
            var exp = GetOrThrow(experimentId);
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            exp.Observations.Add(metrics);
            if (exp.Phase == ExperimentPhase.Injection)
                exp.Phase = ExperimentPhase.Observation;
        }

        // ── Engine 3: Blast Radius Estimator ────────────

        /// <summary>Register a dependency: promptId depends on dependsOnId.</summary>
        public void RegisterDependency(string promptId, string dependsOnId)
        {
            if (string.IsNullOrWhiteSpace(promptId)) throw new ArgumentException("promptId required.", nameof(promptId));
            if (string.IsNullOrWhiteSpace(dependsOnId)) throw new ArgumentException("dependsOnId required.", nameof(dependsOnId));

            if (!_dependencies.ContainsKey(promptId))
                _dependencies[promptId] = new HashSet<string>();
            _dependencies[promptId].Add(dependsOnId);

            if (!_dependents.ContainsKey(dependsOnId))
                _dependents[dependsOnId] = new HashSet<string>();
            _dependents[dependsOnId].Add(promptId);
        }

        /// <summary>Get dependencies for a prompt.</summary>
        public List<string> GetDependencies(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId)) return new List<string>();
            return _dependencies.TryGetValue(promptId, out var deps) ? deps.ToList() : new List<string>();
        }

        /// <summary>Estimate the blast radius for an experiment's targets.</summary>
        public BlastRadiusMap EstimateBlastRadius(string experimentId)
        {
            var exp = GetOrThrow(experimentId);
            var map = new BlastRadiusMap
            {
                DirectTargets = new List<string>(exp.TargetPromptIds)
            };

            // BFS through dependents graph: who depends on the targets?
            var visited = new HashSet<string>(exp.TargetPromptIds);
            var queue = new Queue<(string id, int depth)>();
            foreach (var t in exp.TargetPromptIds)
                queue.Enqueue((t, 0));

            int maxDepth = 0;
            var affected = new List<string>();
            var paths = new Dictionary<string, List<string>>();

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (_dependents.TryGetValue(current, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        if (visited.Add(dep))
                        {
                            affected.Add(dep);
                            int newDepth = depth + 1;
                            if (newDepth > maxDepth) maxDepth = newDepth;
                            queue.Enqueue((dep, newDepth));

                            if (!paths.ContainsKey(dep))
                                paths[dep] = new List<string>();
                            paths[dep].Add(current);
                        }
                    }
                }
            }

            map.AffectedPromptIds = affected;
            map.MaxDepth = maxDepth;
            map.PropagationPaths = paths;

            // Classify blast radius level
            int allKnown = AllKnownPromptIds().Count;
            if (allKnown == 0)
            {
                map.Level = BlastRadiusLevel.Isolated;
            }
            else
            {
                double fraction = (double)map.TotalAffected / allKnown;
                if (fraction >= _config.SystemicThreshold) map.Level = BlastRadiusLevel.Systemic;
                else if (fraction >= _config.WidespreadThreshold) map.Level = BlastRadiusLevel.Widespread;
                else if (fraction >= _config.SpreadingThreshold) map.Level = BlastRadiusLevel.Spreading;
                else if (affected.Count > 0) map.Level = BlastRadiusLevel.Contained;
                else map.Level = BlastRadiusLevel.Isolated;
            }

            return map;
        }

        // ── Engine 4: Resilience Scorer ─────────────────

        /// <summary>Score resilience for a completed (or observation-phase) experiment. Returns composite 0–100.</summary>
        public double ScoreResilience(string experimentId)
        {
            var exp = GetOrThrow(experimentId);
            if (exp.Baseline == null) return 0;
            if (exp.Observations.Count == 0) return 100; // no observations = no degradation measured

            var obs = AggregateObservations(exp.Observations);
            var bl = exp.Baseline;

            double recoveryTime = ScoreRecoveryTime(exp);
            double degradation = ScoreDegradation(bl, obs);
            double errorHandling = ScoreErrorHandling(bl, obs);
            double fallback = ScoreFallback(bl, obs);
            double stateConsistency = ScoreStateConsistency(bl, obs);
            double userImpact = ScoreUserImpact(bl, obs);

            // Weighted average: recovery 20%, degradation 20%, errors 15%, fallback 15%, state 15%, user 15%
            return Math.Round(
                recoveryTime * 0.20 +
                degradation * 0.20 +
                errorHandling * 0.15 +
                fallback * 0.15 +
                stateConsistency * 0.15 +
                userImpact * 0.15, 1);
        }

        /// <summary>Complete an experiment and generate its resilience report.</summary>
        public ChaosResilienceReport CompleteExperiment(string experimentId)
        {
            var exp = GetOrThrow(experimentId);
            if (exp.Phase == ExperimentPhase.Completed)
                throw new InvalidOperationException("Experiment is already completed.");

            double composite = ScoreResilience(experimentId);
            var tier = ClassifyTier(composite);
            var verdict = ClassifyVerdict(composite, exp);
            var blastRadius = EstimateBlastRadius(experimentId);

            var bl = exp.Baseline ?? new SteadyStateMetrics();
            var obs = exp.Observations.Count > 0 ? AggregateObservations(exp.Observations) : bl;

            var report = new ChaosResilienceReport
            {
                ExperimentId = experimentId,
                RecoveryTimeScore = ScoreRecoveryTime(exp),
                DegradationScore = ScoreDegradation(bl, obs),
                ErrorHandlingScore = ScoreErrorHandling(bl, obs),
                FallbackScore = ScoreFallback(bl, obs),
                StateConsistencyScore = ScoreStateConsistency(bl, obs),
                UserImpactScore = ScoreUserImpact(bl, obs),
                CompositeScore = composite,
                Tier = tier,
                Verdict = verdict,
                BlastRadius = blastRadius,
                Recommendations = GenerateRecommendations(exp, composite, blastRadius)
            };

            exp.Report = report;
            exp.Verdict = verdict;
            exp.Phase = ExperimentPhase.Completed;
            exp.CompletedAt = DateTime.UtcNow;

            return report;
        }

        // ── Engine 5: Recovery Verifier ─────────────────

        /// <summary>Verify that post-recovery metrics are within tolerance of baseline.</summary>
        public bool VerifyRecovery(string experimentId, SteadyStateMetrics postMetrics)
        {
            var exp = GetOrThrow(experimentId);
            if (postMetrics == null) throw new ArgumentNullException(nameof(postMetrics));
            exp.PostRecovery = postMetrics;
            exp.Phase = ExperimentPhase.Verification;

            if (exp.Baseline == null) return true; // no baseline = can't compare

            double tol = _config.RecoveryTolerance;
            var bl = exp.Baseline;

            bool latencyOk = bl.AvgLatencyMs == 0 || Math.Abs(postMetrics.AvgLatencyMs - bl.AvgLatencyMs) / Math.Max(bl.AvgLatencyMs, 1) <= tol;
            bool successOk = Math.Abs(postMetrics.SuccessRate - bl.SuccessRate) <= tol;
            bool qualityOk = bl.AvgQuality == 0 || Math.Abs(postMetrics.AvgQuality - bl.AvgQuality) / Math.Max(bl.AvgQuality, 1) <= tol;
            bool errorOk = Math.Abs(postMetrics.ErrorRate - bl.ErrorRate) <= tol;

            return latencyOk && successOk && qualityOk && errorOk;
        }

        // ── Engine 6: Steady State Validator ────────────

        /// <summary>Get the recorded baseline for an experiment.</summary>
        public SteadyStateMetrics? GetBaseline(string experimentId)
        {
            return GetOrThrow(experimentId).Baseline;
        }

        /// <summary>Validate that observation metrics are within acceptable steady-state deviation.</summary>
        public bool ValidateSteadyState(string experimentId, SteadyStateMetrics current)
        {
            var exp = GetOrThrow(experimentId);
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (exp.Baseline == null) return true;

            var bl = exp.Baseline;
            double tol = _config.RecoveryTolerance;

            bool latencyOk = bl.AvgLatencyMs == 0 || Math.Abs(current.AvgLatencyMs - bl.AvgLatencyMs) / Math.Max(bl.AvgLatencyMs, 1) <= tol;
            bool successOk = Math.Abs(current.SuccessRate - bl.SuccessRate) <= tol;
            bool qualityOk = bl.AvgQuality == 0 || Math.Abs(current.AvgQuality - bl.AvgQuality) / Math.Max(bl.AvgQuality, 1) <= tol;

            return latencyOk && successOk && qualityOk;
        }

        // ── Engine 7: Insight Generator ─────────────────

        /// <summary>Generate autonomous insights across all experiments.</summary>
        public List<ChaosInsight> GenerateInsights()
        {
            var insights = new List<ChaosInsight>();
            var completed = _experiments.Values.Where(e => e.Phase == ExperimentPhase.Completed).ToList();
            if (completed.Count == 0) return insights;

            // Coverage gap: which injection types haven't been tested?
            var testedTypes = completed.Select(e => e.InjectionType).Distinct().ToHashSet();
            foreach (ChaosInjectionType t in Enum.GetValues(typeof(ChaosInjectionType)))
            {
                if (!testedTypes.Contains(t))
                {
                    insights.Add(new ChaosInsight
                    {
                        Type = ChaosInsightType.CoverageGap,
                        Description = $"Injection type '{t}' has never been tested. Consider running a chaos experiment.",
                        Priority = 2,
                        EffortHours = 1
                    });
                }
            }

            // Weak spots: injection types with consistently low resilience
            var byType = completed.GroupBy(e => e.InjectionType);
            foreach (var grp in byType)
            {
                var avgScore = grp.Where(e => e.Report != null).Select(e => e.Report!.CompositeScore).DefaultIfEmpty(50).Average();
                if (avgScore < 50)
                {
                    insights.Add(new ChaosInsight
                    {
                        Type = ChaosInsightType.WeakSpot,
                        Description = $"Injection type '{grp.Key}' has average resilience score {avgScore:F1} — consistent weakness detected.",
                        Priority = 1,
                        EffortHours = 4,
                        RelatedExperimentIds = grp.Select(e => e.Id).ToList()
                    });
                }
            }

            // Cascade risk: experiments with blast radius >= Spreading
            foreach (var exp in completed.Where(e => e.Report?.BlastRadius != null))
            {
                var level = exp.Report!.BlastRadius!.Level;
                if (level >= BlastRadiusLevel.Spreading)
                {
                    insights.Add(new ChaosInsight
                    {
                        Type = ChaosInsightType.CascadeRisk,
                        Description = $"Experiment '{exp.Name}' showed {level} blast radius — cascading failure risk.",
                        Priority = level >= BlastRadiusLevel.Widespread ? 1 : 2,
                        EffortHours = 6,
                        RelatedExperimentIds = new List<string> { exp.Id }
                    });
                }
            }

            // Slow recovery: experiments where recovery was poor
            foreach (var exp in completed.Where(e => e.Report != null && e.Report.RecoveryTimeScore < 40))
            {
                insights.Add(new ChaosInsight
                {
                    Type = ChaosInsightType.SlowRecovery,
                    Description = $"Experiment '{exp.Name}' had slow recovery (score {exp.Report!.RecoveryTimeScore:F1}).",
                    Priority = 2,
                    EffortHours = 3,
                    RelatedExperimentIds = new List<string> { exp.Id }
                });
            }

            // Missing fallback: low fallback scores
            foreach (var exp in completed.Where(e => e.Report != null && e.Report.FallbackScore < 30))
            {
                insights.Add(new ChaosInsight
                {
                    Type = ChaosInsightType.MissingFallback,
                    Description = $"Experiment '{exp.Name}' revealed missing fallback mechanisms (score {exp.Report!.FallbackScore:F1}).",
                    Priority = 1,
                    EffortHours = 4,
                    RelatedExperimentIds = new List<string> { exp.Id }
                });
            }

            // Strength found: consistently high resilience
            foreach (var exp in completed.Where(e => e.Report != null && e.Report.CompositeScore >= 85))
            {
                insights.Add(new ChaosInsight
                {
                    Type = ChaosInsightType.StrengthFound,
                    Description = $"Experiment '{exp.Name}' demonstrated antifragile behavior (score {exp.Report!.CompositeScore:F1}).",
                    Priority = 5,
                    EffortHours = 0,
                    RelatedExperimentIds = new List<string> { exp.Id }
                });
            }

            // Single point of failure: prompts that appear in many blast radius maps
            var hotspots = new Dictionary<string, int>();
            foreach (var exp in completed.Where(e => e.Report?.BlastRadius != null))
            {
                foreach (var p in exp.Report!.BlastRadius!.AffectedPromptIds)
                {
                    hotspots.TryGetValue(p, out int c);
                    hotspots[p] = c + 1;
                }
            }
            foreach (var kv in hotspots.Where(x => x.Value >= 3))
            {
                insights.Add(new ChaosInsight
                {
                    Type = ChaosInsightType.SinglePointOfFailure,
                    Description = $"Prompt '{kv.Key}' appeared in blast radius of {kv.Value} experiments — potential single point of failure.",
                    Priority = 1,
                    EffortHours = 5
                });
            }

            return insights.OrderBy(i => i.Priority).ToList();
        }

        /// <summary>Get aggregate fleet resilience score across all completed experiments.</summary>
        public double GetFleetResilienceScore()
        {
            var scores = _experiments.Values
                .Where(e => e.Report != null)
                .Select(e => e.Report!.CompositeScore)
                .ToList();
            return scores.Count == 0 ? 0 : Math.Round(scores.Average(), 1);
        }

        // ── HTML Dashboard ──────────────────────────────

        /// <summary>Render an interactive HTML dashboard.</summary>
        public string RenderDashboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<title>Prompt Chaos Engine Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:system-ui;margin:20px;background:#0d1117;color:#c9d1d9}");
            sb.AppendLine("h1,h2,h3{color:#58a6ff}");
            sb.AppendLine(".card{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:16px;margin:12px 0}");
            sb.AppendLine(".score{font-size:2em;font-weight:bold}");
            sb.AppendLine(".antifragile{color:#3fb950}.resilient{color:#58a6ff}.tolerant{color:#d29922}.fragile{color:#f85149}.brittle{color:#da3633}");
            sb.AppendLine(".passed{color:#3fb950}.degraded{color:#d29922}.failed{color:#f85149}.catastrophic{color:#da3633}");
            sb.AppendLine("table{width:100%;border-collapse:collapse}th,td{padding:8px;text-align:left;border-bottom:1px solid #30363d}");
            sb.AppendLine("th{color:#8b949e}.bar{height:12px;border-radius:6px;background:#21262d}.fill{height:12px;border-radius:6px}");
            sb.AppendLine(".tabs{display:flex;gap:8px;margin:12px 0}.tab{padding:8px 16px;cursor:pointer;border:1px solid #30363d;border-radius:6px;background:#0d1117}");
            sb.AppendLine(".tab.active{background:#161b22;border-color:#58a6ff;color:#58a6ff}");
            sb.AppendLine(".panel{display:none}.panel.active{display:block}");
            sb.AppendLine("</style></head><body>");

            var fleet = GetFleetResilienceScore();
            var tier = ClassifyTier(fleet);
            var completed = _experiments.Values.Where(e => e.Phase == ExperimentPhase.Completed).ToList();
            var insights = GenerateInsights();

            sb.AppendLine("<h1>⚡ Prompt Chaos Engine</h1>");

            // Fleet score
            sb.AppendLine("<div class='card'><h2>Fleet Resilience</h2>");
            sb.AppendLine($"<div class='score {tier.ToString().ToLower()}'>{fleet:F1}/100 — {tier}</div>");
            sb.AppendLine($"<p>{completed.Count} experiments completed, {_experiments.Count} total</p></div>");

            // Tabs
            sb.AppendLine("<div class='tabs'>");
            sb.AppendLine("<div class='tab active' onclick='showTab(0)'>Experiments</div>");
            sb.AppendLine("<div class='tab' onclick='showTab(1)'>Insights</div>");
            sb.AppendLine("<div class='tab' onclick='showTab(2)'>Blast Radius</div>");
            sb.AppendLine("<div class='tab' onclick='showTab(3)'>Coverage</div></div>");

            // Tab 0: Experiments
            sb.AppendLine("<div class='panel active' id='p0'><div class='card'><table>");
            sb.AppendLine("<tr><th>Name</th><th>Injection</th><th>Score</th><th>Tier</th><th>Verdict</th><th>Targets</th></tr>");
            foreach (var exp in _experiments.Values.OrderByDescending(e => e.CreatedAt))
            {
                var r = exp.Report;
                string score = r != null ? $"{r.CompositeScore:F1}" : "—";
                string tierStr = r != null ? $"<span class='{r.Tier.ToString().ToLower()}'>{r.Tier}</span>" : "—";
                string verdict = exp.Verdict != null ? $"<span class='{exp.Verdict.ToString()!.ToLower()}'>{exp.Verdict}</span>" : exp.Phase.ToString();
                sb.AppendLine($"<tr><td>{Esc(exp.Name)}</td><td>{exp.InjectionType}</td><td>{score}</td><td>{tierStr}</td><td>{verdict}</td><td>{exp.TargetPromptIds.Count}</td></tr>");
            }
            sb.AppendLine("</table></div></div>");

            // Tab 1: Insights
            sb.AppendLine("<div class='panel' id='p1'><div class='card'>");
            if (insights.Count == 0)
            {
                sb.AppendLine("<p>No insights yet — complete more experiments.</p>");
            }
            else
            {
                foreach (var ins in insights)
                {
                    string pColor = ins.Priority <= 2 ? "#f85149" : ins.Priority <= 3 ? "#d29922" : "#8b949e";
                    sb.AppendLine($"<div style='margin:8px 0;padding:8px;border-left:3px solid {pColor}'>");
                    sb.AppendLine($"<strong>[P{ins.Priority}] {ins.Type}</strong><br/>{Esc(ins.Description)}");
                    if (ins.EffortHours > 0) sb.AppendLine($"<br/><small>Estimated effort: {ins.EffortHours}h</small>");
                    sb.AppendLine("</div>");
                }
            }
            sb.AppendLine("</div></div>");

            // Tab 2: Blast Radius
            sb.AppendLine("<div class='panel' id='p2'><div class='card'>");
            var withRadius = completed.Where(e => e.Report?.BlastRadius != null).ToList();
            if (withRadius.Count == 0)
            {
                sb.AppendLine("<p>No blast radius data yet.</p>");
            }
            else
            {
                sb.AppendLine("<table><tr><th>Experiment</th><th>Level</th><th>Direct</th><th>Affected</th><th>Max Depth</th></tr>");
                foreach (var exp in withRadius)
                {
                    var br = exp.Report!.BlastRadius!;
                    sb.AppendLine($"<tr><td>{Esc(exp.Name)}</td><td>{br.Level}</td><td>{br.DirectTargets.Count}</td><td>{br.AffectedPromptIds.Count}</td><td>{br.MaxDepth}</td></tr>");
                }
                sb.AppendLine("</table>");
            }
            sb.AppendLine("</div></div>");

            // Tab 3: Coverage
            sb.AppendLine("<div class='panel' id='p3'><div class='card'><h3>Injection Type Coverage</h3>");
            var testedTypes = completed.Select(e => e.InjectionType).Distinct().ToHashSet();
            foreach (ChaosInjectionType t in Enum.GetValues(typeof(ChaosInjectionType)))
            {
                string status = testedTypes.Contains(t) ? "✅" : "❌";
                int count = completed.Count(e => e.InjectionType == t);
                sb.AppendLine($"<div>{status} {t} — {count} experiment(s)</div>");
            }
            sb.AppendLine("</div></div>");

            // JS
            sb.AppendLine("<script>");
            sb.AppendLine("function showTab(i){document.querySelectorAll('.tab').forEach((t,j)=>{t.classList.toggle('active',j===i)});");
            sb.AppendLine("document.querySelectorAll('.panel').forEach((p,j)=>{p.classList.toggle('active',j===i)})}");
            sb.AppendLine("</script></body></html>");

            return sb.ToString();
        }

        // ── Private Helpers ─────────────────────────────

        private ChaosExperiment GetOrThrow(string experimentId)
        {
            if (string.IsNullOrWhiteSpace(experimentId))
                throw new ArgumentException("Experiment ID is required.", nameof(experimentId));
            if (!_experiments.TryGetValue(experimentId, out var exp))
                throw new KeyNotFoundException($"Experiment '{experimentId}' not found.");
            return exp;
        }

        private void EnforceLru()
        {
            while (_experimentOrder.Count >= _config.MaxExperiments && _experimentOrder.Count > 0)
            {
                var oldest = _experimentOrder[0];
                _experimentOrder.RemoveAt(0);
                _experiments.Remove(oldest);
            }
        }

        private HashSet<string> AllKnownPromptIds()
        {
            var all = new HashSet<string>();
            foreach (var kv in _dependencies)
            {
                all.Add(kv.Key);
                foreach (var d in kv.Value) all.Add(d);
            }
            foreach (var kv in _dependents)
            {
                all.Add(kv.Key);
                foreach (var d in kv.Value) all.Add(d);
            }
            return all;
        }

        private static Dictionary<string, string> BuildInjectionParams(ChaosInjectionType type)
        {
            return type switch
            {
                ChaosInjectionType.TokenCorruption => new() { ["corruption_rate"] = "0.1" },
                ChaosInjectionType.LatencySpike => new() { ["spike_ms"] = "5000" },
                ChaosInjectionType.PartialResponse => new() { ["truncation_pct"] = "0.5" },
                ChaosInjectionType.ModelSwitch => new() { ["target_model"] = "gpt-3.5-turbo" },
                ChaosInjectionType.TemperatureShift => new() { ["temperature"] = "2.0" },
                ChaosInjectionType.ContextTruncation => new() { ["keep_pct"] = "0.3" },
                ChaosInjectionType.EncodingCorruption => new() { ["corruption_type"] = "mojibake" },
                ChaosInjectionType.RateLimitSimulation => new() { ["retry_after_sec"] = "30" },
                ChaosInjectionType.ConcurrentOverload => new() { ["concurrent_count"] = "100" },
                ChaosInjectionType.DependencyFailure => new() { ["failure_mode"] = "timeout" },
                _ => new()
            };
        }

        private static SteadyStateMetrics AggregateObservations(List<SteadyStateMetrics> obs)
        {
            if (obs.Count == 0) return new SteadyStateMetrics();
            return new SteadyStateMetrics
            {
                AvgLatencyMs = obs.Average(o => o.AvgLatencyMs),
                SuccessRate = obs.Average(o => o.SuccessRate),
                AvgQuality = obs.Average(o => o.AvgQuality),
                AvgTokens = obs.Average(o => o.AvgTokens),
                ErrorRate = obs.Average(o => o.ErrorRate),
                SampleCount = obs.Sum(o => o.SampleCount)
            };
        }

        private static double ScoreRecoveryTime(ChaosExperiment exp)
        {
            if (exp.PostRecovery == null || exp.Baseline == null) return 50;
            // Compare post-recovery to baseline — closer = better
            double latDiff = exp.Baseline.AvgLatencyMs == 0 ? 0 :
                Math.Abs(exp.PostRecovery.AvgLatencyMs - exp.Baseline.AvgLatencyMs) / Math.Max(exp.Baseline.AvgLatencyMs, 1);
            double successDiff = Math.Abs(exp.PostRecovery.SuccessRate - exp.Baseline.SuccessRate);
            double combined = (latDiff + successDiff) / 2;
            return Math.Max(0, Math.Min(100, 100 - combined * 200));
        }

        private static double ScoreDegradation(SteadyStateMetrics baseline, SteadyStateMetrics obs)
        {
            // How much did quality degrade?
            double qualityDrop = baseline.AvgQuality == 0 ? 0 :
                Math.Max(0, baseline.AvgQuality - obs.AvgQuality) / baseline.AvgQuality;
            double successDrop = Math.Max(0, baseline.SuccessRate - obs.SuccessRate);
            double combined = (qualityDrop + successDrop) / 2;
            return Math.Max(0, Math.Min(100, 100 - combined * 200));
        }

        private static double ScoreErrorHandling(SteadyStateMetrics baseline, SteadyStateMetrics obs)
        {
            double errorIncrease = Math.Max(0, obs.ErrorRate - baseline.ErrorRate);
            return Math.Max(0, Math.Min(100, 100 - errorIncrease * 250));
        }

        private static double ScoreFallback(SteadyStateMetrics baseline, SteadyStateMetrics obs)
        {
            // If quality is maintained despite higher error rate, fallback is working
            double qualityRetention = baseline.AvgQuality == 0 ? 1 :
                Math.Min(1, obs.AvgQuality / baseline.AvgQuality);
            double successRetention = baseline.SuccessRate == 0 ? 1 :
                Math.Min(1, obs.SuccessRate / baseline.SuccessRate);
            return Math.Min(100, (qualityRetention * 60 + successRetention * 40));
        }

        private static double ScoreStateConsistency(SteadyStateMetrics baseline, SteadyStateMetrics obs)
        {
            double tokenDrift = baseline.AvgTokens == 0 ? 0 :
                Math.Abs(obs.AvgTokens - baseline.AvgTokens) / Math.Max(baseline.AvgTokens, 1);
            return Math.Max(0, Math.Min(100, 100 - tokenDrift * 150));
        }

        private static double ScoreUserImpact(SteadyStateMetrics baseline, SteadyStateMetrics obs)
        {
            double latencyIncrease = baseline.AvgLatencyMs == 0 ? 0 :
                Math.Max(0, obs.AvgLatencyMs - baseline.AvgLatencyMs) / Math.Max(baseline.AvgLatencyMs, 1);
            double qualityDrop = baseline.AvgQuality == 0 ? 0 :
                Math.Max(0, baseline.AvgQuality - obs.AvgQuality) / baseline.AvgQuality;
            double combined = (latencyIncrease + qualityDrop) / 2;
            return Math.Max(0, Math.Min(100, 100 - combined * 200));
        }

        private static ResilienceTier ClassifyTier(double score) => score switch
        {
            >= 85 => ResilienceTier.Antifragile,
            >= 70 => ResilienceTier.Resilient,
            >= 50 => ResilienceTier.Tolerant,
            >= 30 => ResilienceTier.Fragile,
            _ => ResilienceTier.Brittle
        };

        private static ExperimentVerdict ClassifyVerdict(double score, ChaosExperiment exp)
        {
            if (score >= 70) return ExperimentVerdict.Passed;
            if (score >= 50) return ExperimentVerdict.Degraded;
            if (score >= 25) return ExperimentVerdict.Failed;
            return ExperimentVerdict.Catastrophic;
        }

        private static List<string> GenerateRecommendations(ChaosExperiment exp, double score, BlastRadiusMap blast)
        {
            var recs = new List<string>();
            if (score < 50) recs.Add($"Critical: resilience score {score:F1} is below acceptable threshold. Implement fallback chains for {exp.InjectionType}.");
            if (blast.Level >= BlastRadiusLevel.Spreading) recs.Add($"Blast radius is {blast.Level} ({blast.TotalAffected} prompts). Add circuit breakers to contain failure propagation.");
            if (blast.MaxDepth >= 3) recs.Add($"Dependency chain depth {blast.MaxDepth} detected. Consider flattening prompt dependencies.");
            if (exp.Observations.Any(o => o.SuccessRate < 0.5)) recs.Add("Success rate dropped below 50% during injection. Implement retry policies with exponential backoff.");
            if (exp.Observations.Any(o => o.ErrorRate > 0.5)) recs.Add("Error rate exceeded 50%. Add graceful degradation paths.");
            if (recs.Count == 0) recs.Add("System showed good resilience. Consider testing with higher fault intensity.");
            return recs;
        }

        private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}