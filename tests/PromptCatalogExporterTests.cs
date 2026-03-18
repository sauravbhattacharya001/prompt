namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using Xunit;

    public class PromptCatalogExporterTests
    {
        // ================================================================
        // Constructor
        // ================================================================

        [Fact]
        public void Constructor_NullLibrary_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptCatalogExporter(null!));
        }

        // ================================================================
        // ToHtml
        // ================================================================

        [Fact]
        public void ToHtml_EmptyLibrary_ReturnsValidHtml()
        {
            var exporter = CreateExporter();
            var html = exporter.ToHtml();

            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Prompt Catalog", html);
            Assert.Contains("0 prompts", html);
        }

        [Fact]
        public void ToHtml_CustomTitle_AppearsInOutput()
        {
            var exporter = CreateExporter();
            var html = exporter.ToHtml("My Custom Title");

            Assert.Contains("My Custom Title", html);
        }

        [Fact]
        public void ToHtml_WithEntries_ContainsCardData()
        {
            var exporter = CreateExporterWithEntries();
            var html = exporter.ToHtml();

            Assert.Contains("code-review", html);
            Assert.Contains("coding", html); // category
            Assert.Contains("Reviews code quality", html); // description
        }

        [Fact]
        public void ToHtml_DarkMode_DifferentColors()
        {
            var exporter = CreateExporterWithEntries();
            var lightHtml = exporter.ToHtml(darkMode: false);
            var darkHtml = exporter.ToHtml(darkMode: true);

            // Dark mode uses different background
            Assert.Contains("#1a1a2e", darkHtml);
            Assert.DoesNotContain("#1a1a2e", lightHtml);
        }

        [Fact]
        public void ToHtml_ContainsSearchAndFilter()
        {
            var exporter = CreateExporterWithEntries();
            var html = exporter.ToHtml();

            Assert.Contains("id=\"search\"", html);
            Assert.Contains("id=\"category-filter\"", html);
            Assert.Contains("<script>", html);
        }

        [Fact]
        public void ToHtml_SpecialCharacters_AreEncoded()
        {
            var lib = new PromptLibrary();
            lib.Add("test-special",
                new PromptTemplate("Hello & goodbye {{name}}"),
                description: "A <b>bold</b> description",
                category: "cat&dog");
            var exporter = new PromptCatalogExporter(lib);
            var html = exporter.ToHtml();

            // Description with HTML should be encoded
            Assert.DoesNotContain("<b>bold</b>", html);
            Assert.Contains("&amp;", html);
        }

        [Fact]
        public void ToHtml_WithTags_DisplaysTags()
        {
            var exporter = CreateExporterWithEntries();
            var html = exporter.ToHtml();

            Assert.Contains("review", html);
            Assert.Contains("quality", html);
        }

        // ================================================================
        // ToCsv
        // ================================================================

        [Fact]
        public void ToCsv_EmptyLibrary_HasHeaderOnly()
        {
            var exporter = CreateExporter();
            var csv = exporter.ToCsv();

            Assert.StartsWith("Name,Category,Description,Tags,Variables,Template,CreatedAt", csv);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines); // header only
        }

        [Fact]
        public void ToCsv_WithEntries_ContainsData()
        {
            var exporter = CreateExporterWithEntries();
            var csv = exporter.ToCsv();

            Assert.Contains("code-review", csv);
            Assert.Contains("coding", csv);
        }

        [Fact]
        public void ToCsv_FieldsWithCommas_AreQuoted()
        {
            var lib = new PromptLibrary();
            lib.Add("test",
                new PromptTemplate("Hello, World"),
                description: "A description, with commas");
            var exporter = new PromptCatalogExporter(lib);
            var csv = exporter.ToCsv();

            Assert.Contains("\"Hello, World\"", csv);
            Assert.Contains("\"A description, with commas\"", csv);
        }

        [Fact]
        public void ToCsv_FieldsWithQuotes_AreEscaped()
        {
            var lib = new PromptLibrary();
            lib.Add("test",
                new PromptTemplate("Say \"hello\""),
                description: "Normal desc");
            var exporter = new PromptCatalogExporter(lib);
            var csv = exporter.ToCsv();

            Assert.Contains("\"\"hello\"\"", csv);
        }

        // ================================================================
        // ToJson
        // ================================================================

        [Fact]
        public void ToJson_EmptyLibrary_ReturnsEmptyArray()
        {
            var exporter = CreateExporter();
            var json = exporter.ToJson();

            Assert.Equal("[]", json);
        }

        [Fact]
        public void ToJson_WithEntries_IsValidJson()
        {
            var exporter = CreateExporterWithEntries();
            var json = exporter.ToJson();

            var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(1, doc.RootElement.GetArrayLength());

            var entry = doc.RootElement[0];
            Assert.Equal("code-review", entry.GetProperty("name").GetString());
            Assert.Equal("coding", entry.GetProperty("category").GetString());
        }

        [Fact]
        public void ToJson_Indented_HasNewlines()
        {
            var exporter = CreateExporterWithEntries();
            var json = exporter.ToJson(indented: true);

            Assert.Contains("\n", json);
        }

        [Fact]
        public void ToJson_NotIndented_IsCompact()
        {
            var exporter = CreateExporterWithEntries();
            var json = exporter.ToJson(indented: false);

            // Compact JSON shouldn't have indentation newlines within the object
            var lines = json.Split('\n');
            Assert.True(lines.Length <= 2); // single line or very few
        }

        [Fact]
        public void ToJson_ContainsVariablesAndTags()
        {
            var exporter = CreateExporterWithEntries();
            var json = exporter.ToJson(indented: true);

            Assert.Contains("\"variables\"", json);
            Assert.Contains("\"tags\"", json);
            Assert.Contains("\"language\"", json); // variable
            Assert.Contains("\"review\"", json); // tag
        }

        // ================================================================
        // SaveHtml / SaveCsv / SaveJson (file I/O)
        // ================================================================

        [Fact]
        public void SaveHtml_CreatesFile()
        {
            var exporter = CreateExporterWithEntries();
            var path = System.IO.Path.GetTempFileName() + ".html";
            try
            {
                exporter.SaveHtml(path, "Test");
                Assert.True(System.IO.File.Exists(path));
                var content = System.IO.File.ReadAllText(path);
                Assert.Contains("<!DOCTYPE html>", content);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void SaveCsv_CreatesFile()
        {
            var exporter = CreateExporterWithEntries();
            var path = System.IO.Path.GetTempFileName() + ".csv";
            try
            {
                exporter.SaveCsv(path);
                Assert.True(System.IO.File.Exists(path));
                var content = System.IO.File.ReadAllText(path);
                Assert.Contains("Name,Category", content);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void SaveJson_CreatesFile()
        {
            var exporter = CreateExporterWithEntries();
            var path = System.IO.Path.GetTempFileName() + ".json";
            try
            {
                exporter.SaveJson(path, indented: true);
                Assert.True(System.IO.File.Exists(path));
                var content = System.IO.File.ReadAllText(path);
                var doc = JsonDocument.Parse(content);
                Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        // ================================================================
        // Multiple entries
        // ================================================================

        [Fact]
        public void AllFormats_MultipleEntries_AllPresent()
        {
            var lib = new PromptLibrary();
            lib.Add("entry-a", new PromptTemplate("A"), category: "cat1");
            lib.Add("entry-b", new PromptTemplate("B"), category: "cat2");
            lib.Add("entry-c", new PromptTemplate("C"), category: "cat1");
            var exporter = new PromptCatalogExporter(lib);

            var html = exporter.ToHtml();
            Assert.Contains("entry-a", html);
            Assert.Contains("entry-b", html);
            Assert.Contains("entry-c", html);
            Assert.Contains("3 prompts", html);
            Assert.Contains("2 categories", html);

            var csv = exporter.ToCsv();
            var dataLines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1);
            Assert.Equal(3, dataLines.Count());

            var json = exporter.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal(3, doc.RootElement.GetArrayLength());
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static PromptCatalogExporter CreateExporter()
        {
            return new PromptCatalogExporter(new PromptLibrary());
        }

        private static PromptCatalogExporter CreateExporterWithEntries()
        {
            var lib = new PromptLibrary();
            lib.Add("code-review",
                new PromptTemplate("Review this {{language}} code:\n{{code}}"),
                description: "Reviews code quality",
                category: "coding",
                tags: new[] { "review", "quality" });
            return new PromptCatalogExporter(lib);
        }
    }
}
