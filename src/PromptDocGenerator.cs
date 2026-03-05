namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Describes a variable extracted from a prompt template.
    /// </summary>
    public class DocVariable
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("hasDefault")]
        public bool HasDefault { get; init; }

        [JsonPropertyName("defaultValue")]
        public string? DefaultValue { get; init; }

        [JsonPropertyName("required")]
        public bool Required => !HasDefault;

        [JsonPropertyName("occurrences")]
        public int Occurrences { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Describes a section detected in a prompt (by heading markers).
    /// </summary>
    public class DocSection
    {
        [JsonPropertyName("heading")]
        public string Heading { get; init; } = "";

        [JsonPropertyName("level")]
        public int Level { get; init; }

        [JsonPropertyName("content")]
        public string Content { get; init; } = "";

        [JsonPropertyName("wordCount")]
        public int WordCount { get; init; }

        [JsonPropertyName("variableCount")]
        public int VariableCount { get; init; }
    }

    /// <summary>
    /// Metadata extracted or annotated for a prompt.
    /// </summary>
    public class DocMetadata
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
    }

    /// <summary>
    /// Complete documentation generated for a single prompt.
    /// </summary>
    public class PromptDoc
    {
        [JsonPropertyName("metadata")]
        public DocMetadata Metadata { get; init; } = new();

        [JsonPropertyName("promptText")]
        public string PromptText { get; init; } = "";

        [JsonPropertyName("variables")]
        public IReadOnlyList<DocVariable> Variables { get; init; } = Array.Empty<DocVariable>();

        [JsonPropertyName("sections")]
        public IReadOnlyList<DocSection> Sections { get; init; } = Array.Empty<DocSection>();

        [JsonPropertyName("estimatedTokens")]
        public int EstimatedTokens { get; init; }

        [JsonPropertyName("wordCount")]
        public int WordCount { get; init; }

        [JsonPropertyName("charCount")]
        public int CharCount { get; init; }

        [JsonPropertyName("lineCount")]
        public int LineCount { get; init; }

        [JsonPropertyName("complexity")]
        public string Complexity { get; init; } = "simple";

        [JsonPropertyName("usageExample")]
        public string UsageExample { get; init; } = "";
    }

    /// <summary>
    /// Documentation for a collection of prompts (a catalog).
    /// </summary>
    public class PromptCatalog
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = "Prompt Catalog";

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("prompts")]
        public IReadOnlyList<PromptDoc> Prompts { get; init; } = Array.Empty<PromptDoc>();

        [JsonPropertyName("totalVariables")]
        public int TotalVariables { get; init; }

        [JsonPropertyName("sharedVariables")]
        public IReadOnlyList<string> SharedVariables { get; init; } = Array.Empty<string>();

        [JsonPropertyName("categoryBreakdown")]
        public IReadOnlyDictionary<string, int> CategoryBreakdown { get; init; }
            = new Dictionary<string, int>();
    }

    /// <summary>
    /// Configuration for documentation generation.
    /// </summary>
    public class DocGeneratorOptions
    {
        [JsonPropertyName("includeUsageExamples")]
        public bool IncludeUsageExamples { get; set; } = true;

        [JsonPropertyName("includeTokenEstimates")]
        public bool IncludeTokenEstimates { get; set; } = true;

        [JsonPropertyName("includeComplexityRating")]
        public bool IncludeComplexityRating { get; set; } = true;

        [JsonPropertyName("variableDescriptions")]
        public Dictionary<string, string> VariableDescriptions { get; set; } = new();

        [JsonPropertyName("catalogTitle")]
        public string CatalogTitle { get; set; } = "Prompt Catalog";
    }

    /// <summary>
    /// Generates structured documentation from prompt texts and templates.
    /// Extracts variables, sections, metadata, and produces Markdown docs.
    /// </summary>
    public class PromptDocGenerator
    {
        private static readonly Regex VariablePattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
        private static readonly Regex HeadingPattern = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex MetadataPattern = new(@"^@(\w+)\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex ConditionalPattern = new(@"\{\{#if\s+(\w+)\}\}.*?\{\{/if\}\}", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex LoopPattern = new(@"\{\{#each\s+(\w+)\}\}.*?\{\{/each\}\}", RegexOptions.Singleline | RegexOptions.Compiled);

        private readonly DocGeneratorOptions _options;

        /// <summary>
        /// Initializes a new <see cref="PromptDocGenerator"/> with optional configuration.
        /// </summary>
        public PromptDocGenerator(DocGeneratorOptions? options = null)
        {
            _options = options ?? new DocGeneratorOptions();
        }

        /// <summary>
        /// Extracts variables from a prompt text, including occurrence counts.
        /// </summary>
        public IReadOnlyList<DocVariable> ExtractVariables(string prompt, Dictionary<string, string>? defaults = null)
        {
            if (string.IsNullOrEmpty(prompt)) return Array.Empty<DocVariable>();

            defaults ??= new Dictionary<string, string>();
            var varCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in VariablePattern.Matches(prompt))
            {
                var name = match.Groups[1].Value;
                varCounts[name] = varCounts.GetValueOrDefault(name) + 1;
            }

            return varCounts.Select(kv => new DocVariable
            {
                Name = kv.Key,
                Occurrences = kv.Value,
                HasDefault = defaults.ContainsKey(kv.Key),
                DefaultValue = defaults.GetValueOrDefault(kv.Key),
                Description = _options.VariableDescriptions.GetValueOrDefault(kv.Key, "")
            }).OrderBy(v => v.Name).ToList();
        }

        /// <summary>
        /// Detects sections in a prompt based on markdown headings.
        /// </summary>
        public IReadOnlyList<DocSection> ExtractSections(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return Array.Empty<DocSection>();

            var headings = HeadingPattern.Matches(prompt);
            if (headings.Count == 0)
            {
                // Treat entire prompt as a single section
                return new[]
                {
                    new DocSection
                    {
                        Heading = "(root)",
                        Level = 0,
                        Content = prompt.Trim(),
                        WordCount = CountWords(prompt),
                        VariableCount = VariablePattern.Matches(prompt).Count
                    }
                };
            }

            var sections = new List<DocSection>();
            for (int i = 0; i < headings.Count; i++)
            {
                var match = headings[i];
                int level = match.Groups[1].Value.Length;
                string heading = match.Groups[2].Value.Trim();

                int contentStart = match.Index + match.Length;
                int contentEnd = i + 1 < headings.Count ? headings[i + 1].Index : prompt.Length;
                string content = prompt[contentStart..contentEnd].Trim();

                sections.Add(new DocSection
                {
                    Heading = heading,
                    Level = level,
                    Content = content,
                    WordCount = CountWords(content),
                    VariableCount = VariablePattern.Matches(content).Count
                });
            }

            // Check for content before first heading
            string preamble = prompt[..headings[0].Index].Trim();
            if (!string.IsNullOrEmpty(preamble))
            {
                sections.Insert(0, new DocSection
                {
                    Heading = "(preamble)",
                    Level = 0,
                    Content = preamble,
                    WordCount = CountWords(preamble),
                    VariableCount = VariablePattern.Matches(preamble).Count
                });
            }

            return sections;
        }

        /// <summary>
        /// Extracts metadata annotations from prompt text (lines starting with @key: value).
        /// </summary>
        public DocMetadata ExtractMetadata(string prompt)
        {
            var meta = new DocMetadata();
            if (string.IsNullOrEmpty(prompt)) return meta;

            foreach (Match match in MetadataPattern.Matches(prompt))
            {
                var key = match.Groups[1].Value.ToLowerInvariant();
                var value = match.Groups[2].Value.Trim();

                switch (key)
                {
                    case "title": meta.Title = value; break;
                    case "description": case "desc": meta.Description = value; break;
                    case "author": meta.Author = value; break;
                    case "version": case "ver": meta.Version = value; break;
                    case "model": meta.Model = value; break;
                    case "category": case "cat": meta.Category = value; break;
                    case "tags": case "tag":
                        meta.Tags.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        break;
                }
            }

            return meta;
        }

        /// <summary>
        /// Rates the complexity of a prompt based on variables, sections, conditionals, etc.
        /// </summary>
        public string RateComplexity(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return "simple";

            int variables = VariablePattern.Matches(prompt).Count;
            int sections = HeadingPattern.Matches(prompt).Count;
            int conditionals = ConditionalPattern.Matches(prompt).Count;
            int loops = LoopPattern.Matches(prompt).Count;
            int words = CountWords(prompt);

            int score = 0;
            score += Math.Min(variables, 10);
            score += Math.Min(sections * 2, 10);
            score += conditionals * 3;
            score += loops * 3;
            score += words > 500 ? 5 : words > 200 ? 3 : words > 50 ? 1 : 0;

            return score switch
            {
                <= 3 => "simple",
                <= 8 => "moderate",
                <= 15 => "complex",
                _ => "advanced"
            };
        }

        /// <summary>
        /// Generates a usage example showing how to render the prompt with sample values.
        /// </summary>
        public string GenerateUsageExample(string prompt, Dictionary<string, string>? defaults = null)
        {
            if (string.IsNullOrEmpty(prompt)) return "";

            var variables = ExtractVariables(prompt, defaults);
            if (variables.Count == 0) return "// No variables — use the prompt text directly.";

            var lines = new List<string>
            {
                "var template = new PromptTemplate(",
                $"    \"{EscapeForCode(Truncate(prompt, 60))}\",",
            };

            var defaultVars = variables.Where(v => v.HasDefault).ToList();
            if (defaultVars.Count > 0)
            {
                lines.Add("    new Dictionary<string, string>");
                lines.Add("    {");
                foreach (var v in defaultVars)
                    lines.Add($"        [\"{v.Name}\"] = \"{EscapeForCode(v.DefaultValue ?? "")}\",");
                lines.Add("    }");
            }

            lines.Add(");");
            lines.Add("");

            var requiredVars = variables.Where(v => v.Required).ToList();
            if (requiredVars.Count > 0)
            {
                lines.Add("string result = template.Render(new Dictionary<string, string>");
                lines.Add("{");
                foreach (var v in requiredVars)
                    lines.Add($"    [\"{v.Name}\"] = \"<{v.Name} value>\",");
                lines.Add("});");
            }
            else
            {
                lines.Add("string result = template.Render();");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Generates complete documentation for a single prompt.
        /// </summary>
        public PromptDoc GenerateDoc(string prompt, DocMetadata? metadata = null, Dictionary<string, string>? defaults = null)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            var meta = metadata ?? ExtractMetadata(prompt);
            var variables = ExtractVariables(prompt, defaults);
            var sections = ExtractSections(prompt);

            return new PromptDoc
            {
                Metadata = meta,
                PromptText = prompt,
                Variables = variables,
                Sections = sections,
                EstimatedTokens = _options.IncludeTokenEstimates ? EstimateTokens(prompt) : 0,
                WordCount = CountWords(prompt),
                CharCount = prompt.Length,
                LineCount = prompt.Split('\n').Length,
                Complexity = _options.IncludeComplexityRating ? RateComplexity(prompt) : "",
                UsageExample = _options.IncludeUsageExamples ? GenerateUsageExample(prompt, defaults) : ""
            };
        }

        /// <summary>
        /// Generates a catalog of documentation for multiple prompts.
        /// </summary>
        public PromptCatalog GenerateCatalog(IEnumerable<(string prompt, DocMetadata? metadata, Dictionary<string, string>? defaults)> prompts)
        {
            var docs = prompts.Select(p => GenerateDoc(p.prompt, p.metadata, p.defaults)).ToList();

            // Find shared variables (appear in 2+ prompts)
            var allVars = docs.SelectMany(d => d.Variables.Select(v => v.Name)).ToList();
            var varCounts = allVars.GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(v => v)
                .ToList();

            var categories = docs
                .Where(d => !string.IsNullOrEmpty(d.Metadata.Category))
                .GroupBy(d => d.Metadata.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            return new PromptCatalog
            {
                Title = _options.CatalogTitle,
                GeneratedAt = DateTime.UtcNow,
                Prompts = docs,
                TotalVariables = allVars.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                SharedVariables = varCounts,
                CategoryBreakdown = categories
            };
        }

        /// <summary>
        /// Renders documentation as Markdown text.
        /// </summary>
        public string ToMarkdown(PromptDoc doc)
        {
            var sb = new System.Text.StringBuilder();

            // Title
            string title = !string.IsNullOrEmpty(doc.Metadata.Title) ? doc.Metadata.Title : "Prompt Documentation";
            sb.AppendLine($"# {title}");
            sb.AppendLine();

            // Metadata
            if (!string.IsNullOrEmpty(doc.Metadata.Description))
            {
                sb.AppendLine($"> {doc.Metadata.Description}");
                sb.AppendLine();
            }

            var metaItems = new List<string>();
            if (!string.IsNullOrEmpty(doc.Metadata.Author)) metaItems.Add($"**Author:** {doc.Metadata.Author}");
            if (!string.IsNullOrEmpty(doc.Metadata.Version)) metaItems.Add($"**Version:** {doc.Metadata.Version}");
            if (!string.IsNullOrEmpty(doc.Metadata.Model)) metaItems.Add($"**Model:** {doc.Metadata.Model}");
            if (!string.IsNullOrEmpty(doc.Metadata.Category)) metaItems.Add($"**Category:** {doc.Metadata.Category}");
            if (doc.Metadata.Tags.Count > 0) metaItems.Add($"**Tags:** {string.Join(", ", doc.Metadata.Tags)}");

            if (metaItems.Count > 0)
            {
                foreach (var item in metaItems) sb.AppendLine($"- {item}");
                sb.AppendLine();
            }

            // Stats
            sb.AppendLine("## Statistics");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Characters | {doc.CharCount:N0} |");
            sb.AppendLine($"| Words | {doc.WordCount:N0} |");
            sb.AppendLine($"| Lines | {doc.LineCount:N0} |");
            if (doc.EstimatedTokens > 0)
                sb.AppendLine($"| Est. Tokens | ~{doc.EstimatedTokens:N0} |");
            if (!string.IsNullOrEmpty(doc.Complexity))
                sb.AppendLine($"| Complexity | {doc.Complexity} |");
            sb.AppendLine();

            // Variables
            if (doc.Variables.Count > 0)
            {
                sb.AppendLine("## Variables");
                sb.AppendLine();
                sb.AppendLine("| Name | Required | Default | Occurrences | Description |");
                sb.AppendLine("|------|----------|---------|-------------|-------------|");
                foreach (var v in doc.Variables)
                {
                    string req = v.Required ? "✅" : "❌";
                    string def = v.HasDefault ? $"`{v.DefaultValue}`" : "—";
                    string desc = !string.IsNullOrEmpty(v.Description) ? v.Description : "—";
                    sb.AppendLine($"| `{v.Name}` | {req} | {def} | {v.Occurrences} | {desc} |");
                }
                sb.AppendLine();
            }

            // Sections
            if (doc.Sections.Count > 0 && !(doc.Sections.Count == 1 && doc.Sections[0].Heading == "(root)"))
            {
                sb.AppendLine("## Structure");
                sb.AppendLine();
                foreach (var section in doc.Sections)
                {
                    string indent = new string(' ', Math.Max(0, (section.Level - 1)) * 2);
                    string vars = section.VariableCount > 0 ? $" ({section.VariableCount} variables)" : "";
                    sb.AppendLine($"{indent}- **{section.Heading}** — {section.WordCount} words{vars}");
                }
                sb.AppendLine();
            }

            // Usage Example
            if (!string.IsNullOrEmpty(doc.UsageExample) && doc.Variables.Count > 0)
            {
                sb.AppendLine("## Usage Example");
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(doc.UsageExample);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // Prompt text
            sb.AppendLine("## Prompt Text");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(doc.PromptText);
            sb.AppendLine("```");

            return sb.ToString();
        }

        /// <summary>
        /// Renders a catalog of prompts as a single Markdown document.
        /// </summary>
        public string CatalogToMarkdown(PromptCatalog catalog)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"# {catalog.Title}");
            sb.AppendLine();
            sb.AppendLine($"*Generated: {catalog.GeneratedAt:yyyy-MM-dd HH:mm} UTC*");
            sb.AppendLine();

            // Summary
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine($"- **Total prompts:** {catalog.Prompts.Count}");
            sb.AppendLine($"- **Unique variables:** {catalog.TotalVariables}");
            if (catalog.SharedVariables.Count > 0)
                sb.AppendLine($"- **Shared variables:** {string.Join(", ", catalog.SharedVariables.Select(v => $"`{v}`"))}");
            sb.AppendLine();

            // Category breakdown
            if (catalog.CategoryBreakdown.Count > 0)
            {
                sb.AppendLine("### Categories");
                sb.AppendLine();
                foreach (var cat in catalog.CategoryBreakdown.OrderByDescending(c => c.Value))
                    sb.AppendLine($"- **{cat.Key}:** {cat.Value} prompt(s)");
                sb.AppendLine();
            }

            // Table of contents
            sb.AppendLine("## Table of Contents");
            sb.AppendLine();
            for (int i = 0; i < catalog.Prompts.Count; i++)
            {
                var doc = catalog.Prompts[i];
                string title = !string.IsNullOrEmpty(doc.Metadata.Title) ? doc.Metadata.Title : $"Prompt {i + 1}";
                string anchor = title.ToLowerInvariant().Replace(' ', '-').Replace(".", "");
                sb.AppendLine($"{i + 1}. [{title}](#{anchor})");
            }
            sb.AppendLine();

            // Individual docs
            sb.AppendLine("---");
            sb.AppendLine();
            for (int i = 0; i < catalog.Prompts.Count; i++)
            {
                if (i > 0) { sb.AppendLine(); sb.AppendLine("---"); sb.AppendLine(); }
                // Render each with sub-headings (bump levels by 1)
                var doc = catalog.Prompts[i];
                string md = ToMarkdown(doc);
                // Bump heading levels for catalog embedding
                md = Regex.Replace(md, @"^(#{1,5})", "$1#", RegexOptions.Multiline);
                sb.Append(md);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Compares two prompt docs and returns a summary of differences.
        /// </summary>
        public string ComparePrompts(PromptDoc a, PromptDoc b)
        {
            var sb = new System.Text.StringBuilder();
            string nameA = !string.IsNullOrEmpty(a.Metadata.Title) ? a.Metadata.Title : "Prompt A";
            string nameB = !string.IsNullOrEmpty(b.Metadata.Title) ? b.Metadata.Title : "Prompt B";

            sb.AppendLine($"# Comparison: {nameA} vs {nameB}");
            sb.AppendLine();

            // Stats comparison
            sb.AppendLine("## Statistics");
            sb.AppendLine();
            sb.AppendLine($"| Metric | {nameA} | {nameB} | Δ |");
            sb.AppendLine("|--------|---------|---------|---|");
            AppendCompareRow(sb, "Words", a.WordCount, b.WordCount);
            AppendCompareRow(sb, "Characters", a.CharCount, b.CharCount);
            AppendCompareRow(sb, "Lines", a.LineCount, b.LineCount);
            AppendCompareRow(sb, "Est. Tokens", a.EstimatedTokens, b.EstimatedTokens);
            AppendCompareRow(sb, "Variables", a.Variables.Count, b.Variables.Count);
            AppendCompareRow(sb, "Sections", a.Sections.Count, b.Sections.Count);
            sb.AppendLine();

            // Variable diff
            var varsA = a.Variables.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var varsB = b.Variables.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var onlyA = varsA.Except(varsB, StringComparer.OrdinalIgnoreCase).ToList();
            var onlyB = varsB.Except(varsA, StringComparer.OrdinalIgnoreCase).ToList();
            var shared = varsA.Intersect(varsB, StringComparer.OrdinalIgnoreCase).ToList();

            if (onlyA.Count > 0 || onlyB.Count > 0)
            {
                sb.AppendLine("## Variable Differences");
                sb.AppendLine();
                if (shared.Count > 0) sb.AppendLine($"- **Shared:** {string.Join(", ", shared.Select(v => $"`{v}`"))}");
                if (onlyA.Count > 0) sb.AppendLine($"- **Only in {nameA}:** {string.Join(", ", onlyA.Select(v => $"`{v}`"))}");
                if (onlyB.Count > 0) sb.AppendLine($"- **Only in {nameB}:** {string.Join(", ", onlyB.Select(v => $"`{v}`"))}");
                sb.AppendLine();
            }

            // Complexity
            if (a.Complexity != b.Complexity)
            {
                sb.AppendLine($"**Complexity:** {a.Complexity} → {b.Complexity}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serializes a PromptDoc to JSON.
        /// </summary>
        public string ToJson(PromptDoc doc)
        {
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Serializes a PromptCatalog to JSON.
        /// </summary>
        public string CatalogToJson(PromptCatalog catalog)
        {
            return JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true });
        }

        // --- Helpers ---

        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Rough approximation: ~4 chars per token for English
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        private static string EscapeForCode(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        private static string Truncate(string text, int maxLen)
        {
            if (text.Length <= maxLen) return text;
            return text[..maxLen] + "...";
        }

        private static void AppendCompareRow(System.Text.StringBuilder sb, string metric, int a, int b)
        {
            int delta = b - a;
            string deltaStr = delta > 0 ? $"+{delta}" : delta.ToString();
            sb.AppendLine($"| {metric} | {a:N0} | {b:N0} | {deltaStr} |");
        }
    }
}
