namespace Prompt.Tests;

using System;
using Prompt;
using Xunit;

/// <summary>
/// Behavioural tests for <see cref="ProviderFactory.Create(string, int)"/>: provider-name
/// normalization, the azure/default fallback, and the missing-credential / unknown-provider
/// error contracts. Each test snapshots and restores the handful of process-scoped
/// environment variables it touches so the suite stays order-independent and leaves no
/// global state behind. These run on <see cref="System.Collections.Generic"/>-free, fully
/// offline paths — no provider here opens a socket at construction time.
/// </summary>
[Collection("ProviderFactoryEnv")]
public class ProviderFactoryTests : IDisposable
{
    private static readonly string[] TouchedVars =
    {
        ProviderFactory.ProviderEnvVar,
        ProviderFactory.ApiKeyEnvVar,
        ProviderFactory.ModelEnvVar,
        ProviderFactory.BaseUrlEnvVar,
        "OPENAI_API_KEY", "ANTHROPIC_API_KEY", "GEMINI_API_KEY", "GOOGLE_API_KEY",
        "XAI_API_KEY", "GROK_API_KEY", "OLLAMA_API_KEY",
    };

    private readonly (string Name, string? Value)[] _snapshot;

    public ProviderFactoryTests()
    {
        _snapshot = Array.ConvertAll(TouchedVars,
            n => (n, Environment.GetEnvironmentVariable(n, EnvironmentVariableTarget.Process)));
        // Start every test from a clean slate so an ambient dev credential can't leak in.
        foreach (var n in TouchedVars) Set(n, null);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _snapshot) Set(name, value);
    }

    private static void Set(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);

    // ── azure / default fallback ──────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("azure")]
    [InlineData("AzureOpenAI")]
    [InlineData("azure-openai")]
    [InlineData("  Azure  ")]
    public void Create_AzureOrDefault_ReturnsAzureProvider(string? provider)
    {
        // Azure reads AZURE_OPENAI_* lazily (not at construction), so this never needs creds.
        var p = ProviderFactory.Create(provider!);
        Assert.IsType<AzureOpenAIProvider>(p);
    }

    // ── normalization: trim + case-fold feeds matching AND credential lookup ──

    [Fact]
    public void Create_NormalizesName_WhitespaceAndCasingResolveSameProvider()
    {
        Set(ProviderFactory.ApiKeyEnvVar, "k-123");
        Set(ProviderFactory.ModelEnvVar, "gpt-4o-mini");

        // "  OpenAI " must resolve exactly like "openai"; before the normalization fix the
        // raw argument (odd casing/whitespace) reached the credential helper and error text.
        var a = ProviderFactory.Create("  OpenAI ");
        var b = ProviderFactory.Create("openai");
        Assert.IsType<OpenAICompatProvider>(a);
        Assert.IsType<OpenAICompatProvider>(b);
    }

    // ── unknown provider: echo the RAW value the caller set (typo visibility) ──

    [Fact]
    public void Create_UnknownProvider_ThrowsEchoingRawValue()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ProviderFactory.Create("  FooBar "));
        // The raw, un-normalized spelling is echoed so a caller sees their actual typo.
        Assert.Contains("FooBar", ex.Message);
        Assert.Contains("Supported:", ex.Message);
    }

    // ── missing credentials: error names the NORMALIZED provider, lists the vars ──

    [Fact]
    public void Create_MissingApiKey_ThrowsNamingNormalizedProviderAndVars()
    {
        Set(ProviderFactory.ModelEnvVar, "gpt-4o-mini"); // model present, key absent

        var ex = Assert.Throws<InvalidOperationException>(() => ProviderFactory.Create(" OpenAI "));
        Assert.Contains("'openai'", ex.Message);              // normalized, not " OpenAI "
        Assert.Contains(ProviderFactory.ApiKeyEnvVar, ex.Message);
        Assert.Contains("OPENAI_API_KEY", ex.Message);
    }

    [Fact]
    public void Create_MissingModel_ThrowsNamingNormalizedProvider()
    {
        Set(ProviderFactory.ApiKeyEnvVar, "k-123"); // key present, model absent

        var ex = Assert.Throws<InvalidOperationException>(() => ProviderFactory.Create("GEMINI"));
        Assert.Contains("'gemini'", ex.Message);
        Assert.Contains(ProviderFactory.ModelEnvVar, ex.Message);
    }

    [Fact]
    public void Create_Grok_MissingKey_ListsBothConventionalVars()
    {
        Set(ProviderFactory.ModelEnvVar, "grok-2");

        var ex = Assert.Throws<InvalidOperationException>(() => ProviderFactory.Create("xai"));
        // grok/xai accepts either XAI_API_KEY or GROK_API_KEY — both must be named.
        Assert.Contains("'xai'", ex.Message);
        Assert.Contains("XAI_API_KEY", ex.Message);
        Assert.Contains("GROK_API_KEY", ex.Message);
    }

    // ── ollama: key is optional, only a model is required ─────────────────

    [Fact]
    public void Create_Ollama_RequiresOnlyModel()
    {
        Set(ProviderFactory.ModelEnvVar, "llama3.1");
        var p = ProviderFactory.Create("ollama");
        Assert.IsType<OpenAICompatProvider>(p);
    }
}
