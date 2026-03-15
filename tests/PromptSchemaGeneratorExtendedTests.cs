using Prompt;
using Xunit;
using System.Text.Json;

namespace Prompt.Tests
{
    /// <summary>
    /// Extended tests for PromptSchemaGenerator covering edge cases,
    /// example generation with custom values, nested schema depth,
    /// and JSON Schema structural correctness.
    /// </summary>
    public class PromptSchemaGeneratorExtendedTests
    {
        // ── Builder validation ───────────────────────────

        [Fact]
        public void WithDescription_Null_ThrowsArgumentNullException()
        {
            var gen = PromptSchemaGenerator.Create("Test");
            Assert.Throws<ArgumentNullException>(() => gen.WithDescription(null!));
        }

        [Fact]
        public void Builder_FluentChaining_ReturnsSameInstance()
        {
            var gen = PromptSchemaGenerator.Create("Chain");
            var result = gen
                .WithDescription("desc")
                .AddString("a", "A")
                .AddInteger("b", "B")
                .AddNumber("c", "C")
                .AddBoolean("d", "D")
                .AddArray("e", "E");

            Assert.Same(gen, result);
        }

        [Fact]
        public void Build_PropertiesAreSnapshot_NotLive()
        {
            var gen = PromptSchemaGenerator.Create("Snap")
                .AddString("x", "First");

            var schema = gen.Build();
            Assert.Single(schema.Properties);

            // Adding more after Build shouldn't affect the built schema
            gen.AddString("y", "Second");
            Assert.Single(schema.Properties);
        }

        // ── JSON Schema structure ────────────────────────

        [Fact]
        public void JsonSchema_IntegerType_MapsCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("IntTest")
                .AddInteger("count", "A count")
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var props = doc.RootElement.GetProperty("properties");
            var countType = props.GetProperty("count").GetProperty("type").GetString();
            Assert.Equal("integer", countType);
        }

