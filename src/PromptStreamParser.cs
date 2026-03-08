namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Type of content extracted from a streaming response.
    /// </summary>
    public enum StreamContentType
    {
        /// <summary>A fenced code block (```lang ... ```).</summary>
        CodeBlock,
        /// <summary>A complete JSON object ({ ... }).</summary>
        JsonObject,
        /// <summary>A complete JSON array ([ ... ]).</summary>
        JsonArray,
        /// <summary>A markdown list (bullet or numbered).</summary>
        List,
        /// <summary>A markdown table.</summary>
        Table,
        /// <summary>A key-value pair (Key: Value).</summary>
        KeyValue,
        /// <summary>A markdown heading (# ...).</summary>
        Heading,
        /// <summary>Plain text paragraph.</summary>
        Text
    }

    /// <summary>
    /// A piece of structured content extracted from a streaming response.
    /// </summary>
    public class StreamContent
    {
        /// <summary>The type of content extracted.</summary>
        public StreamContentType Type { get; init; }

        /// <summary>The raw text content.</summary>
        public string Content { get; init; } = "";

        /// <summary>Language tag for code blocks, heading level for headings, key for key-value pairs.</summary>
        public string? Tag { get; init; }

        /// <summary>Parsed value for key-value pairs, parsed JSON for JSON types.</summary>
        public object? Parsed { get; init; }

        /// <summary>Character offset where this content starts in the full response.</summary>
        public int StartOffset { get; init; }

        /// <summary>Character offset where this content ends in the full response.</summary>
        public int EndOffset { get; init; }

        /// <summary>Whether this content is still being received (not yet closed).</summary>
        public bool IsPartial { get; init; }
    }

    /// <summary>
    /// Event arguments for content extraction events.
    /// </summary>
    public class StreamContentEventArgs : EventArgs
    {
        /// <summary>The extracted content.</summary>
        public StreamContent Content { get; init; } = null!;

        /// <summary>Index of this content item in the stream.</summary>
        public int Index { get; init; }
    }

    /// <summary>
    /// Summary of all content extracted from a completed stream.
    /// </summary>
    public class StreamParserSummary
    {
        /// <summary>All extracted content items.</summary>
        public List<StreamContent> Items { get; init; } = new();

        /// <summary>Count of items by type.</summary>
        public Dictionary<StreamContentType, int> TypeCounts { get; init; } = new();

        /// <summary>Total characters processed.</summary>
        public int TotalCharacters { get; init; }

        /// <summary>Total chunks processed.</summary>
        public int TotalChunks { get; init; }

        /// <summary>All code blocks extracted.</summary>
        public List<StreamContent> CodeBlocks => Items.Where(i => i.Type == StreamContentType.CodeBlock).ToList();

        /// <summary>All JSON objects extracted.</summary>
        public List<StreamContent> JsonObjects => Items.Where(i => i.Type == StreamContentType.JsonObject).ToList();

        /// <summary>All JSON arrays extracted.</summary>
        public List<StreamContent> JsonArrays => Items.Where(i => i.Type == StreamContentType.JsonArray).ToList();

        /// <summary>All key-value pairs as a dictionary.</summary>
        public Dictionary<string, string> KeyValues => Items
            .Where(i => i.Type == StreamContentType.KeyValue && i.Tag != null)
            .GroupBy(i => i.Tag!)
            .ToDictionary(g => g.Key, g => g.Last().Content);

        /// <summary>All headings extracted.</summary>
        public List<StreamContent> Headings => Items.Where(i => i.Type == StreamContentType.Heading).ToList();

        /// <summary>All tables extracted.</summary>
        public List<StreamContent> Tables => Items.Where(i => i.Type == StreamContentType.Table).ToList();

        /// <summary>All lists extracted.</summary>
        public List<StreamContent> Lists => Items.Where(i => i.Type == StreamContentType.List).ToList();
    }

    /// <summary>
    /// Configuration for the stream parser.
    /// </summary>
    public class StreamParserOptions
    {
        /// <summary>Which content types to extract. Null means all.</summary>
        public HashSet<StreamContentType>? EnabledTypes { get; set; }

        /// <summary>Whether to attempt JSON parsing of JSON content. Default true.</summary>
        public bool ParseJson { get; set; } = true;

        /// <summary>Whether to emit partial (incomplete) content items. Default false.</summary>
        public bool EmitPartial { get; set; } = false;

        /// <summary>Maximum content length before truncating. 0 means no limit.</summary>
        public int MaxContentLength { get; set; } = 0;

        /// <summary>Whether to trim whitespace from extracted content. Default true.</summary>
        public bool TrimContent { get; set; } = true;
    }

    /// <summary>
    /// Real-time streaming response parser that extracts structured content
    /// (code blocks, JSON, lists, tables, key-value pairs, headings) from
    /// LLM streaming responses as chunks arrive.
    /// 
    /// Usage:
    ///   var parser = new PromptStreamParser();
    ///   parser.OnContent += (s, e) => Console.WriteLine($"Found {e.Content.Type}: {e.Content.Content}");
    ///   foreach (var chunk in streamingResponse)
    ///       parser.Feed(chunk);
    ///   var summary = parser.Complete();
    /// </summary>
    public class PromptStreamParser
    {
        private readonly StreamParserOptions _options;
        private readonly StringBuilder _buffer = new();
        private readonly List<StreamContent> _extracted = new();
        private int _chunkCount;
        private int _processedUpTo;

        // State tracking
        private bool _inCodeBlock;
        private string? _codeBlockLang;
        private int _codeBlockStart;
        private readonly StringBuilder _codeBlockContent = new();

        private int _jsonBraceDepth;
        private int _jsonBracketDepth;
        private int _jsonStart = -1;
        private bool _inJsonString;
        private bool _jsonEscape;
        private char _jsonOpenChar;

        private bool _inTable;
        private int _tableStart;
        private readonly StringBuilder _tableContent = new();
        private int _tableRowCount;

        private bool _inList;
        private int _listStart;
        private readonly StringBuilder _listContent = new();

        /// <summary>Fired when a complete content item is extracted.</summary>
        public event EventHandler<StreamContentEventArgs>? OnContent;

        /// <summary>Fired when a partial content item is updated (requires EmitPartial=true).</summary>
        public event EventHandler<StreamContentEventArgs>? OnPartialContent;

        /// <summary>Creates a new parser with default options.</summary>
        public PromptStreamParser() : this(new StreamParserOptions()) { }

        /// <summary>Creates a new parser with the specified options.</summary>
        public PromptStreamParser(StreamParserOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Feed a streaming chunk into the parser.
        /// </summary>
        public void Feed(StreamChunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            _chunkCount++;
            _buffer.Append(chunk.Delta);
            ProcessBuffer();

            if (chunk.IsComplete)
            {
                FlushPending();
            }
        }

        /// <summary>
        /// Feed raw text into the parser (for non-StreamChunk usage).
        /// </summary>
        public void Feed(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            _chunkCount++;
            _buffer.Append(text);
            ProcessBuffer();
        }

        /// <summary>
        /// Signal that the stream is complete and flush any pending content.
        /// </summary>
        public StreamParserSummary Complete()
        {
            FlushPending();
            ProcessRemainingText();

            var typeCounts = new Dictionary<StreamContentType, int>();
            foreach (var item in _extracted)
            {
                typeCounts.TryGetValue(item.Type, out var count);
                typeCounts[item.Type] = count + 1;
            }

            return new StreamParserSummary
            {
                Items = new List<StreamContent>(_extracted),
                TypeCounts = typeCounts,
                TotalCharacters = _buffer.Length,
                TotalChunks = _chunkCount
            };
        }

        /// <summary>
        /// Get content extracted so far without finalizing.
        /// </summary>
        public IReadOnlyList<StreamContent> CurrentContent => _extracted.AsReadOnly();

        /// <summary>
        /// Reset the parser to initial state.
        /// </summary>
        public void Reset()
        {
            _buffer.Clear();
            _extracted.Clear();
            _chunkCount = 0;
            _processedUpTo = 0;
            _inCodeBlock = false;
            _codeBlockLang = null;
            _codeBlockContent.Clear();
            _jsonBraceDepth = 0;
            _jsonBracketDepth = 0;
            _jsonStart = -1;
            _inJsonString = false;
            _jsonEscape = false;
            _inTable = false;
            _tableContent.Clear();
            _tableRowCount = 0;
            _inList = false;
            _listContent.Clear();
        }

        private bool IsTypeEnabled(StreamContentType type)
        {
            return _options.EnabledTypes == null || _options.EnabledTypes.Contains(type);
        }

        private void ProcessBuffer()
        {
            var text = _buffer.ToString();
            var i = _processedUpTo;

            while (i < text.Length)
            {
                // Code block detection
                if (IsTypeEnabled(StreamContentType.CodeBlock) && !_inCodeBlock && i + 2 < text.Length
                    && text[i] == '`' && text[i + 1] == '`' && text[i + 2] == '`')
                {
                    FlushListIfActive(i);
                    FlushTableIfActive(i);
                    _inCodeBlock = true;
                    _codeBlockStart = i;
                    _codeBlockContent.Clear();
                    var langEnd = text.IndexOf('\n', i + 3);
                    if (langEnd == -1)
                    {
                        _processedUpTo = i;
                        return;
                    }
                    _codeBlockLang = text.Substring(i + 3, langEnd - (i + 3)).Trim();
                    if (_codeBlockLang == "") _codeBlockLang = null;
                    i = langEnd + 1;
                    continue;
                }

                if (_inCodeBlock)
                {
                    if (i + 2 < text.Length && text[i] == '`' && text[i + 1] == '`' && text[i + 2] == '`')
                    {
                        var content = _codeBlockContent.ToString();
                        if (_options.TrimContent) content = content.Trim();
                        if (_options.MaxContentLength > 0 && content.Length > _options.MaxContentLength)
                            content = content.Substring(0, _options.MaxContentLength);

                        EmitContent(new StreamContent
                        {
                            Type = StreamContentType.CodeBlock,
                            Content = content,
                            Tag = _codeBlockLang,
                            StartOffset = _codeBlockStart,
                            EndOffset = i + 3
                        });

                        _inCodeBlock = false;
                        i += 3;
                        if (i < text.Length && text[i] == '\n') i++;
                        continue;
                    }
                    _codeBlockContent.Append(text[i]);
                    i++;
                    continue;
                }

                // JSON detection
                if (!_inCodeBlock && _jsonStart == -1)
                {
                    if (IsTypeEnabled(StreamContentType.JsonObject) && text[i] == '{')
                    {
                        FlushListIfActive(i);
                        FlushTableIfActive(i);
                        _jsonStart = i;
                        _jsonOpenChar = '{';
                        _jsonBraceDepth = 1;
                        _jsonBracketDepth = 0;
                        _inJsonString = false;
                        _jsonEscape = false;
                        i++;
                        continue;
                    }
                    if (IsTypeEnabled(StreamContentType.JsonArray) && text[i] == '[' && IsLikelyJsonArray(text, i))
                    {
                        FlushListIfActive(i);
                        FlushTableIfActive(i);
                        _jsonStart = i;
                        _jsonOpenChar = '[';
                        _jsonBracketDepth = 1;
                        _jsonBraceDepth = 0;
                        _inJsonString = false;
                        _jsonEscape = false;
                        i++;
                        continue;
                    }
                }

                if (_jsonStart >= 0)
                {
                    if (_jsonEscape)
                    {
                        _jsonEscape = false;
                        i++;
                        continue;
                    }
                    if (text[i] == '\\' && _inJsonString)
                    {
                        _jsonEscape = true;
                        i++;
                        continue;
                    }
                    if (text[i] == '"')
                    {
                        _inJsonString = !_inJsonString;
                        i++;
                        continue;
                    }
                    if (!_inJsonString)
                    {
                        if (text[i] == '{') _jsonBraceDepth++;
                        else if (text[i] == '}') _jsonBraceDepth--;
                        else if (text[i] == '[') _jsonBracketDepth++;
                        else if (text[i] == ']') _jsonBracketDepth--;

                        bool closed = _jsonOpenChar == '{' ? _jsonBraceDepth == 0 : _jsonBracketDepth == 0;
                        if (closed)
                        {
                            var jsonText = text.Substring(_jsonStart, i - _jsonStart + 1);
                            object? parsed = null;
                            if (_options.ParseJson)
                            {
                                try { parsed = JsonSerializer.Deserialize<JsonElement>(jsonText); }
                                catch { /* invalid JSON */ }
                            }

                            if (parsed != null || !_options.ParseJson)
                            {
                                var type = _jsonOpenChar == '{' ? StreamContentType.JsonObject : StreamContentType.JsonArray;
                                EmitContent(new StreamContent
                                {
                                    Type = type,
                                    Content = jsonText,
                                    Parsed = parsed,
                                    StartOffset = _jsonStart,
                                    EndOffset = i + 1
                                });
                            }
                            _jsonStart = -1;
                            i++;
                            continue;
                        }
                    }
                    i++;
                    continue;
                }

                // Heading detection
                if (IsTypeEnabled(StreamContentType.Heading) && text[i] == '#' && (i == 0 || text[i - 1] == '\n'))
                {
                    FlushListIfActive(i);
                    FlushTableIfActive(i);
                    var lineEnd = text.IndexOf('\n', i);
                    if (lineEnd == -1) { _processedUpTo = i; return; }
                    var line = text.Substring(i, lineEnd - i).TrimEnd();
                    var match = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
                    if (match.Success)
                    {
                        EmitContent(new StreamContent
                        {
                            Type = StreamContentType.Heading,
                            Content = match.Groups[2].Value,
                            Tag = match.Groups[1].Value.Length.ToString(),
                            StartOffset = i,
                            EndOffset = lineEnd
                        });
                    }
                    i = lineEnd + 1;
                    continue;
                }

                // Table detection
                if (IsTypeEnabled(StreamContentType.Table) && text[i] == '|' && (i == 0 || text[i - 1] == '\n'))
                {
                    FlushListIfActive(i);
                    if (!_inTable)
                    {
                        _inTable = true;
                        _tableStart = i;
                        _tableContent.Clear();
                        _tableRowCount = 0;
                    }
                    var lineEnd = text.IndexOf('\n', i);
                    if (lineEnd == -1) { _processedUpTo = i; return; }
                    _tableContent.AppendLine(text.Substring(i, lineEnd - i));
                    _tableRowCount++;
                    i = lineEnd + 1;
                    continue;
                }

                if (_inTable && (i == 0 || text[i - 1] == '\n') && text[i] != '|')
                {
                    FlushTableIfActive(i);
                }

                // List detection
                if (IsTypeEnabled(StreamContentType.List) && (i == 0 || text[i - 1] == '\n'))
                {
                    var lineEnd = text.IndexOf('\n', i);
                    if (lineEnd == -1) { _processedUpTo = i; return; }
                    var line = text.Substring(i, lineEnd - i);
                    if (IsListItem(line))
                    {
                        if (!_inList) { _inList = true; _listStart = i; _listContent.Clear(); }
                        _listContent.AppendLine(line.TrimEnd());
                        i = lineEnd + 1;
                        continue;
                    }
                    else if (_inList)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            var nextLineEnd = text.IndexOf('\n', lineEnd + 1);
                            if (nextLineEnd > lineEnd + 1)
                            {
                                var nextLine = text.Substring(lineEnd + 1, nextLineEnd - lineEnd - 1);
                                if (IsListItem(nextLine))
                                {
                                    _listContent.AppendLine();
                                    i = lineEnd + 1;
                                    continue;
                                }
                            }
                        }
                        FlushListIfActive(i);
                    }
                }

                // Key-value detection
                if (IsTypeEnabled(StreamContentType.KeyValue) && (i == 0 || text[i - 1] == '\n'))
                {
                    var lineEnd = text.IndexOf('\n', i);
                    if (lineEnd == -1) { _processedUpTo = i; return; }
                    var line = text.Substring(i, lineEnd - i);
                    var kvMatch = Regex.Match(line, @"^\s{0,3}\*{0,2}([A-Za-z][\w\s]{0,40}?)\*{0,2}\s*:\s+(.+)$");
                    if (kvMatch.Success && !line.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        EmitContent(new StreamContent
                        {
                            Type = StreamContentType.KeyValue,
                            Content = kvMatch.Groups[2].Value.Trim(),
                            Tag = kvMatch.Groups[1].Value.Trim(),
                            StartOffset = i,
                            EndOffset = lineEnd
                        });
                        i = lineEnd + 1;
                        continue;
                    }
                }

                i++;
            }

            _processedUpTo = i;
        }

        private static bool IsListItem(string line)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
                return true;
            return Regex.IsMatch(trimmed, @"^\d+\.\s");
        }

        private static bool IsLikelyJsonArray(string text, int pos)
        {
            if (pos + 1 >= text.Length) return false;
            var next = text[pos + 1];
            return next == '"' || next == '{' || next == '[' || next == '\n' || next == '\r'
                || next == ' ' || next == '\t' || char.IsDigit(next) || next == 't' || next == 'f' || next == 'n';
        }

        private void FlushListIfActive(int endOffset)
        {
            if (!_inList) return;
            var content = _listContent.ToString();
            if (_options.TrimContent) content = content.Trim();
            EmitContent(new StreamContent
            {
                Type = StreamContentType.List,
                Content = content,
                StartOffset = _listStart,
                EndOffset = endOffset
            });
            _inList = false;
            _listContent.Clear();
        }

        private void FlushTableIfActive(int endOffset)
        {
            if (_inTable && _tableRowCount >= 2)
            {
                var content = _tableContent.ToString();
                if (_options.TrimContent) content = content.Trim();
                EmitContent(new StreamContent
                {
                    Type = StreamContentType.Table,
                    Content = content,
                    StartOffset = _tableStart,
                    EndOffset = endOffset
                });
            }
            _inTable = false;
            _tableContent.Clear();
            _tableRowCount = 0;
        }

        private void FlushPending()
        {
            FlushListIfActive(_buffer.Length);
            FlushTableIfActive(_buffer.Length);

            if (_inCodeBlock && _options.EmitPartial)
            {
                var content = _codeBlockContent.ToString();
                if (_options.TrimContent) content = content.Trim();
                EmitContent(new StreamContent
                {
                    Type = StreamContentType.CodeBlock,
                    Content = content,
                    Tag = _codeBlockLang,
                    StartOffset = _codeBlockStart,
                    EndOffset = _buffer.Length,
                    IsPartial = true
                });
            }
            _inCodeBlock = false;

            if (_jsonStart >= 0 && _options.EmitPartial)
            {
                var jsonText = _buffer.ToString().Substring(_jsonStart);
                var type = _jsonOpenChar == '{' ? StreamContentType.JsonObject : StreamContentType.JsonArray;
                EmitContent(new StreamContent
                {
                    Type = type,
                    Content = jsonText,
                    StartOffset = _jsonStart,
                    EndOffset = _buffer.Length,
                    IsPartial = true
                });
            }
            _jsonStart = -1;
        }

        private void ProcessRemainingText()
        {
            if (!IsTypeEnabled(StreamContentType.Text)) return;

            var text = _buffer.ToString();
            var covered = new bool[text.Length];
            foreach (var item in _extracted)
            {
                for (var j = item.StartOffset; j < Math.Min(item.EndOffset, text.Length); j++)
                    covered[j] = true;
            }

            var sb = new StringBuilder();
            int start = -1;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && !covered[i])
                {
                    if (start == -1) start = i;
                    sb.Append(text[i]);
                }
                else if (sb.Length > 0)
                {
                    var paragraph = sb.ToString();
                    if (_options.TrimContent) paragraph = paragraph.Trim();
                    if (paragraph.Length > 0)
                    {
                        _extracted.Add(new StreamContent
                        {
                            Type = StreamContentType.Text,
                            Content = paragraph,
                            StartOffset = start,
                            EndOffset = i
                        });
                    }
                    sb.Clear();
                    start = -1;
                }
            }
        }

        private void EmitContent(StreamContent content)
        {
            _extracted.Add(content);
            var args = new StreamContentEventArgs { Content = content, Index = _extracted.Count - 1 };

            if (content.IsPartial)
                OnPartialContent?.Invoke(this, args);
            else
                OnContent?.Invoke(this, args);
        }
    }
}
