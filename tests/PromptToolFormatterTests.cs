namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Xunit;
    using static PromptToolFormatter;

    public class PromptToolFormatterTests
    {
        // ── Builder ─────────────────────────────────────────────────

        [Fact]
        public void AddTool_CreatesToolWithName()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("get_weather")
                .WithDescription("Get weather")
                .Build();

            Assert.Single(fmt.Tools);
            Assert.Equal("get_weather", fmt.Tools[0].Name);
            Assert.Equal("Get weather", fmt.Tools[0].Description);
        }

        [Fact]
        public void Builder_RequiredString_Shorthand()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search")
                .RequiredString("query", "Search query")
                .Build();

            var param = fmt.Tools[0].Parameters[0];
            Assert.Equal("query", param.Name);
            Assert.Equal(ParamType.String, param.Type);
            Assert.True(param.Required);
        }

        [Fact]
        public void Builder_OptionalString_WithDefault()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search")
                .OptionalString("lang", "Language", defaultValue: "en")
                .Build();

            var param = fmt.Tools[0].Parameters[0];
            Assert.False(param.Required);
            Assert.Equal("en", param.DefaultValue);
        }

        [Fact]
        public void Builder_RequiredInt()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("paginate")
                .WithDescription("Paginate")
                .RequiredInt("page", "Page number")
                .Build();

            var param = fmt.Tools[0].Parameters[0];
            Assert.Equal(ParamType.Integer, param.Type);
            Assert.True(param.Required);
        }

        [Fact]
        public void Builder_EnumParam()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("convert")
                .WithDescription("Convert")
                .EnumParam("unit", new[] { "celsius", "fahrenheit" }, required: true)
                .Build();

            var param = fmt.Tools[0].Parameters[0];
            Assert.Equal(ParamType.Enum, param.Type);
            Assert.Equal(new[] { "celsius", "fahrenheit" }, param.EnumValues);
        }

        [Fact]
        public void Builder_ArrayParam()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("bulk")
                .WithDescription("Bulk ops")
                .ArrayParam("ids", ParamType.Integer, required: true)
                .Build();

            var param = fmt.Tools[0].Parameters[0];
            Assert.Equal(ParamType.Array, param.Type);
            Assert.Equal(ParamType.Integer, param.ArrayItemType);
        }

        [Fact]
        public void Builder_Strict()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("strict_tool")
                .WithDescription("Strict")
                .Strict()
                .Build();

            Assert.True(fmt.Tools[0].Strict);
        }

        [Fact]
        public void Builder_MultipleParams()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("get_weather")
                .WithDescription("Weather")
                .RequiredString("location", "City")
                .EnumParam("unit", new[] { "c", "f" })
                .OptionalString("lang", "Language")
                .Build();

            Assert.Equal(3, fmt.Tools[0].Parameters.Count);
        }

        [Fact]
        public void AddToolDef_RegistersTool()
        {
            var fmt = new PromptToolFormatter();
            var tool = new ToolDefinition { Name = "test", Description = "Test tool" };
            fmt.AddToolDef(tool);
            Assert.Single(fmt.Tools);
        }

        [Fact]
        public void RemoveTool_RemovesByName()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("a").WithDescription("A").Build();
            fmt.AddTool("b").WithDescription("B").Build();

            Assert.True(fmt.RemoveTool("a"));
            Assert.Single(fmt.Tools);
            Assert.Equal("b", fmt.Tools[0].Name);
        }

        [Fact]
        public void RemoveTool_ReturnsFalseForMissing()
        {
            var fmt = new PromptToolFormatter();
            Assert.False(fmt.RemoveTool("nope"));
        }

        [Fact]
        public void GetTool_FindsByName()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search").WithDescription("Search").Build();
            Assert.NotNull(fmt.GetTool("search"));
            Assert.Null(fmt.GetTool("missing"));
        }

        // ── Validation ──────────────────────────────────────────────

        [Fact]
        public void Validate_MissingName()
        {
            var tool = new ToolDefinition { Name = "", Description = "test" };
            var errors = tool.Validate();
            Assert.Contains(errors, e => e.Contains("name is required"));
        }

        [Fact]
        public void Validate_InvalidNameChars()
        {
            var tool = new ToolDefinition { Name = "bad name!", Description = "test" };
            var errors = tool.Validate();
            Assert.Contains(errors, e => e.Contains("invalid characters"));
        }

        [Fact]
        public void Validate_NameTooLong()
        {
            var tool = new ToolDefinition { Name = new string('a', 65), Description = "test" };
            var errors = tool.Validate();
            Assert.Contains(errors, e => e.Contains("64 character"));
        }

        [Fact]
        public void Validate_MissingDescription()
        {
            var tool = new ToolDefinition { Name = "test", Description = "" };
            var errors = tool.Validate();
            Assert.Contains(errors, e => e.Contains("missing a description"));
        }

        [Fact]
        public void Validate_EnumWithoutValues()
        {
            var tool = new ToolDefinition
            {
                Name = "test",
                Description = "test",
                Parameters = new List<ToolParam>
                {
                    new ToolParam { Name = "p", Type = ParamType.Enum }
                }
            };
            var errors = tool.Validate();
            Assert.Contains(errors, e => e.Contains("enum but has no values"));
        }

        [Fact]
        public void Validate_ArrayWithoutItemType()
        {
            var tool = new ToolDefinition
            {
                Name = "test",
                Description = "test",
                Parameters = new List<ToolParam>
                {
                    new ToolParam { Name = "p", Type = ParamType.Array, ArrayItemType = null }
                }
            };
            var errors = tool.Validate();
            Assert.Contains(errors, e => e.Contains("array but has no item type"));
        }

        [Fact]
        public void Validate_ObjectWithoutProperties()
        {
            var tool = new ToolDefinition
            {
                Name = "test",
                Description = "test",
                Parameters = new List<ToolParam>
                {
                    new ToolParam { Name = "p", Type = ParamType.Object }
                }
            };
            var errors = tool.Validate();
            Assert.Contains(errors, e => e.Contains("object but has no properties"));
        }

        [Fact]
        public void ValidateAll_DetectsDuplicateNames()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("dup").WithDescription("A").Build();
            fmt.AddTool("dup").WithDescription("B").Build();

            var errors = fmt.ValidateAll();
            Assert.Contains(errors, e => e.Contains("Duplicate tool name"));
        }

        [Fact]
        public void ValidateAll_PassesForValidTools()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search the web")
                .RequiredString("query")
                .Build();

            var errors = fmt.ValidateAll();
            Assert.Empty(errors);
        }

        // ── OpenAI Format ───────────────────────────────────────────

        [Fact]
        public void Format_OpenAI_CorrectStructure()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("get_weather")
                .WithDescription("Get weather")
                .RequiredString("location", "City name")
                .EnumParam("unit", new[] { "celsius", "fahrenheit" })
                .Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            Assert.Single(result.Tools);

            var tool = result.Tools[0];
            Assert.Equal("function", tool["type"]);
            Assert.True(tool.ContainsKey("function"));

            var fn = (Dictionary<string, object>)tool["function"];
            Assert.Equal("get_weather", fn["name"]);
            Assert.Equal("Get weather", fn["description"]);

            var parameters = (Dictionary<string, object>)fn["parameters"];
            Assert.Equal("object", parameters["type"]);

            var required = (List<string>)parameters["required"];
            Assert.Contains("location", required);
        }

        [Fact]
        public void Format_OpenAI_StrictAddsFlag()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("strict_fn")
                .WithDescription("Strict")
                .Strict()
                .RequiredString("input")
                .Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            var fn = (Dictionary<string, object>)result.Tools[0]["function"];
            Assert.True((bool)fn["strict"]);

            var parameters = (Dictionary<string, object>)fn["parameters"];
            Assert.False((bool)parameters["additionalProperties"]);
        }

        // ── Anthropic Format ────────────────────────────────────────

        [Fact]
        public void Format_Anthropic_CorrectStructure()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search")
                .RequiredString("query")
                .Build();

            var result = fmt.Format(ToolProvider.Anthropic);
            var tool = result.Tools[0];

            Assert.Equal("search", tool["name"]);
            Assert.Equal("Search", tool["description"]);
            Assert.True(tool.ContainsKey("input_schema"));
            Assert.False(tool.ContainsKey("type")); // No "function" wrapper
        }

        [Fact]
        public void Format_Anthropic_ToolChoiceAuto()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("t").WithDescription("T").Build();

            var result = fmt.Format(ToolProvider.Anthropic, ToolChoice.Auto);
            var choice = (Dictionary<string, object>)result.ToolChoiceValue!;
            Assert.Equal("auto", choice["type"]);
        }

        [Fact]
        public void Format_Anthropic_ToolChoiceRequired()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("t").WithDescription("T").Build();

            var result = fmt.Format(ToolProvider.Anthropic, ToolChoice.Required);
            var choice = (Dictionary<string, object>)result.ToolChoiceValue!;
            Assert.Equal("any", choice["type"]);
        }

        [Fact]
        public void Format_Anthropic_ToolChoiceNone()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("t").WithDescription("T").Build();

            var result = fmt.Format(ToolProvider.Anthropic, ToolChoice.None);
            Assert.Null(result.ToolChoiceValue);
        }

        // ── Gemini Format ───────────────────────────────────────────

        [Fact]
        public void Format_Gemini_UsesFunctionDeclarations()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("calc")
                .WithDescription("Calculator")
                .RequiredString("expression")
                .Build();

            var result = fmt.Format(ToolProvider.Gemini);
            var tool = result.Tools[0];
            Assert.True(tool.ContainsKey("functionDeclarations"));
        }

        [Fact]
        public void Format_Gemini_ToolChoice()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("t").WithDescription("T").Build();

            Assert.Equal("AUTO", fmt.Format(ToolProvider.Gemini, ToolChoice.Auto).ToolChoiceValue);
            Assert.Equal("ANY", fmt.Format(ToolProvider.Gemini, ToolChoice.Required).ToolChoiceValue);
            Assert.Equal("NONE", fmt.Format(ToolProvider.Gemini, ToolChoice.None).ToolChoiceValue);
        }

        // ── Mistral Format ──────────────────────────────────────────

        [Fact]
        public void Format_Mistral_SimilarToOpenAI()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search")
                .RequiredString("q")
                .Build();

            var result = fmt.Format(ToolProvider.Mistral);
            var tool = result.Tools[0];
            Assert.Equal("function", tool["type"]);
            Assert.True(tool.ContainsKey("function"));
        }

        [Fact]
        public void Format_Mistral_NoStrictFlag()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("strict_fn")
                .WithDescription("Strict")
                .Strict()
                .Build();

            var result = fmt.Format(ToolProvider.Mistral);
            var fn = (Dictionary<string, object>)result.Tools[0]["function"];
            Assert.False(fn.ContainsKey("strict"));
        }

        // ── Parameter Schema ────────────────────────────────────────

        [Fact]
        public void Schema_NumberType()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("calc")
                .WithDescription("Calculate")
                .AddParam("value", ParamType.Number, "Numeric value")
                .Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            var fn = (Dictionary<string, object>)result.Tools[0]["function"];
            var parameters = (Dictionary<string, object>)fn["parameters"];
            var properties = (Dictionary<string, object>)parameters["properties"];
            var value = (Dictionary<string, object>)properties["value"];
            Assert.Equal("number", value["type"]);
        }

        [Fact]
        public void Schema_BooleanType()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("toggle")
                .WithDescription("Toggle")
                .AddParam("enabled", ParamType.Boolean, "On/off")
                .Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            var fn = (Dictionary<string, object>)result.Tools[0]["function"];
            var parameters = (Dictionary<string, object>)fn["parameters"];
            var properties = (Dictionary<string, object>)parameters["properties"];
            var enabled = (Dictionary<string, object>)properties["enabled"];
            Assert.Equal("boolean", enabled["type"]);
        }

        [Fact]
        public void Schema_ArrayWithItemType()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("bulk")
                .WithDescription("Bulk")
                .ArrayParam("ids", ParamType.Integer, "ID list", required: true)
                .Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            var fn = (Dictionary<string, object>)result.Tools[0]["function"];
            var parameters = (Dictionary<string, object>)fn["parameters"];
            var properties = (Dictionary<string, object>)parameters["properties"];
            var ids = (Dictionary<string, object>)properties["ids"];
            Assert.Equal("array", ids["type"]);
            var items = (Dictionary<string, object>)ids["items"];
            Assert.Equal("integer", items["type"]);
        }

        [Fact]
        public void Schema_NestedObject()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("create")
                .WithDescription("Create")
                .AddParam("address", ParamType.Object, "Address", objectProperties: new List<ToolParam>
                {
                    new ToolParam { Name = "street", Type = ParamType.String, Required = true },
                    new ToolParam { Name = "city", Type = ParamType.String, Required = true },
                    new ToolParam { Name = "zip", Type = ParamType.String },
                })
                .Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            var fn = (Dictionary<string, object>)result.Tools[0]["function"];
            var parameters = (Dictionary<string, object>)fn["parameters"];
            var properties = (Dictionary<string, object>)parameters["properties"];
            var address = (Dictionary<string, object>)properties["address"];
            Assert.Equal("object", address["type"]);
            Assert.True(address.ContainsKey("properties"));
            Assert.True(address.ContainsKey("required"));
        }

        [Fact]
        public void Schema_DefaultValue()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search")
                .OptionalString("lang", "Language", defaultValue: "en")
                .Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            var fn = (Dictionary<string, object>)result.Tools[0]["function"];
            var parameters = (Dictionary<string, object>)fn["parameters"];
            var properties = (Dictionary<string, object>)parameters["properties"];
            var lang = (Dictionary<string, object>)properties["lang"];
            Assert.Equal("en", lang["default"]);
        }

        // ── JSON Output ─────────────────────────────────────────────

        [Fact]
        public void FormatJson_ValidJson()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search")
                .RequiredString("query")
                .Build();

            var json = fmt.FormatJson(ToolProvider.OpenAI);
            Assert.NotEmpty(json);
            // Should be valid JSON
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("tools", out _));
        }

        [Fact]
        public void FormatJson_Gemini_UsesFunctionDeclarations()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("test").WithDescription("Test").Build();

            var json = fmt.FormatJson(ToolProvider.Gemini);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("functionDeclarations", out _));
        }

        [Fact]
        public void FormatJson_IncludesToolChoice()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("test").WithDescription("Test").Build();

            var json = fmt.FormatJson(ToolProvider.OpenAI, ToolChoice.Required);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("tool_choice", out var tc));
            Assert.Equal("required", tc.GetString());
        }

        // ── Text Format ─────────────────────────────────────────────

        [Fact]
        public void FormatText_EmptyTools()
        {
            var fmt = new PromptToolFormatter();
            Assert.Equal("No tools available.", fmt.FormatText());
        }

        [Fact]
        public void FormatText_IncludesToolInfo()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("get_weather")
                .WithDescription("Get weather for a city")
                .RequiredString("location", "City name")
                .EnumParam("unit", new[] { "c", "f" }, "Temperature unit")
                .Build();

            var text = fmt.FormatText();
            Assert.Contains("get_weather", text);
            Assert.Contains("Get weather", text);
            Assert.Contains("location (string)", text);
            Assert.Contains("Required parameters:", text);
            Assert.Contains("Optional parameters:", text);
            Assert.Contains("c, f", text);
        }

        [Fact]
        public void FormatText_ShowsDefaults()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("test")
                .WithDescription("Test")
                .OptionalString("lang", "Language", defaultValue: "en")
                .Build();

            var text = fmt.FormatText();
            Assert.Contains("[default: en]", text);
        }

        // ── Summary ─────────────────────────────────────────────────

        [Fact]
        public void Summary_EmptyTools()
        {
            var fmt = new PromptToolFormatter();
            Assert.Equal("No tools registered.", fmt.Summary());
        }

        [Fact]
        public void Summary_ShowsToolInfo()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search")
                .RequiredString("q")
                .OptionalString("lang")
                .Build();

            var summary = fmt.Summary();
            Assert.Contains("Tools: 1", summary);
            Assert.Contains("search: 1 required, 1 optional params", summary);
        }

        // ── Merge & Subset ──────────────────────────────────────────

        [Fact]
        public void MergeFrom_CombinesTools()
        {
            var a = new PromptToolFormatter();
            a.AddTool("search").WithDescription("Search").Build();

            var b = new PromptToolFormatter();
            b.AddTool("calc").WithDescription("Calculate").Build();

            a.MergeFrom(b);
            Assert.Equal(2, a.Tools.Count);
        }

        [Fact]
        public void MergeFrom_SkipsDuplicates()
        {
            var a = new PromptToolFormatter();
            a.AddTool("search").WithDescription("A").Build();

            var b = new PromptToolFormatter();
            b.AddTool("search").WithDescription("B").Build();

            a.MergeFrom(b);
            Assert.Single(a.Tools);
            Assert.Equal("A", a.Tools[0].Description); // keeps original
        }

        [Fact]
        public void Subset_FiltersTools()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("a").WithDescription("A").Build();
            fmt.AddTool("b").WithDescription("B").Build();
            fmt.AddTool("c").WithDescription("C").Build();

            var sub = fmt.Subset("a", "c");
            Assert.Equal(2, sub.Tools.Count);
            Assert.Contains(sub.Tools, t => t.Name == "a");
            Assert.Contains(sub.Tools, t => t.Name == "c");
            Assert.DoesNotContain(sub.Tools, t => t.Name == "b");
        }

        [Fact]
        public void Subset_EmptyForNoMatches()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("a").WithDescription("A").Build();

            var sub = fmt.Subset("missing");
            Assert.Empty(sub.Tools);
        }

        // ── Token Estimation ────────────────────────────────────────

        [Fact]
        public void EstimateTokens_PositiveForNonEmpty()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search")
                .WithDescription("Search the web for information")
                .RequiredString("query", "The search query string")
                .Build();

            var tokens = fmt.EstimateTokens();
            Assert.True(tokens > 10);
        }

        [Fact]
        public void EstimateTokens_EmptyIsBaseline()
        {
            var fmt = new PromptToolFormatter();
            Assert.Equal(10, fmt.EstimateTokens());
        }

        // ── Multiple Tools ──────────────────────────────────────────

        [Fact]
        public void MultipleTools_AllFormatted()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search").WithDescription("Search").RequiredString("q").Build();
            fmt.AddTool("calc").WithDescription("Calculate").RequiredString("expr").Build();
            fmt.AddTool("weather").WithDescription("Weather").RequiredString("city").Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            Assert.Equal(3, result.ToolCount);
            Assert.Equal(3, result.Tools.Count);
        }

        [Fact]
        public void FormatResult_HasEstimatedTokens()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("search").WithDescription("Search").RequiredString("q").Build();

            var result = fmt.Format(ToolProvider.OpenAI);
            Assert.True(result.EstimatedTokens > 0);
        }

        // ── ToolChoice ──────────────────────────────────────────────

        [Fact]
        public void ToolChoice_OpenAI_Values()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("t").WithDescription("T").Build();

            Assert.Equal("auto", fmt.Format(ToolProvider.OpenAI, ToolChoice.Auto).ToolChoiceValue);
            Assert.Equal("required", fmt.Format(ToolProvider.OpenAI, ToolChoice.Required).ToolChoiceValue);
            Assert.Equal("none", fmt.Format(ToolProvider.OpenAI, ToolChoice.None).ToolChoiceValue);
        }

        [Fact]
        public void ToolChoice_Mistral_Values()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("t").WithDescription("T").Build();

            Assert.Equal("auto", fmt.Format(ToolProvider.Mistral, ToolChoice.Auto).ToolChoiceValue);
            Assert.Equal("required", fmt.Format(ToolProvider.Mistral, ToolChoice.Required).ToolChoiceValue);
        }

        // ── Cross-Provider Consistency ──────────────────────────────

        [Fact]
        public void AllProviders_FormatWithoutError()
        {
            var fmt = new PromptToolFormatter();
            fmt.AddTool("test")
                .WithDescription("Test tool")
                .RequiredString("input", "The input")
                .OptionalString("format", "Output format", defaultValue: "json")
                .EnumParam("mode", new[] { "fast", "slow" })
                .ArrayParam("tags", ParamType.String)
                .AddParam("count", ParamType.Integer, "Count")
                .AddParam("enabled", ParamType.Boolean, "Flag")
                .AddParam("score", ParamType.Number, "Score value")
                .Build();

            foreach (ToolProvider provider in Enum.GetValues(typeof(ToolProvider)))
            {
                var result = fmt.Format(provider);
                Assert.Single(result.Tools);
                Assert.True(result.EstimatedTokens > 0);

                var json = fmt.FormatJson(provider);
                Assert.DoesNotContain("ERROR", json, StringComparison.OrdinalIgnoreCase);
                // Valid JSON
                JsonDocument.Parse(json);
            }
        }
    }
}
