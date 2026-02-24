namespace Prompt.Tests;

using System;
using System.Linq;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptComposer"/> — fluent structured prompt builder.
/// </summary>
public class PromptComposerTests
{
    // ───────────────────── Create ─────────────────────

    [Fact]
    public void Create_ReturnsNewInstance()
    {
        var composer = PromptComposer.Create();
        Assert.NotNull(composer);
    }

    [Fact]
    public void Create_SectionCountIsZero()
    {
        var composer = PromptComposer.Create();
        Assert.Equal(0, composer.SectionCount());
    }

    // ───────────────────── WithPersona ─────────────────────

    [Fact]
    public void WithPersona_SetsPersona()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("You are a helpful assistant")
            .Build();
        Assert.Contains("You are a helpful assistant", prompt);
    }

    [Fact]
    public void WithPersona_TrimsWhitespace()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("  trimmed  ")
            .Build();
        Assert.Equal("trimmed", prompt);
    }

    [Fact]
    public void WithPersona_ThrowsOnNull()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithPersona(null!));
    }

    [Fact]
    public void WithPersona_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithPersona(""));
    }

    [Fact]
    public void WithPersona_ThrowsOnWhitespaceOnly()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithPersona("   "));
    }

    [Fact]
    public void WithPersona_ThrowsOnExceedingMaxSectionLength()
    {
        var longText = new string('a', PromptComposer.MaxSectionLength + 1);
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithPersona(longText));
    }

    // ───────────────────── WithContext ─────────────────────

    [Fact]
    public void WithContext_SetsContext()
    {
        var prompt = PromptComposer.Create()
            .WithContext("User is building a REST API")
            .Build();
        Assert.Contains("User is building a REST API", prompt);
    }

    [Fact]
    public void WithContext_TrimsWhitespace()
    {
        var prompt = PromptComposer.Create()
            .WithContext("  context text  ")
            .Build();
        Assert.Equal("context text", prompt);
    }

    [Fact]
    public void WithContext_ThrowsOnNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithContext(null!));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithContext(""));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithContext("  "));
    }

    // ───────────────────── WithTask ─────────────────────

    [Fact]
    public void WithTask_SetsTask()
    {
        var prompt = PromptComposer.Create()
            .WithTask("Review this code")
            .Build();
        Assert.Contains("Review this code", prompt);
    }

    [Fact]
    public void WithTask_TrimsWhitespace()
    {
        var prompt = PromptComposer.Create()
            .WithTask("  task text  ")
            .Build();
        Assert.Equal("task text", prompt);
    }

    [Fact]
    public void WithTask_ThrowsOnNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithTask(null!));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithTask(""));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithTask("\t"));
    }

    // ───────────────────── WithConstraint ─────────────────────

    [Fact]
    public void WithConstraint_AddsConstraint()
    {
        var prompt = PromptComposer.Create()
            .WithConstraint("Be concise")
            .Build();
        Assert.Contains("- Be concise", prompt);
    }

    [Fact]
    public void WithConstraint_AddsMultipleConstraints()
    {
        var prompt = PromptComposer.Create()
            .WithConstraint("Be concise")
            .WithConstraint("Be accurate")
            .Build();
        Assert.Contains("- Be concise", prompt);
        Assert.Contains("- Be accurate", prompt);
    }

    [Fact]
    public void WithConstraint_TrimsWhitespace()
    {
        var prompt = PromptComposer.Create()
            .WithConstraint("  trimmed constraint  ")
            .Build();
        Assert.Contains("- trimmed constraint", prompt);
    }

    [Fact]
    public void WithConstraint_ThrowsOnNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithConstraint(null!));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithConstraint(""));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithConstraint("   "));
    }

    [Fact]
    public void WithConstraint_ThrowsOnExceedingMaxConstraints()
    {
        var composer = PromptComposer.Create();
        for (int i = 0; i < PromptComposer.MaxConstraints; i++)
            composer.WithConstraint($"Constraint {i}");

        Assert.Throws<InvalidOperationException>(() =>
            composer.WithConstraint("One too many"));
    }

    // ───────────────────── WithConstraints (params) ─────────────────────

    [Fact]
    public void WithConstraints_AddsMultiple()
    {
        var prompt = PromptComposer.Create()
            .WithConstraints("Rule 1", "Rule 2", "Rule 3")
            .Build();
        Assert.Contains("- Rule 1", prompt);
        Assert.Contains("- Rule 2", prompt);
        Assert.Contains("- Rule 3", prompt);
    }

    [Fact]
    public void WithConstraints_ThrowsOnAnyEmptyElement()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithConstraints("Valid", "", "Also valid"));
    }

    // ───────────────────── WithExample ─────────────────────

    [Fact]
    public void WithExample_AddsExamplePair()
    {
        var prompt = PromptComposer.Create()
            .WithExample("What is 2+2?", "4")
            .Build();
        Assert.Contains("Input: What is 2+2?", prompt);
        Assert.Contains("Output: 4", prompt);
        Assert.Contains("Example 1:", prompt);
    }

    [Fact]
    public void WithExample_ThrowsOnEmptyInput()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithExample("", "output"));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithExample(null!, "output"));
    }

    [Fact]
    public void WithExample_ThrowsOnEmptyOutput()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithExample("input", ""));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithExample("input", null!));
    }

    [Fact]
    public void WithExample_ThrowsOnExceedingMaxExamples()
    {
        var composer = PromptComposer.Create();
        for (int i = 0; i < PromptComposer.MaxExamples; i++)
            composer.WithExample($"Input {i}", $"Output {i}");

        Assert.Throws<InvalidOperationException>(() =>
            composer.WithExample("One too many", "Overflow"));
    }

    // ───────────────────── WithOutputFormat ─────────────────────

    [Fact]
    public void WithOutputFormat_SetsFormat()
    {
        var prompt = PromptComposer.Create()
            .WithOutputFormat(OutputSection.JSON)
            .Build();
        Assert.Contains("JSON", prompt);
    }

    [Theory]
    [InlineData(OutputSection.JSON, "JSON")]
    [InlineData(OutputSection.BulletList, "bullet points")]
    [InlineData(OutputSection.NumberedList, "numbered list")]
    [InlineData(OutputSection.Table, "markdown table")]
    [InlineData(OutputSection.StepByStep, "step-by-step")]
    [InlineData(OutputSection.Paragraph, "paragraphs")]
    [InlineData(OutputSection.OneLine, "single line")]
    [InlineData(OutputSection.CodeBlock, "code")]
    [InlineData(OutputSection.XML, "XML")]
    [InlineData(OutputSection.CSV, "CSV")]
    [InlineData(OutputSection.YAML, "YAML")]
    public void WithOutputFormat_EachFormatProducesInstructionText(OutputSection format, string expectedSubstring)
    {
        var prompt = PromptComposer.Create()
            .WithOutputFormat(format)
            .Build();
        Assert.Contains(expectedSubstring, prompt, StringComparison.OrdinalIgnoreCase);
    }

    // ───────────────────── WithSection ─────────────────────

    [Fact]
    public void WithSection_AddsCustomSection()
    {
        var prompt = PromptComposer.Create()
            .WithSection("Notes", "Some important notes")
            .WithMarkdownHeaders()
            .Build();
        Assert.Contains("## Notes", prompt);
        Assert.Contains("Some important notes", prompt);
    }

    [Fact]
    public void WithSection_ThrowsOnEmptyLabel()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithSection("", "content"));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithSection("  ", "content"));
    }

    [Fact]
    public void WithSection_ThrowsOnEmptyContent()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithSection("Label", ""));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithSection("Label", null!));
    }

    [Fact]
    public void WithSection_ThrowsOnExceedingMaxSections()
    {
        var composer = PromptComposer.Create();
        for (int i = 0; i < PromptComposer.MaxSections; i++)
            composer.WithSection($"Section {i}", $"Content {i}");

        Assert.Throws<InvalidOperationException>(() =>
            composer.WithSection("Overflow", "Too many"));
    }

    // ───────────────────── WithSeparator ─────────────────────

    [Fact]
    public void WithSeparator_ChangesSeparator()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Persona")
            .WithTask("Task")
            .WithSeparator("\n---\n")
            .Build();
        Assert.Contains("\n---\n", prompt);
    }

    [Fact]
    public void WithSeparator_NullResetsToDefault()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Persona")
            .WithTask("Task")
            .WithSeparator(null!)
            .Build();
        Assert.Contains("\n\n", prompt);
    }

    // ───────────────────── WithMarkdownHeaders ─────────────────────

    [Fact]
    public void WithMarkdownHeaders_AddsHeaders()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Expert coder")
            .WithTask("Write code")
            .WithMarkdownHeaders()
            .Build();
        Assert.Contains("## Persona", prompt);
        Assert.Contains("## Task", prompt);
    }

    [Fact]
    public void WithMarkdownHeaders_DefaultIsOff()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Expert coder")
            .Build();
        Assert.DoesNotContain("## Persona", prompt);
    }

    // ───────────────────── WithClosingInstruction ─────────────────────

    [Fact]
    public void WithClosingInstruction_AppendsAtEnd()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Helper")
            .WithClosingInstruction("Now respond.")
            .Build();
        Assert.EndsWith("Now respond.", prompt);
    }

    [Fact]
    public void WithClosingInstruction_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithClosingInstruction(""));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithClosingInstruction("  "));
        Assert.Throws<ArgumentException>(() =>
            PromptComposer.Create().WithClosingInstruction(null!));
    }

    // ───────────────────── Build ─────────────────────

    [Fact]
    public void Build_EmptyComposerReturnsEmptyString()
    {
        var prompt = PromptComposer.Create().Build();
        Assert.Equal("", prompt);
    }

    [Fact]
    public void Build_PersonaOnly()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("You are an assistant")
            .Build();
        Assert.Equal("You are an assistant", prompt);
    }

    [Fact]
    public void Build_TaskOnly()
    {
        var prompt = PromptComposer.Create()
            .WithTask("Summarize this")
            .Build();
        Assert.Equal("Summarize this", prompt);
    }

    [Fact]
    public void Build_AllSectionsInOrder()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Persona text")
            .WithContext("Context text")
            .WithTask("Task text")
            .WithConstraint("Constraint text")
            .WithExample("Ex input", "Ex output")
            .WithOutputFormat(OutputSection.JSON)
            .WithSection("Custom", "Custom content")
            .WithClosingInstruction("Closing text")
            .WithMarkdownHeaders()
            .Build();

        var personaIndex = prompt.IndexOf("Persona text");
        var contextIndex = prompt.IndexOf("Context text");
        var taskIndex = prompt.IndexOf("Task text");
        var constraintIndex = prompt.IndexOf("Constraint text");
        var exampleIndex = prompt.IndexOf("Ex input");
        var formatIndex = prompt.IndexOf("JSON");
        var customIndex = prompt.IndexOf("Custom content");
        var closingIndex = prompt.IndexOf("Closing text");

        Assert.True(personaIndex < contextIndex);
        Assert.True(contextIndex < taskIndex);
        Assert.True(taskIndex < constraintIndex);
        Assert.True(constraintIndex < exampleIndex);
        Assert.True(exampleIndex < formatIndex);
        Assert.True(formatIndex < customIndex);
        Assert.True(customIndex < closingIndex);
    }

    [Fact]
    public void Build_ConstraintsAsBulletList()
    {
        var prompt = PromptComposer.Create()
            .WithConstraint("First")
            .WithConstraint("Second")
            .Build();
        Assert.Contains("- First\n- Second", prompt);
    }

    [Fact]
    public void Build_ExamplesNumbered()
    {
        var prompt = PromptComposer.Create()
            .WithExample("In1", "Out1")
            .WithExample("In2", "Out2")
            .Build();
        Assert.Contains("Example 1:", prompt);
        Assert.Contains("Example 2:", prompt);
    }

    [Fact]
    public void Build_CustomSeparator()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("P")
            .WithTask("T")
            .WithSeparator(" | ")
            .Build();
        Assert.Equal("P | T", prompt);
    }

    [Fact]
    public void Build_MarkdownHeaders()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Persona")
            .WithContext("Context")
            .WithTask("Task")
            .WithConstraint("Rule")
            .WithExample("In", "Out")
            .WithOutputFormat(OutputSection.JSON)
            .WithMarkdownHeaders()
            .Build();

        Assert.Contains("## Persona\nPersona", prompt);
        Assert.Contains("## Context\nContext", prompt);
        Assert.Contains("## Task\nTask", prompt);
        Assert.Contains("## Constraints", prompt);
        Assert.Contains("## Examples", prompt);
        Assert.Contains("## Output Format", prompt);
    }

    [Fact]
    public void Build_ClosingInstructionLast()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Persona")
            .WithTask("Task")
            .WithClosingInstruction("Do it now.")
            .Build();
        Assert.True(prompt.EndsWith("Do it now."));
    }

    [Fact]
    public void Build_ThrowsOnExceedingMaxTotalLength()
    {
        // Build a prompt that exceeds MaxTotalLength via many large custom sections
        var composer = PromptComposer.Create();
        var largeContent = new string('x', PromptComposer.MaxSectionLength);
        // 5 sections × 50,000 = 250,000 > 200,000
        for (int i = 0; i < 5; i++)
            composer.WithSection($"Section{i}", largeContent);

        Assert.Throws<InvalidOperationException>(() => composer.Build());
    }

    // ───────────────────── BuildWithTokenEstimate ─────────────────────

    [Fact]
    public void BuildWithTokenEstimate_ReturnsPromptAndPositiveTokenCount()
    {
        var (prompt, tokens) = PromptComposer.Create()
            .WithPersona("You are a helpful assistant")
            .WithTask("Answer questions")
            .BuildWithTokenEstimate();

        Assert.NotEmpty(prompt);
        Assert.True(tokens > 0);
    }

    [Fact]
    public void BuildWithTokenEstimate_TokenCountMatchesEstimateTokens()
    {
        var composer = PromptComposer.Create()
            .WithPersona("Expert reviewer")
            .WithTask("Review code")
            .WithConstraint("Be concise");

        var (prompt, tokens) = composer.BuildWithTokenEstimate();
        var expected = PromptGuard.EstimateTokens(prompt);
        Assert.Equal(expected, tokens);
    }

    // ───────────────────── SectionCount ─────────────────────

    [Fact]
    public void SectionCount_CountsAllSetSections()
    {
        var composer = PromptComposer.Create()
            .WithPersona("P")
            .WithContext("C")
            .WithTask("T")
            .WithConstraint("R")
            .WithExample("I", "O")
            .WithOutputFormat(OutputSection.JSON)
            .WithClosingInstruction("End");

        Assert.Equal(7, composer.SectionCount());
    }

    [Fact]
    public void SectionCount_CustomSectionsCounted()
    {
        var composer = PromptComposer.Create()
            .WithSection("A", "Content A")
            .WithSection("B", "Content B");

        Assert.Equal(2, composer.SectionCount());
    }

    // ───────────────────── Clone ─────────────────────

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = PromptComposer.Create()
            .WithPersona("Original persona")
            .WithTask("Original task");

        var clone = original.Clone();
        Assert.NotSame(original, clone);

        var originalPrompt = original.Build();
        var clonePrompt = clone.Build();
        Assert.Equal(originalPrompt, clonePrompt);
    }

    [Fact]
    public void Clone_ModifyingCloneDoesNotAffectOriginal()
    {
        var original = PromptComposer.Create()
            .WithPersona("Original");

        var clone = original.Clone();
        clone.WithPersona("Modified");

        var originalPrompt = original.Build();
        Assert.Contains("Original", originalPrompt);
        Assert.DoesNotContain("Modified", originalPrompt);
    }

    [Fact]
    public void Clone_PreservesAllFields()
    {
        var original = PromptComposer.Create()
            .WithPersona("Persona")
            .WithContext("Context")
            .WithTask("Task")
            .WithConstraint("Constraint")
            .WithExample("In", "Out")
            .WithOutputFormat(OutputSection.BulletList)
            .WithSection("Custom", "Content")
            .WithSeparator("\n---\n")
            .WithMarkdownHeaders()
            .WithClosingInstruction("End");

        var clone = original.Clone();
        Assert.Equal(original.Build(), clone.Build());
        Assert.Equal(original.SectionCount(), clone.SectionCount());
    }

    // ───────────────────── Reset ─────────────────────

    [Fact]
    public void Reset_ClearsAllFields()
    {
        var composer = PromptComposer.Create()
            .WithPersona("P")
            .WithContext("C")
            .WithTask("T")
            .WithConstraint("R")
            .WithExample("I", "O")
            .WithOutputFormat(OutputSection.JSON)
            .WithSection("S", "Content")
            .WithMarkdownHeaders()
            .WithClosingInstruction("End");

        composer.Reset();
        Assert.Equal("", composer.Build());
    }

    [Fact]
    public void Reset_ReturnsSameInstance()
    {
        var composer = PromptComposer.Create();
        var returned = composer.Reset();
        Assert.Same(composer, returned);
    }

    [Fact]
    public void Reset_SectionCountIsZeroAfterReset()
    {
        var composer = PromptComposer.Create()
            .WithPersona("P")
            .WithTask("T")
            .WithConstraint("R");

        composer.Reset();
        Assert.Equal(0, composer.SectionCount());
    }

    // ───────────────────── ToJson / FromJson ─────────────────────

    [Fact]
    public void ToJson_FromJson_RoundTripsAllFields()
    {
        var original = PromptComposer.Create()
            .WithPersona("Persona")
            .WithContext("Context")
            .WithTask("Task")
            .WithConstraint("Rule 1")
            .WithConstraint("Rule 2")
            .WithExample("Input 1", "Output 1")
            .WithOutputFormat(OutputSection.BulletList)
            .WithSection("Custom", "Content")
            .WithSeparator("\n---\n")
            .WithMarkdownHeaders()
            .WithClosingInstruction("Closing");

        var json = original.ToJson();
        var restored = PromptComposer.FromJson(json);

        Assert.Equal(original.Build(), restored.Build());
    }

    [Fact]
    public void FromJson_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => PromptComposer.FromJson(""));
        Assert.Throws<ArgumentException>(() => PromptComposer.FromJson(null!));
        Assert.Throws<ArgumentException>(() => PromptComposer.FromJson("  "));
    }

    [Fact]
    public void FromJson_HandlesMissingFieldsGracefully()
    {
        var json = "{}";
        var composer = PromptComposer.FromJson(json);
        Assert.Equal("", composer.Build());
        Assert.Equal(0, composer.SectionCount());
    }

    [Fact]
    public void ToJson_SkipsNullValuesInOutput()
    {
        var json = PromptComposer.Create()
            .WithPersona("Test")
            .ToJson();

        Assert.Contains("persona", json);
        Assert.DoesNotContain("\"context\"", json);
        Assert.DoesNotContain("\"task\"", json);
        Assert.DoesNotContain("\"constraints\"", json);
        Assert.DoesNotContain("\"examples\"", json);
    }

    // ───────────────────── Presets ─────────────────────

    [Fact]
    public void Preset_CodeReview_HasPersonaAndConstraints()
    {
        var composer = PromptComposer.CodeReview();
        var prompt = composer.Build();
        Assert.Contains("code reviewer", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("- Focus on bugs", prompt);
        Assert.True(composer.SectionCount() >= 3); // persona, constraints, outputFormat
    }

    [Fact]
    public void Preset_Summarize_HasPersonaAndFormat()
    {
        var composer = PromptComposer.Summarize();
        var prompt = composer.Build();
        Assert.Contains("summaries", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("paragraphs", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Preset_Extract_HasJsonFormat()
    {
        var composer = PromptComposer.Extract();
        var prompt = composer.Build();
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void Preset_Translate_HasPersonaAndFormat()
    {
        var composer = PromptComposer.Translate();
        var prompt = composer.Build();
        Assert.Contains("translator", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("paragraphs", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Presets_CanBeFurtherCustomized()
    {
        var composer = PromptComposer.CodeReview()
            .WithContext("Reviewing a Python project")
            .WithTask("Find security vulnerabilities");

        var prompt = composer.Build();
        Assert.Contains("code reviewer", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Python project", prompt);
        Assert.Contains("security vulnerabilities", prompt);
    }

    // ───────────────────── Additional edge cases ─────────────────────

    [Fact]
    public void Build_MultipleExamplesFormatted()
    {
        var prompt = PromptComposer.Create()
            .WithExample("Q1", "A1")
            .WithExample("Q2", "A2")
            .WithExample("Q3", "A3")
            .Build();
        Assert.Contains("Example 1:", prompt);
        Assert.Contains("Example 2:", prompt);
        Assert.Contains("Example 3:", prompt);
        Assert.Contains("Input: Q1", prompt);
        Assert.Contains("Output: A3", prompt);
    }

    [Fact]
    public void WithMarkdownHeaders_DisableAfterEnable()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("Test")
            .WithMarkdownHeaders(true)
            .WithMarkdownHeaders(false)
            .Build();
        Assert.DoesNotContain("## Persona", prompt);
    }

    [Fact]
    public void FluentChaining_AllMethodsReturnSameInstance()
    {
        var composer = PromptComposer.Create();
        var result = composer
            .WithPersona("P")
            .WithContext("C")
            .WithTask("T")
            .WithConstraint("R")
            .WithExample("I", "O")
            .WithOutputFormat(OutputSection.JSON)
            .WithSection("S", "Content")
            .WithSeparator("\n")
            .WithMarkdownHeaders()
            .WithClosingInstruction("End");

        Assert.Same(composer, result);
    }

    [Fact]
    public void WithPersona_OverwritesPrevious()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("First persona")
            .WithPersona("Second persona")
            .Build();
        Assert.DoesNotContain("First persona", prompt);
        Assert.Contains("Second persona", prompt);
    }

    [Fact]
    public void WithContext_OverwritesPrevious()
    {
        var prompt = PromptComposer.Create()
            .WithContext("First context")
            .WithContext("Second context")
            .Build();
        Assert.DoesNotContain("First context", prompt);
        Assert.Contains("Second context", prompt);
    }

    [Fact]
    public void WithTask_OverwritesPrevious()
    {
        var prompt = PromptComposer.Create()
            .WithTask("First task")
            .WithTask("Second task")
            .Build();
        Assert.DoesNotContain("First task", prompt);
        Assert.Contains("Second task", prompt);
    }

    [Fact]
    public void Clone_ConstraintIndependence()
    {
        var original = PromptComposer.Create()
            .WithConstraint("Original constraint");

        var clone = original.Clone();
        clone.WithConstraint("Clone constraint");

        var originalPrompt = original.Build();
        Assert.DoesNotContain("Clone constraint", originalPrompt);
    }

    [Fact]
    public void Clone_ExampleIndependence()
    {
        var original = PromptComposer.Create()
            .WithExample("Q1", "A1");

        var clone = original.Clone();
        clone.WithExample("Q2", "A2");

        var originalPrompt = original.Build();
        Assert.DoesNotContain("Q2", originalPrompt);
    }

    [Fact]
    public void Clone_CustomSectionIndependence()
    {
        var original = PromptComposer.Create()
            .WithSection("S1", "C1");

        var clone = original.Clone();
        clone.WithSection("S2", "C2");

        Assert.Equal(1, original.SectionCount());
        Assert.Equal(2, clone.SectionCount());
    }

    [Fact]
    public void SectionCount_EmptyComposerIsZero()
    {
        Assert.Equal(0, PromptComposer.Create().SectionCount());
    }

    [Fact]
    public void SectionCount_OnlyPersona()
    {
        Assert.Equal(1, PromptComposer.Create().WithPersona("P").SectionCount());
    }

    [Fact]
    public void SectionCount_OnlyConstraints()
    {
        Assert.Equal(1, PromptComposer.Create().WithConstraint("C").SectionCount());
    }

    [Fact]
    public void SectionCount_OnlyExamples()
    {
        Assert.Equal(1, PromptComposer.Create().WithExample("I", "O").SectionCount());
    }

    [Fact]
    public void FromJson_PartialFields()
    {
        var json = "{\"persona\": \"Test persona\", \"task\": \"Do something\"}";
        var composer = PromptComposer.FromJson(json);
        var prompt = composer.Build();
        Assert.Contains("Test persona", prompt);
        Assert.Contains("Do something", prompt);
        Assert.Equal(2, composer.SectionCount());
    }

    [Fact]
    public void FromJson_WithOutputFormat()
    {
        var json = "{\"outputFormat\": \"JSON\"}";
        var composer = PromptComposer.FromJson(json);
        var prompt = composer.Build();
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void ToJson_FromJson_RoundTripsOutputFormat()
    {
        var original = PromptComposer.Create()
            .WithOutputFormat(OutputSection.YAML);

        var json = original.ToJson();
        var restored = PromptComposer.FromJson(json);
        Assert.Equal(original.Build(), restored.Build());
    }

    [Fact]
    public void ToJson_FromJson_EmptyComposer()
    {
        var original = PromptComposer.Create();
        var json = original.ToJson();
        var restored = PromptComposer.FromJson(json);
        Assert.Equal("", restored.Build());
    }

    [Fact]
    public void WithSection_ContentAppearsInBuild()
    {
        var prompt = PromptComposer.Create()
            .WithSection("Background", "Important background info")
            .Build();
        Assert.Contains("Important background info", prompt);
    }

    [Fact]
    public void WithExample_TrimsInputAndOutput()
    {
        var prompt = PromptComposer.Create()
            .WithExample("  input  ", "  output  ")
            .Build();
        Assert.Contains("Input: input", prompt);
        Assert.Contains("Output: output", prompt);
    }

    [Fact]
    public void Build_WithOnlyClosingInstruction()
    {
        var prompt = PromptComposer.Create()
            .WithClosingInstruction("Just the ending")
            .Build();
        Assert.Equal("Just the ending", prompt);
    }

    [Fact]
    public void Build_WithOnlyOutputFormat()
    {
        var prompt = PromptComposer.Create()
            .WithOutputFormat(OutputSection.OneLine)
            .Build();
        Assert.Contains("single line", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
