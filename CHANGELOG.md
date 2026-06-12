# Changelog

All notable changes to the `prompt-llm-aoi` NuGet package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security
- **PromptGuard** — extend Unicode-bypass stripping to close two prompt-injection detection bypasses. `DetectInjection`/`DetectInjectionPatterns`/`Analyze`/`Sanitize` now strip the WORD JOINER and invisible math operators (U+2060–U+2064) and supplementary-plane Unicode Tag characters (U+E0000–U+E007F), in addition to the previously-handled zero-width and bidi-override ranges. Previously an attacker could split an injection keyword with a word joiner (e.g. `ig\u2060nore all previous instructions`) or smuggle Tag characters and slip past `PromptGuard` entirely, even though `PromptSanitizer` already defended against them. The two security classes are now consistent. 11 regression tests added (including an astral-plane-emoji guard so real supplementary-plane characters are preserved).

### Fixed
- **PromptSecretScanner** — email redaction no longer leaks address structure for short local parts. The redactor took the first two characters of the entire match (`value[..2]`), so a single-character local part captured the literal `@` and produced a malformed result (e.g. `x@y.io` → `x@***@***.io`). It now splits on the first `@`, revealing only the first character of the local part and the final TLD label (e.g. `john.doe@example.com` → `j***@***.com`, `x@y.io` → `x***@***.io`). 5 regression tests added.

### Added
- **PromptLatencyBudgetAdvisor** - 12th agentic sibling: detects latency-cost risks in prompts (oversized prompt, chain-of-thought expansion, unbounded output, exhaustive coverage, serial tool chains, retry loops, heavy multimodal inputs, serializable fanout, missing output cap, streaming disabled) and produces a budgeted P0-first playbook with estimated savings, plus an optimized draft annotated with a `# LATENCY_BUDGET` block. Pure, deterministic, no I/O. 26 passing xUnit tests.

## [5.24.0] - 2026-05-21

### Added
- **PromptStepReasoningAdvisor** — 7th agentic sibling advisor for step-by-step reasoning prompts (0524bd3)
- Comprehensive unit-test suites for `PromptDiffEngine` (ba0c7ca) and `TextAnalysisHelpers` (1e50c6b)

