namespace Prompt.Tests
{
    using System.Text.Json;
    using Xunit;

    public class ResponseParserTests
    {
        // ═══════════════════════════════════════════════════════
        // JSON Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractJson_FencedJsonBlock_Deserializes()
        {
            string response = "Here is the result:\n```json\n{\"name\": \"Alice\", \"age\": 30}\n```\nDone!";
            var result = ResponseParser.ExtractJson<TestPerson>(response);
            Assert.NotNull(result);
            Assert.Equal("Alice", result!.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public void ExtractJson_BareJsonObject_Deserializes()
        {
            string response = "The data is {\"name\": \"Bob\", \"age\": 25} and that's it.";
            var result = ResponseParser.ExtractJson<TestPerson>(response);
            Assert.NotNull(result);
            Assert.Equal("Bob", result!.Name);
            Assert.Equal(25, result.Age);
        }

        [Fact]
        public void ExtractJson_BareJsonArray_Deserializes()
        {
            string response = "Results: [1, 2, 3, 4, 5]";
            var result = ResponseParser.ExtractJson<List<int>>(response);
            Assert.NotNull(result);
            Assert.Equal(5, result!.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
        }

        [Fact]
        public void ExtractJson_NestedJson_Deserializes()
        {
            string response = "```json\n{\"name\": \"Charlie\", \"age\": 35, \"address\": {\"city\": \"Seattle\"}}\n```";
            var result = ResponseParser.ExtractJson<TestPersonWithAddress>(response);
            Assert.NotNull(result);
            Assert.Equal("Charlie", result!.Name);
            Assert.Equal("Seattle", result.Address?.City);
        }

        [Fact]
        public void ExtractJson_NoJson_ReturnsDefault()
        {
            string response = "There is no JSON here, just plain text.";
            var result = ResponseParser.ExtractJson<TestPerson>(response);
            Assert.Null(result);
        }

        [Fact]
        public void ExtractJson_MalformedJson_ReturnsDefault()
        {
            string response = "```json\n{\"name\": \"Alice\", age: 30}\n```";
            var result = ResponseParser.ExtractJson<TestPerson>(response);
            Assert.Null(result);
        }

        [Fact]
        public void ExtractJson_CaseInsensitiveProperties_Works()
        {
            string response = "{\"NAME\": \"Dave\", \"AGE\": 40}";
            var result = ResponseParser.ExtractJson<TestPerson>(response);
            Assert.NotNull(result);
            Assert.Equal("Dave", result!.Name);
            Assert.Equal(40, result.Age);
        }

        [Fact]
        public void ExtractJson_TrailingCommas_Tolerated()
        {
            string response = "{\"name\": \"Eve\", \"age\": 28,}";
            var result = ResponseParser.ExtractJson<TestPerson>(response);
            Assert.NotNull(result);
            Assert.Equal("Eve", result!.Name);
        }

        [Fact]
        public void ExtractJsonDocument_ValidJson_ReturnsParsed()
        {
            string response = "```json\n{\"key\": \"value\", \"count\": 42}\n```";
            using var doc = ResponseParser.ExtractJsonDocument(response);
            Assert.NotNull(doc);
            Assert.Equal("value", doc!.RootElement.GetProperty("key").GetString());
            Assert.Equal(42, doc.RootElement.GetProperty("count").GetInt32());
        }

        [Fact]
        public void ExtractJsonDocument_NoJson_ReturnsNull()
        {
            var doc = ResponseParser.ExtractJsonDocument("No JSON here.");
            Assert.Null(doc);
        }

        [Fact]
        public void TryExtractJson_ValidJson_ReturnsTrue()
        {
            string response = "{\"name\": \"Frank\", \"age\": 50}";
            bool success = ResponseParser.TryExtractJson<TestPerson>(response, out var result);
            Assert.True(success);
            Assert.Equal("Frank", result!.Name);
        }

        [Fact]
        public void TryExtractJson_InvalidJson_ReturnsFalse()
        {
            bool success = ResponseParser.TryExtractJson<TestPerson>("not json", out var result);
            Assert.False(success);
            Assert.Null(result);
        }

        // ═══════════════════════════════════════════════════════
        // List Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractList_NumberedList_ExtractsItems()
        {
            string response = "Here are the steps:\n1. First step\n2. Second step\n3. Third step";
            var items = ResponseParser.ExtractList(response);
            Assert.Equal(3, items.Count);
            Assert.Equal("First step", items[0]);
            Assert.Equal("Second step", items[1]);
            Assert.Equal("Third step", items[2]);
        }

        [Fact]
        public void ExtractList_BulletedList_ExtractsItems()
        {
            string response = "Features:\n- Fast\n- Reliable\n- Easy to use";
            var items = ResponseParser.ExtractList(response);
            Assert.Equal(3, items.Count);
            Assert.Equal("Fast", items[0]);
        }

        [Fact]
        public void ExtractList_AsteriskList_ExtractsItems()
        {
            string response = "* Item A\n* Item B\n* Item C";
            var items = ResponseParser.ExtractList(response);
            Assert.Equal(3, items.Count);
        }

        [Fact]
        public void ExtractList_BulletPointCharacter_ExtractsItems()
        {
            string response = "• Alpha\n• Beta\n• Gamma";
            var items = ResponseParser.ExtractList(response);
            Assert.Equal(3, items.Count);
            Assert.Equal("Alpha", items[0]);
        }

        [Fact]
        public void ExtractList_MixedFormats_ExtractsAll()
        {
            string response = "1. First\n- Second\n* Third\n2. Fourth";
            var items = ResponseParser.ExtractList(response);
            Assert.Equal(4, items.Count);
        }

        [Fact]
        public void ExtractList_NoList_ReturnsEmpty()
        {
            var items = ResponseParser.ExtractList("Just plain text without any list items.");
            Assert.Empty(items);
        }

        [Fact]
        public void ExtractList_EmptyItems_Skipped()
        {
            string response = "1. \n2. Real item\n3. ";
            var items = ResponseParser.ExtractList(response);
            Assert.Single(items);
            Assert.Equal("Real item", items[0]);
        }

        [Fact]
        public void ExtractNumberedList_PreservesNumbers()
        {
            string response = "1. Apple\n2. Banana\n5. Cherry";
            var items = ResponseParser.ExtractNumberedList(response);
            Assert.Equal(3, items.Count);
            Assert.Equal("Apple", items[1]);
            Assert.Equal("Banana", items[2]);
            Assert.Equal("Cherry", items[5]);
        }

        // ═══════════════════════════════════════════════════════
        // Key-Value Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractKeyValuePairs_ColonFormat_Works()
        {
            string response = "Name: Alice\nAge: 30\nCity: Seattle";
            var pairs = ResponseParser.ExtractKeyValuePairs(response);
            Assert.Equal(3, pairs.Count);
            Assert.Equal("Alice", pairs["Name"]);
            Assert.Equal("30", pairs["Age"]);
            Assert.Equal("Seattle", pairs["City"]);
        }

        [Fact]
        public void ExtractKeyValuePairs_EqualsFormat_Works()
        {
            string response = "Width = 100\nHeight = 200";
            var pairs = ResponseParser.ExtractKeyValuePairs(response);
            Assert.Equal("100", pairs["Width"]);
            Assert.Equal("200", pairs["Height"]);
        }

        [Fact]
        public void ExtractKeyValuePairs_BoldKeys_Works()
        {
            string response = "**Name**: Alice\n**Age**: 30";
            var pairs = ResponseParser.ExtractKeyValuePairs(response);
            Assert.Equal("Alice", pairs["Name"]);
        }

        [Fact]
        public void ExtractKeyValuePairs_CaseInsensitive()
        {
            string response = "name: Alice\nNAME: Bob";
            var pairs = ResponseParser.ExtractKeyValuePairs(response);
            // Last value wins for case-insensitive keys
            Assert.Equal("Bob", pairs["name"]);
        }

        [Fact]
        public void ExtractValue_SpecificKey_ReturnsValue()
        {
            string response = "Name: Alice\nAge: 30\nCity: Seattle";
            Assert.Equal("30", ResponseParser.ExtractValue(response, "Age"));
        }

        [Fact]
        public void ExtractValue_MissingKey_ReturnsNull()
        {
            string response = "Name: Alice";
            Assert.Null(ResponseParser.ExtractValue(response, "Email"));
        }

        // ═══════════════════════════════════════════════════════
        // Code Block Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractCodeBlocks_SingleBlock_Extracted()
        {
            string response = "Here's some code:\n```csharp\nConsole.WriteLine(\"Hello\");\n```";
            var blocks = ResponseParser.ExtractCodeBlocks(response);
            Assert.Single(blocks);
            Assert.Equal("csharp", blocks[0].Language);
            Assert.Contains("Console.WriteLine", blocks[0].Code);
        }

        [Fact]
        public void ExtractCodeBlocks_MultipleBlocks_AllExtracted()
        {
            string response = "```python\nprint('hello')\n```\n\nAnd also:\n```javascript\nconsole.log('hi')\n```";
            var blocks = ResponseParser.ExtractCodeBlocks(response);
            Assert.Equal(2, blocks.Count);
            Assert.Equal("python", blocks[0].Language);
            Assert.Equal("javascript", blocks[1].Language);
        }

        [Fact]
        public void ExtractCodeBlocks_NoLanguageTag_NullLanguage()
        {
            string response = "```\nsome code\n```";
            var blocks = ResponseParser.ExtractCodeBlocks(response);
            Assert.Single(blocks);
            Assert.Null(blocks[0].Language);
        }

        [Fact]
        public void ExtractCodeBlock_ByLanguage_ReturnsMatch()
        {
            string response = "```python\nprint('py')\n```\n```csharp\nConsole.Write('cs')\n```";
            var block = ResponseParser.ExtractCodeBlock(response, "csharp");
            Assert.NotNull(block);
            Assert.Equal("csharp", block!.Language);
            Assert.Contains("Console.Write", block.Code);
        }

        [Fact]
        public void ExtractCodeBlock_NoLanguageFilter_ReturnsFirst()
        {
            string response = "```python\nfirst\n```\n```csharp\nsecond\n```";
            var block = ResponseParser.ExtractCodeBlock(response);
            Assert.NotNull(block);
            Assert.Equal("python", block!.Language);
        }

        [Fact]
        public void ExtractCodeBlock_LanguageNotFound_ReturnsNull()
        {
            string response = "```python\ncode\n```";
            Assert.Null(ResponseParser.ExtractCodeBlock(response, "rust"));
        }

        [Fact]
        public void CodeBlock_ToString_FormatsCorrectly()
        {
            var block = new CodeBlock("csharp", "var x = 1;");
            Assert.Equal("```csharp\nvar x = 1;\n```", block.ToString());
        }

        // ═══════════════════════════════════════════════════════
        // Table Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractTable_SimpleTable_Parsed()
        {
            string response = "| Name | Age | City |\n|------|-----|------|\n| Alice | 30 | Seattle |\n| Bob | 25 | Portland |";
            var rows = ResponseParser.ExtractTable(response);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Alice", rows[0]["Name"]);
            Assert.Equal("30", rows[0]["Age"]);
            Assert.Equal("Bob", rows[1]["Name"]);
        }

        [Fact]
        public void ExtractTable_NoTable_ReturnsEmpty()
        {
            var rows = ResponseParser.ExtractTable("Just text.");
            Assert.Empty(rows);
        }

        [Fact]
        public void ExtractTable_HeaderOnly_ReturnsEmpty()
        {
            string response = "| Name | Age |\n|------|-----|";
            var rows = ResponseParser.ExtractTable(response);
            Assert.Empty(rows);
        }

        // ═══════════════════════════════════════════════════════
        // Pattern Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractPattern_EmailPattern_FindsAll()
        {
            string response = "Contact alice@example.com or bob@test.org for info.";
            var emails = ResponseParser.ExtractPattern(response, @"[\w.+-]+@[\w-]+\.[\w.]+");
            Assert.Equal(2, emails.Count);
            Assert.Equal("alice@example.com", emails[0]);
            Assert.Equal("bob@test.org", emails[1]);
        }

        [Fact]
        public void ExtractPattern_CaptureGroup_ReturnsGroupValue()
        {
            string response = "Score: 95 out of 100\nGrade: A+";
            var scores = ResponseParser.ExtractPattern(response, @"Score:\s*(\d+)");
            Assert.Single(scores);
            Assert.Equal("95", scores[0]);
        }

        [Fact]
        public void ExtractFirstPattern_ReturnsOnlyFirst()
        {
            string response = "Prices: $10, $20, $30";
            string? first = ResponseParser.ExtractFirstPattern(response, @"\$(\d+)");
            Assert.Equal("10", first);
        }

        [Fact]
        public void ExtractPattern_NoMatch_ReturnsEmpty()
        {
            var results = ResponseParser.ExtractPattern("no match", @"xyz(\d+)");
            Assert.Empty(results);
        }

        [Fact]
        public void ExtractPattern_NullPattern_Throws()
        {
            Assert.Throws<ArgumentException>(() => ResponseParser.ExtractPattern("text", ""));
        }

        // ═══════════════════════════════════════════════════════
        // Boolean Extraction
        // ═══════════════════════════════════════════════════════

        [Theory]
        [InlineData("Yes, that is correct.", true)]
        [InlineData("yes", true)]
        [InlineData("True", true)]
        [InlineData("Absolutely!", true)]
        [InlineData("Certainly, I can help.", true)]
        [InlineData("Indeed, that's right.", true)]
        [InlineData("Definitely.", true)]
        public void ExtractBoolean_Affirmative_ReturnsTrue(string response, bool expected)
        {
            Assert.Equal(expected, ResponseParser.ExtractBoolean(response));
        }

        [Theory]
        [InlineData("No, that's incorrect.", false)]
        [InlineData("no", false)]
        [InlineData("False", false)]
        [InlineData("Incorrect.", false)]
        [InlineData("Negative.", false)]
        public void ExtractBoolean_Negative_ReturnsFalse(string response, bool expected)
        {
            Assert.Equal(expected, ResponseParser.ExtractBoolean(response));
        }

        [Theory]
        [InlineData("It depends on the context.")]
        [InlineData("Maybe, under certain conditions.")]
        [InlineData("")]
        public void ExtractBoolean_Ambiguous_ReturnsNull(string response)
        {
            Assert.Null(ResponseParser.ExtractBoolean(response));
        }

        // ═══════════════════════════════════════════════════════
        // Number Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractNumbers_MixedText_FindsAll()
        {
            string response = "The result is 42, with a margin of 3.14 and -7 outliers.";
            var numbers = ResponseParser.ExtractNumbers(response);
            Assert.Equal(3, numbers.Count);
            Assert.Equal(42, numbers[0]);
            Assert.Equal(3.14, numbers[1], 2);
            Assert.Equal(-7, numbers[2]);
        }

        [Fact]
        public void ExtractNumbers_CommaFormatted_Parsed()
        {
            string response = "Population: 1,234,567";
            var numbers = ResponseParser.ExtractNumbers(response);
            Assert.Contains(1234567, numbers);
        }

        [Fact]
        public void ExtractNumbers_NoNumbers_ReturnsEmpty()
        {
            var numbers = ResponseParser.ExtractNumbers("No numbers here.");
            Assert.Empty(numbers);
        }

        [Fact]
        public void ExtractFirstNumber_ReturnsFirst()
        {
            Assert.Equal(42, ResponseParser.ExtractFirstNumber("Answer: 42 or 100"));
        }

        [Fact]
        public void ExtractFirstNumber_NoNumber_ReturnsNull()
        {
            Assert.Null(ResponseParser.ExtractFirstNumber("No number"));
        }

        // ═══════════════════════════════════════════════════════
        // Section Extraction
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractSection_ByHeading_ReturnsContent()
        {
            string response = "## Introduction\nSome intro text.\n\n## Details\nThe details here.\n\n## Conclusion\nEnd.";
            var section = ResponseParser.ExtractSection(response, "Details");
            Assert.NotNull(section);
            Assert.Equal("The details here.", section);
        }

        [Fact]
        public void ExtractSection_CaseInsensitive()
        {
            string response = "## MY SECTION\nContent here.";
            Assert.NotNull(ResponseParser.ExtractSection(response, "my section"));
        }

        [Fact]
        public void ExtractSection_MissingHeading_ReturnsNull()
        {
            string response = "## Intro\nText.";
            Assert.Null(ResponseParser.ExtractSection(response, "Missing"));
        }

        [Fact]
        public void ExtractSection_NestedHeadings_StopsAtSameLevel()
        {
            string response = "## A\nText A\n### SubA\nSub text\n## B\nText B";
            var section = ResponseParser.ExtractSection(response, "A");
            Assert.Contains("Text A", section);
            Assert.Contains("SubA", section);
            Assert.DoesNotContain("Text B", section!);
        }

        [Fact]
        public void ExtractHeadings_FindsAllLevels()
        {
            string response = "# Title\n## Section\n### Subsection\n#### Deep";
            var headings = ResponseParser.ExtractHeadings(response);
            Assert.Equal(4, headings.Count);
            Assert.Equal(1, headings[0].Level);
            Assert.Equal("Title", headings[0].Text);
            Assert.Equal(4, headings[3].Level);
        }

        // ═══════════════════════════════════════════════════════
        // Composite Parse
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void Parse_RichResponse_ExtractsEverything()
        {
            string response = @"## Summary
Here are the results:
1. Item one
2. Item two

Name: Alice
Age: 30

```csharp
var x = 1;
```

The answer is 42.

```json
{""key"": ""value""}
```";

            var result = ResponseParser.Parse(response);
            Assert.True(result.HasStructuredData);
            Assert.NotNull(result.Json);
            Assert.True(result.Lists.Count >= 2);
            Assert.True(result.KeyValuePairs.Count >= 2);
            Assert.True(result.CodeBlocks.Count >= 1);
            Assert.Contains(42, result.Numbers);
            Assert.True(result.Headings.Count >= 1);
        }

        [Fact]
        public void Parse_PlainText_HasNoStructuredData()
        {
            var result = ResponseParser.Parse("Just a plain sentence with no structure.");
            Assert.False(result.HasStructuredData);
        }

        [Fact]
        public void Parse_PreservesRawResponse()
        {
            string response = "Hello world";
            var result = ResponseParser.Parse(response);
            Assert.Equal(response, result.RawResponse);
        }

        // ═══════════════════════════════════════════════════════
        // Input Validation
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractJson_NullInput_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => ResponseParser.ExtractJson<TestPerson>(null!));
        }

        [Fact]
        public void ExtractList_NullInput_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => ResponseParser.ExtractList(null!));
        }

