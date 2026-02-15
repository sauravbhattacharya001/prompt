namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptTemplate"/> — construction, variable
/// extraction, rendering, defaults, composition, and serialization.
/// All tests run without an Azure OpenAI endpoint.
/// </summary>
public class PromptTemplateTests : IDisposable
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

    // ───────────────────── Construction ─────────────────────

    [Fact]
    public void Constructor_WithTemplate_Succeeds()
    {
        var t = new PromptTemplate("Hello {{name}}!");
        Assert.Equal("Hello {{name}}!", t.Template);
    }

    [Fact]
    public void Constructor_WithDefaults_StoresDefaults()
    {
        var defaults = new Dictionary<string, string> { ["name"] = "World" };
        var t = new PromptTemplate("Hello {{name}}!", defaults);
        Assert.Equal("World", t.Defaults["name"]);
    }

    [Fact]
    public void Constructor_NullTemplate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PromptTemplate(null!));
    }

    [Fact]
    public void Constructor_EmptyTemplate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PromptTemplate(""));
    }

    [Fact]
    public void Constructor_WhitespaceTemplate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PromptTemplate("   "));
    }

    [Fact]
    public void Constructor_NullDefaults_UsesEmptyDefaults()
    {
        var t = new PromptTemplate("Hello!", null);
        Assert.Empty(t.Defaults);
    }

    // ───────────────────── GetVariables ─────────────────────

    [Fact]
    public void GetVariables_FindsAllVariables()
    {
        var t = new PromptTemplate("{{a}} and {{b}} and {{c}}");
        var vars = t.GetVariables();
        Assert.Equal(3, vars.Count);
        Assert.Contains("a", vars);
        Assert.Contains("b", vars);
        Assert.Contains("c", vars);
    }

    [Fact]
    public void GetVariables_DeduplicatesSameVariable()
    {
        var t = new PromptTemplate("{{x}} and {{x}} and {{x}}");
        var vars = t.GetVariables();
        Assert.Single(vars);
        Assert.Contains("x", vars);
    }

    [Fact]
    public void GetVariables_NoVariables_ReturnsEmpty()
    {
        var t = new PromptTemplate("No variables here.");
        Assert.Empty(t.GetVariables());
    }

    [Fact]
    public void GetVariables_CaseInsensitiveDedup()
    {
        var t = new PromptTemplate("{{Name}} and {{name}}");
        var vars = t.GetVariables();
        // Both should be captured but deduplicated case-insensitively
        Assert.Single(vars);
    }

    // ───────────────────── GetRequiredVariables ─────────────────────

    [Fact]
    public void GetRequiredVariables_ExcludesDefaults()
    {
        var t = new PromptTemplate(
            "{{role}} does {{task}}",
            new Dictionary<string, string> { ["role"] = "developer" }
        );
        var required = t.GetRequiredVariables();
        Assert.Single(required);
        Assert.Contains("task", required);
        Assert.DoesNotContain("role", required);
    }

    [Fact]
    public void GetRequiredVariables_AllDefaults_ReturnsEmpty()
    {
        var t = new PromptTemplate(
            "{{a}} {{b}}",
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }
        );
        Assert.Empty(t.GetRequiredVariables());
    }

    // ───────────────────── SetDefault / RemoveDefault ─────────────────────

    [Fact]
    public void SetDefault_AddsNewDefault()
    {
        var t = new PromptTemplate("{{x}}");
        t.SetDefault("x", "val");
        Assert.Equal("val", t.Defaults["x"]);
    }

    [Fact]
    public void SetDefault_OverridesExisting()
    {
        var t = new PromptTemplate(
            "{{x}}",
            new Dictionary<string, string> { ["x"] = "old" }
        );
        t.SetDefault("x", "new");
        Assert.Equal("new", t.Defaults["x"]);
    }

    [Fact]
    public void SetDefault_EmptyName_Throws()
    {
        var t = new PromptTemplate("{{x}}");
        Assert.Throws<ArgumentException>(() => t.SetDefault("", "val"));
    }

    [Fact]
    public void RemoveDefault_RemovesExisting()
    {
        var t = new PromptTemplate(
            "{{x}}",
            new Dictionary<string, string> { ["x"] = "val" }
        );
        Assert.True(t.RemoveDefault("x"));
        Assert.DoesNotContain("x", (IDictionary<string, string>)t.Defaults);
    }

    [Fact]
    public void RemoveDefault_NonExistent_ReturnsFalse()
    {
        var t = new PromptTemplate("{{x}}");
        Assert.False(t.RemoveDefault("nonexistent"));
    }

    // ───────────────────── Render ─────────────────────

    [Fact]
    public void Render_ReplacesVariables()
    {
        var t = new PromptTemplate("Hello {{name}}, welcome to {{place}}!");
        var result = t.Render(new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["place"] = "Wonderland"
        });
        Assert.Equal("Hello Alice, welcome to Wonderland!", result);
    }

    [Fact]
    public void Render_UsesDefaults()
    {
        var t = new PromptTemplate(
            "You are a {{role}} assistant.",
            new Dictionary<string, string> { ["role"] = "helpful" }
        );
        var result = t.Render();
        Assert.Equal("You are a helpful assistant.", result);
    }

    [Fact]
    public void Render_VariablesOverrideDefaults()
    {
        var t = new PromptTemplate(
            "You are a {{role}} assistant.",
            new Dictionary<string, string> { ["role"] = "helpful" }
        );
        var result = t.Render(new Dictionary<string, string>
        {
            ["role"] = "strict"
        });
        Assert.Equal("You are a strict assistant.", result);
    }

    [Fact]
    public void Render_MissingVariable_Strict_Throws()
    {
        var t = new PromptTemplate("Hello {{name}}!");
        var ex = Assert.Throws<InvalidOperationException>(
            () => t.Render());
        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public void Render_MissingVariable_NonStrict_LeavesPlaceholder()
    {
        var t = new PromptTemplate("Hello {{name}}!");
        var result = t.Render(strict: false);
        Assert.Equal("Hello {{name}}!", result);
    }

    [Fact]
    public void Render_MultipleMissing_Strict_ListsAll()
    {
        var t = new PromptTemplate("{{a}} {{b}} {{c}}");
        var ex = Assert.Throws<InvalidOperationException>(
            () => t.Render());
        Assert.Contains("a", ex.Message);
        Assert.Contains("b", ex.Message);
        Assert.Contains("c", ex.Message);
    }

    [Fact]
    public void Render_NoVariables_ReturnsOriginal()
    {
        var t = new PromptTemplate("Just plain text.");
        Assert.Equal("Just plain text.", t.Render());
    }

    [Fact]
    public void Render_DuplicateVariable_ReplacesAll()
    {
        var t = new PromptTemplate("{{x}} and {{x}} again");
        var result = t.Render(new Dictionary<string, string> { ["x"] = "hi" });
        Assert.Equal("hi and hi again", result);
    }

    [Fact]
    public void Render_EmptyValue_Allowed()
    {
        var t = new PromptTemplate("before{{x}}after");
        var result = t.Render(new Dictionary<string, string> { ["x"] = "" });
        Assert.Equal("beforeafter", result);
    }

    [Fact]
    public void Render_CaseInsensitiveVariables()
    {
        var t = new PromptTemplate("Hello {{Name}}!");
        var result = t.Render(new Dictionary<string, string> { ["name"] = "Bob" });
        Assert.Equal("Hello Bob!", result);
    }

    [Fact]
    public void Render_PartialDefaults_MixedSources()
    {
        var t = new PromptTemplate(
            "{{greeting}}, {{name}}! You are {{role}}.",
            new Dictionary<string, string>
            {
                ["greeting"] = "Hello",
                ["role"] = "a developer"
            }
        );
        var result = t.Render(new Dictionary<string, string> { ["name"] = "Eve" });
        Assert.Equal("Hello, Eve! You are a developer.", result);
    }

    // ───────────────────── Composition ─────────────────────

    [Fact]
    public void Compose_CombinesTemplates()
    {
        var a = new PromptTemplate("You are {{role}}.");
        var b = new PromptTemplate("Help with {{topic}}.");
        var combined = a.Compose(b);

        var result = combined.Render(new Dictionary<string, string>
        {
            ["role"] = "an expert",
            ["topic"] = "math"
        });
        Assert.Equal("You are an expert.\n\nHelp with math.", result);
    }

    [Fact]
    public void Compose_MergesDefaults()
    {
        var a = new PromptTemplate(
            "{{a}}", new Dictionary<string, string> { ["a"] = "1" });
        var b = new PromptTemplate(
            "{{b}}", new Dictionary<string, string> { ["b"] = "2" });

        var combined = a.Compose(b);
        Assert.Equal("1", combined.Defaults["a"]);
        Assert.Equal("2", combined.Defaults["b"]);
    }

    [Fact]
    public void Compose_OtherDefaultsWin()
    {
        var a = new PromptTemplate(
            "{{x}}", new Dictionary<string, string> { ["x"] = "from_a" });
        var b = new PromptTemplate(
            "{{x}}", new Dictionary<string, string> { ["x"] = "from_b" });

        var combined = a.Compose(b);
        Assert.Equal("from_b", combined.Defaults["x"]);
    }

    [Fact]
    public void Compose_CustomSeparator()
    {
        var a = new PromptTemplate("A");
        var b = new PromptTemplate("B");
        var combined = a.Compose(b, " | ");
        Assert.Equal("A | B", combined.Render());
    }

    [Fact]
    public void Compose_NullOther_Throws()
    {
        var t = new PromptTemplate("test");
        Assert.Throws<ArgumentNullException>(() => t.Compose(null!));
    }

    // ───────────────────── Serialization ─────────────────────

    [Fact]
    public void ToJson_RoundTrip()
    {
        var original = new PromptTemplate(
            "{{role}} helps with {{topic}}",
            new Dictionary<string, string>
            {
                ["role"] = "assistant",
                ["topic"] = "coding"
            }
        );

        string json = original.ToJson();
        var restored = PromptTemplate.FromJson(json);

        Assert.Equal(original.Template, restored.Template);
        Assert.Equal("assistant", restored.Defaults["role"]);
        Assert.Equal("coding", restored.Defaults["topic"]);
    }

    [Fact]
    public void ToJson_NoDefaults_OmitsField()
    {
        var t = new PromptTemplate("Hello {{name}}!");
        string json = t.ToJson();
        Assert.DoesNotContain("defaults", json);
    }

    [Fact]
    public void FromJson_NullJson_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => PromptTemplate.FromJson(null!));
    }

    [Fact]
    public void FromJson_EmptyJson_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => PromptTemplate.FromJson(""));
    }

    [Fact]
    public void FromJson_MissingTemplate_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => PromptTemplate.FromJson("{}"));
    }

    // ───────────────────── File I/O ─────────────────────

    [Fact]
    public async Task SaveToFileAsync_CreatesFile()
    {
        var path = GetTempFile();
        var t = new PromptTemplate(
            "Hello {{name}}!",
            new Dictionary<string, string> { ["name"] = "World" }
        );

        await t.SaveToFileAsync(path);

        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Hello {{name}}!", content);
    }

    [Fact]
    public async Task LoadFromFileAsync_RestoresTemplate()
    {
        var path = GetTempFile();
        var original = new PromptTemplate(
            "You are {{role}}.",
            new Dictionary<string, string> { ["role"] = "helpful" }
        );

        await original.SaveToFileAsync(path);
        var loaded = await PromptTemplate.LoadFromFileAsync(path);

        Assert.Equal(original.Template, loaded.Template);
        Assert.Equal("helpful", loaded.Defaults["role"]);
    }

    [Fact]
    public async Task SaveToFileAsync_EmptyPath_Throws()
    {
        var t = new PromptTemplate("test");
        await Assert.ThrowsAsync<ArgumentException>(
            () => t.SaveToFileAsync(""));
    }

    [Fact]
    public async Task LoadFromFileAsync_MissingFile_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => PromptTemplate.LoadFromFileAsync("nonexistent.json"));
    }

    [Fact]
    public async Task LoadFromFileAsync_EmptyPath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => PromptTemplate.LoadFromFileAsync(""));
    }

    // ───────────────────── ToString ─────────────────────

    [Fact]
    public void ToString_ReturnsTemplate()
    {
        var t = new PromptTemplate("Hello {{name}}!");
        Assert.Equal("Hello {{name}}!", t.ToString());
    }

    // ───────────────────── Edge Cases ─────────────────────

    [Fact]
    public void Render_VariableInVariable_NotRecursive()
    {
        var t = new PromptTemplate("{{x}}");
        var result = t.Render(new Dictionary<string, string>
        {
            ["x"] = "{{y}}"
        });
        // Should NOT try to resolve {{y}} — single-pass replacement
        Assert.Equal("{{y}}", result);
    }

    [Fact]
    public void Render_SpecialCharactersInValue()
    {
        var t = new PromptTemplate("Result: {{data}}");
        var result = t.Render(new Dictionary<string, string>
        {
            ["data"] = "line1\nline2\ttab"
        });
        Assert.Equal("Result: line1\nline2\ttab", result);
    }

    [Fact]
    public void Render_ExtraVariablesIgnored()
    {
        var t = new PromptTemplate("Hello {{name}}!");
        var result = t.Render(new Dictionary<string, string>
        {
            ["name"] = "Bob",
            ["unused"] = "whatever"
        });
        Assert.Equal("Hello Bob!", result);
    }

    [Fact]
    public void GetVariables_UnderscoresAllowed()
    {
        var t = new PromptTemplate("{{my_var}} and {{another_one}}");
        var vars = t.GetVariables();
        Assert.Equal(2, vars.Count);
        Assert.Contains("my_var", vars);
        Assert.Contains("another_one", vars);
    }

    [Fact]
    public void Compose_ThreeTemplates_ChainWorks()
    {
        var a = new PromptTemplate("A={{a}}");
        var b = new PromptTemplate("B={{b}}");
        var c = new PromptTemplate("C={{c}}");

        var combined = a.Compose(b, " ").Compose(c, " ");
        var result = combined.Render(new Dictionary<string, string>
        {
            ["a"] = "1", ["b"] = "2", ["c"] = "3"
        });
        Assert.Equal("A=1 B=2 C=3", result);
    }
}
