namespace Prompt
{
    using System;

    /// <summary>
    /// Resolves an <see cref="ILlmProvider"/> from environment variables. The
    /// active provider is chosen by <c>PROMPT_PROVIDER</c>; when it is unset or
    /// set to <c>azure</c>, the library's original Azure OpenAI behavior is used
    /// (reading the <c>AZURE_OPENAI_*</c> variables), so existing deployments are
    /// unaffected.
    /// </summary>
    /// <remarks>
    /// <para>Supported <c>PROMPT_PROVIDER</c> values:</para>
    /// <list type="bullet">
    ///   <item><c>azure</c> (default) — Azure OpenAI via <c>AZURE_OPENAI_API_URI</c>,
    ///         <c>AZURE_OPENAI_API_KEY</c>, <c>AZURE_OPENAI_API_MODEL</c>.</item>
    ///   <item><c>openai</c>, <c>mistral</c>, <c>groq</c>, <c>deepseek</c>,
    ///         <c>grok</c>/<c>xai</c>, <c>openrouter</c>, <c>together</c>,
    ///         <c>fireworks</c>, <c>ollama</c> — OpenAI-compatible endpoints.</item>
    ///   <item><c>anthropic</c> — Claude Messages API.</item>
    ///   <item><c>gemini</c>/<c>google</c> — Google Generative Language API.</item>
    /// </list>
    /// <para>
    /// For the non-Azure providers, the API key and model are read from the
    /// generic <c>PROMPT_API_KEY</c> / <c>PROMPT_MODEL</c> variables first, then
    /// from the provider-conventional variable (for example <c>OPENAI_API_KEY</c>,
    /// <c>ANTHROPIC_API_KEY</c>, <c>GEMINI_API_KEY</c>). An optional
    /// <c>PROMPT_BASE_URL</c> overrides the base URL/endpoint (useful for
    /// self-hosted gateways and for pointing the Ollama provider at a remote host).
    /// </para>
    /// </remarks>
    public static class ProviderFactory
    {
        /// <summary>Environment variable that selects the active provider.</summary>
        public const string ProviderEnvVar = "PROMPT_PROVIDER";

        /// <summary>Generic API-key override applied to all non-Azure providers.</summary>
        public const string ApiKeyEnvVar = "PROMPT_API_KEY";

        /// <summary>Generic model override applied to all non-Azure providers.</summary>
        public const string ModelEnvVar = "PROMPT_MODEL";

        /// <summary>Generic base-URL/endpoint override applied to all non-Azure providers.</summary>
        public const string BaseUrlEnvVar = "PROMPT_BASE_URL";

        /// <summary>
        /// Builds the provider named by <c>PROMPT_PROVIDER</c>. Returns an
        /// <see cref="AzureOpenAIProvider"/> when the variable is unset, empty,
        /// or <c>azure</c>.
        /// </summary>
        /// <param name="maxRetries">Retry count forwarded to the Azure provider.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a required API key or model for the selected provider is missing.
        /// </exception>
        public static ILlmProvider CreateFromEnvironment(int maxRetries = 3)
        {
            string provider = (GetEnv(ProviderEnvVar) ?? "azure").Trim().ToLowerInvariant();
            return Create(provider, maxRetries);
        }

        /// <summary>
        /// Builds the provider identified by <paramref name="provider"/> using
        /// environment variables for credentials and model selection.
        /// </summary>
        /// <param name="provider">
        /// Provider name (case-insensitive). Recognized values are listed on
        /// <see cref="ProviderFactory"/>.
        /// </param>
        /// <param name="maxRetries">Retry count forwarded to the Azure provider.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown for an unknown provider name or a missing key/model.
        /// </exception>
        public static ILlmProvider Create(string provider, int maxRetries = 3)
        {
            switch ((provider ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "azure":
                case "azureopenai":
                case "azure-openai":
                    return new AzureOpenAIProvider(maxRetries);

                case "openai":
                    return OpenAICompatProvider.OpenAI(Key("OPENAI_API_KEY", provider), Model(provider));
                case "mistral":
                    return OpenAICompatProvider.Mistral(Key("MISTRAL_API_KEY", provider), Model(provider));
                case "groq":
                    return OpenAICompatProvider.Groq(Key("GROQ_API_KEY", provider), Model(provider));
                case "deepseek":
                    return OpenAICompatProvider.DeepSeek(Key("DEEPSEEK_API_KEY", provider), Model(provider));
                case "grok":
                case "xai":
                    return OpenAICompatProvider.Grok(Key("XAI_API_KEY", "GROK_API_KEY", provider), Model(provider));
                case "openrouter":
                    return OpenAICompatProvider.OpenRouter(Key("OPENROUTER_API_KEY", provider), Model(provider));
                case "together":
                    return OpenAICompatProvider.Together(Key("TOGETHER_API_KEY", provider), Model(provider));
                case "fireworks":
                    return OpenAICompatProvider.Fireworks(Key("FIREWORKS_API_KEY", provider), Model(provider));

                case "ollama":
                    // API key optional; base URL optional (defaults to localhost).
                    return OpenAICompatProvider.Ollama(
                        Model(provider),
                        baseUrl: GetEnv(BaseUrlEnvVar),
                        apiKey: GetEnv(ApiKeyEnvVar) ?? GetEnv("OLLAMA_API_KEY"));

                case "anthropic":
                case "claude":
                    return new AnthropicProvider(
                        Key("ANTHROPIC_API_KEY", provider),
                        Model(provider),
                        baseUrl: GetEnv(BaseUrlEnvVar));

                case "gemini":
                case "google":
                    return new GeminiProvider(
                        Key("GEMINI_API_KEY", "GOOGLE_API_KEY", provider),
                        Model(provider),
                        baseUrl: GetEnv(BaseUrlEnvVar));

                default:
                    throw new InvalidOperationException(
                        $"Unknown PROMPT_PROVIDER value '{provider}'. Supported: azure, openai, " +
                        "anthropic, gemini, mistral, groq, deepseek, grok, openrouter, together, " +
                        "fireworks, ollama.");
            }
        }

        // ──────────────── Env helpers ────────────────

        private static string Model(string provider)
        {
            string? model = GetEnv(ModelEnvVar);
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException(
                    $"Provider '{provider}' requires a model. Set {ModelEnvVar} " +
                    "(e.g. PROMPT_MODEL=gpt-4o-mini).");
            return model!;
        }

        private static string Key(string conventional, string provider)
            => Key(conventional, null, provider);

        private static string Key(string conventional, string? alternate, string provider)
        {
            string? key = GetEnv(ApiKeyEnvVar)
                ?? GetEnv(conventional)
                ?? (alternate != null ? GetEnv(alternate) : null);

            if (string.IsNullOrWhiteSpace(key))
            {
                string names = alternate == null
                    ? $"{ApiKeyEnvVar} or {conventional}"
                    : $"{ApiKeyEnvVar}, {conventional}, or {alternate}";
                throw new InvalidOperationException(
                    $"Provider '{provider}' requires an API key. Set {names}.");
            }

            return key!;
        }

        /// <summary>
        /// Reads an environment variable with the same cross-platform fallback
        /// chain used elsewhere in the library: Process → User → Machine
        /// (the latter two only on Windows). Empty/whitespace values are
        /// treated as unset.
        /// </summary>
        private static string? GetEnv(string name)
        {
            var value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

            if (string.IsNullOrWhiteSpace(value) && OperatingSystem.IsWindows())
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
                if (string.IsNullOrWhiteSpace(value))
                    value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            }

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
