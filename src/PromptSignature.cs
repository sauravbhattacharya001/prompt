namespace Prompt
{
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The data type of a prompt field. Used for auto-generation
    /// of formatting instructions and response validation.
    /// </summary>
    public enum FieldType
    {
        /// <summary>Free-form text.</summary>
        Text,
        /// <summary>Integer number.</summary>
        Integer,
        /// <summary>Decimal / floating-point number.</summary>
        Number,
        /// <summary>Boolean (true/false, yes/no).</summary>
        Boolean,
        /// <summary>A list/array of items.</summary>
        List,
        /// <summary>A JSON object / dictionary.</summary>
        Json,
        /// <summary>One value from a fixed set (enum).</summary>
        Enum
    }

    /// <summary>
    /// A single field in a prompt signature — either an input parameter
    /// or an expected output field.
    /// </summary>
    public class SignatureField
    {
        /// <summary>Field name (used as key in the response).</summary>
        public string Name { get; }

        /// <summary>Human-readable description of what this field contains.</summary>
        public string Description { get; set; } = "";

        /// <summary>Data type for validation and instruction generation.</summary>
        public FieldType Type { get; set; } = FieldType.Text;

        /// <summary>Whether this field is required (default true).</summary>
        public bool Required { get; set; } = true;

        /// <summary>Allowed values when <see cref="Type"/> is <see cref="FieldType.Enum"/>.</summary>
        public List<string> AllowedValues { get; set; } = new();

        /// <summary>Example value for few-shot instruction generation.</summary>
        public string? Example { get; set; }

        /// <summary>Maximum allowed length (characters for Text, items for List).</summary>
        public int? MaxLength { get; set; }

        /// <summary>Creates a new signature field with the given name.</summary>
        public SignatureField(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Field name cannot be empty.", nameof(name));
            Name = name.Trim();
        }

        /// <summary>Create a text field.</summary>
        public static SignatureField TextF(string name, string description = "") =>
            new(name) { Type = FieldType.Text, Description = description };

        /// <summary>Create an integer field.</summary>
        public static SignatureField IntF(string name, string description = "") =>
            new(name) { Type = FieldType.Integer, Description = description };

        /// <summary>Create a number field.</summary>
        public static SignatureField NumF(string name, string description = "") =>
            new(name) { Type = FieldType.Number, Description = description };

        /// <summary>Create a boolean field.</summary>
        public static SignatureField BoolF(string name, string description = "") =>
            new(name) { Type = FieldType.Boolean, Description = description };

        /// <summary>Create a list field.</summary>
        public static SignatureField ListF(string name, string description = "") =>
            new(name) { Type = FieldType.List, Description = description };

        /// <summary>Create a JSON field.</summary>
        public static SignatureField JsonF(string name, string description = "") =>
            new(name) { Type = FieldType.Json, Description = description };

        /// <summary>Create an enum field with allowed values.</summary>
        public static SignatureField EnumF(string name, string description, params string[] values) =>
            new(name) { Type = FieldType.Enum, Description = description, AllowedValues = values.ToList() };
    }

    /// <summary>
    /// Result of validating a response against a <see cref="PromptSignature"/>.
    /// </summary>
    public class SignatureValidation
    {
        /// <summary>Whether all required fields passed validation.</summary>
        public bool IsValid { get; internal set; } = true;

        /// <summary>Successfully extracted fields.</summary>
        public Dictionary<string, object?> Fields { get; internal set; } = new();

        /// <summary>Validation errors keyed by field name.</summary>
        public Dictionary<string, string> Errors { get; internal set; } = new();

        /// <summary>Fields that were expected but missing from the response.</summary>
        public List<string> MissingFields { get; internal set; } = new();

        /// <summary>Extra fields found in the response that aren't in the signature.</summary>
        public List<string> ExtraFields { get; internal set; } = new();

        /// <summary>Number of fields that passed validation.</summary>
        public int ValidCount => Fields.Count - Errors.Count;

        /// <summary>Total number of expected fields.</summary>
        public int TotalExpected { get; internal set; }

        /// <summary>Validation score from 0.0 to 1.0.</summary>
        public double Score => TotalExpected == 0 ? 1.0 : (double)ValidCount / TotalExpected;

        /// <summary>Human-readable summary of the validation.</summary>
        public string Summary()
        {
            if (IsValid) return $"✓ Valid: {ValidCount}/{TotalExpected} fields extracted.";
            var sb = new StringBuilder();
            sb.AppendLine($"✗ Invalid: {ValidCount}/{TotalExpected} fields OK.");
            foreach (var err in Errors)
                sb.AppendLine($"  - {err.Key}: {err.Value}");
            foreach (var m in MissingFields)
                sb.AppendLine($"  - {m}: missing (required)");
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Defines a strongly-typed prompt signature: named input parameters and
    /// typed output fields. Generates system prompts with formatting instructions
    /// and validates responses against the declared output schema.
    /// </summary>
    /// <remarks>
    /// Inspired by DSPy's typed signatures. Use this to declare what goes in
    /// and what comes out, then let the signature generate the prompt scaffolding
    /// and validate the response automatically.
    ///
    /// <code>
    /// var sig = PromptSignature.Create("Summarize an article")
    ///     .Input("article", "The article text to summarize")
    ///     .Input("max_sentences", "Maximum sentences", FieldType.Integer)
    ///     .Output("summary", "A concise summary")
    ///     .Output("key_points", "Main takeaways", FieldType.List)
    ///     .Output("sentiment", "Overall tone", FieldType.Enum, "positive", "negative", "neutral");
    ///
    /// string systemPrompt = sig.GenerateSystemPrompt();
    /// string userPrompt   = sig.FormatInput(new Dictionary&lt;string, object&gt; {
    ///     ["article"] = articleText,
    ///     ["max_sentences"] = 3
    /// });
    ///
    /// // After getting the LLM response...
    /// var validation = sig.ValidateResponse(llmResponse);
    /// if (validation.IsValid)
    ///     Console.WriteLine(validation.Fields["summary"]);
    /// </code>
    /// </remarks>
    public class PromptSignature
    {
        private static readonly int MaxFields = 50;
        private static readonly int MaxNameLength = 64;

        private string _task = "";
        private readonly List<SignatureField> _inputs = new();
        private readonly List<SignatureField> _outputs = new();
        private string _outputFormat = "json";  // "json" or "yaml" or "markdown"

        /// <summary>The task description for this signature.</summary>
        public string Task => _task;

        /// <summary>Declared input fields.</summary>
        public IReadOnlyList<SignatureField> Inputs => _inputs.AsReadOnly();

        /// <summary>Declared output fields.</summary>
        public IReadOnlyList<SignatureField> Outputs => _outputs.AsReadOnly();

        /// <summary>Output format for prompt generation ("json", "yaml", "markdown").</summary>
        public string OutputFormat => _outputFormat;

        /// <summary>Create a new signature with a task description.</summary>
        public static PromptSignature Create(string task)
        {
            if (string.IsNullOrWhiteSpace(task))
                throw new ArgumentException("Task description cannot be empty.", nameof(task));
            return new PromptSignature { _task = task.Trim() };
        }

        /// <summary>Add an input field.</summary>
        public PromptSignature Input(string name, string description = "", FieldType type = FieldType.Text)
        {
            ValidateFieldName(name, _inputs);
            var field = new SignatureField(name) { Description = description, Type = type };
            _inputs.Add(field);
            return this;
        }

        /// <summary>Add an input field with a custom <see cref="SignatureField"/>.</summary>
        public PromptSignature Input(SignatureField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            ValidateFieldName(field.Name, _inputs);
            _inputs.Add(field);
            return this;
        }

        /// <summary>Add an output field.</summary>
        public PromptSignature Output(string name, string description = "", FieldType type = FieldType.Text)
        {
            ValidateFieldName(name, _outputs);
            var field = new SignatureField(name) { Description = description, Type = type };
            _outputs.Add(field);
            return this;
        }

        /// <summary>Add an enum output field with allowed values.</summary>
        public PromptSignature Output(string name, string description, FieldType type, params string[] allowedValues)
        {
            ValidateFieldName(name, _outputs);
            var field = new SignatureField(name) { Description = description, Type = type };
            if (type == FieldType.Enum)
                field.AllowedValues = allowedValues.ToList();
            _outputs.Add(field);
            return this;
        }

        /// <summary>Add an output field with a custom <see cref="SignatureField"/>.</summary>
        public PromptSignature Output(SignatureField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            ValidateFieldName(field.Name, _outputs);
            _outputs.Add(field);
            return this;
        }

        /// <summary>Set the output format: "json" (default), "yaml", or "markdown".</summary>
        public PromptSignature Format(string format)
        {
            var f = (format ?? "json").ToLowerInvariant();
            if (f != "json" && f != "yaml" && f != "markdown")
                throw new ArgumentException("Format must be 'json', 'yaml', or 'markdown'.", nameof(format));
            _outputFormat = f;
            return this;
        }

        /// <summary>
        /// Generate the system prompt with task description, input/output schema,
        /// type constraints, and formatting instructions.
        /// </summary>
        public string GenerateSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine(_task);
            sb.AppendLine();

            // Input schema
            if (_inputs.Count > 0)
            {
                sb.AppendLine("## Inputs");
                sb.AppendLine("You will receive the following inputs:");
                foreach (var f in _inputs)
                {
                    sb.Append($"- **{f.Name}**");
                    if (!string.IsNullOrEmpty(f.Description))
                        sb.Append($": {f.Description}");
                    sb.Append($" ({TypeLabel(f.Type)}");
                    if (f.Type == FieldType.Enum && f.AllowedValues.Count > 0)
                        sb.Append($", one of: {string.Join(", ", f.AllowedValues)}");
                    if (!f.Required)
                        sb.Append(", optional");
                    sb.AppendLine(")");
                }
                sb.AppendLine();
            }

            // Output schema
            if (_outputs.Count > 0)
            {
                sb.AppendLine("## Output");
                sb.AppendLine($"Respond with a {_outputFormat.ToUpperInvariant()} object containing these fields:");
                foreach (var f in _outputs)
                {
                    sb.Append($"- **{f.Name}**");
                    if (!string.IsNullOrEmpty(f.Description))
                        sb.Append($": {f.Description}");
                    sb.Append($" ({TypeLabel(f.Type)}");
                    if (f.Type == FieldType.Enum && f.AllowedValues.Count > 0)
                        sb.Append($", one of: {string.Join(", ", f.AllowedValues)}");
                    if (f.MaxLength.HasValue)
                        sb.Append(f.Type == FieldType.List ? $", max {f.MaxLength} items" : $", max {f.MaxLength} chars");
                    if (!f.Required)
                        sb.Append(", optional");
                    sb.AppendLine(")");
                }
                sb.AppendLine();

                // Example output template
                sb.AppendLine("## Format");
                if (_outputFormat == "json")
                {
                    sb.AppendLine("```json");
                    sb.AppendLine("{");
                    for (int i = 0; i < _outputs.Count; i++)
                    {
                        var f = _outputs[i];
                        string comma = i < _outputs.Count - 1 ? "," : "";
                        sb.AppendLine($"  \"{f.Name}\": {TypePlaceholder(f)}{comma}");
                    }
                    sb.AppendLine("}");
                    sb.AppendLine("```");
                }
                else if (_outputFormat == "yaml")
                {
                    sb.AppendLine("```yaml");
                    foreach (var f in _outputs)
                        sb.AppendLine($"{f.Name}: {TypePlaceholder(f)}");
                    sb.AppendLine("```");
                }
                else // markdown
                {
                    foreach (var f in _outputs)
                        sb.AppendLine($"### {f.Name}\n{TypePlaceholder(f)}\n");
                }
                sb.AppendLine("Respond with ONLY the formatted output. No extra commentary.");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Format input values into a user prompt string.
        /// Validates that all required inputs are present.
        /// </summary>
        public string FormatInput(Dictionary<string, object?> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            // Validate required inputs
            foreach (var f in _inputs)
            {
                if (f.Required && (!values.ContainsKey(f.Name) || values[f.Name] == null))
                    throw new ArgumentException($"Required input '{f.Name}' is missing.");
            }

            // Check for unknown inputs
            var knownNames = new HashSet<string>(_inputs.Select(f => f.Name));
            foreach (var key in values.Keys)
            {
                if (!knownNames.Contains(key))
                    throw new ArgumentException($"Unknown input field '{key}'. Expected: {string.Join(", ", knownNames)}");
            }

            var sb = new StringBuilder();
            foreach (var f in _inputs)
            {
                if (values.TryGetValue(f.Name, out var val) && val != null)
                {
                    sb.AppendLine($"**{f.Name}:** {FormatValue(val)}");
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Validate an LLM response against the output schema.
        /// Attempts to parse the response and check each field's type and constraints.
        /// </summary>
        public SignatureValidation ValidateResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new SignatureValidation
                {
                    IsValid = false,
                    TotalExpected = _outputs.Count,
                    MissingFields = _outputs.Where(f => f.Required).Select(f => f.Name).ToList()
                };

            var validation = new SignatureValidation { TotalExpected = _outputs.Count };

            if (_outputFormat == "json")
                ValidateJsonResponse(response, validation);
            else if (_outputFormat == "yaml")
                ValidateYamlResponse(response, validation);
            else
                ValidateMarkdownResponse(response, validation);

            return validation;
        }

        /// <summary>
        /// Generate a compact signature string (DSPy-style notation).
        /// Example: "article, max_sentences -> summary, key_points, sentiment"
        /// </summary>
        public string ToSignatureString()
        {
            var inputs = string.Join(", ", _inputs.Select(f => f.Name));
            var outputs = string.Join(", ", _outputs.Select(f => f.Name));
            return $"{inputs} -> {outputs}";
        }

        /// <summary>
        /// Parse a compact signature string and create a basic PromptSignature.
        /// Format: "input1, input2 -> output1, output2"
        /// </summary>
        public static PromptSignature Parse(string signatureString, string task = "")
        {
            if (string.IsNullOrWhiteSpace(signatureString))
                throw new ArgumentException("Signature string cannot be empty.", nameof(signatureString));

            var parts = signatureString.Split("->", 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new FormatException("Signature must contain '->' separating inputs and outputs.");

            var sig = Create(string.IsNullOrWhiteSpace(task) ? "Process the given inputs." : task);

            foreach (var name in parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                sig.Input(name);
            foreach (var name in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                sig.Output(name);

            return sig;
        }

        /// <summary>Export the signature to a JSON schema document.</summary>
        public string ToJsonSchema()
        {
            var schema = new Dictionary<string, object>
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["title"] = _task,
                ["type"] = "object",
                ["properties"] = _outputs.ToDictionary(
                    f => f.Name,
                    f => BuildFieldSchema(f)),
                ["required"] = _outputs.Where(f => f.Required).Select(f => f.Name).ToList()
            };
            return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Export the full signature definition to JSON for persistence/sharing.</summary>
        public string ToJson()
        {
            var dto = new
            {
                task = _task,
                format = _outputFormat,
                inputs = _inputs.Select(f => new
                {
                    name = f.Name,
                    description = f.Description,
                    type = f.Type.ToString().ToLowerInvariant(),
                    required = f.Required,
                    allowedValues = f.AllowedValues.Count > 0 ? f.AllowedValues : null,
                    example = f.Example,
                    maxLength = f.MaxLength
                }),
                outputs = _outputs.Select(f => new
                {
                    name = f.Name,
                    description = f.Description,
                    type = f.Type.ToString().ToLowerInvariant(),
                    required = f.Required,
                    allowedValues = f.AllowedValues.Count > 0 ? f.AllowedValues : null,
                    example = f.Example,
                    maxLength = f.MaxLength
                })
            };
            return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        }

        // ── Private helpers ────────────────────────────────────────

        private void ValidateFieldName(string name, List<SignatureField> existing)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Field name cannot be empty.");
            if (name.Length > MaxNameLength)
                throw new ArgumentException($"Field name exceeds {MaxNameLength} characters.");
            if (existing.Count >= MaxFields)
                throw new InvalidOperationException($"Maximum of {MaxFields} fields allowed.");
            if (existing.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Duplicate field name: '{name}'.");
        }

        private static string TypeLabel(FieldType type) => type switch
        {
            FieldType.Text => "text",
            FieldType.Integer => "integer",
            FieldType.Number => "number",
            FieldType.Boolean => "boolean",
            FieldType.List => "list",
            FieldType.Json => "JSON object",
            FieldType.Enum => "enum",
            _ => "text"
        };

        private static string TypePlaceholder(SignatureField f) => f.Type switch
        {
            FieldType.Text => f.Example != null ? $"\"{f.Example}\"" : "\"...\"",
            FieldType.Integer => f.Example ?? "0",
            FieldType.Number => f.Example ?? "0.0",
            FieldType.Boolean => f.Example ?? "true",
            FieldType.List => f.Example ?? "[\"...\"]",
            FieldType.Json => f.Example ?? "{}",
            FieldType.Enum => f.AllowedValues.Count > 0 ? $"\"{f.AllowedValues[0]}\"" : "\"...\"",
            _ => "\"...\""
        };

        private static string FormatValue(object val)
        {
            if (val is string s) return s;
            if (val is IEnumerable<object> list)
                return JsonSerializer.Serialize(list);
            return val.ToString() ?? "";
        }

        private static Dictionary<string, object> BuildFieldSchema(SignatureField f)
        {
            var schema = new Dictionary<string, object>();
            switch (f.Type)
            {
                case FieldType.Text:
                    schema["type"] = "string";
                    if (f.MaxLength.HasValue) schema["maxLength"] = f.MaxLength.Value;
                    break;
                case FieldType.Integer:
                    schema["type"] = "integer";
                    break;
                case FieldType.Number:
                    schema["type"] = "number";
                    break;
                case FieldType.Boolean:
                    schema["type"] = "boolean";
                    break;
                case FieldType.List:
                    schema["type"] = "array";
                    schema["items"] = new Dictionary<string, string> { ["type"] = "string" };
                    if (f.MaxLength.HasValue) schema["maxItems"] = f.MaxLength.Value;
                    break;
                case FieldType.Json:
                    schema["type"] = "object";
                    break;
                case FieldType.Enum:
                    schema["type"] = "string";
                    if (f.AllowedValues.Count > 0) schema["enum"] = f.AllowedValues;
                    break;
            }
            if (!string.IsNullOrEmpty(f.Description)) schema["description"] = f.Description;
            return schema;
        }

        private void ValidateJsonResponse(string response, SignatureValidation validation)
        {
            // Extract JSON from the response (may be wrapped in markdown code block)
            string jsonStr = ExtractJsonBlock(response);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(jsonStr);
            }
            catch (JsonException ex)
            {
                validation.IsValid = false;
                validation.Errors["_parse"] = $"Invalid JSON: {ex.Message}";
                validation.MissingFields = _outputs.Where(f => f.Required).Select(f => f.Name).ToList();
                return;
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                validation.IsValid = false;
                validation.Errors["_parse"] = "Expected a JSON object at root level.";
                validation.MissingFields = _outputs.Where(f => f.Required).Select(f => f.Name).ToList();
                return;
            }

            var outputNames = new HashSet<string>(_outputs.Select(f => f.Name));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!outputNames.Contains(prop.Name))
                    validation.ExtraFields.Add(prop.Name);
            }

            foreach (var field in _outputs)
            {
                if (!doc.RootElement.TryGetProperty(field.Name, out var element))
                {
                    if (field.Required)
                    {
                        validation.MissingFields.Add(field.Name);
                        validation.IsValid = false;
                    }
                    continue;
                }

                var error = ValidateElement(element, field);
                if (error != null)
                {
                    validation.Errors[field.Name] = error;
                    validation.IsValid = false;
                }

                validation.Fields[field.Name] = ExtractValue(element, field.Type);
            }
        }

        private void ValidateYamlResponse(string response, SignatureValidation validation)
        {
            // Simple YAML-like key: value parsing
            var lines = response.Split('\n').Select(l => l.TrimEnd()).ToList();
            var parsed = new Dictionary<string, string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("```")) continue;
                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line[..colonIdx].Trim();
                    var val = line[(colonIdx + 1)..].Trim();
                    parsed[key] = val;
                }
            }

            var outputNames = new HashSet<string>(_outputs.Select(f => f.Name));
            foreach (var key in parsed.Keys)
            {
                if (!outputNames.Contains(key))
                    validation.ExtraFields.Add(key);
            }

            foreach (var field in _outputs)
            {
                if (!parsed.TryGetValue(field.Name, out var value))
                {
                    if (field.Required)
                    {
                        validation.MissingFields.Add(field.Name);
                        validation.IsValid = false;
                    }
                    continue;
                }
                validation.Fields[field.Name] = value;
            }
        }

        private void ValidateMarkdownResponse(string response, SignatureValidation validation)
        {
            // Parse ### heading sections
            var sections = new Dictionary<string, string>();
            string? currentKey = null;
            var currentValue = new StringBuilder();

            foreach (var line in response.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("### "))
                {
                    if (currentKey != null)
                        sections[currentKey] = currentValue.ToString().Trim();
                    currentKey = trimmed[4..].Trim();
                    currentValue.Clear();
                }
                else if (currentKey != null)
                {
                    currentValue.AppendLine(line);
                }
            }
            if (currentKey != null)
                sections[currentKey] = currentValue.ToString().Trim();

            var outputNames = new HashSet<string>(_outputs.Select(f => f.Name));
            foreach (var key in sections.Keys)
            {
                if (!outputNames.Contains(key))
                    validation.ExtraFields.Add(key);
            }

            foreach (var field in _outputs)
            {
                if (!sections.TryGetValue(field.Name, out var value))
                {
                    if (field.Required)
                    {
                        validation.MissingFields.Add(field.Name);
                        validation.IsValid = false;
                    }
                    continue;
                }
                validation.Fields[field.Name] = value;
            }
        }

        private static string? ValidateElement(JsonElement element, SignatureField field)
        {
            switch (field.Type)
            {
                case FieldType.Text:
                    if (element.ValueKind != JsonValueKind.String)
                        return $"Expected string, got {element.ValueKind}.";
                    if (field.MaxLength.HasValue && element.GetString()!.Length > field.MaxLength.Value)
                        return $"Exceeds max length of {field.MaxLength.Value} characters.";
                    break;

                case FieldType.Integer:
                    if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out _))
                        return $"Expected integer, got {element.ValueKind}.";
                    break;

                case FieldType.Number:
                    if (element.ValueKind != JsonValueKind.Number)
                        return $"Expected number, got {element.ValueKind}.";
                    break;

                case FieldType.Boolean:
                    if (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
                        return $"Expected boolean, got {element.ValueKind}.";
                    break;

                case FieldType.List:
                    if (element.ValueKind != JsonValueKind.Array)
                        return $"Expected array, got {element.ValueKind}.";
                    if (field.MaxLength.HasValue && element.GetArrayLength() > field.MaxLength.Value)
                        return $"Array exceeds max {field.MaxLength.Value} items.";
                    break;

                case FieldType.Json:
                    if (element.ValueKind != JsonValueKind.Object)
                        return $"Expected object, got {element.ValueKind}.";
                    break;

                case FieldType.Enum:
                    if (element.ValueKind != JsonValueKind.String)
                        return $"Expected string, got {element.ValueKind}.";
                    if (field.AllowedValues.Count > 0)
                    {
                        var val = element.GetString()!;
                        if (!field.AllowedValues.Contains(val, StringComparer.OrdinalIgnoreCase))
                            return $"Value '{val}' is not allowed. Expected: {string.Join(", ", field.AllowedValues)}.";
                    }
                    break;
            }
            return null;
        }

        private static object? ExtractValue(JsonElement element, FieldType type) => type switch
        {
            FieldType.Text => element.GetString(),
            FieldType.Integer => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            FieldType.Number => element.GetDouble(),
            FieldType.Boolean => element.GetBoolean(),
            FieldType.List => element.EnumerateArray().Select(e => e.ToString()).ToList(),
            FieldType.Json => element.GetRawText(),
            FieldType.Enum => element.GetString(),
            _ => element.ToString()
        };

        private static string ExtractJsonBlock(string response)
        {
            // Try to extract JSON from markdown code block
            var match = Regex.Match(response, @"```(?:json)?\s*\n?([\s\S]*?)\n?```", RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));
            if (match.Success)
                return match.Groups[1].Value.Trim();

            // Try to find a JSON object directly
            int braceStart = response.IndexOf('{');
            int braceEnd = response.LastIndexOf('}');
            if (braceStart >= 0 && braceEnd > braceStart)
                return response[braceStart..(braceEnd + 1)];

            return response.Trim();
        }
    }
}
