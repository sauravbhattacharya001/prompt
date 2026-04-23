namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Role in a simulated conversation turn.
    /// </summary>
    public enum SimRole
    {
        /// <summary>System message (instructions).</summary>
        System,
        /// <summary>User message.</summary>
        User,
        /// <summary>Assistant response.</summary>
        Assistant
    }

    /// <summary>
    /// A single turn in a conversation simulation script.
    /// </summary>
    public class SimTurn
    {
        /// <summary>Role for this turn.</summary>
        public SimRole Role { get; set; }

        /// <summary>Content of the message. Supports {{variable}} interpolation.</summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Optional condition — if set, this turn is only included when the
        /// condition key exists in the variables dictionary and is truthy
        /// (non-empty, non-"false", non-"0").
        /// </summary>
        public string? Condition { get; set; }

        /// <summary>
        /// Optional branch label. When set on a user turn, the simulator
        /// can jump to a named branch point instead of continuing linearly.
        /// </summary>
        public string? BranchTo { get; set; }

        /// <summary>
        /// Optional label marking this turn as a branch target.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Optional validation pattern (regex) for the preceding assistant
        /// response. Only meaningful on user turns — validates the last
        /// assistant reply before proceeding.
        /// </summary>
        public string? ExpectPattern { get; set; }

        /// <summary>
        /// Optional maximum token estimate for this turn's content.
        /// If the rendered content exceeds this, a warning is recorded.
        /// </summary>
        public int? MaxTokens { get; set; }
    }

    /// <summary>
    /// Result of running a conversation simulation.
    /// </summary>
    public class SimulationResult
    {
        /// <summary>The rendered conversation transcript.</summary>
        public List<SimulatedMessage> Transcript { get; set; } = new();

        /// <summary>Warnings encountered during simulation.</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Validation failures (expectation mismatches).</summary>
        public List<SimValidationFailure> ValidationFailures { get; set; } = new();

        /// <summary>Total estimated tokens across all messages.</summary>
        public int TotalEstimatedTokens { get; set; }

        /// <summary>Number of turns executed.</summary>
        public int TurnsExecuted { get; set; }

        /// <summary>Number of turns skipped due to conditions.</summary>
        public int TurnsSkipped { get; set; }

        /// <summary>Number of branches taken.</summary>
        public int BranchesTaken { get; set; }

        /// <summary>Whether the simulation completed without validation failures.</summary>
        public bool Success => ValidationFailures.Count == 0;

        /// <summary>Variable state at the end of simulation.</summary>
        public Dictionary<string, string> FinalVariables { get; set; } = new();

        /// <summary>Format the transcript as readable text.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            foreach (var msg in Transcript)
            {
                sb.AppendLine($"[{msg.Role}] {msg.Content}");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Format the transcript as OpenAI chat-format JSON.</summary>
        public string ToChatJson(bool indented = true)
        {
            var messages = Transcript.Select(m => new
            {
                role = m.Role.ToString().ToLowerInvariant(),
                content = m.Content
            });
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(new { messages }, options);
        }

        /// <summary>Format the transcript as JSONL (one message per line, for fine-tuning).</summary>
        public string ToJsonl()
        {
            var sb = new StringBuilder();
            var messages = Transcript.Select(m => new
            {
                role = m.Role.ToString().ToLowerInvariant(),
                content = m.Content
            }).ToArray();
            var line = JsonSerializer.Serialize(new { messages });
            sb.AppendLine(line);
            return sb.ToString();
        }
    }

    /// <summary>
    /// A rendered message in the simulation transcript.
    /// </summary>
    public class SimulatedMessage
    {
        /// <summary>Role of this message.</summary>
        public SimRole Role { get; set; }

        /// <summary>Rendered content (variables interpolated).</summary>
        public string Content { get; set; } = "";

        /// <summary>Estimated token count.</summary>
        public int EstimatedTokens { get; set; }

        /// <summary>Index of the source turn in the script.</summary>
        public int SourceTurnIndex { get; set; }
    }

    /// <summary>
    /// Records a validation failure during simulation.
    /// </summary>
    public class SimValidationFailure
    {
        /// <summary>Turn index where validation was checked.</summary>
        public int TurnIndex { get; set; }

        /// <summary>Expected regex pattern.</summary>
        public string ExpectedPattern { get; set; } = "";

        /// <summary>Actual assistant response that failed validation.</summary>
        public string ActualContent { get; set; } = "";

        /// <summary>Human-readable description.</summary>
        public override string ToString() =>
            $"Turn {TurnIndex}: expected pattern '{ExpectedPattern}' not found in: \"{StringHelpers.Truncate(ActualContent, 80)}\"";

    }

    /// <summary>
    /// Simulates multi-turn conversations locally using scripted turns with
    /// variable interpolation, conditional branches, and response validation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Useful for testing conversation flows, validating prompt chains, and
    /// prototyping chatbot behavior without calling any API. Define a script
    /// of turns, provide variables, and run the simulation to see the full
    /// rendered conversation.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var sim = new PromptConversationSimulator();
    ///
    /// sim.AddSystem("You are a {{persona}} assistant.");
    /// sim.AddUser("My name is {{name}}. Help me with {{topic}}.");
    /// sim.AddAssistant("Hello {{name}}! I'd love to help with {{topic}}.");
    /// sim.AddUser("What are the key concepts?");
    /// sim.AddAssistant("Here are the key concepts of {{topic}}: ...");
    ///
    /// var result = sim.Run(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["persona"] = "friendly coding",
    ///     ["name"] = "Alice",
    ///     ["topic"] = "async/await in C#"
    /// });
    ///
    /// Console.WriteLine(result.ToText());
    /// Console.WriteLine($"Total tokens: ~{result.TotalEstimatedTokens}");
    /// </code>
    /// </para>
    /// <para>
    /// Branching example:
    /// <code>
    /// var sim = new PromptConversationSimulator();
    /// sim.AddSystem("You are a support agent.");
    /// sim.AddUser("I have a billing issue.", condition: "billing");
    /// sim.AddAssistant("I'll help with billing. What's your account ID?");
    /// sim.AddUser("I have a technical issue.", condition: "technical", label: "tech");
    /// sim.AddAssistant("Let me troubleshoot. What error do you see?");
    ///
    /// // Run billing path
    /// var billing = sim.Run(new() { ["billing"] = "true" });
    ///
    /// // Run technical path
    /// var tech = sim.Run(new() { ["technical"] = "true" });
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptConversationSimulator
    {
        private readonly List<SimTurn> _script = new();
        private int _maxTurns = 200;

        /// <summary>Maximum turns to execute (prevents infinite loops from branching).</summary>
        /// <exception cref="ArgumentOutOfRangeException">If value is less than 1.</exception>
        public int MaxTurns
        {
            get => _maxTurns;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "MaxTurns must be at least 1.");
                _maxTurns = value;
            }
        }

        /// <summary>The current script of turns.</summary>
        public IReadOnlyList<SimTurn> Script => _script.AsReadOnly();

        /// <summary>Add a system message turn.</summary>
        public PromptConversationSimulator AddSystem(
            string content,
            string? condition = null,
            string? label = null,
            int? maxTokens = null)
        {
            _script.Add(new SimTurn
            {
                Role = SimRole.System,
                Content = content,
                Condition = condition,
                Label = label,
                MaxTokens = maxTokens
            });
            return this;
        }

        /// <summary>Add a user message turn.</summary>
        public PromptConversationSimulator AddUser(
            string content,
            string? condition = null,
            string? label = null,
            string? branchTo = null,
            string? expectPattern = null,
            int? maxTokens = null)
        {
            _script.Add(new SimTurn
            {
                Role = SimRole.User,
                Content = content,
                Condition = condition,
                Label = label,
                BranchTo = branchTo,
                ExpectPattern = expectPattern,
                MaxTokens = maxTokens
            });
            return this;
        }

        /// <summary>Add an assistant response turn.</summary>
        public PromptConversationSimulator AddAssistant(
            string content,
            string? condition = null,
            string? label = null,
            int? maxTokens = null)
        {
            _script.Add(new SimTurn
            {
                Role = SimRole.Assistant,
                Content = content,
                Condition = condition,
                Label = label,
                MaxTokens = maxTokens
            });
            return this;
        }

        /// <summary>Add an arbitrary turn.</summary>
        public PromptConversationSimulator AddTurn(SimTurn turn)
        {
            _script.Add(turn ?? throw new ArgumentNullException(nameof(turn)));
            return this;
        }

        /// <summary>Clear all turns from the script.</summary>
        public PromptConversationSimulator Clear()
        {
            _script.Clear();
            return this;
        }

        /// <summary>
        /// Run the simulation with the given variables.
        /// </summary>
        /// <param name="variables">Variable values for {{key}} interpolation.</param>
        /// <returns>Simulation result with transcript, warnings, and stats.</returns>
        public SimulationResult Run(Dictionary<string, string>? variables = null)
        {
            var vars = variables != null
                ? new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var result = new SimulationResult();
            string? lastAssistantContent = null;

            // Build label index for branching
            var labelIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _script.Count; i++)
            {
                if (_script[i].Label != null)
                {
                    labelIndex[_script[i].Label!] = i;
                }
            }

            int turnIndex = 0;
            int executed = 0;

            while (turnIndex < _script.Count && executed < _maxTurns)
            {
                var turn = _script[turnIndex];

                // Check condition
                if (turn.Condition != null && !IsTruthy(vars, turn.Condition))
                {
                    result.TurnsSkipped++;
                    turnIndex++;
                    continue;
                }

                // Validate previous assistant response if ExpectPattern is set
                if (turn.ExpectPattern != null && lastAssistantContent != null)
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(
                        lastAssistantContent, turn.ExpectPattern,
                        System.Text.RegularExpressions.RegexOptions.None,
                        TimeSpan.FromSeconds(2)))
                    {
                        result.ValidationFailures.Add(new SimValidationFailure
                        {
                            TurnIndex = turnIndex,
                            ExpectedPattern = turn.ExpectPattern,
                            ActualContent = lastAssistantContent
                        });
                    }
                }

                // Render content
                string rendered = Interpolate(turn.Content, vars);
                int tokenEstimate = EstimateTokens(rendered);

                // Check token limit warning
                if (turn.MaxTokens.HasValue && tokenEstimate > turn.MaxTokens.Value)
                {
                    result.Warnings.Add(
                        $"Turn {turnIndex} ({turn.Role}): estimated {tokenEstimate} tokens exceeds limit of {turn.MaxTokens.Value}");
                }

                result.Transcript.Add(new SimulatedMessage
                {
                    Role = turn.Role,
                    Content = rendered,
                    EstimatedTokens = tokenEstimate,
                    SourceTurnIndex = turnIndex
                });

                result.TotalEstimatedTokens += tokenEstimate;
                executed++;

                if (turn.Role == SimRole.Assistant)
                    lastAssistantContent = rendered;

                // Handle branching
                if (turn.BranchTo != null && labelIndex.TryGetValue(turn.BranchTo, out int target))
                {
                    result.BranchesTaken++;
                    turnIndex = target;
                }
                else
                {
                    turnIndex++;
                }
            }

            result.TurnsExecuted = executed;
            result.FinalVariables = new Dictionary<string, string>(vars);

            if (executed >= _maxTurns)
            {
                result.Warnings.Add($"Simulation halted after reaching MaxTurns limit ({_maxTurns}).");
            }

            return result;
        }

        /// <summary>
        /// Run multiple simulations with different variable sets (scenarios).
        /// </summary>
        /// <param name="scenarios">Named scenarios mapping to variable dictionaries.</param>
        /// <returns>Results keyed by scenario name.</returns>
        public Dictionary<string, SimulationResult> RunScenarios(
            Dictionary<string, Dictionary<string, string>> scenarios)
        {
            if (scenarios == null) throw new ArgumentNullException(nameof(scenarios));

            var results = new Dictionary<string, SimulationResult>();
            foreach (var (name, vars) in scenarios)
            {
                results[name] = Run(vars);
            }
            return results;
        }

        /// <summary>
        /// Generate a comparison report across multiple scenario results.
        /// </summary>
        public static string CompareScenarios(Dictionary<string, SimulationResult> results)
        {
            if (results == null || results.Count == 0)
                return "No scenarios to compare.";

            var sb = new StringBuilder();
            sb.AppendLine("═══ Scenario Comparison ═══");
            sb.AppendLine();

            // Summary table
            sb.AppendLine($"{"Scenario",-25} {"Turns",-8} {"Tokens",-10} {"Warnings",-10} {"Pass",5}");
            sb.AppendLine(new string('─', 60));

            foreach (var (name, result) in results)
            {
                sb.AppendLine(
                    $"{StringHelpers.Truncate(name, 24),-25} {result.TurnsExecuted,-8} " +
                    $"{result.TotalEstimatedTokens,-10} {result.Warnings.Count,-10} " +
                    $"{(result.Success ? "✓" : "✗"),5}");
            }

            sb.AppendLine();

            // Failures detail
            var failures = results.Where(r => !r.Value.Success).ToList();
            if (failures.Count > 0)
            {
                sb.AppendLine("─── Validation Failures ───");
                foreach (var (name, result) in failures)
                {
                    foreach (var f in result.ValidationFailures)
                    {
                        sb.AppendLine($"  [{name}] {f}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serialize the script to JSON for saving/sharing.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            return JsonSerializer.Serialize(_script, options);
        }

        /// <summary>
        /// Load a script from JSON.
        /// </summary>
        public static PromptConversationSimulator FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string must not be empty.", nameof(json));

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            var turns = JsonSerializer.Deserialize<List<SimTurn>>(json, options)
                ?? throw new InvalidOperationException("Deserialized script was null.");

            var sim = new PromptConversationSimulator();
            foreach (var turn in turns)
                sim.AddTurn(turn);
            return sim;
        }

        // ── Helpers ──

        private static bool IsTruthy(Dictionary<string, string> vars, string key)
        {
            if (!vars.TryGetValue(key, out string? value))
                return false;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;
            if (value == "0")
                return false;
            return true;
        }

        private static string Interpolate(string template, Dictionary<string, string> vars)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                template,
                @"\{\{(\w+)\}\}",
                match =>
                {
                    string key = match.Groups[1].Value;
                    return vars.TryGetValue(key, out string? val) ? val : match.Value;
                },
                System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(2));
        }

        private static int EstimateTokens(string text) =>
            TextAnalysisHelpers.EstimateTokens(text);

    }
}