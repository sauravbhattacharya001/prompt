<div align="center">

# 🤖 Prompt

**A lightweight .NET library for Azure OpenAI chat completions**

[![NuGet](https://img.shields.io/nuget/v/prompt-llm-aoi?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/prompt-llm-aoi)
[![NuGet Downloads](https://img.shields.io/nuget/dt/prompt-llm-aoi?style=flat-square&logo=nuget&color=004880)](https://www.nuget.org/packages/prompt-llm-aoi)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/sauravbhattacharya001/prompt/codeql.yml?style=flat-square&label=CodeQL&logo=github)](https://github.com/sauravbhattacharya001/prompt/actions/workflows/codeql.yml)
[![CI](https://img.shields.io/github/actions/workflow/status/sauravbhattacharya001/prompt/ci.yml?style=flat-square&label=CI&logo=github)](https://github.com/sauravbhattacharya001/prompt/actions/workflows/ci.yml)
[![Publish](https://img.shields.io/github/actions/workflow/status/sauravbhattacharya001/prompt/nuget-publish.yml?style=flat-square&label=Publish&logo=github)](https://github.com/sauravbhattacharya001/prompt/actions/workflows/nuget-publish.yml)
[![codecov](https://img.shields.io/codecov/c/github/sauravbhattacharya001/prompt?style=flat-square&logo=codecov)](https://codecov.io/gh/sauravbhattacharya001/prompt)

Send prompts to Azure OpenAI and get responses — with built-in retry logic, cancellation support, and singleton client management. Zero boilerplate.

[Installation](#installation) · [Quick Start](#quick-start) · [API Reference](#api-reference) · [Changelog](CHANGELOG.md)

</div>

---

## ✨ Features

- **Single method call** — `GetResponseAsync()` handles everything
- **Multi-turn conversations** — `Conversation` class maintains message history across turns
- **Save & load conversations** — Serialize to JSON (string or file), restore later with full state
- **Configurable parameters** — `PromptOptions` class with presets (`ForCodeGeneration()`, `ForCreativeWriting()`, etc.) for temperature, max tokens, top-p, and penalties
- **Automatic retries** — Exponential backoff for 429 rate-limit and 503 errors
- **System prompts** — Set assistant behavior with an optional parameter
- **Cancellation support** — Pass `CancellationToken` to cancel long-running requests
- **Connection pooling** — Thread-safe singleton client with double-check locking
- **Cross-platform** — Environment variable resolution works on Windows, Linux, and macOS
- **Prompt templates** — Reusable `PromptTemplate` with `{{variable}}` placeholders, defaults, validation, and composition
- **Prompt chains** — `PromptChain` pipelines multiple prompts sequentially, where each step's output feeds into the next as a variable
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

### With Custom Options

Use `PromptOptions` to customize model behavior for any use case:

```csharp
using Prompt;

// Code generation — low temperature, high token limit
var codeOpts = PromptOptions.ForCodeGeneration();
string? code = await Main.GetResponseAsync(
    "Write a merge sort in C#",
    options: codeOpts);

// Creative writing — high temperature
var creativeOpts = PromptOptions.ForCreativeWriting();
string? story = await Main.GetResponseAsync(
    "Write a short story about a time-traveling cat",
    options: creativeOpts);

// Custom configuration
var custom = new PromptOptions
{
    Temperature = 0.4f,
    MaxTokens = 4000,
    TopP = 0.9f,
    FrequencyPenalty = 0.3f,
    PresencePenalty = 0.1f
};
string? result = await Main.GetResponseAsync("Summarize this article...", options: custom);
```

**Built-in presets:**

| Preset | Temperature | MaxTokens | TopP | Use Case |
|---|---|---|---|---|
| `ForCodeGeneration()` | 0.1 | 4000 | 0.95 | Deterministic code output |
| `ForCreativeWriting()` | 0.9 | 2000 | 0.9 | Stories, poems, creative text |
| `ForDataExtraction()` | 0.0 | 2000 | 1.0 | JSON, structured output |
| `ForSummarization()` | 0.3 | 1000 | 0.9 | Text summarization |

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

Each conversation can have its own model parameters, either via `PromptOptions` or individual properties:

```csharp
// Using PromptOptions (recommended)
var opts = PromptOptions.ForCreativeWriting();
var conv = new Conversation("You are a creative writer.", opts);

// Or set properties individually
var conv2 = new Conversation("You are a creative writer.")
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

### Save & Load Conversations

Save a conversation to JSON and restore it later — perfect for persisting sessions across app restarts, sharing conversations, or implementing conversation history:

```csharp
var conv = new Conversation("You are a coding tutor.");
await conv.SendAsync("Explain SOLID principles");
await conv.SendAsync("Show me an example of SRP");

// Save to JSON string
string json = conv.SaveToJson();

// Save to file
await conv.SaveToFileAsync("session.json");

// Later... restore from JSON
var restored = Conversation.LoadFromJson(json);

// Or restore from file
var fromFile = await Conversation.LoadFromFileAsync("session.json");

// Continue the conversation with full context
string? response = await fromFile.SendAsync("Now show me OCP");
```

The serialized JSON includes all messages and model parameters (temperature, max tokens, etc.), so the restored conversation is an exact replica:

```json
{
  "messages": [
    { "role": "system", "content": "You are a coding tutor." },
    { "role": "user", "content": "Explain SOLID principles" },
    { "role": "assistant", "content": "SOLID stands for..." }
  ],
  "parameters": {
    "temperature": 0.7,
    "maxTokens": 800,
    "topP": 0.95,
    "frequencyPenalty": 0,
    "presencePenalty": 0,
    "maxRetries": 3
  }
}
```

## Prompt Templates

The `PromptTemplate` class lets you define reusable prompts with `{{variable}}` placeholders. Set defaults, validate inputs, compose templates together, and serialize them for sharing.

### Basic Usage

```csharp
using Prompt;

var template = new PromptTemplate(
    "You are a {{role}} assistant. Help the user with {{topic}}.",
    new Dictionary<string, string> { ["role"] = "helpful" }
);

// Render with variables (role uses default, topic is provided)
string prompt = template.Render(new Dictionary<string, string>
{
    ["topic"] = "C# programming"
});
// → "You are a helpful assistant. Help the user with C# programming."
```

### Variable Introspection

```csharp
var template = new PromptTemplate(
    "Translate {{text}} from {{source}} to {{target}}.",
    new Dictionary<string, string> { ["source"] = "English" }
);

HashSet<string> all = template.GetVariables();
// { "text", "source", "target" }

HashSet<string> required = template.GetRequiredVariables();
// { "text", "target" } — source has a default
```

### Strict vs. Non-Strict Rendering

```csharp
var template = new PromptTemplate("Hello {{name}}, you are {{role}}!");

// Strict (default) — throws if variables are missing
template.Render(); // ❌ InvalidOperationException

// Non-strict — leaves unresolved placeholders as-is
string result = template.Render(strict: false);
// → "Hello {{name}}, you are {{role}}!"
```

### Composing Templates

Chain templates together to build complex prompts from reusable parts:

```csharp
var persona = new PromptTemplate(
    "You are a {{role}} with expertise in {{domain}}.",
    new Dictionary<string, string> { ["role"] = "senior developer" }
);

var task = new PromptTemplate(
    "Review this code and suggest improvements:\n{{code}}");

var combined = persona.Compose(task);

string prompt = combined.Render(new Dictionary<string, string>
{
    ["domain"] = "C#",
    ["code"] = "public void Foo() { /* ... */ }"
});
```

### Render & Send in One Call

Skip the manual render step — send directly to Azure OpenAI:

```csharp
// Single-turn
var template = new PromptTemplate("Explain {{concept}} in simple terms.");
string? response = await template.RenderAndSendAsync(
    new Dictionary<string, string> { ["concept"] = "recursion" },
    systemPrompt: "You are a teacher."
);

// Multi-turn (with existing Conversation)
var conv = new Conversation("You are a coding tutor.");
await template.RenderAndSendAsync(conv,
    new Dictionary<string, string> { ["concept"] = "closures" });
```

### Save & Load Templates

```csharp
var template = new PromptTemplate(
    "Summarize {{text}} in {{style}} style.",
    new Dictionary<string, string> { ["style"] = "concise" }
);

// Save to file
await template.SaveToFileAsync("summarizer.json");

// Load from file
var loaded = await PromptTemplate.LoadFromFileAsync("summarizer.json");

// Or use JSON strings directly
string json = template.ToJson();
var restored = PromptTemplate.FromJson(json);
```

## Prompt Chains

The `PromptChain` class lets you build multi-step LLM pipelines where each step's output automatically becomes a variable for subsequent steps. Perfect for summarize-then-translate, extract-then-analyze, or any sequential reasoning pattern.

### Basic Chain

```csharp
using Prompt;

var chain = new PromptChain()
    .AddStep("summarize",
        new PromptTemplate("Summarize this text in 2 sentences: {{text}}"),
        "summary")
    .AddStep("translate",
        new PromptTemplate("Translate to French: {{summary}}"),
        "french")
    .AddStep("keywords",
        new PromptTemplate("Extract 5 keywords from: {{summary}}"),
        "keywords");

var result = await chain.RunAsync(new Dictionary<string, string>
{
    ["text"] = "Your long article text here..."
});

Console.WriteLine(result.FinalResponse);          // keywords output
Console.WriteLine(result.GetOutput("summary"));   // the summary
Console.WriteLine(result.GetOutput("french"));    // the French translation
```

### Chain Configuration

```csharp
var chain = new PromptChain()
    .WithSystemPrompt("You are a precise analyst.")
    .WithMaxRetries(5)
    .AddStep("extract",
        new PromptTemplate("Extract key facts from: {{document}}"),
        "facts")
    .AddStep("analyze",
        new PromptTemplate("Analyze these facts for trends: {{facts}}"),
        "analysis");
```

### Validation

Check that all variables are satisfied before running (no API calls):

```csharp
var chain = new PromptChain()
    .AddStep("s1", new PromptTemplate("Process: {{input}}"), "result")
    .AddStep("s2", new PromptTemplate("Refine: {{result}}"), "final");

List<string> errors = chain.Validate(
    new Dictionary<string, string> { ["input"] = "test" });

if (errors.Count == 0)
    Console.WriteLine("Chain is valid!");
else
    errors.ForEach(Console.WriteLine);
```

### Chain Results

Every step is tracked with timing, rendered prompts, and responses:

```csharp
var result = await chain.RunAsync(initialVars);

Console.WriteLine($"Total time: {result.TotalElapsed.TotalSeconds}s");
Console.WriteLine($"Steps: {result.Steps.Count}");

foreach (var step in result.Steps)
{
    Console.WriteLine($"  [{step.StepName}] {step.Elapsed.TotalMilliseconds}ms");
    Console.WriteLine($"    Prompt: {step.RenderedPrompt}");
    Console.WriteLine($"    Response: {step.Response}");
}

// Export results as JSON for logging/analysis
string json = result.ToJson();
```

### Save & Load Chains

```csharp
var chain = new PromptChain()
    .WithSystemPrompt("Be helpful")
    .AddStep("step1", new PromptTemplate("Summarize: {{text}}"), "summary");

// Save to file
await chain.SaveToFileAsync("my-chain.json");

// Load from file
var loaded = await PromptChain.LoadFromFileAsync("my-chain.json");

// Or use JSON strings
string chainJson = chain.ToJson();
var restored = PromptChain.FromJson(chainJson);
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
| `SaveToJson(indented)` | `string` | Serializes the conversation (messages + parameters) to a JSON string. |
| `LoadFromJson(json)` | `Conversation` | *Static.* Restores a conversation from a JSON string. |
| `SaveToFileAsync(filePath, indented, cancellationToken)` | `Task` | Saves the conversation to a JSON file. |
| `LoadFromFileAsync(filePath, cancellationToken)` | `Task<Conversation>` | *Static.* Loads a conversation from a JSON file. |

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

### `PromptTemplate` Class

```csharp
public class PromptTemplate
```

Reusable prompt template with `{{variable}}` placeholders, default values, and composition.

#### Constructor

| Parameter | Type | Default | Description |
|---|---|---|---|
| `template` | `string` | *(required)* | Template string with `{{variable}}` placeholders |
| `defaults` | `Dictionary<string, string>?` | `null` | Default values for variables |

#### Methods

| Method | Returns | Description |
|---|---|---|
| `Render(variables, strict)` | `string` | Renders the template by replacing placeholders. Strict mode throws on missing variables. |
| `RenderAndSendAsync(variables, systemPrompt, maxRetries, cancellationToken)` | `Task<string?>` | Renders and sends as a single-turn prompt via `Main.GetResponseAsync()`. |
| `RenderAndSendAsync(conversation, variables, cancellationToken)` | `Task<string?>` | Renders and sends as a message in an existing `Conversation`. |
| `GetVariables()` | `HashSet<string>` | Returns all variable names found in the template. |
| `GetRequiredVariables()` | `HashSet<string>` | Returns variable names that have no default value. |
| `SetDefault(name, value)` | `void` | Sets or updates a default value for a variable. |
| `RemoveDefault(name)` | `bool` | Removes a default value, making the variable required. |
| `Compose(other, separator)` | `PromptTemplate` | Combines two templates with merged defaults. |
| `ToJson(indented)` | `string` | Serializes the template to JSON. |
| `FromJson(json)` | `PromptTemplate` | *Static.* Deserializes a template from JSON. |
| `SaveToFileAsync(filePath, indented, cancellationToken)` | `Task` | Saves the template to a JSON file. |
| `LoadFromFileAsync(filePath, cancellationToken)` | `Task<PromptTemplate>` | *Static.* Loads a template from a JSON file. |

#### Properties

| Property | Type | Description |
|---|---|---|
| `Template` | `string` | The raw template string |
| `Defaults` | `IReadOnlyDictionary<string, string>` | Read-only copy of default values |

### `PromptChain` Class

```csharp
public class PromptChain
```

Multi-step prompt pipeline where each step's output feeds into subsequent steps as template variables.

#### Methods

| Method | Returns | Description |
|---|---|---|
| `AddStep(name, template, outputVariable)` | `PromptChain` | Adds a step to the chain (fluent). Output variable must be unique. |
| `WithSystemPrompt(systemPrompt)` | `PromptChain` | Sets the system prompt for all API calls (fluent). |
| `WithMaxRetries(maxRetries)` | `PromptChain` | Sets the retry count for API calls (fluent). |
| `RunAsync(initialVariables, cancellationToken)` | `Task<ChainResult>` | Executes all steps sequentially. |
| `Validate(initialVariables)` | `List<string>` | Checks that all required variables are satisfiable (no API calls). |
| `ToJson(indented)` | `string` | Serializes the chain definition to JSON. |
| `FromJson(json)` | `PromptChain` | *Static.* Deserializes a chain from JSON. |
| `SaveToFileAsync(filePath, indented, cancellationToken)` | `Task` | Saves the chain definition to a JSON file. |
| `LoadFromFileAsync(filePath, cancellationToken)` | `Task<PromptChain>` | *Static.* Loads a chain from a JSON file. |

#### Properties

| Property | Type | Description |
|---|---|---|
| `StepCount` | `int` | Number of steps in the chain |
| `Steps` | `IReadOnlyList<ChainStep>` | Read-only view of chain steps |

### `ChainResult` Class

| Property / Method | Type | Description |
|---|---|---|
| `Steps` | `IReadOnlyList<StepResult>` | Ordered step results |
| `Variables` | `IReadOnlyDictionary<string, string>` | All accumulated variables |
| `TotalElapsed` | `TimeSpan` | Total execution time |
| `FinalResponse` | `string?` | Last step's response (convenience) |
| `GetOutput(variableName)` | `string?` | Get a specific step's output by variable name |
| `ToJson(indented)` | `string` | Serialize results to JSON for logging |

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
