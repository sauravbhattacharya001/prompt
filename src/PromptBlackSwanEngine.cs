namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptBlackSwanEngine – Autonomous Rare Catastrophic Failure Detection
    //
    //  Inspired by Nassim Taleb's Black Swan theory: extremely rare,
    //  high-impact events that are difficult to predict but devastating
    //  when they occur.  This engine detects, catalogs, and helps
    //  prevent rare catastrophic failures in prompt operations — the
    //  kind that happen once in 1000 runs but cause 90% of the damage.
    //
    //  7 engines: ExtremeEventDetector, FatTailAnalyzer,
    //  FragilitySurfaceMapper, CascadeChainDetector,
    //  AntecedentPatternMiner, ImpactAmplificationAnalyzer,
    //  InsightGenerator.
    //
    //  Unlike PromptAntifragileEngine (graduated stress testing) or
    //  PromptCircuitBreakerEngine (real-time failure isolation), this
    //  engine focuses on the statistical long tail — finding hidden
    //  correlations and cascade chains that produce catastrophic but
    //  rare outcomes.
    // ────────────────────────────────────────────

    /// <summary>Severity classification for black swan events.</summary>
    public enum BlackSwanSeverity
    {
        /// <summary>Statistical anomaly — unusual but manageable.</summary>
        Anomaly,
        /// <summary>Significant unexpected event.</summary>
        Shock,
        /// <summary>Serious failure requiring immediate attention.</summary>
        Crisis,
        /// <summary>Major catastrophic failure.</summary>
        Catastrophe,
        /// <summary>True black swan — extreme, unprecedented, transformative.</summary>
        BlackSwan
    }

    /// <summary>Category of prompt failure.</summary>
    public enum FailureCategory
    {
        /// <summary>Request timed out.</summary>
        Timeout,
        /// <summary>Model produced hallucinated/fabricated content.</summary>
        Hallucination,
        /// <summary>Prompt injection attack detected.</summary>
        Injection,
        /// <summary>Token limit exceeded.</summary>
        TokenOverflow,
        /// <summary>Model refused to respond.</summary>
        ModelRefusal,
        /// <summary>Output was garbled or unparseable.</summary>
        GarbageOutput,
        /// <summary>Model got stuck in repetitive loops.</summary>
        InfiniteLoop,
        /// <summary>Output appeared correct but was factually wrong.</summary>
        SilentWrong,
        /// <summary>Failure triggered by another prompt's failure.</summary>
        CascadeFailure,
        /// <summary>Unclassified failure.</summary>
        Unknown
    }

    /// <summary>Statistical tail classification.</summary>
    public enum TailType
    {
        /// <summary>Excess kurtosis &lt; 0 — lighter tails than normal.</summary>
        ThinTailed,
        /// <summary>Excess kurtosis 0–3 — near-normal tails.</summary>
        ModerateTail,
        /// <summary>Excess kurtosis 3–6 — heavy tails, significant outlier risk.</summary>
        FatTailed,
        /// <summary>Excess kurtosis &gt; 6 — extreme tail risk.</summary>
        SuperFatTailed
    }

    /// <summary>Health tier for black swan exposure.</summary>
    public enum BlackSwanHealthTier
    {
        /// <summary>Score 85–100: strong defenses, good early warning.</summary>
        Fortified,
        /// <summary>Score 70–84: reasonable preparedness.</summary>
        Prepared,
        /// <summary>Score 50–69: aware of risks but gaps exist.</summary>
        Aware,
        /// <summary>Score 30–49: significant exposure.</summary>
        Exposed,
        /// <summary>Score 0–29: blind to tail risk.</summary>
        Blind
    }

    /// <summary>A recorded black swan event.</summary>
    public class BlackSwanEvent
    {
        /// <summary>Unique event identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];

        /// <summary>Prompt that triggered the event.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Failure category.</summary>
        public FailureCategory Category { get; set; }

        /// <summary>Severity classification.</summary>
        public BlackSwanSeverity Severity { get; set; }

        /// <summary>Impact score 0–100.</summary>
        public double Impact { get; set; }

        /// <summary>When the event occurred.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Human-readable description.</summary>
        public string Description { get; set; } = "";

        /// <summary>Arbitrary metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>IDs of precursor events (lower severity events that preceded this).</summary>
        public List<string> Precursors { get; set; } = new();

        /// <summary>Depth in a cascade chain (0 = origin).</summary>
        public int CascadeDepth { get; set; }
    }

    /// <summary>Tail risk profile for a failure category.</summary>
    public class TailRiskProfile
    {
        /// <summary>Failure category analyzed.</summary>
        public FailureCategory Category { get; set; }

        /// <summary>Number of samples in analysis.</summary>
        public int SampleCount { get; set; }

        /// <summary>Excess kurtosis of impact distribution.</summary>
        public double Kurtosis { get; set; }

        /// <summary>Statistical tail classification.</summary>
        public TailType TailType { get; set; }

        /// <summary>95th percentile Value at Risk.</summary>
        public double ValueAtRisk95 { get; set; }

        /// <summary>99th percentile Value at Risk.</summary>
        public double ValueAtRisk99 { get; set; }

        /// <summary>Expected shortfall (CVaR) — mean of impacts beyond VaR95.</summary>
        public double ExpectedShortfall { get; set; }

        /// <summary>Maximum observed impact.</summary>
        public double MaxObservedImpact { get; set; }
    }

    /// <summary>Correlated failure surface between two dimensions.</summary>
    public class FragilitySurface
    {
        /// <summary>First failure category.</summary>
        public FailureCategory DimensionA { get; set; }

        /// <summary>Second failure category.</summary>
        public FailureCategory DimensionB { get; set; }

        /// <summary>Pearson correlation strength between dimensions.</summary>
        public double CorrelationStrength { get; set; }

        /// <summary>Observed joint failure rate.</summary>
        public double JointFailureRate { get; set; }

        /// <summary>Expected independent failure rate (P(A) × P(B)).</summary>
        public double IndependentFailureRate { get; set; }

        /// <summary>Joint / Independent ratio — values > 1 indicate hidden correlation.</summary>
        public double AmplificationFactor { get; set; }
    }

    /// <summary>A detected cascade chain of failures.</summary>
    public class CascadeChain
    {
        /// <summary>Unique chain identifier.</summary>
        public string ChainId { get; set; } = Guid.NewGuid().ToString("N")[..10];

        /// <summary>Prompt that started the cascade.</summary>
        public string PatientZeroPromptId { get; set; } = "";

        /// <summary>All affected prompt IDs in order.</summary>
        public List<string> AffectedPromptIds { get; set; } = new();

        /// <summary>Total cumulative impact of the chain.</summary>
        public double TotalImpact { get; set; }

        /// <summary>Number of events in the chain.</summary>
        public int ChainLength { get; set; }

        /// <summary>Duration from first to last event in seconds.</summary>
        public double DurationSeconds { get; set; }

        /// <summary>Category that triggered the cascade.</summary>
        public FailureCategory TriggerCategory { get; set; }
    }

    /// <summary>A precursor signal that precedes black swan events.</summary>
    public class AntecedentSignal
    {
        /// <summary>Unique signal identifier.</summary>
        public string SignalId { get; set; } = Guid.NewGuid().ToString("N")[..10];

        /// <summary>Human-readable description of the precursor pattern.</summary>
        public string Description { get; set; } = "";

        /// <summary>How many seconds before the major event the signal appears.</summary>
        public double LeadTimeSeconds { get; set; }

        /// <summary>Confidence that this signal predicts a major event (0–1).</summary>
        public double Confidence { get; set; }

        /// <summary>Category of major event this signal precedes.</summary>
        public FailureCategory AssociatedCategory { get; set; }

        /// <summary>Number of times this pattern has been observed.</summary>
        public int Occurrences { get; set; }
    }

    /// <summary>Impact amplification analysis for an event.</summary>
    public class ImpactAmplification
    {
        /// <summary>Event being analyzed.</summary>
        public string EventId { get; set; } = "";

        /// <summary>Direct impact of the event itself.</summary>
        public double DirectImpact { get; set; }

        /// <summary>Indirect impact on other prompts/systems.</summary>
        public double IndirectImpact { get; set; }

        /// <summary>Total / Direct ratio.</summary>
        public double AmplificationRatio { get; set; }

        /// <summary>Number of other prompts affected.</summary>
        public int BlastRadius { get; set; }

        /// <summary>How difficult recovery is (0–100).</summary>
        public double RecoveryDifficultyScore { get; set; }
    }

    /// <summary>Configuration for the Black Swan Engine.</summary>
    public class BlackSwanConfig
    {
        /// <summary>Z-score threshold for extreme event detection (default 3.0).</summary>
        public double ZScoreThreshold { get; set; } = 3.0;

        /// <summary>Modified z-score threshold using MAD (default 3.5).</summary>
        public double MadThreshold { get; set; } = 3.5;

        /// <summary>Minimum samples required for tail risk analysis.</summary>
        public int TailAnalysisMinSamples { get; set; } = 20;

        /// <summary>Time window in seconds for cascade detection.</summary>
        public double CascadeWindowSeconds { get; set; } = 60;

        /// <summary>Time window in seconds for antecedent mining.</summary>
        public double AntecedentWindowSeconds { get; set; } = 300;

        /// <summary>Whether to generate autonomous insights.</summary>
        public bool EnableAutonomousInsights { get; set; } = true;

        /// <summary>Impact thresholds for severity classification.</summary>
        public Dictionary<BlackSwanSeverity, double> SeverityThresholds { get; set; } = new()
        {
            [BlackSwanSeverity.Anomaly] = 40,
            [BlackSwanSeverity.Shock] = 55,
            [BlackSwanSeverity.Crisis] = 70,
            [BlackSwanSeverity.Catastrophe] = 85,
            [BlackSwanSeverity.BlackSwan] = 95
        };
    }

    /// <summary>Snapshot of a prompt's black swan exposure.</summary>
    public class BlackSwanSnapshot
    {
        /// <summary>Prompt analyzed.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>When the snapshot was taken.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Composite exposure score 0–100 (lower is better).</summary>
        public double ExposureScore { get; set; }

        /// <summary>Health tier.</summary>
        public BlackSwanHealthTier Tier { get; set; }

        /// <summary>Total event count.</summary>
        public int EventCount { get; set; }

        /// <summary>Worst severity observed.</summary>
        public BlackSwanSeverity? WorstSeverity { get; set; }

        /// <summary>Most common failure category.</summary>
        public FailureCategory? DominantCategory { get; set; }

        /// <summary>Summary of tail risk findings.</summary>
        public string TailRiskSummary { get; set; } = "";
    }

    /// <summary>Fleet-wide black swan report.</summary>
    public class BlackSwanFleetReport
    {
        /// <summary>Composite fleet score 0–100 (higher is better = less exposed).</summary>
        public double FleetScore { get; set; }

        /// <summary>Fleet health tier.</summary>
        public BlackSwanHealthTier Tier { get; set; }

        /// <summary>Per-prompt snapshots.</summary>
        public List<BlackSwanSnapshot> Snapshots { get; set; } = new();

        /// <summary>Autonomous insights.</summary>
        public List<string> Insights { get; set; } = new();

        /// <summary>Tail risk profiles across all categories.</summary>
        public List<TailRiskProfile> TailProfiles { get; set; } = new();

        /// <summary>Detected cascade chains.</summary>
        public List<CascadeChain> Cascades { get; set; } = new();
    }

    /// <summary>
    /// Autonomous rare catastrophic failure detection and prevention engine.
    /// Detects extreme outliers, analyzes fat tails, maps fragility surfaces,
    /// finds cascade chains, mines antecedent patterns, and measures impact
    /// amplification across prompt operations.
    /// </summary>
    public class PromptBlackSwanEngine
    {
        private readonly BlackSwanConfig _config;
        private readonly List<BlackSwanEvent> _events = new();

        /// <summary>Create a new engine with optional configuration.</summary>
        public PromptBlackSwanEngine(BlackSwanConfig? config = null)
        {
            _config = config ?? new BlackSwanConfig();
        }

        // ── Event Recording ─────────────────────────

        /// <summary>Record a single black swan event.</summary>
        public void RecordEvent(BlackSwanEvent evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            _events.Add(evt);
        }

        /// <summary>Record multiple events.</summary>
        public void RecordEvents(IEnumerable<BlackSwanEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            _events.AddRange(events);
        }

        /// <summary>Retrieve recorded events, optionally filtered by prompt.</summary>
        public List<BlackSwanEvent> GetEvents(string? promptId = null)
        {
            if (promptId == null) return new List<BlackSwanEvent>(_events);
            return _events.Where(e => e.PromptId == promptId).ToList();
        }

        /// <summary>Total recorded events.</summary>
        public int EventCount => _events.Count;

        // ── Engine 1: Extreme Event Detector ────────

        /// <summary>
        /// Detect extreme events for a prompt using z-score and MAD analysis.
        /// Returns events that exceed the configured statistical thresholds.
        /// </summary>
        public List<BlackSwanEvent> DetectExtremeEvents(string promptId)
        {
            var promptEvents = _events.Where(e => e.PromptId == promptId).ToList();
            if (promptEvents.Count < 3) return new List<BlackSwanEvent>();

            var impacts = promptEvents.Select(e => e.Impact).ToList();
            double mean = impacts.Average();
            double stdDev = ComputeStdDev(impacts, mean);
            double median = ComputeMedian(impacts);
            double mad = ComputeMAD(impacts, median);

            var extreme = new List<BlackSwanEvent>();
            foreach (var evt in promptEvents)
            {
                bool isExtreme = false;

                // Z-score method
                if (stdDev > 1e-9)
                {
                    double zScore = (evt.Impact - mean) / stdDev;
                    if (zScore > _config.ZScoreThreshold) isExtreme = true;
                }

                // Modified z-score using MAD
                if (mad > 1e-9)
                {
                    double modifiedZ = 0.6745 * (evt.Impact - median) / mad;
                    if (modifiedZ > _config.MadThreshold) isExtreme = true;
                }

                // Also flag by severity threshold
                if (evt.Impact >= _config.SeverityThresholds[BlackSwanSeverity.Crisis])
                    isExtreme = true;

                if (isExtreme)
                    extreme.Add(evt);
            }

            return extreme;
        }

        /// <summary>Classify severity based on impact score.</summary>
        public BlackSwanSeverity ClassifySeverity(double impact)
        {
            if (impact >= _config.SeverityThresholds[BlackSwanSeverity.BlackSwan])
                return BlackSwanSeverity.BlackSwan;
            if (impact >= _config.SeverityThresholds[BlackSwanSeverity.Catastrophe])
                return BlackSwanSeverity.Catastrophe;
            if (impact >= _config.SeverityThresholds[BlackSwanSeverity.Crisis])
                return BlackSwanSeverity.Crisis;
            if (impact >= _config.SeverityThresholds[BlackSwanSeverity.Shock])
                return BlackSwanSeverity.Shock;
            if (impact >= _config.SeverityThresholds[BlackSwanSeverity.Anomaly])
                return BlackSwanSeverity.Anomaly;
            return BlackSwanSeverity.Anomaly;
        }

        // ── Engine 2: Fat Tail Analyzer ─────────────

        /// <summary>
        /// Analyze tail risk across failure categories.
        /// Computes kurtosis, VaR, CVaR (Expected Shortfall), and tail type.
        /// </summary>
        public List<TailRiskProfile> AnalyzeTailRisk(string? promptId = null)
        {
            var events = promptId != null
                ? _events.Where(e => e.PromptId == promptId).ToList()
                : _events;

            var profiles = new List<TailRiskProfile>();
            var byCategory = events.GroupBy(e => e.Category);

            foreach (var group in byCategory)
            {
                var impacts = group.Select(e => e.Impact).OrderBy(x => x).ToList();
                if (impacts.Count < _config.TailAnalysisMinSamples) continue;

                double mean = impacts.Average();
                double kurtosis = ComputeExcessKurtosis(impacts, mean);
                double var95 = ComputePercentile(impacts, 0.95);
                double var99 = ComputePercentile(impacts, 0.99);
                double es = ComputeExpectedShortfall(impacts, var95);

                profiles.Add(new TailRiskProfile
                {
                    Category = group.Key,
                    SampleCount = impacts.Count,
                    Kurtosis = Math.Round(kurtosis, 3),
                    TailType = ClassifyTail(kurtosis),
                    ValueAtRisk95 = Math.Round(var95, 2),
                    ValueAtRisk99 = Math.Round(var99, 2),
                    ExpectedShortfall = Math.Round(es, 2),
                    MaxObservedImpact = impacts.Last()
                });
            }

            return profiles.OrderByDescending(p => p.Kurtosis).ToList();
        }

        private static TailType ClassifyTail(double kurtosis) => kurtosis switch
        {
            < 0 => TailType.ThinTailed,
            < 3 => TailType.ModerateTail,
            < 6 => TailType.FatTailed,
            _ => TailType.SuperFatTailed
        };

        // ── Engine 3: Fragility Surface Mapper ──────

        /// <summary>
        /// Map fragility surfaces — hidden correlations between failure categories.
        /// Identifies dimension pairs that co-occur more often than independence predicts.
        /// </summary>
        public List<FragilitySurface> MapFragilitySurface()
        {
            if (_events.Count < 5) return new List<FragilitySurface>();

            // Group events into time windows and track category co-occurrence
            var windows = GroupIntoWindows(_events, _config.CascadeWindowSeconds);
            var categories = _events.Select(e => e.Category).Distinct().ToList();
            int totalWindows = Math.Max(windows.Count, 1);

            // Count per-category window presence
            var categoryWindowCount = new Dictionary<FailureCategory, int>();
            var pairWindowCount = new Dictionary<(FailureCategory, FailureCategory), int>();

            foreach (var cat in categories)
                categoryWindowCount[cat] = 0;

            foreach (var window in windows)
            {
                var catsInWindow = window.Select(e => e.Category).Distinct().ToList();
                foreach (var cat in catsInWindow)
                    categoryWindowCount[cat]++;

                for (int i = 0; i < catsInWindow.Count; i++)
                {
                    for (int j = i + 1; j < catsInWindow.Count; j++)
                    {
                        var key = OrderPair(catsInWindow[i], catsInWindow[j]);
                        pairWindowCount.TryGetValue(key, out int count);
                        pairWindowCount[key] = count + 1;
                    }
                }
            }

            var surfaces = new List<FragilitySurface>();
            foreach (var (pair, jointCount) in pairWindowCount)
            {
                double pA = (double)categoryWindowCount[pair.Item1] / totalWindows;
                double pB = (double)categoryWindowCount[pair.Item2] / totalWindows;
                double pJoint = (double)jointCount / totalWindows;
                double pIndependent = pA * pB;
                double amplification = pIndependent > 1e-9 ? pJoint / pIndependent : 0;

                // Pearson correlation approximation for binary occurrence
                double correlation = ComputePhiCorrelation(
                    totalWindows, jointCount,
                    categoryWindowCount[pair.Item1],
                    categoryWindowCount[pair.Item2]);

                surfaces.Add(new FragilitySurface
                {
                    DimensionA = pair.Item1,
                    DimensionB = pair.Item2,
                    CorrelationStrength = Math.Round(correlation, 3),
                    JointFailureRate = Math.Round(pJoint, 4),
                    IndependentFailureRate = Math.Round(pIndependent, 4),
                    AmplificationFactor = Math.Round(amplification, 2)
                });
            }

            return surfaces.OrderByDescending(s => s.AmplificationFactor).ToList();
        }

        // ── Engine 4: Cascade Chain Detector ────────

        /// <summary>
        /// Detect failure cascades — sequences of events that cluster in time,
        /// suggesting one failure triggered subsequent ones.
        /// </summary>
        public List<CascadeChain> DetectCascades()
        {
            if (_events.Count < 2) return new List<CascadeChain>();

            var sorted = _events.OrderBy(e => e.Timestamp).ToList();
            var chains = new List<CascadeChain>();
            var visited = new HashSet<string>();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (visited.Contains(sorted[i].Id)) continue;

                // Build chain: find all events within cascade window of each other
                var chain = new List<BlackSwanEvent> { sorted[i] };
                visited.Add(sorted[i].Id);

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (visited.Contains(sorted[j].Id)) continue;

                    var lastInChain = chain.Last();
                    double gap = (sorted[j].Timestamp - lastInChain.Timestamp).TotalSeconds;
                    if (gap <= _config.CascadeWindowSeconds)
                    {
                        chain.Add(sorted[j]);
                        visited.Add(sorted[j].Id);
                    }
                    else if (gap > _config.CascadeWindowSeconds * 2)
                    {
                        break; // No point looking further
                    }
                }

                if (chain.Count >= 2)
                {
                    var patientZero = chain.First();
                    chains.Add(new CascadeChain
                    {
                        PatientZeroPromptId = patientZero.PromptId,
                        AffectedPromptIds = chain.Select(e => e.PromptId).Distinct().ToList(),
                        TotalImpact = Math.Round(chain.Sum(e => e.Impact), 2),
                        ChainLength = chain.Count,
                        DurationSeconds = Math.Round(
                            (chain.Last().Timestamp - chain.First().Timestamp).TotalSeconds, 1),
                        TriggerCategory = patientZero.Category
                    });
                }
            }

            return chains.OrderByDescending(c => c.TotalImpact).ToList();
        }

        // ── Engine 5: Antecedent Pattern Miner ──────

        /// <summary>
        /// Mine for precursor patterns — low-severity events that reliably
        /// precede high-severity events within the antecedent window.
        /// </summary>
        public List<AntecedentSignal> MineAntecedents()
        {
            if (_events.Count < 3) return new List<AntecedentSignal>();

            var sorted = _events.OrderBy(e => e.Timestamp).ToList();
            double crisisThreshold = _config.SeverityThresholds[BlackSwanSeverity.Crisis];

            // Find high-severity events
            var majorEvents = sorted.Where(e => e.Impact >= crisisThreshold).ToList();
            if (majorEvents.Count == 0) return new List<AntecedentSignal>();

            // For each major event, look back for precursors
            var precursorPatterns = new Dictionary<(FailureCategory minor, FailureCategory major), List<double>>();

            foreach (var major in majorEvents)
            {
                var precursors = sorted
                    .Where(e => e.Id != major.Id
                        && e.Impact < crisisThreshold
                        && e.Timestamp < major.Timestamp
                        && (major.Timestamp - e.Timestamp).TotalSeconds <= _config.AntecedentWindowSeconds)
                    .ToList();

                foreach (var pre in precursors)
                {
                    var key = (pre.Category, major.Category);
                    if (!precursorPatterns.ContainsKey(key))
                        precursorPatterns[key] = new List<double>();
                    precursorPatterns[key].Add((major.Timestamp - pre.Timestamp).TotalSeconds);
                }
            }

            var signals = new List<AntecedentSignal>();
            int totalMajor = majorEvents.Count;

            foreach (var (pattern, leadTimes) in precursorPatterns)
            {
                if (leadTimes.Count < 2) continue; // Need at least 2 occurrences

                double avgLeadTime = leadTimes.Average();
                double confidence = Math.Min((double)leadTimes.Count / totalMajor, 1.0);

                signals.Add(new AntecedentSignal
                {
                    Description = $"{pattern.minor} events precede {pattern.major} failures " +
                                  $"by ~{avgLeadTime:F0}s on average",
                    LeadTimeSeconds = Math.Round(avgLeadTime, 1),
                    Confidence = Math.Round(confidence, 3),
                    AssociatedCategory = pattern.major,
                    Occurrences = leadTimes.Count
                });
            }

            return signals.OrderByDescending(s => s.Confidence).ThenByDescending(s => s.Occurrences).ToList();
        }

        // ── Engine 6: Impact Amplification Analyzer ─

        /// <summary>
        /// Analyze impact amplification — how much indirect damage events cause
        /// beyond their direct impact.
        /// </summary>
        public List<ImpactAmplification> AnalyzeImpactAmplification(string? eventId = null)
        {
            var targetEvents = eventId != null
                ? _events.Where(e => e.Id == eventId).ToList()
                : _events.Where(e => e.Impact >= _config.SeverityThresholds[BlackSwanSeverity.Shock]).ToList();

            if (targetEvents.Count == 0) return new List<ImpactAmplification>();

            var sorted = _events.OrderBy(e => e.Timestamp).ToList();
            var results = new List<ImpactAmplification>();

            foreach (var evt in targetEvents)
            {
                // Find downstream events within cascade window
                var downstream = sorted
                    .Where(e => e.Id != evt.Id
                        && e.Timestamp >= evt.Timestamp
                        && (e.Timestamp - evt.Timestamp).TotalSeconds <= _config.CascadeWindowSeconds)
                    .ToList();

                double indirectImpact = downstream.Sum(e => e.Impact);
                double totalImpact = evt.Impact + indirectImpact;
                double amplification = evt.Impact > 0 ? totalImpact / evt.Impact : 1.0;
                int blastRadius = downstream.Select(e => e.PromptId).Distinct().Count();

                // Recovery difficulty: based on severity, cascade depth, and blast radius
                double recoveryDifficulty = Math.Min(100, evt.Impact * 0.5 + blastRadius * 10 + evt.CascadeDepth * 5);

                results.Add(new ImpactAmplification
                {
                    EventId = evt.Id,
                    DirectImpact = Math.Round(evt.Impact, 2),
                    IndirectImpact = Math.Round(indirectImpact, 2),
                    AmplificationRatio = Math.Round(amplification, 2),
                    BlastRadius = blastRadius,
                    RecoveryDifficultyScore = Math.Round(recoveryDifficulty, 1)
                });
            }

            return results.OrderByDescending(r => r.AmplificationRatio).ToList();
        }

        // ── Snapshot & Fleet Report ─────────────────

        /// <summary>Generate a black swan exposure snapshot for a prompt.</summary>
        public BlackSwanSnapshot GenerateSnapshot(string promptId)
        {
            var promptEvents = _events.Where(e => e.PromptId == promptId).ToList();
            return BuildSnapshot(promptId, promptEvents, cascades: null);
        }

        /// <summary>
        /// Internal snapshot builder that accepts pre-filtered events and
        /// optional pre-computed cascades to avoid redundant O(N log N)
        /// DetectCascades() calls when invoked from GenerateFleetReport.
        /// </summary>
        private BlackSwanSnapshot BuildSnapshot(
            string promptId,
            List<BlackSwanEvent> promptEvents,
            List<CascadeChain>? cascades)
        {
            var extremeEvents = DetectExtremeEvents(promptId);

            // Re-use fleet cascades when available; otherwise compute on demand
            cascades ??= DetectCascades();

            double exposureScore = ComputeExposureScore(promptId, promptEvents, extremeEvents, cascades);
            var tier = ScoreToTier(100 - exposureScore); // Invert: high exposure = low health

            var worstSeverity = promptEvents.Count > 0
                ? promptEvents.Max(e => e.Severity)
                : (BlackSwanSeverity?)null;

            var dominantCategory = promptEvents.Count > 0
                ? promptEvents.GroupBy(e => e.Category)
                    .OrderByDescending(g => g.Count())
                    .First().Key
                : (FailureCategory?)null;

            var tailProfiles = AnalyzeTailRisk(promptId);
            string tailSummary = tailProfiles.Count > 0
                ? $"{tailProfiles.Count} categories analyzed; worst tail: {tailProfiles.First().TailType}"
                : "Insufficient data for tail analysis";

            return new BlackSwanSnapshot
            {
                PromptId = promptId,
                ExposureScore = Math.Round(exposureScore, 1),
                Tier = tier,
                EventCount = promptEvents.Count,
                WorstSeverity = worstSeverity,
                DominantCategory = dominantCategory,
                TailRiskSummary = tailSummary
            };
        }

        /// <summary>Generate a fleet-wide black swan report.</summary>
        public BlackSwanFleetReport GenerateFleetReport()
        {
            // Pre-compute cascades once (O(N log N)) and pre-group events
            // by promptId to avoid redundant O(N) filtering per prompt.
            // Previously, each GenerateSnapshot→ComputeExposureScore called
            // DetectCascades() independently, costing O(P × N log N) total.
            var cascades = DetectCascades();
            var tailProfiles = AnalyzeTailRisk();

            var eventsByPrompt = _events
                .GroupBy(e => e.PromptId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var snapshots = eventsByPrompt
                .Select(kvp => BuildSnapshot(kvp.Key, kvp.Value, cascades))
                .ToList();

            var insights = GenerateInsights(snapshots, tailProfiles, cascades);

            double fleetScore = snapshots.Count > 0
                ? 100 - snapshots.Average(s => s.ExposureScore)
                : 100;
            fleetScore = Math.Round(Math.Clamp(fleetScore, 0, 100), 1);

            return new BlackSwanFleetReport
            {
                FleetScore = fleetScore,
                Tier = ScoreToTier(fleetScore),
                Snapshots = snapshots,
                Insights = insights,
                TailProfiles = tailProfiles,
                Cascades = cascades
            };
        }

        // ── Engine 7: Insight Generator ─────────────

        private List<string> GenerateInsights(
            List<BlackSwanSnapshot> snapshots,
            List<TailRiskProfile> tailProfiles,
            List<CascadeChain> cascades)
        {
            var insights = new List<string>();
            if (!_config.EnableAutonomousInsights) return insights;

            if (_events.Count == 0)
            {
                insights.Add("No events recorded yet — begin tracking prompt failures to build black swan awareness.");
                return insights;
            }

            // Severity distribution
            var bySeverity = _events.GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count());
            int blackSwanCount = bySeverity.GetValueOrDefault(BlackSwanSeverity.BlackSwan);
            int catastropheCount = bySeverity.GetValueOrDefault(BlackSwanSeverity.Catastrophe);

            if (blackSwanCount > 0)
                insights.Add($"🦢 {blackSwanCount} true Black Swan event(s) detected — review immediately for systemic risk.");
            if (catastropheCount > 0)
                insights.Add($"💥 {catastropheCount} Catastrophe-level event(s) — close to black swan territory.");

            // Fat tail warnings
            var fatTails = tailProfiles.Where(p => p.TailType >= TailType.FatTailed).ToList();
            if (fatTails.Count > 0)
            {
                var names = string.Join(", ", fatTails.Select(f => f.Category));
                insights.Add($"⚠️ Fat-tailed distributions in: {names} — standard risk models underestimate extreme outcomes.");
            }

            // Cascade warnings
            if (cascades.Count > 0)
            {
                var worstCascade = cascades.First();
                insights.Add($"🔗 {cascades.Count} cascade chain(s) detected — worst chain: {worstCascade.ChainLength} events, " +
                             $"impact {worstCascade.TotalImpact:F0}, triggered by {worstCascade.TriggerCategory}.");
            }

            // Fragility surfaces
            var surfaces = MapFragilitySurface();
            var dangerous = surfaces.Where(s => s.AmplificationFactor > 2.0).ToList();
            if (dangerous.Count > 0)
            {
                var worst = dangerous.First();
                insights.Add($"🕸️ Hidden correlation: {worst.DimensionA} + {worst.DimensionB} co-occur " +
                             $"{worst.AmplificationFactor:F1}× more than independence predicts.");
            }

            // Antecedent signals
            var signals = MineAntecedents();
            if (signals.Count > 0)
            {
                var best = signals.First();
                insights.Add($"🔮 Early warning found: {best.Description} (confidence {best.Confidence:P0}, " +
                             $"lead time ~{best.LeadTimeSeconds:F0}s).");
            }

            // Concentration risk
            if (snapshots.Count > 0)
            {
                var mostExposed = snapshots.MaxBy(s => s.ExposureScore)!;
                if (mostExposed.ExposureScore > 70)
                    insights.Add($"🎯 Prompt '{mostExposed.PromptId}' has exposure score {mostExposed.ExposureScore:F0}/100 — highest in fleet.");
            }

            // Impact amplification
            var amplifications = AnalyzeImpactAmplification();
            if (amplifications.Any(a => a.AmplificationRatio > 3.0))
            {
                var worst = amplifications.First();
                insights.Add($"📡 Impact amplification: event {worst.EventId} had {worst.AmplificationRatio:F1}× total impact " +
                             $"(blast radius: {worst.BlastRadius} prompts).");
            }

            if (insights.Count == 0)
                insights.Add("✅ No significant black swan signals detected — fleet appears resilient to tail risk.");

            return insights;
        }

        // ── HTML Dashboard ──────────────────────────

        /// <summary>Generate an interactive HTML dashboard for the fleet.</summary>
        public string GenerateHtmlDashboard()
        {
            var report = GenerateFleetReport();
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
            sb.AppendLine("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            sb.AppendLine("<title>Black Swan Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#e2e8f0;padding:20px}");
            sb.AppendLine(".header{text-align:center;padding:30px 0}");
            sb.AppendLine(".header h1{font-size:2em;color:#f8fafc}");
            sb.AppendLine(".score-ring{width:120px;height:120px;border-radius:50%;margin:20px auto;display:flex;align-items:center;justify-content:center;font-size:2em;font-weight:bold}");
            sb.AppendLine(".tier-fortified{border:4px solid #22c55e;color:#22c55e}");
            sb.AppendLine(".tier-prepared{border:4px solid #3b82f6;color:#3b82f6}");
            sb.AppendLine(".tier-aware{border:4px solid #eab308;color:#eab308}");
            sb.AppendLine(".tier-exposed{border:4px solid #f97316;color:#f97316}");
            sb.AppendLine(".tier-blind{border:4px solid #ef4444;color:#ef4444}");
            sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(350px,1fr));gap:20px;margin-top:20px}");
            sb.AppendLine(".card{background:#1e293b;border-radius:12px;padding:20px;border:1px solid #334155}");
            sb.AppendLine(".card h2{font-size:1.1em;color:#94a3b8;margin-bottom:12px}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;font-size:0.9em}");
            sb.AppendLine("th,td{padding:8px 10px;text-align:left;border-bottom:1px solid #334155}");
            sb.AppendLine("th{color:#64748b;font-weight:600}");
            sb.AppendLine(".tag{display:inline-block;padding:2px 8px;border-radius:4px;font-size:0.8em}");
            sb.AppendLine(".tag-red{background:#7f1d1d;color:#fca5a5}");
            sb.AppendLine(".tag-orange{background:#7c2d12;color:#fed7aa}");
            sb.AppendLine(".tag-yellow{background:#713f12;color:#fde68a}");
            sb.AppendLine(".tag-green{background:#14532d;color:#86efac}");
            sb.AppendLine(".tag-blue{background:#1e3a5f;color:#93c5fd}");
            sb.AppendLine(".insight{padding:10px 14px;margin:6px 0;background:#1a2332;border-left:3px solid #3b82f6;border-radius:0 6px 6px 0;font-size:0.9em}");
            sb.AppendLine("</style></head><body>");

            // Header
            string tierClass = report.Tier.ToString().ToLower();
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>🦢 Black Swan Dashboard</h1>");
            sb.AppendLine($"<div class='score-ring tier-{tierClass}'>{report.FleetScore:F0}</div>");
            sb.AppendLine($"<div style='color:#94a3b8'>{report.Tier} · {_events.Count} events · {report.Snapshots.Count} prompts</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='grid'>");

            // Insights
            sb.AppendLine("<div class='card'><h2>🔮 Autonomous Insights</h2>");
            foreach (var insight in report.Insights)
                sb.AppendLine($"<div class='insight'>{EscapeHtml(insight)}</div>");
            sb.AppendLine("</div>");

            // Tail Risk Profiles
            if (report.TailProfiles.Count > 0)
            {
                sb.AppendLine("<div class='card'><h2>📊 Tail Risk Profiles</h2><table>");
                sb.AppendLine("<tr><th>Category</th><th>Samples</th><th>Kurtosis</th><th>Tail</th><th>VaR95</th><th>CVaR</th></tr>");
                foreach (var tp in report.TailProfiles)
                {
                    string tailTag = tp.TailType switch
                    {
                        TailType.SuperFatTailed => "tag-red",
                        TailType.FatTailed => "tag-orange",
                        TailType.ModerateTail => "tag-yellow",
                        _ => "tag-green"
                    };
                    sb.AppendLine($"<tr><td>{tp.Category}</td><td>{tp.SampleCount}</td>" +
                                  $"<td>{tp.Kurtosis:F1}</td><td><span class='tag {tailTag}'>{tp.TailType}</span></td>" +
                                  $"<td>{tp.ValueAtRisk95:F1}</td><td>{tp.ExpectedShortfall:F1}</td></tr>");
                }
                sb.AppendLine("</table></div>");
            }

            // Cascade Chains
            if (report.Cascades.Count > 0)
            {
                sb.AppendLine("<div class='card'><h2>🔗 Cascade Chains</h2><table>");
                sb.AppendLine("<tr><th>Patient Zero</th><th>Length</th><th>Impact</th><th>Duration</th><th>Trigger</th></tr>");
                foreach (var c in report.Cascades.Take(10))
                {
                    sb.AppendLine($"<tr><td>{EscapeHtml(c.PatientZeroPromptId)}</td><td>{c.ChainLength}</td>" +
                                  $"<td>{c.TotalImpact:F0}</td><td>{c.DurationSeconds:F0}s</td>" +
                                  $"<td>{c.TriggerCategory}</td></tr>");
                }
                sb.AppendLine("</table></div>");
            }

            // Prompt Exposure
            if (report.Snapshots.Count > 0)
            {
                sb.AppendLine("<div class='card'><h2>🎯 Prompt Exposure</h2><table>");
                sb.AppendLine("<tr><th>Prompt</th><th>Exposure</th><th>Tier</th><th>Events</th><th>Worst</th></tr>");
                foreach (var s in report.Snapshots.OrderByDescending(s => s.ExposureScore).Take(15))
                {
                    string tierTag = s.Tier switch
                    {
                        BlackSwanHealthTier.Blind => "tag-red",
                        BlackSwanHealthTier.Exposed => "tag-orange",
                        BlackSwanHealthTier.Aware => "tag-yellow",
                        BlackSwanHealthTier.Prepared => "tag-blue",
                        _ => "tag-green"
                    };
                    sb.AppendLine($"<tr><td>{EscapeHtml(s.PromptId)}</td><td>{s.ExposureScore:F0}</td>" +
                                  $"<td><span class='tag {tierTag}'>{s.Tier}</span></td>" +
                                  $"<td>{s.EventCount}</td><td>{s.WorstSeverity?.ToString() ?? "—"}</td></tr>");
                }
                sb.AppendLine("</table></div>");
            }

            // Severity Distribution
            var sevDist = _events.GroupBy(e => e.Severity)
                .OrderByDescending(g => (int)g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
            sb.AppendLine("<div class='card'><h2>📈 Severity Distribution</h2><table>");
            sb.AppendLine("<tr><th>Severity</th><th>Count</th><th>% of Total</th></tr>");
            foreach (var (sev, count) in sevDist)
            {
                double pct = _events.Count > 0 ? (double)count / _events.Count * 100 : 0;
                sb.AppendLine($"<tr><td>{sev}</td><td>{count}</td><td>{pct:F1}%</td></tr>");
            }
            sb.AppendLine("</table></div>");

            sb.AppendLine("</div>"); // grid
            sb.AppendLine($"<div style='text-align:center;color:#475569;margin-top:30px;font-size:0.8em'>Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · PromptBlackSwanEngine</div>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        // ── Private Helpers ─────────────────────────

        /// <summary>
        /// Computes composite exposure score from pre-filtered prompt events
        /// and pre-computed cascade chains (avoids redundant DetectCascades).
        /// </summary>
        private double ComputeExposureScore(
            string promptId,
            List<BlackSwanEvent> events,
            List<BlackSwanEvent> extremeEvents,
            List<CascadeChain> cascades)
        {
            if (events.Count == 0) return 0;

            // 40%: extreme event rate
            double extremeRate = (double)extremeEvents.Count / events.Count;
            double extremeComponent = extremeRate * 100 * 0.40;

            // 25%: tail risk (use worst kurtosis)
            var tailProfiles = AnalyzeTailRisk(promptId);
            double tailComponent = 0;
            if (tailProfiles.Count > 0)
            {
                double worstKurtosis = tailProfiles.Max(p => p.Kurtosis);
                tailComponent = Math.Min(worstKurtosis / 10 * 100, 100) * 0.25;
            }

            // 20%: cascade involvement (uses pre-computed cascades)
            int cascadeInvolvement = cascades.Count(c =>
                c.PatientZeroPromptId == promptId || c.AffectedPromptIds.Contains(promptId));
            double cascadeComponent = Math.Min(cascadeInvolvement * 20.0, 100) * 0.20;

            // 15%: severity concentration
            double maxImpact = events.Max(e => e.Impact);
            double severityComponent = maxImpact * 0.15;

            return Math.Clamp(extremeComponent + tailComponent + cascadeComponent + severityComponent, 0, 100);
        }

        private static BlackSwanHealthTier ScoreToTier(double score) => score switch
        {
            >= 85 => BlackSwanHealthTier.Fortified,
            >= 70 => BlackSwanHealthTier.Prepared,
            >= 50 => BlackSwanHealthTier.Aware,
            >= 30 => BlackSwanHealthTier.Exposed,
            _ => BlackSwanHealthTier.Blind
        };

        private static double ComputeStdDev(List<double> values, double mean)
        {
            if (values.Count < 2) return 0;
            double sumSq = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / (values.Count - 1));
        }

        private static double ComputeMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            if (n == 0) return 0;
            if (n % 2 == 1) return sorted[n / 2];
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        private static double ComputeMAD(List<double> values, double median)
        {
            var deviations = values.Select(v => Math.Abs(v - median)).ToList();
            return ComputeMedian(deviations);
        }

        private static double ComputeExcessKurtosis(List<double> values, double mean)
        {
            int n = values.Count;
            if (n < 4) return 0;
            double variance = values.Sum(v => (v - mean) * (v - mean)) / n;
            if (variance < 1e-12) return 0;
            double m4 = values.Sum(v => Math.Pow(v - mean, 4)) / n;
            return m4 / (variance * variance) - 3;
        }

        private static double ComputePercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;
            double index = percentile * (sortedValues.Count - 1);
            int lower = (int)Math.Floor(index);
            int upper = Math.Min(lower + 1, sortedValues.Count - 1);
            double frac = index - lower;
            return sortedValues[lower] * (1 - frac) + sortedValues[upper] * frac;
        }

        private static double ComputeExpectedShortfall(List<double> sortedValues, double varThreshold)
        {
            var tail = sortedValues.Where(v => v >= varThreshold).ToList();
            return tail.Count > 0 ? tail.Average() : varThreshold;
        }

        private static double ComputePhiCorrelation(int total, int joint, int countA, int countB)
        {
            // Phi coefficient for 2x2 contingency table
            int notA = total - countA;
            int notB = total - countB;
            int jointNot = total - countA - countB + joint;
            double num = (double)joint * jointNot - (double)(countA - joint) * (countB - joint);
            double denom = Math.Sqrt((double)countA * countB * notA * notB);
            return denom > 1e-9 ? num / denom : 0;
        }

        private static (FailureCategory, FailureCategory) OrderPair(FailureCategory a, FailureCategory b)
        {
            return a <= b ? (a, b) : (b, a);
        }

        private static List<List<BlackSwanEvent>> GroupIntoWindows(List<BlackSwanEvent> events, double windowSeconds)
        {
            if (events.Count == 0) return new List<List<BlackSwanEvent>>();

            var sorted = events.OrderBy(e => e.Timestamp).ToList();
            var windows = new List<List<BlackSwanEvent>>();
            var current = new List<BlackSwanEvent> { sorted[0] };

            for (int i = 1; i < sorted.Count; i++)
            {
                if ((sorted[i].Timestamp - current[0].Timestamp).TotalSeconds <= windowSeconds)
                {
                    current.Add(sorted[i]);
                }
                else
                {
                    windows.Add(current);
                    current = new List<BlackSwanEvent> { sorted[i] };
                }
            }

            if (current.Count > 0)
                windows.Add(current);

            return windows;
        }

        private static string EscapeHtml(string text)
            => System.Net.WebUtility.HtmlEncode(text);
    }
}
