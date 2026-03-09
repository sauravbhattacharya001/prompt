namespace Prompt.Tests
{
    using Xunit;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class PromptSignatureTests
    {
        // ── Creation and fluent API ─────────────────────────────

        [Fact]
        public void Create_WithTask_SetsTask()
        {
            var sig = PromptSignature.Create("Summarize text");
            Assert.Equal("Summarize text", sig.Task);
        }

        [Fact]
        public void Create_EmptyTask_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptSignature.Create(""));
        }

        [Fact]
        public void Input_AddsField()
        {
            var sig = PromptSignature.Create("Test")
                .Input("text", "The input text");
            Assert.Single(sig.Inputs);
            Assert.Equal("text", sig.Inputs[0].Name);
            Assert.Equal("The input text", sig.Inputs[0].Description);
        }

        [Fact]
        public void Output_AddsField()
        {
            var sig = PromptSignature.Create("Test")
                .Output("result", "The output", FieldType.Text);
            Assert.Single(sig.Outputs);
            Assert.Equal("result", sig.Outputs[0].Name);
            Assert.Equal(FieldType.Text, sig.Outputs[0].Type);
        }

        [Fact]
        public void Output_EnumWithValues()
        {
            var sig = PromptSignature.Create("Test")
                .Output("sentiment", "Tone", FieldType.Enum, "positive", "negative", "neutral");
            Assert.Equal(3, sig.Outputs[0].AllowedValues.Count);
            Assert.Contains("negative", sig.Outputs[0].AllowedValues);
        }

        [Fact]
        public void Input_DuplicateName_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PromptSignature.Create("Test")
                    .Input("x")
                    .Input("x"));
        }

        [Fact]
        public void Output_DuplicateName_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PromptSignature.Create("Test")
                    .Output("y")
                    .Output("y"));
        }

        [Fact]
        public void Input_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PromptSignature.Create("Test").Input(""));
        }

        [Fact]
        public void Input_CustomField_Works()
        {
            var field = SignatureField.IntF("count", "Number of items");
            field.Required = false;
            var sig = PromptSignature.Create("Test").Input(field);
            Assert.False(sig.Inputs[0].Required);
            Assert.Equal(FieldType.Integer, sig.Inputs[0].Type);
        }

        [Fact]
        public void Format_ValidValues()
        {
            var sig = PromptSignature.Create("Test").Format("yaml");
            Assert.Equal("yaml", sig.OutputFormat);
        }

        [Fact]
        public void Format_InvalidValue_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PromptSignature.Create("Test").Format("xml"));
        }

        // ── Signature string ────────────────────────────────────

        [Fact]
        public void ToSignatureString_Correct()
        {
            var sig = PromptSignature.Create("Test")
                .Input("article")
                .Input("max_len")
                .Output("summary")
                .Output("keywords");
            Assert.Equal("article, max_len -> summary, keywords", sig.ToSignatureString());
        }

        [Fact]
        public void Parse_RoundTrips()
        {
            var sig = PromptSignature.Parse("text, lang -> translation, confidence", "Translate text");
            Assert.Equal("Translate text", sig.Task);
            Assert.Equal(2, sig.Inputs.Count);
            Assert.Equal(2, sig.Outputs.Count);
            Assert.Equal("text", sig.Inputs[0].Name);
            Assert.Equal("confidence", sig.Outputs[1].Name);
        }

        [Fact]
        public void Parse_NoArrow_Throws()
        {
            Assert.Throws<FormatException>(() => PromptSignature.Parse("abc, def"));
        }

        // ── System prompt generation ────────────────────────────

        [Fact]
        public void GenerateSystemPrompt_ContainsTask()
        {
            var sig = PromptSignature.Create("Classify documents").Output("category");
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("Classify documents", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_ListsInputs()
        {
            var sig = PromptSignature.Create("Test")
                .Input("document", "The document text")
                .Output("label");
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("document", prompt);
            Assert.Contains("The document text", prompt);
            Assert.Contains("Inputs", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_ShowsOutputSchema()
        {
            var sig = PromptSignature.Create("Test")
                .Output("score", "Quality score", FieldType.Integer)
                .Output("tags", "Relevant tags", FieldType.List);
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("score", prompt);
            Assert.Contains("integer", prompt);
            Assert.Contains("tags", prompt);
            Assert.Contains("list", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_JsonFormat_HasCodeBlock()
        {
            var sig = PromptSignature.Create("Test")
                .Output("x", "test", FieldType.Text);
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("```json", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_YamlFormat()
        {
            var sig = PromptSignature.Create("Test")
                .Output("x").Format("yaml");
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("```yaml", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_MarkdownFormat()
        {
            var sig = PromptSignature.Create("Test")
                .Output("x").Format("markdown");
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("### x", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_EnumShowsAllowedValues()
        {
            var sig = PromptSignature.Create("Test")
                .Output("mood", "Mood", FieldType.Enum, "happy", "sad", "neutral");
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("happy", prompt);
            Assert.Contains("sad", prompt);
            Assert.Contains("neutral", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_OptionalField()
        {
            var sig = PromptSignature.Create("Test")
                .Output(new SignatureField("note") { Required = false });
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("optional", prompt);
        }

        [Fact]
        public void GenerateSystemPrompt_MaxLength()
        {
            var sig = PromptSignature.Create("Test")
                .Output(new SignatureField("summary") { MaxLength = 200 });
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("200", prompt);
        }

        // ── FormatInput ─────────────────────────────────────────

        [Fact]
        public void FormatInput_IncludesAllValues()
        {
            var sig = PromptSignature.Create("Test")
                .Input("text")
                .Input("lang");
            var result = sig.FormatInput(new Dictionary<string, object?> {
                ["text"] = "Hello world",
                ["lang"] = "es"
            });
            Assert.Contains("Hello world", result);
            Assert.Contains("es", result);
        }

        [Fact]
        public void FormatInput_MissingRequired_Throws()
        {
            var sig = PromptSignature.Create("Test").Input("text");
            Assert.Throws<ArgumentException>(() =>
                sig.FormatInput(new Dictionary<string, object?>()));
        }

        [Fact]
        public void FormatInput_UnknownField_Throws()
        {
            var sig = PromptSignature.Create("Test").Input("text");
            Assert.Throws<ArgumentException>(() =>
                sig.FormatInput(new Dictionary<string, object?> {
                    ["text"] = "hi",
                    ["unknown"] = "bad"
                }));
        }

        [Fact]
        public void FormatInput_OptionalMissing_OK()
        {
            var sig = PromptSignature.Create("Test")
                .Input(new SignatureField("text"))
                .Input(new SignatureField("hint") { Required = false });
            var result = sig.FormatInput(new Dictionary<string, object?> {
                ["text"] = "Hello"
            });
            Assert.Contains("Hello", result);
            Assert.DoesNotContain("hint", result);
        }

        // ── ValidateResponse (JSON) ─────────────────────────────

        [Fact]
        public void ValidateResponse_ValidJson_AllFields()
        {
            var sig = PromptSignature.Create("Test")
                .Output("summary", "Summary", FieldType.Text)
                .Output("score", "Score", FieldType.Integer);
            var resp = "{\"summary\": \"Good article\", \"score\": 8}";
            var v = sig.ValidateResponse(resp);
            Assert.True(v.IsValid);
            Assert.Equal("Good article", v.Fields["summary"]);
            Assert.Equal(2, v.ValidCount);
            Assert.Equal(1.0, v.Score);
        }

        [Fact]
        public void ValidateResponse_MissingRequired_Invalid()
        {
            var sig = PromptSignature.Create("Test")
                .Output("summary")
                .Output("tags");
            var v = sig.ValidateResponse("{\"summary\": \"hi\"}");
            Assert.False(v.IsValid);
            Assert.Contains("tags", v.MissingFields);
        }

        [Fact]
        public void ValidateResponse_OptionalMissing_StillValid()
        {
            var sig = PromptSignature.Create("Test")
                .Output("summary")
                .Output(new SignatureField("note") { Required = false });
            var v = sig.ValidateResponse("{\"summary\": \"hi\"}");
            Assert.True(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_WrongType_Invalid()
        {
            var sig = PromptSignature.Create("Test")
                .Output("count", "Count", FieldType.Integer);
            var v = sig.ValidateResponse("{\"count\": \"not a number\"}");
            Assert.False(v.IsValid);
            Assert.True(v.Errors.ContainsKey("count"));
        }

        [Fact]
        public void ValidateResponse_EnumInvalid()
        {
            var sig = PromptSignature.Create("Test")
                .Output("color", "Color", FieldType.Enum, "red", "blue");
            var v = sig.ValidateResponse("{\"color\": \"green\"}");
            Assert.False(v.IsValid);
            Assert.Contains("not allowed", v.Errors["color"]);
        }

        [Fact]
        public void ValidateResponse_EnumValid()
        {
            var sig = PromptSignature.Create("Test")
                .Output("color", "Color", FieldType.Enum, "red", "blue");
            var v = sig.ValidateResponse("{\"color\": \"red\"}");
            Assert.True(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_ListType()
        {
            var sig = PromptSignature.Create("Test")
                .Output("items", "Items", FieldType.List);
            var v = sig.ValidateResponse("{\"items\": [\"a\", \"b\"]}");
            Assert.True(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_BooleanType()
        {
            var sig = PromptSignature.Create("Test")
                .Output("flag", "Flag", FieldType.Boolean);
            var v = sig.ValidateResponse("{\"flag\": true}");
            Assert.True(v.IsValid);
            Assert.Equal(true, v.Fields["flag"]);
        }

        [Fact]
        public void ValidateResponse_MaxLength_Exceeded()
        {
            var sig = PromptSignature.Create("Test")
                .Output(new SignatureField("title") { Type = FieldType.Text, MaxLength = 5 });
            var v = sig.ValidateResponse("{\"title\": \"too long text\"}");
            Assert.False(v.IsValid);
            Assert.Contains("max length", v.Errors["title"], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateResponse_ListMaxLength_Exceeded()
        {
            var sig = PromptSignature.Create("Test")
                .Output(new SignatureField("tags") { Type = FieldType.List, MaxLength = 2 });
            var v = sig.ValidateResponse("{\"tags\": [\"a\", \"b\", \"c\"]}");
            Assert.False(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_ExtraFields_Tracked()
        {
            var sig = PromptSignature.Create("Test").Output("x");
            var v = sig.ValidateResponse("{\"x\": \"ok\", \"y\": \"extra\"}");
            Assert.True(v.IsValid);  // Extra fields don't fail validation
            Assert.Contains("y", v.ExtraFields);
        }

        [Fact]
        public void ValidateResponse_JsonInCodeBlock()
        {
            var sig = PromptSignature.Create("Test").Output("x");
            var resp = "```json\n{\"x\": \"ok\"}\n```";
            var v = sig.ValidateResponse(resp);
            Assert.True(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_InvalidJson()
        {
            var sig = PromptSignature.Create("Test").Output("x");
            var v = sig.ValidateResponse("this is not json at all");
            Assert.False(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_EmptyInput()
        {
            var sig = PromptSignature.Create("Test").Output("x");
            var v = sig.ValidateResponse("");
            Assert.False(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_JsonObject_Type()
        {
            var sig = PromptSignature.Create("Test")
                .Output("meta", "Metadata", FieldType.Json);
            var v = sig.ValidateResponse("{\"meta\": {\"key\": \"val\"}}");
            Assert.True(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_NumberType()
        {
            var sig = PromptSignature.Create("Test")
                .Output("ratio", "Ratio", FieldType.Number);
            var v = sig.ValidateResponse("{\"ratio\": 3.14}");
            Assert.True(v.IsValid);
            Assert.Equal(3.14, v.Fields["ratio"]);
        }

        // ── ValidateResponse (YAML) ─────────────────────────────

        [Fact]
        public void ValidateResponse_Yaml_Valid()
        {
            var sig = PromptSignature.Create("Test")
                .Output("name")
                .Output("age")
                .Format("yaml");
            var v = sig.ValidateResponse("name: Alice\nage: 30");
            Assert.True(v.IsValid);
            Assert.Equal("Alice", v.Fields["name"]);
        }

        [Fact]
        public void ValidateResponse_Yaml_MissingField()
        {
            var sig = PromptSignature.Create("Test")
                .Output("name")
                .Output("age")
                .Format("yaml");
            var v = sig.ValidateResponse("name: Alice");
            Assert.False(v.IsValid);
            Assert.Contains("age", v.MissingFields);
        }

        // ── ValidateResponse (Markdown) ─────────────────────────

        [Fact]
        public void ValidateResponse_Markdown_Valid()
        {
            var sig = PromptSignature.Create("Test")
                .Output("summary")
                .Output("conclusion")
                .Format("markdown");
            var resp = "### summary\nThis is the summary.\n### conclusion\nDone.";
            var v = sig.ValidateResponse(resp);
            Assert.True(v.IsValid);
            Assert.Equal("This is the summary.", v.Fields["summary"]);
        }

        // ── JSON Schema export ──────────────────────────────────

        [Fact]
        public void ToJsonSchema_ValidSchema()
        {
            var sig = PromptSignature.Create("Classify")
                .Output("category", "The category", FieldType.Enum, "A", "B")
                .Output("score", "Score", FieldType.Integer);
            var schema = sig.ToJsonSchema();
            var doc = JsonDocument.Parse(schema);
            Assert.Equal("object", doc.RootElement.GetProperty("type").GetString());
            Assert.True(doc.RootElement.GetProperty("properties").TryGetProperty("category", out _));
            Assert.True(doc.RootElement.GetProperty("properties").TryGetProperty("score", out _));
        }

        [Fact]
        public void ToJsonSchema_RequiredFields()
        {
            var sig = PromptSignature.Create("Test")
                .Output("x")
                .Output(new SignatureField("y") { Required = false });
            var schema = sig.ToJsonSchema();
            var doc = JsonDocument.Parse(schema);
            var required = doc.RootElement.GetProperty("required");
            Assert.Equal(1, required.GetArrayLength());
            Assert.Equal("x", required[0].GetString());
        }

        // ── ToJson / serialization ──────────────────────────────

        [Fact]
        public void ToJson_RoundTrip()
        {
            var sig = PromptSignature.Create("Translate text")
                .Input("text", "Source text")
                .Output("translation", "Translated text")
                .Format("json");
            var json = sig.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal("Translate text", doc.RootElement.GetProperty("task").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("inputs").GetArrayLength());
            Assert.Equal(1, doc.RootElement.GetProperty("outputs").GetArrayLength());
        }

        // ── SignatureField factories ────────────────────────────

        [Fact]
        public void FieldFactories_CreateCorrectTypes()
        {
            Assert.Equal(FieldType.Text, SignatureField.TextF("t").Type);
            Assert.Equal(FieldType.Integer, SignatureField.IntF("i").Type);
            Assert.Equal(FieldType.Number, SignatureField.NumF("n").Type);
            Assert.Equal(FieldType.Boolean, SignatureField.BoolF("b").Type);
            Assert.Equal(FieldType.List, SignatureField.ListF("l").Type);
            Assert.Equal(FieldType.Json, SignatureField.JsonF("j").Type);
            var e = SignatureField.EnumF("e", "enum", "a", "b");
            Assert.Equal(FieldType.Enum, e.Type);
            Assert.Equal(2, e.AllowedValues.Count);
        }

        [Fact]
        public void FieldFactory_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => SignatureField.TextF(""));
        }

        // ── SignatureValidation ─────────────────────────────────

        [Fact]
        public void Summary_Valid()
        {
            var v = new SignatureValidation { IsValid = true, TotalExpected = 3 };
            v.Fields["a"] = "x";
            v.Fields["b"] = "y";
            v.Fields["c"] = "z";
            Assert.Contains("Valid", v.Summary());
        }

        [Fact]
        public void Summary_Invalid_ShowsErrors()
        {
            var v = new SignatureValidation { IsValid = false, TotalExpected = 2 };
            v.Fields["a"] = "x";
            v.Errors["b"] = "wrong type";
            v.MissingFields.Add("c");
            var summary = v.Summary();
            Assert.Contains("Invalid", summary);
            Assert.Contains("wrong type", summary);
            Assert.Contains("missing", summary);
        }

        [Fact]
        public void Score_CalculatesCorrectly()
        {
            var v = new SignatureValidation { TotalExpected = 4 };
            v.Fields["a"] = "x";
            v.Fields["b"] = "y";
            v.Fields["c"] = "z";
            v.Errors["c"] = "bad";
            Assert.Equal(0.5, v.Score); // 2 valid out of 4 expected
        }

        // ── Edge cases ──────────────────────────────────────────

        [Fact]
        public void FluentChaining_AllTypes()
        {
            var sig = PromptSignature.Create("Complex task")
                .Input("doc", "Document text")
                .Input("max_words", "Max words", FieldType.Integer)
                .Output("summary", "Brief summary")
                .Output("word_count", "Number of words", FieldType.Integer)
                .Output("keywords", "Key terms", FieldType.List)
                .Output("is_positive", "Sentiment", FieldType.Boolean)
                .Output("metadata", "Extra info", FieldType.Json)
                .Output("category", "Category", FieldType.Enum, "tech", "science", "other");
            Assert.Equal(2, sig.Inputs.Count);
            Assert.Equal(6, sig.Outputs.Count);

            // Generate prompt and validate it contains all pieces
            var prompt = sig.GenerateSystemPrompt();
            Assert.Contains("Complex task", prompt);
            Assert.Contains("integer", prompt);
            Assert.Contains("list", prompt);
            Assert.Contains("boolean", prompt);
            Assert.Contains("tech", prompt);
        }

        [Fact]
        public void ValidateResponse_EnumCaseInsensitive()
        {
            var sig = PromptSignature.Create("Test")
                .Output("status", "Status", FieldType.Enum, "Active", "Inactive");
            var v = sig.ValidateResponse("{\"status\": \"active\"}");
            Assert.True(v.IsValid);
        }

        [Fact]
        public void ValidateResponse_JsonWithSurroundingText()
        {
            var sig = PromptSignature.Create("Test").Output("x");
            var resp = "Here is my response:\n{\"x\": \"found it\"}";
            var v = sig.ValidateResponse(resp);
            Assert.True(v.IsValid);
            Assert.Equal("found it", v.Fields["x"]);
        }
    }
}
