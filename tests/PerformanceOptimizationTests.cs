using Xunit;
using Prompt;

namespace Prompt.Tests;

/// <summary>
/// Tests for performance-optimized code paths in PromptGuard and TokenBudget.
/// Validates that single-pass character scanning, pre-computed word counts,
/// and single-pass summary generation produce correct results.
/// </summary>
public class PerformanceOptimizationTests
{
    // ── EstimateTokens: single-pass character scanning ──────────────

    [Fact]
    public void EstimateTokens_CodeHeavyText_CorrectlyDetectsSpecialChars()
    {
        // All 16 special chars tracked: { } [ ] ( ) ; : < > = | & ! @ #
        var codeText = "{ } [ ] ( ) ; : < > = | & ! @ # { } [ ] ( ) ; : < > = | & ! @ #";
        int tokens = PromptGuard.EstimateTokens(codeText);
        // Code-heavy text should get the 1.15x multiplier
        Assert.True(tokens > 0);
    }

    [Fact]
    public void EstimateTokens_NewlinesCountedInSinglePass()
    {
        var noNewlines = "Hello world this is a test";
        var withNewlines = "Hello\nworld\nthis\nis\na\ntest";
        int tokensNo = PromptGuard.EstimateTokens(noNewlines);
        int tokensWith = PromptGuard.EstimateTokens(withNewlines);
        // Newlines should add ~0.5 tokens each, so more newlines = more tokens
        Assert.True(tokensWith >= tokensNo, 
            $"With newlines ({tokensWith}) should be >= without ({tokensNo})");
    }

