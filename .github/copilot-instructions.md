# Copilot Instructions for `prompt`

## Project Overview

**Prompt** is a lightweight C# (.NET 8) library that wraps Azure OpenAI's chat completions API. It provides a simple static interface (`Prompt.Main.GetResponseAsync`) for sending prompts and receiving model responses with built-in retry logic, connection pooling, and cross-platform environment variable handling.

## Architecture

- **Single-file library:** `src/Main.cs` contains the entire public API
- **Solution:** `Prompt.sln` with the main project (`src/Prompt.csproj`) and tests (`PromptTests/PromptTests.csproj`)
- **Target framework:** .NET 8.0
- **NuGet package:** Published as `prompt-llm-aoi`
- **Key dependency:** `Azure.AI.OpenAI` v2.1.0

## Key Design Decisions

- **Static API:** All methods are static on `Prompt.Main` — no instance needed
- **Singleton client:** `AzureOpenAIClient` and `ChatClient` are cached and reused (thread-safe via double-check locking with `volatile`)
- **Cross-platform env vars:** `GetRequiredEnvVar` tries Process → User → Machine scopes, with User/Machine only on Windows
- **Retry policy:** Uses `ClientRetryPolicy` with configurable max retries (default 3)
- **Deprecated method:** `GetResponseTest` is deprecated in favor of `GetResponseAsync`

## Required Environment Variables

- `AZURE_OPENAI_API_URI` — Azure OpenAI endpoint (must be valid HTTP/HTTPS URI)
- `AZURE_OPENAI_API_KEY` — API key
- `AZURE_OPENAI_API_MODEL` — Deployed model name (e.g., `gpt-4`)

## How to Build

```bash
dotnet restore Prompt.sln
dotnet build Prompt.sln --configuration Release
```

## How to Test

```bash
dotnet test Prompt.sln --configuration Release --verbosity normal
```

Note: Tests may require the environment variables above to be set for integration tests. Unit tests that mock the client should work without them.

## Conventions

- **Nullable enabled** — all reference types are nullable-aware
- **Implicit usings enabled** — standard .NET 8 implicit usings
- **XML documentation** — all public members must have XML doc comments (documentation generation is enabled in the csproj)
- **Thread safety** — any cached state must use proper synchronization (see the `_clientLock` pattern)
- **Input validation** — validate all public method parameters and throw `ArgumentException` / `ArgumentOutOfRangeException` as appropriate

## Common Tasks

- **Adding a new public method:** Add it to `Main.cs`, include XML docs, validate inputs, use `CancellationToken` parameter
- **Changing retry behavior:** Modify `CreateClientOptions` or `GetOrCreateChatClient`
- **Adding a new env var:** Follow the `GetRequiredEnvVar` pattern for cross-platform support
- **Updating Azure.AI.OpenAI:** Update the `PackageReference` in `src/Prompt.csproj` and verify API compatibility
