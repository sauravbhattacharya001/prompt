namespace Prompt.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="Main"/> — input validation, environment variable
/// handling, and client lifecycle.  These tests exercise all code paths
/// that do NOT require an actual Azure OpenAI endpoint.
/// </summary>
public class MainTests : IDisposable
{
    /// <summary>
    /// Ensure the cached client is cleared between tests so that
    /// environment-variable changes take effect.
    /// </summary>
    public MainTests()
    {
        Main.ResetClient();
    }

    public void Dispose()
    {
        // Restore clean state after each test
        Main.ResetClient();
        ClearEnvVars();
    }

    // ───────────────────── Prompt validation ─────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task GetResponseAsync_ThrowsArgumentException_WhenPromptIsNullOrWhitespace(string? prompt)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Main.GetResponseAsync(prompt!));

        Assert.Equal("prompt", ex.ParamName);
        Assert.Contains("cannot be null or empty", ex.Message);
    }

    // ───────────────── maxRetries validation ─────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public async Task GetResponseAsync_ThrowsArgumentOutOfRange_WhenMaxRetriesNegative(int retries)
    {
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Main.GetResponseAsync("test", maxRetries: retries));

        Assert.Equal("maxRetries", ex.ParamName);
        Assert.Contains("non-negative", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_AllowsZeroRetries()
    {
        // maxRetries = 0 is valid — it means "no retries, try once".
        // Should fail with missing env var, NOT with ArgumentOutOfRangeException.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Main.GetResponseAsync("test", maxRetries: 0));
    }

    // ───────────── Environment variable validation ───────────────

    [Fact]
    public async Task GetResponseAsync_ThrowsInvalidOperation_WhenUriNotSet()
    {
        ClearEnvVars();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Main.GetResponseAsync("hello"));

        Assert.Contains("AZURE_OPENAI_API_URI", ex.Message);
        Assert.Contains("not set or is empty", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsInvalidOperation_WhenKeyNotSet()
    {
        ClearEnvVars();
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", "https://test.openai.azure.com/");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Main.GetResponseAsync("hello"));

        Assert.Contains("AZURE_OPENAI_API_KEY", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsInvalidOperation_WhenModelNotSet()
    {
        ClearEnvVars();
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", "https://test.openai.azure.com/");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "test-key-123");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Main.GetResponseAsync("hello"));

        Assert.Contains("AZURE_OPENAI_API_MODEL", ex.Message);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://bad-scheme.com")]
    [InlineData("://missing-scheme")]
    [InlineData("")]
    public async Task GetResponseAsync_ThrowsInvalidOperation_WhenUriIsInvalid(string badUri)
    {
        ClearEnvVars();
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", badUri);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", "gpt-4");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Main.GetResponseAsync("hello"));

        // Empty string triggers "not set or is empty"; others trigger "not a valid HTTP(S) URI"
        Assert.True(
            ex.Message.Contains("not a valid HTTP(S) URI") ||
            ex.Message.Contains("not set or is empty"),
            $"Unexpected message: {ex.Message}");
    }

    // ──────────────── Whitespace env vars treated as unset ────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task GetResponseAsync_TreatsWhitespaceEnvVarAsUnset(string whitespace)
    {
        ClearEnvVars();
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", whitespace);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Main.GetResponseAsync("hello"));

        Assert.Contains("not set or is empty", ex.Message);
    }

    // ──────────────── Cancellation ────────────────

    [Fact]
    public async Task GetResponseAsync_ThrowsOperationCanceled_WhenTokenAlreadyCancelled()
    {
        ClearEnvVars();
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", "https://test.openai.azure.com/");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", "gpt-4");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Main.GetResponseAsync("hello", cancellationToken: cts.Token));
    }

    // ──────────────── ResetClient ────────────────

    [Fact]
    public async Task ResetClient_AllowsRereadingEnvVars()
    {
        ClearEnvVars();
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", "https://test.openai.azure.com/");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", "gpt-4");

        // First call initialises the client (will fail at the network layer
        // but shouldn't throw env-var errors).
        // We catch the expected network/HTTP error.
        try { await Main.GetResponseAsync("hello"); } catch (Exception) { }

        // Now clear the URI and reset
        ClearEnvVars();
        Main.ResetClient();

        // Should throw about missing URI — proves reset worked
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Main.GetResponseAsync("hello again"));

        Assert.Contains("AZURE_OPENAI_API_URI", ex.Message);
    }

    [Fact]
    public void ResetClient_CanBeCalledMultipleTimes()
    {
        // Should not throw
        Main.ResetClient();
        Main.ResetClient();
        Main.ResetClient();
    }

    // ──────────────── Thread safety smoke test ────────────────

    [Fact]
    public async Task GetResponseAsync_HandlesParallelCalls()
    {
        ClearEnvVars();

        // Fire 10 concurrent calls — all should fail consistently with
        // the same env-var error, proving the lock doesn't deadlock.
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Assert.ThrowsAsync<InvalidOperationException>(
                () => Main.GetResponseAsync("concurrent")));

        var results = await Task.WhenAll(tasks);
        Assert.All(results, ex => Assert.Contains("AZURE_OPENAI_API_URI", ex.Message));
    }

    // ──────────────── Helpers ────────────────

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", null);
    }
}
