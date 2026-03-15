using Prompt;
using Xunit;

namespace Prompt.Tests
{
    public class PromptSchemaGeneratorTests
    {
        [Fact]
        public void Create_WithEmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptSchemaGenerator.Create(""));
            Assert.Throws<ArgumentException>(() => PromptSchemaGenerator.Create("  "));
        }

        [Fact]
        public void Build_WithNoProperties_Throws()
        {
            var gen = PromptSchemaGenerator.Create("Empty");
            Assert.Throws<InvalidOperationException>(() => gen.Build());
        }

        [Fact]
        public void SimpleSchema_GeneratesValidJsonSchema()
        {
            var schema = PromptSchemaGenerator.Create("TestResult")
                .WithDescription("A test result")
                .AddString("status", "The status", enumValues: new[] { "pass", "fail" })
                .AddInteger("score", "The score", example: "85")
                .AddBoolean("reviewed", "Whether reviewed", required: false)
                .Build();

            var json = schema.ToJsonSchema();
            Assert.Contains("\"title\": \"TestResult\"", json);
            Assert.Contains("\"description\": \"A test result\"", json);
            Assert.Contains("\"status\"", json);
            Assert.Contains("\"score\"", json);
            Assert.Contains("\"reviewed\"", json);
            Assert.Contains("\"enum\"", json);
            Assert.Contains("\"pass\"", json);

            // required should include status and score but not reviewed
            Assert.Contains("\"status\"", json);
            Assert.Contains("\"score\"", json);

            var doc = System.Text.Json.JsonDocument.Parse(json);
            var required = doc.RootElement.GetProperty("required");
            var requiredNames = new List<string>();
            foreach (var el in required.EnumerateArray())
                requiredNames.Add(el.GetString()!);
            Assert.Contains("status", requiredNames);
            Assert.Contains("score", requiredNames);
            Assert.DoesNotContain("reviewed", requiredNames);
        }

        [Fact]
        public void PromptInstructions_ContainsFieldDescriptions()
        {
            var schema = PromptSchemaGenerator.Create("Sentiment")
                .WithDescription("Sentiment analysis")
                .AddString("sentiment", "Detected sentiment", enumValues: new[] { "positive", "negative" })
                .AddNumber("confidence", "Score 0-1", example: "0.95")
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("Sentiment", instructions);
            Assert.Contains("Detected sentiment", instructions);
            Assert.Contains("confidence", instructions);
            Assert.Contains("\"positive\"", instructions);
            Assert.Contains("Return ONLY valid JSON", instructions);
            Assert.Contains("```json", instructions);
        }

        [Fact]
        public void PromptInstructions_WithoutExample()
        {
            var schema = PromptSchemaGenerator.Create("Simple")
                .AddString("name", "A name")
                .Build();

            var instructions = schema.ToPromptInstructions(includeExample: false);
            Assert.DoesNotContain("```json", instructions);
            Assert.Contains("name", instructions);
        }

        [Fact]
        public void GenerateExample_UsesExampleValues()
        {
            var schema = PromptSchemaGenerator.Create("Test")
                .AddString("city", "The city", example: "Seattle")
                .AddInteger("pop", "Population", example: "750000")
                .Build();

            var example = schema.GenerateExample();
            Assert.Contains("Seattle", example);
            Assert.Contains("750000", example);
        }

        [Fact]
        public void GenerateExample_UsesDefaults()
        {
            var schema = PromptSchemaGenerator.Create("Test")
                .AddString("name", "A name")
                .AddNumber("value", "A value")
                .AddBoolean("flag", "A flag")
                .Build();

            var example = schema.GenerateExample();
            Assert.Contains("example", example);
            Assert.Contains("3.14", example);
            Assert.Contains("true", example);
        }

        [Fact]
        public void ArrayProperty_SimpleType()
        {
            var schema = PromptSchemaGenerator.Create("Tags")
                .AddArray("tags", "List of tags", SchemaType.String)
                .Build();

            var json = schema.ToJsonSchema();
            Assert.Contains("\"items\"", json);
            Assert.Contains("\"string\"", json);

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("array of string", instructions);
        }

        [Fact]
        public void ObjectArrayProperty_NestedSchema()
        {
            var schema = PromptSchemaGenerator.Create("Report")
                .AddObjectArray("items", "Line items", nested =>
                {
                    nested.AddString("label", "Item label");
                    nested.AddNumber("amount", "Dollar amount");
                })
                .Build();

            var json = schema.ToJsonSchema();
            Assert.Contains("\"label\"", json);
            Assert.Contains("\"amount\"", json);

            var example = schema.GenerateExample();
            Assert.Contains("label", example);
            Assert.Contains("amount", example);
        }

        [Fact]
        public void NestedObjectProperty()
        {
            var schema = PromptSchemaGenerator.Create("User")
                .AddString("name", "User name")
                .AddObject("address", "Mailing address", addr =>
                {
                    addr.AddString("street", "Street line");
                    addr.AddString("city", "City");
                    addr.AddString("zip", "ZIP code");
                })
                .Build();

            var json = schema.ToJsonSchema();
            Assert.Contains("\"street\"", json);
            Assert.Contains("\"city\"", json);

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("street", instructions);
        }

        [Fact]
        public void EnumValues_InPromptInstructions()
        {
            var schema = PromptSchemaGenerator.Create("Rating")
                .AddString("level", "Rating level", enumValues: new[] { "low", "medium", "high" })
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("\"low\"", instructions);
            Assert.Contains("\"medium\"", instructions);
            Assert.Contains("\"high\"", instructions);
            Assert.Contains("Allowed values", instructions);
        }

        [Fact]
        public void JsonSchema_HasCorrectMetadata()
        {
            var schema = PromptSchemaGenerator.Create("Meta")
                .WithDescription("Test metadata")
                .AddString("id", "Identifier")
                .Build();

            var json = schema.ToJsonSchema();
            Assert.Contains("json-schema.org/draft/2020-12", json);
            Assert.Contains("\"additionalProperties\": false", json);
        }

        [Fact]
        public void JsonSchema_Compact()
        {
            var schema = PromptSchemaGenerator.Create("Compact")
                .AddString("x", "A value")
                .Build();

            var compact = schema.ToJsonSchema(indented: false);
            Assert.DoesNotContain("\n", compact);
        }

        [Fact]
        public void Properties_Accessible()
        {
            var schema = PromptSchemaGenerator.Create("Access")
                .WithDescription("Desc")
                .AddString("a", "Field A")
                .AddInteger("b", "Field B", required: false)
                .Build();

            Assert.Equal("Access", schema.Name);
            Assert.Equal("Desc", schema.Description);
            Assert.Equal(2, schema.Properties.Count);
            Assert.True(schema.Properties[0].Required);
            Assert.False(schema.Properties[1].Required);
        }
    }
}
