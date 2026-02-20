namespace Prompt
{
    using System.ClientModel;
    using System.Text;
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
        /// <summary>
        /// Maximum number of messages allowed in a conversation to prevent
        /// unbounded memory growth and API token limit exhaustion.
        /// Default: 1000 messages (system + user + assistant).
        /// </summary>
        public const int DefaultMaxMessages = 1000;

        /// <summary>
        /// Maximum allowed JSON payload size for deserialization to prevent
        /// denial-of-service via crafted large payloads.
        /// Default: 10 MB.
        /// </summary>
        internal const int MaxJsonPayloadBytes = SerializationGuards.MaxJsonPayloadBytes;

        /// <summary>
        /// Maximum number of messages allowed when deserializing from JSON
        /// to prevent memory exhaustion from crafted payloads.
        /// </summary>
        internal const int MaxDeserializedMessages = 10_000;

        private readonly List<ChatMessage> _messages = new();
        private readonly object _lock = new();
        private int _maxMessages = DefaultMaxMessages;

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
        /// Creates a new conversation with a system prompt and
        /// <see cref="PromptOptions"/> for model parameter configuration.
        /// </summary>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="options">
        /// Options to configure temperature, max tokens, top-p, and penalties.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="options"/> is null.
        /// </exception>
        public Conversation(string? systemPrompt, PromptOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (!string.IsNullOrWhiteSpace(systemPrompt))
                _messages.Add(new SystemChatMessage(systemPrompt));

            _temperature = options.Temperature;
            _maxTokens = options.MaxTokens;
            _topP = options.TopP;
            _frequencyPenalty = options.FrequencyPenalty;
            _presencePenalty = options.PresencePenalty;
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
        /// Gets or sets the maximum number of messages allowed in the conversation
        /// (including system prompt, user messages, and assistant messages).
        /// When the limit is reached, the oldest non-system messages are removed
        /// to make room for new ones. This prevents unbounded memory growth and
        /// helps stay within API token limits for long-running conversations.
        /// Default: 1000. Set to <see cref="int.MaxValue"/> to disable the limit.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is less than 2.</exception>
        public int MaxMessages
        {
            get => _maxMessages;
            set
            {
                if (value < 2)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "MaxMessages must be at least 2.");
                _maxMessages = value;
            }
        }

        /// <summary>
        /// Sends a user message and returns the assistant's response.
        /// Both the user message and assistant response are added to the
        /// conversation history for future context.
        /// </summary>
        /// <remarks>
        /// If the conversation exceeds <see cref="MaxMessages"/>, the oldest
        /// non-system messages are removed to stay within the limit.
        /// </remarks>
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
                TrimMessagesUnsafe();
                snapshot = new List<ChatMessage>(_messages);
            }

            var completionOptions = new PromptOptions
            {
                Temperature = _temperature,
                MaxTokens = _maxTokens,
                TopP = _topP,
                FrequencyPenalty = _frequencyPenalty,
                PresencePenalty = _presencePenalty,
            }.ToChatCompletionOptions();

            ChatClient chatClient = Main.GetOrCreateChatClient(_maxRetries);

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                snapshot, completionOptions, cancellationToken);

            string? responseText = completion?.Content?.FirstOrDefault()?.Text;

            if (responseText != null)
            {
                lock (_lock)
                {
                    _messages.Add(new AssistantChatMessage(responseText));
                    TrimMessagesUnsafe();
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

            lock (_lock)
            {
                _messages.Add(new UserChatMessage(message));
                TrimMessagesUnsafe();
            }
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

            lock (_lock)
            {
                _messages.Add(new AssistantChatMessage(message));
                TrimMessagesUnsafe();
            }
        }

        /// <summary>
        /// Removes the oldest non-system messages when the conversation
        /// exceeds <see cref="MaxMessages"/>. Must be called under <c>_lock</c>.
        /// </summary>
        /// <remarks>
        /// Optimized to avoid repeated linear scans: since system messages
        /// are only ever at index 0, the first non-system message is always
        /// at index 0 (no system prompt) or index 1 (with system prompt).
        /// </remarks>
        private void TrimMessagesUnsafe()
        {
            if (_messages.Count <= _maxMessages)
                return;

            // Determine the starting index of non-system messages once
            int firstNonSystem = (_messages.Count > 0 && _messages[0] is SystemChatMessage) ? 1 : 0;

            while (_messages.Count > _maxMessages)
            {
                if (firstNonSystem < _messages.Count)
                    _messages.RemoveAt(firstNonSystem);
                else
                    break; // Only system messages left, can't trim further
            }
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
                var history = new List<(string, string)>(_messages.Count);
                foreach (var msg in _messages)
                {
                    history.Add((GetRole(msg), ExtractContent(msg)));
                }
                return history;
            }
        }

        /// <summary>
        /// Extracts text content from a <see cref="ChatMessage"/>.
        /// Uses a single-part fast path to avoid <see cref="StringBuilder"/>
        /// allocation for the common case. Falls back to <see cref="StringBuilder"/>
        /// only when a message has multiple text content parts.
        /// </summary>
        private static string ExtractContent(ChatMessage msg)
        {
            if (msg.Content == null)
                return "";

            // Fast path: most messages have exactly one text part.
            // Avoid StringBuilder allocation entirely for the common case.
            string? singleText = null;
            bool hasMultiple = false;

            foreach (var part in msg.Content)
            {
                if (part.Text != null)
                {
                    if (singleText == null)
                    {
                        singleText = part.Text;
                    }
                    else
                    {
                        hasMultiple = true;
                        break;
                    }
                }
            }

            if (!hasMultiple)
                return singleText ?? "";

            // Slow path: multiple text parts — use StringBuilder
            var sb = new StringBuilder();
            foreach (var part in msg.Content)
            {
                if (part.Text != null)
                    sb.Append(part.Text);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Maps a <see cref="ChatMessage"/> to its role string
        /// ("system", "user", "assistant", or "unknown").
        /// </summary>
        private static string GetRole(ChatMessage msg) => msg switch
        {
            SystemChatMessage => "system",
            UserChatMessage => "user",
            AssistantChatMessage => "assistant",
            _ => "unknown"
        };

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
                    Messages = new List<MessageData>(_messages.Count),
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
                    data.Messages.Add(new MessageData { Role = GetRole(msg), Content = ExtractContent(msg) });
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
        /// <exception cref="InvalidOperationException">
        /// Thrown when the JSON structure is invalid (missing messages array)
        /// or the payload exceeds security limits.
        /// </exception>
        public static Conversation LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON string cannot be null or empty.", nameof(json));

            // Guard against oversized payloads that could cause memory exhaustion
            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var data = JsonSerializer.Deserialize<ConversationData>(json, options);

            if (data?.Messages == null)
                throw new InvalidOperationException(
                    "Invalid conversation JSON: missing messages array.");

            // Guard against message count-based memory exhaustion
            if (data.Messages.Count > MaxDeserializedMessages)
                throw new InvalidOperationException(
                    $"Conversation JSON contains {data.Messages.Count} messages, " +
                    $"exceeding the maximum allowed count of {MaxDeserializedMessages}. " +
                    "This limit prevents denial-of-service from crafted payloads.");

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

            // Restore messages — set MaxMessages high during restore to
            // avoid trimming, then apply the restored conversation's limit.
            // Batch all message additions under a single lock acquisition
            // instead of locking/unlocking per message.
            conv._maxMessages = int.MaxValue;

            lock (conv._lock)
            {
                foreach (var msg in data.Messages)
                {
                    if (string.IsNullOrEmpty(msg.Content))
                        continue;

                    switch (msg.Role?.ToLowerInvariant())
                    {
                        case "system":
                            conv._messages.Add(new SystemChatMessage(msg.Content));
                            break;
                        case "user":
                            conv._messages.Add(new UserChatMessage(msg.Content));
                            break;
                        case "assistant":
                            conv._messages.Add(new AssistantChatMessage(msg.Content));
                            break;
                    }
                }
            }

            // Restore default max messages limit
            conv._maxMessages = DefaultMaxMessages;

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
        /// <exception cref="InvalidOperationException">Thrown when the file exceeds the maximum allowed size.</exception>
        public static async Task<Conversation> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            // Resolve to full path to prevent path traversal ambiguity
            filePath = Path.GetFullPath(filePath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Conversation file not found: {filePath}", filePath);

            // Check file size before reading to prevent memory exhaustion
            SerializationGuards.ThrowIfFileTooLarge(filePath);

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
