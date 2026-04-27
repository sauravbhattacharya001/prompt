# Copilot Instructions for `prompt`

## Project Overview

**Prompt** is a comprehensive C# (.NET 8) prompt engineering library for Azure OpenAI. It started as a simple chat completion wrapper (`Main.cs`) and has grown into a full-featured toolkit with 155+ classes spanning prompt management, security, testing, analytics, chaining, caching, and workflow orchestration.

## Solution Structure

```
Prompt.sln
├── src/Prompt.csproj          # Main library (155+ source files)
│   ├── Main.cs                # Core API — static AzureOpenAI chat completions
│   ├── Conversation.cs        # Multi-turn conversation management
│   ├── FewShotBuilder.cs      # Few-shot example construction
│   ├── PromptTemplate.cs      # Template engine with {{variable}} interpolation
│   ├── PromptChain.cs         # Multi-step prompt chaining with variable passing
│   ├── PromptPipeline.cs      # Middleware pipeline for prompt execution
│   ├── PromptGuard.cs         # Prompt analysis, injection detection, quality scoring
│   ├── PromptCache.cs         # Response caching with TTL
│   ├── PromptRouter.cs        # Model/endpoint routing
│   ├── PromptOrchestrator.cs  # Complex workflow orchestration
│   ├── PromptAdversary.cs     # Adversarial prompt testing (attack templates)
│   ├── PromptAnalytics.cs     # Usage tracking and metrics
│   ├── TokenBudget.cs         # Token budget management
│   ├── StreamChunk.cs         # Streaming response handling
│   ├── SerializationGuards.cs # Safe JSON deserialization
│   ├── StringHelpers.cs       # String utility methods
│   ├── TextAnalysisHelpers.cs # Text analysis utilities
│   └── ... (140+ more domain-specific classes)
└── tests/Prompt.Tests.csproj  # xUnit tests (135 test files)
```

## Key Architectural Patterns

- **Static core API:** `Prompt.Main` uses static methods with a singleton `AzureOpenAIClient` (thread-safe via double-check locking with `volatile`)
- **Namespace:** Everything lives in the `Prompt` namespace
- **Templates:** `PromptTemplate` supports `{{variable}}` interpolation; used throughout chaining and pipeline systems
- **Middleware pipeline:** `PromptPipeline` implements a composable middleware pattern (`PromptPipelineContext` flows through middleware functions)
- **Declarative patterns:** `PromptAdversary` uses `AttackTemplate` records + `MaterialiseVariant` dispatcher (no lambda-per-attack boilerplate)
- **Cross-platform env vars:** `GetRequiredEnvVar` tries Process → User → Machine scopes (User/Machine only on Windows)
- **JSON serialization:** Uses `System.Text.Json` with `JsonSerializerOptions` throughout; `SerializationGuards` for safe deserialization

## Functional Areas

| Area | Key Classes | Purpose |
|------|------------|---------|
| **Core** | `Main`, `Conversation`, `PromptOptions` | Chat completions, multi-turn conversations |
| **Templates** | `PromptTemplate`, `PromptInterpolator`, `FewShotBuilder` | Prompt construction and variable binding |
| **Chaining** | `PromptChain`, `PromptPipeline`, `PromptOrchestrator` | Multi-step workflows and middleware |
| **Security** | `PromptGuard`, `PromptInjectionDetector`, `PromptSanitizer`, `PromptSecretScanner`, `PromptSentinel` | Input validation, injection detection, secret scanning |
| **Testing** | `PromptTestSuite`, `PromptFuzzer`, `PromptAdversary`, `PromptContractTester`, `PromptGoldenTester` | Prompt testing, fuzzing, adversarial testing |
| **Analytics** | `PromptAnalytics`, `PromptHeatmap`, `PromptUsageDashboard`, `PromptUsageReport` | Usage tracking, metrics, visualization |
| **Quality** | `PromptLinter`, `PromptComplexityScorer`, `PromptReadabilityAnalyzer`, `PromptQualityGate` | Prompt quality analysis and enforcement |
| **Cost** | `PromptCostEstimator`, `PromptCostOptimizer`, `PromptTokenBudgetPlanner`, `TokenBudget` | Cost estimation and optimization |
| **Caching** | `PromptCache`, `PromptCachingOptimizer`, `PromptFingerprint` | Response caching and deduplication |
| **Versioning** | `PromptVersionManager`, `PromptHistory`, `PromptRollbackManager`, `PromptDiff` | Prompt version control and diffing |
| **Routing** | `PromptRouter`, `PromptLoadBalancer`, `PromptFallbackChain` | Model routing, load balancing, failover |
| **Monitoring** | `PromptHealthCheck`, `PromptDriftMonitor`, `PromptSLAMonitor`, `PromptCanary` | Runtime health and drift detection |

## Build & Test

```bash
dotnet restore Prompt.sln
dotnet build Prompt.sln --configuration Release
dotnet test Prompt.sln --configuration Release --verbosity normal
```

## Required Environment Variables

- `AZURE_OPENAI_API_URI` — Azure OpenAI endpoint (must be valid HTTP/HTTPS URI)
- `AZURE_OPENAI_API_KEY` — API key
- `AZURE_OPENAI_API_MODEL` — Deployed model name (e.g., `gpt-4`)

Note: Many test classes are self-contained and don't require these environment variables. Integration tests that call the real API do.

## NuGet Package

- **Package ID:** `prompt-llm-aoi`
- **Current version:** 3.3.0
- **Published via:** `dotnet pack` (GeneratePackageOnBuild is enabled)

## Conventions

- **Nullable enabled** — all reference types are nullable-aware
- **Implicit usings enabled** — standard .NET 8 implicit usings
- **XML documentation** — all public members must have `<summary>` XML doc comments (documentation generation is enabled)
- **Thread safety** — any cached/shared state must use proper synchronization (`volatile`, `lock`, `ConcurrentDictionary`, etc.)
- **Input validation** — validate all public method parameters; throw `ArgumentException` / `ArgumentNullException` / `ArgumentOutOfRangeException`
- **`InternalsVisibleTo`** — `Prompt.Tests` can access `internal` members
- **Test framework:** xUnit 2.9.3 with Coverlet for code coverage

## Common Tasks

- **Adding a new class:** Add to `src/`, include XML docs on all public members, validate inputs, follow existing naming (`Prompt{Feature}.cs`)
- **Adding tests:** Add to `tests/`, name the file `{ClassName}Tests.cs`, use xUnit `[Fact]` and `[Theory]` attributes
- **Changing retry behavior:** Modify `CreateClientOptions` or `GetOrCreateChatClient` in `Main.cs`
- **Adding a new env var:** Follow the `GetRequiredEnvVar` pattern in `Main.cs` for cross-platform support
- **Updating dependencies:** Update `PackageReference` in relevant `.csproj` and verify API compatibility
- **Template variables:** Use `{{variableName}}` syntax; `PromptTemplate.Render()` handles interpolation
