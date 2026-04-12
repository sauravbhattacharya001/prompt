namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of a token estimation for a single prompt.
    /// </summary>
    public class TokenEstimate
    {
        /// <summary>The original text that was estimated.</summary>
        [JsonPropertyName("text")]
        public string Text { get; }

        /// <summary>Estimated token count.</summary>
        [JsonPropertyName("tokenCount")]
        public int TokenCount { get; }

        /// <summary>Character count of the input text.</summary>
        [JsonPropertyName("charCount")]
        public int CharCount { get; }

        /// <summary>Word count of the input text.</summary>
        [JsonPropertyName("wordCount")]
        public int WordCount { get; }

        /// <summary>
        /// Creates a new token estimate result.
        /// </summary>
        public TokenEstimate(string text, int tokenCount, int charCount, int wordCount)
        {
            Text = text;
            TokenCount = tokenCount;
            CharCount = charCount;
            WordCount = wordCount;
        }
    }

    /// <summary>
    /// Cost result from <see cref="PromptTokenCounter"/> for a prompt against a specific model.
    /// </summary>
    public class TokenCostResult
    {
        /// <summary>The model used for pricing.</summary>
        [JsonPropertyName("model")]
        public string Model { get; }

        /// <summary>Estimated input token count.</summary>
        [JsonPropertyName("inputTokens")]
        public int InputTokens { get; }

        /// <summary>Estimated output token count.</summary>
        [JsonPropertyName("outputTokens")]
        public int OutputTokens { get; }

        /// <summary>Estimated cost for input tokens in USD.</summary>
        [JsonPropertyName("inputCost")]
        public decimal InputCost { get; }

        /// <summary>Estimated cost for output tokens in USD.</summary>
        [JsonPropertyName("outputCost")]
        public decimal OutputCost { get; }

        /// <summary>Total estimated cost in USD.</summary>
        [JsonPropertyName("totalCost")]
        public decimal TotalCost => InputCost + OutputCost;

        /// <summary>
        /// Creates a new token cost result.
        /// </summary>
        public TokenCostResult(string model, int inputTokens, int outputTokens, decimal inputCost, decimal outputCost)
        {
            Model = model;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
            InputCost = inputCost;
            OutputCost = outputCost;
        }
    }

    /// <summary>
    /// Row in a model cost comparison table produced by <see cref="PromptTokenCounter"/>.
    /// </summary>
    public class CostComparisonRow
    {
        /// <summary>Model name.</summary>
        [JsonPropertyName("model")]
        public string Model { get; }

        /// <summary>Estimated input cost in USD.</summary>
        [JsonPropertyName("inputCost")]
        public decimal InputCost { get; }

        /// <summary>Estimated output cost in USD.</summary>
        [JsonPropertyName("outputCost")]
        public decimal OutputCost { get; }

        /// <summary>Total estimated cost in USD.</summary>
        [JsonPropertyName("totalCost")]
        public decimal TotalCost => InputCost + OutputCost;

        /// <summary>
        /// Creates a new comparison row.
        /// </summary>
        public CostComparisonRow(string model, decimal inputCost, decimal outputCost)
        {
            Model = model;
            InputCost = inputCost;
            OutputCost = outputCost;
        }
    }

    /// <summary>
    /// Estimates token counts for prompts using cl100k_base-style heuristics
    /// and calculates costs across multiple model pricing tiers.
    /// Reuses <see cref="ModelPricing"/> from <see cref="PromptCostEstimator"/>
    /// for model pricing data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// var counter = new PromptTokenCounter();
    ///
    /// // Estimate tokens
    /// var estimate = counter.Estimate("Hello, how are you?");
    /// Console.WriteLine($"Tokens: {estimate.TokenCount}");
    ///
    /// // Calculate cost for a model
    /// var cost = counter.EstimateCost("Write a poem about cats", "gpt-4o", estimatedOutputTokens: 200);
    /// Console.WriteLine($"Cost: ${cost.TotalCost:F6}");
    ///
    /// // Compare across all models
    /// var table = counter.CompareCosts("Explain quantum physics", estimatedOutputTokens: 500);
    /// foreach (var row in table)
    ///     Console.WriteLine($"{row.Model}: ${row.TotalCost:F6}");
    ///
    /// // Formatted table
    /// Console.WriteLine(counter.FormatCostComparison("Hello world", estimatedOutputTokens: 100));
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptTokenCounter
    {
        private readonly Dictionary<string, ModelPricing> _models = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new <see cref="PromptTokenCounter"/> with built-in model pricing tiers.
        /// </summary>
        public PromptTokenCounter()
        {
            // Built-in pricing (USD per 1M tokens, approximate as of 2025)
            AddModel(new ModelPricing("gpt-4", "OpenAI", "GPT-4", 30.00m, 60.00m, 8192, 4096));
            AddModel(new ModelPricing("gpt-4o", "OpenAI", "GPT-4o", 2.50m, 10.00m, 128000, 16384));
            AddModel(new ModelPricing("gpt-4o-mini", "OpenAI", "GPT-4o Mini", 0.15m, 0.60m, 128000, 16384));
            AddModel(new ModelPricing("gpt-3.5-turbo", "OpenAI", "GPT-3.5 Turbo", 0.50m, 1.50m, 16385, 4096));
            AddModel(new ModelPricing("claude-3.5-sonnet", "Anthropic", "Claude 3.5 Sonnet", 3.00m, 15.00m, 200000, 8192));
            AddModel(new ModelPricing("claude-3-haiku", "Anthropic", "Claude 3 Haiku", 0.25m, 1.25m, 200000, 4096));
            AddModel(new ModelPricing("gemini-1.5-pro", "Google", "Gemini 1.5 Pro", 3.50m, 10.50m, 2000000, 8192));
            AddModel(new ModelPricing("gemini-1.5-flash", "Google", "Gemini 1.5 Flash", 0.075m, 0.30m, 1000000, 8192));
        }

        /// <summary>
        /// Adds or replaces a model pricing tier. Returns this instance for fluent chaining.
        /// </summary>
        /// <param name="pricing">The <see cref="ModelPricing"/> to register.</param>
        /// <returns>This <see cref="PromptTokenCounter"/> instance.</returns>
        public PromptTokenCounter AddModel(ModelPricing pricing)
        {
            if (pricing == null) throw new ArgumentNullException(nameof(pricing));
            _models[pricing.ModelId] = pricing;
            return this;
        }

        /// <summary>
        /// Adds or replaces a model pricing tier using individual parameters. Returns this instance for fluent chaining.
        /// </summary>
        /// <param name="modelId">Model identifier (e.g. "gpt-4o").</param>
        /// <param name="provider">Provider name (e.g. "OpenAI").</param>
        /// <param name="displayName">Human-readable model name.</param>
        /// <param name="inputCostPerMillion">USD per 1M input tokens.</param>
        /// <param name="outputCostPerMillion">USD per 1M output tokens.</param>
        /// <param name="contextWindow">Context window size in tokens.</param>
        /// <param name="maxOutputTokens">Maximum output tokens.</param>
        /// <returns>This <see cref="PromptTokenCounter"/> instance.</returns>
        public PromptTokenCounter WithModel(
            string modelId,
            string provider,
            string displayName,
            decimal inputCostPerMillion,
            decimal outputCostPerMillion,
            int contextWindow = 128000,
            int maxOutputTokens = 4096)
        {
            return AddModel(new ModelPricing(
                modelId, provider, displayName,
                inputCostPerMillion, outputCostPerMillion,
                contextWindow, maxOutputTokens));
        }

        /// <summary>
        /// Returns all registered model pricing tiers.
        /// </summary>
        public IReadOnlyList<ModelPricing> GetModels() => _models.Values.ToList().AsReadOnly();

        /// <summary>
        /// Estimates the token count for the given text using cl100k_base-style heuristics.
        /// </summary>
        /// <param name="text">The text to estimate.</param>
        /// <returns>A <see cref="TokenEstimate"/> with token count, char count, and word count.</returns>
        public TokenEstimate Estimate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new TokenEstimate(text ?? string.Empty, 0, 0, 0);

            int charCount = text.Length;
            var words = SplitWords(text);
            int wordCount = words.Count;
            int tokenCount = EstimateTokenCount(text, words);

            return new TokenEstimate(text, tokenCount, charCount, wordCount);
        }

        /// <summary>
        /// Estimates the token count for a collection of prompts (batch estimation).
        /// </summary>
        /// <param name="texts">The texts to estimate.</param>
        /// <returns>A list of <see cref="TokenEstimate"/> results, one per input text.</returns>
        public IReadOnlyList<TokenEstimate> EstimateBatch(IEnumerable<string> texts)
        {
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            return texts.Select(Estimate).ToList().AsReadOnly();
        }

        /// <summary>
        /// Calculates the estimated cost for a prompt against a specific model.
        /// </summary>
        /// <param name="text">The input prompt text.</param>
        /// <param name="modelId">The model ID to price against.</param>
        /// <param name="estimatedOutputTokens">Expected output token count (default 0).</param>
        /// <returns>A <see cref="TokenCostResult"/> with input/output costs.</returns>
        /// <exception cref="ArgumentException">Thrown when the model is not registered.</exception>
        public TokenCostResult EstimateCost(string text, string modelId, int estimatedOutputTokens = 0)
        {
            if (!_models.TryGetValue(modelId, out var pricing))
                throw new ArgumentException($"Unknown model '{modelId}'. Register it with AddModel() first.", nameof(modelId));

            var estimate = Estimate(text);
            decimal inputCost = pricing.InputCost(estimate.TokenCount);
            decimal outputCost = pricing.OutputCost(estimatedOutputTokens);

            return new TokenCostResult(pricing.ModelId, estimate.TokenCount, estimatedOutputTokens, inputCost, outputCost);
        }

        /// <summary>
        /// Compares estimated costs for a prompt across all registered models.
        /// Results are sorted by total cost ascending (cheapest first).
        /// </summary>
        /// <param name="text">The input prompt text.</param>
        /// <param name="estimatedOutputTokens">Expected output token count (default 0).</param>
        /// <returns>A list of <see cref="CostComparisonRow"/> sorted by total cost.</returns>
        public IReadOnlyList<CostComparisonRow> CompareCosts(string text, int estimatedOutputTokens = 0)
        {
            var estimate = Estimate(text);
            var rows = new List<CostComparisonRow>();

            foreach (var pricing in _models.Values)
            {
                decimal inputCost = pricing.InputCost(estimate.TokenCount);
                decimal outputCost = pricing.OutputCost(estimatedOutputTokens);
                rows.Add(new CostComparisonRow(pricing.ModelId, inputCost, outputCost));
            }

            rows.Sort((a, b) => a.TotalCost.CompareTo(b.TotalCost));
            return rows.AsReadOnly();
        }

        /// <summary>
        /// Generates a formatted cost comparison table as a string.
        /// </summary>
        /// <param name="text">The input prompt text.</param>
        /// <param name="estimatedOutputTokens">Expected output token count (default 0).</param>
        /// <returns>A formatted table string.</returns>
        public string FormatCostComparison(string text, int estimatedOutputTokens = 0)
        {
            var estimate = Estimate(text);
            var rows = CompareCosts(text, estimatedOutputTokens);

            var lines = new List<string>
            {
                $"Token Estimate: {estimate.TokenCount} input + {estimatedOutputTokens} output",
                $"Text: \"{(text.Length > 60 ? text.Substring(0, 57) + "..." : text)}\"",
                "",
                string.Format("{0,-22} {1,12} {2,12} {3,12}", "Model", "Input Cost", "Output Cost", "Total"),
                new string('-', 60)
            };

            foreach (var row in rows)
            {
                lines.Add(string.Format("{0,-22} ${1,11:F6} ${2,11:F6} ${3,11:F6}", row.Model, row.InputCost, row.OutputCost, row.TotalCost));
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Estimates total tokens and cost for a batch of prompts against a model.
        /// </summary>
        /// <param name="texts">The input texts.</param>
        /// <param name="modelId">The model ID to price against.</param>
        /// <param name="estimatedOutputTokensEach">Expected output tokens per prompt (default 0).</param>
        /// <returns>A <see cref="TokenCostResult"/> for the entire batch.</returns>
        public TokenCostResult EstimateBatchCost(IEnumerable<string> texts, string modelId, int estimatedOutputTokensEach = 0)
        {
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (!_models.TryGetValue(modelId, out var pricing))
                throw new ArgumentException($"Unknown model '{modelId}'. Register it with AddModel() first.", nameof(modelId));

            var textList = texts.ToList();
            int totalInputTokens = textList.Sum(t => Estimate(t).TokenCount);
            int totalOutputTokens = estimatedOutputTokensEach * textList.Count;

            decimal inputCost = pricing.InputCost(totalInputTokens);
            decimal outputCost = pricing.OutputCost(totalOutputTokens);

            return new TokenCostResult(pricing.ModelId, totalInputTokens, totalOutputTokens, inputCost, outputCost);
        }

        #region Private Helpers

        private static List<string> SplitWords(string text)
        {
            var words = new List<string>();
            int start = -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    if (start >= 0)
                    {
                        words.Add(text.Substring(start, i - start));
                        start = -1;
                    }
                }
                else if (start < 0)
                {
                    start = i;
                }
            }

            if (start >= 0)
                words.Add(text.Substring(start));

            return words;
        }

        private static int EstimateTokenCount(string text, List<string> words)
        {
            // cl100k_base heuristic: ~4 chars per token on average,
            // with adjustments for punctuation and short words.
            int tokens = 0;

            foreach (var word in words)
            {
                if (word.Length <= 4)
                {
                    tokens += 1;
                }
                else
                {
                    // Longer words: roughly 1 token per 4 chars, rounding up
                    tokens += (word.Length + 3) / 4;
                }

                // Punctuation attached to words can add extra tokens
                int punctCount = 0;
                foreach (char c in word)
                {
                    if (char.IsPunctuation(c) || char.IsSymbol(c))
                        punctCount++;
                }

                if (punctCount > 1)
                    tokens += punctCount - 1;
            }

            // Minimum 1 token for non-empty text
            return Math.Max(tokens, text.Length > 0 ? 1 : 0);
        }

        #endregion
    }
}
