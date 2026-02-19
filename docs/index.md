# Prompt — Azure OpenAI Library

Welcome to the documentation for **Prompt**, a lightweight .NET 8 library for Azure OpenAI chat completions.

## Overview

Prompt provides a clean, minimal API for integrating Azure OpenAI into .NET applications. It handles the boilerplate — connection pooling, retry policies, environment configuration — so you can focus on building.

### Key Features

- **Single-call prompts** — `Main.GetResponseAsync()` for quick one-shot interactions
- **Multi-turn conversations** — `Conversation` maintains full message history across turns
- **Template engine** — `PromptTemplate` with `{{variable}}` placeholders and composition
- **Prompt chaining** — `PromptChain` pipes outputs between steps for multi-step reasoning
- **Model presets** — `PromptOptions` with factory methods for code generation, creative writing, summarization, and data extraction
- **Automatic retries** — Exponential backoff for 429 rate-limit and 503 service errors
- **Serialization** — Save/load conversations, templates, and chains as JSON
- **Thread-safe** — Singleton client with connection pooling, safe for concurrent use

## Quick Start

```bash
dotnet add package prompt-llm-aoi
```

```csharp
using Prompt;

// One-shot prompt
string? response = await Main.GetResponseAsync("Explain quantum computing.");
Console.WriteLine(response);
```

## Documentation

| Guide | Description |
|-------|-------------|
| [Getting Started](articles/getting-started.md) | Installation, configuration, and first prompt |
| [Conversations](articles/conversations.md) | Multi-turn dialogue with history and serialization |
| [Templates](articles/templates.md) | Reusable prompts with `{{variables}}` and composition |
| [Prompt Chains](articles/chains.md) | Multi-step reasoning pipelines |
| [Model Options](articles/options.md) | Temperature, tokens, penalties, and presets |
| [API Reference](api/) | Full class and method documentation |

## Links

- [GitHub Repository](https://github.com/sauravbhattacharya001/prompt)
- [NuGet Package](https://www.nuget.org/packages/prompt-llm-aoi)
- [Changelog](https://github.com/sauravbhattacharya001/prompt/blob/main/CHANGELOG.md)
