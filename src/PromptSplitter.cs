namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Strategy for splitting content into chunks.
    /// </summary>
    public enum SplitStrategy
    {
        /// <summary>Split on paragraph boundaries (double newlines).</summary>
        Paragraph,
        /// <summary>Split on sentence boundaries.</summary>
        Sentence,
        /// <summary>Split on any newline boundary.</summary>
        Line,
        /// <summary>Split at exact token count (may break mid-word).</summary>
        Fixed,
        /// <summary>Split on markdown heading boundaries (# through ######).</summary>
        Heading,
        /// <summary>Split on code block boundaries (``` fences).</summary>
        CodeBlock
    }

    /// <summary>
    /// A single chunk produced by the splitter.
    /// </summary>
    public class PromptChunk
    {
        /// <summary>Zero-based index of this chunk in the sequence.</summary>
        [JsonPropertyName("index")]
        public int Index { get; init; }

        /// <summary>Total number of chunks in the split.</summary>
        [JsonPropertyName("totalChunks")]
        public int TotalChunks { get; set; }

        /// <summary>The chunk text content.</summary>
        [JsonPropertyName("content")]
        public string Content { get; init; } = "";

        /// <summary>Estimated token count for this chunk.</summary>
        [JsonPropertyName("estimatedTokens")]
        public int EstimatedTokens { get; init; }

        /// <summary>Character offset in the original text where this chunk starts.</summary>
        [JsonPropertyName("startOffset")]
        public int StartOffset { get; init; }

        /// <summary>Character offset in the original text where this chunk ends (exclusive).</summary>
        [JsonPropertyName("endOffset")]
        public int EndOffset { get; init; }

        /// <summary>Overlap text prepended from the previous chunk (empty for the first chunk).</summary>
        [JsonPropertyName("overlapText")]
        public string OverlapText { get; init; } = "";

        /// <summary>Whether this is the first chunk.</summary>
        [JsonPropertyName("isFirst")]
        public bool IsFirst => Index == 0;

        /// <summary>Whether this is the last chunk.</summary>
        [JsonPropertyName("isLast")]
        public bool IsLast => Index == TotalChunks - 1;

        /// <summary>Returns a formatted header like "[Part 1/3]".</summary>
        public string Header => $"[Part {Index + 1}/{TotalChunks}]";
    }

    /// <summary>
    /// Configuration for the prompt splitter.
    /// </summary>
    public class SplitterConfig
    {
        /// <summary>Maximum estimated tokens per chunk (default 3000).</summary>
        [JsonPropertyName("maxTokensPerChunk")]
        public int MaxTokensPerChunk { get; init; } = 3000;

        /// <summary>Number of overlap tokens to prepend from the previous chunk (default 100).</summary>
        [JsonPropertyName("overlapTokens")]
        public int OverlapTokens { get; init; } = 100;

        /// <summary>Strategy for finding split boundaries (default Paragraph).</summary>
        [JsonPropertyName("strategy")]
        public SplitStrategy Strategy { get; init; } = SplitStrategy.Paragraph;

        /// <summary>Whether to add "[Part N/M]" headers to each chunk (default false).</summary>
        [JsonPropertyName("addHeaders")]
        public bool AddHeaders { get; init; }

        /// <summary>Optional system prompt prepended to every chunk (not counted toward maxTokens).</summary>
        [JsonPropertyName("systemPrompt")]
        public string? SystemPrompt { get; init; }

        /// <summary>Optional continuation instruction appended to non-final chunks.</summary>
        [JsonPropertyName("continuationNote")]
        public string? ContinuationNote { get; init; }

        /// <summary>Characters per token estimate (default 4.0).</summary>
        [JsonPropertyName("charsPerToken")]
        public double CharsPerToken { get; init; } = 4.0;

        /// <summary>Whether to preserve code blocks intact when possible (default true).</summary>
        [JsonPropertyName("preserveCodeBlocks")]
        public bool PreserveCodeBlocks { get; init; } = true;
    }

    /// <summary>
    /// Result of a split operation, containing all chunks and metadata.
    /// </summary>
    public class SplitResult
    {
        /// <summary>The produced chunks.</summary>
        [JsonPropertyName("chunks")]
        public List<PromptChunk> Chunks { get; init; } = new();

        /// <summary>Total estimated tokens across all chunks (excluding overlap).</summary>
        [JsonPropertyName("totalTokens")]
        public int TotalTokens { get; init; }

        /// <summary>The strategy used.</summary>
        [JsonPropertyName("strategy")]
        public SplitStrategy Strategy { get; init; }

        /// <summary>Original text length in characters.</summary>
        [JsonPropertyName("originalLength")]
        public int OriginalLength { get; init; }

        /// <summary>Whether the content needed splitting at all.</summary>
        [JsonPropertyName("wasSplit")]
        public bool WasSplit => Chunks.Count > 1;
    }

    /// <summary>
    /// Splits long prompts into boundary-aware chunks that fit within token limits.
    /// Supports paragraph, sentence, line, heading, code block, and fixed-size strategies.
    /// Configurable overlap ensures continuity across chunks.
    /// </summary>
    /// <remarks>
    /// <para>Usage:</para>
    /// <code>
    /// var splitter = new PromptSplitter(new SplitterConfig
    /// {
    ///     MaxTokensPerChunk = 4000,
    ///     OverlapTokens = 200,
    ///     Strategy = SplitStrategy.Paragraph,
    ///     AddHeaders = true
    /// });
    ///
    /// SplitResult result = splitter.Split(longText);
    /// foreach (var chunk in result.Chunks)
    /// {
    ///     Console.WriteLine($"{chunk.Header}: {chunk.EstimatedTokens} tokens");
    /// }
    /// </code>
    /// </remarks>
    public class PromptSplitter
    {
        private readonly SplitterConfig _config;
        private readonly int _maxCharsPerChunk;
        private readonly int _overlapChars;

        // Sentence boundary regex: period/question/exclamation followed by whitespace or end
        private static readonly Regex SentenceEnd = new(
            @"(?<=[.!?])\s+",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        // Markdown heading pattern
        private static readonly Regex HeadingPattern = new(
            @"^#{1,6}\s",
            RegexOptions.Multiline | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        // Code fence pattern
        private static readonly Regex CodeFencePattern = new(
            @"^```",
            RegexOptions.Multiline | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Creates a new PromptSplitter with the given configuration.
        /// </summary>
        /// <param name="config">Splitter configuration. Uses defaults if null.</param>
        /// <exception cref="ArgumentException">Thrown if config values are invalid.</exception>
        public PromptSplitter(SplitterConfig? config = null)
        {
            _config = config ?? new SplitterConfig();

            if (_config.MaxTokensPerChunk < 10)
                throw new ArgumentException("MaxTokensPerChunk must be at least 10.");
            if (_config.OverlapTokens < 0)
                throw new ArgumentException("OverlapTokens cannot be negative.");
            if (_config.OverlapTokens >= _config.MaxTokensPerChunk)
                throw new ArgumentException("OverlapTokens must be less than MaxTokensPerChunk.");
            if (_config.CharsPerToken <= 0)
                throw new ArgumentException("CharsPerToken must be positive.");

            _maxCharsPerChunk = (int)(_config.MaxTokensPerChunk * _config.CharsPerToken);
            _overlapChars = (int)(_config.OverlapTokens * _config.CharsPerToken);
        }

        /// <summary>
        /// Estimates the token count for a given text.
        /// </summary>
        /// <param name="text">Text to estimate.</param>
        /// <returns>Estimated token count.</returns>
        public int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / _config.CharsPerToken);
        }

        /// <summary>
        /// Splits the input text into chunks according to the configured strategy.
        /// </summary>
        /// <param name="text">Text to split.</param>
        /// <returns>A <see cref="SplitResult"/> containing all chunks and metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown if text is null.</exception>
        public SplitResult Split(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            int totalTokens = EstimateTokens(text);

            // No splitting needed
            if (totalTokens <= _config.MaxTokensPerChunk)
            {
                var singleChunk = new PromptChunk
                {
                    Index = 0,
                    TotalChunks = 1,
                    Content = _applyDecorations(text, 0, 1, ""),
                    EstimatedTokens = totalTokens,
                    StartOffset = 0,
                    EndOffset = text.Length
                };

                return new SplitResult
                {
                    Chunks = new List<PromptChunk> { singleChunk },
                    TotalTokens = totalTokens,
                    Strategy = _config.Strategy,
                    OriginalLength = text.Length
                };
            }

            // Find segments based on strategy
            var segments = _config.Strategy switch
            {
                SplitStrategy.Paragraph => _splitByParagraph(text),
                SplitStrategy.Sentence => _splitBySentence(text),
                SplitStrategy.Line => _splitByLine(text),
                SplitStrategy.Fixed => _splitFixed(text),
                SplitStrategy.Heading => _splitByHeading(text),
                SplitStrategy.CodeBlock => _splitByCodeBlock(text),
                _ => _splitByParagraph(text)
            };

            // Merge segments into chunks that fit within token limits
            var chunks = _mergeSegments(segments, text);

            return new SplitResult
            {
                Chunks = chunks,
                TotalTokens = totalTokens,
                Strategy = _config.Strategy,
                OriginalLength = text.Length
            };
        }

        /// <summary>
        /// Splits text and returns just the content strings (convenience method).
        /// </summary>
        /// <param name="text">Text to split.</param>
        /// <returns>List of chunk content strings.</returns>
        public List<string> SplitToStrings(string text)
        {
            return Split(text).Chunks.Select(c => c.Content).ToList();
        }

        /// <summary>
        /// Reassembles chunks back into the original text, removing overlap and headers.
        /// </summary>
        /// <param name="chunks">Chunks to reassemble.</param>
        /// <returns>Reconstructed text (may differ slightly from original due to whitespace normalization).</returns>
        public string Reassemble(IEnumerable<PromptChunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));

            var ordered = chunks.OrderBy(c => c.Index).ToList();
            if (ordered.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var chunk in ordered)
            {
                // Strip decorations
                string content = chunk.Content;

                // Remove system prompt if present
                if (_config.SystemPrompt != null && content.StartsWith(_config.SystemPrompt))
                {
                    content = content.Substring(_config.SystemPrompt.Length).TrimStart('\n');
                }

                // Remove header if present
                if (_config.AddHeaders)
                {
                    string header = $"[Part {chunk.Index + 1}/{chunk.TotalChunks}]\n";
                    if (content.StartsWith(header))
                        content = content.Substring(header.Length);
                }

                // Remove continuation note if present
                if (_config.ContinuationNote != null && content.EndsWith(_config.ContinuationNote))
                {
                    content = content.Substring(0, content.Length - _config.ContinuationNote.Length).TrimEnd('\n');
                }

                // Skip overlap portion (except for first chunk)
                if (chunk.Index > 0 && chunk.OverlapText.Length > 0)
                {
                    int overlapEnd = content.IndexOf(chunk.OverlapText, StringComparison.Ordinal);
                    if (overlapEnd == 0)
                    {
                        content = content.Substring(chunk.OverlapText.Length);
                    }
                }

                sb.Append(content);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serializes the split result to JSON.
        /// </summary>
        public string ToJson(SplitResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        // ── Segment splitters ───────────────────────────────────────────

        private List<(int start, int end)> _splitByParagraph(string text)
        {
            var segments = new List<(int, int)>();
            int pos = 0;

            while (pos < text.Length)
            {
                int nextBreak = text.IndexOf("\n\n", pos, StringComparison.Ordinal);
                if (nextBreak < 0)
                {
                    segments.Add((pos, text.Length));
                    break;
                }

                // Include up to end of double-newline
                int end = nextBreak + 2;
                segments.Add((pos, end));
                pos = end;
            }

            return segments;
        }

        private List<(int start, int end)> _splitBySentence(string text)
        {
            var segments = new List<(int, int)>();
            var matches = SentenceEnd.Matches(text);

            int pos = 0;
            foreach (Match m in matches)
            {
                int end = m.Index + m.Length;
                if (end > pos)
                {
                    segments.Add((pos, end));
                    pos = end;
                }
            }

            // Remainder
            if (pos < text.Length)
            {
                segments.Add((pos, text.Length));
            }

            return segments;
        }

        private List<(int start, int end)> _splitByLine(string text)
        {
            var segments = new List<(int, int)>();
            int pos = 0;

            while (pos < text.Length)
            {
                int nextNl = text.IndexOf('\n', pos);
                if (nextNl < 0)
                {
                    segments.Add((pos, text.Length));
                    break;
                }

                segments.Add((pos, nextNl + 1));
                pos = nextNl + 1;
            }

            return segments;
        }

        private List<(int start, int end)> _splitFixed(string text)
        {
            var segments = new List<(int, int)>();
            int pos = 0;

            while (pos < text.Length)
            {
                int end = Math.Min(pos + _maxCharsPerChunk, text.Length);
                segments.Add((pos, end));
                pos = end;
            }

            return segments;
        }

        private List<(int start, int end)> _splitByHeading(string text)
        {
            var matches = HeadingPattern.Matches(text);
            if (matches.Count == 0)
            {
                // Fall back to paragraph splitting
                return _splitByParagraph(text);
            }

            var segments = new List<(int, int)>();
            int pos = 0;

            foreach (Match m in matches)
            {
                if (m.Index > pos)
                {
                    segments.Add((pos, m.Index));
                }
                pos = m.Index;
            }

            // Remainder
            if (pos < text.Length)
            {
                segments.Add((pos, text.Length));
            }

            return segments;
        }

        private List<(int start, int end)> _splitByCodeBlock(string text)
        {
            var matches = CodeFencePattern.Matches(text);
            if (matches.Count < 2)
            {
                return _splitByParagraph(text);
            }

            var segments = new List<(int, int)>();
            int pos = 0;
            bool inCode = false;
            int codeStart = 0;

            foreach (Match m in matches)
            {
                if (!inCode)
                {
                    // Text before code block
                    if (m.Index > pos)
                    {
                        segments.Add((pos, m.Index));
                    }
                    codeStart = m.Index;
                    pos = m.Index; // Track that we've consumed up to this point
                    inCode = true;
                }
                else
                {
                    // End of code block — find end of this line
                    int lineEnd = text.IndexOf('\n', m.Index);
                    int end = lineEnd < 0 ? text.Length : lineEnd + 1;
                    segments.Add((codeStart, end));
                    pos = end;
                    inCode = false;
                }
            }

            // Remainder (unclosed code block or trailing text)
            if (pos < text.Length)
            {
                segments.Add((pos, text.Length));
            }

            return segments;
        }

        // ── Segment merging ─────────────────────────────────────────────

        private List<PromptChunk> _mergeSegments(List<(int start, int end)> segments, string text)
        {
            var chunks = new List<PromptChunk>();
            var currentSegments = new List<(int start, int end)>();
            int currentChars = 0;

            foreach (var seg in segments)
            {
                int segLen = seg.end - seg.start;

                // If a single segment exceeds max, split it further
                if (segLen > _maxCharsPerChunk && currentSegments.Count == 0)
                {
                    var subSegments = _forceSubSplit(text, seg.start, seg.end);
                    foreach (var sub in subSegments)
                    {
                        currentSegments.Add(sub);
                        currentChars += sub.end - sub.start;

                        if (currentChars >= _maxCharsPerChunk)
                        {
                            _flushChunk(chunks, currentSegments, text);
                            currentSegments.Clear();
                            currentChars = 0;
                        }
                    }
                    continue;
                }

                // Would adding this segment exceed the limit?
                if (currentChars + segLen > _maxCharsPerChunk && currentSegments.Count > 0)
                {
                    _flushChunk(chunks, currentSegments, text);
                    currentSegments.Clear();
                    currentChars = 0;
                }

                currentSegments.Add(seg);
                currentChars += segLen;
            }

            // Flush remaining
            if (currentSegments.Count > 0)
            {
                _flushChunk(chunks, currentSegments, text);
            }

            // Set total counts and overlap
            int total = chunks.Count;
            for (int i = 0; i < chunks.Count; i++)
            {
                string overlapText = "";
                if (i > 0 && _overlapChars > 0)
                {
                    var prev = chunks[i - 1];
                    // Extract tail of previous chunk's raw content as overlap
                    int prevRawStart = prev.StartOffset;
                    int prevRawEnd = prev.EndOffset;
                    string prevRaw = text.Substring(prevRawStart, prevRawEnd - prevRawStart);

                    int overlapLen = Math.Min(_overlapChars, prevRaw.Length);
                    overlapText = prevRaw.Substring(prevRaw.Length - overlapLen);
                }

                chunks[i] = new PromptChunk
                {
                    Index = i,
                    TotalChunks = total,
                    Content = _applyDecorations(
                        (overlapText.Length > 0 ? overlapText : "") +
                        text.Substring(chunks[i].StartOffset, chunks[i].EndOffset - chunks[i].StartOffset),
                        i, total, overlapText),
                    EstimatedTokens = EstimateTokens(
                        text.Substring(chunks[i].StartOffset, chunks[i].EndOffset - chunks[i].StartOffset)),
                    StartOffset = chunks[i].StartOffset,
                    EndOffset = chunks[i].EndOffset,
                    OverlapText = overlapText
                };
            }

            return chunks;
        }

        private void _flushChunk(List<PromptChunk> chunks, List<(int start, int end)> segs, string text)
        {
            int start = segs[0].start;
            int end = segs[segs.Count - 1].end;

            chunks.Add(new PromptChunk
            {
                Index = chunks.Count,
                TotalChunks = 0, // Set later
                Content = "", // Set later
                EstimatedTokens = 0,
                StartOffset = start,
                EndOffset = end
            });
        }

        private List<(int start, int end)> _forceSubSplit(string text, int start, int end)
        {
            // Force-split an oversized segment at word boundaries
            var subs = new List<(int, int)>();
            int pos = start;

            while (pos < end)
            {
                int chunkEnd = Math.Min(pos + _maxCharsPerChunk, end);

                if (chunkEnd < end)
                {
                    // Try to break at last space before limit
                    int lastSpace = text.LastIndexOf(' ', chunkEnd, chunkEnd - pos);
                    if (lastSpace > pos)
                    {
                        chunkEnd = lastSpace + 1;
                    }
                }

                subs.Add((pos, chunkEnd));
                pos = chunkEnd;
            }

            return subs;
        }

        private string _applyDecorations(string content, int index, int total, string overlapText)
        {
            var sb = new StringBuilder();

            if (_config.SystemPrompt != null)
            {
                sb.Append(_config.SystemPrompt);
                sb.Append('\n');
            }

            if (_config.AddHeaders)
            {
                sb.Append($"[Part {index + 1}/{total}]\n");
            }

            sb.Append(content);

            if (_config.ContinuationNote != null && index < total - 1)
            {
                sb.Append('\n');
                sb.Append(_config.ContinuationNote);
            }

            return sb.ToString();
        }
    }
}
