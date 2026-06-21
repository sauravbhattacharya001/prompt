namespace Prompt.Tests;

using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Direct coverage for <see cref="PromptTagManager"/>, which was previously
/// exercised only indirectly. Focuses on the hierarchical-tag contract:
/// <see cref="PromptTagManager.FindByTag"/> ancestor matching and
/// <see cref="PromptTagManager.RenameTag"/> hierarchy preservation.
/// </summary>
public class PromptTagManagerTests
{
    [Fact]
    public void Tag_And_GetTags_RoundTrips_SortedCaseInsensitive()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "task/summarization", "domain/general");

        Assert.Equal(new[] { "domain/general", "task/summarization" }, m.GetTags("p1"));
        Assert.Equal(1, m.PromptCount);
        Assert.Equal(2, m.TagCount);
    }

    [Fact]
    public void FindByTag_MatchesAncestor_AcrossHierarchy()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "domain/medical");
        m.Tag("p2", "domain/legal");
        m.Tag("p3", "task/translation");

        // Ancestor query returns every descendant, but not unrelated branches.
        Assert.Equal(new[] { "p1", "p2" }, m.FindByTag("domain"));
        Assert.Equal(new[] { "p1" }, m.FindByTag("domain/medical"));
        Assert.Empty(m.FindByTag("domain/cardiology"));
    }

    [Fact]
    public void Untag_RemovesReverseIndexEntry_WhenLastPromptGone()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "task/x");
        m.Untag("p1", "task/x");

        Assert.Empty(m.FindByTag("task/x"));
        Assert.Equal(0, m.TagCount);
        Assert.Equal(0, m.PromptCount); // prompt with no tags is dropped
    }

    [Fact]
    public void RenameTag_ExactTag_MovesPromptsAndDescription()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "task/old");
        m.Tag("p2", "task/old");
        m.DescribeTag("task/old", "legacy task tag");

        int affected = m.RenameTag("task/old", "task/new");

        Assert.Equal(2, affected);
        Assert.Equal(new[] { "p1", "p2" }, m.FindByTag("task/new"));
        Assert.Empty(m.FindByTag("task/old"));
        Assert.Equal("legacy task tag", m.GetTagDescription("task/new"));
        Assert.Null(m.GetTagDescription("task/old"));
    }

    // Regression: renaming a parent tag must rename its descendants even when the
    // parent itself is not directly applied to any prompt. Previously RenameTag
    // returned 0 and left "domain/*" untouched because it bailed out on the
    // missing exact-tag index entry before reaching the child-rename loop.
    [Fact]
    public void RenameTag_ParentWithoutDirectPrompts_RenamesDescendants()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "domain/medical");
        m.Tag("p2", "domain/legal");
        // Note: "domain" itself is never directly applied.

        int affected = m.RenameTag("domain", "category");

        Assert.Equal(2, affected);
        Assert.Contains("category/medical", m.GetTags("p1"));
        Assert.Contains("category/legal", m.GetTags("p2"));
        Assert.DoesNotContain("domain/medical", m.GetTags("p1"));
        Assert.Equal(new[] { "p1", "p2" }, m.FindByTag("category"));
        Assert.Empty(m.FindByTag("domain"));
    }

    [Fact]
    public void RenameTag_ExactAndDescendants_RenamedTogether()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "domain");          // exact
        m.Tag("p2", "domain/medical");  // descendant

        int affected = m.RenameTag("domain", "category");

        Assert.Equal(2, affected);
        Assert.Equal(new[] { "category" }, m.GetTags("p1"));
        Assert.Equal(new[] { "category/medical" }, m.GetTags("p2"));
        Assert.Empty(m.FindByTag("domain"));
    }

    [Fact]
    public void RenameTag_NoMatch_ReturnsZero_AndNoChange()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "task/keep");

        Assert.Equal(0, m.RenameTag("nonexistent", "whatever"));
        Assert.Equal(new[] { "task/keep" }, m.GetTags("p1"));
    }

    [Fact]
    public void MergeTag_FoldsSourceIntoDestination_AndDropsSource()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "task/a");
        m.Tag("p2", "task/b");

        int affected = m.MergeTag("task/a", "task/b");

        Assert.Equal(1, affected);
        Assert.Empty(m.FindByTag("task/a"));
        Assert.Equal(new[] { "p1", "p2" }, m.FindByTag("task/b"));
    }

    [Fact]
    public void Aliases_ResolveOnTagAndSearch()
    {
        var m = new PromptTagManager();
        m.AddAlias("med", "domain/medical");
        m.Tag("p1", "med"); // applied via alias

        Assert.Equal(new[] { "domain/medical" }, m.GetTags("p1"));
        Assert.Equal(new[] { "p1" }, m.FindByTag("med")); // search resolves alias too
    }

    [Fact]
    public void AutoTag_AppliesRules_ByIdSubstring()
    {
        var m = new PromptTagManager();
        m.AddAutoTagRule("translate", "task/translation");

        var applied = m.AutoTag("translate-report-v2");

        Assert.Equal(new[] { "task/translation" }, applied);
        Assert.Contains("task/translation", m.GetTags("translate-report-v2"));
    }

    [Fact]
    public void FindByAllTags_ReturnsIntersection()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "domain/medical", "task/qa");
        m.Tag("p2", "domain/medical");

        Assert.Equal(new[] { "p1" }, m.FindByAllTags("domain/medical", "task/qa"));
    }

    [Fact]
    public void ExportImport_RoundTrips_TagsAliasesAndRules()
    {
        var m = new PromptTagManager();
        m.Tag("p1", "domain/medical");
        m.AddAlias("med", "domain/medical");
        m.AddAutoTagRule("translate", "task/translation");
        m.DescribeTag("domain/medical", "clinical");

        string json = m.ExportToJson();

        var restored = new PromptTagManager();
        restored.ImportFromJson(json);

        Assert.Equal(new[] { "p1" }, restored.FindByTag("domain/medical"));
        Assert.Equal("clinical", restored.GetTagDescription("domain/medical"));
        Assert.Equal(new[] { "task/translation" }, restored.AutoTag("translate-x"));
    }
}
