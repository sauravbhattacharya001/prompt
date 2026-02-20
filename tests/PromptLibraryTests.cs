namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptEntry"/> and <see cref="PromptLibrary"/> —
/// construction, CRUD, search, filtering, merge, serialization, and
/// the default library. All tests run without an Azure OpenAI endpoint.
/// </summary>
public class PromptLibraryTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    private string GetTempFile()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        return path;
    }

    // ═══════════════════════════════════════════════════════
    //  PromptEntry Construction
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Entry_Constructor_ValidName_Succeeds()
    {
        var t = new PromptTemplate("Hello {{name}}!");
        var entry = new PromptEntry("greet", t, "A greeting", "social");
        Assert.Equal("greet", entry.Name);
        Assert.Same(t, entry.Template);
        Assert.Equal("A greeting", entry.Description);
        Assert.Equal("social", entry.Category);
    }

    [Fact]
    public void Entry_Constructor_WithTags_StoresTags()
    {
        var entry = new PromptEntry("test", new PromptTemplate("{{x}}"),
            tags: new[] { "alpha", "beta" });
        Assert.Contains("alpha", entry.Tags);
        Assert.Contains("beta", entry.Tags);
    }

    [Fact]
    public void Entry_Constructor_NullName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PromptEntry(null!, new PromptTemplate("{{x}}")));
    }

    [Fact]
    public void Entry_Constructor_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PromptEntry("", new PromptTemplate("{{x}}")));
    }

    [Fact]
    public void Entry_Constructor_InvalidCharsInName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PromptEntry("has spaces", new PromptTemplate("{{x}}")));
    }

    [Fact]
    public void Entry_Constructor_SpecialValidChars_Succeeds()
    {
        // Hyphens, underscores, dots are allowed
        var entry = new PromptEntry("my-prompt_v2.1", new PromptTemplate("{{x}}"));
        Assert.Equal("my-prompt_v2.1", entry.Name);
    }

    [Fact]
    public void Entry_Constructor_NullTemplate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PromptEntry("test", null!));
    }

    [Fact]
    public void Entry_Constructor_SetsTimestamps()
    {
        var before = DateTimeOffset.UtcNow;
        var entry = new PromptEntry("test", new PromptTemplate("{{x}}"));
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(entry.CreatedAt, before, after);
        Assert.InRange(entry.UpdatedAt, before, after);
    }

    [Fact]
    public void Entry_AddTag_AddsTag()
    {
        var entry = new PromptEntry("test", new PromptTemplate("{{x}}"));
        entry.AddTag("new-tag");
        Assert.True(entry.HasTag("new-tag"));
    }

    [Fact]
    public void Entry_AddTag_CaseInsensitive()
    {
        var entry = new PromptEntry("test", new PromptTemplate("{{x}}"));
        entry.AddTag("MyTag");
        Assert.True(entry.HasTag("mytag"));
        Assert.True(entry.HasTag("MYTAG"));
    }

    [Fact]
    public void Entry_AddTag_EmptyTag_Throws()
    {
        var entry = new PromptEntry("test", new PromptTemplate("{{x}}"));
        Assert.Throws<ArgumentException>(() => entry.AddTag(""));
    }

    [Fact]
    public void Entry_RemoveTag_RemovesExisting()
    {
        var entry = new PromptEntry("test", new PromptTemplate("{{x}}"),
            tags: new[] { "alpha" });
        Assert.True(entry.RemoveTag("alpha"));
        Assert.False(entry.HasTag("alpha"));
    }

    [Fact]
    public void Entry_RemoveTag_NonExistent_ReturnsFalse()
    {
        var entry = new PromptEntry("test", new PromptTemplate("{{x}}"));
        Assert.False(entry.RemoveTag("nope"));
    }

    // ═══════════════════════════════════════════════════════
    //  PromptLibrary — CRUD
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Library_New_IsEmpty()
    {
        var lib = new PromptLibrary();
        Assert.Equal(0, lib.Count);
        Assert.Empty(lib.Names);
        Assert.Empty(lib.Entries);
    }

    [Fact]
    public void Library_Add_IncreasesCount()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        Assert.Equal(1, lib.Count);
    }

    [Fact]
    public void Library_Add_DuplicateName_Throws()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        Assert.Throws<ArgumentException>(() =>
            lib.Add("t1", new PromptTemplate("{{y}}")));
    }

    [Fact]
    public void Library_Add_DuplicateName_CaseInsensitive_Throws()
    {
        var lib = new PromptLibrary();
        lib.Add("MyTemplate", new PromptTemplate("{{x}}"));
        Assert.Throws<ArgumentException>(() =>
            lib.Add("mytemplate", new PromptTemplate("{{y}}")));
    }

    [Fact]
    public void Library_Set_CreatesNew()
    {
        var lib = new PromptLibrary();
        var entry = lib.Set("t1", new PromptTemplate("{{x}}"));
        Assert.Equal(1, lib.Count);
        Assert.Equal("t1", entry.Name);
    }

    [Fact]
    public void Library_Set_ReplacesExisting()
    {
        var lib = new PromptLibrary();
        lib.Set("t1", new PromptTemplate("{{x}}"), description: "old");
        lib.Set("t1", new PromptTemplate("{{y}}"), description: "new");
        Assert.Equal(1, lib.Count);
        Assert.Equal("new", lib.Get("t1").Description);
        Assert.Equal("{{y}}", lib.Get("t1").Template.Template);
    }

    [Fact]
    public void Library_Get_ReturnsEntry()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"), description: "test");
        var entry = lib.Get("t1");
        Assert.Equal("test", entry.Description);
    }

    [Fact]
    public void Library_Get_CaseInsensitive()
    {
        var lib = new PromptLibrary();
        lib.Add("MyPrompt", new PromptTemplate("{{x}}"));
        var entry = lib.Get("myprompt");
        Assert.Equal("MyPrompt", entry.Name);
    }

    [Fact]
    public void Library_Get_NotFound_Throws()
    {
        var lib = new PromptLibrary();
        Assert.Throws<KeyNotFoundException>(() => lib.Get("nope"));
    }

    [Fact]
    public void Library_TryGet_Found_ReturnsTrue()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        Assert.True(lib.TryGet("t1", out var entry));
        Assert.NotNull(entry);
    }

    [Fact]
    public void Library_TryGet_NotFound_ReturnsFalse()
    {
        var lib = new PromptLibrary();
        Assert.False(lib.TryGet("nope", out var entry));
        Assert.Null(entry);
    }

    [Fact]
    public void Library_Contains_Existing_ReturnsTrue()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        Assert.True(lib.Contains("t1"));
    }

    [Fact]
    public void Library_Contains_Missing_ReturnsFalse()
    {
        var lib = new PromptLibrary();
        Assert.False(lib.Contains("nope"));
    }

    [Fact]
    public void Library_Remove_Existing_ReturnsTrue()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        Assert.True(lib.Remove("t1"));
        Assert.Equal(0, lib.Count);
    }

    [Fact]
    public void Library_Remove_Missing_ReturnsFalse()
    {
        var lib = new PromptLibrary();
        Assert.False(lib.Remove("nope"));
    }

    [Fact]
    public void Library_Clear_RemovesAll()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        lib.Add("t2", new PromptTemplate("{{y}}"));
        lib.Clear();
        Assert.Equal(0, lib.Count);
    }

    [Fact]
    public void Library_Update_ChangesDescription()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"), description: "old");
        lib.Update("t1", description: "new");
        Assert.Equal("new", lib.Get("t1").Description);
    }

    [Fact]
    public void Library_Update_ChangesCategory()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"), category: "cat1");
        lib.Update("t1", category: "cat2");
        Assert.Equal("cat2", lib.Get("t1").Category);
    }

    [Fact]
    public void Library_Update_ChangesTemplate()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        lib.Update("t1", template: new PromptTemplate("{{y}}"));
        Assert.Equal("{{y}}", lib.Get("t1").Template.Template);
    }

    [Fact]
    public void Library_Update_ChangesTags()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"), tags: new[] { "old" });
        lib.Update("t1", tags: new[] { "new1", "new2" });
        Assert.False(lib.Get("t1").HasTag("old"));
        Assert.True(lib.Get("t1").HasTag("new1"));
        Assert.True(lib.Get("t1").HasTag("new2"));
    }

    [Fact]
    public void Library_Update_NullParams_PreservesExisting()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"),
            description: "desc", category: "cat", tags: new[] { "tag" });
        lib.Update("t1"); // all null
        var entry = lib.Get("t1");
        Assert.Equal("desc", entry.Description);
        Assert.Equal("cat", entry.Category);
        Assert.True(entry.HasTag("tag"));
    }

    [Fact]
    public void Library_Update_NotFound_Throws()
    {
        var lib = new PromptLibrary();
        Assert.Throws<KeyNotFoundException>(() =>
            lib.Update("nope", description: "new"));
    }

    [Fact]
    public void Library_Names_SortedAlphabetically()
    {
        var lib = new PromptLibrary();
        lib.Add("charlie", new PromptTemplate("{{x}}"));
        lib.Add("alpha", new PromptTemplate("{{x}}"));
        lib.Add("bravo", new PromptTemplate("{{x}}"));
        Assert.Equal(new[] { "alpha", "bravo", "charlie" }, lib.Names);
    }

    // ═══════════════════════════════════════════════════════
    //  Search & Filter
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Library_FindByCategory_ReturnsMatches()
    {
        var lib = CreateSampleLibrary();
        var results = lib.FindByCategory("coding");
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("coding", r.Category));
    }

    [Fact]
    public void Library_FindByCategory_CaseInsensitive()
    {
        var lib = CreateSampleLibrary();
        var results = lib.FindByCategory("CODING");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Library_FindByCategory_NoMatch_ReturnsEmpty()
    {
        var lib = CreateSampleLibrary();
        Assert.Empty(lib.FindByCategory("nonexistent"));
    }

    [Fact]
    public void Library_FindByCategory_EmptyString_ReturnsEmpty()
    {
        var lib = CreateSampleLibrary();
        Assert.Empty(lib.FindByCategory(""));
    }

    [Fact]
    public void Library_FindByTag_ReturnsMatches()
    {
        var lib = CreateSampleLibrary();
        var results = lib.FindByTag("quality");
        Assert.True(results.Count >= 1);
        Assert.All(results, r => Assert.True(r.HasTag("quality")));
    }

    [Fact]
    public void Library_FindByTag_CaseInsensitive()
    {
        var lib = CreateSampleLibrary();
        var results = lib.FindByTag("QUALITY");
        Assert.True(results.Count >= 1);
    }

    [Fact]
    public void Library_FindByTag_NoMatch_ReturnsEmpty()
    {
        var lib = CreateSampleLibrary();
        Assert.Empty(lib.FindByTag("nonexistent"));
    }

    [Fact]
    public void Library_Search_ByName()
    {
        var lib = CreateSampleLibrary();
        var results = lib.Search("review");
        Assert.Contains(results, r => r.Name == "code-review");
    }

    [Fact]
    public void Library_Search_ByDescription()
    {
        var lib = CreateSampleLibrary();
        var results = lib.Search("improvements");
        Assert.Contains(results, r => r.Name == "code-review");
    }

    [Fact]
    public void Library_Search_ByCategory()
    {
        var lib = CreateSampleLibrary();
        var results = lib.Search("writing");
        Assert.Contains(results, r => r.Category == "writing");
    }

    [Fact]
    public void Library_Search_ByTag()
    {
        var lib = CreateSampleLibrary();
        var results = lib.Search("quality");
        Assert.True(results.Count >= 1);
    }

    [Fact]
    public void Library_Search_ByTemplateContent()
    {
        var lib = CreateSampleLibrary();
        var results = lib.Search("Summarize");
        Assert.Contains(results, r => r.Name == "summarize");
    }

    [Fact]
    public void Library_Search_EmptyQuery_ReturnsAll()
    {
        var lib = CreateSampleLibrary();
        var results = lib.Search("");
        Assert.Equal(lib.Count, results.Count);
    }

    [Fact]
    public void Library_Search_NoMatch_ReturnsEmpty()
    {
        var lib = CreateSampleLibrary();
        Assert.Empty(lib.Search("zzzznonexistentzzzz"));
    }

    [Fact]
    public void Library_GetCategories_ReturnsDistinct()
    {
        var lib = CreateSampleLibrary();
        var categories = lib.GetCategories();
        Assert.Contains("coding", categories);
        Assert.Contains("writing", categories);
        // No duplicates
        Assert.Equal(categories.Count, categories.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Library_GetAllTags_ReturnsDistinct()
    {
        var lib = CreateSampleLibrary();
        var tags = lib.GetAllTags();
        Assert.True(tags.Count > 0);
        Assert.Equal(tags.Count, tags.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // ═══════════════════════════════════════════════════════
    //  Merge
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Library_Merge_AddsNewEntries()
    {
        var lib1 = new PromptLibrary();
        lib1.Add("t1", new PromptTemplate("{{x}}"));

        var lib2 = new PromptLibrary();
        lib2.Add("t2", new PromptTemplate("{{y}}"));

        int count = lib1.Merge(lib2);
        Assert.Equal(1, count);
        Assert.Equal(2, lib1.Count);
        Assert.True(lib1.Contains("t2"));
    }

    [Fact]
    public void Library_Merge_SkipsConflicts_WhenOverwriteFalse()
    {
        var lib1 = new PromptLibrary();
        lib1.Add("t1", new PromptTemplate("{{x}}"), description: "original");

        var lib2 = new PromptLibrary();
        lib2.Add("t1", new PromptTemplate("{{y}}"), description: "replacement");

        int count = lib1.Merge(lib2, overwrite: false);
        Assert.Equal(0, count);
        Assert.Equal("original", lib1.Get("t1").Description);
    }

    [Fact]
    public void Library_Merge_OverwritesConflicts_WhenOverwriteTrue()
    {
        var lib1 = new PromptLibrary();
        lib1.Add("t1", new PromptTemplate("{{x}}"), description: "original");

        var lib2 = new PromptLibrary();
        lib2.Add("t1", new PromptTemplate("{{y}}"), description: "replacement");

        int count = lib1.Merge(lib2, overwrite: true);
        Assert.Equal(1, count);
        Assert.Equal("replacement", lib1.Get("t1").Description);
    }

    [Fact]
    public void Library_Merge_NullLibrary_Throws()
    {
        var lib = new PromptLibrary();
        Assert.Throws<ArgumentNullException>(() => lib.Merge(null!));
    }

    // ═══════════════════════════════════════════════════════
    //  JSON Serialization
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Library_ToJson_FromJson_RoundTrip()
    {
        var lib = CreateSampleLibrary();
        string json = lib.ToJson();
        var restored = PromptLibrary.FromJson(json);

        Assert.Equal(lib.Count, restored.Count);
        foreach (var name in lib.Names)
        {
            var original = lib.Get(name);
            var copy = restored.Get(name);
            Assert.Equal(original.Name, copy.Name);
            Assert.Equal(original.Template.Template, copy.Template.Template);
            Assert.Equal(original.Description, copy.Description);
            Assert.Equal(original.Category, copy.Category);
            Assert.Equal(original.Tags.Count, copy.Tags.Count);
        }
    }

    [Fact]
    public void Library_ToJson_ContainsVersion()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        string json = lib.ToJson();
        Assert.Contains("\"version\"", json);
    }

    [Fact]
    public void Library_FromJson_NullString_Throws()
    {
        Assert.Throws<ArgumentException>(() => PromptLibrary.FromJson(null!));
    }

    [Fact]
    public void Library_FromJson_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => PromptLibrary.FromJson(""));
    }

    [Fact]
    public void Library_FromJson_MissingEntries_ReturnsEmpty()
    {
        // When entries key is missing, the deserializer creates a default
        // empty list — the library loads with zero entries.
        var lib = PromptLibrary.FromJson("{\"version\": 1}");
        Assert.Equal(0, lib.Count);
    }

    [Fact]
    public void Library_FromJson_NullEntries_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PromptLibrary.FromJson("{\"version\": 1, \"entries\": null}"));
    }

    [Fact]
    public void Library_FromJson_SkipsInvalidEntries()
    {
        string json = @"{
            ""version"": 1,
            ""entries"": [
                { ""name"": ""valid"", ""template"": ""{{x}}"" },
                { ""name"": """", ""template"": ""{{y}}"" },
                { ""name"": ""notemplate"", ""template"": """" }
            ]
        }";
        var lib = PromptLibrary.FromJson(json);
        Assert.Equal(1, lib.Count);
        Assert.True(lib.Contains("valid"));
    }

    [Fact]
    public void Library_FromJson_RestoresDefaults()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}} {{y}}",
            new Dictionary<string, string> { ["x"] = "hello" }));
        string json = lib.ToJson();
        var restored = PromptLibrary.FromJson(json);
        Assert.Equal("hello", restored.Get("t1").Template.Defaults["x"]);
    }

    [Fact]
    public void Library_FromJson_RestoresTimestamps()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        var originalCreated = lib.Get("t1").CreatedAt;
        string json = lib.ToJson();

        // small delay to ensure time moves
        var restored = PromptLibrary.FromJson(json);
        Assert.Equal(originalCreated, restored.Get("t1").CreatedAt);
    }

    // ═══════════════════════════════════════════════════════
    //  File Serialization
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Library_SaveToFile_LoadFromFile_RoundTrip()
    {
        var lib = CreateSampleLibrary();
        var path = GetTempFile();

        await lib.SaveToFileAsync(path);
        var restored = await PromptLibrary.LoadFromFileAsync(path);

        Assert.Equal(lib.Count, restored.Count);
    }

    [Fact]
    public async Task Library_SaveToFile_NullPath_Throws()
    {
        var lib = new PromptLibrary();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            lib.SaveToFileAsync(null!));
    }

    [Fact]
    public async Task Library_LoadFromFile_NullPath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PromptLibrary.LoadFromFileAsync(null!));
    }

    [Fact]
    public async Task Library_LoadFromFile_NotFound_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            PromptLibrary.LoadFromFileAsync("/nonexistent/path.json"));
    }

    // ═══════════════════════════════════════════════════════
    //  Default Library
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Library_CreateDefault_HasEntries()
    {
        var lib = PromptLibrary.CreateDefault();
        Assert.True(lib.Count >= 8);
    }

    [Fact]
    public void Library_CreateDefault_HasCodeReview()
    {
        var lib = PromptLibrary.CreateDefault();
        Assert.True(lib.Contains("code-review"));
        var entry = lib.Get("code-review");
        Assert.Equal("coding", entry.Category);
        Assert.True(entry.HasTag("review"));
    }

    [Fact]
    public void Library_CreateDefault_HasSummarize()
    {
        var lib = PromptLibrary.CreateDefault();
        var entry = lib.Get("summarize");
        Assert.Equal("writing", entry.Category);
        // Has defaults
        Assert.Equal("concise", entry.Template.Defaults["style"]);
    }

    [Fact]
    public void Library_CreateDefault_HasTranslate()
    {
        var lib = PromptLibrary.CreateDefault();
        var entry = lib.Get("translate");
        Assert.Equal("writing", entry.Category);
    }

    [Fact]
    public void Library_CreateDefault_HasExtractJson()
    {
        var lib = PromptLibrary.CreateDefault();
        var entry = lib.Get("extract-json");
        Assert.Equal("data", entry.Category);
    }

    [Fact]
    public void Library_CreateDefault_HasDebugError()
    {
        var lib = PromptLibrary.CreateDefault();
        var entry = lib.Get("debug-error");
        Assert.Equal("coding", entry.Category);
        Assert.True(entry.HasTag("debug"));
    }

    [Fact]
    public void Library_CreateDefault_HasGenerateTests()
    {
        var lib = PromptLibrary.CreateDefault();
        var entry = lib.Get("generate-tests");
        Assert.True(entry.HasTag("testing"));
    }

    [Fact]
    public void Library_CreateDefault_AllTemplatesRender()
    {
        var lib = PromptLibrary.CreateDefault();
        foreach (var entry in lib.Entries)
        {
            // Should render in non-strict mode without errors
            string rendered = entry.Template.Render(strict: false);
            Assert.False(string.IsNullOrEmpty(rendered));
        }
    }

    [Fact]
    public void Library_CreateDefault_SerializesCorrectly()
    {
        var lib = PromptLibrary.CreateDefault();
        string json = lib.ToJson();
        var restored = PromptLibrary.FromJson(json);
        Assert.Equal(lib.Count, restored.Count);
    }

    // ═══════════════════════════════════════════════════════
    //  Integration — Templates Work End-to-End
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Library_TemplateRendersCorrectly()
    {
        var lib = new PromptLibrary();
        lib.Add("greet", new PromptTemplate("Hello {{name}}!",
            new Dictionary<string, string> { ["name"] = "World" }));

        var entry = lib.Get("greet");
        string rendered = entry.Template.Render();
        Assert.Equal("Hello World!", rendered);
    }

    [Fact]
    public void Library_TemplateRendersWithOverride()
    {
        var lib = new PromptLibrary();
        lib.Add("greet", new PromptTemplate("Hello {{name}}!",
            new Dictionary<string, string> { ["name"] = "World" }));

        string rendered = lib.Get("greet").Template.Render(
            new Dictionary<string, string> { ["name"] = "Alice" });
        Assert.Equal("Hello Alice!", rendered);
    }

    [Fact]
    public void Library_AddWithMetadata_SearchableByAll()
    {
        var lib = new PromptLibrary();
        lib.Add("my-prompt",
            new PromptTemplate("Do {{action}} on {{target}}"),
            description: "A versatile action template",
            category: "utility",
            tags: new[] { "action", "versatile" });

        // All search paths work
        Assert.Single(lib.FindByCategory("utility"));
        Assert.Single(lib.FindByTag("action"));
        Assert.Contains(lib.Search("versatile"), r => r.Name == "my-prompt");
        Assert.Contains(lib.Search("my-prompt"), r => r.Name == "my-prompt");
        Assert.Contains(lib.Search("target"), r => r.Name == "my-prompt");
    }

    // ═══════════════════════════════════════════════════════
    //  Edge Cases
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Library_EmptyLibrary_Serializes()
    {
        var lib = new PromptLibrary();
        string json = lib.ToJson();
        var restored = PromptLibrary.FromJson(json);
        Assert.Equal(0, restored.Count);
    }

    [Fact]
    public void Library_EntryWithNoTags_Serializes()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        string json = lib.ToJson();
        Assert.DoesNotContain("\"tags\"", json); // null tags are omitted
    }

    [Fact]
    public void Library_EntryWithNoDescription_Serializes()
    {
        var lib = new PromptLibrary();
        lib.Add("t1", new PromptTemplate("{{x}}"));
        string json = lib.ToJson();
        Assert.DoesNotContain("\"description\"", json);
    }

    [Fact]
    public void Library_Merge_EmptyIntoEmpty()
    {
        var lib1 = new PromptLibrary();
        var lib2 = new PromptLibrary();
        int count = lib1.Merge(lib2);
        Assert.Equal(0, count);
        Assert.Equal(0, lib1.Count);
    }

    [Fact]
    public void Library_Merge_MultipleEntries()
    {
        var lib1 = new PromptLibrary();
        lib1.Add("t1", new PromptTemplate("{{x}}"));

        var lib2 = new PromptLibrary();
        lib2.Add("t2", new PromptTemplate("{{a}}"));
        lib2.Add("t3", new PromptTemplate("{{b}}"));

        int count = lib1.Merge(lib2);
        Assert.Equal(2, count);
        Assert.Equal(3, lib1.Count);
    }

    [Fact]
    public void Library_GetCategories_Empty_ReturnsEmpty()
    {
        var lib = new PromptLibrary();
        Assert.Empty(lib.GetCategories());
    }

    [Fact]
    public void Library_GetAllTags_Empty_ReturnsEmpty()
    {
        var lib = new PromptLibrary();
        Assert.Empty(lib.GetAllTags());
    }

    [Fact]
    public void Library_Search_NullQuery_ReturnsAll()
    {
        var lib = CreateSampleLibrary();
        var results = lib.Search(null!);
        Assert.Equal(lib.Count, results.Count);
    }

    // ═══════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════

    private static PromptLibrary CreateSampleLibrary()
    {
        var lib = new PromptLibrary();
        lib.Add("code-review",
            new PromptTemplate("Review this {{language}} code:\n{{code}}"),
            description: "Reviews code and suggests improvements",
            category: "coding",
            tags: new[] { "review", "quality" });

        lib.Add("debug",
            new PromptTemplate("Debug this {{language}} error:\n{{error}}"),
            description: "Debugs code errors",
            category: "coding",
            tags: new[] { "debug", "fix" });

        lib.Add("summarize",
            new PromptTemplate("Summarize: {{text}}",
                new Dictionary<string, string> { ["text"] = "" }),
            description: "Summarizes text",
            category: "writing",
            tags: new[] { "summarize" });

        lib.Add("translate",
            new PromptTemplate("Translate to {{language}}: {{text}}"),
            description: "Translates text",
            category: "writing",
            tags: new[] { "translate" });

        return lib;
    }
}
