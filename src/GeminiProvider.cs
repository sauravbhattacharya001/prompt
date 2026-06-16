namespace Prompt
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// <see cref="ILlmProvider"/> for Google's Generative Language API
    /// (Gemini). Non-streaming calls hit
    /// <c>POST {base}/models/{model}:generateContent?key=KEY</c> and streaming
    /// calls hit <c>:streamGenerateContent?alt=sse&amp;key=KEY</c>.
    /// </summary>
    /// <remarks>
    /// Messages map onto <c>contents:[{ role, parts:[{ text }] }]</c> where the
    /// role is <c>"user"</c> or <c>"model"</c> (assistant is mapped to
    /// <c>model</c>); any system-role message is lifted into the top-level
    /// <c>systemInstruction</c>. Sampling parameters are sent under
    /// <c>generationConfig</c> (<c>temperature</c>, <c>maxOutputTokens</c>,
    /// <c>topP</c>). Response text is read from
    /// <c>candidates[0].content.parts[*].text</c>.
    /// </remarks>
    public sealed class GeminiProvider : HttpLlmProviderBase
    {
        /// <summary>Default Generative Language API base URL.</summary>
        public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        /// <summary>
        /// Creates a Gemini provider.
        /// </summary>
        /// <param name="apiKey">Google API key (sent as the <c>key</c> query parameter).</param>
        /// <param name="model">Model name, for example <c>gemini-1.5-flash</c>.</param>
        /// <param name="baseUrl">Optional base URL override; defaults to <see cref="DefaultBaseUrl"/>.</param>
        /// <param name="handler">Optional message handler for testing.</param>
        public GeminiProvider(string apiKey, string model, string? baseUrl = null, HttpMessageHandler? handler = null)
            : base(handler)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("Model cannot be null or empty.", nameof(model));

            _baseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl!).TrimEnd('/');
            _apiKey = apiKey;
            _model = model;
        }

        private string BuildUrl(bool stream)
        {
            string method = stream ? "streamGenerateContent" : "generateContent";
            string query = stream
                ? $"?alt=sse&key={Uri.EscapeDataString(_apiKey)}"
                : $"?key={Uri.EscapeDataString(_apiKey)}";
            return $"{_baseUrl}/models/{_model}:{method}{query}";
        }

        // ──────────────── Request shaping ────────────────

        private static void WriteParts(Utf8JsonWriter w, string text)
        {
            w.WritePropertyName("parts");
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("text", text);
            w.WriteEndObject();
            w.WriteEndArray();
        }

        private void WriteBody(Utf8JsonWriter w, IReadOnlyList<ChatMsg> messages, PromptOptions options)
        {
            // Gather system messages into a single systemInstruction.
            var sysBuilder = new StringBuilder();
            foreach (var msg in messages)
            {
                if (msg.Role == ChatMsg.System)
                {
                    if (sysBuilder.Length > 0)
                        sysBuilder.Append('\n');
                    sysBuilder.Append(msg.Content);
                }
            }

            w.WriteStartObject();

            if (sysBuilder.Length > 0)
            {
                w.WritePropertyName("systemInstruction");
                w.WriteStartObject();
                WriteParts(w, sysBuilder.ToString());
                w.WriteEndObject();
            }

            w.WritePropertyName("contents");
            w.WriteStartArray();
            foreach (var msg in messages)
            {
                if (msg.Role == ChatMsg.System)
                    continue;

                w.WriteStartObject();
                // Gemini uses "user" and "model"; map assistant -> model.
                w.WriteString("role", msg.Role == ChatMsg.Assistant ? "model" : "user");
                WriteParts(w, msg.Content);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WritePropertyName("generationConfig");
            w.WriteStartObject();
            w.WriteNumber("temperature", options.Temperature);
            w.WriteNumber("maxOutputTokens", options.MaxTokens);
            w.WriteNumber("topP", options.TopP);
            w.WriteEndObject();

            w.WriteEndObject();
        }

        private HttpRequestMessage BuildRequest(IReadOnlyList<ChatMsg> messages, PromptOptions options, bool stream)
        {
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                WriteBody(writer, messages, options);
            }

            var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(stream))
            {
                Content = new ByteArrayContent(ms.ToArray())
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
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
            if (!root.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = candidates[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (JsonElement part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    sb.Append(text.GetString());
                }
            }

            return sb.Length == 0 ? null : sb.ToString();
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
                    string? text = ExtractContent(doc.RootElement);
                    if (text != null)
                        delta = text;

                    if (doc.RootElement.TryGetProperty("candidates", out var candidates)
                        && candidates.ValueKind == JsonValueKind.Array
                        && candidates.GetArrayLength() > 0)
                    {
                        JsonElement c0 = candidates[0];
                        if (c0.TryGetProperty("finishReason", out var fr)
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
