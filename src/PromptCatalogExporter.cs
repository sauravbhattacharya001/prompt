namespace Prompt
{
    using System.Text;
    using System.Text.Json;
    using System.Web;

    /// <summary>
    /// Exports a <see cref="PromptLibrary"/> to self-contained static HTML,
    /// JSON, or CSV for browsing, sharing, and embedding in documentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The HTML export produces a single-file page with built-in search,
    /// category filtering, and a responsive card layout — no external
    /// dependencies required.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var library = new PromptLibrary();
    /// library.Add("code-review",
    ///     new PromptTemplate("Review this {{language}} code:\n{{code}}"),
    ///     description: "Reviews code quality",
    ///     category: "coding",
    ///     tags: new[] { "review", "quality" });
    ///
    /// var exporter = new PromptCatalogExporter(library);
    ///
    /// // Export as HTML
    /// string html = exporter.ToHtml("My Prompt Catalog");
    /// File.WriteAllText("catalog.html", html);
    ///
    /// // Export as CSV
    /// string csv = exporter.ToCsv();
    /// File.WriteAllText("catalog.csv", csv);
    ///
    /// // Export as JSON (compact, shareable)
    /// string json = exporter.ToJson(indented: true);
    /// File.WriteAllText("catalog.json", json);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptCatalogExporter
    {
        private readonly PromptLibrary _library;

        /// <summary>
        /// Creates a new catalog exporter for the given library.
        /// </summary>
        /// <param name="library">The prompt library to export.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="library"/> is null.
        /// </exception>
        public PromptCatalogExporter(PromptLibrary library)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        /// <summary>
        /// Exports the library as a self-contained HTML page with search,
        /// category filtering, and responsive card layout.
        /// </summary>
        /// <param name="title">Page title. Defaults to "Prompt Catalog".</param>
        /// <param name="darkMode">
        /// When <c>true</c>, generates a dark-themed page. Default is <c>false</c>.
        /// </param>
        /// <returns>A complete HTML document as a string.</returns>
        public string ToHtml(string? title = null, bool darkMode = false)
        {
            title ??= "Prompt Catalog";
            var entries = _library.Entries.ToList();
            var categories = entries
                .Where(e => !string.IsNullOrEmpty(e.Category))
                .Select(e => e.Category!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"<title>{Enc(title)}</title>");
            sb.AppendLine("<style>");
            AppendCss(sb, darkMode);
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine($"<header><h1>{Enc(title)}</h1>");
            sb.AppendLine($"<p class=\"subtitle\">{entries.Count} prompt{(entries.Count == 1 ? "" : "s")} &middot; ");
            sb.AppendLine($"{categories.Count} categor{(categories.Count == 1 ? "y" : "ies")}</p>");
            sb.AppendLine("</header>");

            // Controls
            sb.AppendLine("<div class=\"controls\">");
            sb.AppendLine("<input type=\"text\" id=\"search\" placeholder=\"Search prompts...\" />");
            sb.AppendLine("<select id=\"category-filter\"><option value=\"\">All Categories</option>");
            foreach (var cat in categories)
                sb.AppendLine($"<option value=\"{Enc(cat)}\">{Enc(cat)}</option>");
            sb.AppendLine("</select>");
            sb.AppendLine("</div>");

            // Cards
            sb.AppendLine("<div class=\"grid\" id=\"grid\">");
            foreach (var entry in entries)
            {
                var vars = entry.Template.GetVariables();
                sb.AppendLine($"<div class=\"card\" data-category=\"{Enc(entry.Category ?? "")}\" ");
                sb.AppendLine($"data-name=\"{Enc(entry.Name.ToLowerInvariant())}\" ");
                sb.AppendLine($"data-tags=\"{Enc(string.Join(" ", entry.Tags).ToLowerInvariant())}\" ");
                sb.AppendLine($"data-desc=\"{Enc((entry.Description ?? "").ToLowerInvariant())}\">");
                sb.AppendLine($"<h3 class=\"card-title\">{Enc(entry.Name)}</h3>");

                if (!string.IsNullOrEmpty(entry.Category))
                    sb.AppendLine($"<span class=\"badge category\">{Enc(entry.Category)}</span>");

                if (!string.IsNullOrEmpty(entry.Description))
                    sb.AppendLine($"<p class=\"desc\">{Enc(entry.Description)}</p>");

                // Template preview
                sb.AppendLine("<pre class=\"template\">" +
                    Enc(Truncate(entry.Template.Template, 300)) + "</pre>");

                // Variables
                if (vars.Any())
                {
                    sb.AppendLine("<div class=\"vars\">");
                    sb.AppendLine("<span class=\"vars-label\">Variables:</span> ");
                    foreach (var v in vars)
                        sb.AppendLine($"<code>{Enc(v)}</code> ");
                    sb.AppendLine("</div>");
                }

                // Tags
                if (entry.Tags.Any())
                {
                    sb.AppendLine("<div class=\"tags\">");
                    foreach (var tag in entry.Tags.OrderBy(t => t))
                        sb.AppendLine($"<span class=\"badge tag\">{Enc(tag)}</span>");
                    sb.AppendLine("</div>");
                }

                sb.AppendLine($"<div class=\"meta\">Created: {entry.CreatedAt:yyyy-MM-dd}</div>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");

            // No results message
            sb.AppendLine("<p id=\"no-results\" class=\"no-results\" style=\"display:none\">No prompts match your search.</p>");

            // Script
            sb.AppendLine("<script>");
            AppendJs(sb);
            sb.AppendLine("</script>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// Exports the library as CSV with columns:
        /// Name, Category, Description, Tags, Variables, Template, CreatedAt.
        /// </summary>
        /// <returns>CSV content as a string.</returns>
        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,Category,Description,Tags,Variables,Template,CreatedAt");

            foreach (var entry in _library.Entries)
            {
                var vars = entry.Template.GetVariables();
                sb.Append(CsvField(entry.Name)).Append(',');
                sb.Append(CsvField(entry.Category ?? "")).Append(',');
                sb.Append(CsvField(entry.Description ?? "")).Append(',');
                sb.Append(CsvField(string.Join("; ", entry.Tags))).Append(',');
                sb.Append(CsvField(string.Join("; ", vars))).Append(',');
                sb.Append(CsvField(entry.Template.Template)).Append(',');
                sb.AppendLine(entry.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the library as a JSON array of catalog entries.
        /// </summary>
        /// <param name="indented">
        /// When <c>true</c>, formats with indentation. Default is <c>false</c>.
        /// </param>
        /// <returns>JSON string.</returns>
        public string ToJson(bool indented = false)
        {
            var entries = _library.Entries.Select(e => new
            {
                name = e.Name,
                category = e.Category ?? "",
                description = e.Description ?? "",
                tags = e.Tags.OrderBy(t => t).ToArray(),
                variables = e.Template.GetVariables().ToArray(),
                template = e.Template.Template,
                createdAt = e.CreatedAt.ToString("o"),
                updatedAt = e.UpdatedAt.ToString("o")
            });

            return JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Writes the HTML catalog to a file.
        /// </summary>
        /// <param name="path">Output file path.</param>
        /// <param name="title">Optional page title.</param>
        /// <param name="darkMode">Whether to use dark theme.</param>
        public void SaveHtml(string path, string? title = null, bool darkMode = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            path = Path.GetFullPath(path);
            File.WriteAllText(path, ToHtml(title, darkMode), Encoding.UTF8);
        }

        /// <summary>
        /// Writes the CSV catalog to a file.
        /// </summary>
        /// <param name="path">Output file path.</param>
        public void SaveCsv(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            path = Path.GetFullPath(path);
            File.WriteAllText(path, ToCsv(), Encoding.UTF8);
        }

        /// <summary>
        /// Writes the JSON catalog to a file.
        /// </summary>
        /// <param name="path">Output file path.</param>
        /// <param name="indented">Whether to indent the JSON.</param>
        public void SaveJson(string path, bool indented = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            path = Path.GetFullPath(path);
            File.WriteAllText(path, ToJson(indented), Encoding.UTF8);
        }

        // ── Helpers ───────────────────────────────────────────────

        private static string Enc(string text) => HttpUtility.HtmlEncode(text);

        private static string CsvField(string value)
        {
            if (value.Contains('"') || value.Contains(',') ||
                value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        private static string Truncate(string text, int maxLength)
        {
            if (text.Length <= maxLength) return text;
            return text[..maxLength] + "...";
        }

        private static void AppendCss(StringBuilder sb, bool darkMode)
        {
            var bg = darkMode ? "#1a1a2e" : "#f5f7fa";
            var fg = darkMode ? "#e0e0e0" : "#333";
            var cardBg = darkMode ? "#16213e" : "#fff";
            var cardBorder = darkMode ? "#0f3460" : "#e1e5ea";
            var accent = darkMode ? "#e94560" : "#4a90d9";
            var codeBg = darkMode ? "#0f3460" : "#f0f4f8";
            var inputBg = darkMode ? "#16213e" : "#fff";
            var inputBorder = darkMode ? "#0f3460" : "#ccc";

            sb.AppendLine($@"
* {{ box-sizing: border-box; margin: 0; padding: 0; }}
body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: {bg}; color: {fg}; padding: 2rem; max-width: 1200px; margin: 0 auto; }}
header {{ text-align: center; margin-bottom: 2rem; }}
h1 {{ font-size: 2rem; margin-bottom: 0.25rem; }}
.subtitle {{ color: #888; font-size: 0.95rem; }}
.controls {{ display: flex; gap: 1rem; margin-bottom: 1.5rem; flex-wrap: wrap; }}
#search {{ flex: 1; min-width: 200px; padding: 0.6rem 1rem; border: 1px solid {inputBorder};
  border-radius: 6px; font-size: 0.95rem; background: {inputBg}; color: {fg}; }}
#category-filter {{ padding: 0.6rem; border: 1px solid {inputBorder}; border-radius: 6px;
  font-size: 0.95rem; background: {inputBg}; color: {fg}; }}
.grid {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: 1.25rem; }}
.card {{ background: {cardBg}; border: 1px solid {cardBorder}; border-radius: 10px;
  padding: 1.25rem; transition: box-shadow 0.2s; }}
.card:hover {{ box-shadow: 0 4px 16px rgba(0,0,0,0.12); }}
.card-title {{ font-size: 1.15rem; margin-bottom: 0.5rem; color: {accent}; }}
.desc {{ font-size: 0.9rem; margin: 0.5rem 0; color: #666; }}
.template {{ background: {codeBg}; padding: 0.75rem; border-radius: 6px; font-size: 0.8rem;
  overflow-x: auto; white-space: pre-wrap; word-break: break-word; margin: 0.5rem 0;
  max-height: 120px; overflow-y: auto; }}
.vars {{ margin: 0.5rem 0; font-size: 0.85rem; }}
.vars-label {{ font-weight: 600; }}
.vars code {{ background: {codeBg}; padding: 0.15rem 0.4rem; border-radius: 3px;
  font-size: 0.8rem; margin-right: 0.25rem; }}
.badge {{ display: inline-block; padding: 0.15rem 0.55rem; border-radius: 12px;
  font-size: 0.75rem; font-weight: 500; margin-right: 0.3rem; }}
.badge.category {{ background: {accent}; color: #fff; }}
.badge.tag {{ background: {codeBg}; color: {fg}; border: 1px solid {cardBorder}; }}
.tags {{ margin-top: 0.5rem; }}
.meta {{ font-size: 0.75rem; color: #999; margin-top: 0.75rem; }}
.no-results {{ text-align: center; color: #888; font-size: 1.1rem; padding: 2rem; }}
@media (max-width: 600px) {{
  body {{ padding: 1rem; }}
  .grid {{ grid-template-columns: 1fr; }}
}}
");
        }

        private static void AppendJs(StringBuilder sb)
        {
            sb.AppendLine(@"
(function() {
  var search = document.getElementById('search');
  var filter = document.getElementById('category-filter');
  var grid = document.getElementById('grid');
  var noResults = document.getElementById('no-results');
  var cards = Array.from(grid.querySelectorAll('.card'));

  function applyFilter() {
    var q = search.value.toLowerCase().trim();
    var cat = filter.value;
    var visible = 0;
    cards.forEach(function(card) {
      var matchCat = !cat || card.dataset.category === cat;
      var matchSearch = !q ||
        card.dataset.name.indexOf(q) !== -1 ||
        card.dataset.tags.indexOf(q) !== -1 ||
        card.dataset.desc.indexOf(q) !== -1;
      var show = matchCat && matchSearch;
      card.style.display = show ? '' : 'none';
      if (show) visible++;
    });
    noResults.style.display = visible === 0 ? '' : 'none';
  }

  search.addEventListener('input', applyFilter);
  filter.addEventListener('change', applyFilter);
})();
");
        }
    }
}
