namespace Prompt
{
    using System.Text;

    /// <summary>
    /// Output format for changelog generation.
    /// </summary>
    public enum ChangelogFormat
    {
        /// <summary>Markdown format with headers and code blocks.</summary>
        Markdown,
        /// <summary>Plain text format.</summary>
        PlainText,
        /// <summary>HTML format with styled elements.</summary>
        Html
    }

    /// <summary>
    /// Options for controlling changelog output.
    /// </summary>
    public class ChangelogOptions
    {
        /// <summary>Output format. Default: Markdown.</summary>
        public ChangelogFormat Format { get; set; } = ChangelogFormat.Markdown;

        /// <summary>Include diff details between consecutive versions. Default: true.</summary>
        public bool IncludeDiffs { get; set; } = true;

        /// <summary>Include full template text for each version. Default: false.</summary>
        public bool IncludeFullText { get; set; }

        /// <summary>Maximum number of versions to include (0 = all). Default: 0.</summary>
        public int MaxVersions { get; set; }

        /// <summary>Filter to only show versions by this author. Default: null (all authors).</summary>
        public string? AuthorFilter { get; set; }

        /// <summary>Only include versions created on or after this date. Default: null.</summary>
        public DateTimeOffset? Since { get; set; }

        /// <summary>Only include versions created on or before this date. Default: null.</summary>
        public DateTimeOffset? Until { get; set; }

        /// <summary>Show newest versions first. Default: true.</summary>
        public bool ReverseChronological { get; set; } = true;

        /// <summary>Title for the changelog document. Default: null (auto-generated).</summary>
        public string? Title { get; set; }
    }

    /// <summary>
    /// Statistics summary for a changelog.
    /// </summary>
    public class ChangelogStats
    {
        /// <summary>Total number of versions in the changelog.</summary>
        public int TotalVersions { get; set; }

        /// <summary>Number of unique authors.</summary>
        public int UniqueAuthors { get; set; }

        /// <summary>List of unique author names.</summary>
        public IReadOnlyList<string> Authors { get; set; } = Array.Empty<string>();

        /// <summary>Date of the earliest version.</summary>
        public DateTimeOffset? FirstVersionDate { get; set; }

        /// <summary>Date of the latest version.</summary>
        public DateTimeOffset? LastVersionDate { get; set; }

        /// <summary>Total lines added across all diffs.</summary>
        public int TotalLinesAdded { get; set; }

        /// <summary>Total lines removed across all diffs.</summary>
        public int TotalLinesRemoved { get; set; }

        /// <summary>Number of versions with text changes.</summary>
        public int VersionsWithChanges { get; set; }

        /// <summary>Number of versions that were rollbacks.</summary>
        public int RollbackCount { get; set; }
    }

