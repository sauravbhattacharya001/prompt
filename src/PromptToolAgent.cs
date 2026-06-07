namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    // ── Models ────────────────────────────────────────────────

    /// <summary>Represents a tool the agent can call.</summary>
    public sealed class AgentTool
    {
        /// <summary>Creates a new agent tool.</summary>
        /// <param name="name">Unique tool name (e.g., "get_weather").</param>
        /// <param name="description">What the tool does.</param>
        /// <param name="execute">Function that takes JSON args and returns a result string.</param>
        public AgentTool(string name, string description, Func<string, CancellationToken, Task<string>> execute)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            Parameters = new List<AgentToolParameter>();
        }

        /// <summary>Tool name.</summary>
        public string Name { get; }

        /// <summary>Human-readable description.</summary>
        public string Description { get; }

        /// <summary>Function to execute when this tool is called.</summary>
        public Func<string, CancellationToken, Task<string>> Execute { get; }

        /// <summary>Parameter definitions for the tool.</summary>
        public List<AgentToolParameter> Parameters { get; }

        /// <summary>Adds a parameter definition to this tool.</summary>
        public AgentTool AddParameter(string name, string type, string description, bool required = true)
        {
            Parameters.Add(new AgentToolParameter(name, type, description, required));
            return this;
        }
    }

    /// <summary>Parameter definition for an agent tool.</summary>
    public sealed class AgentToolParameter
    {
        /// <summary>Creates a tool parameter.</summary>
        public AgentToolParameter(string name, string type, string description, bool required = true)
        {
            Name = name;
            Type = type;
            Description = description;
            Required = required;
        }

        /// <summary>Parameter name.</summary>
        public string Name { get; }

        /// <summary>JSON Schema type (string, number, integer, boolean, array, object).</summary>
        public string Type { get; }

        /// <summary>Parameter description.</summary>
        public string Description { get; }

        /// <summary>Whether the parameter is required.</summary>
        public bool Required { get; }
    }

    /// <summary>A tool call parsed from model output.</summary>
    public sealed class ToolCall
    {
        /// <summary>Unique ID for this call (provider-assigned or generated).</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>Name of the tool to call.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>JSON-encoded arguments.</summary>
        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = "{}";
    }

    /// <summary>Result from executing a tool call.</summary>
    public sealed class ToolResult
    {
        /// <summary>The tool call that produced this result.</summary>
        public ToolCall Call { get; set; } = new();

        /// <summary>The result string from execution.</summary>
        public string Output { get; set; } = "";

        /// <summary>Whether the tool execution succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error message if execution failed.</summary>
        public string? Error { get; set; }

        /// <summary>Execution duration.</summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>Represents one turn in the agent loop.</summary>
    public sealed class AgentTurn
    {
        /// <summary>Turn number (1-based).</summary>
        public int TurnNumber { get; set; }

        /// <summary>The model's raw response text for this turn.</summary>
        public string ModelResponse { get; set; } = "";

        /// <summary>Tool calls the model requested.</summary>
        public List<ToolCall> ToolCalls { get; set; } = new();

        /// <summary>Results from executing tool calls.</summary>
        public List<ToolResult> ToolResults { get; set; } = new();

        /// <summary>Whether this turn produced a final answer (no tool calls).</summary>
        public bool IsFinalAnswer => ToolCalls.Count == 0;

        /// <summary>Duration of this turn (model call + tool execution).</summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>Final result from the agent loop.</summary>
    public sealed class AgentResult
    {
        /// <summary>The final text answer from the agent.</summary>
        public string FinalAnswer { get; set; } = "";

        /// <summary>All turns in the agent loop.</summary>
        public List<AgentTurn> Turns { get; set; } = new();

        /// <summary>Total number of turns (including the final answer turn).</summary>
        public int TotalTurns => Turns.Count;

        /// <summary>Total number of tool calls made across all turns.</summary>
        public int TotalToolCalls => Turns.Sum(t => t.ToolCalls.Count);

        /// <summary>Total execution duration.</summary>
        public TimeSpan TotalDuration { get; set; }

        /// <summary>Whether the agent completed within the max turns limit.</summary>
        public bool Completed { get; set; } = true;

        /// <summary>Reason for stopping if not completed normally.</summary>
        public string? StopReason { get; set; }
    }

    /// <summary>Configuration for the agent loop.</summary>
    public sealed class AgentOptions
    {
        /// <summary>Maximum number of turns before stopping. Default 10.</summary>
        public int MaxTurns { get; set; } = 10;

        /// <summary>System prompt prepended to the conversation.</summary>
        public string? SystemPrompt { get; set; }

        /// <summary>Whether to allow parallel tool execution. Default true.</summary>
        public bool ParallelToolExecution { get; set; } = true;

        /// <summary>Timeout per tool execution. Default 30 seconds.</summary>
        public TimeSpan ToolTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Called when a turn completes. Useful for logging/streaming.</summary>
        public Action<AgentTurn>? OnTurnCompleted { get; set; }

        /// <summary>Called before a tool is executed.</summary>
        public Func<ToolCall, bool>? OnBeforeToolExecution { get; set; }
    }

    /// <summary>
    /// Agentic tool-use loop: sends a prompt to a model, parses tool calls from
    /// the response, executes them, feeds results back, and repeats until the
    /// model produces a final answer (no tool calls) or max turns is reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the core "ReAct" pattern: Reason → Act → Observe → Repeat.
    /// The agent maintains a conversation history, appending tool results after
    /// each execution, so the model has full context of what it's already tried.
    /// </para>
    /// <para>
    /// The model function you provide should handle the actual LLM API call.
    /// The tool call parser handles OpenAI-style function calling format by default,
    /// or you can provide a custom parser for other providers.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var agent = new PromptToolAgent();
    /// agent.AddTool(new AgentTool("get_weather", "Get weather for a city",
    ///     async (args, ct) => { return "{\"temp\": 72, \"condition\": \"sunny\"}"; })
    ///     .AddParameter("city", "string", "City name", required: true));
    ///
    /// var result = await agent.RunAsync(
    ///     "What's the weather in Seattle?",
    ///     modelFunc: async (messages, tools, ct) => {
    ///         // Call your LLM here, return the response
    ///         return await myLlmClient.ChatAsync(messages, tools, ct);
    ///     });
    ///
    /// Console.WriteLine(result.FinalAnswer);
    /// Console.WriteLine($"Took {result.TotalTurns} turns, {result.TotalToolCalls} tool calls");
    /// </code>
    /// </example>
    public class PromptToolAgent
    {
        private readonly Dictionary<string, AgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a new tool agent.</summary>
        public PromptToolAgent() { }

        /// <summary>Creates a new tool agent with options.</summary>
        public PromptToolAgent(AgentOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Agent options.</summary>
        public AgentOptions Options { get; set; } = new();

        /// <summary>Registered tools.</summary>
        public IReadOnlyDictionary<string, AgentTool> Tools => _tools;

        /// <summary>Registers a tool with the agent.</summary>
        /// <param name="tool">The tool to register.</param>
        /// <returns>This agent for fluent chaining.</returns>
        public PromptToolAgent AddTool(AgentTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
            return this;
        }

        /// <summary>Removes a registered tool.</summary>
        public bool RemoveTool(string name) => _tools.Remove(name);

        /// <summary>
        /// Runs the agentic loop: sends the user message to the model, parses tool
        /// calls, executes them, feeds results back, and repeats until a final answer.
        /// </summary>
        /// <param name="userMessage">The user's input message.</param>
        /// <param name="modelFunc">
        /// Function that calls the LLM. Receives conversation messages (as JSON-like list),
        /// tool definitions, and returns the model's response string.
        /// If the model wants to call tools, the response should contain tool calls
        /// in OpenAI format or as parsed by <paramref name="toolCallParser"/>.
        /// </param>
        /// <param name="toolCallParser">
        /// Optional custom parser to extract tool calls from model output.
        /// Default handles OpenAI-style JSON tool_calls.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The agent result with final answer and full turn history.</returns>
        public async Task<AgentResult> RunAsync(
            string userMessage,
            Func<List<ConversationMessage>, List<AgentTool>, CancellationToken, Task<string>> modelFunc,
            Func<string, List<ToolCall>>? toolCallParser = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                throw new ArgumentException("User message cannot be empty.", nameof(userMessage));
            if (modelFunc == null)
                throw new ArgumentNullException(nameof(modelFunc));

            var parser = toolCallParser ?? DefaultToolCallParser;
            var totalSw = Stopwatch.StartNew();
            var result = new AgentResult();

            // Build initial conversation
            var messages = new List<ConversationMessage>();
            if (!string.IsNullOrEmpty(Options.SystemPrompt))
            {
                messages.Add(new ConversationMessage("system", Options.SystemPrompt));
            }
            messages.Add(new ConversationMessage("user", userMessage));

            var toolList = _tools.Values.ToList();

            for (int turn = 1; turn <= Options.MaxTurns; turn++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var turnSw = Stopwatch.StartNew();

                // Call model
                string modelResponse = await modelFunc(messages, toolList, cancellationToken);

                // Parse tool calls
                var toolCalls = parser(modelResponse);

                var agentTurn = new AgentTurn
                {
                    TurnNumber = turn,
                    ModelResponse = modelResponse,
                    ToolCalls = toolCalls
                };

                if (toolCalls.Count == 0)
                {
                    // Final answer — no tool calls
                    agentTurn.Duration = turnSw.Elapsed;
                    result.Turns.Add(agentTurn);
                    result.FinalAnswer = modelResponse;
                    Options.OnTurnCompleted?.Invoke(agentTurn);
                    break;
                }

                // Execute tool calls
                messages.Add(new ConversationMessage("assistant", modelResponse));
                var toolResults = await ExecuteToolCallsAsync(toolCalls, cancellationToken);
                agentTurn.ToolResults = toolResults;
                agentTurn.Duration = turnSw.Elapsed;

                // Append tool results to conversation
                foreach (var tr in toolResults)
                {
                    var content = tr.Success ? tr.Output : $"Error: {tr.Error}";
                    messages.Add(new ConversationMessage("tool", content, tr.Call.Name, tr.Call.Id));
                }

                result.Turns.Add(agentTurn);
                Options.OnTurnCompleted?.Invoke(agentTurn);

                // Check if we've hit max turns
                if (turn == Options.MaxTurns)
                {
                    result.Completed = false;
                    result.StopReason = $"Reached maximum turns ({Options.MaxTurns})";
                    result.FinalAnswer = modelResponse;
                }
            }

            totalSw.Stop();
            result.TotalDuration = totalSw.Elapsed;
            return result;
        }

        private async Task<List<ToolResult>> ExecuteToolCallsAsync(
            List<ToolCall> toolCalls, CancellationToken cancellationToken)
        {
            var results = new List<ToolResult>();

            if (Options.ParallelToolExecution && toolCalls.Count > 1)
            {
                var tasks = toolCalls.Select(tc => ExecuteSingleToolAsync(tc, cancellationToken));
                var completed = await Task.WhenAll(tasks);
                results.AddRange(completed);
            }
            else
            {
                foreach (var tc in toolCalls)
                {
                    results.Add(await ExecuteSingleToolAsync(tc, cancellationToken));
                }
            }

            return results;
        }

        private async Task<ToolResult> ExecuteSingleToolAsync(
            ToolCall toolCall, CancellationToken cancellationToken)
        {
            // Check permission
            if (Options.OnBeforeToolExecution != null && !Options.OnBeforeToolExecution(toolCall))
            {
                return new ToolResult
                {
                    Call = toolCall,
                    Success = false,
                    Error = "Tool execution blocked by OnBeforeToolExecution handler."
                };
            }

            if (!_tools.TryGetValue(toolCall.Name, out var tool))
            {
                return new ToolResult
                {
                    Call = toolCall,
                    Success = false,
                    Error = $"Unknown tool: {toolCall.Name}"
                };
            }

            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(Options.ToolTimeout);

                string output = await tool.Execute(toolCall.Arguments, cts.Token);
                sw.Stop();

                return new ToolResult
                {
                    Call = toolCall,
                    Output = output,
                    Success = true,
                    Duration = sw.Elapsed
                };
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return new ToolResult
                {
                    Call = toolCall,
                    Success = false,
                    Error = "Tool execution timed out.",
                    Duration = sw.Elapsed
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ToolResult
                {
                    Call = toolCall,
                    Success = false,
                    Error = ex.Message,
                    Duration = sw.Elapsed
                };
            }
        }

        /// <summary>
        /// Default parser for tool calls. Handles OpenAI-style JSON format:
        /// <code>
        /// [{"name": "tool_name", "arguments": "{...}"}]
        /// </code>
        /// Also handles markdown-fenced JSON blocks with tool_calls.
        /// </summary>
        public static List<ToolCall> DefaultToolCallParser(string modelResponse)
        {
            var calls = new List<ToolCall>();
            if (string.IsNullOrWhiteSpace(modelResponse)) return calls;

            // Try parsing as direct JSON array of tool calls
            try
            {
                // Look for ```json blocks first
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                    modelResponse, @"```(?:json)?\s*(\[[\s\S]*?\])\s*```");

                string jsonText = jsonMatch.Success ? jsonMatch.Groups[1].Value : modelResponse;

                // Try to parse as tool_calls wrapper: {"tool_calls": [...]}
                if (jsonText.TrimStart().StartsWith("{"))
                {
                    using var doc = JsonDocument.Parse(jsonText);
                    if (doc.RootElement.TryGetProperty("tool_calls", out var tcArray))
                    {
                        jsonText = tcArray.GetRawText();
                    }
                }

                // Parse as array of tool calls
                if (jsonText.TrimStart().StartsWith("["))
                {
                    using var doc = JsonDocument.Parse(jsonText);
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        var call = new ToolCall();
                        if (elem.TryGetProperty("id", out var idProp))
                            call.Id = idProp.GetString() ?? call.Id;
                        if (elem.TryGetProperty("name", out var nameProp))
                            call.Name = nameProp.GetString() ?? "";
                        else if (elem.TryGetProperty("function", out var funcProp))
                        {
                            if (funcProp.TryGetProperty("name", out var fnName))
                                call.Name = fnName.GetString() ?? "";
                            if (funcProp.TryGetProperty("arguments", out var fnArgs))
                                call.Arguments = fnArgs.ValueKind == JsonValueKind.String
                                    ? fnArgs.GetString() ?? "{}"
                                    : fnArgs.GetRawText();
                        }
                        if (elem.TryGetProperty("arguments", out var argsProp))
                            call.Arguments = argsProp.ValueKind == JsonValueKind.String
                                ? argsProp.GetString() ?? "{}"
                                : argsProp.GetRawText();
                        if (!string.IsNullOrEmpty(call.Name))
                            calls.Add(call);
                    }
                }
            }
            catch (JsonException)
            {
                // Not valid JSON — no tool calls
            }

            return calls;
        }
    }

    /// <summary>A message in the agent's conversation history.</summary>
    public sealed class ConversationMessage
    {
        /// <summary>Creates a conversation message.</summary>
        public ConversationMessage(string role, string content, string? toolName = null, string? toolCallId = null)
        {
            Role = role;
            Content = content;
            ToolName = toolName;
            ToolCallId = toolCallId;
        }

        /// <summary>Message role: system, user, assistant, tool.</summary>
        [JsonPropertyName("role")]
        public string Role { get; set; }

        /// <summary>Message content.</summary>
        [JsonPropertyName("content")]
        public string Content { get; set; }

        /// <summary>Tool name (for role=tool messages).</summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolName { get; set; }

        /// <summary>Tool call ID this result corresponds to.</summary>
        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }
    }
}
