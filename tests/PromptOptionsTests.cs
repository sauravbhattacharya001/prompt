namespace Prompt.Tests;

using System;
using System.Text.Json;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptOptions"/> — the configurable model
/// parameter class introduced to fix issue #8.
/// Converted from MSTest to xUnit for consistency with the test project.
/// </summary>
public class PromptOptionsTests
{
    // ──────────────── Defaults ────────────────

    [Fact]
    public void Defaults_MatchLegacyHardcodedValues()
    {
        var opts = new PromptOptions();
        Assert.Equal(0.7f, opts.Temperature);
        Assert.Equal(800, opts.MaxTokens);
        Assert.Equal(0.95f, opts.TopP);
        Assert.Equal(0f, opts.FrequencyPenalty);
        Assert.Equal(0f, opts.PresencePenalty);
    }

    // ──────────────── Temperature validation ────────────────

    [Fact]
    public void Temperature_ZeroIsValid()
    {
        var opts = new PromptOptions { Temperature = 0f };
        Assert.Equal(0f, opts.Temperature);
    }

    [Fact]
    public void Temperature_TwoIsValid()
    {
        var opts = new PromptOptions { Temperature = 2f };
        Assert.Equal(2f, opts.Temperature);
    }

    [Fact]
    public void Temperature_NegativeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { Temperature = -0.1f });
    }

    [Fact]
    public void Temperature_AboveTwoThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { Temperature = 2.01f });
    }

    // ──────────────── MaxTokens validation ────────────────

    [Fact]
    public void MaxTokens_OneIsValid()
    {
        var opts = new PromptOptions { MaxTokens = 1 };
        Assert.Equal(1, opts.MaxTokens);
    }

    [Fact]
    public void MaxTokens_LargeValueIsValid()
    {
        var opts = new PromptOptions { MaxTokens = 128000 };
        Assert.Equal(128000, opts.MaxTokens);
    }

    [Fact]
    public void MaxTokens_ZeroThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { MaxTokens = 0 });
    }

    [Fact]
    public void MaxTokens_NegativeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { MaxTokens = -1 });
    }

    // ──────────────── TopP validation ────────────────

    [Fact]
    public void TopP_ZeroIsValid()
    {
        var opts = new PromptOptions { TopP = 0f };
        Assert.Equal(0f, opts.TopP);
    }

    [Fact]
    public void TopP_OneIsValid()
    {
        var opts = new PromptOptions { TopP = 1f };
        Assert.Equal(1f, opts.TopP);
    }

    [Fact]
    public void TopP_NegativeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { TopP = -0.01f });
    }

    [Fact]
    public void TopP_AboveOneThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { TopP = 1.01f });
    }

    // ──────────────── FrequencyPenalty validation ────────────────

    [Fact]
    public void FrequencyPenalty_NegativeTwoIsValid()
    {
        var opts = new PromptOptions { FrequencyPenalty = -2f };
        Assert.Equal(-2f, opts.FrequencyPenalty);
    }

    [Fact]
    public void FrequencyPenalty_TwoIsValid()
    {
        var opts = new PromptOptions { FrequencyPenalty = 2f };
        Assert.Equal(2f, opts.FrequencyPenalty);
    }

    [Fact]
    public void FrequencyPenalty_BelowNegativeTwoThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { FrequencyPenalty = -2.01f });
    }

    [Fact]
    public void FrequencyPenalty_AboveTwoThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { FrequencyPenalty = 2.01f });
    }

    // ──────────────── PresencePenalty validation ────────────────

    [Fact]
    public void PresencePenalty_NegativeTwoIsValid()
    {
        var opts = new PromptOptions { PresencePenalty = -2f };
        Assert.Equal(-2f, opts.PresencePenalty);
    }

    [Fact]
    public void PresencePenalty_TwoIsValid()
    {
        var opts = new PromptOptions { PresencePenalty = 2f };
        Assert.Equal(2f, opts.PresencePenalty);
    }

    [Fact]
    public void PresencePenalty_BelowNegativeTwoThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { PresencePenalty = -2.01f });
    }

    [Fact]
    public void PresencePenalty_AboveTwoThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptOptions { PresencePenalty = 2.01f });
    }

    // ──────────────── Factory presets ────────────────

    [Fact]
    public void ForCodeGeneration_HasLowTempHighTokens()
    {
        var opts = PromptOptions.ForCodeGeneration();
        Assert.Equal(0.1f, opts.Temperature);
        Assert.Equal(4000, opts.MaxTokens);
        Assert.Equal(0.95f, opts.TopP);
        Assert.Equal(0f, opts.FrequencyPenalty);
        Assert.Equal(0f, opts.PresencePenalty);
    }

    [Fact]
    public void ForCreativeWriting_HasHighTempMedTokens()
    {
        var opts = PromptOptions.ForCreativeWriting();
        Assert.Equal(0.9f, opts.Temperature);
        Assert.Equal(2000, opts.MaxTokens);
        Assert.Equal(0.9f, opts.TopP);
    }

    [Fact]
    public void ForDataExtraction_HasZeroTemp()
    {
        var opts = PromptOptions.ForDataExtraction();
        Assert.Equal(0f, opts.Temperature);
        Assert.Equal(2000, opts.MaxTokens);
        Assert.Equal(1f, opts.TopP);
    }

    [Fact]
    public void ForSummarization_HasLowTemp()
    {
        var opts = PromptOptions.ForSummarization();
        Assert.Equal(0.3f, opts.Temperature);
        Assert.Equal(1000, opts.MaxTokens);
        Assert.Equal(0.9f, opts.TopP);
    }

    // ──────────────── JSON serialization ────────────────

    [Fact]
    public void PromptOptions_RoundTripsViaJson()
    {
        var original = new PromptOptions
        {
            Temperature = 0.3f,
            MaxTokens = 4000,
            TopP = 0.85f,
            FrequencyPenalty = 0.5f,
            PresencePenalty = -0.5f
        };

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PromptOptions>(json)!;

        Assert.Equal(original.Temperature, restored.Temperature);
        Assert.Equal(original.MaxTokens, restored.MaxTokens);
        Assert.Equal(original.TopP, restored.TopP);
        Assert.Equal(original.FrequencyPenalty, restored.FrequencyPenalty);
        Assert.Equal(original.PresencePenalty, restored.PresencePenalty);
    }

    // ──────────────── Conversation integration ────────────────

    [Fact]
    public void Conversation_AcceptsPromptOptions()
    {
        var opts = PromptOptions.ForCodeGeneration();
        var conv = new Conversation("You are a coder.", opts);

        Assert.Equal(0.1f, conv.Temperature);
        Assert.Equal(4000, conv.MaxTokens);
        Assert.Equal(0.95f, conv.TopP);
        Assert.Equal(0f, conv.FrequencyPenalty);
        Assert.Equal(0f, conv.PresencePenalty);
        Assert.Equal(1, conv.MessageCount);
    }

    [Fact]
    public void Conversation_PromptOptionsWithNullSystemPrompt()
    {
        var opts = new PromptOptions { Temperature = 1.5f, MaxTokens = 100 };
        var conv = new Conversation(null, opts);

        Assert.Equal(1.5f, conv.Temperature);
        Assert.Equal(100, conv.MaxTokens);
        Assert.Equal(0, conv.MessageCount);
    }

    [Fact]
    public void Conversation_NullOptionsThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Conversation("test", (PromptOptions)null!));
    }

    // ──────────────── PromptChain integration ────────────────

    [Fact]
    public void PromptChain_WithOptions_SerializesRoundTrip()
    {
        var chain = new PromptChain()
            .WithSystemPrompt("test system")
            .WithOptions(new PromptOptions
            {
                Temperature = 0.2f,
                MaxTokens = 4000,
                TopP = 0.8f,
                FrequencyPenalty = 0.3f,
                PresencePenalty = 0.1f
            })
            .AddStep("step1",
                new PromptTemplate("Do something with {{input}}"),
                "output");

        string json = chain.ToJson();
        var restored = PromptChain.FromJson(json);

        string json2 = restored.ToJson();

        var doc1 = JsonDocument.Parse(json);
        var doc2 = JsonDocument.Parse(json2);

        var opts1 = doc1.RootElement.GetProperty("options");
        var opts2 = doc2.RootElement.GetProperty("options");

        Assert.Equal(
            opts1.GetProperty("temperature").GetSingle(),
            opts2.GetProperty("temperature").GetSingle());
        Assert.Equal(
            opts1.GetProperty("maxTokens").GetInt32(),
            opts2.GetProperty("maxTokens").GetInt32());
        Assert.Equal(
            opts1.GetProperty("topP").GetSingle(),
            opts2.GetProperty("topP").GetSingle());
        Assert.Equal(
            opts1.GetProperty("frequencyPenalty").GetSingle(),
            opts2.GetProperty("frequencyPenalty").GetSingle());
        Assert.Equal(
            opts1.GetProperty("presencePenalty").GetSingle(),
            opts2.GetProperty("presencePenalty").GetSingle());
    }

    [Fact]
    public void PromptChain_WithoutOptions_OmitsFromJson()
    {
        var chain = new PromptChain()
            .AddStep("step1",
                new PromptTemplate("Do {{thing}}"),
                "result");

        string json = chain.ToJson();
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("options", out _));
    }

    [Fact]
    public void PromptChain_WithOptions_FluentApiReturnsThis()
    {
        var chain = new PromptChain();
        var result = chain.WithOptions(new PromptOptions());
        Assert.Same(chain, result);
    }

    [Fact]
    public void PromptChain_WithNullOptions_ClearsOptions()
    {
        var chain = new PromptChain()
            .WithOptions(new PromptOptions { Temperature = 0.1f })
            .WithOptions(null)
            .AddStep("s", new PromptTemplate("test"), "out");

        string json = chain.ToJson();
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("options", out _));
    }

    // ──────────────── Custom values override defaults ────────────────

    [Fact]
    public void CustomValues_AllSettable()
    {
        var opts = new PromptOptions
        {
            Temperature = 1.8f,
            MaxTokens = 32000,
            TopP = 0.1f,
            FrequencyPenalty = 1.5f,
            PresencePenalty = -1.5f
        };

        Assert.Equal(1.8f, opts.Temperature);
        Assert.Equal(32000, opts.MaxTokens);
        Assert.Equal(0.1f, opts.TopP);
        Assert.Equal(1.5f, opts.FrequencyPenalty);
        Assert.Equal(-1.5f, opts.PresencePenalty);
    }
}
