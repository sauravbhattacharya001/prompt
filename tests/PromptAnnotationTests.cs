namespace Prompt.Tests;

using System;
using System.Linq;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptAnnotation"/> — structured prompt
/// annotation system with inline comments, metadata extraction,
/// tag parsing, section markers, directives, and validation.
/// </summary>
public class PromptAnnotationTests
{
    // ── Strip ─────────────────────────────────────────────────

    [Fact]
    public void Strip_RemovesSingleComment()
    {
        var result = PromptAnnotation.Strip("Hello {{# comment #}} world");
        Assert.Equal("Hello  world", result);
    }

    [Fact]
    public void Strip_RemovesMultipleAnnotations()
    {
        var result = PromptAnnotation.Strip(
            "{{# @author: Jane #}}Start {{# note #}} end{{# @version: 1.0 #}}");
        Assert.Equal("Start  end", result);
    }

    [Fact]
    public void Strip_NoAnnotations_ReturnsOriginal()
    {
        Assert.Equal("Hello world", PromptAnnotation.Strip("Hello world"));
    }

    [Fact]
    public void Strip_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", PromptAnnotation.Strip(""));
    }

    [Fact]
    public void Strip_Null_ReturnsEmpty()
    {
        Assert.Equal("", PromptAnnotation.Strip(null!));
    }

    [Fact]
    public void Strip_MultilineAnnotation()
    {
        var prompt = "Start {{# this\nis a\nmultiline comment #}} end";
        Assert.Equal("Start  end", PromptAnnotation.Strip(prompt));
    }

    // ── Extract: Comments ────────────────────────────────────

    [Fact]
    public void Extract_Comment_ParsedCorrectly()
    {
        var result = PromptAnnotation.Extract("Test {{# a comment #}} here");
        Assert.Single(result.Annotations);
        Assert.Equal(AnnotationType.Comment, result.Annotations[0].Type);
        Assert.Equal("a comment", result.Annotations[0].Content);
    }

    [Fact]
    public void Extract_Comment_PositionTracked()
    {
        var result = PromptAnnotation.Extract("ABC {{# note #}} DEF");
        var ann = result.Annotations[0];
        Assert.Equal(4, ann.StartIndex);
        Assert.Equal(16, ann.EndIndex);
        Assert.Equal(1, ann.Line);
    }

    [Fact]
    public void Extract_Comment_LineNumberTracked()
    {
        var result = PromptAnnotation.Extract("Line 1\nLine 2 {{# note #}} rest");
        Assert.Equal(2, result.Annotations[0].Line);
    }

    [Fact]
    public void Extract_MultipleComments()
    {
        var result = PromptAnnotation.Extract(
            "{{# first #}} mid {{# second #}} end");
        Assert.Equal(2, result.Annotations.Count);
        Assert.Equal("first", result.Annotations[0].Content);
        Assert.Equal("second", result.Annotations[1].Content);
    }

    // ── Extract: Metadata ────────────────────────────────────

    [Fact]
    public void Extract_Metadata_KeyValue()
    {
        var result = PromptAnnotation.Extract("{{# @author: Jane Doe #}} prompt");
        Assert.Single(result.Annotations);
        Assert.Equal(AnnotationType.Metadata, result.Annotations[0].Type);
        Assert.Equal("author", result.Annotations[0].Key);
        Assert.Equal("Jane Doe", result.Annotations[0].Value);
        Assert.Equal("Jane Doe", result.Metadata["author"]);
    }

    [Fact]
    public void Extract_Metadata_MultipleKeys()
    {
        var prompt = "{{# @author: John #}} {{# @version: 2.0 #}} text";
        var result = PromptAnnotation.Extract(prompt);
        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal("John", result.Metadata["author"]);
        Assert.Equal("2.0", result.Metadata["version"]);
    }

    [Fact]
    public void Extract_Metadata_DuplicateKeysMerged()
    {
        var prompt = "{{# @author: Alice #}} {{# @author: Bob #}} text";
        var result = PromptAnnotation.Extract(prompt);
        Assert.Equal("Alice, Bob", result.Metadata["author"]);
    }

    [Fact]
    public void Extract_Metadata_CaseInsensitiveKeys()
    {
        var result = PromptAnnotation.Extract("{{# @Author: Jane #}} text");
        Assert.True(result.Metadata.ContainsKey("author"));
        Assert.True(result.Metadata.ContainsKey("AUTHOR"));
    }

    // ── Extract: Tags ────────────────────────────────────────

    [Fact]
    public void Extract_Tags_SingleTag()
    {
        var result = PromptAnnotation.Extract("{{# @tag: safety #}} prompt");
        Assert.Single(result.Tags);
        Assert.Equal("safety", result.Tags[0]);
    }

    [Fact]
    public void Extract_Tags_CommaSeparated()
    {
        var result = PromptAnnotation.Extract(
            "{{# @tag: safety, production, v2 #}} prompt");
        Assert.Equal(3, result.Tags.Count);
        Assert.Contains("safety", result.Tags);
        Assert.Contains("production", result.Tags);
        Assert.Contains("v2", result.Tags);
    }

    [Fact]
    public void Extract_Tags_MultipleTags_NoDuplicates()
    {
        var result = PromptAnnotation.Extract(
            "{{# @tag: safety #}} {{# @tag: safety, new #}} prompt");
        Assert.Equal(2, result.Tags.Count);
        Assert.Contains("safety", result.Tags);
        Assert.Contains("new", result.Tags);
    }

    // ── Extract: Sections ────────────────────────────────────

    [Fact]
    public void Extract_Section_Tracked()
    {
        var result = PromptAnnotation.Extract(
            "{{# @section: intro #}} Welcome {{# @section: body #}} Main text");
        Assert.Equal(2, result.Sections.Count);
        Assert.Equal("intro", result.Sections[0]);
        Assert.Equal("body", result.Sections[1]);
        Assert.Equal(AnnotationType.Section, result.Annotations[0].Type);
    }

    // ── Extract: Directives ──────────────────────────────────

    [Fact]
    public void Extract_Directive_NoValue()
    {
        var result = PromptAnnotation.Extract("{{# @freeze #}} important prompt");
        Assert.Single(result.Annotations);
        Assert.Equal(AnnotationType.Directive, result.Annotations[0].Type);
        Assert.Equal("freeze", result.Annotations[0].Key);
        Assert.Null(result.Annotations[0].Value);
    }

    [Fact]
    public void Extract_Directive_AllKnownDirectives()
    {
        var directives = new[] { "freeze", "lock", "final", "deprecated",
            "experimental", "required", "optional", "sensitive", "redact", "nolog" };
        foreach (var d in directives)
        {
            var result = PromptAnnotation.Extract($"{{{{# @{d} #}}}} text");
            Assert.Equal(AnnotationType.Directive, result.Annotations[0].Type);
        }
    }

    // ── Extract: Stripped Text ────────────────────────────────

    [Fact]
    public void Extract_StrippedText_AnnotationsRemoved()
    {
        var result = PromptAnnotation.Extract(
            "You are {{# @role: system #}} a helpful {{# doc note #}} assistant.");
        Assert.Equal("You are  a helpful  assistant.", result.StrippedText);
    }

    [Fact]
    public void Extract_OriginalTextPreserved()
    {
        var prompt = "Hello {{# test #}} world";
        var result = PromptAnnotation.Extract(prompt);
        Assert.Equal(prompt, result.OriginalText);
    }

    [Fact]
    public void Extract_IsClean_NoAnnotations()
    {
        var result = PromptAnnotation.Extract("No annotations here");
        Assert.True(result.IsClean);
    }

    [Fact]
    public void Extract_IsClean_WithAnnotations()
    {
        var result = PromptAnnotation.Extract("Has {{# one #}} annotation");
        Assert.False(result.IsClean);
    }

    [Fact]
    public void Extract_EstimatedTokensSaved()
    {
        var result = PromptAnnotation.Extract(
            "text {{# this is a comment that saves tokens #}} more text");
        Assert.True(result.EstimatedTokensSaved > 0);
    }

    // ── Validate ─────────────────────────────────────────────

    [Fact]
    public void Validate_ValidSyntax_NoIssues()
    {
        var issues = PromptAnnotation.Validate("Hello {{# comment #}} world");
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_UnclosedAnnotation()
    {
        var issues = PromptAnnotation.Validate("Hello {{# unclosed annotation");
        Assert.Single(issues);
        Assert.Contains("Unclosed", issues[0]);
    }

    [Fact]
    public void Validate_StrayClosingDelimiter()
    {
        var issues = PromptAnnotation.Validate("Hello world #}}");
        Assert.Single(issues);
        Assert.Contains("Stray", issues[0]);
    }

    [Fact]
    public void Validate_EmptyAnnotation()
    {
        var issues = PromptAnnotation.Validate("Hello {{#  #}} world");
        Assert.Single(issues);
        Assert.Contains("Empty", issues[0]);
    }

    [Fact]
    public void Validate_EmptyString_NoIssues()
    {
        Assert.Empty(PromptAnnotation.Validate(""));
    }

    [Fact]
    public void Validate_NullString_NoIssues()
    {
        Assert.Empty(PromptAnnotation.Validate(null!));
    }

    [Fact]
    public void Validate_NoAnnotations_NoIssues()
    {
        Assert.Empty(PromptAnnotation.Validate("Clean text with no annotations"));
    }

    // ── Insert ───────────────────────────────────────────────

    [Fact]
    public void Insert_AtBeginning()
    {
        var result = PromptAnnotation.Insert("Hello", 0, "note");
        Assert.StartsWith("{{# note #}}", result);
        Assert.EndsWith("Hello", result);
    }

    [Fact]
    public void Insert_AtEnd()
    {
        var result = PromptAnnotation.Insert("Hello", 5, "note");
        Assert.StartsWith("Hello", result);
        Assert.EndsWith("{{# note #}}", result);
    }

    [Fact]
    public void Insert_InMiddle()
    {
        var result = PromptAnnotation.Insert("HelloWorld", 5, "note");
        Assert.Equal("Hello{{# note #}}World", result);
    }

    [Fact]
    public void Insert_NegativePosition_ClampsToZero()
    {
        var result = PromptAnnotation.Insert("Hello", -5, "note");
        Assert.StartsWith("{{# note #}}", result);
    }

    [Fact]
    public void Insert_PastEnd_ClampsToEnd()
    {
        var result = PromptAnnotation.Insert("Hi", 100, "note");
        Assert.EndsWith("{{# note #}}", result);
    }

    // ── AddMetadata ──────────────────────────────────────────

    [Fact]
    public void AddMetadata_PrependsAnnotation()
    {
        var result = PromptAnnotation.AddMetadata("Hello", "author", "Jane");
        Assert.StartsWith("{{# @author: Jane #}}", result);
        Assert.EndsWith("Hello", result);
    }

    [Fact]
    public void AddMetadata_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => PromptAnnotation.AddMetadata("text", "", "value"));
    }

    [Fact]
    public void AddMetadata_Extractable()
    {
        var prompt = PromptAnnotation.AddMetadata("Hello", "version", "3.0");
        var result = PromptAnnotation.Extract(prompt);
        Assert.Equal("3.0", result.Metadata["version"]);
    }

    // ── AddTag ───────────────────────────────────────────────

    [Fact]
    public void AddTag_PrependsTagAnnotation()
    {
        var result = PromptAnnotation.AddTag("Hello", "safety", "prod");
        Assert.StartsWith("{{# @tag: safety, prod #}}", result);
    }

    [Fact]
    public void AddTag_Extractable()
    {
        var prompt = PromptAnnotation.AddTag("Hello", "safety", "v2");
        var result = PromptAnnotation.Extract(prompt);
        Assert.Contains("safety", result.Tags);
        Assert.Contains("v2", result.Tags);
    }

    [Fact]
    public void AddTag_EmptyArray_ReturnsOriginal()
    {
        Assert.Equal("Hello", PromptAnnotation.AddTag("Hello"));
    }

    [Fact]
    public void AddTag_NullPrompt_ReturnsAnnotation()
    {
        var result = PromptAnnotation.AddTag(null!, "tag1");
        Assert.Contains("tag1", result);
    }

    // ── AddSection ───────────────────────────────────────────

    [Fact]
    public void AddSection_InsertsMarker()
    {
        var result = PromptAnnotation.AddSection("HelloWorld", 5, "intro");
        Assert.Contains("@section: intro", result);
        Assert.StartsWith("Hello", result);
    }

    [Fact]
    public void AddSection_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => PromptAnnotation.AddSection("text", 0, ""));
    }

    // ── GetByType ────────────────────────────────────────────

    [Fact]
    public void GetByType_FiltersCorrectly()
    {
        var prompt = "{{# comment #}} {{# @author: X #}} {{# @freeze #}} text";
        var comments = PromptAnnotation.GetByType(prompt, AnnotationType.Comment);
        Assert.Single(comments);
        var metas = PromptAnnotation.GetByType(prompt, AnnotationType.Metadata);
        Assert.Single(metas);
        var dirs = PromptAnnotation.GetByType(prompt, AnnotationType.Directive);
        Assert.Single(dirs);
    }

    // ── HasAnnotations ───────────────────────────────────────

    [Fact]
    public void HasAnnotations_True()
    {
        Assert.True(PromptAnnotation.HasAnnotations("{{# note #}} text"));
    }

    [Fact]
    public void HasAnnotations_False()
    {
        Assert.False(PromptAnnotation.HasAnnotations("No annotations"));
    }

    [Fact]
    public void HasAnnotations_Empty()
    {
        Assert.False(PromptAnnotation.HasAnnotations(""));
    }

    [Fact]
    public void HasAnnotations_PartialDelimiter_False()
    {
        Assert.False(PromptAnnotation.HasAnnotations("{{# unclosed"));
    }

    // ── Summarize ────────────────────────────────────────────

    [Fact]
    public void Summarize_NoAnnotations()
    {
        Assert.Equal("No annotations found.", PromptAnnotation.Summarize("clean text"));
    }

    [Fact]
    public void Summarize_WithAnnotations()
    {
        var prompt = "{{# comment #}} {{# @author: X #}} {{# @freeze #}} text";
        var summary = PromptAnnotation.Summarize(prompt);
        Assert.Contains("3 annotation(s)", summary);
        Assert.Contains("1 comment(s)", summary);
        Assert.Contains("1 metadata", summary);
        Assert.Contains("1 directive(s)", summary);
        Assert.Contains("tokens saved", summary);
    }

    // ── Warnings ─────────────────────────────────────────────

    [Fact]
    public void Extract_UnclosedAnnotation_Warning()
    {
        var result = PromptAnnotation.Extract("text {{# unclosed");
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("unclosed", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_BalancedAnnotation_NoWarnings()
    {
        var result = PromptAnnotation.Extract("text {{# balanced #}} end");
        Assert.Empty(result.Warnings);
    }

    // ── Round-trip ───────────────────────────────────────────

    [Fact]
    public void RoundTrip_StripThenHasAnnotations_False()
    {
        var annotated = "{{# @author: Jane #}} {{# comment #}} Do the thing.";
        var stripped = PromptAnnotation.Strip(annotated);
        Assert.False(PromptAnnotation.HasAnnotations(stripped));
    }

    [Fact]
    public void RoundTrip_AddMetadataThenExtract()
    {
        var prompt = "Hello world";
        prompt = PromptAnnotation.AddMetadata(prompt, "version", "1.0");
        prompt = PromptAnnotation.AddMetadata(prompt, "author", "Test");
        prompt = PromptAnnotation.AddTag(prompt, "prod", "v1");
        var result = PromptAnnotation.Extract(prompt);
        Assert.Equal("1.0", result.Metadata["version"]);
        Assert.Equal("Test", result.Metadata["author"]);
        Assert.Contains("prod", result.Tags);
        Assert.Contains("v1", result.Tags);
        Assert.False(PromptAnnotation.HasAnnotations(result.StrippedText));
    }

    // ── Edge Cases ───────────────────────────────────────────

    [Fact]
    public void Extract_OnlyAnnotations_StrippedTextEmpty()
    {
        var result = PromptAnnotation.Extract("{{# only #}}{{# annotations #}}");
        Assert.Equal("", result.StrippedText);
    }

    [Fact]
    public void Extract_WhitespaceInAnnotation_Trimmed()
    {
        var result = PromptAnnotation.Extract("{{#    spaced content    #}}");
        Assert.Equal("spaced content", result.Annotations[0].Content);
    }

    [Fact]
    public void Extract_SpecialCharsInComment()
    {
        var result = PromptAnnotation.Extract(
            "{{# contains <html> & \"quotes\" #}} text");
        Assert.Equal("contains <html> & \"quotes\"", result.Annotations[0].Content);
    }

    [Fact]
    public void Extract_MetadataKeyWithDots()
    {
        var result = PromptAnnotation.Extract("{{# @api.version: 3.1 #}} text");
        Assert.Equal("3.1", result.Metadata["api.version"]);
    }
}
