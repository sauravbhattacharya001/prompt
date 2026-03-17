namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Severity level for a health-check finding.
    /// </summary>
    public enum HealthSeverity
    {
        /// <summary>Informational — not a problem, just noteworthy.</summary>
        Info,

        /// <summary>Warning — something that should probably be fixed.</summary>
        Warning,

        /// <summary>Error — a real problem that could cause runtime failures.</summary>
        Error
    }

    /// <summary>
    /// A single health-check finding for a prompt entry or the library overall.
    /// </summary>
    public class HealthFinding
    {
        /// <summary>Gets the severity level.</summary>
        public HealthSeverity Severity { get; }

        /// <summary>Gets the rule code (e.g., "MISSING_DESCRIPTION", "LONG_TEMPLATE").</summary>
        public string RuleCode { get; }

        /// <summary>Gets the human-readable message.</summary>
        public string Message { get; }

        /// <summary>Gets the entry name this finding applies to, or null for library-level findings.</summary>
        public string? EntryName { get; }

        /// <summary>
        /// Creates a new health finding.
        /// </summary>
        public HealthFinding(HealthSeverity severity, string ruleCode, string message, string? entryName = null)
        {
            Severity = severity;
            RuleCode = ruleCode ?? throw new ArgumentNullException(nameof(ruleCode));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            EntryName = entryName;
        }

        /// <inheritdoc />
        public override string ToString() =>
            EntryName != null
                ? $"[{Severity}] {EntryName}: {RuleCode} — {Message}"
                : $"[{Severity}] {RuleCode} — {Message}";
    }

    /// <summary>
    /// Summary report from a health check run.
    /// </summary>
    public class HealthReport
    {
        /// <summary>Gets all findings.</summary>
        public IReadOnlyList<HealthFinding> Findings { get; }

        /// <summary>Gets the total number of entries checked.</summary>
        public int EntriesChecked { get; }

        /// <summary>Gets when the check was performed.</summary>
        public DateTimeOffset CheckedAt { get; }

        /// <summary>Gets a 0–100 health score (100 = no issues).</summary>
        public int Score { get; }

        internal HealthReport(IReadOnlyList<HealthFinding> findings, int entriesChecked, int score)
        {
            Findings = findings;
            EntriesChecked = entriesChecked;
            CheckedAt = DateTimeOffset.UtcNow;
            Score = Math.Clamp(score, 0, 100);
        }

        /// <summary>Gets findings filtered by severity.</summary>
        public IReadOnlyList<HealthFinding> GetBySeverity(HealthSeverity severity) =>
            Findings.Where(f => f.Severity == severity).ToList().AsReadOnly();

        /// <summary>Gets findings for a specific entry.</summary>
        public IReadOnlyList<HealthFinding> GetByEntry(string entryName) =>
            Findings.Where(f => f.EntryName == entryName).ToList().AsReadOnly();

        /// <summary>Gets error count.</summary>
        public int ErrorCount => Findings.Count(f => f.Severity == HealthSeverity.Error);

        /// <summary>Gets warning count.</summary>
        public int WarningCount => Findings.Count(f => f.Severity == HealthSeverity.Warning);

        /// <summary>Gets info count.</summary>
        public int InfoCount => Findings.Count(f => f.Severity == HealthSeverity.Info);

        /// <summary>Whether the library passed with no errors or warnings.</summary>
        public bool IsHealthy => ErrorCount == 0 && WarningCount == 0;

        /// <summary>
        /// Returns a formatted summary string.
        /// </summary>
        public string ToSummary()
        {
            var lines = new List<string>
            {
                $"Prompt Library Health Report",
                $"===========================",
                $"Checked: {EntriesChecked} entries at {CheckedAt:u}",
                $"Score:   {Score}/100 {(IsHealthy ? "✓ Healthy" : "⚠ Issues found")}",
                $"",
                $"  Errors:   {ErrorCount}",
                $"  Warnings: {WarningCount}",
                $"  Info:     {InfoCount}",
                $""
            };

            if (Findings.Count > 0)
            {
                lines.Add("Findings:");
                lines.Add("---------");
                foreach (var f in Findings.OrderByDescending(f => f.Severity))
                    lines.Add($"  {f}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Serializes the report to JSON.
        /// </summary>
        public string ToJson()
        {
            var obj = new
            {
                checkedAt = CheckedAt,
                entriesChecked = EntriesChecked,
                score = Score,
                isHealthy = IsHealthy,
                counts = new { errors = ErrorCount, warnings = WarningCount, info = InfoCount },
                findings = Findings.Select(f => new
                {
                    severity = f.Severity.ToString().ToLowerInvariant(),
                    ruleCode = f.RuleCode,
                    message = f.Message,
                    entryName = f.EntryName
                })
            };
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Configuration for prompt health checks.
    /// </summary>
    public class HealthCheckOptions
    {
        /// <summary>Maximum template length in characters before warning. Default: 4000.</summary>
        public int MaxTemplateLength { get; set; } = 4000;

        /// <summary>Maximum template length before error. Default: 8000.</summary>
        public int MaxTemplateLengthError { get; set; } = 8000;

        /// <summary>Maximum number of variables in a single template before warning. Default: 10.</summary>
        public int MaxVariables { get; set; } = 10;

        /// <summary>Whether to check for duplicate template text across entries. Default: true.</summary>
        public bool CheckDuplicates { get; set; } = true;

        /// <summary>Whether to check for missing descriptions. Default: true.</summary>
        public bool CheckDescriptions { get; set; } = true;

        /// <summary>Whether to check for missing categories. Default: true.</summary>
        public bool CheckCategories { get; set; } = true;

        /// <summary>Whether to check for empty tags. Default: true.</summary>
        public bool CheckTags { get; set; } = true;

        /// <summary>Minimum similarity ratio (0–1) to flag near-duplicate templates. Default: 0.85.</summary>
        public double NearDuplicateThreshold { get; set; } = 0.85;
    }

    /// <summary>
    /// Analyzes a <see cref="PromptLibrary"/> for common issues: missing metadata,
    /// overly long prompts, duplicate templates, unused variable patterns, and more.
    /// </summary>
    /// <remarks>
    /// <para>Usage:</para>
    /// <code>
    /// var checker = new PromptHealthCheck();
    /// var report = checker.Check(library);
    /// Console.WriteLine(report.ToSummary());
    ///
    /// // With custom options:
    /// var opts = new HealthCheckOptions { MaxTemplateLength = 2000 };
    /// var report2 = checker.Check(library, opts);
    /// </code>
    /// </remarks>
    public class PromptHealthCheck
    {
        private static readonly Regex VariablePattern =
            new Regex(@"\{\{(\w+)\}\}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Runs all health checks against the given library.
        /// </summary>
        /// <param name="library">The prompt library to check.</param>
        /// <param name="options">Optional configuration. Uses defaults if null.</param>
        /// <returns>A health report with findings and overall score.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="library"/> is null.</exception>
        public HealthReport Check(PromptLibrary library, HealthCheckOptions? options = null)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            options ??= new HealthCheckOptions();

            var findings = new List<HealthFinding>();
            var entries = library.Entries.ToList();

            // Library-level checks
            if (entries.Count == 0)
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Info, "EMPTY_LIBRARY",
                    "Library contains no entries."));
            }

            // Per-entry checks
            foreach (var entry in entries)
            {
                CheckMetadata(entry, options, findings);
                CheckTemplateLength(entry, options, findings);
                CheckVariables(entry, options, findings);
                CheckTemplateQuality(entry, findings);
            }

            // Cross-entry checks
            if (options.CheckDuplicates && entries.Count > 1)
            {
                CheckDuplicates(entries, options, findings);
            }

            CheckCategoryDistribution(entries, findings);

            // Score: start at 100, deduct per finding
            int score = 100;
            foreach (var f in findings)
            {
                score -= f.Severity switch
                {
                    HealthSeverity.Error => 10,
                    HealthSeverity.Warning => 3,
                    _ => 0
                };
            }

            return new HealthReport(findings.AsReadOnly(), entries.Count, score);
        }

        private void CheckMetadata(PromptEntry entry, HealthCheckOptions options, List<HealthFinding> findings)
        {
            if (options.CheckDescriptions && string.IsNullOrWhiteSpace(entry.Description))
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Warning, "MISSING_DESCRIPTION",
                    "Entry has no description. Descriptions help users discover and understand prompts.",
                    entry.Name));
            }

            if (options.CheckCategories && string.IsNullOrWhiteSpace(entry.Category))
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Info, "MISSING_CATEGORY",
                    "Entry has no category. Categories help organize large libraries.",
                    entry.Name));
            }

            if (options.CheckTags && (entry.Tags == null || !entry.Tags.Any()))
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Info, "NO_TAGS",
                    "Entry has no tags. Tags improve searchability.",
                    entry.Name));
            }
        }

        private void CheckTemplateLength(PromptEntry entry, HealthCheckOptions options, List<HealthFinding> findings)
        {
            var text = entry.Template.Template;
            if (text.Length > options.MaxTemplateLengthError)
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Error, "TEMPLATE_TOO_LONG",
                    $"Template is {text.Length} characters (limit: {options.MaxTemplateLengthError}). Very long prompts increase cost and may hit token limits.",
                    entry.Name));
            }
            else if (text.Length > options.MaxTemplateLength)
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Warning, "LONG_TEMPLATE",
                    $"Template is {text.Length} characters (recommended max: {options.MaxTemplateLength}). Consider splitting into a chain.",
                    entry.Name));
            }
        }

        private void CheckVariables(PromptEntry entry, HealthCheckOptions options, List<HealthFinding> findings)
        {
            var text = entry.Template.Template;
            var matches = VariablePattern.Matches(text);
            var variables = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

            if (variables.Count > options.MaxVariables)
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Warning, "TOO_MANY_VARIABLES",
                    $"Template has {variables.Count} variables (recommended max: {options.MaxVariables}). Complex templates are harder to maintain.",
                    entry.Name));
            }

            // Check for variables that look like typos (single char, numeric-only)
            foreach (var v in variables)
            {
                if (v.Length == 1)
                {
                    findings.Add(new HealthFinding(
                        HealthSeverity.Warning, "SHORT_VARIABLE_NAME",
                        $"Variable '{{{{{v}}}}}' is a single character. Use descriptive names.",
                        entry.Name));
                }
            }

            // Check for unbalanced braces (potential typos)
            int openCount = 0;
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '{' && text[i + 1] == '{') { openCount++; i++; }
                if (text[i] == '}' && text[i + 1] == '}') { openCount--; i++; }
            }
            if (openCount != 0)
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Error, "UNBALANCED_BRACES",
                    "Template has unbalanced {{ }} braces. This may cause rendering errors.",
                    entry.Name));
            }
        }

        private void CheckTemplateQuality(PromptEntry entry, List<HealthFinding> findings)
        {
            var text = entry.Template.Template;

            // Empty or near-empty template
            if (string.IsNullOrWhiteSpace(text))
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Error, "EMPTY_TEMPLATE",
                    "Template is empty or whitespace-only.",
                    entry.Name));
                return;
            }

            // Check for common prompt anti-patterns
            if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("FIXME", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Warning, "CONTAINS_TODO",
                    "Template contains TODO/FIXME markers. This prompt may be incomplete.",
                    entry.Name));
            }

            // Check for hardcoded model references
            var modelPatterns = new[] { "gpt-4", "gpt-3.5", "claude", "gemini", "llama" };
            foreach (var pattern in modelPatterns)
            {
                if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new HealthFinding(
                        HealthSeverity.Info, "HARDCODED_MODEL_REF",
                        $"Template references model '{pattern}'. Consider using a variable for model-agnostic prompts.",
                        entry.Name));
                    break;
                }
            }

            // Check if template has no instructions (very short non-variable text)
            var withoutVars = VariablePattern.Replace(text, "").Trim();
            if (withoutVars.Length < 10 && text.Length > 0)
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Warning, "MINIMAL_INSTRUCTIONS",
                    "Template is mostly variables with very little instruction text. Add context for better results.",
                    entry.Name));
            }
        }

        private void CheckDuplicates(List<PromptEntry> entries, HealthCheckOptions options, List<HealthFinding> findings)
        {
            var seen = new Dictionary<string, string>(); // normalized text -> first entry name
            var reported = new HashSet<string>();

            foreach (var entry in entries)
            {
                var normalized = NormalizeForComparison(entry.Template.Template);

                if (seen.TryGetValue(normalized, out var firstName))
                {
                    var key = $"{firstName}+{entry.Name}";
                    if (reported.Add(key))
                    {
                        findings.Add(new HealthFinding(
                            HealthSeverity.Warning, "DUPLICATE_TEMPLATE",
                            $"Template is identical to '{firstName}' (after normalization). Consider removing the duplicate.",
                            entry.Name));
                    }
                }
                else
                {
                    seen[normalized] = entry.Name;
                }
            }

            // Near-duplicate check (simple Jaccard on word sets)
            if (options.NearDuplicateThreshold < 1.0 && entries.Count <= 100)
            {
                var wordSets = entries
                    .Select(e => (e.Name, Words: GetWordSet(e.Template.Template)))
                    .ToList();

                for (int i = 0; i < wordSets.Count; i++)
                {
                    for (int j = i + 1; j < wordSets.Count; j++)
                    {
                        var similarity = JaccardSimilarity(wordSets[i].Words, wordSets[j].Words);
                        if (similarity >= options.NearDuplicateThreshold)
                        {
                            var key = $"{wordSets[i].Name}~{wordSets[j].Name}";
                            if (reported.Add(key))
                            {
                                findings.Add(new HealthFinding(
                                    HealthSeverity.Info, "NEAR_DUPLICATE",
                                    $"Template is {similarity:P0} similar to '{wordSets[i].Name}'. Consider consolidating.",
                                    wordSets[j].Name));
                            }
                        }
                    }
                }
            }
        }

        private void CheckCategoryDistribution(List<PromptEntry> entries, List<HealthFinding> findings)
        {
            if (entries.Count < 5) return;

            var uncategorized = entries.Count(e => string.IsNullOrWhiteSpace(e.Category));
            if (uncategorized > entries.Count / 2)
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Warning, "MOSTLY_UNCATEGORIZED",
                    $"{uncategorized} of {entries.Count} entries have no category. Organize with categories for better discoverability."));
            }

            var byCategory = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Category))
                .GroupBy(e => e.Category!)
                .ToList();

            foreach (var group in byCategory.Where(g => g.Count() > 20))
            {
                findings.Add(new HealthFinding(
                    HealthSeverity.Info, "LARGE_CATEGORY",
                    $"Category '{group.Key}' has {group.Count()} entries. Consider sub-categorizing."));
            }
        }

        private static string NormalizeForComparison(string text) =>
            Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");

        private static HashSet<string> GetWordSet(string text) =>
            new HashSet<string>(
                Regex.Split(text.ToLowerInvariant(), @"\W+")
                    .Where(w => w.Length > 2),
                StringComparer.Ordinal);

        private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 && b.Count == 0) return 1.0;
            var intersection = a.Count(x => b.Contains(x));
            var union = a.Count + b.Count - intersection;
            return union == 0 ? 1.0 : (double)intersection / union;
        }
    }
}
