namespace Prompt
{
    using System.ClientModel;
    using System.ClientModel.Primitives;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.AI.OpenAI;
    using OpenAI.Chat;

    /// <summary>
    /// <see cref="ILlmProvider"/> backed by Azure OpenAI. This is the library's
    /// default provider and preserves the original zero-configuration behavior:
    /// it reads <c>AZURE_OPENAI_API_URI</c>, <c>AZURE_OPENAI_API_KEY</c>, and
    /// <c>AZURE_OPENAI_API_MODEL</c> from the environment and reuses a cached,
    /// thread-safe <see cref="ChatClient"/> across calls.
    /// </summary>
    /// <remarks>
    /// The Azure SDK plumbing (client caching, retry policy, environment-variable
    /// resolution) lives here. <see cref="Main"/> delegates its
    /// <c>GetOrCreateChatClient</c> and <c>ResetClient</c> entry points to the
    /// static members of this class so existing callers keep working unchanged.
    /// </remarks>
    public sealed class AzureOpenAIProvider : ILlmProvider
    {
        // Cached client instances for connection reuse.
        // AzureOpenAIClient and ChatClient are thread-safe and designed to be
        // long-lived singletons, so we avoid recreating them on every call.
        //
        // _cachedChatClient is volatile so the double-checked locking pattern in
        // GetOrCreateChatClient works correctly across all CPU architectures:
        // without it a thread could observe a non-null _cachedChatClient before
        // the write to _cachedMaxRetries has been flushed, using a stale field.
        private static readonly object _clientLock = new object();
        private static AzureOpenAIClient? _cachedAzureClient;
        private static volatile ChatClient? _cachedChatClient;
        private static volatile int _cachedMaxRetries = -1;

        private readonly int _maxRetries;

        /// <summary>
        /// Creates an Azure provider that resolves its endpoint, key, and model
        /// from the environment on first use.
        /// </summary>
        /// <param name="maxRetries">
        /// Maximum number of retries for transient failures (default 3). Changing
        /// this value relative to the cached client triggers a transparent rebuild.
        /// </param>
        public AzureOpenAIProvider(int maxRetries = 3)
        {
            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries),
                    maxRetries, "maxRetries must be non-negative.");
            _maxRetries = maxRetries;
        }

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
        /// the client is automatically recreated with the new retry policy.
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
        /// Resets the cached Azure client so the next call re-reads environment
        /// variables and applies a fresh retry policy.
        /// </summary>
        internal static void ResetCache()
        {
            lock (_clientLock)
            {
                _cachedChatClient = null;
                _cachedAzureClient = null;
                _cachedMaxRetries = -1;
            }
        }

        /// <summary>
        /// Maps neutral <see cref="ChatMsg"/> values onto Azure SDK
        /// <see cref="ChatMessage"/> instances, preserving role semantics.
        /// </summary>
        private static List<ChatMessage> ToAzureMessages(IReadOnlyList<ChatMsg> messages)
        {
            var list = new List<ChatMessage>(messages.Count);
            foreach (var msg in messages)
            {
                switch (msg.Role)
                {
                    case ChatMsg.System:
                        list.Add(new SystemChatMessage(msg.Content));
                        break;
                    case ChatMsg.Assistant:
                        list.Add(new AssistantChatMessage(msg.Content));
                        break;
                    default:
                        list.Add(new UserChatMessage(msg.Content));
                        break;
                }
            }
            return list;
        }

        /// <inheritdoc />
        public async Task<string?> CompleteAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            CancellationToken ct = default)
        {
            var chatClient = GetOrCreateChatClient(_maxRetries);
            var azureMessages = ToAzureMessages(messages);
            var completionOptions = options.ToChatCompletionOptions();

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                azureMessages, completionOptions, ct);

            return completion?.Content?.FirstOrDefault()?.Text;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<StreamChunk> CompleteStreamAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var chatClient = GetOrCreateChatClient(_maxRetries);
            var azureMessages = ToAzureMessages(messages);
            var completionOptions = options.ToChatCompletionOptions();

            var accumulated = new StringBuilder();
            string? finishReason = null;

            await foreach (StreamingChatCompletionUpdate update in
                chatClient.CompleteChatStreamingAsync(azureMessages, completionOptions, ct))
            {
                foreach (ChatMessageContentPart part in update.ContentUpdate)
                {
                    string delta = part.Text ?? "";
                    accumulated.Append(delta);

                    if (update.FinishReason != null)
                        finishReason = update.FinishReason.Value.ToString();

                    bool isComplete = update.FinishReason != null;
                    string fullTextSoFar = accumulated.ToString();

                    yield return new StreamChunk
                    {
                        Delta = delta,
                        FullText = fullTextSoFar,
                        IsComplete = isComplete,
                        FinishReason = isComplete ? finishReason : null,
                        TokensUsed = PromptGuard.EstimateTokens(fullTextSoFar)
                    };
                }
            }

            // If stream ended without explicit finish, emit a final chunk
            if (finishReason == null)
            {
                string finalText = accumulated.ToString();
                yield return new StreamChunk
                {
                    Delta = "",
                    FullText = finalText,
                    IsComplete = true,
                    FinishReason = "stop",
                    TokensUsed = PromptGuard.EstimateTokens(finalText)
                };
            }
        }

        /// <summary>
        /// Reads an environment variable with a cross-platform fallback chain:
        /// Process → User (Windows only) → Machine (Windows only).
        /// </summary>
        /// <remarks>
        /// <c>EnvironmentVariableTarget.User</c> only works on Windows. On
        /// Linux/macOS it silently returns <c>null</c>, so we try
        /// <c>EnvironmentVariableTarget.Process</c> first, which reads variables
        /// set via shell profiles, Docker, systemd, etc.
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

            // Treat empty/whitespace-only values as unset — they are never valid
            // for URIs, API keys, or model names and would cause confusing
            // downstream errors if allowed through.
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"Environment variable {name} is not set or is empty. {hint}");

            return value;
        }
    }
}
