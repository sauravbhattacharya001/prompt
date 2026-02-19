# Getting Started

This guide walks you through setting up and using the Prompt library for Azure OpenAI chat completions.

## Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- An [Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service) resource with a deployed model

## Installation

```bash
dotnet add package prompt-llm-aoi
```

## Configuration

Set the following environment variables before running your application:

| Variable | Description | Example |
|---|---|---|
| `AZURE_OPENAI_API_URI` | Your Azure OpenAI endpoint URI | `https://myresource.openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | Your Azure OpenAI API key | `sk-...` |
| `AZURE_OPENAI_API_MODEL` | The deployed model name | `gpt-4` |

### Setting Environment Variables

**Windows (PowerShell):**
```powershell
[Environment]::SetEnvironmentVariable("AZURE_OPENAI_API_URI", "https://myresource.openai.azure.com/", "User")
[Environment]::SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "your-key-here", "User")
[Environment]::SetEnvironmentVariable("AZURE_OPENAI_API_MODEL", "gpt-4", "User")
```

**Linux/macOS:**
```bash
export AZURE_OPENAI_API_URI="https://myresource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-key-here"
export AZURE_OPENAI_API_MODEL="gpt-4"
```

## Basic Usage

```csharp
using Prompt;

// Simple prompt
string? response = await Main.GetResponseAsync("Explain quantum computing in simple terms.");
Console.WriteLine(response);
```

## System Prompts

Control the assistant's behavior with a system prompt:

```csharp
string? response = await Main.GetResponseAsync(
    "Summarize this text: ...",
    systemPrompt: "You are a concise summarizer. Respond in 2-3 sentences max.");
```

## Cancellation

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

## Custom Retry Policy

Adjust the retry count for transient failures:

```csharp
// 5 retries for high-reliability scenarios
string? response = await Main.GetResponseAsync("Hello!", maxRetries: 5);
```

## Resetting the Client

If you change environment variables at runtime, force the client to re-read them:

```csharp
Main.ResetClient();
// Next call will use updated environment variables
```

## Next Steps

- [Conversations](conversations.md) — Multi-turn dialogue with message history
- [Templates](templates.md) — Reusable prompts with `{{variable}}` placeholders
- [Prompt Chains](chains.md) — Multi-step reasoning pipelines
- [Model Options](options.md) — Temperature, tokens, and preset configurations
- [API Reference](../api/) — Full class and method documentation
