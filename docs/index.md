# Prompt — Azure OpenAI Library

Welcome to the documentation for **Prompt**, a lightweight .NET 8 library for Azure OpenAI chat completions.

## Getting Started

Install via NuGet:

```bash
dotnet add package prompt-llm-aoi
```

## Quick Example

```csharp
using Prompt;

string? response = await Main.GetResponseAsync("Explain quantum computing.");
Console.WriteLine(response);
```

## Features

- **Single method call** — `GetResponseAsync()` handles everything
- **Automatic retries** — Exponential backoff for 429 and 503 errors
- **System prompts** — Control assistant behavior
- **Cancellation support** — Pass `CancellationToken` to cancel requests
- **Connection pooling** — Thread-safe singleton client
- **Cross-platform** — Works on Windows, Linux, and macOS

## Navigation

- [API Reference](api/) — Full class and method documentation
- [Changelog](../CHANGELOG.md) — Version history
- [GitHub Repository](https://github.com/sauravbhattacharya001/prompt)
- [NuGet Package](https://www.nuget.org/packages/prompt-llm-aoi)
