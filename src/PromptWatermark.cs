namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies the watermarking strategy used to embed data into prompt text.
    /// </summary>
    public enum WatermarkStrategy
    {
        /// <summary>Embed data using zero-width Unicode characters (invisible in most renderers).</summary>
        ZeroWidth,

        /// <summary>Embed data using homoglyph substitutions (visually identical characters).</summary>
        Homoglyph,

        /// <summary>Embed data using whitespace variations (trailing spaces, tab/space mixing).</summary>
        Whitespace
    }

    /// <summary>
    /// Represents extracted watermark data from a prompt.
    /// </summary>
    public class WatermarkPayload
    {
        /// <summary>The decoded payload string.</summary>
        public string Data { get; set; } = "";

        /// <summary>The strategy that was used to embed this watermark.</summary>
        public WatermarkStrategy Strategy { get; set; }

        /// <summary>Whether the integrity check (HMAC) passed, if a key was provided.</summary>
        public bool IntegrityValid { get; set; } = true;

        /// <summary>Position in the text where the watermark was found.</summary>
        public int Position { get; set; }
    }

    /// <summary>
    /// Embeds invisible watermarks into prompt text for version tracking,
    /// A/B test attribution, and prompt leak detection.
    /// <para>
    /// Supports three strategies: zero-width Unicode characters, homoglyph
    /// substitution, and whitespace variation. Watermarks can optionally be
    /// HMAC-signed to detect tampering.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var wm = new PromptWatermark();
    /// string marked = wm.Embed("Hello world", "v2.1-expA");
    /// var payload = wm.Extract(marked);
    /// Console.WriteLine(payload?.Data); // "v2.1-expA"
    /// </code>
    /// </example>
    public class PromptWatermark
    {
        // Zero-width characters used for binary encoding
        private const char ZeroWidthSpace = '\u200B';       // represents 0
        private const char ZeroWidthNonJoiner = '\u200C';   // represents 1
        private const char ZeroWidthJoiner = '\u200D';      // separator/delimiter
        private const char WordJoiner = '\u2060';           // start/end marker

        // Common homoglyph pairs (ASCII -> visually similar Unicode)
        private static readonly Dictionary<char, char> HomoglyphMap = new()
        {
            { 'a', '\u0430' }, // Cyrillic а
            { 'c', '\u0441' }, // Cyrillic с
            { 'e', '\u0435' }, // Cyrillic е
            { 'o', '\u043E' }, // Cyrillic о
            { 'p', '\u0440' }, // Cyrillic р
            { 's', '\u0455' }, // Cyrillic ѕ
            { 'x', '\u0445' }, // Cyrillic х
            { 'y', '\u0443' }, // Cyrillic у
        };

        private static readonly Dictionary<char, char> ReverseHomoglyphMap =
            HomoglyphMap.ToDictionary(kv => kv.Value, kv => kv.Key);

        private readonly WatermarkStrategy _strategy;
        private readonly string? _hmacKey;

        /// <summary>
        /// Creates a new PromptWatermark instance.
        /// </summary>
        /// <param name="strategy">The watermarking strategy to use.</param>
        /// <param name="hmacKey">Optional HMAC key for integrity verification. If provided,
        /// a truncated HMAC is appended to the payload before embedding.</param>
        public PromptWatermark(WatermarkStrategy strategy = WatermarkStrategy.ZeroWidth, string? hmacKey = null)
        {
            _strategy = strategy;
            _hmacKey = hmacKey;
        }

        /// <summary>
        /// Embeds a watermark payload into the given text.
        /// </summary>
        /// <param name="text">The prompt text to watermark.</param>
        /// <param name="data">The payload to embed (e.g., version ID, variant name, timestamp).</param>
        /// <param name="position">Where to insert the watermark. -1 (default) inserts after the first space.
        /// 0 inserts at the beginning. Any positive value inserts at that character index.</param>
        /// <returns>The watermarked text.</returns>
        /// <exception cref="ArgumentException">Thrown when text or data is null/empty.</exception>
        public string Embed(string text, string data, int position = -1)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Data cannot be null or empty.", nameof(data));

            string payload = _hmacKey != null ? data + "|" + ComputeHmac(data) : data;

            return _strategy switch
            {
                WatermarkStrategy.ZeroWidth => EmbedZeroWidth(text, payload, position),
                WatermarkStrategy.Homoglyph => EmbedHomoglyph(text, payload),
                WatermarkStrategy.Whitespace => EmbedWhitespace(text, payload, position),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        /// <summary>
        /// Attempts to extract a watermark payload from the given text.
        /// </summary>
        /// <param name="text">The potentially watermarked text.</param>
        /// <returns>The extracted payload, or null if no watermark was found.</returns>
        public WatermarkPayload? Extract(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            return _strategy switch
            {
                WatermarkStrategy.ZeroWidth => ExtractZeroWidth(text),
                WatermarkStrategy.Homoglyph => ExtractHomoglyph(text),
                WatermarkStrategy.Whitespace => ExtractWhitespace(text),
                _ => null
            };
        }

        /// <summary>
        /// Removes any watermark from the text, returning clean text.
        /// </summary>
        /// <param name="text">The watermarked text.</param>
        /// <returns>Clean text with watermark removed.</returns>
        public string Strip(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return _strategy switch
            {
                WatermarkStrategy.ZeroWidth => StripZeroWidth(text),
                WatermarkStrategy.Homoglyph => StripHomoglyph(text),
                WatermarkStrategy.Whitespace => StripWhitespace(text),
                _ => text
            };
        }

        /// <summary>
        /// Checks whether the given text contains a watermark.
        /// </summary>
        /// <param name="text">Text to check.</param>
        /// <returns>True if a watermark is detected.</returns>
        public bool Contains(string text)
        {
            return Extract(text) != null;
        }

        /// <summary>
        /// Embeds structured metadata as a JSON watermark.
        /// </summary>
        /// <param name="text">The prompt text.</param>
        /// <param name="metadata">Dictionary of key-value pairs to embed.</param>
        /// <param name="position">Insertion position (-1 for auto).</param>
        /// <returns>Watermarked text.</returns>
        public string EmbedMetadata(string text, Dictionary<string, string> metadata, int position = -1)
        {
            if (metadata == null || metadata.Count == 0)
                throw new ArgumentException("Metadata cannot be null or empty.", nameof(metadata));

            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = false });
            return Embed(text, json, position);
        }

        /// <summary>
        /// Extracts structured metadata from a watermarked text.
        /// </summary>
        /// <param name="text">The watermarked text.</param>
        /// <returns>Dictionary of extracted metadata, or null if not found or not valid JSON.</returns>
        public Dictionary<string, string>? ExtractMetadata(string text)
        {
            var payload = Extract(text);
            if (payload == null) return null;

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(payload.Data);
            }
            catch
            {
                return null;
            }
        }

        // ── Zero-Width Strategy ──────────────────────────────────────────

        private string EmbedZeroWidth(string text, string payload, int position)
        {
            string encoded = EncodeToZeroWidth(payload);
            string watermark = $"{WordJoiner}{encoded}{WordJoiner}";

            int insertAt = ResolvePosition(text, position);
            return text.Insert(insertAt, watermark);
        }

        private WatermarkPayload? ExtractZeroWidth(string text)
        {
            int start = text.IndexOf(WordJoiner);
            if (start < 0) return null;

            int end = text.IndexOf(WordJoiner, start + 1);
            if (end < 0) return null;

            string encoded = text.Substring(start + 1, end - start - 1);
            string decoded = DecodeFromZeroWidth(encoded);

            return BuildPayload(decoded, WatermarkStrategy.ZeroWidth, start);
        }

        private string StripZeroWidth(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool inWatermark = false;
            foreach (char c in text)
            {
                if (c == WordJoiner)
                {
                    inWatermark = !inWatermark;
                    continue;
                }
                if (inWatermark) continue;
                if (c != ZeroWidthSpace && c != ZeroWidthNonJoiner && c != ZeroWidthJoiner)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static string EncodeToZeroWidth(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(ZeroWidthJoiner);
                for (int bit = 7; bit >= 0; bit--)
                {
                    sb.Append((bytes[i] & (1 << bit)) != 0 ? ZeroWidthNonJoiner : ZeroWidthSpace);
                }
            }
            return sb.ToString();
        }

        private static string DecodeFromZeroWidth(string encoded)
        {
            string[] byteParts = encoded.Split(ZeroWidthJoiner);
            var bytes = new List<byte>();
            foreach (string part in byteParts)
            {
                if (part.Length < 8) continue;
                byte b = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (part[i] == ZeroWidthNonJoiner)
                        b |= (byte)(1 << (7 - i));
                }
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        // ── Homoglyph Strategy ───────────────────────────────────────────

        private string EmbedHomoglyph(string text, string payload)
        {
            // Encode payload as bits, then substitute homoglyphs at eligible positions
            // 1 = substitute, 0 = keep original
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            var bits = new List<bool>();
            // Length prefix (2 bytes, big-endian)
            bits.AddRange(ByteToBits((byte)(payloadBytes.Length >> 8)));
            bits.AddRange(ByteToBits((byte)(payloadBytes.Length & 0xFF)));
            foreach (byte b in payloadBytes)
                bits.AddRange(ByteToBits(b));

            var sb = new StringBuilder(text.Length);
            int bitIndex = 0;
            foreach (char c in text)
            {
                char lower = char.ToLowerInvariant(c);
                if (bitIndex < bits.Count && HomoglyphMap.ContainsKey(lower) && c == lower)
                {
                    sb.Append(bits[bitIndex] ? HomoglyphMap[lower] : c);
                    bitIndex++;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private WatermarkPayload? ExtractHomoglyph(string text)
        {
            var bits = new List<bool>();
            foreach (char c in text)
            {
                if (ReverseHomoglyphMap.ContainsKey(c))
                    bits.Add(true);
                else if (HomoglyphMap.ContainsKey(char.ToLowerInvariant(c)) && c == char.ToLowerInvariant(c))
                    bits.Add(false);
            }

            if (bits.Count < 16) return null; // Need at least length prefix

            int length = (BitsToByteValue(bits, 0) << 8) | BitsToByteValue(bits, 8);
            if (length <= 0 || bits.Count < 16 + length * 8) return null;

            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
                bytes[i] = (byte)BitsToByteValue(bits, 16 + i * 8);

            string decoded = Encoding.UTF8.GetString(bytes);
            return BuildPayload(decoded, WatermarkStrategy.Homoglyph, 0);
        }

        private string StripHomoglyph(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (ReverseHomoglyphMap.TryGetValue(c, out char original))
                    sb.Append(original);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Whitespace Strategy ──────────────────────────────────────────

        private string EmbedWhitespace(string text, string payload, int position)
        {
            // Encode each byte as a line of trailing spaces (space count = byte value)
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            var lines = text.Split('\n');

            // Find insertion point (line-based)
            int insertLine = position < 0 ? lines.Length / 2 : Math.Min(position, lines.Length);

            var result = new List<string>();
            for (int i = 0; i < insertLine && i < lines.Length; i++)
                result.Add(lines[i]);

            // Add watermark lines (each byte encoded as trailing spaces after a zero-width joiner marker)
            foreach (byte b in bytes)
                result.Add(WordJoiner + new string(' ', b + 1));

            for (int i = insertLine; i < lines.Length; i++)
                result.Add(lines[i]);

            return string.Join('\n', result);
        }

        private WatermarkPayload? ExtractWhitespace(string text)
        {
            var lines = text.Split('\n');
            var bytes = new List<byte>();
            int pos = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0 && lines[i][0] == WordJoiner)
                {
                    if (pos < 0) pos = i;
                    int spaceCount = lines[i].Length - 1; // subtract the marker
                    if (spaceCount > 0)
                        bytes.Add((byte)(spaceCount - 1));
                }
            }

            if (bytes.Count == 0) return null;

            string decoded = Encoding.UTF8.GetString(bytes.ToArray());
            return BuildPayload(decoded, WatermarkStrategy.Whitespace, pos);
        }

        private string StripWhitespace(string text)
        {
            var lines = text.Split('\n');
            var clean = lines.Where(l => l.Length == 0 || l[0] != WordJoiner).ToArray();
            return string.Join('\n', clean);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private int ResolvePosition(string text, int position)
        {
            if (position >= 0) return Math.Min(position, text.Length);
            int firstSpace = text.IndexOf(' ');
            return firstSpace >= 0 ? firstSpace + 1 : text.Length;
        }

        private WatermarkPayload BuildPayload(string decoded, WatermarkStrategy strategy, int position)
        {
            bool valid = true;
            string data = decoded;

            if (_hmacKey != null && decoded.Contains('|'))
            {
                int lastPipe = decoded.LastIndexOf('|');
                data = decoded[..lastPipe];
                string expectedHmac = decoded[(lastPipe + 1)..];
                valid = ComputeHmac(data) == expectedHmac;
            }

            return new WatermarkPayload
            {
                Data = data,
                Strategy = strategy,
                IntegrityValid = valid,
                Position = position
            };
        }

        private string ComputeHmac(string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacKey!));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash)[..8]; // Truncated for compactness
        }

        private static IEnumerable<bool> ByteToBits(byte b)
        {
            for (int i = 7; i >= 0; i--)
                yield return (b & (1 << i)) != 0;
        }

        private static int BitsToByteValue(List<bool> bits, int offset)
        {
            int val = 0;
            for (int i = 0; i < 8 && offset + i < bits.Count; i++)
            {
                if (bits[offset + i])
                    val |= (1 << (7 - i));
            }
            return val;
        }
    }
}
