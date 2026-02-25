namespace Prompt
{
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Formatting style for few-shot examples.
    /// </summary>
    public enum FewShotFormat
    {
        /// <summary>
        /// Labeled format with configurable labels:
        /// <code>
        /// Input: What is 2+2?
        /// Output: 4
        /// </code>
        /// </summary>
        Labeled,

        /// <summary>
        /// Chat-style format:
        /// <code>
        /// User: What is 2+2?
        /// Assistant: 4
        /// </code>
        /// </summary>
        ChatStyle,

        /// <summary>
        /// Minimal arrow format:
        /// <code>
        /// What is 2+2? => 4
        /// </code>
        /// </summary>
        Minimal,

        /// <summary>
        /// Numbered examples:
        /// <code>
        /// Example 1:
        /// Input: What is 2+2?
        /// Output: 4
        /// </code>
        /// </summary>
        Numbered,

        /// <summary>
        /// XML-tagged format (good for Claude and structured parsing):
        /// <code>
        /// &lt;example&gt;
        ///   &lt;input&gt;What is 2+2?&lt;/input&gt;
        ///   &lt;output&gt;4&lt;/output&gt;
        /// &lt;/example&gt;
        /// </code>
        /// </summary>
        Xml
    }

    /// <summary>
    /// A single input/output example for few-shot prompting.
    /// </summary>
    public class FewShotExample
    {
        /// <summary>The example input/question.</summary>
        [JsonPropertyName("input")]
        public string Input { get; }

        /// <summary>The expected output/answer.</summary>
        [JsonPropertyName("output")]
        public string Output { get; }

        /// <summary>Optional label/category for the example.</summary>
        [JsonPropertyName("label")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Label { get; }

        [JsonConstructor]
        public FewShotExample(string input, string output, string? label = null)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Label = label;
        }

        /// <summary>Estimate token count for this example.</summary>
        public int EstimateTokens()
        {
            return PromptGuard.EstimateTokens(Input) + PromptGuard.EstimateTokens(Output)
                   + (Label != null ? PromptGuard.EstimateTokens(Label) : 0)
                   + 4; // formatting overhead (labels, newlines)
        }
    }

    /// <summary>
    /// Builds structured few-shot prompts from examples. Supports multiple
    /// formatting styles, token-budget-aware example selection, shuffling,
    /// filtering by label, and JSON serialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Few-shot prompting is one of the most effective prompt engineering
    /// techniques: showing the model examples of desired input→output pairs
    /// dramatically improves response quality and consistency.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var builder = new FewShotBuilder("Classify the sentiment of the text.")
    ///     .AddExample("I love this product!", "positive")
    ///     .AddExample("This is terrible.", "negative")
    ///     .AddExample("It's okay I guess.", "neutral");
    ///
    /// string prompt = builder.Build("The weather is beautiful today.");
    /// // Task: Classify the sentiment of the text.
    /// //
    /// // Input: I love this product!
    /// // Output: positive
    /// //
    /// // Input: This is terrible.
    /// // Output: negative
    /// //
    /// // Input: It's okay I guess.
    /// // Output: neutral
    /// //
    /// // Input: The weather is beautiful today.
    /// // Output:
    /// </code>
    /// </para>
    /// </remarks>
    public class FewShotBuilder
    {
        /// <summary>Maximum number of examples allowed.</summary>
        public const int MaxExamples = 500;

        /// <summary>Maximum allowed JSON payload size (10 MB).</summary>
        internal const int MaxJsonPayloadBytes = SerializationGuards.MaxJsonPayloadBytes;

        private readonly List<FewShotExample> _examples = new();
        private string? _taskDescription;
        private string _inputLabel = "Input";
        private string _outputLabel = "Output";
        private string _separator = "\n\n";
        private FewShotFormat _format = FewShotFormat.Labeled;
        private string? _systemContext;

        /// <summary>Number of examples currently stored.</summary>
        public int ExampleCount => _examples.Count;

        /// <summary>Read-only view of all examples.</summary>
        public IReadOnlyList<FewShotExample> Examples => _examples.AsReadOnly();

        /// <summary>The current task description.</summary>
        public string? TaskDescription => _taskDescription;

        /// <summary>The current formatting style.</summary>
        public FewShotFormat Format => _format;

        /// <summary>The current input label.</summary>
        public string InputLabel => _inputLabel;

        /// <summary>The current output label.</summary>
        public string OutputLabel => _outputLabel;

        /// <summary>The current example separator.</summary>
        public string Separator => _separator;

        /// <summary>Optional system context prepended before the task description.</summary>
        public string? SystemContext => _systemContext;

        /// <summary>
        /// Create a new FewShotBuilder.
        /// </summary>
        /// <param name="taskDescription">
        /// Optional task description placed at the top of the prompt.
        /// E.g., "Classify the sentiment of the following text."
        /// </param>
        public FewShotBuilder(string? taskDescription = null)
        {
            _taskDescription = taskDescription;
        }

        // ──────────── Example Management ────────────

        /// <summary>Add a single example.</summary>
        public FewShotBuilder AddExample(string input, string output, string? label = null)
        {
            if (_examples.Count >= MaxExamples)
                throw new InvalidOperationException($"Maximum of {MaxExamples} examples reached.");
            _examples.Add(new FewShotExample(input, output, label));
            return this;
        }

        /// <summary>Add a pre-built example.</summary>
        public FewShotBuilder AddExample(FewShotExample example)
        {
            if (example == null) throw new ArgumentNullException(nameof(example));
            if (_examples.Count >= MaxExamples)
                throw new InvalidOperationException($"Maximum of {MaxExamples} examples reached.");
            _examples.Add(example);
            return this;
        }

        /// <summary>Add multiple examples at once.</summary>
        public FewShotBuilder AddExamples(IEnumerable<FewShotExample> examples)
        {
            if (examples == null) throw new ArgumentNullException(nameof(examples));
            foreach (var ex in examples)
            {
                AddExample(ex);
            }
            return this;
        }

        /// <summary>Remove the example at the given index.</summary>
        public FewShotBuilder RemoveExample(int index)
        {
            if (index < 0 || index >= _examples.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index {index} out of range. Have {_examples.Count} examples.");
            _examples.RemoveAt(index);
            return this;
        }

        /// <summary>Remove all examples.</summary>
        public FewShotBuilder ClearExamples()
        {
            _examples.Clear();
            return this;
        }

        /// <summary>
        /// Shuffle examples randomly (useful to avoid order bias).
        /// </summary>
        /// <param name="seed">Optional random seed for reproducibility.</param>
        public FewShotBuilder ShuffleExamples(int? seed = null)
        {
            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            // Fisher-Yates shuffle
            for (int i = _examples.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_examples[i], _examples[j]) = (_examples[j], _examples[i]);
            }
            return this;
        }

        /// <summary>
        /// Reorder examples by specifying the desired index order.
        /// E.g., ReorderExamples(2, 0, 1) puts example 2 first.
        /// </summary>
        public FewShotBuilder ReorderExamples(params int[] indices)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (indices.Length != _examples.Count)
                throw new ArgumentException(
                    $"Must provide exactly {_examples.Count} indices, got {indices.Length}.");

            var seen = new HashSet<int>();
            foreach (var idx in indices)
            {
                if (idx < 0 || idx >= _examples.Count)
                    throw new ArgumentOutOfRangeException(nameof(indices),
                        $"Index {idx} out of range.");
                if (!seen.Add(idx))
                    throw new ArgumentException($"Duplicate index {idx}.");
            }

            var reordered = new List<FewShotExample>(indices.Length);
            foreach (var idx in indices)
                reordered.Add(_examples[idx]);

            _examples.Clear();
            _examples.AddRange(reordered);
            return this;
        }

        /// <summary>
        /// Get examples filtered by label.
        /// </summary>
        public IReadOnlyList<FewShotExample> GetExamplesByLabel(string label)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            return _examples
                .Where(e => string.Equals(e.Label, label, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Get all distinct labels across examples.
        /// </summary>
        public IReadOnlyList<string> GetLabels()
        {
            return _examples
                .Where(e => e.Label != null)
                .Select(e => e.Label!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        }

        // ──────────── Configuration ────────────

        /// <summary>Set or update the task description.</summary>
        public FewShotBuilder WithTaskDescription(string? description)
        {
            _taskDescription = description;
            return this;
        }

        /// <summary>Set the label for input fields (default: "Input").</summary>
        public FewShotBuilder WithInputLabel(string label)
        {
            _inputLabel = label ?? throw new ArgumentNullException(nameof(label));
            return this;
        }

        /// <summary>Set the label for output fields (default: "Output").</summary>
        public FewShotBuilder WithOutputLabel(string label)
        {
            _outputLabel = label ?? throw new ArgumentNullException(nameof(label));
            return this;
        }

        /// <summary>Set the separator between examples (default: "\n\n").</summary>
        public FewShotBuilder WithSeparator(string separator)
        {
            _separator = separator ?? throw new ArgumentNullException(nameof(separator));
            return this;
        }

        /// <summary>Set the formatting style.</summary>
        public FewShotBuilder WithFormat(FewShotFormat format)
        {
            _format = format;
            return this;
        }

        /// <summary>
        /// Set optional system context prepended before the task description.
        /// Useful for role instructions like "You are an expert linguist."
        /// </summary>
        public FewShotBuilder WithSystemContext(string? context)
        {
            _systemContext = context;
            return this;
        }

        // ──────────── Building ────────────

        /// <summary>
        /// Build the complete few-shot prompt.
        /// </summary>
        /// <param name="query">
        /// Optional query to append at the end. If provided, the prompt
        /// ends with the input label and an empty output for the model to fill in.
        /// </param>
        /// <returns>The formatted prompt string.</returns>
        public string Build(string? query = null)
        {
            return BuildFromExamples(_examples, query);
        }

        /// <summary>
        /// Build a few-shot prompt with a token budget. If all examples
        /// exceed the budget, examples are removed from the end until
        /// the prompt fits. Always includes the query.
        /// </summary>
        /// <param name="query">The query to classify/answer.</param>
        /// <param name="maxTokens">Maximum token budget for the prompt.</param>
        /// <returns>The formatted prompt string within the token budget.</returns>
        public string BuildWithTokenLimit(string query, int maxTokens)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (maxTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maxTokens), "Must be positive.");

            // Try with all examples first
            string full = Build(query);
            if (PromptGuard.EstimateTokens(full) <= maxTokens)
                return full;

            // Progressively remove examples from the end until we fit
            var subset = new List<FewShotExample>(_examples);
            while (subset.Count > 0)
            {
                subset.RemoveAt(subset.Count - 1);
                string attempt = BuildFromExamples(subset, query);
                if (PromptGuard.EstimateTokens(attempt) <= maxTokens)
                    return attempt;
            }

            // No examples fit — just the task description + query
            return BuildFromExamples(new List<FewShotExample>(), query);
        }

        /// <summary>
        /// Build a few-shot prompt using only examples with the specified labels.
        /// </summary>
        public string BuildWithLabels(IEnumerable<string> labels, string? query = null)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            var labelSet = new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);
            var filtered = _examples
                .Where(e => e.Label != null && labelSet.Contains(e.Label))
                .ToList();
            return BuildFromExamples(filtered, query);
        }

        /// <summary>
        /// Build a few-shot prompt selecting N random examples.
        /// Useful for large example pools where variety matters.
        /// </summary>
        /// <param name="count">Number of examples to include.</param>
        /// <param name="query">Optional query.</param>
        /// <param name="seed">Optional random seed for reproducibility.</param>
        public string BuildWithRandomSelection(int count, string? query = null, int? seed = null)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Must be non-negative.");

            if (count >= _examples.Count)
                return Build(query);

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var indices = Enumerable.Range(0, _examples.Count)
                .OrderBy(_ => rng.Next())
                .Take(count)
                .OrderBy(i => i) // preserve original ordering
                .ToList();

            var selected = indices.Select(i => _examples[i]).ToList();
            return BuildFromExamples(selected, query);
        }

        /// <summary>
        /// Estimate the total token count for the built prompt.
        /// </summary>
        public int EstimateTokens(string? query = null)
        {
            return PromptGuard.EstimateTokens(Build(query));
        }

        // ──────────── Formatting Core ────────────

        private string BuildFromExamples(List<FewShotExample> examples, string? query)
        {
            var sb = new StringBuilder();
            bool hasPrior = false;

            // System context
            if (!string.IsNullOrWhiteSpace(_systemContext))
            {
                sb.Append(_systemContext);
                hasPrior = true;
            }

            // Task description
            if (!string.IsNullOrWhiteSpace(_taskDescription))
            {
                if (hasPrior) sb.Append(_separator);
                sb.Append(_taskDescription);
                hasPrior = true;
            }

            // Examples
            for (int i = 0; i < examples.Count; i++)
            {
                if (hasPrior || i > 0)
                    sb.Append(_separator);
                sb.Append(FormatExample(examples[i], i + 1));
                hasPrior = true;
            }

            // Query
            if (query != null)
            {
                if (hasPrior)
                    sb.Append(_separator);
                sb.Append(FormatQuery(query));
            }

            return sb.ToString();
        }

        private string FormatExample(FewShotExample example, int number)
        {
            return _format switch
            {
                FewShotFormat.Labeled => FormatLabeled(example),
                FewShotFormat.ChatStyle => FormatChatStyle(example),
                FewShotFormat.Minimal => FormatMinimal(example),
                FewShotFormat.Numbered => FormatNumbered(example, number),
                FewShotFormat.Xml => FormatXml(example),
                _ => FormatLabeled(example)
            };
        }

        private string FormatLabeled(FewShotExample ex)
        {
            var sb = new StringBuilder();
            if (ex.Label != null)
                sb.AppendLine($"[{ex.Label}]");
            sb.AppendLine($"{_inputLabel}: {ex.Input}");
            sb.Append($"{_outputLabel}: {ex.Output}");
            return sb.ToString();
        }

        private string FormatChatStyle(FewShotExample ex)
        {
            var sb = new StringBuilder();
            if (ex.Label != null)
                sb.AppendLine($"[{ex.Label}]");
            sb.AppendLine($"User: {ex.Input}");
            sb.Append($"Assistant: {ex.Output}");
            return sb.ToString();
        }

        private string FormatMinimal(FewShotExample ex)
        {
            var sb = new StringBuilder();
            if (ex.Label != null)
                sb.Append($"[{ex.Label}] ");
            sb.Append($"{ex.Input} => {ex.Output}");
            return sb.ToString();
        }

        private string FormatNumbered(FewShotExample ex, int number)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Example {number}:");
            if (ex.Label != null)
                sb.AppendLine($"  Category: {ex.Label}");
            sb.AppendLine($"  {_inputLabel}: {ex.Input}");
            sb.Append($"  {_outputLabel}: {ex.Output}");
            return sb.ToString();
        }

        private string FormatXml(FewShotExample ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<example>");
            if (ex.Label != null)
                sb.AppendLine($"  <label>{EscapeXml(ex.Label)}</label>");
            sb.AppendLine($"  <input>{EscapeXml(ex.Input)}</input>");
            sb.AppendLine($"  <output>{EscapeXml(ex.Output)}</output>");
            sb.Append("</example>");
            return sb.ToString();
        }

        private string FormatQuery(string query)
        {
            return _format switch
            {
                FewShotFormat.Labeled => $"{_inputLabel}: {query}\n{_outputLabel}:",
                FewShotFormat.ChatStyle => $"User: {query}\nAssistant:",
                FewShotFormat.Minimal => $"{query} =>",
                FewShotFormat.Numbered => $"Now answer:\n  {_inputLabel}: {query}\n  {_outputLabel}:",
                FewShotFormat.Xml => $"<query>\n  <input>{EscapeXml(query)}</input>\n</query>",
                _ => $"{_inputLabel}: {query}\n{_outputLabel}:"
            };
        }

        private static string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        // ──────────── Serialization ────────────

        /// <summary>Serialize the builder to a JSON string.</summary>
        public string ToJson()
        {
            var dto = new FewShotDto
            {
                TaskDescription = _taskDescription,
                SystemContext = _systemContext,
                InputLabel = _inputLabel,
                OutputLabel = _outputLabel,
                Separator = _separator,
                Format = _format,
                Examples = _examples.ToList()
            };
            return JsonSerializer.Serialize(dto, SerializationGuards.WriteWithEnums);
        }

        /// <summary>Deserialize a builder from a JSON string.</summary>
        public static FewShotBuilder FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));

            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var dto = JsonSerializer.Deserialize<FewShotDto>(json, SerializationGuards.ReadWithEnums);

            if (dto == null)
                throw new JsonException("Failed to deserialize FewShotBuilder.");

            if (dto.Examples != null && dto.Examples.Count > MaxExamples)
                throw new JsonException($"Too many examples in JSON: {dto.Examples.Count} (max {MaxExamples}).");

            var builder = new FewShotBuilder(dto.TaskDescription);
            if (dto.SystemContext != null) builder.WithSystemContext(dto.SystemContext);
            if (dto.InputLabel != null) builder.WithInputLabel(dto.InputLabel);
            if (dto.OutputLabel != null) builder.WithOutputLabel(dto.OutputLabel);
            if (dto.Separator != null) builder.WithSeparator(dto.Separator);
            builder.WithFormat(dto.Format);

            if (dto.Examples != null)
            {
                foreach (var ex in dto.Examples)
                    builder.AddExample(ex);
            }

            return builder;
        }

        /// <summary>Save to a JSON file.</summary>
        public async Task SaveToFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            await File.WriteAllTextAsync(path, ToJson());
        }

        /// <summary>Load from a JSON file.</summary>
        public static async Task<FewShotBuilder> LoadFromFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            string json = await File.ReadAllTextAsync(path);
            return FromJson(json);
        }

        // ──────────── DTO ────────────

        private class FewShotDto
        {
            [JsonPropertyName("taskDescription")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? TaskDescription { get; set; }

            [JsonPropertyName("systemContext")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? SystemContext { get; set; }

            [JsonPropertyName("inputLabel")]
            public string? InputLabel { get; set; }

            [JsonPropertyName("outputLabel")]
            public string? OutputLabel { get; set; }

            [JsonPropertyName("separator")]
            public string? Separator { get; set; }

            [JsonPropertyName("format")]
            public FewShotFormat Format { get; set; }

            [JsonPropertyName("examples")]
            public List<FewShotExample>? Examples { get; set; }
        }
    }
}
