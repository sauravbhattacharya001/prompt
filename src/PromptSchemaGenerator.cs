namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Supported JSON Schema property types.
    /// </summary>
    public enum SchemaType
    {
        /// <summary>A string value.</summary>
        String,
        /// <summary>An integer value.</summary>
        Integer,
        /// <summary>A floating-point number.</summary>
        Number,
        /// <summary>A boolean value.</summary>
        Boolean,
        /// <summary>An array of items.</summary>
        Array,
        /// <summary>A nested object.</summary>
        Object
    }

    /// <summary>
    /// Describes a single property in an output schema.
    /// </summary>
    public class SchemaProperty
    {
        /// <summary>Gets the property name.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the property type.</summary>
        public SchemaType Type { get; internal set; }

        /// <summary>Gets the property description.</summary>
        public string Description { get; internal set; } = "";

        /// <summary>Gets whether this property is required.</summary>
        public bool Required { get; internal set; } = true;

        /// <summary>Gets allowed enum values (for string type).</summary>
        public IReadOnlyList<string>? EnumValues { get; internal set; }

        /// <summary>Gets the item type for array properties.</summary>
        public SchemaType? ItemType { get; internal set; }

        /// <summary>Gets nested properties for object/array-of-object types.</summary>
        public IReadOnlyList<SchemaProperty>? NestedProperties { get; internal set; }

        /// <summary>Gets an example value.</summary>
        public string? Example { get; internal set; }
    }

    /// <summary>
    /// A built output schema with methods to generate prompt instructions and JSON Schema.
    /// </summary>
    public class OutputSchema
    {
        /// <summary>Gets the schema name.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>Gets the schema description.</summary>
        public string Description { get; internal set; } = "";

        /// <summary>Gets the properties defined in this schema.</summary>
        public IReadOnlyList<SchemaProperty> Properties { get; internal set; } = Array.Empty<SchemaProperty>();

        /// <summary>
        /// Generates a JSON Schema (draft 2020-12) for this output schema.
        /// </summary>
        /// <param name="indented">Whether to indent the JSON.</param>
        /// <returns>A JSON Schema string.</returns>
        public string ToJsonSchema(bool indented = true)
        {
            var schema = new Dictionary<string, object>
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["title"] = Name,
                ["description"] = Description,
                ["type"] = "object",
                ["properties"] = BuildProperties(Properties),
                ["required"] = Properties.Where(p => p.Required).Select(p => p.Name).ToList(),
                ["additionalProperties"] = false
            };

            return JsonSerializer.Serialize(schema, new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Generates prompt instructions that tell the LLM to produce JSON
        /// conforming to this schema. Include this in your system or user prompt.
        /// </summary>
        /// <param name="includeExample">Whether to include a generated example.</param>
        /// <returns>A prompt instruction string.</returns>
        public string ToPromptInstructions(bool includeExample = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Respond with a JSON object conforming to the following schema.");
            sb.AppendLine($"Schema: {Name}");
            if (!string.IsNullOrEmpty(Description))
                sb.AppendLine($"Description: {Description}");
            sb.AppendLine();
            sb.AppendLine("Fields:");

            foreach (var prop in Properties)
            {
                var req = prop.Required ? "required" : "optional";
                var typeStr = FormatType(prop);
                sb.AppendLine($"- \"{prop.Name}\" ({typeStr}, {req}): {prop.Description}");
                if (prop.EnumValues != null && prop.EnumValues.Count > 0)
                    sb.AppendLine($"  Allowed values: {string.Join(", ", prop.EnumValues.Select(v => $"\"{v}\""))}");
                if (prop.NestedProperties != null && prop.NestedProperties.Count > 0)
                {
                    foreach (var nested in prop.NestedProperties)
                    {
                        var nReq = nested.Required ? "required" : "optional";
                        sb.AppendLine($"  - \"{nested.Name}\" ({FormatType(nested)}, {nReq}): {nested.Description}");
                    }
                }
            }

            if (includeExample)
            {
                sb.AppendLine();
                sb.AppendLine("Example output:");
                sb.AppendLine("```json");
                sb.AppendLine(GenerateExample());
                sb.AppendLine("```");
            }

            sb.AppendLine();
            sb.AppendLine("Return ONLY valid JSON. No markdown, no explanation, no extra text.");

            return sb.ToString();
        }

        /// <summary>
        /// Generates an example JSON object that satisfies this schema.
        /// </summary>
        /// <returns>A JSON string with placeholder values.</returns>
        public string GenerateExample()
        {
            var example = new Dictionary<string, object>();
            foreach (var prop in Properties)
            {
                example[prop.Name] = GenerateExampleValue(prop);
            }

            return JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true });
        }

        private static object GenerateExampleValue(SchemaProperty prop)
        {
            if (prop.Example != null)
            {
                return prop.Type switch
                {
                    SchemaType.Integer => int.TryParse(prop.Example, out var i) ? i : (object)prop.Example,
                    SchemaType.Number => double.TryParse(prop.Example, out var d) ? d : (object)prop.Example,
                    SchemaType.Boolean => bool.TryParse(prop.Example, out var b) ? b : (object)prop.Example,
                    _ => prop.Example
                };
            }

            return prop.Type switch
            {
                SchemaType.String => prop.EnumValues is { Count: > 0 } ? prop.EnumValues[0] : (object)"example",
                SchemaType.Integer => 42,
                SchemaType.Number => 3.14,
                SchemaType.Boolean => true,
                SchemaType.Array => prop.NestedProperties is { Count: > 0 }
                    ? new List<object> { BuildNestedExample(prop.NestedProperties) }
                    : new List<object> { GetDefaultForType(prop.ItemType ?? SchemaType.String) },
                SchemaType.Object => prop.NestedProperties is { Count: > 0 }
                    ? BuildNestedExample(prop.NestedProperties)
                    : new Dictionary<string, object>(),
                _ => "example"
            };
        }

        private static object GetDefaultForType(SchemaType type) => type switch
        {
            SchemaType.String => "example",
            SchemaType.Integer => 1,
            SchemaType.Number => 1.0,
            SchemaType.Boolean => true,
            _ => "example"
        };

        private static Dictionary<string, object> BuildNestedExample(IReadOnlyList<SchemaProperty> props)
        {
            var obj = new Dictionary<string, object>();
            foreach (var p in props)
                obj[p.Name] = GenerateExampleValue(p);
            return obj;
        }

        private static string FormatType(SchemaProperty prop)
        {
            if (prop.Type == SchemaType.Array)
            {
                var itemStr = prop.NestedProperties is { Count: > 0 }
                    ? "object" : (prop.ItemType?.ToString().ToLowerInvariant() ?? "string");
                return $"array of {itemStr}";
            }
            return prop.Type.ToString().ToLowerInvariant();
        }

        private static Dictionary<string, object> BuildProperties(IReadOnlyList<SchemaProperty> properties)
        {
            var dict = new Dictionary<string, object>();
            foreach (var prop in properties)
            {
                var propSchema = new Dictionary<string, object>
                {
                    ["type"] = prop.Type.ToString().ToLowerInvariant(),
                    ["description"] = prop.Description
                };

                if (prop.EnumValues is { Count: > 0 })
                    propSchema["enum"] = prop.EnumValues.ToList();

                if (prop.Type == SchemaType.Array)
                {
                    if (prop.NestedProperties is { Count: > 0 })
                    {
                        propSchema["items"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = BuildProperties(prop.NestedProperties),
                            ["required"] = prop.NestedProperties.Where(p => p.Required).Select(p => p.Name).ToList()
                        };
                    }
                    else
                    {
                        propSchema["items"] = new Dictionary<string, object>
                        {
                            ["type"] = (prop.ItemType ?? SchemaType.String).ToString().ToLowerInvariant()
                        };
                    }
                }
                else if (prop.Type == SchemaType.Object && prop.NestedProperties is { Count: > 0 })
                {
                    propSchema["properties"] = BuildProperties(prop.NestedProperties);
                    propSchema["required"] = prop.NestedProperties.Where(p => p.Required).Select(p => p.Name).ToList();
                }

                dict[prop.Name] = propSchema;
            }
            return dict;
        }
    }

    /// <summary>
    /// Fluent builder for defining structured output schemas. Generates both
    /// JSON Schema and natural-language prompt instructions for LLM structured output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Modern LLM workflows increasingly require structured JSON output. This class
    /// lets you define the expected output shape once, then generate:
    /// <list type="bullet">
    /// <item>A JSON Schema for OpenAI's structured output mode or validation</item>
    /// <item>Natural-language prompt instructions with examples</item>
    /// <item>Example JSON objects for few-shot prompting</item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var schema = PromptSchemaGenerator.Create("SentimentResult")
    ///     .WithDescription("Sentiment analysis result for a text")
    ///     .AddString("sentiment", "The detected sentiment", enumValues: new[] { "positive", "negative", "neutral" })
    ///     .AddNumber("confidence", "Confidence score from 0 to 1", example: "0.95")
    ///     .AddString("explanation", "Brief explanation of the sentiment", required: false)
    ///     .AddArray("keywords", "Key phrases that influenced the analysis", SchemaType.String)
    ///     .Build();
    ///
    /// // Get prompt instructions to include in your prompt
    /// string instructions = schema.ToPromptInstructions();
    ///
    /// // Get JSON Schema for API structured output parameter
    /// string jsonSchema = schema.ToJsonSchema();
    ///
    /// // Get an example output
    /// string example = schema.GenerateExample();
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptSchemaGenerator
    {
        private string _name = "";
        private string _description = "";
        private readonly List<SchemaProperty> _properties = new();

        /// <summary>
        /// Creates a new schema generator with the given name.
        /// </summary>
        /// <param name="name">The schema name (used in JSON Schema title and prompt instructions).</param>
        /// <returns>A new <see cref="PromptSchemaGenerator"/> instance.</returns>
        public static PromptSchemaGenerator Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Schema name cannot be empty.", nameof(name));
            return new PromptSchemaGenerator { _name = name };
        }

        /// <summary>
        /// Sets the description for this schema.
        /// </summary>
        public PromptSchemaGenerator WithDescription(string description)
        {
            _description = description ?? throw new ArgumentNullException(nameof(description));
            return this;
        }

        /// <summary>
        /// Adds a string property.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="description">Property description.</param>
        /// <param name="required">Whether the property is required.</param>
        /// <param name="enumValues">Allowed values (creates an enum constraint).</param>
        /// <param name="example">Example value.</param>
        public PromptSchemaGenerator AddString(string name, string description, bool required = true,
            IEnumerable<string>? enumValues = null, string? example = null)
        {
            _properties.Add(new SchemaProperty
            {
                Name = name,
                Type = SchemaType.String,
                Description = description,
                Required = required,
                EnumValues = enumValues?.ToList(),
                Example = example
            });
            return this;
        }

        /// <summary>
        /// Adds an integer property.
        /// </summary>
        public PromptSchemaGenerator AddInteger(string name, string description, bool required = true, string? example = null)
        {
            _properties.Add(new SchemaProperty
            {
                Name = name,
                Type = SchemaType.Integer,
                Description = description,
                Required = required,
                Example = example
            });
            return this;
        }

        /// <summary>
        /// Adds a number (float/double) property.
        /// </summary>
        public PromptSchemaGenerator AddNumber(string name, string description, bool required = true, string? example = null)
        {
            _properties.Add(new SchemaProperty
            {
                Name = name,
                Type = SchemaType.Number,
                Description = description,
                Required = required,
                Example = example
            });
            return this;
        }

        /// <summary>
        /// Adds a boolean property.
        /// </summary>
        public PromptSchemaGenerator AddBoolean(string name, string description, bool required = true, string? example = null)
        {
            _properties.Add(new SchemaProperty
            {
                Name = name,
                Type = SchemaType.Boolean,
                Description = description,
                Required = required,
                Example = example
            });
            return this;
        }

        /// <summary>
        /// Adds an array property with simple item type.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="description">Property description.</param>
        /// <param name="itemType">Type of items in the array.</param>
        /// <param name="required">Whether the property is required.</param>
        public PromptSchemaGenerator AddArray(string name, string description, SchemaType itemType = SchemaType.String,
            bool required = true)
        {
            _properties.Add(new SchemaProperty
            {
                Name = name,
                Type = SchemaType.Array,
                Description = description,
                Required = required,
                ItemType = itemType
            });
            return this;
        }

        /// <summary>
        /// Adds an array property with object items (nested schema).
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="description">Property description.</param>
        /// <param name="nestedBuilder">Builder action to define the nested object properties.</param>
        /// <param name="required">Whether the property is required.</param>
        public PromptSchemaGenerator AddObjectArray(string name, string description,
            Action<PromptSchemaGenerator> nestedBuilder, bool required = true)
        {
            var nested = new PromptSchemaGenerator();
            nestedBuilder(nested);
            _properties.Add(new SchemaProperty
            {
                Name = name,
                Type = SchemaType.Array,
                Description = description,
                Required = required,
                NestedProperties = nested._properties
            });
            return this;
        }

        /// <summary>
        /// Adds a nested object property.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="description">Property description.</param>
        /// <param name="nestedBuilder">Builder action to define the nested properties.</param>
        /// <param name="required">Whether the property is required.</param>
        public PromptSchemaGenerator AddObject(string name, string description,
            Action<PromptSchemaGenerator> nestedBuilder, bool required = true)
        {
            var nested = new PromptSchemaGenerator();
            nestedBuilder(nested);
            _properties.Add(new SchemaProperty
            {
                Name = name,
                Type = SchemaType.Object,
                Description = description,
                Required = required,
                NestedProperties = nested._properties
            });
            return this;
        }

        /// <summary>
        /// Builds the output schema.
        /// </summary>
        /// <returns>An <see cref="OutputSchema"/> with methods to generate JSON Schema and prompt instructions.</returns>
        public OutputSchema Build()
        {
            if (_properties.Count == 0)
                throw new InvalidOperationException("Schema must have at least one property.");

            return new OutputSchema
            {
                Name = _name,
                Description = _description,
                Properties = _properties.ToList()
            };
        }
    }
}
