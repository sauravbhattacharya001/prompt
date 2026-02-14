namespace Prompt
{
    using System.ClientModel;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.AI.OpenAI;
    using OpenAI.Chat;

    /// <summary>
    /// Represents a multi-turn conversation with Azure OpenAI. Maintains
    /// message history so the model has full context of the conversation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="Main.GetResponseAsync"/>, which sends a single
    /// prompt and forgets everything, <c>Conversation</c> accumulates
    /// messages across multiple turns — enabling back-and-forth dialogue.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var conv = new Conversation("You are a helpful assistant.");
    /// string? r1 = await conv.SendAsync("What is 2+2?");
    /// string? r2 = await conv.SendAsync("Now multiply that by 3.");
    /// // r2 will know the context (4 * 3 = 12)
    /// </code>
    /// </para>
    /// </remarks>
    public class Conversation
    {
        private readonly List<ChatMessage> _messages = new();
        private readonly object _lock = new();

        // Per-conversation model parameters (defaults match Main.cs)
        private float _temperature = 0.7f;
        private int _maxTokens = 800;
        private float _topP = 0.95f;
        private float _frequencyPenalty = 0f;
        private float _presencePenalty = 0f;
        private int _maxRetries = 3;

        /// <summary>
        /// Creates a new conversation, optionally with a system prompt.
        /// </summary>
        /// <param name="systemPrompt">
        /// Optional system prompt to set the assistant's behavior for
        /// the entire conversation.
        /// </param>
        public Conversation(string? systemPrompt = null)
        {
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                _messages.Add(new SystemChatMessage(systemPrompt));
        }

        /// <summary>
        /// Gets the number of messages in the conversation (including system prompt).
        /// </summary>
        public int MessageCount
        {
            get { lock (_lock) return _messages.Count; }
        }

        /// <summary>
        /// Gets or sets the sampling temperature (0.0–2.0).
        /// Lower values make output more focused; higher values more random.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside 0.0–2.0.</exception>
        public float Temperature
        {
            get => _temperature;
            set
            {
                if (value < 0f || value > 2f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Temperature must be between 0.0 and 2.0.");
                _temperature = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of tokens in the response.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is less than 1.</exception>
        public int MaxTokens
        {
            get => _maxTokens;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "MaxTokens must be at least 1.");
                _maxTokens = value;
            }
        }

        /// <summary>
        /// Gets or sets the nucleus sampling parameter (0.0–1.0).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside 0.0–1.0.</exception>
        public float TopP
        {
            get => _topP;
            set
            {
                if (value < 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "TopP must be between 0.0 and 1.0.");
                _topP = value;
            }
        }

        /// <summary>
        /// Gets or sets the frequency penalty (-2.0–2.0).
        /// Positive values penalize tokens that appear frequently.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside -2.0–2.0.</exception>
        public float FrequencyPenalty
        {
            get => _frequencyPenalty;
            set
            {
                if (value < -2f || value > 2f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "FrequencyPenalty must be between -2.0 and 2.0.");
                _frequencyPenalty = value;
            }
        }

        /// <summary>
        /// Gets or sets the presence penalty (-2.0–2.0).
        /// Positive values penalize tokens based on whether they already appeared.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside -2.0–2.0.</exception>
        public float PresencePenalty
        {
            get => _presencePenalty;
            set
            {
                if (value < -2f || value > 2f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "PresencePenalty must be between -2.0 and 2.0.");
                _presencePenalty = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of retries for transient failures.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is negative.</exception>
        public int MaxRetries
        {
            get => _maxRetries;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "MaxRetries must be non-negative.");
                _maxRetries = value;
            }
        }

        /// <summary>
        /// Sends a user message and returns the assistant's response.
        /// Both the user message and assistant response are added to the
        /// conversation history for future context.
        /// </summary>
        /// <param name="message">The user message to send.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The assistant's response text, or <c>null</c> if none.</returns>
        /// <exception cref="ArgumentException">Message is null or empty.</exception>
        public async Task<string?> SendAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException(
                    "Message cannot be null or empty.", nameof(message));

            List<ChatMessage> snapshot;
            lock (_lock)
            {
                _messages.Add(new UserChatMessage(message));
                snapshot = new List<ChatMessage>(_messages);
            }

            var options = new ChatCompletionOptions()
            {
                Temperature = _temperature,
                MaxOutputTokenCount = _maxTokens,
                TopP = _topP,
                FrequencyPenalty = _frequencyPenalty,
                PresencePenalty = _presencePenalty,
            };

            ChatClient chatClient = Main.GetOrCreateChatClient(_maxRetries);

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                snapshot, options, cancellationToken);

            string? responseText = completion?.Content?.FirstOrDefault()?.Text;

            if (responseText != null)
            {
                lock (_lock)
                {
                    _messages.Add(new AssistantChatMessage(responseText));
                }
            }

            return responseText;
        }

        /// <summary>
        /// Adds a user message to the history without sending to the API.
        /// Useful for injecting context or replaying prior conversations.
        /// </summary>
        /// <param name="message">The user message to add.</param>
        /// <exception cref="ArgumentException">Message is null or empty.</exception>
        public void AddUserMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException(
                    "Message cannot be null or empty.", nameof(message));

            lock (_lock) _messages.Add(new UserChatMessage(message));
        }

        /// <summary>
        /// Adds an assistant message to the history without calling the API.
        /// Useful for injecting context or replaying prior conversations.
        /// </summary>
        /// <param name="message">The assistant message to add.</param>
        /// <exception cref="ArgumentException">Message is null or empty.</exception>
        public void AddAssistantMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException(
                    "Message cannot be null or empty.", nameof(message));

            lock (_lock) _messages.Add(new AssistantChatMessage(message));
        }

        /// <summary>
        /// Clears the conversation history. If a system prompt was set,
        /// it is preserved; all user and assistant messages are removed.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                // Preserve the system prompt if it exists
                ChatMessage? systemMsg = _messages.Count > 0
                    && _messages[0] is SystemChatMessage
                    ? _messages[0] : null;

                _messages.Clear();

                if (systemMsg != null)
                    _messages.Add(systemMsg);
            }
        }

        /// <summary>
        /// Returns a snapshot of the conversation as a list of
        /// role-content pairs (for serialization, logging, etc.).
        /// </summary>
        /// <returns>
        /// A list of tuples where Item1 is the role ("system", "user",
        /// or "assistant") and Item2 is the message content.
        /// </returns>
        public List<(string Role, string Content)> GetHistory()
        {
            lock (_lock)
            {
                var history = new List<(string, string)>();
                foreach (var msg in _messages)
                {
                    string role = msg switch
                    {
                        SystemChatMessage => "system",
                        UserChatMessage => "user",
                        AssistantChatMessage => "assistant",
                        _ => "unknown"
                    };

                    string content = "";
                    if (msg.Content != null)
                    {
                        foreach (var part in msg.Content)
                        {
                            if (part.Text != null)
                            {
                                content += part.Text;
                            }
                        }
                    }

                    history.Add((role, content));
                }
                return history;
            }
        }

        // ──────────────── Serialization ────────────────

        /// <summary>
        /// Serializes the conversation to a JSON string, including all
        /// messages and model parameters. The output can be saved to a
        /// file or transmitted and later restored with <see cref="LoadFromJson"/>.
        /// </summary>
        /// <param name="indented">Whether to format the JSON with indentation (default true).</param>
        /// <returns>A JSON string representing the full conversation state.</returns>
        public string SaveToJson(bool indented = true)
        {
            lock (_lock)
            {
                var data = new ConversationData
                {
                    Messages = new List<MessageData>(),
                    Parameters = new ParameterData
                    {
                        Temperature = _temperature,
                        MaxTokens = _maxTokens,
                        TopP = _topP,
                        FrequencyPenalty = _frequencyPenalty,
                        PresencePenalty = _presencePenalty,
                        MaxRetries = _maxRetries
                    }
                };

                foreach (var msg in _messages)
                {
                    string role = msg switch
                    {
                        SystemChatMessage => "system",
                        UserChatMessage => "user",
                        AssistantChatMessage => "assistant",
                        _ => "unknown"
                    };

                    string content = "";
                    if (msg.Content != null)
                    {
                        foreach (var part in msg.Content)
                        {
                            if (part.Text != null)
                                content += part.Text;
                        }
                    }

                    data.Messages.Add(new MessageData { Role = role, Content = content });
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Serialize(data, options);
            }
        }

        /// <summary>
        /// Restores a conversation from a JSON string produced by
        /// <see cref="SaveToJson"/>. All messages and model parameters
        /// are restored to their saved state.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A new <see cref="Conversation"/> instance with the restored state.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or empty.</exception>
        /// <exception cref="JsonException">Thrown when the JSON is malformed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the JSON structure is invalid (missing messages array).</exception>
        public static Conversation LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON string cannot be null or empty.", nameof(json));

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var data = JsonSerializer.Deserialize<ConversationData>(json, options);

            if (data?.Messages == null)
                throw new InvalidOperationException(
                    "Invalid conversation JSON: missing messages array.");

            // Create without system prompt — we'll add messages manually
            var conv = new Conversation();

            // Restore parameters if present
            if (data.Parameters != null)
            {
                conv.Temperature = data.Parameters.Temperature;
                conv.MaxTokens = data.Parameters.MaxTokens;
                conv.TopP = data.Parameters.TopP;
                conv.FrequencyPenalty = data.Parameters.FrequencyPenalty;
                conv.PresencePenalty = data.Parameters.PresencePenalty;
                conv.MaxRetries = data.Parameters.MaxRetries;
            }

            // Restore messages
            foreach (var msg in data.Messages)
            {
                if (string.IsNullOrEmpty(msg.Content))
                    continue;

                switch (msg.Role?.ToLowerInvariant())
                {
                    case "system":
                        lock (conv._lock)
                            conv._messages.Add(new SystemChatMessage(msg.Content));
                        break;
                    case "user":
                        conv.AddUserMessage(msg.Content);
                        break;
                    case "assistant":
                        conv.AddAssistantMessage(msg.Content);
                        break;
                }
            }

            return conv;
        }

        /// <summary>
        /// Saves the conversation to a JSON file. Creates the file if it
        /// doesn't exist, overwrites if it does.
        /// </summary>
        /// <param name="filePath">Path to the output file.</param>
        /// <param name="indented">Whether to format the JSON with indentation (default true).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
        public async Task SaveToFileAsync(
            string filePath,
            bool indented = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            string json = SaveToJson(indented);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        /// <summary>
        /// Loads a conversation from a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the JSON file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A new <see cref="Conversation"/> instance with the restored state.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
        public static async Task<Conversation> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Conversation file not found: {filePath}", filePath);

            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return LoadFromJson(json);
        }

        // ──────────────── Serialization DTOs ────────────────

        /// <summary>
        /// Data transfer object for conversation serialization.
        /// </summary>
        internal class ConversationData
        {
            [JsonPropertyName("messages")]
            public List<MessageData> Messages { get; set; } = new();

            [JsonPropertyName("parameters")]
            public ParameterData? Parameters { get; set; }
        }

        /// <summary>
        /// Data transfer object for individual messages.
        /// </summary>
        internal class MessageData
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";
        }

        /// <summary>
        /// Data transfer object for model parameters.
        /// </summary>
        internal class ParameterData
        {
            [JsonPropertyName("temperature")]
            public float Temperature { get; set; } = 0.7f;

            [JsonPropertyName("maxTokens")]
            public int MaxTokens { get; set; } = 800;

            [JsonPropertyName("topP")]
            public float TopP { get; set; } = 0.95f;

            [JsonPropertyName("frequencyPenalty")]
            public float FrequencyPenalty { get; set; } = 0f;

            [JsonPropertyName("presencePenalty")]
            public float PresencePenalty { get; set; } = 0f;

            [JsonPropertyName("maxRetries")]
            public int MaxRetries { get; set; } = 3;
        }
    }
}
