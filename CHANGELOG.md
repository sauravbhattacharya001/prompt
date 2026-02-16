# Changelog

All notable changes to the `prompt-llm-aoi` NuGet package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.3.0] - 2026-02-15

### Added
- **Configurable model parameters** — `PromptOptions` class for customizing Azure OpenAI behavior (fixes #8)
- `PromptOptions` with validated `Temperature`, `MaxTokens`, `TopP`, `FrequencyPenalty`, `PresencePenalty`
- Factory presets: `ForCodeGeneration()`, `ForCreativeWriting()`, `ForDataExtraction()`, `ForSummarization()`
- `Main.GetResponseAsync()` now accepts optional `PromptOptions? options` parameter
- `PromptTemplate.RenderAndSendAsync()` now accepts optional `PromptOptions? options` parameter
- `PromptChain.WithOptions(PromptOptions?)` fluent builder for chain-wide model parameters
- `Conversation(string?, PromptOptions)` constructor overload for initializing from `PromptOptions`
- `PromptOptions` serializes to/from JSON with `System.Text.Json` attributes
- Chain JSON serialization preserves `PromptOptions` configuration
- 37 new tests for `PromptOptions` (validation, presets, JSON round-trip, integration with Conversation and PromptChain)

### Changed
- `Main.GetResponseAsync()` no longer hardcodes `Temperature=0.7, MaxTokens=800, TopP=0.95` — uses `PromptOptions` defaults instead (same values, but now configurable)
- **Backward compatible** — all new parameters are optional with null defaults

## [3.2.0] - 2026-02-15

### Added
- **Prompt chains** — `PromptChain` class for multi-step LLM pipelines
- `AddStep(name, template, outputVariable)` — add a step where the template is rendered with accumulated variables and the response is stored under `outputVariable`
- `WithSystemPrompt(systemPrompt)` — set a shared system prompt for all API calls in the chain
- `WithMaxRetries(maxRetries)` — configure retry policy for the chain
- `RunAsync(initialVariables, cancellationToken)` — execute all steps sequentially, returning a `ChainResult` with timing, rendered prompts, and responses
- `Validate(initialVariables)` — static analysis to check all required template variables are satisfiable without calling the API
- `ToJson(indented)` / `FromJson(json)` — serialize and deserialize chain definitions
- `SaveToFileAsync()` / `LoadFromFileAsync()` — persist chain definitions to JSON files
- `ChainResult` with `FinalResponse`, `GetOutput(variable)`, per-step timing, and `ToJson()` for logging
- `ChainStep` and `StepResult` value types for step definitions and results
- Duplicate output variable detection (case-insensitive) prevents accidental overwrites
- `InternalsVisibleTo` for test project to enable internal constructor testing
- 38 new tests for PromptChain, ChainResult, ChainStep, and StepResult

## [3.1.0] - 2026-02-14

### Added
- **Conversation serialization** — save and load conversations as JSON
- `SaveToJson(indented)` — serialize conversation (messages + parameters) to a JSON string
- `LoadFromJson(json)` — restore a conversation from a JSON string (static factory)
- `SaveToFileAsync(filePath, indented, cancellationToken)` — save conversation to a JSON file
- `LoadFromFileAsync(filePath, cancellationToken)` — load conversation from a JSON file (static factory)
- Full round-trip support: all messages, system prompt, and model parameters are preserved
- Uses `System.Text.Json` (zero additional dependencies)
- 27 new unit tests for serialization (ConversationSerializationTests.cs)

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
