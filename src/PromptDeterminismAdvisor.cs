namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>Risk appetite for <see cref="PromptDeterminismAdvisor"/>.</summary>
    public enum DeterminismRiskAppetite
    {
        /// <summary>Stricter: appends a peer-review action whenever any P0/P1 finding exists.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient: trims P3 actions when at least one P0/P1 action exists.</summary>
        Aggressive,
    }

    /// <summary>Reproducibility verdict ladder.</summary>
    public enum DeterminismVerdict
    {
        /// <summary>No detected sources of non-determinism.</summary>
        Reproducible,
        /// <summary>Minor risks; outputs likely stable across runs.</summary>
        MostlyStable,
        /// <summary>Several risks; outputs may drift between runs.</summary>
        Drifty,
        /// <summary>High risk; reproducibility unlikely without changes.</summary>
        NonDeterministic,
        /// <summary>Critical: prompt actively encourages variation or pulls live data with no pin.</summary>
        HighlyVolatile,
    }

    /// <summary>Priority bucket for determinism findings and playbook actions.</summary>
    public enum DeterminismPriority
    {
        /// <summary>Blocking / immediate.</summary>
        P0,
        /// <summary>High priority.</summary>
        P1,
        /// <summary>Medium priority.</summary>
        P2,
        /// <summary>Advisory.</summary>
        P3,
    }

    /// <summary>Enumerated non-determinism modes.</summary>
    public enum DeterminismMode
    {
        /// <summary>Prompt explicitly asks the model to "be creative", "vary", "surprise me", etc.</summary>
        CreativityDirective,
        /// <summary>Prompt references current time / today / now without pinning a timestamp.</summary>
        ImplicitClock,
        /// <summary>Prompt invites randomness ("pick a random", "shuffle", "random sample").</summary>
        RandomnessDirective,
        /// <summary>Prompt asks the model to use external tools / web / live data with no snapshot policy.</summary>
        LiveDataLookup,
        /// <summary>Multiple acceptable outputs with no tie-breaker / canonical ordering rule.</summary>
        UnstableOrdering,
        /// <summary>No identifiers / versions pinned (model, prompt, dataset).</summary>
        UnpinnedVersions,
        /// <summary>Mentions sampling parameters like high temperature, top-p, top-k without pinning.</summary>
        SamplingDrift,
        /// <summary>Asks for "examples" / "a few" without a fixed count.</summary>
        UnboundedEnumeration,
        /// <summary>Asks the model to "remember" or "use prior session" without a memory contract.</summary>
        SessionMemoryAssumed,
        /// <summary>Output format that re-orders silently (set, dict-without-key-sort, JSON without sorted keys).</summary>
        UnorderedSerialization,
    }

    /// <summary>Optional environment context for determinism analysis.</summary>
    public sealed class DeterminismContext
    {
        /// <summary>True if a fixed sampling seed is pinned upstream.</summary>
        public bool SeedPinned { get; init; }

        /// <summary>True if the runner pins a specific model version (e.g. "gpt-4o-2024-08-06").</summary>
        public bool ModelVersionPinned { get; init; }

        /// <summary>True if the runner pins temperature/top-p (e.g. temperature == 0).</summary>
        public bool SamplingPinned { get; init; }

        /// <summary>True if tools / browsing are enabled.</summary>
        public bool ToolsAvailable { get; init; }

        /// <summary>True if the caller relies on the response being byte-stable across runs.</summary>
        public bool RequireByteStability { get; init; }

        /// <summary>Risk appetite knob.</summary>
        public DeterminismRiskAppetite RiskAppetite { get; init; } = DeterminismRiskAppetite.Balanced;
    }

    /// <summary>A single determinism finding.</summary>
    public sealed class DeterminismFinding
    {
        /// <summary>Mode code.</summary>
        public DeterminismMode Mode { get; internal set; }

        /// <summary>Severity 0..100.</summary>
        public int Severity { get; internal set; }

        /// <summary>Priority bucket.</summary>
        public DeterminismPriority Priority { get; internal set; }

        /// <summary>Short reason text.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Optional prompt snippet.</summary>
        public string Snippet { get; internal set; } = "";
    }

    /// <summary>A single playbook action.</summary>
    public sealed class DeterminismAction
    {
        /// <summary>Stable id.</summary>
        public string Id { get; internal set; } = "";

        /// <summary>Priority bucket.</summary>
        public DeterminismPriority Priority { get; internal set; }

        /// <summary>Short label.</summary>
        public string Label { get; internal set; } = "";

        /// <summary>Reason / rationale.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Owner role.</summary>
        public string Owner { get; internal set; } = "prompt_author";

        /// <summary>Modes this action addresses, sorted by name.</summary>
        public IReadOnlyList<string> RelatedFindings { get; internal set; } = Array.Empty<string>();

        /// <summary>Suggested concrete value or change.</summary>
        public string? SuggestedValue { get; internal set; }
    }

    /// <summary>Report from <see cref="PromptDeterminismAdvisor.Analyze"/>.</summary>
    public sealed class DeterminismReport
    {
        /// <summary>Verdict.</summary>
        public DeterminismVerdict Verdict { get; internal set; }

        /// <summary>Letter grade A..F.</summary>
        public string Grade { get; internal set; } = "A";

        /// <summary>Reproducibility score 0..100 (100 = fully reproducible).</summary>
        public int ReproducibilityScore { get; internal set; }

        /// <summary>Findings (priority asc, severity desc, mode-name asc).</summary>
        public IReadOnlyList<DeterminismFinding> Findings { get; internal set; } = Array.Empty<DeterminismFinding>();

        /// <summary>Playbook actions (priority asc, id asc).</summary>
        public IReadOnlyList<DeterminismAction> Playbook { get; internal set; } = Array.Empty<DeterminismAction>();

        /// <summary>Sorted insights.</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();

        /// <summary>One-line headline.</summary>
        public string Headline { get; internal set; } = "";

        /// <summary>Prompt augmented with a deterministic-safeguards block (only for P0/P1 actions).</summary>
        public string PinnedDraft { get; internal set; } = "";
    }

    /// <summary>
    /// 11th agentic sibling. Detects sources of non-determinism in a prompt (creativity
    /// directives, implicit clocks, randomness, live data, unstable ordering, unpinned
    /// versions, sampling drift, unbounded enumeration, session-memory assumptions,
    /// unordered serialization) and produces a deterministic playbook + pinned draft.
    /// Pure, no I/O, no clock.
    /// </summary>
    public sealed class PromptDeterminismAdvisor
    {
        private static readonly string[] CreativityMarkers = {
            "be creative", "surprise me", "be original", "be inventive", "be unique",
            "vary your response", "vary the output", "different each time", "novel response",
            "creative liberty", "feel free to invent",
        };
        private static readonly string[] ClockMarkers = {
            "today", "right now", "current time", "as of now", "this morning",
            "this evening", "this week", "this month", "this year", "latest news",
            "currently", "at present",
        };
        private static readonly string[] RandomnessMarkers = {
            "pick a random", "choose at random", "random sample", "shuffle",
            "in random order", "pseudo-random", "rng", "roll a die", "flip a coin",
            "randomly select",
        };
        private static readonly string[] LiveDataMarkers = {
            "browse the web", "search the web", "look up online", "fetch the page",
            "open the url", "use the internet", "use your browser", "real-time",
            "current price", "stock price", "weather right now",
        };
        private static readonly string[] OrderingMarkers = {
            "list", "enumerate", "show all", "rank", "top ",
        };
        private static readonly string[] OrderingTieBreakers = {
            "alphabetically", "sort by", "in alphabetical", "ascending", "descending",
            "lexicographic", "ordered by", "deterministic order", "stable order",
            "by id", "by name", "by date",
        };
        private static readonly string[] VersionPinMarkers = {
            "model version", "prompt version", "@", "v1.", "v2.", "v3.",
            "dataset version", "snapshot of", "frozen", "pinned to",
        };
        private static readonly string[] SamplingMarkers = {
            "temperature", "top-p", "top_k", "top-k", "sampling", "nucleus",
        };
        private static readonly string[] EnumerationMarkers = {
            "a few", "several", "some examples", "a couple of", "many examples",
            "as many as you can",
        };
        private static readonly string[] SessionMemoryMarkers = {
            "remember from before", "as we discussed", "from last time",
            "use what you learned", "carry over", "as I mentioned earlier",
        };
        private static readonly string[] UnorderedSerializationMarkers = {
            "as a set", "unordered list", "json object", "yaml mapping",
        };
        private static readonly string[] SortedKeysMarkers = {
            "sorted keys", "ordered keys", "deterministic json", "stable serialization",
        };

        private static readonly Regex BoundedCountRegex = new(
            @"\b(exactly|provide|return|list)\s+(\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NumericCountRegex = new(
            @"\b\d+\s+(items?|examples?|results?|entries|bullets?|lines?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Analyze a prompt for determinism / reproducibility risks.</summary>
        /// <param name="prompt">Prompt text (non-null).</param>
        /// <param name="context">Optional environment context.</param>
        /// <returns>A <see cref="DeterminismReport"/>.</returns>
        public DeterminismReport Analyze(string prompt, DeterminismContext? context = null)
        {
            if (prompt is null) throw new ArgumentNullException(nameof(prompt));
            context ??= new DeterminismContext();
            var lower = prompt.ToLowerInvariant();
            var raw = new List<DeterminismFinding>();

            // 1. CreativityDirective
            if (Contains(lower, CreativityMarkers))
            {
                raw.Add(F(DeterminismMode.CreativityDirective, 70,
                    "Prompt explicitly invites variation/creativity.",
                    Preview(prompt, FirstMatch(lower, CreativityMarkers))));
            }

            // 2. ImplicitClock
            if (Contains(lower, ClockMarkers) && !lower.Contains("as of 20"))
            {
                raw.Add(F(DeterminismMode.ImplicitClock, 60,
                    "Time reference without a pinned 'as of YYYY-MM-DD' anchor.",
                    Preview(prompt, FirstMatch(lower, ClockMarkers))));
            }

            // 3. RandomnessDirective
            if (Contains(lower, RandomnessMarkers))
            {
                raw.Add(F(DeterminismMode.RandomnessDirective, 75,
                    "Prompt asks for randomness without a seed contract.",
                    Preview(prompt, FirstMatch(lower, RandomnessMarkers))));
            }

            // 4. LiveDataLookup
            if (Contains(lower, LiveDataMarkers) || (context.ToolsAvailable && lower.Contains("look up")))
            {
                raw.Add(F(DeterminismMode.LiveDataLookup, 65,
                    "Prompt fetches live data; outputs depend on external state.",
                    Preview(prompt, FirstMatch(lower, LiveDataMarkers))));
            }

            // 5. UnstableOrdering
            if (Contains(lower, OrderingMarkers) && !Contains(lower, OrderingTieBreakers))
            {
                raw.Add(F(DeterminismMode.UnstableOrdering, 40,
                    "Enumeration without an explicit tie-breaker or sort key.",
                    Preview(prompt, FirstMatch(lower, OrderingMarkers))));
            }

            // 6. UnpinnedVersions (model + prompt)
            var promptMentionsVersion = Contains(lower, VersionPinMarkers);
            if (!context.ModelVersionPinned && !promptMentionsVersion)
            {
                raw.Add(F(DeterminismMode.UnpinnedVersions, 55,
                    "No model/prompt/dataset version pin detected."));
            }

            // 7. SamplingDrift
            var samplingMentioned = Contains(lower, SamplingMarkers);
            var pinsTempZero = lower.Contains("temperature 0") || lower.Contains("temperature=0") || lower.Contains("temperature: 0");
            if (!context.SamplingPinned && !pinsTempZero)
            {
                var sev = samplingMentioned ? 60 : 35;
                raw.Add(F(DeterminismMode.SamplingDrift, sev,
                    samplingMentioned
                        ? "Sampling parameters mentioned but not pinned (e.g. temperature=0)."
                        : "Sampling parameters not pinned; default temperature varies output."));
            }

            // 8. UnboundedEnumeration
            var hasBoundedCount = BoundedCountRegex.IsMatch(prompt) || NumericCountRegex.IsMatch(prompt);
            if (Contains(lower, EnumerationMarkers) && !hasBoundedCount)
            {
                raw.Add(F(DeterminismMode.UnboundedEnumeration, 35,
                    "Vague quantifier ('a few', 'several') without a fixed count."));
            }

            // 9. SessionMemoryAssumed
            if (Contains(lower, SessionMemoryMarkers))
            {
                raw.Add(F(DeterminismMode.SessionMemoryAssumed, 50,
                    "Refers to prior session/memory without an explicit memory contract."));
            }

            // 10. UnorderedSerialization
            if (Contains(lower, UnorderedSerializationMarkers) && !Contains(lower, SortedKeysMarkers))
            {
                raw.Add(F(DeterminismMode.UnorderedSerialization, 45,
                    "Set/mapping output requested with no key-ordering rule."));
            }

            // Priority assignment
            foreach (var f in raw) f.Priority = ComputePriority(f, context);

            // Byte-stability hard fails
            if (context.RequireByteStability)
            {
                foreach (var f in raw)
                {
                    if (f.Mode == DeterminismMode.CreativityDirective
                        || f.Mode == DeterminismMode.RandomnessDirective
                        || f.Mode == DeterminismMode.LiveDataLookup
                        || f.Mode == DeterminismMode.ImplicitClock)
                    {
                        f.Priority = DeterminismPriority.P0;
                        f.Severity = Math.Max(f.Severity, 80);
                    }
                }
            }

            // Score: start at 100, subtract weighted severities, clamp
            int penalty = 0;
            foreach (var f in raw)
            {
                int w = f.Priority switch
                {
                    DeterminismPriority.P0 => 25,
                    DeterminismPriority.P1 => 15,
                    DeterminismPriority.P2 => 8,
                    _ => 3,
                };
                penalty += (f.Severity * w) / 100;
            }
            int score = Math.Max(0, Math.Min(100, 100 - penalty));

            // Context boosts
            if (context.SeedPinned) score = Math.Min(100, score + 5);
            if (context.ModelVersionPinned) score = Math.Min(100, score + 5);
            if (context.SamplingPinned) score = Math.Min(100, score + 5);

            // Verdict
            DeterminismVerdict verdict;
            bool hasP0 = raw.Any(f => f.Priority == DeterminismPriority.P0);
            bool encouragesVariation = raw.Any(f =>
                f.Mode == DeterminismMode.CreativityDirective
                || f.Mode == DeterminismMode.RandomnessDirective);
            if (hasP0 && encouragesVariation) verdict = DeterminismVerdict.HighlyVolatile;
            else if (score >= 90) verdict = DeterminismVerdict.Reproducible;
            else if (score >= 75) verdict = DeterminismVerdict.MostlyStable;
            else if (score >= 50) verdict = DeterminismVerdict.Drifty;
            else verdict = DeterminismVerdict.NonDeterministic;

            // Grade
            string grade =
                verdict == DeterminismVerdict.HighlyVolatile ? "F"
                : score >= 90 ? "A"
                : score >= 75 ? "B"
                : score >= 60 ? "C"
                : score >= 40 ? "D"
                : "F";

            // Sort findings
            var findings = raw
                .OrderBy(f => (int)f.Priority)
                .ThenByDescending(f => f.Severity)
                .ThenBy(f => f.Mode.ToString(), StringComparer.Ordinal)
                .ToList();

            // Playbook
            var playbook = BuildPlaybook(findings, context);

            // Insights
            var insights = BuildInsights(findings, context, score);

            // Pinned draft
            var pinned = BuildPinnedDraft(prompt, playbook);

            return new DeterminismReport
            {
                Verdict = verdict,
                Grade = grade,
                ReproducibilityScore = score,
                Findings = findings,
                Playbook = playbook,
                Insights = insights,
                Headline = $"{verdict} (Grade {grade}, Score {score})",
                PinnedDraft = pinned,
            };
        }

        /// <summary>Render the report as plain text.</summary>
        public string ToText(DeterminismReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var sb = new StringBuilder();
            sb.AppendLine(report.Headline);
            sb.AppendLine();
            sb.AppendLine($"Findings ({report.Findings.Count}):");
            foreach (var f in report.Findings)
                sb.AppendLine($"  - [{f.Priority}] {f.Mode} (sev {f.Severity}): {f.Reason}");
            sb.AppendLine();
            sb.AppendLine($"Playbook ({report.Playbook.Count}):");
            foreach (var a in report.Playbook)
            {
                sb.AppendLine($"  - [{a.Priority}] {a.Id}: {a.Label} — {a.Reason}");
                if (!string.IsNullOrEmpty(a.SuggestedValue))
                    sb.AppendLine($"      suggestion: {a.SuggestedValue}");
            }
            sb.AppendLine();
            sb.AppendLine($"Insights ({report.Insights.Count}):");
            foreach (var i in report.Insights) sb.AppendLine($"  - {i}");
            return sb.ToString();
        }

        /// <summary>Render the report as Markdown.</summary>
        public string ToMarkdown(DeterminismReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var sb = new StringBuilder();
            sb.AppendLine($"# PromptDeterminismAdvisor — {report.Headline}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Verdict: **{report.Verdict}**");
            sb.AppendLine($"- Grade: **{report.Grade}**");
            sb.AppendLine($"- ReproducibilityScore: **{report.ReproducibilityScore}**");
            sb.AppendLine();
            sb.AppendLine("## Findings");
            if (report.Findings.Count == 0) sb.AppendLine("_None_");
            foreach (var f in report.Findings)
                sb.AppendLine($"- **{f.Mode}** ({f.Priority}, sev {f.Severity}): {f.Reason}");
            sb.AppendLine();
            sb.AppendLine("## Playbook");
            if (report.Playbook.Count == 0) sb.AppendLine("_None_");
            foreach (var a in report.Playbook)
            {
                sb.AppendLine($"- **{a.Id}** ({a.Priority}, owner: {a.Owner}) — {a.Label}");
                sb.AppendLine($"  - Reason: {a.Reason}");
                if (!string.IsNullOrEmpty(a.SuggestedValue))
                    sb.AppendLine($"  - Suggestion: {a.SuggestedValue}");
            }
            sb.AppendLine();
            sb.AppendLine("## Insights");
            foreach (var i in report.Insights) sb.AppendLine($"- {i}");
            return sb.ToString();
        }

        /// <summary>Render the report as deterministic JSON (sorted keys).</summary>
        public string ToJson(DeterminismReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var doc = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["findings"] = report.Findings.Select(f => (object)new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode"] = f.Mode.ToString(),
                    ["priority"] = f.Priority.ToString(),
                    ["reason"] = f.Reason,
                    ["severity"] = f.Severity,
                    ["snippet"] = f.Snippet ?? "",
                }).ToList(),
                ["grade"] = report.Grade,
                ["headline"] = report.Headline,
                ["insights"] = report.Insights.ToList(),
                ["pinnedDraft"] = report.PinnedDraft,
                ["playbook"] = report.Playbook.Select(a => (object)new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = a.Id,
                    ["label"] = a.Label,
                    ["owner"] = a.Owner,
                    ["priority"] = a.Priority.ToString(),
                    ["reason"] = a.Reason,
                    ["relatedFindings"] = a.RelatedFindings.ToList(),
                    ["suggestedValue"] = a.SuggestedValue,
                }).ToList(),
                ["reproducibilityScore"] = report.ReproducibilityScore,
                ["verdict"] = report.Verdict.ToString(),
            };
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }

        private static DeterminismPriority ComputePriority(DeterminismFinding f, DeterminismContext ctx)
        {
            if (f.Mode == DeterminismMode.RandomnessDirective && !ctx.SeedPinned) return DeterminismPriority.P0;
            if (f.Mode == DeterminismMode.LiveDataLookup && ctx.ToolsAvailable) return DeterminismPriority.P0;
            if (f.Mode == DeterminismMode.CreativityDirective) return DeterminismPriority.P1;
            if (f.Severity >= 55) return DeterminismPriority.P1;
            if (f.Severity >= 35) return DeterminismPriority.P2;
            return DeterminismPriority.P3;
        }

        private static IReadOnlyList<DeterminismAction> BuildPlaybook(
            IReadOnlyList<DeterminismFinding> findings, DeterminismContext ctx)
        {
            var byMode = findings.ToLookup(f => f.Mode);
            var actions = new List<DeterminismAction>();

            void Add(string id, DeterminismPriority p, string label, string reason, string owner,
                     IEnumerable<DeterminismMode> related, string? suggested = null)
            {
                actions.Add(new DeterminismAction
                {
                    Id = id,
                    Priority = p,
                    Label = label,
                    Reason = reason,
                    Owner = owner,
                    RelatedFindings = related.Select(r => r.ToString()).OrderBy(s => s, StringComparer.Ordinal).ToList(),
                    SuggestedValue = suggested,
                });
            }

            if (byMode[DeterminismMode.RandomnessDirective].Any())
            {
                Add("PinSeed", DeterminismPriority.P0, "Pin a sampling seed",
                    "Randomness directives need a seed contract for reproducibility.",
                    "platform", new[] { DeterminismMode.RandomnessDirective },
                    "Add: 'Use seed=42 for any random selection.'");
            }
            if (byMode[DeterminismMode.LiveDataLookup].Any() && ctx.ToolsAvailable)
            {
                Add("SnapshotLiveData", DeterminismPriority.P0, "Snapshot live data inputs",
                    "Live lookups make outputs depend on external state at call time.",
                    "platform", new[] { DeterminismMode.LiveDataLookup },
                    "Cache the fetched payload and pass it inline.");
            }
            if (byMode[DeterminismMode.CreativityDirective].Any())
            {
                Add("RemoveCreativityDirective", DeterminismPriority.P1, "Remove creativity directives",
                    "Phrases like 'be creative' or 'surprise me' actively encourage variation.",
                    "prompt_author", new[] { DeterminismMode.CreativityDirective },
                    "Replace with a concrete output contract.");
            }
            if (byMode[DeterminismMode.ImplicitClock].Any())
            {
                Add("PinTimestamp", DeterminismPriority.P1, "Pin an 'as of' timestamp",
                    "Time references make the output depend on the wall clock.",
                    "prompt_author", new[] { DeterminismMode.ImplicitClock },
                    "Add: 'As of YYYY-MM-DD ...'");
            }
            if (byMode[DeterminismMode.SamplingDrift].Any() && !ctx.SamplingPinned)
            {
                Add("PinSamplingParameters", DeterminismPriority.P1, "Pin temperature / top-p",
                    "Default sampling parameters cause run-to-run drift.",
                    "platform", new[] { DeterminismMode.SamplingDrift },
                    "temperature=0, top_p=1");
            }
            if (byMode[DeterminismMode.UnpinnedVersions].Any() && !ctx.ModelVersionPinned)
            {
                Add("PinModelVersion", DeterminismPriority.P1, "Pin the model version",
                    "Auto-rolling model aliases silently change behaviour.",
                    "platform", new[] { DeterminismMode.UnpinnedVersions },
                    "Replace 'latest' alias with a dated version string.");
            }
            if (byMode[DeterminismMode.UnstableOrdering].Any())
            {
                Add("AddSortKey", DeterminismPriority.P2, "Add a deterministic sort key",
                    "Enumerations without a tie-breaker reorder between runs.",
                    "prompt_author", new[] { DeterminismMode.UnstableOrdering },
                    "Add: 'Order results alphabetically by name.'");
            }
            if (byMode[DeterminismMode.UnboundedEnumeration].Any())
            {
                Add("FixEnumerationCount", DeterminismPriority.P2, "Fix the enumeration count",
                    "Vague quantifiers ('a few', 'several') vary between runs.",
                    "prompt_author", new[] { DeterminismMode.UnboundedEnumeration },
                    "Replace with an exact integer (e.g. 'exactly 3').");
            }
            if (byMode[DeterminismMode.UnorderedSerialization].Any())
            {
                Add("RequireSortedKeys", DeterminismPriority.P2, "Require sorted JSON/YAML keys",
                    "Default serializers may emit keys in insertion order.",
                    "prompt_author", new[] { DeterminismMode.UnorderedSerialization },
                    "Add: 'Emit JSON with sorted keys for deterministic output.'");
            }
            if (byMode[DeterminismMode.SessionMemoryAssumed].Any())
            {
                Add("InlinePriorContext", DeterminismPriority.P3, "Inline the prior context",
                    "Implicit session memory makes the prompt non-portable.",
                    "prompt_author", new[] { DeterminismMode.SessionMemoryAssumed });
            }

            // Cautious: append a peer review when there are any P0/P1
            if (ctx.RiskAppetite == DeterminismRiskAppetite.Cautious
                && actions.Any(a => a.Priority == DeterminismPriority.P0 || a.Priority == DeterminismPriority.P1))
            {
                Add("ScheduleReproReview", DeterminismPriority.P2, "Schedule a reproducibility review",
                    "Cautious appetite + P0/P1 finding triggers a peer review.",
                    "platform", Array.Empty<DeterminismMode>());
            }

            // Aggressive: trim P3 if P0/P1 exist
            if (ctx.RiskAppetite == DeterminismRiskAppetite.Aggressive
                && actions.Any(a => a.Priority == DeterminismPriority.P0 || a.Priority == DeterminismPriority.P1))
            {
                actions.RemoveAll(a => a.Priority == DeterminismPriority.P3);
            }

            // Ok action if nothing else fired
            if (actions.Count == 0)
            {
                Add("DETERMINISM_OK", DeterminismPriority.P3, "No determinism risks detected",
                    "No matching modes; prompt looks reproducible.",
                    "prompt_author", Array.Empty<DeterminismMode>());
            }

            return actions
                .OrderBy(a => (int)a.Priority)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static IReadOnlyList<string> BuildInsights(
            IReadOnlyList<DeterminismFinding> findings, DeterminismContext ctx, int score)
        {
            var ins = new SortedSet<string>(StringComparer.Ordinal);
            if (findings.Count == 0)
            {
                ins.Add("No determinism risks were detected in the prompt.");
            }
            else
            {
                ins.Add($"Detected {findings.Count} non-determinism mode(s); top priority is {findings.Min(f => f.Priority)}.");
            }
            if (ctx.SeedPinned) ins.Add("Sampling seed is pinned upstream — random-selection risk is bounded.");
            if (ctx.SamplingPinned) ins.Add("Sampling parameters are pinned upstream.");
            if (ctx.ModelVersionPinned) ins.Add("Model version is pinned upstream.");
            if (ctx.RequireByteStability) ins.Add("Caller requires byte-stable output — clock/randomness/live findings escalate to P0.");
            ins.Add($"Reproducibility score: {score}/100.");
            return ins.ToList();
        }

        private static string BuildPinnedDraft(string prompt, IReadOnlyList<DeterminismAction> playbook)
        {
            var highPriority = playbook
                .Where(a => a.Priority == DeterminismPriority.P0 || a.Priority == DeterminismPriority.P1)
                .ToList();
            if (highPriority.Count == 0) return prompt;
            var sb = new StringBuilder();
            sb.Append(prompt);
            if (!prompt.EndsWith("\n", StringComparison.Ordinal)) sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("# DETERMINISM_SAFEGUARDS (auto-inserted by PromptDeterminismAdvisor)");
            foreach (var a in highPriority)
            {
                sb.AppendLine($"# - [{a.Priority}] {a.Id}: {a.Label}");
                if (!string.IsNullOrEmpty(a.SuggestedValue))
                    sb.AppendLine($"#   suggestion: {a.SuggestedValue}");
            }
            return sb.ToString();
        }

        private static DeterminismFinding F(DeterminismMode mode, int severity, string reason, string snippet = "")
            => new() { Mode = mode, Severity = severity, Reason = reason, Snippet = snippet };

        private static bool Contains(string lower, IEnumerable<string> needles)
        {
            foreach (var n in needles) if (lower.Contains(n, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int FirstMatch(string lower, IEnumerable<string> needles)
        {
            int best = -1;
            foreach (var n in needles)
            {
                int i = lower.IndexOf(n, StringComparison.Ordinal);
                if (i >= 0 && (best < 0 || i < best)) best = i;
            }
            return best < 0 ? 0 : best;
        }

        private static string Preview(string prompt, int offset)
        {
            if (string.IsNullOrEmpty(prompt)) return "";
            int start = Math.Max(0, offset);
            int len = Math.Min(80, prompt.Length - start);
            return prompt.Substring(start, len).Replace('\n', ' ').Replace('\r', ' ').Trim();
        }
    }
}
