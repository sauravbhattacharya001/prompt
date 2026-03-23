namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Strategy for embedding canary tokens into prompts.
    /// </summary>
    public enum CanaryEmbedStrategy
    {
        /// <summary>Append the canary as a comment-style marker at the end.</summary>
        Append,

        /// <summary>Prepend the canary as a comment-style marker at the start.</summary>
        Prepend,

        /// <summary>Embed the canary using Unicode zero-width characters (invisible).</summary>
        ZeroWidth,

        /// <summary>Embed the canary as a bracketed instruction tag.</summary>
        InstructionTag
    }

    /// <summary>
    /// Represents a canary token that can be embedded in prompts to detect leakage.
    /// If a user or model reproduces the canary, you know the prompt was exposed.
    /// </summary>
    public class CanaryToken
    {
        /// <summary>Gets the unique identifier for this canary.</summary>
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        /// <summary>Gets the raw canary value (the secret).</summary>
        [JsonPropertyName("value")]
        public string Value { get; init; } = "";

        /// <summary>Gets the SHA-256 hash of the canary value for safe comparison.</summary>
        [JsonPropertyName("hash")]
        public string Hash { get; init; } = "";

        /// <summary>Gets when this canary was created.</summary>
        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>Gets the embedding strategy used.</summary>
        [JsonPropertyName("strategy")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CanaryEmbedStrategy Strategy { get; init; }

        /// <summary>Gets optional metadata tags (e.g., prompt name, environment).</summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; init; } = new();
    }

    /// <summary>
    /// Result of scanning text for canary tokens.
    /// </summary>
    public class CanaryScanResult
    {
        /// <summary>Gets whether any canary tokens were detected.</summary>
        [JsonPropertyName("detected")]
        public bool Detected { get; init; }

        /// <summary>Gets the list of matched canary IDs.</summary>
        [JsonPropertyName("matchedIds")]
        public List<string> MatchedIds { get; init; } = new();

        /// <summary>Gets the number of canaries found.</summary>
        [JsonPropertyName("matchCount")]
        public int MatchCount { get; init; }

        /// <summary>Gets the scanned text length.</summary>
        [JsonPropertyName("scannedLength")]
        public int ScannedLength { get; init; }
    }

    /// <summary>
    /// Embeds invisible canary tokens into prompts and scans outputs for leakage.
    /// Use this to detect if your system prompts are being extracted or reproduced.
    /// </summary>
    /// <example>
    /// <code>
    /// var canary = new PromptCanary();
    ///
    /// // Create and embed a canary
    /// var token = canary.CreateToken(metadata: new() { ["env"] = "prod", ["prompt"] = "system-v2" });
    /// string protectedPrompt = canary.Embed("You are a helpful assistant.", token);
    ///
    /// // Later, scan model output for leaked canaries
    /// var result = canary.Scan(modelOutput);
    /// if (result.Detected)
    ///     Console.WriteLine($"LEAK DETECTED! Canary IDs: {string.Join(", ", result.MatchedIds)}");
    /// </code>
    /// </example>
    public class PromptCanary
    {
        private readonly List<CanaryToken> _registry = new();
        private static readonly char[] ZeroWidthChars = { '\u200B', '\u200C', '\u200D', '\uFEFF' };

        /// <summary>
        /// Gets the current registry of canary tokens being tracked.
        /// </summary>
        public IReadOnlyList<CanaryToken> Registry => _registry.AsReadOnly();

        /// <summary>
        /// Creates a new canary token with a cryptographically random value.
        /// The token is automatically added to the internal registry for scanning.
        /// </summary>
        /// <param name="strategy">How to embed this canary into prompts.</param>
        /// <param name="metadata">Optional metadata to associate with this canary.</param>
        /// <returns>A new <see cref="CanaryToken"/>.</returns>
        public CanaryToken CreateToken(
            CanaryEmbedStrategy strategy = CanaryEmbedStrategy.ZeroWidth,
            Dictionary<string, string>? metadata = null)
        {
            var bytes = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            string value = "CNRY-" + Convert.ToHexString(bytes).ToLowerInvariant();

            string hash;
            using (var sha = SHA256.Create())
            {
                hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
            }

            var token = new CanaryToken
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Value = value,
                Hash = hash,
                CreatedAt = DateTimeOffset.UtcNow,
                Strategy = strategy,
                Metadata = metadata ?? new()
            };

            _registry.Add(token);
            return token;
        }

        /// <summary>
        /// Registers an externally-created canary token for scanning.
        /// </summary>
        /// <param name="token">The token to register.</param>
        public void Register(CanaryToken token)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (!_registry.Any(t => t.Id == token.Id))
                _registry.Add(token);
        }

        /// <summary>
        /// Embeds a canary token into a prompt using the token's configured strategy.
        /// </summary>
        /// <param name="prompt">The original prompt text.</param>
        /// <param name="token">The canary token to embed.</param>
        /// <returns>The prompt with the embedded canary.</returns>
        public string Embed(string prompt, CanaryToken token)
        {
            if (string.IsNullOrEmpty(prompt)) throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
            if (token == null) throw new ArgumentNullException(nameof(token));

            return token.Strategy switch
            {
                CanaryEmbedStrategy.Append => prompt + $"\n<!-- canary:{token.Value} -->",
                CanaryEmbedStrategy.Prepend => $"<!-- canary:{token.Value} -->\n" + prompt,
                CanaryEmbedStrategy.ZeroWidth => prompt + EncodeZeroWidth(token.Value),
                CanaryEmbedStrategy.InstructionTag => prompt + $"\n[SYSTEM_TRACE id=\"{token.Value}\"]",
                _ => prompt
            };
        }

        /// <summary>
        /// Removes all canary embeddings from a prompt, restoring the original text.
        /// Handles all strategy types.
        /// </summary>
        /// <param name="text">Text potentially containing canary embeddings.</param>
        /// <returns>Clean text with canaries removed.</returns>
        public string Strip(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove comment-style canaries
            text = Regex.Replace(text, @"\n?<!-- canary:CNRY-[0-9a-f]{32} -->", "");

            // Remove instruction tag canaries
            text = Regex.Replace(text, @"\n?\[SYSTEM_TRACE id=""CNRY-[0-9a-f]{32}""\]", "");

            // Remove zero-width characters
            foreach (var c in ZeroWidthChars)
                text = text.Replace(c.ToString(), "");

            return text;
        }

        /// <summary>
        /// Scans text (e.g., model output) for any registered canary tokens.
        /// Detects both raw value matches and zero-width encoded matches.
        /// </summary>
        /// <param name="text">The text to scan for leaked canaries.</param>
        /// <returns>A <see cref="CanaryScanResult"/> indicating whether leakage was detected.</returns>
        public CanaryScanResult Scan(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new CanaryScanResult { Detected = false, ScannedLength = 0 };

            var matched = new List<string>();

            foreach (var token in _registry)
            {
                // Check for raw value in text
                if (text.Contains(token.Value, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(token.Id);
                    continue;
                }

                // Check for zero-width encoded value
                string decoded = DecodeZeroWidth(text);
                if (!string.IsNullOrEmpty(decoded) && decoded.Contains(token.Value, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(token.Id);
                }
            }

            return new CanaryScanResult
            {
                Detected = matched.Count > 0,
                MatchedIds = matched,
                MatchCount = matched.Count,
                ScannedLength = text.Length
            };
        }

        /// <summary>
        /// Exports the canary registry as JSON for persistence or sharing with monitoring systems.
        /// Canary values are included — treat this as sensitive data.
        /// </summary>
        /// <returns>JSON string of all registered canary tokens.</returns>
        public string ExportRegistry()
        {
            return JsonSerializer.Serialize(_registry, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Imports canary tokens from a JSON registry export, adding them to the scanner.
        /// </summary>
        /// <param name="json">JSON array of canary tokens.</param>
        public void ImportRegistry(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("JSON cannot be empty.", nameof(json));
            var tokens = JsonSerializer.Deserialize<List<CanaryToken>>(json) ?? new();
            foreach (var token in tokens)
                Register(token);
        }

        /// <summary>
        /// Encodes a string into zero-width Unicode characters.
        /// Each byte is encoded as a sequence of 2-bit pairs mapped to zero-width chars.
        /// </summary>
        private static string EncodeZeroWidth(string input)
        {
            var sb = new StringBuilder();
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            foreach (byte b in bytes)
            {
                sb.Append(ZeroWidthChars[(b >> 6) & 0x03]);
                sb.Append(ZeroWidthChars[(b >> 4) & 0x03]);
                sb.Append(ZeroWidthChars[(b >> 2) & 0x03]);
                sb.Append(ZeroWidthChars[b & 0x03]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Decodes zero-width characters back into the original string.
        /// Returns empty string if no zero-width sequences are found.
        /// </summary>
        private static string DecodeZeroWidth(string input)
        {
            var zwChars = input.Where(c => ZeroWidthChars.Contains(c)).ToList();
            if (zwChars.Count < 4) return "";

            var bytes = new List<byte>();
            for (int i = 0; i + 3 < zwChars.Count; i += 4)
            {
                int b = (Array.IndexOf(ZeroWidthChars, zwChars[i]) << 6)
                       | (Array.IndexOf(ZeroWidthChars, zwChars[i + 1]) << 4)
                       | (Array.IndexOf(ZeroWidthChars, zwChars[i + 2]) << 2)
                       | Array.IndexOf(ZeroWidthChars, zwChars[i + 3]);
                bytes.Add((byte)b);
            }

            try { return Encoding.UTF8.GetString(bytes.ToArray()); }
            catch { return ""; }
        }
    }
}
