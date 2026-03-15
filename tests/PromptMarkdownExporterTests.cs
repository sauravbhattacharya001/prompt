namespace Prompt.Tests
{
    using Xunit;

    public class PromptMarkdownExporterTests
    {
        private PromptLibrary CreateSampleLibrary()
        {
            var lib = new PromptLibrary();
            lib.Add("code-review",
                new PromptTemplate(
                    "Review this {{language}} code:\n{{code}}",
                    new Dictionary<string, string> { ["language"] = "C#" }),
                description: "Reviews code and suggests improvements",
                category: "coding",
                tags: new[] { "review", "quality" });

            lib.Add("summarize",
                new PromptTemplate("Summarize in {{style}} style:\n{{text}}",
                    new Dictionary<string, string> { ["style"] = "concise" }),
                description: "Summarizes text with configurable style",
                category: "writing",
                tags: new[] { "summary" });

            lib.Add("translate",
                new PromptTemplate("Translate to {{language}}:\n{{text}}"),
                category: "writing");

            return lib;
        }

        [Fact]
        public void Export_NullLibrary_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => PromptMarkdownExporter.Export(null!));
        }

        [Fact]
        public void Export_EmptyLibrary_ProducesHeader()
        {
            var lib = new PromptLibrary();
            var md = PromptMarkdownExporter.Export(lib);

            Assert.Contains("# Prompt Library", md);
            Assert.Contains("Entries: 0", md);
        }

        [Fact]
        public void Export_CustomTitle()
        {
            var lib = new PromptLibrary();
            var md = PromptMarkdownExporter.Export(lib, title: "My Prompts");

            Assert.Contains("# My Prompts", md);
        }

        [Fact]
        public void Export_IncludesTableOfContents()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib);

            Assert.Contains("## Table of Contents", md);
            Assert.Contains("### coding", md);
            Assert.Contains("### writing", md);
            Assert.Contains("[code-review]", md);
        }

        [Fact]
        public void Export_IncludesEntryDetails()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib);

            Assert.Contains("## code-review", md);
            Assert.Contains("*Reviews code and suggests improvements*", md);
            Assert.Contains("**Category**: coding", md);
            Assert.Contains("**Tags**: quality, review", md);
            Assert.Contains("`{{language}}`", md);
            Assert.Contains("- `language` = `C#`", md);
            Assert.Contains("Review this {{language}} code:", md);
        }

        [Fact]
        public void Export_WithoutMetadata_SkipsTimestamps()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib, includeMetadata: false);

            Assert.DoesNotContain("**Created**:", md);
            Assert.DoesNotContain("**Updated**:", md);
        }

        [Fact]
        public void Export_WithMetadata_IncludesTimestamps()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib, includeMetadata: true);

            Assert.Contains("**Created**:", md);
            Assert.Contains("**Updated**:", md);
        }

        [Fact]
        public void Import_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PromptMarkdownExporter.Import(null!));
            Assert.Throws<ArgumentException>(
                () => PromptMarkdownExporter.Import(""));
            Assert.Throws<ArgumentException>(
                () => PromptMarkdownExporter.Import("   "));
        }

        [Fact]
        public void RoundTrip_PreservesEntries()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib, includeMetadata: false);
            var imported = PromptMarkdownExporter.Import(md);

            Assert.Equal(3, imported.Count);
            Assert.NotNull(imported.Get("code-review"));
            Assert.NotNull(imported.Get("summarize"));
            Assert.NotNull(imported.Get("translate"));
        }

        [Fact]
        public void RoundTrip_PreservesDescription()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib);
            var imported = PromptMarkdownExporter.Import(md);

            var entry = imported.Get("code-review");
            Assert.Equal("Reviews code and suggests improvements", entry.Description);
        }

        [Fact]
        public void RoundTrip_PreservesCategory()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib);
            var imported = PromptMarkdownExporter.Import(md);

            Assert.Equal("coding", imported.Get("code-review").Category);
            Assert.Equal("writing", imported.Get("summarize").Category);
        }

        [Fact]
        public void RoundTrip_PreservesTags()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib);
            var imported = PromptMarkdownExporter.Import(md);

            var entry = imported.Get("code-review");
            Assert.Contains("review", entry.Tags);
            Assert.Contains("quality", entry.Tags);
        }

        [Fact]
        public void RoundTrip_PreservesDefaults()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib);
            var imported = PromptMarkdownExporter.Import(md);

            var entry = imported.Get("code-review");
            Assert.Equal("C#", entry.Template.Defaults["language"]);
        }

        [Fact]
        public void RoundTrip_PreservesTemplate()
        {
            var lib = CreateSampleLibrary();
            var md = PromptMarkdownExporter.Export(lib);
            var imported = PromptMarkdownExporter.Import(md);

            Assert.Equal(
                "Review this {{language}} code:\n{{code}}",
                imported.Get("code-review").Template.Template);
        }

        [Fact]
        public void Import_SkipsEntriesWithoutTemplates()
        {
            var md = "## no-template\n\n*Has no code block*\n\n## with-template\n\n```\nHello {{name}}\n```\n";
            var lib = PromptMarkdownExporter.Import(md);

            Assert.Equal(1, lib.Count);
            Assert.NotNull(lib.Get("with-template"));
        }

        [Fact]
        public void Import_SkipsTableOfContentsHeader()
        {
            var md = "## Table of Contents\n\n- item\n\n## real-entry\n\n```\nTemplate text\n```\n";
            var lib = PromptMarkdownExporter.Import(md);

            Assert.Equal(1, lib.Count);
            Assert.NotNull(lib.Get("real-entry"));
        }

        [Fact]
        public void ExportEntry_NullEntry_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => PromptMarkdownExporter.ExportEntry(null!));
        }

        [Fact]
        public void ExportEntry_ProducesValidSnippet()
        {
            var entry = new PromptEntry(
                "test-entry",
                new PromptTemplate("Hello {{name}}"),
                description: "A test",
                category: "testing",
                tags: new[] { "test" });

            var md = PromptMarkdownExporter.ExportEntry(entry);

            Assert.Contains("## test-entry", md);
            Assert.Contains("*A test*", md);
            Assert.Contains("**Category**: testing", md);
            Assert.Contains("Hello {{name}}", md);
        }

        [Fact]
        public void Import_MultilineTemplate()
        {
            var md = "## multi\n\n```\nLine 1\nLine 2\nLine 3\n```\n";
            var lib = PromptMarkdownExporter.Import(md);

            var entry = lib.Get("multi");
            Assert.Equal("Line 1\nLine 2\nLine 3", entry.Template.Template);
        }

        [Fact]
        public void Export_EntryWithNoTags_OmitsTagsLine()
        {
            var lib = new PromptLibrary();
            lib.Add("simple",
                new PromptTemplate("Hello {{name}}"),
                category: "general");

            var md = PromptMarkdownExporter.Export(lib);
            // Should have Category but the Tags line should mention no tags
            Assert.Contains("**Category**: general", md);
        }

        [Fact]
        public async Task ExportToFileAsync_NullPath_Throws()
        {
            var lib = new PromptLibrary();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => PromptMarkdownExporter.ExportToFileAsync(lib, null!));
        }

        [Fact]
        public async Task ImportFromFileAsync_NullPath_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => PromptMarkdownExporter.ImportFromFileAsync(null!));
        }

        [Fact]
        public async Task ImportFromFileAsync_MissingFile_Throws()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => PromptMarkdownExporter.ImportFromFileAsync("nonexistent.md"));
        }

        [Fact]
        public async Task FileRoundTrip_Works()
        {
            var lib = new PromptLibrary();
            lib.Add("file-test",
                new PromptTemplate("Test {{var}}",
                    new Dictionary<string, string> { ["var"] = "value" }),
                description: "File round-trip test",
                category: "testing");

            var path = Path.Combine(Path.GetTempPath(), $"prompt-md-test-{Guid.NewGuid()}.md");
            try
            {
                await PromptMarkdownExporter.ExportToFileAsync(lib, path);
                var imported = await PromptMarkdownExporter.ImportFromFileAsync(path);

                Assert.Equal(1, imported.Count);
                var entry = imported.Get("file-test");
                Assert.Equal("Test {{var}}", entry.Template.Template);
                Assert.Equal("value", entry.Template.Defaults["var"]);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Export_UncategorizedEntries_GroupedCorrectly()
        {
            var lib = new PromptLibrary();
            lib.Add("no-cat", new PromptTemplate("Hello"));

            var md = PromptMarkdownExporter.Export(lib);
            Assert.Contains("### Uncategorized", md);
        }
    }
}
