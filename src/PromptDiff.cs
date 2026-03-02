namespace Prompt
{
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The type of change detected between two prompt versions.
    /// </summary>
    public enum DiffChangeType
    {
        /// <summary>No change.</summary>
        Unchanged,
        /// <summary>Content was added.</summary>
        Added,
        /// <summary>Content was removed.</summary>
        Removed,
        /// <summary>Content was modified.</summary>
        Modified
    }

    /// <summary>
    /// Represents a single change detected by <see cref="PromptDiff"/>.
    /// </summary>
    public class DiffChange
    {
        /// <summary>Gets the type of change.</summary>
        public DiffChangeType Type { get; }

        /// <summary>Gets the section or field that changed.</summary>
        public string Field { get; }

        /// <summary>Gets the old value (null for additions).</summary>
        public string? OldValue { get; }

        /// <summary>Gets the new value (null for removals).</summary>
        public string? NewValue { get; }

        /// <summary>
        /// Creates a new diff change.
        /// </summary>
        public DiffChange(DiffChangeType type, string field, string? oldValue, string? newValue)
        {
            Type = type;
            Field = field ?? throw new ArgumentNullException(nameof(field));
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Type switch
            {
                DiffChangeType.Added => $"+ {Field}: {NewValue}",
                DiffChangeType.Removed => $"- {Field}: {OldValue}",
                DiffChangeType.Modified => $"~ {Field}: {OldValue} → {NewValue}",
                _ => $"  {Field}: {OldValue}"
            };
        }
    }

    /// <summary>
    /// The result of comparing two prompt templates, containing all
    /// detected changes and summary statistics.
    /// </summary>
    public class DiffResult
    {
        /// <summary>Gets the list of changes detected.</summary>
        public IReadOnlyList<DiffChange> Changes { get; }

        /// <summary>Gets whether the two templates are identical.</summary>
        public bool AreEqual => Changes.Count == 0;

        /// <summary>Gets the number of additions.</summary>
        public int Additions => Changes.Count(c => c.Type == DiffChangeType.Added);

        /// <summary>Gets the number of removals.</summary>
        public int Removals => Changes.Count(c => c.Type == DiffChangeType.Removed);

        /// <summary>Gets the number of modifications.</summary>
        public int Modifications => Changes.Count(c => c.Type == DiffChangeType.Modified);

        /// <summary>Gets a similarity score between 0.0 and 1.0.</summary>
        public double Similarity { get; }

        /// <summary>
        /// Creates a new diff result.
        /// </summary>
        internal DiffResult(List<DiffChange> changes, double similarity)
        {
            Changes = changes.AsReadOnly();
            Similarity = Math.Clamp(similarity, 0.0, 1.0);
        }

        /// <summary>
        /// Returns a human-readable summary of the diff.
        /// </summary>
        public string ToSummary()
        {
            if (AreEqual)
                return "Templates are identical.";

            var sb = new StringBuilder();
            sb.AppendLine($"Similarity: {Similarity:P1}");
            sb.AppendLine($"Changes: {Changes.Count} ({Additions} added, {Removals} removed, {Modifications} modified)");
            sb.AppendLine();

            foreach (var change in Changes)
            {
                sb.AppendLine(change.ToString());
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Returns a unified-diff-style representation of the template body changes.
        /// </summary>
        public string ToUnifiedDiff()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- old");
            sb.AppendLine("+++ new");
            sb.AppendLine("@@");

            foreach (var change in Changes)
            {
                switch (change.Type)
                {
                    case DiffChangeType.Added:
                        sb.AppendLine($"+ {change.Field}: {change.NewValue}");
                        break;
                    case DiffChangeType.Removed:
                        sb.AppendLine($"- {change.Field}: {change.OldValue}");
                        break;
                    case DiffChangeType.Modified:
                        sb.AppendLine($"- {change.Field}: {change.OldValue}");
                        sb.AppendLine($"+ {change.Field}: {change.NewValue}");
                        break;
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Serializes the diff result to JSON.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var data = new DiffResultData
            {
                AreEqual = AreEqual,
                Similarity = Math.Round(Similarity, 4),
                Additions = Additions,
                Removals = Removals,
                Modifications = Modifications,
                Changes = Changes.Select(c => new DiffChangeData
                {
                    Type = c.Type.ToString().ToLowerInvariant(),
                    Field = c.Field,
                    OldValue = c.OldValue,
                    NewValue = c.NewValue
                }).ToList()
            };

            return JsonSerializer.Serialize(data, SerializationGuards.WriteOptions(indented));
        }

        internal class DiffResultData
        {
            [JsonPropertyName("areEqual")]
            public bool AreEqual { get; set; }

            [JsonPropertyName("similarity")]
            public double Similarity { get; set; }

            [JsonPropertyName("additions")]
            public int Additions { get; set; }

            [JsonPropertyName("removals")]
            public int Removals { get; set; }

            [JsonPropertyName("modifications")]
            public int Modifications { get; set; }

            [JsonPropertyName("changes")]
            public List<DiffChangeData> Changes { get; set; } = new();
        }

        internal class DiffChangeData
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("field")]
            public string Field { get; set; } = "";

            [JsonPropertyName("oldValue")]
            public string? OldValue { get; set; }

            [JsonPropertyName("newValue")]
            public string? NewValue { get; set; }
        }
    }

    /// <summary>
    /// Compares two <see cref="PromptTemplate"/> instances and produces
    /// a detailed <see cref="DiffResult"/> showing what changed between
    /// them. Useful for prompt engineering iteration, A/B testing, and
    /// version tracking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Compares template body text (line-by-line), variables, and default
    /// values. Produces a similarity score using Levenshtein-based distance.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var v1 = new PromptTemplate(
    ///     "You are a {{role}}. Help with {{topic}}.",
    ///     new Dictionary&lt;string, string&gt; { ["role"] = "assistant" });
    ///
    /// var v2 = new PromptTemplate(
    ///     "You are a {{role}}. Help with {{topic}}.\nBe {{style}}.",
    ///     new Dictionary&lt;string, string&gt; { ["role"] = "expert", ["style"] = "concise" });
    ///
    /// var diff = PromptDiff.Compare(v1, v2);
    /// Console.WriteLine(diff.ToSummary());
    /// // Similarity: 72.3%
    /// // Changes: 3 (1 added, 0 removed, 2 modified)
    /// //
    /// // ~ template: ... → ...
    /// // ~ default[role]: assistant → expert
    /// // + default[style]: concise
    ///
    /// // Or compare library entries
    /// var entryDiff = PromptDiff.CompareEntries(entryV1, entryV2);
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptDiff
    {
        /// <summary>
        /// Compares two prompt templates and returns a detailed diff.
        /// </summary>
        /// <param name="oldTemplate">The original template.</param>
        /// <param name="newTemplate">The updated template.</param>
        /// <returns>A <see cref="DiffResult"/> describing all changes.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either template is null.
        /// </exception>
        public static DiffResult Compare(PromptTemplate oldTemplate, PromptTemplate newTemplate)
        {
            if (oldTemplate == null) throw new ArgumentNullException(nameof(oldTemplate));
            if (newTemplate == null) throw new ArgumentNullException(nameof(newTemplate));

            var changes = new List<DiffChange>();

            // Compare template body
            if (oldTemplate.Template != newTemplate.Template)
            {
                changes.Add(new DiffChange(
                    DiffChangeType.Modified,
                    "template",
                    Truncate(oldTemplate.Template, 200),
                    Truncate(newTemplate.Template, 200)));

                // Also do line-level diff
                var oldLines = oldTemplate.Template.Split('\n');
                var newLines = newTemplate.Template.Split('\n');
                CompareLines(oldLines, newLines, changes);
            }

            // Compare variables
            var oldVars = oldTemplate.GetVariables();
            var newVars = newTemplate.GetVariables();

            foreach (var v in newVars.Except(oldVars, StringComparer.OrdinalIgnoreCase))
            {
                changes.Add(new DiffChange(DiffChangeType.Added, $"variable[{v}]", null, v));
            }

            foreach (var v in oldVars.Except(newVars, StringComparer.OrdinalIgnoreCase))
            {
                changes.Add(new DiffChange(DiffChangeType.Removed, $"variable[{v}]", v, null));
            }

            // Compare defaults
            var oldDefaults = oldTemplate.Defaults;
            var newDefaults = newTemplate.Defaults;

            var allDefaultKeys = new HashSet<string>(
                oldDefaults.Keys.Concat(newDefaults.Keys),
                StringComparer.OrdinalIgnoreCase);

            foreach (var key in allDefaultKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                bool inOld = oldDefaults.TryGetValue(key, out var oldVal);
                bool inNew = newDefaults.TryGetValue(key, out var newVal);

                if (inOld && inNew)
                {
                    if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
                    {
                        changes.Add(new DiffChange(
                            DiffChangeType.Modified, $"default[{key}]", oldVal, newVal));
                    }
                }
                else if (inNew)
                {
                    changes.Add(new DiffChange(
                        DiffChangeType.Added, $"default[{key}]", null, newVal));
                }
                else
                {
                    changes.Add(new DiffChange(
                        DiffChangeType.Removed, $"default[{key}]", oldVal, null));
                }
            }

            // Calculate similarity
            double similarity = ComputeSimilarity(oldTemplate.Template, newTemplate.Template);

            return new DiffResult(changes, similarity);
        }

        /// <summary>
        /// Compares two <see cref="PromptEntry"/> instances, including
        /// metadata (description, category, tags) in addition to
        /// template content.
        /// </summary>
        /// <param name="oldEntry">The original entry.</param>
        /// <param name="newEntry">The updated entry.</param>
        /// <returns>A <see cref="DiffResult"/> describing all changes.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either entry is null.
        /// </exception>
        public static DiffResult CompareEntries(PromptEntry oldEntry, PromptEntry newEntry)
        {
            if (oldEntry == null) throw new ArgumentNullException(nameof(oldEntry));
            if (newEntry == null) throw new ArgumentNullException(nameof(newEntry));

            // Start with template diff
            var templateDiff = Compare(oldEntry.Template, newEntry.Template);
            var changes = new List<DiffChange>(templateDiff.Changes);

            // Compare name
            if (!string.Equals(oldEntry.Name, newEntry.Name, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, "name", oldEntry.Name, newEntry.Name));
            }

            // Compare description
            if (!string.Equals(oldEntry.Description, newEntry.Description, StringComparison.Ordinal))
            {
                if (oldEntry.Description == null)
                    changes.Add(new DiffChange(DiffChangeType.Added, "description", null, newEntry.Description));
                else if (newEntry.Description == null)
                    changes.Add(new DiffChange(DiffChangeType.Removed, "description", oldEntry.Description, null));
                else
                    changes.Add(new DiffChange(DiffChangeType.Modified, "description", oldEntry.Description, newEntry.Description));
            }

            // Compare category
            if (!string.Equals(oldEntry.Category, newEntry.Category, StringComparison.OrdinalIgnoreCase))
            {
                if (oldEntry.Category == null)
                    changes.Add(new DiffChange(DiffChangeType.Added, "category", null, newEntry.Category));
                else if (newEntry.Category == null)
                    changes.Add(new DiffChange(DiffChangeType.Removed, "category", oldEntry.Category, null));
                else
                    changes.Add(new DiffChange(DiffChangeType.Modified, "category", oldEntry.Category, newEntry.Category));
            }

            // Compare tags
            var addedTags = newEntry.Tags.Except(oldEntry.Tags, StringComparer.OrdinalIgnoreCase);
            var removedTags = oldEntry.Tags.Except(newEntry.Tags, StringComparer.OrdinalIgnoreCase);

            foreach (var tag in addedTags.OrderBy(t => t))
            {
                changes.Add(new DiffChange(DiffChangeType.Added, $"tag[{tag}]", null, tag));
            }

            foreach (var tag in removedTags.OrderBy(t => t))
            {
                changes.Add(new DiffChange(DiffChangeType.Removed, $"tag[{tag}]", tag, null));
            }

            return new DiffResult(changes, templateDiff.Similarity);
        }

        /// <summary>
        /// Compares two <see cref="PromptLibrary"/> instances and returns
        /// a summary of which entries were added, removed, or modified.
        /// </summary>
        /// <param name="oldLibrary">The original library.</param>
        /// <param name="newLibrary">The updated library.</param>
        /// <returns>A <see cref="DiffResult"/> with library-level changes.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either library is null.
        /// </exception>
        public static DiffResult CompareLibraries(PromptLibrary oldLibrary, PromptLibrary newLibrary)
        {
            if (oldLibrary == null) throw new ArgumentNullException(nameof(oldLibrary));
            if (newLibrary == null) throw new ArgumentNullException(nameof(newLibrary));

            var changes = new List<DiffChange>();

            var oldNames = new HashSet<string>(oldLibrary.Names, StringComparer.OrdinalIgnoreCase);
            var newNames = new HashSet<string>(newLibrary.Names, StringComparer.OrdinalIgnoreCase);

            // Added entries
            foreach (var name in newNames.Except(oldNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var entry = newLibrary.Get(name);
                changes.Add(new DiffChange(
                    DiffChangeType.Added,
                    $"entry[{name}]",
                    null,
                    Truncate(entry.Template.Template, 100)));
            }

            // Removed entries
            foreach (var name in oldNames.Except(newNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var entry = oldLibrary.Get(name);
                changes.Add(new DiffChange(
                    DiffChangeType.Removed,
                    $"entry[{name}]",
                    Truncate(entry.Template.Template, 100),
                    null));
            }

            // Modified entries
            foreach (var name in oldNames.Intersect(newNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var oldEntry = oldLibrary.Get(name);
                var newEntry = newLibrary.Get(name);
                var entryDiff = CompareEntries(oldEntry, newEntry);

                if (!entryDiff.AreEqual)
                {
                    changes.Add(new DiffChange(
                        DiffChangeType.Modified,
                        $"entry[{name}]",
                        $"{entryDiff.Changes.Count} changes",
                        $"similarity: {entryDiff.Similarity:P1}"));
                }
            }

            // Overall similarity based on entry overlap
            int totalEntries = Math.Max(oldNames.Count, newNames.Count);
            int commonEntries = oldNames.Intersect(newNames, StringComparer.OrdinalIgnoreCase).Count();
            double similarity = totalEntries > 0 ? (double)commonEntries / totalEntries : 1.0;

            return new DiffResult(changes, similarity);
        }

        // ──────────────── Private Helpers ────────────────

        /// <summary>
        /// Performs a simple line-level comparison, adding per-line changes.
        /// </summary>
        private static void CompareLines(string[] oldLines, string[] newLines, List<DiffChange> changes)
        {
            // LCS-based diff: correctly identifies insertions, deletions,
            // and true modifications even when lines are shifted.
            // Previously used naive positional comparison which reported
            // every shifted line as "Modified" (see issue #31).

            var oldTrimmed = oldLines.Select(l => l.TrimEnd()).ToArray();
            var newTrimmed = newLines.Select(l => l.TrimEnd()).ToArray();

            int m = oldTrimmed.Length;
            int n = newTrimmed.Length;

            // Build LCS length table — O(m*n) time and space.
            // For typical prompt templates (< 1000 lines) this is fine.
            var dp = new int[m + 1, n + 1];
            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (oldTrimmed[i - 1] == newTrimmed[j - 1])
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    else
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }

            // Backtrack to produce the diff
            var diffOps = new List<(char op, int oldIdx, int newIdx)>();
            int oi = m, ni = n;
            while (oi > 0 || ni > 0)
            {
                if (oi > 0 && ni > 0 && oldTrimmed[oi - 1] == newTrimmed[ni - 1])
                {
                    // Equal — skip
                    oi--;
                    ni--;
                }
                else if (ni > 0 && (oi == 0 || dp[oi, ni - 1] >= dp[oi - 1, ni]))
                {
                    diffOps.Add(('+', -1, ni - 1));
                    ni--;
                }
                else
                {
                    diffOps.Add(('-', oi - 1, -1));
                    oi--;
                }
            }

            diffOps.Reverse();

            foreach (var (op, oldIdx, newIdx) in diffOps)
            {
                if (op == '-')
                {
                    changes.Add(new DiffChange(
                        DiffChangeType.Removed, $"line[{oldIdx + 1}]",
                        Truncate(oldTrimmed[oldIdx], 120), null));
                }
                else // '+'
                {
                    changes.Add(new DiffChange(
                        DiffChangeType.Added, $"line[{newIdx + 1}]",
                        null, Truncate(newTrimmed[newIdx], 120)));
                }
            }
        }

        /// <summary>
        /// Computes a similarity score between 0.0 and 1.0 using
        /// normalized Levenshtein distance.
        /// </summary>
        private static double ComputeSimilarity(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal))
                return 1.0;

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0.0;

            // For very long strings, use a line-based comparison instead
            // of character-level Levenshtein to avoid O(n*m) memory.
            if (a.Length > 5000 || b.Length > 5000)
            {
                return ComputeLineSimilarity(a, b);
            }

            int distance = LevenshteinDistance(a, b);
            int maxLen = Math.Max(a.Length, b.Length);
            return 1.0 - ((double)distance / maxLen);
        }

        /// <summary>
        /// Line-based similarity for large texts — compares common lines
        /// as a ratio to avoid expensive character-level computation.
        /// </summary>
        private static double ComputeLineSimilarity(string a, string b)
        {
            var aLines = new HashSet<string>(a.Split('\n').Select(l => l.TrimEnd()));
            var bLines = new HashSet<string>(b.Split('\n').Select(l => l.TrimEnd()));

            int common = aLines.Intersect(bLines).Count();
            int total = Math.Max(aLines.Count, bLines.Count);
            return total > 0 ? (double)common / total : 1.0;
        }

        /// <summary>
        /// Standard Levenshtein distance with two-row optimization.
        /// </summary>
        private static int LevenshteinDistance(string a, string b)
        {
            int m = a.Length;
            int n = b.Length;

            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int j = 0; j <= n; j++)
                prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= n; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }

            return prev[n];
        }

        /// <summary>
        /// Truncates a string with ellipsis if it exceeds the max length.
        /// </summary>
        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            return value[..(maxLength - 3)] + "...";
        }
    }
}
