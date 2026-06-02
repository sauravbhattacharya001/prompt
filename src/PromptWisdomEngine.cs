namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ────────────────────────────────────────────
    //  PromptWisdomEngine – Autonomous Learning Engine
    //
    //  Accumulates knowledge from prompt outcomes, extracts
    //  reusable heuristic rules through pattern analysis, and
    //  autonomously advises on new prompts based on learned wisdom.
    //  Learns from experience, adapts over time, and builds
    //  increasingly confident recommendations.
    // ────────────────────────────────────────────

    /// <summary>Outcome verdict for a prompt execution.</summary>
    public enum OutcomeVerdict
    {
        /// <summary>Prompt produced desired results.</summary>
        Success,
        /// <summary>Prompt partially achieved its goal.</summary>
        Partial,
        /// <summary>Prompt failed to produce useful results.</summary>
        Failure
    }

    /// <summary>Category of wisdom a rule belongs to.</summary>
    public enum WisdomCategory
    {
        /// <summary>Structural patterns (lists, headers, examples).</summary>
        Structure,
        /// <summary>Tone and formality patterns.</summary>
        Tone,
        /// <summary>Length and verbosity patterns.</summary>
        Length,
        /// <summary>Specificity and detail level.</summary>
        Specificity,
        /// <summary>Context framing (system prompts, roles).</summary>
        Context,
        /// <summary>Safety and injection avoidance.</summary>
        Safety,
        /// <summary>Token efficiency patterns.</summary>
        Efficiency,
        /// <summary>Clarity and readability.</summary>
        Clarity
    }

    /// <summary>Confidence level of a wisdom rule based on evidence.</summary>
    public enum RuleConfidence
    {
        /// <summary>Fewer than 5 supporting observations.</summary>
        Low,
        /// <summary>5-14 supporting observations.</summary>
        Medium,
        /// <summary>15-29 supporting observations.</summary>
        High,
        /// <summary>30+ supporting observations.</summary>
        Proven
    }

    /// <summary>Urgency level for wisdom advice.</summary>
    public enum AdviceUrgency
    {
        /// <summary>Nice to have improvement.</summary>
        Suggestion,
        /// <summary>Likely to improve quality.</summary>
        Recommended,
        /// <summary>High confidence improvement.</summary>
        StronglyRecommended,
        /// <summary>Must address for acceptable quality.</summary>
        Critical
    }

    /// <summary>Records the outcome of a single prompt execution.</summary>
    public class PromptOutcome
    {
        /// <summary>Unique identifier for the prompt.</summary>
        public string PromptId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>The full prompt text.</summary>
        public string PromptText { get; set; } = "";

        /// <summary>Outcome verdict.</summary>
        public OutcomeVerdict Verdict { get; set; }

        /// <summary>Quality score from 0.0 to 1.0.</summary>
        public double QualityScore { get; set; }

        /// <summary>Metadata tags (model, domain, task-type, etc.).</summary>
        public Dictionary<string, string> Tags { get; set; } = new();

        /// <summary>Reason for failure, if applicable.</summary>
        public string? FailureReason { get; set; }

        /// <summary>When the outcome was recorded.</summary>
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A learned heuristic rule extracted from outcome patterns.</summary>
    public class WisdomRule
    {
        /// <summary>Unique rule identifier.</summary>
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>Category this rule belongs to.</summary>
        public WisdomCategory Category { get; set; }

        /// <summary>Human-readable description of the rule.</summary>
        public string Description { get; set; } = "";

        /// <summary>Detection pattern (keyword or regex fragment).</summary>
        public string Pattern { get; set; } = "";

        /// <summary>True if the pattern correlates with success; false if with failure.</summary>
        public bool IsPositive { get; set; }

        /// <summary>Confidence level based on evidence count.</summary>
        public RuleConfidence Confidence { get; set; }

        /// <summary>Number of outcomes supporting this rule.</summary>
        public int SupportingEvidence { get; set; }

        /// <summary>Number of outcomes conflicting with this rule.</summary>
        public int ConflictingEvidence { get; set; }

        /// <summary>Measured quality difference (positive = better).</summary>
        public double EffectSize { get; set; }

        /// <summary>When this rule was first learned.</summary>
        public DateTime LearnedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When this rule was last validated against new data.</summary>
        public DateTime LastValidated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A piece of advice for a specific prompt.</summary>
    public class WisdomAdvice
    {
        /// <summary>Prompt this advice applies to.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>The rule generating this advice.</summary>
        public WisdomRule Rule { get; set; } = new();

        /// <summary>Urgency of this advice.</summary>
        public AdviceUrgency Urgency { get; set; }

        /// <summary>Human-readable explanation.</summary>
        public string Explanation { get; set; } = "";

        /// <summary>Estimated quality improvement if advice is followed.</summary>
        public double PredictedImpact { get; set; }
    }

    /// <summary>Full wisdom analysis report for a prompt.</summary>
    public class WisdomReport
    {
        /// <summary>Prompt identifier.</summary>
        public string PromptId { get; set; } = "";

        /// <summary>The prompt text analyzed.</summary>
        public string PromptText { get; set; } = "";

        /// <summary>All advice items.</summary>
        public List<WisdomAdvice> Advice { get; set; } = new();

        /// <summary>Predicted quality score (0.0-1.0).</summary>
        public double PredictedQuality { get; set; }

        /// <summary>Engine confidence in the prediction (0.0-1.0).</summary>
        public double WisdomConfidence { get; set; }

        /// <summary>Number of rules that matched.</summary>
        public int RulesApplied { get; set; }

        /// <summary>Total rules in the knowledge base.</summary>
        public int TotalRulesAvailable { get; set; }

        /// <summary>When this report was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>An aggregate insight from the knowledge base.</summary>
    public class WisdomInsight
    {
        /// <summary>Description of the pattern.</summary>
        public string Description { get; set; } = "";

        /// <summary>Category this insight belongs to.</summary>
        public WisdomCategory Category { get; set; }

        /// <summary>How many outcomes exhibit this pattern.</summary>
        public int OccurrenceCount { get; set; }

        /// <summary>Average quality delta associated with this pattern.</summary>
        public double AverageQualityDelta { get; set; }

        /// <summary>Example prompt ids demonstrating this pattern.</summary>
        public List<string> ExamplePromptIds { get; set; } = new();
    }

    /// <summary>Internal persistence wrapper for JSON serialization.</summary>
    internal class WisdomEngineState
    {
        public List<PromptOutcome> Outcomes { get; set; } = new();
        public List<WisdomRule> Rules { get; set; } = new();
    }

    /// <summary>
    /// Autonomous learning engine that accumulates knowledge from prompt
    /// outcomes, extracts heuristic rules through pattern analysis, and
    /// advises on new prompts based on learned wisdom.
    /// </summary>
    public class PromptWisdomEngine
    {
        private readonly List<PromptOutcome> _outcomes = new();
        private readonly List<WisdomRule> _rules = new();

        // ── Pattern detectors (compiled once) ───────────────────────────

        private static readonly Regex QuestionMarkRx = new(@"\?", RegexOptions.Compiled);
        private static readonly Regex BulletListRx = new(@"(?m)^[\s]*[-*•]\s", RegexOptions.Compiled);
        private static readonly Regex NumberedListRx = new(@"(?m)^[\s]*\d+[.)]\s", RegexOptions.Compiled);
        private static readonly Regex CodeBlockRx = new(@"```", RegexOptions.Compiled);
        private static readonly Regex ExampleRx = new(@"\b(example|e\.g\.|for instance|such as)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ImperativeRx = new(@"(?m)^(Please |Do |Make |Create |Write |Generate |Build |Implement |Add |Remove |Fix |Update |Return |List |Explain |Describe |Show )", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RoleRx = new(@"\b(you are|act as|role|persona|assistant|system)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FewShotRx = new(@"\b(input:|output:|Q:|A:|example \d|sample \d)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex InjectionRx = new(@"\b(ignore previous|disregard|forget|override|jailbreak|DAN|bypass)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex VagueRx = new(@"\b(something|stuff|things|whatever|etc|somehow|kind of|sort of)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ConcreteRx = new(@"\b(\d+[\s]*(percent|%|ms|seconds|minutes|hours|days|bytes|KB|MB|GB|items|rows|lines|words|characters))\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ConstraintRx = new(@"\b(must|should|exactly|at most|at least|no more than|maximum|minimum|between)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Creates a new empty wisdom engine.</summary>
        public PromptWisdomEngine() { }

        /// <summary>Number of outcomes in the knowledge base.</summary>
        public int OutcomeCount => _outcomes.Count;

        /// <summary>Current set of learned rules.</summary>
        public List<WisdomRule> Rules => new(_rules);

        // ── Recording ──────────────────────────────────────────────────

        /// <summary>Record a single prompt outcome.</summary>
        public void RecordOutcome(PromptOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            _outcomes.Add(outcome);
        }

        /// <summary>Record multiple outcomes at once.</summary>
        public void RecordOutcomes(IEnumerable<PromptOutcome> outcomes)
        {
            if (outcomes == null) throw new ArgumentNullException(nameof(outcomes));
            foreach (var o in outcomes) RecordOutcome(o);
        }

        // ── Learning ───────────────────────────────────────────────────

        /// <summary>
        /// Analyze all recorded outcomes and extract or update heuristic
        /// wisdom rules. Returns the complete updated rule set.
        /// </summary>
        public List<WisdomRule> LearnRules()
        {
            _rules.Clear();
            if (_outcomes.Count < 2) return new(_rules);

            var successes = _outcomes.Where(o => o.Verdict == OutcomeVerdict.Success).ToList();
            var failures = _outcomes.Where(o => o.Verdict == OutcomeVerdict.Failure).ToList();

            // Need at least one of each to learn contrastive patterns
            if (successes.Count == 0 && failures.Count == 0) return new(_rules);

            LearnLengthRules(successes, failures);
            LearnStructureRules(successes, failures);
            LearnToneRules(successes, failures);
            LearnSpecificityRules(successes, failures);
            LearnContextRules(successes, failures);
            LearnSafetyRules(successes, failures);
            LearnEfficiencyRules(successes, failures);
            LearnClarityRules(successes, failures);

            return new(_rules);
        }

        // ── Advising ──────────────────────────────────────────────────

        /// <summary>
        /// Generate a full wisdom report for a prompt, applying all
        /// learned rules and predicting quality.
        /// </summary>
        public WisdomReport Advise(string promptText, Dictionary<string, string>? tags = null)
        {
            var promptId = Guid.NewGuid().ToString("N")[..12];
            var advice = new List<WisdomAdvice>();

            foreach (var rule in _rules)
            {
                bool matches;
                try
                {
                    matches = Regex.IsMatch(promptText, rule.Pattern, RegexOptions.IgnoreCase);
                }
                catch
                {
                    matches = promptText.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
                }

                bool needsAdvice = rule.IsPositive ? !matches : matches;
                if (!needsAdvice) continue;

                var urgency = ClassifyUrgency(rule);
                var impact = Math.Abs(rule.EffectSize) * ConfidenceMultiplier(rule.Confidence);

                advice.Add(new WisdomAdvice
                {
                    PromptId = promptId,
                    Rule = rule,
                    Urgency = urgency,
                    Explanation = rule.IsPositive
                        ? $"Consider adding: {rule.Description}"
                        : $"Consider removing: {rule.Description}",
                    PredictedImpact = Math.Round(impact, 4)
                });
            }

            advice = advice.OrderByDescending(a => a.PredictedImpact).ToList();

            double baseQuality = _outcomes.Count > 0
                ? _outcomes.Average(o => o.QualityScore)
                : 0.5;

            // Positive matches boost, negative matches penalize
            double adjustment = 0;
            foreach (var rule in _rules)
            {
                bool matches;
                try { matches = Regex.IsMatch(promptText, rule.Pattern, RegexOptions.IgnoreCase); }
                catch { matches = promptText.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase); }

                if (matches && rule.IsPositive)
                    adjustment += rule.EffectSize * 0.1 * ConfidenceMultiplier(rule.Confidence);
                else if (matches && !rule.IsPositive)
                    adjustment -= Math.Abs(rule.EffectSize) * 0.1 * ConfidenceMultiplier(rule.Confidence);
            }

            double predicted = Math.Clamp(baseQuality + adjustment, 0.0, 1.0);

            double wisdomConf = _rules.Count > 0
                ? Math.Min(1.0, _rules.Count / 20.0) * Math.Min(1.0, _outcomes.Count / 50.0)
                : 0.0;

            return new WisdomReport
            {
                PromptId = promptId,
                PromptText = promptText,
                Advice = advice,
                PredictedQuality = Math.Round(predicted, 4),
                WisdomConfidence = Math.Round(wisdomConf, 4),
                RulesApplied = advice.Count,
                TotalRulesAvailable = _rules.Count,
                GeneratedAt = DateTime.UtcNow
            };
        }

        /// <summary>Quick advice — returns the top 5 most impactful items.</summary>
        public List<WisdomAdvice> QuickAdvise(string promptText)
        {
            var report = Advise(promptText);
            return report.Advice.Take(5).ToList();
        }

        // ── Insights ──────────────────────────────────────────────────

        /// <summary>
        /// Generate aggregate insights from the knowledge base: common
        /// failure modes, best practices, and surprising findings.
        /// </summary>
        public List<WisdomInsight> GetInsights()
        {
            var insights = new List<WisdomInsight>();
            if (_outcomes.Count == 0) return insights;

            double overallAvg = _outcomes.Average(o => o.QualityScore);

            // Group by verdict
            foreach (var group in _outcomes.GroupBy(o => o.Verdict))
            {
                var avg = group.Average(o => o.QualityScore);
                insights.Add(new WisdomInsight
                {
                    Description = $"{group.Key} outcomes: avg quality {avg:F3} ({group.Count()} prompts)",
                    Category = WisdomCategory.Clarity,
                    OccurrenceCount = group.Count(),
                    AverageQualityDelta = Math.Round(avg - overallAvg, 4),
                    ExamplePromptIds = group.Take(3).Select(o => o.PromptId).ToList()
                });
            }

            // Top failure reasons
            var failureReasons = _outcomes
                .Where(o => o.Verdict == OutcomeVerdict.Failure && !string.IsNullOrEmpty(o.FailureReason))
                .GroupBy(o => o.FailureReason!)
                .OrderByDescending(g => g.Count())
                .Take(5);

            foreach (var grp in failureReasons)
            {
                insights.Add(new WisdomInsight
                {
                    Description = $"Common failure: {grp.Key}",
                    Category = WisdomCategory.Safety,
                    OccurrenceCount = grp.Count(),
                    AverageQualityDelta = Math.Round(grp.Average(o => o.QualityScore) - overallAvg, 4),
                    ExamplePromptIds = grp.Take(3).Select(o => o.PromptId).ToList()
                });
            }

            // Rules with high effect size
            foreach (var rule in _rules.Where(r => Math.Abs(r.EffectSize) > 0.1).OrderByDescending(r => Math.Abs(r.EffectSize)).Take(5))
            {
                insights.Add(new WisdomInsight
                {
                    Description = $"Strong signal: {rule.Description} (effect={rule.EffectSize:F3})",
                    Category = rule.Category,
                    OccurrenceCount = rule.SupportingEvidence,
                    AverageQualityDelta = Math.Round(rule.EffectSize, 4),
                    ExamplePromptIds = new List<string>()
                });
            }

            return insights;
        }

        /// <summary>Per-category average quality scores.</summary>
        public Dictionary<WisdomCategory, double> GetCategoryHealth()
        {
            var health = new Dictionary<WisdomCategory, double>();
            if (_outcomes.Count == 0) return health;

            double overallAvg = _outcomes.Average(o => o.QualityScore);

            foreach (WisdomCategory cat in Enum.GetValues<WisdomCategory>())
            {
                var catRules = _rules.Where(r => r.Category == cat).ToList();
                if (catRules.Count == 0)
                {
                    health[cat] = overallAvg;
                    continue;
                }
                double avgEffect = catRules.Average(r => r.EffectSize);
                health[cat] = Math.Round(Math.Clamp(overallAvg + avgEffect, 0.0, 1.0), 4);
            }
            return health;
        }

        // ── Persistence ───────────────────────────────────────────────

        /// <summary>Serialize the entire engine state to JSON.</summary>
        public string ExportJson()
        {
            var state = new WisdomEngineState
            {
                Outcomes = new(_outcomes),
                Rules = new(_rules)
            };
            return JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        /// <summary>Restore engine state from JSON.</summary>
        public static PromptWisdomEngine ImportJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json, nameof(json));
            var state = JsonSerializer.Deserialize<WisdomEngineState>(json, SerializationGuards.ReadWithEnums)
                        ?? new WisdomEngineState();
            var engine = new PromptWisdomEngine();
            engine._outcomes.AddRange(state.Outcomes);
            engine._rules.AddRange(state.Rules);
            return engine;
        }

        // ── Forget / Decay ────────────────────────────────────────────

        /// <summary>Remove all outcomes recorded before the given cutoff.</summary>
        public void ForgetBefore(DateTime cutoff)
        {
            _outcomes.RemoveAll(o => o.RecordedAt < cutoff);
        }

        /// <summary>
        /// Decay all rule evidence by a factor (0.0-1.0), simulating
        /// gradual forgetting. Evidence counts are multiplied by the factor.
        /// </summary>
        public void DecayConfidence(double factor)
        {
            factor = Math.Clamp(factor, 0.0, 1.0);
            foreach (var rule in _rules)
            {
                rule.SupportingEvidence = (int)(rule.SupportingEvidence * factor);
                rule.ConflictingEvidence = (int)(rule.ConflictingEvidence * factor);
                rule.Confidence = ClassifyConfidence(rule.SupportingEvidence);
            }
        }

        // ── Private: Learning helpers ─────────────────────────────────

        private void LearnLengthRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            if (successes.Count == 0 || failures.Count == 0) return;

            double avgSuccessLen = successes.Average(o => o.PromptText.Length);
            double avgFailureLen = failures.Average(o => o.PromptText.Length);
            double delta = avgSuccessLen - avgFailureLen;

            if (Math.Abs(delta) < 20) return; // not significant

            double successAvgQuality = successes.Average(o => o.QualityScore);
            double failureAvgQuality = failures.Count > 0 ? failures.Average(o => o.QualityScore) : 0;

            if (delta > 0)
            {
                // Longer prompts tend to succeed
                int support = successes.Count(o => o.PromptText.Length > avgFailureLen);
                int conflict = successes.Count(o => o.PromptText.Length <= avgFailureLen);
                _rules.Add(new WisdomRule
                {
                    Category = WisdomCategory.Length,
                    Description = $"Prompts longer than ~{(int)avgFailureLen} chars tend to succeed more often",
                    Pattern = @".{" + (int)avgFailureLen + ",}",
                    IsPositive = true,
                    Confidence = ClassifyConfidence(support),
                    SupportingEvidence = support,
                    ConflictingEvidence = conflict,
                    EffectSize = Math.Round(successAvgQuality - failureAvgQuality, 4)
                });
            }
            else
            {
                // Shorter prompts tend to succeed
                int support = successes.Count(o => o.PromptText.Length < avgFailureLen);
                int conflict = successes.Count(o => o.PromptText.Length >= avgFailureLen);
                _rules.Add(new WisdomRule
                {
                    Category = WisdomCategory.Length,
                    Description = $"Prompts shorter than ~{(int)avgFailureLen} chars tend to succeed more often",
                    Pattern = @"^.{1," + (int)avgFailureLen + "}$",
                    IsPositive = true,
                    Confidence = ClassifyConfidence(support),
                    SupportingEvidence = support,
                    ConflictingEvidence = conflict,
                    EffectSize = Math.Round(successAvgQuality - failureAvgQuality, 4)
                });
            }
        }

        private void LearnStructureRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            LearnPatternRule(successes, failures, BulletListRx, WisdomCategory.Structure,
                "Using bullet lists", @"(?m)^[\s]*[-*•]\s");
            LearnPatternRule(successes, failures, NumberedListRx, WisdomCategory.Structure,
                "Using numbered lists", @"(?m)^[\s]*\d+[.)]\s");
            LearnPatternRule(successes, failures, CodeBlockRx, WisdomCategory.Structure,
                "Including code blocks", @"```");
            LearnPatternRule(successes, failures, ExampleRx, WisdomCategory.Structure,
                "Including examples or illustrations", @"\b(example|e\.g\.|for instance|such as)\b");
        }

        private void LearnToneRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            LearnPatternRule(successes, failures, ImperativeRx, WisdomCategory.Tone,
                "Using imperative/direct instructions", @"(?m)^(Please |Do |Make |Create |Write |Generate |Build )");
            LearnPatternRule(successes, failures, QuestionMarkRx, WisdomCategory.Tone,
                "Framing as questions", @"\?");
        }

        private void LearnSpecificityRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            LearnPatternRule(successes, failures, ConcreteRx, WisdomCategory.Specificity,
                "Including concrete numbers or measurements", @"\b\d+[\s]*(percent|%|ms|seconds|items|rows)");
            LearnPatternRule(successes, failures, ConstraintRx, WisdomCategory.Specificity,
                "Including explicit constraints", @"\b(must|should|exactly|at most|at least|no more than)\b");
            LearnPatternRule(successes, failures, VagueRx, WisdomCategory.Specificity,
                "Using vague language", @"\b(something|stuff|things|whatever|etc|somehow)\b");
        }

        private void LearnContextRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            LearnPatternRule(successes, failures, RoleRx, WisdomCategory.Context,
                "Setting a role or persona", @"\b(you are|act as|role|persona|assistant|system)\b");
            LearnPatternRule(successes, failures, FewShotRx, WisdomCategory.Context,
                "Including few-shot examples", @"\b(input:|output:|Q:|A:|example \d)\b");
        }

        private void LearnSafetyRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            LearnPatternRule(successes, failures, InjectionRx, WisdomCategory.Safety,
                "Containing injection-like language", @"\b(ignore previous|disregard|forget|override|jailbreak)\b");
        }

        private void LearnEfficiencyRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            if (successes.Count < 3 || failures.Count < 3) return;

            // Words per quality point
            double avgSuccessWords = successes.Average(o => o.PromptText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            double avgFailureWords = failures.Average(o => o.PromptText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            double avgSuccessQuality = successes.Average(o => o.QualityScore);
            double avgFailureQuality = failures.Average(o => o.QualityScore);

            double successEfficiency = avgSuccessWords > 0 ? avgSuccessQuality / avgSuccessWords : 0;
            double failureEfficiency = avgFailureWords > 0 ? avgFailureQuality / avgFailureWords : 0;

            if (Math.Abs(successEfficiency - failureEfficiency) > 0.001)
            {
                _rules.Add(new WisdomRule
                {
                    Category = WisdomCategory.Efficiency,
                    Description = $"Optimal word count is around {(int)avgSuccessWords} words",
                    Pattern = @"\b\w+\b",
                    IsPositive = true,
                    Confidence = ClassifyConfidence(successes.Count),
                    SupportingEvidence = successes.Count,
                    ConflictingEvidence = failures.Count,
                    EffectSize = Math.Round(avgSuccessQuality - avgFailureQuality, 4)
                });
            }
        }

        private void LearnClarityRules(List<PromptOutcome> successes, List<PromptOutcome> failures)
        {
            // Sentence length analysis
            if (successes.Count < 2 || failures.Count < 2) return;

            double avgSuccessSentences = successes.Average(o => CountSentences(o.PromptText));
            double avgFailureSentences = failures.Average(o => CountSentences(o.PromptText));

            if (avgSuccessSentences > avgFailureSentences + 1)
            {
                _rules.Add(new WisdomRule
                {
                    Category = WisdomCategory.Clarity,
                    Description = "Breaking prompt into multiple clear sentences",
                    Pattern = @"[.!?]\s+[A-Z]",
                    IsPositive = true,
                    Confidence = ClassifyConfidence(successes.Count),
                    SupportingEvidence = successes.Count,
                    ConflictingEvidence = failures.Count,
                    EffectSize = Math.Round(successes.Average(o => o.QualityScore) - failures.Average(o => o.QualityScore), 4)
                });
            }
        }

        private void LearnPatternRule(
            List<PromptOutcome> successes,
            List<PromptOutcome> failures,
            Regex pattern,
            WisdomCategory category,
            string description,
            string patternStr)
        {
            int successMatch = successes.Count(o => pattern.IsMatch(o.PromptText));
            int failureMatch = failures.Count(o => pattern.IsMatch(o.PromptText));
            int totalMatch = successMatch + failureMatch;
            if (totalMatch < 2) return;

            double successRate = successes.Count > 0 ? (double)successMatch / successes.Count : 0;
            double failureRate = failures.Count > 0 ? (double)failureMatch / failures.Count : 0;
            double rateDiff = successRate - failureRate;

            if (Math.Abs(rateDiff) < 0.1) return; // not significant enough

            bool isPositive = rateDiff > 0; // more common in successes
            int support = isPositive ? successMatch : failureMatch;
            int conflict = isPositive ? failureMatch : successMatch;

            double matchAvgQuality = _outcomes
                .Where(o => pattern.IsMatch(o.PromptText))
                .DefaultIfEmpty()
                .Average(o => o?.QualityScore ?? 0);
            double noMatchAvgQuality = _outcomes
                .Where(o => !pattern.IsMatch(o.PromptText))
                .DefaultIfEmpty()
                .Average(o => o?.QualityScore ?? 0);
            double effectSize = matchAvgQuality - noMatchAvgQuality;

            _rules.Add(new WisdomRule
            {
                Category = category,
                Description = description,
                Pattern = patternStr,
                IsPositive = isPositive,
                Confidence = ClassifyConfidence(support),
                SupportingEvidence = support,
                ConflictingEvidence = conflict,
                EffectSize = Math.Round(effectSize, 4)
            });
        }

        // ── Private: Utilities ────────────────────────────────────────

        private static RuleConfidence ClassifyConfidence(int evidence)
        {
            if (evidence >= 30) return RuleConfidence.Proven;
            if (evidence >= 15) return RuleConfidence.High;
            if (evidence >= 5) return RuleConfidence.Medium;
            return RuleConfidence.Low;
        }

        private static AdviceUrgency ClassifyUrgency(WisdomRule rule)
        {
            double score = Math.Abs(rule.EffectSize) * ConfidenceMultiplier(rule.Confidence);
            if (score >= 0.4) return AdviceUrgency.Critical;
            if (score >= 0.25) return AdviceUrgency.StronglyRecommended;
            if (score >= 0.1) return AdviceUrgency.Recommended;
            return AdviceUrgency.Suggestion;
        }

        private static double ConfidenceMultiplier(RuleConfidence c) => c switch
        {
            RuleConfidence.Proven => 1.0,
            RuleConfidence.High => 0.75,
            RuleConfidence.Medium => 0.5,
            RuleConfidence.Low => 0.25,
            _ => 0.25
        };

        private static int CountSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return Regex.Matches(text, @"[.!?]+\s*").Count + 1;
        }
    }
}
