namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Supported export formats for prompt sharing.
    /// </summary>
    public enum ShareFormat
    {
        /// <summary>Plain text with minimal formatting.</summary>
        PlainText,
        /// <summary>Markdown with syntax highlighting hints.</summary>
        Markdown,
        /// <summary>Self-contained HTML page with styling.</summary>
        Html,
        /// <summary>Structured JSON suitable for import/export.</summary>
        Json,
        /// <summary>YAML format for config-file embedding.</summary>
        Yaml
    }

    /// <summary>
    /// Metadata attached to a shared prompt.
    /// </summary>
    public class ShareMetadata
    {
        /// <summary>Gets or sets the prompt title.</summary>
        public string Title { get; set; } = "Untitled Prompt";

        /// <summary>Gets or sets the author name.</summary>
        public string? Author { get; set; }

        /// <summary>Gets or sets an optional description.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets tags for categorization.</summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>Gets or sets the target model (e.g., "gpt-4", "gpt-4o").</summary>
        public string? Model { get; set; }

        /// <summary>Gets or sets the creation timestamp.</summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Gets or sets the prompt version.</summary>
        public string? Version { get; set; }

        /// <summary>Gets or sets optional PromptOptions for the prompt.</summary>
        public PromptOptions? Options { get; set; }
    }

    /// <summary>
    /// A shareable prompt bundle with system message, user prompt, and metadata.
    /// </summary>
    public class ShareablePrompt
    {
        /// <summary>Gets or sets the system message.</summary>
        public string? SystemMessage { get; set; }

        /// <summary>Gets or sets the user prompt text.</summary>
        public string UserPrompt { get; set; } = "";

        /// <summary>Gets or sets few-shot examples as (user, assistant) pairs.</summary>
        public List<(string User, string Assistant)> Examples { get; set; } = new();

        /// <summary>Gets or sets the metadata.</summary>
        public ShareMetadata Metadata { get; set; } = new();
    }

    /// <summary>
    /// Formats prompts for sharing across platforms — Markdown, HTML, JSON, YAML, and plain text.
    /// Useful for collaboration, documentation, and prompt cataloging.
    /// </summary>
    /// <example>
    /// <code>
    /// var prompt = new ShareablePrompt
    /// {
    ///     SystemMessage = "You are a helpful assistant.",
    ///     UserPrompt = "Explain quantum computing in simple terms.",
    ///     Metadata = new ShareMetadata
    ///     {
    ///         Title = "Quantum Explainer",
    ///         Author = "Alice",
    ///         Tags = new List&lt;string&gt; { "education", "science" },
    ///         Model = "gpt-4o"
    ///     }
    /// };
    /// 
    /// string md = PromptShareFormatter.Format(prompt, ShareFormat.Markdown);
    /// string html = PromptShareFormatter.Format(prompt, ShareFormat.Html);
    /// string json = PromptShareFormatter.Format(prompt, ShareFormat.Json);
    /// 
    /// // Save to file
    /// await PromptShareFormatter.SaveAsync(prompt, ShareFormat.Markdown, "quantum-explainer.md");
    /// 
    /// // Import from JSON
    /// var imported = PromptShareFormatter.ImportFromJson(json);
    /// </code>
    /// </example>
    public static class PromptShareFormatter
    {
        /// <summary>
        /// Formats a shareable prompt in the specified format.
        /// </summary>
        /// <param name="prompt">The prompt to format.</param>
        /// <param name="format">The output format.</param>
        /// <returns>Formatted string representation.</returns>
        public static string Format(ShareablePrompt prompt, ShareFormat format)
        {
            ArgumentNullException.ThrowIfNull(prompt);

            return format switch
            {
                ShareFormat.PlainText => FormatPlainText(prompt),
                ShareFormat.Markdown => FormatMarkdown(prompt),
                ShareFormat.Html => FormatHtml(prompt),
                ShareFormat.Json => FormatJson(prompt),
                ShareFormat.Yaml => FormatYaml(prompt),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
        }

        /// <summary>
        /// Saves a formatted prompt to a file.
        /// </summary>
        public static async Task SaveAsync(ShareablePrompt prompt, ShareFormat format, string filePath)
        {
            var content = Format(prompt, format);
            await File.WriteAllTextAsync(filePath, content);
        }

        /// <summary>
        /// Imports a ShareablePrompt from a JSON string.
        /// </summary>
        public static ShareablePrompt ImportFromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var prompt = new ShareablePrompt();

            if (root.TryGetProperty("systemMessage", out var sys) && sys.ValueKind == JsonValueKind.String)
                prompt.SystemMessage = sys.GetString();

            if (root.TryGetProperty("userPrompt", out var usr) && usr.ValueKind == JsonValueKind.String)
                prompt.UserPrompt = usr.GetString() ?? "";

            if (root.TryGetProperty("examples", out var exArr) && exArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var ex in exArr.EnumerateArray())
                {
                    var u = ex.TryGetProperty("user", out var uv) ? uv.GetString() ?? "" : "";
                    var a = ex.TryGetProperty("assistant", out var av) ? av.GetString() ?? "" : "";
                    prompt.Examples.Add((u, a));
                }
            }

            var meta = prompt.Metadata;
            if (root.TryGetProperty("metadata", out var m) && m.ValueKind == JsonValueKind.Object)
            {
                if (m.TryGetProperty("title", out var t)) meta.Title = t.GetString() ?? "Untitled Prompt";
                if (m.TryGetProperty("author", out var a)) meta.Author = a.GetString();
                if (m.TryGetProperty("description", out var d)) meta.Description = d.GetString();
                if (m.TryGetProperty("model", out var mo)) meta.Model = mo.GetString();
                if (m.TryGetProperty("version", out var v)) meta.Version = v.GetString();
                if (m.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                    meta.Tags = tags.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
                if (m.TryGetProperty("createdAt", out var ca))
                    meta.CreatedAt = DateTimeOffset.Parse(ca.GetString() ?? DateTimeOffset.UtcNow.ToString("o"));
            }

            return prompt;
        }

        /// <summary>
        /// Creates a ShareablePrompt from a PromptTemplate.
        /// </summary>
        public static ShareablePrompt FromTemplate(PromptTemplate template, string? title = null, string? author = null)
        {
            ArgumentNullException.ThrowIfNull(template);

            return new ShareablePrompt
            {
                UserPrompt = template.Template,
                Metadata = new ShareMetadata
                {
                    Title = title ?? "Prompt Template",
                    Author = author,
                    Description = $"Template with variables: {string.Join(", ", template.GetVariables())}"
                }
            };
        }

        /// <summary>
        /// Creates a ShareablePrompt from a Conversation.
        /// </summary>
        public static ShareablePrompt FromConversation(Conversation conversation, string? title = null, string? author = null)
        {
            ArgumentNullException.ThrowIfNull(conversation);

            var messages = conversation.GetHistory();
            string? systemMsg = null;
            var userParts = new List<string>();
            var examples = new List<(string, string)>();

            foreach (var msg in messages)
            {
                if (msg.Role == "system")
                    systemMsg = msg.Content;
                else if (msg.Role == "user")
                    userParts.Add(msg.Content);
                else if (msg.Role == "assistant" && userParts.Count > 0)
                    examples.Add((userParts.Last(), msg.Content));
            }

            return new ShareablePrompt
            {
                SystemMessage = systemMsg,
                UserPrompt = userParts.LastOrDefault() ?? "",
                Examples = examples.Take(examples.Count - 1).ToList(), // last pair is actual, rest are examples
                Metadata = new ShareMetadata
                {
                    Title = title ?? "Conversation Export",
                    Author = author,
                    Description = $"Exported conversation with {messages.Count} messages"
                }
            };
        }

        /// <summary>
        /// Estimates the token count of the shareable prompt content.
        /// </summary>
        public static int EstimateTokens(ShareablePrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            int tokens = 0;
            if (prompt.SystemMessage != null)
                tokens += PromptGuard.EstimateTokens(prompt.SystemMessage);
            tokens += PromptGuard.EstimateTokens(prompt.UserPrompt);
            foreach (var (user, assistant) in prompt.Examples)
            {
                tokens += PromptGuard.EstimateTokens(user);
                tokens += PromptGuard.EstimateTokens(assistant);
            }
            return tokens;
        }

        private static string FormatPlainText(ShareablePrompt prompt)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {prompt.Metadata.Title} ===");
            if (prompt.Metadata.Author != null) sb.AppendLine($"Author: {prompt.Metadata.Author}");
            if (prompt.Metadata.Description != null) sb.AppendLine($"Description: {prompt.Metadata.Description}");
            if (prompt.Metadata.Model != null) sb.AppendLine($"Model: {prompt.Metadata.Model}");
            if (prompt.Metadata.Tags.Count > 0) sb.AppendLine($"Tags: {string.Join(", ", prompt.Metadata.Tags)}");
            if (prompt.Metadata.Version != null) sb.AppendLine($"Version: {prompt.Metadata.Version}");
            sb.AppendLine($"Created: {prompt.Metadata.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Estimated tokens: {EstimateTokens(prompt)}");
            sb.AppendLine();

            if (prompt.SystemMessage != null)
            {
                sb.AppendLine("--- System Message ---");
                sb.AppendLine(prompt.SystemMessage);
                sb.AppendLine();
            }

            if (prompt.Examples.Count > 0)
            {
                sb.AppendLine("--- Few-Shot Examples ---");
                for (int i = 0; i < prompt.Examples.Count; i++)
                {
                    sb.AppendLine($"Example {i + 1}:");
                    sb.AppendLine($"  User: {prompt.Examples[i].User}");
                    sb.AppendLine($"  Assistant: {prompt.Examples[i].Assistant}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("--- User Prompt ---");
            sb.AppendLine(prompt.UserPrompt);

            return sb.ToString();
        }

        private static string FormatMarkdown(ShareablePrompt prompt)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {prompt.Metadata.Title}");
            sb.AppendLine();

            // Metadata table
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            if (prompt.Metadata.Author != null) sb.AppendLine($"| Author | {prompt.Metadata.Author} |");
            if (prompt.Metadata.Description != null) sb.AppendLine($"| Description | {prompt.Metadata.Description} |");
            if (prompt.Metadata.Model != null) sb.AppendLine($"| Model | `{prompt.Metadata.Model}` |");
            if (prompt.Metadata.Tags.Count > 0) sb.AppendLine($"| Tags | {string.Join(", ", prompt.Metadata.Tags.Select(t => $"`{t}`"))} |");
            if (prompt.Metadata.Version != null) sb.AppendLine($"| Version | {prompt.Metadata.Version} |");
            sb.AppendLine($"| Created | {prompt.Metadata.CreatedAt:yyyy-MM-dd HH:mm} |");
            sb.AppendLine($"| Estimated Tokens | {EstimateTokens(prompt)} |");
            sb.AppendLine();

            if (prompt.SystemMessage != null)
            {
                sb.AppendLine("## System Message");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(prompt.SystemMessage);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (prompt.Examples.Count > 0)
            {
                sb.AppendLine("## Few-Shot Examples");
                sb.AppendLine();
                for (int i = 0; i < prompt.Examples.Count; i++)
                {
                    sb.AppendLine($"### Example {i + 1}");
                    sb.AppendLine();
                    sb.AppendLine($"**User:** {prompt.Examples[i].User}");
                    sb.AppendLine();
                    sb.AppendLine($"**Assistant:** {prompt.Examples[i].Assistant}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("## User Prompt");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(prompt.UserPrompt);
            sb.AppendLine("```");

            return sb.ToString();
        }

        private static string FormatHtml(ShareablePrompt prompt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine($"<title>{Escape(prompt.Metadata.Title)}</title>");
            sb.AppendLine("<meta charset=\"utf-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; max-width: 800px; margin: 40px auto; padding: 0 20px; color: #1a1a2e; background: #f8f9fa; }");
            sb.AppendLine("h1 { color: #16213e; border-bottom: 2px solid #0f3460; padding-bottom: 8px; }");
            sb.AppendLine("h2 { color: #0f3460; margin-top: 24px; }");
            sb.AppendLine(".meta { background: #e8eaf6; border-radius: 8px; padding: 16px; margin: 16px 0; }");
            sb.AppendLine(".meta dt { font-weight: 600; color: #0f3460; }");
            sb.AppendLine(".meta dd { margin: 0 0 8px 0; }");
            sb.AppendLine(".tag { background: #0f3460; color: white; padding: 2px 8px; border-radius: 12px; font-size: 0.85em; margin-right: 4px; }");
            sb.AppendLine("pre { background: #1a1a2e; color: #e0e0e0; padding: 16px; border-radius: 8px; overflow-x: auto; white-space: pre-wrap; }");
            sb.AppendLine(".example { background: white; border: 1px solid #ddd; border-radius: 8px; padding: 16px; margin: 8px 0; }");
            sb.AppendLine(".role { font-weight: 600; color: #0f3460; }");
            sb.AppendLine(".tokens { font-size: 0.9em; color: #666; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine($"<h1>{Escape(prompt.Metadata.Title)}</h1>");

            sb.AppendLine("<div class=\"meta\"><dl>");
            if (prompt.Metadata.Author != null) sb.AppendLine($"<dt>Author</dt><dd>{Escape(prompt.Metadata.Author)}</dd>");
            if (prompt.Metadata.Description != null) sb.AppendLine($"<dt>Description</dt><dd>{Escape(prompt.Metadata.Description)}</dd>");
            if (prompt.Metadata.Model != null) sb.AppendLine($"<dt>Model</dt><dd><code>{Escape(prompt.Metadata.Model)}</code></dd>");
            if (prompt.Metadata.Tags.Count > 0)
                sb.AppendLine($"<dt>Tags</dt><dd>{string.Join(" ", prompt.Metadata.Tags.Select(t => $"<span class=\"tag\">{Escape(t)}</span>"))}</dd>");
            if (prompt.Metadata.Version != null) sb.AppendLine($"<dt>Version</dt><dd>{Escape(prompt.Metadata.Version)}</dd>");
            sb.AppendLine($"<dt>Created</dt><dd>{prompt.Metadata.CreatedAt:yyyy-MM-dd HH:mm}</dd>");
            sb.AppendLine($"<dt>Estimated Tokens</dt><dd class=\"tokens\">{EstimateTokens(prompt)}</dd>");
            sb.AppendLine("</dl></div>");

            if (prompt.SystemMessage != null)
            {
                sb.AppendLine("<h2>System Message</h2>");
                sb.AppendLine($"<pre>{Escape(prompt.SystemMessage)}</pre>");
            }

            if (prompt.Examples.Count > 0)
            {
                sb.AppendLine("<h2>Few-Shot Examples</h2>");
                for (int i = 0; i < prompt.Examples.Count; i++)
                {
                    sb.AppendLine("<div class=\"example\">");
                    sb.AppendLine($"<p><span class=\"role\">User:</span> {Escape(prompt.Examples[i].User)}</p>");
                    sb.AppendLine($"<p><span class=\"role\">Assistant:</span> {Escape(prompt.Examples[i].Assistant)}</p>");
                    sb.AppendLine("</div>");
                }
            }

            sb.AppendLine("<h2>User Prompt</h2>");
            sb.AppendLine($"<pre>{Escape(prompt.UserPrompt)}</pre>");
            sb.AppendLine("<footer style=\"margin-top:32px;color:#888;font-size:0.85em;\">Generated by Prompt Share Formatter</footer>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private static string FormatJson(ShareablePrompt prompt)
        {
            var obj = new Dictionary<string, object?>
            {
                ["systemMessage"] = prompt.SystemMessage,
                ["userPrompt"] = prompt.UserPrompt,
                ["examples"] = prompt.Examples.Select(e => new { user = e.User, assistant = e.Assistant }).ToList(),
                ["metadata"] = new
                {
                    title = prompt.Metadata.Title,
                    author = prompt.Metadata.Author,
                    description = prompt.Metadata.Description,
                    model = prompt.Metadata.Model,
                    tags = prompt.Metadata.Tags,
                    version = prompt.Metadata.Version,
                    createdAt = prompt.Metadata.CreatedAt.ToString("o")
                },
                ["estimatedTokens"] = EstimateTokens(prompt)
            };

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string FormatYaml(ShareablePrompt prompt)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"title: \"{YamlEscape(prompt.Metadata.Title)}\"");
            if (prompt.Metadata.Author != null) sb.AppendLine($"author: \"{YamlEscape(prompt.Metadata.Author)}\"");
            if (prompt.Metadata.Description != null) sb.AppendLine($"description: \"{YamlEscape(prompt.Metadata.Description)}\"");
            if (prompt.Metadata.Model != null) sb.AppendLine($"model: \"{YamlEscape(prompt.Metadata.Model)}\"");
            if (prompt.Metadata.Version != null) sb.AppendLine($"version: \"{YamlEscape(prompt.Metadata.Version)}\"");
            sb.AppendLine($"created_at: \"{prompt.Metadata.CreatedAt:o}\"");
            sb.AppendLine($"estimated_tokens: {EstimateTokens(prompt)}");

            if (prompt.Metadata.Tags.Count > 0)
            {
                sb.AppendLine("tags:");
                foreach (var tag in prompt.Metadata.Tags)
                    sb.AppendLine($"  - \"{YamlEscape(tag)}\"");
            }

            if (prompt.SystemMessage != null)
            {
                sb.AppendLine("system_message: |");
                foreach (var line in prompt.SystemMessage.Split('\n'))
                    sb.AppendLine($"  {line}");
            }

            if (prompt.Examples.Count > 0)
            {
                sb.AppendLine("examples:");
                foreach (var (user, assistant) in prompt.Examples)
                {
                    sb.AppendLine($"  - user: \"{YamlEscape(user)}\"");
                    sb.AppendLine($"    assistant: \"{YamlEscape(assistant)}\"");
                }
            }

            sb.AppendLine("user_prompt: |");
            foreach (var line in prompt.UserPrompt.Split('\n'))
                sb.AppendLine($"  {line}");

            return sb.ToString();
        }

        private static string Escape(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string YamlEscape(string text) =>
            text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
