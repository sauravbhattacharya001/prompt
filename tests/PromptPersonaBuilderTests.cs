namespace Prompt.Tests;

using Xunit;

public class PromptPersonaBuilderTests
{
    [Fact]
    public void Build_WithRequiredFields_ReturnsPersonaResult()
    {
        var persona = new PromptPersonaBuilder()
            .WithName("TestBot")
            .WithRole("Test assistant")
            .Build();

        Assert.Equal("TestBot", persona.Name);
        Assert.Equal("Test assistant", persona.Role);
    }

    [Fact]
    public void Build_WithoutName_Throws()
    {
        var builder = new PromptPersonaBuilder().WithRole("Role");
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithoutRole_Throws()
    {
        var builder = new PromptPersonaBuilder().WithName("Name");
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Render_IncludesAllSections()
    {
        var persona = new PromptPersonaBuilder()
            .WithName("Coder")
            .WithRole("Software engineer")
            .WithBackground("10 years experience")
            .WithTone(PersonaTone.Professional)
            .AddDomain("C#", 9, "Deep .NET knowledge")
            .AddConstraint("Be helpful", "must", "User satisfaction")
            .ForAudience("developers")
            .WithResponseFormat("Use code blocks")
            .AddExamplePhrase("Let me take a look...")
            .AddSection("Ethics", "Be honest")
            .Build();

        var rendered = persona.Render();
        Assert.Contains("# Persona: Coder", rendered);
        Assert.Contains("Software engineer", rendered);
        Assert.Contains("10 years experience", rendered);
        Assert.Contains("C#", rendered);
        Assert.Contains("█████████░", rendered);
        Assert.Contains("Expert", rendered);
        Assert.Contains("Be helpful", rendered);
        Assert.Contains("developers", rendered);
        Assert.Contains("Use code blocks", rendered);
        Assert.Contains("Let me take a look...", rendered);
        Assert.Contains("Ethics", rendered);
    }

    [Fact]
    public void RenderCompact_IsShorter()
    {
        var persona = new PromptPersonaBuilder()
            .WithName("Bot")
            .WithRole("Helper")
            .WithBackground("Experienced")
            .AddDomain("Testing", 7)
            .AddConstraint("Be nice")
            .Build();

        var full = persona.Render();
        var compact = persona.RenderCompact();
        Assert.True(compact.Length < full.Length);
        Assert.Contains("Bot", compact);
    }

    [Fact]
    public void ToJson_And_FromJson_Roundtrip()
    {
        var original = new PromptPersonaBuilder()
            .WithName("JsonBot")
            .WithRole("Tester")
            .AddDomain("JSON", 8)
            .Build();

        var json = original.ToJson();
        var restored = PromptPersonaBuilder.FromJson(json);
        Assert.Equal("JsonBot", restored.Name);
        Assert.Equal("Tester", restored.Role);
    }

    [Fact]
    public void AddDomain_InvalidLevel_Throws()
    {
        var builder = new PromptPersonaBuilder();
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddDomain("X", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddDomain("X", 11));
    }

    [Fact]
    public void MergeWith_CombinesTraits()
    {
        var a = new PromptPersonaBuilder()
            .WithName("A").WithRole("RoleA")
            .AddDomain("D1", 5)
            .AddConstraint("R1")
            .Build();

        var b = new PromptPersonaBuilder()
            .WithName("B").WithRole("RoleB")
            .AddDomain("D2", 7)
            .AddConstraint("R2")
            .Build();

        var merged = a.MergeWith(b);
        Assert.Equal("A", merged.Name); // keeps original name
        Assert.Equal(2, merged.Definition.Domains.Count);
        Assert.Equal(2, merged.Definition.Constraints.Count);
    }

    [Fact]
    public void Presets_CodingAssistant_Renders()
    {
        var persona = PromptPersonaBuilder.Presets.CodingAssistant();
        var rendered = persona.Render();
        Assert.Contains("CodingAssistant", rendered);
        Assert.Contains("Software Engineering", rendered);
    }

    [Fact]
    public void Presets_Tutor_Renders()
    {
        var persona = PromptPersonaBuilder.Presets.Tutor();
        Assert.Contains("Socratic", persona.Render());
    }

    [Fact]
    public void Presets_TechnicalWriter_Renders()
    {
        var persona = PromptPersonaBuilder.Presets.TechnicalWriter();
        Assert.Contains("TechnicalWriter", persona.Render());
    }

    [Fact]
    public void Presets_CreativeWriter_Renders()
    {
        var persona = PromptPersonaBuilder.Presets.CreativeWriter();
        Assert.Contains("CreativeWriter", persona.Render());
    }

    [Fact]
    public void EstimateTokens_ReturnsPositiveValue()
    {
        var persona = new PromptPersonaBuilder()
            .WithName("T").WithRole("R")
            .Build();
        Assert.True(persona.EstimateTokens() > 0);
        Assert.True(persona.EstimateTokens(compact: true) > 0);
    }

    [Fact]
    public void KnowledgeDomain_ExpertiseLabels_AreCorrect()
    {
        Assert.Equal("Novice", new KnowledgeDomain("X", 1).ExpertiseLabel);
        Assert.Equal("Intermediate", new KnowledgeDomain("X", 3).ExpertiseLabel);
        Assert.Equal("Advanced", new KnowledgeDomain("X", 5).ExpertiseLabel);
        Assert.Equal("Expert", new KnowledgeDomain("X", 8).ExpertiseLabel);
        Assert.Equal("World-class", new KnowledgeDomain("X", 10).ExpertiseLabel);
    }
}