        [Fact]
        public void ExtractSection_EmptyHeading_Throws()
        {
            Assert.Throws<ArgumentException>(() => ResponseParser.ExtractSection("text", ""));
        }

        [Fact]
        public void ExtractJson_OversizedInput_Throws()
        {
            string huge = new string('x', ResponseParser.MaxResponseLength + 1);
            Assert.Throws<ArgumentException>(() => ResponseParser.ExtractJson<TestPerson>(huge));
        }

        // ═══════════════════════════════════════════════════════
        // Internal Helpers
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractFencedBlock_SpecificLanguage_Works()
        {
            string response = "```python\nprint('py')\n```\n```json\n{\"a\": 1}\n```";
            string? block = ResponseParser.ExtractFencedBlock(response, "json");
            Assert.NotNull(block);
            Assert.Contains("\"a\"", block);
        }

        [Fact]
        public void ExtractBareJson_NestedBraces_HandledCorrectly()
        {
            string response = "Result: {\"outer\": {\"inner\": {\"deep\": true}}} done.";
            string? json = ResponseParser.ExtractBareJson(response);
            Assert.NotNull(json);
            var doc = JsonDocument.Parse(json!);
            Assert.True(doc.RootElement.GetProperty("outer").GetProperty("inner").GetProperty("deep").GetBoolean());
        }

