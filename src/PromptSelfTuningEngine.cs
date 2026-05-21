namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // ────────────────────────────────────────────
    //  PromptSelfTuningEngine – Autonomous Parameter Optimization
    //
    //  Uses a multi-armed bandit (Upper Confidence Bound) to
    //  autonomously discover optimal prompt parameters across
    //  temperature, top_p, frequency_penalty, and presence_penalty.
    //  Tracks execution outcomes per configuration arm, balances
    //  exploration vs exploitation, detects environment drift, and
    //  converges toward the best-performing parameter set.
    //
    //  Unlike PromptAutoImprover (text-level rewriting) or
    //  PromptCostOptimizer (token cost reduction), this engine
    //  focuses on runtime parameter tuning through statistical
    //  experimentation — prompts that tune themselves.
    // ────────────────────────────────────────────

    /// <summary>Current phase of the tuning lifecycle.</summary>
    public enum TuningPhase
    {
        /// <summary>Initial exploration — sampling all arms broadly.</summary>
        Exploration,
        /// <summary>Balanced phase — UCB drives arm selection.</summary>
        Balancing,
        /// <summary>Converging on a winner — exploitation dominates.</summary>
        Converging,
        /// <summary>Converged — optimal arm identified with high confidence.</summary>
        Converged,
        /// <summary>Drift detected — re-entering exploration.</summary>
        Drifted
    }

    /// <summary>Reason the engine recommended a parameter change.</summary>
    public enum TuningReason
    {
        /// <summary>UCB score highest among arms.</summary>
        UCBSelection,
        /// <summary>Forced exploration of under-sampled arm.</summary>
        ForcedExploration,
        /// <summary>Environment drift triggered re-exploration.</summary>
        DriftRecovery,
        /// <summary>Greedy pick of current best for exploitation.</summary>
        Exploitation,
        /// <summary>Initial round-robin sampling.</summary>
        RoundRobin
    }

    /// <summary>Quality dimension used to score a tuning outcome.</summary>
    public enum TuningQualityDimension
    {
        /// <summary>Overall correctness/accuracy of the response.</summary>
        Accuracy,
        /// <summary>Response latency in milliseconds.</summary>
        Latency,
        /// <summary>Token cost of the response.</summary>
        TokenCost,
        /// <summary>Coherence and readability.</summary>
        Coherence,
        /// <summary>Relevance to the original prompt.</summary>
        Relevance,
        /// <summary>Composite weighted score across all dimensions.</summary>
        Composite
    }

    /// <summary>Health tier for a tuning session.</summary>
    public enum TuningHealthTier
    {
        /// <summary>Score 90–100 — converged on a strong optimum.</summary>
        Optimal,
        /// <summary>Score 70–89 — good candidate found, still refining.</summary>
        Good,
        /// <summary>Score 50–69 — exploring, no clear winner yet.</summary>
        Exploring,
        /// <summary>Score 30–49 — struggling, high variance.</summary>
        Struggling,
        /// <summary>Score 0–29 — insufficient data or all arms failing.</summary>
        Insufficient
    }

    /// <summary>A single parameter configuration arm.</summary>
    public class TuningArm
    {
        /// <summary>Unique arm identifier.</summary>
        public string ArmId { get; set; } = "";

        /// <summary>Temperature setting (0.0–2.0).</summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>Top-P nucleus sampling (0.0–1.0).</summary>
        public double TopP { get; set; } = 1.0;

        /// <summary>Frequency penalty (-2.0 to 2.0).</summary>
        public double FrequencyPenalty { get; set; }

        /// <summary>Presence penalty (-2.0 to 2.0).</summary>
        public double PresencePenalty { get; set; }

        /// <summary>Max tokens limit (0 = no override).</summary>
        public int MaxTokens { get; set; }

        /// <summary>Optional label for this configuration.</summary>
        public string Label { get; set; } = "";

        /// <summary>Creates a display string of the arm's parameters.</summary>
        public string ToParameterString()
        {
            var parts = new List<string>
            {
                $"temp={Temperature:F2}",
                $"top_p={TopP:F2}",
                $"freq_pen={FrequencyPenalty:F2}",
                $"pres_pen={PresencePenalty:F2}"
            };
            if (MaxTokens > 0) parts.Add($"max_tok={MaxTokens}");
            return string.Join(", ", parts);
        }
    }

    /// <summary>Outcome of a single trial on an arm.</summary>
    public class TuningOutcome
    {
        /// <summary>Which arm was used.</summary>
        public string ArmId { get; set; } = "";

        /// <summary>When the trial ran.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Composite quality score 0.0–1.0.</summary>
        public double QualityScore { get; set; }

        /// <summary>Response latency in milliseconds.</summary>
        public double LatencyMs { get; set; }

        /// <summary>Tokens consumed.</summary>
        public int TokensUsed { get; set; }

        /// <summary>Whether the call succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Per-dimension quality breakdown.</summary>
        public Dictionary<TuningQualityDimension, double> DimensionScores { get; set; } = new();
    }

    /// <summary>UCB statistics for a single arm.</summary>
    public class ArmStatistics
    {
        /// <summary>Arm identifier.</summary>
        public string ArmId { get; set; } = "";

        /// <summary>Number of trials run.</summary>
        public int Trials { get; set; }

        /// <summary>Mean reward (quality score).</summary>
        public double MeanReward { get; set; }

        /// <summary>Reward standard deviation.</summary>
        public double StdDevReward { get; set; }

        /// <summary>UCB1 score (mean + exploration bonus).</summary>
        public double UCBScore { get; set; }

        /// <summary>Mean latency across trials.</summary>
        public double MeanLatencyMs { get; set; }

        /// <summary>Mean token usage.</summary>
        public double MeanTokens { get; set; }

        /// <summary>Success rate 0.0–1.0.</summary>
        public double SuccessRate { get; set; }

        /// <summary>Best single-trial quality observed.</summary>
        public double BestScore { get; set; }

        /// <summary>Worst single-trial quality observed.</summary>
        public double WorstScore { get; set; }
    }

    /// <summary>Recommendation from the engine on which arm to use next.</summary>
    public class TuningRecommendation
    {
        /// <summary>Recommended arm.</summary>
        public TuningArm Arm { get; set; } = new();

        /// <summary>Why this arm was selected.</summary>
        public TuningReason Reason { get; set; }

        /// <summary>Confidence in this recommendation 0.0–1.0.</summary>
        public double Confidence { get; set; }

        /// <summary>Current tuning phase.</summary>
        public TuningPhase Phase { get; set; }

        /// <summary>Human-readable explanation.</summary>
        public string Explanation { get; set; } = "";
    }

    /// <summary>Drift detection result for parameter tuning.</summary>
    public class TuningDriftReport
    {
        /// <summary>Whether drift was detected.</summary>
        public bool DriftDetected { get; set; }

        /// <summary>Magnitude of drift 0.0–1.0.</summary>
        public double DriftMagnitude { get; set; }

        /// <summary>Which arm(s) are affected.</summary>
        public List<string> AffectedArms { get; set; } = new();

        /// <summary>Description of the drift.</summary>
        public string Description { get; set; } = "";

        /// <summary>When drift was first detected.</summary>
        public DateTime? DetectedAt { get; set; }
    }

    /// <summary>Snapshot of a tuning session for a prompt.</summary>
    public class TuningSnapshot
    {
        /// <summary>Prompt being tuned.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>Current tuning phase.</summary>
        public TuningPhase Phase { get; set; }

        /// <summary>Total trials across all arms.</summary>
        public int TotalTrials { get; set; }

        /// <summary>Number of arms being tested.</summary>
        public int ArmCount { get; set; }

        /// <summary>Current best arm ID.</summary>
        public string? BestArmId { get; set; }

        /// <summary>Best arm's mean reward.</summary>
        public double BestMeanReward { get; set; }

        /// <summary>Health score 0–100.</summary>
        public double HealthScore { get; set; }

        /// <summary>Health tier classification.</summary>
        public TuningHealthTier HealthTier { get; set; }

        /// <summary>Per-arm statistics.</summary>
        public List<ArmStatistics> ArmStats { get; set; } = new();

        /// <summary>Latest drift report.</summary>
        public TuningDriftReport? Drift { get; set; }
    }

    /// <summary>Fleet-wide tuning health report.</summary>
    public class TuningFleetReport
    {
        /// <summary>Total prompts being tuned.</summary>
        public int TotalSessions { get; set; }

        /// <summary>Sessions in each phase.</summary>
        public Dictionary<TuningPhase, int> PhaseDistribution { get; set; } = new();

        /// <summary>Overall fleet health score 0–100.</summary>
        public double OverallHealthScore { get; set; }

        /// <summary>Sessions with active drift.</summary>
        public int DriftingSessions { get; set; }

        /// <summary>Per-session snapshots.</summary>
        public List<TuningSnapshot> Sessions { get; set; } = new();

        /// <summary>Autonomous insights.</summary>
        public List<string> Insights { get; set; } = new();
    }

    /// <summary>Configuration for the self-tuning engine.</summary>
    public class SelfTuningConfig
    {
        /// <summary>UCB exploration constant (higher = more exploration).</summary>
        public double ExplorationConstant { get; set; } = 1.41;

        /// <summary>Minimum trials per arm before entering Balancing phase.</summary>
        public int MinTrialsPerArm { get; set; } = 3;

        /// <summary>Minimum total trials before convergence is possible.</summary>
        public int MinTrialsForConvergence { get; set; } = 30;

        /// <summary>Minimum lead (mean reward gap) for convergence declaration.</summary>
        public double ConvergenceThreshold { get; set; } = 0.05;

        /// <summary>Window size for drift detection (recent trials).</summary>
        public int DriftWindowSize { get; set; } = 10;

        /// <summary>Drift detection threshold (mean reward drop).</summary>
        public double DriftThreshold { get; set; } = 0.15;

        /// <summary>Weights for quality dimensions in composite score.</summary>
        public Dictionary<TuningQualityDimension, double> DimensionWeights { get; set; } = new()
        {
            [TuningQualityDimension.Accuracy] = 0.4,
            [TuningQualityDimension.Latency] = 0.15,
            [TuningQualityDimension.TokenCost] = 0.1,
            [TuningQualityDimension.Coherence] = 0.2,
            [TuningQualityDimension.Relevance] = 0.15
        };
    }

    /// <summary>Internal per-prompt tuning state.</summary>
    internal class TuningSession
    {
        public string PromptId { get; set; } = "";
        public Dictionary<string, TuningArm> Arms { get; set; } = new();
        public List<TuningOutcome> History { get; set; } = new();
        public TuningPhase Phase { get; set; } = TuningPhase.Exploration;
        public string? ConvergedArmId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public TuningDriftReport? LastDrift { get; set; }
        public int RoundRobinIndex { get; set; }
    }

    /// <summary>
    /// Autonomous self-tuning engine for prompt parameters.
    /// Uses Upper Confidence Bound (UCB1) multi-armed bandit to
    /// balance exploration and exploitation across parameter
    /// configurations, detects environment drift, and converges
    /// on optimal settings over time.
    /// </summary>
    public class PromptSelfTuningEngine
    {
        private readonly SelfTuningConfig _config;
        private readonly Dictionary<string, TuningSession> _sessions = new();

        /// <summary>Creates a new self-tuning engine with default configuration.</summary>
        public PromptSelfTuningEngine() : this(new SelfTuningConfig()) { }

        /// <summary>Creates a new self-tuning engine with the specified configuration.</summary>
        public PromptSelfTuningEngine(SelfTuningConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // ── Arm Management ──────────────────────────────

        /// <summary>Registers a parameter arm for a prompt.</summary>
        public void RegisterArm(string promptId, TuningArm arm)
        {
            if (string.IsNullOrWhiteSpace(promptId)) throw new ArgumentException("promptId required");
            if (arm == null) throw new ArgumentNullException(nameof(arm));
            if (string.IsNullOrWhiteSpace(arm.ArmId)) throw new ArgumentException("arm.ArmId required");

            var session = GetOrCreateSession(promptId);
            session.Arms[arm.ArmId] = arm;

            // Reset convergence if new arm added after convergence
            if (session.Phase == TuningPhase.Converged)
            {
                session.Phase = TuningPhase.Balancing;
                session.ConvergedArmId = null;
            }
        }

        /// <summary>Registers a set of default arms spanning common parameter ranges.</summary>
        public void RegisterDefaultArms(string promptId)
        {
            var defaults = new[]
            {
                new TuningArm { ArmId = "conservative", Temperature = 0.3, TopP = 0.9, FrequencyPenalty = 0.0, PresencePenalty = 0.0, Label = "Conservative (low temp)" },
                new TuningArm { ArmId = "balanced", Temperature = 0.7, TopP = 1.0, FrequencyPenalty = 0.0, PresencePenalty = 0.0, Label = "Balanced (default)" },
                new TuningArm { ArmId = "creative", Temperature = 1.0, TopP = 0.95, FrequencyPenalty = 0.3, PresencePenalty = 0.3, Label = "Creative (high temp + penalties)" },
                new TuningArm { ArmId = "precise", Temperature = 0.2, TopP = 0.8, FrequencyPenalty = 0.1, PresencePenalty = 0.0, Label = "Precise (minimal variance)" },
                new TuningArm { ArmId = "diverse", Temperature = 0.9, TopP = 1.0, FrequencyPenalty = 0.5, PresencePenalty = 0.5, Label = "Diverse (high penalties)" },
            };

            foreach (var arm in defaults)
                RegisterArm(promptId, arm);
        }

        /// <summary>Gets all registered arms for a prompt.</summary>
        public List<TuningArm> GetArms(string promptId)
        {
            return _sessions.TryGetValue(promptId, out var s)
                ? s.Arms.Values.ToList()
                : new List<TuningArm>();
        }

        // ── Recommendation (the bandit) ─────────────────

        /// <summary>
        /// Recommends the next parameter arm to use for a trial.
        /// Uses UCB1 algorithm with forced exploration for under-sampled arms.
        /// </summary>
        public TuningRecommendation Recommend(string promptId)
        {
            if (!_sessions.TryGetValue(promptId, out var session))
                throw new InvalidOperationException($"No tuning session for '{promptId}'. Register arms first.");

            if (session.Arms.Count == 0)
                throw new InvalidOperationException("No arms registered.");

            var armList = session.Arms.Values.ToList();
            int totalTrials = session.History.Count;

            // Phase 1: Round-robin until every arm has MinTrialsPerArm
            var underSampled = armList.Where(a => CountTrials(session, a.ArmId) < _config.MinTrialsPerArm).ToList();
            if (underSampled.Count > 0)
            {
                session.Phase = TuningPhase.Exploration;
                var pick = underSampled[session.RoundRobinIndex % underSampled.Count];
                session.RoundRobinIndex++;
                return new TuningRecommendation
                {
                    Arm = pick,
                    Reason = TuningReason.RoundRobin,
                    Phase = TuningPhase.Exploration,
                    Confidence = 0.1,
                    Explanation = $"Round-robin exploration: arm '{pick.ArmId}' has {CountTrials(session, pick.ArmId)} trials (need {_config.MinTrialsPerArm})"
                };
            }

            // Check for drift
            if (session.LastDrift?.DriftDetected == true && session.Phase != TuningPhase.Drifted)
            {
                session.Phase = TuningPhase.Drifted;
                session.ConvergedArmId = null;
            }

            // Phase 2+: UCB1 selection
            UpdatePhase(session);

            if (session.Phase == TuningPhase.Converged && session.ConvergedArmId != null)
            {
                var best = session.Arms[session.ConvergedArmId];
                return new TuningRecommendation
                {
                    Arm = best,
                    Reason = TuningReason.Exploitation,
                    Phase = TuningPhase.Converged,
                    Confidence = 0.95,
                    Explanation = $"Converged on '{best.ArmId}' ({best.Label}) with mean reward {MeanReward(session, best.ArmId):F3}"
                };
            }

            // UCB1 selection
            string? bestArmId = null;
            double bestUCB = double.MinValue;

            foreach (var arm in armList)
            {
                int n = CountTrials(session, arm.ArmId);
                if (n == 0) continue;

                double mean = MeanReward(session, arm.ArmId);
                double ucb = mean + _config.ExplorationConstant * Math.Sqrt(Math.Log(totalTrials) / n);

                if (ucb > bestUCB)
                {
                    bestUCB = ucb;
                    bestArmId = arm.ArmId;
                }
            }

            bestArmId ??= armList[0].ArmId;
            var selected = session.Arms[bestArmId];
            double conf = session.Phase == TuningPhase.Converging ? 0.7 : 0.4;

            TuningReason reason;
            if (session.Phase == TuningPhase.Drifted)
                reason = TuningReason.DriftRecovery;
            else if (bestArmId == GetBestArmId(session))
                reason = TuningReason.Exploitation;
            else
                reason = TuningReason.UCBSelection;

            return new TuningRecommendation
            {
                Arm = selected,
                Reason = reason,
                Phase = session.Phase,
                Confidence = conf,
                Explanation = $"UCB1 selected '{selected.ArmId}' (UCB={bestUCB:F3}, mean={MeanReward(session, selected.ArmId):F3}, trials={CountTrials(session, selected.ArmId)})"
            };
        }

        // ── Record Outcome ──────────────────────────────

        /// <summary>Records a trial outcome for an arm.</summary>
        public void RecordOutcome(string promptId, TuningOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (!_sessions.TryGetValue(promptId, out var session))
                throw new InvalidOperationException($"No tuning session for '{promptId}'.");
            if (!session.Arms.ContainsKey(outcome.ArmId))
                throw new ArgumentException($"Unknown arm '{outcome.ArmId}'.");

            session.History.Add(outcome);

            // Drift detection
            DetectDrift(session, outcome.ArmId);

            // Re-evaluate phase
            UpdatePhase(session);
        }

        // ── Statistics & Snapshots ──────────────────────

        /// <summary>Gets statistics for a specific arm.</summary>
        public ArmStatistics GetArmStatistics(string promptId, string armId)
        {
            if (!_sessions.TryGetValue(promptId, out var session))
                throw new InvalidOperationException($"No tuning session for '{promptId}'.");

            return ComputeArmStats(session, armId);
        }

        /// <summary>Gets a snapshot of the tuning session for a prompt.</summary>
        public TuningSnapshot GetSnapshot(string promptId)
        {
            if (!_sessions.TryGetValue(promptId, out var session))
                throw new InvalidOperationException($"No tuning session for '{promptId}'.");

            var armStats = session.Arms.Keys.Select(id => ComputeArmStats(session, id)).ToList();
            double health = ComputeHealthScore(session);
            var bestId = GetBestArmId(session);

            return new TuningSnapshot
            {
                PromptId = promptId,
                Phase = session.Phase,
                TotalTrials = session.History.Count,
                ArmCount = session.Arms.Count,
                BestArmId = bestId,
                BestMeanReward = bestId != null ? MeanReward(session, bestId) : 0,
                HealthScore = health,
                HealthTier = ClassifyHealth(health),
                ArmStats = armStats,
                Drift = session.LastDrift
            };
        }

        /// <summary>Gets a fleet-wide report across all tuning sessions.</summary>
        public TuningFleetReport GetFleetReport()
        {
            var sessions = _sessions.Values.ToList();
            var snapshots = sessions.Select(s => GetSnapshot(s.PromptId)).ToList();

            var phaseDistribution = snapshots.GroupBy(s => s.Phase)
                .ToDictionary(g => g.Key, g => g.Count());

            double overallHealth = snapshots.Count > 0 ? snapshots.Average(s => s.HealthScore) : 0;

            var insights = GenerateFleetInsights(snapshots);

            return new TuningFleetReport
            {
                TotalSessions = sessions.Count,
                PhaseDistribution = phaseDistribution,
                OverallHealthScore = overallHealth,
                DriftingSessions = snapshots.Count(s => s.Drift?.DriftDetected == true),
                Sessions = snapshots,
                Insights = insights
            };
        }

        /// <summary>Resets a prompt's tuning session, keeping arms but clearing history.</summary>
        public void ResetSession(string promptId)
        {
            if (_sessions.TryGetValue(promptId, out var session))
            {
                session.History.Clear();
                session.Phase = TuningPhase.Exploration;
                session.ConvergedArmId = null;
                session.LastDrift = null;
                session.RoundRobinIndex = 0;
            }
        }

        /// <summary>Gets all tracked prompt IDs.</summary>
        public List<string> GetTrackedPrompts() => _sessions.Keys.ToList();

        // ── HTML Dashboard ──────────────────────────────

        /// <summary>Generates an interactive HTML dashboard for a prompt's tuning session.</summary>
        public string GenerateDashboard(string promptId)
        {
            var snapshot = GetSnapshot(promptId);
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<title>Self-Tuning Dashboard — " + Esc(promptId) + "</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:'Segoe UI',system-ui,sans-serif;margin:0;padding:20px;background:#0d1117;color:#c9d1d9}");
            sb.AppendLine("h1{color:#58a6ff;margin-bottom:4px}h2{color:#8b949e;font-size:14px;margin-top:0}");
            sb.AppendLine(".cards{display:flex;gap:12px;flex-wrap:wrap;margin:16px 0}");
            sb.AppendLine(".card{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:16px;min-width:140px;flex:1}");
            sb.AppendLine(".card .label{font-size:11px;color:#8b949e;text-transform:uppercase;letter-spacing:0.5px}");
            sb.AppendLine(".card .value{font-size:28px;font-weight:700;margin-top:4px}");
            sb.AppendLine(".phase-badge{display:inline-block;padding:3px 10px;border-radius:12px;font-size:12px;font-weight:600}");
            sb.AppendLine(".phase-Exploration{background:#1f6feb33;color:#58a6ff}");
            sb.AppendLine(".phase-Balancing{background:#d29922aa;color:#f0c000}");
            sb.AppendLine(".phase-Converging{background:#238636aa;color:#3fb950}");
            sb.AppendLine(".phase-Converged{background:#238636;color:#fff}");
            sb.AppendLine(".phase-Drifted{background:#da3633aa;color:#f85149}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin:16px 0}");
            sb.AppendLine("th{text-align:left;padding:8px 12px;background:#161b22;border-bottom:2px solid #30363d;color:#8b949e;font-size:12px;text-transform:uppercase}");
            sb.AppendLine("td{padding:8px 12px;border-bottom:1px solid #21262d}");
            sb.AppendLine("tr:hover{background:#1c2128}");
            sb.AppendLine(".bar{height:8px;border-radius:4px;background:#30363d;position:relative;overflow:hidden}");
            sb.AppendLine(".bar-fill{height:100%;border-radius:4px;transition:width 0.3s}");
            sb.AppendLine(".best{color:#3fb950;font-weight:700}");
            sb.AppendLine(".insight{background:#161b22;border-left:3px solid #58a6ff;padding:10px 14px;margin:6px 0;border-radius:0 6px 6px 0;font-size:13px}");
            sb.AppendLine(".gauge{width:120px;height:120px;margin:0 auto}");
            sb.AppendLine("</style></head><body>");

            // Header
            sb.AppendLine($"<h1>🎯 Self-Tuning: {Esc(promptId)}</h1>");
            sb.AppendLine($"<h2>Autonomous parameter optimization via multi-armed bandit</h2>");

            // Summary cards
            sb.AppendLine("<div class='cards'>");
            AppendCard(sb, "Phase", $"<span class='phase-badge phase-{snapshot.Phase}'>{snapshot.Phase}</span>");
            AppendCard(sb, "Health", $"<span style='color:{HealthColor(snapshot.HealthScore)}'>{snapshot.HealthScore:F0}</span>/100");
            AppendCard(sb, "Trials", snapshot.TotalTrials.ToString());
            AppendCard(sb, "Arms", snapshot.ArmCount.ToString());
            AppendCard(sb, "Best Arm", snapshot.BestArmId != null ? Esc(snapshot.BestArmId) : "—");
            AppendCard(sb, "Best Reward", snapshot.BestMeanReward > 0 ? $"{snapshot.BestMeanReward:F3}" : "—");
            sb.AppendLine("</div>");

            // Arm comparison table
            sb.AppendLine("<h2 style='color:#c9d1d9;font-size:16px;margin-top:24px'>Arm Performance</h2>");
            sb.AppendLine("<table><tr><th>Arm</th><th>Trials</th><th>Mean Reward</th><th>UCB Score</th><th>Success Rate</th><th>Avg Latency</th><th>Reward Distribution</th></tr>");

            foreach (var stat in snapshot.ArmStats.OrderByDescending(s => s.UCBScore))
            {
                bool isBest = stat.ArmId == snapshot.BestArmId;
                string cls = isBest ? " class='best'" : "";
                double barWidth = stat.MeanReward * 100;
                string barColor = isBest ? "#3fb950" : "#58a6ff";

                sb.AppendLine($"<tr><td{cls}>{Esc(stat.ArmId)}{(isBest ? " ★" : "")}</td>");
                sb.AppendLine($"<td>{stat.Trials}</td>");
                sb.AppendLine($"<td{cls}>{stat.MeanReward:F3} ± {stat.StdDevReward:F3}</td>");
                sb.AppendLine($"<td>{stat.UCBScore:F3}</td>");
                sb.AppendLine($"<td>{stat.SuccessRate * 100:F0}%</td>");
                sb.AppendLine($"<td>{stat.MeanLatencyMs:F0}ms</td>");
                sb.AppendLine($"<td><div class='bar'><div class='bar-fill' style='width:{barWidth:F0}%;background:{barColor}'></div></div></td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");

            // Drift section
            if (snapshot.Drift != null)
            {
                sb.AppendLine("<h2 style='color:#c9d1d9;font-size:16px;margin-top:24px'>Drift Detection</h2>");
                string driftColor = snapshot.Drift.DriftDetected ? "#f85149" : "#3fb950";
                string driftStatus = snapshot.Drift.DriftDetected ? "⚠ DRIFT DETECTED" : "✓ Stable";
                sb.AppendLine($"<div class='insight' style='border-left-color:{driftColor}'>");
                sb.AppendLine($"<strong style='color:{driftColor}'>{driftStatus}</strong><br/>");
                sb.AppendLine($"Magnitude: {snapshot.Drift.DriftMagnitude:F3}");
                if (snapshot.Drift.AffectedArms.Count > 0)
                    sb.AppendLine($" | Affected: {string.Join(", ", snapshot.Drift.AffectedArms)}");
                if (!string.IsNullOrEmpty(snapshot.Drift.Description))
                    sb.AppendLine($"<br/>{Esc(snapshot.Drift.Description)}");
                sb.AppendLine("</div>");
            }

            // Insights
            var insights = GenerateSessionInsights(snapshot);
            if (insights.Count > 0)
            {
                sb.AppendLine("<h2 style='color:#c9d1d9;font-size:16px;margin-top:24px'>Autonomous Insights</h2>");
                foreach (var insight in insights)
                    sb.AppendLine($"<div class='insight'>{Esc(insight)}</div>");
            }

            sb.AppendLine($"<p style='color:#484f58;font-size:11px;margin-top:24px'>Generated {DateTime.UtcNow:u} by PromptSelfTuningEngine</p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ── Private Helpers ─────────────────────────────

        private TuningSession GetOrCreateSession(string promptId)
        {
            if (!_sessions.TryGetValue(promptId, out var session))
            {
                session = new TuningSession { PromptId = promptId };
                _sessions[promptId] = session;
            }
            return session;
        }

        private static int CountTrials(TuningSession session, string armId)
            => session.History.Count(o => o.ArmId == armId);

        private static double MeanReward(TuningSession session, string armId)
        {
            var trials = session.History.Where(o => o.ArmId == armId).ToList();
            return trials.Count == 0 ? 0 : trials.Average(o => o.QualityScore);
        }

        private static double StdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;
            double mean = list.Average();
            return Math.Sqrt(list.Sum(v => (v - mean) * (v - mean)) / (list.Count - 1));
        }

        private string? GetBestArmId(TuningSession session)
        {
            string? bestId = null;
            double bestMean = double.MinValue;

            foreach (var armId in session.Arms.Keys)
            {
                int n = CountTrials(session, armId);
                if (n == 0) continue;
                double mean = MeanReward(session, armId);
                if (mean > bestMean)
                {
                    bestMean = mean;
                    bestId = armId;
                }
            }
            return bestId;
        }

        private ArmStatistics ComputeArmStats(TuningSession session, string armId)
        {
            var trials = session.History.Where(o => o.ArmId == armId).ToList();
            int totalTrials = session.History.Count;
            int n = trials.Count;
            double mean = n > 0 ? trials.Average(o => o.QualityScore) : 0;
            double stdDev = StdDev(trials.Select(o => o.QualityScore));
            double ucb = n > 0 && totalTrials > 0
                ? mean + _config.ExplorationConstant * Math.Sqrt(Math.Log(totalTrials) / n)
                : double.MaxValue;

            return new ArmStatistics
            {
                ArmId = armId,
                Trials = n,
                MeanReward = mean,
                StdDevReward = stdDev,
                UCBScore = n > 0 ? ucb : 0,
                MeanLatencyMs = n > 0 ? trials.Average(o => o.LatencyMs) : 0,
                MeanTokens = n > 0 ? trials.Average(o => o.TokensUsed) : 0,
                SuccessRate = n > 0 ? trials.Count(o => o.Success) / (double)n : 0,
                BestScore = n > 0 ? trials.Max(o => o.QualityScore) : 0,
                WorstScore = n > 0 ? trials.Min(o => o.QualityScore) : 0
            };
        }

        private void UpdatePhase(TuningSession session)
        {
            if (session.Arms.Count == 0) return;

            int totalTrials = session.History.Count;
            bool allSampled = session.Arms.Keys.All(id => CountTrials(session, id) >= _config.MinTrialsPerArm);

            if (!allSampled)
            {
                session.Phase = TuningPhase.Exploration;
                return;
            }

            if (session.LastDrift?.DriftDetected == true)
            {
                session.Phase = TuningPhase.Drifted;
                return;
            }

            if (totalTrials < _config.MinTrialsForConvergence)
            {
                session.Phase = TuningPhase.Balancing;
                return;
            }

            // Check for convergence: best arm has sufficient lead over second-best
            var ranked = session.Arms.Keys
                .Select(id => (id, mean: MeanReward(session, id)))
                .OrderByDescending(x => x.mean)
                .ToList();

            if (ranked.Count >= 2)
            {
                double gap = ranked[0].mean - ranked[1].mean;
                if (gap >= _config.ConvergenceThreshold)
                {
                    session.Phase = TuningPhase.Converged;
                    session.ConvergedArmId = ranked[0].id;
                    return;
                }
            }
            else if (ranked.Count == 1)
            {
                session.Phase = TuningPhase.Converged;
                session.ConvergedArmId = ranked[0].id;
                return;
            }

            // Check if we're close to converging
            if (ranked.Count >= 2 && ranked[0].mean - ranked[1].mean >= _config.ConvergenceThreshold * 0.5)
            {
                session.Phase = TuningPhase.Converging;
            }
            else
            {
                session.Phase = TuningPhase.Balancing;
            }
        }

        private void DetectDrift(TuningSession session, string armId)
        {
            var armTrials = session.History.Where(o => o.ArmId == armId).ToList();
            if (armTrials.Count < _config.DriftWindowSize * 2) return;

            var recent = armTrials.Skip(armTrials.Count - _config.DriftWindowSize).ToList();
            var previous = armTrials.Skip(armTrials.Count - _config.DriftWindowSize * 2)
                .Take(_config.DriftWindowSize).ToList();

            double recentMean = recent.Average(o => o.QualityScore);
            double previousMean = previous.Average(o => o.QualityScore);
            double drop = previousMean - recentMean;

            if (drop >= _config.DriftThreshold)
            {
                var existingArms = session.LastDrift?.AffectedArms ?? new List<string>();
                if (!existingArms.Contains(armId))
                    existingArms = existingArms.Append(armId).ToList();

                session.LastDrift = new TuningDriftReport
                {
                    DriftDetected = true,
                    DriftMagnitude = drop,
                    AffectedArms = existingArms,
                    Description = $"Mean reward for '{armId}' dropped from {previousMean:F3} to {recentMean:F3} (Δ={drop:F3}) in the last {_config.DriftWindowSize} trials",
                    DetectedAt = DateTime.UtcNow
                };
            }
            else if (session.LastDrift?.DriftDetected == true && session.LastDrift.AffectedArms.Contains(armId))
            {
                // Drift may have recovered for this arm
                var remaining = session.LastDrift.AffectedArms.Where(a => a != armId).ToList();
                if (remaining.Count == 0)
                {
                    session.LastDrift = new TuningDriftReport
                    {
                        DriftDetected = false,
                        DriftMagnitude = 0,
                        AffectedArms = new List<string>(),
                        Description = "Drift recovered — all arms stable"
                    };
                }
            }
        }

        private double ComputeHealthScore(TuningSession session)
        {
            if (session.Arms.Count == 0 || session.History.Count == 0) return 0;

            double score = 0;

            // Data sufficiency (0–25 points)
            int totalTrials = session.History.Count;
            int minNeeded = session.Arms.Count * _config.MinTrialsPerArm;
            double dataSuff = Math.Min(1.0, (double)totalTrials / Math.Max(1, minNeeded * 3));
            score += dataSuff * 25;

            // Best arm quality (0–30 points)
            var bestId = GetBestArmId(session);
            if (bestId != null)
                score += MeanReward(session, bestId) * 30;

            // Convergence progress (0–25 points)
            switch (session.Phase)
            {
                case TuningPhase.Converged: score += 25; break;
                case TuningPhase.Converging: score += 18; break;
                case TuningPhase.Balancing: score += 10; break;
                case TuningPhase.Exploration: score += 5; break;
                case TuningPhase.Drifted: score += 2; break;
            }

            // Stability / no drift (0–20 points)
            if (session.LastDrift?.DriftDetected == true)
                score += Math.Max(0, 20 - session.LastDrift.DriftMagnitude * 100);
            else
                score += 20;

            return Math.Round(Math.Max(0, Math.Min(100, score)), 1);
        }

        private static TuningHealthTier ClassifyHealth(double score) => score switch
        {
            >= 90 => TuningHealthTier.Optimal,
            >= 70 => TuningHealthTier.Good,
            >= 50 => TuningHealthTier.Exploring,
            >= 30 => TuningHealthTier.Struggling,
            _ => TuningHealthTier.Insufficient
        };

        private List<string> GenerateSessionInsights(TuningSnapshot snapshot)
        {
            var insights = new List<string>();

            if (snapshot.Phase == TuningPhase.Converged && snapshot.BestArmId != null)
            {
                var bestStats = snapshot.ArmStats.FirstOrDefault(s => s.ArmId == snapshot.BestArmId);
                if (bestStats != null)
                    insights.Add($"🏆 Converged on '{snapshot.BestArmId}' with {bestStats.MeanReward:F3} mean reward over {bestStats.Trials} trials. Consider locking this configuration.");
            }

            if (snapshot.Phase == TuningPhase.Drifted)
                insights.Add("⚠️ Environment drift detected — the engine is re-exploring to adapt. Recent model updates or prompt changes may have shifted the optimal parameters.");

            // Identify wasted arms
            var lowPerformers = snapshot.ArmStats
                .Where(s => s.Trials >= 5 && s.MeanReward < 0.3)
                .ToList();
            if (lowPerformers.Count > 0)
                insights.Add($"🗑️ {lowPerformers.Count} arm(s) consistently underperforming (<0.3 mean): {string.Join(", ", lowPerformers.Select(s => s.ArmId))}. Consider removing them to speed convergence.");

            // High-variance arm
            var highVar = snapshot.ArmStats
                .Where(s => s.Trials >= 5 && s.StdDevReward > 0.2)
                .OrderByDescending(s => s.StdDevReward)
                .FirstOrDefault();
            if (highVar != null)
                insights.Add($"📊 Arm '{highVar.ArmId}' shows high variance (σ={highVar.StdDevReward:F3}). Results are inconsistent — may indicate sensitivity to prompt content or external factors.");

            // Latency vs quality trade-off
            var bestByQuality = snapshot.ArmStats.MaxBy(s => s.MeanReward);
            var bestByLatency = snapshot.ArmStats.Where(s => s.Trials > 0).MinBy(s => s.MeanLatencyMs);
            if (bestByQuality != null && bestByLatency != null && bestByQuality.ArmId != bestByLatency.ArmId)
            {
                double qualityGap = bestByQuality.MeanReward - (snapshot.ArmStats.FirstOrDefault(s => s.ArmId == bestByLatency.ArmId)?.MeanReward ?? 0);
                double latencyGap = bestByQuality.MeanLatencyMs - bestByLatency.MeanLatencyMs;
                if (qualityGap > 0.05 && latencyGap > 100)
                    insights.Add($"⚡ Quality-speed trade-off: '{bestByQuality.ArmId}' is highest quality but {latencyGap:F0}ms slower than '{bestByLatency.ArmId}'. Consider which matters more for your use case.");
            }

            if (snapshot.TotalTrials > 0 && snapshot.TotalTrials < snapshot.ArmCount * _config.MinTrialsPerArm)
                insights.Add($"🔬 Still in exploration phase — need {snapshot.ArmCount * _config.MinTrialsPerArm - snapshot.TotalTrials} more trials before the bandit can start optimizing.");

            return insights;
        }

        private List<string> GenerateFleetInsights(List<TuningSnapshot> snapshots)
        {
            var insights = new List<string>();

            int converged = snapshots.Count(s => s.Phase == TuningPhase.Converged);
            int total = snapshots.Count;
            if (total > 0)
                insights.Add($"📈 {converged}/{total} prompts have converged on optimal parameters ({(double)converged / total * 100:F0}%).");

            int drifting = snapshots.Count(s => s.Drift?.DriftDetected == true);
            if (drifting > 0)
                insights.Add($"⚠️ {drifting} prompt(s) experiencing parameter drift — automatic re-tuning in progress.");

            // Find most popular winning arm across converged sessions
            var winners = snapshots
                .Where(s => s.Phase == TuningPhase.Converged && s.BestArmId != null)
                .GroupBy(s => s.BestArmId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (winners != null && converged > 1)
                insights.Add($"🏅 Most common optimal configuration: '{winners.Key}' (winner in {winners.Count()}/{converged} converged sessions). This may be a good fleet-wide default.");

            double avgHealth = snapshots.Count > 0 ? snapshots.Average(s => s.HealthScore) : 0;
            if (avgHealth < 50)
                insights.Add("⚠️ Fleet health is below 50 — many sessions need more trials or are experiencing issues. Consider increasing trial frequency.");

            return insights;
        }

        // ── Dashboard Helpers ───────────────────────────

        private static void AppendCard(StringBuilder sb, string label, string value)
        {
            sb.AppendLine($"<div class='card'><div class='label'>{label}</div><div class='value'>{value}</div></div>");
        }

        private static string HealthColor(double score) => score switch
        {
            >= 90 => "#3fb950",
            >= 70 => "#58a6ff",
            >= 50 => "#d29922",
            >= 30 => "#db6d28",
            _ => "#f85149"
        };

        private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);
    }
}
