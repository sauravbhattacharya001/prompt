<div align="center">

# 🤖 Prompt

**A lightweight .NET library for Azure OpenAI chat completions**

[![NuGet](https://img.shields.io/nuget/v/prompt-llm-aoi?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/prompt-llm-aoi)
[![NuGet Downloads](https://img.shields.io/nuget/dt/prompt-llm-aoi?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/prompt-llm-aoi)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/sauravbhattacharya001/prompt/codeql.yml?style=flat-square&label=CodeQL&logo=github)](https://github.com/sauravbhattacharya001/prompt/actions/workflows/codeql.yml)

Send prompts to Azure OpenAI and get responses — with built-in retry logic, cancellation support, and singleton client management. Zero boilerplate.

[Installation](#installation) · [Quick Start](#quick-start) · [API Reference](#api-reference) · [Changelog](CHANGELOG.md)

</div>

---

## ✨ Features

- **Single method call** — `GetResponseAsync()` handles everything
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

// Simple prompt
string? response = await Main.GetResponseAsync("Explain quantum computing in simple terms.");
Console.WriteLine(response);
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
