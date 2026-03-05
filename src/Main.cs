namespace Prompt
{
    using System.ClientModel;
    using System.ClientModel.Primitives;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Azure.AI.OpenAI;
    using OpenAI.Chat;

    /// <summary>
    /// Entry point for sending chat completions requests to Azure OpenAI.
    /// </summary>
    /// <remarks>
    /// Requires the following user-level environment variables:
    /// <list type="bullet">
    ///   <item><c>AZURE_OPENAI_API_URI</c> – Azure OpenAI endpoint URI</item>
    ///   <item><c>AZURE_OPENAI_API_KEY</c> – Azure OpenAI API key</item>
    ///   <item><c>AZURE_OPENAI_API_MODEL</c> – Deployed model name (e.g. gpt-4)</item>
    /// </list>
    /// </remarks>
    public class Main
    {
        // Cached client instances for connection reuse (fixes #6).
        // AzureOpenAIClient and ChatClient are thread-safe and designed
        // to be long-lived singletons, so we avoid recreating them on
        // every call.
        //
        // _cachedChatClient is marked volatile so that the double-checked
        // locking pattern in GetOrCreateChatClient works correctly across
        // all CPU architectures. Without volatile, a thread could observe
        // a non-null _cachedChatClient before the write to _cachedMaxRetries
        // has been flushed — leading to use of a stale companion field.
        private static readonly object _clientLock = new object();
        private static AzureOpenAIClient? _cachedAzureClient;
        private static volatile ChatClient? _cachedChatClient;
        private static volatile int _cachedMaxRetries = -1;

        /// <summary>
        /// Creates an <see cref="AzureOpenAIClientOptions"/> with retry configuration.
        /// Uses exponential backoff (1s base, 30s max) which handles 429 rate-limit
        /// and 503 service-unavailable responses automatically via the Azure.Core pipeline.
        /// </summary>
        private static AzureOpenAIClientOptions CreateClientOptions(int maxRetries = 3)
        {
            var options = new AzureOpenAIClientOptions();
            options.RetryPolicy = new ClientRetryPolicy(maxRetries);
            return options;
        }

        /// <summary>
        /// Returns a cached <see cref="ChatClient"/>, creating it on first use.
        /// If <paramref name="maxRetries"/> differs from the previously cached value,
        /// the client is automatically recreated with the new retry policy (fixes #7).
        /// Thread-safe via double-check locking.
        /// </summary>
        internal static ChatClient GetOrCreateChatClient(int maxRetries = 3)
        {
            if (_cachedChatClient != null && _cachedMaxRetries == maxRetries)
                return _cachedChatClient;

            lock (_clientLock)
            {
                if (_cachedChatClient != null && _cachedMaxRetries == maxRetries)
                    return _cachedChatClient;

                var uri = GetRequiredEnvVar("AZURE_OPENAI_API_URI",
                    "Set it pointing to your Azure OpenAI endpoint.");

                if (!Uri.TryCreate(uri, UriKind.Absolute, out var endpoint)
                    || (endpoint.Scheme != "https" && endpoint.Scheme != "http"))
                {
                    throw new InvalidOperationException(
                        $"AZURE_OPENAI_API_URI value '{uri}' is not a valid HTTP(S) URI.");
                }

                var key = GetRequiredEnvVar("AZURE_OPENAI_API_KEY",
                    "Set it with your Azure OpenAI API key.");

                var model = GetRequiredEnvVar("AZURE_OPENAI_API_MODEL",
                    "Set it with your deployed model name (e.g. gpt-4).");

                var clientOptions = CreateClientOptions(maxRetries);
                _cachedAzureClient = new AzureOpenAIClient(
                    endpoint,
                    new ApiKeyCredential(key),
                    clientOptions);

                _cachedChatClient = _cachedAzureClient.GetChatClient(model);
                _cachedMaxRetries = maxRetries;
                return _cachedChatClient;
            }
        }

        /// <summary>
        /// Resets the cached client so the next call to <see cref="GetResponseAsync"/>
        /// re-reads environment variables and applies a fresh retry policy.
        /// Useful when environment variables change at runtime or when a different
        /// <c>maxRetries</c> value is needed.
        /// </summary>
        public static void ResetClient()
        {
            lock (_clientLock)
            {
                _cachedChatClient = null;
                _cachedAzureClient = null;
                _cachedMaxRetries = -1;
            }
        }

        /// <summary>
        /// Sends a prompt to Azure OpenAI and returns the response text.
        /// </summary>
        /// <param name="prompt">The user prompt to send as a user message.</param>
        /// <param name="systemPrompt">Optional system prompt to set the assistant's behavior.</param>
        /// <param name="maxRetries">Maximum number of retries for transient failures (default 3).</param>
        /// <param name="options">
        /// Optional <see cref="PromptOptions"/> to customize model behavior
        /// (temperature, max tokens, top-p, penalties). When <c>null</c>,
        /// uses the library defaults (Temperature 0.7, MaxTokens 800, TopP 0.95).
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the request.</param>
        /// <returns>The model's response text, or <c>null</c> if no response was generated.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="prompt"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a required environment variable is not set.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.
        /// </exception>
        public static async Task<string?> GetResponseAsync(
            string prompt,
            string? systemPrompt = null,
            int maxRetries = 3,
            PromptOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries),
                    maxRetries, "maxRetries must be non-negative.");

            ChatClient chatClient = GetOrCreateChatClient(maxRetries);

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(new SystemChatMessage(systemPrompt));
            messages.Add(new UserChatMessage(prompt));

            // Use caller-provided options or fall back to library defaults
            var opts = options ?? new PromptOptions();
            var completionOptions = opts.ToChatCompletionOptions();

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                messages, completionOptions, cancellationToken);

            return completion?.Content?.FirstOrDefault()?.Text;
        }

        /// <summary>
        /// Sends a prompt to Azure OpenAI and streams the response as an
        /// <see cref="IAsyncEnumerable{StreamChunk}"/>. Each chunk contains
        /// the incremental delta text and the accumulated full text so far.
        /// </summary>
        /// <param name="prompt">The user prompt to send.</param>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="maxRetries">Maximum retries for transient failures.</param>
        /// <param name="options">Optional model parameters.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An async stream of <see cref="StreamChunk"/> instances.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="prompt"/> is null or empty.</exception>
        public static async IAsyncEnumerable<StreamChunk> GetResponseStreamAsync(
            string prompt,
            string? systemPrompt = null,
            int maxRetries = 3,
            PromptOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries),
                    maxRetries, "maxRetries must be non-negative.");

            ChatClient chatClient = GetOrCreateChatClient(maxRetries);

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(new SystemChatMessage(systemPrompt));
            messages.Add(new UserChatMessage(prompt));

            var opts = options ?? new PromptOptions();
            var completionOptions = opts.ToChatCompletionOptions();

            var fullText = new StringBuilder();
            string? finishReason = null;

            AsyncCollectionResult<StreamingChatCompletionUpdate> stream =
                chatClient.CompleteChatStreamingAsync(
                    messages, completionOptions, ct);

            await foreach (StreamingChatCompletionUpdate update in stream)
            {
                ct.ThrowIfCancellationRequested();

                // Capture finish reason when available
                if (update.FinishReason.HasValue)
                {
                    finishReason = update.FinishReason.Value.ToString().ToLowerInvariant();
                }

                // Extract delta text from content update parts
                string delta = "";
                if (update.ContentUpdate != null)
                {
                    foreach (var part in update.ContentUpdate)
                    {
                        if (part.Text != null)
                            delta += part.Text;
                    }
                }

                if (delta.Length > 0)
                {
                    fullText.Append(delta);
                }

                // Yield a chunk for every update that has content or is final
                bool isComplete = update.FinishReason.HasValue;
                if (delta.Length > 0 || isComplete)
                {
                    string accumulated = fullText.ToString();
                    yield return new StreamChunk
                    {
                        Delta = delta,
                        FullText = accumulated,
                        IsComplete = isComplete,
                        FinishReason = isComplete ? finishReason : null,
                        TokensUsed = EstimateTokens(accumulated)
                    };
                }
            }
        }

        /// <summary>
        /// Streams a response from Azure OpenAI for a pre-built message list.
        /// Used internally by <see cref="Conversation"/> and <see cref="PromptChain"/>.
        /// </summary>
        internal static async IAsyncEnumerable<StreamChunk> StreamFromMessagesAsync(
            IEnumerable<ChatMessage> messages,
            ChatCompletionOptions completionOptions,
            int maxRetries = 3,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ChatClient chatClient = GetOrCreateChatClient(maxRetries);

            var fullText = new StringBuilder();
            string? finishReason = null;

            AsyncCollectionResult<StreamingChatCompletionUpdate> stream =
                chatClient.CompleteChatStreamingAsync(
                    messages.ToList(), completionOptions, ct);

            await foreach (StreamingChatCompletionUpdate update in stream)
            {
                ct.ThrowIfCancellationRequested();

                if (update.FinishReason.HasValue)
                    finishReason = update.FinishReason.Value.ToString().ToLowerInvariant();

                string delta = "";
                if (update.ContentUpdate != null)
                {
                    foreach (var part in update.ContentUpdate)
                    {
                        if (part.Text != null)
                            delta += part.Text;
                    }
                }

                if (delta.Length > 0)
                    fullText.Append(delta);

                bool isComplete = update.FinishReason.HasValue;
                if (delta.Length > 0 || isComplete)
                {
                    string accumulated = fullText.ToString();
                    yield return new StreamChunk
                    {
                        Delta = delta,
                        FullText = accumulated,
                        IsComplete = isComplete,
                        FinishReason = isComplete ? finishReason : null,
                        TokensUsed = EstimateTokens(accumulated)
                    };
                }
            }
        }

        /// <summary>
        /// Estimates token count using a simple heuristic (~4 chars per token for English).
        /// This matches the approach used by <see cref="PromptGuard"/>.
        /// </summary>
        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Approximate: 1 token ≈ 4 characters for English text
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        /// <summary>
        /// Reads an environment variable with a cross-platform fallback chain:
        /// Process → User (Windows only) → Machine (Windows only).
        /// </summary>
        /// <remarks>
        /// <c>EnvironmentVariableTarget.User</c> only works on Windows.
        /// On Linux/macOS it silently returns <c>null</c>, so we try
        /// <c>EnvironmentVariableTarget.Process</c> first, which reads
        /// variables set via shell profiles, Docker, systemd, etc.
        /// Fixes <see href="https://github.com/sauravbhattacharya001/prompt/issues/2">#2</see>.
        /// </remarks>
        private static string GetRequiredEnvVar(string name, string hint)
        {
            // Process-level covers shell exports, Docker env, CI, etc. — works everywhere
            var value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

            // On Windows, also check User and Machine scopes
            if (string.IsNullOrWhiteSpace(value) && OperatingSystem.IsWindows())
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
                if (string.IsNullOrWhiteSpace(value))
                    value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            }

            // Treat empty/whitespace-only values as unset — they are never
            // valid for URIs, API keys, or model names and would cause
            // confusing downstream errors if allowed through.
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"Environment variable {name} is not set or is empty. {hint}");

            return value;
        }
    }
}
