<div align="center">

# 🤖 Prompt

**A lightweight .NET library for Azure OpenAI chat completions**

[![NuGet](https://img.shields.io/nuget/v/prompt-llm-aoi?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/prompt-llm-aoi)
[![NuGet Downloads](https://img.shields.io/nuget/dt/prompt-llm-aoi?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/prompt-llm-aoi)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/sauravbhattacharya001/prompt/codeql.yml?style=flat-square&label=CodeQL&logo=github)](https://github.com/sauravbhattacharya001/prompt/actions/workflows/codeql.yml)
[![CI](https://img.shields.io/github/actions/workflow/status/sauravbhattacharya001/prompt/ci.yml?style=flat-square&label=CI&logo=github)](https://github.com/sauravbhattacharya001/prompt/actions/workflows/ci.yml)
[![codecov](https://img.shields.io/codecov/c/github/sauravbhattacharya001/prompt?style=flat-square&logo=codecov)](https://codecov.io/gh/sauravbhattacharya001/prompt)

Send prompts to Azure OpenAI and get responses — with built-in retry logic, cancellation support, and singleton client management. Zero boilerplate.

[Installation](#installation) · [Quick Start](#quick-start) · [API Reference](#api-reference) · [Changelog](CHANGELOG.md)

</div>

---

## ✨ Features

- **Single method call** — `GetResponseAsync()` handles everything
- **Multi-turn conversations** — `Conversation` class maintains message history across turns
- **Configurable parameters** — Temperature, max tokens, top-p, frequency/presence penalty per conversation
- **Automatic retries** — Exponential backoff for 429 rate-limit and 503 errors
- **System prompts** — Set assistant behavior with an optional parameter
- **Cancellation support** — Pass `CancellationToken` to cancel long-running requests
- **Connection pooling** — Thread-safe singleton client with double-check locking
- **Cross-platform** — Environment variable resolution works on Windows, Linux, and macOS
- **NuGet ready** — Published as [`prompt-llm-aoi`](https://www.nuget.org/packages/prompt-llm-aoi)

## Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- An [Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service) resource with a deployed model

## Installation

```bash
dotnet add package prompt-llm-aoi
```

## Configuration

Set the following environment variables:

| Variable | Description | Example |
|---|---|---|
| `AZURE_OPENAI_API_URI` | Your Azure OpenAI endpoint URI | `https://myresource.openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | Your Azure OpenAI API key | `sk-...` |
| `AZURE_OPENAI_API_MODEL` | The deployed model name | `gpt-4` |

> **Note:** On Windows, the library checks Process → User → Machine scopes. On Linux/macOS, it reads from the process environment (shell exports, Docker env, systemd, etc.).

## Quick Start

```csharp
using Prompt;

// Simple prompt (single-turn)
string? response = await Main.GetResponseAsync("Explain quantum computing in simple terms.");
Console.WriteLine(response);
```

## Multi-Turn Conversations

The `Conversation` class maintains message history so the model has full context:

```csharp
using Prompt;

var conv = new Conversation("You are a helpful math tutor.");

string? r1 = await conv.SendAsync("What is 2+2?");
Console.WriteLine(r1); // "4"

string? r2 = await conv.SendAsync("Now multiply that by 3.");
Console.WriteLine(r2); // "12" — the model remembers the context!

string? r3 = await conv.SendAsync("What was my first question?");
Console.WriteLine(r3); // It knows: "What is 2+2?"
```

### Customizing Parameters

Each conversation can have its own model parameters:

```csharp
var conv = new Conversation("You are a creative writer.")
{
    Temperature = 1.2f,     // More creative
    MaxTokens = 2000,       // Longer responses
    TopP = 0.9f,
    FrequencyPenalty = 0.5f // Less repetition
};

string? story = await conv.SendAsync("Write a short story about a robot.");
```

### Replaying Conversations

Inject prior messages to give the model context from a previous session:

```csharp
var conv = new Conversation("You are a coding assistant.");
conv.AddUserMessage("How do I sort a list in C#?");
conv.AddAssistantMessage("Use list.Sort() for in-place sorting or list.OrderBy() for LINQ.");

// Now continue the conversation with full context
string? response = await conv.SendAsync("Show me the LINQ version with a custom comparer.");
```

### Conversation History

Export the conversation for logging, serialization, or display:

```csharp
var conv = new Conversation("System prompt");
conv.AddUserMessage("Hello");
conv.AddAssistantMessage("Hi there!");

List<(string Role, string Content)> history = conv.GetHistory();
foreach (var (role, content) in history)
    Console.WriteLine($"[{role}] {content}");

// [system] System prompt
// [user] Hello
// [assistant] Hi there!
```

### Clearing History

Reset the conversation while preserving the system prompt:

```csharp
var conv = new Conversation("You are helpful.");
conv.AddUserMessage("Hello");
conv.Clear(); // Removes user/assistant messages, keeps system prompt
```

## Usage Examples

### System Prompt

Set the assistant's behavior to control the style of responses:

```csharp
string? response = await Main.GetResponseAsync(
    "Summarize this text: ...",
    systemPrompt: "You are a concise summarizer. Respond in 2-3 sentences max.");
```

### Cancellation

Cancel long-running requests with a timeout:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    string? response = await Main.GetResponseAsync("Hello!", cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Request timed out.");
}
```

### Custom Retry Policy

Adjust the retry count for transient failures:

```csharp
// Default: 3 retries with exponential backoff
string? response = await Main.GetResponseAsync("Hello!");

// Custom: 5 retries for high-reliability scenarios
string? response = await Main.GetResponseAsync("Hello!", maxRetries: 5);
```

### Reset Client

Force the client to re-read environment variables (useful for runtime config changes):

```csharp
Main.ResetClient();
```

## API Reference

### `Main.GetResponseAsync()`

```csharp
public static async Task<string?> GetResponseAsync(
    string prompt,
    string? systemPrompt = null,
    int maxRetries = 3,
    CancellationToken cancellationToken = default)
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `prompt` | `string` | *(required)* | The user prompt to send |
| `systemPrompt` | `string?` | `null` | Optional system prompt for assistant behavior |
| `maxRetries` | `int` | `3` | Maximum retries for transient failures |
| `cancellationToken` | `CancellationToken` | `default` | Token to cancel the operation |

**Returns:** `Task<string?>` — The model's response text, or `null` if no response was generated.

**Throws:**
- `ArgumentException` — if `prompt` is null or empty
- `ArgumentOutOfRangeException` — if `maxRetries` is negative
- `InvalidOperationException` — if required environment variables are missing
- `OperationCanceledException` — if cancelled via token

### `Main.ResetClient()`

```csharp
public static void ResetClient()
```

Clears the cached client, forcing re-initialization on the next call. Thread-safe.

### `Conversation` Class

```csharp
public class Conversation
```

Multi-turn conversation manager with full message history and configurable model parameters.

#### Constructor

| Parameter | Type | Default | Description |
|---|---|---|---|
| `systemPrompt` | `string?` | `null` | Optional system prompt for the entire conversation |

#### Methods

| Method | Returns | Description |
|---|---|---|
| `SendAsync(message, cancellationToken)` | `Task<string?>` | Sends a message and returns the response. Both are added to history. |
| `AddUserMessage(message)` | `void` | Adds a user message to history without calling the API. |
| `AddAssistantMessage(message)` | `void` | Adds an assistant message to history without calling the API. |
| `Clear()` | `void` | Clears history but preserves the system prompt. |
| `GetHistory()` | `List<(string Role, string Content)>` | Returns a snapshot of the conversation. |

#### Properties

| Property | Type | Default | Range | Description |
|---|---|---|---|---|
| `MessageCount` | `int` | — | — | Number of messages including system prompt |
| `Temperature` | `float` | `0.7` | `0.0–2.0` | Sampling temperature |
| `MaxTokens` | `int` | `800` | `≥ 1` | Maximum response tokens |
| `TopP` | `float` | `0.95` | `0.0–1.0` | Nucleus sampling |
| `FrequencyPenalty` | `float` | `0.0` | `-2.0–2.0` | Frequency penalty |
| `PresencePenalty` | `float` | `0.0` | `-2.0–2.0` | Presence penalty |
| `MaxRetries` | `int` | `3` | `≥ 0` | Retry count for transient failures |

### Default Model Parameters

| Parameter | Value |
|---|---|
| Temperature | 0.7 |
| Max Tokens | 800 |
| Top P | 0.95 |
| Frequency Penalty | 0 |
| Presence Penalty | 0 |

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# 12 |
| Framework | .NET 8.0 |
| SDK | [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI) 2.1.0 |
| Retry | Azure.Core pipeline (exponential backoff with jitter) |
| Security | [CodeQL](https://github.com/sauravbhattacharya001/prompt/actions/workflows/codeql.yml) |
| Package | [NuGet](https://www.nuget.org/packages/prompt-llm-aoi) |

## Architecture

```
┌─────────────────────────────────────────┐
│              Your Application           │
│                                         │
│  await Main.GetResponseAsync(prompt)    │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│          Prompt Library (this)          │
│  ┌─────────────┐  ┌──────────────────┐ │
│  │ Env Config   │  │ Singleton Client │ │
│  │ Resolution   │  │ (Thread-Safe)    │ │
│  └──────┬──────┘  └────────┬─────────┘ │
│         └─────────┬────────┘           │
│                   │                     │
│  ┌────────────────▼──────────────────┐ │
│  │ Azure.Core Retry Pipeline         │ │
│  │ (Exponential Backoff + Jitter)    │ │
│  └────────────────┬──────────────────┘ │
└───────────────────┼─────────────────────┘
                    │
┌───────────────────▼─────────────────────┐
│         Azure OpenAI Service            │
│     Chat Completions API (GPT-4)        │
└─────────────────────────────────────────┘
```

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

<div align="center">

Made by [Saurav Bhattacharya](https://github.com/sauravbhattacharya001)

</div>
