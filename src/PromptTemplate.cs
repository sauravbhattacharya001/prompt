namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A reusable prompt template with variable placeholders that get
    /// filled in at render time. Supports default values, required
    /// variable validation, and composition (nesting templates).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Variables use <c>{{name}}</c> syntax. Defaults can be set at
    /// construction time and overridden at render time.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var template = new PromptTemplate(
    ///     "You are a {{role}} assistant. Help the user with {{topic}}.",
    ///     new Dictionary&lt;string, string&gt; { ["role"] = "helpful" }
    /// );
    ///
    /// string prompt = template.Render(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["topic"] = "C# programming"
    /// });
    /// // → "You are a helpful assistant. Help the user with C# programming."
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptTemplate
    {
        /// <summary>
        /// Maximum allowed JSON payload size for deserialization to prevent
        /// denial-of-service via crafted large payloads.
        /// Default: 10 MB.
        /// </summary>
        internal const int MaxJsonPayloadBytes = 10 * 1024 * 1024;

        private static readonly Regex VariablePattern =
            new Regex(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

        private readonly string _template;
        private readonly Dictionary<string, string> _defaults;

        /// <summary>
        /// Creates a new prompt template.
        /// </summary>
        /// <param name="template">
        /// The template string with <c>{{variable}}</c> placeholders.
        /// </param>
        /// <param name="defaults">
        /// Optional default values for variables. These are used when
        /// a variable is not provided at render time.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="template"/> is null or empty.
        /// </exception>
        public PromptTemplate(
            string template,
            Dictionary<string, string>? defaults = null)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException(
                    "Template cannot be null or empty.", nameof(template));

            _template = template;
            _defaults = defaults != null
                ? new Dictionary<string, string>(defaults, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the raw template string.
        /// </summary>
        public string Template => _template;

        /// <summary>
        /// Gets a read-only copy of the default values.
        /// </summary>
        public IReadOnlyDictionary<string, string> Defaults =>
            new Dictionary<string, string>(_defaults, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the set of variable names found in the template.
        /// </summary>
        /// <returns>A set of unique variable names (case-preserved).</returns>
        public HashSet<string> GetVariables()
        {
            var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in VariablePattern.Matches(_template))
            {
                variables.Add(match.Groups[1].Value);
            }
            return variables;
        }

        /// <summary>
        /// Returns the set of variable names that have no default value
        /// and must be supplied at render time.
        /// </summary>
        /// <returns>A set of required variable names.</returns>
        public HashSet<string> GetRequiredVariables()
        {
            var all = GetVariables();
            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in all)
            {
                if (!_defaults.ContainsKey(v))
                    required.Add(v);
            }
            return required;
        }

        /// <summary>
        /// Sets or updates a default value for a variable.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <param name="value">Default value.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> is null or empty.
        /// </exception>
        public void SetDefault(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Variable name cannot be null or empty.", nameof(name));

            _defaults[name] = value ?? "";
        }

        /// <summary>
        /// Removes a default value for a variable, making it required.
        /// </summary>
        /// <param name="name">Variable name to remove the default for.</param>
        /// <returns><c>true</c> if the default was removed; <c>false</c> if it didn't exist.</returns>
        public bool RemoveDefault(string name)
        {
            return _defaults.Remove(name);
        }

        /// <summary>
        /// Renders the template by replacing all <c>{{variable}}</c>
        /// placeholders with the provided values (falling back to defaults).
        /// </summary>
        /// <param name="variables">
        /// Variable values to use. Overrides defaults for matching keys.
        /// Can be <c>null</c> if all variables have defaults.
        /// </param>
        /// <param name="strict">
        /// When <c>true</c> (default), throws if any variable has no value
        /// and no default. When <c>false</c>, unresolved variables are
        /// left as-is in the output.
        /// </param>
        /// <returns>The rendered prompt string.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="strict"/> is <c>true</c> and one
        /// or more variables have no value and no default.
        /// </exception>
        public string Render(
            Dictionary<string, string>? variables = null,
            bool strict = true)
        {
            var merged = new Dictionary<string, string>(
                _defaults, StringComparer.OrdinalIgnoreCase);

            if (variables != null)
            {
                foreach (var kvp in variables)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }

            var missing = new List<string>();

            string result = VariablePattern.Replace(_template, match =>
            {
                string name = match.Groups[1].Value;
                if (merged.TryGetValue(name, out var value))
                    return value;

                if (strict)
                    missing.Add(name);

                return match.Value; // leave as-is in non-strict mode
            });

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Missing values for template variables: {string.Join(", ", missing)}. " +
                    $"Provide them via the variables parameter or set defaults.");
            }

            return result;
        }

        /// <summary>
        /// Renders the template and sends the result as a prompt to
        /// Azure OpenAI via <see cref="Main.GetResponseAsync"/>.
        /// </summary>
        /// <param name="variables">Variable values for rendering.</param>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="maxRetries">Max retries for the API call.</param>
        /// <param name="options">
        /// Optional <see cref="PromptOptions"/> to customize model behavior.
        /// When <c>null</c>, uses library defaults.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The model's response text.</returns>
        public async Task<string?> RenderAndSendAsync(
            Dictionary<string, string>? variables = null,
            string? systemPrompt = null,
            int maxRetries = 3,
            PromptOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            string rendered = Render(variables);
            return await Main.GetResponseAsync(
                rendered, systemPrompt, maxRetries, options, cancellationToken);
        }

        /// <summary>
        /// Renders the template and sends the result as a message in
        /// an existing <see cref="Conversation"/>.
        /// </summary>
        /// <param name="conversation">The conversation to send the message in.</param>
        /// <param name="variables">Variable values for rendering.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The assistant's response text.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="conversation"/> is null.
        /// </exception>
        public async Task<string?> RenderAndSendAsync(
            Conversation conversation,
            Dictionary<string, string>? variables = null,
            CancellationToken cancellationToken = default)
        {
            if (conversation == null)
                throw new ArgumentNullException(nameof(conversation));

            string rendered = Render(variables);
            return await conversation.SendAsync(rendered, cancellationToken);
        }

        // ──────────────── Composition ────────────────

        /// <summary>
        /// Combines this template with another by concatenating them
        /// with a separator. Defaults from both templates are merged
        /// (the other template's defaults take precedence on conflict).
        /// </summary>
        /// <param name="other">The template to append.</param>
        /// <param name="separator">
        /// Separator between the two templates (default: newline + newline).
        /// </param>
        /// <returns>A new <see cref="PromptTemplate"/> combining both.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="other"/> is null.
        /// </exception>
        public PromptTemplate Compose(
            PromptTemplate other,
            string separator = "\n\n")
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            var mergedDefaults = new Dictionary<string, string>(
                _defaults, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in other._defaults)
            {
                mergedDefaults[kvp.Key] = kvp.Value;
            }

            return new PromptTemplate(
                _template + separator + other._template,
                mergedDefaults);
        }

        // ──────────────── Serialization ────────────────

        /// <summary>
        /// Serializes the template to a JSON string.
        /// </summary>
        /// <param name="indented">Whether to format with indentation (default true).</param>
        /// <returns>A JSON string representing the template.</returns>
        public string ToJson(bool indented = true)
        {
            var data = new TemplateData
            {
                Template = _template,
                Defaults = _defaults.Count > 0
                    ? new Dictionary<string, string>(_defaults)
                    : null
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(data, options);
        }

        /// <summary>
        /// Deserializes a template from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string.</param>
        /// <returns>A new <see cref="PromptTemplate"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="json"/> is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the JSON is missing the template field or exceeds security limits.
        /// </exception>
        public static PromptTemplate FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON string cannot be null or empty.", nameof(json));

            // Guard against oversized payloads
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxJsonPayloadBytes)
                throw new InvalidOperationException(
                    $"JSON payload exceeds the maximum allowed size of {MaxJsonPayloadBytes / (1024 * 1024)} MB. " +
                    "This limit prevents denial-of-service from crafted large payloads.");

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var data = JsonSerializer.Deserialize<TemplateData>(json, options);

            if (data == null || string.IsNullOrWhiteSpace(data.Template))
                throw new InvalidOperationException(
                    "Invalid template JSON: missing template field.");

            return new PromptTemplate(data.Template, data.Defaults);
        }

        /// <summary>
        /// Saves the template to a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the output file.</param>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task SaveToFileAsync(
            string filePath,
            bool indented = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            string json = ToJson(indented);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        /// <summary>
        /// Loads a template from a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the JSON file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A new <see cref="PromptTemplate"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the file exceeds the maximum allowed size.</exception>
        public static async Task<PromptTemplate> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            filePath = Path.GetFullPath(filePath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Template file not found: {filePath}", filePath);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxJsonPayloadBytes)
                throw new InvalidOperationException(
                    $"File '{filePath}' is {fileInfo.Length / (1024 * 1024)} MB, " +
                    $"exceeding the maximum allowed size of {MaxJsonPayloadBytes / (1024 * 1024)} MB.");

            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return FromJson(json);
        }

        /// <inheritdoc/>
        public override string ToString() => _template;

        // ──────────────── Serialization DTO ────────────────

        internal class TemplateData
        {
            [JsonPropertyName("template")]
            public string Template { get; set; } = "";

            [JsonPropertyName("defaults")]
            public Dictionary<string, string>? Defaults { get; set; }
        }
    }
}
