namespace Prompt.Tests;

using Xunit;

/// <summary>
/// Tests for <see cref="PromptCostOptimizer"/>.
/// </summary>
public class PromptCostOptimizerTests
{
    [Fact]
    public void Analyze_EmptyPrompt_ReturnsEmptyReport()
    {
        var optimizer = new PromptCostOptimizer();
        var report = optimizer.Analyze("");
        Assert.Equal(0, report.OriginalTokens);
        Assert.Empty(report.Suggestions);
    }

    [Fact]
    public void Analyze_DetectsVerbosePhrases()
    {
        var optimizer = new PromptCostOptimizer();
        var report = optimizer.Analyze("I would like you to summarize this text. In order to do this, read it carefully.");
        Assert.Contains(report.Suggestions, s => s.Category == "Verbose Phrase");
    }

    [Fact]
    public void Analyze_DetectsDuplicateSentences()
    {
        var optimizer = new PromptCostOptimizer();
        var prompt = "Summarize the document carefully. Read all sections. Summarize the document carefully.";
        var report = optimizer.Analyze(prompt);
        Assert.Contains(report.Suggestions, s => s.Category == "Duplicate Content");
    }

    [Fact]
    public void Optimize_RemovesFillerPhrases()
    {
        var optimizer = new PromptCostOptimizer();
        var result = optimizer.Optimize("I would like you to summarize this text.");
        Assert.DoesNotContain("I would like you to", result);
        Assert.Contains("summarize", result);
    }

    [Fact]
    public void Optimize_CollapsesWhitespace()
    {
        var optimizer = new PromptCostOptimizer();
        var result = optimizer.Optimize("Hello     world");
        Assert.DoesNotContain("     ", result);
    }

    [Fact]
    public void Optimize_DeduplicatesSentences()
    {
        var optimizer = new PromptCostOptimizer();
        var result = optimizer.Optimize("Do this task. Do this task. Do another thing.");
        // Should only have one "Do this task."
        var count = System.Text.RegularExpressions.Regex.Matches(result, "Do this task").Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public void AnalyzeAndOptimize_ReturnsOptimizedPrompt()
    {
        var optimizer = new PromptCostOptimizer();
        var report = optimizer.AnalyzeAndOptimize("I would like you to help me. In order to do this, please read the document.");
        Assert.NotNull(report.OptimizedPrompt);
        Assert.True(report.OptimizedTokens <= report.OriginalTokens);
    }

    [Fact]
    public void Analyze_RecommendsModelDowngrade_ForSimplePrompt()
    {
        var optimizer = new PromptCostOptimizer("gpt-4");
        var report = optimizer.Analyze("What is the capital of France?");
        // Simple prompt should suggest a cheaper model
        Assert.Contains(report.Suggestions, s => s.Category == "Model Tier");
    }

    [Fact]
    public void Summary_FormatsCorrectly()
    {
        var optimizer = new PromptCostOptimizer();
        var report = optimizer.Analyze("I would like you to help. In order to do this, read it.");
        var summary = report.Summary();
        Assert.Contains("Cost Optimization Report", summary);
        Assert.Contains("Original tokens", summary);
    }
}
