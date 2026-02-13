# Prompt

A lightweight C# library for connecting to **Azure OpenAI (AOI)** and sending chat completions requests. Send prompts, receive responses, and integrate AI capabilities into your .NET applications.

## Prerequisites

- [.NET 6.0](https://dotnet.microsoft.com/download/dotnet/6.0) or later
- An [Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service) resource with a deployed model

## Installation

Install via NuGet:

```bash
dotnet add package prompt-llm-aoi
```

## Configuration

Set the following **user-level** environment variables:

| Variable | Description |
|---|---|
| `AZURE_OPENAI_API_URI` | Your Azure OpenAI endpoint URI |
| `AZURE_OPENAI_API_KEY` | Your Azure OpenAI API key |
| `AZURE_OPENAI_API_MODEL` | The deployed model name (e.g. `gpt-4`) |

## Usage

```csharp
using Prompt;

string? response = await Main.GetResponseTest("Explain quantum computing in simple terms.");

Console.WriteLine(response);
```

## How It Works

The library reads your Azure OpenAI credentials from environment variables and uses the official `Azure.AI.OpenAI` SDK to send a chat completions request with sensible defaults:

- **Temperature:** 0.7
- **Max Tokens:** 800
- **Nucleus Sampling:** 0.95

## License

[MIT](LICENSE)
