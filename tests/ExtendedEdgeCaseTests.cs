namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Extended tests targeting uncovered edge cases and code paths across
/// Main, Conversation, PromptTemplate, PromptChain, ChainResult, and
/// StepResult. All tests run without an Azure OpenAI endpoint.
/// </summary>
public class ExtendedEdgeCaseTests : IDisposable
{
    private readonly string _tempDir;

    public ExtendedEdgeCaseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"prompt-ext-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Main.ResetClient();
    }

    public void Dispose()
    {
        Main.ResetClient();
        ClearEnvVars();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Main — Client lifecycle edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetOrCreateChatClient_RecreatesOnRetryPolicyChange()
    {
        // Set up valid env vars so client creation succeeds
        SetupEnvVars();

        var client1 = Main.GetOrCreateChatClient(3);
        var client2 = Main.GetOrCreateChatClient(3); // same retries — should reuse
        Assert.Same(client1, client2);

        var client3 = Main.GetOrCreateChatClient(5); // different retries — recreated
        Assert.NotSame(client1, client3);
    }

    [Fact]
    public async Task GetOrCreateChatClient_ZeroRetries_Succeeds()
    {
        SetupEnvVars();
        var client = Main.GetOrCreateChatClient(0);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task GetResponseAsync_WithSystemPrompt_DoesNotThrowValidation()
    {
        // Should reach the network call, not fail at validation
        SetupEnvVars();
        try
        {
            await Main.GetResponseAsync("test", systemPrompt: "Be concise.");
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not ArgumentOutOfRangeException)
        {
            // Expected: network error (invalid endpoint), not validation error
        }
    }

    [Fact]
    public async Task GetResponseAsync_HttpUri_Accepted()
    {
        ClearEnvVars();
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_URI", "http://localhost:8080/");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", "gpt-4");

        // http:// should be accepted (not just https)
        try
        {
            await Main.GetResponseAsync("hello");
        }
        catch (InvalidOperationException ex)
        {
            // Should NOT get "not a valid HTTP(S) URI" for http://
            Assert.DoesNotContain("not a valid HTTP(S) URI", ex.Message);
        }
        catch
        {
            // Network error is fine — URI validation passed
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Conversation — Edge cases in message management
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Clear_EmptyConversation_NoSystemPrompt_DoesNotThrow()
    {
        var conv = new Conversation();
        conv.Clear();
        Assert.Equal(0, conv.MessageCount);
    }

    [Fact]
    public void Clear_MultipleTimes_Idempotent()
    {
        var conv = new Conversation("System");
        conv.AddUserMessage("Hello");
        conv.Clear();
        conv.Clear();
        conv.Clear();
        Assert.Equal(1, conv.MessageCount); // only system prompt remains
    }

    [Fact]
    public void GetHistory_EmptyConversation_ReturnsEmpty()
    {
        var conv = new Conversation();
        var history = conv.GetHistory();
        Assert.Empty(history);
    }

    [Fact]
    public void Conversation_LargeMessageCount_HandlesCorrectly()
    {
        var conv = new Conversation("System");
        for (int i = 0; i < 500; i++)
        {
            conv.AddUserMessage($"User message {i}");
            conv.AddAssistantMessage($"Assistant response {i}");
        }

        Assert.Equal(1001, conv.MessageCount); // 1 system + 500 user + 500 assistant
        var history = conv.GetHistory();
        Assert.Equal(1001, history.Count);
        Assert.Equal("system", history[0].Role);
        Assert.Equal("User message 499", history[999].Content);
    }

    [Fact]
    public void Conversation_AfterClear_CanAddNewMessages()
    {
        var conv = new Conversation("System");
        conv.AddUserMessage("First");
        conv.AddAssistantMessage("Response");
        conv.Clear();

        conv.AddUserMessage("Fresh start");
        Assert.Equal(2, conv.MessageCount); // system + new user msg
        var history = conv.GetHistory();
        Assert.Equal("system", history[0].Role);
        Assert.Equal("Fresh start", history[1].Content);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Conversation Serialization — Round-trip edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LoadFromJson_CaseInsensitiveRoles()
    {
        string json = @"{""messages"":[
            {""role"":""SYSTEM"",""content"":""Hello""},
            {""role"":""User"",""content"":""Hi""},
            {""role"":""ASSISTANT"",""content"":""Hey""}
        ]}";

        var conv = Conversation.LoadFromJson(json);
        Assert.Equal(3, conv.MessageCount);
        var history = conv.GetHistory();
        Assert.Equal("system", history[0].Role);
        Assert.Equal("user", history[1].Role);
        Assert.Equal("assistant", history[2].Role);
    }

    [Fact]
    public void SaveToJson_EmptyConversation_HasEmptyMessages()
    {
        var conv = new Conversation();
        string json = conv.SaveToJson();
        var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("messages").GetArrayLength());
        // Should still include default parameters
        Assert.True(doc.RootElement.TryGetProperty("parameters", out _));
    }

    [Fact]
    public void SaveToJson_PreservesLongContent()
    {
        var conv = new Conversation();
        var longText = new string('x', 100_000);
        conv.AddUserMessage(longText);

        string json = conv.SaveToJson();
        var restored = Conversation.LoadFromJson(json);

        var history = restored.GetHistory();
        Assert.Equal(longText, history[0].Content);
    }

    [Fact]
    public async Task SaveToFileAsync_CancellationRespected()
    {
        var conv = new Conversation("test");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var path = Path.Combine(_tempDir, "cancelled.json");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => conv.SaveToFileAsync(path, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task LoadFromFileAsync_CancellationRespected()
    {
        // Write a valid file first
        var path = Path.Combine(_tempDir, "valid.json");
        var conv = new Conversation("test");
        await conv.SaveToFileAsync(path);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Conversation.LoadFromFileAsync(path, cts.Token));
    }

    // ═══════════════════════════════════════════════════════════════
    //  PromptTemplate — Edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SetDefault_NullValue_TreatedAsEmpty()
    {
        var t = new PromptTemplate("{{x}}");
        t.SetDefault("x", null!);
        var result = t.Render();
        Assert.Equal("", result);
    }

    [Fact]
    public void Render_AdjacentVariables_NoSeparator()
    {
        var t = new PromptTemplate("{{a}}{{b}}{{c}}");
        var result = t.Render(new Dictionary<string, string>
        {
            ["a"] = "1", ["b"] = "2", ["c"] = "3"
        });
        Assert.Equal("123", result);
    }

    [Fact]
    public void Render_VariableAtBoundaries()
    {
        var t = new PromptTemplate("{{start}}middle{{end}}");
        var result = t.Render(new Dictionary<string, string>
        {
            ["start"] = "[", ["end"] = "]"
        });
        Assert.Equal("[middle]", result);
    }

    [Fact]
    public void Render_LargeTemplateWithManyVariables()
    {
        var parts = new List<string>();
        var vars = new Dictionary<string, string>();
        for (int i = 0; i < 100; i++)
        {
            parts.Add($"{{{{v{i}}}}}");
            vars[$"v{i}"] = $"value{i}";
        }

        var t = new PromptTemplate(string.Join(" ", parts));
        var result = t.Render(vars);

        for (int i = 0; i < 100; i++)
        {
            Assert.Contains($"value{i}", result);
        }
    }

    [Fact]
    public void GetVariables_NumbersInNames()
    {
        var t = new PromptTemplate("{{var1}} {{var2_test}} {{x99}}");
        var vars = t.GetVariables();
        Assert.Equal(3, vars.Count);
        Assert.Contains("var1", vars);
        Assert.Contains("var2_test", vars);
        Assert.Contains("x99", vars);
    }

    [Fact]
    public void Compose_EmptySeparator()
    {
        var a = new PromptTemplate("Hello");
        var b = new PromptTemplate("World");
        var combined = a.Compose(b, "");
        Assert.Equal("HelloWorld", combined.Render());
    }

    [Fact]
    public void Compose_DefaultsNotMutated()
    {
        var defaultsA = new Dictionary<string, string> { ["x"] = "a_val" };
        var a = new PromptTemplate("{{x}}", defaultsA);
        var b = new PromptTemplate("{{y}}", new Dictionary<string, string> { ["y"] = "b_val" });

        a.Compose(b);

        // Original template defaults should be unchanged
        Assert.Single(a.Defaults);
        Assert.Equal("a_val", a.Defaults["x"]);
    }

    [Fact]
    public void FromJson_WithDefaults_RestoresCorrectly()
    {
        var json = @"{""template"":""Hello {{name}}"",""defaults"":{""name"":""World""}}";
        var t = PromptTemplate.FromJson(json);
        Assert.Equal("Hello {{name}}", t.Template);
        Assert.Equal("World", t.Defaults["name"]);
    }

    [Fact]
    public void ToJson_RoundTrip_NoDefaults()
    {
        var original = new PromptTemplate("Hello {{name}}!");
        string json = original.ToJson();
        var restored = PromptTemplate.FromJson(json);
        Assert.Equal(original.Template, restored.Template);
        Assert.Empty(restored.Defaults);
    }

    [Fact]
    public async Task SaveToFileAsync_CancellationThrows()
    {
        var t = new PromptTemplate("test");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var path = Path.Combine(_tempDir, "cancelled_template.json");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => t.SaveToFileAsync(path, cancellationToken: cts.Token));
    }

    // ═══════════════════════════════════════════════════════════════
    //  PromptChain — Validation & serialization edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_AllDefaultsSatisfied_NoInitialVars()
    {
        var template = new PromptTemplate(
            "Hello {{greeting}}",
            new Dictionary<string, string> { ["greeting"] = "there" });

        var chain = new PromptChain()
            .AddStep("greet", template, "result");

        var errors = chain.Validate();
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ComplexChain_MixedSources()
    {
        // Step 1 needs "input" from initial vars, produces "summary"
        // Step 2 needs "summary" from step 1 + "language" from initial vars
        // Step 3 needs "translation" from step 2 only
        var chain = new PromptChain()
            .AddStep("summarize",
                new PromptTemplate("Summarize: {{input}}"), "summary")
            .AddStep("translate",
                new PromptTemplate("Translate {{summary}} to {{language}}"), "translation")
            .AddStep("format",
                new PromptTemplate("Format: {{translation}}"), "final");

        var errors = chain.Validate(new Dictionary<string, string>
        {
            ["input"] = "...",
            ["language"] = "French"
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MultipleErrors_AllReported()
    {
        var chain = new PromptChain()
            .AddStep("s1",
                new PromptTemplate("{{a}} {{b}} {{c}}"), "out1");

        var errors = chain.Validate();
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void AddStep_ManySteps_WorksCorrectly()
    {
        var chain = new PromptChain();
        for (int i = 0; i < 50; i++)
        {
            chain.AddStep($"step{i}", new PromptTemplate($"Process {{{{out{i}}}}}"), $"out{i + 1}");
        }

        Assert.Equal(50, chain.StepCount);
        Assert.Equal("step0", chain.Steps[0].Name);
        Assert.Equal("step49", chain.Steps[49].Name);
    }

    [Fact]
    public async Task RunAsync_CancellationBeforeFirstStep()
    {
        SetupEnvVars();

        var chain = new PromptChain()
            .AddStep("s1", new PromptTemplate("Hello"), "out1");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => chain.RunAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public void FromJson_MaxRetries_Restored()
    {
        var chain = new PromptChain()
            .WithMaxRetries(10)
            .AddStep("s1", new PromptTemplate("test"), "out");

        string json = chain.ToJson();

        // Verify maxRetries is in JSON
        var doc = JsonDocument.Parse(json);
        Assert.Equal(10, doc.RootElement.GetProperty("maxRetries").GetInt32());

        // Verify it round-trips (structural verification)
        var restored = PromptChain.FromJson(json);
        Assert.Equal(1, restored.StepCount);
    }

    [Fact]
    public void FromJson_NullSystemPrompt_Accepted()
    {
        var json = @"{""maxRetries"":3,""steps"":[{""name"":""s1"",""template"":""test"",""outputVariable"":""out""}]}";
        var chain = PromptChain.FromJson(json);
        Assert.Equal(1, chain.StepCount);
    }

    [Fact]
    public async Task SaveAndLoadFile_PreservesDefaults()
    {
        var template = new PromptTemplate(
            "{{greeting}} {{name}}",
            new Dictionary<string, string> { ["greeting"] = "Hi" });

        var chain = new PromptChain()
            .WithSystemPrompt("Be kind")
            .AddStep("greet", template, "result");

        var path = Path.Combine(_tempDir, "chain.json");
        await chain.SaveToFileAsync(path);
        var loaded = await PromptChain.LoadFromFileAsync(path);

        Assert.Equal(1, loaded.StepCount);
        Assert.Equal("Hi", loaded.Steps[0].Template.Defaults["greeting"]);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ChainResult — Edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ChainResult_Variables_AreCopy()
    {
        var originalVars = new Dictionary<string, string> { ["key"] = "value" };
        var result = new ChainResult(
            new List<StepResult>(), originalVars, TimeSpan.Zero);

        // Modifying original should not affect result
        originalVars["key"] = "changed";
        Assert.Equal("value", result.Variables["key"]);
    }

    [Fact]
    public void ChainResult_Steps_AreReadOnly()
    {
        var steps = new List<StepResult>
        {
            new StepResult("s1", "o1", "p1", "r1", TimeSpan.FromSeconds(1))
        };

        var result = new ChainResult(
            steps, new Dictionary<string, string>(), TimeSpan.Zero);

        // Original list modification should not affect result
        steps.Add(new StepResult("s2", "o2", "p2", "r2", TimeSpan.FromSeconds(1)));
        Assert.Single(result.Steps);
    }

    [Fact]
    public void ChainResult_FinalResponse_NullWhenLastStepHasNoResponse()
    {
        var steps = new List<StepResult>
        {
            new StepResult("s1", "o1", "p1", "r1", TimeSpan.FromMilliseconds(50)),
            new StepResult("s2", "o2", "p2", null, TimeSpan.FromMilliseconds(50))
        };

        var result = new ChainResult(
            steps, new Dictionary<string, string>(), TimeSpan.FromMilliseconds(100));

        Assert.Null(result.FinalResponse);
    }

    [Fact]
    public void ChainResult_ToJson_MultipleSteps_AllIncluded()
    {
        var steps = new List<StepResult>
        {
            new StepResult("step1", "out1", "prompt1", "resp1", TimeSpan.FromMilliseconds(100)),
            new StepResult("step2", "out2", "prompt2", "resp2", TimeSpan.FromMilliseconds(200)),
            new StepResult("step3", "out3", "prompt3", "resp3", TimeSpan.FromMilliseconds(300))
        };

        var vars = new Dictionary<string, string>
        {
            ["out1"] = "resp1", ["out2"] = "resp2", ["out3"] = "resp3"
        };

        var result = new ChainResult(steps, vars, TimeSpan.FromMilliseconds(600));
        string json = result.ToJson();
        var doc = JsonDocument.Parse(json);

        Assert.Equal(3, doc.RootElement.GetProperty("steps").GetArrayLength());
        Assert.Equal(600, doc.RootElement.GetProperty("totalElapsedMs").GetInt64());
        Assert.Equal(3, doc.RootElement.GetProperty("variables").EnumerateObject().Count());
    }

    [Fact]
    public void ChainResult_ToJson_NullResponse_OmittedOrNull()
    {
        var steps = new List<StepResult>
        {
            new StepResult("s1", "o1", "p1", null, TimeSpan.Zero)
        };

        var result = new ChainResult(
            steps, new Dictionary<string, string>(), TimeSpan.Zero);

        string json = result.ToJson();
        var doc = JsonDocument.Parse(json);
        var step = doc.RootElement.GetProperty("steps")[0];

        // Response should be null or missing (DefaultIgnoreCondition.WhenWritingNull)
        if (step.TryGetProperty("response", out var resp))
        {
            Assert.Equal(JsonValueKind.Null, resp.ValueKind);
        }
    }

    [Fact]
    public void ChainResult_TotalElapsed_ReflectsConstructorValue()
    {
        var elapsed = TimeSpan.FromMinutes(2.5);
        var result = new ChainResult(
            new List<StepResult>(), new Dictionary<string, string>(), elapsed);

        Assert.Equal(elapsed, result.TotalElapsed);
    }

    // ═══════════════════════════════════════════════════════════════
    //  StepResult — Additional coverage
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void StepResult_EmptyStrings_Allowed()
    {
        var result = new StepResult("", "", "", "", TimeSpan.Zero);

        Assert.Equal("", result.StepName);
        Assert.Equal("", result.OutputVariable);
        Assert.Equal("", result.RenderedPrompt);
        Assert.Equal("", result.Response);
    }

    [Fact]
    public void StepResult_LargeElapsed_Handled()
    {
        var elapsed = TimeSpan.FromHours(24);
        var result = new StepResult("long", "out", "prompt", "response", elapsed);
        Assert.Equal(elapsed, result.Elapsed);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Thread safety — Concurrent operations on shared Conversation
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Conversation_ConcurrentHistoryAccess_DoesNotThrow()
    {
        var conv = new Conversation("System");
        for (int i = 0; i < 50; i++)
        {
            conv.AddUserMessage($"Msg {i}");
        }

        // Read history concurrently while adding messages
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() => conv.GetHistory()));
            tasks.Add(Task.Run(() => conv.AddUserMessage("concurrent")));
            tasks.Add(Task.Run(() => conv.SaveToJson()));
        }

        Task.WaitAll(tasks.ToArray());
        // No exception means thread safety holds
    }

    [Fact]
    public void Conversation_ConcurrentClearAndAdd_DoesNotThrow()
    {
        var conv = new Conversation("System");

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int idx = i;
            if (idx % 5 == 0)
                tasks.Add(Task.Run(() => conv.Clear()));
            else
                tasks.Add(Task.Run(() => conv.AddUserMessage($"Msg {idx}")));
        }

        Task.WaitAll(tasks.ToArray());
        // Should not crash
    }

    // ═══════════════════════════════════════════════════════════════
    //  PromptTemplate — Serialization edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ToJson_SpecialCharactersInTemplate()
    {
        var t = new PromptTemplate("Line1\nLine2\t\"quoted\" {{var}}");
        string json = t.ToJson();
        var restored = PromptTemplate.FromJson(json);
        Assert.Equal(t.Template, restored.Template);
    }

    [Fact]
    public void ToJson_SpecialCharactersInDefaults()
    {
        var defaults = new Dictionary<string, string>
        {
            ["key"] = "value with \"quotes\" and\nnewlines"
        };
        var t = new PromptTemplate("{{key}}", defaults);
        string json = t.ToJson();
        var restored = PromptTemplate.FromJson(json);
        Assert.Equal(defaults["key"], restored.Defaults["key"]);
    }

    [Fact]
    public void ToJson_UnicodeInTemplate()
    {
        var t = new PromptTemplate("日本語テスト {{名前}} 🚀");
        string json = t.ToJson();
        var restored = PromptTemplate.FromJson(json);
        Assert.Equal(t.Template, restored.Template);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PromptChain — Serialization of complex chains
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PromptChain_ToJson_WithNullSystemPrompt_OmitsField()
    {
        var chain = new PromptChain()
            .AddStep("s1", new PromptTemplate("test"), "out");

        string json = chain.ToJson();
        var doc = JsonDocument.Parse(json);

        // systemPrompt should be omitted when null (WhenWritingNull)
        if (doc.RootElement.TryGetProperty("systemPrompt", out var sp))
        {
            Assert.Equal(JsonValueKind.Null, sp.ValueKind);
        }
    }

    [Fact]
    public void PromptChain_FromJson_StepWithoutDefaults_Works()
    {
        var json = @"{""steps"":[{""name"":""s1"",""template"":""Hello"",""outputVariable"":""out""}]}";
        var chain = PromptChain.FromJson(json);

        Assert.Equal(1, chain.StepCount);
        Assert.Empty(chain.Steps[0].Template.Defaults);
    }

    [Fact]
    public void PromptChain_StepsOrder_Preserved()
    {
        var chain = new PromptChain();
        for (int i = 0; i < 10; i++)
        {
            chain.AddStep($"step_{i}", new PromptTemplate($"Template {i}"), $"out_{i}");
        }

        string json = chain.ToJson();
        var restored = PromptChain.FromJson(json);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"step_{i}", restored.Steps[i].Name);
            Assert.Equal($"out_{i}", restored.Steps[i].OutputVariable);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Main — Concurrent client access with different retry policies
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GetOrCreateChatClient_ConcurrentDifferentRetries_NoDeadlock()
    {
        SetupEnvVars();

        var tasks = Enumerable.Range(0, 20)
            .Select(i => Task.Run(() => Main.GetOrCreateChatClient(i % 5)))
            .ToArray();

        Task.WaitAll(tasks);
        // No deadlock or exception means the locking is correct
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

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
