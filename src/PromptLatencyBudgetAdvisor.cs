namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>Risk appetite for <see cref="PromptLatencyBudgetAdvisor"/>.</summary>
    public enum LatencyRiskAppetite
    {
        /// <summary>Stricter: scales penalties up and appends a perf-review action when any P0/P1 fires.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient: scales penalties down and trims P3 actions when a P0/P1 exists.</summary>
        Aggressive,
    }

    /// <summary>Latency verdict ladder.</summary>
    public enum LatencyVerdict
    {
        /// <summary>Well inside the latency budget.</summary>
        Fast,
        /// <summary>Mostly within budget, minor risks.</summary>
        Acceptable,
        /// <summary>Near or just over budget; trim recommended.</summary>
        Slow,
        /// <summary>Will materially exceed the budget.</summary>
        TooSlow,
        /// <summary>Pathological: multiple compounding cost sources.</summary>
        Pathological,
    }

    /// <summary>Priority bucket for latency findings and playbook actions.</summary>
    public enum LatencyPriority
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

    /// <summary>Enumerated latency-cost modes.</summary>
    public enum LatencyMode
    {
        /// <summary>Prompt itself is large; encoding + attention cost dominates.</summary>
        OversizedPrompt,
        /// <summary>Prompt asks for explicit step-by-step / chain-of-thought reasoning.</summary>
        ChainOfThoughtExpansion,
        /// <summary>Prompt asks for an unbounded or very large output (no length cap).</summary>
        UnboundedOutput,
        /// <summary>Prompt requests comprehensive / exhaustive / detailed coverage.</summary>
        ExhaustiveCoverage,
        /// <summary>Prompt chains multiple sequential tool calls ("then ... then ...").</summary>
        SerialToolChain,
        /// <summary>Prompt asks the model to retry / iterate until criteria met.</summary>
        RetryLoop,
        /// <summary>Prompt processes attached images, documents, or large file contents inline.</summary>
        HeavyMultimodalInput,
        /// <summary>Multi-step plan that could be parallelized but is phrased serially.</summary>
        SerializableFanout,
        /// <summary>No explicit output-length cap (token budget for response unbounded).</summary>
        MissingOutputCap,
        /// <summary>Streaming is not enabled, so the user waits for the full response.</summary>
        StreamingDisabled,
    }

    /// <summary>Optional environment context for latency analysis.</summary>
    public sealed class LatencyContext
    {
        /// <summary>Soft prompt-token budget. Default 4000.</summary>
        public int PromptTokenBudget { get; init; } = 4000;

        /// <summary>Soft response-token budget. Default 800.</summary>
        public int ResponseTokenBudget { get; init; } = 800;

        /// <summary>Soft end-to-end latency budget in milliseconds. Default 5000ms.</summary>
        public int LatencyBudgetMs { get; init; } = 5000;

        /// <summary>Estimated per-output-token latency (ms). Default 25ms.</summary>
        public double MsPerOutputToken { get; init; } = 25.0;

        /// <summary>Estimated per-input-token latency (ms). Default 0.5ms.</summary>
        public double MsPerInputToken { get; init; } = 0.5;

        /// <summary>Fixed per-request overhead (ms): TLS, queueing, scheduling. Default 400ms.</summary>
        public int FixedOverheadMs { get; init; } = 400;

        /// <summary>True if the runner streams tokens to the user. Lowers perceived latency.</summary>
        public bool StreamingEnabled { get; init; }

        /// <summary>True if tools / browsing are wired up (relevant for SerialToolChain costing).</summary>
        public bool ToolsAvailable { get; init; }

        /// <summary>True if the runner can fan out tool calls in parallel.</summary>
        public bool ParallelToolCallsEnabled { get; init; }

        /// <summary>Risk appetite knob.</summary>
        public LatencyRiskAppetite RiskAppetite { get; init; } = LatencyRiskAppetite.Balanced;
    }

    /// <summary>A single latency finding.</summary>
    public sealed class LatencyFinding
    {
        /// <summary>Mode code.</summary>
        public LatencyMode Mode { get; internal set; }

        /// <summary>Severity 0..100.</summary>
        public int Severity { get; internal set; }

        /// <summary>Priority bucket.</summary>
        public LatencyPriority Priority { get; internal set; }

        /// <summary>Short reason text.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Optional prompt snippet (≤80 chars).</summary>
        public string Snippet { get; internal set; } = "";

        /// <summary>Estimated extra latency (ms) attributable to this finding.</summary>
        public int EstimatedExtraMs { get; internal set; }
    }

    /// <summary>A single playbook action.</summary>
    public sealed class LatencyAction
    {
        /// <summary>Stable id.</summary>
        public string Id { get; internal set; } = "";

        /// <summary>Priority bucket.</summary>
        public LatencyPriority Priority { get; internal set; }

        /// <summary>Short label.</summary>
        public string Label { get; internal set; } = "";

        /// <summary>Reason / rationale.</summary>
        public string Reason { get; internal set; } = "";

        /// <summary>Owner role.</summary>
        public string Owner { get; internal set; } = "prompt_author";

        /// <summary>Modes this action addresses, sorted by name.</summary>
        public IReadOnlyList<string> RelatedFindings { get; internal set; } = Array.Empty<string>();

        /// <summary>Suggested concrete change.</summary>
        public string? SuggestedValue { get; internal set; }

        /// <summary>Estimated savings (ms) if applied.</summary>
        public int EstimatedSavingsMs { get; internal set; }
    }

    /// <summary>Report from <see cref="PromptLatencyBudgetAdvisor.Analyze"/>.</summary>
    public sealed class LatencyReport
    {
        /// <summary>Verdict.</summary>
        public LatencyVerdict Verdict { get; internal set; }

        /// <summary>Letter grade A..F.</summary>
        public string Grade { get; internal set; } = "A";

        /// <summary>Latency score 0..100 (100 = fastest).</summary>
        public int LatencyScore { get; internal set; }

        /// <summary>Estimated total latency in milliseconds (uncached, no streaming credit).</summary>
        public int EstimatedTotalLatencyMs { get; internal set; }

        /// <summary>Estimated time-to-first-token in milliseconds (used when streaming).</summary>
        public int EstimatedTimeToFirstTokenMs { get; internal set; }

        /// <summary>Estimated prompt token count.</summary>
        public int EstimatedPromptTokens { get; internal set; }

        /// <summary>Estimated response token count.</summary>
        public int EstimatedResponseTokens { get; internal set; }

        /// <summary>Findings (priority asc, severity desc, mode-name asc).</summary>
        public IReadOnlyList<LatencyFinding> Findings { get; internal set; } = Array.Empty<LatencyFinding>();

        /// <summary>Playbook actions (priority asc, id asc).</summary>
        public IReadOnlyList<LatencyAction> Playbook { get; internal set; } = Array.Empty<LatencyAction>();

        /// <summary>Sorted insights.</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();

        /// <summary>One-line headline.</summary>
        public string Headline { get; internal set; } = "";

        /// <summary>Prompt augmented with a latency-budget block (only for P0/P1 actions).</summary>
        public string OptimizedDraft { get; internal set; } = "";
    }

    /// <summary>
    /// 12th agentic sibling. Detects latency-cost risks in a prompt (oversized prompt,
    /// chain-of-thought expansion, unbounded output, exhaustive coverage requests,
    /// serial tool chains, retry loops, heavy multimodal inputs, serializable fanout,
    /// missing output cap, streaming disabled) and produces a budgeted playbook
    /// plus an optimized draft. Pure, no I/O, no clock.
    /// </summary>
    public sealed class PromptLatencyBudgetAdvisor
    {
        private static readonly string[] CotMarkers = {
            "think step by step", "step-by-step", "step by step", "chain of thought",
            "show your reasoning", "explain your reasoning", "show your work",
            "walk me through", "reason carefully", "work through this", "let's think",
        };
        private static readonly string[] ExhaustiveMarkers = {
            "comprehensive", "exhaustive", "complete list", "all possible",
            "every single", "in great detail", "thorough analysis", "fully detailed",
            "as detailed as possible", "leave nothing out",
        };
        private static readonly string[] SerialChainMarkers = {
            "then ", " next, ", " after that", " followed by ", " and then ",
            " subsequently", " once that's done",
        };
        private static readonly string[] RetryMarkers = {
            "try again", "retry until", "iterate until", "keep trying",
            "repeat until", "loop until", "until you succeed", "until correct",
            "self-correct", "refine the answer",
        };
        private static readonly string[] MultimodalMarkers = {
            "analyze this image", "analyze the image", "describe this picture",
            "the attached document", "the attached pdf", "the attached file",
            "the uploaded file", "the screenshot", "the photo above",
            "process the document", "ocr this", "transcribe this audio",
        };
        private static readonly string[] FanoutMarkers = {
            "for each ", "one by one", "go through each", "iterate over each",
            "process each", "handle each",
        };
        private static readonly string[] UnboundedOutputMarkers = {
            "as long as needed", "no length limit", "as detailed as", "until done",
            "write everything", "no token limit", "without truncation",
        };

        private static readonly Regex WordRegex = new(@"\S+", RegexOptions.Compiled);
        // "N words/bullets/lines/sentences/paragraphs/items/examples"
        private static readonly Regex OutputCapRegex = new(
            @"\b(\d+)\s+(words?|tokens?|bullets?|lines?|sentences?|paragraphs?|items?|examples?|characters?|chars?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ShortOutputRegex = new(
            @"\b(brief|short|one[\-\s]liner|tl;dr|tldr|concise|terse|in (?:one|1|2|two|3|three) (?:line|sentence|word)s?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // Serial-chain regex: counts ordered-step markers like "Step 1:", "1.", "1)"
        private static readonly Regex NumberedStepRegex = new(
            @"(?m)^\s*(?:step\s+)?(\d+)[\.\)]\s+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Analyze a prompt for latency-budget risks.</summary>
        /// <param name="prompt">Prompt text (non-null).</param>
        /// <param name="context">Optional environment context.</param>
        /// <returns>A <see cref="LatencyReport"/>.</returns>
        public LatencyReport Analyze(string prompt, LatencyContext? context = null)
        {
            if (prompt is null) throw new ArgumentNullException(nameof(prompt));
            context ??= new LatencyContext();
            var lower = prompt.ToLowerInvariant();
            var raw = new List<LatencyFinding>();

            int promptTokens = EstimateTokens(prompt);
            int responseTokens = EstimateResponseTokens(prompt, lower, context);

            // 1. OversizedPrompt
            if (promptTokens > context.PromptTokenBudget)
            {
                int over = promptTokens - context.PromptTokenBudget;
                int sev = Math.Min(100, 40 + (int)(50.0 * over / Math.Max(1, context.PromptTokenBudget)));
                int extra = (int)Math.Round(over * context.MsPerInputToken);
                raw.Add(F(LatencyMode.OversizedPrompt, sev,
                    $"Prompt ~{promptTokens} tokens exceeds budget {context.PromptTokenBudget}.",
                    Preview(prompt, 0), extra));
            }
            else if (promptTokens > (int)(context.PromptTokenBudget * 0.8))
            {
                raw.Add(F(LatencyMode.OversizedPrompt, 30,
                    $"Prompt ~{promptTokens} tokens is within 80% of budget {context.PromptTokenBudget}.",
                    Preview(prompt, 0), 0));
            }

            // 2. ChainOfThoughtExpansion
            if (Contains(lower, CotMarkers))
            {
                int extra = (int)Math.Round(context.MsPerOutputToken * 300); // CoT often adds ~300 tokens
                raw.Add(F(LatencyMode.ChainOfThoughtExpansion, 65,
                    "Prompt asks for explicit step-by-step reasoning; output token count balloons.",
                    Preview(prompt, FirstMatch(lower, CotMarkers)), extra));
            }

            // 3. ExhaustiveCoverage
            if (Contains(lower, ExhaustiveMarkers))
            {
                int extra = (int)Math.Round(context.MsPerOutputToken * 400);
                raw.Add(F(LatencyMode.ExhaustiveCoverage, 70,
                    "Prompt requests exhaustive/comprehensive coverage; expect long output.",
                    Preview(prompt, FirstMatch(lower, ExhaustiveMarkers)), extra));
            }

            // 4. UnboundedOutput / MissingOutputCap
            bool hasCap = OutputCapRegex.IsMatch(prompt) || ShortOutputRegex.IsMatch(prompt);
            bool explicitlyUnbounded = Contains(lower, UnboundedOutputMarkers);
            if (explicitlyUnbounded)
            {
                int extra = (int)Math.Round(context.MsPerOutputToken * 500);
                raw.Add(F(LatencyMode.UnboundedOutput, 75,
                    "Prompt explicitly removes the output cap ('as long as needed' / 'no token limit').",
                    Preview(prompt, FirstMatch(lower, UnboundedOutputMarkers)), extra));
            }
            else if (!hasCap)
            {
                raw.Add(F(LatencyMode.MissingOutputCap, 35,
                    "No explicit output-length cap (e.g. 'in 100 words', 'in 3 bullets').",
                    "", (int)Math.Round(context.MsPerOutputToken * 150)));
            }

            // 5. SerialToolChain
            int chainHits = CountOccurrences(lower, SerialChainMarkers);
            int numberedSteps = NumberedStepRegex.Matches(prompt).Count;
            int chainScore = chainHits + Math.Max(0, numberedSteps - 1);
            if (context.ToolsAvailable && chainScore >= 2)
            {
                int sev = Math.Min(85, 40 + chainScore * 10);
                int extra = (chainScore - 1) * (context.FixedOverheadMs / 2);
                raw.Add(F(LatencyMode.SerialToolChain, sev,
                    $"Detected {chainScore} sequential step markers; each tool hop adds round-trip cost.",
                    Preview(prompt, FirstMatch(lower, SerialChainMarkers)), extra));
            }

            // 6. RetryLoop
            if (Contains(lower, RetryMarkers))
            {
                int extra = context.FixedOverheadMs + (int)Math.Round(context.MsPerOutputToken * responseTokens);
                raw.Add(F(LatencyMode.RetryLoop, 70,
                    "Prompt asks for retry/refine loop; multiplies request cost.",
                    Preview(prompt, FirstMatch(lower, RetryMarkers)), extra));
            }

            // 7. HeavyMultimodalInput
            if (Contains(lower, MultimodalMarkers))
            {
                raw.Add(F(LatencyMode.HeavyMultimodalInput, 55,
                    "Prompt processes attached images/documents/audio inline.",
                    Preview(prompt, FirstMatch(lower, MultimodalMarkers)),
                    context.FixedOverheadMs)); // upload/decode overhead
            }

            // 8. SerializableFanout
            int fanoutHits = CountOccurrences(lower, FanoutMarkers);
            if (fanoutHits >= 1 && !context.ParallelToolCallsEnabled)
            {
                raw.Add(F(LatencyMode.SerializableFanout, 45,
                    "'For each ...' pattern detected and parallel tool calls are off.",
                    Preview(prompt, FirstMatch(lower, FanoutMarkers)),
                    context.FixedOverheadMs));
            }

            // 9. StreamingDisabled
            if (!context.StreamingEnabled)
            {
                raw.Add(F(LatencyMode.StreamingDisabled, 25,
                    "Streaming disabled; user waits for the full response.",
                    "", 0));
            }

            // Priority assignment
            foreach (var f in raw) f.Priority = ComputePriority(f, context);

            // Estimated latency
            int baseMs = context.FixedOverheadMs
                + (int)Math.Round(promptTokens * context.MsPerInputToken)
                + (int)Math.Round(responseTokens * context.MsPerOutputToken);
            int extraMs = raw.Sum(f => f.EstimatedExtraMs);
            int totalMs = baseMs + extraMs;
            // ttft = overhead + prompt encoding + ~1 output token
            int ttftMs = context.FixedOverheadMs
                + (int)Math.Round(promptTokens * context.MsPerInputToken)
                + (int)Math.Round(context.MsPerOutputToken);

            // Score: start at 100, subtract weighted severity penalty, plus budget penalty
            int penalty = 0;
            foreach (var f in raw)
            {
                int w = f.Priority switch
                {
                    LatencyPriority.P0 => 25,
                    LatencyPriority.P1 => 15,
                    LatencyPriority.P2 => 8,
                    _ => 3,
                };
                penalty += (f.Severity * w) / 100;
            }
            // Appetite scaling
            double mult = context.RiskAppetite switch
            {
                LatencyRiskAppetite.Cautious => 1.15,
                LatencyRiskAppetite.Aggressive => 0.85,
                _ => 1.0,
            };
            penalty = (int)Math.Round(penalty * mult);

            // Budget overrun penalty
            if (totalMs > context.LatencyBudgetMs)
            {
                int overPct = (int)(100.0 * (totalMs - context.LatencyBudgetMs) / Math.Max(1, context.LatencyBudgetMs));
                penalty += Math.Min(40, overPct / 2);
            }
            int score = Math.Max(0, Math.Min(100, 100 - penalty));

            // Streaming credit
            if (context.StreamingEnabled) score = Math.Min(100, score + 5);

            // Verdict
            int p0Count = raw.Count(f => f.Priority == LatencyPriority.P0);
            LatencyVerdict verdict;
            if (p0Count >= 2) verdict = LatencyVerdict.Pathological;
            else if (score >= 85) verdict = LatencyVerdict.Fast;
            else if (score >= 65) verdict = LatencyVerdict.Acceptable;
            else if (score >= 40) verdict = LatencyVerdict.Slow;
            else verdict = LatencyVerdict.TooSlow;

            // Grade
            string grade =
                verdict == LatencyVerdict.Pathological ? "F"
                : score >= 85 ? "A"
                : score >= 70 ? "B"
                : score >= 55 ? "C"
                : score >= 40 ? "D"
                : "F";

            // Sort findings
            var findings = raw
                .OrderBy(f => (int)f.Priority)
                .ThenByDescending(f => f.Severity)
                .ThenBy(f => f.Mode.ToString(), StringComparer.Ordinal)
                .ToList();

            // Playbook
            var playbook = BuildPlaybook(findings, context, totalMs);

            // Insights
            var insights = BuildInsights(findings, context, score, totalMs, ttftMs, promptTokens, responseTokens);

            // Optimized draft
            var optimized = BuildOptimizedDraft(prompt, playbook);

            return new LatencyReport
            {
                Verdict = verdict,
                Grade = grade,
                LatencyScore = score,
                EstimatedTotalLatencyMs = totalMs,
                EstimatedTimeToFirstTokenMs = ttftMs,
                EstimatedPromptTokens = promptTokens,
                EstimatedResponseTokens = responseTokens,
                Findings = findings,
                Playbook = playbook,
                Insights = insights,
                Headline = $"{verdict} (Grade {grade}, Score {score}, ~{totalMs}ms)",
                OptimizedDraft = optimized,
            };
        }

        /// <summary>Render the report as plain text.</summary>
        public string ToText(LatencyReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var sb = new StringBuilder();
            sb.AppendLine(report.Headline);
            sb.AppendLine($"  prompt~{report.EstimatedPromptTokens}tok, response~{report.EstimatedResponseTokens}tok, ttft~{report.EstimatedTimeToFirstTokenMs}ms");
            sb.AppendLine();
            sb.AppendLine($"Findings ({report.Findings.Count}):");
            foreach (var f in report.Findings)
                sb.AppendLine($"  - [{f.Priority}] {f.Mode} (sev {f.Severity}, +{f.EstimatedExtraMs}ms): {f.Reason}");
            sb.AppendLine();
            sb.AppendLine($"Playbook ({report.Playbook.Count}):");
            foreach (var a in report.Playbook)
            {
                sb.AppendLine($"  - [{a.Priority}] {a.Id}: {a.Label} (~{a.EstimatedSavingsMs}ms saved) — {a.Reason}");
                if (!string.IsNullOrEmpty(a.SuggestedValue))
                    sb.AppendLine($"      suggestion: {a.SuggestedValue}");
            }
            sb.AppendLine();
            sb.AppendLine($"Insights ({report.Insights.Count}):");
            foreach (var i in report.Insights) sb.AppendLine($"  - {i}");
            return sb.ToString();
        }

        /// <summary>Render the report as Markdown.</summary>
        public string ToMarkdown(LatencyReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var sb = new StringBuilder();
            sb.AppendLine($"# PromptLatencyBudgetAdvisor — {report.Headline}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Verdict: **{report.Verdict}**");
            sb.AppendLine($"- Grade: **{report.Grade}**");
            sb.AppendLine($"- LatencyScore: **{report.LatencyScore}**");
            sb.AppendLine($"- EstimatedTotalLatencyMs: **{report.EstimatedTotalLatencyMs}**");
            sb.AppendLine($"- EstimatedTimeToFirstTokenMs: **{report.EstimatedTimeToFirstTokenMs}**");
            sb.AppendLine($"- EstimatedPromptTokens: **{report.EstimatedPromptTokens}**");
            sb.AppendLine($"- EstimatedResponseTokens: **{report.EstimatedResponseTokens}**");
            sb.AppendLine();
            sb.AppendLine("## Findings");
            if (report.Findings.Count == 0) sb.AppendLine("_None_");
            foreach (var f in report.Findings)
                sb.AppendLine($"- **{f.Mode}** ({f.Priority}, sev {f.Severity}, +{f.EstimatedExtraMs}ms): {f.Reason}");
            sb.AppendLine();
            sb.AppendLine("## Playbook");
            if (report.Playbook.Count == 0) sb.AppendLine("_None_");
            foreach (var a in report.Playbook)
            {
                sb.AppendLine($"- **{a.Id}** ({a.Priority}, owner: {a.Owner}, ~{a.EstimatedSavingsMs}ms saved) — {a.Label}");
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
        public string ToJson(LatencyReport report)
        {
            if (report is null) throw new ArgumentNullException(nameof(report));
            var doc = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["estimatedPromptTokens"] = report.EstimatedPromptTokens,
                ["estimatedResponseTokens"] = report.EstimatedResponseTokens,
                ["estimatedTimeToFirstTokenMs"] = report.EstimatedTimeToFirstTokenMs,
                ["estimatedTotalLatencyMs"] = report.EstimatedTotalLatencyMs,
                ["findings"] = report.Findings.Select(f => (object)new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["estimatedExtraMs"] = f.EstimatedExtraMs,
                    ["mode"] = f.Mode.ToString(),
                    ["priority"] = f.Priority.ToString(),
                    ["reason"] = f.Reason,
                    ["severity"] = f.Severity,
                    ["snippet"] = f.Snippet ?? "",
                }).ToList(),
                ["grade"] = report.Grade,
                ["headline"] = report.Headline,
                ["insights"] = report.Insights.ToList(),
                ["latencyScore"] = report.LatencyScore,
                ["optimizedDraft"] = report.OptimizedDraft,
                ["playbook"] = report.Playbook.Select(a => (object)new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["estimatedSavingsMs"] = a.EstimatedSavingsMs,
                    ["id"] = a.Id,
                    ["label"] = a.Label,
                    ["owner"] = a.Owner,
                    ["priority"] = a.Priority.ToString(),
                    ["reason"] = a.Reason,
                    ["relatedFindings"] = a.RelatedFindings.ToList(),
                    ["suggestedValue"] = a.SuggestedValue,
                }).ToList(),
                ["verdict"] = report.Verdict.ToString(),
            };
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }

        private static LatencyPriority ComputePriority(LatencyFinding f, LatencyContext ctx)
        {
            // Pathological modes that are nearly always blocking
            if (f.Mode == LatencyMode.UnboundedOutput) return LatencyPriority.P0;
            if (f.Mode == LatencyMode.RetryLoop) return LatencyPriority.P0;
            if (f.Mode == LatencyMode.OversizedPrompt && f.Severity >= 70) return LatencyPriority.P0;
            if (f.Mode == LatencyMode.SerialToolChain && f.Severity >= 70 && ctx.ToolsAvailable) return LatencyPriority.P0;
            if (f.Severity >= 60) return LatencyPriority.P1;
            if (f.Severity >= 35) return LatencyPriority.P2;
            return LatencyPriority.P3;
        }

        private static IReadOnlyList<LatencyAction> BuildPlaybook(
            IReadOnlyList<LatencyFinding> findings, LatencyContext ctx, int totalMs)
        {
            var byMode = findings.ToLookup(f => f.Mode);
            var actions = new List<LatencyAction>();

            void Add(string id, LatencyPriority p, string label, string reason, string owner,
                     IEnumerable<LatencyMode> related, int savingsMs, string? suggested = null)
            {
                actions.Add(new LatencyAction
                {
                    Id = id,
                    Priority = p,
                    Label = label,
                    Reason = reason,
                    Owner = owner,
                    RelatedFindings = related.Select(r => r.ToString()).OrderBy(s => s, StringComparer.Ordinal).ToList(),
                    SuggestedValue = suggested,
                    EstimatedSavingsMs = savingsMs,
                });
            }

            if (byMode[LatencyMode.UnboundedOutput].Any())
            {
                int s = byMode[LatencyMode.UnboundedOutput].Sum(f => f.EstimatedExtraMs);
                Add("CapOutputLength", LatencyPriority.P0, "Cap the output length",
                    "Unbounded output lets the model generate until token limit.",
                    "prompt_author", new[] { LatencyMode.UnboundedOutput }, s,
                    "Add: 'Reply in ≤120 words.'");
            }
            if (byMode[LatencyMode.RetryLoop].Any())
            {
                int s = byMode[LatencyMode.RetryLoop].Sum(f => f.EstimatedExtraMs);
                Add("RemoveRetryLoop", LatencyPriority.P0, "Remove in-prompt retry loop",
                    "Self-retry directives multiply request cost; do it client-side with a cap.",
                    "prompt_author", new[] { LatencyMode.RetryLoop }, s,
                    "Replace 'retry until correct' with a single-shot prompt + client-side max_attempts=2.");
            }
            if (byMode[LatencyMode.OversizedPrompt].Any(f => f.Priority == LatencyPriority.P0))
            {
                int s = byMode[LatencyMode.OversizedPrompt].Sum(f => f.EstimatedExtraMs);
                Add("CompressPrompt", LatencyPriority.P0, "Compress the prompt",
                    "Prompt size exceeds budget; encoding + attention dominate latency.",
                    "prompt_author", new[] { LatencyMode.OversizedPrompt }, s,
                    "Summarize background; move boilerplate to a system instruction.");
            }
            else if (byMode[LatencyMode.OversizedPrompt].Any())
            {
                int s = byMode[LatencyMode.OversizedPrompt].Sum(f => f.EstimatedExtraMs);
                Add("TrimContextWindow", LatencyPriority.P2, "Trim mid-history context",
                    "Prompt is approaching budget; drop low-signal turns or summarize.",
                    "prompt_author", new[] { LatencyMode.OversizedPrompt }, s);
            }
            if (byMode[LatencyMode.SerialToolChain].Any())
            {
                int s = byMode[LatencyMode.SerialToolChain].Sum(f => f.EstimatedExtraMs);
                var pri = ctx.ToolsAvailable && findings.Any(f => f.Mode == LatencyMode.SerialToolChain && f.Severity >= 70)
                    ? LatencyPriority.P0 : LatencyPriority.P1;
                Add("ParallelizeToolCalls", pri, "Parallelize independent tool calls",
                    "Serial tool hops add per-call overhead; fan out independent steps.",
                    "platform", new[] { LatencyMode.SerialToolChain }, s,
                    "Enable parallel tool calls or batch independent requests.");
            }
            if (byMode[LatencyMode.ChainOfThoughtExpansion].Any())
            {
                int s = byMode[LatencyMode.ChainOfThoughtExpansion].Sum(f => f.EstimatedExtraMs);
                Add("TrimChainOfThought", LatencyPriority.P1, "Replace CoT with structured output",
                    "Explicit step-by-step reasoning balloons response size.",
                    "prompt_author", new[] { LatencyMode.ChainOfThoughtExpansion }, s,
                    "Ask for a short answer + a 1-line rationale, or move CoT to a draft pass.");
            }
            if (byMode[LatencyMode.ExhaustiveCoverage].Any())
            {
                int s = byMode[LatencyMode.ExhaustiveCoverage].Sum(f => f.EstimatedExtraMs);
                Add("NarrowScope", LatencyPriority.P1, "Narrow the coverage scope",
                    "'Comprehensive'/'exhaustive' wording invites long answers.",
                    "prompt_author", new[] { LatencyMode.ExhaustiveCoverage }, s,
                    "Ask for the top 3-5 items only; allow follow-up for depth.");
            }
            if (byMode[LatencyMode.HeavyMultimodalInput].Any())
            {
                int s = byMode[LatencyMode.HeavyMultimodalInput].Sum(f => f.EstimatedExtraMs);
                Add("PreprocessMultimodalInput", LatencyPriority.P1, "Preprocess multimodal inputs",
                    "Image/document/audio inputs add upload + decode overhead.",
                    "platform", new[] { LatencyMode.HeavyMultimodalInput }, s,
                    "Resize images, OCR documents, or cache embeddings.");
            }
            if (byMode[LatencyMode.SerializableFanout].Any())
            {
                int s = byMode[LatencyMode.SerializableFanout].Sum(f => f.EstimatedExtraMs);
                Add("EnableParallelFanout", LatencyPriority.P2, "Enable parallel fanout",
                    "'For each ...' pattern serializes work that could run in parallel.",
                    "platform", new[] { LatencyMode.SerializableFanout }, s,
                    "Process items concurrently when independent.");
            }
            if (byMode[LatencyMode.MissingOutputCap].Any())
            {
                int s = byMode[LatencyMode.MissingOutputCap].Sum(f => f.EstimatedExtraMs);
                Add("AddOutputCap", LatencyPriority.P2, "Add an explicit output cap",
                    "No length cap encourages overlong responses.",
                    "prompt_author", new[] { LatencyMode.MissingOutputCap }, s,
                    "Append a budget such as 'in ≤5 bullets' or 'in 120 words'.");
            }
            if (byMode[LatencyMode.StreamingDisabled].Any())
            {
                Add("EnableStreaming", LatencyPriority.P2, "Enable streaming",
                    "Streaming returns the first token sooner and improves perceived latency.",
                    "platform", new[] { LatencyMode.StreamingDisabled },
                    Math.Max(0, totalMs / 4),
                    "Set stream=true on the request.");
            }

            // Cautious: append a perf review when any P0/P1
            if (ctx.RiskAppetite == LatencyRiskAppetite.Cautious
                && actions.Any(a => a.Priority == LatencyPriority.P0 || a.Priority == LatencyPriority.P1))
            {
                Add("SchedulePerfReview", LatencyPriority.P2, "Schedule a latency review",
                    "Cautious appetite + P0/P1 finding triggers a perf review.",
                    "platform", Array.Empty<LatencyMode>(), 0);
            }

            // Aggressive: trim P3 when P0/P1 exists
            if (ctx.RiskAppetite == LatencyRiskAppetite.Aggressive
                && actions.Any(a => a.Priority == LatencyPriority.P0 || a.Priority == LatencyPriority.P1))
            {
                actions.RemoveAll(a => a.Priority == LatencyPriority.P3);
            }

            // OK action if nothing else fired
            if (actions.Count == 0)
            {
                Add("LATENCY_OK", LatencyPriority.P3, "No latency risks detected",
                    "No matching modes; prompt fits comfortably in budget.",
                    "prompt_author", Array.Empty<LatencyMode>(), 0);
            }

            return actions
                .OrderBy(a => (int)a.Priority)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static IReadOnlyList<string> BuildInsights(
            IReadOnlyList<LatencyFinding> findings, LatencyContext ctx,
            int score, int totalMs, int ttftMs, int promptTokens, int responseTokens)
        {
            var ins = new SortedSet<string>(StringComparer.Ordinal);
            if (findings.Count == 0)
            {
                ins.Add("No latency risks were detected in the prompt.");
            }
            else
            {
                ins.Add($"Detected {findings.Count} latency mode(s); top priority is {findings.Min(f => (int)f.Priority).ToString()} ({(LatencyPriority)findings.Min(f => (int)f.Priority)}).");
            }
            if (ctx.StreamingEnabled) ins.Add($"Streaming enabled — perceived latency ≈ ttft {ttftMs}ms.");
            else ins.Add("Streaming disabled — user waits for full response.");
            if (totalMs > ctx.LatencyBudgetMs)
                ins.Add($"Estimated latency {totalMs}ms exceeds budget {ctx.LatencyBudgetMs}ms.");
            else
                ins.Add($"Estimated latency {totalMs}ms fits within budget {ctx.LatencyBudgetMs}ms.");
            if (promptTokens > ctx.PromptTokenBudget)
                ins.Add($"Prompt tokens {promptTokens} over budget {ctx.PromptTokenBudget}.");
            if (responseTokens > ctx.ResponseTokenBudget)
                ins.Add($"Projected response tokens {responseTokens} over budget {ctx.ResponseTokenBudget}.");
            ins.Add($"Latency score: {score}/100.");
            return ins.ToList();
        }

        private static string BuildOptimizedDraft(string prompt, IReadOnlyList<LatencyAction> playbook)
        {
            var highPriority = playbook
                .Where(a => a.Priority == LatencyPriority.P0 || a.Priority == LatencyPriority.P1)
                .ToList();
            if (highPriority.Count == 0) return prompt;
            var sb = new StringBuilder();
            sb.Append(prompt);
            if (!prompt.EndsWith("\n", StringComparison.Ordinal)) sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("# LATENCY_BUDGET (auto-inserted by PromptLatencyBudgetAdvisor)");
            foreach (var a in highPriority)
            {
                sb.AppendLine($"# - [{a.Priority}] {a.Id}: {a.Label} (~{a.EstimatedSavingsMs}ms saved)");
                if (!string.IsNullOrEmpty(a.SuggestedValue))
                    sb.AppendLine($"#   suggestion: {a.SuggestedValue}");
            }
            return sb.ToString();
        }

        private static LatencyFinding F(LatencyMode mode, int severity, string reason, string snippet, int extraMs)
            => new() { Mode = mode, Severity = severity, Reason = reason, Snippet = snippet, EstimatedExtraMs = extraMs };

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Rough heuristic: 1 token ≈ 4 chars OR ~0.75 words; take the max.
            int byChars = text.Length / 4;
            int byWords = (int)(WordRegex.Matches(text).Count / 0.75);
            return Math.Max(1, Math.Max(byChars, byWords));
        }

        private static int EstimateResponseTokens(string prompt, string lower, LatencyContext ctx)
        {
            // Honor explicit caps if present.
            var m = OutputCapRegex.Match(prompt);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
            {
                var unit = m.Groups[2].Value.ToLowerInvariant();
                if (unit.StartsWith("token")) return Math.Min(n, ctx.ResponseTokenBudget * 2);
                if (unit.StartsWith("char")) return Math.Max(1, n / 4);
                if (unit.StartsWith("word")) return Math.Max(1, (int)(n / 0.75));
                // bullets/lines/sentences/paragraphs/items/examples
                int perItem = unit.StartsWith("paragraph") ? 60 : unit.StartsWith("sentence") ? 25 : 20;
                return Math.Max(1, n * perItem);
            }
            if (ShortOutputRegex.IsMatch(prompt)) return Math.Min(60, ctx.ResponseTokenBudget);

            int est = ctx.ResponseTokenBudget;
            if (Contains(lower, CotMarkers)) est += 300;
            if (Contains(lower, ExhaustiveMarkers)) est += 400;
            if (Contains(lower, UnboundedOutputMarkers)) est += 600;
            return est;
        }

        private static bool Contains(string lower, IEnumerable<string> needles)
        {
            foreach (var n in needles) if (lower.Contains(n, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int CountOccurrences(string lower, IEnumerable<string> needles)
        {
            int total = 0;
            foreach (var n in needles)
            {
                int idx = 0;
                while ((idx = lower.IndexOf(n, idx, StringComparison.Ordinal)) >= 0)
                {
                    total++;
                    idx += n.Length;
                }
            }
            return total;
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
