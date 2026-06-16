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
    /// <see cref="ILlmProvider"/> for Anthropic's Claude Messages API
    /// (<c>POST https://api.anthropic.com/v1/messages</c>). Any system-role
    /// message is lifted out of the message array into the top-level
    /// <c>system</c> field, <c>max_tokens</c> is always sent (the API requires
    /// it), and the remaining messages are emitted as user/assistant turns.
    /// </summary>
    /// <remarks>
    /// Authentication uses the <c>x-api-key</c> header together with the
    /// <c>anthropic-version: 2023-06-01</c> header. Non-streaming responses carry
    /// the text at <c>content[0].text</c>; streaming responses are decoded from
    /// the <c>content_block_delta</c> events' <c>delta.text</c> field.
    /// </remarks>
    public sealed class AnthropicProvider : HttpLlmProviderBase
    {
        /// <summary>Default Anthropic Messages API endpoint.</summary>
        public const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";

        /// <summary>Anthropic API version header value.</summary>
        public const string AnthropicVersion = "2023-06-01";

        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _model;

        /// <summary>
        /// Creates a Claude Messages API provider.
        /// </summary>
        /// <param name="apiKey">Anthropic API key (sent as <c>x-api-key</c>).</param>
        /// <param name="model">Model name, for example <c>claude-3-5-sonnet-latest</c>.</param>
        /// <param name="baseUrl">Optional endpoint override; defaults to <see cref="DefaultEndpoint"/>.</param>
        /// <param name="handler">Optional message handler for testing.</param>
        public AnthropicProvider(string apiKey, string model, string? baseUrl = null, HttpMessageHandler? handler = null)
            : base(handler)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("Model cannot be null or empty.", nameof(model));

            _endpoint = string.IsNullOrWhiteSpace(baseUrl) ? DefaultEndpoint : baseUrl!;
            _apiKey = apiKey;
            _model = model;
        }

        // ──────────────── Request shaping ────────────────

        private void WriteBody(Utf8JsonWriter w, IReadOnlyList<ChatMsg> messages, PromptOptions options, bool stream)
        {
            // Collect system messages into a single top-level instruction.
            string? system = null;
            var sb = new StringBuilder();
            foreach (var msg in messages)
            {
                if (msg.Role == ChatMsg.System)
                {
                    if (sb.Length > 0)
                        sb.Append('\n');
                    sb.Append(msg.Content);
                }
            }
            if (sb.Length > 0)
                system = sb.ToString();

            w.WriteStartObject();
            w.WriteString("model", _model);
            // max_tokens is REQUIRED by the Messages API.
            w.WriteNumber("max_tokens", options.MaxTokens);
            w.WriteNumber("temperature", options.Temperature);
            w.WriteNumber("top_p", options.TopP);

            if (system != null)
                w.WriteString("system", system);

            w.WritePropertyName("messages");
            w.WriteStartArray();
            foreach (var msg in messages)
            {
                if (msg.Role == ChatMsg.System)
                    continue;

                w.WriteStartObject();
                // Anthropic only accepts "user" and "assistant" roles here.
                w.WriteString("role", msg.Role == ChatMsg.Assistant ? "assistant" : "user");

                w.WritePropertyName("content");
                w.WriteStartArray();
                w.WriteStartObject();
                w.WriteString("type", "text");
                w.WriteString("text", msg.Content);
                w.WriteEndObject();
                w.WriteEndArray();

                w.WriteEndObject();
            }
            w.WriteEndArray();

            if (stream)
                w.WriteBoolean("stream", true);

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
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);

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
            if (!root.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array
                || content.GetArrayLength() == 0)
            {
                return null;
            }

            // Concatenate every text block so multi-block replies are preserved.
            var sb = new StringBuilder();
            foreach (JsonElement block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() == "text"
                    && block.TryGetProperty("text", out var text)
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
                string? stopReason = null;
                bool isMessageStop = false;

                using (JsonDocument doc = JsonDocument.Parse(payload))
                {
                    JsonElement root = doc.RootElement;
                    string? type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString()
                        : null;

                    switch (type)
                    {
                        case "content_block_delta":
                            if (root.TryGetProperty("delta", out var d)
                                && d.TryGetProperty("text", out var textEl)
                                && textEl.ValueKind == JsonValueKind.String)
                            {
                                delta = textEl.GetString() ?? "";
                            }
                            break;

                        case "message_delta":
                            if (root.TryGetProperty("delta", out var md)
                                && md.TryGetProperty("stop_reason", out var sr)
                                && sr.ValueKind == JsonValueKind.String)
                            {
                                stopReason = sr.GetString();
                            }
                            break;

                        case "message_stop":
                            isMessageStop = true;
                            break;
                    }
                }

                if (stopReason != null)
                    finishReason = stopReason;

                if (isMessageStop)
                    break;

                if (delta.Length == 0)
                    continue;

                accumulated.Append(delta);
                string fullTextSoFar = accumulated.ToString();

                yield return new StreamChunk
                {
                    Delta = delta,
                    FullText = fullTextSoFar,
                    IsComplete = false,
                    FinishReason = null,
                    TokensUsed = PromptGuard.EstimateTokens(fullTextSoFar)
                };
            }

            string finalText = accumulated.ToString();
            yield return new StreamChunk
            {
                Delta = "",
                FullText = finalText,
                IsComplete = true,
                FinishReason = finishReason ?? "stop",
                TokensUsed = PromptGuard.EstimateTokens(finalText)
            };
        }
    }
}
