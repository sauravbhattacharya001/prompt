namespace Prompt
{
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Fluent builder for constructing structured prompts from semantic sections.
    /// Composes persona, context, task, constraints, examples, and output format
    /// into well-organized prompt text — no template syntax needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="PromptTemplate"/> which uses {{variable}} substitution,
    /// PromptComposer builds prompts from meaningful sections that map to how
    /// humans think about prompt engineering: who the AI should be (persona),
    /// what it knows (context), what to do (task), and how to respond (format).
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var prompt = PromptComposer.Create()
    ///     .WithPersona("You are a senior code reviewer")
    ///     .WithContext("The user is building a REST API in C#")
    ///     .WithTask("Review the code for security issues")
    ///     .WithConstraint("Be concise")
    ///     .WithConstraint("Focus on OWASP Top 10")
    ///     .WithOutputFormat(OutputSection.BulletList)
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptComposer
    {
        // ---- Limits ----

        /// <summary>Maximum number of custom sections allowed.</summary>
        public const int MaxSections = 20;

        /// <summary>Maximum number of constraints allowed.</summary>
        public const int MaxConstraints = 15;

        /// <summary>Maximum number of examples allowed.</summary>
        public const int MaxExamples = 10;

        /// <summary>Maximum character length for any single section.</summary>
        public const int MaxSectionLength = 50_000;

        /// <summary>Maximum total character length of the built prompt.</summary>
        public const int MaxTotalLength = 200_000;

        // ---- Internal state ----
        private string? _persona;
        private string? _context;
        private string? _task;
        private readonly List<string> _constraints = new();
        private readonly List<(string Input, string Output)> _examples = new();
        private OutputSection? _outputFormat;
        private readonly List<(string Label, string Content)> _customSections = new();
        private string _sectionSeparator = "\n\n";
        private bool _useMarkdownHeaders = false;
        private string? _closingInstruction;

        // Private constructor — use Create()
        private PromptComposer() { }

        /// <summary>Creates a new PromptComposer instance.</summary>
        public static PromptComposer Create() => new PromptComposer();

        /// <summary>Sets the AI persona / role.</summary>
        /// <param name="persona">The persona text describing who the AI should be.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when persona is null, empty, or exceeds <see cref="MaxSectionLength"/>.</exception>
        public PromptComposer WithPersona(string persona)
        {
            ValidateSection(persona, nameof(persona));
            _persona = persona.Trim();
            return this;
        }

        /// <summary>Sets background context the AI needs to know.</summary>
        /// <param name="context">The context text providing background information.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when context is null, empty, or exceeds <see cref="MaxSectionLength"/>.</exception>
        public PromptComposer WithContext(string context)
        {
            ValidateSection(context, nameof(context));
            _context = context.Trim();
            return this;
        }

        /// <summary>Sets the primary task/instruction.</summary>
        /// <param name="task">The task text describing what the AI should do.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when task is null, empty, or exceeds <see cref="MaxSectionLength"/>.</exception>
        public PromptComposer WithTask(string task)
        {
            ValidateSection(task, nameof(task));
            _task = task.Trim();
            return this;
        }

        /// <summary>Adds a constraint/rule the AI should follow.</summary>
        /// <param name="constraint">The constraint text.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when constraint is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when exceeding <see cref="MaxConstraints"/>.</exception>
        public PromptComposer WithConstraint(string constraint)
        {
            if (string.IsNullOrWhiteSpace(constraint))
                throw new ArgumentException("Constraint cannot be empty.", nameof(constraint));
            if (_constraints.Count >= MaxConstraints)
                throw new InvalidOperationException($"Cannot exceed {MaxConstraints} constraints.");
            _constraints.Add(constraint.Trim());
            return this;
        }

        /// <summary>Adds multiple constraints at once.</summary>
        /// <param name="constraints">The constraints to add.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when any constraint is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when exceeding <see cref="MaxConstraints"/>.</exception>
        public PromptComposer WithConstraints(params string[] constraints)
        {
            foreach (var c in constraints) WithConstraint(c);
            return this;
        }

        /// <summary>Adds an input/output example for few-shot prompting.</summary>
        /// <param name="input">The example input text.</param>
        /// <param name="output">The example output text.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when input or output is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when exceeding <see cref="MaxExamples"/>.</exception>
        public PromptComposer WithExample(string input, string output)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Example input cannot be empty.", nameof(input));
            if (string.IsNullOrWhiteSpace(output))
                throw new ArgumentException("Example output cannot be empty.", nameof(output));
            if (_examples.Count >= MaxExamples)
                throw new InvalidOperationException($"Cannot exceed {MaxExamples} examples.");
            _examples.Add((input.Trim(), output.Trim()));
            return this;
        }

        /// <summary>Sets the desired output format.</summary>
        /// <param name="format">The output format to use.</param>
        /// <returns>This composer for fluent chaining.</returns>
        public PromptComposer WithOutputFormat(OutputSection format)
        {
            _outputFormat = format;
            return this;
        }

        /// <summary>Adds a custom named section.</summary>
        /// <param name="label">The section label/title.</param>
        /// <param name="content">The section content.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when label or content is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when exceeding <see cref="MaxSections"/>.</exception>
        public PromptComposer WithSection(string label, string content)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Section label cannot be empty.", nameof(label));
            ValidateSection(content, label);
            if (_customSections.Count >= MaxSections)
                throw new InvalidOperationException($"Cannot exceed {MaxSections} custom sections.");
            _customSections.Add((label.Trim(), content.Trim()));
            return this;
        }

        /// <summary>Sets the separator between sections (default: double newline).</summary>
        /// <param name="separator">The separator string, or null to reset to default.</param>
        /// <returns>This composer for fluent chaining.</returns>
        public PromptComposer WithSeparator(string separator)
        {
            _sectionSeparator = separator ?? "\n\n";
            return this;
        }

        /// <summary>Enables markdown headers (## Section) for each section.</summary>
        /// <param name="enabled">Whether to enable markdown headers.</param>
        /// <returns>This composer for fluent chaining.</returns>
        public PromptComposer WithMarkdownHeaders(bool enabled = true)
        {
            _useMarkdownHeaders = enabled;
            return this;
        }

        /// <summary>Sets a closing instruction appended at the end.</summary>
        /// <param name="instruction">The closing instruction text.</param>
        /// <returns>This composer for fluent chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when instruction is null or empty.</exception>
        public PromptComposer WithClosingInstruction(string instruction)
        {
            if (string.IsNullOrWhiteSpace(instruction))
                throw new ArgumentException("Closing instruction cannot be empty.", nameof(instruction));
            _closingInstruction = instruction.Trim();
            return this;
        }

        /// <summary>Builds the prompt string from all configured sections.</summary>
        /// <returns>The composed prompt string.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the built prompt exceeds <see cref="MaxTotalLength"/>.</exception>
        public string Build()
        {
            var sections = new List<string>();

            if (_persona != null)
                sections.Add(FormatSection("Persona", _persona));

            if (_context != null)
                sections.Add(FormatSection("Context", _context));

            if (_task != null)
                sections.Add(FormatSection("Task", _task));

            if (_constraints.Count > 0)
            {
                var constraintList = string.Join("\n", _constraints.Select((c, i) => $"- {c}"));
                sections.Add(FormatSection("Constraints", constraintList));
            }

            if (_examples.Count > 0)
            {
                var exampleTexts = _examples.Select((ex, i) =>
                    $"Example {i + 1}:\nInput: {ex.Input}\nOutput: {ex.Output}");
                sections.Add(FormatSection("Examples", string.Join("\n\n", exampleTexts)));
            }

            if (_outputFormat.HasValue)
            {
                sections.Add(FormatSection("Output Format", GetOutputFormatInstruction(_outputFormat.Value)));
            }

            foreach (var (label, content) in _customSections)
            {
                sections.Add(FormatSection(label, content));
            }

            if (_closingInstruction != null)
                sections.Add(_closingInstruction);

            var result = string.Join(_sectionSeparator, sections);

            if (result.Length > MaxTotalLength)
                throw new InvalidOperationException(
                    $"Built prompt exceeds maximum length of {MaxTotalLength:N0} characters.");

            return result;
        }

        /// <summary>Builds the prompt and estimates its token count.</summary>
        /// <returns>A tuple of the prompt string and estimated token count.</returns>
        public (string Prompt, int EstimatedTokens) BuildWithTokenEstimate()
        {
            var prompt = Build();
            var tokens = PromptGuard.EstimateTokens(prompt);
            return (prompt, tokens);
        }

        /// <summary>Returns section count (including built-in sections that are set).</summary>
        /// <returns>The number of sections currently configured.</returns>
        public int SectionCount()
        {
            int count = 0;
            if (_persona != null) count++;
            if (_context != null) count++;
            if (_task != null) count++;
            if (_constraints.Count > 0) count++;
            if (_examples.Count > 0) count++;
            if (_outputFormat.HasValue) count++;
            count += _customSections.Count;
            if (_closingInstruction != null) count++;
            return count;
        }

        /// <summary>Creates a deep copy of this composer for branching.</summary>
        /// <returns>A new PromptComposer with the same configuration.</returns>
        public PromptComposer Clone()
        {
            var clone = new PromptComposer
            {
                _persona = _persona,
                _context = _context,
                _task = _task,
                _outputFormat = _outputFormat,
                _sectionSeparator = _sectionSeparator,
                _useMarkdownHeaders = _useMarkdownHeaders,
                _closingInstruction = _closingInstruction,
            };
            clone._constraints.AddRange(_constraints);
            clone._examples.AddRange(_examples);
            clone._customSections.AddRange(_customSections);
            return clone;
        }

        /// <summary>Resets the composer to empty state.</summary>
        /// <returns>This composer for fluent chaining.</returns>
        public PromptComposer Reset()
        {
            _persona = null;
            _context = null;
            _task = null;
            _constraints.Clear();
            _examples.Clear();
            _outputFormat = null;
            _customSections.Clear();
            _sectionSeparator = "\n\n";
            _useMarkdownHeaders = false;
            _closingInstruction = null;
            return this;
        }

        /// <summary>Serializes the composer configuration to JSON.</summary>
        /// <returns>A JSON string representing this composer's configuration.</returns>
        public string ToJson()
        {
            var data = new Dictionary<string, object?>
            {
                ["persona"] = _persona,
                ["context"] = _context,
                ["task"] = _task,
                ["constraints"] = _constraints.Count > 0 ? _constraints.ToList() : null,
                ["examples"] = _examples.Count > 0
                    ? _examples.Select(e => new { input = e.Input, output = e.Output }).ToList()
                    : null,
                ["outputFormat"] = _outputFormat?.ToString(),
                ["customSections"] = _customSections.Count > 0
                    ? _customSections.Select(s => new { label = s.Label, content = s.Content }).ToList()
                    : null,
                ["sectionSeparator"] = _sectionSeparator != "\n\n" ? _sectionSeparator : null,
                ["useMarkdownHeaders"] = _useMarkdownHeaders ? true : null,
                ["closingInstruction"] = _closingInstruction,
            };

            // Remove null entries
            var filtered = data.Where(kv => kv.Value != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return JsonSerializer.Serialize(filtered, SerializationGuards.WriteCamelCase);
        }

        /// <summary>Deserializes a composer from JSON.</summary>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>A new PromptComposer configured from the JSON.</returns>
        /// <exception cref="ArgumentException">Thrown when json is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the JSON payload is too large.</exception>
        public static PromptComposer FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty.", nameof(json));

            SerializationGuards.ThrowIfPayloadTooLarge(json);

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var composer = new PromptComposer();

            if (root.TryGetProperty("persona", out var persona) && persona.ValueKind == JsonValueKind.String)
                composer._persona = persona.GetString()?.Trim();

            if (root.TryGetProperty("context", out var context) && context.ValueKind == JsonValueKind.String)
                composer._context = context.GetString()?.Trim();

            if (root.TryGetProperty("task", out var task) && task.ValueKind == JsonValueKind.String)
                composer._task = task.GetString()?.Trim();

            if (root.TryGetProperty("constraints", out var constraints) && constraints.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in constraints.EnumerateArray())
                {
                    if (c.ValueKind == JsonValueKind.String)
                        composer._constraints.Add(c.GetString()!.Trim());
                }
            }

            if (root.TryGetProperty("examples", out var examples) && examples.ValueKind == JsonValueKind.Array)
            {
                foreach (var ex in examples.EnumerateArray())
                {
                    if (ex.TryGetProperty("input", out var inp) && ex.TryGetProperty("output", out var outp))
                    {
                        composer._examples.Add((inp.GetString()!.Trim(), outp.GetString()!.Trim()));
                    }
                }
            }

            if (root.TryGetProperty("outputFormat", out var fmt) && fmt.ValueKind == JsonValueKind.String)
            {
                if (Enum.TryParse<OutputSection>(fmt.GetString(), true, out var parsed))
                    composer._outputFormat = parsed;
            }

            if (root.TryGetProperty("customSections", out var sections) && sections.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sections.EnumerateArray())
                {
                    if (s.TryGetProperty("label", out var lbl) && s.TryGetProperty("content", out var cnt))
                    {
                        composer._customSections.Add((lbl.GetString()!.Trim(), cnt.GetString()!.Trim()));
                    }
                }
            }

            if (root.TryGetProperty("sectionSeparator", out var sep) && sep.ValueKind == JsonValueKind.String)
                composer._sectionSeparator = sep.GetString() ?? "\n\n";

            if (root.TryGetProperty("useMarkdownHeaders", out var mkd) && mkd.ValueKind == JsonValueKind.True)
                composer._useMarkdownHeaders = true;

            if (root.TryGetProperty("closingInstruction", out var closing) && closing.ValueKind == JsonValueKind.String)
                composer._closingInstruction = closing.GetString()?.Trim();

            return composer;
        }

        // ---- Private helpers ----

        private string FormatSection(string label, string content)
        {
            if (_useMarkdownHeaders)
                return $"## {label}\n{content}";
            return content;
        }

        private static void ValidateSection(string text, string name)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException($"{name} cannot be empty.", name);
            if (text.Length > MaxSectionLength)
                throw new ArgumentException(
                    $"{name} exceeds maximum length of {MaxSectionLength:N0} characters.", name);
        }

        private static string GetOutputFormatInstruction(OutputSection format)
        {
            return format switch
            {
                OutputSection.JSON => "Respond with valid JSON only. No additional text or explanations.",
                OutputSection.BulletList => "Respond using bullet points (- item). One point per line.",
                OutputSection.NumberedList => "Respond using a numbered list (1. item). One item per line.",
                OutputSection.Table => "Respond using a markdown table with headers.",
                OutputSection.StepByStep => "Respond with a step-by-step walkthrough, numbering each step.",
                OutputSection.Paragraph => "Respond in well-structured paragraphs.",
                OutputSection.OneLine => "Respond with a single line. No explanations.",
                OutputSection.CodeBlock => "Respond with code only, wrapped in a markdown code block.",
                OutputSection.XML => "Respond with valid XML only. No additional text.",
                OutputSection.CSV => "Respond with CSV data including a header row.",
                OutputSection.YAML => "Respond with valid YAML only. No additional text.",
                _ => ""
            };
        }

        // ---- Preset builders ----

        /// <summary>Creates a code review prompt composer with sensible defaults.</summary>
        /// <returns>A PromptComposer configured for code review.</returns>
        public static PromptComposer CodeReview()
        {
            return Create()
                .WithPersona("You are an expert code reviewer with deep knowledge of software engineering best practices, security, and performance.")
                .WithConstraint("Focus on bugs, security issues, and performance problems")
                .WithConstraint("Suggest specific improvements with code examples")
                .WithConstraint("Be concise — skip obvious observations")
                .WithOutputFormat(OutputSection.BulletList);
        }

        /// <summary>Creates a text summarization prompt composer.</summary>
        /// <returns>A PromptComposer configured for summarization.</returns>
        public static PromptComposer Summarize()
        {
            return Create()
                .WithPersona("You are an expert at distilling complex information into clear, concise summaries.")
                .WithConstraint("Preserve key facts and conclusions")
                .WithConstraint("Omit filler and redundant information")
                .WithConstraint("Use plain language")
                .WithOutputFormat(OutputSection.Paragraph);
        }

        /// <summary>Creates a data extraction prompt composer.</summary>
        /// <returns>A PromptComposer configured for data extraction.</returns>
        public static PromptComposer Extract()
        {
            return Create()
                .WithPersona("You are a precise data extraction specialist.")
                .WithConstraint("Extract only the requested information")
                .WithConstraint("Return null for missing fields")
                .WithConstraint("Do not infer or guess values")
                .WithOutputFormat(OutputSection.JSON);
        }

        /// <summary>Creates a translation prompt composer.</summary>
        /// <returns>A PromptComposer configured for translation.</returns>
        public static PromptComposer Translate()
        {
            return Create()
                .WithPersona("You are a professional translator.")
                .WithConstraint("Preserve meaning, tone, and formatting")
                .WithConstraint("Use natural phrasing in the target language")
                .WithConstraint("Transliterate proper nouns when appropriate")
                .WithOutputFormat(OutputSection.Paragraph);
        }
    }

    /// <summary>Output format options for PromptComposer.</summary>
    public enum OutputSection
    {
        /// <summary>JSON format output.</summary>
        JSON,
        /// <summary>Bullet list format output.</summary>
        BulletList,
        /// <summary>Numbered list format output.</summary>
        NumberedList,
        /// <summary>Markdown table format output.</summary>
        Table,
        /// <summary>Step-by-step format output.</summary>
        StepByStep,
        /// <summary>Paragraph format output.</summary>
        Paragraph,
        /// <summary>Single line format output.</summary>
        OneLine,
        /// <summary>Code block format output.</summary>
        CodeBlock,
        /// <summary>XML format output.</summary>
        XML,
        /// <summary>CSV format output.</summary>
        CSV,
        /// <summary>YAML format output.</summary>
        YAML
    }
}
