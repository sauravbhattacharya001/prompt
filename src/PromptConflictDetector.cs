namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Severity of a detected conflict.
    /// </summary>
    public enum ConflictSeverity
    {
        /// <summary>Potentially contradictory — worth reviewing.</summary>
        Low,
        /// <summary>Likely contradictory instructions.</summary>
        Medium,
        /// <summary>Directly contradictory instructions.</summary>
        High
    }

    /// <summary>
    /// Type of conflict detected between prompt instructions.
    /// </summary>
    public enum ConflictType
    {
        /// <summary>Instructions that directly negate each other (e.g., "be verbose" vs "be concise").</summary>
        DirectContradiction,
        /// <summary>Instructions that set incompatible constraints (e.g., "max 100 words" + "include detailed examples").</summary>
        IncompatibleConstraints,
        /// <summary>Same topic addressed with different guidance in multiple places.</summary>
        DuplicateGuidance,
        /// <summary>Numeric limits that overlap or conflict (e.g., "max 50 tokens" vs "at least 200 tokens").</summary>
        NumericConflict,
        /// <summary>Role or persona assignments that clash.</summary>
        RoleConflict,
        /// <summary>Tone/style directives that contradict each other.</summary>
        ToneConflict
    }

    /// <summary>
    /// Represents a single detected conflict between prompt instructions.
    /// </summary>
    public class PromptConflict
    {
        /// <summary>Gets or sets the type of conflict.</summary>
        [JsonPropertyName("type")]
        public ConflictType Type { get; set; }

        /// <summary>Gets or sets the severity of the conflict.</summary>
        [JsonPropertyName("severity")]
        public ConflictSeverity Severity { get; set; }

        /// <summary>Gets or sets the first conflicting instruction text.</summary>
        [JsonPropertyName("instructionA")]
        public string InstructionA { get; set; } = string.Empty;

        /// <summary>Gets or sets the second conflicting instruction text.</summary>
        [JsonPropertyName("instructionB")]
        public string InstructionB { get; set; } = string.Empty;

        /// <summary>Gets or sets the source label for instruction A (e.g., prompt name or "line 5").</summary>
        [JsonPropertyName("sourceA")]
        public string SourceA { get; set; } = string.Empty;

        /// <summary>Gets or sets the source label for instruction B.</summary>
        [JsonPropertyName("sourceB")]
        public string SourceB { get; set; } = string.Empty;

        /// <summary>Gets or sets a human-readable explanation of the conflict.</summary>
        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional suggested resolution.</summary>
        [JsonPropertyName("suggestion")]
        public string? Suggestion { get; set; }
    }

    /// <summary>
    /// Result of a conflict detection scan.
    /// </summary>
    public class ConflictReport
    {
        /// <summary>Gets or sets the list of detected conflicts.</summary>
        [JsonPropertyName("conflicts")]
        public List<PromptConflict> Conflicts { get; set; } = new();

        /// <summary>Gets or sets the prompts that were analyzed.</summary>
        [JsonPropertyName("sourcesAnalyzed")]
        public List<string> SourcesAnalyzed { get; set; } = new();

        /// <summary>Gets or sets when the analysis was performed.</summary>
        [JsonPropertyName("analyzedAt")]
        public DateTimeOffset AnalyzedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Gets the total number of conflicts found.</summary>
        [JsonIgnore]
        public int TotalConflicts => Conflicts.Count;

        /// <summary>Gets the number of high-severity conflicts.</summary>
        [JsonIgnore]
        public int HighSeverityCount => Conflicts.Count(c => c.Severity == ConflictSeverity.High);

        /// <summary>Gets whether any conflicts were detected.</summary>
        [JsonIgnore]
        public bool HasConflicts => Conflicts.Count > 0;

        /// <summary>
        /// Returns a summary string of the conflict report.
        /// </summary>
        public string ToSummary()
        {
            if (!HasConflicts)
                return $"No conflicts detected across {SourcesAnalyzed.Count} source(s).";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Found {TotalConflicts} conflict(s) across {SourcesAnalyzed.Count} source(s):");
            sb.AppendLine($"  High: {Conflicts.Count(c => c.Severity == ConflictSeverity.High)}");
            sb.AppendLine($"  Medium: {Conflicts.Count(c => c.Severity == ConflictSeverity.Medium)}");
            sb.AppendLine($"  Low: {Conflicts.Count(c => c.Severity == ConflictSeverity.Low)}");
            sb.AppendLine();

            foreach (var conflict in Conflicts.OrderByDescending(c => c.Severity))
            {
                sb.AppendLine($"[{conflict.Severity}] {conflict.Type}");
                sb.AppendLine($"  A ({conflict.SourceA}): {StringHelpers.Truncate(conflict.InstructionA, 80)}");
                sb.AppendLine($"  B ({conflict.SourceB}): {StringHelpers.Truncate(conflict.InstructionB, 80)}");
                sb.AppendLine($"  → {conflict.Explanation}");
                if (conflict.Suggestion != null)
                    sb.AppendLine($"  💡 {conflict.Suggestion}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>Serializes the report to JSON.</summary>
        public string ToJson() =>
            JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });

    }

    /// <summary>
    /// Detects contradictions and conflicts between prompt instructions.
    /// Analyzes single prompts for internal contradictions or multiple prompts for cross-prompt conflicts.
    /// </summary>
    /// <example>
    /// <code>
    /// var detector = new PromptConflictDetector();
    /// // Single prompt analysis
    /// var report = detector.Analyze("Be concise. Always provide detailed, lengthy explanations.");
    ///
    /// // Multi-prompt analysis
    /// var report2 = detector.AnalyzeMultiple(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["system"] = "You are a formal assistant. Never use casual language.",
    ///     ["user-preamble"] = "Be friendly and casual in your responses."
    /// });
    /// </code>
    /// </example>
    public class PromptConflictDetector
    {
        // Antonym pairs that signal direct contradictions
        private static readonly (string, string)[] AntonymPairs = new[]
        {
            ("concise", "verbose"), ("concise", "detailed"), ("concise", "lengthy"),
            ("brief", "verbose"), ("brief", "detailed"), ("brief", "lengthy"),
            ("short", "long"), ("short", "detailed"), ("short", "lengthy"),
            ("formal", "casual"), ("formal", "informal"), ("formal", "relaxed"),
            ("professional", "casual"), ("professional", "informal"),
            ("serious", "humorous"), ("serious", "funny"), ("serious", "playful"),
            ("technical", "simple"), ("technical", "layman"),
            ("always", "never"),
            ("include", "exclude"), ("include", "omit"), ("include", "skip"),
            ("allow", "deny"), ("allow", "forbid"), ("allow", "prohibit"),
            ("encourage", "discourage"), ("encourage", "prohibit"),
            ("positive", "negative"), ("optimistic", "pessimistic"),
            ("creative", "strict"), ("creative", "rigid"),
            ("friendly", "cold"), ("friendly", "distant"),
            ("assertive", "passive"), ("assertive", "meek"),
            ("direct", "indirect"), ("direct", "vague"),
            ("specific", "vague"), ("specific", "general"),
            ("objective", "subjective"), ("objective", "biased"),
            ("conservative", "aggressive"), ("conservative", "bold"),
            ("minimal", "comprehensive"), ("minimal", "exhaustive"),
            ("succinct", "elaborate"), ("succinct", "verbose"),
        };

        // Patterns that extract numeric constraints
        private static readonly Regex NumericConstraintPattern = new(
            @"(?:max(?:imum)?|min(?:imum)?|at\s+(?:least|most)|no\s+more\s+than|no\s+fewer\s+than|under|over|exactly|limit(?:ed)?\s+to|up\s+to)\s+(\d+)\s*(word|token|sentence|paragraph|character|line|item|point|step|example)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Role/persona assignment pattern
        private static readonly Regex RolePattern = new(
            @"(?:you\s+are|act\s+as|behave\s+as|pretend\s+to\s+be|role\s*:\s*|persona\s*:\s*)\s*(?:a\s+|an\s+)?(.+?)(?:\.|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);

        // Tone directives
        private static readonly Regex TonePattern = new(
            @"(?:tone|style|voice|manner)\s*(?::|should\s+be|is|must\s+be)\s*(.+?)(?:\.|,|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Analyzes a single prompt for internal contradictions.
        /// </summary>
        /// <param name="promptText">The prompt text to analyze.</param>
        /// <param name="sourceName">Optional label for the source.</param>
        /// <returns>A <see cref="ConflictReport"/> with any detected conflicts.</returns>
        public ConflictReport Analyze(string promptText, string sourceName = "prompt")
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return new ConflictReport { SourcesAnalyzed = { sourceName } };

            return AnalyzeMultiple(new Dictionary<string, string> { [sourceName] = promptText });
        }

        /// <summary>
        /// Analyzes multiple prompts for cross-prompt conflicts and internal contradictions.
        /// </summary>
        /// <param name="prompts">Dictionary of source-name → prompt-text.</param>
        /// <returns>A <see cref="ConflictReport"/> with all detected conflicts.</returns>
        public ConflictReport AnalyzeMultiple(Dictionary<string, string> prompts)
        {
            var report = new ConflictReport
            {
                SourcesAnalyzed = prompts.Keys.ToList()
            };

            // Extract instructions from each prompt
            var allInstructions = new List<(string source, string instruction, string fullText)>();
            foreach (var (name, text) in prompts)
            {
                var instructions = ExtractInstructions(text);
                foreach (var inst in instructions)
                    allInstructions.Add((name, inst, text));
            }

            // Check all pairs for conflicts
            for (int i = 0; i < allInstructions.Count; i++)
            {
                for (int j = i + 1; j < allInstructions.Count; j++)
                {
                    var a = allInstructions[i];
                    var b = allInstructions[j];

                    CheckAntonymConflict(a.source, a.instruction, b.source, b.instruction, report);
                    CheckNumericConflict(a.source, a.instruction, b.source, b.instruction, report);
                }
            }

            // Check role conflicts across sources
            CheckRoleConflicts(prompts, report);

            // Check tone conflicts across sources
            CheckToneConflicts(prompts, report);

            // Deduplicate
            report.Conflicts = report.Conflicts
                .GroupBy(c => $"{c.Type}|{c.InstructionA}|{c.InstructionB}")
                .Select(g => g.First())
                .ToList();

            return report;
        }

        /// <summary>
        /// Extracts individual instruction sentences from prompt text.
        /// </summary>
        private static List<string> ExtractInstructions(string text)
        {
            var results = new List<string>();

            // Split on sentence boundaries and bullet points
            var parts = Regex.Split(text, @"(?<=[.!?])\s+|[\r\n]+\s*[-•*]\s*|[\r\n]+\s*\d+[.)]\s*");

            foreach (var part in parts)
            {
                var trimmed = part.Trim().TrimStart('-', '•', '*', ' ');
                if (trimmed.Length > 5) // Skip very short fragments
                    results.Add(trimmed);
            }

            return results;
        }

        /// <summary>
        /// Checks if two instructions contain antonym pairs suggesting a contradiction.
        /// </summary>
        private static void CheckAntonymConflict(
            string srcA, string instA, string srcB, string instB, ConflictReport report)
        {
            var wordsA = NormalizeWords(instA);
            var wordsB = NormalizeWords(instB);

            foreach (var (wordX, wordY) in AntonymPairs)
            {
                bool aHasX = wordsA.Contains(wordX);
                bool aHasY = wordsA.Contains(wordY);
                bool bHasX = wordsB.Contains(wordX);
                bool bHasY = wordsB.Contains(wordY);

                if ((aHasX && bHasY) || (aHasY && bHasX))
                {
                    var matchedA = aHasX ? wordX : wordY;
                    var matchedB = bHasX ? wordX : wordY;

                    report.Conflicts.Add(new PromptConflict
                    {
                        Type = ConflictType.DirectContradiction,
                        Severity = srcA == srcB ? ConflictSeverity.High : ConflictSeverity.High,
                        InstructionA = instA,
                        InstructionB = instB,
                        SourceA = srcA,
                        SourceB = srcB,
                        Explanation = $"'{matchedA}' in [{srcA}] contradicts '{matchedB}' in [{srcB}].",
                        Suggestion = $"Reconcile the '{matchedA}' vs '{matchedB}' directives — pick one or scope them to different contexts."
                    });
                    return; // One conflict per pair is enough
                }
            }
        }

        /// <summary>
        /// Checks if two instructions contain conflicting numeric constraints.
        /// </summary>
        private static void CheckNumericConflict(
            string srcA, string instA, string srcB, string instB, ConflictReport report)
        {
            var constraintsA = ExtractNumericConstraints(instA);
            var constraintsB = ExtractNumericConstraints(instB);

            foreach (var ca in constraintsA)
            {
                foreach (var cb in constraintsB)
                {
                    if (!ca.unit.Equals(cb.unit, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Check for direct conflicts: max < min, or two different exact values
                    bool conflict = false;
                    string explanation = "";

                    if (ca.isMax && cb.isMin && ca.value < cb.value)
                    {
                        conflict = true;
                        explanation = $"Maximum {ca.value} {ca.unit}(s) conflicts with minimum {cb.value} {cb.unit}(s).";
                    }
                    else if (ca.isMin && cb.isMax && ca.value > cb.value)
                    {
                        conflict = true;
                        explanation = $"Minimum {ca.value} {ca.unit}(s) conflicts with maximum {cb.value} {cb.unit}(s).";
                    }
                    else if (ca.isExact && cb.isExact && ca.value != cb.value)
                    {
                        conflict = true;
                        explanation = $"Conflicting exact values: {ca.value} vs {cb.value} {ca.unit}(s).";
                    }

                    if (conflict)
                    {
                        report.Conflicts.Add(new PromptConflict
                        {
                            Type = ConflictType.NumericConflict,
                            Severity = ConflictSeverity.High,
                            InstructionA = instA,
                            InstructionB = instB,
                            SourceA = srcA,
                            SourceB = srcB,
                            Explanation = explanation,
                            Suggestion = "Align the numeric limits so they don't create an impossible range."
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Checks for conflicting role/persona assignments across prompts.
        /// </summary>
        private static void CheckRoleConflicts(Dictionary<string, string> prompts, ConflictReport report)
        {
            var roles = new List<(string source, string role, string instruction)>();

            foreach (var (name, text) in prompts)
            {
                foreach (Match m in RolePattern.Matches(text))
                    roles.Add((name, m.Groups[1].Value.Trim(), m.Value.Trim()));
            }

            for (int i = 0; i < roles.Count; i++)
            {
                for (int j = i + 1; j < roles.Count; j++)
                {
                    var a = roles[i];
                    var b = roles[j];

                    // Different roles assigned
                    if (!a.role.Equals(b.role, StringComparison.OrdinalIgnoreCase))
                    {
                        report.Conflicts.Add(new PromptConflict
                        {
                            Type = ConflictType.RoleConflict,
                            Severity = a.source == b.source ? ConflictSeverity.High : ConflictSeverity.Medium,
                            InstructionA = a.instruction,
                            InstructionB = b.instruction,
                            SourceA = a.source,
                            SourceB = b.source,
                            Explanation = $"Different roles assigned: '{a.role}' vs '{b.role}'.",
                            Suggestion = "Use a single, consistent role definition or clearly scope each role to a different phase."
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Checks for conflicting tone/style directives across prompts.
        /// </summary>
        private static void CheckToneConflicts(Dictionary<string, string> prompts, ConflictReport report)
        {
            var tones = new List<(string source, string tone, string instruction)>();

            foreach (var (name, text) in prompts)
            {
                foreach (Match m in TonePattern.Matches(text))
                    tones.Add((name, m.Groups[1].Value.Trim().ToLowerInvariant(), m.Value.Trim()));
            }

            for (int i = 0; i < tones.Count; i++)
            {
                for (int j = i + 1; j < tones.Count; j++)
                {
                    var a = tones[i];
                    var b = tones[j];

                    if (!a.tone.Equals(b.tone, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if the tones are actually contradictory via antonym pairs
                        var aWords = new HashSet<string>(a.tone.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        var bWords = new HashSet<string>(b.tone.Split(' ', StringSplitOptions.RemoveEmptyEntries));

                        bool isContradictory = AntonymPairs.Any(pair =>
                            (aWords.Contains(pair.Item1) && bWords.Contains(pair.Item2)) ||
                            (aWords.Contains(pair.Item2) && bWords.Contains(pair.Item1)));

                        if (isContradictory)
                        {
                            report.Conflicts.Add(new PromptConflict
                            {
                                Type = ConflictType.ToneConflict,
                                Severity = ConflictSeverity.Medium,
                                InstructionA = a.instruction,
                                InstructionB = b.instruction,
                                SourceA = a.source,
                                SourceB = b.source,
                                Explanation = $"Conflicting tone directives: '{a.tone}' vs '{b.tone}'.",
                                Suggestion = "Unify tone guidance into one authoritative directive."
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts numeric constraints from an instruction string.
        /// </summary>
        private static List<(int value, string unit, bool isMax, bool isMin, bool isExact)> ExtractNumericConstraints(string text)
        {
            var results = new List<(int, string, bool, bool, bool)>();

            foreach (Match m in NumericConstraintPattern.Matches(text))
            {
                if (int.TryParse(m.Groups[1].Value, out int val))
                {
                    var prefix = m.Value.ToLowerInvariant();
                    var unit = m.Groups[2].Value.ToLowerInvariant();

                    bool isMax = prefix.Contains("max") || prefix.Contains("at most") ||
                                 prefix.Contains("no more") || prefix.Contains("under") ||
                                 prefix.Contains("limit") || prefix.Contains("up to");
                    bool isMin = prefix.Contains("min") || prefix.Contains("at least") ||
                                 prefix.Contains("no fewer") || prefix.Contains("over");
                    bool isExact = prefix.Contains("exactly");

                    results.Add((val, unit, isMax, isMin, isExact));
                }
            }

            return results;
        }

        /// <summary>
        /// Normalizes text to a set of lowercase words.
        /// </summary>
        private static HashSet<string> NormalizeWords(string text) =>
            new(Regex.Split(text.ToLowerInvariant(), @"\W+")
                .Where(w => w.Length > 0), StringComparer.OrdinalIgnoreCase);
    }
}