# Changelog

All notable changes to the `prompt-llm-aoi` NuGet package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2026-02-14

### Added
- **`Conversation` class** for multi-turn chat with persistent message history
- Configurable per-conversation model parameters: Temperature, MaxTokens, TopP, FrequencyPenalty, PresencePenalty, MaxRetries
- `SendAsync()` — send a message and get a response with full conversation context
- `AddUserMessage()` / `AddAssistantMessage()` — inject prior messages for conversation replay
- `Clear()` — reset conversation while preserving system prompt
- `GetHistory()` — export conversation as role-content pairs for logging/serialization
- Thread-safe message management with proper locking
- 28 new unit tests for `Conversation` class (ConversationTests.cs)

### Changed
- `GetOrCreateChatClient` visibility changed from `private` to `internal` for `Conversation` access

## [2.1.0] - 2026-02-14

### Removed
- Deprecated `GetResponseTest` method (use `GetResponseAsync` instead)
- Unused `_cachedModel` static field — model name is now a local variable
- Orphaned `PromptTests` project reference from solution file (directory was already removed)

### Fixed
- Solution file now builds cleanly without phantom test project errors

## [2.0.0] - 2026-02-08

### Added
- Async `GetResponseAsync` method with cancellation token support
- Configurable retry policy with exponential backoff (via `Azure.Core`)
- Optional `systemPrompt` parameter for setting assistant behavior
- Cached singleton `ChatClient` with thread-safe double-check locking
- `ResetClient()` method to force client re-creation
- Cross-platform environment variable resolution (Process → User → Machine)
- Full XML documentation comments

### Changed
- Renamed `GetResponseTest` to `GetResponseAsync` (breaking change)
- URI validation now accepts both `http` and `https` schemes

### Deprecated
- `GetResponseTest` is marked `[Obsolete]` — use `GetResponseAsync` instead

## [1.0.0] - 2025-01-01

### Added
- Initial release with basic Azure OpenAI chat completion support
- Environment variable–based configuration
