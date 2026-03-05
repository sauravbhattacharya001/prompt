namespace Prompt
{
    /// <summary>
    /// Represents an incremental chunk from a streaming chat completion response.
    /// Each chunk contains the delta text from the current SSE event plus
    /// the accumulated full text so far.
    /// </summary>
    public class StreamChunk
    {
        /// <summary>
        /// Gets the incremental text content from this streaming event.
        /// May be empty for the final chunk that only carries completion metadata.
        /// </summary>
        public string Delta { get; init; } = "";

        /// <summary>
        /// Gets the full accumulated text from all chunks received so far,
        /// including this chunk's delta.
        /// </summary>
        public string FullText { get; init; } = "";

        /// <summary>
        /// Gets whether this is the final chunk in the stream.
        /// When true, <see cref="FullText"/> contains the complete response.
        /// </summary>
        public bool IsComplete { get; init; }

        /// <summary>
        /// Gets the finish reason from the model, if available.
        /// Common values: "stop" (natural completion), "length" (token limit hit).
        /// Only set on the final chunk.
        /// </summary>
        public string? FinishReason { get; init; }

        /// <summary>
        /// Gets the running count of output tokens consumed so far.
        /// This is an estimate based on the accumulated text length using
        /// the same heuristic as <see cref="PromptGuard"/>'s token estimator.
        /// Exact token counts are only available after the stream completes.
        /// </summary>
        public int TokensUsed { get; init; }
    }

    /// <summary>
    /// Represents a streaming update from a <see cref="PromptChain"/> execution.
    /// Wraps a <see cref="StreamChunk"/> with metadata about which step
    /// in the chain produced it.
    /// </summary>
    public class ChainStreamUpdate
    {
        /// <summary>
        /// Gets the name of the chain step that produced this update.
        /// </summary>
        public string StepName { get; init; } = "";

        /// <summary>
        /// Gets the output variable name for the step producing this update.
        /// </summary>
        public string OutputVariable { get; init; } = "";

        /// <summary>
        /// Gets the streaming chunk from the current step.
        /// </summary>
        public StreamChunk Chunk { get; init; } = null!;

        /// <summary>
        /// Gets whether the current step has finished streaming.
        /// When true, the chain will proceed to the next step (if any).
        /// </summary>
        public bool IsStepComplete { get; init; }

        /// <summary>
        /// Gets whether this is the final update from the entire chain
        /// (last chunk of the last step).
        /// </summary>
        public bool IsChainComplete { get; init; }
    }
}