### Fixed
- **PromptDiffEngine** (#c42c703): `ToUnifiedDiff` now honours the `contextLines` parameter; `ThreeWayMerge` no longer drops theirs-only edits
- **PromptDiffViewer** (#b3865f1): LCS backtrack now respects `ignoreWhitespace`, fixing spurious diffs that differed only in whitespace
- **PromptBatchProcessor** (#70686e2): `RetryPolicy.GetDelay` semantics corrected and `AddItem` now validates ids
- **PromptRollbackManager** (#f01b6d7): `ExportJson` / `ImportJson` round-trip now lossless, with regression tests
- **#191 — token estimator** (#83a2923): converged all duplicated `EstimateTokens` implementations on the canonical ~4 chars/token helper to eliminate drift

### Changed
- **Refactor** (#c4535f5): use `MinBy` / `MaxBy` instead of `OrderBy(...).First()` across 21 files — fewer allocations, clearer intent
- **Performance** (#18f7f85): eliminated redundant LINQ sorts in `PromptEvaluator` and `PromptABTester` hot paths
- **Docs** (#116b759): expanded XML doc comments on metadata APIs and hardened the Dependabot config to match documented intent (Azure.AI.OpenAI / System.ClientModel / `runtime-deps` major bumps now actually ignored, not just commented)

### Security
- **PromptSanitizer** (#b7c46c9): strip Unicode bidi-override and Tag characters to defend against homoglyph / hidden-instruction smuggling

### Dependencies
- nuget: bump the `nuget-minor-and-patch` group (#192)

### Added
- **PromptCatalogExporter** — export prompt library to HTML, CSV, and JSON formats
- **PromptBenchmarkSuite** — benchmark prompt variants against test scenarios
- **PromptHealthCheck** — library-wide quality analysis and health scoring
- **PromptChainVisualizer** — Mermaid, DOT, and ASCII flowchart generation from prompt chains
- **PromptSnapshotManager** — point-in-time library snapshots with diff comparison and rollback
- **PromptChangeImpactAnalyzer** — blast-radius analysis for prompt template changes
- **PromptPromotionManager** — lifecycle stage management with approval gates, rollback, and history
- **PromptCompatibilityChecker** — cross-provider prompt portability analysis
- **PromptRiskAssessor** — multi-dimensional security risk analysis for prompts
- **PromptInheritance** — block-based template inheritance with `{{super}}` support
- **PromptCoverageAnalyzer** — library coverage analysis with health scoring
- **PromptUsageReport** — comprehensive usage reporting with time-bucketed breakdowns and cost analysis
- **PromptMatrix** — combinatorial template variable testing
- **PromptPerformanceProfiler** — execution profiling with percentiles, comparison, and reports
- **PromptChangelogGenerator** — formatted changelogs from version history with multiple output formats
- **PromptMarkdownExporter** — export and import prompt libraries as Markdown
- **PromptSchemaGenerator** — fluent structured output schema builder
- **PromptQualityGate** — configurable pass/fail gate for prompt validation
- **PromptSamplerConfig** — LLM sampling parameter builder
- **PromptSimilarityAnalyzer** — multi-metric prompt comparison and duplicate detection
- **PromptGoldenTester** — snapshot testing for prompt outputs
- **PromptScorecardBuilder** — custom evaluation rubrics with weighted scoring
- **PromptSlotFiller** — structured slot extraction and multi-turn filling
- **PromptAnnotation** — structured inline comments and metadata for prompts
- **PromptStyleTransfer** — heuristic prompt tone and style rewriting
- **PromptReplayRecorder** — VCR-style prompt interaction recording and replay
- **PromptLinter** — rule-based static analysis for LLM prompts
- **PromptSplitter** — boundary-aware content chunking for long prompts
- **PromptDatasetBuilder** — evaluation and fine-tuning dataset builder
- **PromptMetadataExtractor** — structured prompt analysis for routing and analytics
- **PromptNegotiator** — iterative prompt refinement with validation feedback loops
- **PromptDependencyGraph** — DAG analysis for prompt pipelines
- **PromptSignature** — strongly-typed prompt signatures (DSPy-style)
- **PromptToolFormatter** — unified tool/function calling format across LLM providers
- **PromptContextCompressor** — intelligent conversation context compression with 4 strategies
- **PromptInterpolator** — pipe-based template variable transformations
- **PromptWorkflow** — DAG-based prompt workflow engine
- **PromptStreamParser** — real-time streaming content extraction
- **PromptAuditLog** — immutable hash-chained execution audit trail
- **PromptRetryPolicy** — configurable retry with backoff, circuit breaker, and error classification
- **PromptEnsemble** — multi-response aggregation (majority vote, best-of-N, consensus)
- **PromptOutputValidator** — LLM response validation against configurable rules
- **PromptChatFormatter** — multi-provider chat message formatting
- **PromptSanitizer** — prompt cleaning and normalization utility
- **PromptComplexityScorer** — multi-dimensional prompt complexity analysis
- **PromptExplainer** — analyze prompts for techniques, sections, and improvement suggestions
- **PromptFallbackChain** — resilient multi-model execution with automatic fallback
- **PromptConditional** — conditional logic for prompt templates
- **PromptContextBuilder** — priority-based prompt context assembly with token budgeting
- **PromptMigrationAssistant** — cross-provider prompt adaptation assistant

### Fixed
- Track acquire timestamps to prevent orphaned `RecordCompletion` from corrupting `ConcurrentCount`
- Resolve stack overflow in `AddToHistory` and add async `ExecuteAsync`
- Cap retry history to prevent memory leak; fix case-insensitive token escaping
- `CachingMiddleware` thread-safety and thundering-herd fix

### Changed
- **Security:** add DoS guards to `PromptInterpolator` filters
- **Performance:** single-pass `PurgeExpired` eliminates intermediate list allocation
- **Performance:** optimize `PromptSimilarityAnalyzer` memory and redundant work
- **Refactor:** unify `RetryPolicy` with `PromptRetryPolicy` to eliminate duplicate backoff/jitter logic
- **Refactor:** remove 5 redundant `EstimateTokens` wrappers, use `PromptGuard` directly

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
