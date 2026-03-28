namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptFeatureFlagManager"/> — registration, evaluation,
/// rollout, audience targeting, and prompt building.
/// </summary>
public class PromptFeatureFlagTests
{
    [Fact]
    public void Register_and_retrieve_flag()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag { Name = "cot", Content = "Think step by step." });
        Assert.Equal(1, mgr.FlagCount);
        Assert.NotNull(mgr.GetFlag("cot"));
    }

    [Fact]
    public void Evaluate_enabled_flag_returns_active()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag { Name = "cot", Content = "Think step by step." });
        var result = mgr.Evaluate("cot", new FlagEvaluationContext { UserId = "u1" });
        Assert.True(result.IsActive);
        Assert.Equal("Think step by step.", result.SelectedContent);
    }

    [Fact]
    public void Evaluate_disabled_flag_returns_fallback()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag
        {
            Name = "cot",
            Enabled = false,
            Content = "Think step by step.",
            FallbackContent = "Answer directly."
        });
        var result = mgr.Evaluate("cot", new FlagEvaluationContext { UserId = "u1" });
        Assert.False(result.IsActive);
        Assert.Equal("Answer directly.", result.SelectedContent);
    }

    [Fact]
    public void Evaluate_unknown_flag_returns_inactive()
    {
        var mgr = new PromptFeatureFlagManager();
        var result = mgr.Evaluate("nonexistent", new FlagEvaluationContext());
        Assert.False(result.IsActive);
        Assert.Equal("Flag not found", result.Reason);
    }

    [Fact]
    public void Audience_targeting_filters_correctly()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag
        {
            Name = "beta",
            Audiences = new List<string> { "beta-testers" },
            Content = "Beta content"
        });

        var match = mgr.Evaluate("beta",
            new FlagEvaluationContext { Segments = new List<string> { "beta-testers" } });
        Assert.True(match.IsActive);

        var noMatch = mgr.Evaluate("beta",
            new FlagEvaluationContext { Segments = new List<string> { "free-tier" } });
        Assert.False(noMatch.IsActive);
        Assert.Equal("Audience mismatch", noMatch.Reason);
    }

    [Fact]
    public void Rollout_is_consistent_for_same_user()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag
        {
            Name = "experiment",
            RolloutPercent = 50,
            Content = "Experimental"
        });
        var ctx = new FlagEvaluationContext { UserId = "stable-user" };
        var first = mgr.Evaluate("experiment", ctx);
        var second = mgr.Evaluate("experiment", ctx);
        Assert.Equal(first.IsActive, second.IsActive);
    }

    [Fact]
    public void BuildPrompt_assembles_active_content()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag { Name = "cot", Content = "Think step by step." });
        mgr.Register(new PromptFeatureFlag
        {
            Name = "off",
            Enabled = false,
            Content = "Should not appear"
        });

        var prompt = mgr.BuildPrompt("Answer the question.", new FlagEvaluationContext());
        Assert.Contains("Answer the question.", prompt);
        Assert.Contains("Think step by step.", prompt);
        Assert.DoesNotContain("Should not appear", prompt);
    }

    [Fact]
    public void ExportImport_roundtrip()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag { Name = "f1", Content = "c1" });
        mgr.Register(new PromptFeatureFlag { Name = "f2", Content = "c2" });
        var json = mgr.ExportJson();

        var mgr2 = new PromptFeatureFlagManager();
        var count = mgr2.ImportJson(json);
        Assert.Equal(2, count);
        Assert.Equal(2, mgr2.FlagCount);
    }

    [Fact]
    public void Tracking_records_evaluations()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.SetTracking(true);
        mgr.Register(new PromptFeatureFlag { Name = "a", Content = "x" });
        mgr.Evaluate("a", new FlagEvaluationContext());
        Assert.Single(mgr.GetEvaluationLog());
        mgr.ClearLog();
        Assert.Empty(mgr.GetEvaluationLog());
    }

    [Fact]
    public void Remove_flag()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag { Name = "tmp", Content = "x" });
        Assert.True(mgr.Remove("tmp"));
        Assert.Equal(0, mgr.FlagCount);
    }

    [Fact]
    public void GetSummary_includes_flag_info()
    {
        var mgr = new PromptFeatureFlagManager();
        mgr.Register(new PromptFeatureFlag
        {
            Name = "demo",
            Description = "A demo flag",
            RolloutPercent = 75,
            Audiences = new List<string> { "premium" }
        });
        var summary = mgr.GetSummary();
        Assert.Contains("demo", summary);
        Assert.Contains("75%", summary);
        Assert.Contains("premium", summary);
    }
}
