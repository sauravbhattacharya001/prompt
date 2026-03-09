namespace Prompt
{
    using System.Text.Json;

    /// <summary>
    /// Pricing tier for an LLM model, specifying cost per million tokens
    /// for input and output separately.
    /// </summary>
    public record ModelPricing(
        string ModelId,
        string Provider,
        string DisplayName,
        decimal InputCostPerMillionTokens,
        decimal OutputCostPerMillionTokens,
        int ContextWindow,
        int MaxOutputTokens
    )
    {
        /// <summary>
        /// Calculate the cost for a given number of input tokens.
        /// </summary>
        public decimal InputCost(int tokens) =>
            tokens <= 0 ? 0m : InputCostPerMillionTokens * tokens / 1_000_000m;

        /// <summary>
        /// Calculate the cost for a given number of output tokens.
        /// </summary>
        public decimal OutputCost(int tokens) =>
            tokens <= 0 ? 0m : OutputCostPerMillionTokens * tokens / 1_000_000m;

        /// <summary>
        /// Calculate the total cost for a prompt + response.
        /// </summary>
        public decimal TotalCost(int inputTokens, int outputTokens) =>
            InputCost(inputTokens) + OutputCost(outputTokens);
    }

    /// <summary>
    /// Result of a cost estimation for a single model.
    /// </summary>
    public record CostEstimate(
        ModelPricing Model,
        int InputTokens,
        int EstimatedOutputTokens,
        decimal InputCost,
        decimal OutputCost,
        decimal TotalCost,
        double ContextUsagePercent,
        bool ExceedsContext,
        string? Warning
    );

    /// <summary>
    /// Comparative cost report across multiple models.
    /// </summary>
    public class CostReport
    {
        /// <summary>The prompt text that was analyzed.</summary>
        public string PromptText { get; init; } = "";

        /// <summary>Estimated input token count.</summary>
        public int InputTokens { get; init; }

        /// <summary>Estimated output token count used for calculations.</summary>
        public int EstimatedOutputTokens { get; init; }

        /// <summary>Individual cost estimates per model, sorted cheapest first.</summary>
        public List<CostEstimate> Estimates { get; init; } = new();

        /// <summary>The cheapest viable model (that fits the context window).</summary>
        public CostEstimate? CheapestViable =>
            Estimates.FirstOrDefault(e => !e.ExceedsContext);

        /// <summary>The most expensive viable model.</summary>
        public CostEstimate? MostExpensiveViable =>
            Estimates.LastOrDefault(e => !e.ExceedsContext);

        /// <summary>
        /// Cost savings if using the cheapest vs most expensive viable model.
        /// </summary>
        public decimal? MaxSavings
        {
            get
            {
                var cheap = CheapestViable;
                var expensive = MostExpensiveViable;
                if (cheap == null || expensive == null || cheap == expensive) return null;
                return expensive.TotalCost - cheap.TotalCost;
            }
        }

        /// <summary>
        /// Number of models where the prompt fits the context window.
        /// </summary>
        public int ViableModelCount => Estimates.Count(e => !e.ExceedsContext);

        /// <summary>
        /// Serialize the report to a JSON string.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var data = new
            {
                inputTokens = InputTokens,
                estimatedOutputTokens = EstimatedOutputTokens,
                viableModels = ViableModelCount,
                cheapest = CheapestViable != null ? new
                {
                    model = CheapestViable.Model.DisplayName,
                    provider = CheapestViable.Model.Provider,
                    totalCost = CheapestViable.TotalCost
                } : null,
                estimates = Estimates.Select(e => new
                {
                    model = e.Model.DisplayName,
                    provider = e.Model.Provider,
                    inputCost = e.InputCost,
                    outputCost = e.OutputCost,
                    totalCost = e.TotalCost,
                    contextUsagePercent = Math.Round(e.ContextUsagePercent, 1),
                    exceedsContext = e.ExceedsContext,
                    warning = e.Warning
                }).ToList()
            };

            return JsonSerializer.Serialize(data, SerializationGuards.WriteOptions(indented));
        }

        /// <summary>
        /// Format the report as a human-readable text table.
        /// </summary>
        public string ToText()
        {
            var lines = new List<string>();
            lines.Add("═══════════════════════════════════════════════════════════════");
            lines.Add("  Prompt Cost Estimator Report");
            lines.Add("═══════════════════════════════════════════════════════════════");
            lines.Add($"  Input tokens:    {InputTokens:N0}");
            lines.Add($"  Output estimate: {EstimatedOutputTokens:N0}");
            lines.Add($"  Viable models:   {ViableModelCount}/{Estimates.Count}");
            lines.Add("");
            lines.Add($"  {"Model",-28} {"Provider",-12} {"Input",-10} {"Output",-10} {"Total",-10} {"Ctx%",-6} {"Status"}");
            lines.Add($"  {"─────",-28} {"────────",-12} {"─────",-10} {"──────",-10} {"─────",-10} {"────",-6} {"──────"}");

            foreach (var e in Estimates)
            {
                var status = e.ExceedsContext ? "⚠ TOO BIG" : "✓";
                if (e.Warning != null && !e.ExceedsContext)
                    status = "⚡ TIGHT";

                lines.Add($"  {e.Model.DisplayName,-28} {e.Model.Provider,-12} ${e.InputCost,-9:F6} ${e.OutputCost,-9:F6} ${e.TotalCost,-9:F6} {e.ContextUsagePercent,5:F1}% {status}");
            }

            lines.Add("");

            if (MaxSavings.HasValue && MaxSavings > 0)
            {
                lines.Add($"  💡 Max savings: ${MaxSavings:F6} by using {CheapestViable!.Model.DisplayName} instead of {MostExpensiveViable!.Model.DisplayName}");
            }

            if (CheapestViable != null)
            {
                lines.Add($"  ✨ Best value: {CheapestViable.Model.DisplayName} ({CheapestViable.Model.Provider}) at ${CheapestViable.TotalCost:F6}");
            }

            lines.Add("═══════════════════════════════════════════════════════════════");

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Estimates the cost of running prompts against various LLM providers.
    /// Maintains a registry of model pricing and provides comparative cost
    /// analysis to help choose the most cost-effective model for a task.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="PromptGuard.EstimateTokens"/> for token counting
    /// (heuristic, not exact BPE). For precise costs, use actual tokenizer
    /// counts from the provider.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var estimator = new PromptCostEstimator();
    /// var report = estimator.Estimate("Explain quantum computing in detail.");
    /// Console.WriteLine(report.ToText());
    /// Console.WriteLine($"Cheapest: {report.CheapestViable?.Model.DisplayName}");
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptCostEstimator
    {
        /// <summary>Maximum number of custom models that can be registered.</summary>
        public const int MaxModels = 100;

        /// <summary>Maximum estimated output tokens allowed.</summary>
        public const int MaxOutputTokens = 1_000_000;

        /// <summary>Catalog version date for built-in pricing data.</summary>
        public const string CatalogVersion = "2026-03-09";

        /// <summary>Default estimated output tokens if not specified.</summary>
        public const int DefaultOutputTokens = 1_000;

        private readonly List<ModelPricing> _models = new();

        /// <summary>
        /// Creates a new cost estimator with built-in model pricing.
        /// </summary>
        public PromptCostEstimator()
        {
            LoadBuiltInModels();
        }

        /// <summary>
        /// Register a custom model with its pricing.
        /// </summary>
        /// <exception cref="ArgumentNullException">If pricing is null.</exception>
        /// <exception cref="ArgumentException">If modelId is empty or duplicate.</exception>
        /// <exception cref="InvalidOperationException">If model limit is reached.</exception>
        public void AddModel(ModelPricing pricing)
        {
            ArgumentNullException.ThrowIfNull(pricing);
            if (string.IsNullOrWhiteSpace(pricing.ModelId))
                throw new ArgumentException("ModelId cannot be empty.");
            if (pricing.InputCostPerMillionTokens < 0)
                throw new ArgumentException("Input cost cannot be negative.");
            if (pricing.OutputCostPerMillionTokens < 0)
                throw new ArgumentException("Output cost cannot be negative.");
            if (pricing.ContextWindow <= 0)
                throw new ArgumentException("Context window must be positive.");
            if (pricing.MaxOutputTokens <= 0)
                throw new ArgumentException("Max output tokens must be positive.");
            if (_models.Count >= MaxModels)
                throw new InvalidOperationException($"Cannot exceed {MaxModels} models.");
            if (_models.Any(m => m.ModelId.Equals(pricing.ModelId, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Duplicate model: {pricing.ModelId}");
            _models.Add(pricing);
        }

        /// <summary>
        /// Remove a model by its ID.
        /// </summary>
        public bool RemoveModel(string modelId) =>
            _models.RemoveAll(m => m.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase)) > 0;

        /// <summary>
        /// Get all registered models.
        /// </summary>
        public IReadOnlyList<ModelPricing> GetModels() => _models.AsReadOnly();

        /// <summary>
        /// Get the number of registered models.
        /// </summary>
        public int ModelCount => _models.Count;

        /// <summary>
        /// Get pricing for a specific model by ID.
        /// </summary>
        public ModelPricing? GetModel(string modelId) =>
            _models.FirstOrDefault(m => m.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Estimate the cost of running a prompt across all registered models.
        /// </summary>
        /// <param name="promptText">The prompt text to estimate.</param>
        /// <param name="estimatedOutputTokens">Expected output length in tokens (default: 1000).</param>
        /// <returns>A <see cref="CostReport"/> with per-model estimates sorted by total cost.</returns>
        /// <exception cref="ArgumentNullException">If promptText is null.</exception>
        /// <exception cref="ArgumentException">If estimatedOutputTokens is invalid.</exception>
        public CostReport Estimate(string promptText, int estimatedOutputTokens = DefaultOutputTokens)
        {
            ArgumentNullException.ThrowIfNull(promptText);
            if (estimatedOutputTokens < 0)
                throw new ArgumentException("Estimated output tokens cannot be negative.");
            if (estimatedOutputTokens > MaxOutputTokens)
                throw new ArgumentException($"Estimated output tokens cannot exceed {MaxOutputTokens}.");

            var inputTokens = PromptGuard.EstimateTokens(promptText);

            var estimates = new List<CostEstimate>();

            foreach (var model in _models)
            {
                var totalInputNeeded = inputTokens + estimatedOutputTokens;
                var exceedsContext = totalInputNeeded > model.ContextWindow;
                var contextUsage = model.ContextWindow > 0
                    ? (double)totalInputNeeded / model.ContextWindow * 100
                    : 100.0;

                var cappedOutput = Math.Min(estimatedOutputTokens, model.MaxOutputTokens);
                var inputCost = model.InputCost(inputTokens);
                var outputCost = model.OutputCost(cappedOutput);

                string? warning = null;
                if (!exceedsContext && contextUsage > 80)
                    warning = "Context window usage above 80%";
                if (estimatedOutputTokens > model.MaxOutputTokens)
                    warning = $"Output capped to model max ({model.MaxOutputTokens})";

                estimates.Add(new CostEstimate(
                    Model: model,
                    InputTokens: inputTokens,
                    EstimatedOutputTokens: cappedOutput,
                    InputCost: inputCost,
                    OutputCost: outputCost,
                    TotalCost: inputCost + outputCost,
                    ContextUsagePercent: contextUsage,
                    ExceedsContext: exceedsContext,
                    Warning: warning
                ));
            }

            estimates.Sort((a, b) => a.TotalCost.CompareTo(b.TotalCost));

            return new CostReport
            {
                PromptText = promptText,
                InputTokens = inputTokens,
                EstimatedOutputTokens = estimatedOutputTokens,
                Estimates = estimates
            };
        }

        /// <summary>
        /// Estimate cost for a pre-counted token amount (when you already know the token count).
        /// </summary>
        /// <param name="inputTokens">Known input token count.</param>
        /// <param name="estimatedOutputTokens">Expected output length in tokens.</param>
        /// <returns>A <see cref="CostReport"/> with per-model estimates.</returns>
        public CostReport EstimateFromTokens(int inputTokens, int estimatedOutputTokens = DefaultOutputTokens)
        {
            if (inputTokens < 0)
                throw new ArgumentException("Input tokens cannot be negative.");
            if (estimatedOutputTokens < 0)
                throw new ArgumentException("Estimated output tokens cannot be negative.");
            if (estimatedOutputTokens > MaxOutputTokens)
                throw new ArgumentException($"Estimated output tokens cannot exceed {MaxOutputTokens}.");

            var estimates = new List<CostEstimate>();

            foreach (var model in _models)
            {
                var totalInputNeeded = inputTokens + estimatedOutputTokens;
                var exceedsContext = totalInputNeeded > model.ContextWindow;
                var contextUsage = model.ContextWindow > 0
                    ? (double)totalInputNeeded / model.ContextWindow * 100
                    : 100.0;

                var cappedOutput = Math.Min(estimatedOutputTokens, model.MaxOutputTokens);
                var inputCost = model.InputCost(inputTokens);
                var outputCost = model.OutputCost(cappedOutput);

                string? warning = null;
                if (!exceedsContext && contextUsage > 80)
                    warning = "Context window usage above 80%";
                if (estimatedOutputTokens > model.MaxOutputTokens)
                    warning = $"Output capped to model max ({model.MaxOutputTokens})";

                estimates.Add(new CostEstimate(
                    Model: model,
                    InputTokens: inputTokens,
                    EstimatedOutputTokens: cappedOutput,
                    InputCost: inputCost,
                    OutputCost: outputCost,
                    TotalCost: inputCost + outputCost,
                    ContextUsagePercent: contextUsage,
                    ExceedsContext: exceedsContext,
                    Warning: warning
                ));
            }

            estimates.Sort((a, b) => a.TotalCost.CompareTo(b.TotalCost));

            return new CostReport
            {
                PromptText = "",
                InputTokens = inputTokens,
                EstimatedOutputTokens = estimatedOutputTokens,
                Estimates = estimates
            };
        }

        /// <summary>
        /// Estimate how many calls you can make within a budget.
        /// </summary>
        /// <param name="modelId">The model to estimate for.</param>
        /// <param name="budgetDollars">Total budget in USD.</param>
        /// <param name="avgInputTokens">Average input tokens per call.</param>
        /// <param name="avgOutputTokens">Average output tokens per call.</param>
        /// <returns>Number of calls that fit within the budget.</returns>
        public int EstimateCallsInBudget(string modelId, decimal budgetDollars, int avgInputTokens, int avgOutputTokens)
        {
            if (budgetDollars <= 0)
                throw new ArgumentException("Budget must be positive.");
            if (avgInputTokens <= 0)
                throw new ArgumentException("Average input tokens must be positive.");
            if (avgOutputTokens <= 0)
                throw new ArgumentException("Average output tokens must be positive.");

            var model = GetModel(modelId)
                ?? throw new ArgumentException($"Unknown model: {modelId}");

            var costPerCall = model.TotalCost(avgInputTokens, avgOutputTokens);
            if (costPerCall <= 0) return int.MaxValue;

            return (int)(budgetDollars / costPerCall);
        }

        /// <summary>
        /// Get models filtered by provider name.
        /// </summary>
        public IReadOnlyList<ModelPricing> GetModelsByProvider(string provider) =>
            _models.Where(m => m.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
                   .ToList()
                   .AsReadOnly();

        /// <summary>
        /// Get all unique provider names.
        /// </summary>
        public IReadOnlyList<string> GetProviders() =>
            _models.Select(m => m.Provider).Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();

        /// <summary>
        /// Built-in model pricing (approximate, as of early 2026).
        /// </summary>
        private void LoadBuiltInModels()
        {
            // OpenAI
            _models.Add(new ModelPricing("gpt-4o", "OpenAI", "GPT-4o", 2.50m, 10.00m, 128_000, 16_384));
            _models.Add(new ModelPricing("gpt-4o-mini", "OpenAI", "GPT-4o Mini", 0.15m, 0.60m, 128_000, 16_384));
            _models.Add(new ModelPricing("gpt-4.5-preview", "OpenAI", "GPT-4.5 Preview", 75.00m, 150.00m, 128_000, 16_384));
            _models.Add(new ModelPricing("gpt-4-turbo", "OpenAI", "GPT-4 Turbo", 10.00m, 30.00m, 128_000, 4_096));
            _models.Add(new ModelPricing("o1", "OpenAI", "o1", 15.00m, 60.00m, 200_000, 100_000));
            _models.Add(new ModelPricing("o1-mini", "OpenAI", "o1-mini", 3.00m, 12.00m, 128_000, 65_536));
            _models.Add(new ModelPricing("o3-mini", "OpenAI", "o3-mini", 1.10m, 4.40m, 200_000, 100_000));

            // Anthropic
            _models.Add(new ModelPricing("claude-4-sonnet", "Anthropic", "Claude 4 Sonnet", 3.00m, 15.00m, 200_000, 16_000));
            _models.Add(new ModelPricing("claude-3.7-sonnet", "Anthropic", "Claude 3.7 Sonnet", 3.00m, 15.00m, 200_000, 128_000));
            _models.Add(new ModelPricing("claude-3-5-sonnet", "Anthropic", "Claude 3.5 Sonnet", 3.00m, 15.00m, 200_000, 8_192));
            _models.Add(new ModelPricing("claude-3-5-haiku", "Anthropic", "Claude 3.5 Haiku", 0.80m, 4.00m, 200_000, 8_192));
            _models.Add(new ModelPricing("claude-3-opus", "Anthropic", "Claude 3 Opus", 15.00m, 75.00m, 200_000, 4_096));

            // Google
            _models.Add(new ModelPricing("gemini-2.5-pro", "Google", "Gemini 2.5 Pro", 1.25m, 10.00m, 1_048_576, 65_536));
            _models.Add(new ModelPricing("gemini-2.0-flash", "Google", "Gemini 2.0 Flash", 0.10m, 0.40m, 1_048_576, 8_192));
            _models.Add(new ModelPricing("gemini-1.5-pro", "Google", "Gemini 1.5 Pro", 1.25m, 5.00m, 2_000_000, 8_192));

            // DeepSeek
            _models.Add(new ModelPricing("deepseek-v3", "DeepSeek", "DeepSeek V3", 0.27m, 1.10m, 128_000, 8_192));
            _models.Add(new ModelPricing("deepseek-r1", "DeepSeek", "DeepSeek R1", 0.55m, 2.19m, 128_000, 8_192));
        }
    }
}
