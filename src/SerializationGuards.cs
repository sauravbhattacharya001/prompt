namespace Prompt
{
    /// <summary>
    /// Shared security guards for JSON serialization/deserialization.
    /// Prevents denial-of-service via oversized or crafted payloads.
    /// </summary>
    internal static class SerializationGuards
    {
        /// <summary>
        /// Maximum allowed JSON payload size for deserialization to prevent
        /// denial-of-service via crafted large payloads.
        /// Default: 10 MB.
        /// </summary>
        internal const int MaxJsonPayloadBytes = 10 * 1024 * 1024;

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> if the JSON string
        /// exceeds <see cref="MaxJsonPayloadBytes"/> in UTF-8 encoded size.
        /// </summary>
        /// <param name="json">The JSON string to check.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the payload exceeds the maximum allowed size.
        /// </exception>
        internal static void ThrowIfPayloadTooLarge(string json)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxJsonPayloadBytes)
                throw new InvalidOperationException(
                    $"JSON payload exceeds the maximum allowed size of " +
                    $"{MaxJsonPayloadBytes / (1024 * 1024)} MB. " +
                    "This limit prevents denial-of-service from crafted large payloads.");
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> if the file
        /// exceeds <see cref="MaxJsonPayloadBytes"/> in size.
        /// </summary>
        /// <param name="filePath">Full path to the file.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the file exceeds the maximum allowed size.
        /// </exception>
        internal static void ThrowIfFileTooLarge(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxJsonPayloadBytes)
                throw new InvalidOperationException(
                    $"File '{filePath}' is {fileInfo.Length / (1024 * 1024)} MB, " +
                    $"exceeding the maximum allowed size of " +
                    $"{MaxJsonPayloadBytes / (1024 * 1024)} MB.");
        }
    }
}