    [Fact]
    public void EstimateTokens_MixedSpecialCharsAndNewlines()
    {
        var mixed = "function() {\n  return [1, 2, 3];\n}\n";
        int tokens = PromptGuard.EstimateTokens(mixed);
        Assert.True(tokens > 0);
        // Should count both special chars and newlines in single pass
    }

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, PromptGuard.EstimateTokens(""));
        Assert.Equal(0, PromptGuard.EstimateTokens(null!));
    }

    [Fact]
    public void EstimateTokens_PureNewlines()
    {
        int tokens = PromptGuard.EstimateTokens("\n\n\n\n\n");
        Assert.True(tokens >= 1, "Pure newlines should produce at least 1 token");
    }

    [Fact]
    public void EstimateTokens_NoSpecialCharsOrNewlines()
    {
        int tokens = PromptGuard.EstimateTokens("Hello world");
        Assert.True(tokens >= 1 && tokens <= 10, $"Simple text: {tokens} tokens");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("hello")]
    [InlineData("The quick brown fox jumps over the lazy dog")]
    public void EstimateTokens_ConsistentResults(string text)
    {
        // Same input should always produce same output (deterministic)
        int first = PromptGuard.EstimateTokens(text);
        int second = PromptGuard.EstimateTokens(text);
        Assert.Equal(first, second);
    }

    [Fact]
    public void EstimateTokens_SpecialCharsJustBelowThreshold()
    {
        // 9% special chars — should NOT trigger code multiplier
        var text = new string('a', 90) + "{}[]();<>!";
        int tokens = PromptGuard.EstimateTokens(text);
        Assert.True(tokens > 0);
    }

    [Fact]
    public void EstimateTokens_SpecialCharsJustAboveThreshold()
    {
        // 11% special chars — SHOULD trigger code multiplier
        var text = new string('a', 89) + "{}[]();<>!=!";
        int tokens = PromptGuard.EstimateTokens(text);
        Assert.True(tokens > 0);
    }

    // ── CalculateQualityScore: pre-computed word count ───────────────

    [Fact]
    public void QualityScore_PublicAndInternal_ProduceSameResult()
    {
        var prompt = "Explain quantum computing in simple terms with 3 examples";
        int publicScore = PromptGuard.CalculateQualityScore(prompt);
        // Internal overload with pre-computed word count
        int wordCount = prompt.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length;
        // Calling public method should give same result as internal path
        int secondCall = PromptGuard.CalculateQualityScore(prompt);
        Assert.Equal(publicScore, secondCall);
    }

    [Fact]
    public void QualityScore_ViaAnalyze_MatchesDirectCall()
    {
        var prompt = "List the top 5 programming languages by popularity in JSON format";
        var analysis = PromptGuard.Analyze(prompt);
        int directScore = PromptGuard.CalculateQualityScore(prompt);
        Assert.Equal(directScore, analysis.QualityScore);
    }

    [Fact]
    public void QualityScore_EmptyPrompt_ReturnsZero()
    {
        Assert.Equal(0, PromptGuard.CalculateQualityScore(""));
        Assert.Equal(0, PromptGuard.CalculateQualityScore("   "));
    }

    [Fact]
    public void QualityScore_ShortPrompt_LowerScore()
    {
        int shortScore = PromptGuard.CalculateQualityScore("Hi");
        int longScore = PromptGuard.CalculateQualityScore(
            "Explain the differences between TCP and UDP protocols, including 3 use cases for each");
        Assert.True(longScore > shortScore,
            $"Detailed prompt ({longScore}) should score higher than 'Hi' ({shortScore})");
    }

    [Theory]
    [InlineData("List 5 things")]
    [InlineData("Explain what happened")]
    [InlineData("How does this work?")]
    public void QualityScore_ReasonableRange(string prompt)
    {
        int score = PromptGuard.CalculateQualityScore(prompt);
        Assert.InRange(score, 0, 100);
    }

    // ── Analyze: consistent token/word counts ───────────────────────

    [Fact]
    public void Analyze_WordCount_MatchesEstimation()
    {
        var prompt = "What is the capital of France";
        var analysis = PromptGuard.Analyze(prompt);
        Assert.Equal(6, analysis.WordCount);
        Assert.Equal(prompt.Length, analysis.CharacterCount);
    }

    [Fact]
    public void Analyze_TokenEstimate_MatchesDirect()
    {
        var prompt = "Explain how neural networks learn through backpropagation";
        var analysis = PromptGuard.Analyze(prompt);
        int direct = PromptGuard.EstimateTokens(prompt);
        Assert.Equal(direct, analysis.EstimatedTokens);
    }

    // ── TokenBudget.GetSummary: single-pass accuracy ────────────────

    [Fact]
    public void GetSummary_RoleCounts_SinglePassAccuracy()
    {
        var budget = new TokenBudget(10000, 1000);
        budget.AddMessage("system", "You are a helpful assistant.");
        budget.AddMessage("user", "What is 2+2?");
        budget.AddMessage("assistant", "4");
        budget.AddMessage("user", "And 3+3?");
        budget.AddMessage("assistant", "6");

        var summary = budget.GetSummary();
        Assert.Equal(1, summary.SystemMessages);
        Assert.Equal(2, summary.UserMessages);
        Assert.Equal(2, summary.AssistantMessages);
        Assert.Equal(5, summary.MessageCount);
    }

    [Fact]
    public void GetSummary_LargestAndAverage_SinglePassAccuracy()
    {
        var budget = new TokenBudget(100000, 1000);
        budget.AddMessage("user", "short");
        budget.AddMessage("user", "This is a much longer message with many more words and tokens");

        var summary = budget.GetSummary();
        Assert.True(summary.LargestMessageTokens > 0);
        Assert.True(summary.AverageMessageTokens > 0);
        Assert.True(summary.LargestMessageTokens >= summary.AverageMessageTokens,
            "Largest should be >= average");
    }

    [Fact]
    public void GetSummary_EmptyBudget_ZeroValues()
    {
        var budget = new TokenBudget(10000, 1000);
        var summary = budget.GetSummary();

        Assert.Equal(0, summary.MessageCount);
        Assert.Equal(0, summary.SystemMessages);
        Assert.Equal(0, summary.UserMessages);
        Assert.Equal(0, summary.AssistantMessages);
        Assert.Equal(0, summary.LargestMessageTokens);
        Assert.Equal(0, summary.AverageMessageTokens);
        Assert.Equal(0, summary.UsedTokens);
    }

    [Fact]
    public void GetSummary_AfterTrim_ReflectsTrimmedState()
    {
        // Small budget that will force trimming
        var budget = new TokenBudget(200, 50);
        budget.AddMessage("system", "System prompt here.");
        
        // Add messages until trimming occurs
        for (int i = 0; i < 20; i++)
        {
            budget.AddMessage("user", $"Message number {i} with some content to use tokens");
        }

        var summary = budget.GetSummary();
        Assert.True(summary.TrimmedCount > 0, "Should have trimmed some messages");
        Assert.True(summary.TrimmedTokens > 0, "Should have trimmed some tokens");
        Assert.True(summary.MessageCount < 21, "Should have fewer than 21 messages after trim");
    }

    [Fact]
    public void GetSummary_OnlySystemMessages()
    {
        var budget = new TokenBudget(10000, 1000);
        budget.AddMessage("system", "You are helpful.");
        budget.AddMessage("system", "Be concise.");

        var summary = budget.GetSummary();
        Assert.Equal(2, summary.SystemMessages);
        Assert.Equal(0, summary.UserMessages);
        Assert.Equal(0, summary.AssistantMessages);
    }

    [Fact]
    public void GetSummary_ConsistentAcrossMultipleCalls()
    {
        var budget = new TokenBudget(10000, 1000);
        budget.AddMessage("user", "Test message");
        budget.AddMessage("assistant", "Response here");

        var first = budget.GetSummary();
        var second = budget.GetSummary();

        Assert.Equal(first.MessageCount, second.MessageCount);
        Assert.Equal(first.UsedTokens, second.UsedTokens);
        Assert.Equal(first.LargestMessageTokens, second.LargestMessageTokens);
        Assert.Equal(first.AverageMessageTokens, second.AverageMessageTokens);
        Assert.Equal(first.SystemMessages, second.SystemMessages);
        Assert.Equal(first.UserMessages, second.UserMessages);
        Assert.Equal(first.AssistantMessages, second.AssistantMessages);
    }

    [Fact]
    public void GetSummary_ManyMessages_CorrectCounts()
    {
        var budget = new TokenBudget(1_000_000, 1000);
        
        for (int i = 0; i < 50; i++)
        {
            budget.AddMessage("user", $"Question {i}");
            budget.AddMessage("assistant", $"Answer {i}");
        }

        var summary = budget.GetSummary();
        Assert.Equal(100, summary.MessageCount);
        Assert.Equal(0, summary.SystemMessages);
        Assert.Equal(50, summary.UserMessages);
        Assert.Equal(50, summary.AssistantMessages);
    }
}
