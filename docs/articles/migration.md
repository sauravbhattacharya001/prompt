# Migration Guide

This guide covers breaking changes and upgrade steps between major versions of the Prompt library.

## Upgrading from 3.2 to 3.3

**No breaking changes.** This is an additive release.

### New: PromptOptions

`PromptOptions` gives you fine-grained control over model behavior. All existing code continues to work — the new `options` parameter is optional everywhere.

**Before (3.2):**
```csharp
string? response = await Main.GetResponseAsync("Hello!");
// Always used Temperature=0.7, MaxTokens=800, TopP=0.95
```

**After (3.3) — optional upgrade:**
```csharp
var opts = PromptOptions.ForCodeGeneration();
string? response = await Main.GetResponseAsync("Write merge sort", options: opts);
```

**Works with all APIs:**
```csharp
// Conversations
var conv = new Conversation("System prompt", opts);

// Templates
await template.RenderAndSendAsync(vars, options: opts);

// Chains
var chain = new PromptChain().WithOptions(opts);
```

See the [Model Options](options.md) guide for details on parameters and presets.

## Upgrading from 3.1 to 3.2

**No breaking changes.** Adds `PromptChain` for multi-step pipelines.

### New: Prompt Chains

If you were manually chaining `Main.GetResponseAsync` calls and passing outputs between them, `PromptChain` automates this pattern:

**Before (manual chaining):**
```csharp
string? summary = await Main.GetResponseAsync($"Summarize: {text}");
string? translation = await Main.GetResponseAsync($"Translate to French: {summary}");
```

**After (PromptChain):**
```csharp
var chain = new PromptChain()
    .AddStep("summarize", new PromptTemplate("Summarize: {{text}}"), "summary")
    .AddStep("translate", new PromptTemplate("Translate to French: {{summary}}"), "french");

var result = await chain.RunAsync(new Dictionary<string, string> { ["text"] = text });
string? translation = result.GetOutput("french");
```

Benefits: automatic variable passing, per-step timing, validation, and JSON serialization.

See the [Prompt Chains](chains.md) guide for patterns and examples.

## Upgrading from 3.0 to 3.1

**No breaking changes.** Adds conversation serialization.

### New: Save & Load Conversations

```csharp
// Save conversation state
string json = conv.SaveToJson();
await conv.SaveToFileAsync("session.json");

// Restore later
var restored = Conversation.LoadFromJson(json);
var fromFile = await Conversation.LoadFromFileAsync("session.json");
```

All messages and model parameters are preserved in the JSON.

## Upgrading from 2.x to 3.0

### Breaking: New `Conversation` class uses `internal` API

`Main.GetOrCreateChatClient` changed from `private` to `internal`. This isn't a breaking change for consumers but affects anyone who was using reflection to access private members.

### New: Multi-Turn Conversations

Previously, every call to `GetResponseAsync` was stateless — the model forgot everything between calls. The `Conversation` class maintains message history:

**Before (2.x) — stateless:**
```csharp
// Each call is independent — no context from previous calls
string? r1 = await Main.GetResponseAsync("My name is Alice.");
string? r2 = await Main.GetResponseAsync("What's my name?");
// r2 won't know the answer
```

**After (3.0) — multi-turn:**
```csharp
var conv = new Conversation("You are a helpful assistant.");
string? r1 = await conv.SendAsync("My name is Alice.");
string? r2 = await conv.SendAsync("What's my name?");
// r2 knows: "Your name is Alice."
```

## Upgrading from 1.0 to 2.0

### Breaking: Method Rename

The synchronous `GetResponseTest` method was replaced with `GetResponseAsync`:

**Before (1.0):**
```csharp
string? result = Main.GetResponseTest("Hello");
```

**After (2.0):**
```csharp
string? result = await Main.GetResponseAsync("Hello!");
```

### Breaking: Async Only

All API calls are now async. You must `await` them or use `.Result` / `.GetAwaiter().GetResult()` in synchronous contexts (not recommended).

### New Features in 2.0

- **Cancellation support** — pass `CancellationToken` to cancel long-running requests
- **Configurable retries** — `maxRetries` parameter (default: 3)
- **System prompts** — `systemPrompt` parameter for controlling assistant behavior
- **Cross-platform** — environment variable resolution works on Windows, Linux, and macOS
- **Connection pooling** — thread-safe singleton client for efficient connection reuse
- **`ResetClient()`** — force client re-creation when environment variables change

## Version Compatibility Matrix

| Feature | 1.0 | 2.0 | 3.0 | 3.1 | 3.2 | 3.3 |
|---------|-----|-----|-----|-----|-----|-----|
| Basic prompts | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Async API | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| System prompts | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Cancellation | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Retry policy | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Conversations | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Conversation save/load | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |
| Prompt templates | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Prompt chains | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| PromptOptions | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| NuGet package | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
