namespace Prompt
{
    /// <summary>
    /// Represents a single chunk in a streaming response from Azure OpenAI.
    /// Each chunk contains incremental text (delta) and accumulated state.
    /// </summary>
    public class StreamChunk
    {
        /// <summary>
        /// The incremental text received in this chunk.
        /// </summary>
        public string Delta { get; init; } = "";

        /// <summary>
        /// The full accumulated text from all chunks received so far.
        /// </summary>
        public string FullText { get; init; } = "";

        /// <summary>
        /// Whether this is the final chunk in the stream.
        /// </summary>
        public bool IsComplete { get; init; }

        /// <summary>
        /// The reason the model stopped generating, if available
        /// (e.g., "stop", "length"). Only set on the final chunk.
        /// </summary>
        public string? FinishReason { get; init; }

        /// <summary>
        /// Running count of tokens used so far (approximated from
        /// accumulated text length when exact count is unavailable).
        /// </summary>
        public int TokensUsed { get; init; }
    }
}
