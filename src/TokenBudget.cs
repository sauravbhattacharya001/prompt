namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Strategy for trimming messages when the token budget is exceeded.
    /// </summary>
    public enum TrimStrategy
    {
        /// <summary>
        /// Remove the oldest non-system messages first (FIFO).
        /// Simple and predictable.
        /// </summary>
        RemoveOldest,

        /// <summary>
        /// Remove the oldest messages but keep the first N turns
        /// to preserve initial context/instructions. Configurable via
        /// <see cref="TokenBudget.KeepFirstTurns"/>.
        /// </summary>
        SlidingWindow,

        /// <summary>
        /// Remove the longest messages first to free the most tokens
        /// with the fewest message removals. Good for conversations
        /// with mixed short/long messages.
        /// </summary>
        RemoveLongest
    }

    /// <summary>
    /// Manages token budgets for conversations to prevent exceeding
    /// model context windows. Tracks estimated token usage across messages,
    /// automatically trims when approaching the limit, and provides
    /// usage analytics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Token estimation uses <see cref="PromptGuard.EstimateTokens"/> —
    /// a heuristic approximation, not exact BPE tokenization. The
    /// <see cref="ReserveTokens"/> property provides a safety margin
    /// for estimation inaccuracy.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var budget = new TokenBudget(maxTokens: 4096, reserveForResponse: 800);
    /// budget.AddMessage("system", "You are a helpful assistant.");
    /// budget.AddMessage("user", "What is quantum computing?");
    /// budget.AddMessage("assistant", "Quantum computing uses qubits...");
    /// budget.AddMessage("user", "Tell me more about qubits.");
    ///
    /// // Check budget status
    /// Console.WriteLine($"Used: {budget.UsedTokens}/{budget.AvailableTokens}");
    /// Console.WriteLine($"Usage: {budget.UsagePercent:F1}%");
    ///
    /// // Get trimmed messages for API call
    /// var messages = budget.GetMessages();
    /// </code>
    /// </para>
    /// </remarks>
    public class TokenBudget
    {
        /// <summary>Default context window size (GPT-4 Turbo).</summary>
        public const int DefaultMaxTokens = 128_000;

        /// <summary>Default tokens reserved for the model's response.</summary>
        public const int DefaultReserveForResponse = 4_096;

        /// <summary>Default safety margin for token estimation inaccuracy.</summary>
        public const int DefaultReserveTokens = 200;

        private readonly List<BudgetMessage> _messages = new();
        private readonly object _lock = new();
        private int _totalTokens;

        /// <summary>
        /// Creates a new token budget manager.
        /// </summary>
        /// <param name="maxTokens">
        /// The model's maximum context window size in tokens.
        /// Default: 128,000 (GPT-4 Turbo).
        /// </param>
        /// <param name="reserveForResponse">
        /// Tokens to reserve for the model's response. These tokens
        /// are subtracted from the budget so the context doesn't consume
        /// the full window. Default: 4,096.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when maxTokens &lt; 100 or reserveForResponse &lt; 0.
        /// </exception>
        public TokenBudget(
            int maxTokens = DefaultMaxTokens,
            int reserveForResponse = DefaultReserveForResponse)
        {
            if (maxTokens < 100)
                throw new ArgumentOutOfRangeException(nameof(maxTokens), maxTokens,
                    "Max tokens must be at least 100.");
            if (reserveForResponse < 0)
                throw new ArgumentOutOfRangeException(nameof(reserveForResponse), reserveForResponse,
                    "Reserve for response cannot be negative.");
            if (reserveForResponse >= maxTokens)
                throw new ArgumentOutOfRangeException(nameof(reserveForResponse), reserveForResponse,
                    "Reserve for response must be less than max tokens.");

            MaxTokens = maxTokens;
            ReserveForResponse = reserveForResponse;
        }

        /// <summary>
        /// Gets the model's maximum context window size in tokens.
        /// </summary>
        public int MaxTokens { get; }

        /// <summary>
        /// Gets the tokens reserved for the model's response.
        /// </summary>
        public int ReserveForResponse { get; }

        private int _reserveTokens = DefaultReserveTokens;

        /// <summary>
        /// Gets or sets additional reserve tokens as a safety margin for
        /// token estimation inaccuracy. Default: 200.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is negative.</exception>
        public int ReserveTokens
        {
            get => _reserveTokens;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Reserve tokens cannot be negative.");
                _reserveTokens = value;
            }
        }

        /// <summary>
        /// Gets or sets the trim strategy used when the budget is exceeded.
        /// Default: <see cref="TrimStrategy.RemoveOldest"/>.
        /// </summary>
        public TrimStrategy Strategy { get; set; } = TrimStrategy.RemoveOldest;

        private int _keepFirstTurns = 1;

        /// <summary>
        /// Gets or sets the number of initial user/assistant turn pairs to keep
        /// when using <see cref="TrimStrategy.SlidingWindow"/>. Default: 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is negative.</exception>
        public int KeepFirstTurns
        {
            get => _keepFirstTurns;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "KeepFirstTurns cannot be negative.");
                _keepFirstTurns = value;
            }
        }

        /// <summary>
        /// Gets the available token budget for conversation messages
        /// (total minus reserves).
        /// </summary>
        public int AvailableTokens =>
            Math.Max(0, MaxTokens - ReserveForResponse - ReserveTokens);

        /// <summary>
        /// Gets the total estimated tokens currently used by all messages.
        /// </summary>
        public int UsedTokens
        {
            get { lock (_lock) return _totalTokens; }
        }

        /// <summary>
        /// Gets the remaining tokens before the budget is exhausted.
        /// </summary>
        public int RemainingTokens =>
            Math.Max(0, AvailableTokens - UsedTokens);

        /// <summary>
        /// Gets the usage percentage (0–100).
        /// </summary>
        public double UsagePercent
        {
            get
            {
                int available = AvailableTokens;
                return available > 0
                    ? Math.Min(100.0, (double)UsedTokens / available * 100.0)
                    : 100.0;
            }
        }

        /// <summary>
        /// Gets whether the budget is exceeded.
        /// </summary>
        public bool IsOverBudget => UsedTokens > AvailableTokens;

        /// <summary>
        /// Gets the number of messages in the budget.
        /// </summary>
        public int MessageCount
        {
            get { lock (_lock) return _messages.Count; }
        }

        /// <summary>
        /// Gets the number of messages that have been trimmed since creation.
        /// </summary>
        public int TrimmedCount { get; private set; }

        /// <summary>
        /// Gets the total tokens that have been trimmed since creation.
        /// </summary>
        public int TrimmedTokens { get; private set; }

        /// <summary>
        /// Adds a message to the budget and trims if necessary.
        /// </summary>
        /// <param name="role">Message role ("system", "user", or "assistant").</param>
        /// <param name="content">Message content.</param>
        /// <returns>
        /// The number of messages trimmed to fit this addition (0 if none).
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when role or content is null/empty, or role is invalid.
        /// </exception>
        public int AddMessage(string role, string content)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentException(
                    "Role cannot be null or empty.", nameof(role));
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException(
                    "Content cannot be null or empty.", nameof(content));

            var normalizedRole = role.ToLowerInvariant();
            if (normalizedRole != "system" && normalizedRole != "user" && normalizedRole != "assistant")
                throw new ArgumentException(
                    $"Invalid role '{role}'. Must be 'system', 'user', or 'assistant'.", nameof(role));

            int tokens = PromptGuard.EstimateTokens(content);
            // Add ~4 tokens overhead per message for role/formatting
            tokens += 4;

            var message = new BudgetMessage
            {
                Role = normalizedRole,
                Content = content,
                Tokens = tokens,
                AddedAt = DateTimeOffset.UtcNow
            };

            lock (_lock)
            {
                _messages.Add(message);
                _totalTokens += tokens;
                return TrimIfNeeded();
            }
        }

        /// <summary>
        /// Gets a snapshot of all current messages as role-content pairs.
        /// </summary>
        public List<(string Role, string Content)> GetMessages()
        {
            lock (_lock)
            {
                return _messages
                    .Select(m => (m.Role, m.Content))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets a snapshot of all current messages with token estimates.
        /// </summary>
        public List<(string Role, string Content, int Tokens)> GetMessagesWithTokens()
        {
            lock (_lock)
            {
                return _messages
                    .Select(m => (m.Role, m.Content, m.Tokens))
                    .ToList();
            }
        }

        /// <summary>
        /// Checks whether a new message would fit within the budget
        /// without actually adding it.
        /// </summary>
        /// <param name="content">The message content to check.</param>
        /// <returns>True if the message fits; false if trimming would be needed.</returns>
        public bool WouldFit(string content)
        {
            if (string.IsNullOrEmpty(content))
                return true;

            int tokens = PromptGuard.EstimateTokens(content) + 4;
            return (UsedTokens + tokens) <= AvailableTokens;
        }

        /// <summary>
        /// Clears all non-system messages. System messages are preserved.
        /// </summary>
        public void ClearHistory()
        {
            lock (_lock)
            {
                var systemMessages = _messages
                    .Where(m => m.Role == "system")
                    .ToList();

                _messages.Clear();
                _messages.AddRange(systemMessages);
                _totalTokens = systemMessages.Sum(m => m.Tokens);
            }
        }

        /// <summary>
        /// Clears all messages including system messages.
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                _messages.Clear();
                _totalTokens = 0;
            }
        }

        /// <summary>
        /// Returns a summary of the current budget state.
        /// </summary>
        public BudgetSummary GetSummary()
        {
            lock (_lock)
            {
                return new BudgetSummary
                {
                    MaxTokens = MaxTokens,
                    ReserveForResponse = ReserveForResponse,
                    ReserveTokens = ReserveTokens,
                    AvailableTokens = AvailableTokens,
                    UsedTokens = _totalTokens,
                    RemainingTokens = RemainingTokens,
                    UsagePercent = UsagePercent,
                    IsOverBudget = IsOverBudget,
                    MessageCount = _messages.Count,
                    SystemMessages = _messages.Count(m => m.Role == "system"),
                    UserMessages = _messages.Count(m => m.Role == "user"),
                    AssistantMessages = _messages.Count(m => m.Role == "assistant"),
                    TrimmedCount = TrimmedCount,
                    TrimmedTokens = TrimmedTokens,
                    Strategy = Strategy,
                    LargestMessageTokens = _messages.Count > 0
                        ? _messages.Max(m => m.Tokens)
                        : 0,
                    AverageMessageTokens = _messages.Count > 0
                        ? (int)Math.Round(_messages.Average(m => (double)m.Tokens))
                        : 0
                };
            }
        }

        /// <summary>
        /// Serializes the budget state to JSON for persistence.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            lock (_lock)
            {
                var data = new TokenBudgetData
                {
                    MaxTokens = MaxTokens,
                    ReserveForResponse = ReserveForResponse,
                    ReserveTokens = ReserveTokens,
                    Strategy = Strategy.ToString(),
                    KeepFirstTurns = KeepFirstTurns,
                    Messages = _messages.Select(m => new TokenBudgetMessageData
                    {
                        Role = m.Role,
                        Content = m.Content,
                        Tokens = m.Tokens,
                    }).ToList(),
                    TrimmedCount = TrimmedCount,
                    TrimmedTokens = TrimmedTokens
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Serialize(data, options);
            }
        }

        /// <summary>
        /// Restores a token budget from JSON.
        /// </summary>
        public static TokenBudget FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON string cannot be null or empty.", nameof(json));

            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var data = JsonSerializer.Deserialize<TokenBudgetData>(json, options)
                ?? throw new InvalidOperationException(
                    "Invalid token budget JSON: deserialization returned null.");

            if (data.Messages == null)
                throw new InvalidOperationException(
                    "Invalid token budget JSON: missing messages array.");

            var budget = new TokenBudget(data.MaxTokens, data.ReserveForResponse)
            {
                ReserveTokens = data.ReserveTokens,
                KeepFirstTurns = data.KeepFirstTurns,
                TrimmedCount = data.TrimmedCount,
                TrimmedTokens = data.TrimmedTokens
            };

            if (Enum.TryParse<TrimStrategy>(data.Strategy, true, out var strategy))
                budget.Strategy = strategy;

            lock (budget._lock)
            {
                foreach (var msg in data.Messages)
                {
                    if (string.IsNullOrEmpty(msg.Content))
                        continue;

                    budget._messages.Add(new BudgetMessage
                    {
                        Role = msg.Role,
                        Content = msg.Content,
                        Tokens = msg.Tokens > 0 ? msg.Tokens : PromptGuard.EstimateTokens(msg.Content) + 4,
                        AddedAt = DateTimeOffset.UtcNow
                    });
                }

                budget._totalTokens = budget._messages.Sum(m => m.Tokens);
            }

            return budget;
        }

        /// <summary>
        /// Creates a TokenBudget pre-configured for a specific model.
        /// </summary>
        /// <param name="model">Model name (e.g., "gpt-4", "gpt-4-turbo", "gpt-3.5-turbo").</param>
        /// <param name="reserveForResponse">Tokens to reserve for the response.</param>
        /// <returns>A TokenBudget configured for the specified model.</returns>
        public static TokenBudget ForModel(string model, int reserveForResponse = DefaultReserveForResponse)
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException(
                    "Model name cannot be null or empty.", nameof(model));

            int contextWindow = model.ToLowerInvariant() switch
            {
                "gpt-4" or "gpt-4-0613" => 8_192,
                "gpt-4-32k" or "gpt-4-32k-0613" => 32_768,
                "gpt-4-turbo" or "gpt-4-turbo-2024-04-09" or "gpt-4-1106-preview" => 128_000,
                "gpt-4o" or "gpt-4o-2024-05-13" or "gpt-4o-2024-08-06" => 128_000,
                "gpt-4o-mini" or "gpt-4o-mini-2024-07-18" => 128_000,
                "gpt-3.5-turbo" or "gpt-3.5-turbo-0125" => 16_385,
                "gpt-3.5-turbo-16k" => 16_385,
                "claude-3-opus" or "claude-3-opus-20240229" => 200_000,
                "claude-3-sonnet" or "claude-3-5-sonnet" or "claude-3-5-sonnet-20241022" => 200_000,
                "claude-3-haiku" or "claude-3-5-haiku" => 200_000,
                "claude-4-opus" or "claude-4-sonnet" => 200_000,
                _ => DefaultMaxTokens
            };

            // Clamp reserve to be less than context window
            int reserve = Math.Min(reserveForResponse, contextWindow - 100);

            return new TokenBudget(contextWindow, reserve);
        }

        // ──────────────── Trim Logic ────────────────

        /// <summary>
        /// Trims messages according to the current strategy until the budget
        /// is satisfied. Must be called under <c>_lock</c>.
        /// </summary>
        /// <returns>Number of messages trimmed.</returns>
        private int TrimIfNeeded()
        {
            int trimmed = 0;
            int available = AvailableTokens;

            while (_totalTokens > available)
            {
                int indexToRemove = FindTrimCandidate();
                if (indexToRemove < 0)
                    break; // Only system messages left

                var removed = _messages[indexToRemove];
                _messages.RemoveAt(indexToRemove);
                _totalTokens -= removed.Tokens;
                trimmed++;
                TrimmedCount++;
                TrimmedTokens += removed.Tokens;
            }

            return trimmed;
        }

        /// <summary>
        /// Finds the index of the next message to remove based on the
        /// current trim strategy. Returns -1 if no trimmable message exists.
        /// Must be called under <c>_lock</c>.
        /// </summary>
        private int FindTrimCandidate()
        {
            return Strategy switch
            {
                TrimStrategy.RemoveOldest => FindOldestCandidate(),
                TrimStrategy.SlidingWindow => FindSlidingWindowCandidate(),
                TrimStrategy.RemoveLongest => FindLongestCandidate(),
                _ => FindOldestCandidate()
            };
        }

        /// <summary>
        /// Finds the oldest non-system message.
        /// </summary>
        private int FindOldestCandidate()
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].Role != "system")
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the oldest non-system, non-protected message.
        /// Protected messages are the first N turn pairs after system messages.
        /// </summary>
        private int FindSlidingWindowCandidate()
        {
            int nonSystemSeen = 0;
            int protectedCount = KeepFirstTurns * 2; // user + assistant per turn

            for (int i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].Role == "system")
                    continue;

                nonSystemSeen++;

                if (nonSystemSeen > protectedCount)
                    return i;
            }

            // All non-system messages are protected — fall back to oldest
            return FindOldestCandidate();
        }

        /// <summary>
        /// Finds the non-system message with the most tokens.
        /// </summary>
        private int FindLongestCandidate()
        {
            int maxTokens = -1;
            int maxIndex = -1;

            for (int i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].Role == "system")
                    continue;

                if (_messages[i].Tokens > maxTokens)
                {
                    maxTokens = _messages[i].Tokens;
                    maxIndex = i;
                }
            }

            return maxIndex;
        }

        // ──────────────── Internal Types ────────────────

        private class BudgetMessage
        {
            public string Role { get; set; } = "";
            public string Content { get; set; } = "";
            public int Tokens { get; set; }
            public DateTimeOffset AddedAt { get; set; }
        }

        internal class TokenBudgetData
        {
            [JsonPropertyName("maxTokens")]
            public int MaxTokens { get; set; } = DefaultMaxTokens;

            [JsonPropertyName("reserveForResponse")]
            public int ReserveForResponse { get; set; } = DefaultReserveForResponse;

            [JsonPropertyName("reserveTokens")]
            public int ReserveTokens { get; set; } = DefaultReserveTokens;

            [JsonPropertyName("strategy")]
            public string Strategy { get; set; } = "RemoveOldest";

            [JsonPropertyName("keepFirstTurns")]
            public int KeepFirstTurns { get; set; } = 1;

            [JsonPropertyName("messages")]
            public List<TokenBudgetMessageData> Messages { get; set; } = new();

            [JsonPropertyName("trimmedCount")]
            public int TrimmedCount { get; set; }

            [JsonPropertyName("trimmedTokens")]
            public int TrimmedTokens { get; set; }
        }

        internal class TokenBudgetMessageData
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";

            [JsonPropertyName("tokens")]
            public int Tokens { get; set; }
        }
    }

    /// <summary>
    /// Summary of the current token budget state.
    /// </summary>
    public class BudgetSummary
    {
        /// <summary>Maximum context window size.</summary>
        public int MaxTokens { get; init; }

        /// <summary>Tokens reserved for response.</summary>
        public int ReserveForResponse { get; init; }

        /// <summary>Safety margin tokens.</summary>
        public int ReserveTokens { get; init; }

        /// <summary>Available tokens for messages.</summary>
        public int AvailableTokens { get; init; }

        /// <summary>Currently used tokens.</summary>
        public int UsedTokens { get; init; }

        /// <summary>Remaining tokens before budget exhaustion.</summary>
        public int RemainingTokens { get; init; }

        /// <summary>Usage percentage (0–100).</summary>
        public double UsagePercent { get; init; }

        /// <summary>Whether the budget is exceeded.</summary>
        public bool IsOverBudget { get; init; }

        /// <summary>Total message count.</summary>
        public int MessageCount { get; init; }

        /// <summary>System message count.</summary>
        public int SystemMessages { get; init; }

        /// <summary>User message count.</summary>
        public int UserMessages { get; init; }

        /// <summary>Assistant message count.</summary>
        public int AssistantMessages { get; init; }

        /// <summary>Total messages trimmed since creation.</summary>
        public int TrimmedCount { get; init; }

        /// <summary>Total tokens trimmed since creation.</summary>
        public int TrimmedTokens { get; init; }

        /// <summary>Active trim strategy.</summary>
        public TrimStrategy Strategy { get; init; }

        /// <summary>Tokens in the largest message.</summary>
        public int LargestMessageTokens { get; init; }

        /// <summary>Average tokens per message.</summary>
        public int AverageMessageTokens { get; init; }

        /// <summary>Returns a human-readable budget summary.</summary>
        public override string ToString() =>
            $"TokenBudget: {UsedTokens}/{AvailableTokens} tokens " +
            $"({UsagePercent:F1}%), {MessageCount} messages, " +
            $"{TrimmedCount} trimmed ({TrimmedTokens} tokens), " +
            $"strategy={Strategy}";
    }
}
