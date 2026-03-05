namespace Prompt.Tests
{
    using System.Text.Json;
    using Xunit;
    using Prompt;

    public class PromptDocGeneratorTests
    {
        private readonly PromptDocGenerator _generator = new();

        // ===== ExtractVariables =====

        [Fact]
        public void ExtractVariables_EmptyPrompt_ReturnsEmpty()
        {
            var result = _generator.ExtractVariables("");
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractVariables_NullPrompt_ReturnsEmpty()
        {
            var result = _generator.ExtractVariables(null!);
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractVariables_NoVariables_ReturnsEmpty()
        {
            var result = _generator.ExtractVariables("Just a plain prompt with no placeholders.");
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractVariables_SingleVariable_Extracted()
        {
            var result = _generator.ExtractVariables("Hello {{name}}!");
            Assert.Single(result);
            Assert.Equal("name", result[0].Name);
            Assert.Equal(1, result[0].Occurrences);
            Assert.True(result[0].Required);
        }

        [Fact]
        public void ExtractVariables_MultipleVariables_AllExtracted()
        {
            var result = _generator.ExtractVariables("You are a {{role}}. Help with {{topic}} about {{subject}}.");
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void ExtractVariables_DuplicateVariables_CountsOccurrences()
        {
            var result = _generator.ExtractVariables("{{name}} said hello. {{name}} waved.");
            Assert.Single(result);
            Assert.Equal(2, result[0].Occurrences);
        }

        [Fact]
        public void ExtractVariables_WithDefaults_MarksNotRequired()
        {
            var defaults = new Dictionary<string, string> { ["role"] = "assistant" };
            var result = _generator.ExtractVariables("You are a {{role}}. Help with {{topic}}.", defaults);

            var role = result.First(v => v.Name == "role");
            var topic = result.First(v => v.Name == "topic");
            Assert.False(role.Required);
            Assert.True(role.HasDefault);
            Assert.Equal("assistant", role.DefaultValue);
            Assert.True(topic.Required);
        }

        [Fact]
        public void ExtractVariables_SortedAlphabetically()
        {
            var result = _generator.ExtractVariables("{{zebra}} {{alpha}} {{middle}}");
            Assert.Equal("alpha", result[0].Name);
            Assert.Equal("middle", result[1].Name);
            Assert.Equal("zebra", result[2].Name);
        }

        [Fact]
        public void ExtractVariables_WithDescriptions_Applied()
        {
            var options = new DocGeneratorOptions
            {
                VariableDescriptions = new Dictionary<string, string> { ["name"] = "User's display name" }
            };
            var gen = new PromptDocGenerator(options);
            var result = gen.ExtractVariables("Hello {{name}}!");
            Assert.Equal("User's display name", result[0].Description);
        }

        // ===== ExtractSections =====

        [Fact]
        public void ExtractSections_EmptyPrompt_ReturnsEmpty()
        {
            var result = _generator.ExtractSections("");
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractSections_NoHeadings_ReturnsSingleRoot()
        {
            var result = _generator.ExtractSections("Just plain text.\nMore text.");
            Assert.Single(result);
            Assert.Equal("(root)", result[0].Heading);
            Assert.Equal(0, result[0].Level);
        }

        [Fact]
        public void ExtractSections_SingleHeading_OneSection()
        {
            var result = _generator.ExtractSections("# Instructions\nDo this thing.");
            Assert.Single(result);
            Assert.Equal("Instructions", result[0].Heading);
            Assert.Equal(1, result[0].Level);
        }

        [Fact]
        public void ExtractSections_MultipleHeadings_MultipleSections()
        {
            var prompt = "# Role\nYou are helpful.\n## Tasks\nDo stuff.\n## Rules\nBe safe.";
            var result = _generator.ExtractSections(prompt);
            Assert.Equal(3, result.Count);
            Assert.Equal("Role", result[0].Heading);
            Assert.Equal("Tasks", result[1].Heading);
            Assert.Equal("Rules", result[2].Heading);
        }

        [Fact]
        public void ExtractSections_PreambleBeforeFirstHeading_Included()
        {
            var prompt = "Some preamble text.\n# Main Section\nContent here.";
            var result = _generator.ExtractSections(prompt);
            Assert.Equal(2, result.Count);
            Assert.Equal("(preamble)", result[0].Heading);
            Assert.Equal("Main Section", result[1].Heading);
        }

        [Fact]
        public void ExtractSections_CountsWordsAndVariables()
        {
            var result = _generator.ExtractSections("# Section\nHello {{name}} world.");
            Assert.Equal(3, result[0].WordCount);
            Assert.Equal(1, result[0].VariableCount);
        }

        [Fact]
        public void ExtractSections_HeadingLevels_Preserved()
        {
            var prompt = "# H1\nA\n## H2\nB\n### H3\nC";
            var result = _generator.ExtractSections(prompt);
            Assert.Equal(1, result[0].Level);
            Assert.Equal(2, result[1].Level);
            Assert.Equal(3, result[2].Level);
        }

        // ===== ExtractMetadata =====

        [Fact]
        public void ExtractMetadata_EmptyPrompt_ReturnsDefaults()
        {
            var meta = _generator.ExtractMetadata("");
            Assert.Equal("", meta.Title);
            Assert.Empty(meta.Tags);
        }

        [Fact]
        public void ExtractMetadata_AllFields_Extracted()
        {
            var prompt = "@title: My Prompt\n@description: Does stuff\n@author: Alice\n@version: 1.0\n@model: gpt-4\n@category: coding\n@tags: ai, helper\nActual prompt here.";
            var meta = _generator.ExtractMetadata(prompt);
            Assert.Equal("My Prompt", meta.Title);
            Assert.Equal("Does stuff", meta.Description);
            Assert.Equal("Alice", meta.Author);
            Assert.Equal("1.0", meta.Version);
            Assert.Equal("gpt-4", meta.Model);
            Assert.Equal("coding", meta.Category);
            Assert.Equal(2, meta.Tags.Count);
            Assert.Contains("ai", meta.Tags);
            Assert.Contains("helper", meta.Tags);
        }

        [Fact]
        public void ExtractMetadata_ShortAliases_Work()
        {
            var prompt = "@desc: Short desc\n@ver: 2.0\n@cat: tools\n@tag: one, two";
            var meta = _generator.ExtractMetadata(prompt);
            Assert.Equal("Short desc", meta.Description);
            Assert.Equal("2.0", meta.Version);
            Assert.Equal("tools", meta.Category);
            Assert.Equal(2, meta.Tags.Count);
        }

        [Fact]
        public void ExtractMetadata_MultipleTags_Accumulated()
        {
            var prompt = "@tags: a, b\n@tags: c";
            var meta = _generator.ExtractMetadata(prompt);
            Assert.Equal(3, meta.Tags.Count);
        }

        // ===== RateComplexity =====

        [Fact]
        public void RateComplexity_Empty_Simple()
        {
            Assert.Equal("simple", _generator.RateComplexity(""));
        }

        [Fact]
        public void RateComplexity_ShortPlainPrompt_Simple()
        {
            Assert.Equal("simple", _generator.RateComplexity("Do this thing."));
        }

        [Fact]
        public void RateComplexity_WithVariablesAndSections_Moderate()
        {
            var prompt = "# Section\nUse {{var1}} and {{var2}}.\n## Another\nMore text {{var3}}.";
            var result = _generator.RateComplexity(prompt);
            Assert.True(result == "moderate" || result == "complex");
        }

        [Fact]
        public void RateComplexity_WithConditionalsAndLoops_ComplexOrAdvanced()
        {
            var prompt = "{{#if admin}}Admin mode{{/if}} {{#each items}}Item{{/each}} {{#if debug}}Debug{{/if}} {{#each logs}}Log{{/each}}";
            var result = _generator.RateComplexity(prompt);
            Assert.True(result == "complex" || result == "advanced");
        }

        [Fact]
        public void RateComplexity_LongPrompt_HigherComplexity()
        {
            var prompt = string.Join(" ", Enumerable.Range(0, 600).Select(i => "word"));
            var result = _generator.RateComplexity(prompt);
            Assert.NotEqual("simple", result);
        }

        // ===== GenerateUsageExample =====

        [Fact]
        public void GenerateUsageExample_Empty_ReturnsEmpty()
        {
            Assert.Equal("", _generator.GenerateUsageExample(""));
        }

        [Fact]
        public void GenerateUsageExample_NoVariables_DirectUse()
        {
            var result = _generator.GenerateUsageExample("Plain prompt.");
            Assert.Contains("No variables", result);
        }

        [Fact]
        public void GenerateUsageExample_WithVariables_ContainsTemplate()
        {
            var result = _generator.GenerateUsageExample("Hello {{name}}!");
            Assert.Contains("PromptTemplate", result);
            Assert.Contains("name", result);
        }

        [Fact]
        public void GenerateUsageExample_WithDefaults_ShowsDefaults()
        {
            var defaults = new Dictionary<string, string> { ["role"] = "helper" };
            var result = _generator.GenerateUsageExample("You are {{role}}. Do {{task}}.", defaults);
            Assert.Contains("helper", result);
            Assert.Contains("task", result);
        }

        // ===== GenerateDoc =====

        [Fact]
        public void GenerateDoc_NullPrompt_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _generator.GenerateDoc(null!));
        }

        [Fact]
        public void GenerateDoc_BasicPrompt_PopulatesAllFields()
        {
            var doc = _generator.GenerateDoc("Hello {{name}}! Welcome to {{place}}.");
            Assert.Equal(2, doc.Variables.Count);
            Assert.True(doc.WordCount > 0);
            Assert.True(doc.CharCount > 0);
            Assert.True(doc.LineCount > 0);
            Assert.True(doc.EstimatedTokens > 0);
            Assert.NotEmpty(doc.Complexity);
            Assert.NotEmpty(doc.UsageExample);
        }

        [Fact]
        public void GenerateDoc_WithMetadata_UsesProvided()
        {
            var meta = new DocMetadata { Title = "My Prompt", Author = "Bob" };
            var doc = _generator.GenerateDoc("Plain prompt.", meta);
            Assert.Equal("My Prompt", doc.Metadata.Title);
            Assert.Equal("Bob", doc.Metadata.Author);
        }

        [Fact]
        public void GenerateDoc_ExtractsMetadataFromText()
        {
            var doc = _generator.GenerateDoc("@title: Auto Title\n@author: Auto Author\nActual prompt.");
            Assert.Equal("Auto Title", doc.Metadata.Title);
            Assert.Equal("Auto Author", doc.Metadata.Author);
        }

        [Fact]
        public void GenerateDoc_DisabledOptions_OmitsFields()
        {
            var options = new DocGeneratorOptions
            {
                IncludeUsageExamples = false,
                IncludeTokenEstimates = false,
                IncludeComplexityRating = false
            };
            var gen = new PromptDocGenerator(options);
            var doc = gen.GenerateDoc("Hello {{name}}!");
            Assert.Equal(0, doc.EstimatedTokens);
            Assert.Equal("", doc.Complexity);
            Assert.Equal("", doc.UsageExample);
        }

        // ===== GenerateCatalog =====

        [Fact]
        public void GenerateCatalog_Empty_ReturnsEmptyCatalog()
        {
            var catalog = _generator.GenerateCatalog(Array.Empty<(string, DocMetadata?, Dictionary<string, string>?)>());
            Assert.Empty(catalog.Prompts);
            Assert.Equal(0, catalog.TotalVariables);
        }

        [Fact]
        public void GenerateCatalog_MultiplePrompts_AllDocumented()
        {
            var prompts = new[]
            {
                ("Hello {{name}}!", (DocMetadata?)new DocMetadata { Title = "Greeting", Category = "social" }, (Dictionary<string, string>?)null),
                ("Summarize {{text}} for {{audience}}.", (DocMetadata?)new DocMetadata { Title = "Summary", Category = "work" }, (Dictionary<string, string>?)null)
            };
            var catalog = _generator.GenerateCatalog(prompts);
            Assert.Equal(2, catalog.Prompts.Count);
            Assert.Equal(3, catalog.TotalVariables);
        }

        [Fact]
        public void GenerateCatalog_SharedVariables_Detected()
        {
            var prompts = new[]
            {
                ("Hello {{name}}!", (DocMetadata?)null, (Dictionary<string, string>?)null),
                ("Goodbye {{name}}!", (DocMetadata?)null, (Dictionary<string, string>?)null)
            };
            var catalog = _generator.GenerateCatalog(prompts);
            Assert.Contains("name", catalog.SharedVariables);
        }

        [Fact]
        public void GenerateCatalog_CategoryBreakdown_Counted()
        {
            var prompts = new[]
            {
                ("A", (DocMetadata?)new DocMetadata { Category = "coding" }, (Dictionary<string, string>?)null),
                ("B", (DocMetadata?)new DocMetadata { Category = "coding" }, (Dictionary<string, string>?)null),
                ("C", (DocMetadata?)new DocMetadata { Category = "writing" }, (Dictionary<string, string>?)null)
            };
            var catalog = _generator.GenerateCatalog(prompts);
            Assert.Equal(2, catalog.CategoryBreakdown["coding"]);
            Assert.Equal(1, catalog.CategoryBreakdown["writing"]);
        }

        [Fact]
        public void GenerateCatalog_CustomTitle_Used()
        {
            var options = new DocGeneratorOptions { CatalogTitle = "My Prompts" };
            var gen = new PromptDocGenerator(options);
            var catalog = gen.GenerateCatalog(Array.Empty<(string, DocMetadata?, Dictionary<string, string>?)>());
            Assert.Equal("My Prompts", catalog.Title);
        }

        // ===== ToMarkdown =====

        [Fact]
        public void ToMarkdown_BasicDoc_ContainsExpectedSections()
        {
            var doc = _generator.GenerateDoc("Hello {{name}}!");
            var md = _generator.ToMarkdown(doc);
            Assert.Contains("# Prompt Documentation", md);
            Assert.Contains("## Statistics", md);
            Assert.Contains("## Variables", md);
            Assert.Contains("`name`", md);
            Assert.Contains("## Usage Example", md);
            Assert.Contains("## Prompt Text", md);
        }

        [Fact]
        public void ToMarkdown_WithTitle_UsesCustomTitle()
        {
            var doc = _generator.GenerateDoc("Test", new DocMetadata { Title = "My Great Prompt" });
            var md = _generator.ToMarkdown(doc);
            Assert.Contains("# My Great Prompt", md);
        }

        [Fact]
        public void ToMarkdown_WithMetadata_RendersMetaItems()
        {
            var meta = new DocMetadata
            {
                Title = "T",
                Author = "Alice",
                Version = "2.0",
                Model = "gpt-4",
                Category = "coding",
                Tags = new List<string> { "ai", "dev" }
            };
            var doc = _generator.GenerateDoc("Text", meta);
            var md = _generator.ToMarkdown(doc);
            Assert.Contains("**Author:** Alice", md);
            Assert.Contains("**Version:** 2.0", md);
            Assert.Contains("**Model:** gpt-4", md);
            Assert.Contains("ai, dev", md);
        }

        [Fact]
        public void ToMarkdown_WithDescription_RendersBlockquote()
        {
            var doc = _generator.GenerateDoc("Text", new DocMetadata { Description = "A useful prompt" });
            var md = _generator.ToMarkdown(doc);
            Assert.Contains("> A useful prompt", md);
        }

        [Fact]
        public void ToMarkdown_WithSections_RendersStructure()
        {
            var doc = _generator.GenerateDoc("# Role\nAssistant.\n## Rules\nBe nice.");
            var md = _generator.ToMarkdown(doc);
            Assert.Contains("## Structure", md);
            Assert.Contains("**Role**", md);
            Assert.Contains("**Rules**", md);
        }

        [Fact]
        public void ToMarkdown_NoVariables_NoUsageSection()
        {
            var doc = _generator.GenerateDoc("Plain text only.");
            var md = _generator.ToMarkdown(doc);
            Assert.DoesNotContain("## Usage Example", md);
        }

        // ===== CatalogToMarkdown =====

        [Fact]
        public void CatalogToMarkdown_EmptyCatalog_HasTitleAndSummary()
        {
            var catalog = _generator.GenerateCatalog(Array.Empty<(string, DocMetadata?, Dictionary<string, string>?)>());
            var md = _generator.CatalogToMarkdown(catalog);
            Assert.Contains("# Prompt Catalog", md);
            Assert.Contains("**Total prompts:** 0", md);
        }

        [Fact]
        public void CatalogToMarkdown_WithPrompts_HasTOCAndDocs()
        {
            var prompts = new[]
            {
                ("Hello {{name}}!", (DocMetadata?)new DocMetadata { Title = "Greeting" }, (Dictionary<string, string>?)null),
                ("Bye {{name}}!", (DocMetadata?)new DocMetadata { Title = "Farewell" }, (Dictionary<string, string>?)null)
            };
            var catalog = _generator.GenerateCatalog(prompts);
            var md = _generator.CatalogToMarkdown(catalog);
            Assert.Contains("## Table of Contents", md);
            Assert.Contains("Greeting", md);
            Assert.Contains("Farewell", md);
            Assert.Contains("---", md);
        }

        [Fact]
        public void CatalogToMarkdown_SharedVariables_Listed()
        {
            var prompts = new[]
            {
                ("{{name}} hi", (DocMetadata?)null, (Dictionary<string, string>?)null),
                ("{{name}} bye", (DocMetadata?)null, (Dictionary<string, string>?)null)
            };
            var catalog = _generator.GenerateCatalog(prompts);
            var md = _generator.CatalogToMarkdown(catalog);
            Assert.Contains("`name`", md);
        }

        // ===== ComparePrompts =====

        [Fact]
        public void ComparePrompts_SamePrompt_MinimalDifferences()
        {
            var doc = _generator.GenerateDoc("Hello {{name}}!");
            var result = _generator.ComparePrompts(doc, doc);
            Assert.Contains("Comparison", result);
            Assert.DoesNotContain("Variable Differences", result);
        }

        [Fact]
        public void ComparePrompts_DifferentVariables_ShowsDiff()
        {
            var docA = _generator.GenerateDoc("Hello {{name}}!");
            var docB = _generator.GenerateDoc("Hello {{user}} in {{city}}!");
            var result = _generator.ComparePrompts(docA, docB);
            Assert.Contains("Variable Differences", result);
            Assert.Contains("`name`", result);
            Assert.Contains("`user`", result);
        }

        [Fact]
        public void ComparePrompts_DifferentComplexity_Noted()
        {
            var docA = _generator.GenerateDoc("Simple.");
            var longPrompt = string.Join("\n", Enumerable.Range(0, 20).Select(i =>
                $"## Section {i}\n{{{{var{i}}}}} content {{{{#if cond{i}}}}}yes{{{{/if}}}}"));
            var docB = _generator.GenerateDoc(longPrompt);
            var result = _generator.ComparePrompts(docA, docB);
            Assert.Contains("Complexity", result);
        }

        [Fact]
        public void ComparePrompts_CustomTitles_UsedInHeader()
        {
            var docA = _generator.GenerateDoc("A", new DocMetadata { Title = "First" });
            var docB = _generator.GenerateDoc("B", new DocMetadata { Title = "Second" });
            var result = _generator.ComparePrompts(docA, docB);
            Assert.Contains("First", result);
            Assert.Contains("Second", result);
        }

        // ===== Serialization =====

        [Fact]
        public void ToJson_ValidDoc_ValidJson()
        {
            var doc = _generator.GenerateDoc("Hello {{name}}!");
            var json = _generator.ToJson(doc);
            var parsed = JsonSerializer.Deserialize<PromptDoc>(json);
            Assert.NotNull(parsed);
            Assert.Equal(doc.WordCount, parsed!.WordCount);
        }

        [Fact]
        public void CatalogToJson_ValidCatalog_ValidJson()
        {
            var prompts = new[]
            {
                ("Hello {{name}}!", (DocMetadata?)null, (Dictionary<string, string>?)null)
            };
            var catalog = _generator.GenerateCatalog(prompts);
            var json = _generator.CatalogToJson(catalog);
            Assert.Contains("\"prompts\"", json);
            Assert.Contains("\"sharedVariables\"", json);
        }

        // ===== Edge cases =====

        [Fact]
        public void ExtractVariables_NestedBraces_NotMatched()
        {
            // {{{name}}} should still find {{name}}
            var result = _generator.ExtractVariables("{{{name}}}");
            Assert.Single(result);
            Assert.Equal("name", result[0].Name);
        }

        [Fact]
        public void ExtractSections_OnlyPreamble_HasPreambleSection()
        {
            var result = _generator.ExtractSections("Preamble.\n# Heading\nBody.");
            Assert.Equal(2, result.Count);
            Assert.Equal("(preamble)", result[0].Heading);
        }

        [Fact]
        public void GenerateDoc_VeryLongPrompt_HandledGracefully()
        {
            var longPrompt = new string('x', 100000);
            var doc = _generator.GenerateDoc(longPrompt);
            Assert.Equal(100000, doc.CharCount);
            Assert.True(doc.EstimatedTokens > 0);
        }

        [Fact]
        public void GenerateDoc_MultilineVariables_AllCounted()
        {
            var prompt = "Line1: {{a}}\nLine2: {{b}}\nLine3: {{a}} {{c}}";
            var doc = _generator.GenerateDoc(prompt);
            Assert.Equal(3, doc.Variables.Count); // a, b, c
            var varA = doc.Variables.First(v => v.Name == "a");
            Assert.Equal(2, varA.Occurrences);
        }

        [Fact]
        public void GenerateUsageExample_ManyVariables_AllIncluded()
        {
            var prompt = string.Join(" ", Enumerable.Range(0, 10).Select(i => $"{{{{v{i}}}}}"));
            var result = _generator.GenerateUsageExample(prompt);
            for (int i = 0; i < 10; i++)
                Assert.Contains($"v{i}", result);
        }

        [Fact]
        public void RateComplexity_ManyVariablesOnly_ModerateOrHigher()
        {
            var prompt = string.Join(" ", Enumerable.Range(0, 15).Select(i => $"{{{{v{i}}}}}"));
            var result = _generator.RateComplexity(prompt);
            Assert.True(result == "moderate" || result == "complex" || result == "advanced");
        }

    }
}

