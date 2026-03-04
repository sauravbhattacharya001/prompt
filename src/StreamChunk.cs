namespace Prompt
{
    /// <summary>
    /// Represents a single chunk from a streaming chat completion response.
    /// </summary>
    public class StreamChunk
    {
        /// <summary>Incremental text received in this chunk.</summary>
        public string Delta { get; init; } = string.Empty;

        /// <summary>Accumulated text from all chunks received so far.</summary>
        public string FullText { get; init; } = string.Empty;

        /// <summary>True when this is the final chunk in the stream.</summary>
        public bool IsComplete { get; init; }

        /// <summary>Finish reason from the model (e.g. "stop", "length").</summary>
        public string? FinishReason { get; init; }

        /// <summary>Running count of tokens used (estimated from accumulated text length / 4).</summary>
        public int TokensUsed { get; init; }
    }
}
