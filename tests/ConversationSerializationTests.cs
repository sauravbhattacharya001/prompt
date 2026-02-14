namespace Prompt.Tests;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="Conversation"/> serialization — SaveToJson,
/// LoadFromJson, SaveToFileAsync, and LoadFromFileAsync.
/// </summary>
public class ConversationSerializationTests : IDisposable
{
    private readonly string _tempDir;

    public ConversationSerializationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"prompt-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ───────────────────── SaveToJson ─────────────────────

    [Fact]
    public void SaveToJson_EmptyConversation_ReturnsValidJson()
    {
        var conv = new Conversation();
        string json = conv.SaveToJson();

        Assert.False(string.IsNullOrWhiteSpace(json));
        var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public void SaveToJson_WithSystemPrompt_IncludesSystemMessage()
    {
        var conv = new Conversation("You are helpful.");
        string json = conv.SaveToJson();

        var doc = JsonDocument.Parse(json);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("You are helpful.", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public void SaveToJson_WithMessages_PreservesOrder()
    {
        var conv = new Conversation("System prompt");
        conv.AddUserMessage("Hello");
        conv.AddAssistantMessage("Hi there!");
        conv.AddUserMessage("How are you?");

        string json = conv.SaveToJson();
        var doc = JsonDocument.Parse(json);
        var messages = doc.RootElement.GetProperty("messages");

        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[2].GetProperty("role").GetString());
        Assert.Equal("user", messages[3].GetProperty("role").GetString());
    }

    [Fact]
    public void SaveToJson_IncludesParameters()
    {
        var conv = new Conversation
        {
            Temperature = 1.5f,
            MaxTokens = 2000,
            TopP = 0.8f,
            FrequencyPenalty = 0.5f,
            PresencePenalty = -0.5f,
            MaxRetries = 5
        };

        string json = conv.SaveToJson();
        var doc = JsonDocument.Parse(json);
        var parameters = doc.RootElement.GetProperty("parameters");

        Assert.Equal(1.5f, parameters.GetProperty("temperature").GetSingle());
        Assert.Equal(2000, parameters.GetProperty("maxTokens").GetInt32());
        Assert.Equal(0.8f, parameters.GetProperty("topP").GetSingle());
        Assert.Equal(0.5f, parameters.GetProperty("frequencyPenalty").GetSingle());
        Assert.Equal(-0.5f, parameters.GetProperty("presencePenalty").GetSingle());
        Assert.Equal(5, parameters.GetProperty("maxRetries").GetInt32());
    }

    [Fact]
    public void SaveToJson_NotIndented_ProducesCompactJson()
    {
        var conv = new Conversation("Test");
        string json = conv.SaveToJson(indented: false);

        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void SaveToJson_Indented_ProducesFormattedJson()
    {
        var conv = new Conversation("Test");
        string json = conv.SaveToJson(indented: true);

        Assert.Contains("\n", json);
    }

    // ───────────────────── LoadFromJson ─────────────────────

    [Fact]
    public void LoadFromJson_RestoresMessages()
    {
        var original = new Conversation("You are a tutor.");
        original.AddUserMessage("What is 2+2?");
        original.AddAssistantMessage("4");

        string json = original.SaveToJson();
        var restored = Conversation.LoadFromJson(json);

        var history = restored.GetHistory();
        Assert.Equal(3, history.Count);
        Assert.Equal(("system", "You are a tutor."), history[0]);
        Assert.Equal(("user", "What is 2+2?"), history[1]);
        Assert.Equal(("assistant", "4"), history[2]);
    }

    [Fact]
    public void LoadFromJson_RestoresParameters()
    {
        var original = new Conversation
        {
            Temperature = 1.2f,
            MaxTokens = 4096,
            TopP = 0.9f,
            FrequencyPenalty = 0.3f,
            PresencePenalty = 0.1f,
            MaxRetries = 7
        };

        string json = original.SaveToJson();
        var restored = Conversation.LoadFromJson(json);

        Assert.Equal(1.2f, restored.Temperature);
        Assert.Equal(4096, restored.MaxTokens);
        Assert.Equal(0.9f, restored.TopP);
        Assert.Equal(0.3f, restored.FrequencyPenalty);
        Assert.Equal(0.1f, restored.PresencePenalty);
        Assert.Equal(7, restored.MaxRetries);
    }

    [Fact]
    public void LoadFromJson_RoundTrip_PreservesEverything()
    {
        var original = new Conversation("Be concise.")
        {
            Temperature = 0.5f,
            MaxTokens = 1000,
        };
        original.AddUserMessage("Explain AI");
        original.AddAssistantMessage("AI is machine intelligence.");
        original.AddUserMessage("More detail");
        original.AddAssistantMessage("AI uses neural networks to learn patterns.");

        string json = original.SaveToJson();
        var restored = Conversation.LoadFromJson(json);

        // Verify messages
        var origHistory = original.GetHistory();
        var restHistory = restored.GetHistory();
        Assert.Equal(origHistory.Count, restHistory.Count);
        for (int i = 0; i < origHistory.Count; i++)
        {
            Assert.Equal(origHistory[i].Role, restHistory[i].Role);
            Assert.Equal(origHistory[i].Content, restHistory[i].Content);
        }

        // Verify parameters
        Assert.Equal(original.Temperature, restored.Temperature);
        Assert.Equal(original.MaxTokens, restored.MaxTokens);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LoadFromJson_ThrowsArgumentException_WhenJsonEmpty(string? json)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Conversation.LoadFromJson(json!));

        Assert.Equal("json", ex.ParamName);
    }

    [Fact]
    public void LoadFromJson_ThrowsJsonException_WhenMalformed()
    {
        Assert.Throws<JsonException>(
            () => Conversation.LoadFromJson("{not valid json}"));
    }

    [Fact]
    public void LoadFromJson_ThrowsInvalidOperation_WhenNoMessagesArray()
    {
        Assert.Throws<InvalidOperationException>(
            () => Conversation.LoadFromJson("{\"parameters\":{}}"));
    }

    [Fact]
    public void LoadFromJson_HandlesEmptyMessagesArray()
    {
        string json = "{\"messages\":[]}";
        var conv = Conversation.LoadFromJson(json);

        Assert.Equal(0, conv.MessageCount);
    }

    [Fact]
    public void LoadFromJson_HandlesAbsentParameters()
    {
        string json = "{\"messages\":[{\"role\":\"user\",\"content\":\"Hello\"}]}";
        var conv = Conversation.LoadFromJson(json);

        Assert.Equal(1, conv.MessageCount);
        // Should use defaults
        Assert.Equal(0.7f, conv.Temperature);
        Assert.Equal(800, conv.MaxTokens);
    }

    [Fact]
    public void LoadFromJson_SkipsEmptyContent()
    {
        string json = "{\"messages\":[{\"role\":\"user\",\"content\":\"\"},{\"role\":\"user\",\"content\":\"Hi\"}]}";
        var conv = Conversation.LoadFromJson(json);

        Assert.Equal(1, conv.MessageCount);
        var history = conv.GetHistory();
        Assert.Equal("Hi", history[0].Content);
    }

    [Fact]
    public void LoadFromJson_IgnoresUnknownRoles()
    {
        string json = "{\"messages\":[{\"role\":\"tool\",\"content\":\"data\"},{\"role\":\"user\",\"content\":\"Hi\"}]}";
        var conv = Conversation.LoadFromJson(json);

        Assert.Equal(1, conv.MessageCount);
    }

    [Fact]
    public void LoadFromJson_HandleSpecialCharacters()
    {
        var conv = new Conversation();
        conv.AddUserMessage("Hello \"world\" \n\ttab & <html>");
        conv.AddAssistantMessage("Response with 日本語 and émojis 🚀");

        string json = conv.SaveToJson();
        var restored = Conversation.LoadFromJson(json);

        var history = restored.GetHistory();
        Assert.Equal("Hello \"world\" \n\ttab & <html>", history[0].Content);
        Assert.Equal("Response with 日本語 and émojis 🚀", history[1].Content);
    }

    // ───────────────────── SaveToFileAsync ─────────────────────

    [Fact]
    public async Task SaveToFileAsync_CreatesFile()
    {
        var conv = new Conversation("Test");
        conv.AddUserMessage("Hello");

        string filePath = Path.Combine(_tempDir, "conv.json");
        await conv.SaveToFileAsync(filePath);

        Assert.True(File.Exists(filePath));
        string content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("\"messages\"", content);
    }

    [Fact]
    public async Task SaveToFileAsync_OverwritesExistingFile()
    {
        string filePath = Path.Combine(_tempDir, "conv.json");
        await File.WriteAllTextAsync(filePath, "old content");

        var conv = new Conversation();
        conv.AddUserMessage("New content");
        await conv.SaveToFileAsync(filePath);

        string content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("New content", content);
        Assert.DoesNotContain("old content", content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveToFileAsync_ThrowsArgumentException_WhenPathEmpty(string? path)
    {
        var conv = new Conversation();
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => conv.SaveToFileAsync(path!));

        Assert.Equal("filePath", ex.ParamName);
    }

    // ───────────────────── LoadFromFileAsync ─────────────────────

    [Fact]
    public async Task LoadFromFileAsync_RestoresConversation()
    {
        var original = new Conversation("System");
        original.AddUserMessage("Q1");
        original.AddAssistantMessage("A1");

        string filePath = Path.Combine(_tempDir, "conv.json");
        await original.SaveToFileAsync(filePath);

        var restored = await Conversation.LoadFromFileAsync(filePath);

        var history = restored.GetHistory();
        Assert.Equal(3, history.Count);
        Assert.Equal(("system", "System"), history[0]);
        Assert.Equal(("user", "Q1"), history[1]);
        Assert.Equal(("assistant", "A1"), history[2]);
    }

    [Fact]
    public async Task LoadFromFileAsync_ThrowsFileNotFound()
    {
        string filePath = Path.Combine(_tempDir, "nonexistent.json");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => Conversation.LoadFromFileAsync(filePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadFromFileAsync_ThrowsArgumentException_WhenPathEmpty(string? path)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Conversation.LoadFromFileAsync(path!));

        Assert.Equal("filePath", ex.ParamName);
    }

    [Fact]
    public async Task RoundTrip_FileSaveLoad_PreservesFullState()
    {
        var original = new Conversation("You are a coder.")
        {
            Temperature = 0.3f,
            MaxTokens = 4096,
            TopP = 0.85f,
            FrequencyPenalty = 0.2f,
            PresencePenalty = 0.1f,
            MaxRetries = 2
        };
        original.AddUserMessage("Write a sort function");
        original.AddAssistantMessage("def sort(arr): return sorted(arr)");
        original.AddUserMessage("Now in C#");
        original.AddAssistantMessage("public static void Sort<T>(List<T> list) => list.Sort();");

        string filePath = Path.Combine(_tempDir, "full-roundtrip.json");
        await original.SaveToFileAsync(filePath);
        var restored = await Conversation.LoadFromFileAsync(filePath);

        // Messages
        var origHistory = original.GetHistory();
        var restHistory = restored.GetHistory();
        Assert.Equal(origHistory.Count, restHistory.Count);
        for (int i = 0; i < origHistory.Count; i++)
        {
            Assert.Equal(origHistory[i], restHistory[i]);
        }

        // Parameters
        Assert.Equal(original.Temperature, restored.Temperature);
        Assert.Equal(original.MaxTokens, restored.MaxTokens);
        Assert.Equal(original.TopP, restored.TopP);
        Assert.Equal(original.FrequencyPenalty, restored.FrequencyPenalty);
        Assert.Equal(original.PresencePenalty, restored.PresencePenalty);
        Assert.Equal(original.MaxRetries, restored.MaxRetries);
    }

    // ───────────── Conversation continues after restore ─────────────

    [Fact]
    public void RestoredConversation_CanAddMoreMessages()
    {
        var original = new Conversation("System");
        original.AddUserMessage("First");
        original.AddAssistantMessage("Response");

        string json = original.SaveToJson();
        var restored = Conversation.LoadFromJson(json);

        // Continue the conversation
        restored.AddUserMessage("Second");
        restored.AddAssistantMessage("Another response");

        Assert.Equal(5, restored.MessageCount);
        var history = restored.GetHistory();
        Assert.Equal("Second", history[3].Content);
        Assert.Equal("Another response", history[4].Content);
    }

    [Fact]
    public void RestoredConversation_ClearPreservesSystemPrompt()
    {
        var conv = new Conversation("Keep me.");
        conv.AddUserMessage("Delete me");

        string json = conv.SaveToJson();
        var restored = Conversation.LoadFromJson(json);

        restored.Clear();

        Assert.Equal(1, restored.MessageCount);
        var history = restored.GetHistory();
        Assert.Equal("system", history[0].Role);
        Assert.Equal("Keep me.", history[0].Content);
    }
}
