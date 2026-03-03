namespace Prompt.Tests;

using System;
using Xunit;

public class PromptMinifierTests
{
    // ── Minify (string return) ──

    [Fact]
    public void Minify_NullInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PromptMinifier.Minify(null!));
    }

    [Fact]
    public void Minify_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PromptMinifier.Minify(""));
    }

    [Fact]
    public void Minify_WhitespaceOnly_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PromptMinifier.Minify("   "));
    }

    [Fact]
    public void Minify_SimpleText_ReturnsTrimmed()
    {
        var result = PromptMinifier.Minify("Hello world");
        Assert.Equal("Hello world", result);
    }

    // ── Light Level ──

    [Fact]
    public void Light_RemovesHelpMeTo()
    {
        var result = PromptMinifier.Minify("Please help me to write a summary", MinifyLevel.Light);
        Assert.DoesNotContain("help me to", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("write", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_RemovesIWouldLikeYouTo()
    {
        var result = PromptMinifier.Minify("I would like you to analyze the data", MinifyLevel.Light);
        Assert.DoesNotContain("I would like you to", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("analyze", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_RemovesIWantYouTo()
    {
        var result = PromptMinifier.Minify("I want you to create a report", MinifyLevel.Light);
        Assert.DoesNotContain("I want you to", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_RemovesINeedYouTo()
    {
        var result = PromptMinifier.Minify("I need you to fix the bug", MinifyLevel.Light);
        Assert.DoesNotContain("I need you to", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_RemovesCouldYouPlease()
    {
        var result = PromptMinifier.Minify("Could you please summarize this?", MinifyLevel.Light);
        Assert.DoesNotContain("could you please", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_RemovesCanYou()
    {
        var result = PromptMinifier.Minify("Can you explain quantum physics?", MinifyLevel.Light);
        Assert.DoesNotContain("can you", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_RemovesPlease()
    {
        var result = PromptMinifier.Minify("Please write a poem", MinifyLevel.Light);
        Assert.DoesNotContain("please", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_RemovesFillerWords()
    {
        var result = PromptMinifier.Minify("I basically just really need a very simple answer", MinifyLevel.Light);
        Assert.DoesNotContain("basically", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("really", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("very", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_SimplifiesInOrderTo()
    {
        var result = PromptMinifier.Minify("Use this method in order to achieve results", MinifyLevel.Light);
        Assert.DoesNotContain("in order to", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to", result);
    }

    [Fact]
    public void Light_SimplifiesDueToTheFactThat()
    {
        var result = PromptMinifier.Minify("This fails due to the fact that the input is wrong", MinifyLevel.Light);
        Assert.Contains("because", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("due to the fact that", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_SimplifiesAtThisPointInTime()
    {
        var result = PromptMinifier.Minify("At this point in time we need action", MinifyLevel.Light);
        Assert.Contains("now", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_SimplifiesInTheEventThat()
    {
        var result = PromptMinifier.Minify("In the event that errors occur, retry", MinifyLevel.Light);
        Assert.Contains("if", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_SimplifiesForThePurposeOf()
    {
        var result = PromptMinifier.Minify("For the purpose of testing, use mocks", MinifyLevel.Light);
        Assert.Contains("for", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_SimplifiesWithRegardTo()
    {
        var result = PromptMinifier.Minify("With regard to performance, use caching", MinifyLevel.Light);
        Assert.Contains("about", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_SimplifiesHasTheAbilityTo()
    {
        var result = PromptMinifier.Minify("The system has the ability to scale", MinifyLevel.Light);
        Assert.Contains("can", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Light_SimplifiesItIsImportantToNoteThat()
    {
        var result = PromptMinifier.Minify("It is important to note that exceptions may occur", MinifyLevel.Light);
        Assert.Contains("Note:", result);
    }

    [Fact]
    public void Light_NormalizesMultipleSpaces()
    {
        var result = PromptMinifier.Minify("Hello    world    test", MinifyLevel.Light);
        Assert.DoesNotContain("  ", result);
    }

    [Fact]
    public void Light_NormalizesMultipleBlankLines()
    {
        var result = PromptMinifier.Minify("Line1\n\n\n\n\nLine2", MinifyLevel.Light);
        Assert.DoesNotContain("\n\n\n", result);
    }

    // ── Medium Level ──

    [Fact]
    public void Medium_IncludesLightTransforms()
    {
        var result = PromptMinifier.Minify("Could you please help me to write a report", MinifyLevel.Medium);
        Assert.DoesNotContain("could you please", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("help me to", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_RemovesThankYou()
    {
        var result = PromptMinifier.Minify("Write a summary. Thank you for your help.", MinifyLevel.Medium);
        Assert.DoesNotContain("thank you", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_RemovesThanksInAdvance()
    {
        var result = PromptMinifier.Minify("Help with this task. Thanks in advance!", MinifyLevel.Medium);
        Assert.DoesNotContain("thanks in advance", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_RemovesIAppreciate()
    {
        var result = PromptMinifier.Minify("Fix the code. I appreciate your help.", MinifyLevel.Medium);
        Assert.DoesNotContain("I appreciate", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_CondensesMakeSureTo()
    {
        var result = PromptMinifier.Minify("Make sure to validate the input", MinifyLevel.Medium);
        Assert.Contains("ensure", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_CondensesTakeIntoAccount()
    {
        var result = PromptMinifier.Minify("Take into account edge cases", MinifyLevel.Medium);
        Assert.Contains("consider", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_CondensesProvideMeWith()
    {
        var result = PromptMinifier.Minify("Provide me with a detailed analysis", MinifyLevel.Medium);
        Assert.Contains("give", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_CondensesItIsImportantTo()
    {
        var result = PromptMinifier.Minify("It is important to validate inputs", MinifyLevel.Medium);
        Assert.Contains("Must", result);
    }

    [Fact]
    public void Medium_CondensesItIsCriticalThat()
    {
        var result = PromptMinifier.Minify("It is critical that we test this", MinifyLevel.Medium);
        Assert.Contains("Must", result);
    }

    [Fact]
    public void Medium_CondensesAsMuchAsPossible()
    {
        var result = PromptMinifier.Minify("Optimize performance as much as possible", MinifyLevel.Medium);
        Assert.Contains("maximally", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_CondensesALargeNumberOf()
    {
        var result = PromptMinifier.Minify("There are a large number of errors", MinifyLevel.Medium);
        Assert.Contains("many", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Medium_CondensesASmallNumberOf()
    {
        var result = PromptMinifier.Minify("Only a small number of tests failed", MinifyLevel.Medium);
        Assert.Contains("few", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Aggressive Level ──

    [Fact]
    public void Aggressive_IncludesMediumTransforms()
    {
        var result = PromptMinifier.Minify("Could you please provide me with help. Thank you.", MinifyLevel.Aggressive);
        Assert.DoesNotContain("could you please", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("thank you", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_RemovesArticles()
    {
        var result = PromptMinifier.Minify("Write the summary of a report", MinifyLevel.Aggressive);
        Assert.DoesNotContain(" the ", result);
        Assert.DoesNotContain(" a ", result);
    }

    [Fact]
    public void Aggressive_ShortensInformation()
    {
        var result = PromptMinifier.Minify("Provide information about the system", MinifyLevel.Aggressive);
        Assert.Contains("info", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_ShortensApplication()
    {
        var result = PromptMinifier.Minify("Build an application for tracking", MinifyLevel.Aggressive);
        Assert.Contains("app", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_ShortensConfiguration()
    {
        var result = PromptMinifier.Minify("Update the configuration file", MinifyLevel.Aggressive);
        Assert.Contains("config", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_ShortensDocumentation()
    {
        var result = PromptMinifier.Minify("Write documentation for the API", MinifyLevel.Aggressive);
        Assert.Contains("docs", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_ShortensImplementation()
    {
        var result = PromptMinifier.Minify("Review the implementation of the feature", MinifyLevel.Aggressive);
        Assert.Contains("impl", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_ShortensEnvironment()
    {
        var result = PromptMinifier.Minify("Set up the environment variables", MinifyLevel.Aggressive);
        Assert.Contains("env", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_ShortensRepository()
    {
        var result = PromptMinifier.Minify("Clone the repository and build", MinifyLevel.Aggressive);
        Assert.Contains("repo", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aggressive_ShortensForExample()
    {
        var result = PromptMinifier.Minify("Use types, for example integers and strings", MinifyLevel.Aggressive);
        Assert.Contains("e.g.", result);
    }

    // ── MinifyWithStats ──

    [Fact]
    public void MinifyWithStats_ReturnsOriginal()
    {
        var result = PromptMinifier.MinifyWithStats("Hello world");
        Assert.Equal("Hello world", result.Original);
    }

    [Fact]
    public void MinifyWithStats_ReturnsMinified()
    {
        var result = PromptMinifier.MinifyWithStats("Could you please help me to write a summary");
        Assert.NotEqual(result.Original, result.Minified);
        Assert.Contains("write", result.Minified, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MinifyWithStats_CountsTokens()
    {
        var result = PromptMinifier.MinifyWithStats("Could you please help me to write a very detailed summary of the following text");
        Assert.True(result.OriginalTokens > 0);
        Assert.True(result.MinifiedTokens > 0);
        Assert.True(result.MinifiedTokens <= result.OriginalTokens);
    }

    [Fact]
    public void MinifyWithStats_CalculatesTokensSaved()
    {
        var result = PromptMinifier.MinifyWithStats("I would like you to please help me to write a detailed summary");
        Assert.True(result.TokensSaved >= 0);
        Assert.Equal(result.OriginalTokens - result.MinifiedTokens, result.TokensSaved);
    }

    [Fact]
    public void MinifyWithStats_CalculatesSavingsPercent()
    {
        var result = PromptMinifier.MinifyWithStats("Could you please help me to write a summary. Thank you for your help.");
        Assert.True(result.SavingsPercent >= 0);
        Assert.True(result.SavingsPercent <= 100);
    }

    [Fact]
    public void MinifyWithStats_TracksTransformations()
    {
        var result = PromptMinifier.MinifyWithStats("Could you please help me to write something");
        Assert.NotEmpty(result.Transformations);
    }

    [Fact]
    public void MinifyWithStats_NoChanges_EmptyTransformations()
    {
        var result = PromptMinifier.MinifyWithStats("Write code.");
        Assert.Empty(result.Transformations);
    }

    // ── Level Behavior ──

    [Fact]
    public void DefaultLevel_IsMedium()
    {
        var light = PromptMinifier.Minify("Provide me with a summary. Thanks!", MinifyLevel.Light);
        var defaultResult = PromptMinifier.Minify("Provide me with a summary. Thanks!");
        var medium = PromptMinifier.Minify("Provide me with a summary. Thanks!", MinifyLevel.Medium);
        Assert.Equal(medium, defaultResult);
    }

    [Fact]
    public void AggressiveProducesShortestOutput()
    {
        var input = "I would like you to please provide me with detailed information about the application configuration documentation.";
        var light = PromptMinifier.Minify(input, MinifyLevel.Light);
        var medium = PromptMinifier.Minify(input, MinifyLevel.Medium);
        var aggressive = PromptMinifier.Minify(input, MinifyLevel.Aggressive);
        Assert.True(aggressive.Length <= medium.Length, $"Aggressive ({aggressive.Length}) should be <= Medium ({medium.Length})");
        Assert.True(medium.Length <= light.Length, $"Medium ({medium.Length}) should be <= Light ({light.Length})");
    }

    // ── Edge Cases ──

    [Fact]
    public void Minify_PreservesSemanticMeaning()
    {
        var result = PromptMinifier.Minify("Write a function that sorts an array in ascending order", MinifyLevel.Medium);
        Assert.Contains("sort", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("array", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ascending", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Minify_CaseInsensitive()
    {
        var result = PromptMinifier.Minify("COULD YOU PLEASE write a summary", MinifyLevel.Light);
        Assert.DoesNotContain("COULD YOU PLEASE", result);
    }

    [Fact]
    public void Minify_CapitalizesFirstLetter()
    {
        // After removing leading filler, the first letter should be capitalized
        var result = PromptMinifier.Minify("basically write code", MinifyLevel.Light);
        Assert.True(char.IsUpper(result[0]), $"First char '{result[0]}' should be uppercase");
    }

    [Fact]
    public void Minify_MultipleRulesApplyTogether()
    {
        var result = PromptMinifier.MinifyWithStats(
            "Could you please help me to write a summary due to the fact that I need it? Thank you so much!",
            MinifyLevel.Medium);
        Assert.True(result.Transformations.Count >= 3,
            $"Expected >=3 transforms, got {result.Transformations.Count}");
        Assert.True(result.SavingsPercent > 20,
            $"Expected >20% savings, got {result.SavingsPercent}%");
    }

    [Fact]
    public void Minify_CleanupRemovesDoubleSpaces()
    {
        var result = PromptMinifier.Minify("Hello  basically  world", MinifyLevel.Light);
        Assert.DoesNotContain("  ", result);
    }

    [Fact]
    public void Minify_CleanupRemovesLeadingSpaces()
    {
        var result = PromptMinifier.Minify("Hello\n   basically world", MinifyLevel.Light);
        // After removing 'basically', leading spaces on that line should be cleaned
        Assert.DoesNotContain("\n   ", result);
    }

    // ── MinifyResult Properties ──

    [Fact]
    public void MinifyResult_TokensSaved_DerivedProperty()
    {
        var result = new MinifyResult
        {
            Original = "test",
            Minified = "t",
            OriginalTokens = 10,
            MinifiedTokens = 3
        };
        Assert.Equal(7, result.TokensSaved);
    }

    [Fact]
    public void MinifyResult_SavingsPercent_CalculatesCorrectly()
    {
        var result = new MinifyResult
        {
            OriginalTokens = 100,
            MinifiedTokens = 75
        };
        Assert.Equal(25.0, result.SavingsPercent);
    }

    [Fact]
    public void MinifyResult_SavingsPercent_ZeroOriginal_ReturnsZero()
    {
        var result = new MinifyResult { OriginalTokens = 0, MinifiedTokens = 0 };
        Assert.Equal(0, result.SavingsPercent);
    }

    [Fact]
    public void MinifyResult_Defaults()
    {
        var result = new MinifyResult();
        Assert.Equal("", result.Original);
        Assert.Equal("", result.Minified);
        Assert.Equal(0, result.OriginalTokens);
        Assert.Equal(0, result.MinifiedTokens);
        Assert.Equal(0, result.TokensSaved);
        Assert.Equal(0, result.SavingsPercent);
        Assert.Empty(result.Transformations);
    }

    // ── MinifyLevel enum ──

    [Fact]
    public void MinifyLevel_ValuesExist()
    {
        Assert.Equal(0, (int)MinifyLevel.Light);
        Assert.Equal(1, (int)MinifyLevel.Medium);
        Assert.Equal(2, (int)MinifyLevel.Aggressive);
    }

    // ── Realistic Prompts ──

    [Fact]
    public void Minify_RealisticPrompt_ReasonableSavings()
    {
        var prompt = @"Hello! I would like you to please help me to write a very detailed and comprehensive 
summary of the following article. Could you please make sure to include all the important points? 
I really need this to be quite thorough. Thank you so much for your help in advance!

The article discusses the implications of quantum computing on modern cryptography.";

        var result = PromptMinifier.MinifyWithStats(prompt, MinifyLevel.Medium);
        Assert.True(result.SavingsPercent > 10, $"Expected >10% savings on realistic prompt, got {result.SavingsPercent}%");
        Assert.Contains("quantum", result.Minified, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cryptography", result.Minified, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Minify_TechnicalPrompt_PreservesCode()
    {
        var prompt = "Write a function that basically takes a list of integers and returns the sorted unique values";
        var result = PromptMinifier.Minify(prompt, MinifyLevel.Light);
        Assert.Contains("function", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("list", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("integers", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sorted", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unique", result, StringComparison.OrdinalIgnoreCase);
    }
}
