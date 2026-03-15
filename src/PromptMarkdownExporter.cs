namespace Prompt
{
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Exports a <see cref="PromptLibrary"/> to human-readable Markdown and
    /// imports it back. Useful for documentation, sharing prompt catalogs,
    /// and version-controlling prompts in a readable format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Markdown format uses H2 headers for each prompt entry, with
    /// metadata in a front-matter-style block and the template text in a
    /// fenced code block.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var library = new PromptLibrary();
    /// library.Add("summarize",
    ///     new PromptTemplate("Summarize: {{text}}"),
    ///     description: "Summarizes text",
    ///     category: "writing",
    ///     tags: new[] { "summary", "text" });
    ///
    /// // Export to Markdown
    /// string markdown = PromptMarkdownExporter.Export(library);
    /// await File.WriteAllTextAsync("prompts.md", markdown);
    ///
    /// // Import from Markdown
    /// string md = await File.ReadAllTextAsync("prompts.md");
    /// PromptLibrary imported = PromptMarkdownExporter.Import(md);
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptMarkdownExporter
    {
        private static readonly Regex EntryHeaderPattern =
            new(@"^##\s+(.+)$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex MetadataPattern =
            new(@"^\*\*(\w[\w\s]*)\*\*:\s*(.+)$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly Regex DefaultValuePattern =
            new(@"^-\s+`(\w+)`\s*=\s*`(.*)`$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Maximum number of entries allowed during import to prevent
        /// denial-of-service via crafted large documents.
        /// </summary>
        public const int MaxImportEntries = 10_000;

        /// <summary>
        /// Exports a prompt library to a Markdown document.
        /// </summary>
        /// <param name="library">The library to export.</param>
        /// <param name="title">Optional document title. Defaults to "Prompt Library".</param>
        /// <param name="includeMetadata">Whether to include timestamps and variable info.</param>
        /// <returns>A Markdown-formatted string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="library"/> is null.
        /// </exception>
        public static string Export(
            PromptLibrary library,
            string? title = null,
            bool includeMetadata = true)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));

            var sb = new StringBuilder();
            title ??= "Prompt Library";

            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"> Exported: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"> Entries: {library.Count}");
            sb.AppendLine();

            // Table of contents
            if (library.Count > 0)
            {
                sb.AppendLine("## Table of Contents");
                sb.AppendLine();

                var categories = library.Entries
                    .GroupBy(e => e.Category ?? "Uncategorized")
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var group in categories)
                {
                    sb.AppendLine($"### {group.Key}");
                    sb.AppendLine();
                    foreach (var entry in group.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var anchor = entry.Name.ToLowerInvariant().Replace('.', '-');
                        sb.AppendLine($"- [{entry.Name}](#{anchor})" +
                            (entry.Description != null ? $" — {entry.Description}" : ""));
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Entries
            foreach (var entry in library.Entries)
            {
                sb.AppendLine($"## {entry.Name}");
                sb.AppendLine();

                if (entry.Description != null)
                {
                    sb.AppendLine($"*{entry.Description}*");
                    sb.AppendLine();
                }

                if (entry.Category != null)
                    sb.AppendLine($"**Category**: {entry.Category}");

                if (entry.Tags.Count > 0)
                    sb.AppendLine($"**Tags**: {string.Join(", ", entry.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))}");

                var variables = entry.Template.GetVariables();
                if (variables.Count > 0)
                    sb.AppendLine($"**Variables**: {string.Join(", ", variables.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).Select(v => $"`{{{{{v}}}}}`"))}");

                if (includeMetadata)
                {
                    sb.AppendLine($"**Created**: {entry.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                    sb.AppendLine($"**Updated**: {entry.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                }

                sb.AppendLine();

                // Default values
                var defaults = entry.Template.Defaults;
                if (defaults.Count > 0)
                {
                    sb.AppendLine("**Defaults**:");
                    sb.AppendLine();
                    foreach (var kvp in defaults.OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"- `{kvp.Key}` = `{kvp.Value}`");
                    }
                    sb.AppendLine();
                }

                // Template text
                sb.AppendLine("```");
                sb.AppendLine(entry.Template.Template);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd() + "\n";
        }

        /// <summary>
        /// Exports a prompt library to a Markdown file.
        /// </summary>
        /// <param name="library">The library to export.</param>
        /// <param name="filePath">Path to write the Markdown file.</param>
        /// <param name="title">Optional document title.</param>
        /// <param name="includeMetadata">Whether to include timestamps and variable info.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="library"/> or <paramref name="filePath"/> is null.
        /// </exception>
        public static async Task ExportToFileAsync(
            PromptLibrary library,
            string filePath,
            string? title = null,
            bool includeMetadata = true)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var markdown = Export(library, title, includeMetadata);
            await File.WriteAllTextAsync(filePath, markdown);
        }

        /// <summary>
        /// Imports a prompt library from a Markdown document.
        /// </summary>
        /// <param name="markdown">The Markdown content to parse.</param>
        /// <returns>A new <see cref="PromptLibrary"/> with the parsed entries.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="markdown"/> is null or empty.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the Markdown format is invalid or unparseable.
        /// </exception>
        public static PromptLibrary Import(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                throw new ArgumentException(
                    "Markdown content cannot be null or empty.", nameof(markdown));

            var library = new PromptLibrary();
            // Normalize line endings to \n for consistent parsing
            markdown = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = markdown.Split('\n');

            string? currentName = null;
            string? currentDescription = null;
            string? currentCategory = null;
            var currentTags = new List<string>();
            var currentDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool inCodeBlock = false;
            var templateBuilder = new StringBuilder();
            int entryCount = 0;

            void FlushEntry()
            {
                if (currentName == null) return;

                entryCount++;
                if (entryCount > MaxImportEntries)
                    throw new FormatException(
                        $"Markdown contains more than {MaxImportEntries} entries. " +
                        "This exceeds the safety limit.");

                var templateText = templateBuilder.ToString().TrimEnd('\r', '\n');
                if (string.IsNullOrWhiteSpace(templateText))
                    return; // Skip entries without templates

                var defaults = currentDefaults.Count > 0
                    ? new Dictionary<string, string>(currentDefaults)
                    : null;

                var template = new PromptTemplate(templateText, defaults);
                library.Add(
                    currentName,
                    template,
                    description: currentDescription,
                    category: currentCategory,
                    tags: currentTags.Count > 0 ? currentTags.ToArray() : null);

                // Reset
                currentName = null;
                currentDescription = null;
                currentCategory = null;
                currentTags.Clear();
                currentDefaults.Clear();
                templateBuilder.Clear();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                // Check for code block toggle
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        inCodeBlock = false;
                        continue;
                    }
                    else if (currentName != null)
                    {
                        inCodeBlock = true;
                        continue;
                    }
                }

                // Inside a code block = template content
                if (inCodeBlock)
                {
                    if (templateBuilder.Length > 0)
                        templateBuilder.Append('\n');
                    templateBuilder.Append(line);
                    continue;
                }

                // H2 header = new entry
                var headerMatch = EntryHeaderPattern.Match(line);
                if (headerMatch.Success)
                {
                    // Skip "Table of Contents" header
                    var headerText = headerMatch.Groups[1].Value.Trim();
                    if (string.Equals(headerText, "Table of Contents", StringComparison.OrdinalIgnoreCase))
                        continue;

                    FlushEntry();
                    currentName = headerText;
                    continue;
                }

                if (currentName == null) continue;

                // Italic description line
                if (line.StartsWith("*") && line.EndsWith("*") && !line.StartsWith("**"))
                {
                    currentDescription = line.Trim('*').Trim();
                    continue;
                }

                // Metadata lines
                var metaMatch = MetadataPattern.Match(line);
                if (metaMatch.Success)
                {
                    var key = metaMatch.Groups[1].Value.Trim();
                    var value = metaMatch.Groups[2].Value.Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "category":
                            currentCategory = value;
                            break;
                        case "tags":
                            currentTags.AddRange(
                                value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0));
                            break;
                        // Skip Created, Updated, Variables — they're informational
                    }
                    continue;
                }

                // Default value lines
                var defaultMatch = DefaultValuePattern.Match(line);
                if (defaultMatch.Success)
                {
                    currentDefaults[defaultMatch.Groups[1].Value] = defaultMatch.Groups[2].Value;
                    continue;
                }
            }

            // Flush last entry
            FlushEntry();

            return library;
        }

        /// <summary>
        /// Imports a prompt library from a Markdown file.
        /// </summary>
        /// <param name="filePath">Path to the Markdown file.</param>
        /// <returns>A new <see cref="PromptLibrary"/> with the parsed entries.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="filePath"/> is null.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the file does not exist.
        /// </exception>
        public static async Task<PromptLibrary> ImportFromFileAsync(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Markdown file not found: {filePath}", filePath);

            var markdown = await File.ReadAllTextAsync(filePath);
            return Import(markdown);
        }

        /// <summary>
        /// Exports a single <see cref="PromptEntry"/> to a Markdown snippet.
        /// </summary>
        /// <param name="entry">The entry to export.</param>
        /// <returns>A Markdown-formatted string for the single entry.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="entry"/> is null.
        /// </exception>
        public static string ExportEntry(PromptEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var sb = new StringBuilder();
            sb.AppendLine($"## {entry.Name}");
            sb.AppendLine();

            if (entry.Description != null)
            {
                sb.AppendLine($"*{entry.Description}*");
                sb.AppendLine();
            }

            if (entry.Category != null)
                sb.AppendLine($"**Category**: {entry.Category}");

            if (entry.Tags.Count > 0)
                sb.AppendLine($"**Tags**: {string.Join(", ", entry.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))}");

            var variables = entry.Template.GetVariables();
            if (variables.Count > 0)
                sb.AppendLine($"**Variables**: {string.Join(", ", variables.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).Select(v => $"`{{{{{v}}}}}`"))}");

            sb.AppendLine();

            var defaults = entry.Template.Defaults;
            if (defaults.Count > 0)
            {
                sb.AppendLine("**Defaults**:");
                sb.AppendLine();
                foreach (var kvp in defaults.OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"- `{kvp.Key}` = `{kvp.Value}`");
                }
                sb.AppendLine();
            }

            sb.AppendLine("```");
            sb.AppendLine(entry.Template.Template);
            sb.AppendLine("```");

            return sb.ToString().TrimEnd() + "\n";
        }
    }
}
