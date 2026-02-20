namespace Prompt.Tests;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// Tests for security hardening: JSON payload size limits, message count limits,
/// conversation history trimming, and file size validation.
/// </summary>
public class SecurityTests : IDisposable
{
    private readonly string _tempDir;

    public SecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"prompt-security-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ─────────── Conversation: MaxMessages property ───────────

    [Fact]
    public void MaxMessages_DefaultValue_Is1000()
    {
        Assert.Equal(1000, Conversation.DefaultMaxMessages);
    }

    [Fact]
    public void MaxMessages_CanBeSet()
    {
        var conv = new Conversation();
        conv.MaxMessages = 50;
        Assert.Equal(50, conv.MaxMessages);
    }

    [Fact]
    public void MaxMessages_ThrowsWhenLessThan2()
    {
        var conv = new Conversation();
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.MaxMessages = 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.MaxMessages = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => conv.MaxMessages = -5);
    }

    // ─────────── Conversation: History trimming ───────────

    [Fact]
    public void AddMessages_TrimsOldestWhenExceedingMax()
    {
        var conv = new Conversation("System prompt");
        conv.MaxMessages = 5; // system + 4 messages max

        conv.AddUserMessage("msg1");
        conv.AddAssistantMessage("reply1");
        conv.AddUserMessage("msg2");
        conv.AddAssistantMessage("reply2");

        // Now at 5 messages (system + 4). Adding one more should trim.
        conv.AddUserMessage("msg3");

        Assert.Equal(5, conv.MessageCount);

        var history = conv.GetHistory();
        // System prompt should be preserved
        Assert.Equal("system", history[0].Role);
        Assert.Equal("System prompt", history[0].Content);

        // Oldest non-system message ("msg1") should be removed
        Assert.DoesNotContain(history, h => h.Content == "msg1");

        // Newest message should be present
        Assert.Contains(history, h => h.Content == "msg3");
    }

    [Fact]
    public void AddMessages_PreservesSystemPromptDuringTrimming()
    {
        var conv = new Conversation("Important system instructions");
        conv.MaxMessages = 3; // system + 2 messages

        conv.AddUserMessage("user1");
        conv.AddAssistantMessage("assistant1");

        // At limit. Adding more should trim oldest non-system.
        conv.AddUserMessage("user2");

        var history = conv.GetHistory();
        Assert.Equal(3, history.Count);
        Assert.Equal("system", history[0].Role);
        Assert.Equal("Important system instructions", history[0].Content);
    }

    [Fact]
    public void AddMessages_NoTrimWhenUnderLimit()
    {
        var conv = new Conversation();
        conv.MaxMessages = 100;

        conv.AddUserMessage("msg1");
        conv.AddAssistantMessage("reply1");
        conv.AddUserMessage("msg2");

        Assert.Equal(3, conv.MessageCount);
    }

    [Fact]
    public void AddMessages_TrimMultipleAtOnce()
    {
        var conv = new Conversation("sys");
        conv.MaxMessages = 100; // Start with high limit

        // Add 10 messages
        for (int i = 0; i < 10; i++)
            conv.AddUserMessage($"msg{i}");

        Assert.Equal(11, conv.MessageCount); // sys + 10

        // Now lower the limit — next add should trim to fit
        conv.MaxMessages = 5;
        conv.AddUserMessage("final");

        Assert.Equal(5, conv.MessageCount);
    }

    // ─────────── Conversation: LoadFromJson size limits ───────────

    [Fact]
    public void LoadFromJson_RejectsExcessiveMessageCount()
    {
        // Build a JSON with more messages than the limit
        var messages = new StringBuilder("[");
        for (int i = 0; i < Conversation.MaxDeserializedMessages + 1; i++)
        {
            if (i > 0) messages.Append(',');
            messages.Append($"{{\"role\":\"user\",\"content\":\"msg{i}\"}}");
        }
        messages.Append(']');

        string json = $"{{\"messages\":{messages}}}";

        var ex = Assert.Throws<InvalidOperationException>(
            () => Conversation.LoadFromJson(json));
        Assert.Contains("exceeding the maximum allowed count", ex.Message);
    }

    [Fact]
    public void LoadFromJson_AcceptsNormalPayload()
    {
        string json = @"{
            ""messages"": [
                { ""role"": ""system"", ""content"": ""You are helpful."" },
                { ""role"": ""user"", ""content"": ""Hello"" },
                { ""role"": ""assistant"", ""content"": ""Hi there!"" }
            ],
            ""parameters"": {
                ""temperature"": 0.7,
                ""maxTokens"": 800,
                ""topP"": 0.95,
                ""frequencyPenalty"": 0,
                ""presencePenalty"": 0,
                ""maxRetries"": 3
            }
        }";

        var conv = Conversation.LoadFromJson(json);
        Assert.Equal(3, conv.MessageCount);
    }

    // ─────────── PromptChain: FromJson size limits ───────────

    [Fact]
    public void ChainFromJson_RejectsExcessiveStepCount()
    {
        var steps = new StringBuilder("[");
        for (int i = 0; i < PromptChain.MaxDeserializedSteps + 1; i++)
        {
            if (i > 0) steps.Append(',');
            steps.Append($"{{\"name\":\"step{i}\",\"template\":\"Do {{{{input}}}}\",\"outputVariable\":\"out{i}\"}}");
        }
        steps.Append(']');

        string json = $"{{\"steps\":{steps},\"maxRetries\":3}}";

        var ex = Assert.Throws<InvalidOperationException>(
            () => PromptChain.FromJson(json));
        Assert.Contains("exceeding the maximum allowed count", ex.Message);
    }

    [Fact]
    public void ChainFromJson_AcceptsNormalPayload()
    {
        string json = @"{
            ""steps"": [
                { ""name"": ""step1"", ""template"": ""Summarize: {{text}}"", ""outputVariable"": ""summary"" }
            ],
            ""maxRetries"": 3
        }";

        var chain = PromptChain.FromJson(json);
        Assert.Equal(1, chain.StepCount);
    }

    // ─────────── PromptTemplate: FromJson size limits ───────────

    [Fact]
    public void TemplateFromJson_AcceptsNormalPayload()
    {
        string json = @"{ ""template"": ""Hello {{name}}"" }";
        var template = PromptTemplate.FromJson(json);
        Assert.Equal("Hello {{name}}", template.Template);
    }

    // ─────────── File loading: path resolution ───────────

    [Fact]
    public async void ConversationLoadFromFile_ResolvesPath()
    {
        // Save a valid conversation file
        var conv = new Conversation("Test");
        conv.AddUserMessage("Hello");
        string filePath = Path.Combine(_tempDir, "test-conv.json");
        await conv.SaveToFileAsync(filePath);

        // Load it back — should work fine with full path
        var loaded = await Conversation.LoadFromFileAsync(filePath);
        Assert.Equal(2, loaded.MessageCount);
    }

    [Fact]
    public async void TemplateLoadFromFile_ResolvesPath()
    {
        var template = new PromptTemplate("Hello {{name}}");
        string filePath = Path.Combine(_tempDir, "test-template.json");
        await template.SaveToFileAsync(filePath);

        var loaded = await PromptTemplate.LoadFromFileAsync(filePath);
        Assert.Equal("Hello {{name}}", loaded.Template);
    }

    [Fact]
    public async void ChainSaveAndLoad_ResolvesPath()
    {
        var chain = new PromptChain()
            .AddStep("s1", new PromptTemplate("Do: {{input}}"), "output");
        string filePath = Path.Combine(_tempDir, "test-chain.json");
        await chain.SaveToFileAsync(filePath);

        var loaded = await PromptChain.LoadFromFileAsync(filePath);
        Assert.Equal(1, loaded.StepCount);
    }

    // ─────────── Conversation: trimming preserves conversation coherence ───────────

    [Fact]
    public void Trimming_MaintainsChronologicalOrder()
    {
        var conv = new Conversation("sys");
        conv.MaxMessages = 5; // sys + 4

        conv.AddUserMessage("first");
        conv.AddAssistantMessage("reply-first");
        conv.AddUserMessage("second");
        conv.AddAssistantMessage("reply-second");

        // At limit. Add two more.
        conv.AddUserMessage("third");
        conv.AddAssistantMessage("reply-third");

        var history = conv.GetHistory();
        Assert.Equal(5, history.Count);
        Assert.Equal("system", history[0].Role);

        // Verify remaining messages are in order
        var nonSystem = history.Skip(1).ToList();
        Assert.Equal("second", nonSystem[0].Content);
        Assert.Equal("reply-second", nonSystem[1].Content);
        Assert.Equal("third", nonSystem[2].Content);
        Assert.Equal("reply-third", nonSystem[3].Content);
    }

    [Fact]
    public void MaxMessages_CanBeSetToMaxValue_DisablesLimit()
    {
        var conv = new Conversation();
        conv.MaxMessages = int.MaxValue;

        // Should be able to add many messages without issue
        for (int i = 0; i < 100; i++)
            conv.AddUserMessage($"msg{i}");

        Assert.Equal(100, conv.MessageCount);
    }
}
