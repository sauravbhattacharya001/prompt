namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptDriftMonitor – Autonomous Prompt Performance Drift Detection
    //
    //  Tracks prompt effectiveness over time and detects degradation
    //  ("drift") caused by model updates, data shifts, or prompt rot.
    //  Records observations, computes statistical baselines, detects
    //  anomalies via z-score, measures trend via linear regression,
    //  and generates proactive recommendations and adaptation plans.
    // ────────────────────────────────────────────

    /// <summary>Metric dimension being monitored for drift.</summary>
    public enum DriftMetric
    {
        QualityScore,
        Latency,
        TokenUsage,
        ErrorRate,
        Consistency
    }

    /// <summary>Severity of a drift alert.</summary>
    public enum DriftSeverity
    {
        Info,
        Warning,
        Critical,
        Emergency
    }

    /// <summary>Direction of drift for a metric.</summary>
    public enum DriftDirection
    {
        Improving,
        Stable,
        Degrading,
        Volatile
    }

    /// <summary>Export format for drift reports.</summary>
    public enum DriftExportFormat
    {
        Json,
        Markdown,
        Html
    }

    /// <summary>A single recorded performance data point.</summary>
    public sealed class DriftObservation
    {
        public string PromptId { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double Score { get; set; }
        public double LatencyMs { get; set; }
        public int TokenCount { get; set; }
        public double ErrorRate { get; set; }
        public string ModelVersion { get; set; } = "";
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>Configurable thresholds for drift detection.</summary>
    public sealed class DriftPolicy
    {
        public double ScoreDropThreshold { get; set; } = 0.15;
        public double LatencyIncreasePercent { get; set; } = 50.0;
        public double ZScoreThreshold { get; set; } = 2.5;
        public int MinObservationsForBaseline { get; set; } = 20;
        public int WindowSizeDays { get; set; } = 7;
        public HashSet<DriftMetric> EnabledMetrics { get; set; } = new HashSet<DriftMetric>(
            (DriftMetric[])Enum.GetValues(typeof(DriftMetric)));

        /// <summary>High-sensitivity policy for critical prompts.</summary>
        public static DriftPolicy Sensitive() => new DriftPolicy
        {
            ScoreDropThreshold = 0.08,
            LatencyIncreasePercent = 25.0,
            ZScoreThreshold = 2.0,
            MinObservationsForBaseline = 10,
            WindowSizeDays = 3
        };

        /// <summary>Relaxed policy tolerating larger deviations.</summary>
        public static DriftPolicy Relaxed() => new DriftPolicy
        {
            ScoreDropThreshold = 0.25,
            LatencyIncreasePercent = 100.0,
            ZScoreThreshold = 3.0,
            MinObservationsForBaseline = 30,
            WindowSizeDays = 14
        };

        /// <summary>Balanced default policy.</summary>
        public static DriftPolicy Standard() => new DriftPolicy();
    }

    /// <summary>Statistics for a time window of observations.</summary>
    public sealed class DriftWindow
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int ObservationCount { get; set; }
        public double MeanScore { get; set; }
        public double StdDevScore { get; set; }
        public double MinScore { get; set; }
        public double MaxScore { get; set; }
        public double MeanLatency { get; set; }
        public double StdDevLatency { get; set; }
        public double MeanTokens { get; set; }
        public double MeanErrorRate { get; set; }
        public double TrendSlope { get; set; }
        public DriftDirection Direction { get; set; }
    }

    /// <summary>Alert generated when drift is detected.</summary>
    public sealed class DriftAlert
    {
        public DriftMetric Metric { get; set; }
        public DriftSeverity Severity { get; set; }
        public DriftDirection Direction { get; set; }
        public string Message { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double DeviationPercent { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Comparison between baseline and recent windows.</summary>
    public sealed class DriftComparison
    {
        public DriftWindow BaselineWindow { get; set; } = new DriftWindow();
        public DriftWindow RecentWindow { get; set; } = new DriftWindow();
        public Dictionary<DriftMetric, double> PerMetricDelta { get; set; } = new Dictionary<DriftMetric, double>();
        public DriftDirection OverallVerdict { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>Autonomous adaptation plan based on detected drift.</summary>
    public sealed class AdaptationPlan
    {
        public List<string> Actions { get; set; } = new List<string>();
        public DriftSeverity Priority { get; set; }
        public string EstimatedImpact { get; set; } = "";
        public bool AutoApplicable { get; set; }
    }

    /// <summary>Per-metric summary within a report.</summary>
    public sealed class MetricSummary
    {
        public DriftMetric Metric { get; set; }
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double DeltaPercent { get; set; }
        public DriftDirection Direction { get; set; }
    }

    /// <summary>Full drift analysis report for a prompt.</summary>
    public sealed class DriftReport
    {
        public string PromptId { get; set; } = "";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public double HealthScore { get; set; }
        public DriftWindow BaselineWindow { get; set; } = new DriftWindow();
        public DriftWindow RecentWindow { get; set; } = new DriftWindow();
        public List<DriftAlert> Alerts { get; set; } = new List<DriftAlert>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public AdaptationPlan AdaptationPlan { get; set; } = new AdaptationPlan();
        public Dictionary<DriftMetric, MetricSummary> PerMetricSummary { get; set; } = new Dictionary<DriftMetric, MetricSummary>();
    }

    /// <summary>
    /// Autonomous prompt performance drift monitor.  Records observations,
    /// detects statistical anomalies, computes trend lines, and produces
    /// actionable reports with adaptation plans.
    /// </summary>
    public sealed class PromptDriftMonitor
    {
        private readonly DriftPolicy _policy;
        private readonly Dictionary<string, List<DriftObservation>> _store = new Dictionary<string, List<DriftObservation>>();

        public PromptDriftMonitor() : this(DriftPolicy.Standard()) { }
        public PromptDriftMonitor(DriftPolicy policy) { _policy = policy ?? DriftPolicy.Standard(); }

        // ── Recording ────────────────────────────

        /// <summary>Record a single observation.</summary>
        public void Record(DriftObservation obs)
        {
            if (obs == null) return;
            if (!_store.ContainsKey(obs.PromptId))
                _store[obs.PromptId] = new List<DriftObservation>();
            _store[obs.PromptId].Add(obs);
        }

        /// <summary>Record a batch of observations.</summary>
        public void RecordBatch(IEnumerable<DriftObservation> observations)
        {
            if (observations == null) return;
            foreach (var obs in observations) Record(obs);
        }

        /// <summary>List all tracked prompt IDs.</summary>
        public List<string> TrackedPrompts => _store.Keys.OrderBy(k => k).ToList();

        // ── Analysis ─────────────────────────────

        /// <summary>Full drift analysis for a single prompt.</summary>
        public DriftReport Analyze(string promptId)
        {
            var report = new DriftReport { PromptId = promptId };
            if (!_store.ContainsKey(promptId) || _store[promptId].Count == 0)
            {
                report.HealthScore = 100;
                report.Recommendations.Add("No observations recorded yet. Start recording to enable drift detection.");
                return report;
            }

            var all = _store[promptId].OrderBy(o => o.Timestamp).ToList();
            var baseline = GetWindowInternal(all, _policy.WindowSizeDays * 2, _policy.WindowSizeDays);
            var recent = GetWindowInternal(all, _policy.WindowSizeDays, 0);

            report.BaselineWindow = baseline;
            report.RecentWindow = recent;

            var alerts = new List<DriftAlert>();

            // Quality score drift
            if (_policy.EnabledMetrics.Contains(DriftMetric.QualityScore) && baseline.ObservationCount >= _policy.MinObservationsForBaseline)
            {
                double drop = baseline.MeanScore - recent.MeanScore;
                if (drop > _policy.ScoreDropThreshold)
                {
                    var severity = drop > _policy.ScoreDropThreshold * 2 ? DriftSeverity.Critical :
                                   drop > _policy.ScoreDropThreshold * 1.5 ? DriftSeverity.Warning : DriftSeverity.Warning;
                    alerts.Add(new DriftAlert
                    {
                        Metric = DriftMetric.QualityScore,
                        Severity = severity,
                        Direction = DriftDirection.Degrading,
                        BaselineValue = baseline.MeanScore,
                        CurrentValue = recent.MeanScore,
                        DeviationPercent = baseline.MeanScore > 0 ? (drop / baseline.MeanScore) * 100 : 0,
                        Message = $"Quality score dropped {drop:F3} (from {baseline.MeanScore:F3} to {recent.MeanScore:F3})",
                        Recommendation = DetectModelChange(all)
                            ? "Score dropped after model version change — consider re-tuning prompts for the new model"
                            : "Quality degradation detected — review recent prompt changes or test with alternative phrasings"
                    });
                }
                report.PerMetricSummary[DriftMetric.QualityScore] = new MetricSummary
                {
                    Metric = DriftMetric.QualityScore,
                    BaselineValue = baseline.MeanScore,
                    CurrentValue = recent.MeanScore,
                    DeltaPercent = baseline.MeanScore > 0 ? ((recent.MeanScore - baseline.MeanScore) / baseline.MeanScore) * 100 : 0,
                    Direction = ClassifyDelta(recent.MeanScore - baseline.MeanScore, baseline.StdDevScore)
                };
            }

            // Latency drift
            if (_policy.EnabledMetrics.Contains(DriftMetric.Latency) && baseline.ObservationCount >= _policy.MinObservationsForBaseline)
            {
                double incPct = baseline.MeanLatency > 0 ? ((recent.MeanLatency - baseline.MeanLatency) / baseline.MeanLatency) * 100 : 0;
                if (incPct > _policy.LatencyIncreasePercent)
                {
                    alerts.Add(new DriftAlert
                    {
                        Metric = DriftMetric.Latency,
                        Severity = incPct > _policy.LatencyIncreasePercent * 2 ? DriftSeverity.Critical : DriftSeverity.Warning,
                        Direction = DriftDirection.Degrading,
                        BaselineValue = baseline.MeanLatency,
                        CurrentValue = recent.MeanLatency,
                        DeviationPercent = incPct,
                        Message = $"Latency increased {incPct:F1}% (from {baseline.MeanLatency:F0}ms to {recent.MeanLatency:F0}ms)",
                        Recommendation = "Investigate model endpoint performance or consider prompt simplification to reduce token count"
                    });
                }
                report.PerMetricSummary[DriftMetric.Latency] = new MetricSummary
                {
                    Metric = DriftMetric.Latency,
                    BaselineValue = baseline.MeanLatency,
                    CurrentValue = recent.MeanLatency,
                    DeltaPercent = incPct,
                    Direction = incPct > 10 ? DriftDirection.Degrading : incPct < -10 ? DriftDirection.Improving : DriftDirection.Stable
                };
            }

            // Token usage drift
            if (_policy.EnabledMetrics.Contains(DriftMetric.TokenUsage) && baseline.ObservationCount >= _policy.MinObservationsForBaseline)
            {
                double tokenDelta = baseline.MeanTokens > 0 ? ((recent.MeanTokens - baseline.MeanTokens) / baseline.MeanTokens) * 100 : 0;
                if (Math.Abs(tokenDelta) > 30)
                {
                    alerts.Add(new DriftAlert
                    {
                        Metric = DriftMetric.TokenUsage,
                        Severity = DriftSeverity.Info,
                        Direction = tokenDelta > 0 ? DriftDirection.Degrading : DriftDirection.Improving,
                        BaselineValue = baseline.MeanTokens,
                        CurrentValue = recent.MeanTokens,
                        DeviationPercent = tokenDelta,
                        Message = $"Token usage shifted {tokenDelta:F1}% (from {baseline.MeanTokens:F0} to {recent.MeanTokens:F0})",
                        Recommendation = "Token usage change may indicate prompt expansion or model verbosity shift"
                    });
                }
                report.PerMetricSummary[DriftMetric.TokenUsage] = new MetricSummary
                {
                    Metric = DriftMetric.TokenUsage,
                    BaselineValue = baseline.MeanTokens,
                    CurrentValue = recent.MeanTokens,
                    DeltaPercent = tokenDelta,
                    Direction = Math.Abs(tokenDelta) < 10 ? DriftDirection.Stable : tokenDelta > 0 ? DriftDirection.Degrading : DriftDirection.Improving
                };
            }

            // Error rate drift
            if (_policy.EnabledMetrics.Contains(DriftMetric.ErrorRate) && baseline.ObservationCount >= _policy.MinObservationsForBaseline)
            {
                double errDelta = recent.MeanErrorRate - baseline.MeanErrorRate;
                if (errDelta > 0.05)
                {
                    alerts.Add(new DriftAlert
                    {
                        Metric = DriftMetric.ErrorRate,
                        Severity = errDelta > 0.15 ? DriftSeverity.Critical : DriftSeverity.Warning,
                        Direction = DriftDirection.Degrading,
                        BaselineValue = baseline.MeanErrorRate,
                        CurrentValue = recent.MeanErrorRate,
                        DeviationPercent = baseline.MeanErrorRate > 0 ? (errDelta / baseline.MeanErrorRate) * 100 : errDelta * 100,
                        Message = $"Error rate increased from {baseline.MeanErrorRate:P1} to {recent.MeanErrorRate:P1}",
                        Recommendation = "Rising error rate may indicate model API issues or prompt incompatibility with current model version"
                    });
                }
                report.PerMetricSummary[DriftMetric.ErrorRate] = new MetricSummary
                {
                    Metric = DriftMetric.ErrorRate,
                    BaselineValue = baseline.MeanErrorRate,
                    CurrentValue = recent.MeanErrorRate,
                    DeltaPercent = baseline.MeanErrorRate > 0 ? (errDelta / baseline.MeanErrorRate) * 100 : 0,
                    Direction = errDelta > 0.05 ? DriftDirection.Degrading : errDelta < -0.05 ? DriftDirection.Improving : DriftDirection.Stable
                };
            }

            report.Alerts = alerts;
            report.HealthScore = ComputeHealthScore(report);
            report.Recommendations = RecommendInternal(promptId, all, baseline, recent);
            report.AdaptationPlan = AutoAdaptInternal(promptId, all, baseline, recent);

            return report;
        }

        /// <summary>Analyze all tracked prompts.</summary>
        public List<DriftReport> AnalyzeAll()
        {
            return _store.Keys.Select(Analyze).OrderBy(r => r.HealthScore).ToList();
        }

        /// <summary>Get all alerts at or above a severity threshold.</summary>
        public List<DriftAlert> GetAlerts(DriftSeverity minSeverity = DriftSeverity.Warning)
        {
            return _store.Keys
                .SelectMany(id => Analyze(id).Alerts)
                .Where(a => a.Severity >= minSeverity)
                .OrderByDescending(a => a.Severity)
                .ToList();
        }

        /// <summary>Get a statistical window for a prompt over the last N days.</summary>
        public DriftWindow GetWindow(string promptId, int days)
        {
            if (!_store.ContainsKey(promptId)) return new DriftWindow();
            return GetWindowInternal(_store[promptId].OrderBy(o => o.Timestamp).ToList(), days, 0);
        }

        /// <summary>Get metric trend as time-series data points.</summary>
        public List<(DateTime Time, double Value)> GetTrend(string promptId, DriftMetric metric)
        {
            if (!_store.ContainsKey(promptId)) return new List<(DateTime, double)>();
            return _store[promptId]
                .OrderBy(o => o.Timestamp)
                .Select(o => (o.Timestamp, ExtractMetric(o, metric)))
                .ToList();
        }

        /// <summary>Compare recent window against baseline window.</summary>
        public DriftComparison CompareWindows(string promptId, int recentDays, int baselineDays)
        {
            if (!_store.ContainsKey(promptId))
                return new DriftComparison { OverallVerdict = DriftDirection.Stable, Confidence = 0 };

            var all = _store[promptId].OrderBy(o => o.Timestamp).ToList();
            var baseline = GetWindowInternal(all, baselineDays, recentDays);
            var recent = GetWindowInternal(all, recentDays, 0);

            var deltas = new Dictionary<DriftMetric, double>();
            if (baseline.MeanScore > 0) deltas[DriftMetric.QualityScore] = ((recent.MeanScore - baseline.MeanScore) / baseline.MeanScore) * 100;
            if (baseline.MeanLatency > 0) deltas[DriftMetric.Latency] = ((recent.MeanLatency - baseline.MeanLatency) / baseline.MeanLatency) * 100;
            if (baseline.MeanTokens > 0) deltas[DriftMetric.TokenUsage] = ((recent.MeanTokens - baseline.MeanTokens) / baseline.MeanTokens) * 100;

            int degrading = deltas.Count(d => (d.Key == DriftMetric.QualityScore && d.Value < -10) ||
                                               (d.Key != DriftMetric.QualityScore && d.Value > 15));
            int improving = deltas.Count(d => (d.Key == DriftMetric.QualityScore && d.Value > 10) ||
                                               (d.Key != DriftMetric.QualityScore && d.Value < -15));

            var verdict = degrading > improving ? DriftDirection.Degrading :
                          improving > degrading ? DriftDirection.Improving :
                          DriftDirection.Stable;

            double confidence = Math.Min(1.0, (double)Math.Min(baseline.ObservationCount, recent.ObservationCount) / _policy.MinObservationsForBaseline);

            return new DriftComparison
            {
                BaselineWindow = baseline,
                RecentWindow = recent,
                PerMetricDelta = deltas,
                OverallVerdict = verdict,
                Confidence = confidence
            };
        }

        /// <summary>Detect statistical outlier observations via z-score.</summary>
        public List<DriftObservation> DetectAnomalies(string promptId)
        {
            if (!_store.ContainsKey(promptId) || _store[promptId].Count < 5) return new List<DriftObservation>();
            var all = _store[promptId];
            double mean = all.Average(o => o.Score);
            double stddev = StdDev(all.Select(o => o.Score));
            if (stddev < 0.001) return new List<DriftObservation>();
            return all.Where(o => Math.Abs((o.Score - mean) / stddev) > _policy.ZScoreThreshold).ToList();
        }

        /// <summary>Generate proactive recommendations for a prompt.</summary>
        public List<string> Recommend(string promptId)
        {
            if (!_store.ContainsKey(promptId) || _store[promptId].Count == 0)
                return new List<string> { "Start recording observations to enable drift detection." };

            var all = _store[promptId].OrderBy(o => o.Timestamp).ToList();
            if (all.Count < _policy.MinObservationsForBaseline)
                return new List<string> { $"Only {all.Count}/{_policy.MinObservationsForBaseline} observations recorded. Collect more data for reliable drift detection." };

            var baseline = GetWindowInternal(all, _policy.WindowSizeDays * 2, _policy.WindowSizeDays);
            var recent = GetWindowInternal(all, _policy.WindowSizeDays, 0);
            return RecommendInternal(promptId, all, baseline, recent);
        }

        /// <summary>Internal recommendation logic using pre-computed windows — avoids
        /// redundant sort + filter + stats when called from <see cref="Analyze"/>.</summary>
        private List<string> RecommendInternal(string promptId, List<DriftObservation> all, DriftWindow baseline, DriftWindow recent)
        {
            var recs = new List<string>();
            if (all.Count < _policy.MinObservationsForBaseline)
            {
                recs.Add($"Only {all.Count}/{_policy.MinObservationsForBaseline} observations recorded. Collect more data for reliable drift detection.");
                return recs;
            }

            if (recent.MeanScore < baseline.MeanScore - _policy.ScoreDropThreshold)
            {
                if (DetectModelChange(all))
                    recs.Add($"Quality score dropped after model version change — consider re-tuning prompts for the new model version");
                else
                    recs.Add("Quality degradation detected — A/B test alternative prompt phrasings");
            }

            if (recent.TrendSlope < -0.01)
                recs.Add("Negative quality trend detected — schedule a prompt review and refresh cycle");

            if (baseline.MeanLatency > 0 && ((recent.MeanLatency - baseline.MeanLatency) / baseline.MeanLatency) > 0.3)
                recs.Add("Latency trending upward — consider prompt compression or switching to a faster model tier");

            if (recent.StdDevScore > baseline.StdDevScore * 1.5)
                recs.Add("Response consistency declining — add more explicit constraints or output format instructions");

            var anomalies = DetectAnomalies(promptId);
            if (anomalies.Count > 0)
                recs.Add($"{anomalies.Count} anomalous observations detected — investigate outlier conditions (model, time of day, input patterns)");

            if (recent.Direction == DriftDirection.Improving)
                recs.Add("Positive trend detected — document current prompt version as a known-good baseline");

            if (recs.Count == 0)
                recs.Add("All metrics within normal range. No action needed.");

            return recs;
        }

        /// <summary>Generate an autonomous adaptation plan based on detected drift.</summary>
        public AdaptationPlan AutoAdapt(string promptId)
        {
            if (!_store.ContainsKey(promptId) || _store[promptId].Count < _policy.MinObservationsForBaseline)
                return new AdaptationPlan { Priority = DriftSeverity.Info, EstimatedImpact = "Insufficient data for adaptation", AutoApplicable = false };

            var all = _store[promptId].OrderBy(o => o.Timestamp).ToList();
            var baseline = GetWindowInternal(all, _policy.WindowSizeDays * 2, _policy.WindowSizeDays);
            var recent = GetWindowInternal(all, _policy.WindowSizeDays, 0);
            return AutoAdaptInternal(promptId, all, baseline, recent);
        }

        /// <summary>Internal adaptation logic using pre-computed windows — avoids
        /// redundant sort + filter + stats when called from <see cref="Analyze"/>.</summary>
        private AdaptationPlan AutoAdaptInternal(string promptId, List<DriftObservation> all, DriftWindow baseline, DriftWindow recent)
        {
            var plan = new AdaptationPlan();

            double scoreDrop = baseline.MeanScore - recent.MeanScore;
            bool modelChanged = DetectModelChange(all);
            double latencyInc = baseline.MeanLatency > 0 ? ((recent.MeanLatency - baseline.MeanLatency) / baseline.MeanLatency) * 100 : 0;

            if (scoreDrop > _policy.ScoreDropThreshold * 2)
            {
                plan.Priority = DriftSeverity.Critical;
                plan.Actions.Add("URGENT: Roll back to last known-good prompt version");
                plan.Actions.Add("Run A/B test comparing current vs previous prompt");
                if (modelChanged)
                    plan.Actions.Add("Re-calibrate prompt for new model version — adjust tone, specificity, and format instructions");
                plan.EstimatedImpact = $"Potential score recovery of +{scoreDrop:F3}";
                plan.AutoApplicable = true;
            }
            else if (scoreDrop > _policy.ScoreDropThreshold)
            {
                plan.Priority = DriftSeverity.Warning;
                plan.Actions.Add("Schedule prompt review within 48 hours");
                plan.Actions.Add("Generate variant prompts for A/B testing");
                if (modelChanged)
                    plan.Actions.Add("Test prompt with explicit model-version-aware instructions");
                plan.EstimatedImpact = $"Expected score stabilization within {_policy.WindowSizeDays} days";
                plan.AutoApplicable = false;
            }
            else
            {
                plan.Priority = DriftSeverity.Info;
                plan.EstimatedImpact = "No significant drift — continue monitoring";
                plan.AutoApplicable = false;
            }

            if (latencyInc > _policy.LatencyIncreasePercent)
            {
                plan.Actions.Add("Optimize prompt token count — remove redundant instructions");
                plan.Actions.Add("Consider response streaming to improve perceived latency");
            }

            if (recent.StdDevScore > baseline.StdDevScore * 1.5)
                plan.Actions.Add("Add output format constraints to reduce response variance");

            if (plan.Actions.Count == 0)
                plan.Actions.Add("Continue current monitoring — no intervention needed");

            return plan;
        }

        /// <summary>Overall health score (0-100) for a prompt.</summary>
        public double GetHealthScore(string promptId) => ComputeHealthScore(Analyze(promptId));

        /// <summary>Export a drift report in the specified format.</summary>
        public string ExportReport(DriftReport report, DriftExportFormat format)
        {
            switch (format)
            {
                case DriftExportFormat.Json: return ExportJson(report);
                case DriftExportFormat.Markdown: return ExportMarkdown(report);
                case DriftExportFormat.Html: return ExportHtml(report);
                default: return ExportJson(report);
            }
        }

        /// <summary>Clear history for a specific prompt.</summary>
        public void Reset(string promptId) { _store.Remove(promptId); }

        /// <summary>Clear all history.</summary>
        public void ResetAll() { _store.Clear(); }

        // ── Internal helpers ─────────────────────

        private DriftWindow GetWindowInternal(List<DriftObservation> sorted, int daysBack, int daysForward)
        {
            var now = sorted.Count > 0 ? sorted.Last().Timestamp : DateTime.UtcNow;
            var windowEnd = now.AddDays(-daysForward);
            var windowStart = now.AddDays(-daysBack);
            var windowed = sorted.Where(o => o.Timestamp >= windowStart && o.Timestamp <= windowEnd).ToList();

            if (windowed.Count == 0)
                return new DriftWindow { Start = windowStart, End = windowEnd, Direction = DriftDirection.Stable };

            var scores = windowed.Select(o => o.Score).ToList();
            double meanScore = scores.Average();
            double stdScore = StdDev(scores);

            // Linear regression on score over time
            double slope = 0;
            if (windowed.Count >= 3)
            {
                var xs = windowed.Select(o => (o.Timestamp - windowed[0].Timestamp).TotalDays).ToList();
                var ys = scores;
                slope = LinearSlope(xs, ys);
            }

            var dir = Math.Abs(slope) < 0.002 ? DriftDirection.Stable :
                      slope > 0 ? DriftDirection.Improving : DriftDirection.Degrading;
            if (stdScore > meanScore * 0.3 && meanScore > 0) dir = DriftDirection.Volatile;

            return new DriftWindow
            {
                Start = windowStart,
                End = windowEnd,
                ObservationCount = windowed.Count,
                MeanScore = meanScore,
                StdDevScore = stdScore,
                MinScore = scores.Min(),
                MaxScore = scores.Max(),
                MeanLatency = windowed.Average(o => o.LatencyMs),
                StdDevLatency = StdDev(windowed.Select(o => o.LatencyMs)),
                MeanTokens = windowed.Average(o => (double)o.TokenCount),
                MeanErrorRate = windowed.Average(o => o.ErrorRate),
                TrendSlope = slope,
                Direction = dir
            };
        }

        private static double ExtractMetric(DriftObservation obs, DriftMetric metric)
        {
            switch (metric)
            {
                case DriftMetric.QualityScore: return obs.Score;
                case DriftMetric.Latency: return obs.LatencyMs;
                case DriftMetric.TokenUsage: return obs.TokenCount;
                case DriftMetric.ErrorRate: return obs.ErrorRate;
                default: return obs.Score;
            }
        }

        private static double StdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;
            double mean = list.Average();
            double sumSq = list.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / (list.Count - 1));
        }

        private static double LinearSlope(List<double> xs, List<double> ys)
        {
            int n = xs.Count;
            if (n < 2) return 0;
            double sumX = xs.Sum(), sumY = ys.Sum();
            double sumXY = xs.Zip(ys, (x, y) => x * y).Sum();
            double sumX2 = xs.Sum(x => x * x);
            double denom = n * sumX2 - sumX * sumX;
            return Math.Abs(denom) < 1e-10 ? 0 : (n * sumXY - sumX * sumY) / denom;
        }

        private bool DetectModelChange(List<DriftObservation> sorted)
        {
            if (sorted.Count < 2) return false;
            var versions = sorted.Where(o => !string.IsNullOrEmpty(o.ModelVersion))
                                  .Select(o => o.ModelVersion).Distinct().ToList();
            return versions.Count > 1;
        }

        private DriftDirection ClassifyDelta(double delta, double stddev)
        {
            if (stddev > 0 && Math.Abs(delta) / stddev > 2) return delta > 0 ? DriftDirection.Improving : DriftDirection.Degrading;
            if (Math.Abs(delta) < 0.01) return DriftDirection.Stable;
            return delta > 0 ? DriftDirection.Improving : DriftDirection.Degrading;
        }

        private double ComputeHealthScore(DriftReport report)
        {
            double score = 100.0;
            foreach (var alert in report.Alerts)
            {
                switch (alert.Severity)
                {
                    case DriftSeverity.Emergency: score -= 30; break;
                    case DriftSeverity.Critical: score -= 20; break;
                    case DriftSeverity.Warning: score -= 10; break;
                    case DriftSeverity.Info: score -= 3; break;
                }
            }
            return Math.Max(0, Math.Min(100, score));
        }

        // ── Export helpers ───────────────────────

        private string ExportJson(DriftReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"promptId\": \"{Esc(r.PromptId)}\",");
            sb.AppendLine($"  \"generatedAt\": \"{r.GeneratedAt:O}\",");
            sb.AppendLine($"  \"healthScore\": {r.HealthScore:F1},");
            sb.AppendLine($"  \"alertCount\": {r.Alerts.Count},");
            sb.AppendLine("  \"alerts\": [");
            for (int i = 0; i < r.Alerts.Count; i++)
            {
                var a = r.Alerts[i];
                sb.Append($"    {{ \"metric\": \"{a.Metric}\", \"severity\": \"{a.Severity}\", \"message\": \"{Esc(a.Message)}\"");
                sb.Append($", \"recommendation\": \"{Esc(a.Recommendation)}\"");
                sb.Append($", \"baseline\": {a.BaselineValue:F4}, \"current\": {a.CurrentValue:F4}, \"deviation\": {a.DeviationPercent:F1}");
                sb.AppendLine(i < r.Alerts.Count - 1 ? " }," : " }");
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"recommendations\": [");
            for (int i = 0; i < r.Recommendations.Count; i++)
                sb.AppendLine($"    \"{Esc(r.Recommendations[i])}\"{(i < r.Recommendations.Count - 1 ? "," : "")}");
            sb.AppendLine("  ],");
            sb.AppendLine($"  \"adaptationPriority\": \"{r.AdaptationPlan.Priority}\",");
            sb.AppendLine("  \"adaptationActions\": [");
            for (int i = 0; i < r.AdaptationPlan.Actions.Count; i++)
                sb.AppendLine($"    \"{Esc(r.AdaptationPlan.Actions[i])}\"{(i < r.AdaptationPlan.Actions.Count - 1 ? "," : "")}");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"windows\": {");
            sb.AppendLine($"    \"baseline\": {{ \"count\": {r.BaselineWindow.ObservationCount}, \"meanScore\": {r.BaselineWindow.MeanScore:F4}, \"meanLatency\": {r.BaselineWindow.MeanLatency:F1}, \"direction\": \"{r.BaselineWindow.Direction}\" }},");
            sb.AppendLine($"    \"recent\": {{ \"count\": {r.RecentWindow.ObservationCount}, \"meanScore\": {r.RecentWindow.MeanScore:F4}, \"meanLatency\": {r.RecentWindow.MeanLatency:F1}, \"direction\": \"{r.RecentWindow.Direction}\" }}");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string ExportMarkdown(DriftReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Drift Report: {r.PromptId}");
            sb.AppendLine($"*Generated: {r.GeneratedAt:yyyy-MM-dd HH:mm} UTC*");
            sb.AppendLine();
            sb.AppendLine($"## Health Score: {r.HealthScore:F0}/100 {HealthEmoji(r.HealthScore)}");
            sb.AppendLine();

            if (r.Alerts.Count > 0)
            {
                sb.AppendLine("## Alerts");
                foreach (var a in r.Alerts)
                    sb.AppendLine($"- **[{a.Severity}]** {a.Message}  \n  → {a.Recommendation}");
                sb.AppendLine();
            }

            sb.AppendLine("## Windows");
            sb.AppendLine($"- **Baseline** ({r.BaselineWindow.ObservationCount} obs): score={r.BaselineWindow.MeanScore:F3}, latency={r.BaselineWindow.MeanLatency:F0}ms, trend={r.BaselineWindow.Direction}");
            sb.AppendLine($"- **Recent** ({r.RecentWindow.ObservationCount} obs): score={r.RecentWindow.MeanScore:F3}, latency={r.RecentWindow.MeanLatency:F0}ms, trend={r.RecentWindow.Direction}");
            sb.AppendLine();

            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine("## Recommendations");
                foreach (var rec in r.Recommendations) sb.AppendLine($"- {rec}");
                sb.AppendLine();
            }

            if (r.AdaptationPlan.Actions.Count > 0)
            {
                sb.AppendLine($"## Adaptation Plan (Priority: {r.AdaptationPlan.Priority})");
                foreach (var act in r.AdaptationPlan.Actions) sb.AppendLine($"1. {act}");
                if (!string.IsNullOrEmpty(r.AdaptationPlan.EstimatedImpact))
                    sb.AppendLine($"\n*Impact: {r.AdaptationPlan.EstimatedImpact}*");
            }

            return sb.ToString();
        }

        private string ExportHtml(DriftReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
            sb.AppendLine("<title>Drift Report – " + Esc(r.PromptId) + "</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:system-ui,sans-serif;max-width:800px;margin:2rem auto;padding:0 1rem;color:#1a1a2e;background:#f5f5f5}");
            sb.AppendLine("h1{color:#16213e}h2{color:#0f3460;border-bottom:2px solid #e94560;padding-bottom:.3rem}");
            sb.AppendLine(".health{font-size:2.5rem;font-weight:700;text-align:center;padding:1rem;border-radius:12px;margin:1rem 0}");
            sb.AppendLine(".health.good{background:#d4edda;color:#155724}.health.warn{background:#fff3cd;color:#856404}.health.bad{background:#f8d7da;color:#721c24}");
            sb.AppendLine(".alert{padding:.8rem;margin:.5rem 0;border-radius:8px;border-left:4px solid}");
            sb.AppendLine(".alert.Critical{border-color:#dc3545;background:#f8d7da}.alert.Warning{border-color:#ffc107;background:#fff3cd}");
            sb.AppendLine(".alert.Info{border-color:#17a2b8;background:#d1ecf1}.alert.Emergency{border-color:#6f42c1;background:#e8d5f5}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin:1rem 0}th,td{padding:.5rem;text-align:left;border-bottom:1px solid #dee2e6}th{background:#16213e;color:#fff}");
            sb.AppendLine(".rec{background:#fff;padding:.6rem;margin:.3rem 0;border-radius:6px;border:1px solid #dee2e6}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h1>📊 Drift Report: {Esc(r.PromptId)}</h1>");
            sb.AppendLine($"<p><em>Generated: {r.GeneratedAt:yyyy-MM-dd HH:mm} UTC</em></p>");

            string hclass = r.HealthScore >= 80 ? "good" : r.HealthScore >= 50 ? "warn" : "bad";
            sb.AppendLine($"<div class=\"health {hclass}\">{HealthEmoji(r.HealthScore)} {r.HealthScore:F0}/100</div>");

            if (r.Alerts.Count > 0)
            {
                sb.AppendLine("<h2>🚨 Alerts</h2>");
                foreach (var a in r.Alerts)
                {
                    sb.AppendLine($"<div class=\"alert {a.Severity}\">");
                    sb.AppendLine($"<strong>[{a.Severity}] {a.Metric}</strong>: {Esc(a.Message)}<br>");
                    sb.AppendLine($"<em>→ {Esc(a.Recommendation)}</em></div>");
                }
            }

            sb.AppendLine("<h2>📈 Windows</h2><table><tr><th>Window</th><th>Observations</th><th>Mean Score</th><th>Mean Latency</th><th>Trend</th></tr>");
            sb.AppendLine($"<tr><td>Baseline</td><td>{r.BaselineWindow.ObservationCount}</td><td>{r.BaselineWindow.MeanScore:F3}</td><td>{r.BaselineWindow.MeanLatency:F0}ms</td><td>{r.BaselineWindow.Direction}</td></tr>");
            sb.AppendLine($"<tr><td>Recent</td><td>{r.RecentWindow.ObservationCount}</td><td>{r.RecentWindow.MeanScore:F3}</td><td>{r.RecentWindow.MeanLatency:F0}ms</td><td>{r.RecentWindow.Direction}</td></tr>");
            sb.AppendLine("</table>");

            if (r.Recommendations.Count > 0)
            {
                sb.AppendLine("<h2>💡 Recommendations</h2>");
                foreach (var rec in r.Recommendations)
                    sb.AppendLine($"<div class=\"rec\">{Esc(rec)}</div>");
            }

            if (r.AdaptationPlan.Actions.Count > 0)
            {
                sb.AppendLine($"<h2>🔧 Adaptation Plan ({r.AdaptationPlan.Priority})</h2><ol>");
                foreach (var act in r.AdaptationPlan.Actions)
                    sb.AppendLine($"<li>{Esc(act)}</li>");
                sb.AppendLine("</ol>");
                if (!string.IsNullOrEmpty(r.AdaptationPlan.EstimatedImpact))
                    sb.AppendLine($"<p><em>Impact: {Esc(r.AdaptationPlan.EstimatedImpact)}</em></p>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string HealthEmoji(double score) => score >= 90 ? "🟢" : score >= 70 ? "🟡" : score >= 50 ? "🟠" : "🔴";
        private static string Esc(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") ?? "";
    }
}
