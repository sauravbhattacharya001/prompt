namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a single step in a <see cref="PromptChain"/>. Each step
    /// has a <see cref="PromptTemplate"/> and an output variable name.
    /// When the step executes, the template is rendered with all accumulated
    /// variables, sent to the model, and the response is stored under the
    /// output variable name for subsequent steps.
    /// </summary>
    public class ChainStep
    {
        /// <summary>
        /// Creates a new chain step.
        /// </summary>
        /// <param name="name">
        /// A descriptive name for this step (e.g., "summarize", "translate").
        /// </param>
        /// <param name="template">
        /// The prompt template to render and send. Can reference variables
        /// from previous steps via <c>{{outputVar}}</c>.
        /// </param>
        /// <param name="outputVariable">
        /// The variable name under which this step's response will be stored.
        /// Subsequent steps can reference it as <c>{{outputVariable}}</c>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> or <paramref name="outputVariable"/>
        /// is null or empty, or when <paramref name="template"/> is null.
        /// </exception>
        public ChainStep(string name, PromptTemplate template, string outputVariable)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Step name cannot be null or empty.", nameof(name));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (string.IsNullOrWhiteSpace(outputVariable))
                throw new ArgumentException(
                    "Output variable cannot be null or empty.", nameof(outputVariable));

            Name = name;
            Template = template;
            OutputVariable = outputVariable;
        }

        /// <summary>Gets the step's descriptive name.</summary>
        public string Name { get; }

        /// <summary>Gets the prompt template for this step.</summary>
        public PromptTemplate Template { get; }

        /// <summary>
        /// Gets the variable name where this step's output is stored.
        /// </summary>
        public string OutputVariable { get; }
    }

    /// <summary>
    /// Records the result of a single step after execution.
    /// </summary>
    public class StepResult
    {
        internal StepResult(string stepName, string outputVariable,
            string renderedPrompt, string? response, TimeSpan elapsed)
        {
            StepName = stepName;
            OutputVariable = outputVariable;
            RenderedPrompt = renderedPrompt;
            Response = response;
            Elapsed = elapsed;
        }

        /// <summary>Gets the step name.</summary>
        public string StepName { get; }

        /// <summary>Gets the output variable name.</summary>
        public string OutputVariable { get; }

        /// <summary>Gets the fully rendered prompt that was sent.</summary>
        public string RenderedPrompt { get; }

        /// <summary>Gets the model's response (null if none).</summary>
        public string? Response { get; }

        /// <summary>Gets how long this step took to execute.</summary>
        public TimeSpan Elapsed { get; }
    }

    /// <summary>
    /// The result of running a complete <see cref="PromptChain"/>.
    /// Contains all step results and the accumulated variable context.
    /// </summary>
    public class ChainResult
    {
        internal ChainResult(
            List<StepResult> steps,
            Dictionary<string, string> variables,
            TimeSpan totalElapsed)
        {
            Steps = steps.AsReadOnly();
            Variables = new Dictionary<string, string>(
                variables, StringComparer.OrdinalIgnoreCase);
            TotalElapsed = totalElapsed;
        }

        /// <summary>Gets the ordered list of step results.</summary>
        public IReadOnlyList<StepResult> Steps { get; }

        /// <summary>
        /// Gets all accumulated variables (inputs + outputs from each step).
        /// </summary>
        public IReadOnlyDictionary<string, string> Variables { get; }

        /// <summary>Gets the total execution time for the chain.</summary>
        public TimeSpan TotalElapsed { get; }

        /// <summary>
        /// Gets the final step's response (convenience shortcut).
        /// Returns <c>null</c> if there are no steps or the last step had no response.
        /// </summary>
        public string? FinalResponse =>
            Steps.Count > 0 ? Steps[Steps.Count - 1].Response : null;

        /// <summary>
        /// Gets the output of a specific step by its output variable name.
        /// </summary>
        /// <param name="variableName">The output variable name to look up.</param>
        /// <returns>The step's response, or <c>null</c> if not found.</returns>
        public string? GetOutput(string variableName)
        {
            if (Variables is Dictionary<string, string> dict)
            {
                dict.TryGetValue(variableName, out var value);
                return value;
            }
            return Variables.ContainsKey(variableName) ? Variables[variableName] : null;
        }

        /// <summary>
        /// Serializes the chain result to a JSON string for logging or analysis.
        /// </summary>
        /// <param name="indented">Whether to format with indentation (default true).</param>
        /// <returns>A JSON string representing the chain result.</returns>
        public string ToJson(bool indented = true)
        {
            var data = new ChainResultData
            {
                TotalElapsedMs = (long)TotalElapsed.TotalMilliseconds,
                Steps = Steps.Select(s => new StepResultData
                {
                    StepName = s.StepName,
                    OutputVariable = s.OutputVariable,
                    RenderedPrompt = s.RenderedPrompt,
                    Response = s.Response,
                    ElapsedMs = (long)s.Elapsed.TotalMilliseconds
                }).ToList(),
                Variables = new Dictionary<string, string>(
                    (IDictionary<string, string>)Variables)
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        internal class ChainResultData
        {
            [JsonPropertyName("totalElapsedMs")]
            public long TotalElapsedMs { get; set; }

            [JsonPropertyName("steps")]
            public List<StepResultData> Steps { get; set; } = new();

            [JsonPropertyName("variables")]
            public Dictionary<string, string> Variables { get; set; } = new();
        }

        internal class StepResultData
        {
            [JsonPropertyName("stepName")]
            public string StepName { get; set; } = "";

            [JsonPropertyName("outputVariable")]
            public string OutputVariable { get; set; } = "";

            [JsonPropertyName("renderedPrompt")]
            public string RenderedPrompt { get; set; } = "";

            [JsonPropertyName("response")]
            public string? Response { get; set; }

            [JsonPropertyName("elapsedMs")]
            public long ElapsedMs { get; set; }
        }
    }

    /// <summary>
    /// Chains multiple prompt steps together, where each step's output
    /// feeds into subsequent steps as template variables. This enables
    /// common LLM patterns like summarize-then-translate, extract-then-analyze,
    /// or any multi-step reasoning pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var chain = new PromptChain()
    ///     .AddStep("summarize",
    ///         new PromptTemplate("Summarize this text in 2 sentences: {{text}}"),
    ///         "summary")
    ///     .AddStep("translate",
    ///         new PromptTemplate("Translate to French: {{summary}}"),
    ///         "french")
    ///     .AddStep("keywords",
    ///         new PromptTemplate("Extract 5 keywords from: {{summary}}"),
    ///         "keywords");
    ///
    /// var result = await chain.RunAsync(new Dictionary&lt;string, string&gt;
    /// {
    ///     ["text"] = "Your long article text here..."
    /// });
    ///
    /// Console.WriteLine(result.FinalResponse);          // keywords
    /// Console.WriteLine(result.GetOutput("summary"));   // the summary
    /// Console.WriteLine(result.GetOutput("french"));    // the French translation
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptChain
    {
        private readonly List<ChainStep> _steps = new();
        private string? _systemPrompt;
        private int _maxRetries = 3;

        /// <summary>
        /// Creates a new empty prompt chain.
        /// </summary>
        public PromptChain() { }

        /// <summary>
        /// Gets the number of steps in the chain.
        /// </summary>
        public int StepCount => _steps.Count;

        /// <summary>
        /// Gets a read-only view of the steps.
        /// </summary>
        public IReadOnlyList<ChainStep> Steps => _steps.AsReadOnly();

        /// <summary>
        /// Sets the system prompt used for all API calls in the chain.
        /// </summary>
        /// <param name="systemPrompt">The system prompt text.</param>
        /// <returns>This chain instance for fluent chaining.</returns>
        public PromptChain WithSystemPrompt(string? systemPrompt)
        {
            _systemPrompt = systemPrompt;
            return this;
        }

        /// <summary>
        /// Sets the max retries for API calls in the chain.
        /// </summary>
        /// <param name="maxRetries">Maximum retry count (must be non-negative).</param>
        /// <returns>This chain instance for fluent chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="maxRetries"/> is negative.
        /// </exception>
        public PromptChain WithMaxRetries(int maxRetries)
        {
            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries),
                    maxRetries, "maxRetries must be non-negative.");
            _maxRetries = maxRetries;
            return this;
        }

        /// <summary>
        /// Adds a step to the chain.
        /// </summary>
        /// <param name="name">A descriptive name for this step.</param>
        /// <param name="template">The prompt template.</param>
        /// <param name="outputVariable">
        /// Variable name to store this step's output under.
        /// </param>
        /// <returns>This chain instance for fluent chaining.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="outputVariable"/> is already used
        /// by another step in this chain.
        /// </exception>
        public PromptChain AddStep(
            string name,
            PromptTemplate template,
            string outputVariable)
        {
            // Validate uniqueness of output variable names
            foreach (var existing in _steps)
            {
                if (string.Equals(existing.OutputVariable, outputVariable,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Output variable '{outputVariable}' is already used by step '{existing.Name}'. " +
                        "Each step must have a unique output variable.",
                        nameof(outputVariable));
                }
            }

            _steps.Add(new ChainStep(name, template, outputVariable));
            return this;
        }

        /// <summary>
        /// Executes the chain sequentially. Each step's template is rendered
        /// with accumulated variables, sent to Azure OpenAI, and the response
        /// is stored under the step's output variable for subsequent steps.
        /// </summary>
        /// <param name="initialVariables">
        /// Initial variables to seed the chain with. These are available
        /// to all steps and can be referenced as <c>{{name}}</c>.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="ChainResult"/> containing all step results
        /// and accumulated variables.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the chain has no steps.
        /// </exception>
        public async Task<ChainResult> RunAsync(
            Dictionary<string, string>? initialVariables = null,
            CancellationToken cancellationToken = default)
        {
            if (_steps.Count == 0)
                throw new InvalidOperationException(
                    "Cannot run an empty chain. Add at least one step with AddStep().");

            var variables = initialVariables != null
                ? new Dictionary<string, string>(initialVariables, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var stepResults = new List<StepResult>();
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepWatch = System.Diagnostics.Stopwatch.StartNew();

                // Render the template with all accumulated variables (non-strict
                // so missing variables from future steps don't throw)
                string rendered = step.Template.Render(variables, strict: false);

                // Send to Azure OpenAI
                string? response = await Main.GetResponseAsync(
                    rendered, _systemPrompt, _maxRetries, cancellationToken);

                stepWatch.Stop();

                // Store the response as a variable for subsequent steps
                if (response != null)
                {
                    variables[step.OutputVariable] = response;
                }

                stepResults.Add(new StepResult(
                    step.Name,
                    step.OutputVariable,
                    rendered,
                    response,
                    stepWatch.Elapsed));
            }

            totalWatch.Stop();

            return new ChainResult(stepResults, variables, totalWatch.Elapsed);
        }

        /// <summary>
        /// Validates that all required template variables can be satisfied
        /// by initial variables and upstream step outputs. Does not call
        /// the API — purely static analysis.
        /// </summary>
        /// <param name="initialVariables">
        /// The initial variables that will be provided at run time.
        /// </param>
        /// <returns>
        /// A list of validation errors. Empty means the chain is valid.
        /// </returns>
        public List<string> Validate(
            Dictionary<string, string>? initialVariables = null)
        {
            var errors = new List<string>();

            if (_steps.Count == 0)
            {
                errors.Add("Chain has no steps.");
                return errors;
            }

            // Track which variables are available at each step
            var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (initialVariables != null)
            {
                foreach (var key in initialVariables.Keys)
                    available.Add(key);
            }

            foreach (var step in _steps)
            {
                // Get required variables for this step's template
                var required = step.Template.GetRequiredVariables();

                foreach (var v in required)
                {
                    if (!available.Contains(v))
                    {
                        errors.Add(
                            $"Step '{step.Name}': variable '{v}' is required " +
                            $"but not available (not in initial variables or " +
                            $"produced by a prior step).");
                    }
                }

                // This step's output becomes available for subsequent steps
                available.Add(step.OutputVariable);
            }

            return errors;
        }

        /// <summary>
        /// Serializes the chain definition to JSON (steps and configuration,
        /// not results). Can be used to save chain definitions for reuse.
        /// </summary>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <returns>A JSON string representing the chain definition.</returns>
        public string ToJson(bool indented = true)
        {
            var data = new ChainData
            {
                SystemPrompt = _systemPrompt,
                MaxRetries = _maxRetries,
                Steps = _steps.Select(s => new ChainStepData
                {
                    Name = s.Name,
                    Template = s.Template.Template,
                    Defaults = s.Template.Defaults.Count > 0
                        ? new Dictionary<string, string>(
                            (IDictionary<string, string>)s.Template.Defaults)
                        : null,
                    OutputVariable = s.OutputVariable
                }).ToList()
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Deserializes a chain definition from JSON.
        /// </summary>
        /// <param name="json">The JSON string.</param>
        /// <returns>A new <see cref="PromptChain"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="json"/> is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the JSON structure is invalid.
        /// </exception>
        public static PromptChain FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON string cannot be null or empty.", nameof(json));

            var data = JsonSerializer.Deserialize<ChainData>(json,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

            if (data?.Steps == null || data.Steps.Count == 0)
                throw new InvalidOperationException(
                    "Invalid chain JSON: missing or empty steps array.");

            var chain = new PromptChain();

            if (data.SystemPrompt != null)
                chain.WithSystemPrompt(data.SystemPrompt);

            chain.WithMaxRetries(data.MaxRetries);

            foreach (var stepData in data.Steps)
            {
                if (string.IsNullOrWhiteSpace(stepData.Name)
                    || string.IsNullOrWhiteSpace(stepData.Template)
                    || string.IsNullOrWhiteSpace(stepData.OutputVariable))
                {
                    throw new InvalidOperationException(
                        "Invalid chain step: name, template, and outputVariable are required.");
                }

                var template = new PromptTemplate(stepData.Template, stepData.Defaults);
                chain.AddStep(stepData.Name, template, stepData.OutputVariable);
            }

            return chain;
        }

        /// <summary>
        /// Saves the chain definition to a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the output file.</param>
        /// <param name="indented">Whether to format with indentation.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task SaveToFileAsync(
            string filePath,
            bool indented = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            string json = ToJson(indented);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        /// <summary>
        /// Loads a chain definition from a JSON file.
        /// </summary>
        /// <param name="filePath">Path to the JSON file.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A new <see cref="PromptChain"/> instance.</returns>
        public static async Task<PromptChain> LoadFromFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Chain file not found: {filePath}", filePath);

            string json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return FromJson(json);
        }

        // ──────────────── Serialization DTOs ────────────────

        internal class ChainData
        {
            [JsonPropertyName("systemPrompt")]
            public string? SystemPrompt { get; set; }

            [JsonPropertyName("maxRetries")]
            public int MaxRetries { get; set; } = 3;

            [JsonPropertyName("steps")]
            public List<ChainStepData> Steps { get; set; } = new();
        }

        internal class ChainStepData
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("template")]
            public string Template { get; set; } = "";

            [JsonPropertyName("defaults")]
            public Dictionary<string, string>? Defaults { get; set; }

            [JsonPropertyName("outputVariable")]
            public string OutputVariable { get; set; } = "";
        }
    }
}
