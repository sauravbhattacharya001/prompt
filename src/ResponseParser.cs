namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Extracts structured data from LLM text responses. Supports JSON blocks,
    /// key-value pairs, numbered/bulleted lists, markdown tables, code blocks,
    /// and custom regex patterns — so you can turn free-form AI output into
    /// typed data without manual string manipulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LLM responses are unpredictable: sometimes they wrap JSON in markdown
    /// fences, sometimes they return bare objects, sometimes they mix prose
    /// with structured data. <c>ResponseParser</c> handles all of these cases.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// string response = "Here is the data:\n```json\n{\"name\": \"Alice\", \"age\": 30}\n```";
    /// var person = ResponseParser.ExtractJson&lt;Person&gt;(response);
    /// // person.Name == "Alice", person.Age == 30
    ///
    /// string listResponse = "1. Buy groceries\n2. Walk the dog\n3. Read a book";
    /// var items = ResponseParser.ExtractList(listResponse);
    /// // items == ["Buy groceries", "Walk the dog", "Read a book"]
    /// </code>
    /// </para>
    /// </remarks>
    public static class ResponseParser
    {
        /// <summary>
        /// Maximum response length accepted for parsing to prevent denial-of-service
        /// via extremely large inputs. Default: 1 MB.
        /// </summary>
        public const int MaxResponseLength = 1_048_576;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        // ═══════════════════════════════════════════════════════
        // JSON Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts and deserializes JSON from an LLM response. Handles bare JSON,
        /// markdown-fenced JSON (```json ... ```), and JSON embedded in prose.
        /// Returns <c>default</c> if no valid JSON is found.
        /// </summary>
        /// <typeparam name="T">Target type to deserialize into.</typeparam>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns>Deserialized object, or <c>default(T)</c> if extraction fails.</returns>
        public static T? ExtractJson<T>(string response)
        {
            ValidateInput(response);

            // Try markdown-fenced JSON first (```json ... ``` or ``` ... ```)
            string? jsonBlock = ExtractFencedBlock(response, "json");
            if (jsonBlock != null)
            {
                try { return JsonSerializer.Deserialize<T>(jsonBlock, _jsonOptions); }
                catch (JsonException) { /* fall through to other strategies */ }
            }

            // Try bare JSON (find first { or [)
            string? bareJson = ExtractBareJson(response);
            if (bareJson != null)
            {
                try { return JsonSerializer.Deserialize<T>(bareJson, _jsonOptions); }
                catch (JsonException) { /* fall through */ }
            }

            return default;
        }

        /// <summary>
        /// Extracts JSON from an LLM response as a <see cref="JsonDocument"/>.
        /// Useful when you don't have a target type or want to inspect the
        /// structure dynamically.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns>Parsed <see cref="JsonDocument"/>, or <c>null</c> if no JSON found.</returns>
        public static JsonDocument? ExtractJsonDocument(string response)
        {
            ValidateInput(response);

            string? jsonBlock = ExtractFencedBlock(response, "json");
            if (jsonBlock != null)
            {
                try { return JsonDocument.Parse(jsonBlock); }
                catch (JsonException) { }
            }

            string? bareJson = ExtractBareJson(response);
            if (bareJson != null)
            {
                try { return JsonDocument.Parse(bareJson); }
                catch (JsonException) { }
            }

            return null;
        }

        /// <summary>
        /// Attempts to extract and deserialize JSON, returning a success flag
        /// instead of <c>default</c> on failure.
        /// </summary>
        public static bool TryExtractJson<T>(string response, out T? result)
        {
            try
            {
                result = ExtractJson<T>(response);
                return result != null;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════
        // List Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts an ordered list from an LLM response. Handles numbered lists
        /// (1. item), bulleted lists (- item, * item, • item), and mixed formats.
        /// Filters out empty items and trims whitespace.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns>List of extracted items in order.</returns>
        public static List<string> ExtractList(string response)
        {
            ValidateInput(response);

            var items = new List<string>();
            var lines = response.Split('\n');

            // Pattern: numbered (1. item, 1) item) or bulleted (- item, * item, • item)
            var listPattern = new Regex(@"^\s*(?:\d+[\.\)]\s+|[-\*•]\s+)(.+)$");

            foreach (var line in lines)
            {
                var match = listPattern.Match(line);
                if (match.Success)
                {
                    string item = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(item))
                        items.Add(item);
                }
            }

            return items;
        }

        /// <summary>
        /// Extracts a numbered list specifically, preserving the original numbering.
        /// Returns a dictionary mapping the number to the item text.
        /// </summary>
        public static Dictionary<int, string> ExtractNumberedList(string response)
        {
            ValidateInput(response);

            var items = new Dictionary<int, string>();
            var pattern = new Regex(@"^\s*(\d+)[\.\)]\s+(.+)$", RegexOptions.Multiline);

            foreach (Match match in pattern.Matches(response))
            {
                if (int.TryParse(match.Groups[1].Value, out int number))
                {
                    string item = match.Groups[2].Value.Trim();
                    if (!string.IsNullOrEmpty(item))
                        items[number] = item;
                }
            }

            return items;
        }

        // ═══════════════════════════════════════════════════════
        // Key-Value Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts key-value pairs from an LLM response. Handles formats like
        /// "Key: Value", "Key = Value", "**Key**: Value", and "Key - Value".
        /// Keys are normalized to trimmed strings; duplicate keys use the last value.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns>Dictionary of extracted key-value pairs.</returns>
        public static Dictionary<string, string> ExtractKeyValuePairs(string response)
        {
            ValidateInput(response);

            var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = response.Split('\n');

            // Pattern: key: value, key = value, **key**: value, key - value
            var kvPattern = new Regex(@"^\s*\*{0,2}([^:=\-\n]+?)\*{0,2}\s*(?::|=|-)\s*(.+)$");

            foreach (var line in lines)
            {
                var match = kvPattern.Match(line);
                if (match.Success)
                {
                    string key = match.Groups[1].Value.Trim();
                    string value = match.Groups[2].Value.Trim();

                    // Skip lines that look like list items, not key-value pairs
                    if (key.Length > 0 && key.Length <= 100 && value.Length > 0)
                        pairs[key] = value;
                }
            }

            return pairs;
        }

        /// <summary>
        /// Extracts a specific value by key name from an LLM response.
        /// Case-insensitive key matching.
        /// </summary>
        public static string? ExtractValue(string response, string key)
        {
            var pairs = ExtractKeyValuePairs(response);
            return pairs.TryGetValue(key, out string? value) ? value : null;
        }

        // ═══════════════════════════════════════════════════════
        // Code Block Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts all code blocks from a markdown-formatted LLM response.
        /// Returns each block with its language tag (if specified) and content.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns>List of code blocks as (Language, Code) tuples.</returns>
        public static List<CodeBlock> ExtractCodeBlocks(string response)
        {
            ValidateInput(response);

            var blocks = new List<CodeBlock>();
            var pattern = new Regex(@"```(\w*)\s*\n([\s\S]*?)```", RegexOptions.Multiline);

            foreach (Match match in pattern.Matches(response))
            {
                string language = match.Groups[1].Value.Trim();
                string code = match.Groups[2].Value.TrimEnd();
                blocks.Add(new CodeBlock(
                    string.IsNullOrEmpty(language) ? null : language,
                    code
                ));
            }

            return blocks;
        }

        /// <summary>
        /// Extracts the first code block with the specified language tag.
        /// Returns <c>null</c> if no matching block is found.
        /// </summary>
        public static CodeBlock? ExtractCodeBlock(string response, string? language = null)
        {
            var blocks = ExtractCodeBlocks(response);
            if (language == null)
                return blocks.Count > 0 ? blocks[0] : null;

            return blocks.Find(b =>
                string.Equals(b.Language, language, StringComparison.OrdinalIgnoreCase));
        }

        // ═══════════════════════════════════════════════════════
        // Markdown Table Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts data from a markdown table in the LLM response.
        /// Returns a list of dictionaries, where each dictionary maps
        /// column headers to cell values for that row.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns>List of row dictionaries.</returns>
        public static List<Dictionary<string, string>> ExtractTable(string response)
        {
            ValidateInput(response);

            var rows = new List<Dictionary<string, string>>();
            var lines = response.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("|") && l.EndsWith("|"))
                .ToList();

            if (lines.Count < 2)
                return rows;

            // First line is headers
            var headers = ParseTableRow(lines[0]);
            if (headers.Count == 0)
                return rows;

            // Skip separator line(s) (|---|---|)
            int dataStart = 1;
            for (int i = 1; i < lines.Count; i++)
            {
                if (Regex.IsMatch(lines[i], @"^\|[\s\-:]+(\|[\s\-:]+)*\|$"))
                {
                    dataStart = i + 1;
                    break;
                }
            }

            // Parse data rows
            for (int i = dataStart; i < lines.Count; i++)
            {
                var cells = ParseTableRow(lines[i]);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < Math.Min(headers.Count, cells.Count); j++)
                {
                    row[headers[j]] = cells[j];
                }
                if (row.Count > 0)
                    rows.Add(row);
            }

            return rows;
        }

        // ═══════════════════════════════════════════════════════
        // Custom Pattern Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts all matches for a custom regex pattern from the response.
        /// Returns the first capture group from each match, or the full match
        /// if no groups are defined.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <param name="pattern">Regex pattern to match.</param>
        /// <returns>List of matched strings.</returns>
        /// <exception cref="ArgumentException">If the pattern is invalid.</exception>
        public static List<string> ExtractPattern(string response, string pattern)
        {
            ValidateInput(response);
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be null or empty.", nameof(pattern));

            var results = new List<string>();
            var regex = new Regex(pattern, RegexOptions.Multiline, TimeSpan.FromSeconds(5));

            foreach (Match match in regex.Matches(response))
            {
                string value = match.Groups.Count > 1
                    ? match.Groups[1].Value
                    : match.Value;
                if (!string.IsNullOrEmpty(value))
                    results.Add(value);
            }

            return results;
        }

        /// <summary>
        /// Extracts the first match for a custom regex pattern.
        /// Returns <c>null</c> if no match is found.
        /// </summary>
        public static string? ExtractFirstPattern(string response, string pattern)
        {
            var matches = ExtractPattern(response, pattern);
            return matches.Count > 0 ? matches[0] : null;
        }

        // ═══════════════════════════════════════════════════════
        // Boolean / Sentiment Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Determines a yes/no answer from an LLM response. Handles common
        /// affirmative and negative patterns (yes, no, true, false, certainly,
        /// I don't think so, etc.).
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns><c>true</c> for affirmative, <c>false</c> for negative,
        /// <c>null</c> if indeterminate.</returns>
        public static bool? ExtractBoolean(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return null;

            ValidateInput(response);

            // Normalize: take the first meaningful line
            string firstLine = response.Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? "";
            firstLine = firstLine.ToLowerInvariant();

            // Strong affirmative signals
            var yesPatterns = new[]
            {
                @"^\s*yes\b", @"^\s*true\b", @"^\s*correct\b",
                @"^\s*affirmative\b", @"^\s*absolutely\b",
                @"^\s*certainly\b", @"^\s*indeed\b", @"^\s*definitely\b",
                @"\bthat(?:'s| is) (?:correct|right|true)\b",
                @"\byes,?\s+(?:it|that|this)\b"
            };

            // Strong negative signals
            var noPatterns = new[]
            {
                @"^\s*no\b", @"^\s*false\b", @"^\s*incorrect\b",
                @"^\s*negative\b", @"^\s*not?\s+(?:really|exactly)\b",
                @"\bdon'?t think so\b", @"\bthat(?:'s| is) (?:incorrect|wrong|false)\b",
                @"\bno,?\s+(?:it|that|this)\b"
            };

            foreach (var p in yesPatterns)
                if (Regex.IsMatch(firstLine, p))
                    return true;

            foreach (var p in noPatterns)
                if (Regex.IsMatch(firstLine, p))
                    return false;

            return null;
        }

        // ═══════════════════════════════════════════════════════
        // Number Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts all numbers (integer and decimal) from the response.
        /// Handles negative numbers and numbers with commas (e.g., 1,234.56).
        /// </summary>
        public static List<double> ExtractNumbers(string response)
        {
            ValidateInput(response);

            var numbers = new List<double>();
            // Match numbers with optional commas and decimals, but not inside words
            var pattern = new Regex(@"(?<!\w)-?(?:\d{1,3}(?:,\d{3})*|\d+)(?:\.\d+)?(?!\w)");

            foreach (Match match in pattern.Matches(response))
            {
                string numStr = match.Value.Replace(",", "");
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double num))
                {
                    numbers.Add(num);
                }
            }

            return numbers;
        }

        /// <summary>
        /// Extracts the first number found in the response.
        /// Returns <c>null</c> if no number is found.
        /// </summary>
        public static double? ExtractFirstNumber(string response)
        {
            var numbers = ExtractNumbers(response);
            return numbers.Count > 0 ? numbers[0] : null;
        }

        // ═══════════════════════════════════════════════════════
        // Section Extraction
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Extracts content under a specific markdown heading from the response.
        /// Matches headings at any level (##, ###, etc.) case-insensitively.
        /// Returns all text until the next heading of equal or higher level.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <param name="heading">Heading text to find (without # prefix).</param>
        /// <returns>Content under the heading, or <c>null</c> if not found.</returns>
        public static string? ExtractSection(string response, string heading)
        {
            ValidateInput(response);
            if (string.IsNullOrWhiteSpace(heading))
                throw new ArgumentException("Heading cannot be null or empty.", nameof(heading));

            var lines = response.Split('\n');
            int startIndex = -1;
            int headingLevel = 0;

            // Find the target heading
            for (int i = 0; i < lines.Length; i++)
            {
                var match = Regex.Match(lines[i], @"^(#{1,6})\s+(.+)$");
                if (match.Success &&
                    string.Equals(match.Groups[2].Value.Trim(), heading, StringComparison.OrdinalIgnoreCase))
                {
                    startIndex = i + 1;
                    headingLevel = match.Groups[1].Value.Length;
                    break;
                }
            }

            if (startIndex < 0)
                return null;

            // Collect content until next heading of equal or higher level
            var content = new List<string>();
            for (int i = startIndex; i < lines.Length; i++)
            {
                var match = Regex.Match(lines[i], @"^(#{1,6})\s+");
                if (match.Success && match.Groups[1].Value.Length <= headingLevel)
                    break;
                content.Add(lines[i]);
            }

            string result = string.Join("\n", content).Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// Extracts all markdown headings from the response with their levels.
        /// </summary>
        public static List<(int Level, string Text)> ExtractHeadings(string response)
        {
            ValidateInput(response);

            var headings = new List<(int Level, string Text)>();
            var pattern = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);

            foreach (Match match in pattern.Matches(response))
            {
                headings.Add((match.Groups[1].Value.Length, match.Groups[2].Value.Trim()));
            }

            return headings;
        }

        // ═══════════════════════════════════════════════════════
        // Composite Parsing (ParseResult)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Parses an LLM response and returns a <see cref="ParseResult"/> containing
        /// all detected structured elements (JSON, lists, key-value pairs, code blocks,
        /// tables, numbers, headings). One call to extract everything.
        /// </summary>
        /// <param name="response">Raw LLM response text.</param>
        /// <returns>A <see cref="ParseResult"/> with all extracted data.</returns>
        public static ParseResult Parse(string response)
        {
            ValidateInput(response);

            return new ParseResult
            {
                RawResponse = response,
                Json = ExtractJsonDocument(response),
                Lists = ExtractList(response),
                KeyValuePairs = ExtractKeyValuePairs(response),
                CodeBlocks = ExtractCodeBlocks(response),
                Tables = ExtractTable(response),
                Numbers = ExtractNumbers(response),
                Headings = ExtractHeadings(response),
                Boolean = ExtractBoolean(response),
            };
        }

        // ═══════════════════════════════════════════════════════
        // Internal Helpers
        // ═══════════════════════════════════════════════════════

        private static void ValidateInput(string response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));
            if (response.Length > MaxResponseLength)
                throw new ArgumentException(
                    $"Response exceeds maximum length of {MaxResponseLength:N0} characters.",
                    nameof(response));
        }

        /// <summary>
        /// Extracts content from a markdown-fenced block (```lang ... ```).
        /// If <paramref name="language"/> is specified, only matches blocks
        /// with that language tag. Otherwise matches any fenced block.
        /// </summary>
        internal static string? ExtractFencedBlock(string response, string? language)
        {
            string langPattern = string.IsNullOrEmpty(language) ? @"\w*" : Regex.Escape(language);
            var pattern = new Regex($@"```{langPattern}\s*\n([\s\S]*?)```", RegexOptions.Multiline);
            var match = pattern.Match(response);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>
        /// Attempts to extract bare JSON (object or array) from text by finding
        /// matching braces/brackets. Handles nested structures.
        /// </summary>
        internal static string? ExtractBareJson(string response)
        {
            // Find first { or [
            int objStart = response.IndexOf('{');
            int arrStart = response.IndexOf('[');

            int start;
            char open, close;

            if (objStart >= 0 && (arrStart < 0 || objStart < arrStart))
            {
                start = objStart;
                open = '{';
                close = '}';
            }
            else if (arrStart >= 0)
            {
                start = arrStart;
                open = '[';
                close = ']';
            }
            else
            {
                return null;
            }

            // Match braces/brackets with nesting, tracking string literals
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < response.Length; i++)
            {
                char c = response[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return response.Substring(start, i - start + 1);
                }
            }

            return null;
        }

        private static List<string> ParseTableRow(string line)
        {
            return line
                .Trim('|')
                .Split('|')
                .Select(cell => cell.Trim())
                .Where(cell => !string.IsNullOrEmpty(cell))
                .ToList();
        }
    }

    // ═══════════════════════════════════════════════════════
    // Supporting Types
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Represents an extracted code block with its language tag and content.
    /// </summary>
    public class CodeBlock
    {
        /// <summary>Language tag (e.g., "csharp", "python"), or <c>null</c> if unspecified.</summary>
        [JsonPropertyName("language")]
        public string? Language { get; }

        /// <summary>The code content (without fence markers).</summary>
        [JsonPropertyName("code")]
        public string Code { get; }

        /// <summary>Creates a new <see cref="CodeBlock"/>.</summary>
        public CodeBlock(string? language, string code)
        {
            Language = language;
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }

        /// <inheritdoc />
        public override string ToString() =>
            Language != null ? $"```{Language}\n{Code}\n```" : $"```\n{Code}\n```";
    }

    /// <summary>
    /// Result of a comprehensive parse operation containing all detected
    /// structured elements from an LLM response.
    /// </summary>
    public class ParseResult
    {
        /// <summary>The original raw response text.</summary>
        public string RawResponse { get; init; } = "";

        /// <summary>Extracted JSON document, or <c>null</c> if none found.</summary>
        public JsonDocument? Json { get; init; }

        /// <summary>Extracted list items (numbered or bulleted).</summary>
        public List<string> Lists { get; init; } = new();

        /// <summary>Extracted key-value pairs.</summary>
        public Dictionary<string, string> KeyValuePairs { get; init; } = new();

        /// <summary>Extracted code blocks with language tags.</summary>
        public List<CodeBlock> CodeBlocks { get; init; } = new();

        /// <summary>Extracted markdown table rows.</summary>
        public List<Dictionary<string, string>> Tables { get; init; } = new();

        /// <summary>Extracted numbers.</summary>
        public List<double> Numbers { get; init; } = new();

        /// <summary>Extracted markdown headings with levels.</summary>
        public List<(int Level, string Text)> Headings { get; init; } = new();

        /// <summary>Extracted boolean/yes-no answer, or <c>null</c> if indeterminate.</summary>
        public bool? Boolean { get; init; }

        /// <summary>Whether any structured data was found.</summary>
        public bool HasStructuredData =>
            Json != null || Lists.Count > 0 || KeyValuePairs.Count > 0 ||
            CodeBlocks.Count > 0 || Tables.Count > 0 || Numbers.Count > 0;
    }
}
