namespace Prompt.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="Conversation"/> — construction, parameter
/// validation, message management, and history tracking. All tests
/// run without an Azure OpenAI endpoint.
/// </summary>
public class ConversationTests : IDisposable
{
    public ConversationTests()
    {
        Main.ResetClient();
    }

    public void Dispose()
    {
        Main.ResetClient();
        ClearEnvVars();
    }

    // ───────────────────── Construction ─────────────────────

    [Fact]
    public void Constructor_WithoutSystemPrompt_StartsEmpty()
    {
        var conv = new Conversation();
        Assert.Equal(0, conv.MessageCount);
    }

    [Fact]
    public void Constructor_WithSystemPrompt_HasOneMessage()
    {
        var conv = new Conversation("You are helpful.");
        Assert.Equal(1, conv.MessageCount);
    }

    [Fact]
    public void Constructor_WithWhitespaceSystemPrompt_StartsEmpty()
    {
        var conv = new Conversation("   ");
        Assert.Equal(0, conv.MessageCount);
    }

    [Fact]
    public void Constructor_WithNullSystemPrompt_StartsEmpty()
    {
        var conv = new Conversation(null);
        Assert.Equal(0, conv.MessageCount);
    }

    // ───────────────── SendAsync validation ─────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task SendAsync_ThrowsArgumentException_WhenMessageIsNullOrWhitespace(string? msg)
    {
        var conv = new Conversation();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => conv.SendAsync(msg!));

        Assert.Equal("message", ex.ParamName);
        Assert.Contains("cannot be null or empty", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ThrowsInvalidOperation_WhenEnvVarsMissing()
    {
        ClearEnvVars();
        var conv = new Conversation();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => conv.SendAsync("hello"));

        Assert.Contains("AZURE_OPENAI_API_URI", ex.Message);
    }

    [Fact]
    public async Task SendAsync_AddsUserMessageToHistory_EvenOnNetworkFailure()
    {
        SetupEnvVars();
        var conv = new Conversation("System prompt");

        // This will fail at the network layer, but the user message
        // should still be in history.
        try { await conv.SendAsync("test message"); }
        catch { }

        Assert.Equal(2, conv.MessageCount); // system + user
        var history = conv.GetHistory();
        Assert.Equal("system", history[0].Role);
        Assert.Equal("user", history[1].Role);
        Assert.Equal("test message", history[1].Content);
    }

