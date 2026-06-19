namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Merges multiple <see cref="PromptTemplate"/> instances into a single
    /// combined template. Useful for composing complex prompts from reusable
    /// fragments — e.g., combining a persona template, a task template, and
    /// a format template into one cohesive prompt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The merger concatenates template bodies with configurable separators
    /// and intelligently combines default variable values (later templates
    /// can override earlier defaults).
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var persona = new PromptTemplate("You are a {{role}} assistant.");
    /// var task = new PromptTemplate("Help the user with {{topic}}.");
    /// var format = new PromptTemplate("Respond in {{format}} format.");
    ///
    /// var merged = PromptMerger.Create()
    ///     .Add(persona)
    ///     .Add(task)
    ///     .Add(format)
    ///     .WithSeparator("\n\n")
    ///     .WithDefaults(new Dictionary&lt;string, string&gt;
    ///     {
    ///         ["role"] = "helpful",
    ///         ["format"] = "markdown"
    ///     })
    ///     .Merge();
    ///
    /// // Result template: "You are a {{role}} assistant.\n\nHelp the user with {{topic}}.\n\nRespond in {{format}} format."
    /// // With defaults: role=helpful, format=markdown
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptMerger
    {
        /// <summary>Maximum number of templates that can be merged at once.</summary>
        public const int MaxTemplates = 50;

        /// <summary>Maximum total character length of all template bodies combined.</summary>
        public const int MaxTotalLength = 500_000;

        private readonly List<MergeEntry> _entries = new();
        private string _separator = "\n\n";
        private readonly Dictionary<string, string> _globalDefaults = new();
        private ConflictResolution _conflictMode = ConflictResolution.LastWins;
        private string? _prefix;
        private string? _suffix;

        private PromptMerger() { }

        /// <summary>
        /// Creates a new <see cref="PromptMerger"/> instance.
        /// </summary>
        public static PromptMerger Create() => new();

        /// <summary>
        /// Adds a template to the merge list.
        /// </summary>
        /// <param name="template">The template to add.</param>
        /// <param name="label">Optional label for this section (added as a comment/header).</param>
        /// <returns>This merger for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when template is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when max template count is exceeded.</exception>
        public PromptMerger Add(PromptTemplate template, string? label = null)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (_entries.Count >= MaxTemplates)
                throw new InvalidOperationException($"Cannot merge more than {MaxTemplates} templates.");

            _entries.Add(new MergeEntry(template, label));
            return this;
        }

        /// <summary>
        /// Adds a raw text section (not a template) to the merge list.
        /// Useful for inserting static separators, headers, or instructions.
        /// </summary>
        /// <param name="text">The raw text to insert.</param>
        /// <param name="label">Optional label for this section.</param>
        /// <returns>This merger for fluent chaining.</returns>
        public PromptMerger AddText(string text, string? label = null)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Text cannot be null or empty.", nameof(text));
            if (_entries.Count >= MaxTemplates)
                throw new InvalidOperationException($"Cannot merge more than {MaxTemplates} entries.");

            _entries.Add(new MergeEntry(text, label));
            return this;
        }

        /// <summary>
        /// Sets the separator used between merged template sections.
        /// Default is "\n\n" (double newline).
        /// </summary>
        public PromptMerger WithSeparator(string separator)
        {
            _separator = separator ?? throw new ArgumentNullException(nameof(separator));
            return this;
        }

        /// <summary>
        /// Adds global default values that apply to all merged templates.
        /// These take lowest priority — template-level defaults override them.
        /// </summary>
        public PromptMerger WithDefaults(Dictionary<string, string> defaults)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            foreach (var kv in defaults)
                _globalDefaults[kv.Key] = kv.Value;
            return this;
        }

        /// <summary>
        /// Sets how conflicting default values are resolved when multiple
        /// templates define the same variable with different defaults.
        /// </summary>
        public PromptMerger WithConflictResolution(ConflictResolution mode)
        {
            _conflictMode = mode;
            return this;
        }

        /// <summary>
        /// Adds a prefix that will be prepended before all merged content.
        /// </summary>
        public PromptMerger WithPrefix(string prefix)
        {
            _prefix = prefix;
            return this;
        }

        /// <summary>
        /// Adds a suffix that will be appended after all merged content.
        /// </summary>
        public PromptMerger WithSuffix(string suffix)
        {
            _suffix = suffix;
            return this;
        }

        /// <summary>
        /// Merges all added templates into a single <see cref="PromptTemplate"/>.
        /// </summary>
        /// <returns>A new template combining all entries with merged defaults.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no templates have been added or total length exceeds limit.
        /// </exception>
        public PromptTemplate Merge()
        {
            if (_entries.Count == 0)
                throw new InvalidOperationException("No templates to merge. Add at least one template.");

            var body = BuildBody();
            if (body.Length > MaxTotalLength)
                throw new InvalidOperationException(
                    $"Merged template exceeds maximum length of {MaxTotalLength:N0} characters (got {body.Length:N0}).");

            var defaults = BuildDefaults();
            return new PromptTemplate(body, defaults);
        }

        /// <summary>
        /// Returns a summary of the merge plan: how many entries, which variables
        /// are referenced, and any conflicting default values detected.
        /// </summary>
        /// <remarks>
        /// A conflict is reported when two or more sources (entry-level defaults
        /// and/or global defaults) supply <em>different</em> default values for
        /// the same variable. This mirrors what <see cref="Merge"/> actually
        /// merges, so a summary that reports no conflicts will not throw under
        /// <see cref="ConflictResolution.ThrowOnConflict"/>. Note that a default
        /// can conflict even if the variable is never referenced in any template
        /// body.
        /// </remarks>
        public MergeSummary Summarize()
        {
            var allVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect every default value contributed for each variable, in
            // merge order, tagged with its source. Conflict detection must be
            // driven by the defaults themselves (which is what Merge() merges),
            // NOT by which entries reference {{var}} in their body — a template
            // can define a default for a variable it never references, and
            // Merge(ThrowOnConflict) throws on those too. Keying on body
            // references produced false negatives where Summarize() reported
            // no conflict yet Merge() threw.
            var defaultSources = new Dictionary<string, List<(string Source, string Value)>>(StringComparer.OrdinalIgnoreCase);

            void RecordDefault(string key, string value, string source)
            {
                if (!defaultSources.TryGetValue(key, out var list))
                {
                    list = new List<(string, string)>();
                    defaultSources[key] = list;
                }
                list.Add((source, value));
            }

            // Global defaults participate in the merge (and in ThrowOnConflict),
            // so they must be considered a conflict source as well.
            foreach (var kv in _globalDefaults)
                RecordDefault(kv.Key, kv.Value, "global defaults");

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var label = entry.Label ?? $"Entry[{i}]";

                foreach (var v in entry.GetVariables())
                    allVariables.Add(v);

                foreach (var kv in entry.GetDefaults())
                    RecordDefault(kv.Key, kv.Value, label);
            }

            var conflicts = new List<string>();
            foreach (var kv in defaultSources)
            {
                var contributions = kv.Value;
                if (contributions.Count <= 1)
                    continue;

                // A conflict exists only when at least two sources disagree on
                // the value. Identical defaults across sources are harmless.
                var distinctValues = contributions.Select(c => c.Value).Distinct(StringComparer.Ordinal).ToList();
                if (distinctValues.Count <= 1)
                    continue;

                var sources = contributions.Select(c => c.Source).Distinct().ToList();
                conflicts.Add($"Variable '{kv.Key}' has conflicting defaults from: {string.Join(", ", sources)}");
            }

            return new MergeSummary(
                _entries.Count,
                allVariables.ToList(),
                conflicts,
                _conflictMode
            );
        }

        private string BuildBody()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(_prefix))
            {
                sb.Append(_prefix);
                sb.Append(_separator);
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0) sb.Append(_separator);

                var entry = _entries[i];
                if (entry.Label != null)
                {
                    sb.Append($"[{entry.Label}]");
                    sb.Append('\n');
                }

                sb.Append(entry.GetBody());
            }

            if (!string.IsNullOrEmpty(_suffix))
            {
                sb.Append(_separator);
                sb.Append(_suffix);
            }

            return sb.ToString();
        }

        private Dictionary<string, string> BuildDefaults()
        {
            var merged = new Dictionary<string, string>(_globalDefaults);

            foreach (var entry in _entries)
            {
                var entryDefaults = entry.GetDefaults();
                foreach (var kv in entryDefaults)
                {
                    switch (_conflictMode)
                    {
                        case ConflictResolution.LastWins:
                            merged[kv.Key] = kv.Value;
                            break;

                        case ConflictResolution.FirstWins:
                            if (!merged.ContainsKey(kv.Key))
                                merged[kv.Key] = kv.Value;
                            break;

                        case ConflictResolution.ThrowOnConflict:
                            if (merged.ContainsKey(kv.Key) && merged[kv.Key] != kv.Value)
                                throw new InvalidOperationException(
                                    $"Conflicting defaults for variable '{kv.Key}': " +
                                    $"'{merged[kv.Key]}' vs '{kv.Value}'");
                            merged[kv.Key] = kv.Value;
                            break;
                    }
                }
            }

            return merged;
        }

        // ---- Inner Types ----

        private class MergeEntry
        {
            private readonly PromptTemplate? _template;
            private readonly string? _rawText;

            public string? Label { get; }

            public MergeEntry(PromptTemplate template, string? label)
            {
                _template = template;
                Label = label;
            }

            public MergeEntry(string rawText, string? label)
            {
                _rawText = rawText;
                Label = label;
            }

            public string GetBody()
            {
                if (_template != null)
                    return _template.Template;
                return _rawText!;
            }

            public HashSet<string> GetVariables()
            {
                if (_template != null)
                    return _template.GetVariables();
                return new HashSet<string>();
            }

            public Dictionary<string, string> GetDefaults()
            {
                if (_template != null)
                    return new Dictionary<string, string>(_template.Defaults);
                return new Dictionary<string, string>();
            }
        }
    }

    /// <summary>
    /// Controls how conflicting default values are handled during merge.
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>Later templates override earlier defaults. This is the default.</summary>
        LastWins,

        /// <summary>The first default encountered is kept; later values are ignored.</summary>
        FirstWins,

        /// <summary>Throws an exception if two templates define different defaults for the same variable.</summary>
        ThrowOnConflict
    }

    /// <summary>
    /// Summary of a merge plan, including variable inventory and conflict detection.
    /// </summary>
    public class MergeSummary
    {
        /// <summary>Number of entries being merged.</summary>
        public int EntryCount { get; }

        /// <summary>All unique variables across all entries.</summary>
        public IReadOnlyList<string> Variables { get; }

        /// <summary>Any detected conflicts between entry defaults.</summary>
        public IReadOnlyList<string> Conflicts { get; }

        /// <summary>The conflict resolution mode in effect.</summary>
        public ConflictResolution ConflictMode { get; }

        /// <summary>Whether any conflicts were detected.</summary>
        public bool HasConflicts => Conflicts.Count > 0;

        internal MergeSummary(
            int entryCount,
            List<string> variables,
            List<string> conflicts,
            ConflictResolution conflictMode)
        {
            EntryCount = entryCount;
            Variables = variables.AsReadOnly();
            Conflicts = conflicts.AsReadOnly();
            ConflictMode = conflictMode;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Merge Summary: {EntryCount} entries, {Variables.Count} variables");
            if (HasConflicts)
            {
                sb.AppendLine($"⚠ {Conflicts.Count} conflict(s):");
                foreach (var c in Conflicts) sb.AppendLine($"  - {c}");
            }
            sb.AppendLine($"Resolution: {ConflictMode}");
            return sb.ToString();
        }
    }
}
