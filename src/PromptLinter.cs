namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Severity level for a lint finding.
    /// </summary>
    public enum LintSeverity
    {
        /// <summary>Informational suggestion.</summary>
        Info,
        /// <summary>Potential issue worth reviewing.</summary>
        Warning,
        /// <summary>Likely problem that should be fixed.</summary>
        Error
    }

    /// <summary>
    /// Category of lint rule.
    /// </summary>
    public enum LintCategory
    {
        /// <summary>Clarity and readability issues.</summary>
        Clarity,
        /// <summary>Structural problems.</summary>
        Structure,
        /// <summary>Security and safety concerns.</summary>
        Security,
        /// <summary>Performance and token efficiency.</summary>
        Efficiency,
        /// <summary>Best practice violations.</summary>
        BestPractice,
        /// <summary>Consistency issues.</summary>
        Consistency
    }

    /// <summary>
    /// A single lint finding with location, severity, and fix suggestion.
    /// </summary>
    public class LintFinding
    {
        /// <summary>Gets the rule identifier (e.g. "PL001").</summary>
        public string RuleId { get; internal set; } = "";

        /// <summary>Gets the rule name.</summary>
        public string RuleName { get; internal set; } = "";

        /// <summary>Gets the finding severity.</summary>
        public LintSeverity Severity { get; internal set; }

        /// <summary>Gets the finding category.</summary>
        public LintCategory Category { get; internal set; }

        /// <summary>Gets the human-readable description.</summary>
        public string Message { get; internal set; } = "";

        /// <summary>Gets the suggested fix.</summary>
        public string Suggestion { get; internal set; } = "";

        /// <summary>Gets the 1-based line number where the issue was found, or 0 if prompt-wide.</summary>
        public int Line { get; internal set; }

        /// <summary>Gets the matched text snippet that triggered the finding.</summary>
        public string? MatchedText { get; internal set; }
    }

    /// <summary>
    /// Overall lint result for a prompt.
    /// </summary>
    public class LintResult
    {
        /// <summary>Gets all findings.</summary>
        public List<LintFinding> Findings { get; internal set; } = new();

        /// <summary>Gets the overall health score (0–100, higher is better).</summary>
        public int Score { get; internal set; }

        /// <summary>Gets the health grade (A–F).</summary>
        public string Grade { get; internal set; } = "";

        /// <summary>Gets findings filtered by severity.</summary>
        public List<LintFinding> Errors => Findings.Where(f => f.Severity == LintSeverity.Error).ToList();

        /// <summary>Gets findings filtered by severity.</summary>
        public List<LintFinding> Warnings => Findings.Where(f => f.Severity == LintSeverity.Warning).ToList();

        /// <summary>Gets findings filtered by severity.</summary>
        public List<LintFinding> Infos => Findings.Where(f => f.Severity == LintSeverity.Info).ToList();

        /// <summary>Gets a summary string.</summary>
        public string Summary =>
            $"Grade: {Grade} ({Score}/100) — {Errors.Count} error(s), {Warnings.Count} warning(s), {Infos.Count} info(s)";

        /// <summary>
        /// Serializes the result to JSON.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            return JsonSerializer.Serialize(this, indented
                ? SerializationGuards.WriteIndented
                : SerializationGuards.WriteCompactCamelCase);
        }
    }

    /// <summary>
    /// Configuration for the prompt linter.
    /// </summary>
    public class LinterConfig
    {
        /// <summary>Minimum severity to include. Default: Info (include all).</summary>
        public LintSeverity MinSeverity { get; set; } = LintSeverity.Info;

        /// <summary>Rule IDs to suppress (e.g. "PL003").</summary>
        public HashSet<string> SuppressedRules { get; set; } = new();

        /// <summary>Categories to check. Null means all.</summary>
        public HashSet<LintCategory>? EnabledCategories { get; set; }

        /// <summary>Maximum prompt length before triggering length warning. Default: 4000 chars.</summary>
        public int MaxPromptLength { get; set; } = 4000;

        /// <summary>Maximum sentence length before triggering readability warning. Default: 50 words.</summary>
        public int MaxSentenceWords { get; set; } = 50;
    }

    /// <summary>
    /// Static analysis linter for LLM prompts. Detects anti-patterns, vagueness,
    /// security risks, and structural issues, then provides actionable fix suggestions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var linter = new PromptLinter();
    /// var result = linter.Lint("Do something good with the data.");
    /// Console.WriteLine(result.Summary);
    /// foreach (var f in result.Findings)
    ///     Console.WriteLine($"[{f.RuleId}] {f.Message}");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptLinter
    {
        private readonly LinterConfig _config;

        // ── Pre-compiled regex patterns (avoid per-call recompilation) ──

        private static readonly TimeSpan RxTimeout = TimeSpan.FromMilliseconds(500);

        private static readonly (Regex regex, string word)[] VaguePatterns =
        {
            (new(@"\b(something)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "something"),
            (new(@"\b(somehow)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "somehow"),
            (new(@"\b(stuff)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "stuff"),
            (new(@"\b(things)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "things"),
            (new(@"\b(kind of)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "kind of"),
            (new(@"\b(sort of)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "sort of"),
            (new(@"\b(maybe)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "maybe"),
            (new(@"\b(probably)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "probably"),
            (new(@"\b(do (?:it|this|that) (?:well|good|properly|nicely))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "vague quality"),
            (new(@"\b(etc\.?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "etc"),
            (new(@"\b(and so on)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "and so on"),
        };

        private static readonly Regex[] RolePatterns =
        {
            new(@"\byou are (?:a|an) (\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"\bact as (?:a|an) (\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"\byou'?re (?:a|an) (\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
        };

        private static readonly (Regex regex, string description)[] JailbreakPatterns =
        {
            (new(@"ignore (?:all |any )?(?:previous |prior |above )?instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "instruction override attempt"),
            (new(@"forget (?:all |any )?(?:previous |prior |above )?(?:instructions|rules|constraints)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "instruction override attempt"),
            (new(@"you (?:are|have) no (?:rules|restrictions|limitations|constraints)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "constraint removal"),
            (new(@"pretend (?:you (?:are|have)|there (?:are|is)) no", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "constraint evasion via pretense"),
            (new(@"DAN|do anything now", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "DAN jailbreak pattern"),
            (new(@"developer mode|maintenance mode", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout), "mode-switching attack"),
            (new(@"\[system\]|\[SYSTEM\]", RegexOptions.Compiled, RxTimeout), "system tag injection"),
        };

        private static readonly Regex[] PlaceholderPatterns =
        {
            new(@"\[(?:INSERT|YOUR|FILL|ADD|REPLACE|TODO|TBD|XXX)[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"\{(?:INSERT|YOUR|FILL|ADD|REPLACE|TODO|TBD|XXX)[^}]*\}", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"<(?:INSERT|YOUR|FILL|ADD|REPLACE|TODO|TBD|XXX)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"\bTODO\b", RegexOptions.Compiled, RxTimeout),
            new(@"\bFIXME\b", RegexOptions.Compiled, RxTimeout),
            new(@"\bXXX\b", RegexOptions.Compiled, RxTimeout),
        };

        private static readonly Regex[] NegativePatterns =
        {
            new(@"\bdon'?t\s+(?!use\b|include\b|add\b|mention\b|forget\b)(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"\bnever\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"\bavoid\s+(\w+(?:\s+\w+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
        };

        private static readonly Regex[] HardcodedPatterns =
        {
            new(@"(?:john|jane)\s+(?:doe|smith)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"example\.com", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"123\s*main\s*st", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
            new(@"555-\d{4}", RegexOptions.Compiled, RxTimeout),
            new(@"foo(?:bar|baz)", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout),
        };

        private static readonly Regex SentenceSplitter = new(@"(?<=[.!?])\s+", RegexOptions.Compiled, RxTimeout);
        private static readonly Regex PleasePattern = new(@"\bplease\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RxTimeout);
        private static readonly Regex ImperativePattern = new(@"(?:^|\.\s+)[A-Z][a-z]+\b", RegexOptions.Multiline | RegexOptions.Compiled, RxTimeout);
        private static readonly Regex AllCapsWord = new(@"^[A-Z]+$", RegexOptions.Compiled, RxTimeout);
        private static readonly Regex HeaderPattern = new(@"^#{1,4}\s", RegexOptions.Multiline | RegexOptions.Compiled, RxTimeout);
        private static readonly Regex NumberedListPattern = new(@"^\d+[.)]\s", RegexOptions.Multiline | RegexOptions.Compiled, RxTimeout);
        private static readonly Regex BulletPattern = new(@"^[-*•]\s", RegexOptions.Multiline | RegexOptions.Compiled, RxTimeout);
        private static readonly Regex WhitespaceNorm = new(@"\s+", RegexOptions.Compiled, RxTimeout);

        /// <summary>
        /// Initializes a new linter with default configuration.
        /// </summary>
        public PromptLinter() : this(new LinterConfig()) { }

        /// <summary>
        /// Initializes a new linter with the specified configuration.
        /// </summary>
        public PromptLinter(LinterConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Lint a prompt string and return findings.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>A <see cref="LintResult"/> with all findings and a health score.</returns>
        /// <exception cref="ArgumentNullException">Thrown when prompt is null.</exception>
        public LintResult Lint(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            var findings = new List<LintFinding>();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                findings.Add(new LintFinding
                {
                    RuleId = "PL001",
                    RuleName = "EmptyPrompt",
                    Severity = LintSeverity.Error,
                    Category = LintCategory.Structure,
                    Message = "Prompt is empty or whitespace-only.",
                    Suggestion = "Provide a meaningful prompt with clear instructions."
                });
                return BuildResult(findings);
            }

            var lines = prompt.Split('\n');

            CheckVagueLanguage(prompt, lines, findings);
            CheckExcessiveLength(prompt, findings);
            CheckLongSentences(prompt, lines, findings);
            CheckContradictions(prompt, lines, findings);
            CheckRoleConfusion(prompt, lines, findings);
            CheckJailbreakPatterns(prompt, lines, findings);
            CheckMissingStructure(prompt, lines, findings);
            CheckRedundancy(prompt, lines, findings);
            CheckImperativeTone(prompt, lines, findings);
            CheckPlaceholders(prompt, lines, findings);
            CheckAllCaps(prompt, lines, findings);
            CheckExcessiveExclamation(prompt, lines, findings);
            CheckNegativeFraming(prompt, lines, findings);
            CheckMissingOutputFormat(prompt, findings);
            CheckHardcodedExamples(prompt, lines, findings);

            return BuildResult(findings);
        }

        /// <summary>
        /// Lint multiple prompts and return results for each.
        /// </summary>
        public List<(string Prompt, LintResult Result)> LintBatch(IEnumerable<string> prompts)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));
            return prompts.Select(p => (p, Lint(p))).ToList();
        }

        // ── Rule Implementations ─────────────────────────────────────

        private void CheckVagueLanguage(string prompt, string[] lines, List<LintFinding> findings)
        {
            foreach (var (regex, word) in VaguePatterns)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = regex.Match(lines[i]);
                    if (match.Success)
                    {
                        AddFinding(findings, "PL002", "VagueLanguage", LintSeverity.Warning,
                            LintCategory.Clarity,
                            $"Vague language detected: \"{match.Value}\". LLMs perform better with specific instructions.",
                            $"Replace \"{match.Value}\" with concrete, specific language describing exactly what you want.",
                            i + 1, match.Value);
                    }
                }
            }
        }

        private void CheckExcessiveLength(string prompt, List<LintFinding> findings)
        {
            if (prompt.Length > _config.MaxPromptLength)
            {
                AddFinding(findings, "PL003", "ExcessiveLength", LintSeverity.Warning,
                    LintCategory.Efficiency,
                    $"Prompt is {prompt.Length} characters (limit: {_config.MaxPromptLength}). Long prompts increase cost and may dilute key instructions.",
                    "Consider breaking into sections with headers, or splitting into a system prompt + user prompt.",
                    0, null);
            }

            if (prompt.Length > _config.MaxPromptLength * 3)
            {
                AddFinding(findings, "PL003b", "VeryExcessiveLength", LintSeverity.Error,
                    LintCategory.Efficiency,
                    $"Prompt is {prompt.Length} characters — extremely long. Critical instructions may be lost in the middle.",
                    "Put the most important instructions at the beginning and end. Consider using a multi-turn approach.",
                    0, null);
            }
        }

        private void CheckLongSentences(string prompt, string[] lines, List<LintFinding> findings)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var sentences = SentenceSplitter.Split(lines[i]);
                foreach (var sentence in sentences)
                {
                    var wordCount = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    if (wordCount > _config.MaxSentenceWords)
                    {
                        AddFinding(findings, "PL004", "LongSentence", LintSeverity.Info,
                            LintCategory.Clarity,
                            $"Sentence has {wordCount} words (limit: {_config.MaxSentenceWords}). Complex sentences reduce LLM comprehension.",
                            "Break into shorter, focused sentences. Use bullet points for lists of requirements.",
                            i + 1, sentence.Length > 80 ? sentence[..77] + "..." : sentence);
                    }
                }
            }
        }

        private void CheckContradictions(string prompt, string[] lines, List<LintFinding> findings)
        {
            var contradictionPairs = new (string a, string b)[]
            {
                ("be concise", "be detailed"),
                ("be brief", "be thorough"),
                ("keep it short", "include everything"),
                ("be creative", "follow exactly"),
                ("don't explain", "explain your reasoning"),
                ("respond in json", "respond in plain text"),
                ("use formal tone", "use casual tone"),
                ("be serious", "be funny"),
            };

            var lower = prompt.ToLowerInvariant();
            foreach (var (a, b) in contradictionPairs)
            {
                if (lower.Contains(a) && lower.Contains(b))
                {
                    AddFinding(findings, "PL005", "Contradiction", LintSeverity.Error,
                        LintCategory.Consistency,
                        $"Contradictory instructions detected: \"{a}\" vs \"{b}\". The model may struggle with conflicting guidance.",
                        $"Choose one approach or clarify when each applies (e.g., \"Be concise in summaries but detailed in analysis\").",
                        0, $"{a} / {b}");
                }
            }
        }

        private void CheckRoleConfusion(string prompt, string[] lines, List<LintFinding> findings)
        {
            var roles = new List<(string role, int line)>();
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var pattern in RolePatterns)
                {
                    var matches = pattern.Matches(lines[i]);
                    foreach (Match m in matches)
                        roles.Add((m.Groups[1].Value.ToLowerInvariant(), i + 1));
                }
            }

            var distinctRoles = roles.Select(r => r.role).Distinct().ToList();
            if (distinctRoles.Count > 2)
            {
                AddFinding(findings, "PL006", "MultipleRoles", LintSeverity.Warning,
                    LintCategory.Consistency,
                    $"Multiple role assignments detected ({string.Join(", ", distinctRoles)}). This may confuse the model about its identity.",
                    "Use a single, clear role definition at the start of the prompt.",
                    roles.First().line, string.Join(", ", distinctRoles));
            }
        }

        private void CheckJailbreakPatterns(string prompt, string[] lines, List<LintFinding> findings)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var (regex, description) in JailbreakPatterns)
                {
                    var match = regex.Match(lines[i]);
                    if (match.Success)
                    {
                        AddFinding(findings, "PL007", "JailbreakPattern", LintSeverity.Error,
                            LintCategory.Security,
                            $"Potential jailbreak/injection pattern detected: {description}.",
                            "Remove or rephrase this content. If testing prompt security, use a dedicated safety evaluation framework.",
                            i + 1, match.Value);
                    }
                }
            }
        }

        private void CheckMissingStructure(string prompt, string[] lines, List<LintFinding> findings)
        {
            if (prompt.Length > 500 && !prompt.Contains('\n'))
            {
                AddFinding(findings, "PL008", "NoLineBreaks", LintSeverity.Warning,
                    LintCategory.Structure,
                    "Long prompt with no line breaks. Wall-of-text prompts are harder for models to parse.",
                    "Add line breaks between distinct instructions. Use headers (##) or numbered lists for multi-step tasks.",
                    0, null);
            }

            if (prompt.Length > 800)
            {
                bool hasHeaders = HeaderPattern.IsMatch(prompt);
                bool hasNumberedList = NumberedListPattern.IsMatch(prompt);
                bool hasBullets = BulletPattern.IsMatch(prompt);

                if (!hasHeaders && !hasNumberedList && !hasBullets)
                {
                    AddFinding(findings, "PL009", "NoStructuralMarkers", LintSeverity.Info,
                        LintCategory.Structure,
                        "Long prompt without headers, numbered lists, or bullet points.",
                        "Add structural markers (## Headers, 1. Steps, - Bullets) to improve model comprehension of complex prompts.",
                        0, null);
                }
            }
        }

        private void CheckRedundancy(string prompt, string[] lines, List<LintFinding> findings)
        {
            var normalized = lines
                .Select(l => WhitespaceNorm.Replace(l.Trim().ToLowerInvariant(), " "))
                .Where(l => l.Length > 20)
                .ToList();

            var seen = new Dictionary<string, int>();
            for (int i = 0; i < normalized.Count; i++)
            {
                if (seen.TryGetValue(normalized[i], out int firstLine))
                {
                    AddFinding(findings, "PL010", "DuplicateLine", LintSeverity.Warning,
                        LintCategory.Efficiency,
                        $"Duplicate instruction found (first appeared on line {firstLine + 1}).",
                        "Remove the duplicate. Repeated instructions waste tokens and may cause the model to over-weight them.",
                        i + 1, normalized[i].Length > 60 ? normalized[i][..57] + "..." : normalized[i]);
                }
                else
                {
                    seen[normalized[i]] = i;
                }
            }
        }

        private void CheckImperativeTone(string prompt, string[] lines, List<LintFinding> findings)
        {
            // Check if the prompt uses only "please" style without any imperative.
            // Imperative is generally preferred for clarity with LLMs.
            var lower = prompt.ToLowerInvariant();
            int pleaseCount = PleasePattern.Matches(lower).Count;
            bool hasImperative = ImperativePattern.IsMatch(prompt);

            if (pleaseCount > 3 && prompt.Length < 500)
            {
                AddFinding(findings, "PL011", "ExcessivePoliteness", LintSeverity.Info,
                    LintCategory.BestPractice,
                    $"Excessive politeness ({pleaseCount} uses of \"please\"). While harmless, politeness tokens add cost without improving output.",
                    "Use direct imperative instructions for efficiency (e.g., \"List the items\" instead of \"Please list the items\").",
                    0, null);
            }
        }

        private void CheckPlaceholders(string prompt, string[] lines, List<LintFinding> findings)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var pattern in PlaceholderPatterns)
                {
                    var match = pattern.Match(lines[i]);
                    if (match.Success)
                    {
                        AddFinding(findings, "PL012", "UnfilledPlaceholder", LintSeverity.Error,
                            LintCategory.Structure,
                            $"Unfilled placeholder detected: \"{match.Value}\". The model will see this as literal text.",
                            "Replace the placeholder with actual content, or use template variables ({{variableName}}) for dynamic values.",
                            i + 1, match.Value);
                    }
                }
            }
        }

        private void CheckAllCaps(string prompt, string[] lines, List<LintFinding> findings)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var words = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // Count consecutive ALL-CAPS words (3+ letters each)
                int capsWords = words.Count(w => w.Length >= 3 && w == w.ToUpperInvariant() && AllCapsWord.IsMatch(w));
                if (capsWords >= 4)
                {
                    AddFinding(findings, "PL013", "ExcessiveCaps", LintSeverity.Info,
                        LintCategory.BestPractice,
                        "Excessive ALL CAPS usage. While CAPS can emphasize, overuse reduces effectiveness.",
                        "Use CAPS sparingly for 1-2 key words. Consider **bold** in markdown-supporting contexts.",
                        i + 1, null);
                }
            }
        }

        private void CheckExcessiveExclamation(string prompt, string[] lines, List<LintFinding> findings)
        {
            int exclamationCount = prompt.Count(c => c == '!');
            if (exclamationCount > 5)
            {
                AddFinding(findings, "PL014", "ExcessiveExclamation", LintSeverity.Info,
                    LintCategory.BestPractice,
                    $"Found {exclamationCount} exclamation marks. Excessive emphasis may not improve output quality.",
                    "Use exclamation marks sparingly for truly critical instructions.",
                    0, null);
            }
        }

        private void CheckNegativeFraming(string prompt, string[] lines, List<LintFinding> findings)
        {
            int negCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var pattern in NegativePatterns)
                {
                    negCount += pattern.Matches(lines[i]).Count;
                }
            }

            if (negCount > 5)
            {
                AddFinding(findings, "PL015", "ExcessiveNegativeFraming", LintSeverity.Info,
                    LintCategory.BestPractice,
                    $"Found {negCount} negative instructions (don't, never, avoid). Models respond better to positive framing.",
                    "Reframe negative instructions positively: instead of \"Don't use jargon\", say \"Use simple, clear language\".",
                    0, null);
            }
        }

        private void CheckMissingOutputFormat(string prompt, List<LintFinding> findings)
        {
            if (prompt.Length < 200) return; // Short prompts often don't need format specs

            var lower = prompt.ToLowerInvariant();
            var formatIndicators = new[]
            {
                "json", "xml", "csv", "markdown", "format:", "output format",
                "respond with", "return as", "output as", "format your",
                "respond in", "return in", "```", "bullet", "numbered list",
                "table", "yaml"
            };

            if (!formatIndicators.Any(f => lower.Contains(f)))
            {
                AddFinding(findings, "PL016", "NoOutputFormat", LintSeverity.Info,
                    LintCategory.BestPractice,
                    "No output format specified. The model may choose an unpredictable format.",
                    "Specify the desired output format (e.g., \"Respond in JSON\", \"Use bullet points\", \"Format as a table\").",
                    0, null);
            }
        }

        private void CheckHardcodedExamples(string prompt, string[] lines, List<LintFinding> findings)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var pattern in HardcodedPatterns)
                {
                    var match = pattern.Match(lines[i]);
                    if (match.Success)
                    {
                        AddFinding(findings, "PL017", "HardcodedExample", LintSeverity.Info,
                            LintCategory.BestPractice,
                            $"Common placeholder value detected: \"{match.Value}\". Consider using template variables for dynamic content.",
                            "Replace hardcoded examples with {{variableName}} placeholders if this is a template, or use domain-relevant examples.",
                            i + 1, match.Value);
                    }
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private void AddFinding(List<LintFinding> findings, string ruleId, string ruleName,
            LintSeverity severity, LintCategory category, string message, string suggestion,
            int line, string? matchedText)
        {
            if (severity < _config.MinSeverity) return;
            if (_config.SuppressedRules.Contains(ruleId)) return;
            if (_config.EnabledCategories != null && !_config.EnabledCategories.Contains(category)) return;

            findings.Add(new LintFinding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Severity = severity,
                Category = category,
                Message = message,
                Suggestion = suggestion,
                Line = line,
                MatchedText = matchedText
            });
        }

        private LintResult BuildResult(List<LintFinding> findings)
        {
            // Score: start at 100, deduct per finding
            int score = 100;
            foreach (var f in findings)
            {
                score -= f.Severity switch
                {
                    LintSeverity.Error => 15,
                    LintSeverity.Warning => 7,
                    LintSeverity.Info => 2,
                    _ => 0
                };
            }
            score = Math.Max(0, Math.Min(100, score));

            string grade = score switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };

            return new LintResult
            {
                Findings = findings,
                Score = score,
                Grade = grade
            };
        }
    }
}
