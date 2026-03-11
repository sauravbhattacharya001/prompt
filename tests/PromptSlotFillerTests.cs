namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Prompt;

public class PromptSlotFillerTests
{
    // ── DetectSlots ──────────────────────────────────────────

    [Fact]
    public void DetectSlots_EmptyTemplate_ReturnsEmpty()
    {
        var filler = new PromptSlotFiller();
        var slots = filler.DetectSlots("");
        Assert.Empty(slots);
    }

    [Fact]
    public void DetectSlots_NullTemplate_ReturnsEmpty()
    {
        var filler = new PromptSlotFiller();
        var slots = filler.DetectSlots(null!);
        Assert.Empty(slots);
    }

    [Fact]
    public void DetectSlots_NoSlots_ReturnsEmpty()
    {
        var filler = new PromptSlotFiller();
        var slots = filler.DetectSlots("Just plain text with no variables.");
        Assert.Empty(slots);
    }

    [Fact]
    public void DetectSlots_DoubleCurly_FindsSlots()
    {
        var filler = new PromptSlotFiller();
        var slots = filler.DetectSlots("Hello {{name}}, you are a {{role}}.");
        Assert.Equal(2, slots.Count);
        Assert.Equal("name", slots[0].Name);
        Assert.Equal("role", slots[1].Name);
    }

    [Fact]
    public void DetectSlots_WithDefault_ParsesDefault()
    {
        var filler = new PromptSlotFiller();
        var slots = filler.DetectSlots("Role: {{role:assistant}}");
        Assert.Single(slots);
        Assert.Equal("role", slots[0].Name);
        Assert.True(slots[0].HasDefault);
        Assert.Equal("assistant", slots[0].DefaultValue);
        Assert.False(slots[0].IsRequired);
    }

    [Fact]
    public void DetectSlots_DuplicateSlots_DeduplicatesIgnoringCase()
    {
        var filler = new PromptSlotFiller();
        var slots = filler.DetectSlots("{{name}} and {{name}} and {{Name}}");
        Assert.Single(slots);
    }

