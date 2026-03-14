namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Supported LLM providers for sampling configuration export.
    /// </summary>
    public enum SamplerProvider
    {
        /// <summary>OpenAI Chat Completions API.</summary>
        OpenAI,
        /// <summary>Anthropic Messages API.</summary>
        Anthropic,
        /// <summary>Google Gemini API.</summary>
        Gemini,
        /// <summary>Generic/provider-agnostic format.</summary>
        Generic
    }

    /// <summary>
    /// Named presets for common sampling strategies.
    /// </summary>
    public enum SamplerPreset
    {
        /// <summary>Balanced defaults suitable for most tasks.</summary>
        Balanced,
        /// <summary>High temperature, high top_p for creative writing.</summary>
        Creative,
        /// <summary>Zero temperature for deterministic, factual output.</summary>
        Deterministic,
        /// <summary>Low temperature with frequency penalty for code generation.</summary>
        Code,
        /// <summary>Very low temperature with tight top_k for precise Q&amp;A.</summary>
        Precise,
        /// <summary>Moderate temperature with presence penalty for brainstorming.</summary>
        Brainstorm
    }

    /// <summary>
    /// A validation issue found in sampling parameters.
    /// </summary>
    public class SamplerValidationIssue
    {
        /// <summary>The parameter name with the issue.</summary>
        [JsonPropertyName("parameter")]
        public string Parameter { get; set; } = "";

        /// <summary>Description of the issue.</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        /// <summary>Whether this is a hard error or a warning.</summary>
        [JsonPropertyName("isError")]
        public bool IsError { get; set; }

        /// <inheritdoc/>
        public override string ToString() => $"[{(IsError ? "ERROR" : "WARN")}] {Parameter}: {Message}";
    }

    /// <summary>
    /// Result of validating a sampler configuration.
    /// </summary>
    public class SamplerValidationResult
    {
        /// <summary>Whether the configuration is valid (no errors).</summary>
        [JsonPropertyName("isValid")]
        public bool IsValid => !Issues.Any(i => i.IsError);

        /// <summary>All validation issues found.</summary>
        [JsonPropertyName("issues")]
        public List<SamplerValidationIssue> Issues { get; set; } = new();

        /// <summary>Errors only.</summary>
        [JsonIgnore]
        public IEnumerable<SamplerValidationIssue> Errors => Issues.Where(i => i.IsError);

        /// <summary>Warnings only.</summary>
        [JsonIgnore]
        public IEnumerable<SamplerValidationIssue> Warnings => Issues.Where(i => !i.IsError);
    }

    /// <summary>
    /// Builds and validates LLM sampling parameters (temperature, top_p, top_k,
    /// penalties, stop sequences, etc.) with provider-specific constraints,
    /// named presets, and JSON export for different APIs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prompt libraries handle the prompt text but not the sampling configuration
    /// that accompanies each API call. This class fills that gap by providing:
    /// </para>
    /// <list type="bullet">
    ///   <item>Fluent builder API for constructing sampling configs</item>
    ///   <item>Named presets (Creative, Deterministic, Code, Precise, Brainstorm)</item>
    ///   <item>Provider-specific validation (OpenAI, Anthropic, Gemini constraints)</item>
    ///   <item>JSON export in provider-native format</item>
    ///   <item>Clone and diff support for A/B testing sampling strategies</item>
    /// </list>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Use a preset
    /// var config = PromptSamplerConfig.FromPreset(SamplerPreset.Creative);
    ///
    /// // Or build from scratch
    /// var config = new PromptSamplerConfig()
    ///     .WithTemperature(0.7)
    ///     .WithTopP(0.9)
    ///     .WithMaxTokens(2048)
    ///     .WithStopSequences("---", "END");
    ///
    /// // Validate for a provider
    /// var result = config.Validate(SamplerProvider.OpenAI);
    ///
    /// // Export as provider-specific JSON
    /// string json = config.ToJson(SamplerProvider.Anthropic);
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptSamplerConfig
    {
        /// <summary>Sampling temperature (0.0-2.0). Higher = more random.</summary>
        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Temperature { get; set; }

        /// <summary>Nucleus sampling threshold (0.0-1.0). Lower = more focused.</summary>
        [JsonPropertyName("top_p")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TopP { get; set; }

        /// <summary>Top-K sampling (1+). Only consider the K most likely tokens.</summary>
        [JsonPropertyName("top_k")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TopK { get; set; }

        /// <summary>Maximum tokens to generate.</summary>
        [JsonPropertyName("max_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxTokens { get; set; }

        /// <summary>Frequency penalty (-2.0 to 2.0). Penalizes repeated tokens by frequency.</summary>
        [JsonPropertyName("frequency_penalty")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? FrequencyPenalty { get; set; }

        /// <summary>Presence penalty (-2.0 to 2.0). Penalizes tokens that have appeared at all.</summary>
        [JsonPropertyName("presence_penalty")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PresencePenalty { get; set; }

        /// <summary>Stop sequences — generation halts when any of these are produced.</summary>
        [JsonPropertyName("stop")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? StopSequences { get; set; }

        /// <summary>Random seed for reproducible outputs (provider support varies).</summary>
        [JsonPropertyName("seed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Seed { get; set; }

        /// <summary>Optional label for this configuration.</summary>
        [JsonPropertyName("label")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Label { get; set; }

        // ── Fluent Builder ─────────────────────────────────────────────

        /// <summary>Sets the sampling temperature.</summary>
        public PromptSamplerConfig WithTemperature(double temperature)
        {
            Temperature = temperature;
            return this;
        }

        /// <summary>Sets the nucleus sampling (top_p) threshold.</summary>
        public PromptSamplerConfig WithTopP(double topP)
        {
            TopP = topP;
            return this;
        }

        /// <summary>Sets the top-K sampling value.</summary>
        public PromptSamplerConfig WithTopK(int topK)
        {
            TopK = topK;
            return this;
        }

        /// <summary>Sets the maximum tokens to generate.</summary>
        public PromptSamplerConfig WithMaxTokens(int maxTokens)
        {
            MaxTokens = maxTokens;
            return this;
        }

        /// <summary>Sets the frequency penalty.</summary>
        public PromptSamplerConfig WithFrequencyPenalty(double penalty)
        {
            FrequencyPenalty = penalty;
            return this;
        }

        /// <summary>Sets the presence penalty.</summary>
        public PromptSamplerConfig WithPresencePenalty(double penalty)
        {
            PresencePenalty = penalty;
            return this;
        }

        /// <summary>Sets the stop sequences.</summary>
        public PromptSamplerConfig WithStopSequences(params string[] stops)
        {
            StopSequences = stops.ToList();
            return this;
        }

        /// <summary>Sets the random seed.</summary>
        public PromptSamplerConfig WithSeed(int seed)
        {
            Seed = seed;
            return this;
        }

        /// <summary>Sets a label for this configuration.</summary>
        public PromptSamplerConfig WithLabel(string label)
        {
            Label = label;
            return this;
        }

        // ── Presets ────────────────────────────────────────────────────

        /// <summary>
        /// Creates a configuration from a named preset.
        /// </summary>
        /// <param name="preset">The preset to use.</param>
        /// <returns>A new <see cref="PromptSamplerConfig"/> with preset values.</returns>
        public static PromptSamplerConfig FromPreset(SamplerPreset preset)
        {
            return preset switch
            {
                SamplerPreset.Balanced => new PromptSamplerConfig
                {
                    Label = "Balanced",
                    Temperature = 0.7,
                    TopP = 1.0,
                    MaxTokens = 2048,
                },
                SamplerPreset.Creative => new PromptSamplerConfig
                {
                    Label = "Creative",
                    Temperature = 1.2,
                    TopP = 0.95,
                    PresencePenalty = 0.6,
                    FrequencyPenalty = 0.3,
                    MaxTokens = 4096,
                },
                SamplerPreset.Deterministic => new PromptSamplerConfig
                {
                    Label = "Deterministic",
                    Temperature = 0.0,
                    TopP = 1.0,
                    MaxTokens = 2048,
                    Seed = 42,
                },
                SamplerPreset.Code => new PromptSamplerConfig
                {
                    Label = "Code",
                    Temperature = 0.2,
                    TopP = 0.95,
                    FrequencyPenalty = 0.1,
                    MaxTokens = 4096,
                    StopSequences = new List<string> { "```" },
                },
                SamplerPreset.Precise => new PromptSamplerConfig
                {
                    Label = "Precise",
                    Temperature = 0.1,
                    TopP = 0.8,
                    TopK = 40,
                    MaxTokens = 1024,
                },
                SamplerPreset.Brainstorm => new PromptSamplerConfig
                {
                    Label = "Brainstorm",
                    Temperature = 0.9,
                    TopP = 0.95,
                    PresencePenalty = 1.0,
                    MaxTokens = 4096,
                },
                _ => new PromptSamplerConfig { Label = "Balanced", Temperature = 0.7, TopP = 1.0, MaxTokens = 2048 }
            };
        }

        /// <summary>
        /// Lists all available preset names and their descriptions.
        /// </summary>
        public static List<(SamplerPreset Preset, string Description)> ListPresets()
        {
            return new List<(SamplerPreset, string)>
            {
                (SamplerPreset.Balanced, "Balanced defaults for general tasks (temp=0.7)"),
                (SamplerPreset.Creative, "High randomness for creative writing (temp=1.2, penalties)"),
                (SamplerPreset.Deterministic, "Zero temperature for reproducible output (temp=0, seed=42)"),
                (SamplerPreset.Code, "Low temperature for code generation (temp=0.2, stop=```)"),
                (SamplerPreset.Precise, "Very focused for factual Q&A (temp=0.1, top_k=40)"),
                (SamplerPreset.Brainstorm, "Diverse outputs with high presence penalty (temp=0.9)"),
            };
        }

        // ── Validation ─────────────────────────────────────────────────

        /// <summary>
        /// Validates this configuration against provider-specific constraints.
        /// </summary>
        /// <param name="provider">Target provider (affects allowed ranges).</param>
        /// <returns>Validation result with any issues found.</returns>
        public SamplerValidationResult Validate(SamplerProvider provider = SamplerProvider.Generic)
        {
            var result = new SamplerValidationResult();

            // Temperature
            if (Temperature.HasValue)
            {
                double maxTemp = provider == SamplerProvider.Anthropic ? 1.0 : 2.0;
                if (Temperature.Value < 0.0)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "temperature",
                        Message = "Temperature cannot be negative.",
                        IsError = true
                    });
                else if (Temperature.Value > maxTemp)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "temperature",
                        Message = $"Temperature exceeds provider maximum ({maxTemp}) for {provider}.",
                        IsError = true
                    });
            }

            // TopP
            if (TopP.HasValue)
            {
                if (TopP.Value < 0.0 || TopP.Value > 1.0)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "top_p",
                        Message = "top_p must be between 0.0 and 1.0.",
                        IsError = true
                    });
            }

            // TopK
            if (TopK.HasValue)
            {
                if (TopK.Value < 1)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "top_k",
                        Message = "top_k must be at least 1.",
                        IsError = true
                    });
                if (provider == SamplerProvider.OpenAI)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "top_k",
                        Message = "OpenAI Chat Completions API does not support top_k. It will be ignored.",
                        IsError = false
                    });
            }

            // MaxTokens
            if (MaxTokens.HasValue && MaxTokens.Value < 1)
                result.Issues.Add(new SamplerValidationIssue
                {
                    Parameter = "max_tokens",
                    Message = "max_tokens must be at least 1.",
                    IsError = true
                });

            // FrequencyPenalty
            if (FrequencyPenalty.HasValue)
            {
                if (FrequencyPenalty.Value < -2.0 || FrequencyPenalty.Value > 2.0)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "frequency_penalty",
                        Message = "frequency_penalty must be between -2.0 and 2.0.",
                        IsError = true
                    });
                if (provider == SamplerProvider.Anthropic)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "frequency_penalty",
                        Message = "Anthropic does not support frequency_penalty. It will be ignored.",
                        IsError = false
                    });
            }

            // PresencePenalty
            if (PresencePenalty.HasValue)
            {
                if (PresencePenalty.Value < -2.0 || PresencePenalty.Value > 2.0)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "presence_penalty",
                        Message = "presence_penalty must be between -2.0 and 2.0.",
                        IsError = true
                    });
                if (provider == SamplerProvider.Anthropic)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "presence_penalty",
                        Message = "Anthropic does not support presence_penalty. It will be ignored.",
                        IsError = false
                    });
            }

            // StopSequences
            if (StopSequences != null)
            {
                int maxStops = provider switch
                {
                    SamplerProvider.OpenAI => 4,
                    SamplerProvider.Anthropic => 8192,
                    SamplerProvider.Gemini => 5,
                    _ => int.MaxValue
                };
                if (StopSequences.Count > maxStops)
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "stop",
                        Message = $"{provider} allows at most {maxStops} stop sequence(s). Got {StopSequences.Count}.",
                        IsError = true
                    });
                if (StopSequences.Any(string.IsNullOrEmpty))
                    result.Issues.Add(new SamplerValidationIssue
                    {
                        Parameter = "stop",
                        Message = "Stop sequences must not be null or empty.",
                        IsError = true
                    });
            }

            // Seed
            if (Seed.HasValue && provider == SamplerProvider.Gemini)
                result.Issues.Add(new SamplerValidationIssue
                {
                    Parameter = "seed",
                    Message = "Gemini does not support the seed parameter. It will be ignored.",
                    IsError = false
                });

            // Conflicting settings warnings
            if (Temperature.HasValue && Temperature.Value == 0.0 && TopP.HasValue && TopP.Value < 1.0)
                result.Issues.Add(new SamplerValidationIssue
                {
                    Parameter = "temperature+top_p",
                    Message = "When temperature is 0, top_p has no effect (output is deterministic).",
                    IsError = false
                });

            return result;
        }

        // ── Export ──────────────────────────────────────────────────────

        /// <summary>
        /// Exports this configuration as a JSON string in provider-specific format.
        /// </summary>
        /// <param name="provider">Target provider format.</param>
        /// <param name="indented">Whether to pretty-print the JSON.</param>
        /// <returns>JSON string with provider-appropriate parameter names.</returns>
        public string ToJson(SamplerProvider provider = SamplerProvider.Generic, bool indented = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            var dict = new Dictionary<string, object>();

            switch (provider)
            {
                case SamplerProvider.OpenAI:
                    if (Temperature.HasValue) dict["temperature"] = Temperature.Value;
                    if (TopP.HasValue) dict["top_p"] = TopP.Value;
                    if (MaxTokens.HasValue) dict["max_tokens"] = MaxTokens.Value;
                    if (FrequencyPenalty.HasValue) dict["frequency_penalty"] = FrequencyPenalty.Value;
                    if (PresencePenalty.HasValue) dict["presence_penalty"] = PresencePenalty.Value;
                    if (StopSequences != null && StopSequences.Count > 0) dict["stop"] = StopSequences;
                    if (Seed.HasValue) dict["seed"] = Seed.Value;
                    break;

                case SamplerProvider.Anthropic:
                    if (Temperature.HasValue) dict["temperature"] = Temperature.Value;
                    if (TopP.HasValue) dict["top_p"] = TopP.Value;
                    if (TopK.HasValue) dict["top_k"] = TopK.Value;
                    if (MaxTokens.HasValue) dict["max_tokens"] = MaxTokens.Value;
                    if (StopSequences != null && StopSequences.Count > 0) dict["stop_sequences"] = StopSequences;
                    // Anthropic doesn't support frequency/presence penalties or seed
                    break;

                case SamplerProvider.Gemini:
                    if (Temperature.HasValue) dict["temperature"] = Temperature.Value;
                    if (TopP.HasValue) dict["topP"] = TopP.Value;
                    if (TopK.HasValue) dict["topK"] = TopK.Value;
                    if (MaxTokens.HasValue) dict["maxOutputTokens"] = MaxTokens.Value;
                    if (StopSequences != null && StopSequences.Count > 0) dict["stopSequences"] = StopSequences;
                    if (FrequencyPenalty.HasValue) dict["frequencyPenalty"] = FrequencyPenalty.Value;
                    if (PresencePenalty.HasValue) dict["presencePenalty"] = PresencePenalty.Value;
                    break;

                default: // Generic
                    if (Temperature.HasValue) dict["temperature"] = Temperature.Value;
                    if (TopP.HasValue) dict["top_p"] = TopP.Value;
                    if (TopK.HasValue) dict["top_k"] = TopK.Value;
                    if (MaxTokens.HasValue) dict["max_tokens"] = MaxTokens.Value;
                    if (FrequencyPenalty.HasValue) dict["frequency_penalty"] = FrequencyPenalty.Value;
                    if (PresencePenalty.HasValue) dict["presence_penalty"] = PresencePenalty.Value;
                    if (StopSequences != null && StopSequences.Count > 0) dict["stop"] = StopSequences;
                    if (Seed.HasValue) dict["seed"] = Seed.Value;
                    break;
            }

            return JsonSerializer.Serialize(dict, options);
        }

        /// <summary>
        /// Exports as a provider-agnostic dictionary of set parameters.
        /// </summary>
        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            if (Temperature.HasValue) dict["temperature"] = Temperature.Value;
            if (TopP.HasValue) dict["top_p"] = TopP.Value;
            if (TopK.HasValue) dict["top_k"] = TopK.Value;
            if (MaxTokens.HasValue) dict["max_tokens"] = MaxTokens.Value;
            if (FrequencyPenalty.HasValue) dict["frequency_penalty"] = FrequencyPenalty.Value;
            if (PresencePenalty.HasValue) dict["presence_penalty"] = PresencePenalty.Value;
            if (StopSequences != null) dict["stop"] = StopSequences;
            if (Seed.HasValue) dict["seed"] = Seed.Value;
            return dict;
        }

        // ── Clone & Diff ───────────────────────────────────────────────

        /// <summary>
        /// Creates a deep copy of this configuration.
        /// </summary>
        public PromptSamplerConfig Clone()
        {
            return new PromptSamplerConfig
            {
                Temperature = Temperature,
                TopP = TopP,
                TopK = TopK,
                MaxTokens = MaxTokens,
                FrequencyPenalty = FrequencyPenalty,
                PresencePenalty = PresencePenalty,
                StopSequences = StopSequences != null ? new List<string>(StopSequences) : null,
                Seed = Seed,
                Label = Label,
            };
        }

        /// <summary>
        /// Compares two configurations and returns the differing parameters.
        /// </summary>
        /// <param name="other">The other configuration to compare against.</param>
        /// <returns>Dictionary of parameter names to (this value, other value) tuples.</returns>
        public Dictionary<string, (object? ThisValue, object? OtherValue)> Diff(PromptSamplerConfig other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            var diffs = new Dictionary<string, (object?, object?)>();

            if (!NullableEquals(Temperature, other.Temperature))
                diffs["temperature"] = (Temperature, other.Temperature);
            if (!NullableEquals(TopP, other.TopP))
                diffs["top_p"] = (TopP, other.TopP);
            if (!NullableEquals(TopK, other.TopK))
                diffs["top_k"] = (TopK, other.TopK);
            if (!NullableEquals(MaxTokens, other.MaxTokens))
                diffs["max_tokens"] = (MaxTokens, other.MaxTokens);
            if (!NullableEquals(FrequencyPenalty, other.FrequencyPenalty))
                diffs["frequency_penalty"] = (FrequencyPenalty, other.FrequencyPenalty);
            if (!NullableEquals(PresencePenalty, other.PresencePenalty))
                diffs["presence_penalty"] = (PresencePenalty, other.PresencePenalty);
            if (!NullableEquals(Seed, other.Seed))
                diffs["seed"] = (Seed, other.Seed);

            bool stopsEqual = (StopSequences == null && other.StopSequences == null) ||
                              (StopSequences != null && other.StopSequences != null &&
                               StopSequences.SequenceEqual(other.StopSequences));
            if (!stopsEqual)
                diffs["stop"] = (StopSequences, other.StopSequences);

            return diffs;
        }

        /// <summary>
        /// Generates a human-readable summary of this configuration.
        /// </summary>
        public string ToSummary()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Label)) parts.Add($"[{Label}]");
            if (Temperature.HasValue) parts.Add($"temp={Temperature.Value:F1}");
            if (TopP.HasValue) parts.Add($"top_p={TopP.Value:F2}");
            if (TopK.HasValue) parts.Add($"top_k={TopK.Value}");
            if (MaxTokens.HasValue) parts.Add($"max_tokens={MaxTokens.Value}");
            if (FrequencyPenalty.HasValue) parts.Add($"freq_penalty={FrequencyPenalty.Value:F1}");
            if (PresencePenalty.HasValue) parts.Add($"pres_penalty={PresencePenalty.Value:F1}");
            if (StopSequences != null && StopSequences.Count > 0)
                parts.Add($"stops={StopSequences.Count}");
            if (Seed.HasValue) parts.Add($"seed={Seed.Value}");
            return parts.Count > 0 ? string.Join(", ", parts) : "(empty configuration)";
        }

        /// <summary>
        /// Merges another configuration into this one. Non-null values from
        /// <paramref name="other"/> overwrite values in this configuration.
        /// </summary>
        /// <param name="other">Configuration to merge from.</param>
        /// <returns>This instance for chaining.</returns>
        public PromptSamplerConfig Merge(PromptSamplerConfig other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (other.Temperature.HasValue) Temperature = other.Temperature;
            if (other.TopP.HasValue) TopP = other.TopP;
            if (other.TopK.HasValue) TopK = other.TopK;
            if (other.MaxTokens.HasValue) MaxTokens = other.MaxTokens;
            if (other.FrequencyPenalty.HasValue) FrequencyPenalty = other.FrequencyPenalty;
            if (other.PresencePenalty.HasValue) PresencePenalty = other.PresencePenalty;
            if (other.StopSequences != null) StopSequences = new List<string>(other.StopSequences);
            if (other.Seed.HasValue) Seed = other.Seed;
            if (other.Label != null) Label = other.Label;
            return this;
        }

        /// <summary>
        /// Returns the number of explicitly set parameters.
        /// </summary>
        public int SetParameterCount()
        {
            int count = 0;
            if (Temperature.HasValue) count++;
            if (TopP.HasValue) count++;
            if (TopK.HasValue) count++;
            if (MaxTokens.HasValue) count++;
            if (FrequencyPenalty.HasValue) count++;
            if (PresencePenalty.HasValue) count++;
            if (StopSequences != null) count++;
            if (Seed.HasValue) count++;
            return count;
        }

        private static bool NullableEquals<T>(T? a, T? b) where T : struct
        {
            if (!a.HasValue && !b.HasValue) return true;
            if (!a.HasValue || !b.HasValue) return false;
            return EqualityComparer<T>.Default.Equals(a.Value, b.Value);
        }
    }
}
