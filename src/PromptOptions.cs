namespace Prompt
{
    using System.Text.Json.Serialization;
    using OpenAI.Chat;

    /// <summary>
    /// Configurable options for Azure OpenAI chat completion requests.
    /// Wraps model parameters without exposing the Azure SDK dependency,
    /// so consumers can customize behavior without importing Azure.AI.OpenAI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default values match the library's existing behavior for backward
    /// compatibility. Pass an instance to <see cref="Main.GetResponseAsync"/>,
    /// <see cref="PromptTemplate.RenderAndSendAsync"/>, or
    /// <see cref="PromptChain"/> to override any parameter.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Code generation — low temperature, high token limit
    /// var opts = new PromptOptions { Temperature = 0.1f, MaxTokens = 4000 };
    /// var result = await Main.GetResponseAsync("Write a merge sort", options: opts);
    ///
    /// // Creative writing — high temperature
    /// var creative = new PromptOptions { Temperature = 0.9f, TopP = 0.9f, MaxTokens = 2000 };
    /// var story = await Main.GetResponseAsync("Write a short story", options: creative);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptOptions
    {
        private float _temperature = 0.7f;
        private int _maxTokens = 800;
        private float _topP = 0.95f;
        private float _frequencyPenalty = 0f;
        private float _presencePenalty = 0f;

        /// <summary>
        /// Gets or sets the sampling temperature (0.0–2.0).
        /// Lower values (e.g., 0.0–0.2) make output more focused and deterministic.
        /// Higher values (e.g., 0.8–1.0) make output more random and creative.
        /// Default: 0.7.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside 0.0–2.0.</exception>
        [JsonPropertyName("temperature")]
        public float Temperature
        {
            get => _temperature;
            set
            {
                if (value < 0f || value > 2f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Temperature must be between 0.0 and 2.0.");
                _temperature = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of tokens in the response.
        /// Default: 800.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is less than 1.</exception>
        [JsonPropertyName("maxTokens")]
        public int MaxTokens
        {
            get => _maxTokens;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "MaxTokens must be at least 1.");
                _maxTokens = value;
            }
        }

        /// <summary>
        /// Gets or sets the nucleus sampling parameter (0.0–1.0).
        /// An alternative to temperature — the model considers tokens
        /// within the top <c>TopP</c> probability mass.
        /// Default: 0.95.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside 0.0–1.0.</exception>
        [JsonPropertyName("topP")]
        public float TopP
        {
            get => _topP;
            set
            {
                if (value < 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "TopP must be between 0.0 and 1.0.");
                _topP = value;
            }
        }

        /// <summary>
        /// Gets or sets the frequency penalty (-2.0–2.0).
        /// Positive values penalize tokens based on how often they appear,
        /// reducing repetition. Default: 0.0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside -2.0–2.0.</exception>
        [JsonPropertyName("frequencyPenalty")]
        public float FrequencyPenalty
        {
            get => _frequencyPenalty;
            set
            {
                if (value < -2f || value > 2f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "FrequencyPenalty must be between -2.0 and 2.0.");
                _frequencyPenalty = value;
            }
        }

        /// <summary>
        /// Gets or sets the presence penalty (-2.0–2.0).
        /// Positive values penalize tokens based on whether they have
        /// appeared at all, encouraging the model to discuss new topics.
        /// Default: 0.0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Value is outside -2.0–2.0.</exception>
        [JsonPropertyName("presencePenalty")]
        public float PresencePenalty
        {
            get => _presencePenalty;
            set
            {
                if (value < -2f || value > 2f)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "PresencePenalty must be between -2.0 and 2.0.");
                _presencePenalty = value;
            }
        }

        /// <summary>
        /// Creates a <see cref="PromptOptions"/> pre-configured for code generation:
        /// Temperature 0.1, MaxTokens 4000, TopP 0.95.
        /// </summary>
        public static PromptOptions ForCodeGeneration() => new()
        {
            Temperature = 0.1f,
            MaxTokens = 4000,
            TopP = 0.95f
        };

        /// <summary>
        /// Creates a <see cref="PromptOptions"/> pre-configured for creative writing:
        /// Temperature 0.9, MaxTokens 2000, TopP 0.9.
        /// </summary>
        public static PromptOptions ForCreativeWriting() => new()
        {
            Temperature = 0.9f,
            MaxTokens = 2000,
            TopP = 0.9f
        };

        /// <summary>
        /// Creates a <see cref="PromptOptions"/> pre-configured for structured data extraction:
        /// Temperature 0.0, MaxTokens 2000, TopP 1.0.
        /// </summary>
        public static PromptOptions ForDataExtraction() => new()
        {
            Temperature = 0f,
            MaxTokens = 2000,
            TopP = 1f
        };

        /// <summary>
        /// Creates a <see cref="PromptOptions"/> pre-configured for summarization:
        /// Temperature 0.3, MaxTokens 1000, TopP 0.9.
        /// </summary>
        public static PromptOptions ForSummarization() => new()
        {
            Temperature = 0.3f,
            MaxTokens = 1000,
            TopP = 0.9f
        };

        /// <summary>
        /// Converts this <see cref="PromptOptions"/> into Azure SDK
        /// <see cref="ChatCompletionOptions"/>. Centralizes the mapping
        /// so callers don't need to manually copy each property.
        /// </summary>
        /// <returns>A new <see cref="ChatCompletionOptions"/> instance.</returns>
        internal ChatCompletionOptions ToChatCompletionOptions()
        {
            return new ChatCompletionOptions()
            {
                Temperature = _temperature,
                MaxOutputTokenCount = _maxTokens,
                TopP = _topP,
                FrequencyPenalty = _frequencyPenalty,
                PresencePenalty = _presencePenalty,
            };
        }
    }
}
