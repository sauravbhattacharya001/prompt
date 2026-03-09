namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Strategy for resolving conflicts when merging overlapping prompt sections.
    /// </summary>
    public enum MergeConflictStrategy
    {
        /// <summary>Keep the section from the first (higher-priority) prompt.</summary>
        KeepFirst,
        /// <summary>Keep the section from the last (lower-priority) prompt.</summary>
        KeepLast,
        /// <summary>Concatenate both sections with a separator.</summary>
        Concatenate,
        /// <summary>Interleave lines from both sections alternately.</summary>
        Interleave,
        /// <summary>Mark conflicts with conflict markers for manual resolution.</summary>
        MarkConflict
    }

    /// <summary>
    /// Represents a detected section within a prompt, identified by heading or pattern.
    /// </summary>
    public class MergedSection
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("heading")]
        public string Heading { get; init; } = "";

        [JsonPropertyName("content")]
        public string Content { get; init; } = "";

        [JsonPropertyName("order")]
        public int Order { get; init; }

        [JsonPropertyName("sourceIndex")]
        public int SourceIndex { get; init; }
    }

    /// <summary>Configuration for how prompts should be merged.</summary>
    public class MergeOptions
    {
        [JsonPropertyName("conflictStrategy")]
        public MergeConflictStrategy ConflictStrategy { get; set; } = MergeConflictStrategy.KeepFirst;

        [JsonPropertyName("sectionSeparator")]
        public string SectionSeparator { get; set; } = "\n";

        [JsonPropertyName("deduplicateLines")]
        public bool DeduplicateLines { get; set; } = true;

        [JsonPropertyName("mergeVariables")]
        public bool MergeVariables { get; set; } = true;

        [JsonPropertyName("sectionOrder")]
        public List<string>? SectionOrder { get; set; }

        [JsonPropertyName("excludeSections")]
        public HashSet<string>? ExcludeSections { get; set; }

        [JsonPropertyName("maxLength")]
        public int MaxLength { get; set; } = 0;

        [JsonPropertyName("conflictMarkerPrefix")]
        public string ConflictMarkerPrefix { get; set; } = "<<<<<<< ";
    }

    /// <summary>Records a conflict encountered during merging.</summary>
    public class MergeConflict
    {
        [JsonPropertyName("sectionName")]
        public string SectionName { get; init; } = "";

        [JsonPropertyName("sourceIndices")]
        public List<int> SourceIndices { get; init; } = new();

        [JsonPropertyName("resolution")]
        public MergeConflictStrategy Resolution { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";
    }

    /// <summary>Result of a merge operation.</summary>
    public class MergeResult
    {
        [JsonPropertyName("mergedText")]
        public string MergedText { get; init; } = "";

        [JsonPropertyName("sourceCount")]
        public int SourceCount { get; init; }

        [JsonPropertyName("sections")]
        public List<MergedSection> Sections { get; init; } = new();

        [JsonPropertyName("conflicts")]
        public List<MergeConflict> Conflicts { get; init; } = new();

        [JsonPropertyName("variables")]
        public Dictionary<string, List<int>> Variables { get; init; } = new();

        [JsonPropertyName("duplicatesRemoved")]
        public int DuplicatesRemoved { get; init; }

        [JsonPropertyName("wasTruncated")]
        public bool WasTruncated { get; init; }

        [JsonPropertyName("length")]
        public int Length => MergedText.Length;

        [JsonPropertyName("isClean")]
        public bool IsClean => Conflicts.Count == 0;
    }

    /// <summary>
    /// Intelligently merges multiple prompts by detecting sections, resolving conflicts,
    /// deduplicating content, and consolidating template variables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PromptMerger understands prompt structure — it detects sections by headings
    /// (markdown-style ## or ALL CAPS:), identifies template variables ({{var}}),
    /// and merges intelligently rather than blindly concatenating.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var merger = new PromptMerger();
    /// var result = merger.Merge(
    ///     new[] { systemPrompt, userContext, taskInstructions },
    ///     new MergeOptions { ConflictStrategy = MergeConflictStrategy.Concatenate }
    /// );
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptMerger
    {
        public const int MaxSources = 20;
        public const int MaxSourceLength = 100_000;

        private static readonly Regex HeadingPattern = new(
            @"^(?:#{1,4}\s+(.+)|([A-Z][A-Z\s]{2,}):)\s*$",
            RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex VariablePattern = new(
            @"\{\{([a-zA-Z_][a-zA-Z0-9_]*)\}\}",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>Merges multiple prompts into a single cohesive prompt.</summary>
        public MergeResult Merge(IEnumerable<string> prompts, MergeOptions? options = null)
        {
            var opts = options ?? new MergeOptions();
            var sources = prompts?.ToList() ?? throw new ArgumentException("Prompts cannot be null.");

            if (sources.Count == 0)
                throw new ArgumentException("At least one prompt is required.");
            if (sources.Count > MaxSources)
                throw new ArgumentException($"Cannot merge more than {MaxSources} prompts.");
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] == null)
                    throw new ArgumentException($"Prompt at index {i} is null.");
                if (sources[i].Length > MaxSourceLength)
                    throw new ArgumentException($"Prompt at index {i} exceeds {MaxSourceLength} characters.");
            }

            var allSections = new List<List<MergedSection>>();
            for (int i = 0; i < sources.Count; i++)
                allSections.Add(ParseSectionsInternal(sources[i], i));

            var variables = new Dictionary<string, List<int>>();
            if (opts.MergeVariables)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    foreach (Match m in VariablePattern.Matches(sources[i]))
                    {
                        var name = m.Groups[1].Value;
                        if (!variables.ContainsKey(name))
                            variables[name] = new List<int>();
                        if (!variables[name].Contains(i))
                            variables[name].Add(i);
                    }
                }
            }

            var sectionGroups = new Dictionary<string, List<MergedSection>>(StringComparer.OrdinalIgnoreCase);
            var sectionAppearanceOrder = new List<string>();

            foreach (var sourceSections in allSections)
            {
                foreach (var section in sourceSections)
                {
                    if (opts.ExcludeSections != null &&
                        opts.ExcludeSections.Contains(section.Name, StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (!sectionGroups.ContainsKey(section.Name))
                    {
                        sectionGroups[section.Name] = new List<MergedSection>();
                        sectionAppearanceOrder.Add(section.Name);
                    }
                    sectionGroups[section.Name].Add(section);
                }
            }

            var orderedKeys = DetermineSectionOrder(sectionAppearanceOrder, opts.SectionOrder);

            var mergedSections = new List<MergedSection>();
            var conflicts = new List<MergeConflict>();
            int duplicatesRemoved = 0;
            int orderIndex = 0;

            foreach (var key in orderedKeys)
            {
                if (!sectionGroups.ContainsKey(key)) continue;
                var group = sectionGroups[key];

                if (group.Count == 1)
                {
                    mergedSections.Add(new MergedSection
                    {
                        Name = group[0].Name,
                        Heading = group[0].Heading,
                        Content = group[0].Content,
                        Order = orderIndex++,
                        SourceIndex = group[0].SourceIndex
                    });
                }
                else
                {
                    var (merged, removed) = ResolveSectionConflict(group, opts);
                    duplicatesRemoved += removed;

                    mergedSections.Add(new MergedSection
                    {
                        Name = group[0].Name,
                        Heading = group[0].Heading,
                        Content = merged,
                        Order = orderIndex++,
                        SourceIndex = -1
                    });

                    var distinct = group.Select(g => g.Content.Trim()).Distinct().ToList();
                    if (distinct.Count > 1)
                    {
                        conflicts.Add(new MergeConflict
                        {
                            SectionName = key,
                            SourceIndices = group.Select(g => g.SourceIndex).ToList(),
                            Resolution = opts.ConflictStrategy,
                            Description = $"Section '{key}' found in {group.Count} sources with different content."
                        });
                    }
                }
            }

            var parts = new List<string>();
            foreach (var section in mergedSections)
            {
                if (!string.IsNullOrEmpty(section.Heading))
                    parts.Add(section.Heading);
                if (!string.IsNullOrEmpty(section.Content))
                    parts.Add(section.Content);
            }

            var mergedText = string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

            if (opts.DeduplicateLines)
            {
                var (deduped, count) = DeduplicateLines(mergedText);
                mergedText = deduped;
                duplicatesRemoved += count;
            }

            bool wasTruncated = false;
            if (opts.MaxLength > 0 && mergedText.Length > opts.MaxLength)
            {
                mergedText = mergedText.Substring(0, opts.MaxLength).TrimEnd();
                wasTruncated = true;
            }

            return new MergeResult
            {
                MergedText = mergedText,
                SourceCount = sources.Count,
                Sections = mergedSections,
                Conflicts = conflicts,
                Variables = variables,
                DuplicatesRemoved = duplicatesRemoved,
                WasTruncated = wasTruncated
            };
        }

        /// <summary>Convenience overload for merging two prompts.</summary>
        public MergeResult Merge(string first, string second, MergeOptions? options = null)
            => Merge(new[] { first, second }, options);

        /// <summary>Parses a prompt into its constituent sections.</summary>
        public List<MergedSection> ParseSections(string prompt)
            => ParseSectionsInternal(prompt, 0);

        /// <summary>Extracts all template variables ({{name}}) from text.</summary>
        public HashSet<string> ExtractVariables(string text)
        {
            if (string.IsNullOrEmpty(text)) return new HashSet<string>();
            var vars = new HashSet<string>();
            foreach (Match m in VariablePattern.Matches(text))
                vars.Add(m.Groups[1].Value);
            return vars;
        }

        /// <summary>Computes Jaccard line-similarity (0.0-1.0) between two texts.</summary>
        public double ComputeSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

            var linesA = new HashSet<string>(a.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
            var linesB = new HashSet<string>(b.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
            if (linesA.Count == 0 && linesB.Count == 0) return 1.0;

            var intersection = linesA.Intersect(linesB).Count();
            var union = linesA.Union(linesB).Count();
            return union == 0 ? 1.0 : (double)intersection / union;
        }

        /// <summary>Generates a text report showing each source's contribution.</summary>
        public string GenerateContributionReport(MergeResult result, IEnumerable<string> originalPrompts)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Merge Contribution Report ===");
            sb.AppendLine($"Sources: {result.SourceCount}");
            sb.AppendLine($"Sections: {result.Sections.Count}");
            sb.AppendLine($"Conflicts: {result.Conflicts.Count}");
            sb.AppendLine($"Variables: {result.Variables.Count}");
            sb.AppendLine($"Duplicates removed: {result.DuplicatesRemoved}");
            sb.AppendLine($"Truncated: {result.WasTruncated}");
            sb.AppendLine($"Output length: {result.Length} chars");
            sb.AppendLine();

            sb.AppendLine("--- Sections ---");
            foreach (var section in result.Sections)
            {
                var source = section.SourceIndex >= 0 ? $"source {section.SourceIndex}" : "merged";
                sb.AppendLine($"  [{section.Order}] {section.Name} (from {source}, {section.Content.Length} chars)");
            }

            if (result.Conflicts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- Conflicts ---");
                foreach (var c in result.Conflicts)
                    sb.AppendLine($"  {c.SectionName}: {c.Description} (resolved: {c.Resolution})");
            }

            if (result.Variables.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- Variables ---");
                foreach (var kv in result.Variables.OrderBy(v => v.Key))
                    sb.AppendLine($"  {{{{{kv.Key}}}}} — found in sources: {string.Join(", ", kv.Value)}");
            }

            return sb.ToString();
        }

        /// <summary>Serializes a MergeResult to JSON.</summary>
        public string ToJson(MergeResult result)
            => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

        // ---- Private ----

        private List<MergedSection> ParseSectionsInternal(string prompt, int sourceIndex)
        {
            var sections = new List<MergedSection>();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                sections.Add(new MergedSection { Name = "_default", Heading = "", Content = prompt ?? "", Order = 0, SourceIndex = sourceIndex });
                return sections;
            }

            var matches = HeadingPattern.Matches(prompt);
            if (matches.Count == 0)
            {
                sections.Add(new MergedSection { Name = "_default", Heading = "", Content = prompt.Trim(), Order = 0, SourceIndex = sourceIndex });
                return sections;
            }

            var firstMatchStart = matches[0].Index;
            if (firstMatchStart > 0)
            {
                var preamble = prompt.Substring(0, firstMatchStart).Trim();
                if (preamble.Length > 0)
                    sections.Add(new MergedSection { Name = "_preamble", Heading = "", Content = preamble, Order = sections.Count, SourceIndex = sourceIndex });
            }

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var headingText = match.Value.Trim();
                var name = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();

                int contentStart = match.Index + match.Length;
                int contentEnd = (i + 1 < matches.Count) ? matches[i + 1].Index : prompt.Length;
                var content = prompt.Substring(contentStart, contentEnd - contentStart).Trim();

                sections.Add(new MergedSection
                {
                    Name = NormalizeSectionName(name),
                    Heading = headingText,
                    Content = content,
                    Order = sections.Count,
                    SourceIndex = sourceIndex
                });
            }

            return sections;
        }

        private static string NormalizeSectionName(string name)
            => Regex.Replace(name.Trim().ToLowerInvariant(), @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(500));

        private (string merged, int removed) ResolveSectionConflict(List<MergedSection> group, MergeOptions opts)
        {
            switch (opts.ConflictStrategy)
            {
                case MergeConflictStrategy.KeepFirst:
                    return (group[0].Content, 0);

                case MergeConflictStrategy.KeepLast:
                    return (group[^1].Content, 0);

                case MergeConflictStrategy.Concatenate:
                {
                    var contents = group.Select(g => g.Content).ToList();
                    if (opts.DeduplicateLines)
                    {
                        var seen = new HashSet<string>();
                        var deduped = new List<string>();
                        int removed = 0;
                        foreach (var content in contents)
                        {
                            foreach (var line in content.Split('\n'))
                            {
                                var trimmed = line.Trim();
                                if (trimmed.Length == 0 || seen.Add(trimmed))
                                    deduped.Add(line);
                                else
                                    removed++;
                            }
                            deduped.Add("");
                        }
                        return (string.Join("\n", deduped).Trim(), removed);
                    }
                    return (string.Join(opts.SectionSeparator, contents), 0);
                }

                case MergeConflictStrategy.Interleave:
                {
                    var lineArrays = group.Select(g =>
                        g.Content.Split('\n').Where(l => l.Trim().Length > 0).ToArray()).ToList();
                    var maxLen = lineArrays.Max(a => a.Length);
                    var result = new List<string>();
                    for (int i = 0; i < maxLen; i++)
                        foreach (var lines in lineArrays)
                            if (i < lines.Length)
                                result.Add(lines[i]);
                    return (string.Join("\n", result), 0);
                }

                case MergeConflictStrategy.MarkConflict:
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < group.Count; i++)
                    {
                        sb.AppendLine(i == 0
                            ? $"{opts.ConflictMarkerPrefix}Source {group[i].SourceIndex}"
                            : $"======= Source {group[i].SourceIndex}");
                        sb.AppendLine(group[i].Content);
                    }
                    sb.AppendLine(">>>>>>> end conflict");
                    return (sb.ToString().Trim(), 0);
                }

                default:
                    return (group[0].Content, 0);
            }
        }

        private List<string> DetermineSectionOrder(List<string> appearanceOrder, List<string>? customOrder)
        {
            if (customOrder == null || customOrder.Count == 0)
                return appearanceOrder;

            var result = new List<string>();
            var remaining = new List<string>(appearanceOrder);

            foreach (var name in customOrder)
            {
                var match = remaining.FirstOrDefault(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    result.Add(match);
                    remaining.Remove(match);
                }
            }
            result.AddRange(remaining);
            return result;
        }

        private static (string text, int removed) DeduplicateLines(string text)
        {
            var lines = text.Split('\n');
            var seen = new HashSet<string>();
            var result = new List<string>();
            int removed = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || seen.Add(trimmed))
                    result.Add(line);
                else
                    removed++;
            }
            return (string.Join("\n", result), removed);
        }
    }
}