    /// <summary>
    /// Generates formatted changelogs from prompt version history.
    /// Supports Markdown, plain text, and HTML output with configurable
    /// filtering, diff inclusion, and statistics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var vm = new PromptVersionManager();
    /// vm.CreateVersion("greeting", "Hello {{name}}", "Initial version", "Alice");
    /// vm.CreateVersion("greeting", "Hello {{name}}! Welcome.", "Added welcome", "Bob");
    ///
    /// var generator = new PromptChangelogGenerator(vm);
    ///
    /// // Generate Markdown changelog for one template
    /// string md = generator.Generate("greeting");
    ///
    /// // Generate HTML changelog with options
    /// string html = generator.Generate("greeting", new ChangelogOptions
    /// {
    ///     Format = ChangelogFormat.Html,
    ///     IncludeDiffs = true,
    ///     Since = DateTimeOffset.UtcNow.AddDays(-7)
    /// });
    ///
    /// // Generate changelog for all templates
    /// string all = generator.GenerateAll();
    ///
    /// // Get statistics
    /// var stats = generator.GetStats("greeting");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptChangelogGenerator
    {
        private readonly PromptVersionManager _versionManager;

        /// <summary>
        /// Creates a new changelog generator backed by a version manager.
        /// </summary>
        /// <param name="versionManager">The version manager containing prompt history.</param>
        /// <exception cref="ArgumentNullException">Thrown when versionManager is null.</exception>
        public PromptChangelogGenerator(PromptVersionManager versionManager)
        {
            _versionManager = versionManager
                ?? throw new ArgumentNullException(nameof(versionManager));
        }

        /// <summary>
        /// Generate a changelog for a single template.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <param name="options">Optional formatting and filter options.</param>
        /// <returns>Formatted changelog string.</returns>
        /// <exception cref="ArgumentException">Thrown when templateName is null or empty.</exception>
        public string Generate(string templateName, ChangelogOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException(
                    "Template name cannot be null or empty.", nameof(templateName));

            options ??= new ChangelogOptions();
            var versions = FilterVersions(
                _versionManager.GetHistory(templateName), options);

            return options.Format switch
            {
                ChangelogFormat.Markdown => GenerateMarkdown(templateName, versions, options),
                ChangelogFormat.PlainText => GeneratePlainText(templateName, versions, options),
                ChangelogFormat.Html => GenerateHtml(templateName, versions, options),
                _ => GenerateMarkdown(templateName, versions, options)
            };
        }

        /// <summary>
        /// Generate a combined changelog for all tracked templates.
        /// </summary>
        /// <param name="options">Optional formatting and filter options.</param>
        /// <returns>Formatted changelog string covering all templates.</returns>
        public string GenerateAll(ChangelogOptions? options = null)
        {
            options ??= new ChangelogOptions();
            var templates = _versionManager.GetTrackedTemplates();

            if (templates.Count == 0)
                return FormatEmptyChangelog(options);

            var sb = new StringBuilder();
            var title = options.Title ?? "Prompt Changelog — All Templates";

            switch (options.Format)
            {
                case ChangelogFormat.Html:
                    sb.AppendLine("<!DOCTYPE html><html><head>");
                    sb.AppendLine($"<title>{HtmlEncode(title)}</title>");
                    sb.AppendLine(GetHtmlStyles());
                    sb.AppendLine("</head><body>");
                    sb.AppendLine($"<h1>{HtmlEncode(title)}</h1>");
                    sb.AppendLine($"<p class=\"meta\">{templates.Count} template(s) tracked</p>");
                    break;
                case ChangelogFormat.Markdown:
                    sb.AppendLine($"# {title}");
                    sb.AppendLine();
                    sb.AppendLine($"*{templates.Count} template(s) tracked*");
                    sb.AppendLine();
                    break;
                case ChangelogFormat.PlainText:
                    sb.AppendLine(title.ToUpperInvariant());
                    sb.AppendLine(new string('=', title.Length));
                    sb.AppendLine($"{templates.Count} template(s) tracked");
                    sb.AppendLine();
                    break;
            }

            foreach (var tmpl in templates)
            {
                var versions = FilterVersions(
                    _versionManager.GetHistory(tmpl), options);
                if (versions.Count == 0) continue;

                switch (options.Format)
                {
                    case ChangelogFormat.Html:
                        sb.AppendLine("<hr/>");
                        sb.Append(GenerateHtmlBody(tmpl, versions, options));
                        break;
                    case ChangelogFormat.Markdown:
                        sb.AppendLine("---");
                        sb.AppendLine();
                        sb.Append(GenerateMarkdownBody(tmpl, versions, options));
                        break;
                    case ChangelogFormat.PlainText:
                        sb.AppendLine(new string('-', 60));
                        sb.Append(GeneratePlainTextBody(tmpl, versions, options));
                        break;
                }
            }

            if (options.Format == ChangelogFormat.Html)
                sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        /// <summary>
        /// Get statistics for a template's version history.
        /// </summary>
        /// <param name="templateName">The template name.</param>
        /// <param name="options">Optional filter options (author, date range).</param>
        /// <returns>Statistics about the template's changelog.</returns>
        public ChangelogStats GetStats(string templateName, ChangelogOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return new ChangelogStats();

            options ??= new ChangelogOptions();
            var versions = FilterVersions(
                _versionManager.GetHistory(templateName), options);

            return ComputeStats(templateName, versions);
        }

        /// <summary>
        /// Get aggregate statistics across all tracked templates.
        /// </summary>
        /// <param name="options">Optional filter options.</param>
        /// <returns>Aggregate statistics.</returns>
        public ChangelogStats GetAllStats(ChangelogOptions? options = null)
        {
            options ??= new ChangelogOptions();
            var allVersions = new List<PromptVersion>();

            foreach (var tmpl in _versionManager.GetTrackedTemplates())
            {
                allVersions.AddRange(FilterVersions(
                    _versionManager.GetHistory(tmpl), options));
            }

            return ComputeStats("(all)", allVersions.AsReadOnly());
        }

        // --- Filtering ---

        private IReadOnlyList<PromptVersion> FilterVersions(
            IReadOnlyList<PromptVersion> versions, ChangelogOptions options)
        {
            IEnumerable<PromptVersion> filtered = versions;

            if (!string.IsNullOrWhiteSpace(options.AuthorFilter))
                filtered = filtered.Where(v =>
                    string.Equals(v.Author, options.AuthorFilter,
                        StringComparison.OrdinalIgnoreCase));

            if (options.Since.HasValue)
                filtered = filtered.Where(v => v.CreatedAt >= options.Since.Value);

            if (options.Until.HasValue)
                filtered = filtered.Where(v => v.CreatedAt <= options.Until.Value);

            var list = options.ReverseChronological
                ? filtered.OrderByDescending(v => v.VersionNumber).ToList()
                : filtered.OrderBy(v => v.VersionNumber).ToList();

            if (options.MaxVersions > 0 && list.Count > options.MaxVersions)
                list = list.Take(options.MaxVersions).ToList();

            return list.AsReadOnly();
        }

        // --- Markdown ---

        private string GenerateMarkdown(
            string templateName, IReadOnlyList<PromptVersion> versions,
            ChangelogOptions options)
        {
            var sb = new StringBuilder();
            var title = options.Title ?? $"Changelog: {templateName}";
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.Append(GenerateMarkdownBody(templateName, versions, options));
            return sb.ToString();
        }

        private string GenerateMarkdownBody(
            string templateName, IReadOnlyList<PromptVersion> versions,
            ChangelogOptions options)
        {
            var sb = new StringBuilder();

            if (versions.Count == 0)
            {
                sb.AppendLine("*No versions found.*");
                sb.AppendLine();
                return sb.ToString();
            }

            sb.AppendLine($"## {templateName}");
            sb.AppendLine();
            sb.AppendLine($"*{versions.Count} version(s)*");
            sb.AppendLine();

            // Get all versions ordered chronologically for diff lookups
            var allVersions = _versionManager.GetHistory(templateName);

            foreach (var v in versions)
            {
                sb.AppendLine($"### v{v.VersionNumber}");
                sb.AppendLine();
                sb.Append($"- **Date:** {v.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine();
                if (!string.IsNullOrEmpty(v.Author))
                    sb.AppendLine($"- **Author:** {v.Author}");
                if (!string.IsNullOrEmpty(v.Description))
                    sb.AppendLine($"- **Description:** {v.Description}");

                if (options.IncludeDiffs && v.VersionNumber > 1)
                {
                    var prevVersion = allVersions.LastOrDefault(
                        pv => pv.VersionNumber < v.VersionNumber);
                    if (prevVersion != null)
                    {
                        try
                        {
                            var diff = _versionManager.Compare(
                                templateName, prevVersion.VersionNumber, v.VersionNumber);
                            sb.AppendLine($"- **Changes:** {diff.GetSummary()}");

                            if (diff.AddedLines.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine("**Added:**");
                                sb.AppendLine("```");
                                foreach (var line in diff.AddedLines.Take(10))
                                    sb.AppendLine($"+ {line}");
                                if (diff.AddedLines.Count > 10)
                                    sb.AppendLine($"... and {diff.AddedLines.Count - 10} more");
                                sb.AppendLine("```");
                            }

                            if (diff.RemovedLines.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine("**Removed:**");
                                sb.AppendLine("```");
                                foreach (var line in diff.RemovedLines.Take(10))
                                    sb.AppendLine($"- {line}");
                                if (diff.RemovedLines.Count > 10)
                                    sb.AppendLine($"... and {diff.RemovedLines.Count - 10} more");
                                sb.AppendLine("```");
                            }
                        }
                        catch { /* skip diff if comparison fails */ }
                    }
                }

                if (options.IncludeFullText)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Template:**");
                    sb.AppendLine("```");
                    sb.AppendLine(v.TemplateText);
                    sb.AppendLine("```");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // --- Plain Text ---

        private string GeneratePlainText(
            string templateName, IReadOnlyList<PromptVersion> versions,
            ChangelogOptions options)
        {
            var sb = new StringBuilder();
            var title = options.Title ?? $"Changelog: {templateName}";
            sb.AppendLine(title.ToUpperInvariant());
            sb.AppendLine(new string('=', title.Length));
            sb.AppendLine();
            sb.Append(GeneratePlainTextBody(templateName, versions, options));
            return sb.ToString();
        }

        private string GeneratePlainTextBody(
            string templateName, IReadOnlyList<PromptVersion> versions,
            ChangelogOptions options)
        {
            var sb = new StringBuilder();

            if (versions.Count == 0)
            {
                sb.AppendLine("No versions found.");
                sb.AppendLine();
                return sb.ToString();
            }

            sb.AppendLine($"[{templateName}] ({versions.Count} versions)");
            sb.AppendLine();

            var allVersions = _versionManager.GetHistory(templateName);

            foreach (var v in versions)
            {
                sb.AppendLine($"  v{v.VersionNumber} | {v.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                if (!string.IsNullOrEmpty(v.Author))
                    sb.AppendLine($"    Author: {v.Author}");
                if (!string.IsNullOrEmpty(v.Description))
                    sb.AppendLine($"    {v.Description}");

                if (options.IncludeDiffs && v.VersionNumber > 1)
                {
                    var prev = allVersions.LastOrDefault(
                        pv => pv.VersionNumber < v.VersionNumber);
                    if (prev != null)
                    {
                        try
                        {
                            var diff = _versionManager.Compare(
                                templateName, prev.VersionNumber, v.VersionNumber);
                            sb.AppendLine($"    Changes: {diff.GetSummary()}");
                        }
                        catch { }
                    }
                }

                if (options.IncludeFullText)
                {
                    sb.AppendLine($"    Text: {v.TemplateText}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // --- HTML ---

        private string GenerateHtml(
            string templateName, IReadOnlyList<PromptVersion> versions,
            ChangelogOptions options)
        {
            var sb = new StringBuilder();
            var title = options.Title ?? $"Changelog: {templateName}";
            sb.AppendLine("<!DOCTYPE html><html><head>");
            sb.AppendLine($"<title>{HtmlEncode(title)}</title>");
            sb.AppendLine(GetHtmlStyles());
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h1>{HtmlEncode(title)}</h1>");
            sb.Append(GenerateHtmlBody(templateName, versions, options));
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GenerateHtmlBody(
            string templateName, IReadOnlyList<PromptVersion> versions,
            ChangelogOptions options)
        {
            var sb = new StringBuilder();

            if (versions.Count == 0)
            {
                sb.AppendLine("<p><em>No versions found.</em></p>");
                return sb.ToString();
            }

            sb.AppendLine($"<h2>{HtmlEncode(templateName)}</h2>");
            sb.AppendLine($"<p class=\"meta\">{versions.Count} version(s)</p>");

            var allVersions = _versionManager.GetHistory(templateName);

            foreach (var v in versions)
            {
                sb.AppendLine("<div class=\"version\">");
                sb.AppendLine($"<h3>v{v.VersionNumber}</h3>");
                sb.AppendLine("<ul>");
                sb.AppendLine($"<li><strong>Date:</strong> {HtmlEncode(v.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))} UTC</li>");
                if (!string.IsNullOrEmpty(v.Author))
                    sb.AppendLine($"<li><strong>Author:</strong> {HtmlEncode(v.Author)}</li>");
                if (!string.IsNullOrEmpty(v.Description))
                    sb.AppendLine($"<li><strong>Description:</strong> {HtmlEncode(v.Description)}</li>");

                if (options.IncludeDiffs && v.VersionNumber > 1)
                {
                    var prev = allVersions.LastOrDefault(
                        pv => pv.VersionNumber < v.VersionNumber);
                    if (prev != null)
                    {
                        try
                        {
                            var diff = _versionManager.Compare(
                                templateName, prev.VersionNumber, v.VersionNumber);
                            sb.AppendLine($"<li><strong>Changes:</strong> {HtmlEncode(diff.GetSummary())}</li>");

                            if (diff.AddedLines.Count > 0 || diff.RemovedLines.Count > 0)
                            {
                                sb.AppendLine("</ul>");
                                sb.AppendLine("<div class=\"diff\">");
                                foreach (var line in diff.AddedLines.Take(10))
                                    sb.AppendLine($"<div class=\"added\">+ {HtmlEncode(line)}</div>");
                                if (diff.AddedLines.Count > 10)
                                    sb.AppendLine($"<div class=\"meta\">... and {diff.AddedLines.Count - 10} more added</div>");
                                foreach (var line in diff.RemovedLines.Take(10))
                                    sb.AppendLine($"<div class=\"removed\">- {HtmlEncode(line)}</div>");
                                if (diff.RemovedLines.Count > 10)
                                    sb.AppendLine($"<div class=\"meta\">... and {diff.RemovedLines.Count - 10} more removed</div>");
                                sb.AppendLine("</div>");
                                sb.AppendLine("<ul style=\"display:none\">");
                            }
                        }
                        catch { }
                    }
                }

                sb.AppendLine("</ul>");

                if (options.IncludeFullText)
                {
                    sb.AppendLine($"<pre class=\"template\">{HtmlEncode(v.TemplateText)}</pre>");
                }

                sb.AppendLine("</div>");
            }

            return sb.ToString();
        }

        // --- Stats ---

        private ChangelogStats ComputeStats(
            string templateName, IReadOnlyList<PromptVersion> versions)
        {
            var stats = new ChangelogStats
            {
                TotalVersions = versions.Count
            };

            if (versions.Count == 0) return stats;

            var authors = versions
                .Where(v => !string.IsNullOrEmpty(v.Author))
                .Select(v => v.Author!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            stats.UniqueAuthors = authors.Count;
            stats.Authors = authors.AsReadOnly();

            var ordered = versions.OrderBy(v => v.CreatedAt).ToList();
            stats.FirstVersionDate = ordered.First().CreatedAt;
            stats.LastVersionDate = ordered.Last().CreatedAt;

            stats.RollbackCount = versions.Count(v =>
                v.Description != null &&
                v.Description.StartsWith("Rollback to v", StringComparison.OrdinalIgnoreCase));

            // Compute diff stats
            var allVersions = _versionManager.GetHistory(templateName);
            foreach (var v in versions)
            {
                if (v.VersionNumber <= 1) continue;
                var prev = allVersions.LastOrDefault(
                    pv => pv.VersionNumber < v.VersionNumber);
                if (prev == null) continue;

                try
                {
                    var diff = _versionManager.Compare(
                        templateName, prev.VersionNumber, v.VersionNumber);
                    if (diff.HasTextChanges) stats.VersionsWithChanges++;
                    stats.TotalLinesAdded += diff.AddedLineCount;
                    stats.TotalLinesRemoved += diff.RemovedLineCount;
                }
                catch { }
            }

            return stats;
        }

        // --- Helpers ---

        private static string FormatEmptyChangelog(ChangelogOptions options)
        {
            return options.Format switch
            {
                ChangelogFormat.Html =>
                    "<!DOCTYPE html><html><head><title>Changelog</title></head><body><p><em>No templates tracked.</em></p></body></html>",
                ChangelogFormat.Markdown =>
                    "# Changelog\n\n*No templates tracked.*\n",
                _ =>
                    "CHANGELOG\n=========\nNo templates tracked.\n"
            };
        }

        private static string HtmlEncode(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string GetHtmlStyles()
        {
            return @"<style>
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 900px; margin: 0 auto; padding: 2rem; color: #333; }
h1 { color: #1a1a2e; border-bottom: 2px solid #e0e0e0; padding-bottom: 0.5rem; }
h2 { color: #16213e; margin-top: 2rem; }
h3 { color: #0f3460; margin-bottom: 0.5rem; }
.version { border-left: 3px solid #0f3460; padding-left: 1rem; margin-bottom: 1.5rem; }
.meta { color: #666; font-style: italic; }
.diff { background: #f8f9fa; border-radius: 4px; padding: 0.5rem; margin: 0.5rem 0; font-family: monospace; font-size: 0.9em; }
.added { color: #22863a; }
.removed { color: #cb2431; }
.template { background: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 4px; padding: 1rem; overflow-x: auto; }
ul { padding-left: 1.5rem; }
</style>";
        }
    }
}
