namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptDeprecationManager"/>.
/// </summary>
public class PromptDeprecationManagerTests
{
    [Fact]
    public void Deprecate_CreatesNotice()
    {
        var mgr = new PromptDeprecationManager();
        var notice = mgr.Deprecate("old-prompt", "Replaced by v2", "new-prompt-v2");

        Assert.Equal("old-prompt", notice.PromptId);
        Assert.Equal("Replaced by v2", notice.Reason);
        Assert.Equal("new-prompt-v2", notice.ReplacementId);
        Assert.Equal(DeprecationSeverity.Warning, notice.Severity);
        Assert.True(mgr.IsDeprecated("old-prompt"));
    }

    [Fact]
    public void IsDeprecated_CaseInsensitive()
    {
        var mgr = new PromptDeprecationManager();
        mgr.Deprecate("MyPrompt", "test reason");

        Assert.True(mgr.IsDeprecated("myprompt"));
        Assert.True(mgr.IsDeprecated("MYPROMPT"));
    }

    [Fact]
    public void Deprecate_ThrowsOnEmptyId()
    {
        var mgr = new PromptDeprecationManager();
        Assert.Throws<ArgumentException>(() => mgr.Deprecate("", "reason"));
    }

    [Fact]
    public void Deprecate_ThrowsOnEmptyReason()
    {
        var mgr = new PromptDeprecationManager();
        Assert.Throws<ArgumentException>(() => mgr.Deprecate("id", ""));
    }

    [Fact]
    public void GetNotice_ReturnsNullForNonDeprecated()
    {
        var mgr = new PromptDeprecationManager();
        Assert.Null(mgr.GetNotice("does-not-exist"));
    }

    [Fact]
    public void Revoke_RemovesNotice()
    {
        var mgr = new PromptDeprecationManager();
        mgr.Deprecate("temp", "testing");
        Assert.True(mgr.Revoke("temp"));
        Assert.False(mgr.IsDeprecated("temp"));
    }

    [Fact]
    public void IsSunset_TrueWhenPastDate()
    {
        var mgr = new PromptDeprecationManager();
        var notice = mgr.Deprecate("old", "gone",
            sunsetDate: DateTimeOffset.UtcNow.AddDays(-1));

        Assert.True(notice.IsSunset);
    }

    [Fact]
    public void DaysUntilSunset_CalculatesCorrectly()
    {
        var mgr = new PromptDeprecationManager();
        var notice = mgr.Deprecate("soon", "migrating",
            sunsetDate: DateTimeOffset.UtcNow.AddDays(10));

        Assert.NotNull(notice.DaysUntilSunset);
        Assert.InRange(notice.DaysUntilSunset!.Value, 9, 11);
    }

    [Fact]
    public void Audit_ReturnsAllNotices()
    {
        var mgr = new PromptDeprecationManager();
        mgr.Deprecate("a", "reason a");
        mgr.Deprecate("b", "reason b", sunsetDate: DateTimeOffset.UtcNow.AddDays(-5));
        mgr.Deprecate("c", "reason c", sunsetDate: DateTimeOffset.UtcNow.AddDays(7));

        var result = mgr.Audit();
        Assert.Equal(3, result.Notices.Count);
        Assert.Single(result.Sunsetted);
        Assert.Single(result.Approaching);
    }

    [Fact]
    public void Scan_FindsDeprecatedPrompts()
    {
        var mgr = new PromptDeprecationManager();
        mgr.Deprecate("dep1", "old");
        mgr.Deprecate("dep2", "older");

        var found = mgr.Scan(new[] { "dep1", "safe", "dep2", "also-safe" });
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void GetAll_FiltersBySeverity()
    {
        var mgr = new PromptDeprecationManager();
        mgr.Deprecate("a", "info", severity: DeprecationSeverity.Info);
        mgr.Deprecate("b", "warning", severity: DeprecationSeverity.Warning);
        mgr.Deprecate("c", "error", severity: DeprecationSeverity.Error);

        Assert.Single(mgr.GetAll(DeprecationSeverity.Error));
        Assert.Equal(3, mgr.GetAll().Count);
    }

    [Fact]
    public void ExportReport_ProducesFormattedOutput()
    {
        var mgr = new PromptDeprecationManager();
        mgr.Deprecate("old-chat", "Replaced", "new-chat",
            sunsetDate: DateTimeOffset.UtcNow.AddDays(15),
            migrationNotes: "Change template name to new-chat");

        var report = mgr.ExportReport();
        Assert.Contains("old-chat", report);
        Assert.Contains("Replacement:", report);
        Assert.Contains("Migration:", report);
    }

    [Fact]
    public void ExportReport_EmptyWhenNoDeprecations()
    {
        var mgr = new PromptDeprecationManager();
        Assert.Equal("No deprecated prompts.", mgr.ExportReport());
    }
}
