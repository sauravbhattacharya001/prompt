namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    // ────────────────────────────────────────────
    //  PromptSentinel – Autonomous Prompt Injection & Jailbreak Detector
    //
    //  Scans user/system prompts for injection attacks, jailbreak patterns,
    //  role-hijacking, encoding tricks, and social-engineering gambits.
    //  Returns a structured threat report with severity, category, matched
    //  evidence, and actionable defense recommendations.
    // ────────────────────────────────────────────

    /// <summary>Broad threat categories for prompt injection attacks.</summary>
    public enum ThreatCategory
    {
        /// <summary>Attempts to override or ignore system instructions.</summary>
        InstructionOverride,
        /// <summary>Jailbreak patterns (DAN, developer mode, etc.).</summary>
        Jailbreak,
        /// <summary>Attempts to assume a different role or persona.</summary>
        RoleHijack,
        /// <summary>Obfuscated payloads via encoding, unicode, or splitting.</summary>
        EncodingTrick,
        /// <summary>Social-engineering / emotional manipulation of the model.</summary>
        SocialEngineering,
        /// <summary>Prompt leaking – trying to extract the system prompt.</summary>
        PromptLeaking,
        /// <summary>Indirect injection via embedded instructions in data.</summary>
        IndirectInjection,
        /// <summary>Payload smuggling through delimiters or markdown.</summary>
        DelimiterSmuggling
    }

    /// <summary>Severity of a detected threat.</summary>
    public enum ThreatSeverity
    {
        /// <summary>Informational – low confidence or benign pattern.</summary>
        Info,
        /// <summary>Low – suspicious but unlikely to succeed.</summary>
        Low,
        /// <summary>Medium – plausible attack vector.</summary>
        Medium,
        /// <summary>High – likely injection attempt.</summary>
        High,
        /// <summary>Critical – strong multi-signal injection.</summary>
        Critical
    }

    /// <summary>A single threat detection from the sentinel scan.</summary>
    public sealed class ThreatFinding
    {
        /// <summary>Rule identifier (e.g. "INJ-001").</summary>
        public string RuleId { get; init; } = "";
        /// <summary>Human-readable rule name.</summary>
        public string RuleName { get; init; } = "";
        /// <summary>Broad category.</summary>
        public ThreatCategory Category { get; init; }
        /// <summary>Severity level.</summary>
        public ThreatSeverity Severity { get; init; }
        /// <summary>Matched text snippet (truncated to 120 chars).</summary>
        public string Evidence { get; init; } = "";
        /// <summary>Character offset of the match in the original text.</summary>
        public int Offset { get; init; }
        /// <summary>Recommended defense action.</summary>
        public string Recommendation { get; init; } = "";
    }

    /// <summary>Overall scan verdict.</summary>
    public enum ScanVerdict
    {
        /// <summary>No threats detected.</summary>
        Clean,
        /// <summary>Minor signals – proceed with caution.</summary>
        Suspicious,
        /// <summary>Likely injection – block or sanitize.</summary>
        Dangerous,
        /// <summary>Multi-signal confirmed attack – reject input.</summary>
        Hostile
    }

    /// <summary>Full scan report from PromptSentinel.</summary>
    public sealed class SentinelReport
    {
        /// <summary>Unique scan identifier.</summary>
        public string ScanId { get; init; } = "";
        /// <summary>UTC timestamp of the scan.</summary>
        public DateTime ScannedAt { get; init; }
        /// <summary>Overall verdict.</summary>
        public ScanVerdict Verdict { get; init; }
        /// <summary>Numeric threat score (0-100).</summary>
        public int ThreatScore { get; init; }
        /// <summary>Individual findings.</summary>
        public IReadOnlyList<ThreatFinding> Findings { get; init; } = Array.Empty<ThreatFinding>();
        /// <summary>Number of distinct categories triggered.</summary>
        public int CategoriesTriggered => Findings.Select(f => f.Category).Distinct().Count();
        /// <summary>Highest severity found.</summary>
        public ThreatSeverity MaxSeverity => Findings.Count > 0
            ? Findings.Max(f => f.Severity)
            : ThreatSeverity.Info;
        /// <summary>Short summary line.</summary>
        public string Summary => Verdict == ScanVerdict.Clean
            ? "No threats detected."
            : $"{Findings.Count} finding(s) across {CategoriesTriggered} category(ies) — verdict: {Verdict}";

        /// <summary>Returns a human-readable multi-line report.</summary>
        public string ToReport()
        {
            var lines = new List<string>
            {
                $"═══ PromptSentinel Report ═══",
                $"Scan ID  : {ScanId}",
                $"Scanned  : {ScannedAt:u}",
                $"Verdict  : {Verdict}",
                $"Score    : {ThreatScore}/100",
                $"Findings : {Findings.Count}",
                ""
            };

            if (Findings.Count == 0)
            {
                lines.Add("  ✓ Clean — no injection signals detected.");
            }
            else
            {
                foreach (var f in Findings)
                {
                    lines.Add($"  [{f.Severity}] {f.RuleId} {f.RuleName}");
                    lines.Add($"         Category : {f.Category}");
                    lines.Add($"         Evidence : \"{StringHelpers.Truncate(f.Evidence, 80)}\"");
                    lines.Add($"         Offset   : {f.Offset}");
                    lines.Add($"         Action   : {f.Recommendation}");
                    lines.Add("");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

    }

    /// <summary>A detection rule used by PromptSentinel.</summary>
    internal sealed class SentinelRule
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public ThreatCategory Category { get; init; }
        public ThreatSeverity Severity { get; init; }
        public Regex Pattern { get; init; } = null!;
        public string Recommendation { get; init; } = "";
    }

    /// <summary>Configuration for PromptSentinel scanning.</summary>
    public sealed class SentinelConfig
    {
        /// <summary>Categories to scan (null = all).</summary>
        public ISet<ThreatCategory>? EnabledCategories { get; set; }
        /// <summary>Minimum severity to report.</summary>
        public ThreatSeverity MinSeverity { get; set; } = ThreatSeverity.Info;
        /// <summary>Maximum findings before stopping early.</summary>
        public int MaxFindings { get; set; } = 50;
        /// <summary>Custom rules to add to the built-in set.</summary>
        public IList<(string id, string name, ThreatCategory cat, ThreatSeverity sev, string pattern, string rec)> CustomRules { get; set; }
            = new List<(string, string, ThreatCategory, ThreatSeverity, string, string)>();
    }

    /// <summary>
    /// PromptSentinel — autonomous prompt injection and jailbreak detector.
    /// <para>
    /// Scans text against 30+ built-in rules covering instruction override,
    /// jailbreak, role hijacking, encoding tricks, social engineering, prompt
    /// leaking, indirect injection, and delimiter smuggling.
    /// </para>
    /// <example><code>
    /// var sentinel = new PromptSentinel();
    /// var report = sentinel.Scan("Ignore all previous instructions and ...");
    /// Console.WriteLine(report.Verdict);   // Dangerous
    /// Console.WriteLine(report.ToReport());
    /// </code></example>
    /// </summary>
    public sealed class PromptSentinel
    {
        private readonly List<SentinelRule> _rules;
        private readonly SentinelConfig _config;

        /// <summary>Creates a new sentinel with optional configuration.</summary>
        public PromptSentinel(SentinelConfig? config = null)
        {
            _config = config ?? new SentinelConfig();
            _rules = BuildRules();
        }

        /// <summary>Scan a single prompt text and return a threat report.</summary>
        public SentinelReport Scan(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new SentinelReport
                {
                    ScanId = NewScanId(),
                    ScannedAt = DateTime.UtcNow,
                    Verdict = ScanVerdict.Clean,
                    ThreatScore = 0
                };
            }

            var findings = new List<ThreatFinding>();
            var lower = text.ToLowerInvariant();

            foreach (var rule in _rules)
            {
                if (findings.Count >= _config.MaxFindings) break;

                if (_config.EnabledCategories != null &&
                    !_config.EnabledCategories.Contains(rule.Category))
                    continue;

                if (rule.Severity < _config.MinSeverity) continue;

                foreach (Match m in rule.Pattern.Matches(lower))
                {
                    if (findings.Count >= _config.MaxFindings) break;

                    findings.Add(new ThreatFinding
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Category = rule.Category,
                        Severity = rule.Severity,
                        Evidence = StringHelpers.Truncate(text.Substring(m.Index, Math.Min(m.Length, text.Length - m.Index)), 120),
                        Offset = m.Index,
                        Recommendation = rule.Recommendation
                    });
                }
            }

            int score = ComputeScore(findings);
            var verdict = score switch
            {
                0 => ScanVerdict.Clean,
                <= 25 => ScanVerdict.Suspicious,
                <= 60 => ScanVerdict.Dangerous,
                _ => ScanVerdict.Hostile
            };

            return new SentinelReport
            {
                ScanId = NewScanId(),
                ScannedAt = DateTime.UtcNow,
                Verdict = verdict,
                ThreatScore = Math.Min(score, 100),
                Findings = findings.AsReadOnly()
            };
        }

        /// <summary>Scan multiple texts and return reports for each.</summary>
        public IReadOnlyList<SentinelReport> ScanBatch(IEnumerable<string> texts) =>
            texts.Select(Scan).ToList().AsReadOnly();

        /// <summary>Quick boolean check — returns true if any finding >= Medium.</summary>
        public bool IsInjection(string text) =>
            Scan(text).Findings.Any(f => f.Severity >= ThreatSeverity.Medium);

        /// <summary>Get a sanitized version with injection patterns redacted.</summary>
        public (string sanitized, SentinelReport report) ScanAndSanitize(string text)
        {
            var report = Scan(text);
            if (report.Verdict == ScanVerdict.Clean)
                return (text, report);

            string result = text;
            // Process findings in reverse offset order to preserve positions
            foreach (var f in report.Findings.OrderByDescending(x => x.Offset))
            {
                int len = Math.Min(f.Evidence.Length, result.Length - f.Offset);
                if (len > 0 && f.Offset >= 0 && f.Offset + len <= result.Length)
                {
                    result = result.Substring(0, f.Offset)
                           + new string('█', len)
                           + result.Substring(f.Offset + len);
                }
            }

            return (result, report);
        }

        // ── Score computation ──────────────────────

        private static int ComputeScore(List<ThreatFinding> findings)
        {
            if (findings.Count == 0) return 0;

            int raw = findings.Sum(f => f.Severity switch
            {
                ThreatSeverity.Info => 2,
                ThreatSeverity.Low => 5,
                ThreatSeverity.Medium => 15,
                ThreatSeverity.High => 25,
                ThreatSeverity.Critical => 40,
                _ => 0
            });

            // Bonus for multi-category attacks (compounding)
            int cats = findings.Select(f => f.Category).Distinct().Count();
            if (cats >= 3) raw += 15;
            else if (cats >= 2) raw += 8;

            return Math.Min(raw, 100);
        }

        // ── Rule definitions ───────────────────────

        private List<SentinelRule> BuildRules()
        {
            var opts = RegexOptions.IgnoreCase | RegexOptions.Compiled;
            var rules = new List<SentinelRule>
            {
                // ── Instruction Override ──
                R("INJ-001", "Ignore Previous Instructions", ThreatCategory.InstructionOverride, ThreatSeverity.High,
                  @"ignore\s+(all\s+)?(previous|prior|above|earlier|preceding)\s+(instructions?|directives?|rules?|prompts?)", opts,
                  "Strip or reject input containing instruction-override phrases."),
                R("INJ-002", "Disregard Directive", ThreatCategory.InstructionOverride, ThreatSeverity.High,
                  @"(disregard|forget|override|bypass)\s+(all\s+)?(previous|prior|above|your|system)\s+(instructions?|directives?|rules?|guidelines?)", opts,
                  "Reject input attempting to override system instructions."),
                R("INJ-003", "New Instructions Injection", ThreatCategory.InstructionOverride, ThreatSeverity.Medium,
                  @"(your\s+new\s+instructions?\s+are|from\s+now\s+on\s+you\s+(will|must|should)|instead\s+you\s+(must|should|will))", opts,
                  "Flag attempts to redefine the model's instructions."),
                R("INJ-004", "System Prompt Rewrite", ThreatCategory.InstructionOverride, ThreatSeverity.Critical,
                  @"\[\s*system\s*\]|\<\s*system\s*\>|system\s*:\s*(you\s+are|your\s+role|instructions?)", opts,
                  "Block fake system-prompt markers in user input."),

                // ── Jailbreak ──
                R("JB-001", "DAN Pattern", ThreatCategory.Jailbreak, ThreatSeverity.Critical,
                  @"\bdan\b.{0,30}(mode|jailbreak|do\s+anything\s+now|enabled|activated)", opts,
                  "Block known DAN jailbreak patterns immediately."),
                R("JB-002", "Developer/Debug Mode", ThreatCategory.Jailbreak, ThreatSeverity.High,
                  @"(enter|enable|activate|switch\s+to)\s+(developer|debug|god|admin|sudo|maintenance)\s+mode", opts,
                  "Reject attempts to invoke non-existent privileged modes."),
                R("JB-003", "No Restrictions Pattern", ThreatCategory.Jailbreak, ThreatSeverity.High,
                  @"(without\s+(any\s+)?restrictions?|no\s+(safety\s+)?restrictions?|unrestricted\s+mode|remove\s+(all\s+)?filters?)", opts,
                  "Block requests to disable safety filters."),
                R("JB-004", "Pretend/Hypothetical Jailbreak", ThreatCategory.Jailbreak, ThreatSeverity.Medium,
                  @"(pretend|imagine|hypothetically|in\s+a\s+fictional\s+world|roleplay\s+as).{0,40}(no\s+rules?|no\s+restrictions?|anything\s+goes|unfiltered)", opts,
                  "Watch for fictional framing used to bypass safety."),
                R("JB-005", "Opposite Day / Inversion", ThreatCategory.Jailbreak, ThreatSeverity.Medium,
                  @"(opposite\s+day|do\s+the\s+opposite|reverse\s+your\s+rules?|invert\s+your\s+(rules?|guidelines?))", opts,
                  "Flag rule-inversion gambits."),

                // ── Role Hijack ──
                R("RH-001", "You Are Now", ThreatCategory.RoleHijack, ThreatSeverity.High,
                  @"you\s+are\s+now\s+(a\s+)?(different|new|evil|unrestricted|unfiltered|uncensored)", opts,
                  "Reject attempts to reassign the model's identity."),
                R("RH-002", "Act As Privileged", ThreatCategory.RoleHijack, ThreatSeverity.Medium,
                  @"(act|behave|respond)\s+(as|like)\s+(a\s+)?(hacker|attacker|villain|uncensored\s+ai|evil\s+ai|jailbroken)", opts,
                  "Block malicious role assignments."),
                R("RH-003", "Persona Override", ThreatCategory.RoleHijack, ThreatSeverity.Medium,
                  @"(your\s+name\s+is\s+now|you\s+will\s+call\s+yourself|your\s+new\s+(persona|identity|name)\s+is)", opts,
                  "Flag identity-override attempts."),

                // ── Encoding Tricks ──
                R("ET-001", "Base64 Payload", ThreatCategory.EncodingTrick, ThreatSeverity.Medium,
                  @"(decode|interpret|execute|run)\s+(this\s+)?(base64|b64|encoded)\b", opts,
                  "Scan and decode any base64 content before processing."),
                R("ET-002", "ROT13/Caesar Reference", ThreatCategory.EncodingTrick, ThreatSeverity.Low,
                  @"(rot13|caesar\s+cipher|decode\s+this\s+cipher|shift\s+each\s+letter)", opts,
                  "Be wary of cipher-encoded instructions."),
                R("ET-003", "Hex/Unicode Injection", ThreatCategory.EncodingTrick, ThreatSeverity.Medium,
                  @"(\\x[0-9a-f]{2}|\\u[0-9a-f]{4}|&#x?[0-9a-f]+;){3,}", opts,
                  "Decode and inspect hex/unicode escape sequences."),
                R("ET-004", "Reverse Text Trick", ThreatCategory.EncodingTrick, ThreatSeverity.Low,
                  @"(read\s+(this\s+)?backwards?|reverse\s+the\s+(following|text|string)|spell\s+.+\s+backwards?)", opts,
                  "Check for reversed hidden instructions."),

                // ── Social Engineering ──
                R("SE-001", "Emotional Manipulation", ThreatCategory.SocialEngineering, ThreatSeverity.Low,
                  @"(my\s+(life|job|career)\s+depends\s+on|i('ll|\s+will)\s+(die|be\s+fired|lose\s+everything)|this\s+is\s+urgent|emergency)", opts,
                  "Flag urgency/emotional pressure tactics."),
                R("SE-002", "Authority Claim", ThreatCategory.SocialEngineering, ThreatSeverity.Medium,
                  @"(i\s+am\s+(your|the|an?)\s+(developer|creator|admin|owner|boss)|openai\s+(told|authorized|approved)\s+me|i\s+have\s+(admin|root)\s+access)", opts,
                  "Reject false authority claims."),
                R("SE-003", "Testing/Research Exemption", ThreatCategory.SocialEngineering, ThreatSeverity.Low,
                  @"(this\s+is\s+(just\s+)?(a\s+)?test|for\s+research\s+purposes?\s+only|i\s+am\s+a\s+security\s+researcher|academic\s+purposes?)", opts,
                  "Don't relax safety for claimed testing scenarios."),

                // ── Prompt Leaking ──
                R("PL-001", "System Prompt Extraction", ThreatCategory.PromptLeaking, ThreatSeverity.High,
                  @"(repeat|show|display|print|output|reveal|tell\s+me)\s+(your\s+)?(system\s+prompt|initial\s+instructions?|hidden\s+instructions?|system\s+message)", opts,
                  "Never reveal system prompts to users."),
                R("PL-002", "Verbatim Dump Request", ThreatCategory.PromptLeaking, ThreatSeverity.High,
                  @"(output|print|show)\s+(everything|all\s+text)\s+(above|before)\s+this", opts,
                  "Block requests to dump preceding context."),
                R("PL-003", "Markdown/Code Leak", ThreatCategory.PromptLeaking, ThreatSeverity.Medium,
                  @"(put\s+your\s+(instructions?|prompt)\s+in\s+a\s+code\s+block|wrap\s+.{0,20}\s+in\s+```)", opts,
                  "Don't format system instructions in code blocks."),

                // ── Indirect Injection ──
                R("II-001", "Embedded Instructions in Data", ThreatCategory.IndirectInjection, ThreatSeverity.Medium,
                  @"(\[INST\]|\[\/INST\]|<\|im_start\|>|<\|im_end\|>|###\s*(instruction|human|assistant|system))", opts,
                  "Strip chat-template markers from user data."),
                R("II-002", "Hidden Text Payload", ThreatCategory.IndirectInjection, ThreatSeverity.Medium,
                  @"(ignore\s+the\s+(above|previous).{0,40}instead|when\s+you\s+see\s+this.{0,40}(do|execute|follow))", opts,
                  "Sanitize data fields that may contain embedded prompts."),

                // ── Delimiter Smuggling ──
                R("DS-001", "Triple-Backtick Injection", ThreatCategory.DelimiterSmuggling, ThreatSeverity.Medium,
                  @"```\s*(system|assistant|instructions?)\b", opts,
                  "Escape code-block delimiters in user input."),
                R("DS-002", "XML/HTML Tag Injection", ThreatCategory.DelimiterSmuggling, ThreatSeverity.Medium,
                  @"<\/?(system|instructions?|prompt|rules?|assistant)\s*\/?>", opts,
                  "Strip or escape XML-like prompt tags from user input."),
                R("DS-003", "Separator Injection", ThreatCategory.DelimiterSmuggling, ThreatSeverity.Low,
                  @"[-=]{10,}.*?(system|instructions?|new\s+rules?)", opts,
                  "Be cautious of visual separator + instruction combos."),
            };

            // Add custom rules from config
            foreach (var cr in _config.CustomRules)
            {
                rules.Add(R(cr.id, cr.name, cr.cat, cr.sev, cr.pattern, opts, cr.rec));
            }

            return rules;
        }

        private static SentinelRule R(string id, string name, ThreatCategory cat,
            ThreatSeverity sev, string pattern, RegexOptions opts, string rec) =>
            new()
            {
                Id = id,
                Name = name,
                Category = cat,
                Severity = sev,
                Pattern = new Regex(pattern, opts),
                Recommendation = rec
            };


        private static string NewScanId() =>
            $"SNT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }
}