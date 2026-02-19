# Conversations

The `Conversation` class enables multi-turn dialogue with Azure OpenAI. Unlike `Main.GetResponseAsync()` which is stateless, a conversation accumulates messages so the model has full context of the discussion.

## Creating a Conversation

```csharp
using Prompt;

// Basic conversation
var conv = new Conversation();

// With a system prompt
var conv = new Conversation("You are a helpful coding assistant.");

// With system prompt and custom options
var opts = new PromptOptions { Temperature = 0.3f, MaxTokens = 2000 };
var conv = new Conversation("You are a code reviewer.", opts);
```

## Sending Messages

Each call to `SendAsync` adds the user message and the assistant's response to the history:

```csharp
var conv = new Conversation("You are a math tutor.");

string? r1 = await conv.SendAsync("What is 2 + 2?");
Console.WriteLine(r1);  // "4"

string? r2 = await conv.SendAsync("Now multiply that by 3.");
Console.WriteLine(r2);  // "12" — the model remembers the context

Console.WriteLine(conv.MessageCount);  // 5 (system + 2 user + 2 assistant)
```

## Adjusting Parameters Mid-Conversation

You can change model parameters at any point during the conversation:

```csharp
var conv = new Conversation("You are a writer.");

// Start creative
conv.Temperature = 0.9f;
await conv.SendAsync("Write the opening paragraph of a mystery novel.");

// Switch to precise for editing
conv.Temperature = 0.1f;
await conv.SendAsync("Now proofread that paragraph and fix any grammar issues.");
```

### Available Parameters

| Property | Range | Default | Description |
|----------|-------|---------|-------------|
| `Temperature` | 0.0–2.0 | 0.7 | Randomness of output |
| `MaxTokens` | ≥ 1 | 800 | Maximum response length |
| `TopP` | 0.0–1.0 | 0.95 | Nucleus sampling |
| `FrequencyPenalty` | -2.0–2.0 | 0.0 | Penalize frequent tokens |
| `PresencePenalty` | -2.0–2.0 | 0.0 | Penalize tokens that already appeared |
| `MaxRetries` | ≥ 0 | 3 | Retry count for transient failures |

## Injecting Context

Add messages to the history without calling the API — useful for replaying prior conversations or injecting context:

```csharp
var conv = new Conversation("You are a helpful assistant.");

// Inject prior context
conv.AddUserMessage("My name is Alice.");
conv.AddAssistantMessage("Hello Alice! How can I help you?");

// Now the model knows the user's name
string? response = await conv.SendAsync("What's my name?");
// → "Your name is Alice."
```

## Clearing History

Reset the conversation while keeping the system prompt:

```csharp
var conv = new Conversation("You are a translator.");
await conv.SendAsync("Translate 'hello' to French.");

conv.Clear();  // Removes all user/assistant messages, keeps system prompt
Console.WriteLine(conv.MessageCount);  // 1 (just the system prompt)
```

## Retrieving History

Get a snapshot of the conversation for logging or display:

```csharp
var history = conv.GetHistory();
foreach (var (role, content) in history)
{
    Console.WriteLine($"[{role}] {content}");
}
// [system] You are a translator.
// [user] Translate 'hello' to French.
// [assistant] "Bonjour"
```

## Serialization

Save and restore conversations as JSON — useful for persistence, logging, and session resumption.

### Save to JSON String

```csharp
string json = conv.SaveToJson();
Console.WriteLine(json);
// {
//   "messages": [
//     { "role": "system", "content": "You are a translator." },
//     { "role": "user", "content": "Translate 'hello' to French." },
//     { "role": "assistant", "content": "Bonjour" }
//   ],
//   "parameters": {
//     "temperature": 0.7,
//     "maxTokens": 800,
//     "topP": 0.95,
//     ...
//   }
// }
```

### Load from JSON String

```csharp
var restored = Conversation.LoadFromJson(json);
// Continues with full history and parameters intact
string? next = await restored.SendAsync("Now translate 'goodbye'.");
```

### File-Based Persistence

```csharp
// Save
await conv.SaveToFileAsync("session.json");

// Load
var loaded = await Conversation.LoadFromFileAsync("session.json");
```

## Cancellation

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

try
{
    string? response = await conv.SendAsync("Complex question...", cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Request timed out.");
}
```

## Thread Safety

`Conversation` uses internal locking for message list operations. The message history is safe to access from multiple threads, though concurrent `SendAsync` calls will serialize through the Azure OpenAI client.
