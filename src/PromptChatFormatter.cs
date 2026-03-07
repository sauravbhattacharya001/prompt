namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Formats prompt strings into structured chat message arrays for different
    /// LLM API providers (OpenAI, Anthropic, Google Gemini). Handles system
    /// message extraction, multi-turn conversation splitting, and provider-specific
    /// formatting quirks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prompts often contain implicit structure — a system instruction followed by
    /// a user query, or multi-turn exchanges marked with role prefixes. This class
    /// parses that structure and outputs provider-ready message arrays.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var formatter = new PromptChatFormatter();
    ///
    /// // Simple prompt → messages
    /// var messages = formatter.Format("You are a helpful assistant.\n\nWhat is 2+2?");
    /// // → [{ role: "system", content: "You are a helpful assistant." },
    /// //    { role: "user", content: "What is 2+2?" }]
    ///
    /// // Export for Anthropic
    /// string json = formatter.FormatAsJson("...", ChatProvider.Anthropic);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptChatFormatter
    {
        /// <summary>
        /// Supported LLM API providers with different message format requirements.
        /// </summary>
        public enum ChatProvider
        {
            /// <summary>OpenAI Chat Completions API format (system/user/assistant roles).</summary>
            OpenAI,
            /// <summary>Anthropic Messages API format (system as top-level param, user/assistant roles).</summary>
            Anthropic,
            /// <summary>Google Gemini API format (system_instruction + contents with user/model roles).</summary>
            Gemini
        }

        /// <summary>
        /// A single chat message with a role and content.
        /// </summary>
        public class ChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";

            public ChatMessage() { }

            public ChatMessage(string role, string content)
            {
                Role = role ?? throw new ArgumentNullException(nameof(role));
                Content = content ?? throw new ArgumentNullException(nameof(content));
            }

            public override string ToString() => $"[{Role}]: {Content}";
        }

        /// <summary>
        /// Result of formatting a prompt for a specific provider.
        /// </summary>
        public class FormatResult
        {
            /// <summary>Provider the format targets.</summary>
            public ChatProvider Provider { get; set; }

            /// <summary>System instruction extracted from the prompt (null if none detected).</summary>
            [JsonPropertyName("system")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? SystemMessage { get; set; }

            /// <summary>Chat messages in provider-appropriate format.</summary>
            [JsonPropertyName("messages")]
            public List<ChatMessage> Messages { get; set; } = new();

            /// <summary>Total message count including system message if separate.</summary>
            public int TotalParts => Messages.Count + (SystemMessage != null ? 1 : 0);
        }

        // Patterns for detecting role prefixes in multi-turn prompts.
        // Timeout protects against ReDoS when user-supplied text is long.
        private static readonly Regex RolePrefixPattern = new(
            @"^(system|user|assistant|human|ai|bot|model)\s*:\s*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        // Heuristic patterns for detecting system-like instructions at the start.
        private static readonly string[] SystemPrefixes = new[]
        {
            "you are", "act as", "behave as", "your role is", "your task is",
            "as a", "as an", "pretend you", "imagine you", "from now on",
            "instructions:", "system:", "context:", "role:", "persona:"
        };

        private readonly string _defaultSystemRole;
        private readonly bool _mergeConsecutive;

        /// <summary>
        /// Creates a new chat formatter.
        /// </summary>
        /// <param name="defaultSystemRole">
        /// Default role name for system messages. Defaults to "system".
        /// </param>
        /// <param name="mergeConsecutive">
        /// When true, consecutive messages with the same role are merged
        /// into a single message. Defaults to true.
        /// </param>
        public PromptChatFormatter(string defaultSystemRole = "system", bool mergeConsecutive = true)
        {
            _defaultSystemRole = defaultSystemRole ?? "system";
            _mergeConsecutive = mergeConsecutive;
        }

        /// <summary>
        /// Parses a prompt string into structured chat messages.
        /// Detects role prefixes (e.g., "user: hello") and system instructions.
        /// </summary>
        /// <param name="prompt">The raw prompt text to parse.</param>
        /// <returns>List of chat messages with detected roles.</returns>
        /// <exception cref="ArgumentException">Thrown when prompt is null or whitespace.</exception>
        public List<ChatMessage> Parse(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var messages = new List<ChatMessage>();
            var lines = prompt.Split('\n');
            string currentRole = "";
            var currentContent = new List<string>();

            foreach (var line in lines)
            {
                var match = SafeRegexMatch(line.TrimStart());
                if (match != null && match.Success)
                {
                    // Flush previous block
                    if (currentContent.Count > 0)
                    {
                        var text = string.Join("\n", currentContent).Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var role = string.IsNullOrEmpty(currentRole) ? DetectRole(text) : currentRole;
                            messages.Add(new ChatMessage(NormalizeRole(role), text));
                        }
                        currentContent.Clear();
                    }

                    currentRole = match.Groups[1].Value.ToLowerInvariant();
                    var remainder = line.TrimStart().Substring(match.Length);
                    if (!string.IsNullOrWhiteSpace(remainder))
                        currentContent.Add(remainder);
                }
                else
                {
                    currentContent.Add(line);
                }
            }

            // Flush final block
            if (currentContent.Count > 0)
            {
                var text = string.Join("\n", currentContent).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    var role = string.IsNullOrEmpty(currentRole) ? DetectRole(text) : currentRole;
                    messages.Add(new ChatMessage(NormalizeRole(role), text));
                }
            }

            // If no explicit roles were detected, try splitting on paragraph breaks
            if (messages.Count <= 1 && !prompt.Contains(':'))
            {
                messages = SplitByParagraphs(prompt);
            }

            if (_mergeConsecutive)
                messages = MergeConsecutive(messages);

            return messages;
        }

        /// <summary>
        /// Formats a prompt for a specific LLM provider.
        /// </summary>
        /// <param name="prompt">The raw prompt text.</param>
        /// <param name="provider">Target provider format.</param>
        /// <returns>A <see cref="FormatResult"/> with provider-appropriate structure.</returns>
        public FormatResult Format(string prompt, ChatProvider provider = ChatProvider.OpenAI)
        {
            var messages = Parse(prompt);
            var result = new FormatResult { Provider = provider };

            switch (provider)
            {
                case ChatProvider.OpenAI:
                    result.Messages = messages;
                    break;

                case ChatProvider.Anthropic:
                    // Anthropic wants system as a separate top-level parameter
                    var systemMsg = messages.FirstOrDefault(m => m.Role == "system");
                    if (systemMsg != null)
                    {
                        result.SystemMessage = systemMsg.Content;
                        result.Messages = messages
                            .Where(m => m.Role != "system")
                            .Select(m => new ChatMessage(
                                m.Role == "assistant" ? "assistant" : "user",
                                m.Content))
                            .ToList();
                    }
                    else
                    {
                        result.Messages = messages
                            .Select(m => new ChatMessage(
                                m.Role == "assistant" ? "assistant" : "user",
                                m.Content))
                            .ToList();
                    }
                    break;

                case ChatProvider.Gemini:
                    // Gemini uses "model" instead of "assistant", system as system_instruction
                    var geminiSystem = messages.FirstOrDefault(m => m.Role == "system");
                    if (geminiSystem != null)
                    {
                        result.SystemMessage = geminiSystem.Content;
                        result.Messages = messages
                            .Where(m => m.Role != "system")
                            .Select(m => new ChatMessage(
                                m.Role == "assistant" ? "model" : "user",
                                m.Content))
                            .ToList();
                    }
                    else
                    {
                        result.Messages = messages
                            .Select(m => new ChatMessage(
                                m.Role == "assistant" ? "model" : "user",
                                m.Content))
                            .ToList();
                    }
                    break;
            }

            // Ensure at least one user message exists
            if (result.Messages.Count == 0 && !string.IsNullOrWhiteSpace(prompt))
            {
                result.Messages.Add(new ChatMessage("user", prompt.Trim()));
            }

            if (_mergeConsecutive)
                result.Messages = MergeConsecutive(result.Messages);

            return result;
        }

        /// <summary>
        /// Formats a prompt and returns provider-ready JSON.
        /// </summary>
        /// <param name="prompt">The raw prompt text.</param>
        /// <param name="provider">Target provider format.</param>
        /// <param name="indented">Whether to pretty-print the JSON.</param>
        /// <returns>JSON string ready for the provider's API.</returns>
        public string FormatAsJson(string prompt, ChatProvider provider = ChatProvider.OpenAI, bool indented = true)
        {
            var result = Format(prompt, provider);
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            switch (provider)
            {
                case ChatProvider.OpenAI:
                    return JsonSerializer.Serialize(new { messages = result.Messages }, options);

                case ChatProvider.Anthropic:
                    if (result.SystemMessage != null)
                        return JsonSerializer.Serialize(new { system = result.SystemMessage, messages = result.Messages }, options);
                    return JsonSerializer.Serialize(new { messages = result.Messages }, options);

                case ChatProvider.Gemini:
                    if (result.SystemMessage != null)
                        return JsonSerializer.Serialize(new
                        {
                            system_instruction = new { parts = new[] { new { text = result.SystemMessage } } },
                            contents = result.Messages.Select(m => new
                            {
                                role = m.Role,
                                parts = new[] { new { text = m.Content } }
                            })
                        }, options);
                    return JsonSerializer.Serialize(new
                    {
                        contents = result.Messages.Select(m => new
                        {
                            role = m.Role,
                            parts = new[] { new { text = m.Content } }
                        })
                    }, options);

                default:
                    return JsonSerializer.Serialize(result, options);
            }
        }

        /// <summary>
        /// Converts a list of <see cref="ChatMessage"/> objects into a plain text
        /// prompt with role prefixes, useful for providers that expect a single string.
        /// </summary>
        /// <param name="messages">The messages to flatten.</param>
        /// <param name="separator">Separator between messages.</param>
        /// <returns>A formatted prompt string.</returns>
        public static string Flatten(IEnumerable<ChatMessage> messages, string separator = "\n\n")
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            return string.Join(separator, messages.Select(m => $"{m.Role}: {m.Content}"));
        }

        /// <summary>
        /// Creates messages from explicit role-content pairs.
        /// Useful for building multi-turn conversations programmatically.
        /// </summary>
        /// <param name="turns">
        /// Alternating role and content strings: "user", "hello", "assistant", "hi there"
        /// </param>
        /// <returns>List of chat messages.</returns>
        /// <exception cref="ArgumentException">Thrown when turns count is odd.</exception>
        public static List<ChatMessage> FromTurns(params string[] turns)
        {
            if (turns == null || turns.Length == 0)
                throw new ArgumentException("At least one turn pair is required.", nameof(turns));
            if (turns.Length % 2 != 0)
                throw new ArgumentException("Turns must be provided in role-content pairs.", nameof(turns));

            var messages = new List<ChatMessage>();
            for (int i = 0; i < turns.Length; i += 2)
            {
                messages.Add(new ChatMessage(turns[i], turns[i + 1]));
            }
            return messages;
        }

        /// <summary>
        /// Estimates the token count of formatted messages using a simple
        /// word-based heuristic (roughly 0.75 tokens per word).
        /// </summary>
        /// <param name="messages">Messages to estimate tokens for.</param>
        /// <returns>Estimated token count.</returns>
        public static int EstimateTokens(IEnumerable<ChatMessage> messages)
        {
            if (messages == null) return 0;
            int totalChars = 0;
            foreach (var msg in messages)
            {
                // Role overhead: ~4 tokens per message for formatting
                totalChars += 16; // ~4 tokens * 4 chars/token
                totalChars += (msg.Content?.Length ?? 0);
            }
            // ~4 chars per token on average for English
            return (int)Math.Ceiling(totalChars / 4.0);
        }

        #region Private helpers

        private static Match? SafeRegexMatch(string input)
        {
            try
            {
                return RolePrefixPattern.Match(input);
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        private static string NormalizeRole(string role)
        {
            return role.ToLowerInvariant() switch
            {
                "human" => "user",
                "ai" => "assistant",
                "bot" => "assistant",
                "model" => "assistant",
                "system" => "system",
                "user" => "user",
                "assistant" => "assistant",
                _ => "user"
            };
        }

        private string DetectRole(string text)
        {
            var trimmed = text.TrimStart().ToLowerInvariant();
            foreach (var prefix in SystemPrefixes)
            {
                if (trimmed.StartsWith(prefix))
                    return _defaultSystemRole;
            }
            return "user";
        }

        private List<ChatMessage> SplitByParagraphs(string prompt)
        {
            var paragraphs = Regex.Split(prompt, @"\n\s*\n", RegexOptions.None, TimeSpan.FromSeconds(2))
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            if (paragraphs.Count <= 1)
            {
                var role = DetectRole(prompt.Trim());
                return new List<ChatMessage> { new ChatMessage(NormalizeRole(role), prompt.Trim()) };
            }

            var messages = new List<ChatMessage>();
            for (int i = 0; i < paragraphs.Count; i++)
            {
                string role;
                if (i == 0)
                    role = DetectRole(paragraphs[i]);
                else
                    role = "user";
                messages.Add(new ChatMessage(NormalizeRole(role), paragraphs[i]));
            }
            return messages;
        }

        private static List<ChatMessage> MergeConsecutive(List<ChatMessage> messages)
        {
            if (messages.Count <= 1) return messages;

            var merged = new List<ChatMessage> { messages[0] };
            for (int i = 1; i < messages.Count; i++)
            {
                if (messages[i].Role == merged[^1].Role)
                {
                    merged[^1] = new ChatMessage(
                        merged[^1].Role,
                        merged[^1].Content + "\n\n" + messages[i].Content);
                }
                else
                {
                    merged.Add(messages[i]);
                }
            }
            return merged;
        }

        #endregion
    }
}
