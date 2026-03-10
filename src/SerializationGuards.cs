namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Shared security guards and JSON configuration for serialization.
    /// Prevents denial-of-service via oversized payloads and provides
    /// reusable <see cref="JsonSerializerOptions"/> instances to avoid
    /// repeated allocations and enable internal metadata caching.
    /// </summary>
    internal static class SerializationGuards
    {
        /// <summary>
        /// Maximum allowed JSON payload size for deserialization to prevent
        /// denial-of-service via crafted large payloads.
        /// Default: 10 MB.
        /// </summary>
        internal const int MaxJsonPayloadBytes = 10 * 1024 * 1024;

        // ── Shared JsonSerializerOptions ─────────────────────────────

        /// <summary>
        /// Read/deserialize options: camelCase property names.
        /// </summary>
        internal static readonly JsonSerializerOptions ReadCamelCase = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Write/serialize options: indented, skip null properties (no camelCase).
        /// Used by components that serialize with PascalCase property names.
        /// </summary>
        internal static readonly JsonSerializerOptions WriteIndentedSkipNull = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Write/serialize options: indented only (no camelCase, no null skipping).
        /// </summary>
        internal static readonly JsonSerializerOptions WriteIndented = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Write/serialize options: indented, camelCase, skip null properties.
        /// </summary>
        internal static readonly JsonSerializerOptions WriteCamelCase = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Write/serialize options: compact (not indented), camelCase, skip null properties.
        /// </summary>
        internal static readonly JsonSerializerOptions WriteCompactCamelCase = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Returns the appropriate write options based on the <paramref name="indented"/> flag.
        /// </summary>
        internal static JsonSerializerOptions WriteOptions(bool indented) =>
            indented ? WriteCamelCase : WriteCompactCamelCase;

        /// <summary>
        /// Read options with camelCase and string enum conversion.
        /// </summary>
        internal static readonly JsonSerializerOptions ReadWithEnums = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Write options: indented with string enum conversion.
        /// </summary>
        internal static readonly JsonSerializerOptions WriteWithEnums = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // ── Payload guards ───────────────────────────────────────────

        /// <summary>
        /// Validates a JSON input string: checks for null/empty and oversized payloads.
        /// Combines the null check and size guard that most FromJson methods need.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <param name="paramName">Parameter name for the exception (default: "json").</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="json"/> is null or whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the payload exceeds <see cref="MaxJsonPayloadBytes"/>.
        /// </exception>
        internal static void ValidateJsonInput(string json, string paramName = "json")
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string cannot be null or empty.", paramName);
            ThrowIfPayloadTooLarge(json);
        }

        /// <summary>
        /// Deserializes JSON with input validation and size guard.
        /// Combines null check, payload size check, and deserialization.
        /// </summary>
        /// <typeparam name="T">Target type to deserialize into.</typeparam>
        /// <param name="json">JSON string to deserialize.</param>
        /// <param name="options">JSON serializer options (null uses defaults).</param>
        /// <param name="paramName">Parameter name for exceptions (default: "json").</param>
        /// <returns>The deserialized object.</returns>
        /// <exception cref="ArgumentException">If JSON is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">If payload too large or deserialization returns null.</exception>
        internal static T SafeDeserialize<T>(string json, JsonSerializerOptions? options = null,
            string paramName = "json") where T : class
        {
            ValidateJsonInput(json, paramName);
            return JsonSerializer.Deserialize<T>(json, options ?? ReadCamelCase)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize JSON to {typeof(T).Name}.");
        }

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
