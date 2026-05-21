namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A single hallucination-risk dimension score.
    /// </summary>
    public class HallucinationDimension
    {
        /// <summary>Gets the dimension name.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the raw score (0-10 scale).</summary>
        public double Score { get; internal set; }

        /// <summary>Gets the weight used in overall calculation.</summary>
        public double Weight { get; internal set; }

        /// <summary>Gets a human-readable explanation.</summary>
        public string Explanation { get; internal set; } = "";

        /// <summary>Gets specific evidence items extracted from the prompt.</summary>
        public List<string> Evidence { get; internal set; } = new();
    }

    /// <summary>
    /// A concrete, actionable fix that mitigates a hallucination risk.
    /// </summary>
    public class HallucinationFix
    {
        /// <summary>Gets the fix category, e.g. "grounding", "scope", "uncertainty", "citation", "specificity", "temporal", "compound".</summary>
        public string Category { get; internal set; } = "";

        /// <summary>Gets the severity: "low", "medium", "high", or "critical".</summary>
        public string Severity { get; internal set; } = "low";

        /// <summary>Gets the human-readable issue description.</summary>
        public string Issue { get; internal set; } = "";

        /// <summary>Gets the suggested remediation written as a sentence.</summary>
        public string Suggestion { get; internal set; } = "";

        /// <summary>Gets a ready-to-append snippet that mitigates the issue when added to the prompt.</summary>
        public string ExampleSnippet { get; internal set; } = "";
    }

    /// <summary>
    /// Full hallucination-risk analysis with dimension breakdown and actionable fixes.
    /// </summary>
    public class HallucinationRiskResult
    {
        /// <summary>Gets the overall hallucination risk score (0-10).</summary>
        public double OverallScore { get; internal set; }

        /// <summary>Gets the risk level label.</summary>
        public string Level { get; internal set; } = "";

        /// <summary>Gets per-dimension breakdowns.</summary>
        public List<HallucinationDimension> Dimensions { get; internal set; } = new();

        /// <summary>Gets concrete fixes, sorted by severity descending.</summary>
        public List<HallucinationFix> Fixes { get; internal set; } = new();

        /// <summary>
        /// Gets the original prompt with an automatically appended
        /// <c># Anti-Hallucination Guardrails</c> section drawn from the top fixes.
        /// </summary>
        public string MitigatedPromptDraft { get; internal set; } = "";

        /// <summary>Gets a one-line summary string.</summary>
        public string Summary => $"Hallucination Risk: {OverallScore:F1}/10 ({Level}) - {Fixes.Count} fixes suggested";
    }

    /// <summary>
    /// Analyzes prompts for patterns that tend to induce LLM hallucinations
    /// and produces concrete, paste-ready remediation snippets.
    /// </summary>
    /// <remarks>
    /// <para>This differs from <see cref="PromptComplexityScorer"/> (which measures cognitive load)
    /// and <see cref="PromptRiskAssessor"/> / <see cref="PromptOutputValidator"/> (which look at
    /// generic risk and output shape). The focus here is specifically on prompt patterns
    /// that empirically make models invent unsupported facts, and on emitting fixes the
    /// caller can paste straight back into the prompt.</para>
    ///
    /// <para>Dimensions scored (each 0-10, with the weight applied to the weighted average):</para>
    /// <list type="bullet">
    /// <item><description><b>Unverifiable Specificity</b> (1.5) - asks for exact facts (dates, names, numbers, citations) without supplying source material.</description></item>
    /// <item><description><b>Missing Grounding</b> (2.0) - no Context/Source block, no quoted material, no citations, no URLs.</description></item>
    /// <item><description><b>Speculative Framing</b> (1.2) - "imagine", "invent", "make up", "hypothetical", "guess".</description></item>
    /// <item><description><b>No Uncertainty Allowance</b> (1.3) - forbids "I don't know"; rewards explicit escape hatches.</description></item>
    /// <item><description><b>Open-Ended Recall</b> (1.4) - "tell me about NAME", "history of X", "describe everything".</description></item>
    /// <item><description><b>Temporal Drift</b> (1.0) - "current", "latest", recent year references without dated sources.</description></item>
    /// <item><description><b>Compound Claim Density</b> (1.1) - many independent factual asks chained together.</description></item>
    /// </list>
    /// </remarks>
    public class PromptHallucinationRiskScorer
    {
        private const double FixThreshold = 5.0;

        private static readonly Regex SpecificityRegex = new(
            @"(?i)\b(exact|exactly|precisely|specific|specifically|verbatim|word[- ]for[- ]word)\b",
            RegexOptions.Compiled);

        private static readonly Regex FactNounRegex = new(
            @"(?i)\b(date|year|name|author|citation|quote|statistic|percentage|number|count|price|score|address|phone|email|isbn|doi|figure|metric)s?\b",
            RegexOptions.Compiled);

        private static readonly Regex ListAllRegex = new(
            @"(?i)\b(list\s+(?:all|every)|enumerate\s+all|every\s+single|all\s+of\s+the)\b",
            RegexOptions.Compiled);

        private static readonly Regex HowManyRegex = new(
            @"(?i)\bhow\s+(?:many|much|often)\b",
            RegexOptions.Compiled);

        private static readonly Regex GroundingHeaderRegex = new(
            @"(?im)^\s*(context|source|sources|reference|references|given|based\s+on|using\s+the\s+following|here\s+is\s+the\s+(?:text|document|article))\s*:",
            RegexOptions.Compiled);

        private static readonly Regex FencedBlockRegex = new(
            @"(```[\s\S]+?```|""""""[\s\S]+?"""""")",
            RegexOptions.Compiled);

        private static readonly Regex CitationMarkerRegex = new(
            @"(\[\d+\]|\(\s*\w[\w .'-]*,\s*(?:18|19|20)\d{2}\s*\))",
            RegexOptions.Compiled);

        private static readonly Regex UrlRegex = new(
            @"https?://[^\s)]+",
            RegexOptions.Compiled);

        private static readonly Regex SpeculativeRegex = new(
            @"(?i)\b(imagine|invent|make\s+up|made\s+up|fabricate|hypothetical(?:ly)?|what\s+would\s+\w+\s+say|guess|plausible|fictional|fictitious|fill\s+in\s+the\s+blanks?|pretend)\b",
            RegexOptions.Compiled);

        private static readonly Regex ForbidUncertaintyRegex = new(
            @"(?i)(do\s+not\s+say\s+(?:you\s+)?(?:don'?t\s+know|unknown|unsure)|never\s+(?:refuse|say\s+(?:you\s+)?(?:don'?t\s+know|unknown))|must\s+(?:answer|provide|give)|always\s+(?:answer|provide|give\s+(?:an?\s+)?answer)|don'?t\s+refuse|no\s+i\s+don'?t\s+know|do\s+not\s+hedge)",
            RegexOptions.Compiled);

        private static readonly Regex AllowUncertaintyRegex = new(
            @"(?i)(if\s+(?:you'?re\s+)?(?:not\s+sure|unsure|uncertain|you\s+don'?t\s+know|you\s+can'?t\s+verify)|say\s+['""]?(?:i\s+don'?t\s+know|unknown|not\s+in\s+source|insufficient)|reply\s+['""]?(?:i\s+don'?t\s+know|unknown|not\s+in\s+source)|it\s+is\s+ok\s+to\s+(?:say|admit))",
            RegexOptions.Compiled);

        private static readonly Regex OpenRecallRegex = new(
            @"(?i)\b(tell\s+me\s+about|what\s+do\s+you\s+know\s+about|describe\s+(?:everything\s+about|the\s+(?:work|history|life|career)\s+of)|history\s+of|biography\s+of|summarize\s+the\s+(?:work|career|writings)\s+of|give\s+me\s+(?:a\s+)?(?:rundown|overview|summary)\s+of)\b",
            RegexOptions.Compiled);

        private static readonly Regex ProperNounRegex = new(
            @"\b([A-Z][a-z]{2,}(?:\s+[A-Z][a-z]+){0,3})\b",
            RegexOptions.Compiled);

        private static readonly Regex TemporalCueRegex = new(
            @"(?i)\b(current(?:ly)?|latest|today|this\s+(?:year|month|week)|right\s+now|as\s+of\s+now|recently|recent|most\s+recent|up[- ]to[- ]date|nowadays)\b",
            RegexOptions.Compiled);

        private static readonly Regex RecentYearRegex = new(
            @"\b(20(?:2[4-9]|[3-9]\d))\b",
            RegexOptions.Compiled);

        private static readonly Regex DatedSourceRegex = new(
            @"(?i)\b(as\s+of\s+\d{4}|knowledge\s+cutoff|published\s+\d{4}|dated?\s+\d{4}|retrieved\s+\d{4})\b",
            RegexOptions.Compiled);

        private static readonly Regex ConjunctionRegex = new(
            @"(?i)\b(and|as\s+well\s+as|plus|along\s+with|also|additionally|furthermore|moreover)\b",
            RegexOptions.Compiled);

        private static readonly Regex NumberedListRegex = new(
            @"(?m)^\s*(?:\d+[.)]\s|[-*]\s)",
            RegexOptions.Compiled);

        /// <summary>
        /// Analyzes the given prompt and returns a full hallucination-risk breakdown
        /// plus an auto-generated mitigated prompt draft.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>A populated <see cref="HallucinationRiskResult"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompt"/> is null.</exception>
        public HallucinationRiskResult Score(string prompt)
        {
            if (prompt is null) throw new ArgumentNullException(nameof(prompt));

            var result = new HallucinationRiskResult();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                result.Level = "Empty";
                result.MitigatedPromptDraft = "";
                return result;
            }

            var tokenEstimate = Math.Max(1, EstimateTokens(prompt));

            var groundingPresent = HasGrounding(prompt);

            var dims = new List<HallucinationDimension>
            {
                ScoreUnverifiableSpecificity(prompt, groundingPresent),
                ScoreMissingGrounding(prompt, groundingPresent),
                ScoreSpeculativeFraming(prompt),
                ScoreUncertaintyAllowance(prompt),
                ScoreOpenEndedRecall(prompt, groundingPresent),
                ScoreTemporalDrift(prompt),
                ScoreCompoundClaimDensity(prompt, tokenEstimate),
            };

            result.Dimensions = dims;

            double weightSum = dims.Sum(d => d.Weight);
            double weighted = dims.Sum(d => d.Score * d.Weight);
            double overall = weightSum > 0 ? weighted / weightSum : 0;
            overall = Math.Clamp(overall, 0, 10);
            result.OverallScore = Math.Round(overall, 2);
            result.Level = LevelFor(result.OverallScore);

            result.Fixes = BuildFixes(dims);
            result.MitigatedPromptDraft = BuildMitigatedDraft(prompt, result.Fixes);

            return result;
        }

        private static bool HasGrounding(string prompt)
        {
            return GroundingHeaderRegex.IsMatch(prompt)
                || FencedBlockRegex.IsMatch(prompt)
                || CitationMarkerRegex.IsMatch(prompt)
                || UrlRegex.IsMatch(prompt);
        }

        private static HallucinationDimension ScoreUnverifiableSpecificity(string prompt, bool grounded)
        {
            var evidence = new List<string>();
            int hits = 0;

            foreach (Match m in SpecificityRegex.Matches(prompt)) { hits++; evidence.Add($"specificity cue: '{m.Value}'"); }
            foreach (Match m in FactNounRegex.Matches(prompt)) { hits++; evidence.Add($"fact noun: '{m.Value}'"); }
            foreach (Match m in ListAllRegex.Matches(prompt)) { hits += 2; evidence.Add($"list-all cue: '{m.Value}'"); }
            foreach (Match m in HowManyRegex.Matches(prompt)) { hits++; evidence.Add($"quantity ask: '{m.Value}'"); }

            double raw = Math.Min(10, hits * 1.2);
            if (raw > 0 && !grounded) raw = Math.Min(10, raw + 2.0);
            if (grounded) raw *= 0.5;

            return new HallucinationDimension
            {
                Name = "Unverifiable Specificity",
                Weight = 1.5,
                Score = Math.Round(Math.Clamp(raw, 0, 10), 2),
                Explanation = raw >= 5
                    ? "Prompt demands precise facts (dates, names, numbers, citations) but provides little or no source material to ground them in."
                    : "Specificity demands are modest or are paired with grounding context.",
                Evidence = Dedupe(evidence, 6),
            };
        }

        private static HallucinationDimension ScoreMissingGrounding(string prompt, bool grounded)
        {
            var evidence = new List<string>();
            double raw;

            int factSignals =
                SpecificityRegex.Matches(prompt).Count
                + FactNounRegex.Matches(prompt).Count
                + ListAllRegex.Matches(prompt).Count
                + HowManyRegex.Matches(prompt).Count
                + ProperNounRegex.Matches(prompt).Count;

            if (grounded)
            {
                raw = 1.0;
                if (GroundingHeaderRegex.IsMatch(prompt)) evidence.Add("found grounding header (Context:/Source:/...)");
                if (FencedBlockRegex.IsMatch(prompt)) evidence.Add("found fenced or triple-quoted source block");
                if (CitationMarkerRegex.IsMatch(prompt)) evidence.Add("found citation marker(s)");
                if (UrlRegex.IsMatch(prompt)) evidence.Add("found URL reference(s)");
            }
            else
            {
                raw = Math.Min(10, 3.0 + factSignals * 0.6);
                evidence.Add("no Context/Source/Reference block detected");
                evidence.Add("no quoted source material or citation markers");
                if (factSignals > 0)
                    evidence.Add($"{factSignals} fact-seeking cues detected without supporting grounding");
            }

            return new HallucinationDimension
            {
                Name = "Missing Grounding",
                Weight = 2.0,
                Score = Math.Round(Math.Clamp(raw, 0, 10), 2),
                Explanation = grounded
                    ? "Prompt supplies grounding material the model can quote from."
                    : "Prompt asks the model to produce facts with no source material to draw on; the model will fall back to memorized training data.",
                Evidence = Dedupe(evidence, 6),
            };
        }

        private static HallucinationDimension ScoreSpeculativeFraming(string prompt)
        {
            var evidence = new List<string>();
            int hits = 0;
            foreach (Match m in SpeculativeRegex.Matches(prompt)) { hits++; evidence.Add($"speculative cue: '{m.Value}'"); }

            double raw = Math.Min(10, hits * 2.0);
            return new HallucinationDimension
            {
                Name = "Speculative Framing",
                Weight = 1.2,
                Score = Math.Round(raw, 2),
                Explanation = hits == 0
                    ? "No speculative-framing language detected."
                    : "Prompt invites invention; the model is being asked to produce content that, by construction, has no ground truth.",
                Evidence = Dedupe(evidence, 6),
            };
        }

        private static HallucinationDimension ScoreUncertaintyAllowance(string prompt)
        {
            var evidence = new List<string>();
            int forbid = 0;
            foreach (Match m in ForbidUncertaintyRegex.Matches(prompt)) { forbid++; evidence.Add($"forbids uncertainty: '{m.Value}'"); }

            bool allowUncertainty = AllowUncertaintyRegex.IsMatch(prompt);
            if (allowUncertainty) evidence.Add("explicit 'I don't know' escape hatch present");

            double raw;
            if (forbid > 0) raw = Math.Min(10, 5.0 + forbid * 1.5);
            else if (allowUncertainty) raw = 0.5;
            else raw = 3.5;

            return new HallucinationDimension
            {
                Name = "No Uncertainty Allowance",
                Weight = 1.3,
                Score = Math.Round(raw, 2),
                Explanation = forbid > 0
                    ? "Prompt forbids the model from declining or saying 'I don't know', which forces fabrication when it lacks knowledge."
                    : allowUncertainty
                        ? "Prompt explicitly allows the model to admit uncertainty."
                        : "Prompt neither allows nor forbids uncertainty; the model has no clear escape hatch.",
                Evidence = Dedupe(evidence, 6),
            };
        }

        private static HallucinationDimension ScoreOpenEndedRecall(string prompt, bool grounded)
        {
            var evidence = new List<string>();
            int hits = 0;
            foreach (Match m in OpenRecallRegex.Matches(prompt)) { hits++; evidence.Add($"open recall: '{m.Value}'"); }

            int properNouns = ProperNounRegex.Matches(prompt).Count;
            double raw = Math.Min(10, hits * 2.5 + Math.Min(properNouns, 4) * 0.6);
            if (grounded) raw *= 0.4;

            return new HallucinationDimension
            {
                Name = "Open-Ended Recall",
                Weight = 1.4,
                Score = Math.Round(Math.Clamp(raw, 0, 10), 2),
                Explanation = hits == 0
                    ? "No open-ended recall asks detected."
                    : "Prompt asks the model to recall broad information about specific entities from training memory.",
                Evidence = Dedupe(evidence, 6),
            };
        }

        private static HallucinationDimension ScoreTemporalDrift(string prompt)
        {
            var evidence = new List<string>();
            int hits = 0;
            foreach (Match m in TemporalCueRegex.Matches(prompt)) { hits++; evidence.Add($"temporal cue: '{m.Value}'"); }
            foreach (Match m in RecentYearRegex.Matches(prompt)) { hits++; evidence.Add($"recent year: '{m.Value}'"); }
            bool dated = DatedSourceRegex.IsMatch(prompt);
            if (dated) evidence.Add("dated source / cutoff acknowledgement present");

            double raw = Math.Min(10, hits * 1.8);
            if (dated) raw *= 0.4;

            return new HallucinationDimension
            {
                Name = "Temporal Drift",
                Weight = 1.0,
                Score = Math.Round(Math.Clamp(raw, 0, 10), 2),
                Explanation = hits == 0
                    ? "No temporal-recency cues detected."
                    : dated
                        ? "Prompt references recency but also supplies/acknowledges a date or cutoff."
                        : "Prompt asks about recent/current information without dated sources or a knowledge-cutoff acknowledgement.",
                Evidence = Dedupe(evidence, 6),
            };
        }

        private static HallucinationDimension ScoreCompoundClaimDensity(string prompt, int tokenEstimate)
        {
            int conj = ConjunctionRegex.Matches(prompt).Count;
            int list = NumberedListRegex.Matches(prompt).Count;
            double per100 = (conj + list) * 100.0 / tokenEstimate;
            double raw = Math.Min(10, per100 * 0.8);

            var evidence = new List<string>
            {
                $"{conj} conjunction tokens",
                $"{list} list bullets",
                $"~{per100:F1} chained claims per 100 tokens",
            };

            return new HallucinationDimension
            {
                Name = "Compound Claim Density",
                Weight = 1.1,
                Score = Math.Round(raw, 2),
                Explanation = raw >= 5
                    ? "Many independent factual asks are chained together; per-claim error probability compounds."
                    : "Claim density is modest.",
                Evidence = evidence,
            };
        }

        private static List<HallucinationFix> BuildFixes(List<HallucinationDimension> dims)
        {
            var fixes = new List<HallucinationFix>();

            foreach (var d in dims)
            {
                if (d.Score < FixThreshold) continue;
                var f = FixFor(d);
                if (f is not null) fixes.Add(f);
            }

            var order = new Dictionary<string, int>
            {
                { "critical", 0 }, { "high", 1 }, { "medium", 2 }, { "low", 3 },
            };
            return fixes.OrderBy(f => order.TryGetValue(f.Severity, out var v) ? v : 99).ToList();
        }

        private static HallucinationFix? FixFor(HallucinationDimension d)
        {
            var sev = SeverityFor(d.Score);
            var firstEvidence = d.Evidence.FirstOrDefault() ?? "";

            return d.Name switch
            {
                "Unverifiable Specificity" => new HallucinationFix
                {
                    Category = "specificity",
                    Severity = sev,
                    Issue = $"Unverifiable Specificity={d.Score:F1}/10. Prompt demands exact facts ({firstEvidence}) without source material.",
                    Suggestion = "Either supply the source material the precise facts must come from, or relax the precision (allow approximate / 'roughly' answers).",
                    ExampleSnippet = "Only state precise facts (dates, names, numbers, citations) that are present verbatim in the provided Context. If not present, reply: 'Not in source.'",
                },
                "Missing Grounding" => new HallucinationFix
                {
                    Category = "grounding",
                    Severity = sev,
                    Issue = $"Missing Grounding={d.Score:F1}/10. No Context/Source block, quoted material, or citations found.",
                    Suggestion = "Add a grounding block at the top of the prompt that quotes the source material the model should rely on, e.g. 'Context: <paste excerpt here>'. Without it, the model will fill gaps from training data.",
                    ExampleSnippet = "Only use facts from the provided Context block. If the answer is not present, reply: 'Not in source.'",
                },
                "Speculative Framing" => new HallucinationFix
                {
                    Category = "scope",
                    Severity = sev,
                    Issue = $"Speculative Framing={d.Score:F1}/10. Prompt uses invention-inviting language ({firstEvidence}).",
                    Suggestion = "If you want creative/speculative output, mark it clearly as fiction. If you want facts, remove the speculative verbs ('imagine', 'invent', 'make up', 'plausible').",
                    ExampleSnippet = "Treat this as a factual task. Do not invent, fabricate, or fill in unknowns; if a value is unknown, say so explicitly.",
                },
                "No Uncertainty Allowance" => new HallucinationFix
                {
                    Category = "uncertainty",
                    Severity = sev,
                    Issue = $"No Uncertainty Allowance={d.Score:F1}/10. Prompt forbids or omits an 'I don't know' escape hatch.",
                    Suggestion = "Explicitly give the model permission to admit uncertainty. Models forced to always answer will fabricate.",
                    ExampleSnippet = "If you are not sure or cannot verify a claim, say 'I don't know' instead of guessing.",
                },
                "Open-Ended Recall" => new HallucinationFix
                {
                    Category = "scope",
                    Severity = sev,
                    Issue = $"Open-Ended Recall={d.Score:F1}/10. Prompt asks for broad recall about specific entities from training memory.",
                    Suggestion = "Narrow the scope to a specific verifiable sub-question, or supply reference material the model should summarize instead of recalling from memory.",
                    ExampleSnippet = "Limit the answer to facts that can be verified from the supplied material; do not extrapolate from training memory.",
                },
                "Temporal Drift" => new HallucinationFix
                {
                    Category = "temporal",
                    Severity = sev,
                    Issue = $"Temporal Drift={d.Score:F1}/10. Prompt asks about recent/current info without dated sources.",
                    Suggestion = "Supply dated source material, or explicitly acknowledge the model's knowledge cutoff and ask it to flag time-sensitive claims.",
                    ExampleSnippet = "Acknowledge your knowledge cutoff. If the question concerns events after that cutoff, say so explicitly rather than guessing.",
                },
                "Compound Claim Density" => new HallucinationFix
                {
                    Category = "scope",
                    Severity = sev,
                    Issue = $"Compound Claim Density={d.Score:F1}/10. Many independent factual asks are chained together; per-claim error probability compounds.",
                    Suggestion = "Break the prompt into smaller, single-claim sub-prompts so each fact can be verified individually.",
                    ExampleSnippet = "Answer each numbered question separately. For any single sub-question you cannot verify, reply: 'I don't know' for that item.",
                },
                _ => null,
            };
        }

        private static string SeverityFor(double score)
        {
            if (score >= 8) return "critical";
            if (score >= 7) return "high";
            if (score >= 6) return "medium";
            return "low";
        }

        private static string BuildMitigatedDraft(string prompt, List<HallucinationFix> fixes)
        {
            if (fixes.Count == 0) return prompt;

            var snippets = fixes
                .Select(f => f.ExampleSnippet)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToList();

            if (snippets.Count == 0) return prompt;

            var sb = new StringBuilder();
            sb.Append(prompt.TrimEnd());
            sb.Append("\n\n# Anti-Hallucination Guardrails\n");
            foreach (var s in snippets)
                sb.Append("- ").Append(s).Append('\n');
            return sb.ToString();
        }

        private static string LevelFor(double score)
        {
            if (score == 0) return "Empty";
            if (score <= 1.5) return "Minimal";
            if (score <= 3.0) return "Low";
            if (score <= 4.5) return "Moderate";
            if (score <= 6.5) return "Elevated";
            if (score <= 8.0) return "High";
            return "Severe";
        }

        // Delegates to the canonical char-based estimator (issue #191).
        // Note: previous local impl floored to a minimum of 1 for non-empty
        // input; TextAnalysisHelpers returns 0 for empty/null and ceil(len/4)
        // otherwise, which matches the rest of the library.
        private static int EstimateTokens(string s) =>
            TextAnalysisHelpers.EstimateTokens(s);

        private static List<string> Dedupe(List<string> items, int max)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var outList = new List<string>();
            foreach (var i in items)
            {
                if (seen.Add(i)) outList.Add(i);
                if (outList.Count >= max) break;
            }
            return outList;
        }
    }
}
