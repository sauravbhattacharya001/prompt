namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Supported AI model dialects for prompt conversion.
    /// </summary>
    public enum ModelDialect
    {
        /// <summary>OpenAI ChatGPT style (system/user/assistant roles).</summary>
        ChatGPT,
        /// <summary>Anthropic Claude style (Human/Assistant turns, system block).</summary>
        Claude,
        /// <summary>Google Gemini style (user/model roles).</summary>
        Gemini,
        /// <summary>Meta Llama chat template ([INST] markers).</summary>
        Llama,
        /// <summary>Mistral instruction format ([INST] with BOS).</summary>
        Mistral,
        /// <summary>Generic/raw text with no special formatting.</summary>
        Raw
    }

    /// <summary>
    /// A model-agnostic message representation.
    /// </summary>
    public record DialectMessage(
        string Role,    // "system", "user", "assistant"
        string Content
    );

    /// <summary>
    /// Configuration for dialect-specific prompt formatting behaviors.
    /// </summary>
    public class DialectConfig
    {
        /// <summary>Maximum recommended system prompt length in characters.</summary>
        public int? MaxSystemPromptLength { get; set; }

        /// <summary>Whether the dialect supports a separate system message.</summary>
        public bool SupportsSystemMessage { get; set; } = true;

        /// <summary>Model-specific tips and best practices.</summary>
        public List<string> Tips { get; set; } = new();

        /// <summary>Token limit for the model family.</summary>
        public int? DefaultTokenLimit { get; set; }

        /// <summary>Custom preamble to prepend to system prompts for this dialect.</summary>
        public string? SystemPreamble { get; set; }
    }

    /// <summary>
    /// Result of converting a prompt to a specific dialect.
    /// </summary>
    public class DialectConversionResult
    {
        /// <summary>The target dialect.</summary>
        public ModelDialect Dialect { get; set; }

        /// <summary>The formatted prompt string.</summary>
        public string FormattedPrompt { get; set; } = string.Empty;

        /// <summary>Individual messages in the target format.</summary>
        public List<DialectMessage> Messages { get; set; } = new();

        /// <summary>Warnings generated during conversion.</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Tips for the target dialect.</summary>
        public List<string> Tips { get; set; } = new();

        /// <summary>Estimated token count.</summary>
        public int EstimatedTokens { get; set; }
    }

    /// <summary>
    /// Converts prompts between different AI model dialects (ChatGPT, Claude,
    /// Gemini, Llama, Mistral) with model-specific formatting conventions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each AI model family has its own preferred prompt format. PromptDialect
    /// handles the conversion so you can write once and deploy to any model.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var dialect = new PromptDialect();
    ///
    /// // Build a conversation
    /// dialect.SetSystemPrompt("You are a helpful coding assistant.");
    /// dialect.AddUserMessage("Explain async/await in C#");
    /// dialect.AddAssistantMessage("Async/await allows non-blocking...");
    /// dialect.AddUserMessage("Show me an example");
    ///
    /// // Convert to different dialects
    /// var chatgpt = dialect.Convert(ModelDialect.ChatGPT);
    /// var claude = dialect.Convert(ModelDialect.Claude);
    /// var llama = dialect.Convert(ModelDialect.Llama);
    ///
    /// // Parse from existing formatted prompt
    /// var parsed = PromptDialect.Parse(rawText, ModelDialect.Claude);
    /// var converted = parsed.Convert(ModelDialect.ChatGPT);
    ///
    /// // Get JSON messages for API calls
    /// string json = dialect.ToApiJson(ModelDialect.ChatGPT);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptDialect
    {
        private string? _systemPrompt;
        private readonly List<DialectMessage> _messages = new();
        private readonly Dictionary<ModelDialect, DialectConfig> _configs;

        /// <summary>
        /// Initializes a new PromptDialect with default model configurations.
        /// </summary>
        public PromptDialect()
        {
            _configs = new Dictionary<ModelDialect, DialectConfig>
            {
                [ModelDialect.ChatGPT] = new DialectConfig
                {
                    SupportsSystemMessage = true,
                    DefaultTokenLimit = 128000,
                    Tips = new List<string>
                    {
                        "Use clear role-based system prompts",
                        "Separate instructions from context with markdown headers",
                        "Use numbered lists for multi-step instructions"
                    }
                },
                [ModelDialect.Claude] = new DialectConfig
                {
                    SupportsSystemMessage = true,
                    DefaultTokenLimit = 200000,
                    Tips = new List<string>
                    {
                        "Claude responds well to XML tags for structure",
                        "Place long documents in <document> tags",
                        "Use <thinking> tags to encourage step-by-step reasoning"
                    }
                },
                [ModelDialect.Gemini] = new DialectConfig
                {
                    SupportsSystemMessage = true,
                    DefaultTokenLimit = 1000000,
                    Tips = new List<string>
                    {
                        "Gemini supports multimodal inputs natively",
                        "Use 'model' role instead of 'assistant'",
                        "System instructions are set separately from messages"
                    }
                },
                [ModelDialect.Llama] = new DialectConfig
                {
                    SupportsSystemMessage = true,
                    DefaultTokenLimit = 8192,
                    Tips = new List<string>
                    {
                        "Use [INST] and [/INST] markers for user messages",
                        "System prompt goes in <<SYS>> tags",
                        "Keep system prompts concise for smaller context windows"
                    }
                },
                [ModelDialect.Mistral] = new DialectConfig
                {
                    SupportsSystemMessage = false,
                    DefaultTokenLimit = 32000,
                    Tips = new List<string>
                    {
                        "Mistral v0.1 has no system message support — prepend to first user message",
                        "Use [INST] and [/INST] markers",
                        "Newer Mistral models may support system prompts via API"
                    }
                },
                [ModelDialect.Raw] = new DialectConfig
                {
                    SupportsSystemMessage = true,
                    Tips = new List<string>
                    {
                        "Raw format uses plain text with no special markers",
                        "Useful for completion-style models"
                    }
                }
            };
        }

        /// <summary>
        /// Sets the system prompt.
        /// </summary>
        public PromptDialect SetSystemPrompt(string systemPrompt)
        {
            _systemPrompt = systemPrompt;
            return this;
        }

        /// <summary>
        /// Adds a user message.
        /// </summary>
        public PromptDialect AddUserMessage(string content)
        {
            _messages.Add(new DialectMessage("user", content));
            return this;
        }

        /// <summary>
        /// Adds an assistant message.
        /// </summary>
        public PromptDialect AddAssistantMessage(string content)
        {
            _messages.Add(new DialectMessage("assistant", content));
            return this;
        }

        /// <summary>
        /// Adds a message with an explicit role.
        /// </summary>
        public PromptDialect AddMessage(string role, string content)
        {
            _messages.Add(new DialectMessage(role.ToLowerInvariant(), content));
            return this;
        }

        /// <summary>
        /// Gets or sets the dialect configuration for a model.
        /// </summary>
        public DialectConfig GetConfig(ModelDialect dialect) => _configs[dialect];

        /// <summary>
        /// Converts the conversation to the specified dialect format.
        /// </summary>
        public DialectConversionResult Convert(ModelDialect target)
        {
            var result = new DialectConversionResult
            {
                Dialect = target,
                Tips = _configs[target].Tips.ToList()
            };

            switch (target)
            {
                case ModelDialect.ChatGPT:
                    FormatChatGPT(result);
                    break;
                case ModelDialect.Claude:
                    FormatClaude(result);
                    break;
                case ModelDialect.Gemini:
                    FormatGemini(result);
                    break;
                case ModelDialect.Llama:
                    FormatLlama(result);
                    break;
                case ModelDialect.Mistral:
                    FormatMistral(result);
                    break;
                case ModelDialect.Raw:
                    FormatRaw(result);
                    break;
            }

            result.EstimatedTokens = PromptGuard.EstimateTokens(result.FormattedPrompt);
            return result;
        }

        /// <summary>
        /// Converts the conversation to all supported dialects for comparison.
        /// </summary>
        public Dictionary<ModelDialect, DialectConversionResult> ConvertAll()
        {
            return Enum.GetValues<ModelDialect>()
                .ToDictionary(d => d, d => Convert(d));
        }

        /// <summary>
        /// Generates a JSON payload suitable for API calls to the target model.
        /// </summary>
        public string ToApiJson(ModelDialect dialect, bool indented = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            switch (dialect)
            {
                case ModelDialect.ChatGPT:
                    var chatMessages = new List<object>();
                    if (!string.IsNullOrEmpty(_systemPrompt))
                        chatMessages.Add(new { role = "system", content = _systemPrompt });
                    foreach (var msg in _messages)
                        chatMessages.Add(new { role = msg.Role, content = msg.Content });
                    return JsonSerializer.Serialize(new { messages = chatMessages }, options);

                case ModelDialect.Claude:
                    var claudeMessages = _messages.Select(m =>
                        new { role = m.Role, content = m.Content }).ToList();
                    var claudePayload = new Dictionary<string, object>
                    {
                        ["messages"] = claudeMessages
                    };
                    if (!string.IsNullOrEmpty(_systemPrompt))
                        claudePayload["system"] = _systemPrompt;
                    return JsonSerializer.Serialize(claudePayload, options);

                case ModelDialect.Gemini:
                    var contents = _messages.Select(m => new
                    {
                        role = m.Role == "assistant" ? "model" : m.Role,
                        parts = new[] { new { text = m.Content } }
                    }).ToList();
                    var geminiPayload = new Dictionary<string, object>
                    {
                        ["contents"] = contents
                    };
                    if (!string.IsNullOrEmpty(_systemPrompt))
                        geminiPayload["systemInstruction"] = new { parts = new[] { new { text = _systemPrompt } } };
                    return JsonSerializer.Serialize(geminiPayload, options);

                default:
                    var result = Convert(dialect);
                    return JsonSerializer.Serialize(new { prompt = result.FormattedPrompt }, options);
            }
        }

        /// <summary>
        /// Parses a formatted prompt string from a known dialect back into messages.
        /// </summary>
        public static PromptDialect Parse(string formattedPrompt, ModelDialect sourceDialect)
        {
            var dialect = new PromptDialect();

            switch (sourceDialect)
            {
                case ModelDialect.Claude:
                    ParseClaude(formattedPrompt, dialect);
                    break;
                case ModelDialect.Llama:
                    ParseLlama(formattedPrompt, dialect);
                    break;
                case ModelDialect.Raw:
                    dialect.AddUserMessage(formattedPrompt);
                    break;
                default:
                    // For JSON-based formats, try to parse as JSON
                    TryParseJson(formattedPrompt, dialect);
                    break;
            }

            return dialect;
        }

        /// <summary>
        /// Returns a side-by-side comparison of two dialects as a formatted string.
        /// </summary>
        public string Compare(ModelDialect a, ModelDialect b)
        {
            var resultA = Convert(a);
            var resultB = Convert(b);
            var sb = new StringBuilder();

            sb.AppendLine($"╔══════════════════════════════════════════════════╗");
            sb.AppendLine($"║         Dialect Comparison: {a} vs {b}");
            sb.AppendLine($"╚══════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"── {a} ({resultA.EstimatedTokens} tokens) ──");
            sb.AppendLine(resultA.FormattedPrompt);
            sb.AppendLine();
            sb.AppendLine($"── {b} ({resultB.EstimatedTokens} tokens) ──");
            sb.AppendLine(resultB.FormattedPrompt);

            if (resultA.Warnings.Any() || resultB.Warnings.Any())
            {
                sb.AppendLine();
                sb.AppendLine("⚠️ Warnings:");
                foreach (var w in resultA.Warnings) sb.AppendLine($"  [{a}] {w}");
                foreach (var w in resultB.Warnings) sb.AppendLine($"  [{b}] {w}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the current messages in model-agnostic form.
        /// </summary>
        public IReadOnlyList<DialectMessage> GetMessages() => _messages.AsReadOnly();

        /// <summary>
        /// Gets the system prompt, if set.
        /// </summary>
        public string? GetSystemPrompt() => _systemPrompt;

        // ── Private formatting methods ──

        private void FormatChatGPT(DialectConversionResult result)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_systemPrompt))
            {
                result.Messages.Add(new DialectMessage("system", _systemPrompt));
                sb.AppendLine($"[System]: {_systemPrompt}");
                sb.AppendLine();
            }
            foreach (var msg in _messages)
            {
                var role = char.ToUpper(msg.Role[0]) + msg.Role[1..];
                result.Messages.Add(msg);
                sb.AppendLine($"[{role}]: {msg.Content}");
                sb.AppendLine();
            }
            result.FormattedPrompt = sb.ToString().TrimEnd();
        }

        private void FormatClaude(DialectConversionResult result)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_systemPrompt))
            {
                sb.AppendLine(_systemPrompt);
                sb.AppendLine();
            }
            foreach (var msg in _messages)
            {
                var role = msg.Role == "user" ? "Human" : "Assistant";
                result.Messages.Add(new DialectMessage(msg.Role, msg.Content));
                sb.AppendLine($"{role}: {msg.Content}");
                sb.AppendLine();
            }
            // Claude expects a trailing Assistant: to prompt a response
            if (_messages.LastOrDefault()?.Role == "user")
            {
                sb.AppendLine("Assistant:");
            }
            result.FormattedPrompt = sb.ToString().TrimEnd();
        }

        private void FormatGemini(DialectConversionResult result)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_systemPrompt))
            {
                sb.AppendLine($"[System Instruction]: {_systemPrompt}");
                sb.AppendLine();
            }
            foreach (var msg in _messages)
            {
                var role = msg.Role == "assistant" ? "model" : msg.Role;
                result.Messages.Add(new DialectMessage(role, msg.Content));
                sb.AppendLine($"[{role}]: {msg.Content}");
                sb.AppendLine();
            }
            result.FormattedPrompt = sb.ToString().TrimEnd();
        }

        private void FormatLlama(DialectConversionResult result)
        {
            var sb = new StringBuilder();
            bool firstUser = true;

            foreach (var msg in _messages)
            {
                if (msg.Role == "user")
                {
                    sb.Append("<s>[INST] ");
                    if (firstUser && !string.IsNullOrEmpty(_systemPrompt))
                    {
                        sb.Append($"<<SYS>>\n{_systemPrompt}\n<</SYS>>\n\n");
                        firstUser = false;
                    }
                    sb.Append(msg.Content);
                    sb.AppendLine(" [/INST]");
                    result.Messages.Add(msg);
                }
                else if (msg.Role == "assistant")
                {
                    sb.Append(msg.Content);
                    sb.AppendLine("</s>");
                    result.Messages.Add(msg);
                }
            }
            result.FormattedPrompt = sb.ToString().TrimEnd();

            if (_messages.Count == 0 && !string.IsNullOrEmpty(_systemPrompt))
            {
                result.Warnings.Add("Llama format requires at least one user message to embed the system prompt.");
            }
        }

        private void FormatMistral(DialectConversionResult result)
        {
            var sb = new StringBuilder();
            var config = _configs[ModelDialect.Mistral];

            if (!config.SupportsSystemMessage && !string.IsNullOrEmpty(_systemPrompt))
            {
                result.Warnings.Add("Mistral v0.1 does not support system messages. System prompt prepended to first user message.");
            }

            bool systemPrepended = false;
            foreach (var msg in _messages)
            {
                if (msg.Role == "user")
                {
                    sb.Append("<s>[INST] ");
                    if (!systemPrepended && !string.IsNullOrEmpty(_systemPrompt))
                    {
                        sb.Append($"{_systemPrompt}\n\n");
                        systemPrepended = true;
                    }
                    sb.Append(msg.Content);
                    sb.AppendLine(" [/INST]");
                    result.Messages.Add(msg);
                }
                else if (msg.Role == "assistant")
                {
                    sb.Append(msg.Content);
                    sb.AppendLine("</s>");
                    result.Messages.Add(msg);
                }
            }
            result.FormattedPrompt = sb.ToString().TrimEnd();
        }

        private void FormatRaw(DialectConversionResult result)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_systemPrompt))
            {
                sb.AppendLine(_systemPrompt);
                sb.AppendLine();
            }
            foreach (var msg in _messages)
            {
                sb.AppendLine(msg.Content);
                sb.AppendLine();
                result.Messages.Add(msg);
            }
            result.FormattedPrompt = sb.ToString().TrimEnd();
        }

        // ── Private parsing methods ──

        private static void ParseClaude(string text, PromptDialect dialect)
        {
            var lines = text.Split('\n');
            var currentRole = "";
            var currentContent = new StringBuilder();
            string? systemContent = null;

            // Check for system prompt before first Human: turn
            var firstHumanIdx = Array.FindIndex(lines, l => l.TrimStart().StartsWith("Human:"));
            if (firstHumanIdx > 0)
            {
                systemContent = string.Join("\n", lines.Take(firstHumanIdx)).Trim();
                if (!string.IsNullOrEmpty(systemContent))
                    dialect.SetSystemPrompt(systemContent);
                lines = lines.Skip(firstHumanIdx).ToArray();
            }

            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("Human:"))
                {
                    FlushMessage(dialect, currentRole, currentContent);
                    currentRole = "user";
                    currentContent.Clear();
                    currentContent.Append(trimmed["Human:".Length..].TrimStart());
                }
                else if (trimmed.StartsWith("Assistant:"))
                {
                    FlushMessage(dialect, currentRole, currentContent);
                    currentRole = "assistant";
                    currentContent.Clear();
                    currentContent.Append(trimmed["Assistant:".Length..].TrimStart());
                }
                else
                {
                    if (currentContent.Length > 0) currentContent.AppendLine();
                    currentContent.Append(line);
                }
            }
            FlushMessage(dialect, currentRole, currentContent);
        }

        private static void ParseLlama(string text, PromptDialect dialect)
        {
            // Extract system prompt from <<SYS>> tags
            var sysMatch = Regex.Match(text, @"<<SYS>>\s*(.*?)\s*<</SYS>>", RegexOptions.Singleline);
            if (sysMatch.Success)
                dialect.SetSystemPrompt(sysMatch.Groups[1].Value.Trim());

            // Extract [INST]...[/INST] pairs
            var instMatches = Regex.Matches(text, @"\[INST\]\s*(.*?)\s*\[/INST\]\s*(.*?)(?=<s>|\z)", RegexOptions.Singleline);
            foreach (Match m in instMatches)
            {
                var userContent = m.Groups[1].Value.Trim();
                // Remove <<SYS>> block from user content if present
                userContent = Regex.Replace(userContent, @"<<SYS>>.*?<</SYS>>\s*", "", RegexOptions.Singleline).Trim();
                if (!string.IsNullOrEmpty(userContent))
                    dialect.AddUserMessage(userContent);

                var assistantContent = m.Groups[2].Value.Replace("</s>", "").Trim();
                if (!string.IsNullOrEmpty(assistantContent))
                    dialect.AddAssistantMessage(assistantContent);
            }
        }

        private static void TryParseJson(string text, PromptDialect dialect)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                // Try "system" field
                if (root.TryGetProperty("system", out var sys))
                    dialect.SetSystemPrompt(sys.GetString() ?? "");

                // Try "messages" array
                if (root.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var msg in msgs.EnumerateArray())
                    {
                        var role = msg.GetProperty("role").GetString() ?? "user";
                        var content = msg.GetProperty("content").GetString() ?? "";
                        if (role == "system")
                            dialect.SetSystemPrompt(content);
                        else
                            dialect.AddMessage(role, content);
                    }
                }

                // Try Gemini "contents" format
                if (root.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in contents.EnumerateArray())
                    {
                        var role = item.GetProperty("role").GetString() ?? "user";
                        if (role == "model") role = "assistant";
                        var parts = item.GetProperty("parts");
                        var text2 = parts[0].GetProperty("text").GetString() ?? "";
                        dialect.AddMessage(role, text2);
                    }
                }
            }
            catch
            {
                // Not valid JSON, treat as raw
                dialect.AddUserMessage(text);
            }
        }

        private static void FlushMessage(PromptDialect dialect, string role, StringBuilder content)
        {
            if (string.IsNullOrEmpty(role) || content.Length == 0) return;
            var text = content.ToString().Trim();
            if (!string.IsNullOrEmpty(text))
                dialect.AddMessage(role, text);
        }
    }
}
