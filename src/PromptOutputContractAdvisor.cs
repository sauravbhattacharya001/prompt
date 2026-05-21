namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>Risk appetite for <see cref="PromptOutputContractAdvisor"/>.</summary>
    public enum OutputContractRiskAppetite
    {
        /// <summary>Stricter scoring; appends second-reviewer action at C/D/F.</summary>
        Cautious,
        /// <summary>Default scoring.</summary>
        Balanced,
        /// <summary>Lenient scoring; trims P3 fallback when higher-priority items present.</summary>
        Aggressive,
    }

    /// <summary>Priority bucket for output-contract findings and playbook actions.</summary>
    public enum OutputContractPriority
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

    /// <summary>Per-prompt verdict ladder.</summary>
    public enum OutputContractVerdict
    {
        /// <summary>Output contract is clear and complete.</summary>
        Ready,
        /// <summary>Some gaps - human review recommended.</summary>
        Review,
        /// <summary>Significant gaps - rewrite recommended before sending.</summary>
        RewriteRecommended,
        /// <summary>Do not send: high-severity contract failure detected.</summary>
        BlockSend,
    }

    /// <summary>Options for <see cref="PromptOutputContractAdvisor.Analyze"/>.</summary>
    public sealed class OutputContractOptions
    {
        /// <summary>Risk appetite knob.</summary>
        public OutputContractRiskAppetite RiskAppetite { get; init; } = OutputContractRiskAppetite.Balanced;
        /// <summary>Skip the NO_REFUSAL_POLICY detector (useful when refusal policy lives in a separate system prompt section).</summary>
        public bool SkipRefusalPolicyCheck { get; init; }
    }

    /// <summary>A single detected gap in the output contract.</summary>
    public sealed class ContractFinding
    {
        /// <summary>Detector code.</summary>
        public string Code { get; internal set; } = "";
        /// <summary>Severity 0..100.</summary>
        public int Severity { get; internal set; }
        /// <summary>Priority bucket.</summary>
        public OutputContractPriority Priority { get; internal set; }
        /// <summary>Short human-readable label.</summary>
        public string Label { get; internal set; } = "";
        /// <summary>Reason / detail.</summary>
        public string Reason { get; internal set; } = "";
    }

    /// <summary>One playbook action recommended by the advisor.</summary>
    public sealed class ContractPlaybookAction
    {
        /// <summary>Stable action id.</summary>
        public string Id { get; internal set; } = "";
        /// <summary>Priority bucket.</summary>
        public OutputContractPriority Priority { get; internal set; }
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

    /// <summary>Report produced by <see cref="PromptOutputContractAdvisor.Analyze"/>.</summary>
    public sealed class OutputContractReport
    {
        /// <summary>0..100 score; higher is better.</summary>
        public int ContractScore { get; internal set; }
        /// <summary>A-F grade.</summary>
        public char Grade { get; internal set; }
        /// <summary>Verdict ladder.</summary>
        public OutputContractVerdict Verdict { get; internal set; }
        /// <summary>Headline summary.</summary>
        public string Headline { get; internal set; } = "";
        /// <summary>Per-detector findings.</summary>
        public IReadOnlyList<ContractFinding> Findings { get; internal set; } = Array.Empty<ContractFinding>();
        /// <summary>Ranked P0-first playbook.</summary>
        public IReadOnlyList<ContractPlaybookAction> Playbook { get; internal set; } = Array.Empty<ContractPlaybookAction>();
        /// <summary>Cross-prompt insights.</summary>
        public IReadOnlyList<string> Insights { get; internal set; } = Array.Empty<string>();
        /// <summary>Paste-ready prompt with an appended `# Output Contract` block.</summary>
        public string ContractTightenedDraft { get; internal set; } = "";
        /// <summary>Generation timestamp (deterministic when an explicit clock is injected).</summary>
        public DateTime GeneratedAt { get; internal set; }

        /// <summary>Render a plain-text view.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{Verdict}] {Headline}");
            sb.AppendLine($"Score: {ContractScore}/100   Grade: {Grade}");
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
            sb.AppendLine($"# Output Contract Audit");
            sb.AppendLine();
            sb.AppendLine($"**Verdict:** `{Verdict}`  ");
            sb.AppendLine($"**Score:** `{ContractScore}/100`  ");
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
    /// Agentic output-contract advisor: scans a prompt to verify it tells the model exactly how to
    /// structure its response (format, schema, length, error-mode, refusal policy, etc.).
    /// </summary>
    public sealed class PromptOutputContractAdvisor
    {
        private readonly Func<DateTime> _now;

        /// <summary>Create a new advisor. Pass an explicit clock for deterministic tests.</summary>
        public PromptOutputContractAdvisor(Func<DateTime>? nowProvider = null)
        {
            _now = nowProvider ?? (() => DateTime.UtcNow);
        }

        // -----------------------------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------------------------

        /// <summary>Analyze a prompt and produce a structured contract report.</summary>
        public OutputContractReport Analyze(string prompt, OutputContractOptions? options = null)
        {
            options ??= new OutputContractOptions();
            prompt ??= string.Empty;
            var lower = prompt.ToLowerInvariant();

            var findings = new List<ContractFinding>();

            // 1. NO_FORMAT_DECLARED
            bool hasFormat = HasAny(lower, FormatKeywords);
            if (!hasFormat && prompt.Trim().Length > 0)
            {
                findings.Add(MakeFinding("NO_FORMAT_DECLARED", 60,
                    "No output format declared",
                    "The prompt does not name an output format (JSON, markdown, table, bullet list, plain text, yaml, xml, csv)."));
            }

            // 2. AMBIGUOUS_FORMAT - two or more conflicting format keywords
            var hitFormats = FormatKeywords.Where(k => lower.Contains(k)).Distinct().ToList();
            if (hitFormats.Count >= 2)
            {
                // Treat structured vs prose as conflict; near-synonyms (e.g. table + markdown) don't conflict on their own.
                bool hasStructured = hitFormats.Any(k => StructuredFormats.Contains(k));
                bool hasProse = hitFormats.Any(k => ProseFormats.Contains(k));
                if (hasStructured && hasProse)
                {
                    findings.Add(MakeFinding("AMBIGUOUS_FORMAT", 50,
                        "Conflicting format hints",
                        $"Prompt mentions both structured ({string.Join(", ", hitFormats.Where(StructuredFormats.Contains))}) and prose ({string.Join(", ", hitFormats.Where(ProseFormats.Contains))}) formats."));
                }
            }

            // 3. MISSING_SCHEMA - JSON mentioned but no field names listed
            bool mentionsJson = lower.Contains("json");
            bool hasSchemaCue = JsonSchemaPattern.IsMatch(prompt) || lower.Contains("with the following keys") || lower.Contains("with these keys") || lower.Contains("the following fields") || lower.Contains("schema:") || BulletKeysPattern.IsMatch(prompt);
            if (mentionsJson && !hasSchemaCue)
            {
                findings.Add(MakeFinding("MISSING_SCHEMA", 65,
                    "JSON requested without schema",
                    "Prompt asks for JSON output but does not list the required fields/keys or provide a schema."));
            }

            // 4. MISSING_LENGTH_BUDGET
            bool hasGenerative = GenerativeVerbPattern.IsMatch(lower);
            bool hasLengthBudget = LengthBudgetPattern.IsMatch(lower);
            if (hasGenerative && !hasLengthBudget)
            {
                findings.Add(MakeFinding("MISSING_LENGTH_BUDGET", 35,
                    "No length budget for generative task",
                    "A generative verb (summarize/list/describe/explain/draft/write/generate) was used without a length budget (N words, N sentences, N bullets, N paragraphs)."));
            }

            // 5. NO_ERROR_MODE
            bool hasErrorMode = ErrorModePattern.IsMatch(lower);
            if (!hasErrorMode && prompt.Trim().Length > 0)
            {
                findings.Add(MakeFinding("NO_ERROR_MODE", 30,
                    "No uncertainty / no-data behaviour specified",
                    "Prompt does not tell the model what to do when it lacks information (e.g., 'say UNKNOWN', 'return empty array if none')."));
            }

            // 6. NO_REFUSAL_POLICY
            if (!options.SkipRefusalPolicyCheck)
            {
                bool hasRefusal = RefusalPattern.IsMatch(lower);
                if (!hasRefusal && prompt.Trim().Length > 0)
                {
                    findings.Add(MakeFinding("NO_REFUSAL_POLICY", 25,
                        "No refusal / out-of-scope policy",
                        "Prompt does not state what to do for unsafe or out-of-scope requests."));
                }
            }

            // 7. EXAMPLE_FORMAT_MISMATCH
            bool hasExample = lower.Contains("for example:") || lower.Contains("example:");
            if (hasExample && mentionsJson)
            {
                // Take the chunk after the first "example" marker and check if it looks JSON-shaped.
                int idx = lower.IndexOf("example");
                if (idx >= 0)
                {
                    string tail = prompt.Substring(idx);
                    bool jsonShaped = tail.Contains("{") && tail.Contains("}") && tail.Contains(":");
                    if (!jsonShaped)
                    {
                        findings.Add(MakeFinding("EXAMPLE_FORMAT_MISMATCH", 55,
                            "Example does not match JSON format",
                            "Prompt asks for JSON but the provided example is not JSON-shaped."));
                    }
                }
            }

            // 8. NO_FIELD_ORDER
            if (mentionsJson && hasSchemaCue)
            {
                bool hasOrderCue = lower.Contains("in this order") || lower.Contains("in the order") || lower.Contains("ordered as") || lower.Contains("respect this order");
                if (!hasOrderCue)
                {
                    findings.Add(MakeFinding("NO_FIELD_ORDER", 20,
                        "JSON keys mentioned without required ordering",
                        "If downstream consumers depend on field order, declare the required ordering explicitly."));
                }
            }

            // 9. FREEFORM_BUT_PARSED (P0-risk)
            bool machineReadable = MachineReadablePattern.IsMatch(lower);
            if (machineReadable && !hasFormat)
            {
                findings.Add(MakeFinding("FREEFORM_BUT_PARSED", 70,
                    "Machine-readable claim without strict format",
                    "Prompt implies downstream parsing/automation but does not declare a strict format the parser can rely on."));
            }
            else if (machineReadable && mentionsJson && !hasSchemaCue)
            {
                // Schemaless JSON for a machine-consumed response is itself FREEFORM_BUT_PARSED.
                findings.Add(MakeFinding("FREEFORM_BUT_PARSED", 70,
                    "Machine-readable JSON without schema",
                    "Prompt is consumed by downstream automation but the JSON contract has no schema."));
            }

            // 10. NO_DELIMITER
            bool embedded = EmbeddedDocPattern.IsMatch(lower);
            bool hasDelimiter = prompt.Contains("```") || prompt.Contains("<output>") || prompt.Contains("---");
            if (embedded && !hasDelimiter)
            {
                findings.Add(MakeFinding("NO_DELIMITER", 25,
                    "No output delimiter requested",
                    "Output appears to be embedded in a larger document but no fence/delimiter is requested."));
            }

            // 11. UNSPECIFIED_LANGUAGE_OF_OUTPUT
            bool hasLanguage = LanguagePinPattern.IsMatch(lower);
            if (!hasLanguage && prompt.Trim().Length > 0)
            {
                findings.Add(MakeFinding("UNSPECIFIED_LANGUAGE_OF_OUTPUT", 15,
                    "Output language not pinned",
                    "Prompt does not pin the response language (e.g., 'respond in English')."));
            }

            // Dedupe findings by Code (keep the highest-severity).
            findings = findings
                .GroupBy(f => f.Code)
                .Select(g => g.MaxBy(f => f.Severity)!)
                .OrderByDescending(f => f.Severity)
                .ToList();

            // Assign priority per finding.
            foreach (var f in findings)
                f.Priority = BucketPriority(f.Severity);

            // Score.
            int topSev = findings.Count == 0 ? 0 : findings.Max(f => f.Severity);
            int restSum = findings.Count <= 1 ? 0 : findings.OrderByDescending(f => f.Severity).Skip(1).Sum(f => f.Severity);
            double appetiteMult = options.RiskAppetite switch
            {
                OutputContractRiskAppetite.Cautious => 1.15,
                OutputContractRiskAppetite.Aggressive => 0.85,
                _ => 1.0,
            };
            int rawPenalty = (int)Math.Round((topSev + 0.4 * Math.Min(restSum, 60)) * appetiteMult);
            int contractScore = Math.Max(0, Math.Min(100, 100 - rawPenalty));

            // Verdict ladder.
            bool blockTrigger = findings.Any(f =>
                (f.Code == "FREEFORM_BUT_PARSED" && f.Severity >= 70) ||
                (f.Code == "MISSING_SCHEMA" && f.Severity >= 70));
            OutputContractVerdict verdict;
            if (blockTrigger) verdict = OutputContractVerdict.BlockSend;
            else if (contractScore < 50) verdict = OutputContractVerdict.RewriteRecommended;
            else if (contractScore < 75) verdict = OutputContractVerdict.Review;
            else verdict = OutputContractVerdict.Ready;

            // Grade.
            char grade;
            if (verdict == OutputContractVerdict.BlockSend) grade = 'F';
            else if (contractScore >= 85) grade = 'A';
            else if (contractScore >= 70) grade = 'B';
            else if (contractScore >= 55) grade = 'C';
            else if (contractScore >= 40) grade = 'D';
            else grade = 'F';

            // Playbook (synthesised from findings).
            var playbook = BuildPlaybook(findings, options, grade);

            // Insights.
            var insights = BuildInsights(findings);

            // ContractTightenedDraft.
            string draft = BuildTightenedDraft(prompt, findings);

            string headline = findings.Count == 0
                ? "Output contract is well-defined."
                : $"{findings.Count} contract gap(s) detected; top: {findings[0].Code}.";

            var report = new OutputContractReport
            {
                ContractScore = contractScore,
                Grade = grade,
                Verdict = verdict,
                Headline = headline,
                Findings = findings,
                Playbook = playbook,
                Insights = insights,
                ContractTightenedDraft = draft,
                GeneratedAt = _now(),
            };
            return report;
        }

        // -----------------------------------------------------------------------------------------
        // Detector helpers
        // -----------------------------------------------------------------------------------------

        private static readonly string[] StructuredFormats = { "json", "yaml", "xml", "csv", "table" };
        private static readonly string[] ProseFormats = { "markdown", "plain text", "bullet list", "paragraph" };
        private static readonly string[] FormatKeywords = StructuredFormats.Concat(ProseFormats).ToArray();

        private static readonly Regex JsonSchemaPattern =
            new("\"[A-Za-z_][A-Za-z0-9_]*\"\\s*:", RegexOptions.Compiled);

        private static readonly Regex BulletKeysPattern =
            new(@"(^|\n)\s*[-*]\s+[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);

        private static readonly Regex GenerativeVerbPattern =
            new(@"\b(summari[sz]e|list|describe|explain|draft|write|generate|enumerate|outline)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LengthBudgetPattern =
            new(@"\b(\d+|one|two|three|four|five|six|seven|eight|nine|ten)\s*(words?|sentences?|bullets?|paragraphs?|lines?|items?|tokens?|characters?)\b|\b(at most|under|no more than|up to)\s+\d+",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ErrorModePattern =
            new(@"\bif you (do not|don't|cannot|can't)\b|\bif (unknown|unsure|unclear|no .* (is )?available|none|missing)\b|\breturn (empty|null|none|unknown)\b|\brespond with [\""']?(unknown|none|n/a)[\""']?\b|\bsay [\""']?unknown[\""']?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RefusalPattern =
            new(@"\b(refuse|decline|do not answer|don't answer|out[- ]of[- ]scope|unsafe|policy|disallow)\b|\bsay you cannot\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MachineReadablePattern =
            new(@"\b(machine[- ]readable|i will parse|will be parsed|for downstream|ingest|ingested|automated|automation|pipeline|consumed by|programmatically)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex EmbeddedDocPattern =
            new(@"\b(embed(ded)? in|insert(ed)? into|inside a larger|inside the document|part of a larger|surrounded by|wrap (it )?in)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LanguagePinPattern =
            new(@"\b(respond|reply|answer|write)\s+in\s+(english|spanish|french|german|italian|portuguese|japanese|chinese|korean|hindi|arabic|russian)\b|\bin\s+(english|spanish|french|german)\s+only\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool HasAny(string lower, IEnumerable<string> needles)
        {
            foreach (var n in needles) if (lower.Contains(n)) return true;
            return false;
        }

        private static ContractFinding MakeFinding(string code, int sev, string label, string reason)
            => new() { Code = code, Severity = sev, Label = label, Reason = reason };

        private static OutputContractPriority BucketPriority(int sev)
        {
            if (sev >= 65) return OutputContractPriority.P0;
            if (sev >= 45) return OutputContractPriority.P1;
            if (sev >= 25) return OutputContractPriority.P2;
            return OutputContractPriority.P3;
        }

        // -----------------------------------------------------------------------------------------
        // Playbook + insights + draft
        // -----------------------------------------------------------------------------------------

        private static List<ContractPlaybookAction> BuildPlaybook(
            List<ContractFinding> findings, OutputContractOptions opts, char grade)
        {
            var actions = new List<ContractPlaybookAction>();

            bool Has(string code) => findings.Any(f => f.Code == code);

            if (Has("NO_FORMAT_DECLARED") || Has("FREEFORM_BUT_PARSED"))
                actions.Add(Action("DECLARE_OUTPUT_FORMAT", OutputContractPriority.P0,
                    "Declare the exact output format",
                    "Name the format explicitly (JSON, markdown, plain text, etc.) so the model and downstream parsers agree."));
            if (Has("MISSING_SCHEMA") || Has("FREEFORM_BUT_PARSED"))
                actions.Add(Action("DEFINE_JSON_SCHEMA", OutputContractPriority.P0,
                    "Define the JSON schema (fields, types, required-ness)",
                    "List the required keys with types and which are optional; consider a fenced JSON example."));
            if (Has("NO_ERROR_MODE"))
                actions.Add(Action("ADD_ERROR_MODE", OutputContractPriority.P1,
                    "Add an explicit error / no-data behaviour",
                    "Tell the model what to emit when uncertain (e.g., 'return an empty array' or 'say UNKNOWN')."));
            if (Has("MISSING_LENGTH_BUDGET"))
                actions.Add(Action("ADD_LENGTH_BUDGET", OutputContractPriority.P1,
                    "Add a length budget",
                    "Specify a hard cap such as 'at most 5 bullets' or 'under 120 words'."));
            if (Has("AMBIGUOUS_FORMAT"))
                actions.Add(Action("RESOLVE_FORMAT_AMBIGUITY", OutputContractPriority.P1,
                    "Resolve conflicting format hints",
                    "Pick one canonical output format and remove the alternative."));
            if (Has("EXAMPLE_FORMAT_MISMATCH"))
                actions.Add(Action("ALIGN_EXAMPLES_TO_FORMAT", OutputContractPriority.P1,
                    "Align in-prompt examples to the declared format",
                    "If JSON is required, the example block must also be JSON-shaped."));
            if (Has("NO_REFUSAL_POLICY"))
                actions.Add(Action("ADD_REFUSAL_POLICY", OutputContractPriority.P2,
                    "Add a refusal / out-of-scope policy",
                    "State explicitly what to do when a request is unsafe or outside scope."));
            if (Has("NO_DELIMITER"))
                actions.Add(Action("ADD_DELIMITERS", OutputContractPriority.P2,
                    "Request explicit output delimiters",
                    "Ask the model to wrap output in a fence or `<output>` tags to simplify extraction."));
            if (Has("UNSPECIFIED_LANGUAGE_OF_OUTPUT"))
                actions.Add(Action("PIN_OUTPUT_LANGUAGE", OutputContractPriority.P2,
                    "Pin the response language",
                    "Add a single line such as 'Respond in English.' to avoid translation drift."));
            if (Has("NO_FIELD_ORDER"))
                actions.Add(Action("SPECIFY_FIELD_ORDER", OutputContractPriority.P2,
                    "Specify the required field order",
                    "If downstream consumers depend on key order, state it explicitly."));

            if (opts.RiskAppetite == OutputContractRiskAppetite.Cautious && (grade == 'C' || grade == 'D' || grade == 'F'))
                actions.Add(Action("SECOND_REVIEWER", OutputContractPriority.P2,
                    "Solicit a second reviewer",
                    "Cautious mode + non-passing grade: have another author review the contract before send."));

            if (actions.Count == 0)
                actions.Add(Action("OUTPUT_CONTRACT_OK", OutputContractPriority.P3,
                    "Output contract is well-defined",
                    "No contract gaps detected; ship as-is."));

            // Aggressive: trim the P3 fallback when higher-priority items present.
            if (opts.RiskAppetite == OutputContractRiskAppetite.Aggressive)
            {
                if (actions.Any(a => a.Priority < OutputContractPriority.P3))
                    actions = actions.Where(a => a.Priority < OutputContractPriority.P3).ToList();
            }

            // Dedupe by Id and order P0-first (then by Id for determinism).
            actions = actions
                .GroupBy(a => a.Id)
                .Select(g => g.First())
                .OrderBy(a => (int)a.Priority)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();

            return actions;
        }

        private static ContractPlaybookAction Action(string id, OutputContractPriority priority, string label, string reason,
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

        private static List<string> BuildInsights(List<ContractFinding> findings)
        {
            var insights = new List<string>();
            bool Has(string c) => findings.Any(f => f.Code == c);

            if (Has("MISSING_SCHEMA")) insights.Add("SCHEMALESS_JSON");
            if (Has("FREEFORM_BUT_PARSED")) insights.Add("FREEFORM_FOR_MACHINE");
            if (Has("NO_ERROR_MODE")) insights.Add("NO_GUARDRAILS_FOR_UNCERTAINTY");
            if (Has("AMBIGUOUS_FORMAT")) insights.Add("MULTIPLE_FORMAT_HINTS");
            if (Has("MISSING_LENGTH_BUDGET")) insights.Add("LENGTH_UNBOUNDED");
            if (findings.Count == 0) insights.Add("WELL_DEFINED_CONTRACT");
            return insights;
        }

        private static string BuildTightenedDraft(string prompt, List<ContractFinding> findings)
        {
            var sb = new StringBuilder();
            sb.Append(prompt);
            if (!prompt.EndsWith("\n")) sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("# Output Contract");
            var todoFindings = findings
                .Where(f => f.Priority == OutputContractPriority.P0 || f.Priority == OutputContractPriority.P1)
                .ToList();
            if (todoFindings.Count == 0)
            {
                sb.AppendLine("- [x] Output contract reviewed: no P0/P1 gaps.");
            }
            else
            {
                foreach (var f in todoFindings)
                    sb.AppendLine($"- [ ] {f.Code}: {f.Label} - {f.Reason}");
            }

            bool needScaffold = findings.Any(f =>
                f.Code == "MISSING_SCHEMA" ||
                f.Code == "MISSING_LENGTH_BUDGET" ||
                f.Code == "NO_ERROR_MODE" ||
                f.Code == "NO_FORMAT_DECLARED" ||
                f.Code == "FREEFORM_BUT_PARSED");
            if (needScaffold)
            {
                sb.AppendLine();
                sb.AppendLine("# Suggested scaffolding");
                if (findings.Any(f => f.Code == "NO_FORMAT_DECLARED" || f.Code == "FREEFORM_BUT_PARSED"))
                    sb.AppendLine("Format: <choose one: JSON | markdown | plain text>");
                if (findings.Any(f => f.Code == "MISSING_SCHEMA"))
                {
                    sb.AppendLine("Schema:");
                    sb.AppendLine("```json");
                    sb.AppendLine("{");
                    sb.AppendLine("  \"<field_name>\": \"<type>\"");
                    sb.AppendLine("}");
                    sb.AppendLine("```");
                }
                if (findings.Any(f => f.Code == "MISSING_LENGTH_BUDGET"))
                    sb.AppendLine("Length budget: <e.g., at most 5 bullets / under 120 words>");
                if (findings.Any(f => f.Code == "NO_ERROR_MODE"))
                    sb.AppendLine("Error mode: <e.g., return an empty array if no items match>");
            }
            return sb.ToString();
        }
    }
}
