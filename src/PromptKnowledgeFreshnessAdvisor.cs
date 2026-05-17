namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>Verdict bucket for an individual freshness finding (and the overall report).</summary>
    public enum FreshnessRiskLevel
    {
        /// <summary>No staleness signals detected.</summary>
        Fresh,
        /// <summary>Stale language is present but anchored to a recent explicit as-of marker.</summary>
        Anchored,
        /// <summary>Vague temporal language ("today", "now", "recent") without an anchor.</summary>
        StaleLanguage,
        /// <summary>A literal year is embedded in the prompt.</summary>
        HardcodedYear,
        /// <summary>Date-sensitive content with no retrieval grounding declared.</summary>
        MissingRetrieval,
        /// <summary>Anchor or content is well past a safe freshness window.</summary>
        CriticallyStale,
    }

    /// <summary>Priority bucket for an emitted action.</summary>
    public enum FreshnessPriority
    {
        /// <summary>Immediate / blocking action.</summary>
        P0,
        /// <summary>High-priority action.</summary>
        P1,
        /// <summary>Medium-priority action.</summary>
        P2,
        /// <summary>Advisory action.</summary>
        P3,
    }

    /// <summary>Domain hint used to escalate staleness risk for time-sensitive subject matter.</summary>
    public enum FreshnessDomain
    {
        /// <summary>Generic / non-time-sensitive content.</summary>
        General,
        /// <summary>News and current events.</summary>
        News,
        /// <summary>Pricing, quotes, market rates.</summary>
        Pricing,
        /// <summary>Laws, regulations, compliance.</summary>
        Regulation,
        /// <summary>Software versions, APIs, libraries.</summary>
        Software,
        /// <summary>Medical / clinical guidance.</summary>
        Medical,
        /// <summary>Sports schedules / scores.</summary>
        Sports,
        /// <summary>Finance / markets.</summary>
        Finance,
    }

    /// <summary>One detected freshness risk in a prompt.</summary>
    public sealed record FreshnessFinding(
        string Code,
        string Span,
        int Severity,
        string Reason,
        FreshnessRiskLevel Verdict);

    /// <summary>One recommended action emitted by the advisor.</summary>
    public sealed record FreshnessAction(
        string Code,
        FreshnessPriority Priority,
        string Owner,
        string Reason,
        string Snippet);

    /// <summary>Caller-supplied context describing how the prompt is wired up.</summary>
    public sealed record FreshnessContext(
        FreshnessDomain Domain = FreshnessDomain.General,
        bool HasRetrievalGrounding = false,
        DateTime? AsOf = null,
        DateTime? KnowledgeCutoff = null,
        int KbRefreshDays = 0,
        string? RiskAppetite = "balanced");

    /// <summary>Full report from <see cref="PromptKnowledgeFreshnessAdvisor"/>.</summary>
    public sealed class FreshnessReport
    {
        /// <summary>First ~80 chars of the analyzed prompt (for the renderers).</summary>
        public string PromptPreview { get; init; } = "";

        /// <summary>Aggregate staleness score 0-100 (higher = more stale risk).</summary>
        public int OverallScore { get; init; }

        /// <summary>A-F letter grade.</summary>
        public char Grade { get; init; } = 'A';

        /// <summary>Overall verdict (highest-severity finding's verdict, or Fresh).</summary>
        public FreshnessRiskLevel Verdict { get; init; } = FreshnessRiskLevel.Fresh;

        /// <summary>Individual findings, sorted by severity desc then code asc.</summary>
        public IReadOnlyList<FreshnessFinding> Findings { get; init; } = Array.Empty<FreshnessFinding>();

        /// <summary>Recommended actions, sorted by priority then code.</summary>
        public IReadOnlyList<FreshnessAction> Playbook { get; init; } = Array.Empty<FreshnessAction>();

        /// <summary>Cross-finding insights (≤5 lines).</summary>
        public IReadOnlyList<string> Insights { get; init; } = Array.Empty<string>();

        /// <summary>Generation timestamp (from injected nowProvider).</summary>
        public DateTime GeneratedAt { get; init; }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>Plain-text render.</summary>
        public string Render()
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(Verdict).Append("] score ").Append(OverallScore).Append("/100 grade ")
              .Append(Grade).Append(" — ").Append(Findings.Count).Append(" findings, ")
              .Append(Playbook.Count).Append(" actions").AppendLine();
            if (!string.IsNullOrEmpty(PromptPreview))
            {
                sb.Append("Prompt: ").AppendLine(PromptPreview);
            }
            if (Findings.Count > 0)
            {
                sb.AppendLine("Findings:");
                foreach (var f in Findings)
                {
                    sb.Append("  - [").Append(f.Severity).Append("] ").Append(f.Code).Append(" :: ")
                      .Append(f.Reason);
                    if (!string.IsNullOrEmpty(f.Span))
                    {
                        sb.Append(" (\"").Append(f.Span).Append("\")");
                    }
                    sb.AppendLine();
                }
            }
            if (Playbook.Count > 0)
            {
                sb.AppendLine("Playbook:");
                foreach (var a in Playbook)
                {
                    sb.Append("  - [").Append(a.Priority).Append("] ").Append(a.Code)
                      .Append(" (owner=").Append(a.Owner).Append(") :: ").AppendLine(a.Reason);
                    if (!string.IsNullOrEmpty(a.Snippet))
                    {
                        sb.Append("      snippet: ").AppendLine(a.Snippet);
                    }
                }
            }
            if (Insights.Count > 0)
            {
                sb.AppendLine("Insights:");
                foreach (var i in Insights)
                {
                    sb.Append("  - ").AppendLine(i);
                }
            }
            return sb.ToString();
        }

        /// <summary>Markdown render.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## PromptKnowledgeFreshnessAdvisor");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|---|---|");
            sb.Append("| Verdict | ").Append(Verdict).AppendLine(" |");
            sb.Append("| Score | ").Append(OverallScore).AppendLine("/100 |");
            sb.Append("| Grade | ").Append(Grade).AppendLine(" |");
            sb.Append("| Findings | ").Append(Findings.Count).AppendLine(" |");
            sb.Append("| Actions | ").Append(Playbook.Count).AppendLine(" |");
            sb.Append("| GeneratedAt | ").Append(GeneratedAt.ToString("O")).AppendLine(" |");
            sb.AppendLine();
            sb.AppendLine("### Findings");
            if (Findings.Count == 0)
            {
                sb.AppendLine("_None._");
            }
            else
            {
                sb.AppendLine("| Severity | Code | Verdict | Reason | Span |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var f in Findings)
                {
                    sb.Append("| ").Append(f.Severity)
                      .Append(" | ").Append(f.Code)
                      .Append(" | ").Append(f.Verdict)
                      .Append(" | ").Append(EscapeMd(f.Reason))
                      .Append(" | ").Append(EscapeMd(f.Span)).AppendLine(" |");
                }
            }
            sb.AppendLine();
            sb.AppendLine("### Playbook");
            if (Playbook.Count == 0)
            {
                sb.AppendLine("_None._");
            }
            else
            {
                foreach (var a in Playbook)
                {
                    sb.Append("- **[").Append(a.Priority).Append("] ").Append(a.Code)
                      .Append("** (owner: `").Append(a.Owner).Append("`) — ").AppendLine(EscapeMd(a.Reason));
                    if (!string.IsNullOrEmpty(a.Snippet))
                    {
                        sb.Append("  > ").AppendLine(EscapeMd(a.Snippet));
                    }
                }
            }
            sb.AppendLine();
            sb.AppendLine("### Insights");
            if (Insights.Count == 0)
            {
                sb.AppendLine("_None._");
            }
            else
            {
                foreach (var i in Insights)
                {
                    sb.Append("- ").AppendLine(i);
                }
            }
            return sb.ToString();
        }

        /// <summary>Deterministic JSON render (sorted lists, fixed options).</summary>
        public string ToJson() => JsonSerializer.Serialize(new
        {
            promptPreview = PromptPreview,
            overallScore = OverallScore,
            grade = Grade.ToString(),
            verdict = Verdict,
            findings = Findings,
            playbook = Playbook,
            insights = Insights,
            generatedAt = GeneratedAt.ToString("O"),
        }, JsonOpts);

        private static string EscapeMd(string? s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
    }

    /// <summary>
    /// Agentic temporal-staleness analyzer for prompt templates.
    /// Detects hardcoded years, stale language, missing as-of anchors, missing retrieval
    /// grounding for date-sensitive domains, and exposure of model training cutoffs.
    /// Emits a ranked, deduped P0-P3 mitigation playbook with paste-ready snippets.
    /// Companion to <see cref="PromptHallucinationRiskScorer"/> and <see cref="PromptDefenseAdvisor"/>.
    /// </summary>
    public sealed class PromptKnowledgeFreshnessAdvisor
    {
        private readonly Func<DateTime> _now;

        private static readonly Regex YearRx = new(@"\b(19[9]\d|20\d{2}|21\d{2})\b", RegexOptions.Compiled);

        private static readonly string[] StalePhrases =
        {
            "currently", "current", "today", "latest", "now",
            "recent", "recently", "at the moment", "as of now",
        };

        private static readonly string[] CutoffPhrases =
        {
            "my training data", "my knowledge", "as a language model",
            "as an ai language model", "i was trained", "knowledge cutoff",
            "i don't have access to real", "i do not have access to real",
        };

        private static readonly Regex AnchorRx = new(
            @"\b(as of|dated|valid as of)\b|\b\d{4}-\d{2}-\d{2}\b|" +
            @"\b(jan(uary)?|feb(ruary)?|mar(ch)?|apr(il)?|may|jun(e)?|jul(y)?|aug(ust)?|sep(t(ember)?)?|oct(ober)?|nov(ember)?|dec(ember)?)\s+\d{4}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Create an advisor. Inject <paramref name="nowProvider"/> for deterministic tests.</summary>
        public PromptKnowledgeFreshnessAdvisor(Func<DateTime>? nowProvider = null)
        {
            _now = nowProvider ?? (() => DateTime.UtcNow);
        }

        /// <summary>Analyze a prompt under the supplied context and return a freshness report.</summary>
        public FreshnessReport Analyze(string? prompt, FreshnessContext? context = null)
        {
            var ctx = context ?? new FreshnessContext();
            var now = _now();
            var preview = (prompt ?? string.Empty).Trim();
            if (preview.Length > 80) preview = preview.Substring(0, 77) + "...";

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new FreshnessReport
                {
                    PromptPreview = preview,
                    OverallScore = 0,
                    Grade = 'A',
                    Verdict = FreshnessRiskLevel.Fresh,
                    GeneratedAt = now,
                };
            }

            var findings = new List<FreshnessFinding>();
            var lower = prompt.ToLowerInvariant();
            bool hasAnchor = AnchorRx.IsMatch(prompt);
            bool asOfRecent = ctx.AsOf.HasValue && (now - ctx.AsOf.Value).TotalDays <= 30;

            // 1. HARDCODED_YEAR
            var yearMatches = YearRx.Matches(prompt);
            var seenYears = new HashSet<int>();
            foreach (Match m in yearMatches)
            {
                if (!int.TryParse(m.Value, out var y)) continue;
                if (y < 1995 || y > now.Year + 5) continue;
                if (!seenYears.Add(y)) continue;
                // Future-looking years (>= current year) are not "stale-hardcoded".
                if (y >= now.Year) continue;
                int age = now.Year - y;
                int severity = Math.Min(75, 20 + age * 5);
                findings.Add(new FreshnessFinding(
                    "HARDCODED_YEAR",
                    m.Value,
                    severity,
                    $"Literal year {y} is {age} year(s) old and will not auto-update.",
                    FreshnessRiskLevel.HardcodedYear));
            }

            // 2. STALE_PHRASE
            var stalePhraseHits = new List<string>();
            foreach (var phrase in StalePhrases)
            {
                var rx = new Regex(@"\b" + Regex.Escape(phrase) + @"\b", RegexOptions.IgnoreCase);
                if (rx.IsMatch(lower)) stalePhraseHits.Add(phrase);
            }
            if (stalePhraseHits.Count > 0)
            {
                if (asOfRecent)
                {
                    findings.Add(new FreshnessFinding(
                        "STALE_PHRASE_ANCHORED",
                        string.Join(",", stalePhraseHits),
                        Math.Min(15, 5 + stalePhraseHits.Count * 3),
                        $"Stale-language phrase(s) present but anchored by recent As-Of ({ctx.AsOf:yyyy-MM-dd}).",
                        FreshnessRiskLevel.Anchored));
                }
                else
                {
                    int sev = Math.Min(45, 25 + (stalePhraseHits.Count - 1) * 5);
                    findings.Add(new FreshnessFinding(
                        "STALE_PHRASE",
                        string.Join(",", stalePhraseHits),
                        sev,
                        $"Vague temporal phrase(s) used without an `as of` anchor: {string.Join(", ", stalePhraseHits)}.",
                        FreshnessRiskLevel.StaleLanguage));
                }
            }

            // 3. TRAINING_CUTOFF_EXPOSURE
            string? cutoffHit = CutoffPhrases.FirstOrDefault(p => lower.Contains(p));
            if (cutoffHit != null)
            {
                findings.Add(new FreshnessFinding(
                    "TRAINING_CUTOFF_EXPOSURE",
                    cutoffHit,
                    cutoffHit.Contains("knowledge cutoff") ? 55 : 40,
                    "Prompt mentions the model's training data / language-model nature, anchoring users to stale knowledge.",
                    FreshnessRiskLevel.MissingRetrieval));
            }

            // 4. NO_AS_OF_ANCHOR (only when temporal-ish vocabulary is present)
            if (!hasAnchor && !ctx.AsOf.HasValue &&
                (stalePhraseHits.Count > 0 || yearMatches.Count > 0))
            {
                findings.Add(new FreshnessFinding(
                    "NO_AS_OF_ANCHOR",
                    "",
                    stalePhraseHits.Count > 0 ? 35 : 20,
                    "Prompt discusses time-sensitive content but declares no `as of <date>` anchor.",
                    FreshnessRiskLevel.StaleLanguage));
            }

            // 5. STALE_AS_OF
            if (ctx.AsOf.HasValue)
            {
                double days = (now - ctx.AsOf.Value).TotalDays;
                if (days > 180)
                {
                    int sev = (int)Math.Min(80, 20 + (days - 180) / 10.0);
                    findings.Add(new FreshnessFinding(
                        "STALE_AS_OF",
                        ctx.AsOf.Value.ToString("yyyy-MM-dd"),
                        sev,
                        $"As-Of marker is {(int)days} days old — exceeds the 180-day safety window.",
                        FreshnessRiskLevel.CriticallyStale));
                }
            }

            // 6. DATE_SENSITIVE_DOMAIN_NO_GROUNDING
            bool dateSensitive = ctx.Domain is FreshnessDomain.News or FreshnessDomain.Pricing
                or FreshnessDomain.Regulation or FreshnessDomain.Sports
                or FreshnessDomain.Finance or FreshnessDomain.Medical;
            if (dateSensitive && !ctx.HasRetrievalGrounding)
            {
                int sev = ctx.Domain is FreshnessDomain.Medical or FreshnessDomain.Regulation ? 70 : 55;
                findings.Add(new FreshnessFinding(
                    "DATE_SENSITIVE_DOMAIN_NO_GROUNDING",
                    ctx.Domain.ToString(),
                    sev,
                    $"Domain `{ctx.Domain}` is date-sensitive but no retrieval grounding is configured.",
                    FreshnessRiskLevel.MissingRetrieval));
            }

            // 7. NO_RETRIEVAL (general, knowledge-cutoff aware)
            if (!ctx.HasRetrievalGrounding && ctx.KnowledgeCutoff.HasValue &&
                (now - ctx.KnowledgeCutoff.Value).TotalDays > 365)
            {
                int days = (int)(now - ctx.KnowledgeCutoff.Value).TotalDays;
                int sev = Math.Min(45, 25 + (days - 365) / 60);
                findings.Add(new FreshnessFinding(
                    "NO_RETRIEVAL",
                    ctx.KnowledgeCutoff.Value.ToString("yyyy-MM-dd"),
                    sev,
                    $"Knowledge cutoff is {days} days old and no retrieval grounding is configured.",
                    FreshnessRiskLevel.MissingRetrieval));
            }

            // Risk-appetite modulation on severity (clamped 0..100, affects overall score).
            int appetiteShift = (ctx.RiskAppetite ?? "balanced").ToLowerInvariant() switch
            {
                "cautious" => +8,
                "aggressive" => -8,
                _ => 0,
            };

            // Sort findings.
            findings = findings.OrderByDescending(f => f.Severity).ThenBy(f => f.Code).ToList();

            // Compute overall score (weighted mean + max-finding boost).
            int overall;
            if (findings.Count == 0)
            {
                overall = 0;
            }
            else
            {
                double mean = findings.Average(f => f.Severity);
                int max = findings.Max(f => f.Severity);
                double raw = mean * 0.5 + max * 0.5;
                overall = Clamp((int)Math.Round(raw) + appetiteShift, 0, 100);
            }

            // Grade.
            char grade = overall switch
            {
                <= 15 => 'A',
                <= 30 => 'B',
                <= 50 => 'C',
                <= 70 => 'D',
                _ => 'F',
            };
            if (findings.Any(f => f.Severity >= 75)) grade = 'F';

            // Verdict.
            var verdict = findings.Count == 0
                ? FreshnessRiskLevel.Fresh
                : findings.First().Verdict;

            // Playbook.
            var actions = BuildPlaybook(findings, ctx, now);
            string appetite = (ctx.RiskAppetite ?? "balanced").ToLowerInvariant();
            if (appetite == "aggressive")
            {
                actions = actions.Where(a => a.Priority is FreshnessPriority.P0 or FreshnessPriority.P1).ToList();
            }
            actions = actions
                .GroupBy(a => a.Code)
                .Select(g => g.OrderBy(a => (int)a.Priority).First())
                .OrderBy(a => (int)a.Priority).ThenBy(a => a.Code)
                .ToList();

            // Insights.
            var insights = BuildInsights(findings, ctx, now);

            return new FreshnessReport
            {
                PromptPreview = preview,
                OverallScore = overall,
                Grade = grade,
                Verdict = verdict,
                Findings = findings,
                Playbook = actions,
                Insights = insights,
                GeneratedAt = now,
            };
        }

        private static List<FreshnessAction> BuildPlaybook(
            List<FreshnessFinding> findings, FreshnessContext ctx, DateTime now)
        {
            var actions = new List<FreshnessAction>();

            bool hasHardYear = findings.Any(f => f.Code == "HARDCODED_YEAR" && f.Severity >= 50);
            bool hasAnyHardYear = findings.Any(f => f.Code == "HARDCODED_YEAR");
            bool hasStaleAsOf = findings.Any(f => f.Code == "STALE_AS_OF");
            bool hasDateSensitive = findings.Any(f => f.Code == "DATE_SENSITIVE_DOMAIN_NO_GROUNDING");
            bool hasCutoffExposure = findings.Any(f => f.Code == "TRAINING_CUTOFF_EXPOSURE");
            bool hasStalePhrase = findings.Any(f => f.Code == "STALE_PHRASE");
            bool hasNoRetrieval = findings.Any(f => f.Code == "NO_RETRIEVAL");
            bool hasNoAnchor = findings.Any(f => f.Code == "NO_AS_OF_ANCHOR");

            if (hasHardYear || hasAnyHardYear && findings.First(f => f.Code == "HARDCODED_YEAR").Severity >= 40)
            {
                actions.Add(new FreshnessAction(
                    "REPLACE_HARDCODED_DATE",
                    FreshnessPriority.P0,
                    "prompt_author",
                    "Replace literal year(s) with a `{{as_of_date}}` placeholder injected at runtime.",
                    "Replace any literal year with: \"As of {{as_of_date}}, ...\" and inject today's ISO date at runtime."));
            }

            if (hasStaleAsOf)
            {
                actions.Add(new FreshnessAction(
                    "ADD_KB_REFRESH_CADENCE",
                    FreshnessPriority.P0,
                    "ops",
                    "Schedule a knowledge-base refresh and bump the as-of anchor on every run.",
                    "ops: schedule weekly KB refresh; prompt: bind {{as_of_date}} := today() at runtime."));
                actions.Add(new FreshnessAction(
                    "WARN_USER_DATA_MAY_BE_STALE",
                    FreshnessPriority.P1,
                    "prompt_author",
                    "Prepend a user-visible staleness disclaimer until the anchor is refreshed.",
                    "Prepend: \"Note: this answer is based on data as of {{as_of_date}}; verify time-sensitive details.\""));
            }

            if (hasDateSensitive)
            {
                actions.Add(new FreshnessAction(
                    "ADD_RETRIEVAL_GROUNDING",
                    FreshnessPriority.P0,
                    "retrieval",
                    $"Domain `{ctx.Domain}` is date-sensitive — wire in a retrieval tool before answering.",
                    "Before answering, query the {{kb_name}} retrieval tool. If no source is returned, reply: \"I don't have current data.\""));
            }
            else if (hasNoRetrieval)
            {
                actions.Add(new FreshnessAction(
                    "ADD_RETRIEVAL_GROUNDING",
                    FreshnessPriority.P1,
                    "retrieval",
                    "Knowledge cutoff is well over a year old — augment with retrieval grounding.",
                    "Before answering, query the {{kb_name}} retrieval tool. If no source is returned, reply: \"I don't have current data.\""));
            }

            if (hasCutoffExposure)
            {
                actions.Add(new FreshnessAction(
                    "GUARD_TRAINING_CUTOFF",
                    FreshnessPriority.P1,
                    "prompt_author",
                    "Strip language that exposes the model's training-data cutoff; defer to retrieval instead.",
                    "Do not reference your knowledge cutoff or training data. If asked about post-cutoff events, state you would need a retrieval tool."));
            }

            if (hasStalePhrase || hasNoAnchor)
            {
                actions.Add(new FreshnessAction(
                    "ADD_AS_OF_ANCHOR",
                    FreshnessPriority.P1,
                    "prompt_author",
                    "Anchor vague temporal language to an explicit, injected as-of date.",
                    "Assume today's date is {{today_iso}}. Treat anything older than 30 days as potentially stale."));
                actions.Add(new FreshnessAction(
                    "NARROW_TEMPORAL_SCOPE",
                    FreshnessPriority.P2,
                    "prompt_author",
                    "Replace open-ended words like \"latest\" with explicit windows (e.g. \"in the last 7 days\").",
                    "Rewrite: \"the latest X\" → \"the most recent X published on or after {{since_iso}}\"."));
            }

            // Advisory note when nothing fired.
            if (actions.Count == 0 && findings.Count == 0)
            {
                actions.Add(new FreshnessAction(
                    "WARN_USER_DATA_MAY_BE_STALE",
                    FreshnessPriority.P3,
                    "prompt_author",
                    "Advisory only — no staleness signals detected, but add a user-facing freshness note if domain shifts.",
                    "Optional: \"All facts are accurate as of {{as_of_date}}.\""));
            }

            return actions;
        }

        private static List<string> BuildInsights(
            List<FreshnessFinding> findings, FreshnessContext ctx, DateTime now)
        {
            var insights = new List<string>();
            var stalePhraseFinding = findings.FirstOrDefault(f => f.Code == "STALE_PHRASE");
            if (stalePhraseFinding != null)
            {
                int count = stalePhraseFinding.Span.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
                insights.Add($"{count} stale-language phrase(s) without an `as of` anchor.");
            }

            var hardYear = findings.Where(f => f.Code == "HARDCODED_YEAR").ToList();
            foreach (var f in hardYear.Take(2))
            {
                int.TryParse(f.Span, out var y);
                insights.Add($"Hardcoded year {y} is {now.Year - y} year(s) old (severity {f.Severity}).");
            }

            var dateSensitive = findings.FirstOrDefault(f => f.Code == "DATE_SENSITIVE_DOMAIN_NO_GROUNDING");
            if (dateSensitive != null)
            {
                insights.Add($"Date-sensitive domain `{ctx.Domain}` with no retrieval grounding.");
            }

            var staleAsOf = findings.FirstOrDefault(f => f.Code == "STALE_AS_OF");
            if (staleAsOf != null && ctx.AsOf.HasValue)
            {
                int days = (int)(now - ctx.AsOf.Value).TotalDays;
                insights.Add($"As-of marker is {days} days old — exceeds 180-day safety window.");
            }

            if (findings.Any(f => f.Code == "TRAINING_CUTOFF_EXPOSURE"))
            {
                insights.Add("Training-cutoff exposure: prompt reveals model is a language model with stale data.");
            }

            if (insights.Count > 5) insights = insights.Take(5).ToList();
            return insights;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
