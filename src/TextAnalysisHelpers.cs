namespace Prompt
{
    /// <summary>
    /// Shared text analysis utilities. Currently exposes token-count estimation;
    /// this is the single home for that primitive so any prompt-analysis component
    /// can share one consistent implementation instead of re-deriving it.
    /// </summary>
    internal static class TextAnalysisHelpers
    {
        /// <summary>
        /// Estimates token count for a text string using the ~4 chars/token
        /// approximation common to GPT-family tokenizers.
        /// </summary>
        /// <param name="text">The text to estimate tokens for.</param>
        /// <returns>Estimated token count (0 for null/empty input).</returns>
        internal static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
