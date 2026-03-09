namespace Prompt.Tests;

using Xunit;

/// <summary>
/// Tests for <see cref="StreamChunk"/> — init-only properties,
/// defaults, and value semantics.
/// </summary>
public class StreamChunkTests
{
    // ── Default values ──────────────────────────────────────────────

    [Fact]
    public void DefaultDelta_IsEmptyString()
    {
        var chunk = new StreamChunk();
        Assert.Equal("", chunk.Delta);
    }

    [Fact]
    public void DefaultFullText_IsEmptyString()
    {
        var chunk = new StreamChunk();
        Assert.Equal("", chunk.FullText);
    }

    [Fact]
    public void DefaultIsComplete_IsFalse()
    {
        var chunk = new StreamChunk();
        Assert.False(chunk.IsComplete);
    }

    [Fact]
    public void DefaultFinishReason_IsNull()
    {
        var chunk = new StreamChunk();
        Assert.Null(chunk.FinishReason);
    }

    [Fact]
    public void DefaultTokensUsed_IsZero()
    {
        var chunk = new StreamChunk();
        Assert.Equal(0, chunk.TokensUsed);
    }

    // ── Init-only property assignment ───────────────────────────────

    [Fact]
    public void CanSetDelta()
    {
        var chunk = new StreamChunk { Delta = "hello " };
        Assert.Equal("hello ", chunk.Delta);
    }

    [Fact]
    public void CanSetFullText()
    {
        var chunk = new StreamChunk { FullText = "hello world" };
        Assert.Equal("hello world", chunk.FullText);
    }

    [Fact]
    public void CanSetIsComplete()
    {
        var chunk = new StreamChunk { IsComplete = true };
        Assert.True(chunk.IsComplete);
    }

    [Fact]
    public void CanSetFinishReason_Stop()
    {
        var chunk = new StreamChunk { FinishReason = "stop" };
        Assert.Equal("stop", chunk.FinishReason);
    }

    [Fact]
    public void CanSetFinishReason_Length()
    {
        var chunk = new StreamChunk { FinishReason = "length" };
        Assert.Equal("length", chunk.FinishReason);
    }

    [Fact]
    public void CanSetTokensUsed()
    {
        var chunk = new StreamChunk { TokensUsed = 42 };
        Assert.Equal(42, chunk.TokensUsed);
    }

    // ── Full construction ───────────────────────────────────────────

    [Fact]
    public void FullConstruction_SetsAllProperties()
    {
        var chunk = new StreamChunk
        {
            Delta = " world",
            FullText = "hello world",
            IsComplete = true,
            FinishReason = "stop",
            TokensUsed = 5
        };

        Assert.Equal(" world", chunk.Delta);
        Assert.Equal("hello world", chunk.FullText);
        Assert.True(chunk.IsComplete);
        Assert.Equal("stop", chunk.FinishReason);
        Assert.Equal(5, chunk.TokensUsed);
    }

    // ── Intermediate chunk (not complete) ───────────────────────────

    [Fact]
    public void IntermediateChunk_HasNoFinishReasonAndNotComplete()
    {
        var chunk = new StreamChunk
        {
            Delta = "partial",
            FullText = "some partial",
            TokensUsed = 3
        };

        Assert.False(chunk.IsComplete);
        Assert.Null(chunk.FinishReason);
    }

    // ── Final chunk ─────────────────────────────────────────────────

    [Fact]
    public void FinalChunk_HasFinishReasonAndIsComplete()
    {
        var chunk = new StreamChunk
        {
            Delta = "",
            FullText = "complete response",
            IsComplete = true,
            FinishReason = "stop",
            TokensUsed = 10
        };

        Assert.True(chunk.IsComplete);
        Assert.Equal("stop", chunk.FinishReason);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void EmptyDelta_OnFinalChunk_IsValid()
    {
        var chunk = new StreamChunk
        {
            Delta = "",
            FullText = "all done",
            IsComplete = true,
            FinishReason = "stop"
        };
        Assert.Equal("", chunk.Delta);
    }

    [Fact]
    public void LargeTokensUsed_IsValid()
    {
        var chunk = new StreamChunk { TokensUsed = int.MaxValue };
        Assert.Equal(int.MaxValue, chunk.TokensUsed);
    }

    [Fact]
    public void MultipleDeltasAccumulate_InSeparateInstances()
    {
        var chunk1 = new StreamChunk { Delta = "Hello", FullText = "Hello", TokensUsed = 1 };
        var chunk2 = new StreamChunk { Delta = " world", FullText = "Hello world", TokensUsed = 2 };
        var chunk3 = new StreamChunk
        {
            Delta = "!", FullText = "Hello world!",
            IsComplete = true, FinishReason = "stop", TokensUsed = 3
        };

        Assert.Equal("Hello", chunk1.Delta);
        Assert.Equal("Hello world", chunk2.FullText);
        Assert.Equal("Hello world!", chunk3.FullText);
        Assert.True(chunk3.IsComplete);
    }
}
