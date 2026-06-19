namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using Xunit;

public class PromptMergerTests
{
    [Fact]
    public void Merge_TwoTemplates_CombinesBodies()
    {
        var t1 = new PromptTemplate("You are a {{role}} assistant.");
        var t2 = new PromptTemplate("Help with {{topic}}.");

        var merged = PromptMerger.Create()
            .Add(t1)
            .Add(t2)
            .Merge();

        Assert.Contains("{{role}}", merged.Template);
        Assert.Contains("{{topic}}", merged.Template);
    }

    [Fact]
    public void Merge_WithDefaults_CombinesAllDefaults()
    {
        var t1 = new PromptTemplate("Hello {{name}}.", new Dictionary<string, string> { ["name"] = "Alice" });
        var t2 = new PromptTemplate("Your role is {{role}}.", new Dictionary<string, string> { ["role"] = "helper" });

        var merged = PromptMerger.Create()
            .Add(t1)
            .Add(t2)
            .Merge();

        Assert.Equal("Alice", merged.Defaults["name"]);
        Assert.Equal("helper", merged.Defaults["role"]);
    }

    [Fact]
    public void Merge_LastWins_OverridesDefaults()
    {
        var t1 = new PromptTemplate("A {{x}}.", new Dictionary<string, string> { ["x"] = "first" });
        var t2 = new PromptTemplate("B {{x}}.", new Dictionary<string, string> { ["x"] = "second" });

        var merged = PromptMerger.Create()
            .Add(t1)
            .Add(t2)
            .WithConflictResolution(ConflictResolution.LastWins)
            .Merge();

        Assert.Equal("second", merged.Defaults["x"]);
    }

    [Fact]
    public void Merge_FirstWins_KeepsFirstDefault()
    {
        var t1 = new PromptTemplate("A {{x}}.", new Dictionary<string, string> { ["x"] = "first" });
        var t2 = new PromptTemplate("B {{x}}.", new Dictionary<string, string> { ["x"] = "second" });

        var merged = PromptMerger.Create()
            .Add(t1)
            .Add(t2)
            .WithConflictResolution(ConflictResolution.FirstWins)
            .Merge();

        Assert.Equal("first", merged.Defaults["x"]);
    }

    [Fact]
    public void Merge_ThrowOnConflict_Throws()
    {
        var t1 = new PromptTemplate("A {{x}}.", new Dictionary<string, string> { ["x"] = "first" });
        var t2 = new PromptTemplate("B {{x}}.", new Dictionary<string, string> { ["x"] = "second" });

        var merger = PromptMerger.Create()
            .Add(t1)
            .Add(t2)
            .WithConflictResolution(ConflictResolution.ThrowOnConflict);

        Assert.Throws<InvalidOperationException>(() => merger.Merge());
    }

    [Fact]
    public void Merge_WithLabels_IncludesLabelsInBody()
    {
        var t1 = new PromptTemplate("Be helpful.");

        var merged = PromptMerger.Create()
            .Add(t1, "Persona")
            .Merge();

        Assert.Contains("[Persona]", merged.Template);
    }

    [Fact]
    public void Merge_WithPrefixAndSuffix()
    {
        var t1 = new PromptTemplate("Do the thing.");

        var merged = PromptMerger.Create()
            .WithPrefix("BEGIN")
            .WithSuffix("END")
            .Add(t1)
            .Merge();

        Assert.StartsWith("BEGIN", merged.Template);
        Assert.EndsWith("END", merged.Template);
    }

    [Fact]
    public void Merge_AddText_InsertsRawText()
    {
        var t1 = new PromptTemplate("Part one.");

        var merged = PromptMerger.Create()
            .Add(t1)
            .AddText("---")
            .Merge();

        Assert.Contains("---", merged.Template);
    }

    [Fact]
    public void Merge_CustomSeparator()
    {
        var t1 = new PromptTemplate("A.");
        var t2 = new PromptTemplate("B.");

        var merged = PromptMerger.Create()
            .Add(t1)
            .Add(t2)
            .WithSeparator(" | ")
            .Merge();

        Assert.Contains(" | ", merged.Template);
    }

    [Fact]
    public void Merge_Empty_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PromptMerger.Create().Merge());
    }

    [Fact]
    public void Summarize_DetectsConflicts()
    {
        var t1 = new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "a" });
        var t2 = new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "b" });

        var summary = PromptMerger.Create()
            .Add(t1, "T1")
            .Add(t2, "T2")
            .Summarize();

        Assert.True(summary.HasConflicts);
        Assert.Equal(2, summary.EntryCount);
    }

    [Fact]
    public void Summarize_ConflictingDefaultNotReferencedInBody_StillDetected()
    {
        // t1 references {{x}}; t2's body is a literal but it ALSO carries a
        // default for x with a different value. Merge(ThrowOnConflict) throws
        // on this, so Summarize() must report it (regression: the old code
        // keyed conflict detection on {{var}} body references and missed it).
        var t1 = new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "a" });
        var t2 = new PromptTemplate("literal only", new Dictionary<string, string> { ["x"] = "b" });

        var merger = PromptMerger.Create().Add(t1, "T1").Add(t2, "T2");

        var summary = merger.Summarize();
        Assert.True(summary.HasConflicts);
        Assert.Single(summary.Conflicts);

        // Summarize() and Merge(ThrowOnConflict) must agree: a reported
        // conflict means the throwing merge actually throws.
        Assert.Throws<InvalidOperationException>(() =>
            merger.WithConflictResolution(ConflictResolution.ThrowOnConflict).Merge());
    }

    [Fact]
    public void Summarize_GlobalVsTemplateDefaultConflict_Detected()
    {
        // A global default and a template default disagree on the same key.
        // BuildDefaults seeds from globals first, so ThrowOnConflict throws —
        // Summarize() must treat global defaults as a conflict source too.
        var t1 = new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "template" });

        var merger = PromptMerger.Create()
            .WithDefaults(new Dictionary<string, string> { ["x"] = "global" })
            .Add(t1, "T1");

        Assert.True(merger.Summarize().HasConflicts);
        Assert.Throws<InvalidOperationException>(() =>
            merger.WithConflictResolution(ConflictResolution.ThrowOnConflict).Merge());
    }

    [Fact]
    public void Summarize_IdenticalDefaults_NoConflict()
    {
        // Same default value from two entries is not a conflict, and the
        // throwing merge agrees (it does not throw on equal values).
        var t1 = new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "same" });
        var t2 = new PromptTemplate("{{x}}", new Dictionary<string, string> { ["x"] = "same" });

        var merger = PromptMerger.Create().Add(t1).Add(t2);

        Assert.False(merger.Summarize().HasConflicts);
        var ex = Record.Exception(() =>
            merger.WithConflictResolution(ConflictResolution.ThrowOnConflict).Merge());
        Assert.Null(ex);
    }

    [Fact]
    public void Merge_GlobalDefaults_LowestPriority()
    {
        var t1 = new PromptTemplate("{{x}} {{y}}.", new Dictionary<string, string> { ["x"] = "from_template" });

        var merged = PromptMerger.Create()
            .WithDefaults(new Dictionary<string, string> { ["x"] = "from_global", ["y"] = "global_y" })
            .Add(t1)
            .Merge();

        // Template default overrides global for x
        Assert.Equal("from_template", merged.Defaults["x"]);
        // Global provides y
        Assert.Equal("global_y", merged.Defaults["y"]);
    }
}
