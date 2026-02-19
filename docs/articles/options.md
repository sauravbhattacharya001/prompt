# Model Options

The `PromptOptions` class configures Azure OpenAI model parameters without exposing the Azure SDK. Use it to control temperature, token limits, sampling, and penalties across all prompt methods.

## Basic Usage

```csharp
using Prompt;

var opts = new PromptOptions
{
    Temperature = 0.3f,
    MaxTokens = 2000,
    TopP = 0.9f
};

// With Main.GetResponseAsync
string? response = await Main.GetResponseAsync(
    "Explain recursion", options: opts);

// With Conversation
var conv = new Conversation("You are a tutor.", opts);

// With PromptChain
var chain = new PromptChain()
    .WithOptions(opts)
    .AddStep("step1", template, "output");
```

## Parameters

| Parameter | Range | Default | Effect |
|-----------|-------|---------|--------|
| `Temperature` | 0.0–2.0 | 0.7 | Controls randomness. Lower = more focused and deterministic. Higher = more creative and varied. |
| `MaxTokens` | ≥ 1 | 800 | Maximum tokens in the response. |
| `TopP` | 0.0–1.0 | 0.95 | Nucleus sampling — the model considers tokens within the top probability mass. Alternative to Temperature. |
| `FrequencyPenalty` | -2.0–2.0 | 0.0 | Penalizes tokens based on how often they appear. Positive values reduce repetition. |
| `PresencePenalty` | -2.0–2.0 | 0.0 | Penalizes tokens based on whether they've appeared at all. Positive values encourage topic diversity. |

### Parameter Guidelines

**Temperature vs TopP:** Generally, adjust one or the other — not both. Temperature controls the shape of the probability distribution; TopP truncates it.

- **Temperature 0.0–0.2:** Deterministic, factual answers. Best for code, math, data extraction.
- **Temperature 0.5–0.7:** Balanced. Good default for most tasks.
- **Temperature 0.8–1.2:** Creative, varied outputs. Good for writing, brainstorming.

## Factory Presets

`PromptOptions` includes static factory methods for common use cases:

### Code Generation

```csharp
var opts = PromptOptions.ForCodeGeneration();
// Temperature: 0.1, MaxTokens: 4000, TopP: 0.95
// Low temperature for precise, deterministic code
```

### Creative Writing

```csharp
var opts = PromptOptions.ForCreativeWriting();
// Temperature: 0.9, MaxTokens: 2000, TopP: 0.9
// High temperature for varied, imaginative output
```

### Data Extraction

```csharp
var opts = PromptOptions.ForDataExtraction();
// Temperature: 0.0, MaxTokens: 2000, TopP: 1.0
// Zero temperature for maximum determinism
```

### Summarization

```csharp
var opts = PromptOptions.ForSummarization();
// Temperature: 0.3, MaxTokens: 1000, TopP: 0.9
// Moderate temperature for faithful but readable summaries
```

## Custom Presets

Build your own presets:

```csharp
public static class MyPresets
{
    // Chatbot — warm and conversational
    public static PromptOptions Chatbot => new()
    {
        Temperature = 0.8f,
        MaxTokens = 500,
        TopP = 0.9f,
        PresencePenalty = 0.6f  // encourage varied responses
    };

    // Classification — deterministic labeling
    public static PromptOptions Classification => new()
    {
        Temperature = 0.0f,
        MaxTokens = 50,
        TopP = 1.0f
    };

    // Long-form — detailed technical writing
    public static PromptOptions LongForm => new()
    {
        Temperature = 0.5f,
        MaxTokens = 4000,
        TopP = 0.95f,
        FrequencyPenalty = 0.3f  // reduce repetition in long outputs
    };
}
```

## JSON Serialization

`PromptOptions` properties are annotated with `[JsonPropertyName]` for serialization:

```csharp
var opts = new PromptOptions { Temperature = 0.3f, MaxTokens = 2000 };
string json = JsonSerializer.Serialize(opts);
// {"temperature":0.3,"maxTokens":2000,"topP":0.95,"frequencyPenalty":0,"presencePenalty":0}

var restored = JsonSerializer.Deserialize<PromptOptions>(json);
```

## Validation

All setters validate ranges and throw `ArgumentOutOfRangeException` for invalid values:

```csharp
var opts = new PromptOptions();

opts.Temperature = 3.0f;    // throws: must be 0.0–2.0
opts.MaxTokens = 0;         // throws: must be ≥ 1
opts.TopP = -0.5f;          // throws: must be 0.0–1.0
opts.FrequencyPenalty = 5f;  // throws: must be -2.0–2.0
```