    [Fact]
    public void DetectSlots_DuplicateSlots_CaseSensitive_KeepsBoth()
    {
        var filler = new PromptSlotFiller().CaseSensitive();
        var slots = filler.DetectSlots("{{name}} and {{Name}}");
        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void DetectSlots_SingleCurly_FindsSlots()
    {
        var filler = new PromptSlotFiller().WithSyntax(PromptSlotFiller.SlotSyntax.SingleCurly);
        var slots = filler.DetectSlots("Hello {name}, role is {role}.");
        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void DetectSlots_DollarSyntax_FindsSlots()
    {
        var filler = new PromptSlotFiller().WithSyntax(PromptSlotFiller.SlotSyntax.Dollar);
        var slots = filler.DetectSlots("Hello $name$, your role is $role$.");
        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void DetectSlots_AutoSyntax_FindsAllFormats()
    {
        var filler = new PromptSlotFiller().WithSyntax(PromptSlotFiller.SlotSyntax.Auto);
        var slots = filler.DetectSlots("{{name}} and {role} and $topic$");
        Assert.Equal(3, slots.Count);
    }

    [Fact]
    public void DetectSlots_RecordsPosition()
    {
        var filler = new PromptSlotFiller();
        var slots = filler.DetectSlots("Hello {{name}}!");
        Assert.Equal(6, slots[0].Position);
    }

    // ── Fill ──────────────────────────────────────────────────

    [Fact]
    public void Fill_EmptyTemplate_ReturnsEmpty()
    {
        var filler = new PromptSlotFiller();
        var result = filler.Fill("");
        Assert.Equal("", result.FilledText);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void Fill_WithExplicitValues_FillsSlots()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["name"] = "Alice", ["role"] = "engineer" });

        var result = filler.Fill("Hello {{name}}, you are a {{role}}.");
        Assert.Equal("Hello Alice, you are a engineer.", result.FilledText);
        Assert.True(result.IsComplete);
        Assert.Equal(100.0, result.FillPercentage);
    }

    [Fact]
    public void Fill_PartialValues_LeavesUnfilledSlots()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["name"] = "Bob" });

        var result = filler.Fill("{{name}} does {{task}}.");
        Assert.Equal("Bob does {{task}}.", result.FilledText);
        Assert.False(result.IsComplete);
        Assert.Single(result.UnfilledSlots);
        Assert.Equal("task", result.UnfilledSlots[0].Name);
    }

    [Fact]
    public void Fill_WithFallback_FillsUnresolvedSlots()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["name"] = "Charlie" })
            .WithFallback("[TBD]");

        var result = filler.Fill("{{name}} does {{task}}.");
        Assert.Equal("Charlie does [TBD].", result.FilledText);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void Fill_WithDefault_UsesDefaultWhenNoProvider()
    {
        var filler = new PromptSlotFiller();
        var result = filler.Fill("Role: {{role:assistant}}");
        Assert.Equal("Role: assistant", result.FilledText);
        Assert.True(result.IsComplete);
        Assert.Equal(SlotResolution.Default, result.Slots[0].Resolution);
    }

    [Fact]
    public void Fill_ExplicitOverridesDefault()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["role"] = "data scientist" });

        var result = filler.Fill("Role: {{role:assistant}}");
        Assert.Equal("Role: data scientist", result.FilledText);
        Assert.Equal(SlotResolution.Explicit, result.Slots[0].Resolution);
    }

    [Fact]
    public void Fill_ConvenienceOverload_WorksWithDictionary()
    {
        var filler = new PromptSlotFiller();
        var result = filler.Fill("Hi {{name}}!", new Dictionary<string, string> { ["name"] = "Dana" });
        Assert.Equal("Hi Dana!", result.FilledText);
    }

    // ── Providers ────────────────────────────────────────────

    [Fact]
    public void Fill_MultipleProviders_UsesPriority()
    {
        var low = new DictionarySlotProvider(
            new Dictionary<string, string> { ["name"] = "LowPriority" }, "Low", 100);
        var high = new DictionarySlotProvider(
            new Dictionary<string, string> { ["name"] = "HighPriority" }, "High", 1);

        var filler = new PromptSlotFiller()
            .AddProvider(low)
            .AddProvider(high);

        var result = filler.Fill("Hello {{name}}.");
        Assert.Equal("Hello HighPriority.", result.FilledText);
    }

    [Fact]
    public void Fill_FuncProvider_Works()
    {
        var filler = new PromptSlotFiller()
            .AddProvider(new FuncSlotProvider(name => name == "time" ? "12:00" : null));

        var result = filler.Fill("The time is {{time}}.");
        Assert.Equal("The time is 12:00.", result.FilledText);
    }

    [Fact]
    public void Fill_ProviderThrows_AddsWarning()
    {
        var badProvider = new FuncSlotProvider(_ => throw new Exception("boom"), "Bad", 1);
        var filler = new PromptSlotFiller()
            .AddProvider(badProvider)
            .WithFallback("[ERR]");

        var result = filler.Fill("Value: {{x}}");
        Assert.Contains(result.Warnings, w => w.Contains("boom"));
    }

    // ── Validators ───────────────────────────────────────────

    [Fact]
    public void Fill_Validator_TransformsValue()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["format"] = "json" })
            .AddValidator(new SlotValidator
            {
                SlotPattern = "format",
                Transform = v => v.ToUpperInvariant()
            });

        var result = filler.Fill("Format: {{format}}");
        Assert.Equal("Format: JSON", result.FilledText);
    }

    [Fact]
    public void Fill_Validator_RejectsInvalidValue()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["format"] = "YAML" })
            .AddValidator(new SlotValidator
            {
                SlotPattern = "format",
                Validate = v => new[] { "JSON", "CSV" }.Contains(v) ? null : "Invalid format",
                RejectOnFailure = true
            })
            .WithFallback("[INVALID]");

        var result = filler.Fill("Format: {{format}}");
        Assert.Equal("Format: [INVALID]", result.FilledText);
        Assert.Contains(result.Warnings, w => w.Contains("rejected"));
    }

    [Fact]
    public void Fill_Validator_WarnsButKeepsValue()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["count"] = "abc" })
            .AddValidator(new SlotValidator
            {
                SlotPattern = "count",
                Validate = v => int.TryParse(v, out _) ? null : "Not a number",
                RejectOnFailure = false
            });

        var result = filler.Fill("Count: {{count}}");
        Assert.Equal("Count: abc", result.FilledText);
        Assert.Contains(result.Warnings, w => w.Contains("Not a number"));
    }

    [Fact]
    public void Validator_WildcardPattern_MatchesAll()
    {
        var v = new SlotValidator { SlotPattern = "*" };
        Assert.True(v.Matches("anything"));
    }

    [Fact]
    public void Validator_PrefixWildcard_Matches()
    {
        var v = new SlotValidator { SlotPattern = "user_*" };
        Assert.True(v.Matches("user_name"));
        Assert.False(v.Matches("role"));
    }

    [Fact]
    public void Validator_SuffixWildcard_Matches()
    {
        var v = new SlotValidator { SlotPattern = "*_format" };
        Assert.True(v.Matches("output_format"));
        Assert.False(v.Matches("format_type"));
    }

    // ── Strict Mode ──────────────────────────────────────────

    [Fact]
    public void Fill_StrictMode_ThrowsOnUnfilledRequired()
    {
        var filler = new PromptSlotFiller().Strict();
        Assert.Throws<InvalidOperationException>(() => filler.Fill("Hello {{name}}"));
    }

    [Fact]
    public void Fill_StrictMode_OkWhenDefaultExists()
    {
        var filler = new PromptSlotFiller().Strict();
        var result = filler.Fill("Hello {{name:World}}");
        Assert.Equal("Hello World", result.FilledText);
    }

    [Fact]
    public void Fill_StrictMode_OkWhenAllFilled()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["name"] = "Test" })
            .Strict();

        var result = filler.Fill("Hello {{name}}");
        Assert.Equal("Hello Test", result.FilledText);
    }

    // ── SlotFillResult ───────────────────────────────────────

    [Fact]
    public void Result_FillPercentage_NoSlots_Returns100()
    {
        var filler = new PromptSlotFiller();
        var result = filler.Fill("No slots here.");
        Assert.Equal(100.0, result.FillPercentage);
    }

    [Fact]
    public void Result_FillPercentage_HalfFilled_Returns50()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["a"] = "1" });

        var result = filler.Fill("{{a}} and {{b}}");
        Assert.Equal(50.0, result.FillPercentage);
    }

    [Fact]
    public void Result_ResolutionSummary_Populated()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["a"] = "1" });

        var result = filler.Fill("{{a}} and {{b}} and {{c:default}}");
        var summary = result.ResolutionSummary;

        Assert.True(summary.ContainsKey(SlotResolution.Explicit));
        Assert.True(summary.ContainsKey(SlotResolution.Unfilled));
        Assert.True(summary.ContainsKey(SlotResolution.Default));
    }

    [Fact]
    public void Result_RequiredSlotsFilled_TrueWhenDefaultsExist()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["name"] = "X" });

        var result = filler.Fill("{{name}} {{optional:yes}}");
        Assert.True(result.RequiredSlotsFilled);
    }

    // ── Diagnose ─────────────────────────────────────────────

    [Fact]
    public void Diagnose_ReturnsFormattedReport()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["role"] = "dev" });

        var report = filler.Diagnose("You are a {{role}}. Topic: {{topic}}.");
        Assert.Contains("Slot Fill Diagnostic Report", report);
        Assert.Contains("role", report);
        Assert.Contains("topic", report);
        Assert.Contains("50", report); // 50% fill
    }

    // ── ToJson ───────────────────────────────────────────────

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["x"] = "1" });

        var result = filler.Fill("{{x}} {{y}}");
        var json = PromptSlotFiller.ToJson(result);

        Assert.NotEmpty(json);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("1", doc.RootElement.GetProperty("filledText").GetString()?.Split(' ')[0]);
    }

    [Fact]
    public void ToJson_NullResult_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PromptSlotFiller.ToJson(null!));
    }

    // ── EnvironmentSlotProvider ──────────────────────────────

    [Fact]
    public void EnvironmentProvider_ResolvesEnvVar()
    {
        var key = "TEST_SLOT_FILLER_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "hello");
        try
        {
            var provider = new EnvironmentSlotProvider("TEST_SLOT_FILLER_");
            var name = key.Replace("TEST_SLOT_FILLER_", "").ToLowerInvariant();
            // The provider uppercases the name, so we need the raw suffix
            var result = provider.Resolve(key.Replace("TEST_SLOT_FILLER_", ""), null);
            Assert.Equal("hello", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    // ── DictionarySlotProvider ───────────────────────────────

    [Fact]
    public void DictionaryProvider_CanResolve_ReturnsTrueForKnownKey()
    {
        var provider = new DictionarySlotProvider(new Dictionary<string, string> { ["x"] = "1" });
        Assert.True(provider.CanResolve("x"));
        Assert.False(provider.CanResolve("y"));
    }

    [Fact]
    public void DictionaryProvider_NullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DictionarySlotProvider(null!));
    }

    // ── FuncSlotProvider ─────────────────────────────────────

    [Fact]
    public void FuncProvider_NullResolver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FuncSlotProvider(null!));
    }

    [Fact]
    public void FuncProvider_CanResolve_DelegatesCorrectly()
    {
        var provider = new FuncSlotProvider(name => name == "yes" ? "value" : null);
        Assert.True(provider.CanResolve("yes"));
        Assert.False(provider.CanResolve("no"));
    }

    // ── Edge Cases ───────────────────────────────────────────

    [Fact]
    public void Fill_NoProviders_DefaultsStillWork()
    {
        var filler = new PromptSlotFiller();
        var result = filler.Fill("{{a:hello}} {{b:world}}");
        Assert.Equal("hello world", result.FilledText);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void Fill_NestedBraces_NotConfused()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["code"] = "x=1" });

        var result = filler.Fill("Code: {{code}}");
        Assert.Equal("Code: x=1", result.FilledText);
    }

    [Fact]
    public void Fill_MultipleOccurrences_AllReplaced()
    {
        var filler = new PromptSlotFiller()
            .AddValues(new Dictionary<string, string> { ["name"] = "Test" });

        var result = filler.Fill("{{name}} said: {{name}} is here.");
        Assert.Equal("Test said: Test is here.", result.FilledText);
        // But only one slot detected (deduped)
        Assert.Single(result.Slots);
    }
}
