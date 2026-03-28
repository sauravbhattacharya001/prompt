namespace Prompt.Tests;

using System;
using Xunit;

public class PromptStyleTransformerTests
{
    private readonly PromptStyleTransformer _transformer = new();

    [Fact]
    public void Transform_Formal_ExpandsContractions()
    {
        var result = _transformer.Transform("I can't do it, don't worry.", PromptStyle.Formal);
        Assert.Contains("cannot", result.Transformed);
        Assert.Contains("do not", result.Transformed);
        Assert.Equal(PromptStyle.Formal, result.TargetStyle);
    }

    [Fact]
    public void Transform_Formal_ReplacesCasualWords()
    {
        var result = _transformer.Transform("Hey, can you fix this bug?", PromptStyle.Formal);
        Assert.DoesNotContain("Hey", result.Transformed);
        Assert.DoesNotContain("bug", result.Transformed);
    }

    [Fact]
    public void Transform_Casual_ContractsAndSimplifies()
    {
        var result = _transformer.Transform("Please utilize this. Do not forget.", PromptStyle.Casual);
        Assert.Contains("Don't", result.Transformed);
        Assert.Contains("use", result.Transformed);
    }

    [Fact]
    public void Transform_Casual_RemovesFormalTransitions()
    {
        var result = _transformer.Transform("Furthermore, this is important. Therefore act now.", PromptStyle.Casual);
        Assert.Contains("Also", result.Transformed);
        Assert.Contains("So", result.Transformed);
    }

    [Fact]
    public void Transform_Concise_StripsPoliteness()
    {
        var result = _transformer.Transform("Please help me write code. Thank you.", PromptStyle.Concise);
        Assert.DoesNotContain("Please", result.Transformed);
        Assert.DoesNotContain("Thank you", result.Transformed);
    }

    [Fact]
    public void Transform_Concise_RemovesFillerWords()
    {
        var result = _transformer.Transform("I think basically you should just do it.", PromptStyle.Concise);
        Assert.DoesNotContain("basically", result.Transformed);
        Assert.DoesNotContain("I think", result.Transformed);
    }

    [Fact]
    public void Transform_Simple_ReplacesJargon()
    {
        var result = _transformer.Transform("Serialize the payload and check latency.", PromptStyle.Simple);
        Assert.Contains("convert to text", result.Transformed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data", result.Transformed);
        Assert.Contains("delay", result.Transformed);
    }

    [Fact]
    public void Transform_Verbose_AddsFramingAndClosing()
    {
        var result = _transformer.Transform("Sort a list", PromptStyle.Verbose);
        Assert.Contains("Please carefully follow", result.Transformed);
        Assert.Contains("thorough", result.Transformed);
    }

    [Fact]
    public void Transform_Verbose_SkipsFramingIfAlreadyFramed()
    {
        var result = _transformer.Transform("You are a coding assistant.", PromptStyle.Verbose);
        Assert.StartsWith("You are", result.Transformed);
        Assert.DoesNotContain("Please carefully follow", result.Transformed);
    }

    [Fact]
    public void Transform_Instructional_CreatesNumberedSteps()
    {
        var result = _transformer.Transform("Read the file. Parse the data. Output results.", PromptStyle.Instructional);
        Assert.Contains("1.", result.Transformed);
        Assert.Contains("2.", result.Transformed);
        Assert.Contains("3.", result.Transformed);
        Assert.Contains("Follow these steps:", result.Transformed);
    }

    [Fact]
    public void Transform_Instructional_SingleSentenceUnchanged()
    {
        var result = _transformer.Transform("Do the thing.", PromptStyle.Instructional);
        Assert.Equal("Do the thing.", result.Transformed);
    }

    [Fact]
    public void Transform_Socratic_ConvertsImperativesToQuestions()
    {
        var result = _transformer.Transform("Write a sorting algorithm.", PromptStyle.Socratic);
        Assert.Contains("?", result.Transformed);
        Assert.Contains("How would you", result.Transformed);
    }

    [Fact]
    public void Transform_Socratic_KeepsExistingQuestions()
    {
        var result = _transformer.Transform("What is recursion?", PromptStyle.Socratic);
        Assert.Contains("What is recursion?", result.Transformed);
    }

    [Fact]
    public void TransformChain_AppliesMultipleStyles()
    {
        var result = _transformer.TransformChain(
            "Please help me implement this API endpoint. Thank you.",
            PromptStyle.Concise, PromptStyle.Formal
        );
        Assert.DoesNotContain("Thank you", result.Transformed);
        Assert.Equal(PromptStyle.Formal, result.TargetStyle);
        Assert.True(result.Transformations.Count > 0);
    }

    [Fact]
    public void Transform_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _transformer.Transform(null!, PromptStyle.Formal));
    }

    [Fact]
    public void Transform_EmptyString_ReturnsEmpty()
    {
        var result = _transformer.Transform("", PromptStyle.Concise);
        Assert.Equal("", result.Transformed);
    }

    [Fact]
    public void Transform_ResultHasTokenCounts()
    {
        var result = _transformer.Transform("Hello world, this is a test.", PromptStyle.Formal);
        Assert.True(result.OriginalTokens > 0);
        Assert.True(result.TransformedTokens > 0);
    }

    [Fact]
    public void Transform_OriginalIsPreserved()
    {
        var input = "Hey, can you fix this bug?";
        var result = _transformer.Transform(input, PromptStyle.Formal);
        Assert.Equal(input, result.Original);
    }

    [Fact]
    public void TransformChain_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _transformer.TransformChain(null!, PromptStyle.Formal));
    }

    [Fact]
    public void TransformChain_NoStyles_DefaultsToFormal()
    {
        var result = _transformer.TransformChain("Hello world.");
        Assert.Equal(PromptStyle.Formal, result.TargetStyle);
    }
}
