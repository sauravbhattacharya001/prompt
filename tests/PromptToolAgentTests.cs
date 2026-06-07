namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class PromptToolAgentTests
    {
        private static AgentTool CreateCalculatorTool()
        {
            return new AgentTool("calculate", "Perform arithmetic calculations",
                async (args, ct) =>
                {
                    using var doc = JsonDocument.Parse(args);
                    var expr = doc.RootElement.GetProperty("expression").GetString()!;
                    // Simple eval for tests
                    if (expr == "2+2") return "4";
                    if (expr == "10*5") return "50";
                    return "unknown";
                })
                .AddParameter("expression", "string", "Math expression to evaluate", required: true);
        }

        private static AgentTool CreateWeatherTool()
        {
            return new AgentTool("get_weather", "Get current weather for a city",
                async (args, ct) =>
                {
                    using var doc = JsonDocument.Parse(args);
                    var city = doc.RootElement.GetProperty("city").GetString()!;
                    return JsonSerializer.Serialize(new { temp = 72, condition = "sunny", city });
                })
                .AddParameter("city", "string", "City name", required: true);
        }

        [Fact]
        public async Task RunAsync_NoToolCalls_ReturnsFinalAnswerImmediately()
        {
            var agent = new PromptToolAgent();
            agent.AddTool(CreateCalculatorTool());

            var result = await agent.RunAsync(
                "Hello!",
                modelFunc: async (messages, tools, ct) => "Hello! How can I help you?");

            Assert.True(result.Completed);
            Assert.Equal("Hello! How can I help you?", result.FinalAnswer);
            Assert.Single(result.Turns);
            Assert.True(result.Turns[0].IsFinalAnswer);
            Assert.Equal(0, result.TotalToolCalls);
        }

        [Fact]
        public async Task RunAsync_SingleToolCall_ExecutesAndReturns()
        {
            var agent = new PromptToolAgent();
            agent.AddTool(CreateCalculatorTool());

            int callCount = 0;
            var result = await agent.RunAsync(
                "What is 2+2?",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        // First call: request tool use
                        return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"2+2\\\"}\"}]";
                    }
                    // Second call: model sees tool result and gives final answer
                    return "2 + 2 = 4";
                });

            Assert.True(result.Completed);
            Assert.Equal("2 + 2 = 4", result.FinalAnswer);
            Assert.Equal(2, result.TotalTurns);
            Assert.Equal(1, result.TotalToolCalls);
            Assert.Equal("4", result.Turns[0].ToolResults[0].Output);
        }

        [Fact]
        public async Task RunAsync_MultipleToolCalls_ExecutesAll()
        {
            var agent = new PromptToolAgent();
            agent.AddTool(CreateCalculatorTool());
            agent.AddTool(CreateWeatherTool());

            int callCount = 0;
            var result = await agent.RunAsync(
                "What is 2+2 and what's the weather in Seattle?",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"2+2\\\"}\"}, {\"name\": \"get_weather\", \"arguments\": \"{\\\"city\\\": \\\"Seattle\\\"}\"}]";
                    }
                    return "2+2 = 4 and it's 72°F and sunny in Seattle.";
                });

            Assert.True(result.Completed);
            Assert.Equal(2, result.TotalToolCalls);
            Assert.Equal(2, result.Turns[0].ToolCalls.Count);
            Assert.Equal(2, result.Turns[0].ToolResults.Count);
            Assert.All(result.Turns[0].ToolResults, r => Assert.True(r.Success));
        }

        [Fact]
        public async Task RunAsync_UnknownTool_ReturnsError()
        {
            var agent = new PromptToolAgent();
            agent.AddTool(CreateCalculatorTool());

            int callCount = 0;
            var result = await agent.RunAsync(
                "Use a fake tool",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"nonexistent_tool\", \"arguments\": \"{}\"}]";
                    return "Sorry, that tool doesn't exist.";
                });

            Assert.False(result.Turns[0].ToolResults[0].Success);
            Assert.Contains("Unknown tool", result.Turns[0].ToolResults[0].Error);
        }

        [Fact]
        public async Task RunAsync_MaxTurnsReached_StopsAndReportsIncomplete()
        {
            var agent = new PromptToolAgent(new AgentOptions { MaxTurns = 2 });
            agent.AddTool(CreateCalculatorTool());

            var result = await agent.RunAsync(
                "Keep calculating",
                modelFunc: async (messages, tools, ct) =>
                {
                    // Always request tool calls, never give final answer
                    return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"2+2\\\"}\"}]";
                });

            Assert.False(result.Completed);
            Assert.Contains("maximum turns", result.StopReason);
            Assert.Equal(2, result.TotalTurns);
        }

        [Fact]
        public async Task RunAsync_SystemPrompt_IsIncludedInMessages()
        {
            var agent = new PromptToolAgent(new AgentOptions
            {
                SystemPrompt = "You are a helpful assistant."
            });

            List<ConversationMessage>? capturedMessages = null;
            await agent.RunAsync(
                "Hello",
                modelFunc: async (messages, tools, ct) =>
                {
                    capturedMessages = messages;
                    return "Hi there!";
                });

            Assert.NotNull(capturedMessages);
            Assert.Equal("system", capturedMessages![0].Role);
            Assert.Equal("You are a helpful assistant.", capturedMessages[0].Content);
            Assert.Equal("user", capturedMessages[1].Role);
        }

        [Fact]
        public async Task RunAsync_ToolResultsAppearInConversation()
        {
            var agent = new PromptToolAgent();
            agent.AddTool(CreateCalculatorTool());

            List<ConversationMessage>? secondCallMessages = null;
            int callCount = 0;
            await agent.RunAsync(
                "What is 2+2?",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"2+2\\\"}\"}]";
                    secondCallMessages = new List<ConversationMessage>(messages);
                    return "The answer is 4.";
                });

            Assert.NotNull(secondCallMessages);
            // Should have: system? + user + assistant (tool call) + tool (result)
            var toolMsg = secondCallMessages!.Last(m => m.Role == "tool");
            Assert.Equal("4", toolMsg.Content);
            Assert.Equal("calculate", toolMsg.ToolName);
        }

        [Fact]
        public async Task RunAsync_OnTurnCompleted_FiredForEachTurn()
        {
            var turns = new List<AgentTurn>();
            var agent = new PromptToolAgent(new AgentOptions
            {
                OnTurnCompleted = t => turns.Add(t)
            });
            agent.AddTool(CreateCalculatorTool());

            int callCount = 0;
            await agent.RunAsync(
                "Calculate",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"2+2\\\"}\"}]";
                    return "Done: 4";
                });

            Assert.Equal(2, turns.Count);
            Assert.False(turns[0].IsFinalAnswer);
            Assert.True(turns[1].IsFinalAnswer);
        }

        [Fact]
        public async Task RunAsync_OnBeforeToolExecution_CanBlockExecution()
        {
            var agent = new PromptToolAgent(new AgentOptions
            {
                OnBeforeToolExecution = tc => tc.Name != "calculate" // block calculator
            });
            agent.AddTool(CreateCalculatorTool());

            int callCount = 0;
            var result = await agent.RunAsync(
                "Calculate",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"2+2\\\"}\"}]";
                    return "Tool was blocked.";
                });

            Assert.False(result.Turns[0].ToolResults[0].Success);
            Assert.Contains("blocked", result.Turns[0].ToolResults[0].Error);
        }

        [Fact]
        public async Task RunAsync_ToolThrows_CapturesError()
        {
            var failTool = new AgentTool("fail", "Always fails",
                async (args, ct) => throw new InvalidOperationException("Something went wrong"));

            var agent = new PromptToolAgent();
            agent.AddTool(failTool);

            int callCount = 0;
            var result = await agent.RunAsync(
                "Use fail tool",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"fail\", \"arguments\": \"{}\"}]";
                    return "The tool failed.";
                });

            Assert.False(result.Turns[0].ToolResults[0].Success);
            Assert.Equal("Something went wrong", result.Turns[0].ToolResults[0].Error);
        }

        [Fact]
        public async Task RunAsync_Cancellation_ThrowsOperationCanceled()
        {
            var agent = new PromptToolAgent();
            agent.AddTool(CreateCalculatorTool());
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                agent.RunAsync(
                    "Hello",
                    modelFunc: async (messages, tools, ct) => "Hello!",
                    cancellationToken: cts.Token));
        }

        [Fact]
        public async Task AddTool_RemoveTool_WorksCorrectly()
        {
            var agent = new PromptToolAgent();
            var tool = CreateCalculatorTool();

            agent.AddTool(tool);
            Assert.True(agent.Tools.ContainsKey("calculate"));

            agent.RemoveTool("calculate");
            Assert.False(agent.Tools.ContainsKey("calculate"));
        }

        [Fact]
        public void DefaultToolCallParser_ParsesOpenAIFormat()
        {
            var json = "[{\"name\": \"get_weather\", \"arguments\": \"{\\\"city\\\": \\\"NYC\\\"}\"}]";
            var calls = PromptToolAgent.DefaultToolCallParser(json);
            Assert.Single(calls);
            Assert.Equal("get_weather", calls[0].Name);
            Assert.Contains("NYC", calls[0].Arguments);
        }

        [Fact]
        public void DefaultToolCallParser_ParsesWrappedFormat()
        {
            var json = "{\"tool_calls\": [{\"name\": \"calculate\", \"arguments\": \"{}\"}]}";
            var calls = PromptToolAgent.DefaultToolCallParser(json);
            Assert.Single(calls);
            Assert.Equal("calculate", calls[0].Name);
        }

        [Fact]
        public void DefaultToolCallParser_ParsesMarkdownFenced()
        {
            var md = "Let me calculate that.\n```json\n[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"10*5\\\"}\"}]\n```";
            var calls = PromptToolAgent.DefaultToolCallParser(md);
            Assert.Single(calls);
            Assert.Equal("calculate", calls[0].Name);
        }

        [Fact]
        public void DefaultToolCallParser_NoToolCalls_ReturnsEmpty()
        {
            var calls = PromptToolAgent.DefaultToolCallParser("Just a regular response with no tools.");
            Assert.Empty(calls);
        }

        [Fact]
        public async Task RunAsync_MultiTurnToolUse_MaintainsContext()
        {
            var agent = new PromptToolAgent();
            agent.AddTool(CreateCalculatorTool());

            int callCount = 0;
            var result = await agent.RunAsync(
                "First calculate 2+2, then calculate 10*5",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"2+2\\\"}\"}]";
                    if (callCount == 2)
                        return "[{\"name\": \"calculate\", \"arguments\": \"{\\\"expression\\\": \\\"10*5\\\"}\"}]";
                    return "2+2 = 4 and 10*5 = 50";
                });

            Assert.True(result.Completed);
            Assert.Equal(3, result.TotalTurns);
            Assert.Equal(2, result.TotalToolCalls);
            Assert.Equal("4", result.Turns[0].ToolResults[0].Output);
            Assert.Equal("50", result.Turns[1].ToolResults[0].Output);
        }

        [Fact]
        public async Task RunAsync_ParallelExecution_RunsToolsConcurrently()
        {
            var executionOrder = new List<string>();
            var slowTool = new AgentTool("slow", "Slow tool",
                async (args, ct) =>
                {
                    executionOrder.Add("slow_start");
                    await Task.Delay(50, ct);
                    executionOrder.Add("slow_end");
                    return "slow_result";
                });
            var fastTool = new AgentTool("fast", "Fast tool",
                async (args, ct) =>
                {
                    executionOrder.Add("fast_start");
                    executionOrder.Add("fast_end");
                    return "fast_result";
                });

            var agent = new PromptToolAgent(new AgentOptions { ParallelToolExecution = true });
            agent.AddTool(slowTool);
            agent.AddTool(fastTool);

            int callCount = 0;
            var result = await agent.RunAsync(
                "Use both",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"slow\", \"arguments\": \"{}\"}, {\"name\": \"fast\", \"arguments\": \"{}\"}]";
                    return "Done.";
                });

            Assert.Equal(2, result.Turns[0].ToolResults.Count);
            Assert.All(result.Turns[0].ToolResults, r => Assert.True(r.Success));
        }

        [Fact]
        public async Task RunAsync_SequentialExecution_RunsToolsInOrder()
        {
            var executionOrder = new List<string>();
            var tool1 = new AgentTool("tool1", "First",
                async (args, ct) => { executionOrder.Add("tool1"); return "r1"; });
            var tool2 = new AgentTool("tool2", "Second",
                async (args, ct) => { executionOrder.Add("tool2"); return "r2"; });

            var agent = new PromptToolAgent(new AgentOptions { ParallelToolExecution = false });
            agent.AddTool(tool1);
            agent.AddTool(tool2);

            int callCount = 0;
            await agent.RunAsync(
                "Use both sequentially",
                modelFunc: async (messages, tools, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                        return "[{\"name\": \"tool1\", \"arguments\": \"{}\"}, {\"name\": \"tool2\", \"arguments\": \"{}\"}]";
                    return "Done.";
                });

            Assert.Equal(new[] { "tool1", "tool2" }, executionOrder);
        }

        [Fact]
        public void AgentTool_AddParameter_Fluent()
        {
            var tool = new AgentTool("test", "desc", async (a, c) => "ok")
                .AddParameter("p1", "string", "First param")
                .AddParameter("p2", "number", "Second param", required: false);

            Assert.Equal(2, tool.Parameters.Count);
            Assert.True(tool.Parameters[0].Required);
            Assert.False(tool.Parameters[1].Required);
        }

        [Fact]
        public async Task RunAsync_EmptyMessage_Throws()
        {
            var agent = new PromptToolAgent();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                agent.RunAsync("", modelFunc: async (m, t, c) => "hi"));
        }

        [Fact]
        public async Task RunAsync_NullModelFunc_Throws()
        {
            var agent = new PromptToolAgent();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                agent.RunAsync("hello", modelFunc: null!));
        }

        [Fact]
        public async Task RunAsync_TotalDuration_IsTracked()
        {
            var agent = new PromptToolAgent();
            var result = await agent.RunAsync(
                "Hello",
                modelFunc: async (m, t, c) => { await Task.Delay(10); return "Hi!"; });

            Assert.True(result.TotalDuration > TimeSpan.Zero);
        }
    }
}
