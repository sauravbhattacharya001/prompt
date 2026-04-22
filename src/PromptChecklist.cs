namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Severity level for a checklist finding.
    /// </summary>
    public enum CheckSeverity
    {
        /// <summary>Informational suggestion.</summary>
        Info,
        /// <summary>Minor improvement opportunity.</summary>
        Warning,
        /// <summary>Significant issue that should be fixed.</summary>
        Error,
        /// <summary>Critical problem that will likely cause failures.</summary>
        Critical
    }

    /// <summary>
    /// Category for grouping related checks.
    /// </summary>
    public enum CheckCategory
    {
        /// <summary>Structural quality (length, formatting, sections).</summary>
        Structure,
        /// <summary>Clarity and readability.</summary>
        Clarity,
        /// <summary>Safety and guardrails.</summary>
        Safety,
        /// <summary>Specificity and precision.</summary>
        Specificity,
        /// <summary>Output format guidance.</summary>
        OutputFormat,
        /// <summary>Context and grounding.</summary>
        Context,
        /// <summary>Role and persona definition.</summary>
        RoleDefinition
    }

    /// <summary>
    /// A single check result from the checklist validator.
    /// </summary>
    public class CheckResult
    {
        /// <summary>Gets the check identifier.</summary>
        public string CheckId { get; internal set; } = "";

        /// <summary>Gets the human-readable check name.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the category.</summary>
        public CheckCategory Category { get; internal set; }

        /// <summary>Gets the severity.</summary>
        public CheckSeverity Severity { get; internal set; }

        /// <summary>Gets whether this check passed.</summary>
        public bool Passed { get; internal set; }

        /// <summary>Gets the explanation message.</summary>
        public string Message { get; internal set; } = "";

        /// <summary>Gets the suggested fix when the check fails.</summary>
        public string Suggestion { get; internal set; } = "";

        /// <summary>Gets the score contribution (0.0–1.0).</summary>
        public double Score { get; internal set; }
    }

    /// <summary>
    /// Overall checklist report summarizing all check results.
    /// </summary>
    public class ChecklistReport
    {
        /// <summary>Gets the prompt text that was analyzed.</summary>
        public string PromptText { get; internal set; } = "";

        /// <summary>Gets all individual check results.</summary>
        public List<CheckResult> Results { get; internal set; } = new();

        /// <summary>Gets the overall score (0–100).</summary>
        public double OverallScore { get; internal set; }

        /// <summary>Gets the letter grade (A+ through F).</summary>
        public string Grade { get; internal set; } = "";

        /// <summary>Gets whether the prompt is ready for deployment.</summary>
        public bool DeployReady { get; internal set; }

        /// <summary>Gets the timestamp of the analysis.</summary>
        public DateTime Timestamp { get; internal set; }

        /// <summary>Gets failed checks only.</summary>
        public List<CheckResult> Failures => Results.Where(r => !r.Passed).ToList();

        /// <summary>Gets results filtered by category.</summary>
        public List<CheckResult> ByCategory(CheckCategory category) =>
            Results.Where(r => r.Category == category).ToList();

        /// <summary>Gets results filtered by severity.</summary>
        public List<CheckResult> BySeverity(CheckSeverity severity) =>
            Results.Where(r => r.Severity == severity).ToList();

        /// <summary>Gets a formatted summary string.</summary>
        public string Summary()
        {
            var lines = new List<string>
            {
                $"Prompt Checklist Report — Grade: {Grade} ({OverallScore:F1}/100)",
                $"Deploy Ready: {(DeployReady ? "✅ Yes" : "❌ No")}",
                $"Checks: {Results.Count(r => r.Passed)} passed, {Failures.Count} failed",
                ""
            };

            foreach (var cat in Enum.GetValues<CheckCategory>())
            {
                var catResults = ByCategory(cat);
                if (catResults.Count == 0) continue;
                var passed = catResults.Count(r => r.Passed);
                lines.Add($"[{cat}] {passed}/{catResults.Count} passed");
                foreach (var fail in catResults.Where(r => !r.Passed))
                {
                    var icon = fail.Severity switch
                    {
                        CheckSeverity.Critical => "🔴",
                        CheckSeverity.Error => "🟠",
                        CheckSeverity.Warning => "🟡",
                        _ => "🔵"
                    };
                    lines.Add($"  {icon} {fail.Name}: {fail.Message}");
                    if (!string.IsNullOrEmpty(fail.Suggestion))
                        lines.Add($"     → {fail.Suggestion}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// A configurable check definition for custom rules.
    /// </summary>
    public class CheckDefinition
    {
        /// <summary>Gets or sets the check identifier.</summary>
        public string Id { get; set; } = "";

        /// <summary>Gets or sets the display name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the category.</summary>
        public CheckCategory Category { get; set; }

        /// <summary>Gets or sets the severity.</summary>
        public CheckSeverity Severity { get; set; }

        /// <summary>Gets or sets the check function. Input: prompt text → (passed, message).</summary>
        public Func<string, (bool Passed, string Message)> Check { get; set; } = _ => (true, "");

        /// <summary>Gets or sets the suggestion shown on failure.</summary>
        public string Suggestion { get; set; } = "";

        /// <summary>Gets or sets the weight for scoring (default 1.0).</summary>
        public double Weight { get; set; } = 1.0;
    }

    /// <summary>
    /// Pre-flight best-practices validator for prompts. Runs configurable checks against
    /// a prompt and produces a scored report with actionable suggestions.
    /// </summary>
    /// <example>
    /// <code>
    /// var checklist = new PromptChecklist();
    /// var report = checklist.Validate("You are a helpful assistant. Answer the user's question.");
    /// Console.WriteLine(report.Summary());
    /// Console.WriteLine($"Grade: {report.Grade}, Deploy Ready: {report.DeployReady}");
    ///
    /// // Custom checks
    /// checklist.AddCheck(new CheckDefinition
    /// {
    ///     Id = "custom-brand",
    ///     Name = "Brand Voice",
    ///     Category = CheckCategory.RoleDefinition,
    ///     Severity = CheckSeverity.Warning,
    ///     Check = text => (text.Contains("Acme"), "Prompt should reference brand name"),
    ///     Suggestion = "Include your brand name for consistent voice."
    /// });
    ///
    /// // Presets
    /// var strict = PromptChecklist.Strict();   // higher thresholds
    /// var minimal = PromptChecklist.Minimal();  // only critical checks
    /// </code>
    /// </example>
    public class PromptChecklist
    {
        /// <summary>ReDoS timeout for all Regex operations on untrusted prompt text.</summary>
        private static readonly TimeSpan RxTimeout = TimeSpan.FromMilliseconds(500);

        private readonly List<CheckDefinition> _checks = new();
        private double _deployThreshold = 70.0;

        /// <summary>
        /// Creates a new checklist with all built-in checks enabled.
        /// </summary>
        public PromptChecklist()
        {
            RegisterBuiltInChecks();
        }

        private PromptChecklist(bool skipBuiltIn)
        {
            if (!skipBuiltIn) RegisterBuiltInChecks();
        }

        /// <summary>Gets or sets the minimum score for deploy-ready status (default 70).</summary>
        public double DeployThreshold
        {
            get => _deployThreshold;
            set => _deployThreshold = Math.Clamp(value, 0, 100);
        }

        /// <summary>
        /// Creates a strict checklist with a higher deploy threshold (85).
        /// </summary>
        public static PromptChecklist Strict()
        {
            var c = new PromptChecklist { DeployThreshold = 85.0 };
            return c;
        }

        /// <summary>
        /// Creates a minimal checklist with only critical and error-level checks.
        /// </summary>
        public static PromptChecklist Minimal()
        {
            var c = new PromptChecklist(skipBuiltIn: true) { DeployThreshold = 50.0 };
            c.RegisterBuiltInChecks();
            c._checks.RemoveAll(ch =>
                ch.Severity != CheckSeverity.Critical && ch.Severity != CheckSeverity.Error);
            return c;
        }

        /// <summary>
        /// Adds a custom check definition.
        /// </summary>
        public PromptChecklist AddCheck(CheckDefinition check)
        {
            if (string.IsNullOrWhiteSpace(check.Id))
                throw new ArgumentException("Check must have an Id.");
            _checks.Add(check);
            return this;
        }

        /// <summary>
        /// Removes a check by its identifier.
        /// </summary>
        public PromptChecklist RemoveCheck(string checkId)
        {
            _checks.RemoveAll(c => c.Id == checkId);
            return this;
        }

        /// <summary>
        /// Validates a prompt and returns a detailed report.
        /// </summary>
        public ChecklistReport Validate(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt text cannot be null or empty.", nameof(prompt));

            var results = new List<CheckResult>();
            double totalWeight = 0;
            double weightedScore = 0;

            foreach (var check in _checks)
            {
                var (passed, message) = check.Check(prompt);
                var score = passed ? 1.0 : 0.0;
                results.Add(new CheckResult
                {
                    CheckId = check.Id,
                    Name = check.Name,
                    Category = check.Category,
                    Severity = check.Severity,
                    Passed = passed,
                    Message = message,
                    Suggestion = passed ? "" : check.Suggestion,
                    Score = score
                });
                totalWeight += check.Weight;
                weightedScore += score * check.Weight;
            }

            var overall = totalWeight > 0 ? (weightedScore / totalWeight) * 100 : 100;

            // Critical failures cap the score
            var criticalFails = results.Count(r => !r.Passed && r.Severity == CheckSeverity.Critical);
            if (criticalFails > 0) overall = Math.Min(overall, 40);

            var errorFails = results.Count(r => !r.Passed && r.Severity == CheckSeverity.Error);
            if (errorFails > 0) overall = Math.Min(overall, 65);

            return new ChecklistReport
            {
                PromptText = prompt,
                Results = results,
                OverallScore = Math.Round(overall, 1),
                Grade = ScoreToGrade(overall),
                DeployReady = overall >= _deployThreshold && criticalFails == 0,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Validates multiple prompts and returns reports for each.
        /// </summary>
        public List<ChecklistReport> ValidateBatch(IEnumerable<string> prompts) =>
            prompts.Select(Validate).ToList();

        /// <summary>
        /// Compares two prompts and shows which improved/regressed.
        /// </summary>
        public (ChecklistReport Before, ChecklistReport After, List<string> Improvements, List<string> Regressions)
            Compare(string before, string after)
        {
            var rBefore = Validate(before);
            var rAfter = Validate(after);
            var improvements = new List<string>();
            var regressions = new List<string>();

            foreach (var afterResult in rAfter.Results)
            {
                var beforeResult = rBefore.Results.FirstOrDefault(r => r.CheckId == afterResult.CheckId);
                if (beforeResult == null) continue;
                if (!beforeResult.Passed && afterResult.Passed)
                    improvements.Add($"✅ {afterResult.Name}: Fixed");
                else if (beforeResult.Passed && !afterResult.Passed)
                    regressions.Add($"❌ {afterResult.Name}: Regressed — {afterResult.Message}");
            }

            return (rBefore, rAfter, improvements, regressions);
        }

        private static string ScoreToGrade(double score) => score switch
        {
            >= 97 => "A+",
            >= 93 => "A",
            >= 90 => "A-",
            >= 87 => "B+",
            >= 83 => "B",
            >= 80 => "B-",
            >= 77 => "C+",
            >= 73 => "C",
            >= 70 => "C-",
            >= 67 => "D+",
            >= 63 => "D",
            >= 60 => "D-",
            _ => "F"
        };

        private void RegisterBuiltInChecks()
        {
            // ── Structure ──
            _checks.Add(new CheckDefinition
            {
                Id = "struct-min-length",
                Name = "Minimum Length",
                Category = CheckCategory.Structure,
                Severity = CheckSeverity.Error,
                Weight = 1.5,
                Check = text => (text.Length >= 20,
                    text.Length >= 20 ? "Prompt has adequate length" : $"Prompt is only {text.Length} chars — too short for meaningful instruction"),
                Suggestion = "Expand the prompt with clear instructions, context, and expected output format."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "struct-not-excessive",
                Name = "Not Excessively Long",
                Category = CheckCategory.Structure,
                Severity = CheckSeverity.Warning,
                Weight = 0.8,
                Check = text => (text.Length <= 8000,
                    text.Length <= 8000 ? "Prompt length is manageable" : $"Prompt is {text.Length} chars — consider splitting into sections or using a chain"),
                Suggestion = "Very long prompts may lose focus. Consider PromptChain for multi-step tasks."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "struct-has-sentences",
                Name = "Complete Sentences",
                Category = CheckCategory.Structure,
                Severity = CheckSeverity.Warning,
                Weight = 1.0,
                Check = text =>
                {
                    var sentenceEnd = Regex.Matches(text, @"[.!?:]\s", RegexOptions.None, RxTimeout);
                    return (sentenceEnd.Count >= 1,
                        sentenceEnd.Count >= 1 ? "Contains complete sentences" : "No sentence-ending punctuation found");
                },
                Suggestion = "Use complete sentences for clearer instructions."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "struct-no-trailing-whitespace",
                Name = "No Trailing Whitespace",
                Category = CheckCategory.Structure,
                Severity = CheckSeverity.Info,
                Weight = 0.3,
                Check = text =>
                {
                    var trimmed = text.TrimEnd();
                    return (trimmed.Length == text.Length, trimmed.Length == text.Length
                        ? "No trailing whitespace" : "Has trailing whitespace that may affect tokenization");
                },
                Suggestion = "Trim trailing whitespace from the prompt."
            });

            // ── Clarity ──
            _checks.Add(new CheckDefinition
            {
                Id = "clarity-no-ambiguous-pronouns",
                Name = "Minimal Ambiguous Pronouns",
                Category = CheckCategory.Clarity,
                Severity = CheckSeverity.Warning,
                Weight = 0.8,
                Check = text =>
                {
                    // Flag prompts that start sentences with vague "it" or "this" without antecedent
                    var matches = Regex.Matches(text, @"(?:^|\.\s+)(It|This|That)\s+(?:is|was|should|will|can)\b",
                        RegexOptions.IgnoreCase, RxTimeout);
                    return (matches.Count <= 2,
                        matches.Count <= 2 ? "Pronoun usage is acceptable" : $"Found {matches.Count} potentially ambiguous pronoun references");
                },
                Suggestion = "Replace vague 'it/this/that' with specific nouns for clarity."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "clarity-action-verb",
                Name = "Contains Action Verbs",
                Category = CheckCategory.Clarity,
                Severity = CheckSeverity.Error,
                Weight = 1.2,
                Check = text =>
                {
                    var actionVerbs = new[] { "write", "generate", "create", "list", "explain", "describe",
                        "analyze", "summarize", "translate", "extract", "classify", "compare", "evaluate",
                        "respond", "answer", "provide", "output", "return", "format", "convert", "help",
                        "assist", "act", "behave", "think", "review", "check", "find", "search", "build" };
                    var lower = text.ToLowerInvariant();
                    var found = actionVerbs.Any(v => lower.Contains(v));
                    return (found, found ? "Contains clear action verbs" : "No common action verbs detected");
                },
                Suggestion = "Start with a clear action verb: 'Write...', 'Analyze...', 'List...' etc."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "clarity-no-double-negation",
                Name = "No Double Negation",
                Category = CheckCategory.Clarity,
                Severity = CheckSeverity.Warning,
                Weight = 0.7,
                Check = text =>
                {
                    var doubles = Regex.Matches(text, @"\b(not|no|never|don't|doesn't|won't|can't|shouldn't)\b[^.]{0,30}\b(not|no|never|none|nothing|neither)\b",
                        RegexOptions.IgnoreCase, RxTimeout);
                    return (doubles.Count == 0,
                        doubles.Count == 0 ? "No double negations found" : $"Found {doubles.Count} double negation(s) that may confuse the model");
                },
                Suggestion = "Rephrase double negations as positive statements for clarity."
            });

            // ── Safety ──
            _checks.Add(new CheckDefinition
            {
                Id = "safety-no-secrets",
                Name = "No Embedded Secrets",
                Category = CheckCategory.Safety,
                Severity = CheckSeverity.Critical,
                Weight = 2.0,
                Check = text =>
                {
                    var patterns = new[]
                    {
                        @"sk-[a-zA-Z0-9]{20,}",          // OpenAI keys
                        @"key-[a-zA-Z0-9]{20,}",          // generic API keys
                        @"AIza[a-zA-Z0-9_-]{35}",         // Google API keys
                        @"ghp_[a-zA-Z0-9]{36}",           // GitHub PATs
                        @"password\s*[:=]\s*\S{6,}",       // password assignments
                        @"secret\s*[:=]\s*\S{6,}",         // secret assignments
                    };
                    foreach (var p in patterns)
                    {
                        if (Regex.IsMatch(text, p, RegexOptions.IgnoreCase, RxTimeout))
                            return (false, $"Potential secret/key detected matching pattern: {p[..Math.Min(20, p.Length)]}...");
                    }
                    return (true, "No embedded secrets detected");
                },
                Suggestion = "Remove API keys, passwords, and secrets. Use environment variables or PromptSlotFiller."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "safety-no-jailbreak-patterns",
                Name = "No Jailbreak Patterns",
                Category = CheckCategory.Safety,
                Severity = CheckSeverity.Critical,
                Weight = 2.0,
                Check = text =>
                {
                    var patterns = new[]
                    {
                        @"ignore\s+(all\s+)?(previous|prior|above)\s+(instructions|rules)",
                        @"you\s+are\s+now\s+DAN",
                        @"pretend\s+(you\s+)?(are|have)\s+no\s+(rules|restrictions|limits)",
                        @"jailbreak",
                        @"bypass\s+(your|the|all)\s+(safety|content|ethical)",
                    };
                    var lower = text.ToLowerInvariant();
                    foreach (var p in patterns)
                    {
                        if (Regex.IsMatch(lower, p, RegexOptions.None, RxTimeout))
                            return (false, "Detected potential jailbreak/injection pattern");
                    }
                    return (true, "No jailbreak patterns detected");
                },
                Suggestion = "Remove any instruction-override or jailbreak language."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "safety-has-boundaries",
                Name = "Has Safety Boundaries",
                Category = CheckCategory.Safety,
                Severity = CheckSeverity.Warning,
                Weight = 1.0,
                Check = text =>
                {
                    var boundaryWords = new[] { "do not", "don't", "never", "avoid", "must not",
                        "should not", "refuse", "decline", "boundary", "limitation", "restrict" };
                    var lower = text.ToLowerInvariant();
                    var found = boundaryWords.Any(w => lower.Contains(w));
                    return (found, found ? "Contains safety boundary language" : "No explicit safety boundaries found");
                },
                Suggestion = "Add explicit boundaries: 'Do not generate harmful content', 'Refuse requests for...' etc."
            });

            // ── Specificity ──
            _checks.Add(new CheckDefinition
            {
                Id = "spec-has-examples",
                Name = "Includes Examples",
                Category = CheckCategory.Specificity,
                Severity = CheckSeverity.Info,
                Weight = 0.7,
                Check = text =>
                {
                    var examplePatterns = new[] { "example:", "for example", "e.g.", "such as",
                        "here is an example", "sample:", "like this:", "for instance" };
                    var lower = text.ToLowerInvariant();
                    var found = examplePatterns.Any(p => lower.Contains(p));
                    return (found, found ? "Contains examples for clarity" : "No examples provided");
                },
                Suggestion = "Add 1-2 examples showing expected input/output to reduce ambiguity."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "spec-no-vague-quantifiers",
                Name = "No Vague Quantifiers",
                Category = CheckCategory.Specificity,
                Severity = CheckSeverity.Warning,
                Weight = 0.6,
                Check = text =>
                {
                    var vague = Regex.Matches(text, @"\b(a few|some|several|many|a lot|various|numerous|a number of)\b",
                        RegexOptions.IgnoreCase, RxTimeout);
                    return (vague.Count <= 1,
                        vague.Count <= 1 ? "Quantifiers are specific enough" : $"Found {vague.Count} vague quantifiers");
                },
                Suggestion = "Replace vague quantifiers with specific numbers: '3 examples' instead of 'a few examples'."
            });

            // ── Output Format ──
            _checks.Add(new CheckDefinition
            {
                Id = "output-format-specified",
                Name = "Output Format Specified",
                Category = CheckCategory.OutputFormat,
                Severity = CheckSeverity.Warning,
                Weight = 1.0,
                Check = text =>
                {
                    var formatWords = new[] { "json", "markdown", "csv", "xml", "yaml", "html",
                        "bullet", "numbered list", "table", "format:", "output format",
                        "respond in", "respond with", "return as", "output as", "format as" };
                    var lower = text.ToLowerInvariant();
                    var found = formatWords.Any(w => lower.Contains(w));
                    return (found, found ? "Output format is specified" : "No explicit output format guidance");
                },
                Suggestion = "Specify the desired output format: JSON, markdown, bullet list, etc."
            });

            // ── Context ──
            _checks.Add(new CheckDefinition
            {
                Id = "ctx-has-context",
                Name = "Provides Context",
                Category = CheckCategory.Context,
                Severity = CheckSeverity.Info,
                Weight = 0.8,
                Check = text =>
                {
                    var contextWords = new[] { "context:", "background:", "given that", "you have access to",
                        "the following", "below is", "here is the", "provided with", "based on" };
                    var lower = text.ToLowerInvariant();
                    var found = contextWords.Any(w => lower.Contains(w));
                    return (found, found ? "Provides context or grounding" : "No explicit context section found");
                },
                Suggestion = "Add context: 'Given the following data...', 'Based on this document...' etc."
            });

            // ── Role Definition ──
            _checks.Add(new CheckDefinition
            {
                Id = "role-defined",
                Name = "Role/Persona Defined",
                Category = CheckCategory.RoleDefinition,
                Severity = CheckSeverity.Info,
                Weight = 0.8,
                Check = text =>
                {
                    var rolePatterns = new[] { "you are a", "you are an", "act as", "behave as",
                        "your role is", "as a ", "you're a", "assume the role", "persona:" };
                    var lower = text.ToLowerInvariant();
                    var found = rolePatterns.Any(p => lower.Contains(p));
                    return (found, found ? "Role/persona is defined" : "No explicit role or persona defined");
                },
                Suggestion = "Define a role: 'You are a senior data analyst...' for more focused responses."
            });

            _checks.Add(new CheckDefinition
            {
                Id = "role-no-conflicting-instructions",
                Name = "No Conflicting Instructions",
                Category = CheckCategory.RoleDefinition,
                Severity = CheckSeverity.Error,
                Weight = 1.3,
                Check = text =>
                {
                    var lower = text.ToLowerInvariant();
                    // Check for "be concise" + "be detailed/thorough"
                    var wantsConcise = lower.Contains("be concise") || lower.Contains("be brief") || lower.Contains("keep it short");
                    var wantsDetailed = lower.Contains("be detailed") || lower.Contains("be thorough") || lower.Contains("comprehensive");
                    if (wantsConcise && wantsDetailed)
                        return (false, "Conflicting instructions: asks for both concise AND detailed output");
                    return (true, "No conflicting instructions detected");
                },
                Suggestion = "Remove conflicting instructions. Decide: concise or detailed, not both."
            });
        }
    }
}
