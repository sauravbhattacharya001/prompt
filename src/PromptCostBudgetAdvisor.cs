namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>Risk appetite for <see cref="PromptCostBudgetAdvisor"/>.</summary>
    public enum CostRiskAppetite
    {
        /// <summary>Stricter scoring; appends a second-reviewer action at C/D/F.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient scoring; trims P3 fallback when higher-priority items present.</summary>
        Aggressive,
    }

    /// <summary>Priority bucket for cost-budget findings and playbook actions.</summary>
    public enum CostPriority
    {
        /// <summary>Blocking / immediate action.</summary>
        P0,
        /// <summary>High-priority action.</summary>
        P1,
        /// <summary>Medium-priority action.</summary>
        P2,
        /// <summary>Advisory / fallback.</summary>
        P3,
    }

    /// <summary>Per-prompt verdict ladder for the cost-budget advisor.</summary>
    public enum CostVerdict
    {
        /// <summary>Prompt fits comfortably in budget.</summary>
        Ready,
        /// <summary>Modest bloat - human review recommended.</summary>
        Review,
        /// <summary>Significant bloat - rewrite recommended before sending.</summary>
        RewriteRecommended,
        /// <summary>Do not send: hard token-budget exceeded or other blocking cost issue.</summary>
        BlockSend,
    }

    /// <summary>Options for <see cref="PromptCostBudgetAdvisor.Analyze"/>.</summary>
    public sealed class CostOptions
    {
        /// <summary>Risk appetite knob.</summary>
        public CostRiskAppetite RiskAppetite { get; init; } = CostRiskAppetite.Balanced;

        /// <summary>Soft char budget; beyond this, EXCESSIVE_PROMPT_LENGTH starts to fire.</summary>
        public int SoftCharLimit { get; init; } = 6000;

        /// <summary>Hard char budget; beyond this, HARD_BUDGET_EXCEEDED fires (BlockSend).</summary>
        public int HardCharLimit { get; init; } = 12000;

        /// <summary>Token estimator: tokens ~= chars * TargetTokensPerChar. Default 0.25.</summary>
        public double TargetTokensPerChar { get; init; } = 0.25;
    }

    /// <summary>A single detected cost / verbosity gap.</summary>
    public sealed class CostFinding
    {
        /// <summary>Detector code.</summary>
        public string Code { get; internal set; } = "";
        /// <summary>Severity 0..100 (after risk-appetite modulation).</summary>
        public int Severity { get; internal set; }
        /// <summary>Priority bucket.</summary>
        public CostPriority Priority { get; internal set; }
        /// <summary>Short human-readable label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Reason / detail.</summary>
        public string Reason { get; internal set; } = "";
        /// <summary>Estimated chars that could be trimmed if this finding is acted on.</summary>
        public int EstimatedSavingsChars { get; internal set; }
    }

    /// <summary>One playbook action recommended by the cost-budget advisor.</summary>
    public sealed class CostPlaybookAction
    {
        /// <summary>Stable action id.</summary>
        public string Id { get; internal set; } = "";
        /// <summary>Priority bucket.</summary>
        public CostPriority Priority { get; internal set; }
        /// <summary>Short label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Reason / detail.</summary>
        public string Reason { get; internal set; } = "";
        /// <summary>Suggested owner role.</summary>
        public string Owner { get; internal set; } = "prompt_author";
        /// <summary>Blast radius 1..3 (advisory).</summary>
        public int BlastRadius { get; internal set; } = 1;
        /// <summary>Reversibility (always "high" for prompt-author edits).</summary>
        public string Reversibility { get; internal set; } = "high";
    }

    /// <summary>Report produced by <see cref="PromptCostBudgetAdvisor.Analyze"/>.</summary>
    public sealed class PromptCostBudgetReport
    {
        /// <summary>0..100 score; higher is better.</summary>
        public int CostScore { get; internal set; }
        /// <summary>A-F grade.</summary>
        public char Grade { get; internal set; }
        /// <summary>Verdict ladder.</summary>
        public CostVerdict Verdict { get; internal set; }
        /// <summary>Headline summary.</summary>
        public string Headline { get; internal set; } = "";
        /// <summary>Per-detector findings.</summary>
        public IReadOnlyList<CostFinding> Findings { get; internal set; } = Array.Empty<CostFinding>();
        /// <summary>Ranked P0-first playbook.</summary>
        public IReadOnlyList<CostPlaybookAction> Playbook { get; internal set; } = Array.Empty<CostPlaybookAction>();
        /// <summary>Cross-prompt insights.</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();
        /// <summary>Paste-ready prompt with an appended `# Prompt-Budget Trim Suggestions` block (unchanged when no P0/P1).</summary>
        public string CostTrimmedDraft { get; internal set; } = "";
        /// <summary>Estimated tokens for the original prompt (chars * TargetTokensPerChar).</summary>
        public int EstimatedTokens { get; internal set; }
        /// <summary>Estimated tokens saved if the playbook is applied.</summary>
        public int EstimatedSavingsTokens { get; internal set; }
        /// <summary>Generation timestamp (deterministic when an explicit clock is injected).</summary>
        public DateTime GeneratedAt { get; internal set; }

        /// <summary>Render a plain-text view.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{Verdict}] {Headline}");
            sb.AppendLine($"Score: {CostScore}/100   Grade: {Grade}");
            sb.AppendLine($"EstTokens: {EstimatedTokens}   EstSavingsTokens: {EstimatedSavingsTokens}");
            if (Findings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Findings:");
                foreach (var f in Findings)
                {
                    sb.AppendLine($"  [{f.Priority}] {f.Code} (sev {f.Severity}, saveChars {f.EstimatedSavingsChars}) - {f.Label}");
                    sb.AppendLine($"      {f.Reason}");
                }
            }
            if (Playbook.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Playbook:");
                foreach (var a in Playbook)
                {
                    sb.AppendLine($"  [{a.Priority}] {a.Id} - {a.Label} (owner={a.Owner}, blast={a.BlastRadius})");
                    sb.AppendLine($"      {a.Reason}");
                }
            }
            if (Insights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Insights:");
                foreach (var i in Insights) sb.AppendLine($"  - {i}");
            }
            return sb.ToString().TrimEnd() + "\n";
        }

        /// <summary>Render a Markdown view.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Prompt Cost / Budget Audit");
            sb.AppendLine();
            sb.AppendLine($"**Verdict:** `{Verdict}`  ");
            sb.AppendLine($"**Score:** `{CostScore}/100`  ");
            sb.AppendLine($"**Grade:** `{Grade}`  ");
            sb.AppendLine($"**EstTokens:** `{EstimatedTokens}`  ");
            sb.AppendLine($"**EstSavingsTokens:** `{EstimatedSavingsTokens}`");
            sb.AppendLine();
            sb.AppendLine($"> {Headline}");
            sb.AppendLine();
            sb.AppendLine("## Findings");
            if (Findings.Count == 0)
            {
                sb.AppendLine("_None._");
            }
            else
            {
                sb.AppendLine("| Priority | Code | Sev | SaveChars | Label |");
                sb.AppendLine("|---|---|---:|---:|---|");
                foreach (var f in Findings)
                    sb.AppendLine($"| {f.Priority} | `{f.Code}` | {f.Severity} | {f.EstimatedSavingsChars} | {EscapePipe(f.Label)} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Playbook");
            if (Playbook.Count == 0)
            {
                sb.AppendLine("_None._");
            }
            else
            {
                sb.AppendLine("| Priority | Id | Owner | Blast | Label |");
                sb.AppendLine("|---|---|---|---:|---|");
                foreach (var a in Playbook)
                    sb.AppendLine($"| {a.Priority} | `{a.Id}` | {a.Owner} | {a.BlastRadius} | {EscapePipe(a.Label)} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Insights");
            if (Insights.Count == 0) sb.AppendLine("_None._");
            else foreach (var i in Insights) sb.AppendLine($"- {i}");
            return sb.ToString();
        }

        /// <summary>Render a deterministic JSON view.</summary>
        public string ToJson()
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            };
            return JsonSerializer.Serialize(this, opts);
        }

        private static string EscapePipe(string s) => (s ?? "").Replace("|", "\\|");
    }

    /// <summary>
    /// Agentic prompt cost / token-budget advisor: audits a system prompt for verbosity,
    /// duplication, inline-data bloat, missing length caps, and budget-overrun risks; recommends
    /// paste-ready trims. 9th sibling to PromptHallucinationRiskScorer / PromptDefenseAdvisor /
    /// PromptKnowledgeFreshnessAdvisor / PromptExampleQualityAdvisor /
    /// PromptInstructionConflictAdvisor / PromptOutputContractAdvisor / PromptStepReasoningAdvisor /
    /// PromptToolUseContractAdvisor.
    /// </summary>
    public sealed class PromptCostBudgetAdvisor
    {
        private readonly Func<DateTime> _now;

        /// <summary>Create a new advisor. Pass an explicit clock for deterministic tests.</summary>
        public PromptCostBudgetAdvisor(Func<DateTime>? nowProvider = null)
        {
            _now = nowProvider ?? (() => DateTime.UtcNow);
        }

        /// <summary>Analyze a prompt and produce a structured cost-budget report.</summary>
        public PromptCostBudgetReport Analyze(string? prompt, CostOptions? options = null)
        {
            options ??= new CostOptions();
            prompt ??= string.Empty;
            int chars = prompt.Length;
            string lower = prompt.ToLowerInvariant();
            string normalized = WhitespaceRun.Replace(lower, " ").Trim();

            int soft = Math.Max(1, options.SoftCharLimit);
            int hard = Math.Max(soft + 1, options.HardCharLimit);
            double tokensPerChar = options.TargetTokensPerChar <= 0 ? 0.25 : options.TargetTokensPerChar;

            var findings = new List<CostFinding>();

            // 1. HARD_BUDGET_EXCEEDED (P0, sev 90)
            if (chars > hard)
            {
                int over = chars - hard;
                findings.Add(MakeFinding("HARD_BUDGET_EXCEEDED", 90,
                    "Prompt exceeds the hard char budget",
                    $"Prompt is {chars} chars; hard limit is {hard}. Trim at least {over} chars or split the prompt.",
                    savingsChars: over));
            }

            // 2. EXCESSIVE_PROMPT_LENGTH (P1)
            if (chars > soft)
            {
                int over = chars - soft;
                int spanToHard = Math.Max(1, hard - soft);
                int sev = Math.Min(80, 30 + (int)Math.Round(50.0 * over / spanToHard));
                findings.Add(MakeFinding("EXCESSIVE_PROMPT_LENGTH", sev,
                    "Prompt exceeds the soft char budget",
                    $"Prompt is {chars} chars; soft limit is {soft}. Aim to trim ~{over} chars.",
                    savingsChars: over / 2));
            }

            // 3. REDUNDANT_INSTRUCTIONS (P1, sev 55)
            var redundantHits = DetectRedundantInstructions(normalized);
            if (redundantHits.Count > 0)
            {
                findings.Add(MakeFinding("REDUNDANT_INSTRUCTIONS", 55,
                    "Redundant / duplicate instruction phrasing",
                    $"Multiple instructions say the same thing: {string.Join("; ", redundantHits)}. Pick one canonical phrasing.",
                    savingsChars: 40 * redundantHits.Count));
            }

            // 4. OVERLONG_FEW_SHOT_EXAMPLES (P2, sev 45)
            var (exampleBlocks, longestExample, totalExampleChars) = ScanExamples(prompt);
            bool overlongSingle = longestExample > 400;
            bool examplesDominate = chars > 0 && totalExampleChars > 0 && totalExampleChars * 2 > chars;
            if (overlongSingle || examplesDominate)
            {
                int sav = overlongSingle ? Math.Max(0, longestExample - 400) : Math.Max(0, totalExampleChars / 2);
                findings.Add(MakeFinding("OVERLONG_FEW_SHOT_EXAMPLES", 45,
                    "Few-shot examples are too long or dominate the prompt",
                    $"Found {exampleBlocks} example block(s); longest={longestExample} chars; total={totalExampleChars} chars. Compress or move to retrieval.",
                    savingsChars: sav));
            }

            // 5. EXCESSIVE_HEDGING_FILLER (P2)
            int fillerHits = CountFiller(lower);
            if (fillerHits >= 6)
            {
                int sev = Math.Min(60, 25 + 3 * (fillerHits - 6));
                findings.Add(MakeFinding("EXCESSIVE_HEDGING_FILLER", sev,
                    "Excessive hedging / filler words",
                    $"Detected {fillerHits} filler occurrences (please, kindly, very, really, just, simply, make sure to, ...). These add tokens without instruction value.",
                    savingsChars: 8 * fillerHits));
            }

            // 6. BLOATED_ROLE_PREAMBLE (P2, sev 40)
            int preambleLen = MeasureRolePreamble(prompt);
            if (preambleLen > 300)
            {
                findings.Add(MakeFinding("BLOATED_ROLE_PREAMBLE", 40,
                    "Role / persona preamble is too long",
                    $"Opening 'You are a ...' / 'Act as ...' preamble is {preambleLen} chars before the first instruction verb. Compress to <=150 chars.",
                    savingsChars: preambleLen - 150));
            }

            // 7. RESTATED_OUTPUT_FORMAT (P2, sev 35)
            int formatRestate = CountFormatRestate(lower);
            if (formatRestate >= 2)
            {
                findings.Add(MakeFinding("RESTATED_OUTPUT_FORMAT", 35,
                    "Output-format instruction repeated",
                    $"Output format requirement (JSON/markdown/etc.) appears {formatRestate} times. State it once.",
                    savingsChars: 40 * (formatRestate - 1)));
            }

            // 8. UNBOUNDED_OUTPUT_LENGTH (P1, sev 50)
            bool asksLongOutput = UnboundedOutputPattern.IsMatch(lower);
            bool hasLengthCap = LengthCapPattern.IsMatch(lower);
            if (asksLongOutput && !hasLengthCap)
            {
                findings.Add(MakeFinding("UNBOUNDED_OUTPUT_LENGTH", 50,
                    "Asks for long output without a length cap",
                    "Prompt asks for comprehensive/exhaustive/detailed output but does not bound length (max N words/tokens/sentences/bullets/paragraphs).",
                    savingsChars: 0));
            }

            // 9. LARGE_INLINE_DATA_BLOCK (P1, sev 60)
            int largeBlockLen = DetectLargeInlineBlock(prompt);
            if (largeBlockLen > 2000)
            {
                findings.Add(MakeFinding("LARGE_INLINE_DATA_BLOCK", 60,
                    "Large inline data block detected",
                    $"A single inline block is {largeBlockLen} chars. Move to retrieval / chunk it / load via a tool.",
                    savingsChars: (int)Math.Round(largeBlockLen * 0.7)));
            }

            // 10. DUPLICATED_SYSTEM_BLOCK (P1, sev 65)
            int duplicatedLen = DetectDuplicateParagraph(prompt);
            if (duplicatedLen >= 200)
            {
                findings.Add(MakeFinding("DUPLICATED_SYSTEM_BLOCK", 65,
                    "A long paragraph appears verbatim twice",
                    $"Detected a duplicate paragraph of {duplicatedLen} chars. Delete one copy.",
                    savingsChars: duplicatedLen));
            }

            // 11. POLITENESS_OVERHEAD (P3, sev 15)
            int politeHits = CountPoliteness(lower);
            if (politeHits >= 3)
            {
                findings.Add(MakeFinding("POLITENESS_OVERHEAD", 15,
                    "Politeness tokens add overhead",
                    $"Detected {politeHits} please/thank-you tokens. Models do not need them.",
                    savingsChars: 8 * politeHits));
            }

            // 12. NO_LENGTH_GUIDANCE_FOR_LIST (P2, sev 30)
            if (ListRequestPattern.IsMatch(lower) && !ListLengthCapPattern.IsMatch(lower))
            {
                findings.Add(MakeFinding("NO_LENGTH_GUIDANCE_FOR_LIST", 30,
                    "List request without item-count cap",
                    "Prompt asks for a list / bullets / enumeration without bounding the number of items.",
                    savingsChars: 0));
            }

            // Risk-appetite modulation on severity.
            double appetiteMult = options.RiskAppetite switch
            {
                CostRiskAppetite.Cautious => 1.15,
                CostRiskAppetite.Aggressive => 0.85,
                _ => 1.0,
            };
            foreach (var f in findings)
                f.Severity = Math.Max(0, Math.Min(100, (int)Math.Round(f.Severity * appetiteMult)));

            // Dedupe by Code (keep highest-severity).
            findings = findings
                .GroupBy(f => f.Code)
                .Select(g => g.MaxBy(f => f.Severity)!)
                .ToList();

            foreach (var f in findings)
                f.Priority = BucketPriority(f.Code);

            // Order: Priority asc, Severity desc, Code asc.
            findings = findings
                .OrderBy(f => f.Priority)
                .ThenByDescending(f => f.Severity)
                .ThenBy(f => f.Code, StringComparer.Ordinal)
                .ToList();

            // Score.
            int topSev = findings.Count == 0 ? 0 : findings.Max(f => f.Severity);
            int restSum = findings.Count <= 1 ? 0 : findings.OrderByDescending(f => f.Severity).Skip(1).Sum(f => f.Severity);
            int penalty = (int)Math.Round(topSev + 0.4 * Math.Min(restSum, 60));
            int costScore = Math.Max(0, Math.Min(100, 100 - penalty));

            // Verdict ladder.
            bool anyP0 = findings.Any(f => f.Priority == CostPriority.P0);
            CostVerdict verdict;
            if (anyP0) verdict = CostVerdict.BlockSend;
            else if (costScore <= 45) verdict = CostVerdict.RewriteRecommended;
            else if (costScore <= 70) verdict = CostVerdict.Review;
            else verdict = CostVerdict.Ready;

            // Grade.
            char grade;
            if (verdict == CostVerdict.BlockSend) grade = 'F';
            else if (costScore >= 85) grade = 'A';
            else if (costScore >= 70) grade = 'B';
            else if (costScore >= 55) grade = 'C';
            else if (costScore >= 40) grade = 'D';
            else grade = 'F';

            // Token estimates.
            int estimatedTokens = (int)Math.Round(chars * tokensPerChar);
            int savingsChars = findings.Sum(f => Math.Max(0, f.EstimatedSavingsChars));
            int estimatedSavings = (int)Math.Round(savingsChars * tokensPerChar);

            var playbook = BuildPlaybook(findings, options, grade);
            var insights = BuildInsights(findings, chars, hard, soft, totalExampleChars, largeBlockLen);
            string draft = BuildTrimmedDraft(prompt, playbook);

            string headline = findings.Count == 0
                ? (chars < 200 ? "Prompt is very short and within budget." : "Prompt fits comfortably within the token budget.")
                : $"{findings.Count} cost issue(s) detected; top: {findings[0].Code} (~{estimatedSavings} tokens potential savings).";

            return new PromptCostBudgetReport
            {
                CostScore = costScore,
                Grade = grade,
                Verdict = verdict,
                Headline = headline,
                Findings = findings,
                Playbook = playbook,
                Insights = insights,
                CostTrimmedDraft = draft,
                EstimatedTokens = estimatedTokens,
                EstimatedSavingsTokens = estimatedSavings,
                GeneratedAt = _now(),
            };
        }

        // -----------------------------------------------------------------------------------------
        // Detectors / helpers
        // -----------------------------------------------------------------------------------------

        private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

        private static readonly string[][] RedundantPairs = new[]
        {
            new[] { "be concise", "keep it short" },
            new[] { "be concise", "be brief" },
            new[] { "keep it short", "be brief" },
            new[] { "do not hallucinate", "don't hallucinate" },
            new[] { "do not make things up", "do not hallucinate" },
            new[] { "do not make up", "do not hallucinate" },
            new[] { "think step by step", "reason step by step" },
            new[] { "step by step", "think carefully" },
        };

        private static readonly string[] RedundantSinglePhrases = new[]
        {
            "do not hallucinate",
            "don't hallucinate",
            "be concise",
            "be helpful",
            "be accurate",
            "be polite",
        };

        private static List<string> DetectRedundantInstructions(string normalized)
        {
            var hits = new List<string>();
            foreach (var pair in RedundantPairs)
            {
                if (normalized.Contains(pair[0]) && normalized.Contains(pair[1]))
                {
                    string entry = $"'{pair[0]}' + '{pair[1]}'";
                    if (!hits.Contains(entry)) hits.Add(entry);
                }
            }
            foreach (var phrase in RedundantSinglePhrases)
            {
                int count = CountSubstring(normalized, phrase);
                if (count >= 2)
                {
                    string entry = $"'{phrase}' x{count}";
                    if (!hits.Contains(entry)) hits.Add(entry);
                }
            }
            return hits;
        }

        private static int CountSubstring(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return 0;
            int count = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) != -1)
            {
                count++;
                i += needle.Length;
            }
            return count;
        }

        private static readonly Regex FencedBlockPattern =
            new(@"```[\s\S]*?```", RegexOptions.Compiled);

        private static readonly Regex ExampleHeaderPattern =
            new(@"(?im)^\s*Example\s*\d*\s*[:\-]", RegexOptions.Compiled);

        private static (int blocks, int longest, int total) ScanExamples(string prompt)
        {
            int blocks = 0, longest = 0, total = 0;
            foreach (Match m in FencedBlockPattern.Matches(prompt))
            {
                blocks++;
                int len = m.Value.Length;
                if (len > longest) longest = len;
                total += len;
            }
            // Example N: ... blocks (everything until next Example marker or end).
            var matches = ExampleHeaderPattern.Matches(prompt);
            for (int i = 0; i < matches.Count; i++)
            {
                int start = matches[i].Index;
                int end = (i + 1 < matches.Count) ? matches[i + 1].Index : prompt.Length;
                int len = end - start;
                blocks++;
                if (len > longest) longest = len;
                total += len;
            }
            return (blocks, longest, total);
        }

        private static readonly string[] FillerWords = new[]
        {
            "please ", " kindly ", " very ", " really ", " just ", " simply ",
            "as you know", "make sure to", "be sure to", "remember to",
        };

        private static int CountFiller(string lower)
        {
            int total = 0;
            string padded = " " + lower + " ";
            foreach (var w in FillerWords)
                total += CountSubstring(padded, w);
            return total;
        }

        private static int CountPoliteness(string lower)
        {
            string padded = " " + lower + " ";
            return CountSubstring(padded, "please ") + CountSubstring(padded, "thank you")
                 + CountSubstring(padded, "thanks ");
        }

        private static readonly Regex RolePreamblePattern =
            new(@"^\s*(you are|act as|imagine you are|you're|pretend to be)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FirstInstructionVerbPattern =
            new(@"\b(your task is|your job is|do the following|do this|complete the following|respond|reply|answer|write|generate|produce|return|emit|output|summarize|translate|extract|analyze|classify|create|build|list|enumerate|explain|describe)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static int MeasureRolePreamble(string prompt)
        {
            if (!RolePreamblePattern.IsMatch(prompt)) return 0;
            var m = FirstInstructionVerbPattern.Match(prompt);
            if (!m.Success) return Math.Min(prompt.Length, 9999);
            return m.Index;
        }

        private static readonly Regex OutputFormatPattern =
            new(@"\b(respond in (json|yaml|xml|markdown|html|csv)|return (?:it )?as (json|yaml|xml|markdown|html|csv)|output (?:as )?(json|yaml|xml|markdown|html|csv)|format\s*:\s*(json|yaml|xml|markdown|html|csv)|in (json|yaml|xml|markdown|html|csv) format)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static int CountFormatRestate(string lower)
        {
            return OutputFormatPattern.Matches(lower).Count;
        }

        private static readonly Regex UnboundedOutputPattern =
            new(@"\b(comprehensive|exhaustive|as detailed as possible|in (full )?detail|thorough(ly)?|elaborate(ly)?|as much detail as|extensive(ly)?|complete (overview|breakdown))\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LengthCapPattern =
            new(@"\b(max(imum)? \d+|at most \d+|no more than \d+|up to \d+|under \d+|fewer than \d+|less than \d+|\d+ (words?|tokens?|sentences?|bullets?|paragraphs?|lines?|items?)|in (one|two|three) (sentence|paragraph|bullet)|tl;?dr)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ListRequestPattern =
            new(@"\b(list|enumerate|bullet points?|bulleted list|provide a list|give (me )?a list)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ListLengthCapPattern =
            new(@"\b(\d+ (items?|bullets?|points?|things?|examples?)|at most \d+|no more than \d+|up to \d+|top \d+|exactly \d+)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static int DetectLargeInlineBlock(string prompt)
        {
            int longest = 0;
            foreach (Match m in FencedBlockPattern.Matches(prompt))
                if (m.Value.Length > longest) longest = m.Value.Length;
            // Also consider longest single line.
            int lineMax = 0, curr = 0;
            foreach (char c in prompt)
            {
                if (c == '\n') { if (curr > lineMax) lineMax = curr; curr = 0; }
                else curr++;
            }
            if (curr > lineMax) lineMax = curr;
            return Math.Max(longest, lineMax);
        }

        private static int DetectDuplicateParagraph(string prompt)
        {
            var paragraphs = prompt.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            int longest = 0;
            foreach (var p in paragraphs)
            {
                var trimmed = p.Trim();
                if (trimmed.Length < 200) continue;
                if (seen.ContainsKey(trimmed))
                {
                    if (trimmed.Length > longest) longest = trimmed.Length;
                }
                else
                {
                    seen[trimmed] = 1;
                }
            }
            return longest;
        }

        private static CostFinding MakeFinding(string code, int sev, string label, string reason, int savingsChars = 0)
            => new() { Code = code, Severity = sev, Label = label, Reason = reason, EstimatedSavingsChars = savingsChars };

        private static CostPriority BucketPriority(string code)
        {
            switch (code)
            {
                case "HARD_BUDGET_EXCEEDED": return CostPriority.P0;
                case "EXCESSIVE_PROMPT_LENGTH":
                case "REDUNDANT_INSTRUCTIONS":
                case "UNBOUNDED_OUTPUT_LENGTH":
                case "LARGE_INLINE_DATA_BLOCK":
                case "DUPLICATED_SYSTEM_BLOCK":
                    return CostPriority.P1;
                case "OVERLONG_FEW_SHOT_EXAMPLES":
                case "EXCESSIVE_HEDGING_FILLER":
                case "BLOATED_ROLE_PREAMBLE":
                case "RESTATED_OUTPUT_FORMAT":
                case "NO_LENGTH_GUIDANCE_FOR_LIST":
                    return CostPriority.P2;
                case "POLITENESS_OVERHEAD":
                    return CostPriority.P3;
                default: return CostPriority.P3;
            }
        }

        // -----------------------------------------------------------------------------------------
        // Playbook / insights / draft
        // -----------------------------------------------------------------------------------------

        private static readonly (string Id, CostPriority Priority, string Trigger, int Blast, string Label, string Reason)[] PlaybookCatalog = new[]
        {
            ("SHRINK_TO_HARD_BUDGET",          CostPriority.P0, "HARD_BUDGET_EXCEEDED",            3, "Shrink prompt under the hard budget", "Trim duplicated, filler, or inline-data content to bring the prompt under the hard char/token limit before sending."),
            ("BLOCK_AND_REWRITE",              CostPriority.P0, "HARD_BUDGET_EXCEEDED",            3, "Block send and rewrite prompt",       "A blocking cost issue was detected. Do not send the prompt as-is; rewrite using the suggestions below."),
            ("EXTRACT_INLINE_DATA_TO_RETRIEVAL", CostPriority.P1, "LARGE_INLINE_DATA_BLOCK",       2, "Extract inline data to retrieval",    "Move the large inline block (CSV/log/JSON) into a retrieval/tool call rather than pasting it inside the system prompt."),
            ("DEDUPE_SYSTEM_BLOCKS",           CostPriority.P1, "DUPLICATED_SYSTEM_BLOCK",         1, "Delete the duplicated paragraph",     "A long paragraph appears twice verbatim. Delete one copy."),
            ("CAP_OUTPUT_LENGTH",              CostPriority.P1, "UNBOUNDED_OUTPUT_LENGTH",         1, "Cap output length",                   "Add an explicit length cap (e.g. 'max 200 words', '<=5 bullets', 'one paragraph') to stop runaway generations."),
            ("TIGHTEN_REDUNDANT_INSTRUCTIONS", CostPriority.P1, "REDUNDANT_INSTRUCTIONS",          1, "Collapse redundant instructions",     "Pick one canonical phrasing for each instruction. Repeating the same idea wastes tokens and confuses the model."),
            ("SHORTEN_FEW_SHOTS",              CostPriority.P2, "OVERLONG_FEW_SHOT_EXAMPLES",      1, "Shorten few-shot examples",           "Compress or trim examples; aim for the smallest example that demonstrates the pattern."),
            ("REMOVE_FILLER",                  CostPriority.P2, "EXCESSIVE_HEDGING_FILLER",        1, "Remove filler / hedging words",       "Strip filler words (please/kindly/just/simply/really/make sure to/...). The model does not need them."),
            ("COMPRESS_ROLE_PREAMBLE",         CostPriority.P2, "BLOATED_ROLE_PREAMBLE",           1, "Compress the role / persona preamble", "Reduce the opening 'You are a ...' block to <=150 chars before the first instruction verb."),
            ("CONSOLIDATE_FORMAT_INSTRUCTION", CostPriority.P2, "RESTATED_OUTPUT_FORMAT",          1, "State the output format once",        "Keep the strongest output-format instruction and delete the rest."),
            ("ADD_LIST_LENGTH_CAP",            CostPriority.P2, "NO_LENGTH_GUIDANCE_FOR_LIST",     1, "Cap list / bullet count",             "Specify a maximum number of items (e.g. 'top 5', 'no more than 7 bullets')."),
            ("STRIP_POLITENESS_TOKENS",        CostPriority.P3, "POLITENESS_OVERHEAD",             1, "Strip politeness tokens",             "Remove please/thank-you tokens; they add overhead without changing model behavior."),
        };

        private static List<CostPlaybookAction> BuildPlaybook(
            List<CostFinding> findings, CostOptions opts, char grade)
        {
            var actions = new List<CostPlaybookAction>();
            var triggers = findings.Select(f => f.Code).ToHashSet(StringComparer.Ordinal);

            foreach (var entry in PlaybookCatalog)
            {
                if (triggers.Contains(entry.Trigger))
                {
                    actions.Add(new CostPlaybookAction
                    {
                        Id = entry.Id,
                        Priority = entry.Priority,
                        Label = entry.Label,
                        Reason = entry.Reason,
                        BlastRadius = entry.Blast,
                    });
                }
            }

            // Cautious: add review action at C/D/F grades.
            if (opts.RiskAppetite == CostRiskAppetite.Cautious && (grade == 'C' || grade == 'D' || grade == 'F'))
            {
                if (!actions.Any(a => a.Id == "SCHEDULE_PROMPT_BUDGET_REVIEW"))
                {
                    actions.Add(new CostPlaybookAction
                    {
                        Id = "SCHEDULE_PROMPT_BUDGET_REVIEW",
                        Priority = CostPriority.P2,
                        Label = "Schedule a second-pass prompt-budget review",
                        Reason = "Cautious risk appetite at this grade: schedule a second reviewer to audit the prompt before deployment.",
                        Owner = "reviewer",
                        BlastRadius = 1,
                    });
                }
            }

            // Fallback when no findings.
            if (findings.Count == 0)
            {
                actions.Add(new CostPlaybookAction
                {
                    Id = "PROMPT_BUDGET_OK",
                    Priority = CostPriority.P3,
                    Label = "Prompt is within budget",
                    Reason = "No cost or verbosity issues detected.",
                    BlastRadius = 1,
                });
            }

            // Aggressive: trim P3 fallback when any P0/P1 is present.
            if (opts.RiskAppetite == CostRiskAppetite.Aggressive
                && actions.Any(a => a.Priority == CostPriority.P0 || a.Priority == CostPriority.P1))
            {
                actions = actions.Where(a => a.Priority != CostPriority.P3).ToList();
            }

            // Stable ordering: priority asc then catalog index then id asc.
            var idIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < PlaybookCatalog.Length; i++)
                idIndex[PlaybookCatalog[i].Id] = i;
            actions = actions
                .OrderBy(a => a.Priority)
                .ThenBy(a => idIndex.TryGetValue(a.Id, out var idx) ? idx : int.MaxValue)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();

            return actions;
        }

        private static List<string> BuildInsights(
            List<CostFinding> findings, int chars, int hard, int soft, int totalExampleChars, int largeBlockLen)
        {
            var insights = new List<string>();
            if (chars > hard) insights.Add("PROMPT_OVER_HARD_BUDGET");
            else if (chars >= hard * 0.9) insights.Add("PROMPT_NEAR_HARD_BUDGET");
            if (chars > soft && chars <= hard) insights.Add("PROMPT_OVER_SOFT_BUDGET");
            if (largeBlockLen > 2000 && chars > 0 && largeBlockLen * 2 >= chars)
                insights.Add("INLINE_DATA_DOMINATES");
            if (totalExampleChars > 0 && chars > 0 && totalExampleChars * 2 > chars)
                insights.Add("EXAMPLES_DOMINATE");
            if (findings.Any(f => f.Code == "DUPLICATED_SYSTEM_BLOCK"))
                insights.Add("DUPLICATION_DETECTED");
            if (findings.Count == 0)
            {
                if (chars < 200) insights.Add("VERY_SHORT_PROMPT");
                else insights.Add("EFFICIENT_PROMPT");
            }
            return insights;
        }

        private static string BuildTrimmedDraft(string prompt, List<CostPlaybookAction> playbook)
        {
            var top = playbook
                .Where(a => a.Priority == CostPriority.P0 || a.Priority == CostPriority.P1)
                .ToList();
            if (top.Count == 0) return prompt;
            var sb = new StringBuilder();
            sb.Append(prompt);
            if (!prompt.EndsWith("\n", StringComparison.Ordinal)) sb.Append('\n');
            sb.Append('\n');
            sb.AppendLine("# Prompt-Budget Trim Suggestions");
            foreach (var a in top)
                sb.AppendLine($"- [{a.Priority}] {a.Id} - {a.Label}: {a.Reason}");
            return sb.ToString();
        }
    }
}
