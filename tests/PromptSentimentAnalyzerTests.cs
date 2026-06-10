namespace Prompt.Tests;

using System;
using System.Linq;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptSentimentAnalyzer"/> covering tone detection,
/// polarity, register metrics, comparison, and tone-shift rewriting.
///
/// Particular attention is paid to <see cref="PromptSentimentAnalyzer.Rewrite"/>,
/// whose replacement rules previously left behind artifacts (lowercased sentence
/// starts after a leading "Please " was removed, and a dangling "please" when it
/// preceded punctuation rather than a space). Those cases are pinned below.
/// </summary>
public class PromptSentimentAnalyzerTests
{
    private readonly PromptSentimentAnalyzer _analyzer = new();

    // ----------------------------------------------------------------- //
    // Input validation
    // ----------------------------------------------------------------- //

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Analyze_NullOrWhitespace_Throws(string? text)
    {
        Assert.Throws<ArgumentException>(() => _analyzer.Analyze(text!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rewrite_NullOrWhitespace_Throws(string? text)
    {
        Assert.Throws<ArgumentException>(() => _analyzer.Rewrite(text!, SentimentTone.Formal));
    }

    // ----------------------------------------------------------------- //
    // Tone detection
    // ----------------------------------------------------------------- //

    [Fact]
    public void Analyze_PoliteText_DetectsPoliteTone()
    {
        var report = _analyzer.Analyze("Please summarize this document, thank you.");
        Assert.Equal(SentimentTone.Polite, report.DominantTone);
        Assert.Contains(report.Signals, s => s.Tone == SentimentTone.Polite);
    }

    [Fact]
    public void Analyze_CommandingText_DetectsAssertiveTone()
    {
        var report = _analyzer.Analyze("You must ensure the output is correct. You shall not deviate.");
        Assert.Equal(SentimentTone.Assertive, report.DominantTone);
    }

    [Fact]
    public void Analyze_FormalConnectors_DetectFormalTone()
    {
        var report = _analyzer.Analyze(
            "Furthermore, the results are consistent; therefore we proceed accordingly.");
        Assert.Equal(SentimentTone.Formal, report.DominantTone);
    }

    [Fact]
    public void Analyze_UrgentLanguage_DetectsUrgentTone()
    {
        var report = _analyzer.Analyze("This is urgent, handle it immediately, asap.");
        Assert.Equal(SentimentTone.Urgent, report.DominantTone);
    }

    [Fact]
    public void Analyze_NeutralText_HasNeutralToneAndHalfConfidence()
    {
        var report = _analyzer.Analyze("The capital of France is Paris.");
        Assert.Equal(SentimentTone.Neutral, report.DominantTone);
        Assert.Equal(0.5, report.Confidence, 3);
        Assert.Equal(1.0, report.ToneDistribution[SentimentTone.Neutral], 3);
    }

    // ----------------------------------------------------------------- //
    // Distribution / confidence invariants
    // ----------------------------------------------------------------- //

    [Fact]
    public void Analyze_ToneDistribution_SumsToOne()
    {
        var report = _analyzer.Analyze("Please make sure you must do this immediately!!");
        double sum = report.ToneDistribution.Values.Sum();
        Assert.Equal(1.0, sum, 3);
    }

    [Fact]
    public void Analyze_Confidence_IsWithinUnitInterval()
    {
        var report = _analyzer.Analyze("Please kindly, would you, could you, may I, thank you.");
        Assert.InRange(report.Confidence, 0.0, 1.0);
    }

    // ----------------------------------------------------------------- //
    // Register metrics
    // ----------------------------------------------------------------- //

    [Fact]
    public void Analyze_CountsQuestionsAndExclamations()
    {
        var report = _analyzer.Analyze("Is this right? Really? Do it now! Go!");
        Assert.Equal(2, report.QuestionCount);
        Assert.Equal(2, report.ExclamationCount);
    }

    [Fact]
    public void Analyze_ImperativeSentences_RaiseImperativeRatio()
    {
        var report = _analyzer.Analyze("Write code. Fix bugs. Ship it.");
        Assert.True(report.ImperativeRatio > 0.5,
            $"expected imperative-heavy text to score high, got {report.ImperativeRatio}");
        Assert.InRange(report.ImperativeRatio, 0.0, 1.0);
    }

    [Fact]
    public void Analyze_QuestionSentence_IsNotImperative()
    {
        var report = _analyzer.Analyze("Could you help me?");
        Assert.Equal(0.0, report.ImperativeRatio, 3);
    }

    // ----------------------------------------------------------------- //
    // Polarity
    // ----------------------------------------------------------------- //

    [Fact]
    public void Analyze_PositiveWords_YieldPositivePolarity()
    {
        var report = _analyzer.Analyze("This is an excellent and helpful, effective approach.");
        Assert.Equal(SentimentPolarity.Positive, report.Polarity);
    }

    [Fact]
    public void Analyze_NegativeWords_YieldNegativePolarity()
    {
        var report = _analyzer.Analyze("This is a terrible, broken, useless result.");
        Assert.Equal(SentimentPolarity.Negative, report.Polarity);
    }

    // ----------------------------------------------------------------- //
    // Rewrite: regression coverage for the cleanup bug
    // ----------------------------------------------------------------- //

    [Fact]
    public void Rewrite_RemovingLeadingPlease_RecapitalizesSentence()
    {
        // Regression: previously produced "summarize this." (lowercase start).
        string result = _analyzer.Rewrite("Please summarize this.", SentimentTone.Assertive);
        Assert.Equal("Summarize this.", result);
    }

    [Fact]
    public void Rewrite_PleaseBeforePunctuation_IsRemovedWithoutStrandedSpace()
    {
        // Regression: "\\bplease\\s+" did not match "please?" so the word lingered,
        // and the strengthened request left a space before the question mark.
        string result = _analyzer.Rewrite("Would you review the code please?", SentimentTone.Assertive);
        Assert.Equal("You must review the code?", result);
    }

    [Fact]
    public void Rewrite_DoesNotLeaveDoubleSpaces()
    {
        string result = _analyzer.Rewrite("Could you please write a summary?", SentimentTone.Assertive);
        Assert.DoesNotContain("  ", result);
        Assert.Equal("You should write a summary?", result);
    }

    [Fact]
    public void Rewrite_DoesNotLeaveSpaceBeforePunctuation()
    {
        string result = _analyzer.Rewrite("Fix the bug please.", SentimentTone.Assertive);
        Assert.DoesNotContain(" .", result);
        Assert.DoesNotContain(" ?", result);
        Assert.Equal("Fix the bug.", result);
    }

    [Fact]
    public void Rewrite_CasualToFormal_ExpandsContractions()
    {
        string result = _analyzer.Rewrite("hey wanna grab stuff?", SentimentTone.Formal);
        Assert.Equal("Greetings would like to grab materials?", result);
    }

    [Fact]
    public void Rewrite_AssertiveToPolite_SoftensCommands()
    {
        string result = _analyzer.Rewrite("You must finish this.", SentimentTone.Polite);
        Assert.StartsWith("Could you please", result);
    }

    [Fact]
    public void Rewrite_NoMatchingRules_PreservesTextApartFromTrim()
    {
        // Targeting Encouraging has no shift rules, so the text is unchanged
        // except for the capitalization normalization that always runs.
        string result = _analyzer.Rewrite("The report is ready.", SentimentTone.Encouraging);
        Assert.Equal("The report is ready.", result);
    }

    [Fact]
    public void Rewrite_MultipleSentences_CapitalizesEachStart()
    {
        string result = _analyzer.Rewrite("Please review this. Please sign off.", SentimentTone.Assertive);
        Assert.Equal("Review this. Sign off.", result);
    }

    // ----------------------------------------------------------------- //
    // Suggestions
    // ----------------------------------------------------------------- //

    [Fact]
    public void Analyze_WithTargetTone_ProducesShiftSuggestions()
    {
        var report = _analyzer.Analyze("Could you please help me?", SentimentTone.Assertive);
        Assert.NotEmpty(report.Suggestions);
        Assert.All(report.Suggestions, s => Assert.Equal(SentimentTone.Assertive, s.TargetTone));
    }

    [Fact]
    public void Analyze_WithoutTargetTone_ProducesNoSuggestions()
    {
        var report = _analyzer.Analyze("Could you please help me?");
        Assert.Empty(report.Suggestions);
    }

    // ----------------------------------------------------------------- //
    // Compare
    // ----------------------------------------------------------------- //

    [Fact]
    public void Compare_PoliteVsAssertive_FlagsToneShift()
    {
        var cmp = _analyzer.Compare("Please help me write a summary.", "Write a summary now.");
        Assert.True(cmp.ToneShifted);
        Assert.NotEmpty(cmp.Differences);
    }

    [Fact]
    public void Compare_IdenticalText_ReportsNoToneOrPolarityShift()
    {
        var cmp = _analyzer.Compare("Please summarize this.", "Please summarize this.");
        Assert.False(cmp.ToneShifted);
        Assert.False(cmp.PolarityShifted);
    }

    [Fact]
    public void Compare_PositiveVsNegative_FlagsPolarityShift()
    {
        var cmp = _analyzer.Compare("This is an excellent result.", "This is a terrible result.");
        Assert.True(cmp.PolarityShifted);
    }

    // ----------------------------------------------------------------- //
    // Reporting
    // ----------------------------------------------------------------- //

    [Fact]
    public void ToSummary_IncludesCoreFields()
    {
        var report = _analyzer.Analyze("Please summarize this carefully.");
        string summary = report.ToSummary();
        Assert.Contains("Sentiment Analysis Report", summary);
        Assert.Contains("Dominant Tone", summary);
        Assert.Contains("Tone Distribution", summary);
    }

    [Fact]
    public void Analyze_IsDeterministic()
    {
        const string text = "Please make sure you review this immediately, thank you!";
        var first = _analyzer.Analyze(text);
        var second = _analyzer.Analyze(text);
        Assert.Equal(first.DominantTone, second.DominantTone);
        Assert.Equal(first.Confidence, second.Confidence, 6);
        Assert.Equal(first.Polarity, second.Polarity);
    }
}