        [Fact]
        public void ExtractBareJson_StringsWithBraces_HandledCorrectly()
        {
            string response = "Data: {\"text\": \"hello {world}\"} end";
            string? json = ResponseParser.ExtractBareJson(response);
            Assert.NotNull(json);
            var doc = JsonDocument.Parse(json!);
            Assert.Equal("hello {world}", doc.RootElement.GetProperty("text").GetString());
        }

        [Fact]
        public void ExtractBareJson_NoJson_ReturnsNull()
        {
            Assert.Null(ResponseParser.ExtractBareJson("no json here"));
        }

        // ═══════════════════════════════════════════════════════
        // Edge Cases
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void ExtractJson_NumbersAsStrings_Handled()
        {
            string response = "{\"name\": \"Test\", \"age\": \"42\"}";
            var result = ResponseParser.ExtractJson<TestPerson>(response);
            Assert.NotNull(result);
            Assert.Equal(42, result!.Age);
        }

        [Fact]
        public void ExtractList_IndentedItems_Extracted()
        {
            string response = "  1. First\n  2. Second\n  3. Third";
            var items = ResponseParser.ExtractList(response);
            Assert.Equal(3, items.Count);
        }

        [Fact]
        public void ExtractCodeBlocks_MultilineCode_PreservedCorrectly()
        {
            string response = "```python\ndef hello():\n    print('hello')\n    return True\n```";
            var blocks = ResponseParser.ExtractCodeBlocks(response);
            Assert.Single(blocks);
            Assert.Contains("def hello():", blocks[0].Code);
            Assert.Contains("return True", blocks[0].Code);
        }

