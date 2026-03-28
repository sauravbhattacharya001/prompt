namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Manages hierarchical tags for organizing and categorizing prompts.
    /// Supports tag hierarchies (e.g., "domain/medical/diagnosis"), bulk
    /// operations, tag statistics, auto-tagging rules, and filtering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tags use a path-like hierarchy separated by <c>/</c>. A prompt tagged
    /// with <c>"domain/medical"</c> is implicitly also under <c>"domain"</c>.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var manager = new PromptTagManager();
    ///
    /// // Tag some prompts
    /// manager.Tag("summarize-v1", "task/summarization", "domain/general");
    /// manager.Tag("diagnose-v1", "task/classification", "domain/medical");
    /// manager.Tag("translate-v1", "task/translation", "lang/en-es");
    ///
    /// // Find prompts by tag
    /// var medical = manager.FindByTag("domain/medical");
    /// // → ["diagnose-v1"]
    ///
    /// // Find by ancestor tag (hierarchical)
    /// var allDomain = manager.FindByTag("domain");
    /// // → ["summarize-v1", "diagnose-v1"]
    ///
    /// // Auto-tagging rules
    /// manager.AddAutoTagRule("translate", "task/translation");
    /// manager.AutoTag("translate-report-v2");
    /// // → automatically tagged with "task/translation"
    ///
    /// // Statistics
    /// var stats = manager.GetStatistics();
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptTagManager
    {
        /// <summary>
        /// Maximum allowed JSON payload size for deserialization.
        /// </summary>
        internal const int MaxJsonPayloadBytes = SerializationGuards.MaxJsonPayloadBytes;

        private static readonly Regex TagPattern =
            new Regex(@"^[a-zA-Z0-9_-]+(/[a-zA-Z0-9_-]+)*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

        // promptId → set of tags
        private readonly Dictionary<string, HashSet<string>> _promptTags = new();
        // tag → set of promptIds (reverse index)
        private readonly Dictionary<string, HashSet<string>> _tagIndex = new();
        // auto-tag rules: pattern → tag
        private readonly List<AutoTagRule> _autoTagRules = new();
        // tag descriptions
        private readonly Dictionary<string, string> _tagDescriptions = new();
        // tag aliases: alias → canonical tag
        private readonly Dictionary<string, string> _tagAliases = new();

        /// <summary>
        /// Gets the total number of tagged prompts.
        /// </summary>
        public int PromptCount => _promptTags.Count;

        /// <summary>
        /// Gets the total number of unique tags in use.
        /// </summary>
        public int TagCount => _tagIndex.Count;

        /// <summary>
        /// Assigns one or more tags to a prompt.
        /// </summary>
        /// <param name="promptId">The prompt identifier.</param>
        /// <param name="tags">Tags to assign (hierarchical path format, e.g. "domain/medical").</param>
        /// <exception cref="ArgumentException">Thrown when promptId is empty or a tag has invalid format.</exception>
        public void Tag(string promptId, params string[] tags)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("Prompt ID cannot be empty.", nameof(promptId));

            if (!_promptTags.ContainsKey(promptId))
                _promptTags[promptId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawTag in tags)
            {
                var tag = ResolveAlias(rawTag.Trim());
                ValidateTag(tag);
                _promptTags[promptId].Add(tag);
                if (!_tagIndex.ContainsKey(tag))
                    _tagIndex[tag] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _tagIndex[tag].Add(promptId);
            }
        }

        /// <summary>
        /// Removes one or more tags from a prompt.
        /// </summary>
        /// <param name="promptId">The prompt identifier.</param>
        /// <param name="tags">Tags to remove.</param>
        public void Untag(string promptId, params string[] tags)
        {
            if (!_promptTags.ContainsKey(promptId)) return;

            foreach (var rawTag in tags)
            {
                var tag = ResolveAlias(rawTag.Trim());
                _promptTags[promptId].Remove(tag);
                if (_tagIndex.ContainsKey(tag))
                {
                    _tagIndex[tag].Remove(promptId);
                    if (_tagIndex[tag].Count == 0)
                        _tagIndex.Remove(tag);
                }
            }

            if (_promptTags[promptId].Count == 0)
                _promptTags.Remove(promptId);
        }

        /// <summary>
        /// Gets all tags assigned to a prompt.
        /// </summary>
        /// <param name="promptId">The prompt identifier.</param>
        /// <returns>A sorted list of tags, or empty if the prompt has no tags.</returns>
        public List<string> GetTags(string promptId)
        {
            if (!_promptTags.ContainsKey(promptId))
                return new List<string>();
            var tags = _promptTags[promptId].ToList();
            tags.Sort(StringComparer.OrdinalIgnoreCase);
            return tags;
        }

        /// <summary>
        /// Finds all prompts that have the specified tag or any tag under it
        /// in the hierarchy. For example, searching for "domain" returns prompts
        /// tagged with "domain/medical", "domain/legal", etc.
        /// </summary>
        /// <param name="tag">The tag to search for (exact or ancestor).</param>
        /// <returns>A sorted list of matching prompt IDs.</returns>
        public List<string> FindByTag(string tag)
        {
            tag = ResolveAlias(tag.Trim());
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _tagIndex)
            {
                if (kvp.Key.Equals(tag, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.StartsWith(tag + "/", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var id in kvp.Value)
                        results.Add(id);
                }
            }

            var list = results.ToList();
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>
        /// Finds prompts that match ALL specified tags (intersection).
        /// </summary>
        /// <param name="tags">Tags that must all be present.</param>
        /// <returns>A sorted list of matching prompt IDs.</returns>
        public List<string> FindByAllTags(params string[] tags)
        {
            if (tags.Length == 0) return new List<string>();

            HashSet<string>? result = null;
            foreach (var rawTag in tags)
            {
                var matching = new HashSet<string>(FindByTag(rawTag), StringComparer.OrdinalIgnoreCase);
                if (result == null)
                    result = matching;
                else
                    result.IntersectWith(matching);
            }

            var list = (result ?? new HashSet<string>()).ToList();
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>
        /// Finds prompts that match ANY of the specified tags (union).
        /// </summary>
        /// <param name="tags">Tags to match (any one is sufficient).</param>
        /// <returns>A sorted list of matching prompt IDs.</returns>
        public List<string> FindByAnyTag(params string[] tags)
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                foreach (var id in FindByTag(tag))
                    results.Add(id);
            }
            var list = results.ToList();
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>
        /// Returns the tag hierarchy as a tree structure showing parent/child relationships.
        /// </summary>
        /// <returns>A dictionary representing the tag tree with counts at each level.</returns>
        public Dictionary<string, TagTreeNode> GetTagTree()
        {
            var root = new Dictionary<string, TagTreeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in _tagIndex.Keys.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
            {
                var parts = tag.Split('/');
                var current = root;
                var path = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    path = i == 0 ? parts[i] : path + "/" + parts[i];
                    if (!current.ContainsKey(parts[i]))
                        current[parts[i]] = new TagTreeNode { FullPath = path };
                    if (i == parts.Length - 1)
                        current[parts[i]].DirectCount = _tagIndex.ContainsKey(tag) ? _tagIndex[tag].Count : 0;
                    current = current[parts[i]].Children;
                }
            }

            return root;
        }

        /// <summary>
        /// Registers a description for a tag.
        /// </summary>
        /// <param name="tag">The tag to describe.</param>
        /// <param name="description">Human-readable description of what the tag means.</param>
        public void DescribeTag(string tag, string description)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("Tag cannot be empty.", nameof(tag));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be empty.", nameof(description));
            _tagDescriptions[tag.Trim()] = description.Trim();
        }

        /// <summary>
        /// Gets the description for a tag, if one has been registered.
        /// </summary>
        public string? GetTagDescription(string tag) =>
            _tagDescriptions.TryGetValue(tag.Trim(), out var desc) ? desc : null;

        /// <summary>
        /// Creates an alias that maps to a canonical tag. When tagging or searching,
        /// aliases are automatically resolved.
        /// </summary>
        /// <param name="alias">The alias name.</param>
        /// <param name="canonicalTag">The canonical tag the alias resolves to.</param>
        public void AddAlias(string alias, string canonicalTag)
        {
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("Alias cannot be empty.", nameof(alias));
            if (string.IsNullOrWhiteSpace(canonicalTag))
                throw new ArgumentException("Canonical tag cannot be empty.", nameof(canonicalTag));
            ValidateTag(canonicalTag.Trim());
            _tagAliases[alias.Trim()] = canonicalTag.Trim();
        }

        /// <summary>
        /// Adds an auto-tagging rule. When <see cref="AutoTag"/> is called,
        /// prompts whose ID contains the pattern will automatically receive the tag.
        /// </summary>
        /// <param name="pattern">Substring pattern to match against prompt IDs (case-insensitive).</param>
        /// <param name="tag">Tag to apply when pattern matches.</param>
        public void AddAutoTagRule(string pattern, string tag)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be empty.", nameof(pattern));
            ValidateTag(tag.Trim());
            _autoTagRules.Add(new AutoTagRule { Pattern = pattern.Trim(), Tag = tag.Trim() });
        }

        /// <summary>
        /// Applies auto-tagging rules to a prompt ID. If the prompt ID matches
        /// any registered rule patterns, the corresponding tags are applied.
        /// </summary>
        /// <param name="promptId">The prompt identifier to auto-tag.</param>
        /// <returns>List of tags that were auto-applied.</returns>
        public List<string> AutoTag(string promptId)
        {
            var applied = new List<string>();
            foreach (var rule in _autoTagRules)
            {
                if (promptId.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
                {
                    Tag(promptId, rule.Tag);
                    applied.Add(rule.Tag);
                }
            }
            return applied;
        }

        /// <summary>
        /// Applies auto-tagging rules to all currently tracked prompts.
        /// </summary>
        /// <returns>Dictionary of promptId → newly applied tags.</returns>
        public Dictionary<string, List<string>> AutoTagAll()
        {
            var results = new Dictionary<string, List<string>>();
            foreach (var promptId in _promptTags.Keys.ToList())
            {
                var applied = AutoTag(promptId);
                if (applied.Count > 0)
                    results[promptId] = applied;
            }
            return results;
        }

        /// <summary>
        /// Renames a tag across all prompts. Preserves tag hierarchy.
        /// </summary>
        /// <param name="oldTag">Current tag name.</param>
        /// <param name="newTag">New tag name.</param>
        /// <returns>Number of prompts affected.</returns>
        public int RenameTag(string oldTag, string newTag)
        {
            ValidateTag(newTag.Trim());
            oldTag = oldTag.Trim();
            newTag = newTag.Trim();

            if (!_tagIndex.ContainsKey(oldTag))
                return 0;

            var affected = _tagIndex[oldTag].ToList();
            foreach (var promptId in affected)
            {
                Untag(promptId, oldTag);
                Tag(promptId, newTag);
            }

            // Also rename child tags
            var childTags = _tagIndex.Keys
                .Where(t => t.StartsWith(oldTag + "/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var childTag in childTags)
            {
                var newChildTag = newTag + childTag.Substring(oldTag.Length);
                var childPrompts = _tagIndex[childTag].ToList();
                foreach (var promptId in childPrompts)
                {
                    Untag(promptId, childTag);
                    Tag(promptId, newChildTag);
                }
                affected.AddRange(childPrompts);
            }

            // Move description if exists
            if (_tagDescriptions.ContainsKey(oldTag))
            {
                _tagDescriptions[newTag] = _tagDescriptions[oldTag];
                _tagDescriptions.Remove(oldTag);
            }

            return affected.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        /// <summary>
        /// Merges one tag into another. All prompts with the source tag
        /// get the destination tag instead, and the source tag is removed.
        /// </summary>
        /// <param name="sourceTag">Tag to merge from (will be removed).</param>
        /// <param name="destinationTag">Tag to merge into.</param>
        /// <returns>Number of prompts affected.</returns>
        public int MergeTag(string sourceTag, string destinationTag)
        {
            ValidateTag(destinationTag.Trim());
            sourceTag = sourceTag.Trim();
            destinationTag = destinationTag.Trim();

            if (!_tagIndex.ContainsKey(sourceTag))
                return 0;

            var affected = _tagIndex[sourceTag].ToList();
            foreach (var promptId in affected)
            {
                Untag(promptId, sourceTag);
                Tag(promptId, destinationTag);
            }
            return affected.Count;
        }

        /// <summary>
        /// Finds prompts that share the most tags with the given prompt
        /// (tag-based similarity).
        /// </summary>
        /// <param name="promptId">The reference prompt.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>List of (promptId, sharedTagCount, sharedTags) tuples, sorted by similarity.</returns>
        public List<TagSimilarityResult> FindSimilar(string promptId, int maxResults = 10)
        {
            if (!_promptTags.ContainsKey(promptId))
                return new List<TagSimilarityResult>();

            var sourceTags = _promptTags[promptId];
            var results = new List<TagSimilarityResult>();

            foreach (var kvp in _promptTags)
            {
                if (kvp.Key.Equals(promptId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var shared = sourceTags.Intersect(kvp.Value, StringComparer.OrdinalIgnoreCase).ToList();
                if (shared.Count > 0)
                {
                    // Jaccard similarity
                    var union = sourceTags.Union(kvp.Value, StringComparer.OrdinalIgnoreCase).Count();
                    results.Add(new TagSimilarityResult
                    {
                        PromptId = kvp.Key,
                        SharedTagCount = shared.Count,
                        SharedTags = shared,
                        Similarity = union > 0 ? (double)shared.Count / union : 0
                    });
                }
            }

            return results
                .OrderByDescending(r => r.Similarity)
                .ThenByDescending(r => r.SharedTagCount)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Gets comprehensive statistics about the tag system.
        /// </summary>
        public TagStatistics GetStatistics()
        {
            var stats = new TagStatistics
            {
                TotalPrompts = _promptTags.Count,
                TotalTags = _tagIndex.Count,
                TotalAutoTagRules = _autoTagRules.Count,
                TotalAliases = _tagAliases.Count,
                TotalDescriptions = _tagDescriptions.Count
            };

            if (_tagIndex.Count > 0)
            {
                stats.MostUsedTags = _tagIndex
                    .OrderByDescending(kvp => kvp.Value.Count)
                    .Take(10)
                    .Select(kvp => new TagUsageInfo { Tag = kvp.Key, Count = kvp.Value.Count })
                    .ToList();

                stats.LeastUsedTags = _tagIndex
                    .OrderBy(kvp => kvp.Value.Count)
                    .Take(10)
                    .Select(kvp => new TagUsageInfo { Tag = kvp.Key, Count = kvp.Value.Count })
                    .ToList();

                stats.AverageTagsPerPrompt = _promptTags.Values.Average(v => v.Count);

                // Top-level category breakdown
                stats.CategoryBreakdown = _tagIndex.Keys
                    .Select(t => t.Split('/')[0])
                    .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                // Orphan tags (tags with no prompts - shouldn't happen but track it)
                stats.OrphanTags = _tagIndex
                    .Where(kvp => kvp.Value.Count == 0)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // Untagged prompts
                stats.UntaggedPrompts = _promptTags
                    .Where(kvp => kvp.Value.Count == 0)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }

            return stats;
        }

        /// <summary>
        /// Exports the entire tag state to JSON for persistence.
        /// </summary>
        public string ExportToJson()
        {
            var export = new TagExportData
            {
                PromptTags = _promptTags.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList()),
                AutoTagRules = _autoTagRules.ToList(),
                TagDescriptions = new Dictionary<string, string>(_tagDescriptions),
                TagAliases = new Dictionary<string, string>(_tagAliases)
            };

            return JsonSerializer.Serialize(export, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Imports tag state from a JSON string, merging with existing data.
        /// </summary>
        /// <param name="json">JSON string produced by <see cref="ExportToJson"/>.</param>
        /// <param name="overwrite">If true, clears existing data before importing.</param>
        public void ImportFromJson(string json, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON input cannot be empty.", nameof(json));

            SerializationGuards.ValidateJsonInput(json);

            var data = JsonSerializer.Deserialize<TagExportData>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (data == null)
                throw new JsonException("Failed to deserialize tag data.");

            if (overwrite)
            {
                _promptTags.Clear();
                _tagIndex.Clear();
                _autoTagRules.Clear();
                _tagDescriptions.Clear();
                _tagAliases.Clear();
            }

            if (data.TagAliases != null)
                foreach (var kvp in data.TagAliases)
                    _tagAliases[kvp.Key] = kvp.Value;

            if (data.TagDescriptions != null)
                foreach (var kvp in data.TagDescriptions)
                    _tagDescriptions[kvp.Key] = kvp.Value;

            if (data.AutoTagRules != null)
                foreach (var rule in data.AutoTagRules)
                    if (!_autoTagRules.Any(r => r.Pattern == rule.Pattern && r.Tag == rule.Tag))
                        _autoTagRules.Add(rule);

            if (data.PromptTags != null)
                foreach (var kvp in data.PromptTags)
                    Tag(kvp.Key, kvp.Value.ToArray());
        }

        /// <summary>
        /// Generates a formatted summary of the tag system.
        /// </summary>
        /// <param name="format">Output format: "text", "json", or "markdown".</param>
        public string GenerateReport(string format = "text")
        {
            var stats = GetStatistics();

            return format.ToLowerInvariant() switch
            {
                "json" => JsonSerializer.Serialize(stats, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }),
                "markdown" => GenerateMarkdownReport(stats),
                _ => GenerateTextReport(stats)
            };
        }

        private string GenerateTextReport(TagStatistics stats)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("           PROMPT TAG MANAGER REPORT       ");
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  Total Prompts:    {stats.TotalPrompts}");
            sb.AppendLine($"  Total Tags:       {stats.TotalTags}");
            sb.AppendLine($"  Auto-Tag Rules:   {stats.TotalAutoTagRules}");
            sb.AppendLine($"  Aliases:          {stats.TotalAliases}");
            sb.AppendLine($"  Avg Tags/Prompt:  {stats.AverageTagsPerPrompt:F1}");
            sb.AppendLine();

            if (stats.MostUsedTags?.Count > 0)
            {
                sb.AppendLine("  Most Used Tags:");
                foreach (var t in stats.MostUsedTags)
                    sb.AppendLine($"    {t.Tag,-30} ({t.Count} prompts)");
                sb.AppendLine();
            }

            if (stats.CategoryBreakdown?.Count > 0)
            {
                sb.AppendLine("  Categories:");
                foreach (var cat in stats.CategoryBreakdown.OrderByDescending(c => c.Value))
                    sb.AppendLine($"    {cat.Key,-20} {cat.Value} tags");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }

        private string GenerateMarkdownReport(TagStatistics stats)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Prompt Tag Manager Report");
            sb.AppendLine();
            sb.AppendLine("## Overview");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Total Prompts | {stats.TotalPrompts} |");
            sb.AppendLine($"| Total Tags | {stats.TotalTags} |");
            sb.AppendLine($"| Auto-Tag Rules | {stats.TotalAutoTagRules} |");
            sb.AppendLine($"| Aliases | {stats.TotalAliases} |");
            sb.AppendLine($"| Avg Tags/Prompt | {stats.AverageTagsPerPrompt:F1} |");
            sb.AppendLine();

            if (stats.MostUsedTags?.Count > 0)
            {
                sb.AppendLine("## Most Used Tags");
                sb.AppendLine();
                sb.AppendLine("| Tag | Prompts |");
                sb.AppendLine("|-----|---------|");
                foreach (var t in stats.MostUsedTags)
                    sb.AppendLine($"| `{t.Tag}` | {t.Count} |");
                sb.AppendLine();
            }

            if (stats.CategoryBreakdown?.Count > 0)
            {
                sb.AppendLine("## Categories");
                sb.AppendLine();
                foreach (var cat in stats.CategoryBreakdown.OrderByDescending(c => c.Value))
                    sb.AppendLine($"- **{cat.Key}**: {cat.Value} tags");
            }

            return sb.ToString();
        }

        private string ResolveAlias(string tag)
        {
            return _tagAliases.TryGetValue(tag, out var canonical) ? canonical : tag;
        }

        private static void ValidateTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("Tag cannot be empty.");
            if (tag.Length > 200)
                throw new ArgumentException($"Tag exceeds maximum length of 200 characters: '{tag}'.");
            if (!TagPattern.IsMatch(tag))
                throw new ArgumentException(
                    $"Invalid tag format: '{tag}'. Tags must use alphanumeric characters, hyphens, " +
                    "underscores, separated by '/' for hierarchy (e.g., 'domain/medical/diagnosis').");
        }

        /// <summary>
        /// Represents a node in the tag hierarchy tree.
        /// </summary>
        public class TagTreeNode
        {
            /// <summary>Full path of this tag node.</summary>
            [JsonPropertyName("fullPath")]
            public string FullPath { get; set; } = "";

            /// <summary>Number of prompts directly tagged with this exact tag.</summary>
            [JsonPropertyName("directCount")]
            public int DirectCount { get; set; }

            /// <summary>Child tag nodes.</summary>
            [JsonPropertyName("children")]
            public Dictionary<string, TagTreeNode> Children { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Result of a tag-based similarity search.
        /// </summary>
        public class TagSimilarityResult
        {
            /// <summary>The matching prompt ID.</summary>
            [JsonPropertyName("promptId")]
            public string PromptId { get; set; } = "";

            /// <summary>Number of shared tags.</summary>
            [JsonPropertyName("sharedTagCount")]
            public int SharedTagCount { get; set; }

            /// <summary>Jaccard similarity (0-1).</summary>
            [JsonPropertyName("similarity")]
            public double Similarity { get; set; }

            /// <summary>The actual shared tags.</summary>
            [JsonPropertyName("sharedTags")]
            public List<string> SharedTags { get; set; } = new();
        }

        /// <summary>
        /// Tag usage information for statistics.
        /// </summary>
        public class TagUsageInfo
        {
            /// <summary>The tag.</summary>
            [JsonPropertyName("tag")]
            public string Tag { get; set; } = "";

            /// <summary>Number of prompts with this tag.</summary>
            [JsonPropertyName("count")]
            public int Count { get; set; }
        }

        /// <summary>
        /// Comprehensive statistics about the tag system.
        /// </summary>
        public class TagStatistics
        {
            [JsonPropertyName("totalPrompts")]
            public int TotalPrompts { get; set; }
            [JsonPropertyName("totalTags")]
            public int TotalTags { get; set; }
            [JsonPropertyName("totalAutoTagRules")]
            public int TotalAutoTagRules { get; set; }
            [JsonPropertyName("totalAliases")]
            public int TotalAliases { get; set; }
            [JsonPropertyName("totalDescriptions")]
            public int TotalDescriptions { get; set; }
            [JsonPropertyName("averageTagsPerPrompt")]
            public double AverageTagsPerPrompt { get; set; }
            [JsonPropertyName("mostUsedTags")]
            public List<TagUsageInfo>? MostUsedTags { get; set; }
            [JsonPropertyName("leastUsedTags")]
            public List<TagUsageInfo>? LeastUsedTags { get; set; }
            [JsonPropertyName("categoryBreakdown")]
            public Dictionary<string, int>? CategoryBreakdown { get; set; }
            [JsonPropertyName("orphanTags")]
            public List<string>? OrphanTags { get; set; }
            [JsonPropertyName("untaggedPrompts")]
            public List<string>? UntaggedPrompts { get; set; }
        }

        /// <summary>
        /// An auto-tagging rule that maps a pattern to a tag.
        /// </summary>
        public class AutoTagRule
        {
            [JsonPropertyName("pattern")]
            public string Pattern { get; set; } = "";
            [JsonPropertyName("tag")]
            public string Tag { get; set; } = "";
        }

        /// <summary>
        /// Data transfer object for JSON export/import.
        /// </summary>
        private class TagExportData
        {
            [JsonPropertyName("promptTags")]
            public Dictionary<string, List<string>>? PromptTags { get; set; }
            [JsonPropertyName("autoTagRules")]
            public List<AutoTagRule>? AutoTagRules { get; set; }
            [JsonPropertyName("tagDescriptions")]
            public Dictionary<string, string>? TagDescriptions { get; set; }
            [JsonPropertyName("tagAliases")]
            public Dictionary<string, string>? TagAliases { get; set; }
        }
    }
}
