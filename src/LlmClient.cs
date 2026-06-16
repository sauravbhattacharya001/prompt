namespace Prompt
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Ergonomic wrapper around an <see cref="ILlmProvider"/>. Provides the same
    /// single-prompt convenience surface as <see cref="Main"/>
    /// (<see cref="GetResponseAsync"/> / <see cref="GetResponseStreamAsync"/>) but
    /// bound to an explicit provider instance — so callers can target any vendor
    /// without touching environment-variable configuration.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// var client = new LlmClient(new AnthropicProvider(key, "claude-3-5-sonnet-latest"));
    /// string? answer = await client.GetResponseAsync("Explain quicksort.");
    ///
    /// // Or wrap an OpenAI-compatible preset:
    /// var groq = new LlmClient(OpenAICompatProvider.Groq(key, "llama-3.1-70b-versatile"));
    /// await foreach (var chunk in groq.GetResponseStreamAsync("Stream a haiku."))
    ///     Console.Write(chunk.Delta);
    /// </code>
    /// </remarks>
    public sealed class LlmClient
    {
        private readonly ILlmProvider _provider;

        /// <summary>
        /// Creates a client over the supplied provider.
        /// </summary>
        /// <param name="provider">The backend to send requests to.</param>
        /// <exception cref="ArgumentNullException">When <paramref name="provider"/> is null.</exception>
        public LlmClient(ILlmProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Creates a client whose provider is selected from the environment via
        /// <see cref="ProviderFactory.CreateFromEnvironment"/> (Azure by default).
        /// </summary>
        public static LlmClient FromEnvironment(int maxRetries = 3)
            => new(ProviderFactory.CreateFromEnvironment(maxRetries));

        /// <summary>The underlying provider this client delegates to.</summary>
        public ILlmProvider Provider => _provider;

        /// <summary>
        /// Sends a single prompt (with an optional system prompt) and returns the
        /// response text, or <c>null</c> when no content was produced.
        /// </summary>
        /// <param name="prompt">The user prompt.</param>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="options">Optional model parameter overrides.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <exception cref="ArgumentException">When <paramref name="prompt"/> is null or empty.</exception>
        public Task<string?> GetResponseAsync(
            string prompt,
            string? systemPrompt = null,
            PromptOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var messages = BuildMessages(prompt, systemPrompt);
            return _provider.CompleteAsync(messages, options ?? new PromptOptions(), cancellationToken);
        }

        /// <summary>
        /// Sends a single prompt and streams the response as
        /// <see cref="StreamChunk"/> values.
        /// </summary>
        /// <param name="prompt">The user prompt.</param>
        /// <param name="systemPrompt">Optional system prompt.</param>
        /// <param name="options">Optional model parameter overrides.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <exception cref="ArgumentException">When <paramref name="prompt"/> is null or empty.</exception>
        public IAsyncEnumerable<StreamChunk> GetResponseStreamAsync(
            string prompt,
            string? systemPrompt = null,
            PromptOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messages = BuildMessages(prompt, systemPrompt);
            return _provider.CompleteStreamAsync(messages, options ?? new PromptOptions(), cancellationToken);
        }

        private static IReadOnlyList<ChatMsg> BuildMessages(string prompt, string? systemPrompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var messages = new List<ChatMsg>(2);
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(ChatMsg.FromSystem(systemPrompt!));
            messages.Add(ChatMsg.FromUser(prompt));
            return messages;
        }
    }
}