        [Fact]
        public void JsonSchema_NumberType_MapsCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("NumTest")
                .AddNumber("ratio", "A ratio")
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var props = doc.RootElement.GetProperty("properties");
            var ratioType = props.GetProperty("ratio").GetProperty("type").GetString();
            Assert.Equal("number", ratioType);
        }

        [Fact]
        public void JsonSchema_BooleanType_MapsCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("BoolTest")
                .AddBoolean("active", "Is active")
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var props = doc.RootElement.GetProperty("properties");
            var activeType = props.GetProperty("active").GetProperty("type").GetString();
            Assert.Equal("boolean", activeType);
        }

        [Fact]
        public void JsonSchema_ArrayOfIntegers_ItemTypeCorrect()
        {
            var schema = PromptSchemaGenerator.Create("IntArray")
                .AddArray("scores", "Score list", SchemaType.Integer)
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var items = doc.RootElement
                .GetProperty("properties")
                .GetProperty("scores")
                .GetProperty("items");
            Assert.Equal("integer", items.GetProperty("type").GetString());
        }

        [Fact]
        public void JsonSchema_ArrayOfNumbers_ItemTypeCorrect()
        {
            var schema = PromptSchemaGenerator.Create("NumArray")
                .AddArray("values", "Value list", SchemaType.Number)
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var items = doc.RootElement
                .GetProperty("properties")
                .GetProperty("values")
                .GetProperty("items");
            Assert.Equal("number", items.GetProperty("type").GetString());
        }

        [Fact]
        public void JsonSchema_ArrayOfBooleans_ItemTypeCorrect()
        {
            var schema = PromptSchemaGenerator.Create("BoolArray")
                .AddArray("flags", "Flags", SchemaType.Boolean)
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var items = doc.RootElement
                .GetProperty("properties")
                .GetProperty("flags")
                .GetProperty("items");
            Assert.Equal("boolean", items.GetProperty("type").GetString());
        }

        [Fact]
        public void JsonSchema_ObjectArray_HasNestedRequired()
        {
            var schema = PromptSchemaGenerator.Create("NestedReq")
                .AddObjectArray("entries", "Entries", nested =>
                {
                    nested.AddString("key", "Key");
                    nested.AddString("value", "Value", required: false);
                })
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var items = doc.RootElement
                .GetProperty("properties")
                .GetProperty("entries")
                .GetProperty("items");

            var required = items.GetProperty("required");
            var requiredNames = new List<string>();
            foreach (var el in required.EnumerateArray())
                requiredNames.Add(el.GetString()!);

            Assert.Contains("key", requiredNames);
            Assert.DoesNotContain("value", requiredNames);
        }

        [Fact]
        public void JsonSchema_NestedObject_HasProperties()
        {
            var schema = PromptSchemaGenerator.Create("Deep")
                .AddObject("meta", "Metadata", m =>
                {
                    m.AddString("version", "Version");
                    m.AddInteger("build", "Build number");
                })
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var meta = doc.RootElement
                .GetProperty("properties")
                .GetProperty("meta");

            Assert.Equal("object", meta.GetProperty("type").GetString());
            var nested = meta.GetProperty("properties");
            Assert.True(nested.TryGetProperty("version", out _));
            Assert.True(nested.TryGetProperty("build", out _));
        }

        [Fact]
        public void JsonSchema_NestedObject_RequiredArray()
        {
            var schema = PromptSchemaGenerator.Create("ObjReq")
                .AddObject("config", "Configuration", c =>
                {
                    c.AddString("host", "Hostname");
                    c.AddInteger("port", "Port", required: false);
                })
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var config = doc.RootElement
                .GetProperty("properties")
                .GetProperty("config");

            var required = config.GetProperty("required");
            var names = new List<string>();
            foreach (var el in required.EnumerateArray())
                names.Add(el.GetString()!);

            Assert.Contains("host", names);
            Assert.DoesNotContain("port", names);
        }

        [Fact]
        public void JsonSchema_AllOptionalProperties_EmptyRequired()
        {
            var schema = PromptSchemaGenerator.Create("AllOptional")
                .AddString("a", "Field A", required: false)
                .AddInteger("b", "Field B", required: false)
                .Build();

            var doc = JsonDocument.Parse(schema.ToJsonSchema());
            var required = doc.RootElement.GetProperty("required");
            Assert.Equal(0, required.GetArrayLength());
        }

        // ── Example generation ───────────────────────────

        [Fact]
        public void GenerateExample_BooleanExample_ParsesCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("BoolEx")
                .AddBoolean("flag", "A flag", example: "false")
                .Build();

            var example = schema.GenerateExample();
            var doc = JsonDocument.Parse(example);
            Assert.False(doc.RootElement.GetProperty("flag").GetBoolean());
        }

        [Fact]
        public void GenerateExample_NumberExample_ParsesCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("NumEx")
                .AddNumber("score", "Score", example: "99.5")
                .Build();

            var example = schema.GenerateExample();
            var doc = JsonDocument.Parse(example);
            Assert.Equal(99.5, doc.RootElement.GetProperty("score").GetDouble());
        }

        [Fact]
        public void GenerateExample_IntegerExample_ParsesCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("IntEx")
                .AddInteger("count", "Count", example: "7")
                .Build();

            var example = schema.GenerateExample();
            var doc = JsonDocument.Parse(example);
            Assert.Equal(7, doc.RootElement.GetProperty("count").GetInt32());
        }

        [Fact]
        public void GenerateExample_EnumString_UsesFirstValue()
        {
            var schema = PromptSchemaGenerator.Create("EnumEx")
                .AddString("status", "Status", enumValues: new[] { "active", "inactive" })
                .Build();

            var example = schema.GenerateExample();
            var doc = JsonDocument.Parse(example);
            Assert.Equal("active", doc.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public void GenerateExample_ArrayOfIntegers_ContainsDefault()
        {
            var schema = PromptSchemaGenerator.Create("ArrEx")
                .AddArray("nums", "Numbers", SchemaType.Integer)
                .Build();

            var example = schema.GenerateExample();
            var doc = JsonDocument.Parse(example);
            var arr = doc.RootElement.GetProperty("nums");
            Assert.Equal(JsonValueKind.Array, arr.ValueKind);
            Assert.Equal(1, arr[0].GetInt32());
        }

        [Fact]
        public void GenerateExample_NestedObject_AllFieldsPresent()
        {
            var schema = PromptSchemaGenerator.Create("NestEx")
                .AddObject("location", "Location data", loc =>
                {
                    loc.AddNumber("lat", "Latitude", example: "47.6");
                    loc.AddNumber("lng", "Longitude", example: "-122.3");
                })
                .Build();

            var example = schema.GenerateExample();
            var doc = JsonDocument.Parse(example);
            var loc = doc.RootElement.GetProperty("location");
            Assert.Equal(47.6, loc.GetProperty("lat").GetDouble());
            Assert.Equal(-122.3, loc.GetProperty("lng").GetDouble());
        }

        [Fact]
        public void GenerateExample_ObjectWithNoNestedProps_EmptyObject()
        {
            // Object type with no nested builder — should produce {}
            var schema = PromptSchemaGenerator.Create("EmptyObj")
                .AddObject("meta", "Metadata", _ => { })
                .Build();

            // Build with empty nested still requires at least one property
            // Actually the nested builder adds no properties, so NestedProperties
            // will be an empty list. The source code checks Count > 0.
            var example = schema.GenerateExample();
            var doc = JsonDocument.Parse(example);
            var meta = doc.RootElement.GetProperty("meta");
            Assert.Equal(JsonValueKind.Object, meta.ValueKind);
        }

        [Fact]
        public void GenerateExample_IsValidJson()
        {
            var schema = PromptSchemaGenerator.Create("Valid")
                .AddString("name", "Name")
                .AddInteger("age", "Age")
                .AddNumber("score", "Score")
                .AddBoolean("active", "Active")
                .AddArray("tags", "Tags")
                .AddObjectArray("items", "Items", n =>
                {
                    n.AddString("id", "ID");
                    n.AddNumber("price", "Price");
                })
                .AddObject("meta", "Metadata", m =>
                {
                    m.AddString("version", "Version");
                })
                .Build();

            var example = schema.GenerateExample();
            // Should parse without throwing
            var doc = JsonDocument.Parse(example);
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            Assert.Equal(7, doc.RootElement.EnumerateObject().Count());
        }

        // ── Prompt instructions format ───────────────────

        [Fact]
        public void PromptInstructions_ShowsRequiredAndOptional()
        {
            var schema = PromptSchemaGenerator.Create("ReqOpt")
                .AddString("req_field", "Required field")
                .AddString("opt_field", "Optional field", required: false)
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("required", instructions);
            Assert.Contains("optional", instructions);
        }

        [Fact]
        public void PromptInstructions_ArrayOfObject_ShowsNestedFields()
        {
            var schema = PromptSchemaGenerator.Create("Nested")
                .AddObjectArray("steps", "Process steps", n =>
                {
                    n.AddString("action", "Action to take");
                    n.AddInteger("order", "Step order");
                })
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("action", instructions);
            Assert.Contains("order", instructions);
            Assert.Contains("array of object", instructions);
        }

        [Fact]
        public void PromptInstructions_NumberType_DisplayedCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("Num")
                .AddNumber("ratio", "A ratio")
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("number", instructions);
        }

        [Fact]
        public void PromptInstructions_BooleanType_DisplayedCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("Bool")
                .AddBoolean("enabled", "Is enabled")
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("boolean", instructions);
        }

        [Fact]
        public void PromptInstructions_WithDescription_IncludesIt()
        {
            var schema = PromptSchemaGenerator.Create("Desc")
                .WithDescription("This is a detailed description")
                .AddString("x", "X field")
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.Contains("This is a detailed description", instructions);
        }

        [Fact]
        public void PromptInstructions_NoDescription_OmitsDescriptionLine()
        {
            var schema = PromptSchemaGenerator.Create("NoDesc")
                .AddString("x", "X field")
                .Build();

            var instructions = schema.ToPromptInstructions();
            Assert.DoesNotContain("Description:", instructions);
        }

        // ── SchemaProperty accessors ─────────────────────

        [Fact]
        public void SchemaProperty_ItemType_SetForArrays()
        {
            var schema = PromptSchemaGenerator.Create("ItemType")
                .AddArray("ids", "ID list", SchemaType.Integer)
                .Build();

            Assert.Equal(SchemaType.Integer, schema.Properties[0].ItemType);
        }

        [Fact]
        public void SchemaProperty_NestedProperties_SetForObjects()
        {
            var schema = PromptSchemaGenerator.Create("NestProp")
                .AddObject("data", "Data", d =>
                {
                    d.AddString("key", "Key");
                    d.AddString("val", "Val");
                })
                .Build();

            var nested = schema.Properties[0].NestedProperties;
            Assert.NotNull(nested);
            Assert.Equal(2, nested!.Count);
            Assert.Equal("key", nested[0].Name);
            Assert.Equal("val", nested[1].Name);
        }

        [Fact]
        public void SchemaProperty_Example_StoredCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("ExProp")
                .AddString("city", "City name", example: "Seattle")
                .Build();

            Assert.Equal("Seattle", schema.Properties[0].Example);
        }

        [Fact]
        public void SchemaProperty_EnumValues_StoredCorrectly()
        {
            var schema = PromptSchemaGenerator.Create("EnumProp")
                .AddString("color", "Color", enumValues: new[] { "red", "green", "blue" })
                .Build();

            var enums = schema.Properties[0].EnumValues;
            Assert.NotNull(enums);
            Assert.Equal(3, enums!.Count);
            Assert.Contains("green", enums);
        }

        // ── Complex schema ───────────────────────────────

        [Fact]
        public void ComplexSchema_AllTypesPresent_ValidJsonSchema()
        {
            var schema = PromptSchemaGenerator.Create("FullSchema")
                .WithDescription("Complete test schema")
                .AddString("name", "Name")
                .AddInteger("age", "Age")
                .AddNumber("gpa", "GPA")
                .AddBoolean("enrolled", "Enrolled")
                .AddArray("courses", "Course list")
                .AddArray("grades", "Grades", SchemaType.Number)
                .AddObjectArray("refs", "References", r =>
                {
                    r.AddString("name", "Ref name");
                    r.AddString("relation", "Relation");
                })
                .AddObject("address", "Address", a =>
                {
                    a.AddString("street", "Street");
                    a.AddString("city", "City");
                    a.AddInteger("zip", "ZIP");
                })
                .Build();

            var json = schema.ToJsonSchema();
            var doc = JsonDocument.Parse(json);

            // Verify all top-level properties exist
            var props = doc.RootElement.GetProperty("properties");
            Assert.True(props.TryGetProperty("name", out _));
            Assert.True(props.TryGetProperty("age", out _));
            Assert.True(props.TryGetProperty("gpa", out _));
            Assert.True(props.TryGetProperty("enrolled", out _));
            Assert.True(props.TryGetProperty("courses", out _));
            Assert.True(props.TryGetProperty("grades", out _));
            Assert.True(props.TryGetProperty("refs", out _));
            Assert.True(props.TryGetProperty("address", out _));

            // All should be required by default
            var required = doc.RootElement.GetProperty("required");
            Assert.Equal(8, required.GetArrayLength());
        }
    }
}