    // ───────────── Model parameter validation ───────────────

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(2.1f)]
    [InlineData(float.MaxValue)]
    public void Temperature_ThrowsOutOfRange_WhenInvalid(float value)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.Temperature = value);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.7f)]
    [InlineData(2.0f)]
    public void Temperature_AcceptsValidValues(float value)
    {
        var conv = new Conversation();
        conv.Temperature = value;
        Assert.Equal(value, conv.Temperature);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxTokens_ThrowsOutOfRange_WhenLessThan1(int value)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.MaxTokens = value);
    }

    [Fact]
    public void MaxTokens_AcceptsPositiveValues()
    {
        var conv = new Conversation();
        conv.MaxTokens = 4096;
        Assert.Equal(4096, conv.MaxTokens);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void TopP_ThrowsOutOfRange_WhenInvalid(float value)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.TopP = value);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void TopP_AcceptsValidValues(float value)
    {
        var conv = new Conversation();
        conv.TopP = value;
        Assert.Equal(value, conv.TopP);
    }

    [Theory]
    [InlineData(-2.1f)]
    [InlineData(2.1f)]
    public void FrequencyPenalty_ThrowsOutOfRange_WhenInvalid(float value)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.FrequencyPenalty = value);
    }

    [Theory]
    [InlineData(-2.0f)]
    [InlineData(0f)]
    [InlineData(2.0f)]
    public void FrequencyPenalty_AcceptsValidValues(float value)
    {
        var conv = new Conversation();
        conv.FrequencyPenalty = value;
        Assert.Equal(value, conv.FrequencyPenalty);
    }

    [Theory]
    [InlineData(-2.1f)]
    [InlineData(2.1f)]
    public void PresencePenalty_ThrowsOutOfRange_WhenInvalid(float value)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.PresencePenalty = value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxRetries_ThrowsOutOfRange_WhenNegative(int value)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.MaxRetries = value);
    }

    [Fact]
    public void MaxRetries_AcceptsZero()
    {
        var conv = new Conversation();
        conv.MaxRetries = 0;
        Assert.Equal(0, conv.MaxRetries);
    }

    // ───────────── AddUserMessage ─────────────

    [Fact]
    public void AddUserMessage_AddsToHistory()
    {
        var conv = new Conversation();
        conv.AddUserMessage("Hello");

        Assert.Equal(1, conv.MessageCount);
        var history = conv.GetHistory();
        Assert.Equal("user", history[0].Role);
        Assert.Equal("Hello", history[0].Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddUserMessage_ThrowsArgumentException_WhenEmpty(string? msg)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentException>(() => conv.AddUserMessage(msg!));
    }

    // ───────────── AddAssistantMessage ─────────────

    [Fact]
    public void AddAssistantMessage_AddsToHistory()
    {
        var conv = new Conversation();
        conv.AddAssistantMessage("I can help!");

        Assert.Equal(1, conv.MessageCount);
        var history = conv.GetHistory();
        Assert.Equal("assistant", history[0].Role);
        Assert.Equal("I can help!", history[0].Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAssistantMessage_ThrowsArgumentException_WhenEmpty(string? msg)
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentException>(() => conv.AddAssistantMessage(msg!));
    }

    // ───────────── Clear ─────────────

    [Fact]
    public void Clear_RemovesAllMessages_WhenNoSystemPrompt()
    {
        var conv = new Conversation();
        conv.AddUserMessage("Hello");
        conv.AddAssistantMessage("Hi!");

        conv.Clear();

        Assert.Equal(0, conv.MessageCount);
    }

    [Fact]
    public void Clear_PreservesSystemPrompt()
    {
        var conv = new Conversation("You are helpful.");
        conv.AddUserMessage("Hello");
        conv.AddAssistantMessage("Hi!");

        conv.Clear();

        Assert.Equal(1, conv.MessageCount);
        var history = conv.GetHistory();
        Assert.Equal("system", history[0].Role);
        Assert.Equal("You are helpful.", history[0].Content);
    }

    // ───────────── GetHistory ─────────────

    [Fact]
    public void GetHistory_ReturnsCorrectRolesAndContent()
    {
        var conv = new Conversation("System");
        conv.AddUserMessage("User msg");
        conv.AddAssistantMessage("Assistant msg");

        var history = conv.GetHistory();

        Assert.Equal(3, history.Count);
        Assert.Equal(("system", "System"), history[0]);
        Assert.Equal(("user", "User msg"), history[1]);
        Assert.Equal(("assistant", "Assistant msg"), history[2]);
    }

    [Fact]
    public void GetHistory_ReturnsSnapshotNotReference()
    {
        var conv = new Conversation();
        conv.AddUserMessage("Hello");

        var history1 = conv.GetHistory();
        conv.AddUserMessage("World");
        var history2 = conv.GetHistory();

        Assert.Single(history1);
        Assert.Equal(2, history2.Count);
    }

    // ───────────── Conversation replay ─────────────

    [Fact]
    public void CanReplayConversation()
    {
        // Simulate replaying a prior conversation
        var conv = new Conversation("You are a math tutor.");
        conv.AddUserMessage("What is 2+2?");
        conv.AddAssistantMessage("4");
        conv.AddUserMessage("Multiply that by 3");
        conv.AddAssistantMessage("12");

        var history = conv.GetHistory();
        Assert.Equal(5, history.Count);
        Assert.Equal("system", history[0].Role);
        Assert.Equal("user", history[1].Role);
        Assert.Equal("assistant", history[2].Role);
        Assert.Equal("user", history[3].Role);
        Assert.Equal("assistant", history[4].Role);
    }

    // ───────────── Default values ─────────────

    [Fact]
    public void DefaultParameters_MatchMainDefaults()
    {
        var conv = new Conversation();

        Assert.Equal(0.7f, conv.Temperature);
        Assert.Equal(800, conv.MaxTokens);
        Assert.Equal(0.95f, conv.TopP);
        Assert.Equal(0f, conv.FrequencyPenalty);
        Assert.Equal(0f, conv.PresencePenalty);
        Assert.Equal(3, conv.MaxRetries);
    }

    // ───────────── Thread safety smoke test ─────────────

    [Fact]
    public void AddMessages_HandlesParallelCalls()
    {
        var conv = new Conversation();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                if (i % 2 == 0)
                    conv.AddUserMessage($"User {i}");
                else
                    conv.AddAssistantMessage($"Assistant {i}");
            }));

        Task.WaitAll(tasks.ToArray());

        Assert.Equal(100, conv.MessageCount);
    }

    // ───────────── Cancellation ─────────────

    [Fact]
    public async Task SendAsync_ThrowsOperationCanceled_WhenPreCancelled()
    {
        SetupEnvVars();
        var conv = new Conversation();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => conv.SendAsync("hello", cts.Token));
    }

    // ───────────── Helpers ─────────────

    private static void SetupEnvVars()
    {
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI",
            "https://test.openai.azure.com/");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", "gpt-4");
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", null);
    }
}
