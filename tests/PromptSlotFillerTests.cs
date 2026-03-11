namespace Prompt.Tests;

using Xunit;
using System;

public class PromptSlotFillerTests
{
    [Fact]
    public void Constructor_WithValidTemplate_Stores()
    {
        var f = new PromptSlotFiller("Hello {name}");
        Assert.Equal("Hello {name}", f.Template);
    }

    [Fact]
    public void Constructor_EmptyTemplate_Throws()
        => Assert.Throws<ArgumentException>(() => new PromptSlotFiller(""));

    [Fact]
    public void DefineSlot_RegistersSlot()
    {
        var f = new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x"));
        Assert.True(f.Slots.ContainsKey("x"));
    }

    [Fact]
    public void DefineSlot_Fluent()
    {
        var f = new PromptSlotFiller("{a}");
        Assert.Same(f, f.DefineSlot(new SlotDefinition("a")));
    }

    [Fact]
    public void DefineSlot_Null_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PromptSlotFiller("x").DefineSlot(null!));

    [Fact]
    public void SlotDefinition_EmptyName_Throws()
        => Assert.Throws<ArgumentException>(() => new SlotDefinition(""));

    [Fact]
    public void AutoDiscover_FindsPlaceholders()
    {
        var f = new PromptSlotFiller("{name} {age}").AutoDiscover();
        Assert.True(f.Slots.ContainsKey("name"));
        Assert.True(f.Slots.ContainsKey("age"));
    }

    [Fact]
    public void AutoDiscover_NoOverride()
    {
        var f = new PromptSlotFiller("{n}").DefineSlot(new SlotDefinition("n", SlotType.Integer)).AutoDiscover();
        Assert.Equal(SlotType.Integer, f.Slots["n"].Type);
    }

    [Fact]
    public void GetPlaceholders_Deduplicates()
    {
        var ph = new PromptSlotFiller("{a} {b} {a}").GetPlaceholders();
        Assert.Equal(2, ph.Count);
    }

    [Fact]
    public void GetUndeclaredPlaceholders()
    {
        var u = new PromptSlotFiller("{x} {y}").DefineSlot(new SlotDefinition("x")).GetUndeclaredPlaceholders();
        Assert.Single(u);
        Assert.Equal("y", u[0]);
    }

    [Fact]
    public void Fill_KeyColon()
    {
        var r = new PromptSlotFiller("{name}").DefineSlot(new SlotDefinition("name")).Fill("name: Alice");
        Assert.Equal("Alice", r.FilledSlots["name"]);
    }

    [Fact]
    public void Fill_KeyEquals()
    {
        var r = new PromptSlotFiller("{city}").DefineSlot(new SlotDefinition("city")).Fill("city = Seattle");
        Assert.Equal("Seattle", r.FilledSlots["city"]);
    }

    [Fact]
    public void Fill_KeyIs()
    {
        var r = new PromptSlotFiller("{color}").DefineSlot(new SlotDefinition("color")).Fill("color is blue");
        Assert.Equal("blue", r.FilledSlots["color"]);
    }

    [Fact]
    public void Fill_EmptyInput()
    {
        var r = new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x")).Fill("");
        Assert.Empty(r.FilledSlots);
    }

    [Fact]
    public void Fill_MultiTurn_Merges()
    {
        var f = new PromptSlotFiller("{a} and {b}")
            .DefineSlot(new SlotDefinition("a")).DefineSlot(new SlotDefinition("b"));
        var r1 = f.Fill("a: hello");
        Assert.Single(r1.FilledSlots);
        var r2 = f.Fill("b: world", r1);
        Assert.True(r2.IsComplete);
        Assert.Equal("hello and world", r2.RenderedPrompt);
    }

    [Fact]
    public void Fill_NoOverwrite()
    {
        var f = new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x"));
        var r1 = f.Fill("x: first");
        var r2 = f.Fill("x: second", r1);
        Assert.Equal("first", r2.FilledSlots["x"]);
    }

    [Fact]
    public void Fill_Integer()
    {
        var r = new PromptSlotFiller("{n}").DefineSlot(new SlotDefinition("n", SlotType.Integer))
            .Fill("I need 42 items");
        Assert.Equal("42", r.FilledSlots["n"]);
    }

    [Fact]
    public void Fill_Boolean_Yes()
    {
        var r = new PromptSlotFiller("{c}").DefineSlot(new SlotDefinition("c", SlotType.Boolean))
            .Fill("yes please");
        Assert.Equal("true", r.FilledSlots["c"]);
    }

    [Fact]
    public void Fill_Boolean_No()
    {
        var r = new PromptSlotFiller("{c}").DefineSlot(new SlotDefinition("c", SlotType.Boolean))
            .Fill("nope");
        Assert.Equal("false", r.FilledSlots["c"]);
    }

    [Fact]
    public void Fill_Date_ISO()
    {
        var r = new PromptSlotFiller("{d}").DefineSlot(new SlotDefinition("d", SlotType.Date))
            .Fill("on 2026-04-15");
        Assert.Equal("2026-04-15", r.FilledSlots["d"]);
    }

    [Fact]
    public void Fill_Email()
    {
        var r = new PromptSlotFiller("{e}").DefineSlot(new SlotDefinition("e", SlotType.Email))
            .Fill("send to user@example.com please");
        Assert.Equal("user@example.com", r.FilledSlots["e"]);
    }

    [Fact]
    public void Fill_Enum()
    {
        var r = new PromptSlotFiller("{s}")
            .DefineSlot(new SlotDefinition("s", SlotType.Enum) { AllowedValues = new[] { "small", "medium", "large" } })
            .Fill("a medium pizza");
        Assert.Equal("medium", r.FilledSlots["s"]);
    }

    [Fact]
    public void Validate_IntegerOutOfRange()
    {
        var r = new PromptSlotFiller("{g}")
            .DefineSlot(new SlotDefinition("g", SlotType.Integer) { MinValue = 1, MaxValue = 10 })
            .SetSlot("g", "50");
        Assert.True(r.Errors.ContainsKey("g"));
    }

    [Fact]
    public void Validate_IntegerInRange()
    {
        var r = new PromptSlotFiller("{g}")
            .DefineSlot(new SlotDefinition("g", SlotType.Integer) { MinValue = 1, MaxValue = 10 })
            .SetSlot("g", "5");
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void Validate_InvalidEmail()
    {
        var r = new PromptSlotFiller("{e}").DefineSlot(new SlotDefinition("e", SlotType.Email))
            .SetSlot("e", "bad");
        Assert.True(r.Errors.ContainsKey("e"));
    }

    [Fact]
    public void Validate_MinLength()
    {
        var r = new PromptSlotFiller("{n}").DefineSlot(new SlotDefinition("n") { MinLength = 3 })
            .SetSlot("n", "Jo");
        Assert.True(r.Errors.ContainsKey("n"));
    }

    [Fact]
    public void Validate_MaxLength()
    {
        var r = new PromptSlotFiller("{c}").DefineSlot(new SlotDefinition("c") { MaxLength = 5 })
            .SetSlot("c", "TOOLONG");
        Assert.True(r.Errors.ContainsKey("c"));
    }

    [Fact]
    public void Validate_PatternFail()
    {
        var r = new PromptSlotFiller("{z}").DefineSlot(new SlotDefinition("z") { ValidationPattern = @"^\d{5}$" })
            .SetSlot("z", "ABC");
        Assert.True(r.Errors.ContainsKey("z"));
    }

    [Fact]
    public void Validate_PatternPass()
    {
        var r = new PromptSlotFiller("{z}").DefineSlot(new SlotDefinition("z") { ValidationPattern = @"^\d{5}$" })
            .SetSlot("z", "98101");
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void Validate_InvalidBoolean()
    {
        var r = new PromptSlotFiller("{f}").DefineSlot(new SlotDefinition("f", SlotType.Boolean))
            .SetSlot("f", "maybe");
        Assert.True(r.Errors.ContainsKey("f"));
    }

    [Fact]
    public void Validate_InvalidDate()
    {
        var r = new PromptSlotFiller("{d}").DefineSlot(new SlotDefinition("d", SlotType.Date))
            .SetSlot("d", "bad");
        Assert.True(r.Errors.ContainsKey("d"));
    }

    [Fact]
    public void Validate_InvalidEnum()
    {
        var r = new PromptSlotFiller("{t}")
            .DefineSlot(new SlotDefinition("t", SlotType.Enum) { AllowedValues = new[] { "gold", "silver" } })
            .SetSlot("t", "platinum");
        Assert.True(r.Errors.ContainsKey("t"));
    }

    [Fact]
    public void CustomValidator_Rejects()
    {
        var r = new PromptSlotFiller("{d}").DefineSlot(new SlotDefinition("d"))
            .WithValidator("d", v => v.Contains(".") ? null : "Need dot")
            .SetSlot("d", "nodot");
        Assert.Contains("dot", r.Errors["d"]);
    }

    [Fact]
    public void CustomValidator_Accepts()
    {
        var r = new PromptSlotFiller("{d}").DefineSlot(new SlotDefinition("d"))
            .WithValidator("d", v => v.Contains(".") ? null : "Need dot")
            .SetSlot("d", "ok.com");
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void WithValidator_EmptyName_Throws()
        => Assert.Throws<ArgumentException>(() => new PromptSlotFiller("x").WithValidator("", _ => null));

    [Fact]
    public void WithValidator_NullFunc_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PromptSlotFiller("x").WithValidator("a", null!));

    [Fact]
    public void DefaultValue_Applied()
    {
        var r = new PromptSlotFiller("{l}")
            .DefineSlot(new SlotDefinition("l") { Required = false, DefaultValue = "en" })
            .Fill("unrelated");
        Assert.Equal("en", r.FilledSlots["l"]);
    }

    [Fact]
    public void DefaultValue_NotAppliedIfProvided()
    {
        var r = new PromptSlotFiller("{l}")
            .DefineSlot(new SlotDefinition("l") { Required = false, DefaultValue = "en" })
            .Fill("l: fr");
        Assert.Equal("fr", r.FilledSlots["l"]);
    }

    [Fact]
    public void OptionalSlot_NotMissing()
    {
        var r = new PromptSlotFiller("{a} {b}")
            .DefineSlot(new SlotDefinition("a"))
            .DefineSlot(new SlotDefinition("b") { Required = false })
            .Fill("a: hi");
        Assert.DoesNotContain("b", r.MissingSlots);
    }

    [Fact]
    public void SetSlot_Works()
    {
        var r = new PromptSlotFiller("{n}").DefineSlot(new SlotDefinition("n")).SetSlot("n", "Bob");
        Assert.Equal("Bob", r.FilledSlots["n"]);
    }

    [Fact]
    public void SetSlot_Merges()
    {
        var f = new PromptSlotFiller("{a} {b}").DefineSlot(new SlotDefinition("a")).DefineSlot(new SlotDefinition("b"));
        var r2 = f.SetSlot("b", "Y", f.SetSlot("a", "X"));
        Assert.True(r2.IsComplete);
    }

    [Fact]
    public void SetSlot_UnknownSlot()
    {
        var r = new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x"))
            .SetSlot("unknown", "val");
        Assert.Equal("val", r.FilledSlots["unknown"]);
    }

    [Fact]
    public void ClearSlot_Removes()
    {
        var f = new PromptSlotFiller("{a} {b}").DefineSlot(new SlotDefinition("a")).DefineSlot(new SlotDefinition("b"));
        var r = f.ClearSlot("a", f.Fill("b: Y", f.Fill("a: X")));
        Assert.False(r.FilledSlots.ContainsKey("a"));
        Assert.False(r.IsComplete);
    }

    [Fact]
    public void ClearSlot_NullPrev_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PromptSlotFiller("{x}").ClearSlot("x", null!));

    [Fact]
    public void Reset()
    {
        var f = new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x") { Required = false });
        Assert.Empty(f.Reset().Errors);
    }

    [Fact]
    public void Render_OnComplete()
    {
        var f = new PromptSlotFiller("Book {s} for {g}")
            .DefineSlot(new SlotDefinition("s", SlotType.Enum) { AllowedValues = new[] { "dinner" } })
            .DefineSlot(new SlotDefinition("g", SlotType.Integer) { MinValue = 1, MaxValue = 20 });
        var r1 = f.Fill("s: dinner");
        Assert.Null(r1.RenderedPrompt);
        var r2 = f.Fill("g: 4", r1);
        Assert.Equal("Book dinner for 4", r2.RenderedPrompt);
    }

    [Fact]
    public void GeneratePromptForMissing_Lists()
    {
        var f = new PromptSlotFiller("{name}")
            .DefineSlot(new SlotDefinition("name") { Description = "Full name" });
        Assert.Contains("Full name", f.GeneratePromptForMissing(f.Fill("")));
    }

    [Fact]
    public void GeneratePromptForMissing_EnumOptions()
    {
        var f = new PromptSlotFiller("{s}")
            .DefineSlot(new SlotDefinition("s", SlotType.Enum) { AllowedValues = new[] { "S", "M" } });
        Assert.Contains("S, M", f.GeneratePromptForMissing(f.Fill("")));
    }

    [Fact]
    public void GeneratePromptForMissing_Complete()
    {
        var f = new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x"));
        Assert.Contains("All information", f.GeneratePromptForMissing(f.SetSlot("x", "v")));
    }

    [Fact]
    public void Aliases_ExtractViaAlias()
    {
        var r = new PromptSlotFiller("{email}")
            .DefineSlot(new SlotDefinition("email", SlotType.Email) { Aliases = new[] { "mail" } })
            .Fill("mail: test@example.com");
        Assert.Equal("test@example.com", r.FilledSlots["email"]);
    }

    [Fact]
    public void Summary_Incomplete()
    {
        var r = new PromptSlotFiller("{a} {b}").DefineSlot(new SlotDefinition("a")).DefineSlot(new SlotDefinition("b"))
            .Fill("a: hi");
        Assert.Contains("Filled: 1", r.Summary);
        Assert.Contains("Incomplete", r.Summary);
    }

    [Fact]
    public void Summary_Complete()
    {
        var r = new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x")).SetSlot("x", "v");
        Assert.Contains("Complete", r.Summary);
    }

    [Fact]
    public void IsComplete_True()
        => Assert.True(new PromptSlotFiller("{x}").DefineSlot(new SlotDefinition("x")).SetSlot("x", "v").IsComplete);

    [Fact]
    public void IsComplete_WithErrors_False()
        => Assert.False(new PromptSlotFiller("{n}").DefineSlot(new SlotDefinition("n", SlotType.Integer)).SetSlot("n", "abc").IsComplete);

    [Fact]
    public void Phone_Valid()
    {
        var r = new PromptSlotFiller("{p}").DefineSlot(new SlotDefinition("p", SlotType.Phone))
            .Fill("p: +1-555-123-4567");
        Assert.True(r.FilledSlots.ContainsKey("p"));
    }

    [Fact]
    public void Phone_Invalid()
    {
        var r = new PromptSlotFiller("{p}").DefineSlot(new SlotDefinition("p", SlotType.Phone))
            .SetSlot("p", "abc");
        Assert.True(r.Errors.ContainsKey("p"));
    }

    [Fact]
    public void Integer_PrefersInRange()
    {
        var r = new PromptSlotFiller("{q}")
            .DefineSlot(new SlotDefinition("q", SlotType.Integer) { MinValue = 1, MaxValue = 100 })
            .Fill("In 2026 I need 5 things");
        Assert.Equal("5", r.FilledSlots["q"]);
    }

    [Fact]
    public void Number_MinMax()
    {
        var f = new PromptSlotFiller("{s}").DefineSlot(new SlotDefinition("s", SlotType.Number) { MinValue = 0, MaxValue = 100 });
        Assert.Empty(f.SetSlot("s", "75.5").Errors);
        Assert.True(f.SetSlot("s", "150").Errors.ContainsKey("s"));
    }

    [Fact]
    public void EndToEnd_MultiTurn()
    {
        var f = new PromptSlotFiller("Book {meal} for {guests} at {place} on {date}. OK: {confirm}")
            .DefineSlot(new SlotDefinition("meal", SlotType.Enum) { AllowedValues = new[] { "breakfast", "lunch", "dinner" } })
            .DefineSlot(new SlotDefinition("guests", SlotType.Integer) { MinValue = 1, MaxValue = 20 })
            .DefineSlot(new SlotDefinition("place"))
            .DefineSlot(new SlotDefinition("date", SlotType.Date))
            .DefineSlot(new SlotDefinition("confirm", SlotType.Boolean));

        var r1 = f.Fill("dinner on 2026-05-01");
        Assert.Equal("dinner", r1.FilledSlots["meal"]);
        Assert.Equal("2026-05-01", r1.FilledSlots["date"]);
        Assert.False(r1.IsComplete);

        var r2 = f.Fill("place: Canlis, guests: 4", r1);
        Assert.Equal("Canlis", r2.FilledSlots["place"]);

        var r3 = f.Fill("yes", r2);
        Assert.True(r3.IsComplete);
        Assert.Contains("dinner", r3.RenderedPrompt!);
        Assert.Contains("Canlis", r3.RenderedPrompt!);
    }
}
