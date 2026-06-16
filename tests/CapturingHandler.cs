namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Test double that captures the outgoing <see cref="HttpRequestMessage"/>
/// (including its fully-read body) and returns a canned response. Lets the
/// provider tests assert URL, headers, and request JSON without any network I/O.
/// </summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responder;

    /// <summary>The most recent request the handler observed.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The most recent request body, read to a string.</summary>
    public string LastBody { get; private set; } = "";

    /// <summary>Number of times the handler was invoked.</summary>
    public int CallCount { get; private set; }

    public CapturingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>Creates a handler that always returns the given JSON body with status 200.</summary>
    public static CapturingHandler Json(string json)
        => new((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    /// <summary>
    /// Creates a handler that returns the given lines as an SSE <c>text/event-stream</c>
    /// body. Each element is emitted verbatim followed by a blank line.
    /// </summary>
    public static CapturingHandler Sse(IEnumerable<string> events)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in events)
        {
            sb.Append(e);
            sb.Append("\n\n");
        }
        string payload = sb.ToString();
        return new((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "text/event-stream")
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastBody = request.Content == null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return _responder(request, LastBody);
    }
}
