namespace Prompt
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// <see cref="ILlmProvider"/> for any backend that speaks the OpenAI
    /// Chat Completions schema (<c>POST {baseUrl}/chat/completions</c> with a
    /// <c>{ model, messages, temperature, max_tokens, top_p, frequency_penalty,
    /// presence_penalty, stream }</c> body). A single configurable base URL,
    /// API key, and model name covers OpenAI, Mistral, Groq, DeepSeek, xAI/Grok,
    /// OpenRouter, Together, Fireworks, and a local Ollama server.
    /// </summary>
    /// <remarks>
    /// Use the named factory helpers (<see cref="OpenAI"/>, <see cref="Mistral"/>,
    /// <see cref="Groq"/>, <see cref="DeepSeek"/>, <see cref="Grok"/>,
    /// <see cref="OpenRouter"/>, <see cref="Together"/>, <see cref="Fireworks"/>,
    /// <see cref="Ollama"/>) for the common vendor base URLs, or construct
    /// directly with a custom base URL.
    /// </remarks>
    public sealed class OpenAICompatProvider : HttpLlmProviderBase
    {
        private readonly string _endpoint;
        private readonly string? _apiKey;
        private readonly string _model;

        /// <summary>
        /// Creates a provider targeting an OpenAI-compatible backend.
        /// </summary>
        /// <param name="baseUrl">
        /// The API base URL, for example <c>https://api.openai.com/v1</c>. The
        /// <c>/chat/completions</c> path is appended automatically; a trailing
        /// slash on <paramref name="baseUrl"/> is tolerated.
        /// </param>
        /// <param name="apiKey">
        /// Bearer API key. May be <c>null</c> or empty for keyless servers such
        /// as a local Ollama instance.
        /// </param>
        /// <param name="model">The model/deployment name to request.</param>
        /// <param name="handler">Optional message handler for testing.</param>
        public OpenAICompatProvider(string baseUrl, string? apiKey, string model, HttpMessageHandler? handler = null)
            : base(handler)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL cannot be null or empty.", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("Model cannot be null or empty.", nameof(model));

            _endpoint = baseUrl.TrimEnd('/') + "/chat/completions";
            _apiKey = apiKey;
            _model = model;
        }

        // ──────────────── Vendor presets ────────────────

        /// <summary>OpenAI (<c>https://api.openai.com/v1</c>).</summary>
        public static OpenAICompatProvider OpenAI(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://api.openai.com/v1", apiKey, model, handler);

        /// <summary>Mistral (<c>https://api.mistral.ai/v1</c>).</summary>
        public static OpenAICompatProvider Mistral(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://api.mistral.ai/v1", apiKey, model, handler);

        /// <summary>Groq (<c>https://api.groq.com/openai/v1</c>).</summary>
        public static OpenAICompatProvider Groq(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://api.groq.com/openai/v1", apiKey, model, handler);

        /// <summary>DeepSeek (<c>https://api.deepseek.com/v1</c>).</summary>
        public static OpenAICompatProvider DeepSeek(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://api.deepseek.com/v1", apiKey, model, handler);

        /// <summary>xAI / Grok (<c>https://api.x.ai/v1</c>).</summary>
        public static OpenAICompatProvider Grok(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://api.x.ai/v1", apiKey, model, handler);

        /// <summary>OpenRouter (<c>https://openrouter.ai/api/v1</c>).</summary>
        public static OpenAICompatProvider OpenRouter(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://openrouter.ai/api/v1", apiKey, model, handler);

        /// <summary>Together AI (<c>https://api.together.xyz/v1</c>).</summary>
        public static OpenAICompatProvider Together(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://api.together.xyz/v1", apiKey, model, handler);

        /// <summary>Fireworks AI (<c>https://api.fireworks.ai/inference/v1</c>).</summary>
        public static OpenAICompatProvider Fireworks(string apiKey, string model, HttpMessageHandler? handler = null)
            => new("https://api.fireworks.ai/inference/v1", apiKey, model, handler);

        /// <summary>
        /// A local Ollama server. The API key is optional and the base URL
        /// defaults to <c>http://localhost:11434/v1</c>.
        /// </summary>
        public static OpenAICompatProvider Ollama(string model, string? baseUrl = null, string? apiKey = null, HttpMessageHandler? handler = null)
            => new(string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434/v1" : baseUrl!, apiKey, model, handler);

        // ──────────────── Request shaping ────────────────

        private void WriteBody(Utf8JsonWriter w, IReadOnlyList<ChatMsg> messages, PromptOptions options, bool stream)
        {
            w.WriteStartObject();
            w.WriteString("model", _model);

            w.WritePropertyName("messages");
            w.WriteStartArray();
            foreach (var msg in messages)
            {
                w.WriteStartObject();
                w.WriteString("role", msg.Role);
                w.WriteString("content", msg.Content);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteNumber("temperature", options.Temperature);
            w.WriteNumber("max_tokens", options.MaxTokens);
            w.WriteNumber("top_p", options.TopP);
            w.WriteNumber("frequency_penalty", options.FrequencyPenalty);
            w.WriteNumber("presence_penalty", options.PresencePenalty);
            w.WriteBoolean("stream", stream);
            w.WriteEndObject();
        }

        private HttpRequestMessage BuildRequest(IReadOnlyList<ChatMsg> messages, PromptOptions options, bool stream)
        {
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                WriteBody(writer, messages, options, stream);
            }

            var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new ByteArrayContent(ms.ToArray())
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            if (!string.IsNullOrWhiteSpace(_apiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            return request;
        }

        /// <inheritdoc />
        public override async Task<string?> CompleteAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            CancellationToken ct = default)
        {
            using HttpRequestMessage request = BuildRequest(messages, options, stream: false);
            using HttpResponseMessage response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

            await using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            return ExtractContent(doc.RootElement);
        }

        private static string? ExtractContent(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = choices[0];
            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }

            return null;
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<StreamChunk> CompleteStreamAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            using HttpRequestMessage request = BuildRequest(messages, options, stream: true);

            var accumulated = new StringBuilder();
            string? finishReason = null;

            await foreach (string payload in ReadSseDataLinesAsync(request, ct).ConfigureAwait(false))
            {
                if (payload == "[DONE]")
                    break;

                string delta = "";
                string? chunkFinish = null;

                using (JsonDocument doc = JsonDocument.Parse(payload))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices)
                        && choices.ValueKind == JsonValueKind.Array
                        && choices.GetArrayLength() > 0)
                    {
                        JsonElement choice = choices[0];
                        if (choice.TryGetProperty("delta", out var deltaEl)
                            && deltaEl.TryGetProperty("content", out var contentEl)
                            && contentEl.ValueKind == JsonValueKind.String)
                        {
                            delta = contentEl.GetString() ?? "";
                        }

                        if (choice.TryGetProperty("finish_reason", out var fr)
                            && fr.ValueKind == JsonValueKind.String)
                        {
                            chunkFinish = fr.GetString();
                        }
                    }
                }

                if (chunkFinish != null)
                    finishReason = chunkFinish;

                if (delta.Length == 0 && chunkFinish == null)
                    continue;

                accumulated.Append(delta);
                bool isComplete = chunkFinish != null;
                string fullTextSoFar = accumulated.ToString();

                yield return new StreamChunk
                {
                    Delta = delta,
                    FullText = fullTextSoFar,
                    IsComplete = isComplete,
                    FinishReason = isComplete ? finishReason : null,
                    TokensUsed = PromptGuard.EstimateTokens(fullTextSoFar)
                };
            }

            if (finishReason == null)
            {
                string finalText = accumulated.ToString();
                yield return new StreamChunk
                {
                    Delta = "",
                    FullText = finalText,
                    IsComplete = true,
                    FinishReason = "stop",
                    TokensUsed = PromptGuard.EstimateTokens(finalText)
                };
            }
        }
    }
}
