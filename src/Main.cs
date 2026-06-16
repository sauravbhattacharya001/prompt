namespace Prompt
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using OpenAI.Chat;

    /// <summary>
    /// Entry point for sending chat completions requests. By default this targets
    /// Azure OpenAI (preserving the library's original zero-configuration
    /// behavior), but the active backend can be switched to any supported vendor
    /// via the <c>PROMPT_PROVIDER</c> environment variable. See
    /// <see cref="ProviderFactory"/> for the full list and the variables each
    /// provider reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <c>PROMPT_PROVIDER</c> unset (or set to <c>azure</c>), the following
    /// user-level environment variables are required, exactly as before:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>AZURE_OPENAI_API_URI</c> – Azure OpenAI endpoint URI</item>
    ///   <item><c>AZURE_OPENAI_API_KEY</c> – Azure OpenAI API key</item>
    ///   <item><c>AZURE_OPENAI_API_MODEL</c> – Deployed model name (e.g. gpt-4)</item>
    /// </list>
    /// <para>
    /// For an explicit, environment-free alternative, construct a provider
    /// directly (for example <see cref="AnthropicProvider"/> or an
    /// <see cref="OpenAICompatProvider"/> preset) and wrap it in
    /// <see cref="LlmClient"/>.
    /// </para>
    /// </remarks>
    public class Main
    {
        /// <summary>
        /// Returns the cached Azure <see cref="ChatClient"/>, creating it on first
        /// use. Retained for backward compatibility; the Azure plumbing now lives
        /// in <see cref="AzureOpenAIProvider"/>, to which this delegates.
        /// </summary>
        internal static ChatClient GetOrCreateChatClient(int maxRetries = 3)
            => AzureOpenAIProvider.GetOrCreateChatClient(maxRetries);

        /// <summary>
        /// Resets the cached client so the next call re-reads environment
        /// variables and applies a fresh retry policy. Useful when environment
        /// variables change at runtime or when a different <c>maxRetries</c> value
        /// is needed.
        /// </summary>
        public static void ResetClient()
        {
            AzureOpenAIProvider.ResetCache();
        }

        /// <summary>
        /// Validates the common parameters shared by <see cref="GetResponseAsync"/>
        /// and <see cref="GetResponseStreamAsync"/>, then builds the neutral
        /// message list and resolves the active <see cref="ILlmProvider"/> from
        /// the environment.
        /// </summary>
        private static (ILlmProvider Provider, List<ChatMsg> Messages, PromptOptions Options)
            PrepareRequest(string prompt, string? systemPrompt, int maxRetries, PromptOptions? options)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries),
                    maxRetries, "maxRetries must be non-negative.");

            var messages = new List<ChatMsg>(2);
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(ChatMsg.FromSystem(systemPrompt!));
            messages.Add(ChatMsg.FromUser(prompt));

            var provider = ProviderFactory.CreateFromEnvironment(maxRetries);
            var opts = options ?? new PromptOptions();

            return (provider, messages, opts);
        }

        /// <summary>
        /// Sends a prompt to the active provider and returns the response text.
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
            var (provider, messages, opts) =
                PrepareRequest(prompt, systemPrompt, maxRetries, options);

            return await provider.CompleteAsync(messages, opts, cancellationToken);
        }

        /// <summary>
        /// Sends a prompt to the active provider and streams the response as an
        /// <see cref="IAsyncEnumerable{StreamChunk}"/>. Each chunk contains
        /// incremental text and accumulated state, enabling real-time display.
        /// </summary>
        /// <param name="prompt">The user prompt to send.</param>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="maxRetries">Maximum retries for transient failures.</param>
        /// <param name="options">Optional model parameter overrides.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An async stream of <see cref="StreamChunk"/> instances.</returns>
        /// <exception cref="ArgumentException">Thrown when prompt is null or empty.</exception>
        public static async IAsyncEnumerable<StreamChunk> GetResponseStreamAsync(
            string prompt,
            string? systemPrompt = null,
            int maxRetries = 3,
            PromptOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var (provider, messages, opts) =
                PrepareRequest(prompt, systemPrompt, maxRetries, options);

            await foreach (StreamChunk chunk in
                provider.CompleteStreamAsync(messages, opts, cancellationToken))
            {
                yield return chunk;
            }
        }
    }
}
