namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ────────────────────────────────────────────
    //  PromptMetabolismEngine – Autonomous Token Efficiency Tracker
    //
    //  Monitors prompt token consumption patterns over time like a
    //  metabolic system.  Identifies metabolic states (fast-burn,
    //  slow-burn, balanced), computes efficiency ratios (quality per
    //  token spent), detects metabolic disorders (cost spikes,
    //  diminishing returns, token bloat), and generates autonomous
    //  dietary recommendations to maintain optimal token health.
    //
    //  Unlike PromptTokenOptimizer (one-time compression) or
    //  PromptCostEstimator (static pricing), this engine is a
    //  continuous health monitor that learns consumption patterns
    //  and autonomously recommends interventions.
    // ────────────────────────────────────────────

    /// <summary>Metabolic state classification for token consumption.</summary>
    public enum MetabolicState
    {
        /// <summary>Very low token usage, possibly under-prompting.</summary>
        Starving,
        /// <summary>Conservative token usage with good efficiency.</summary>
        SlowBurn,
        /// <summary>Optimal token-to-quality ratio.</summary>
        Balanced,
        /// <summary>High token usage with acceptable quality gains.</summary>
        FastBurn,
        /// <summary>Excessive token usage with diminishing returns.</summary>
        Gorging,
        /// <summary>Erratic consumption with no stable pattern.</summary>
        Erratic
    }

    /// <summary>Types of metabolic disorders detected in prompt usage.</summary>
    public enum MetabolicDisorder
    {
        /// <summary>Sudden unexplained spike in token consumption.</summary>
        CostSpike,
        /// <summary>Token count growing but quality not improving.</summary>
        DiminishingReturns,
        /// <summary>Gradual creep in prompt size without awareness.</summary>
        TokenBloat,
        /// <summary>Quality dropping despite stable or rising token use.</summary>
        NutrientDeficiency,
        /// <summary>Oscillating wildly between high and low consumption.</summary>
        BingeStarveCycle,
        /// <summary>Using expensive models for simple tasks.</summary>
        Overfeeding,
        /// <summary>Context window nearly exhausted regularly.</summary>
        CapacityExhaustion,
        /// <summary>Repeated identical tokens wasting budget.</summary>
        RedundancyWaste
    }

    /// <summary>Severity of a metabolic disorder finding.</summary>
    public enum DisorderSeverity
    {
        /// <summary>Minor inefficiency, worth noting.</summary>
        Mild,
        /// <summary>Noticeable waste, should be addressed.</summary>
        Moderate,
        /// <summary>Significant budget impact.</summary>
        Severe,
        /// <summary>Critical — immediate action needed.</summary>
        Critical
    }

    /// <summary>Type of dietary recommendation.</summary>
    public enum DietaryAction
    {
        /// <summary>Reduce token count in specific areas.</summary>
        Trim,
        /// <summary>Switch to a cheaper model for certain tasks.</summary>
        Downgrade,
        /// <summary>Consolidate redundant prompt components.</summary>
        Consolidate,
        /// <summary>Add caching to reduce repeated computation.</summary>
        Cache,
        /// <summary>Split large prompts into targeted smaller ones.</summary>
        Decompose,
        /// <summary>Increase tokens where quality is suffering.</summary>
        Supplement,
        /// <summary>Schedule expensive prompts during off-peak.</summary>
        Schedule,
        /// <summary>Set hard budget limits to prevent overruns.</summary>
        Cap
    }

    /// <summary>A single token consumption measurement at a point in time.</summary>
    public class ConsumptionSample
    {
        /// <summary>When this sample was recorded.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Prompt template or identifier that was invoked.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>Input tokens consumed.</summary>
        public int InputTokens { get; set; }

        /// <summary>Output tokens generated.</summary>
        public int OutputTokens { get; set; }

        /// <summary>Total tokens (input + output).</summary>
        public int TotalTokens => InputTokens + OutputTokens;

        /// <summary>Model used for this invocation.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Estimated cost in USD (optional, computed if pricing known).</summary>
        public double? CostUsd { get; set; }

        /// <summary>Quality score of the response (0-100, user-provided or auto-assessed).</summary>
        public double? QualityScore { get; set; }

        /// <summary>Latency in milliseconds.</summary>
        public double? LatencyMs { get; set; }

        /// <summary>Whether the response was successful.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Optional tags for categorization.</summary>
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>Efficiency ratio for a prompt or portfolio.</summary>
    public class EfficiencyRatio
    {
        /// <summary>Quality points per 1000 tokens spent.</summary>
        public double QualityPerKToken { get; set; }

        /// <summary>Quality points per $0.01 spent.</summary>
        public double QualityPerCent { get; set; }

        /// <summary>Success rate as a fraction.</summary>
        public double SuccessRate { get; set; }

        /// <summary>Average tokens per successful outcome.</summary>
        public double TokensPerSuccess { get; set; }

        /// <summary>Efficiency grade (A+ to F).</summary>
        public string Grade { get; set; } = "C";

        /// <summary>Percentile rank compared to portfolio (0-100).</summary>
        public double Percentile { get; set; }
    }

    /// <summary>A detected metabolic disorder with evidence.</summary>
    public class DisorderDiagnosis
    {
        /// <summary>Type of disorder detected.</summary>
        public MetabolicDisorder Disorder { get; set; }

        /// <summary>How severe the disorder is.</summary>
        public DisorderSeverity Severity { get; set; }

        /// <summary>Confidence in the diagnosis (0-1).</summary>
        public double Confidence { get; set; }

        /// <summary>Which prompt(s) are affected.</summary>
        public List<string> AffectedPrompts { get; set; } = new();

        /// <summary>Evidence supporting the diagnosis.</summary>
        public List<string> Evidence { get; set; } = new();

        /// <summary>Estimated monthly cost impact in USD.</summary>
        public double EstimatedMonthlyCostImpact { get; set; }

        /// <summary>When the disorder was first detected.</summary>
        public DateTime FirstDetected { get; set; }

        /// <summary>Whether this is worsening over time.</summary>
        public bool IsProgressing { get; set; }
    }

    /// <summary>A dietary recommendation to improve token health.</summary>
    public class DietaryRecommendation
    {
        /// <summary>What action to take.</summary>
        public DietaryAction Action { get; set; }

        /// <summary>Which prompt(s) this applies to.</summary>
        public List<string> TargetPrompts { get; set; } = new();

        /// <summary>Human-readable description of the recommendation.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Estimated monthly savings in USD.</summary>
        public double EstimatedMonthlySavings { get; set; }

        /// <summary>Estimated token reduction percentage.</summary>
        public double TokenReductionPercent { get; set; }

        /// <summary>Risk of quality degradation (0-1).</summary>
        public double QualityRisk { get; set; }

        /// <summary>Priority rank (1 = highest priority).</summary>
        public int Priority { get; set; }

        /// <summary>Concrete implementation steps.</summary>
        public List<string> Steps { get; set; } = new();
    }

    /// <summary>Metabolic profile for a single prompt.</summary>
    public class PromptMetabolicProfile
    {
        /// <summary>Prompt identifier.</summary>
        public string PromptId { get; set; } = string.Empty;

        /// <summary>Current metabolic state.</summary>
        public MetabolicState State { get; set; }

        /// <summary>Efficiency metrics.</summary>
        public EfficiencyRatio Efficiency { get; set; } = new();

        /// <summary>Average tokens per call.</summary>
        public double AvgTokensPerCall { get; set; }

        /// <summary>Token consumption trend (positive = growing).</summary>
        public double ConsumptionTrend { get; set; }

        /// <summary>Average cost per call in USD.</summary>
        public double AvgCostPerCall { get; set; }

        /// <summary>Total samples analyzed.</summary>
        public int SampleCount { get; set; }

        /// <summary>Estimated monthly spend at current rate.</summary>
        public double ProjectedMonthlySpend { get; set; }

        /// <summary>Detected disorders for this prompt.</summary>
        public List<DisorderDiagnosis> Disorders { get; set; } = new();
    }

    /// <summary>Full metabolic health report for a prompt portfolio.</summary>
    public class MetabolicHealthReport
    {
        /// <summary>When this report was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Overall metabolic state of the portfolio.</summary>
        public MetabolicState OverallState { get; set; }

        /// <summary>Overall health score (0-100).</summary>
        public double HealthScore { get; set; }

        /// <summary>Total tokens consumed in the analysis window.</summary>
        public long TotalTokensConsumed { get; set; }

        /// <summary>Total estimated cost in the analysis window.</summary>
        public double TotalCostUsd { get; set; }

        /// <summary>Portfolio-wide efficiency ratio.</summary>
        public EfficiencyRatio PortfolioEfficiency { get; set; } = new();

        /// <summary>Per-prompt metabolic profiles.</summary>
        public List<PromptMetabolicProfile> Profiles { get; set; } = new();

        /// <summary>All detected disorders.</summary>
        public List<DisorderDiagnosis> Disorders { get; set; } = new();

        /// <summary>Prioritized dietary recommendations.</summary>
        public List<DietaryRecommendation> Recommendations { get; set; } = new();

        /// <summary>Estimated total monthly savings if all recommendations followed.</summary>
        public double PotentialMonthlySavings { get; set; }

        /// <summary>Summary narrative.</summary>
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Autonomous token metabolism monitor — tracks consumption patterns,
    /// diagnoses inefficiencies, and prescribes optimizations.
    /// </summary>
    public class PromptMetabolismEngine
    {
        private readonly List<ConsumptionSample> _samples = new();
        private readonly Dictionary<string, double> _modelPricingInput = new();
        private readonly Dictionary<string, double> _modelPricingOutput = new();
        private readonly Dictionary<string, int> _modelContextLimits = new();

        /// <summary>Creates a new metabolism engine with default model pricing.</summary>
        public PromptMetabolismEngine()
        {
            InitializeDefaultPricing();
        }

        private void InitializeDefaultPricing()
        {
            // Per-1K-token pricing (USD)
            _modelPricingInput["gpt-4o"] = 0.005;
            _modelPricingOutput["gpt-4o"] = 0.015;
            _modelPricingInput["gpt-4o-mini"] = 0.00015;
            _modelPricingOutput["gpt-4o-mini"] = 0.0006;
            _modelPricingInput["gpt-4"] = 0.03;
            _modelPricingOutput["gpt-4"] = 0.06;
            _modelPricingInput["gpt-3.5-turbo"] = 0.0005;
            _modelPricingOutput["gpt-3.5-turbo"] = 0.0015;
            _modelPricingInput["claude-3-opus"] = 0.015;
            _modelPricingOutput["claude-3-opus"] = 0.075;
            _modelPricingInput["claude-3-sonnet"] = 0.003;
            _modelPricingOutput["claude-3-sonnet"] = 0.015;
            _modelPricingInput["claude-3-haiku"] = 0.00025;
            _modelPricingOutput["claude-3-haiku"] = 0.00125;

            _modelContextLimits["gpt-4o"] = 128000;
            _modelContextLimits["gpt-4o-mini"] = 128000;
            _modelContextLimits["gpt-4"] = 8192;
            _modelContextLimits["gpt-3.5-turbo"] = 16385;
            _modelContextLimits["claude-3-opus"] = 200000;
            _modelContextLimits["claude-3-sonnet"] = 200000;
            _modelContextLimits["claude-3-haiku"] = 200000;
        }

        /// <summary>Configure custom pricing for a model (per 1K tokens).</summary>
        public void SetModelPricing(string model, double inputPer1K, double outputPer1K, int? contextLimit = null)
        {
            _modelPricingInput[model] = inputPer1K;
            _modelPricingOutput[model] = outputPer1K;
            if (contextLimit.HasValue)
                _modelContextLimits[model] = contextLimit.Value;
        }

        /// <summary>Record a consumption sample.</summary>
        public void RecordSample(ConsumptionSample sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));
            if (sample.CostUsd == null && !string.IsNullOrEmpty(sample.Model))
            {
                sample.CostUsd = EstimateCost(sample.Model, sample.InputTokens, sample.OutputTokens);
            }
            _samples.Add(sample);
        }

        /// <summary>Record multiple samples at once.</summary>
        public void RecordSamples(IEnumerable<ConsumptionSample> samples)
        {
            foreach (var s in samples) RecordSample(s);
        }

        /// <summary>Get total recorded sample count.</summary>
        public int SampleCount => _samples.Count;

        /// <summary>Estimate cost for a model invocation.</summary>
        public double EstimateCost(string model, int inputTokens, int outputTokens)
        {
            double inputCost = _modelPricingInput.TryGetValue(model, out var ip) ? ip : 0.005;
            double outputCost = _modelPricingOutput.TryGetValue(model, out var op) ? op : 0.015;
            return (inputTokens / 1000.0 * inputCost) + (outputTokens / 1000.0 * outputCost);
        }

        /// <summary>Generate a full metabolic health report for the recorded samples.</summary>
        public MetabolicHealthReport Analyze(TimeSpan? window = null)
        {
            var cutoff = window.HasValue ? DateTime.UtcNow - window.Value : DateTime.MinValue;
            var relevantSamples = _samples.Where(s => s.Timestamp >= cutoff).ToList();

            if (relevantSamples.Count == 0)
            {
                return new MetabolicHealthReport
                {
                    OverallState = MetabolicState.Starving,
                    HealthScore = 0,
                    Summary = "No consumption data available for analysis."
                };
            }

            var report = new MetabolicHealthReport
            {
                TotalTokensConsumed = relevantSamples.Sum(s => (long)s.TotalTokens),
                TotalCostUsd = relevantSamples.Sum(s => s.CostUsd ?? 0)
            };

            // Pre-group samples by PromptId once — eliminates redundant O(N)
            // GroupBy calls in BuildProfile and each Detect* method.
            var groupedByPrompt = relevantSamples
                .GroupBy(s => s.PromptId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Pre-compute per-group base scores for percentile calculation,
            // avoiding O(N×G) re-grouping inside each ComputeEfficiency call.
            var precomputedScores = new List<double>(groupedByPrompt.Count);
            foreach (var g in groupedByPrompt.Values)
            {
                var a = g.Average(s => (double)s.TotalTokens);
                var sr = g.Count(s => s.Success) / (double)g.Count;
                precomputedScores.Add(sr * 60 + (1 - Math.Min(a / 10000.0, 1)) * 40);
            }
            precomputedScores.Sort();

            // Per-prompt profiles
            foreach (var (promptId, samples) in groupedByPrompt)
            {
                var profile = BuildProfile(promptId, samples, precomputedScores);
                report.Profiles.Add(profile);
            }

            // Portfolio efficiency
            report.PortfolioEfficiency = ComputePortfolioEfficiency(relevantSamples, precomputedScores);

            // Classify overall state
            report.OverallState = ClassifyPortfolioState(report);

            // Detect disorders — pass pre-grouped dictionary to avoid re-scanning
            report.Disorders = DetectDisorders(groupedByPrompt, report.Profiles);

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(report.Disorders, report.Profiles);
            report.PotentialMonthlySavings = report.Recommendations.Sum(r => r.EstimatedMonthlySavings);

            // Health score
            report.HealthScore = ComputeHealthScore(report);

            // Summary
            report.Summary = GenerateSummary(report);

            return report;
        }

        /// <summary>Get metabolic state for a single prompt.</summary>
        public MetabolicState GetPromptState(string promptId)
        {
            var samples = _samples.Where(s => s.PromptId == promptId).ToList();
            if (samples.Count < 3) return MetabolicState.Starving;
            return ClassifyState(samples);
        }

        /// <summary>Export the full report as JSON.</summary>
        public string ExportJson(MetabolicHealthReport report)
        {
            return JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        /// <summary>Generate an interactive HTML dashboard for the report.</summary>
        public string ExportHtml(MetabolicHealthReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.AppendLine("<title>Prompt Metabolism Dashboard</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#0f0f23;color:#e0e0e0;padding:2rem}");
            sb.AppendLine("h1{color:#00d4aa;margin-bottom:1rem;font-size:1.8rem}");
            sb.AppendLine("h2{color:#7ec8e3;margin:1.5rem 0 0.8rem;font-size:1.3rem}");
            sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:1rem;margin:1rem 0}");
            sb.AppendLine(".card{background:#1a1a2e;border:1px solid #333;border-radius:8px;padding:1.2rem}");
            sb.AppendLine(".metric{font-size:2rem;font-weight:700;color:#00d4aa}");
            sb.AppendLine(".label{font-size:0.85rem;color:#888;margin-top:0.3rem}");
            sb.AppendLine(".state{display:inline-block;padding:0.3rem 0.8rem;border-radius:4px;font-weight:600;font-size:0.85rem}");
            sb.AppendLine(".state-Balanced{background:#1b4332;color:#52b788}");
            sb.AppendLine(".state-SlowBurn{background:#1b3a4b;color:#48bfe3}");
            sb.AppendLine(".state-FastBurn{background:#3d2000;color:#f4a261}");
            sb.AppendLine(".state-Gorging{background:#3d0000;color:#ef476f}");
            sb.AppendLine(".state-Starving{background:#2d2d2d;color:#aaa}");
            sb.AppendLine(".state-Erratic{background:#3d003d;color:#c77dff}");
            sb.AppendLine(".disorder{background:#1a1a2e;border-left:3px solid #ef476f;padding:0.8rem;margin:0.5rem 0;border-radius:0 4px 4px 0}");
            sb.AppendLine(".disorder-Mild{border-left-color:#48bfe3}");
            sb.AppendLine(".disorder-Moderate{border-left-color:#f4a261}");
            sb.AppendLine(".disorder-Severe{border-left-color:#ef476f}");
            sb.AppendLine(".disorder-Critical{border-left-color:#d00000}");
            sb.AppendLine(".rec{background:#0d1b2a;border:1px solid #1b4332;padding:0.8rem;margin:0.5rem 0;border-radius:4px}");
            sb.AppendLine(".rec-savings{color:#52b788;font-weight:600}");
            sb.AppendLine(".bar{height:8px;background:#333;border-radius:4px;margin:0.5rem 0}");
            sb.AppendLine(".bar-fill{height:100%;border-radius:4px;transition:width 0.5s}");
            sb.AppendLine(".grade{font-size:1.5rem;font-weight:700}");
            sb.AppendLine(".grade-A{color:#52b788}.grade-B{color:#48bfe3}.grade-C{color:#f4a261}.grade-D{color:#ef476f}.grade-F{color:#d00000}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin:0.5rem 0}");
            sb.AppendLine("th,td{padding:0.5rem;text-align:left;border-bottom:1px solid #333}");
            sb.AppendLine("th{color:#7ec8e3;font-size:0.85rem}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine($"<h1>🧬 Prompt Metabolism Dashboard</h1>");
            sb.AppendLine($"<p style='color:#888'>Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC | Samples analyzed: {report.Profiles.Sum(p => p.SampleCount)}</p>");

            // Overview cards
            sb.AppendLine("<div class='grid'>");
            sb.AppendLine($"<div class='card'><div class='metric'>{report.HealthScore:F0}</div><div class='label'>Health Score (0-100)</div></div>");
            sb.AppendLine($"<div class='card'><div class='state state-{report.OverallState}'>{report.OverallState}</div><div class='label' style='margin-top:0.8rem'>Metabolic State</div></div>");
            sb.AppendLine($"<div class='card'><div class='metric'>{report.TotalTokensConsumed:N0}</div><div class='label'>Total Tokens Consumed</div></div>");
            sb.AppendLine($"<div class='card'><div class='metric'>${report.TotalCostUsd:F4}</div><div class='label'>Total Cost (USD)</div></div>");
            var gradeClass = report.PortfolioEfficiency.Grade.StartsWith("A") ? "A" : report.PortfolioEfficiency.Grade.StartsWith("B") ? "B" : report.PortfolioEfficiency.Grade.StartsWith("C") ? "C" : report.PortfolioEfficiency.Grade.StartsWith("D") ? "D" : "F";
            sb.AppendLine($"<div class='card'><div class='grade grade-{gradeClass}'>{report.PortfolioEfficiency.Grade}</div><div class='label'>Efficiency Grade</div></div>");
            sb.AppendLine($"<div class='card'><div class='metric rec-savings'>${report.PotentialMonthlySavings:F2}/mo</div><div class='label'>Potential Savings</div></div>");
            sb.AppendLine("</div>");

            // Prompt profiles table
            if (report.Profiles.Count > 0)
            {
                sb.AppendLine("<h2>📊 Prompt Metabolic Profiles</h2>");
                sb.AppendLine("<table><tr><th>Prompt</th><th>State</th><th>Avg Tokens</th><th>Efficiency</th><th>Trend</th><th>Monthly $</th></tr>");
                foreach (var p in report.Profiles.OrderByDescending(x => x.ProjectedMonthlySpend))
                {
                    var trendIcon = p.ConsumptionTrend > 0.05 ? "📈" : p.ConsumptionTrend < -0.05 ? "📉" : "➡️";
                    sb.AppendLine($"<tr><td>{Escape(p.PromptId)}</td><td><span class='state state-{p.State}'>{p.State}</span></td><td>{p.AvgTokensPerCall:F0}</td><td>{p.Efficiency.Grade}</td><td>{trendIcon} {p.ConsumptionTrend:+0.0%;-0.0%;0%}</td><td>${p.ProjectedMonthlySpend:F2}</td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // Disorders
            if (report.Disorders.Count > 0)
            {
                sb.AppendLine("<h2>🩺 Metabolic Disorders Detected</h2>");
                foreach (var d in report.Disorders.OrderByDescending(x => x.Severity))
                {
                    sb.AppendLine($"<div class='disorder disorder-{d.Severity}'>");
                    sb.AppendLine($"<strong>{d.Disorder}</strong> <span style='color:#888'>({d.Severity} | Confidence: {d.Confidence:P0})</span>");
                    sb.AppendLine($"<br/><span style='font-size:0.9rem'>Affected: {string.Join(", ", d.AffectedPrompts.Take(5))}</span>");
                    sb.AppendLine($"<br/><span style='font-size:0.85rem;color:#888'>Impact: ~${d.EstimatedMonthlyCostImpact:F2}/month</span>");
                    if (d.Evidence.Count > 0)
                        sb.AppendLine($"<br/><span style='font-size:0.85rem'>Evidence: {Escape(d.Evidence.First())}</span>");
                    sb.AppendLine("</div>");
                }
            }

            // Recommendations
            if (report.Recommendations.Count > 0)
            {
                sb.AppendLine("<h2>💊 Dietary Recommendations</h2>");
                foreach (var r in report.Recommendations.OrderBy(x => x.Priority))
                {
                    sb.AppendLine($"<div class='rec'>");
                    sb.AppendLine($"<strong>#{r.Priority} {r.Action}</strong> — {Escape(r.Description)}");
                    sb.AppendLine($"<br/><span class='rec-savings'>Save ~${r.EstimatedMonthlySavings:F2}/mo</span>");
                    sb.AppendLine($" | Token reduction: {r.TokenReductionPercent:F0}% | Quality risk: {r.QualityRisk:P0}");
                    if (r.Steps.Count > 0)
                    {
                        sb.AppendLine("<ul style='margin-top:0.5rem;padding-left:1.2rem;font-size:0.85rem'>");
                        foreach (var step in r.Steps.Take(4))
                            sb.AppendLine($"<li>{Escape(step)}</li>");
                        sb.AppendLine("</ul>");
                    }
                    sb.AppendLine("</div>");
                }
            }

            sb.AppendLine("<p style='margin-top:2rem;color:#555;font-size:0.8rem'>Generated by PromptMetabolismEngine — Autonomous Token Health Monitor</p>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ── Private Implementation ──────────────────────────────

        private PromptMetabolicProfile BuildProfile(string promptId, List<ConsumptionSample> samples, List<double> precomputedScores)
        {
            var profile = new PromptMetabolicProfile
            {
                PromptId = promptId,
                SampleCount = samples.Count,
                AvgTokensPerCall = samples.Average(s => s.TotalTokens),
                AvgCostPerCall = samples.Average(s => s.CostUsd ?? 0),
                State = ClassifyState(samples),
                Efficiency = ComputeEfficiency(samples, precomputedScores),
                ConsumptionTrend = ComputeTrend(samples)
            };

            // Project monthly spend based on current rate
            if (samples.Count >= 2)
            {
                var span = (samples.Max(s => s.Timestamp) - samples.Min(s => s.Timestamp)).TotalDays;
                if (span > 0)
                {
                    var dailyRate = samples.Count / span;
                    profile.ProjectedMonthlySpend = dailyRate * 30 * profile.AvgCostPerCall;
                }
            }

            return profile;
        }

        private MetabolicState ClassifyState(List<ConsumptionSample> samples)
        {
            if (samples.Count < 3) return MetabolicState.Starving;

            var avgTokens = samples.Average(s => s.TotalTokens);
            var stdDev = Math.Sqrt(samples.Average(s => Math.Pow(s.TotalTokens - avgTokens, 2)));
            var cv = avgTokens > 0 ? stdDev / avgTokens : 0;

            // High coefficient of variation = erratic
            if (cv > 0.8) return MetabolicState.Erratic;

            // Quality-based classification if available
            var qualitySamples = samples.Where(s => s.QualityScore.HasValue).ToList();
            if (qualitySamples.Count >= 3)
            {
                var avgQuality = qualitySamples.Average(s => s.QualityScore!.Value);
                var qpt = avgQuality / (avgTokens / 1000.0); // quality per K tokens

                if (avgTokens < 500 && avgQuality < 40) return MetabolicState.Starving;
                if (qpt > 30) return MetabolicState.SlowBurn; // High efficiency
                if (qpt > 15) return MetabolicState.Balanced;
                if (qpt > 5) return MetabolicState.FastBurn;
                return MetabolicState.Gorging;
            }

            // Token-only classification
            if (avgTokens < 200) return MetabolicState.Starving;
            if (avgTokens < 1000) return MetabolicState.SlowBurn;
            if (avgTokens < 4000) return MetabolicState.Balanced;
            if (avgTokens < 10000) return MetabolicState.FastBurn;
            return MetabolicState.Gorging;
        }

        /// <summary>
        /// Computes efficiency ratio using pre-sorted portfolio scores for O(log G)
        /// percentile calculation via binary search, replacing the prior O(N) re-grouping.
        /// </summary>
        private EfficiencyRatio ComputeEfficiency(List<ConsumptionSample> samples, List<double> sortedPortfolioScores)
        {
            var ratio = new EfficiencyRatio();
            var avgTokens = samples.Average(s => (double)s.TotalTokens);
            var avgCost = samples.Average(s => s.CostUsd ?? 0);
            var successRate = samples.Count > 0 ? samples.Count(s => s.Success) / (double)samples.Count : 0;

            ratio.SuccessRate = successRate;
            ratio.TokensPerSuccess = successRate > 0 ? avgTokens / successRate : avgTokens;

            var qualitySamples = samples.Where(s => s.QualityScore.HasValue).ToList();
            if (qualitySamples.Count > 0)
            {
                var avgQuality = qualitySamples.Average(s => s.QualityScore!.Value);
                ratio.QualityPerKToken = avgTokens > 0 ? avgQuality / (avgTokens / 1000.0) : 0;
                ratio.QualityPerCent = avgCost > 0 ? avgQuality / (avgCost * 100) : 0;
            }

            // Grade based on composite efficiency
            var score = (ratio.QualityPerKToken * 0.4) + (ratio.SuccessRate * 60 * 0.4) + ((1 - Math.Min(avgTokens / 10000.0, 1)) * 40 * 0.2);
            ratio.Grade = score >= 45 ? "A+" : score >= 40 ? "A" : score >= 35 ? "B+" : score >= 30 ? "B" : score >= 25 ? "C+" : score >= 20 ? "C" : score >= 15 ? "D" : "F";

            // Percentile within portfolio — O(log G) binary search on pre-sorted scores
            if (sortedPortfolioScores.Count > 0)
            {
                var thisScore = successRate * 60 + (1 - Math.Min(avgTokens / 10000.0, 1)) * 40;
                // BinarySearch returns index if found, or ~index of first larger element
                int idx = sortedPortfolioScores.BinarySearch(thisScore);
                int below = idx >= 0 ? idx : ~idx;
                ratio.Percentile = (below / (double)sortedPortfolioScores.Count) * 100;
            }

            return ratio;
        }

        private EfficiencyRatio ComputePortfolioEfficiency(List<ConsumptionSample> samples, List<double> sortedPortfolioScores)
        {
            return ComputeEfficiency(samples, sortedPortfolioScores);
        }

        private double ComputeTrend(List<ConsumptionSample> samples)
        {
            if (samples.Count < 3) return 0;

            var ordered = samples.OrderBy(s => s.Timestamp).ToList();
            int n = ordered.Count;
            int half = n / 2;
            var firstHalf = ordered.Take(half).Average(s => (double)s.TotalTokens);
            var secondHalf = ordered.Skip(n - half).Average(s => (double)s.TotalTokens);

            if (firstHalf == 0) return 0;
            return (secondHalf - firstHalf) / firstHalf;
        }

        private MetabolicState ClassifyPortfolioState(MetabolicHealthReport report)
        {
            if (report.Profiles.Count == 0) return MetabolicState.Starving;

            var states = report.Profiles.Select(p => p.State).ToList();
            var counts = states.GroupBy(s => s).OrderByDescending(g => g.Count()).ToList();

            // If dominant state has majority, use it
            if (counts.First().Count() > states.Count / 2)
                return counts.First().Key;

            // Mixed states
            if (states.Contains(MetabolicState.Gorging) && states.Contains(MetabolicState.Starving))
                return MetabolicState.Erratic;

            return MetabolicState.Balanced;
        }

        private List<DisorderDiagnosis> DetectDisorders(Dictionary<string, List<ConsumptionSample>> groupedByPrompt, List<PromptMetabolicProfile> profiles)
        {
            var disorders = new List<DisorderDiagnosis>();

            disorders.AddRange(DetectCostSpikes(groupedByPrompt));
            disorders.AddRange(DetectDiminishingReturns(groupedByPrompt));
            disorders.AddRange(DetectTokenBloat(profiles));
            disorders.AddRange(DetectBingeStarveCycle(groupedByPrompt));
            disorders.AddRange(DetectOverfeeding(groupedByPrompt));
            disorders.AddRange(DetectCapacityExhaustion(groupedByPrompt));
            disorders.AddRange(DetectRedundancyWaste(groupedByPrompt));

            return disorders;
        }

        private List<DisorderDiagnosis> DetectCostSpikes(Dictionary<string, List<ConsumptionSample>> groupedByPrompt)
        {
            var results = new List<DisorderDiagnosis>();

            foreach (var (promptId, g) in groupedByPrompt)
            {
                var ordered = g.OrderBy(s => s.Timestamp).ToList();
                if (ordered.Count < 5) continue;

                var baseline = ordered.Take(ordered.Count - 2).Average(s => s.CostUsd ?? 0);
                var recent = ordered.Skip(ordered.Count - 2).Average(s => s.CostUsd ?? 0);

                if (baseline > 0 && recent > baseline * 2.5)
                {
                    // Estimate daily call rate from sample time span for accurate monthly projection
                    var spanDays = (ordered.Last().Timestamp - ordered.First().Timestamp).TotalDays;
                    var dailyRate = spanDays > 0 ? ordered.Count / spanDays : 1.0;

                    results.Add(new DisorderDiagnosis
                    {
                        Disorder = MetabolicDisorder.CostSpike,
                        Severity = recent > baseline * 5 ? DisorderSeverity.Critical : recent > baseline * 3 ? DisorderSeverity.Severe : DisorderSeverity.Moderate,
                        Confidence = Math.Min(0.9, 0.5 + (ordered.Count / 20.0)),
                        AffectedPrompts = new List<string> { promptId },
                        Evidence = new List<string> { $"Recent avg cost ${recent:F4} is {recent / baseline:F1}x baseline ${baseline:F4}" },
                        EstimatedMonthlyCostImpact = (recent - baseline) * dailyRate * 30,
                        FirstDetected = ordered.Last().Timestamp,
                        IsProgressing = true
                    });
                }
            }

            return results;
        }

        private List<DisorderDiagnosis> DetectDiminishingReturns(Dictionary<string, List<ConsumptionSample>> groupedByPrompt)
        {
            var results = new List<DisorderDiagnosis>();

            foreach (var (promptId, samples) in groupedByPrompt)
            {
                var qualitySamples = samples.Where(s => s.QualityScore.HasValue).ToList();
                if (qualitySamples.Count < 6) continue;

                var ordered = qualitySamples.OrderBy(s => s.Timestamp).ToList();
                int half = ordered.Count / 2;
                var firstTokens = ordered.Take(half).Average(s => (double)s.TotalTokens);
                var secondTokens = ordered.Skip(half).Average(s => (double)s.TotalTokens);
                var firstQuality = ordered.Take(half).Average(s => s.QualityScore!.Value);
                var secondQuality = ordered.Skip(half).Average(s => s.QualityScore!.Value);

                // Tokens grew significantly but quality didn't
                if (secondTokens > firstTokens * 1.3 && secondQuality <= firstQuality * 1.05)
                {
                    results.Add(new DisorderDiagnosis
                    {
                        Disorder = MetabolicDisorder.DiminishingReturns,
                        Severity = secondTokens > firstTokens * 2 ? DisorderSeverity.Severe : DisorderSeverity.Moderate,
                        Confidence = 0.7,
                        AffectedPrompts = new List<string> { promptId },
                        Evidence = new List<string> { $"Tokens grew {(secondTokens / firstTokens - 1):P0} but quality only {(secondQuality / firstQuality - 1):+P0;-P0;unchanged}" },
                        EstimatedMonthlyCostImpact = (secondTokens - firstTokens) / 1000.0 * 0.01 * 30,
                        FirstDetected = ordered[half].Timestamp
                    });
                }
            }

            return results;
        }

        private List<DisorderDiagnosis> DetectTokenBloat(List<PromptMetabolicProfile> profiles)
        {
            var results = new List<DisorderDiagnosis>();

            foreach (var p in profiles)
            {
                if (p.ConsumptionTrend > 0.3 && p.SampleCount >= 5)
                {
                    results.Add(new DisorderDiagnosis
                    {
                        Disorder = MetabolicDisorder.TokenBloat,
                        Severity = p.ConsumptionTrend > 0.8 ? DisorderSeverity.Severe : p.ConsumptionTrend > 0.5 ? DisorderSeverity.Moderate : DisorderSeverity.Mild,
                        Confidence = Math.Min(0.85, 0.4 + p.SampleCount / 20.0),
                        AffectedPrompts = new List<string> { p.PromptId },
                        Evidence = new List<string> { $"Token consumption trending up {p.ConsumptionTrend:P0} over observation period" },
                        EstimatedMonthlyCostImpact = p.ProjectedMonthlySpend * p.ConsumptionTrend,
                        IsProgressing = true
                    });
                }
            }

            return results;
        }

        private List<DisorderDiagnosis> DetectBingeStarveCycle(Dictionary<string, List<ConsumptionSample>> groupedByPrompt)
        {
            var results = new List<DisorderDiagnosis>();

            foreach (var (promptId, g) in groupedByPrompt)
            {
                var tokens = g.Select(s => (double)s.TotalTokens).ToList();
                if (tokens.Count < 5) continue;

                var avg = tokens.Average();
                if (avg == 0) continue;
                var cv = Math.Sqrt(tokens.Average(t => Math.Pow(t - avg, 2))) / avg;

                // Count direction changes (oscillations)
                int changes = 0;
                for (int i = 2; i < tokens.Count; i++)
                {
                    if ((tokens[i] > tokens[i - 1]) != (tokens[i - 1] > tokens[i - 2]))
                        changes++;
                }
                var oscillationRate = changes / (double)(tokens.Count - 2);

                if (cv > 0.7 && oscillationRate > 0.6)
                {
                    results.Add(new DisorderDiagnosis
                    {
                        Disorder = MetabolicDisorder.BingeStarveCycle,
                        Severity = cv > 1.2 ? DisorderSeverity.Severe : DisorderSeverity.Moderate,
                        Confidence = Math.Min(0.8, cv * 0.5),
                        AffectedPrompts = new List<string> { promptId },
                        Evidence = new List<string> { $"CV={cv:F2}, oscillation rate={oscillationRate:P0} — inconsistent consumption" },
                        EstimatedMonthlyCostImpact = avg * cv * 0.001 * 30
                    });
                }
            }

            return results;
        }

        private List<DisorderDiagnosis> DetectOverfeeding(Dictionary<string, List<ConsumptionSample>> groupedByPrompt)
        {
            var results = new List<DisorderDiagnosis>();
            var expensiveModels = new HashSet<string> { "gpt-4", "claude-3-opus" };

            foreach (var (promptId, samples) in groupedByPrompt)
            {
                var lowComplexity = samples.Where(s => expensiveModels.Contains(s.Model) && s.InputTokens < 500 && (s.QualityScore ?? 80) > 70).ToList();
                if (lowComplexity.Count >= 3)
                {
                    var wastedCost = lowComplexity.Sum(s => s.CostUsd ?? 0) * 0.8; // 80% could be saved with cheaper model
                    results.Add(new DisorderDiagnosis
                    {
                        Disorder = MetabolicDisorder.Overfeeding,
                        Severity = lowComplexity.Count > 10 ? DisorderSeverity.Severe : DisorderSeverity.Moderate,
                        Confidence = 0.75,
                        AffectedPrompts = new List<string> { promptId },
                        Evidence = new List<string> { $"{lowComplexity.Count} simple tasks routed to expensive model ({lowComplexity.First().Model})" },
                        EstimatedMonthlyCostImpact = wastedCost * 4 // roughly monthly extrapolation
                    });
                }
            }

            return results;
        }

        private List<DisorderDiagnosis> DetectCapacityExhaustion(Dictionary<string, List<ConsumptionSample>> groupedByPrompt)
        {
            var results = new List<DisorderDiagnosis>();

            foreach (var (promptId, samples) in groupedByPrompt)
            {
                // Sub-group by model within each prompt group
                var modelGroups = samples.Where(s => !string.IsNullOrEmpty(s.Model)).GroupBy(s => s.Model);
                foreach (var mg in modelGroups)
                {
                    if (!_modelContextLimits.TryGetValue(mg.Key, out var limit)) continue;

                    var highUsage = mg.Where(s => s.TotalTokens > limit * 0.8).ToList();
                    if (highUsage.Count >= 2)
                    {
                        results.Add(new DisorderDiagnosis
                        {
                            Disorder = MetabolicDisorder.CapacityExhaustion,
                            Severity = highUsage.Any(s => s.TotalTokens > limit * 0.95) ? DisorderSeverity.Critical : DisorderSeverity.Severe,
                            Confidence = 0.85,
                            AffectedPrompts = new List<string> { promptId },
                            Evidence = new List<string> { $"{highUsage.Count} calls used >{(highUsage.Average(s => (double)s.TotalTokens) / limit):P0} of {mg.Key} context ({limit:N0} tokens)" },
                            EstimatedMonthlyCostImpact = highUsage.Average(s => s.CostUsd ?? 0) * 30
                        });
                    }
                }
            }

            return results;
        }

        private List<DisorderDiagnosis> DetectRedundancyWaste(Dictionary<string, List<ConsumptionSample>> groupedByPrompt)
        {
            var results = new List<DisorderDiagnosis>();

            foreach (var (promptId, g) in groupedByPrompt)
            {
                var ordered = g.OrderBy(s => s.Timestamp).ToList();
                if (ordered.Count < 4) continue;

                // Check if input tokens are nearly identical (±5%) across most calls
                var avgInput = ordered.Average(s => (double)s.InputTokens);
                if (avgInput == 0) continue;
                var identicalCount = ordered.Count(s => Math.Abs(s.InputTokens - avgInput) / avgInput < 0.05);
                var identicalRate = identicalCount / (double)ordered.Count;

                if (identicalRate > 0.8 && ordered.Count >= 5)
                {
                    var potentialSavings = avgInput * 0.001 * 0.005 * ordered.Count * 0.7; // ~70% cacheable
                    results.Add(new DisorderDiagnosis
                    {
                        Disorder = MetabolicDisorder.RedundancyWaste,
                        Severity = identicalRate > 0.95 ? DisorderSeverity.Moderate : DisorderSeverity.Mild,
                        Confidence = identicalRate,
                        AffectedPrompts = new List<string> { promptId },
                        Evidence = new List<string> { $"{identicalRate:P0} of calls have nearly identical input tokens (~{avgInput:F0}) — likely cacheable" },
                        EstimatedMonthlyCostImpact = potentialSavings
                    });
                }
            }

            return results;
        }

        private List<DietaryRecommendation> GenerateRecommendations(List<DisorderDiagnosis> disorders, List<PromptMetabolicProfile> profiles)
        {
            var recs = new List<DietaryRecommendation>();
            int priority = 1;

            // Recommendations based on disorders
            foreach (var d in disorders.OrderByDescending(x => x.EstimatedMonthlyCostImpact))
            {
                var rec = d.Disorder switch
                {
                    MetabolicDisorder.CostSpike => new DietaryRecommendation
                    {
                        Action = DietaryAction.Cap,
                        Description = "Set budget caps to prevent cost spikes. Investigate root cause of sudden increase.",
                        EstimatedMonthlySavings = d.EstimatedMonthlyCostImpact * 0.7,
                        TokenReductionPercent = 0,
                        QualityRisk = 0.1,
                        Steps = new List<string> { "Add per-prompt budget limits", "Set up cost alerts", "Review recent prompt changes", "Consider rate limiting" }
                    },
                    MetabolicDisorder.DiminishingReturns => new DietaryRecommendation
                    {
                        Action = DietaryAction.Trim,
                        Description = "Trim token count back to earlier levels — extra tokens aren't improving quality.",
                        EstimatedMonthlySavings = d.EstimatedMonthlyCostImpact * 0.8,
                        TokenReductionPercent = 25,
                        QualityRisk = 0.15,
                        Steps = new List<string> { "Identify which sections grew", "A/B test trimmed vs full version", "Remove low-signal context", "Monitor quality after trim" }
                    },
                    MetabolicDisorder.TokenBloat => new DietaryRecommendation
                    {
                        Action = DietaryAction.Consolidate,
                        Description = "Consolidate growing prompt components. Token creep indicates accumulating context.",
                        EstimatedMonthlySavings = d.EstimatedMonthlyCostImpact * 0.5,
                        TokenReductionPercent = 20,
                        QualityRisk = 0.2,
                        Steps = new List<string> { "Audit prompt template for accumulated cruft", "Summarize long context sections", "Use reference IDs instead of inline data", "Set max-token guardrails" }
                    },
                    MetabolicDisorder.Overfeeding => new DietaryRecommendation
                    {
                        Action = DietaryAction.Downgrade,
                        Description = "Route simple tasks to cheaper models. Expensive models add no value for low-complexity work.",
                        EstimatedMonthlySavings = d.EstimatedMonthlyCostImpact * 0.8,
                        TokenReductionPercent = 0,
                        QualityRisk = 0.1,
                        Steps = new List<string> { "Classify tasks by complexity", "Route simple tasks to gpt-4o-mini or haiku", "Keep expensive models for complex reasoning", "Monitor quality with model routing" }
                    },
                    MetabolicDisorder.BingeStarveCycle => new DietaryRecommendation
                    {
                        Action = DietaryAction.Schedule,
                        Description = "Stabilize consumption patterns. Erratic usage suggests poorly controlled prompt sizing.",
                        EstimatedMonthlySavings = d.EstimatedMonthlyCostImpact * 0.3,
                        TokenReductionPercent = 15,
                        QualityRisk = 0.05,
                        Steps = new List<string> { "Add input validation for prompt variables", "Set min/max token boundaries", "Batch similar requests", "Review variable-length inputs" }
                    },
                    MetabolicDisorder.CapacityExhaustion => new DietaryRecommendation
                    {
                        Action = DietaryAction.Decompose,
                        Description = "Split large prompts into smaller focused calls. Hitting context limits risks truncation.",
                        EstimatedMonthlySavings = d.EstimatedMonthlyCostImpact * 0.4,
                        TokenReductionPercent = 30,
                        QualityRisk = 0.25,
                        Steps = new List<string> { "Identify separable sub-tasks", "Chain smaller prompts with intermediate results", "Compress context with summaries", "Consider models with larger context" }
                    },
                    MetabolicDisorder.RedundancyWaste => new DietaryRecommendation
                    {
                        Action = DietaryAction.Cache,
                        Description = "Enable prompt caching for repeated identical inputs. Significant savings available.",
                        EstimatedMonthlySavings = d.EstimatedMonthlyCostImpact * 0.7,
                        TokenReductionPercent = 50,
                        QualityRisk = 0.0,
                        Steps = new List<string> { "Enable API-level prompt caching", "Hash input tokens for cache keys", "Set appropriate TTL", "Monitor cache hit rates" }
                    },
                    MetabolicDisorder.NutrientDeficiency => new DietaryRecommendation
                    {
                        Action = DietaryAction.Supplement,
                        Description = "Quality is suffering — add more context or better instructions where needed.",
                        EstimatedMonthlySavings = 0,
                        TokenReductionPercent = -10,
                        QualityRisk = 0.0,
                        Steps = new List<string> { "Add few-shot examples", "Improve system prompt clarity", "Include relevant context", "Consider upgrading model tier" }
                    },
                    _ => null
                };

                if (rec != null)
                {
                    rec.Priority = priority++;
                    rec.TargetPrompts = d.AffectedPrompts;
                    recs.Add(rec);
                }
            }

            // Additional recommendations from profiles (high-spend prompts without disorders)
            foreach (var p in profiles.Where(pr => pr.ProjectedMonthlySpend > 1.0 && (pr.Efficiency.Grade.StartsWith("D") || pr.Efficiency.Grade == "F")))
            {
                if (!recs.Any(r => r.TargetPrompts.Contains(p.PromptId)))
                {
                    recs.Add(new DietaryRecommendation
                    {
                        Action = DietaryAction.Trim,
                        Description = $"Low-efficiency prompt with high spend — review and optimize.",
                        TargetPrompts = new List<string> { p.PromptId },
                        EstimatedMonthlySavings = p.ProjectedMonthlySpend * 0.3,
                        TokenReductionPercent = 20,
                        QualityRisk = 0.2,
                        Priority = priority++,
                        Steps = new List<string> { "Review prompt for unnecessary verbosity", "Test with reduced context", "Consider alternative approaches" }
                    });
                }
            }

            return recs;
        }

        private double ComputeHealthScore(MetabolicHealthReport report)
        {
            double score = 100;

            // Deduct for disorders
            foreach (var d in report.Disorders)
            {
                score -= d.Severity switch
                {
                    DisorderSeverity.Critical => 20,
                    DisorderSeverity.Severe => 12,
                    DisorderSeverity.Moderate => 7,
                    DisorderSeverity.Mild => 3,
                    _ => 0
                };
            }

            // Deduct for poor efficiency
            if (report.PortfolioEfficiency.Grade == "F") score -= 15;
            else if (report.PortfolioEfficiency.Grade == "D") score -= 10;
            else if (report.PortfolioEfficiency.Grade.StartsWith("C")) score -= 5;

            // Deduct for extreme states
            var gorgingCount = report.Profiles.Count(p => p.State == MetabolicState.Gorging);
            score -= gorgingCount * 5;

            var erraticCount = report.Profiles.Count(p => p.State == MetabolicState.Erratic);
            score -= erraticCount * 3;

            // Bonus for good health
            var balancedCount = report.Profiles.Count(p => p.State == MetabolicState.Balanced || p.State == MetabolicState.SlowBurn);
            score += Math.Min(balancedCount * 2, 10);

            return Math.Max(0, Math.Min(100, score));
        }

        private string GenerateSummary(MetabolicHealthReport report)
        {
            var sb = new StringBuilder();
            sb.Append($"Portfolio metabolic state: {report.OverallState} (Health: {report.HealthScore:F0}/100). ");
            sb.Append($"Analyzed {report.Profiles.Sum(p => p.SampleCount)} samples across {report.Profiles.Count} prompts. ");

            if (report.Disorders.Count > 0)
            {
                var critical = report.Disorders.Count(d => d.Severity >= DisorderSeverity.Severe);
                sb.Append($"Detected {report.Disorders.Count} metabolic disorders ({critical} severe+). ");
            }
            else
            {
                sb.Append("No metabolic disorders detected — token health is good. ");
            }

            if (report.PotentialMonthlySavings > 0)
                sb.Append($"Following all {report.Recommendations.Count} recommendations could save ~${report.PotentialMonthlySavings:F2}/month.");

            return sb.ToString();
        }

        private static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);
    }
}
