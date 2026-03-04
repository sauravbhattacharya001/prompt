namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ───────────────────────── Enums & DTOs ─────────────────────────

    /// <summary>Severity of a refactoring suggestion.</summary>
    public enum RefactorSeverity
    {
        /// <summary>Minor improvement, nice to have.</summary>
        Info,
        /// <summary>Moderate improvement, recommended.</summary>
        Warning,
        /// <summary>Significant issue, strongly recommended.</summary>
        Error
    }

    /// <summary>Category of a refactoring suggestion.</summary>
    public enum RefactorCategory
    {
        /// <summary>Repeated phrases or sections.</summary>
        Redundancy,
        /// <summary>Vague or ambiguous instructions.</summary>
        Specificity,
        /// <summary>Missing structural elements.</summary>
        Structure,
        /// <summary>Opportunities to extract variables.</summary>
        ExtractVariable,
        /// <summary>Overly long prompt sections.</summary>
        Length,
        /// <summary>Conflicting or contradictory instructions.</summary>
        Contradiction,
        /// <summary>Missing role or persona definition.</summary>
        Persona,
        /// <summary>Format or output specification issues.</summary>
        Format,
        /// <summary>Prompt could be split into a chain.</summary>
        Decomposition
    }

    /// <summary>
    /// A single refactoring suggestion with location, description, and optional auto-fix.
    /// </summary>
    public class RefactorSuggestion
    {
        /// <summary>The category of this suggestion.</summary>
        [JsonPropertyName("category")]
        public RefactorCategory Category { get; init; }

        /// <summary>The severity of this suggestion.</summary>
        [JsonPropertyName("severity")]
        public RefactorSeverity Severity { get; init; }

        /// <summary>Human-readable description of the issue.</summary>
        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        /// <summary>The problematic text snippet (if applicable).</summary>
        [JsonPropertyName("snippet")]
        public string? Snippet { get; init; }

        /// <summary>Suggested replacement text (null if no auto-fix).</summary>
        [JsonPropertyName("suggestion")]
        public string? Suggestion { get; init; }

        /// <summary>Zero-based character offset where the issue starts.</summary>
        [JsonPropertyName("offset")]
        public int Offset { get; init; }

        /// <summary>Length of the problematic span.</summary>
        [JsonPropertyName("length")]
        public int Length { get; init; }

        /// <summary>Whether an automatic fix is available.</summary>
        [JsonIgnore]
        public bool HasAutoFix => Suggestion != null;
    }

    /// <summary>
    /// Overall quality grade for a prompt after analysis.
    /// </summary>
    public enum PromptGrade
    {
        /// <summary>Excellent prompt, minimal issues.</summary>
        A,
        /// <summary>Good prompt, minor improvements possible.</summary>
        B,
        /// <summary>Acceptable prompt, several issues.</summary>
        C,
        /// <summary>Below average, needs refactoring.</summary>
        D,
        /// <summary>Poor prompt, significant refactoring needed.</summary>
        F
    }

    /// <summary>
    /// Complete result of analyzing a prompt for refactoring opportunities.
    /// </summary>
    public class RefactorReport
    {
        /// <summary>The original prompt text.</summary>
        [JsonPropertyName("originalPrompt")]
        public string OriginalPrompt { get; init; } = "";

        /// <summary>List of all suggestions found.</summary>
        [JsonPropertyName("suggestions")]
        public List<RefactorSuggestion> Suggestions { get; init; } = new();

        /// <summary>Overall quality score from 0 to 100.</summary>
        [JsonPropertyName("score")]
        public int Score { get; init; }

        /// <summary>Letter grade derived from score.</summary>
        [JsonPropertyName("grade")]
        public PromptGrade Grade { get; init; }

        /// <summary>Estimated token count of the original prompt.</summary>
        [JsonPropertyName("tokenCount")]
        public int TokenCount { get; init; }

        /// <summary>Number of distinct sections detected.</summary>
        [JsonPropertyName("sectionCount")]
        public int SectionCount { get; init; }

        /// <summary>Number of template variables found.</summary>
        [JsonPropertyName("variableCount")]
        public int VariableCount { get; init; }

        /// <summary>Suggestions grouped by category.</summary>
        [JsonIgnore]
        public Dictionary<RefactorCategory, List<RefactorSuggestion>> ByCategory =>
            Suggestions.GroupBy(s => s.Category)
                       .ToDictionary(g => g.Key, g => g.ToList());

        /// <summary>Count of error-severity suggestions.</summary>
        [JsonIgnore]
        public int ErrorCount => Suggestions.Count(s => s.Severity == RefactorSeverity.Error);

        /// <summary>Count of warning-severity suggestions.</summary>
        [JsonIgnore]
        public int WarningCount => Suggestions.Count(s => s.Severity == RefactorSeverity.Warning);

        /// <summary>Count of info-severity suggestions.</summary>
        [JsonIgnore]
        public int InfoCount => Suggestions.Count(s => s.Severity == RefactorSeverity.Info);

        /// <summary>Whether any auto-fixable suggestions exist.</summary>
        [JsonIgnore]
        public bool HasAutoFixes => Suggestions.Any(s => s.HasAutoFix);

        /// <summary>Generates a human-readable text report.</summary>
        public string ToTextReport()
        {
            var lines = new List<string>
            {
                "═══════════════════════════════════════════",
                "  PROMPT REFACTORING REPORT",
                "═══════════════════════════════════════════",
                "",
                $"  Score: {Score}/100 ({Grade})",
                $"  Tokens: ~{TokenCount}  |  Sections: {SectionCount}  |  Variables: {VariableCount}",
                $"  Issues: {ErrorCount} errors, {WarningCount} warnings, {InfoCount} info",
                ""
            };

            if (Suggestions.Count == 0)
            {
                lines.Add("  ✅ No refactoring suggestions — prompt looks great!");
            }
            else
            {
                foreach (var group in ByCategory.OrderByDescending(g => g.Value.Max(s => (int)s.Severity)))
                {
                    lines.Add($"  ── {group.Key} ──");
                    foreach (var s in group.Value)
                    {
                        var icon = s.Severity switch
                        {
                            RefactorSeverity.Error => "🔴",
                            RefactorSeverity.Warning => "🟡",
                            _ => "🔵"
                        };
                        lines.Add($"    {icon} {s.Description}");
                        if (s.Snippet != null)
                        {
                            var snip = s.Snippet.Length > 80
                                ? s.Snippet[..77] + "..."
                                : s.Snippet;
                            lines.Add($"       └─ \"{snip}\"");
                        }
                        if (s.HasAutoFix)
                            lines.Add($"       💡 Fix: {s.Suggestion}");
                    }
                    lines.Add("");
                }
            }

            lines.Add("═══════════════════════════════════════════");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Serializes the report to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    /// <summary>
    /// Configuration for the prompt refactorer.
    /// </summary>
    public class RefactorerConfig
    {
        /// <summary>Minimum number of repeated words to flag redundancy (default: 3).</summary>
        public int MinRepeatThreshold { get; init; } = 3;

        /// <summary>Minimum phrase length (words) for duplicate phrase detection (default: 4).</summary>
        public int MinPhraseLength { get; init; } = 4;

        /// <summary>Maximum recommended prompt length in estimated tokens (default: 2000).</summary>
        public int MaxRecommendedTokens { get; init; } = 2000;

        /// <summary>Maximum recommended section length in estimated tokens (default: 500).</summary>
        public int MaxSectionTokens { get; init; } = 500;

        /// <summary>Whether to check for persona/role definition (default: true).</summary>
        public bool CheckPersona { get; init; } = true;

        /// <summary>Whether to check for output format specification (default: true).</summary>
        public bool CheckOutputFormat { get; init; } = true;

        /// <summary>Whether to detect extract-variable opportunities (default: true).</summary>
        public bool DetectExtractVariable { get; init; } = true;

        /// <summary>Whether to detect decomposition opportunities (default: true).</summary>
        public bool DetectDecomposition { get; init; } = true;

        /// <summary>Categories to skip during analysis.</summary>
        public HashSet<RefactorCategory> SkipCategories { get; init; } = new();

        /// <summary>Default configuration.</summary>
        public static RefactorerConfig Default => new();
    }

    /// <summary>
    /// Analyzes prompts for refactoring opportunities — redundancy, vagueness,
    /// missing structure, extract-variable candidates, contradictions, and more.
    /// Like a code linter but for prompt engineering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The refactorer performs heuristic-based static analysis (no LLM calls)
    /// and produces actionable suggestions with optional auto-fix text.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var refactorer = new PromptRefactorer();
    /// var report = refactorer.Analyze("You are a helpful assistant. Be helpful. Help the user.");
    /// // report.Suggestions → redundancy detected ("helpful" repeated 3x)
    /// // report.Score → 62
    /// // report.Grade → C
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptRefactorer
    {
        private readonly RefactorerConfig _config;

        // ── Vague phrase patterns ──
        private static readonly string[] VaguePhrases = new[]
        {
            "do your best", "try to", "if possible", "as needed",
            "be good", "do well", "make it nice", "handle appropriately",
            "use common sense", "figure it out", "as you see fit",
            "whatever works", "something like", "kind of", "sort of",
            "maybe", "perhaps", "probably", "I guess", "I think maybe",
            "do the right thing", "be smart about it", "use your judgment"
        };

        // ── Contradiction pair patterns (simplified) ──
        private static readonly (string, string)[] ContradictionPairs = new[]
        {
            ("be concise", "be detailed"),
            ("be brief", "be thorough"),
            ("be concise", "be thorough"),
            ("keep it short", "be comprehensive"),
            ("be formal", "be casual"),
            ("be formal", "be informal"),
            ("use simple language", "use technical language"),
            ("avoid jargon", "use technical terms"),
            ("be creative", "follow the template exactly"),
            ("be creative", "stick to the facts"),
            ("don't make assumptions", "infer what the user means"),
            ("never apologize", "apologize if wrong"),
            ("respond in json", "respond in plain text"),
            ("respond in json", "respond in markdown"),
            ("use bullet points", "use numbered lists"),
            ("one paragraph", "multiple paragraphs"),
            ("always", "never")
        };

        // ── Output format indicators ──
        private static readonly string[] OutputFormatIndicators = new[]
        {
            "json", "xml", "csv", "markdown", "bullet", "numbered list",
            "table", "yaml", "plain text", "format:", "output format",
            "respond in", "respond with", "return as", "output as",
            "structured as", "formatted as"
        };

        // ── Persona indicators ──
        private static readonly string[] PersonaIndicators = new[]
        {
            "you are", "act as", "pretend to be", "role:", "persona:",
            "as a", "you're a", "behave as", "imagine you are"
        };

        // ── Filler/hedge phrases that can be removed ──
        private static readonly string[] FillerPhrases = new[]
        {
            "please note that", "it is important to note that",
            "it should be noted that", "keep in mind that",
            "i would like you to", "i want you to",
            "i need you to", "can you please",
            "would you please", "could you please",
            "basically", "essentially", "fundamentally",
            "in order to", "for the purpose of",
            "at this point in time", "at the end of the day",
            "it goes without saying"
        };

        /// <summary>
        /// Creates a new PromptRefactorer with the specified configuration.
        /// </summary>
        public PromptRefactorer(RefactorerConfig? config = null)
        {
            _config = config ?? RefactorerConfig.Default;
        }

        /// <summary>
        /// Analyzes a prompt and returns a refactoring report with suggestions.
        /// </summary>
        /// <param name="prompt">The prompt text to analyze.</param>
        /// <returns>A complete refactoring report.</returns>
        /// <exception cref="ArgumentException">Thrown when prompt is null or empty.</exception>
        public RefactorReport Analyze(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var suggestions = new List<RefactorSuggestion>();
            var sections = DetectSections(prompt);
            var variables = DetectVariables(prompt);
            int tokenCount = EstimateTokens(prompt);

            if (!_config.SkipCategories.Contains(RefactorCategory.Redundancy))
                suggestions.AddRange(CheckRedundancy(prompt));

            if (!_config.SkipCategories.Contains(RefactorCategory.Specificity))
                suggestions.AddRange(CheckSpecificity(prompt));

            if (!_config.SkipCategories.Contains(RefactorCategory.Structure))
                suggestions.AddRange(CheckStructure(prompt, sections));

            if (!_config.SkipCategories.Contains(RefactorCategory.ExtractVariable) && _config.DetectExtractVariable)
                suggestions.AddRange(CheckExtractVariable(prompt));

            if (!_config.SkipCategories.Contains(RefactorCategory.Length))
                suggestions.AddRange(CheckLength(prompt, sections, tokenCount));

            if (!_config.SkipCategories.Contains(RefactorCategory.Contradiction))
                suggestions.AddRange(CheckContradictions(prompt));

            if (!_config.SkipCategories.Contains(RefactorCategory.Persona) && _config.CheckPersona)
                suggestions.AddRange(CheckPersona(prompt));

            if (!_config.SkipCategories.Contains(RefactorCategory.Format) && _config.CheckOutputFormat)
                suggestions.AddRange(CheckOutputFormat(prompt));

            if (!_config.SkipCategories.Contains(RefactorCategory.Decomposition) && _config.DetectDecomposition)
                suggestions.AddRange(CheckDecomposition(prompt, sections, tokenCount));

            // Filler phrase detection (maps to Redundancy)
            if (!_config.SkipCategories.Contains(RefactorCategory.Redundancy))
                suggestions.AddRange(CheckFillerPhrases(prompt));

            int score = CalculateScore(suggestions, tokenCount, sections.Count, variables.Count);
            var grade = ScoreToGrade(score);

            return new RefactorReport
            {
                OriginalPrompt = prompt,
                Suggestions = suggestions,
                Score = score,
                Grade = grade,
                TokenCount = tokenCount,
                SectionCount = sections.Count,
                VariableCount = variables.Count
            };
        }

        /// <summary>
        /// Applies all auto-fixable suggestions to the prompt, returning the refactored text.
        /// Fixes are applied in reverse offset order to preserve positions.
        /// </summary>
        /// <param name="prompt">The original prompt.</param>
        /// <returns>The refactored prompt with all auto-fixes applied, and the list of applied fixes.</returns>
        public (string RefactoredPrompt, List<RefactorSuggestion> AppliedFixes) ApplyFixes(string prompt)
        {
            var report = Analyze(prompt);
            return ApplyFixes(prompt, report);
        }

        /// <summary>
        /// Applies auto-fixable suggestions from an existing report.
        /// </summary>
        public (string RefactoredPrompt, List<RefactorSuggestion> AppliedFixes) ApplyFixes(
            string prompt, RefactorReport report)
        {
            var fixable = report.Suggestions
                .Where(s => s.HasAutoFix && s.Offset >= 0 && s.Length > 0
                            && s.Offset + s.Length <= prompt.Length)
                .OrderByDescending(s => s.Offset)
                .ToList();

            var applied = new List<RefactorSuggestion>();
            var result = prompt;

            foreach (var fix in fixable)
            {
                // Verify the text at the offset still matches the snippet
                var actual = result.Substring(fix.Offset, fix.Length);
                if (string.Equals(actual, fix.Snippet, StringComparison.OrdinalIgnoreCase))
                {
                    result = result[..fix.Offset] + fix.Suggestion + result[(fix.Offset + fix.Length)..];
                    applied.Add(fix);
                }
            }

            applied.Reverse(); // return in forward order
            return (result, applied);
        }

        /// <summary>
        /// Compares two prompts and reports which refactoring issues were fixed or introduced.
        /// </summary>
        public RefactorComparison Compare(string before, string after)
        {
            var reportBefore = Analyze(before);
            var reportAfter = Analyze(after);

            var fixedIssues = reportBefore.Suggestions
                .Where(s => !reportAfter.Suggestions.Any(a =>
                    a.Category == s.Category && a.Description == s.Description))
                .ToList();

            var newIssues = reportAfter.Suggestions
                .Where(s => !reportBefore.Suggestions.Any(b =>
                    b.Category == s.Category && b.Description == s.Description))
                .ToList();

            return new RefactorComparison
            {
                BeforeScore = reportBefore.Score,
                AfterScore = reportAfter.Score,
                BeforeGrade = reportBefore.Grade,
                AfterGrade = reportAfter.Grade,
                FixedIssues = fixedIssues,
                NewIssues = newIssues,
                ScoreDelta = reportAfter.Score - reportBefore.Score,
                BeforeTokens = reportBefore.TokenCount,
                AfterTokens = reportAfter.TokenCount
            };
        }

        /// <summary>
        /// Batch-analyzes multiple prompts and returns reports sorted by score (worst first).
        /// </summary>
        public List<RefactorReport> BatchAnalyze(IEnumerable<string> prompts)
        {
            if (prompts == null)
                throw new ArgumentNullException(nameof(prompts));

            return prompts
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(Analyze)
                .OrderBy(r => r.Score)
                .ToList();
        }

        // ───────────────────── Detection Methods ─────────────────────

        private List<RefactorSuggestion> CheckRedundancy(string prompt)
        {
            var suggestions = new List<RefactorSuggestion>();
            var lower = prompt.ToLowerInvariant();
            var words = Regex.Split(lower, @"\W+").Where(w => w.Length > 3).ToArray();

            // Word frequency — flag words repeated too many times
            var freq = new Dictionary<string, int>();
            foreach (var w in words)
            {
                freq.TryGetValue(w, out int c);
                freq[w] = c + 1;
            }

            var stopWords = new HashSet<string>
            {
                "that", "this", "with", "from", "will", "have", "been",
                "should", "would", "could", "your", "they", "them", "their",
                "about", "which", "when", "what", "where", "there", "here",
                "into", "also", "each", "more", "most", "than", "then",
                "very", "just", "only", "some", "such", "does", "make",
                "like", "well", "made", "over", "must", "don't"
            };

            foreach (var (word, count) in freq)
            {
                if (count >= _config.MinRepeatThreshold * 2 && !stopWords.Contains(word))
                {
                    var idx = lower.IndexOf(word, StringComparison.Ordinal);
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.Redundancy,
                        Severity = count >= _config.MinRepeatThreshold * 3
                            ? RefactorSeverity.Warning : RefactorSeverity.Info,
                        Description = $"Word \"{word}\" repeated {count} times — consider consolidating.",
                        Snippet = word,
                        Offset = idx >= 0 ? idx : 0,
                        Length = word.Length
                    });
                }
            }

            // Duplicate phrases (n-grams)
            var phraseLen = _config.MinPhraseLength;
            if (words.Length >= phraseLen * 2)
            {
                var phrases = new Dictionary<string, List<int>>();
                for (int i = 0; i <= words.Length - phraseLen; i++)
                {
                    var phrase = string.Join(" ", words.Skip(i).Take(phraseLen));
                    if (!phrases.ContainsKey(phrase))
                        phrases[phrase] = new List<int>();
                    phrases[phrase].Add(i);
                }

                foreach (var (phrase, positions) in phrases)
                {
                    if (positions.Count >= 2)
                    {
                        var idx = lower.IndexOf(phrase, StringComparison.Ordinal);
                        suggestions.Add(new RefactorSuggestion
                        {
                            Category = RefactorCategory.Redundancy,
                            Severity = RefactorSeverity.Warning,
                            Description = $"Phrase \"{phrase}\" appears {positions.Count} times — extract or consolidate.",
                            Snippet = phrase,
                            Offset = idx >= 0 ? idx : 0,
                            Length = phrase.Length
                        });
                    }
                }
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckSpecificity(string prompt)
        {
            var suggestions = new List<RefactorSuggestion>();
            var lower = prompt.ToLowerInvariant();

            foreach (var vague in VaguePhrases)
            {
                int idx = lower.IndexOf(vague, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.Specificity,
                        Severity = RefactorSeverity.Warning,
                        Description = $"Vague phrase \"{vague}\" — replace with specific instructions.",
                        Snippet = prompt.Substring(idx, vague.Length),
                        Offset = idx,
                        Length = vague.Length
                    });
                }
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckStructure(string prompt, List<PromptSection> sections)
        {
            var suggestions = new List<RefactorSuggestion>();
            int tokenCount = EstimateTokens(prompt);

            // Large prompt with no clear sections
            if (tokenCount > 200 && sections.Count <= 1)
            {
                suggestions.Add(new RefactorSuggestion
                {
                    Category = RefactorCategory.Structure,
                    Severity = RefactorSeverity.Warning,
                    Description = "Long prompt with no clear sections — add headers or numbered sections for clarity.",
                    Offset = 0,
                    Length = 0
                });
            }

            // No line breaks in a long prompt
            if (tokenCount > 100 && !prompt.Contains('\n'))
            {
                suggestions.Add(new RefactorSuggestion
                {
                    Category = RefactorCategory.Structure,
                    Severity = RefactorSeverity.Info,
                    Description = "Long prompt with no line breaks — break into paragraphs for readability.",
                    Offset = 0,
                    Length = 0
                });
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckExtractVariable(string prompt)
        {
            var suggestions = new List<RefactorSuggestion>();

            // Detect repeated literal values that could be variables
            // Look for quoted strings or specific patterns repeated
            var quotedPattern = new Regex(@"""([^""]{3,50})""", RegexOptions.Compiled);
            var matches = quotedPattern.Matches(prompt);
            var quotedValues = new Dictionary<string, List<int>>();

            foreach (Match m in matches)
            {
                var val = m.Groups[1].Value;
                if (!quotedValues.ContainsKey(val))
                    quotedValues[val] = new List<int>();
                quotedValues[val].Add(m.Index);
            }

            foreach (var (value, positions) in quotedValues)
            {
                if (positions.Count >= 2)
                {
                    var varName = SuggestVariableName(value);
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.ExtractVariable,
                        Severity = RefactorSeverity.Info,
                        Description = $"Literal \"{value}\" appears {positions.Count} times — extract to {{{{" + varName + "}}}}.",
                        Snippet = $"\"{value}\"",
                        Suggestion = "{{" + varName + "}}",
                        Offset = positions[0],
                        Length = value.Length + 2
                    });
                }
            }

            // Detect hardcoded numbers that might be configurable
            var numberPattern = new Regex(@"\b(\d{2,})\b");
            var numbers = new Dictionary<string, List<int>>();
            foreach (Match m in numberPattern.Matches(prompt))
            {
                var val = m.Value;
                if (!numbers.ContainsKey(val))
                    numbers[val] = new List<int>();
                numbers[val].Add(m.Index);
            }

            foreach (var (value, positions) in numbers)
            {
                if (positions.Count >= 2)
                {
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.ExtractVariable,
                        Severity = RefactorSeverity.Info,
                        Description = $"Number {value} appears {positions.Count} times — consider extracting to a variable.",
                        Snippet = value,
                        Offset = positions[0],
                        Length = value.Length
                    });
                }
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckLength(string prompt, List<PromptSection> sections, int tokenCount)
        {
            var suggestions = new List<RefactorSuggestion>();

            if (tokenCount > _config.MaxRecommendedTokens)
            {
                suggestions.Add(new RefactorSuggestion
                {
                    Category = RefactorCategory.Length,
                    Severity = tokenCount > _config.MaxRecommendedTokens * 2
                        ? RefactorSeverity.Error : RefactorSeverity.Warning,
                    Description = $"Prompt is ~{tokenCount} tokens (recommended max: {_config.MaxRecommendedTokens}) — consider trimming or splitting.",
                    Offset = 0,
                    Length = 0
                });
            }

            // Check individual sections
            foreach (var section in sections)
            {
                int sectionTokens = EstimateTokens(section.Content);
                if (sectionTokens > _config.MaxSectionTokens)
                {
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.Length,
                        Severity = RefactorSeverity.Info,
                        Description = $"Section \"{section.Title}\" is ~{sectionTokens} tokens — consider breaking it down.",
                        Snippet = section.Title,
                        Offset = section.Offset,
                        Length = section.Title.Length
                    });
                }
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckContradictions(string prompt)
        {
            var suggestions = new List<RefactorSuggestion>();
            var lower = prompt.ToLowerInvariant();

            foreach (var (a, b) in ContradictionPairs)
            {
                int idxA = lower.IndexOf(a, StringComparison.Ordinal);
                int idxB = lower.IndexOf(b, StringComparison.Ordinal);

                if (idxA >= 0 && idxB >= 0)
                {
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.Contradiction,
                        Severity = RefactorSeverity.Error,
                        Description = $"Contradictory instructions: \"{a}\" vs \"{b}\" — resolve the conflict.",
                        Snippet = prompt.Substring(idxA, a.Length),
                        Offset = idxA,
                        Length = a.Length
                    });
                }
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckPersona(string prompt)
        {
            var suggestions = new List<RefactorSuggestion>();
            var lower = prompt.ToLowerInvariant();
            int tokenCount = EstimateTokens(prompt);

            // Only suggest persona for prompts of reasonable length
            if (tokenCount > 30)
            {
                bool hasPersona = PersonaIndicators.Any(p =>
                    lower.Contains(p, StringComparison.Ordinal));

                if (!hasPersona)
                {
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.Persona,
                        Severity = RefactorSeverity.Info,
                        Description = "No persona/role definition found — consider adding \"You are a...\" to set the AI's behavior.",
                        Suggestion = "You are a [role]. ",
                        Offset = 0,
                        Length = 0
                    });
                }
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckOutputFormat(string prompt)
        {
            var suggestions = new List<RefactorSuggestion>();
            var lower = prompt.ToLowerInvariant();
            int tokenCount = EstimateTokens(prompt);

            if (tokenCount > 50)
            {
                bool hasFormat = OutputFormatIndicators.Any(f =>
                    lower.Contains(f, StringComparison.Ordinal));

                if (!hasFormat)
                {
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.Format,
                        Severity = RefactorSeverity.Info,
                        Description = "No output format specified — consider adding format instructions (JSON, bullet points, etc.).",
                        Offset = 0,
                        Length = 0
                    });
                }
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckDecomposition(string prompt, List<PromptSection> sections, int tokenCount)
        {
            var suggestions = new List<RefactorSuggestion>();

            // Detect multiple distinct tasks in a single prompt
            var taskIndicators = new[] { "first,", "second,", "third,", "then,", "next,", "finally,",
                "step 1", "step 2", "step 3", "task 1", "task 2",
                "1.", "2.", "3.", "4.", "5." };
            var lower = prompt.ToLowerInvariant();

            int taskCount = taskIndicators.Count(t => lower.Contains(t, StringComparison.Ordinal));

            if (taskCount >= 4 && tokenCount > 300)
            {
                suggestions.Add(new RefactorSuggestion
                {
                    Category = RefactorCategory.Decomposition,
                    Severity = RefactorSeverity.Info,
                    Description = $"Prompt contains ~{taskCount} sequential tasks — consider splitting into a prompt chain for better results.",
                    Offset = 0,
                    Length = 0
                });
            }

            // Many sections could indicate a prompt that's trying to do too much
            if (sections.Count >= 6)
            {
                suggestions.Add(new RefactorSuggestion
                {
                    Category = RefactorCategory.Decomposition,
                    Severity = RefactorSeverity.Info,
                    Description = $"Prompt has {sections.Count} sections — consider if some can be separate prompts in a chain.",
                    Offset = 0,
                    Length = 0
                });
            }

            return suggestions;
        }

        private List<RefactorSuggestion> CheckFillerPhrases(string prompt)
        {
            var suggestions = new List<RefactorSuggestion>();
            var lower = prompt.ToLowerInvariant();

            foreach (var filler in FillerPhrases)
            {
                int idx = lower.IndexOf(filler, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    suggestions.Add(new RefactorSuggestion
                    {
                        Category = RefactorCategory.Redundancy,
                        Severity = RefactorSeverity.Info,
                        Description = $"Filler phrase \"{filler}\" can be removed or simplified.",
                        Snippet = prompt.Substring(idx, filler.Length),
                        Suggestion = "",
                        Offset = idx,
                        Length = filler.Length
                    });
                }
            }

            return suggestions;
        }

        // ───────────────────── Helpers ─────────────────────

        private static List<PromptSection> DetectSections(string prompt)
        {
            var sections = new List<PromptSection>();

            // Detect markdown headers, numbered sections, or labeled sections
            var headerPattern = new Regex(
                @"^(#{1,3}\s+.+|[A-Z][A-Za-z\s]{2,30}:\s*$|\d+\.\s+[A-Z].+)",
                RegexOptions.Multiline);

            var matches = headerPattern.Matches(prompt);
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                int end = i + 1 < matches.Count ? matches[i + 1].Index : prompt.Length;
                sections.Add(new PromptSection
                {
                    Title = m.Value.Trim().TrimStart('#').Trim(),
                    Content = prompt[m.Index..end],
                    Offset = m.Index
                });
            }

            // If no sections detected, treat entire prompt as one section
            if (sections.Count == 0)
            {
                sections.Add(new PromptSection
                {
                    Title = "(entire prompt)",
                    Content = prompt,
                    Offset = 0
                });
            }

            return sections;
        }

        private static List<string> DetectVariables(string prompt)
        {
            var pattern = new Regex(@"\{\{(\w+)\}\}");
            return pattern.Matches(prompt)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Rough estimate: ~4 chars per token for English
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        private static string SuggestVariableName(string value)
        {
            // Generate a sensible variable name from a literal value
            var cleaned = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9\s]", "");
            var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "value";
            if (words.Length == 1) return words[0];
            return string.Join("_", words.Take(3));
        }

        private static int CalculateScore(List<RefactorSuggestion> suggestions, int tokens, int sections, int variables)
        {
            int score = 100;

            foreach (var s in suggestions)
            {
                score -= s.Severity switch
                {
                    RefactorSeverity.Error => 15,
                    RefactorSeverity.Warning => 8,
                    RefactorSeverity.Info => 3,
                    _ => 0
                };
            }

            // Bonus for good structure
            if (sections >= 2 && sections <= 5) score += 5;
            if (variables > 0) score += 3;

            return Math.Clamp(score, 0, 100);
        }

        private static PromptGrade ScoreToGrade(int score)
        {
            return score switch
            {
                >= 90 => PromptGrade.A,
                >= 75 => PromptGrade.B,
                >= 60 => PromptGrade.C,
                >= 40 => PromptGrade.D,
                _ => PromptGrade.F
            };
        }
    }

    /// <summary>Represents a detected section within a prompt.</summary>
    internal class PromptSection
    {
        public string Title { get; init; } = "";
        public string Content { get; init; } = "";
        public int Offset { get; init; }
    }

    /// <summary>
    /// Result of comparing two prompts' refactoring reports.
    /// </summary>
    public class RefactorComparison
    {
        /// <summary>Score of the original prompt.</summary>
        [JsonPropertyName("beforeScore")]
        public int BeforeScore { get; init; }

        /// <summary>Score of the modified prompt.</summary>
        [JsonPropertyName("afterScore")]
        public int AfterScore { get; init; }

        /// <summary>Grade of the original prompt.</summary>
        [JsonPropertyName("beforeGrade")]
        public PromptGrade BeforeGrade { get; init; }

        /// <summary>Grade of the modified prompt.</summary>
        [JsonPropertyName("afterGrade")]
        public PromptGrade AfterGrade { get; init; }

        /// <summary>Score change (positive = improvement).</summary>
        [JsonPropertyName("scoreDelta")]
        public int ScoreDelta { get; init; }

        /// <summary>Token count of original prompt.</summary>
        [JsonPropertyName("beforeTokens")]
        public int BeforeTokens { get; init; }

        /// <summary>Token count of modified prompt.</summary>
        [JsonPropertyName("afterTokens")]
        public int AfterTokens { get; init; }

        /// <summary>Issues that were fixed in the new version.</summary>
        [JsonPropertyName("fixedIssues")]
        public List<RefactorSuggestion> FixedIssues { get; init; } = new();

        /// <summary>New issues introduced in the new version.</summary>
        [JsonPropertyName("newIssues")]
        public List<RefactorSuggestion> NewIssues { get; init; } = new();

        /// <summary>Whether the refactoring improved the prompt.</summary>
        [JsonIgnore]
        public bool IsImprovement => ScoreDelta > 0;

        /// <summary>Generates a human-readable comparison summary.</summary>
        public string ToTextReport()
        {
            var lines = new List<string>
            {
                "═══════════════════════════════════════════",
                "  REFACTORING COMPARISON",
                "═══════════════════════════════════════════",
                "",
                $"  Before: {BeforeScore}/100 ({BeforeGrade}) — ~{BeforeTokens} tokens",
                $"  After:  {AfterScore}/100 ({AfterGrade}) — ~{AfterTokens} tokens",
                $"  Delta:  {(ScoreDelta >= 0 ? "+" : "")}{ScoreDelta} points  {(IsImprovement ? "✅ Improved" : ScoreDelta == 0 ? "➡️ No change" : "⚠️ Regressed")}",
                ""
            };

            if (FixedIssues.Count > 0)
            {
                lines.Add($"  Fixed ({FixedIssues.Count}):");
                foreach (var f in FixedIssues)
                    lines.Add($"    ✅ {f.Description}");
                lines.Add("");
            }

            if (NewIssues.Count > 0)
            {
                lines.Add($"  New Issues ({NewIssues.Count}):");
                foreach (var n in NewIssues)
                    lines.Add($"    ⚠️ {n.Description}");
                lines.Add("");
            }

            lines.Add("═══════════════════════════════════════════");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
