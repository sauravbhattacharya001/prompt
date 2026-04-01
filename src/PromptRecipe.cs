namespace Prompt
{
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A self-contained prompt recipe that bundles a template, few-shot examples,
    /// system persona, default variables, and metadata into a single portable unit.
    /// Recipes can be serialized to JSON for sharing, stored in catalogs, and
    /// rendered with variable overrides at execution time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A recipe is the "complete package" for a prompt use-case. Instead of
    /// managing templates, few-shot examples, and persona settings separately,
    /// a recipe combines them into one shareable artifact.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var recipe = new PromptRecipeBuilder("code-reviewer")
    ///     .WithDescription("Reviews code for bugs, style, and security issues")
    ///     .WithSystemPersona("You are an expert code reviewer. Be concise and actionable.")
    ///     .WithTemplate("Review this {{language}} code:\n\n```\n{{code}}\n```")
    ///     .WithFewShot("Review this Python code:\n```\nx = eval(input())\n```",
    ///                   "🔴 Security: `eval(input())` allows arbitrary code execution. Use `ast.literal_eval()` or explicit parsing instead.")
    ///     .WithDefault("language", "Python")
    ///     .WithTag("security")
    ///     .WithTag("code-quality")
    ///     .Build();
    ///
    /// string prompt = recipe.Render(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["code"] = "os.system(user_input)"
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptRecipe
    {
        internal const int MaxJsonPayloadBytes = SerializationGuards.MaxJsonPayloadBytes;

        /// <summary>
        /// Creates a new prompt recipe.
        /// </summary>
        /// <param name="name">Unique name/identifier for this recipe.</param>
        /// <param name="template">The prompt template with {{variable}} placeholders.</param>
        /// <param name="description">Human-readable description of what this recipe does.</param>
        /// <param name="systemPersona">Optional system/persona prompt prepended to output.</param>
        /// <param name="fewShotExamples">Optional list of (input, output) example pairs.</param>
        /// <param name="defaults">Optional default variable values.</param>
        /// <param name="tags">Optional tags for categorization and search.</param>
        /// <param name="metadata">Optional key-value metadata (author, version, etc.).</param>
        public PromptRecipe(
            string name,
            PromptTemplate template,
            string? description = null,
            string? systemPersona = null,
            IReadOnlyList<PromptRecipeExample>? fewShotExamples = null,
            IReadOnlyDictionary<string, string>? defaults = null,
            IReadOnlyList<string>? tags = null,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Recipe name cannot be null or empty.", nameof(name));
            Name = name;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Description = description ?? string.Empty;
            SystemPersona = systemPersona;
            FewShotExamples = fewShotExamples ?? Array.Empty<PromptRecipeExample>();
            Defaults = defaults ?? new Dictionary<string, string>();
            Tags = tags ?? Array.Empty<string>();
            Metadata = metadata ?? new Dictionary<string, string>();
        }

        /// <summary>Gets the recipe name/identifier.</summary>
        [JsonPropertyName("name")]
        public string Name { get; }

        /// <summary>Gets the recipe description.</summary>
        [JsonPropertyName("description")]
        public string Description { get; }

        /// <summary>Gets the system persona prompt, if any.</summary>
        [JsonPropertyName("systemPersona")]
        public string? SystemPersona { get; }

        /// <summary>Gets the prompt template.</summary>
        [JsonPropertyName("template")]
        public PromptTemplate Template { get; }

        /// <summary>Gets the few-shot examples.</summary>
        [JsonPropertyName("fewShotExamples")]
        public IReadOnlyList<PromptRecipeExample> FewShotExamples { get; }

        /// <summary>Gets the default variable values.</summary>
        [JsonPropertyName("defaults")]
        public IReadOnlyDictionary<string, string> Defaults { get; }

        /// <summary>Gets the tags for categorization.</summary>
        [JsonPropertyName("tags")]
        public IReadOnlyList<string> Tags { get; }

        /// <summary>Gets additional metadata.</summary>
        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Renders the complete prompt, combining system persona, few-shot examples,
        /// and the main template with variables applied.
        /// </summary>
        /// <param name="variables">Variable values to merge with defaults.</param>
        /// <returns>The fully rendered prompt string.</returns>
        public string Render(Dictionary<string, string>? variables = null)
        {
            var merged = new Dictionary<string, string>(
                (IDictionary<string, string>)Defaults);
            if (variables != null)
            {
                foreach (var kv in variables)
                    merged[kv.Key] = kv.Value;
            }

            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(SystemPersona))
            {
                sb.AppendLine(SystemPersona);
                sb.AppendLine();
            }

            if (FewShotExamples.Count > 0)
            {
                sb.AppendLine("Examples:");
                sb.AppendLine();
                for (int i = 0; i < FewShotExamples.Count; i++)
                {
                    var ex = FewShotExamples[i];
                    sb.AppendLine($"Input: {ex.Input}");
                    sb.AppendLine($"Output: {ex.Output}");
                    if (i < FewShotExamples.Count - 1)
                        sb.AppendLine();
                }
                sb.AppendLine();
            }

            sb.Append(Template.Render(merged));
            return sb.ToString();
        }

        /// <summary>
        /// Gets all variable names required by the template that have no default value.
        /// </summary>
        /// <returns>Set of required variable names.</returns>
        public IReadOnlySet<string> GetRequiredVariables()
        {
            var all = Template.GetVariables();
            var required = new HashSet<string>();
            foreach (var v in all)
            {
                if (!Defaults.ContainsKey(v))
                    required.Add(v);
            }
            return required;
        }

        /// <summary>
        /// Validates that all required variables are provided.
        /// </summary>
        /// <param name="variables">Variables to check.</param>
        /// <returns>List of missing variable names; empty if valid.</returns>
        public IReadOnlyList<string> Validate(Dictionary<string, string>? variables = null)
        {
            var required = GetRequiredVariables();
            var missing = new List<string>();
            foreach (var v in required)
            {
                if (variables == null || !variables.ContainsKey(v))
                    missing.Add(v);
            }
            return missing;
        }

        /// <summary>
        /// Serializes this recipe to a JSON string.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var dto = new RecipeDto
            {
                Name = Name,
                Description = Description,
                SystemPersona = SystemPersona,
                TemplateText = Template.Render(new Dictionary<string, string>()),
                FewShotExamples = FewShotExamples.ToList(),
                Defaults = new Dictionary<string, string>(
                    (IDictionary<string, string>)Defaults),
                Tags = Tags.ToList(),
                Metadata = new Dictionary<string, string>(
                    (IDictionary<string, string>)Metadata)
            };
            return JsonSerializer.Serialize(dto, new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Deserializes a recipe from a JSON string.
        /// </summary>
        public static PromptRecipe FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be null or empty.", nameof(json));
            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var dto = JsonSerializer.Deserialize<RecipeDto>(json)
                ?? throw new JsonException("Failed to deserialize recipe.");

            return new PromptRecipe(
                dto.Name ?? throw new JsonException("Recipe name is required."),
                new PromptTemplate(dto.TemplateText ?? ""),
                dto.Description,
                dto.SystemPersona,
                dto.FewShotExamples,
                dto.Defaults,
                dto.Tags,
                dto.Metadata);
        }

        /// <summary>
        /// Returns a human-readable summary of this recipe.
        /// </summary>
        public string Summarize()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Recipe: {Name}");
            if (!string.IsNullOrEmpty(Description))
                sb.AppendLine($"  Description: {Description}");
            if (Tags.Count > 0)
                sb.AppendLine($"  Tags: {string.Join(", ", Tags)}");
            sb.AppendLine($"  Variables: {string.Join(", ", Template.GetVariables())}");
            var required = GetRequiredVariables();
            if (required.Count > 0)
                sb.AppendLine($"  Required: {string.Join(", ", required)}");
            if (FewShotExamples.Count > 0)
                sb.AppendLine($"  Examples: {FewShotExamples.Count}");
            if (SystemPersona != null)
                sb.AppendLine($"  Has persona: yes");
            foreach (var kv in Metadata)
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
            return sb.ToString().TrimEnd();
        }

        private class RecipeDto
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            [JsonPropertyName("description")]
            public string? Description { get; set; }
            [JsonPropertyName("systemPersona")]
            public string? SystemPersona { get; set; }
            [JsonPropertyName("templateText")]
            public string? TemplateText { get; set; }
            [JsonPropertyName("fewShotExamples")]
            public List<PromptRecipeExample>? FewShotExamples { get; set; }
            [JsonPropertyName("defaults")]
            public Dictionary<string, string>? Defaults { get; set; }
            [JsonPropertyName("tags")]
            public List<string>? Tags { get; set; }
            [JsonPropertyName("metadata")]
            public Dictionary<string, string>? Metadata { get; set; }
        }
    }

    /// <summary>
    /// A single input/output example pair for a <see cref="PromptRecipe"/>.
    /// </summary>
    public class PromptRecipeExample
    {
        /// <summary>Creates a new example pair.</summary>
        [JsonConstructor]
        public PromptRecipeExample(string input, string output)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>Gets the example input.</summary>
        [JsonPropertyName("input")]
        public string Input { get; }

        /// <summary>Gets the expected output.</summary>
        [JsonPropertyName("output")]
        public string Output { get; }
    }

    /// <summary>
    /// Fluent builder for creating <see cref="PromptRecipe"/> instances.
    /// </summary>
    public class PromptRecipeBuilder
    {
        private readonly string _name;
        private string? _description;
        private string? _systemPersona;
        private string _templateText = "";
        private readonly List<PromptRecipeExample> _examples = new();
        private readonly Dictionary<string, string> _defaults = new();
        private readonly List<string> _tags = new();
        private readonly Dictionary<string, string> _metadata = new();

        /// <summary>Creates a new recipe builder with the given name.</summary>
        public PromptRecipeBuilder(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Sets the recipe description.</summary>
        public PromptRecipeBuilder WithDescription(string description)
        { _description = description; return this; }

        /// <summary>Sets the system persona prompt.</summary>
        public PromptRecipeBuilder WithSystemPersona(string persona)
        { _systemPersona = persona; return this; }

        /// <summary>Sets the template text with {{variable}} placeholders.</summary>
        public PromptRecipeBuilder WithTemplate(string templateText)
        { _templateText = templateText; return this; }

        /// <summary>Adds a few-shot example pair.</summary>
        public PromptRecipeBuilder WithFewShot(string input, string output)
        { _examples.Add(new PromptRecipeExample(input, output)); return this; }

        /// <summary>Sets a default variable value.</summary>
        public PromptRecipeBuilder WithDefault(string variable, string value)
        { _defaults[variable] = value; return this; }

        /// <summary>Adds a tag.</summary>
        public PromptRecipeBuilder WithTag(string tag)
        { _tags.Add(tag); return this; }

        /// <summary>Adds metadata.</summary>
        public PromptRecipeBuilder WithMetadata(string key, string value)
        { _metadata[key] = value; return this; }

        /// <summary>Builds the recipe.</summary>
        public PromptRecipe Build()
        {
            return new PromptRecipe(
                _name,
                new PromptTemplate(_templateText, new Dictionary<string, string>(_defaults)),
                _description,
                _systemPersona,
                _examples.AsReadOnly(),
                _defaults,
                _tags.AsReadOnly(),
                _metadata);
        }
    }
}
