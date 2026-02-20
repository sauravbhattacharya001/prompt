# Error Handling

This guide covers how the Prompt library handles errors, what exceptions to expect, and best practices for robust error handling in production.

## Exception Types

### ArgumentException

Thrown when invalid arguments are passed to any method:

```csharp
// Empty prompt
await Main.GetResponseAsync("");
// → ArgumentException: "Prompt cannot be null or empty."

// Empty message in conversation
var conv = new Conversation();
await conv.SendAsync("");
// → ArgumentException: "Message cannot be null or empty."

// Empty template
var template = new PromptTemplate("");
// → ArgumentException: "Template cannot be null or empty."
```

### ArgumentOutOfRangeException

Thrown when numeric parameters are outside valid ranges:

```csharp
// Negative retries
await Main.GetResponseAsync("Hello", maxRetries: -1);
// → ArgumentOutOfRangeException: "maxRetries must be non-negative."

// Temperature out of range
var conv = new Conversation();
conv.Temperature = 3.0f;
// → ArgumentOutOfRangeException: "Temperature must be between 0.0 and 2.0."

// Invalid PromptOptions values
var opts = new PromptOptions { MaxTokens = 0 };
// → ArgumentOutOfRangeException: "MaxTokens must be at least 1."
```

### InvalidOperationException

Thrown when required configuration is missing or operations are invalid:

```csharp
// Missing environment variable
// (when AZURE_OPENAI_API_URI is not set)
await Main.GetResponseAsync("Hello");
// → InvalidOperationException: "Environment variable AZURE_OPENAI_API_URI is not set or is empty."

// Invalid URI format
// (when AZURE_OPENAI_API_URI contains a non-HTTP URI)
// → InvalidOperationException: "AZURE_OPENAI_API_URI value '...' is not a valid HTTP(S) URI."

// Missing template variables (strict mode)
var template = new PromptTemplate("Hello {{name}}");
template.Render();
// → InvalidOperationException: "Missing values for template variables: name."

// Empty chain
var chain = new PromptChain();
await chain.RunAsync();
// → InvalidOperationException: "Cannot run an empty chain."
```

### OperationCanceledException

Thrown when a request is cancelled via `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    string? response = await Main.GetResponseAsync("Complex question...", cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Request was cancelled or timed out.");
}
```

### Azure SDK Exceptions

The Azure.AI.OpenAI SDK may throw exceptions for API errors. These are propagated after retries are exhausted:

- **`ClientResultException`** — General API errors (authentication failures, invalid model, quota exceeded)
- **HTTP 401** — Invalid API key
- **HTTP 404** — Model deployment not found
- **HTTP 429** — Rate limit exceeded (retried automatically, thrown after max retries)
- **HTTP 503** — Service unavailable (retried automatically, thrown after max retries)

## Retry Behavior

The library automatically retries on transient failures using Azure.Core's exponential backoff:

| Setting | Value |
|---------|-------|
| Default retries | 3 |
| Backoff strategy | Exponential with jitter |
| Base delay | ~1 second |
| Max delay | ~30 seconds |
| Retried status codes | 429 (rate limit), 503 (service unavailable) |

### Customizing Retries

```csharp
// Per-request
string? r = await Main.GetResponseAsync("Hello", maxRetries: 5);

// Per-conversation
var conv = new Conversation("System prompt");
conv.MaxRetries = 5;

// Per-chain
var chain = new PromptChain().WithMaxRetries(5);
```

### How Retry Interacts with the Client Cache

The `ChatClient` is cached as a singleton. If you change `maxRetries` between calls, the client is automatically recreated with the new retry policy:

```csharp
await Main.GetResponseAsync("Hello", maxRetries: 3);  // Creates client with 3 retries
await Main.GetResponseAsync("Hello", maxRetries: 5);  // Recreates client with 5 retries
await Main.GetResponseAsync("Hello", maxRetries: 5);  // Reuses cached client
```

## Best Practices

### 1. Always Handle Cancellation

Any call to Azure OpenAI can take seconds or longer. Always provide a timeout:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var result = await Main.GetResponseAsync(prompt, cancellationToken: cts.Token);
    // Use result
}
catch (OperationCanceledException)
{
    // Handle timeout gracefully
}
```

### 2. Validate Templates Before Sending

Use `GetRequiredVariables()` and chain `Validate()` to catch errors before making API calls:

```csharp
// Template validation
var template = new PromptTemplate("Analyze {{data}} for {{metric}}");
var required = template.GetRequiredVariables();
// Check that your variables dictionary covers all required variables

// Chain validation
var errors = chain.Validate(myVariables);
if (errors.Count > 0)
{
    foreach (var error in errors)
        logger.LogWarning("Chain validation: {Error}", error);
    return;
}
```

### 3. Check for Null Responses

The model can return `null` if no content was generated:

```csharp
string? response = await Main.GetResponseAsync("Hello");
if (response == null)
{
    // Handle no-response case
    Console.WriteLine("No response generated.");
    return;
}
```

### 4. Validate Environment Variables Early

Check that required environment variables are set at application startup, not at the first API call:

```csharp
// In Program.cs or Startup.cs
var requiredVars = new[] { "AZURE_OPENAI_API_URI", "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_API_MODEL" };
foreach (var varName in requiredVars)
{
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(varName)))
    {
        throw new InvalidOperationException($"Required environment variable {varName} is not set.");
    }
}
```

### 5. Use Non-Strict Mode for Dynamic Templates

When templates have optional sections or variables that might not always be available:

```csharp
var template = new PromptTemplate(
    "Analyze {{data}}. {{#context}}Additional context: {{context}}{{/context}}"
);

// Non-strict mode leaves unresolved variables as-is
string rendered = template.Render(
    new Dictionary<string, string> { ["data"] = "..." },
    strict: false
);
```

### 6. Wrap Chain Execution for Partial Results

In a `PromptChain`, if an intermediate step fails, you may want to capture what completed:

```csharp
ChainResult? result = null;
try
{
    result = await chain.RunAsync(variables, cancellationToken);
}
catch (Exception ex)
{
    logger.LogError(ex, "Chain failed at step");
    // result is null — no partial results available from RunAsync
    // Consider breaking chains into individual steps if partial results matter
}
```

### 7. Production Error Handling Pattern

```csharp
public async Task<string> GetSafeLlmResponse(string prompt, CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(30));

    try
    {
        string? response = await Main.GetResponseAsync(
            prompt,
            systemPrompt: "Be concise.",
            maxRetries: 3,
            cancellationToken: cts.Token);

        return response ?? "No response generated.";
    }
    catch (OperationCanceledException)
    {
        return "Request timed out. Please try again.";
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Environment variable"))
    {
        // Missing configuration — this is a setup error, not a runtime error
        throw;
    }
    catch (Exception ex)
    {
        // Log and return a user-friendly message
        logger.LogError(ex, "Azure OpenAI request failed");
        return "An error occurred. Please try again later.";
    }
}
```

## Debugging Tips

### Enable Azure SDK Logging

For detailed request/response logging during development:

```csharp
using Azure.Core.Diagnostics;

// Enable console logging for Azure SDK
using var listener = AzureEventSourceListener.CreateConsoleLogger();
```

### Check Client State

If you suspect stale configuration, reset the client:

```csharp
Main.ResetClient();
// Next call will re-read environment variables and create a fresh client
```

### Inspect Chain Execution

Use `ChainResult.ToJson()` for detailed step-by-step logging:

```csharp
var result = await chain.RunAsync(vars);
string debugJson = result.ToJson();
File.WriteAllText("chain-debug.json", debugJson);
// Contains: rendered prompts, responses, timing for each step
```
