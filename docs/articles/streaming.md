# Streaming Responses

Parse and extract structured content from LLM streaming responses in real time using `PromptStreamParser` and `StreamChunk`.

## Overview

When working with streaming chat completions, responses arrive as incremental text chunks. `PromptStreamParser` processes these chunks as they arrive and extracts structured content — code blocks, JSON objects, lists, tables, headings, and key-value pairs — without waiting for the full response.

This is useful for:

- **Real-time UIs** — render code blocks or tables as soon as they're complete
- **Pipeline processing** — feed extracted JSON into downstream handlers immediately
- **Progress feedback** — show users structured content as it streams in

## StreamChunk

Each streaming chunk is represented by `StreamChunk`:

```csharp
var chunk = new StreamChunk
{
    Delta = "Hello",        // Incremental text in this chunk
    FullText = "Hello",     // Accumulated text so far
    IsComplete = false,     // True only on the final chunk
    FinishReason = null,    // "stop", "length", etc. (final chunk only)
    TokensUsed = 1          // Approximate running token count
};
```

| Property | Type | Description |
|----------|------|-------------|
| `Delta` | `string` | Incremental text received in this chunk |
| `FullText` | `string` | Full accumulated text from all chunks so far |
| `IsComplete` | `bool` | Whether this is the final chunk |
| `FinishReason` | `string?` | Stop reason (`"stop"`, `"length"`) — final chunk only |
| `TokensUsed` | `int` | Approximate running token count |

## Basic Usage

```csharp
using Prompt;

var parser = new PromptStreamParser();

// Subscribe to content extraction events
parser.OnContent += (sender, e) =>
{
    Console.WriteLine($"[{e.Content.Type}] {e.Content.Content}");
};

// Feed chunks as they arrive from the API
foreach (var chunk in streamingResponse)
{
    parser.Feed(chunk);
}

// Finalize and get summary
StreamParserSummary summary = parser.Complete();
Console.WriteLine($"Extracted {summary.Items.Count} items from {summary.TotalChunks} chunks");
```

You can also feed raw strings instead of `StreamChunk` objects:

```csharp
parser.Feed("Here is some ```csharp\nConsole.WriteLine(\"hi\");\n```");
```

## Content Types

The parser recognizes these content types (`StreamContentType`):

| Type | Detection | Example |
|------|-----------|---------|
| `CodeBlock` | Fenced with triple backticks | `` ```csharp ... ``` `` |
| `JsonObject` | Balanced `{ ... }` with valid JSON | `{"key": "value"}` |
| `JsonArray` | Balanced `[ ... ]` with valid JSON | `[1, 2, 3]` |
| `List` | Lines starting with `- `, `* `, `+ `, or `1. ` | `- item one` |
| `Table` | Lines starting with `\|` (2+ rows) | `\| col1 \| col2 \|` |
| `KeyValue` | `Key: Value` pattern | `Status: Active` |
| `Heading` | Lines starting with `#` | `## Section Title` |
| `Text` | Everything not matched above | Plain paragraphs |

Each extracted item is a `StreamContent` object:

```csharp
public class StreamContent
{
    public StreamContentType Type { get; init; }  // What was extracted
    public string Content { get; init; }           // The raw text
    public string? Tag { get; init; }              // Language for code, level for headings, key for KV
    public object? Parsed { get; init; }           // Parsed JSON (JsonElement) for JSON types
    public int StartOffset { get; init; }          // Start position in full response
    public int EndOffset { get; init; }            // End position in full response
    public bool IsPartial { get; init; }           // True if content is still streaming
}
```

## Parser Options

Configure the parser with `StreamParserOptions`:

```csharp
var parser = new PromptStreamParser(new StreamParserOptions
{
    // Only extract code blocks and JSON
    EnabledTypes = new HashSet<StreamContentType>
    {
        StreamContentType.CodeBlock,
        StreamContentType.JsonObject
    },

    ParseJson = true,        // Attempt to deserialize JSON (default: true)
    EmitPartial = false,     // Emit incomplete items? (default: false)
    MaxContentLength = 5000, // Truncate long content (0 = no limit)
    TrimContent = true       // Trim whitespace (default: true)
});
```

### Partial Content

When `EmitPartial = true`, the parser fires `OnPartialContent` events for items that haven't closed yet (e.g., a code block whose closing `` ``` `` hasn't arrived). Partial items have `IsPartial = true`.

```csharp
parser.OnPartialContent += (sender, e) =>
{
    Console.WriteLine($"[partial {e.Content.Type}] {e.Content.Content.Length} chars so far...");
};
```

## Working with the Summary

After calling `Complete()`, you get a `StreamParserSummary` with convenient accessors:

```csharp
StreamParserSummary summary = parser.Complete();

// All extracted items
List<StreamContent> items = summary.Items;

// Filtered by type
List<StreamContent> codeBlocks = summary.CodeBlocks;
List<StreamContent> jsonObjects = summary.JsonObjects;
List<StreamContent> tables = summary.Tables;
List<StreamContent> headings = summary.Headings;

// Key-value pairs as a dictionary
Dictionary<string, string> kvPairs = summary.KeyValues;

// Counts by type
Dictionary<StreamContentType, int> counts = summary.TypeCounts;

// Stats
int totalChars = summary.TotalCharacters;
int totalChunks = summary.TotalChunks;
```

## Real-time Content Extraction Example

```csharp
var parser = new PromptStreamParser();

parser.OnContent += (sender, e) =>
{
    switch (e.Content.Type)
    {
        case StreamContentType.CodeBlock:
            var lang = e.Content.Tag ?? "text";
            RenderCodeBlock(lang, e.Content.Content);
            break;

        case StreamContentType.JsonObject:
            var json = (JsonElement)e.Content.Parsed!;
            ProcessStructuredData(json);
            break;

        case StreamContentType.Heading:
            UpdateTableOfContents(e.Content.Tag!, e.Content.Content);
            break;
    }
};

// Feed chunks from Azure OpenAI streaming response
await foreach (var update in client.GetChatCompletionsStreamingAsync(options))
{
    if (update.ContentUpdate is { } delta)
    {
        parser.Feed(new StreamChunk
        {
            Delta = delta,
            FullText = accumulatedText += delta,
            IsComplete = false
        });
    }
}

var summary = parser.Complete();
```

## Peeking During Streaming

Use `CurrentContent` to inspect extracted items without finalizing the parser:

```csharp
// Check what's been extracted so far (mid-stream)
IReadOnlyList<StreamContent> current = parser.CurrentContent;
if (current.Any(c => c.Type == StreamContentType.JsonObject))
{
    // Already got structured data, can start processing
}
```

## Resetting the Parser

Reuse a parser instance for multiple streams:

```csharp
parser.Reset();  // Clears all state, ready for a new stream
```

## See Also

- [Error Handling](error-handling.md) — handling stream interruptions
- [Options](options.md) — configuring streaming parameters
- [Advanced Features](advanced-features.md) — combining streaming with other features
