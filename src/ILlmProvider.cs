namespace Prompt
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction over a chat-completion backend. Each supported vendor
    /// ships a concrete implementation (for example
    /// <see cref="AzureOpenAIProvider"/>, <see cref="OpenAICompatProvider"/>,
    /// <see cref="AnthropicProvider"/>, and <see cref="GeminiProvider"/>).
    /// </summary>
    /// <remarks>
    /// Implementations receive a vendor-neutral <see cref="ChatMsg"/> list and
    /// a <see cref="PromptOptions"/> instance, and are responsible for mapping
    /// those onto the vendor's request shape. The execution layer
    /// (<see cref="Main"/>, <see cref="Conversation"/>, and <see cref="LlmClient"/>)
    /// talks only to this interface.
    /// </remarks>
    public interface ILlmProvider
    {
        /// <summary>
        /// Sends the supplied messages and returns the assistant's response text,
        /// or <c>null</c> when the backend produced no content.
        /// </summary>
        /// <param name="messages">The conversation so far, in order.</param>
        /// <param name="options">Model parameter overrides (temperature, max tokens, etc.).</param>
        /// <param name="ct">Optional cancellation token.</param>
        Task<string?> CompleteAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            CancellationToken ct = default);

        /// <summary>
        /// Sends the supplied messages and streams the assistant's response as a
        /// sequence of <see cref="StreamChunk"/> values, each carrying the
        /// incremental delta and the accumulated text so far.
        /// </summary>
        /// <param name="messages">The conversation so far, in order.</param>
        /// <param name="options">Model parameter overrides.</param>
        /// <param name="ct">Optional cancellation token.</param>
        IAsyncEnumerable<StreamChunk> CompleteStreamAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken ct = default);
    }
}
