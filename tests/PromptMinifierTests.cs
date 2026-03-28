namespace Prompt.Tests;

using Xunit;

/// <summary>
/// Tests for <see cref="PromptMinifier"/>.
/// </summary>
public class PromptMinifierTests
{
    private readonly PromptMinifier _minifier = new();

    [Fact]
    public void Minify_EmptyString_ReturnsEmpty()
    {
        var result = _minifier.Minify("");
        Assert.Equal("", result.Text);
        Assert.Equal(0, result.SavingsPercent);
    }

    [Fact]
    public void Minify_Null_ReturnsEmpty()
    {
        var result = _minifier.Minify(null!);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void Minify_CollapsesBlankLines()
    {
        var input = "Line 1\n\n\n\n\nLine 2";
        var result = _minifier.Minify(input);
        Assert.Equal("Line 1\n\nLine 2", result.Text);
    }

    [Fact]
    public void Minify_StripsHtmlComments()
    {
        var input = "Before <!-- this is a comment --> After";
        var result = _minifier.Minify(input);
        Assert.Equal("Before  After", result.Text);
    }

    [Fact]
    public void Minify_StripsHorizontalRules()
    {
        var input = "Before\n---\nAfter";
        var result = _minifier.Minify(input);
        Assert.Equal("Before\nAfter", result.Text);
    }

    [Fact]
    public void Minify_TrimsTrailingWhitespace()
    {
        var input = "Hello   \nWorld  ";
        var result = _minifier.Minify(input);
        Assert.Equal("Hello\nWorld", result.Text);
    }

    [Fact]
    public void Minify_PreservesCodeBlocks()
    {
        var input = "Text\n```\n  code   \n  indented  \n```\nMore text";
        var result = _minifier.Minify(input);
        Assert.Contains("  code   ", result.Text);
        Assert.Contains("  indented  ", result.Text);
    }

    [Fact]
    public void Minify_AggressiveCollapsesPunctuation()
    {
        var input = "Wow!!!!! Amazing???";
        var result = _minifier.Minify(input, new MinifyOptions { Level = MinifyLevel.Aggressive });
        Assert.Equal("Wow! Amazing?", result.Text);
    }

    [Fact]
    public void Minify_ReportsSavings()
    {
        var input = "Line\n\n\n\n\nLine\n\n\n\n\nLine";
        var result = _minifier.Minify(input);
        Assert.True(result.SavingsPercent > 0);
        Assert.True(result.EstimatedTokensSaved > 0);
        Assert.True(result.MinifiedChars < result.OriginalChars);
    }

    [Fact]
    public void MinifyText_ReturnsJustText()
    {
        var input = "Hello\n\n\n\nWorld";
        var text = _minifier.MinifyText(input);
        Assert.Equal("Hello\n\nWorld", text);
    }

    [Fact]
    public void Report_ReturnsValidJson()
    {
        var input = "Test\n\n\n\nprompt";
        var json = _minifier.Report(input);
        Assert.Contains("\"savingsPercent\"", json);
        Assert.Contains("\"estimatedTokensSaved\"", json);
    }

    [Fact]
    public void Minify_NormaliseBullets()
    {
        var input = "* item1\n+ item2\n- item3";
        var result = _minifier.Minify(input, new MinifyOptions { NormaliseBullets = true });
        Assert.Equal("- item1\n- item2\n- item3", result.Text);
    }
}
