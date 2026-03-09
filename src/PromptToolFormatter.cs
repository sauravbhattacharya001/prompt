namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Formats tool/function calling definitions for multiple LLM providers.
    /// Define tools once with a fluent builder, then export to OpenAI, Anthropic,
    /// Gemini, or Mistral format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LLM providers each have their own schema for declaring callable tools.
    /// OpenAI uses <c>tools[].function</c>, Anthropic uses <c>tools[].input_schema</c>,
    /// Gemini uses <c>functionDeclarations</c>, and Mistral mirrors OpenAI with
    /// minor differences. This class normalizes the definition so you write it once.
    /// </para>
    /// <para>
    /// Supports parameter types: string, number, integer, boolean, array, object, enum.
    /// Parameters can be required/optional with defaults and descriptions.
    /// Nested object schemas are supported for complex tools.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var formatter = new PromptToolFormatter();
    /// formatter.AddTool("get_weather")
    ///     .WithDescription("Get current weather for a location")
    ///     .AddParam("location", ParamType.String, "City and state", required: true)
    ///     .AddParam("unit", ParamType.Enum, "Temperature unit", enumValues: new[] { "celsius", "fahrenheit" });
    ///
    /// string openai = formatter.FormatJson(ToolProvider.OpenAI);
    /// string anthropic = formatter.FormatJson(ToolProvider.Anthropic);
    /// </code>
    /// </example>
    public class PromptToolFormatter
    {
        /// <summary>Target LLM provider for tool formatting.</summary>
        public enum ToolProvider
        {
            /// <summary>OpenAI function calling format (tools[].function).</summary>
            OpenAI,
            /// <summary>Anthropic tool use format (tools[].input_schema).</summary>
            Anthropic,
            /// <summary>Google Gemini format (functionDeclarations).</summary>
            Gemini,
            /// <summary>Mistral function calling (OpenAI-compatible with minor diffs).</summary>
            Mistral
        }

        /// <summary>JSON Schema parameter types.</summary>
        public enum ParamType
        {
            String,
            Number,
            Integer,
            Boolean,
            Array,
            Object,
            Enum
        }

        /// <summary>How the model should use tools.</summary>
        public enum ToolChoice
        {
            /// <summary>Model decides whether to call a tool.</summary>
            Auto,
            /// <summary>Model must call at least one tool.</summary>
            Required,
            /// <summary>Model must not call tools.</summary>
            None
        }

        /// <summary>Defines a single parameter in a tool's input schema.</summary>
        public class ToolParam
        {
            public string Name { get; set; } = "";
            public ParamType Type { get; set; } = ParamType.String;
            public string Description { get; set; } = "";
            public bool Required { get; set; } = false;
            public string? DefaultValue { get; set; }
            public string[]? EnumValues { get; set; }
            public ParamType? ArrayItemType { get; set; }
            public List<ToolParam>? ObjectProperties { get; set; }
        }

        /// <summary>A complete tool definition with name, description, and parameters.</summary>
        public class ToolDefinition
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public List<ToolParam> Parameters { get; set; } = new();
            public bool Strict { get; set; } = false;

            /// <summary>Validates the tool definition for completeness.</summary>
            public List<string> Validate()
            {
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(Name))
                    errors.Add("Tool name is required");
                else if (!Regex.IsMatch(Name, @"^[a-zA-Z_][a-zA-Z0-9_-]*$", RegexOptions.None, TimeSpan.FromMilliseconds(500)))
                    errors.Add($"Tool name '{Name}' contains invalid characters (use letters, digits, underscores, hyphens)");
                if (Name.Length > 64)
                    errors.Add($"Tool name '{Name}' exceeds 64 character limit");
                if (string.IsNullOrWhiteSpace(Description))
                    errors.Add($"Tool '{Name}' is missing a description");
                if (Description.Length > 1024)
                    errors.Add($"Tool '{Name}' description exceeds 1024 characters");
                foreach (var p in Parameters)
                {
                    if (string.IsNullOrWhiteSpace(p.Name))
                        errors.Add($"Tool '{Name}' has a parameter with no name");
                    if (p.Type == ParamType.Enum && (p.EnumValues == null || p.EnumValues.Length == 0))
                        errors.Add($"Tool '{Name}' param '{p.Name}' is enum but has no values");
                    if (p.Type == ParamType.Array && p.ArrayItemType == null)
                        errors.Add($"Tool '{Name}' param '{p.Name}' is array but has no item type");
                    if (p.Type == ParamType.Object && (p.ObjectProperties == null || p.ObjectProperties.Count == 0))
                        errors.Add($"Tool '{Name}' param '{p.Name}' is object but has no properties");
                }
                return errors;
            }
        }

        /// <summary>Fluent builder for constructing a tool definition.</summary>
        public class ToolBuilder
        {
            private readonly ToolDefinition _tool;
            private readonly PromptToolFormatter _formatter;

            internal ToolBuilder(PromptToolFormatter formatter, string name)
            {
                _formatter = formatter;
                _tool = new ToolDefinition { Name = name };
            }

            /// <summary>Set the tool description.</summary>
            public ToolBuilder WithDescription(string description)
            {
                _tool.Description = description;
                return this;
            }

            /// <summary>Mark tool as strict mode (OpenAI structured outputs).</summary>
            public ToolBuilder Strict(bool strict = true)
            {
                _tool.Strict = strict;
                return this;
            }

            /// <summary>Add a parameter to the tool.</summary>
            public ToolBuilder AddParam(
                string name,
                ParamType type,
                string description = "",
                bool required = false,
                string? defaultValue = null,
                string[]? enumValues = null,
                ParamType? arrayItemType = null,
                List<ToolParam>? objectProperties = null)
            {
                _tool.Parameters.Add(new ToolParam
                {
                    Name = name,
                    Type = type,
                    Description = description,
                    Required = required,
                    DefaultValue = defaultValue,
                    EnumValues = enumValues,
                    ArrayItemType = arrayItemType ?? (type == ParamType.Array ? ParamType.String : null),
                    ObjectProperties = objectProperties,
                });
                return this;
            }

            /// <summary>Add a required string parameter.</summary>
            public ToolBuilder RequiredString(string name, string description = "")
                => AddParam(name, ParamType.String, description, required: true);

            /// <summary>Add an optional string parameter.</summary>
            public ToolBuilder OptionalString(string name, string description = "", string? defaultValue = null)
                => AddParam(name, ParamType.String, description, required: false, defaultValue: defaultValue);

            /// <summary>Add a required integer parameter.</summary>
            public ToolBuilder RequiredInt(string name, string description = "")
                => AddParam(name, ParamType.Integer, description, required: true);

            /// <summary>Add an enum parameter.</summary>
            public ToolBuilder EnumParam(string name, string[] values, string description = "", bool required = false)
                => AddParam(name, ParamType.Enum, description, required: required, enumValues: values);

            /// <summary>Add an array parameter.</summary>
            public ToolBuilder ArrayParam(string name, ParamType itemType, string description = "", bool required = false)
                => AddParam(name, ParamType.Array, description, required: required, arrayItemType: itemType);

            /// <summary>Finish building and register the tool.</summary>
            public PromptToolFormatter Build()
            {
                _formatter._tools.Add(_tool);
                return _formatter;
            }

            /// <summary>Get the built tool without registering.</summary>
            public ToolDefinition BuildTool()
            {
                _formatter._tools.Add(_tool);
                return _tool;
            }
        }

        /// <summary>Result of formatting tools for a provider.</summary>
        public class FormatResult
        {
            public ToolProvider Provider { get; set; }
            public List<Dictionary<string, object>> Tools { get; set; } = new();
            public int ToolCount { get; set; }
            public object? ToolChoiceValue { get; set; }
            public int EstimatedTokens { get; set; }
        }

        internal readonly List<ToolDefinition> _tools = new();

        /// <summary>All registered tool definitions.</summary>
        public IReadOnlyList<ToolDefinition> Tools => _tools.AsReadOnly();

        /// <summary>Begin building a new tool definition.</summary>
        public ToolBuilder AddTool(string name) => new ToolBuilder(this, name);

        /// <summary>Add a pre-built tool definition.</summary>
        public PromptToolFormatter AddToolDef(ToolDefinition tool)
        {
            _tools.Add(tool);
            return this;
        }

        /// <summary>Remove a tool by name.</summary>
        public bool RemoveTool(string name)
            => _tools.RemoveAll(t => t.Name == name) > 0;

        /// <summary>Get a tool by name.</summary>
        public ToolDefinition? GetTool(string name)
            => _tools.FirstOrDefault(t => t.Name == name);

        /// <summary>Validate all registered tools.</summary>
        public List<string> ValidateAll()
        {
            var errors = new List<string>();
            var names = new HashSet<string>();
            foreach (var tool in _tools)
            {
                if (!names.Add(tool.Name))
                    errors.Add($"Duplicate tool name: '{tool.Name}'");
                errors.AddRange(tool.Validate());
            }
            return errors;
        }

        // ── Formatting ──────────────────────────────────────────────

        /// <summary>Format tools as structured objects for the given provider.</summary>
        public FormatResult Format(ToolProvider provider, ToolChoice choice = ToolChoice.Auto)
        {
            var result = new FormatResult
            {
                Provider = provider,
                ToolCount = _tools.Count,
                ToolChoiceValue = FormatToolChoice(provider, choice),
            };

            foreach (var tool in _tools)
            {
                result.Tools.Add(FormatTool(tool, provider));
            }

            result.EstimatedTokens = EstimateTokens(result);
            return result;
        }

        /// <summary>Format tools as a JSON string.</summary>
        public string FormatJson(ToolProvider provider, ToolChoice choice = ToolChoice.Auto, bool indented = true)
        {
            var result = Format(provider, choice);
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            var output = new Dictionary<string, object>();

            switch (provider)
            {
                case ToolProvider.Gemini:
                    output["functionDeclarations"] = result.Tools.Select(t =>
                    {
                        if (t.TryGetValue("functionDeclarations", out var fd))
                            return fd;
                        return t;
                    }).ToList();
                    break;
                default:
                    output["tools"] = result.Tools;
                    break;
            }

            if (result.ToolChoiceValue != null)
                output["tool_choice"] = result.ToolChoiceValue;

            return JsonSerializer.Serialize(output, options);
        }

        /// <summary>
        /// Format tools as a plain-text description for models that don't support
        /// structured tool definitions (or for system prompt injection).
        /// </summary>
        public string FormatText()
        {
            if (_tools.Count == 0) return "No tools available.";

            var sb = new StringBuilder();
            sb.AppendLine("Available tools:");
            sb.AppendLine();

            foreach (var tool in _tools)
            {
                sb.AppendLine($"## {tool.Name}");
                if (!string.IsNullOrEmpty(tool.Description))
                    sb.AppendLine(tool.Description);

                var required = tool.Parameters.Where(p => p.Required).ToList();
                var optional = tool.Parameters.Where(p => !p.Required).ToList();

                if (required.Count > 0)
                {
                    sb.AppendLine("Required parameters:");
                    foreach (var p in required)
                    {
                        sb.Append($"  - {p.Name} ({TypeName(p.Type)})");
                        if (!string.IsNullOrEmpty(p.Description))
                            sb.Append($": {p.Description}");
                        if (p.Type == ParamType.Enum && p.EnumValues != null)
                            sb.Append($" [values: {string.Join(", ", p.EnumValues)}]");
                        sb.AppendLine();
                    }
                }

                if (optional.Count > 0)
                {
                    sb.AppendLine("Optional parameters:");
                    foreach (var p in optional)
                    {
                        sb.Append($"  - {p.Name} ({TypeName(p.Type)})");
                        if (!string.IsNullOrEmpty(p.Description))
                            sb.Append($": {p.Description}");
                        if (p.DefaultValue != null)
                            sb.Append($" [default: {p.DefaultValue}]");
                        if (p.Type == ParamType.Enum && p.EnumValues != null)
                            sb.Append($" [values: {string.Join(", ", p.EnumValues)}]");
                        sb.AppendLine();
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Estimate total token overhead of tool definitions.</summary>
        public int EstimateTokens()
        {
            int tokens = 10; // base overhead
            foreach (var tool in _tools)
            {
                tokens += 3; // wrapper
                tokens += EstimateStringTokens(tool.Name);
                tokens += EstimateStringTokens(tool.Description);
                foreach (var p in tool.Parameters)
                {
                    tokens += 3; // param wrapper
                    tokens += EstimateStringTokens(p.Name);
                    tokens += EstimateStringTokens(p.Description);
                    if (p.EnumValues != null)
                        tokens += p.EnumValues.Sum(v => EstimateStringTokens(v));
                }
            }
            return tokens;
        }

        /// <summary>Generate a summary of all registered tools.</summary>
        public string Summary()
        {
            if (_tools.Count == 0) return "No tools registered.";

            var sb = new StringBuilder();
            sb.AppendLine($"Tools: {_tools.Count}");
            sb.AppendLine($"Estimated tokens: ~{EstimateTokens()}");
            sb.AppendLine();

            foreach (var tool in _tools)
            {
                var req = tool.Parameters.Count(p => p.Required);
                var opt = tool.Parameters.Count(p => !p.Required);
                sb.AppendLine($"  {tool.Name}: {req} required, {opt} optional params");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Clone all tools from another formatter.</summary>
        public PromptToolFormatter MergeFrom(PromptToolFormatter other)
        {
            foreach (var tool in other._tools)
            {
                if (!_tools.Any(t => t.Name == tool.Name))
                    _tools.Add(tool);
            }
            return this;
        }

        /// <summary>Create a subset formatter with only the named tools.</summary>
        public PromptToolFormatter Subset(params string[] names)
        {
            var sub = new PromptToolFormatter();
            var nameSet = new HashSet<string>(names);
            foreach (var tool in _tools.Where(t => nameSet.Contains(t.Name)))
                sub._tools.Add(tool);
            return sub;
        }

        // ── Private helpers ─────────────────────────────────────────

        private Dictionary<string, object> FormatTool(ToolDefinition tool, ToolProvider provider)
        {
            var schema = BuildParamSchema(tool);

            switch (provider)
            {
                case ToolProvider.OpenAI:
                case ToolProvider.Mistral:
                {
                    var fn = new Dictionary<string, object>
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = schema,
                    };
                    if (tool.Strict && provider == ToolProvider.OpenAI)
                        fn["strict"] = true;

                    return new Dictionary<string, object>
                    {
                        ["type"] = "function",
                        ["function"] = fn,
                    };
                }

                case ToolProvider.Anthropic:
                {
                    return new Dictionary<string, object>
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["input_schema"] = schema,
                    };
                }

                case ToolProvider.Gemini:
                {
                    return new Dictionary<string, object>
                    {
                        ["functionDeclarations"] = new Dictionary<string, object>
                        {
                            ["name"] = tool.Name,
                            ["description"] = tool.Description,
                            ["parameters"] = schema,
                        }
                    };
                }

                default:
                    throw new ArgumentException($"Unknown provider: {provider}");
            }
        }

        private Dictionary<string, object> BuildParamSchema(ToolDefinition tool)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var p in tool.Parameters)
            {
                properties[p.Name] = BuildParamType(p);
                if (p.Required)
                    required.Add(p.Name);
            }

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
            };

            if (required.Count > 0)
                schema["required"] = required;

            if (tool.Strict)
                schema["additionalProperties"] = false;

            return schema;
        }

        private Dictionary<string, object> BuildParamType(ToolParam param)
        {
            var prop = new Dictionary<string, object>();

            switch (param.Type)
            {
                case ParamType.Enum:
                    prop["type"] = "string";
                    if (param.EnumValues != null)
                        prop["enum"] = param.EnumValues;
                    break;

                case ParamType.Array:
                    prop["type"] = "array";
                    prop["items"] = new Dictionary<string, object>
                    {
                        ["type"] = TypeName(param.ArrayItemType ?? ParamType.String)
                    };
                    break;

                case ParamType.Object:
                    prop["type"] = "object";
                    if (param.ObjectProperties != null)
                    {
                        var nested = new Dictionary<string, object>();
                        var nestedReq = new List<string>();
                        foreach (var np in param.ObjectProperties)
                        {
                            nested[np.Name] = BuildParamType(np);
                            if (np.Required) nestedReq.Add(np.Name);
                        }
                        prop["properties"] = nested;
                        if (nestedReq.Count > 0)
                            prop["required"] = nestedReq;
                    }
                    break;

                default:
                    prop["type"] = TypeName(param.Type);
                    break;
            }

            if (!string.IsNullOrEmpty(param.Description))
                prop["description"] = param.Description;

            if (param.DefaultValue != null)
                prop["default"] = param.DefaultValue;

            return prop;
        }

        private static string TypeName(ParamType type) => type switch
        {
            ParamType.String => "string",
            ParamType.Number => "number",
            ParamType.Integer => "integer",
            ParamType.Boolean => "boolean",
            ParamType.Array => "array",
            ParamType.Object => "object",
            ParamType.Enum => "string",
            _ => "string",
        };

        private static object? FormatToolChoice(ToolProvider provider, ToolChoice choice)
        {
            switch (provider)
            {
                case ToolProvider.OpenAI:
                case ToolProvider.Mistral:
                    return choice switch
                    {
                        ToolChoice.Auto => "auto",
                        ToolChoice.Required => "required",
                        ToolChoice.None => "none",
                        _ => "auto",
                    };

                case ToolProvider.Anthropic:
                    return choice switch
                    {
                        ToolChoice.Auto => new Dictionary<string, object> { ["type"] = "auto" },
                        ToolChoice.Required => new Dictionary<string, object> { ["type"] = "any" },
                        ToolChoice.None => null,
                        _ => new Dictionary<string, object> { ["type"] = "auto" },
                    };

                case ToolProvider.Gemini:
                    return choice switch
                    {
                        ToolChoice.Auto => "AUTO",
                        ToolChoice.Required => "ANY",
                        ToolChoice.None => "NONE",
                        _ => "AUTO",
                    };

                default:
                    return null;
            }
        }

        private static int EstimateTokens(FormatResult result)
        {
            // Rough estimate: JSON serialized length / 4
            try
            {
                var json = JsonSerializer.Serialize(result.Tools);
                return json.Length / 4;
            }
            catch
            {
                return 0;
            }
        }

        private static int EstimateStringTokens(string s)
            => string.IsNullOrEmpty(s) ? 0 : Math.Max(1, s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
    }
}
