namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Prompt;
using Xunit;

/// <summary>
/// Request-shaping tests for the HTTP-based <see cref="ILlmProvider"/>
/// implementations. All requests are intercepted by <see cref="CapturingHandler"/>
/// so no real network calls are made; assertions cover URLs, headers, and the
/// JSON body each provider produces, plus SSE streaming decode.
/// </summary>
public class LlmProviderTests
{
    private static IReadOnlyList<ChatMsg> SystemUserConversation() => new List<ChatMsg>
    {
        ChatMsg.FromSystem("You are helpful."),
        ChatMsg.FromUser("Hello there."),
    };

    private static PromptOptions Opts() => new PromptOptions
    {
        Temperature = 0.3f,
        MaxTokens = 256,
        TopP = 0.9f,
        FrequencyPenalty = 0.1f,
        PresencePenalty = 0.2f,
    };

    // ════════════════════ OpenAI-compatible ════════════════════

    [Fact]
    public async Task OpenAICompat_PostsChatCompletions_WithBearerAndSchema()
    {
        var handler = CapturingHandler.Json(
            "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Hi!\"}}]}");
        var provider = OpenAICompatProvider.OpenAI("sk-test", "gpt-4o-mini", handler);

        string? result = await provider.CompleteAsync(SystemUserConversation(), Opts());

        Assert.Equal("Hi!", result);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.openai.com/v1/chat/completions",
            handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test", handler.LastRequest!.Headers.Authorization!.Parameter);

        using var doc = JsonDocument.Parse(handler.LastBody);
        var root = doc.RootElement;
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(256, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.9, root.GetProperty("top_p").GetDouble(), 3);
        Assert.Equal(0.1, root.GetProperty("frequency_penalty").GetDouble(), 3);
        Assert.Equal(0.2, root.GetProperty("presence_penalty").GetDouble(), 3);

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("You are helpful.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("Hello there.", messages[1].GetProperty("content").GetString());
    }

    [Theory]
    [InlineData("mistral", "https://api.mistral.ai/v1/chat/completions")]
    [InlineData("groq", "https://api.groq.com/openai/v1/chat/completions")]
    [InlineData("deepseek", "https://api.deepseek.com/v1/chat/completions")]
    [InlineData("grok", "https://api.x.ai/v1/chat/completions")]
    [InlineData("openrouter", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("together", "https://api.together.xyz/v1/chat/completions")]
    [InlineData("fireworks", "https://api.fireworks.ai/inference/v1/chat/completions")]
    public async Task OpenAICompat_Presets_UseExpectedBaseUrls(string preset, string expectedUrl)
    {
        var handler = CapturingHandler.Json(
            "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");

        OpenAICompatProvider provider = preset switch
        {
            "mistral" => OpenAICompatProvider.Mistral("k", "m", handler),
            "groq" => OpenAICompatProvider.Groq("k", "m", handler),
            "deepseek" => OpenAICompatProvider.DeepSeek("k", "m", handler),
            "grok" => OpenAICompatProvider.Grok("k", "m", handler),
            "openrouter" => OpenAICompatProvider.OpenRouter("k", "m", handler),
            "together" => OpenAICompatProvider.Together("k", "m", handler),
            "fireworks" => OpenAICompatProvider.Fireworks("k", "m", handler),
            _ => throw new ArgumentOutOfRangeException(nameof(preset)),
        };

        await provider.CompleteAsync(SystemUserConversation(), Opts());

        Assert.Equal(expectedUrl, handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Ollama_UsesLocalhostBaseUrl_AndNoAuthByDefault()
    {
        var handler = CapturingHandler.Json(
            "{\"choices\":[{\"message\":{\"content\":\"pong\"}}]}");
        var provider = OpenAICompatProvider.Ollama("llama3", handler: handler);

        string? result = await provider.CompleteAsync(SystemUserConversation(), Opts());

        Assert.Equal("pong", result);
        Assert.Equal("http://localhost:11434/v1/chat/completions",
            handler.LastRequest!.RequestUri!.ToString());
        // No API key supplied → no Authorization header.
        Assert.Null(handler.LastRequest!.Headers.Authorization);

        using var doc = JsonDocument.Parse(handler.LastBody);
        Assert.Equal("llama3", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Ollama_HonorsCustomBaseUrl()
    {
        var handler = CapturingHandler.Json("{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
        var provider = OpenAICompatProvider.Ollama("llama3", baseUrl: "http://10.0.0.5:11434/v1", handler: handler);

        await provider.CompleteAsync(SystemUserConversation(), Opts());

        Assert.Equal("http://10.0.0.5:11434/v1/chat/completions",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task OpenAICompat_Streaming_SetsStreamFlag_AndDecodesDeltas()
    {
        var handler = CapturingHandler.Sse(new[]
        {
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}",
            "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}",
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}",
            "data: [DONE]",
        });
        var provider = OpenAICompatProvider.OpenAI("sk", "gpt-4o-mini", handler);

        var chunks = new List<StreamChunk>();
        await foreach (var c in provider.CompleteStreamAsync(SystemUserConversation(), Opts()))
            chunks.Add(c);

        // stream=true was sent
        using var doc = JsonDocument.Parse(handler.LastBody);
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());

        Assert.Equal("Hello", chunks[^1].FullText);
        Assert.Contains(chunks, c => c.Delta == "Hel");
        Assert.Contains(chunks, c => c.Delta == "lo");
        Assert.True(chunks[^1].IsComplete);
    }

    // ════════════════════ Anthropic ════════════════════

    [Fact]
    public async Task Anthropic_ExtractsSystem_RequiresMaxTokens_AndUsesHeaders()
    {
        var handler = CapturingHandler.Json(
            "{\"content\":[{\"type\":\"text\",\"text\":\"Hello back.\"}]}");
        var provider = new AnthropicProvider("ak-test", "claude-3-5-sonnet-latest", handler: handler);

        var messages = new List<ChatMsg>
        {
            ChatMsg.FromSystem("Be terse."),
            ChatMsg.FromUser("Hi"),
            ChatMsg.FromAssistant("Hello"),
            ChatMsg.FromUser("Again"),
        };

        string? result = await provider.CompleteAsync(messages, Opts());

        Assert.Equal("Hello back.", result);
        Assert.Equal("https://api.anthropic.com/v1/messages",
            handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("ak-test", handler.LastRequest!.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", handler.LastRequest!.Headers.GetValues("anthropic-version").Single());

        using var doc = JsonDocument.Parse(handler.LastBody);
        var root = doc.RootElement;

        // System is a top-level field, not in messages.
        Assert.Equal("Be terse.", root.GetProperty("system").GetString());
        // max_tokens is required and present.
        Assert.Equal(256, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("claude-3-5-sonnet-latest", root.GetProperty("model").GetString());

        var msgs = root.GetProperty("messages");
        Assert.Equal(3, msgs.GetArrayLength()); // system removed
        Assert.Equal("user", msgs[0].GetProperty("role").GetString());
        Assert.Equal("assistant", msgs[1].GetProperty("role").GetString());
        Assert.Equal("user", msgs[2].GetProperty("role").GetString());
        // content is an array of {type:text,text:...} blocks.
        var firstContent = msgs[0].GetProperty("content");
        Assert.Equal(JsonValueKind.Array, firstContent.ValueKind);
        Assert.Equal("text", firstContent[0].GetProperty("type").GetString());
        Assert.Equal("Hi", firstContent[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Anthropic_OmitsSystem_WhenNoSystemMessage()
    {
        var handler = CapturingHandler.Json("{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}");
        var provider = new AnthropicProvider("ak", "claude-x", handler: handler);

        await provider.CompleteAsync(new List<ChatMsg> { ChatMsg.FromUser("Hi") }, Opts());

        using var doc = JsonDocument.Parse(handler.LastBody);
        Assert.False(doc.RootElement.TryGetProperty("system", out _));
    }

    [Fact]
    public async Task Anthropic_Streaming_DecodesContentBlockDeltas()
    {
        var handler = CapturingHandler.Sse(new[]
        {
            "data: {\"type\":\"message_start\"}",
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hel\"}}",
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"lo\"}}",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}",
            "data: {\"type\":\"message_stop\"}",
        });
        var provider = new AnthropicProvider("ak", "claude-x", handler: handler);

        var chunks = new List<StreamChunk>();
        await foreach (var c in provider.CompleteStreamAsync(new List<ChatMsg> { ChatMsg.FromUser("Hi") }, Opts()))
            chunks.Add(c);

        Assert.Equal("Hello", chunks[^1].FullText);
        Assert.True(chunks[^1].IsComplete);
        Assert.Equal("end_turn", chunks[^1].FinishReason);
    }

    // ════════════════════ Gemini ════════════════════

    [Fact]
    public async Task Gemini_BuildsGenerateContentUrl_WithKey_AndMapsRoles()
    {
        var handler = CapturingHandler.Json(
            "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hi from Gemini\"}]}}]}");
        var provider = new GeminiProvider("gk-test", "gemini-1.5-flash", handler: handler);

        var messages = new List<ChatMsg>
        {
            ChatMsg.FromSystem("Be brief."),
            ChatMsg.FromUser("Hello"),
            ChatMsg.FromAssistant("Hey"),
            ChatMsg.FromUser("More"),
        };

        string? result = await provider.CompleteAsync(messages, Opts());

        Assert.Equal("Hi from Gemini", result);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=gk-test",
            handler.LastRequest!.RequestUri!.ToString());

        using var doc = JsonDocument.Parse(handler.LastBody);
        var root = doc.RootElement;

        // System lifted into systemInstruction.
        var sysParts = root.GetProperty("systemInstruction").GetProperty("parts");
        Assert.Equal("Be brief.", sysParts[0].GetProperty("text").GetString());

        var contents = root.GetProperty("contents");
        Assert.Equal(3, contents.GetArrayLength()); // system not in contents
        Assert.Equal("user", contents[0].GetProperty("role").GetString());
        Assert.Equal("model", contents[1].GetProperty("role").GetString()); // assistant -> model
        Assert.Equal("user", contents[2].GetProperty("role").GetString());
        Assert.Equal("Hello", contents[0].GetProperty("parts")[0].GetProperty("text").GetString());

        var gen = root.GetProperty("generationConfig");
        Assert.Equal(256, gen.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal(0.3, gen.GetProperty("temperature").GetDouble(), 3);
        Assert.Equal(0.9, gen.GetProperty("topP").GetDouble(), 3);
    }

    [Fact]
    public async Task Gemini_Streaming_UsesStreamEndpoint_WithAltSse()
    {
        var handler = CapturingHandler.Sse(new[]
        {
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hel\"}]}}]}",
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"lo\"}]},\"finishReason\":\"STOP\"}]}",
        });
        var provider = new GeminiProvider("gk", "gemini-1.5-flash", handler: handler);

        var chunks = new List<StreamChunk>();
        await foreach (var c in provider.CompleteStreamAsync(new List<ChatMsg> { ChatMsg.FromUser("Hi") }, Opts()))
            chunks.Add(c);

        Assert.Contains("streamGenerateContent", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("alt=sse", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Hello", chunks[^1].FullText);
        Assert.True(chunks[^1].IsComplete);
    }

    // ════════════════════ Error handling ════════════════════

    [Fact]
    public async Task HttpProvider_ThrowsWithBody_OnNonSuccess()
    {
        var handler = new CapturingHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new System.Net.Http.StringContent("{\"error\":\"bad key\"}"),
            });
        var provider = OpenAICompatProvider.OpenAI("sk", "gpt-4o-mini", handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.CompleteAsync(SystemUserConversation(), Opts()));
        Assert.Contains("401", ex.Message);
        Assert.Contains("bad key", ex.Message);
    }

    // ════════════════════ Constructor validation ════════════════════

    [Fact]
    public void OpenAICompat_Constructor_RejectsEmptyModel()
        => Assert.Throws<ArgumentException>(() => new OpenAICompatProvider("https://x/v1", "k", ""));

    [Fact]
    public void Anthropic_Constructor_RejectsEmptyKey()
        => Assert.Throws<ArgumentException>(() => new AnthropicProvider("", "claude"));

    [Fact]
    public void Gemini_Constructor_RejectsEmptyModel()
        => Assert.Throws<ArgumentException>(() => new GeminiProvider("k", ""));
}
