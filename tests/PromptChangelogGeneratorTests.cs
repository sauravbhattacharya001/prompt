namespace Prompt.Tests
{
    using Xunit;

    public class PromptChangelogGeneratorTests
    {
        private PromptVersionManager CreateSampleManager()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("greeting", "Hello {{name}}", "Initial version", "Alice");
            vm.CreateVersion("greeting", "Hello {{name}}! Welcome to {{place}}.", "Added welcome and place", "Bob");
            vm.CreateVersion("greeting", "Hi {{name}}! Welcome to {{place}}. Enjoy your stay!", "Friendlier tone", "Alice");
            vm.CreateVersion("farewell", "Goodbye {{name}}", "Initial farewell", "Charlie");
            vm.CreateVersion("farewell", "See you later, {{name}}!", "More casual", "Alice");
            return vm;
        }

        [Fact]
        public void Constructor_NullVersionManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptChangelogGenerator(null!));
        }

        [Fact]
        public void Generate_NullTemplateName_Throws()
        {
            var gen = new PromptChangelogGenerator(new PromptVersionManager());
            Assert.Throws<ArgumentException>(() => gen.Generate(null!));
        }

        [Fact]
        public void Generate_EmptyTemplateName_Throws()
        {
            var gen = new PromptChangelogGenerator(new PromptVersionManager());
            Assert.Throws<ArgumentException>(() => gen.Generate(""));
        }

        [Fact]
        public void Generate_Markdown_DefaultOptions()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting");

            Assert.Contains("# Changelog: greeting", result);
            Assert.Contains("## greeting", result);
            Assert.Contains("### v3", result);
            Assert.Contains("### v2", result);
            Assert.Contains("### v1", result);
            Assert.Contains("Alice", result);
            Assert.Contains("Bob", result);
            Assert.Contains("Friendlier tone", result);
        }

        [Fact]
        public void Generate_Markdown_IncludesChangeSummary()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting");

            Assert.Contains("**Changes:**", result);
        }

        [Fact]
        public void Generate_Markdown_NoDiffs()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { IncludeDiffs = false });

            Assert.DoesNotContain("**Changes:**", result);
        }

        [Fact]
        public void Generate_Markdown_IncludeFullText()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { IncludeFullText = true });

            Assert.Contains("**Template:**", result);
            Assert.Contains("Hello {{name}}", result);
        }

        [Fact]
        public void Generate_PlainText()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { Format = ChangelogFormat.PlainText });

            Assert.Contains("CHANGELOG: GREETING", result);
            Assert.Contains("v3", result);
            Assert.Contains("v2", result);
            Assert.Contains("Alice", result);
        }

        [Fact]
        public void Generate_Html()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { Format = ChangelogFormat.Html });

            Assert.Contains("<!DOCTYPE html>", result);
            Assert.Contains("<h1>", result);
            Assert.Contains("class=\"version\"", result);
            Assert.Contains("v3", result);
        }

        [Fact]
        public void Generate_Html_DiffColors()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { Format = ChangelogFormat.Html });

            Assert.Contains("class=\"added\"", result);
        }

        [Fact]
        public void Generate_AuthorFilter()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { AuthorFilter = "Bob" });

            Assert.Contains("Bob", result);
            Assert.Contains("1 version(s)", result);
        }

        [Fact]
        public void Generate_MaxVersions()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { MaxVersions = 2 });

            Assert.Contains("2 version(s)", result);
        }

        [Fact]
        public void Generate_ReverseChronological_False()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { ReverseChronological = false });

            int v1Pos = result.IndexOf("### v1");
            int v3Pos = result.IndexOf("### v3");
            Assert.True(v1Pos < v3Pos, "v1 should appear before v3 in chronological order");
        }

        [Fact]
        public void Generate_CustomTitle()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions { Title = "My Custom Title" });

            Assert.Contains("# My Custom Title", result);
        }

        [Fact]
        public void Generate_NoVersions_ShowsEmptyMessage()
        {
            var vm = new PromptVersionManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("nonexistent");

            Assert.Contains("No versions found", result);
        }

        [Fact]
        public void GenerateAll_MultipleTemplates()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.GenerateAll();

            Assert.Contains("All Templates", result);
            Assert.Contains("greeting", result);
            Assert.Contains("farewell", result);
            Assert.Contains("2 template(s) tracked", result);
        }

        [Fact]
        public void GenerateAll_EmptyManager()
        {
            var vm = new PromptVersionManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.GenerateAll();

            Assert.Contains("No templates tracked", result);
        }

        [Fact]
        public void GenerateAll_Html()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.GenerateAll(new ChangelogOptions { Format = ChangelogFormat.Html });

            Assert.Contains("<!DOCTYPE html>", result);
            Assert.Contains("greeting", result);
            Assert.Contains("farewell", result);
        }

        [Fact]
        public void GenerateAll_PlainText()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.GenerateAll(new ChangelogOptions { Format = ChangelogFormat.PlainText });

            Assert.Contains("ALL TEMPLATES", result);
            Assert.Contains("greeting", result);
            Assert.Contains("farewell", result);
        }

        [Fact]
        public void GetStats_BasicStats()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var stats = gen.GetStats("greeting");

            Assert.Equal(3, stats.TotalVersions);
            Assert.Equal(2, stats.UniqueAuthors);
            Assert.Contains("Alice", stats.Authors);
            Assert.Contains("Bob", stats.Authors);
            Assert.NotNull(stats.FirstVersionDate);
            Assert.NotNull(stats.LastVersionDate);
        }

        [Fact]
        public void GetStats_DiffStats()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var stats = gen.GetStats("greeting");

            Assert.True(stats.TotalLinesAdded > 0);
            Assert.True(stats.VersionsWithChanges > 0);
        }

        [Fact]
        public void GetStats_EmptyTemplate()
        {
            var vm = new PromptVersionManager();
            var gen = new PromptChangelogGenerator(vm);
            var stats = gen.GetStats("nonexistent");

            Assert.Equal(0, stats.TotalVersions);
            Assert.Equal(0, stats.UniqueAuthors);
        }

        [Fact]
        public void GetStats_NullTemplateName()
        {
            var gen = new PromptChangelogGenerator(new PromptVersionManager());
            var stats = gen.GetStats(null!);

            Assert.Equal(0, stats.TotalVersions);
        }

        [Fact]
        public void GetStats_RollbackDetection()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("test", "Version 1 text", "Initial", "Author");
            vm.CreateVersion("test", "Version 2 text", "Update", "Author");
            vm.Rollback("test", 1, "Author");

            var gen = new PromptChangelogGenerator(vm);
            var stats = gen.GetStats("test");

            Assert.Equal(1, stats.RollbackCount);
        }

        [Fact]
        public void GetAllStats_AcrossTemplates()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var stats = gen.GetAllStats();

            Assert.Equal(5, stats.TotalVersions);
            Assert.Equal(3, stats.UniqueAuthors);
            Assert.Contains("Charlie", stats.Authors);
        }

        [Fact]
        public void Generate_DateFilter_Since()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("test", "v1", "First", "A");
            // All versions created at ~now, so Since = now-1h should include all
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("test", new ChangelogOptions
            {
                Since = DateTimeOffset.UtcNow.AddHours(-1)
            });

            Assert.Contains("v1", result);
        }

        [Fact]
        public void Generate_DateFilter_Until_ExcludesFuture()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("test", "v1", "First", "A");
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("test", new ChangelogOptions
            {
                Until = DateTimeOffset.UtcNow.AddHours(-1)
            });

            Assert.Contains("No versions found", result);
        }

        [Fact]
        public void Generate_Html_EncodesSpecialChars()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("test", "Hello <b>world</b> & \"friends\"", "Has HTML chars", "Author");

            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("test", new ChangelogOptions
            {
                Format = ChangelogFormat.Html,
                IncludeFullText = true
            });

            Assert.Contains("&lt;b&gt;", result);
            Assert.Contains("&amp;", result);
            Assert.Contains("&quot;friends&quot;", result);
            Assert.DoesNotContain("<b>world</b>", result);
        }

        [Fact]
        public void Generate_SingleVersion_NoDiffShown()
        {
            var vm = new PromptVersionManager();
            vm.CreateVersion("single", "Just one version", "Only one", "A");

            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("single");

            Assert.DoesNotContain("**Changes:**", result);
            Assert.Contains("v1", result);
        }

        [Fact]
        public void Generate_PlainText_IncludeFullText()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.Generate("greeting", new ChangelogOptions
            {
                Format = ChangelogFormat.PlainText,
                IncludeFullText = true
            });

            Assert.Contains("Text:", result);
        }

        [Fact]
        public void GenerateAll_WithAuthorFilter()
        {
            var vm = CreateSampleManager();
            var gen = new PromptChangelogGenerator(vm);
            var result = gen.GenerateAll(new ChangelogOptions { AuthorFilter = "Alice" });

            // Alice has versions in both templates
            Assert.Contains("greeting", result);
            Assert.Contains("farewell", result); // Alice has a farewell version
        }
    }
}
