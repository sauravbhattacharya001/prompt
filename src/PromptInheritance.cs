namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Represents a named block within a prompt template that can be
    /// overridden by child templates.
    /// </summary>
    public class PromptBlock
    {
        /// <summary>Gets the block name.</summary>
        public string Name { get; }

        /// <summary>Gets the default content of the block.</summary>
        public string Content { get; }

        /// <summary>
        /// Creates a new prompt block.
        /// </summary>
        /// <param name="name">Block name (must be non-empty, alphanumeric + underscores).</param>
        /// <param name="content">Default content for the block.</param>
        public PromptBlock(string name, string content)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Block name cannot be null or empty.", nameof(name));
            if (!Regex.IsMatch(name, @"^[a-zA-Z_]\w*$"))
                throw new ArgumentException(
                    $"Block name '{name}' is invalid. Use letters, digits, and underscores.", nameof(name));
            Name = name;
            Content = content ?? "";
        }
    }

    /// <summary>
    /// Result of rendering an inheritable prompt template, including
    /// block resolution details for debugging.
    /// </summary>
    public class InheritanceRenderResult
    {
        /// <summary>The fully rendered prompt text.</summary>
        public string Text { get; }

        /// <summary>Names of blocks resolved from the parent template.</summary>
        public IReadOnlyList<string> ParentBlocks { get; }

        /// <summary>Names of blocks overridden by a child.</summary>
        public IReadOnlyList<string> OverriddenBlocks { get; }

        /// <summary>Names of all blocks in the final rendered template.</summary>
        public IReadOnlyList<string> AllBlocks { get; }

        /// <summary>The inheritance depth (0 = no parent).</summary>
        public int Depth { get; }

        internal InheritanceRenderResult(
            string text,
            IReadOnlyList<string> parentBlocks,
            IReadOnlyList<string> overriddenBlocks,
            IReadOnlyList<string> allBlocks,
            int depth)
        {
            Text = text;
            ParentBlocks = parentBlocks;
            OverriddenBlocks = overriddenBlocks;
            AllBlocks = allBlocks;
            Depth = depth;
        }
    }

    /// <summary>
    /// A prompt template that supports block-based inheritance, similar to
    /// Jinja2/Django template inheritance. Parent templates define named blocks
    /// with <c>{%% block name %%}...{%% endblock %%}</c> syntax that child
    /// templates can override while preserving the surrounding structure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enables prompt engineering patterns like:
    /// <list type="bullet">
    ///   <item>Base system prompts with customizable sections</item>
    ///   <item>Shared instruction frameworks with role-specific overrides</item>
    ///   <item>Multi-level prompt hierarchies (grandparent → parent → child)</item>
    ///   <item>Appending to parent blocks with <c>{{super}}</c></item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Define a base prompt with blocks
    /// var basePrompt = new InheritablePrompt(
    ///     "You are a {% block role %}helpful assistant{% endblock %}.\n" +
    ///     "{% block instructions %}Answer clearly and concisely.{% endblock %}\n" +
    ///     "{% block format %}Use plain text.{% endblock %}"
    /// );
    ///
    /// // Create a child that overrides specific blocks
    /// var codeReviewer = basePrompt.CreateChild(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["role"] = "senior code reviewer",
    ///     ["instructions"] = "Review the code for bugs, style, and performance.\n" +
    ///                        "Rate severity as: critical, major, minor, suggestion.",
    ///     ["format"] = "Use markdown with code blocks."
    /// });
    ///
    /// string rendered = codeReviewer.Render();
    /// // → "You are a senior code reviewer.\n" +
    /// //   "Review the code for bugs, style, and performance.\n" +
    /// //   "Rate severity as: critical, major, minor, suggestion.\n" +
    /// //   "Use markdown with code blocks."
    /// </code>
    /// </para>
    /// </remarks>
    public class InheritablePrompt
    {
        /// <summary>Maximum inheritance depth to prevent infinite loops.</summary>
        public const int MaxDepth = 10;

        private static readonly Regex BlockPattern = new Regex(
            @"\{%\s*block\s+(\w+)\s*%\}(.*?)\{%\s*endblock\s*%\}", RegexOptions.Compiled | RegexOptions.Singleline,
            TimeSpan.FromMilliseconds(1000));

        private static readonly Regex SuperPattern = new Regex(
            @"\{\{\s*super\s*\}\}", RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        private readonly string _rawTemplate;
        private readonly InheritablePrompt? _parent;
        private readonly Dictionary<string, string> _blockOverrides;

        /// <summary>
        /// Creates a new inheritable prompt template (root/parent level).
        /// </summary>
        /// <param name="template">
        /// Template string with optional <c>{%% block name %%}...{%% endblock %%}</c> sections.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="template"/> is null or empty, or contains
        /// duplicate block names.
        /// </exception>
        public InheritablePrompt(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Template cannot be null or empty.", nameof(template));

            ValidateBlocks(template);
            _rawTemplate = template;
            _parent = null;
            _blockOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a child prompt that inherits from a parent and overrides specific blocks.
        /// </summary>
        private InheritablePrompt(InheritablePrompt parent, Dictionary<string, string> overrides)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _rawTemplate = parent._rawTemplate;
            _blockOverrides = new Dictionary<string, string>(
                overrides ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the inheritance depth (0 for root templates).</summary>
        public int Depth
        {
            get
            {
                int d = 0;
                var current = _parent;
                while (current != null)
                {
                    d++;
                    current = current._parent;
                }
                return d;
            }
        }

        /// <summary>Gets the parent template, or null if this is a root template.</summary>
        public InheritablePrompt? Parent => _parent;

        /// <summary>Gets the raw template string.</summary>
        public string RawTemplate => _rawTemplate;

        /// <summary>
        /// Gets the names of all blocks defined in the template.
        /// </summary>
        public IReadOnlyList<string> BlockNames
        {
            get
            {
                var matches = BlockPattern.Matches(_rawTemplate);
                return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
            }
        }

        /// <summary>
        /// Gets the names of blocks that have been overridden in this child.
        /// </summary>
        public IReadOnlyList<string> OverriddenBlockNames =>
            _blockOverrides.Keys.ToList();

        /// <summary>
        /// Gets the default content of a named block.
        /// </summary>
        /// <param name="blockName">The block name to look up.</param>
        /// <returns>The default block content, or null if no such block exists.</returns>
        public string? GetBlockDefault(string blockName)
        {
            var match = BlockPattern.Matches(_rawTemplate)
                .FirstOrDefault(m => string.Equals(
                    m.Groups[1].Value, blockName, StringComparison.OrdinalIgnoreCase));
            return match?.Groups[2].Value;
        }

        /// <summary>
        /// Creates a child template that inherits this template's structure
        /// and overrides specific blocks.
        /// </summary>
        /// <param name="blockOverrides">
        /// Dictionary mapping block names to replacement content.
        /// Use <c>{{super}}</c> in the override content to include the parent's
        /// block content at that position.
        /// </param>
        /// <returns>A new child <see cref="InheritablePrompt"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when an override references a block name that doesn't exist
        /// in the template, or when maximum depth would be exceeded.
        /// </exception>
        public InheritablePrompt CreateChild(Dictionary<string, string> blockOverrides)
        {
            if (blockOverrides == null)
                throw new ArgumentNullException(nameof(blockOverrides));

            if (Depth + 1 >= MaxDepth)
                throw new InvalidOperationException(
                    $"Maximum inheritance depth of {MaxDepth} exceeded.");

            var definedBlocks = new HashSet<string>(
                BlockNames, StringComparer.OrdinalIgnoreCase);

            foreach (var key in blockOverrides.Keys)
            {
                if (!definedBlocks.Contains(key))
                    throw new ArgumentException(
                        $"Block '{key}' does not exist in the template. " +
                        $"Available blocks: {string.Join(", ", definedBlocks)}.",
                        nameof(blockOverrides));
            }

            return new InheritablePrompt(this, blockOverrides);
        }

        /// <summary>
        /// Renders the template by resolving all blocks through the
        /// inheritance chain.
        /// </summary>
        /// <returns>The fully rendered prompt string.</returns>
        public string Render()
        {
            return RenderDetailed().Text;
        }

        /// <summary>
        /// Renders the template with detailed information about block resolution.
        /// </summary>
        /// <returns>An <see cref="InheritanceRenderResult"/> with the rendered text and metadata.</returns>
        public InheritanceRenderResult RenderDetailed()
        {
            var parentBlocks = new List<string>();
            var overriddenBlocks = new List<string>();
            var allBlocks = new List<string>();

            string result = BlockPattern.Replace(_rawTemplate, match =>
            {
                string blockName = match.Groups[1].Value;
                string defaultContent = match.Groups[2].Value;
                allBlocks.Add(blockName);

                // Walk the inheritance chain to find the most-derived override
                string? resolved = ResolveBlock(blockName, defaultContent);

                if (resolved != defaultContent)
                    overriddenBlocks.Add(blockName);
                else
                    parentBlocks.Add(blockName);

                return resolved;
            });

            return new InheritanceRenderResult(
                result, parentBlocks, overriddenBlocks, allBlocks, Depth);
        }

        /// <summary>
        /// Resolves a block's content by walking the inheritance chain.
        /// The most-derived override wins.
        /// </summary>
        private string ResolveBlock(string blockName, string defaultContent)
        {
            // Collect overrides from the entire chain (most-derived first)
            var chain = new List<InheritablePrompt>();
            var current = this;
            while (current != null)
            {
                chain.Add(current);
                current = current._parent;
            }

            // Find the most-derived override
            string content = defaultContent;
            for (int i = 0; i < chain.Count; i++)
            {
                if (chain[i]._blockOverrides.TryGetValue(blockName, out var overrideContent))
                {
                    // Replace {{super}} with the parent's content
                    content = SuperPattern.Replace(overrideContent, defaultContent);
                    break;
                }
            }

            return content;
        }

        /// <summary>
        /// Creates a copy of this template with an additional block override,
        /// returning a new child.
        /// </summary>
        /// <param name="blockName">The block to override.</param>
        /// <param name="content">The new content for the block.</param>
        /// <returns>A new child template with the override applied.</returns>
        public InheritablePrompt WithBlock(string blockName, string content)
        {
            return CreateChild(new Dictionary<string, string> { [blockName] = content });
        }

        /// <summary>
        /// Generates a visual representation of the template's block structure.
        /// </summary>
        /// <returns>A string showing block names, default content, and override status.</returns>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"InheritablePrompt (depth={Depth})");
            sb.AppendLine(new string('-', 40));

            var blocks = BlockPattern.Matches(_rawTemplate);
            if (blocks.Count == 0)
            {
                sb.AppendLine("  (no blocks defined)");
            }
            else
            {
                foreach (Match match in blocks)
                {
                    string name = match.Groups[1].Value;
                    string defaultContent = match.Groups[2].Value;
                    bool isOverridden = _blockOverrides.ContainsKey(name);

                    sb.AppendLine($"  Block: {name}");
                    sb.AppendLine($"    Status: {(isOverridden ? "OVERRIDDEN" : "default")}");

                    string preview = isOverridden
                        ? _blockOverrides[name]
                        : defaultContent;
                    if (preview.Length > 60)
                        preview = preview.Substring(0, 57) + "...";
                    sb.AppendLine($"    Content: {preview.Replace("\n", "\\n")}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the template structure as a JSON string for serialization.
        /// </summary>
        /// <returns>JSON representation of the template and its blocks.</returns>
        public string ToJson()
        {
            var data = new Dictionary<string, object>
            {
                ["template"] = _rawTemplate,
                ["depth"] = Depth,
                ["blocks"] = BlockNames.Select(name => new Dictionary<string, object>
                {
                    ["name"] = name,
                    ["default"] = GetBlockDefault(name) ?? "",
                    ["overridden"] = _blockOverrides.ContainsKey(name),
                    ["content"] = _blockOverrides.TryGetValue(name, out var ov) ? ov : (GetBlockDefault(name) ?? "")
                }).ToList()
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Creates an InheritablePrompt from a JSON representation.
        /// </summary>
        /// <param name="json">JSON string previously produced by <see cref="ToJson"/>.</param>
        /// <returns>A new <see cref="InheritablePrompt"/>.</returns>
        public static InheritablePrompt FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or empty.", nameof(json));

            if (json.Length > SerializationGuards.MaxJsonPayloadBytes)
                throw new ArgumentException(
                    $"JSON payload exceeds maximum size of {SerializationGuards.MaxJsonPayloadBytes} bytes.");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string template = root.GetProperty("template").GetString()
                ?? throw new JsonException("Missing 'template' property.");

            var prompt = new InheritablePrompt(template);

            if (root.TryGetProperty("blocks", out var blocksElement) &&
                blocksElement.ValueKind == JsonValueKind.Array)
            {
                var overrides = new Dictionary<string, string>();
                foreach (var block in blocksElement.EnumerateArray())
                {
                    bool overridden = block.TryGetProperty("overridden", out var ov) &&
                                      ov.GetBoolean();
                    if (overridden)
                    {
                        string name = block.GetProperty("name").GetString() ?? "";
                        string content = block.GetProperty("content").GetString() ?? "";
                        overrides[name] = content;
                    }
                }
                if (overrides.Count > 0)
                    return prompt.CreateChild(overrides);
            }

            return prompt;
        }

        /// <summary>
        /// Validates that block names in the template are unique.
        /// </summary>
        private static void ValidateBlocks(string template)
        {
            var matches = BlockPattern.Matches(template);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in matches)
            {
                string name = match.Groups[1].Value;
                if (!seen.Add(name))
                    throw new ArgumentException(
                        $"Duplicate block name '{name}' in template.");
            }
        }
    }
}
