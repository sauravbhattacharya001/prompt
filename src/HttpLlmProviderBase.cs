namespace Prompt
{
    using System.IO;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared plumbing for the HTTP-based <see cref="ILlmProvider"/> implementations
    /// (<see cref="OpenAICompatProvider"/>, <see cref="AnthropicProvider"/>, and
    /// <see cref="GeminiProvider"/>). Provides a shared, process-wide
    /// <see cref="HttpClient"/> by default while allowing a custom
    /// <see cref="HttpMessageHandler"/> to be injected for testing.
    /// </summary>
    public abstract class HttpLlmProviderBase : ILlmProvider
    {
        // A single shared HttpClient avoids socket exhaustion when many provider
        // instances are created. Providers set per-request URLs/headers, so the
        // shared instance carries no provider-specific default state.
        private static readonly HttpClient SharedClient = new HttpClient();

        /// <summary>The <see cref="HttpClient"/> used for requests.</summary>
        protected HttpClient Http { get; }

        /// <summary>
        /// Initializes the base with either the shared client or a client wrapping
        /// the supplied <paramref name="handler"/> (used by tests to intercept requests).
        /// </summary>
        /// <param name="handler">
        /// Optional message handler. When non-null, a dedicated <see cref="HttpClient"/>
        /// is created around it; when null, the shared client is used.
        /// </param>
        protected HttpLlmProviderBase(HttpMessageHandler? handler)
        {
            Http = handler == null ? SharedClient : new HttpClient(handler);
        }

        /// <inheritdoc />
        public abstract Task<string?> CompleteAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            CancellationToken ct = default);

        /// <inheritdoc />
        public abstract IAsyncEnumerable<StreamChunk> CompleteStreamAsync(
            IReadOnlyList<ChatMsg> messages,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken ct = default);

        /// <summary>
        /// Sends a request and reads the response body as Server-Sent Events,
        /// yielding the payload that follows each <c>data:</c> prefix (with the
        /// prefix stripped). Comment/blank lines are skipped. The caller is
        /// responsible for interpreting each payload (including <c>[DONE]</c>).
        /// </summary>
        protected async IAsyncEnumerable<string> ReadSseDataLinesAsync(
            HttpRequestMessage request,
            [EnumeratorCancellation] CancellationToken ct)
        {
            using HttpResponseMessage response = await Http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

            using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null)
                    break;

                if (line.Length == 0)
                    continue;

                // SSE comments start with ':' — ignore them (e.g. keep-alive pings).
                if (line[0] == ':')
                    continue;

                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    // Strip the prefix and a single optional leading space.
                    string payload = line.Substring(5);
                    if (payload.Length > 0 && payload[0] == ' ')
                        payload = payload.Substring(1);
                    yield return payload;
                }
            }
        }

        /// <summary>
        /// Throws an <see cref="HttpRequestException"/> with the response body
        /// appended when the status code does not indicate success. Keeps the
        /// vendor error text visible to callers for diagnostics.
        /// </summary>
        protected static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
                return;

            string body = "";
            try
            {
                body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: the status code alone is still informative.
            }

            throw new HttpRequestException(
                $"Provider request failed with status {(int)response.StatusCode} " +
                $"({response.ReasonPhrase}). {body}");
        }
    }
}
