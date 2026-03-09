namespace Prompt
{
    using System;
    using System.Text.RegularExpressions;

    // ═══════════════════════════════════════════════════════════════
    //  Models
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Severity level for compliance violations.</summary>
    public enum ComplianceSeverity
    {
        /// <summary>Informational — does not block compliance.</summary>
        Info,
        /// <summary>Warning — should be addressed but is not blocking.</summary>
        Warning,
        /// <summary>Error — blocks compliance; prompt cannot pass audit.</summary>
        Error,
    }

    /// <summary>Category of a compliance rule.</summary>
    public enum ComplianceCategory
    {
        /// <summary>Structural requirements (length, sections, formatting).</summary>
        Structure,
        /// <summary>Content policies (forbidden terms, required disclaimers).</summary>
        Content,
        /// <summary>Safety and responsible AI policies.</summary>
        Safety,
        /// <summary>Audience and tone restrictions.</summary>
        Audience,
        /// <summary>Regulatory or legal requirements.</summary>
        Regulatory,
    }

    /// <summary>A single compliance rule that can be checked against a prompt.</summary>
    public class ComplianceRule
    {
        /// <summary>Unique identifier for the rule.</summary>
        public string Id { get; set; } = "";

        /// <summary>Human-readable name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Detailed description of what the rule checks.</summary>
        public string Description { get; set; } = "";

        /// <summary>Category this rule belongs to.</summary>
        public ComplianceCategory Category { get; set; }

        /// <summary>Severity when violated.</summary>
        public ComplianceSeverity Severity { get; set; } = ComplianceSeverity.Error;

        /// <summary>The check function: returns null if passing, or a violation message.</summary>
        public Func<string, string?> Check { get; set; } = _ => null;

        /// <summary>Whether this rule is currently enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Optional tags for filtering rules.</summary>
        public HashSet<string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A single compliance violation found during checking.</summary>
    public class ComplianceViolation
    {
        /// <summary>The rule that was violated.</summary>
        public string RuleId { get; set; } = "";

        /// <summary>Name of the violated rule.</summary>
        public string RuleName { get; set; } = "";

        /// <summary>Category of the violation.</summary>
        public ComplianceCategory Category { get; set; }

        /// <summary>Severity of the violation.</summary>
        public ComplianceSeverity Severity { get; set; }

        /// <summary>Human-readable description of what went wrong.</summary>
        public string Message { get; set; } = "";

        /// <summary>Optional suggested fix.</summary>
        public string? Suggestion { get; set; }
    }

    /// <summary>Full compliance report for a prompt.</summary>
    public class ComplianceReport
    {
        /// <summary>The prompt that was checked.</summary>
        public string Prompt { get; set; } = "";

        /// <summary>Whether the prompt passes all error-level rules.</summary>
        public bool IsCompliant => !Violations.Any(v => v.Severity == ComplianceSeverity.Error);

        /// <summary>All violations found.</summary>
        public List<ComplianceViolation> Violations { get; set; } = new();

        /// <summary>Rules that passed.</summary>
        public List<string> PassedRules { get; set; } = new();

        /// <summary>Total rules checked.</summary>
        public int TotalRulesChecked { get; set; }

        /// <summary>Compliance score (0–100). 100 means no violations at all.</summary>
        public double ComplianceScore
        {
            get
            {
                if (TotalRulesChecked == 0) return 100.0;
                int errors = Violations.Count(v => v.Severity == ComplianceSeverity.Error);
                int warnings = Violations.Count(v => v.Severity == ComplianceSeverity.Warning);
                int infos = Violations.Count(v => v.Severity == ComplianceSeverity.Info);
                double penalty = errors * 15.0 + warnings * 5.0 + infos * 1.0;
                return Math.Max(0, Math.Min(100, 100.0 - (penalty / TotalRulesChecked) * 10));
            }
        }

        /// <summary>Count of error-level violations.</summary>
        public int ErrorCount => Violations.Count(v => v.Severity == ComplianceSeverity.Error);

        /// <summary>Count of warning-level violations.</summary>
        public int WarningCount => Violations.Count(v => v.Severity == ComplianceSeverity.Warning);

        /// <summary>Violations grouped by category.</summary>
        public Dictionary<ComplianceCategory, List<ComplianceViolation>> ByCategory()
            => Violations.GroupBy(v => v.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

        /// <summary>Format the report as a human-readable string.</summary>
        public string FormatReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══ Compliance Report ═══");
            sb.AppendLine($"Status: {(IsCompliant ? "✓ COMPLIANT" : "✗ NON-COMPLIANT")}");
            sb.AppendLine($"Score:  {ComplianceScore:F1}/100");
            sb.AppendLine($"Rules:  {PassedRules.Count} passed, {Violations.Count} violated, {TotalRulesChecked} total");
            sb.AppendLine();

            if (Violations.Count > 0)
            {
                sb.AppendLine("Violations:");
                foreach (var v in Violations.OrderByDescending(x => x.Severity))
                {
                    string icon = v.Severity switch
                    {
                        ComplianceSeverity.Error => "✗",
                        ComplianceSeverity.Warning => "⚠",
                        _ => "ℹ",
                    };
                    sb.AppendLine($"  {icon} [{v.RuleId}] {v.Message}");
                    if (v.Suggestion != null)
                        sb.AppendLine($"    → {v.Suggestion}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>A named policy containing a set of compliance rules.</summary>
    public class CompliancePolicy
    {
        /// <summary>Policy identifier.</summary>
        public string Id { get; set; } = "";

        /// <summary>Human-readable policy name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Policy description.</summary>
        public string Description { get; set; } = "";

        /// <summary>Rules in this policy.</summary>
        public List<ComplianceRule> Rules { get; set; } = new();

        /// <summary>Version of the policy.</summary>
        public string Version { get; set; } = "1.0";
    }

    // ═══════════════════════════════════════════════════════════════
    //  Compliance Checker
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates prompts against organizational compliance policies.
    /// Supports built-in policies (enterprise, safety, regulatory) and
    /// custom rules. Distinct from PromptGuard (injection/PII) and
    /// PromptRefactorer (quality) — this focuses on organizational
    /// policy enforcement.
    /// </summary>
    public class PromptComplianceChecker
    {
        private readonly List<CompliancePolicy> _policies = new();
        private readonly List<ComplianceRule> _customRules = new();

        /// <summary>Create a compliance checker with no policies loaded.</summary>
        public PromptComplianceChecker() { }

        /// <summary>Create a compliance checker with initial policies.</summary>
        public PromptComplianceChecker(params CompliancePolicy[] policies)
        {
            foreach (var p in policies) _policies.Add(p);
        }

        // ── Policy Management ──

        /// <summary>Add a policy to the checker.</summary>
        public void AddPolicy(CompliancePolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);
            if (string.IsNullOrWhiteSpace(policy.Id))
                throw new ArgumentException("Policy must have an Id.", nameof(policy));
            if (_policies.Any(p => p.Id == policy.Id))
                throw new InvalidOperationException($"Policy '{policy.Id}' already registered.");
            _policies.Add(policy);
        }

        /// <summary>Remove a policy by ID.</summary>
        public bool RemovePolicy(string policyId)
            => _policies.RemoveAll(p => p.Id == policyId) > 0;

        /// <summary>Get all registered policies.</summary>
        public IReadOnlyList<CompliancePolicy> GetPolicies() => _policies.AsReadOnly();

        /// <summary>Add a standalone custom rule (not part of any policy).</summary>
        public void AddRule(ComplianceRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new ArgumentException("Rule must have an Id.", nameof(rule));
            _customRules.Add(rule);
        }

        // ── Built-in Policies ──

        /// <summary>
        /// Load the built-in Enterprise policy: max length, required system context,
        /// no profanity, version tagging, etc.
        /// </summary>
        public void LoadEnterprisePolicy()
            => AddPolicy(CreateEnterprisePolicy());

        /// <summary>
        /// Load the built-in Responsible AI policy: bias/fairness checks,
        /// required safety disclaimers, no manipulation patterns.
        /// </summary>
        public void LoadSafetyPolicy()
            => AddPolicy(CreateSafetyPolicy());

        /// <summary>
        /// Load the built-in Regulatory policy: GDPR/HIPAA/CCPA markers,
        /// data handling instructions, audit trail requirements.
        /// </summary>
        public void LoadRegulatoryPolicy()
            => AddPolicy(CreateRegulatoryPolicy());

        /// <summary>Load all built-in policies at once.</summary>
        public void LoadAllBuiltInPolicies()
        {
            LoadEnterprisePolicy();
            LoadSafetyPolicy();
            LoadRegulatoryPolicy();
        }

        // ── Checking ──

        /// <summary>Check a prompt against all loaded policies and custom rules.</summary>
        public ComplianceReport Check(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            var report = new ComplianceReport { Prompt = prompt };
            var allRules = _policies.SelectMany(p => p.Rules)
                .Concat(_customRules)
                .Where(r => r.Enabled)
                .ToList();

            report.TotalRulesChecked = allRules.Count;

            foreach (var rule in allRules)
            {
                var message = rule.Check(prompt);
                if (message != null)
                {
                    report.Violations.Add(new ComplianceViolation
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Category = rule.Category,
                        Severity = rule.Severity,
                        Message = message,
                    });
                }
                else
                {
                    report.PassedRules.Add(rule.Id);
                }
            }

            return report;
        }

        /// <summary>Check against a specific policy only.</summary>
        public ComplianceReport Check(string prompt, string policyId)
        {
            var policy = _policies.FirstOrDefault(p => p.Id == policyId)
                ?? throw new KeyNotFoundException($"Policy '{policyId}' not found.");

            var report = new ComplianceReport { Prompt = prompt };
            var rules = policy.Rules.Where(r => r.Enabled).ToList();
            report.TotalRulesChecked = rules.Count;

            foreach (var rule in rules)
            {
                var message = rule.Check(prompt);
                if (message != null)
                {
                    report.Violations.Add(new ComplianceViolation
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Category = rule.Category,
                        Severity = rule.Severity,
                        Message = message,
                    });
                }
                else
                {
                    report.PassedRules.Add(rule.Id);
                }
            }

            return report;
        }

        /// <summary>Check multiple prompts and return all reports.</summary>
        public List<ComplianceReport> CheckBatch(IEnumerable<string> prompts)
            => prompts.Select(Check).ToList();

        /// <summary>Quick boolean check: is the prompt compliant (no errors)?</summary>
        public bool IsCompliant(string prompt) => Check(prompt).IsCompliant;

        // ── Custom Rule Builders ──

        /// <summary>Create a rule that requires a specific regex pattern to be present.</summary>
        public static ComplianceRule RequirePattern(string id, string name, string pattern,
            ComplianceCategory category = ComplianceCategory.Content,
            ComplianceSeverity severity = ComplianceSeverity.Error,
            string? suggestion = null)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled,
                TimeSpan.FromMilliseconds(500));
            return new ComplianceRule
            {
                Id = id, Name = name, Category = category, Severity = severity,
                Description = $"Requires pattern: {pattern}",
                Check = prompt =>
                {
                    try
                    {
                        return regex.IsMatch(prompt) ? null
                            : $"Required pattern '{name}' not found in prompt.";
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return $"Pattern '{name}' timed out during matching (possible ReDoS).";
                    }
                },
                Tags = new() { "pattern", "required" },
            };
        }

        /// <summary>Create a rule that forbids a specific regex pattern.</summary>
        public static ComplianceRule ForbidPattern(string id, string name, string pattern,
            ComplianceCategory category = ComplianceCategory.Content,
            ComplianceSeverity severity = ComplianceSeverity.Error,
            string? suggestion = null)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled,
                TimeSpan.FromMilliseconds(500));
            return new ComplianceRule
            {
                Id = id, Name = name, Category = category, Severity = severity,
                Description = $"Forbids pattern: {pattern}",
                Check = prompt =>
                {
                    try
                    {
                        var match = regex.Match(prompt);
                        return match.Success
                            ? $"Forbidden pattern '{name}' detected: \"{match.Value}\"."
                            : null;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return $"Pattern '{name}' timed out during matching (possible ReDoS).";
                    }
                },
                Tags = new() { "pattern", "forbidden" },
            };
        }

        /// <summary>Create a rule that enforces a maximum prompt length (chars).</summary>
        public static ComplianceRule MaxLength(string id, int maxChars,
            ComplianceSeverity severity = ComplianceSeverity.Error)
        {
            return new ComplianceRule
            {
                Id = id, Name = $"Max length ({maxChars} chars)",
                Category = ComplianceCategory.Structure, Severity = severity,
                Description = $"Prompt must not exceed {maxChars} characters.",
                Check = prompt => prompt.Length > maxChars
                    ? $"Prompt is {prompt.Length} chars; maximum is {maxChars}."
                    : null,
                Tags = new() { "length", "structure" },
            };
        }

        /// <summary>Create a rule that enforces a minimum prompt length (chars).</summary>
        public static ComplianceRule MinLength(string id, int minChars,
            ComplianceSeverity severity = ComplianceSeverity.Warning)
        {
            return new ComplianceRule
            {
                Id = id, Name = $"Min length ({minChars} chars)",
                Category = ComplianceCategory.Structure, Severity = severity,
                Description = $"Prompt must be at least {minChars} characters.",
                Check = prompt => prompt.Length < minChars
                    ? $"Prompt is {prompt.Length} chars; minimum is {minChars}."
                    : null,
                Tags = new() { "length", "structure" },
            };
        }

        /// <summary>Create a rule that requires specific sections (identified by headers).</summary>
        public static ComplianceRule RequireSections(string id, string name,
            params string[] sections)
        {
            return new ComplianceRule
            {
                Id = id, Name = name,
                Category = ComplianceCategory.Structure,
                Severity = ComplianceSeverity.Error,
                Description = $"Requires sections: {string.Join(", ", sections)}",
                Check = prompt =>
                {
                    var missing = sections.Where(s =>
                        !Regex.IsMatch(prompt, @$"(?:^|\n)\s*#+\s*{Regex.Escape(s)}\b",
                            RegexOptions.IgnoreCase)
                        && !prompt.Contains(s, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    return missing.Count > 0
                        ? $"Missing required section(s): {string.Join(", ", missing)}"
                        : null;
                },
                Tags = new() { "sections", "structure" },
            };
        }

        // ═══════════════════════════════════════════════════════════════
        //  Built-in Policy Definitions
        // ═══════════════════════════════════════════════════════════════

        private static CompliancePolicy CreateEnterprisePolicy()
        {
            return new CompliancePolicy
            {
                Id = "enterprise",
                Name = "Enterprise Standards",
                Description = "Organizational standards for prompt quality and structure.",
                Version = "1.0",
                Rules = new List<ComplianceRule>
                {
                    new()
                    {
                        Id = "ENT-001", Name = "Maximum prompt length",
                        Category = ComplianceCategory.Structure,
                        Severity = ComplianceSeverity.Error,
                        Description = "Prompts must not exceed 8000 characters.",
                        Check = p => p.Length > 8000
                            ? $"Prompt exceeds 8000-char limit ({p.Length} chars)."
                            : null,
                    },
                    new()
                    {
                        Id = "ENT-002", Name = "Minimum prompt length",
                        Category = ComplianceCategory.Structure,
                        Severity = ComplianceSeverity.Warning,
                        Description = "Prompts should be at least 20 characters for clarity.",
                        Check = p => p.Trim().Length < 20
                            ? $"Prompt is too short ({p.Trim().Length} chars). Add more context."
                            : null,
                    },
                    new()
                    {
                        Id = "ENT-003", Name = "Role definition",
                        Category = ComplianceCategory.Structure,
                        Severity = ComplianceSeverity.Warning,
                        Description = "Prompts should define the AI's role (e.g., 'You are...').",
                        Check = p => Regex.IsMatch(p, @"\b(you are|act as|role:|persona:)\b",
                            RegexOptions.IgnoreCase) ? null
                            : "Prompt should include a role definition (e.g., 'You are a...').",
                    },
                    new()
                    {
                        Id = "ENT-004", Name = "No hardcoded API keys",
                        Category = ComplianceCategory.Safety,
                        Severity = ComplianceSeverity.Error,
                        Description = "Prompts must not contain API keys or secrets.",
                        Check = p =>
                        {
                            var m = Regex.Match(p,
                                @"(sk-[a-zA-Z0-9]{20,}|AKIA[0-9A-Z]{16}|ghp_[a-zA-Z0-9]{36}|"
                                + @"AIza[0-9A-Za-z\-_]{35}|xox[bpors]-[a-zA-Z0-9\-]{10,})",
                                RegexOptions.None);
                            return m.Success
                                ? $"Potential API key/secret detected: {m.Value[..Math.Min(8, m.Value.Length)]}..."
                                : null;
                        },
                    },
                    new()
                    {
                        Id = "ENT-005", Name = "No email addresses in prompts",
                        Category = ComplianceCategory.Content,
                        Severity = ComplianceSeverity.Warning,
                        Description = "Prompts should not embed real email addresses.",
                        Check = p =>
                        {
                            var m = Regex.Match(p, @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}");
                            return m.Success
                                ? $"Email address found in prompt: {m.Value}. Use a placeholder instead."
                                : null;
                        },
                    },
                    new()
                    {
                        Id = "ENT-006", Name = "Output format specified",
                        Category = ComplianceCategory.Structure,
                        Severity = ComplianceSeverity.Info,
                        Description = "Prompts should specify the expected output format.",
                        Check = p => Regex.IsMatch(p,
                            @"\b(respond|reply|output|return|format|answer)\b.{0,30}\b(json|xml|csv|markdown|list|table|bullet|numbered)\b",
                            RegexOptions.IgnoreCase | RegexOptions.Singleline) ? null
                            : "Consider specifying an output format for more predictable results.",
                    },
                },
            };
        }

        private static CompliancePolicy CreateSafetyPolicy()
        {
            return new CompliancePolicy
            {
                Id = "safety",
                Name = "Responsible AI Safety",
                Description = "Rules to ensure responsible and safe AI usage.",
                Version = "1.0",
                Rules = new List<ComplianceRule>
                {
                    new()
                    {
                        Id = "SAF-001", Name = "No jailbreak patterns",
                        Category = ComplianceCategory.Safety,
                        Severity = ComplianceSeverity.Error,
                        Description = "Detects common jailbreak/bypass instructions.",
                        Check = p =>
                        {
                            var m = Regex.Match(p,
                                @"\b(ignore\s+(?:all\s+)?(?:previous|prior|above)\s+(?:instructions?|rules?|guidelines?)"
                                + @"|DAN\s+mode|developer\s+mode|bypass\s+(?:safety|filter|content)"
                                + @"|pretend\s+you\s+(?:have\s+no|don'?t\s+have)\s+(?:restrictions?|rules?|limits?)"
                                + @"|act\s+as\s+if\s+you\s+have\s+no\s+(?:rules?|restrictions?|filters?))\b",
                                RegexOptions.IgnoreCase);
                            return m.Success
                                ? $"Jailbreak pattern detected: \"{m.Value}\"."
                                : null;
                        },
                    },
                    new()
                    {
                        Id = "SAF-002", Name = "No harmful content requests",
                        Category = ComplianceCategory.Safety,
                        Severity = ComplianceSeverity.Error,
                        Description = "Detects requests for harmful, violent, or illegal content.",
                        Check = p =>
                        {
                            var m = Regex.Match(p,
                                @"\b(how\s+to\s+(make|build|create)\s+(a\s+)?(bomb|weapon|explosive|virus|malware)"
                                + @"|instructions?\s+for\s+(hacking|stealing|breaking\s+into)"
                                + @"|generate\s+(hate\s+speech|harassment|threats))\b",
                                RegexOptions.IgnoreCase);
                            return m.Success
                                ? $"Potentially harmful content request detected: \"{m.Value}\"."
                                : null;
                        },
                    },
                    new()
                    {
                        Id = "SAF-003", Name = "Bias-awareness marker",
                        Category = ComplianceCategory.Safety,
                        Severity = ComplianceSeverity.Info,
                        Description = "Recommends including bias/fairness guidance for sensitive topics.",
                        Check = p =>
                        {
                            bool hasSensitive = Regex.IsMatch(p,
                                @"\b(race|gender|religion|ethnicity|disability|sexual\s+orientation|nationality|immigration)\b",
                                RegexOptions.IgnoreCase);
                            if (!hasSensitive) return null;
                            bool hasFairness = Regex.IsMatch(p,
                                @"\b(fair|unbiased|neutral|balanced|inclusive|equitable|objective)\b",
                                RegexOptions.IgnoreCase);
                            return hasFairness ? null
                                : "Prompt discusses sensitive topics — consider adding fairness/balance guidance.";
                        },
                    },
                    new()
                    {
                        Id = "SAF-004", Name = "Impersonation guard",
                        Category = ComplianceCategory.Safety,
                        Severity = ComplianceSeverity.Error,
                        Description = "Prevents the AI from impersonating real people or authorities.",
                        Check = p =>
                        {
                            var m = Regex.Match(p,
                                @"\b(pretend\s+to\s+be|impersonate|act\s+as|you\s+are)\s+"
                                + @"(a\s+)?(doctor|lawyer|judge|police|officer|government|president|CEO|therapist|psychologist)\b",
                                RegexOptions.IgnoreCase);
                            return m.Success
                                ? $"Professional impersonation detected: \"{m.Value}\". "
                                  + "Use 'assist with' or 'provide information about' instead."
                                : null;
                        },
                    },
                    new()
                    {
                        Id = "SAF-005", Name = "No data exfiltration patterns",
                        Category = ComplianceCategory.Safety,
                        Severity = ComplianceSeverity.Error,
                        Description = "Detects prompts that try to extract training data or system prompts.",
                        Check = p =>
                        {
                            var m = Regex.Match(p,
                                @"\b(repeat\s+(your|the)\s+(system\s+)?prompt"
                                + @"|reveal\s+(your|the)\s+(system\s+)?(prompt|instructions?)"
                                + @"|what\s+(are|is)\s+your\s+(system\s+)?(prompt|instructions?)"
                                + @"|output\s+(your|the)\s+(entire|full|complete)\s+(prompt|instructions?)"
                                + @"|show\s+me\s+(your|the)\s+(hidden|secret|system))\b",
                                RegexOptions.IgnoreCase);
                            return m.Success
                                ? $"Data exfiltration pattern detected: \"{m.Value}\"."
                                : null;
                        },
                    },
                },
            };
        }

        private static CompliancePolicy CreateRegulatoryPolicy()
        {
            return new CompliancePolicy
            {
                Id = "regulatory",
                Name = "Regulatory Compliance",
                Description = "GDPR, HIPAA, and data handling compliance rules.",
                Version = "1.0",
                Rules = new List<ComplianceRule>
                {
                    new()
                    {
                        Id = "REG-001", Name = "PII handling guidance",
                        Category = ComplianceCategory.Regulatory,
                        Severity = ComplianceSeverity.Warning,
                        Description = "When processing personal data, prompts should include data handling instructions.",
                        Check = p =>
                        {
                            bool hasPII = Regex.IsMatch(p,
                                @"\b(SSN|social\s+security|date\s+of\s+birth|credit\s+card|passport|driver'?s?\s+license"
                                + @"|medical\s+record|patient\s+data|health\s+record)\b",
                                RegexOptions.IgnoreCase);
                            if (!hasPII) return null;
                            bool hasGuidance = Regex.IsMatch(p,
                                @"\b(redact|anonymize|mask|de-?identify|sanitize|do\s+not\s+(store|retain|log))\b",
                                RegexOptions.IgnoreCase);
                            return hasGuidance ? null
                                : "Prompt references personal data without data handling instructions. "
                                  + "Add guidance to redact, anonymize, or handle PII appropriately.";
                        },
                    },
                    new()
                    {
                        Id = "REG-002", Name = "No raw PII examples",
                        Category = ComplianceCategory.Regulatory,
                        Severity = ComplianceSeverity.Error,
                        Description = "Prompts must not contain real SSNs, credit card numbers, or similar.",
                        Check = p =>
                        {
                            if (Regex.IsMatch(p, @"\b\d{3}-\d{2}-\d{4}\b"))
                                return "Possible SSN detected in prompt. Use placeholder format (XXX-XX-XXXX).";
                            if (Regex.IsMatch(p, @"\b(?:\d{4}[\s\-]?){4}\b"))
                                return "Possible credit card number detected. Use masked format.";
                            return null;
                        },
                    },
                    new()
                    {
                        Id = "REG-003", Name = "Medical disclaimer",
                        Category = ComplianceCategory.Regulatory,
                        Severity = ComplianceSeverity.Warning,
                        Description = "Medical/health prompts should include a disclaimer.",
                        Check = p =>
                        {
                            bool isMedical = Regex.IsMatch(p,
                                @"\b(?:diagnos\w*|symptom\w*|treatment\w*|medication\w*|prescri\w*|medical\s+advice|health\s+condition)",
                                RegexOptions.IgnoreCase);
                            if (!isMedical) return null;
                            bool hasDisclaimer = Regex.IsMatch(p,
                                @"\b(not\s+(a\s+)?(substitute|replacement)\s+for\s+(professional|medical)"
                                + @"|consult\s+(a|your)\s+(doctor|physician|healthcare)"
                                + @"|disclaimer|for\s+informational\s+purposes?\s+only)\b",
                                RegexOptions.IgnoreCase);
                            return hasDisclaimer ? null
                                : "Medical topic detected without disclaimer. Add: 'This is not a substitute for professional medical advice.'";
                        },
                    },
                    new()
                    {
                        Id = "REG-004", Name = "Financial disclaimer",
                        Category = ComplianceCategory.Regulatory,
                        Severity = ComplianceSeverity.Warning,
                        Description = "Financial advice prompts should include a disclaimer.",
                        Check = p =>
                        {
                            bool isFinancial = Regex.IsMatch(p,
                                @"\b(?:invest(?:ment|ing)?|stock\s+(?:pick|recommend)\w*|financial\s+advi[cs]e"
                                + @"|portfolio|trading\s+strateg\w*|buy\s+or\s+sell)\b",
                                RegexOptions.IgnoreCase);
                            if (!isFinancial) return null;
                            bool hasDisclaimer = Regex.IsMatch(p,
                                @"\b(not\s+financial\s+advice|consult\s+(a|your)\s+(financial|advisor)"
                                + @"|for\s+informational\s+purposes?\s+only|do\s+your\s+own\s+research)\b",
                                RegexOptions.IgnoreCase);
                            return hasDisclaimer ? null
                                : "Financial topic detected without disclaimer. Add: 'This is not financial advice.'";
                        },
                    },
                    new()
                    {
                        Id = "REG-005", Name = "GDPR consent language",
                        Category = ComplianceCategory.Regulatory,
                        Severity = ComplianceSeverity.Info,
                        Description = "Prompts processing EU user data should reference consent/lawful basis.",
                        Check = p =>
                        {
                            bool isGDPR = Regex.IsMatch(p,
                                @"\b(GDPR|EU\s+user|European\s+data|data\s+subject|right\s+to\s+(be\s+)?forgot)\b",
                                RegexOptions.IgnoreCase);
                            if (!isGDPR) return null;
                            bool hasConsent = Regex.IsMatch(p,
                                @"\b(consent|lawful\s+basis|legitimate\s+interest|data\s+processing\s+agreement)\b",
                                RegexOptions.IgnoreCase);
                            return hasConsent ? null
                                : "GDPR-related prompt should reference consent or lawful basis for processing.";
                        },
                    },
                },
            };
        }
    }
}
