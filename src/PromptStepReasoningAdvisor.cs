namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>Risk appetite for <see cref="PromptStepReasoningAdvisor"/>.</summary>
    public enum StepReasoningRiskAppetite
    {
        /// <summary>Stricter scoring; appends second-reviewer action at C/D/F.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient scoring; trims P3 fallback when higher-priority items present.</summary>
        Aggressive,
    }

    /// <summary>Priority bucket for step-reasoning findings and playbook actions.</summary>
    public enum StepReasoningPriority
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

    /// <summary>Per-prompt verdict ladder for the reasoning contract.</summary>
    public enum StepReasoningVerdict
    {
        /// <summary>Reasoning protocol is clear and well-tuned.</summary>
        Ready,
        /// <summary>Some gaps - human review recommended.</summary>
        Review,
        /// <summary>Significant gaps - rewrite recommended before sending.</summary>
        RewriteRecommended,
        /// <summary>Do not send: high-severity reasoning failure detected.</summary>
        BlockSend,
    }

    /// <summary>Options for <see cref="PromptStepReasoningAdvisor.Analyze"/>.</summary>
    public sealed class StepReasoningOptions
    {
        /// <summary>Risk appetite knob.</summary>
        public StepReasoningRiskAppetite RiskAppetite { get; init; } = StepReasoningRiskAppetite.Balanced;

        /// <summary>Skip the <c>UNGROUNDED_SELF_CHECK</c> detector
        /// (useful when self-check criteria live in a separate system prompt section).</summary>
        public bool SkipSelfCheckCheck { get; init; }
    }

    /// <summary>A single detected gap in the reasoning contract.</summary>
    public sealed class StepReasoningFinding
    {
        /// <summary>Detector code.</summary>
        public string Code { get; internal set; } = "";
        /// <summary>Severity 0..100.</summary>
        public int Severity { get; internal set; }
        /// <summary>Priority bucket.</summary>
        public StepReasoningPriority Priority { get; internal set; }
        /// <summary>Short human-readable label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Reason / detail.</summary>
        public string Reason { get; internal set; } = "";
    }

    /// <summary>One playbook action recommended by the step-reasoning advisor.</summary>
    public sealed class StepReasoningPlaybookAction
    {
        /// <summary>Stable action id.</summary>
        public string Id { get; internal set; } = "";
        /// <summary>Priority bucket.</summary>
        public StepReasoningPriority Priority { get; internal set; }
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

    /// <summary>Report produced by <see cref="PromptStepReasoningAdvisor.Analyze"/>.</summary>
    public sealed class StepReasoningReport
    {
        /// <summary>0..100 score; higher is better.</summary>
        public int ReasoningScore { get; internal set; }
        /// <summary>A-F grade.</summary>
        public char Grade { get; internal set; }
        /// <summary>Verdict ladder.</summary>
        public StepReasoningVerdict Verdict { get; internal set; }
        /// <summary>Headline summary.</summary>
        public string Headline { get; internal set; } = "";
        /// <summary>Per-detector findings.</summary>
        public IReadOnlyList<StepReasoningFinding> Findings { get; internal set; } = Array.Empty<StepReasoningFinding>();
        /// <summary>Ranked P0-first playbook.</summary>
        public IReadOnlyList<StepReasoningPlaybookAction> Playbook { get; internal set; } = Array.Empty<StepReasoningPlaybookAction>();
        /// <summary>Cross-prompt insights.</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();
        /// <summary>Paste-ready prompt with an appended `# Reasoning Contract` block.</summary>
        public string StepReasoningTightenedDraft { get; internal set; } = "";
        /// <summary>Generation timestamp (deterministic when an explicit clock is injected).</summary>
        public DateTime GeneratedAt { get; internal set; }

        /// <summary>Render a plain-text view.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{Verdict}] {Headline}");
            sb.AppendLine($"Score: {ReasoningScore}/100   Grade: {Grade}");
            if (Findings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Findings:");
                foreach (var f in Findings)
                {
                    sb.AppendLine($"  [{f.Priority}] {f.Code} (sev {f.Severity}) - {f.Label}");
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
            sb.AppendLine($"# Reasoning Contract Audit");
            sb.AppendLine();
            sb.AppendLine($"**Verdict:** `{Verdict}`  ");
            sb.AppendLine($"**Score:** `{ReasoningScore}/100`  ");
            sb.AppendLine($"**Grade:** `{Grade}`");
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
                sb.AppendLine("| Priority | Code | Sev | Label |");
                sb.AppendLine("|---|---|---:|---|");
                foreach (var f in Findings)
                    sb.AppendLine($"| {f.Priority} | `{f.Code}` | {f.Severity} | {EscapePipe(f.Label)} |");
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

        /// <summary>Render a deterministic JSON view (System.Text.Json, indented, enum-as-string).</summary>
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
    /// Agentic step-reasoning advisor: scans a prompt to verify it sets up the model to reason
    /// carefully (or NOT reason verbosely when it shouldn't). 7th sibling to
    /// PromptHallucinationRiskScorer / PromptDefenseAdvisor / PromptKnowledgeFreshnessAdvisor /
    /// PromptExampleQualityAdvisor / PromptInstructionConflictAdvisor / PromptOutputContractAdvisor.
    /// </summary>
    public sealed class PromptStepReasoningAdvisor
    {
        private readonly Func<DateTime> _now;

        /// <summary>Create a new advisor. Pass an explicit clock for deterministic tests.</summary>
        public PromptStepReasoningAdvisor(Func<DateTime>? nowProvider = null)
        {
            _now = nowProvider ?? (() => DateTime.UtcNow);
        }

        // -----------------------------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------------------------

        /// <summary>Analyze a prompt and produce a structured reasoning-contract report.</summary>
        public StepReasoningReport Analyze(string prompt, StepReasoningOptions? options = null)
        {
            options ??= new StepReasoningOptions();
            prompt ??= string.Empty;
            string trimmed = prompt.Trim();
            var lower = prompt.ToLowerInvariant();
            bool nonEmpty = trimmed.Length > 0;

            bool hasCoT = CoTPattern.IsMatch(lower);
            bool hasComplex = ComplexTaskPattern.IsMatch(lower);
            bool hasTrivial = TrivialTaskPattern.IsMatch(lower);
            bool hasShowWork = ShowWorkPattern.IsMatch(lower);
            bool hasReasoningFormat = ReasoningFormatPattern.IsMatch(lower);
            bool hasConcise = ConcisePattern.IsMatch(lower);
            bool hasAnswerOnly = AnswerOnlyPattern.IsMatch(lower);
            bool hasFinalDelimiter = FinalDelimiterPattern.IsMatch(prompt) || FinalDelimiterLowerPattern.IsMatch(lower);
            bool hasSequenceCue = SequenceCuePattern.IsMatch(lower);
            bool hasConcreteSteps = ConcreteStepsPattern.IsMatch(prompt);
            bool hasLatency = LatencyPattern.IsMatch(lower);
            bool hasSelfCheck = SelfCheckPattern.IsMatch(lower);
            bool hasSelfCheckCriteria = SelfCheckCriteriaPattern.IsMatch(lower);
            bool hasStopCondition = StopConditionPattern.IsMatch(lower);

            var findings = new List<StepReasoningFinding>();

            // 1. MISSING_REASONING_GUIDANCE (sev 55)
            if (nonEmpty && hasComplex && !hasCoT)
            {
                findings.Add(MakeFinding("MISSING_REASONING_GUIDANCE", 55,
                    "Complex task without chain-of-thought guidance",
                    "Prompt requires multi-step reasoning but does not invite the model to think step by step / show its work / break the problem down."));
            }

            // 2. OVERPRESCRIBED_REASONING (sev 30)
            if (hasCoT && hasTrivial && !hasComplex)
            {
                findings.Add(MakeFinding("OVERPRESCRIBED_REASONING", 30,
                    "Chain-of-thought requested for a trivial task",
                    "Task is a simple classification / yes-no / pick-one but the prompt requests verbose reasoning, wasting tokens and latency."));
            }

            // 3. NO_FINAL_ANSWER_DELIMITER (sev 45)
            if (hasCoT && !hasFinalDelimiter)
            {
                findings.Add(MakeFinding("NO_FINAL_ANSWER_DELIMITER", 45,
                    "Chain-of-thought requested without a final-answer marker",
                    "If the model reasons aloud, downstream consumers need a marker (e.g., 'Final answer:', '## Answer', '<answer>') to extract the result."));
            }

            // 4. UNSPECIFIED_REASONING_FORMAT (sev 35)
            if (hasShowWork && !hasReasoningFormat)
            {
                findings.Add(MakeFinding("UNSPECIFIED_REASONING_FORMAT", 35,
                    "'Show your work' without a reasoning format",
                    "Prompt asks the model to explain its reasoning but does not say how (numbered steps, bullets, sections, etc.)."));
            }

            // 5. CONFLICTING_REASONING_DIRECTIVES (sev 60)
            bool conflictA = hasConcise && hasCoT;
            bool conflictB = hasAnswerOnly && hasShowWork;
            if (conflictA || conflictB)
            {
                findings.Add(MakeFinding("CONFLICTING_REASONING_DIRECTIVES", 60,
                    "Conflicting reasoning directives",
                    conflictB
                        ? "Prompt simultaneously asks for the answer only and for the model to show its work."
                        : "Prompt simultaneously asks for concise output and chain-of-thought reasoning."));
            }

            // 6. VAGUE_STEP_SCAFFOLD (sev 25)
            if (hasSequenceCue && !hasConcreteSteps)
            {
                findings.Add(MakeFinding("VAGUE_STEP_SCAFFOLD", 25,
                    "Sequence cue without concrete steps",
                    "Prompt mentions a step sequence (first/then/finally, step 1, step 2) but does not list the concrete steps the model should follow."));
            }

            // 7. LATENCY_VS_REASONING_CONFLICT (sev 75, P0, BlockSend trigger)
            if (hasLatency && hasCoT)
            {
                findings.Add(MakeFinding("LATENCY_VS_REASONING_CONFLICT", 75,
                    "Latency-critical task with verbose reasoning",
                    "Prompt is latency-sensitive (real-time / low-latency / fast) but also asks the model to reason verbosely; this is contradictory."));
            }

            // 8. UNGROUNDED_SELF_CHECK (sev 35)
            if (!options.SkipSelfCheckCheck && hasSelfCheck && !hasSelfCheckCriteria)
            {
                findings.Add(MakeFinding("UNGROUNDED_SELF_CHECK", 35,
                    "Self-check requested without criteria",
                    "Prompt asks the model to verify its answer but does not state what to check against (rules, schema, constraints)."));
            }

            // 9. REASONING_SUPPRESSED_FOR_COMPLEX_TASK (sev 70, P0, BlockSend trigger)
            if (hasAnswerOnly && hasComplex)
            {
                findings.Add(MakeFinding("REASONING_SUPPRESSED_FOR_COMPLEX_TASK", 70,
                    "Reasoning suppressed for a complex task",
                    "Prompt forbids explanation / asks for the answer only on a task that benefits from chain-of-thought; accuracy is at risk."));
            }

            // 10. NO_STOP_CONDITION_FOR_THOUGHT (sev 30)
            if (hasCoT && !hasStopCondition)
            {
                findings.Add(MakeFinding("NO_STOP_CONDITION_FOR_THOUGHT", 30,
                    "Chain-of-thought requested without a stop condition",
                    "Prompt invites reasoning but does not bound it (max steps, 'stop when', length budget); the model may ramble."));
            }

            // Dedupe by Code (keep the highest-severity).
            findings = findings
                .GroupBy(f => f.Code)
                .Select(g => g.MaxBy(f => f.Severity)!)
                .OrderByDescending(f => f.Severity)
                .ThenBy(f => f.Code, StringComparer.Ordinal)
                .ToList();

            // Assign priority per finding.
            foreach (var f in findings)
                f.Priority = BucketPriority(f.Severity);

            // Score.
            int topSev = findings.Count == 0 ? 0 : findings.Max(f => f.Severity);
            int restSum = findings.Count <= 1 ? 0 : findings.OrderByDescending(f => f.Severity).Skip(1).Sum(f => f.Severity);
            double appetiteMult = options.RiskAppetite switch
            {
                StepReasoningRiskAppetite.Cautious => 1.15,
                StepReasoningRiskAppetite.Aggressive => 0.85,
                _ => 1.0,
            };
            int rawPenalty = (int)Math.Round((topSev + 0.4 * Math.Min(restSum, 60)) * appetiteMult);
            int reasoningScore = Math.Max(0, Math.Min(100, 100 - rawPenalty));

            // Verdict ladder.
            bool blockTrigger = findings.Any(f =>
                (f.Code == "LATENCY_VS_REASONING_CONFLICT" && f.Severity >= 70) ||
                (f.Code == "REASONING_SUPPRESSED_FOR_COMPLEX_TASK" && f.Severity >= 70));
            StepReasoningVerdict verdict;
            if (blockTrigger) verdict = StepReasoningVerdict.BlockSend;
            else if (reasoningScore < 50) verdict = StepReasoningVerdict.RewriteRecommended;
            else if (reasoningScore < 75) verdict = StepReasoningVerdict.Review;
            else verdict = StepReasoningVerdict.Ready;

            // Grade.
            char grade;
            if (verdict == StepReasoningVerdict.BlockSend) grade = 'F';
            else if (reasoningScore >= 85) grade = 'A';
            else if (reasoningScore >= 70) grade = 'B';
            else if (reasoningScore >= 55) grade = 'C';
            else if (reasoningScore >= 40) grade = 'D';
            else grade = 'F';

            // Playbook (synthesised from findings).
            var playbook = BuildPlaybook(findings, options, grade);

            // Insights.
            var insights = BuildInsights(findings);

            // Tightened draft.
            string draft = BuildTightenedDraft(prompt, findings);

            string headline = findings.Count == 0
                ? "Reasoning contract is well-tuned."
                : $"{findings.Count} reasoning gap(s) detected; top: {findings[0].Code}.";

            return new StepReasoningReport
            {
                ReasoningScore = reasoningScore,
                Grade = grade,
                Verdict = verdict,
                Headline = headline,
                Findings = findings,
                Playbook = playbook,
                Insights = insights,
                StepReasoningTightenedDraft = draft,
                GeneratedAt = _now(),
            };
        }

        // -----------------------------------------------------------------------------------------
        // Detector helpers
        // -----------------------------------------------------------------------------------------

        private static readonly Regex CoTPattern =
            new(@"\bthink (it )?step[- ]by[- ]step\b|\breason through\b|\bwork (it )?out\b|\bshow your (work|reasoning)\b|\bexplain your (reasoning|thinking|steps)\b|\bbreak (it|this|the problem) down\b|\bwalk (me )?through\b|\bchain[- ]of[- ]thought\b|\blet'?s think\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ComplexTaskPattern =
            new(@"\b(calculate|compute|prove|derive|solve|integrate|differentiate|optimi[sz]e|compare|contrast|multi[- ]step|plan|debug|troubleshoot|analy[sz]e|investigate|diagnose|design an algorithm|reason about|infer|deduce)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TrivialTaskPattern =
            new(@"\b(classify|label|categori[sz]e|tag|yes[/ ]?no|true[/ ]?false|pick one|select one|multiple choice|choose (one|from)|sentiment)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ShowWorkPattern =
            new(@"\bshow your (work|reasoning|steps)\b|\bexplain your (reasoning|thinking|steps|answer)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ReasoningFormatPattern =
            new(@"\b(numbered (steps|list)|bullet (points|list)|in bullets|as a list|use sections|under headings|in markdown|step\s*\d+|use the format|in the following format)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ConcisePattern =
            new(@"\b(be concise|keep it (short|brief|terse)|terse|one[- ]sentence|in one sentence|as briefly as possible|no fluff|minimal output|short answer)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AnswerOnlyPattern =
            new(@"\b(do not explain|don't explain|just the answer|answer only|only (the )?answer|no reasoning|skip explanation|without explanation|answer directly|no preamble|return only)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FinalDelimiterPattern =
            new(@"<answer>|</answer>|##\s*Answer\b|===+|---+\s*answer|\bFINAL ANSWER\b",
                RegexOptions.Compiled);

        private static readonly Regex FinalDelimiterLowerPattern =
            new(@"\bfinal answer\s*[:=]|\banswer\s*:\s*$|\btherefore,?\b|\bconclusion\s*:|\bthe answer is\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex SequenceCuePattern =
            new(@"\bfirst[\s,].{0,80}\bthen\b.{0,80}\b(finally|lastly|at the end)\b|\bstep\s*1\b|\bstep\s*one\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Two or more "step N" / numbered "1. ... 2. ..." lines == concrete steps.
        private static readonly Regex ConcreteStepsPattern =
            new(@"(?is)(step\s*\d+[:.\s].{0,200}){2,}|(\b1[.)]\s+\S.{0,200}\b2[.)]\s+\S)",
                RegexOptions.Compiled);

        private static readonly Regex LatencyPattern =
            new(@"\breal[- ]time\b|\blow[- ]latency\b|\bfast response\b|\brespond quickly\b|\bunder \d+\s*(ms|milliseconds?|seconds?|s)\b|\bstreaming\b|\binteractive (chat|response)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SelfCheckPattern =
            new(@"\bdouble[- ]?check\b|\bverify your (answer|work|output)\b|\bcheck your (work|answer)\b|\bself[- ]?check\b|\bvalidate (your )?(answer|response|output)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SelfCheckCriteriaPattern =
            new(@"\bagainst\b|\busing the (rules|criteria|schema|constraints)\b|\bcompare (it|the answer) (to|with|against)\b|\bensure (that )?(it|the answer) (matches|satisfies|follows)\b|\baccording to (the )?(rules|criteria|schema)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex StopConditionPattern =
            new(@"\bstop (when|once|after)\b|\bat most \d+ (steps?|iterations?)\b|\bno more than \d+ (steps?|iterations?)\b|\bmax(imum)? \d+ (steps?|iterations?)\b|\buntil\b|\b(in|under) \d+ (steps?|iterations?)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static StepReasoningFinding MakeFinding(string code, int sev, string label, string reason)
            => new() { Code = code, Severity = sev, Label = label, Reason = reason };

        private static StepReasoningPriority BucketPriority(int sev)
        {
            if (sev >= 65) return StepReasoningPriority.P0;
            if (sev >= 45) return StepReasoningPriority.P1;
            if (sev >= 25) return StepReasoningPriority.P2;
            return StepReasoningPriority.P3;
        }

        // -----------------------------------------------------------------------------------------
        // Playbook + insights + draft
        // -----------------------------------------------------------------------------------------

        private static List<StepReasoningPlaybookAction> BuildPlaybook(
            List<StepReasoningFinding> findings, StepReasoningOptions opts, char grade)
        {
            var actions = new List<StepReasoningPlaybookAction>();

            bool Has(string code) => findings.Any(f => f.Code == code);

            if (Has("LATENCY_VS_REASONING_CONFLICT"))
                actions.Add(Action("RESOLVE_LATENCY_VS_REASONING", StepReasoningPriority.P0,
                    "Resolve the latency-vs-reasoning conflict",
                    "Either drop the chain-of-thought requirement or relax the latency constraint; you cannot have both."));
            if (Has("REASONING_SUPPRESSED_FOR_COMPLEX_TASK"))
                actions.Add(Action("REENABLE_REASONING_FOR_COMPLEX", StepReasoningPriority.P0,
                    "Re-enable reasoning for a complex task",
                    "Remove the 'answer only / no explanation' directive on tasks that require multi-step reasoning, or split the request into two stages."));
            if (Has("MISSING_REASONING_GUIDANCE"))
                actions.Add(Action("DECLARE_REASONING_PROTOCOL", StepReasoningPriority.P1,
                    "Declare an explicit reasoning protocol",
                    "Tell the model how to reason (e.g., 'think step by step', 'break the problem down') and where to put the final answer."));
            if (Has("NO_FINAL_ANSWER_DELIMITER"))
                actions.Add(Action("ADD_FINAL_ANSWER_DELIMITER", StepReasoningPriority.P1,
                    "Add a final-answer delimiter",
                    "After reasoning, require the model to emit a marker such as '## Answer', '<answer>', or 'Final answer:' so downstream code can extract it."));
            if (Has("CONFLICTING_REASONING_DIRECTIVES"))
                actions.Add(Action("RESOLVE_REASONING_CONFLICT", StepReasoningPriority.P1,
                    "Resolve conflicting reasoning directives",
                    "Pick one: concise answer OR chain-of-thought, not both. Remove the contradictory directive."));
            if (Has("UNSPECIFIED_REASONING_FORMAT"))
                actions.Add(Action("SPECIFY_REASONING_FORMAT", StepReasoningPriority.P1,
                    "Specify the reasoning format",
                    "Tell the model how to lay out its reasoning (numbered steps, bullets, sections) so the output stays scannable."));
            if (Has("OVERPRESCRIBED_REASONING"))
                actions.Add(Action("REMOVE_OVERPRESCRIBED_COT", StepReasoningPriority.P2,
                    "Remove over-prescribed chain-of-thought",
                    "The task is trivial; drop the 'think step by step' directive to save tokens and latency."));
            if (Has("VAGUE_STEP_SCAFFOLD"))
                actions.Add(Action("LIST_CONCRETE_STEPS", StepReasoningPriority.P2,
                    "List the concrete steps explicitly",
                    "Replace 'first ... then ... finally' with an enumerated list of the specific steps the model should follow."));
            if (Has("UNGROUNDED_SELF_CHECK"))
                actions.Add(Action("ADD_SELF_CHECK_CRITERIA", StepReasoningPriority.P2,
                    "Add self-check criteria",
                    "If you ask the model to verify its work, tell it what to check against (rules, schema, constraints)."));
            if (Has("NO_STOP_CONDITION_FOR_THOUGHT"))
                actions.Add(Action("ADD_REASONING_STOP_CONDITION", StepReasoningPriority.P2,
                    "Add a stop condition for reasoning",
                    "Bound the chain-of-thought (e.g., 'at most 5 steps', 'stop when you reach a numeric answer') so the model doesn't ramble."));

            if (opts.RiskAppetite == StepReasoningRiskAppetite.Cautious && (grade == 'C' || grade == 'D' || grade == 'F'))
                actions.Add(Action("SECOND_REVIEWER", StepReasoningPriority.P2,
                    "Solicit a second reviewer",
                    "Cautious mode + non-passing grade: have another author review the reasoning contract before send."));

            if (actions.Count == 0)
                actions.Add(Action("STEP_REASONING_OK", StepReasoningPriority.P3,
                    "Reasoning contract is well-tuned",
                    "No reasoning gaps detected; ship as-is."));

            if (opts.RiskAppetite == StepReasoningRiskAppetite.Aggressive)
            {
                if (actions.Any(a => a.Priority < StepReasoningPriority.P3))
                    actions = actions.Where(a => a.Priority < StepReasoningPriority.P3).ToList();
            }

            actions = actions
                .GroupBy(a => a.Id)
                .Select(g => g.First())
                .OrderBy(a => (int)a.Priority)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();

            return actions;
        }

        private static StepReasoningPlaybookAction Action(
            string id, StepReasoningPriority priority, string label, string reason,
            string owner = "prompt_author", int blast = 1)
            => new()
            {
                Id = id,
                Priority = priority,
                Label = label,
                Reason = reason,
                Owner = owner,
                BlastRadius = blast,
                Reversibility = "high",
            };

        private static List<string> BuildInsights(List<StepReasoningFinding> findings)
        {
            var insights = new List<string>();
            bool Has(string c) => findings.Any(f => f.Code == c);

            if (Has("MISSING_REASONING_GUIDANCE")) insights.Add("REASONING_GAP");
            if (Has("OVERPRESCRIBED_REASONING")) insights.Add("OVERTHINKING_RISK");
            if (Has("CONFLICTING_REASONING_DIRECTIVES")) insights.Add("CONFLICTING_REASONING_RULES");
            if (Has("LATENCY_VS_REASONING_CONFLICT")) insights.Add("LATENCY_REASONING_TENSION");
            if (Has("REASONING_SUPPRESSED_FOR_COMPLEX_TASK")) insights.Add("ACCURACY_AT_RISK");
            if (findings.Count == 0) insights.Add("WELL_TUNED_REASONING");
            return insights;
        }

        private static string BuildTightenedDraft(string prompt, List<StepReasoningFinding> findings)
        {
            var sb = new StringBuilder();
            sb.Append(prompt);
            if (!prompt.EndsWith("\n")) sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("# Reasoning Contract");
            var todoFindings = findings
                .Where(f => f.Priority == StepReasoningPriority.P0 || f.Priority == StepReasoningPriority.P1)
                .ToList();
            if (todoFindings.Count == 0)
            {
                sb.AppendLine("- [x] Reasoning contract reviewed: no P0/P1 gaps.");
            }
            else
            {
                foreach (var f in todoFindings)
                    sb.AppendLine($"- [ ] {f.Code}: {f.Label} - {f.Reason}");
            }

            bool needScaffold = findings.Any(f =>
                f.Code == "MISSING_REASONING_GUIDANCE" ||
                f.Code == "NO_FINAL_ANSWER_DELIMITER" ||
                f.Code == "UNSPECIFIED_REASONING_FORMAT" ||
                f.Code == "VAGUE_STEP_SCAFFOLD" ||
                f.Code == "NO_STOP_CONDITION_FOR_THOUGHT");
            if (needScaffold)
            {
                sb.AppendLine();
                sb.AppendLine("# Suggested scaffolding");
                if (findings.Any(f => f.Code == "MISSING_REASONING_GUIDANCE"))
                    sb.AppendLine("Reasoning protocol: think step by step, then emit the final answer.");
                if (findings.Any(f => f.Code == "UNSPECIFIED_REASONING_FORMAT" || f.Code == "VAGUE_STEP_SCAFFOLD"))
                    sb.AppendLine("Reasoning format: numbered steps (1., 2., 3., ...).");
                if (findings.Any(f => f.Code == "NO_FINAL_ANSWER_DELIMITER"))
                    sb.AppendLine("Final-answer marker: ## Answer");
                if (findings.Any(f => f.Code == "NO_STOP_CONDITION_FOR_THOUGHT"))
                    sb.AppendLine("Stop condition: at most 5 steps; stop once you reach the final answer.");
            }
            return sb.ToString();
        }
    }
}