        [Fact]
        public void ExtractNumbers_Decimals_CorrectPrecision()
        {
            string response = "Pi is approximately 3.14159265 and e is about 2.71828.";
            var numbers = ResponseParser.ExtractNumbers(response);
            Assert.Equal(2, numbers.Count);
            Assert.Equal(3.14159265, numbers[0], 8);
            Assert.Equal(2.71828, numbers[1], 5);
        }

        [Fact]
        public void ExtractBoolean_WhitespaceInput_ReturnsNull()
        {
            Assert.Null(ResponseParser.ExtractBoolean("   \n   "));
        }

        [Fact]
        public void ExtractTable_WithAlignment_Parsed()
        {
            string response = "| Left | Center | Right |\n|:-----|:------:|------:|\n| a | b | c |";
            var rows = ResponseParser.ExtractTable(response);
            Assert.Single(rows);
            Assert.Equal("a", rows[0]["Left"]);
        }

        // ═══════════════════════════════════════════════════════
        // Test Models
        // ═══════════════════════════════════════════════════════

        private class TestPerson
        {
            public string Name { get; set; } = "";
            public int Age { get; set; }
        }

        private class TestPersonWithAddress
        {
            public string Name { get; set; } = "";
            public int Age { get; set; }
            public TestAddress? Address { get; set; }
        }

        private class TestAddress
        {
            public string City { get; set; } = "";
        }
    }
}
