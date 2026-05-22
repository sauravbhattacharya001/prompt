namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>Attack-vector dimension considered by <see cref="PromptDefenseAdvisor"/>.</summary>
    public enum DefenseVector
    {
        /// <summary>Direct prompt-injection ("ignore previous instructions", role override, etc.).</summary>
        Injection,
        /// <summary>Jailbreak / safety-bypass framings ("DAN", "developer mode", roleplay-as-evil).</summary>
        Jailbreak,
        /// <summary>Data exfiltration ("print the system prompt", "list your tools", leak secrets).</summary>
        DataExfiltration,
        /// <summary>Role / authority confusion (impersonating system, developer, admin).</summary>
        RoleConfusion,
        /// <summary>System-prompt leakage or fingerprinting requests.</summary>
        SystemPromptLeakage,
        /// <summary>Indirect injection via untrusted document / URL / tool output.</summary>
        IndirectInjection,
        /// <summary>Unsafe tool / function-calling invocation (delete, exfiltrate, network egress).</summary>
        UnsafeToolUse,
        /// <summary>Output-channel manipulation (markdown image exfil, link smuggling, encoded payloads).</summary>
        OutputManipulation
    }

    /// <summary>Priority bucket for a recommended defense.</summary>
    public enum DefensePriority
    {
        /// <summary>Critical, deploy immediately.</summary>
        P0 = 0,
        /// <summary>High priority, deploy this iteration.</summary>
        P1 = 1,
        /// <summary>Medium priority, deploy when budget permits.</summary>
        P2 = 2,
        /// <summary>Hygiene / nice-to-have.</summary>
        P3 = 3
    }

    /// <summary>Operational context that tunes the advisor.</summary>
    public sealed class DefenseContext
    {
        /// <summary>True when the prompt is consumed by tool / function calling.</summary>
        public bool HasToolCalling { get; set; }

        /// <summary>True when the prompt is rendered in a markdown UI (image exfil risk).</summary>
        public bool RendersMarkdown { get; set; }

        /// <summary>True when the prompt may include untrusted content (RAG, scraped pages, email body).</summary>
        public bool HasUntrustedContent { get; set; }

        /// <summary>True when the prompt is end-user authored (vs internal/dev).</summary>
        public bool EndUserAuthored { get; set; } = true;

        /// <summary>True when responses are streamed back to the user without server-side validation.</summary>
        public bool StreamsToUser { get; set; }

        /// <summary>True when conversation memory persists across turns.</summary>
        public bool PersistsMemory { get; set; }

        /// <summary>Risk appetite: "strict", "balanced" (default), or "permissive".</summary>
        public string RiskAppetite { get; set; } = "balanced";
    }

    /// <summary>Per-vector finding produced by <see cref="PromptDefenseAdvisor"/>.</summary>
    public sealed class DefenseFinding
    {
        /// <summary>The attack vector.</summary>
        public DefenseVector Vector { get; internal set; }

        /// <summary>Risk score on a 0–10 scale.</summary>
        public double Risk { get; internal set; }

        /// <summary>"low" / "medium" / "high" / "critical".</summary>
        public string Level { get; internal set; } = "low";

        /// <summary>Concrete evidence substrings extracted from the prompt.</summary>
        public List<string> Evidence { get; internal set; } = new();

        /// <summary>One-line human-readable explanation.</summary>
        public string Explanation { get; internal set; } = "";
    }

    /// <summary>A single, concrete recommendation for hardening the prompt pipeline.</summary>
    public sealed class DefenseRecommendation
    {
        /// <summary>Priority bucket.</summary>
        public DefensePriority Priority { get; internal set; }

        /// <summary>Library module to deploy, e.g. "PromptInjectionDetector".</summary>
        public string Module { get; internal set; } = "";

        /// <summary>One-line action.</summary>
        public string Action { get; internal set; } = "";

        /// <summary>Why this recommendation was generated.</summary>
        public string Rationale { get; internal set; } = "";

        /// <summary>Paste-ready C# snippet showing the recommended configuration.</summary>
        public string Snippet { get; internal set; } = "";

        /// <summary>Vectors that this recommendation mitigates.</summary>
        public List<DefenseVector> Mitigates { get; internal set; } = new();
    }

    /// <summary>Full defense advisory produced by <see cref="PromptDefenseAdvisor"/>.</summary>
    public sealed class DefenseAdvisoryReport
    {
        /// <summary>Per-vector findings.</summary>
        public List<DefenseFinding> Findings { get; internal set; } = new();

        /// <summary>Recommended defenses, ranked.</summary>
        public List<DefenseRecommendation> Recommendations { get; internal set; } = new();

        /// <summary>Overall posture score 0–10 (higher = more dangerous prompt surface).</summary>
        public double OverallRisk { get; internal set; }

        /// <summary>Overall posture level: "low" / "medium" / "high" / "critical".</summary>
        public string Posture { get; internal set; } = "low";

        /// <summary>Concise executive summary line.</summary>
        public string Summary { get; internal set; } = "";

        /// <summary>Render a compact plain-text report.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Defense posture: {Posture.ToUpperInvariant()} (risk {OverallRisk:F1}/10)");
            sb.AppendLine(Summary);
            sb.AppendLine();
            sb.AppendLine("Vector findings:");
            foreach (var f in Findings.OrderByDescending(x => x.Risk))
            {
                sb.AppendLine($"  [{f.Level,-8}] {f.Vector,-22} {f.Risk:F1}/10  {f.Explanation}");
                foreach (var e in f.Evidence.Take(3))
                {
                    sb.AppendLine($"      · {Truncate(e, 110)}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("Recommended defenses:");
            foreach (var r in Recommendations)
            {
                sb.AppendLine($"  {r.Priority} {r.Module} — {r.Action}");
                sb.AppendLine($"      rationale: {r.Rationale}");
            }
            return sb.ToString();
        }

        /// <summary>Render a markdown report.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Defense Advisory");
            sb.AppendLine();
            sb.AppendLine($"**Posture:** {Posture.ToUpperInvariant()}  ");
            sb.AppendLine($"**Risk:** {OverallRisk:F1} / 10  ");
            sb.AppendLine($"**Summary:** {Summary}");
            sb.AppendLine();
            sb.AppendLine("## Vector findings");
            sb.AppendLine();
            sb.AppendLine("| Vector | Risk | Level | Notes |");
            sb.AppendLine("|---|---:|---|---|");
            foreach (var f in Findings.OrderByDescending(x => x.Risk))
            {
                sb.AppendLine($"| {f.Vector} | {f.Risk:F1} | {f.Level} | {EscapeMd(f.Explanation)} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Recommended defenses");
            sb.AppendLine();
            foreach (var r in Recommendations)
            {
                sb.AppendLine($"### {r.Priority} · {r.Module}");
                sb.AppendLine();
                sb.AppendLine($"**Action:** {r.Action}  ");
                sb.AppendLine($"**Rationale:** {r.Rationale}  ");
                sb.AppendLine($"**Mitigates:** {string.Join(", ", r.Mitigates)}");
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(r.Snippet);
                sb.AppendLine("```");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Render as JSON.</summary>
        public string ToJson(bool indented = true)
        {
            var dto = new
            {
                overallRisk = Math.Round(OverallRisk, 2),
                posture = Posture,
                summary = Summary,
                findings = Findings.Select(f => new
                {
                    vector = f.Vector.ToString(),
                    risk = Math.Round(f.Risk, 2),
                    level = f.Level,
                    explanation = f.Explanation,
                    evidence = f.Evidence
                }),
                recommendations = Recommendations.Select(r => new
                {
                    priority = r.Priority.ToString(),
                    module = r.Module,
                    action = r.Action,
                    rationale = r.Rationale,
                    mitigates = r.Mitigates.Select(m => m.ToString()),
                    snippet = r.Snippet
                })
            };
            return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = indented });
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "…");

        private static string EscapeMd(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("|", "\\|").Replace("\n", " ");
    }

    /// <summary>
    /// Agentic security posture advisor. Profiles a prompt against a catalogue of LLM attack
    /// vectors, scores risk per vector, and synthesizes a ranked playbook of concrete defenses
    /// using existing modules in the library (PromptGuard, PromptInjectionDetector,
    /// PromptSanitizer, PromptSecretScanner, PromptSentinel, PromptOutputValidator).
    /// </summary>
    public sealed class PromptDefenseAdvisor
    {
        // Lightweight pattern catalogue. Each entry: (vector, regex, weight, evidence-cap).
        private static readonly (DefenseVector v, Regex re, double w)[] _patterns = new[]
        {
            // Injection
            (DefenseVector.Injection,           Re(@"\bignore\s+(all\s+)?(previous|prior|above)\s+(instructions?|prompts?|rules?)\b"), 3.5),
            (DefenseVector.Injection,           Re(@"\bdisregard\s+(the\s+)?(prior|previous|earlier|above)\b"), 2.5),
            (DefenseVector.Injection,           Re(@"\boverride\s+(your|the)\s+(instructions?|rules?|system)\b"), 3.0),
            (DefenseVector.Injection,           Re(@"\bnew\s+instructions?\s*:\s*"), 2.0),

            // Jailbreak
            (DefenseVector.Jailbreak,           Re(@"\b(DAN|do\s+anything\s+now|developer\s+mode|jailbreak)\b"), 3.5),
            (DefenseVector.Jailbreak,           Re(@"\bpretend\s+(you\s+are|to\s+be)\s+.{0,40}(unrestricted|uncensored|evil|without\s+rules)\b"), 3.0),
            (DefenseVector.Jailbreak,           Re(@"\bno\s+(filters?|restrictions?|guardrails?|ethics?)\b"), 2.5),

            // DataExfiltration
            (DefenseVector.DataExfiltration,    Re(@"\b(print|show|reveal|tell\s+me|output|display|leak|give\s+me)\s+(me\s+)?(your\s+)?(system\s+prompt|hidden\s+prompt|initial\s+prompt|instructions)\b"), 3.5),
            (DefenseVector.DataExfiltration,    Re(@"\brepeat\s+(everything|the\s+text)\s+above\b"), 2.5),
            (DefenseVector.DataExfiltration,    Re(@"\b(api\s*keys?|secrets?|tokens?|passwords?|credentials?)\b"), 1.5),

            // RoleConfusion
            (DefenseVector.RoleConfusion,       Re(@"\b(system\s*[:>]|\bsystem\s+message\s*[:>])"), 3.0),
            (DefenseVector.RoleConfusion,       Re(@"\b(you\s+are\s+now|act\s+as|behave\s+as)\s+.{0,40}(admin|root|developer|owner|super\s*user)\b"), 2.5),
            (DefenseVector.RoleConfusion,       Re(@"<\s*/?\s*(system|user|assistant|developer)\s*>"), 3.0),

            // SystemPromptLeakage
            (DefenseVector.SystemPromptLeakage, Re(@"\bwhat\s+(were|are)\s+your\s+(original\s+)?instructions\b"), 3.0),
            (DefenseVector.SystemPromptLeakage, Re(@"\b(your\s+)?(rules?|guidelines?|policy|prompt)\s+(verbatim|word[\s-]for[\s-]word)\b"), 3.0),

            // IndirectInjection (markers that *suggest* untrusted content is being concatenated)
            (DefenseVector.IndirectInjection,   Re(@"\b(scraped|fetched|from\s+the\s+url|retrieved\s+from)\b"), 1.5),
            (DefenseVector.IndirectInjection,   Re(@"<!--\s*prompt[-_]?injection\s*-->", RegexOptions.IgnoreCase), 4.0),
            (DefenseVector.IndirectInjection,   Re(@"\[\[\s*system\s*]]"), 3.0),

            // UnsafeToolUse
            (DefenseVector.UnsafeToolUse,       Re(@"\b(rm\s+-rf|drop\s+table|truncate\s+table|delete\s+from)\b"), 4.0),
            (DefenseVector.UnsafeToolUse,       Re(@"\bcurl\b.*\bhttp"), 2.0),
            (DefenseVector.UnsafeToolUse,       Re(@"\bexec\s*\(|\beval\s*\("), 3.0),

            // OutputManipulation
            (DefenseVector.OutputManipulation,  Re(@"!\[[^\]]*]\(https?://"), 3.0), // markdown image with URL
            (DefenseVector.OutputManipulation,  Re(@"data:[a-z]+/[a-z0-9.+-]+;base64,", RegexOptions.IgnoreCase), 2.5),
            (DefenseVector.OutputManipulation,  Re(@"\b(?:[A-Za-z0-9+/]{40,}={0,2})\b"), 1.5) // long base64 blob
        };

        private static Regex Re(string p, RegexOptions extra = RegexOptions.None)
            => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled | extra, TimeSpan.FromMilliseconds(500));

        /// <summary>Analyze a prompt and produce an advisory report.</summary>
        public DefenseAdvisoryReport Analyze(string prompt, DefenseContext? context = null)
        {
            if (prompt is null) throw new ArgumentNullException(nameof(prompt));
            context ??= new DefenseContext();

            var report = new DefenseAdvisoryReport();
            var perVector = new Dictionary<DefenseVector, DefenseFinding>();

            foreach (DefenseVector v in Enum.GetValues(typeof(DefenseVector)))
            {
                perVector[v] = new DefenseFinding { Vector = v, Risk = 0, Level = "low", Explanation = "no signals detected" };
            }

            if (prompt.Length == 0)
            {
                FinalizeFindings(report, perVector, context);
                report.Summary = "Empty prompt — no surface to attack, but also nothing to defend.";
                return report;
            }

            // Pattern scan
            foreach (var (vec, re, weight) in _patterns)
            {
                var matches = re.Matches(prompt);
                if (matches.Count == 0) continue;
                var f = perVector[vec];
                // Diminishing returns on repeated hits within the same pattern.
                double add = weight + Math.Log(1 + matches.Count) * (weight * 0.25);
                f.Risk += add;
                foreach (Match m in matches.Cast<Match>().Take(3))
                {
                    var snip = m.Value.Trim();
                    if (snip.Length > 120) snip = snip.Substring(0, 117) + "…";
                    if (!f.Evidence.Contains(snip)) f.Evidence.Add(snip);
                }
            }

            // Context-driven nudges
            if (context.HasUntrustedContent)
            {
                Bump(perVector, DefenseVector.IndirectInjection, 2.5, "untrusted content channel declared");
            }
            if (context.HasToolCalling)
            {
                Bump(perVector, DefenseVector.UnsafeToolUse, 2.0, "tool / function-calling enabled");
            }
            if (context.RendersMarkdown)
            {
                Bump(perVector, DefenseVector.OutputManipulation, 1.5, "markdown rendering surface");
            }
            if (context.EndUserAuthored)
            {
                Bump(perVector, DefenseVector.Injection, 1.0, "end-user-authored input");
                Bump(perVector, DefenseVector.Jailbreak, 1.0, "end-user-authored input");
            }
            if (context.PersistsMemory)
            {
                Bump(perVector, DefenseVector.IndirectInjection, 1.0, "persistent memory amplifies poisoning");
            }
            if (context.StreamsToUser)
            {
                Bump(perVector, DefenseVector.OutputManipulation, 1.0, "unvalidated streaming output");
                Bump(perVector, DefenseVector.DataExfiltration, 0.8, "unvalidated streaming output");
            }

            // Cap each vector at 10 and write level
            FinalizeFindings(report, perVector, context);

            // Synthesize recommendations
            report.Recommendations = SynthesizeRecommendations(report.Findings, context);

            // Posture summary
            report.OverallRisk = ComputeOverall(report.Findings);
            report.Posture = LevelOf(report.OverallRisk);
            report.Summary = BuildSummary(report);

            return report;
        }

        private static void Bump(Dictionary<DefenseVector, DefenseFinding> map, DefenseVector v, double delta, string note)
        {
            var f = map[v];
            f.Risk += delta;
            if (!f.Evidence.Contains($"[context] {note}")) f.Evidence.Add($"[context] {note}");
        }

        private static void FinalizeFindings(
            DefenseAdvisoryReport report,
            Dictionary<DefenseVector, DefenseFinding> map,
            DefenseContext ctx)
        {
            double scale = ctx.RiskAppetite switch
            {
                "strict" => 1.2,
                "permissive" => 0.8,
                _ => 1.0
            };

            foreach (var f in map.Values)
            {
                f.Risk = Math.Min(10.0, Math.Round(f.Risk * scale, 2));
                f.Level = LevelOf(f.Risk);
                f.Explanation = ExplainVector(f);
                report.Findings.Add(f);
            }
        }

        private static string ExplainVector(DefenseFinding f)
        {
            if (f.Evidence.Count == 0) return "no signals detected";
            return f.Level switch
            {
                "critical" => $"{f.Evidence.Count} strong signal(s); deploy a hard control",
                "high" => $"{f.Evidence.Count} signal(s); deploy a dedicated detector",
                "medium" => $"{f.Evidence.Count} signal(s); enable monitoring + soft rules",
                _ => $"{f.Evidence.Count} weak signal(s); track but don't block",
            };
        }

        private static string LevelOf(double risk)
        {
            if (risk >= 8.0) return "critical";
            if (risk >= 5.0) return "high";
            if (risk >= 2.5) return "medium";
            return "low";
        }

        private static double ComputeOverall(IEnumerable<DefenseFinding> findings)
        {
            // Take the worst three vectors, weighted (5/3/2). Encourages broad coverage > one extreme.
            var sorted = findings.OrderByDescending(f => f.Risk).ToList();
            if (sorted.Count == 0) return 0;
            double sum = 0;
            double w = 0;
            double[] weights = { 5.0, 3.0, 2.0 };
            for (int i = 0; i < weights.Length && i < sorted.Count; i++)
            {
                sum += sorted[i].Risk * weights[i];
                w += weights[i];
            }
            return Math.Round(sum / w, 2);
        }

        private static string BuildSummary(DefenseAdvisoryReport report)
        {
            var hot = report.Findings.Where(f => f.Risk >= 5.0).Select(f => f.Vector.ToString()).ToList();
            if (hot.Count == 0)
            {
                return "No high-risk attack vectors detected; baseline defenses still recommended.";
            }
            return $"High-risk vectors: {string.Join(", ", hot)}.";
        }

        // ---------- Recommendation synthesis ----------

        private static List<DefenseRecommendation> SynthesizeRecommendations(
            List<DefenseFinding> findings, DefenseContext ctx)
        {
            var recs = new List<DefenseRecommendation>();
            var byVec = findings.ToDictionary(f => f.Vector, f => f);

            double inj = Math.Max(byVec[DefenseVector.Injection].Risk, byVec[DefenseVector.Jailbreak].Risk);
            double indirect = byVec[DefenseVector.IndirectInjection].Risk;
            double exfil = byVec[DefenseVector.DataExfiltration].Risk;
            double leak = byVec[DefenseVector.SystemPromptLeakage].Risk;
            double role = byVec[DefenseVector.RoleConfusion].Risk;
            double tool = byVec[DefenseVector.UnsafeToolUse].Risk;
            double output = byVec[DefenseVector.OutputManipulation].Risk;

            // 1. PromptInjectionDetector (injection / jailbreak / role confusion)
            double injMax = Math.Max(Math.Max(inj, role), indirect);
            if (injMax >= 2.5 || ctx.EndUserAuthored)
            {
                var minRisk = injMax >= 5.0 ? "InjectionRisk.Medium" : "InjectionRisk.Low";
                recs.Add(new DefenseRecommendation
                {
                    Priority = injMax >= 5.0 ? DefensePriority.P0 : DefensePriority.P1,
                    Module = "PromptInjectionDetector",
                    Action = "Scan every user-authored input for injection / jailbreak patterns.",
                    Rationale = injMax >= 5.0
                        ? "Strong injection/jailbreak signals detected — block before model sees the prompt."
                        : "End-user-authored inputs always carry residual injection risk; cheap to scan.",
                    Mitigates = new() { DefenseVector.Injection, DefenseVector.Jailbreak, DefenseVector.RoleConfusion },
                    Snippet =
                        "var detector = new PromptInjectionDetector();\n" +
                        "var scan = detector.Scan(userInput);\n" +
                        $"if (detector.IsUnsafe(userInput, {minRisk}))\n" +
                        "{\n" +
                        "    // reject, log, or fall back to a constrained template\n" +
                        "    throw new InvalidOperationException(\"prompt-injection blocked: \" + scan.ToReport());\n" +
                        "}"
                });
            }

            // 2. PromptSentinel (defense-in-depth wrapper)
            if (injMax >= 5.0 || exfil >= 5.0 || leak >= 5.0)
            {
                recs.Add(new DefenseRecommendation
                {
                    Priority = DefensePriority.P0,
                    Module = "PromptSentinel",
                    Action = "Wrap every model call with Sentinel scan-and-sanitize.",
                    Rationale = "Critical-tier risk across multiple vectors warrants a single chokepoint.",
                    Mitigates = new() { DefenseVector.Injection, DefenseVector.Jailbreak, DefenseVector.DataExfiltration, DefenseVector.SystemPromptLeakage },
                    Snippet =
                        "var sentinel = new PromptSentinel();\n" +
                        "var (safePrompt, report) = sentinel.ScanAndSanitize(userInput);\n" +
                        "if (report.Verdict == ScanVerdict.Block) { return Reject(report); }\n" +
                        "prompt = safePrompt;"
                });
            }

            // 3. PromptSanitizer (PII + soft normalization)
            if (ctx.EndUserAuthored || exfil >= 2.5)
            {
                recs.Add(new DefenseRecommendation
                {
                    Priority = exfil >= 5.0 ? DefensePriority.P1 : DefensePriority.P2,
                    Module = "PromptSanitizer",
                    Action = "Sanitize PII and obvious injection markers before forwarding to the model.",
                    Rationale = exfil >= 5.0
                        ? "Data-exfiltration signals present — strip PII before it can be reflected back."
                        : "Cheap, broad hygiene pass for any user-authored input.",
                    Mitigates = new() { DefenseVector.DataExfiltration, DefenseVector.Injection },
                    Snippet =
                        "var sanitizer = new PromptSanitizer();\n" +
                        "var result = sanitizer.Sanitize(userInput);\n" +
                        "prompt = result.Sanitized;"
                });
            }

            // 4. PromptSecretScanner
            if (exfil >= 2.5 || ctx.HasUntrustedContent)
            {
                recs.Add(new DefenseRecommendation
                {
                    Priority = exfil >= 5.0 ? DefensePriority.P1 : DefensePriority.P2,
                    Module = "PromptSecretScanner",
                    Action = "Reject or redact secrets (API keys, tokens, JWTs) before model submission.",
                    Rationale = "Prompt mentions credentials or pulls in untrusted content — secret leakage is a realistic outcome.",
                    Mitigates = new() { DefenseVector.DataExfiltration },
                    Snippet =
                        "var scanner = new PromptSecretScanner().MinSeverity(SecretSeverity.Medium);\n" +
                        "if (scanner.ContainsSecrets(prompt))\n" +
                        "{\n" +
                        "    prompt = scanner.Redact(prompt);\n" +
                        "}"
                });
            }

            // 5. PromptOutputValidator
            if (output >= 2.5 || ctx.StreamsToUser || ctx.RendersMarkdown)
            {
                recs.Add(new DefenseRecommendation
                {
                    Priority = output >= 5.0 ? DefensePriority.P1 : DefensePriority.P2,
                    Module = "PromptOutputValidator",
                    Action = "Validate model output before rendering it (block markdown images, oversize bodies, encoded blobs).",
                    Rationale = ctx.RendersMarkdown
                        ? "Markdown rendering enables image-tag and link-smuggling exfiltration channels."
                        : "Streaming output is otherwise unmoderated; impose post-conditions.",
                    Mitigates = new() { DefenseVector.OutputManipulation, DefenseVector.DataExfiltration },
                    Snippet =
                        "var validator = new PromptOutputValidator()\n" +
                        "    .MaxLength(8_000)\n" +
                        "    .MustNotMatchRegex(@\"!\\[[^\\]]*]\\(https?://\", \"no markdown image exfil\")\n" +
                        "    .MustNotMatchRegex(@\"data:[a-z]+/[a-z0-9.+-]+;base64,\", \"no data URLs\");\n" +
                        "var v = validator.Validate(response);\n" +
                        "if (!v.IsValid) { /* drop or sanitize */ }"
                });
            }

            // 6. PromptGuard (token-budget + structural wrap)
            recs.Add(new DefenseRecommendation
            {
                Priority = DefensePriority.P3,
                Module = "PromptGuard",
                Action = "Use the standard prompt analyzer as a hygiene gate (token budget + structural wrap).",
                Rationale = "Cheap, always-on. Catches token-budget overruns and re-asserts the desired output schema.",
                Mitigates = new() { DefenseVector.OutputManipulation, DefenseVector.SystemPromptLeakage },
                Snippet =
                    "var analysis = PromptGuard.Analyze(prompt, tokenLimit: 4_000);\n" +
                    "if (!analysis.IsWithinTokenLimit) { prompt = PromptGuard.TruncateToTokenLimit(prompt, 4_000); }\n" +
                    "prompt = PromptGuard.WrapWithFormat(prompt, OutputFormat.Json);"
            });

            // 7. Tool-use containment (advisory text, not a module call)
            if (ctx.HasToolCalling || tool >= 2.5)
            {
                recs.Add(new DefenseRecommendation
                {
                    Priority = tool >= 5.0 ? DefensePriority.P0 : DefensePriority.P1,
                    Module = "ToolPolicy",
                    Action = "Constrain tool / function calls behind an allow-list with per-call confirmation for destructive ops.",
                    Rationale = "Tool calling is the highest-blast-radius exit from the sandbox; never trust the model's choice unconditionally.",
                    Mitigates = new() { DefenseVector.UnsafeToolUse },
                    Snippet =
                        "// Pseudocode:\n" +
                        "// 1. Whitelist allowed tools per route.\n" +
                        "// 2. Re-confirm destructive tools (delete, send-email, run-shell) with an out-of-band check.\n" +
                        "// 3. Log every tool invocation with the prompt-id for forensic replay.\n"
                });
            }

            // Stable ordering: by priority asc, then by mitigation breadth desc, then module name.
            return recs
                .OrderBy(r => (int)r.Priority)
                .ThenByDescending(r => r.Mitigates.Count)
                .ThenBy(r => r.Module, StringComparer.Ordinal)
                .ToList();
        }
    }
}
