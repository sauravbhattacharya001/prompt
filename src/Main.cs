namespace Prompt
{
    using System.ClientModel;
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
        // every call. The Lazy<T> wrapper ensures one-time initialization.
        private static readonly object _clientLock = new object();
        private static AzureOpenAIClient? _cachedAzureClient;
        private static ChatClient? _cachedChatClient;
        private static string? _cachedModel;

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
        /// The client reads environment variables once and reuses the HTTP
        /// connection pool across calls. Thread-safe via double-check locking.
        /// </summary>
        /// <remarks>
        /// The <paramref name="maxRetries"/> value is only used during first initialization.
        /// Subsequent calls return the cached client regardless of the value passed.
        /// Call <see cref="ResetClient"/> to force re-creation with a new retry policy.
        /// </remarks>
        private static ChatClient GetOrCreateChatClient(int maxRetries = 3)
        {
            if (_cachedChatClient != null)
                return _cachedChatClient;

            lock (_clientLock)
            {
                if (_cachedChatClient != null)
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

                _cachedModel = GetRequiredEnvVar("AZURE_OPENAI_API_MODEL",
                    "Set it with your deployed model name (e.g. gpt-4).");

                var clientOptions = CreateClientOptions(maxRetries);
                _cachedAzureClient = new AzureOpenAIClient(
                    endpoint,
                    new ApiKeyCredential(key),
                    clientOptions);

                _cachedChatClient = _cachedAzureClient.GetChatClient(_cachedModel);
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
                _cachedModel = null;
            }
        }

        /// <summary>
        /// Sends a prompt to Azure OpenAI and returns the response text.
        /// </summary>
        /// <param name="prompt">The user prompt to send as a user message.</param>
        /// <param name="systemPrompt">Optional system prompt to set the assistant's behavior.</param>
        /// <param name="maxRetries">Maximum number of retries for transient failures (default 3).</param>
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

            var completionOptions = new ChatCompletionOptions()
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 800,
                TopP = 0.95f,
                FrequencyPenalty = 0f,
                PresencePenalty = 0f,
            };

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                messages, completionOptions, cancellationToken);

            return completion?.Content?.FirstOrDefault()?.Text;
        }

        /// <summary>
        /// Sends a prompt to Azure OpenAI and returns the response text.
        /// </summary>
        /// <remarks>
        /// Deprecated: use <see cref="GetResponseAsync"/> instead.
        /// This method exists for backward compatibility and will be removed in a future major version.
        /// </remarks>
        [Obsolete("Use GetResponseAsync instead. This method will be removed in a future major version.")]
        public static Task<string?> GetResponseTest(
            string prompt,
            string? systemPrompt = null,
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
            => GetResponseAsync(prompt, systemPrompt, maxRetries, cancellationToken);

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
            if (string.IsNullOrEmpty(value) && OperatingSystem.IsWindows())
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
                     ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            }

            return value ?? throw new InvalidOperationException(
                $"Environment variable {name} is not set. {hint}");
        }
    }
}
