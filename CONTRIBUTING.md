# Contributing to Prompt

Thanks for your interest in contributing to **Prompt** — a comprehensive .NET library for building, testing, managing, and optimizing LLM prompts at scale.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Module Catalog](#module-catalog)
- [Development Workflow](#development-workflow)
- [Writing Tests](#writing-tests)
- [Code Style & Conventions](#code-style--conventions)
- [CI/CD Pipeline](#cicd-pipeline)
- [Submitting a Pull Request](#submitting-a-pull-request)
- [Reporting Issues](#reporting-issues)
- [Architecture Notes](#architecture-notes)
- [License](#license)

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Git
- A GitHub account
- (Optional) An Azure OpenAI endpoint for integration testing

## Getting Started

```bash
# 1. Fork & clone
git clone https://github.com/<your-username>/prompt.git
cd prompt

# 2. Restore dependencies
dotnet restore

# 3. Build
dotnet build -c Release

# 4. Run tests
dotnet test
```

## Project Structure

```
prompt/
├── src/                     # Library source (175 modules)
│   ├── Prompt.csproj        # .NET 8.0 library — NuGet package: prompt-llm-aoi
│   ├── Main.cs              # Core entry point — GetResponseAsync()
│   ├── Conversation.cs      # Multi-turn conversation manager
│   ├── PromptTemplate.cs    # Reusable templates with {{variables}}
│   ├── PromptChain.cs       # Multi-step LLM pipelines
│   └── ... (175 files)      # See Module Catalog below
├── tests/                   # xUnit test suite (149 test files, 5800+ tests)
│   └── Prompt.Tests.csproj
├── docs/                    # GitHub Pages documentation site
├── .github/
│   └── workflows/
│       ├── ci.yml           # Build + test on every push/PR
│       ├── codeql.yml       # Security scanning
│       ├── nuget-publish.yml # NuGet package publishing
│       ├── docker.yml       # Docker image build
│       ├── pages.yml        # Docs site deployment
│       ├── auto-label.yml   # PR auto-labeling
│       ├── pr-size.yml      # PR size annotations
│       └── stale.yml        # Stale issue/PR cleanup
├── Prompt.sln               # Solution file
├── Dockerfile               # Multi-stage build for NuGet packaging
├── SECURITY.md              # Security policy
├── CHANGELOG.md             # Release changelog
└── LICENSE                  # MIT
```

## Module Catalog

The library is organized into functional areas. Each module is a single `.cs` file in `src/`.

### Core (6 modules)

The foundation every other module builds on.

| Module | Purpose |
|--------|---------|
| `Main` | Singleton `ChatClient` entry point — `GetResponseAsync()` |
| `Conversation` | Multi-turn conversation state management |
| `PromptTemplate` | `{{variable}}` templates with validation |
| `PromptChain` | Multi-step LLM pipelines with step chaining |
| `PromptOptions` | Configuration model for prompt execution |
| `PromptPipeline` | Composable processing pipeline builder |

### Text Processing & Formatting (13 modules)

Prompt construction, formatting, and text manipulation.

| Module | Purpose |
|--------|---------|
| `FewShotBuilder` | Structured few-shot example construction |
| `PromptChatFormatter` | Chat message formatting for various LLM APIs |
| `PromptComposer` | Multi-section prompt assembly |
| `PromptContextBuilder` | Context window construction with priorities |
| `PromptContextCompressor` | Token-aware context compression |
| `PromptDialect` | Cross-model prompt dialect translation |
| `PromptExpander` | Macro/shorthand expansion in prompts |
| `PromptInterpolator` | Advanced variable interpolation engine |
| `PromptMinifier` | Token-efficient prompt compression |
| `PromptNormalizer` | Whitespace/encoding normalization |
| `PromptSlotFiller` | Named-slot filling with validation |
| `PromptSplitter` | Long-prompt chunking and segmentation |
| `StringHelpers` | Common string utility methods |

### Token Management (5 modules)

Token counting, budgeting, and optimization.

| Module | Purpose |
|--------|---------|
| `TokenBudget` | Token budget allocation model |
| `PromptTokenCounter` | Model-aware token counting |
| `PromptTokenOptimizer` | Token usage optimization strategies |
| `PromptTokenBudgetPlanner` | Multi-component token budget planning |
| `PromptContextAllocator` | Context window allocation across components |

### Quality & Validation (14 modules)

Prompt linting, validation, compliance, and quality gates.

| Module | Purpose |
|--------|---------|
| `PromptLinter` | Rule-based prompt linting |
| `PromptGrammarValidator` | Grammar and syntax validation |
| `PromptOutputValidator` | Response format/schema validation |
| `PromptQualityGate` | Multi-criteria quality checks before execution |
| `PromptComplianceChecker` | Policy/regulation compliance validation |
| `PromptChecklist` | Pre-flight checklist runner |
| `PromptContractTester` | Input/output contract verification |
| `PromptSanitizer` | Input sanitization and XSS/injection prevention |
| `PromptGuard` | Runtime safety guardrails |
| `PromptInjectionDetector` | Prompt injection attack detection |
| `PromptSecretScanner` | Secret/credential leak detection in prompts |
| `PromptReadabilityAnalyzer` | Readability scoring (Flesch-Kincaid, etc.) |
| `PromptComplexityScorer` | Structural complexity measurement |
| `PromptMaturityModel` | Prompt maturity level assessment |

### Testing & Evaluation (14 modules)

Testing, benchmarking, and evaluation frameworks.

| Module | Purpose |
|--------|---------|
| `PromptTestSuite` | Automated prompt test runner |
| `PromptGoldenTester` | Golden-file regression testing |
| `PromptBenchmarkSuite` | Performance benchmarking framework |
| `PromptFuzzer` | Fuzz testing with adversarial inputs |
| `PromptAdversary` | Adversarial attack generation |
| `PromptABTester` | A/B testing for prompt variants |
| `PromptResponseEvaluator` | Response quality evaluation |
| `PromptCoverageAnalyzer` | Prompt coverage analysis (edge cases, intents) |
| `PromptConversationSimulator` | Multi-turn conversation simulation |
| `PromptShadowRunner` | Shadow-mode execution for safe comparison |
| `PromptTournament` | Head-to-head prompt tournament evaluation |
| `PromptComparator` | Side-by-side prompt comparison |
| `PromptAntifragileEngine` | Stress-testing prompts to build resilience |
| `PromptDatasetBuilder` | Evaluation dataset construction |

### Analytics & Monitoring (14 modules)

Usage analytics, cost tracking, drift detection, and observability.

| Module | Purpose |
|--------|---------|
| `PromptAnalytics` | Usage and performance analytics |
| `PromptUsageReport` | Detailed usage reporting |
| `PromptUsageDashboard` | Dashboard data aggregation |
| `PromptHeatmap` | Usage heatmap visualization data |
| `PromptCostEstimator` | Pre-execution cost estimation |
| `PromptCostOptimizer` | Cost reduction strategies |
| `PromptDriftMonitor` | Prompt/response drift detection over time |
| `PromptSLAMonitor` | SLA compliance monitoring |
| `PromptHealthCheck` | System health diagnostics |
| `PromptPerformanceProfiler` | Execution performance profiling |
| `PromptScorecardBuilder` | Prompt scorecard generation |
| `PromptSituationRoom` | Real-time operational overview |
| `PromptBlackSwanEngine` | Rare/unexpected failure pattern analysis |
| `PromptRiskForecaster` | Predictive risk modeling |

### Version Control & Lifecycle (14 modules)

Versioning, history, change tracking, and lifecycle management.

| Module | Purpose |
|--------|---------|
| `PromptVersionManager` | Semantic versioning for prompts |
| `PromptHistory` | Change history tracking |
| `PromptDiff` | Prompt diff computation |
| `PromptDiffEngine` | Structural diff engine |
| `PromptDiffViewer` | Diff visualization data |
| `PromptRollbackManager` | Version rollback support |
| `PromptSnapshotManager` | Point-in-time snapshots |
| `PromptChangelogGenerator` | Automated changelog generation |
| `PromptChangeImpactAnalyzer` | Change impact assessment |
| `PromptDeprecationManager` | Deprecation lifecycle management |
| `PromptStaleDetector` | Stale/unused prompt detection |
| `PromptPromotionManager` | Staging → production promotion |
| `PromptMigrationAssistant` | Cross-version migration helper |
| `PromptGenealogyTracker` | Prompt lineage/ancestry tracking |

### Resilience & Reliability (8 modules)

Fault tolerance, retry logic, and error recovery.

| Module | Purpose |
|--------|---------|
| `PromptResilience` | Resilience patterns (retry, circuit breaker, bulkhead) |
| `PromptRetryPolicy` | Configurable retry policies |
| `PromptCircuitBreakerEngine` | Circuit breaker state machine |
| `PromptFallbackChain` | Cascading fallback execution |
| `PromptErrorRecovery` | Intelligent error recovery strategies |
| `PromptSelfHealer` | Self-healing prompt correction |
| `PromptLoadBalancer` | Multi-endpoint load balancing |
| `PromptRateLimiter` | Request rate limiting |

### Optimization & Evolution (12 modules)

Prompt improvement, refactoring, and evolutionary optimization.

| Module | Purpose |
|--------|---------|
| `PromptAutoImprover` | Automated prompt improvement |
| `PromptRefactorer` | Prompt refactoring operations |
| `PromptEvolutionEngine` | Evolutionary/genetic prompt optimization |
| `PromptCoEvolver` | Co-evolutionary optimization across prompt pairs |
| `PromptMutationLab` | Controlled prompt mutation for variation |
| `PromptVariantGenerator` | Systematic variant generation |
| `PromptSelfTuningEngine` | Self-tuning parameter adjustment |
| `PromptFeedbackLoop` | Feedback-driven improvement cycles |
| `PromptCachingOptimizer` | Response caching strategies |
| `PromptMetabolismEngine` | Prompt lifecycle metabolism modeling |
| `PromptForgettingCurveEngine` | Memory decay and retention modeling |
| `PromptWisdomEngine` | Distilled knowledge from prompt history |

### Routing & Orchestration (10 modules)

Multi-model routing, ensemble execution, and orchestration.

| Module | Purpose |
|--------|---------|
| `PromptRouter` | Intent-based model/prompt routing |
| `PromptOrchestrator` | Complex multi-step orchestration |
| `PromptEnsemble` | Multi-model ensemble execution |
| `PromptMixtureOfExperts` | Mixture-of-experts routing |
| `PromptSwarm` | Swarm-intelligence prompt coordination |
| `PromptNegotiator` | Multi-agent negotiation protocols |
| `PromptGoalPlanner` | Goal decomposition and planning |
| `PromptAutopilot` | Autonomous prompt execution management |
| `PromptWorkflow` | Multi-step workflow definition |
| `PromptScheduler` | Scheduled/deferred prompt execution |

### NLP & Content Analysis (9 modules)

Sentiment analysis, intent classification, and content understanding.

| Module | Purpose |
|--------|---------|
| `PromptIntentClassifier` | User intent classification |
| `PromptSentimentAnalyzer` | Sentiment analysis of prompts/responses |
| `PromptEmotionAnalyzer` | Emotion detection and analysis |
| `PromptToneAnalyzer` | Tone and voice analysis |
| `PromptBiasDetector` | Bias detection in prompts and responses |
| `PromptExplainer` | Prompt behavior explanation generation |
| `PromptStyleTransfer` | Style transformation between tones |
| `PromptStyleTransformer` | Advanced style transformation engine |
| `TextAnalysisHelpers` | Text analysis utility methods |

### Library & Organization (12 modules)

Prompt storage, cataloging, search, and metadata management.

| Module | Purpose |
|--------|---------|
| `PromptLibrary` | Prompt library/registry |
| `PromptArchetypeLibrary` | Reusable prompt archetype patterns |
| `PromptRecipe` | Step-by-step prompt recipes |
| `PromptInheritance` | Template inheritance and override chains |
| `PromptTagManager` | Tagging and categorization |
| `PromptAnnotation` | Prompt annotation and commentary |
| `PromptGlossary` | Domain terminology glossary |
| `PromptMetadataExtractor` | Metadata extraction from prompts |
| `PromptSemanticSearch` | Semantic prompt search |
| `PromptSimilarityAnalyzer` | Prompt similarity/deduplication detection |
| `PromptFingerprint` | Content fingerprinting for change detection |
| `PromptDependencyGraph` | Inter-prompt dependency mapping |

### Security & Risk (5 modules)

Security analysis and risk assessment beyond injection detection.

| Module | Purpose |
|--------|---------|
| `PromptRiskAssessor` | Comprehensive risk assessment |
| `PromptSentinel` | Real-time security monitoring |
| `PromptCanary` | Canary token detection |
| `PromptWatermark` | Content watermarking |
| `PromptSupplyChainAuditor` | Prompt supply chain auditing |

### Export, Sharing & Integration (12 modules)

Formatting, exporting, and cross-platform integration.

| Module | Purpose |
|--------|---------|
| `PromptDocGenerator` | API documentation generation |
| `PromptMarkdownExporter` | Markdown export |
| `PromptCatalogExporter` | Full catalog export |
| `PromptShareFormatter` | Shareable prompt formatting |
| `PromptSchemaGenerator` | JSON schema generation |
| `PromptToolFormatter` | Tool/function-call formatting |
| `PromptFlowChart` | Flow visualization data |
| `PromptChainVisualizer` | Chain execution visualization |
| `PromptMatrix` | Prompt comparison matrix builder |
| `PromptPlayground` | Interactive playground data |
| `PromptReplayRecorder` | Execution replay recording |
| `StreamChunk` | Streaming response chunk model |

### Configuration & Environment (9 modules)

Feature flags, environment management, and configuration.

| Module | Purpose |
|--------|---------|
| `PromptFeatureFlag` | Feature flag management |
| `PromptEnvironmentManager` | Environment-specific configuration |
| `PromptProfileSwitcher` | Configuration profile switching |
| `PromptSamplerConfig` | Sampling parameter configuration |
| `PromptConditional` | Conditional prompt section inclusion |
| `PromptCompatibilityChecker` | Cross-model compatibility checking |
| `PromptEcosystem` | Ecosystem-wide configuration |
| `PromptSymbiosis` | Cross-component symbiosis modeling |
| `PromptPersonaBuilder` | AI persona construction |

### Localization (3 modules)

Multi-language and audience adaptation.

| Module | Purpose |
|--------|---------|
| `PromptLocalizer` | Basic prompt localization |
| `PromptLocalizationManager` | Full localization lifecycle management |
| `PromptAudienceAdapter` | Audience-specific prompt adaptation |
| `PromptTranslator` | Prompt translation across languages |

### Operational (8 modules)

Logging, auditing, caching, and batch operations.

| Module | Purpose |
|--------|---------|
| `PromptAuditLog` | Audit trail for prompt operations |
| `PromptCache` | In-memory response caching |
| `PromptBatchProcessor` | Batch prompt execution |
| `PromptTriageEngine` | Issue triage and prioritization |
| `PromptRegressionDetector` | Regression detection across versions |
| `PromptConflictDetector` | Prompt conflict/collision detection |
| `PromptSignature` | Prompt content signing |
| `ResponseParser` | Structured response parsing |

### Shared Utilities (2 modules)

| Module | Purpose |
|--------|---------|
| `SerializationGuards` | Safe JSON serialization with injection guards |
| `PromptDebugger` | Debug logging and trace output |

## Development Workflow

### Creating a Branch

```bash
git checkout -b feature/add-streaming-support
git checkout -b fix/retry-timeout-handling
git checkout -b docs/update-api-reference
git checkout -b refactor/simplify-chain-execution
```

### Making Changes

- **Follow existing code style** — C# 12 with nullable reference types (`<Nullable>enable</Nullable>`).
- **Add XML doc comments** to all public APIs — the project generates documentation files (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`).
- **Keep backward compatibility** — avoid breaking changes. Use optional parameters with defaults for new functionality.
- **Use `internal` visibility** where possible. Only make types/members `public` when part of the library's API surface.
- **No unused `using` statements** — keep imports clean.
- **File-scoped namespaces** and primary constructors where appropriate.

### Build Verification

Always verify before committing:

```bash
# Must produce zero warnings
dotnet build -c Release

# All tests must pass
dotnet test
```

## Writing Tests

All changes must include tests. The project uses **xUnit** with 5800+ existing tests.

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test tests/Prompt.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/ \
  /p:Include="[Prompt]*"
```

### Test Guidelines

- **Mock `ChatClient`** for tests that would call Azure OpenAI — never require real API credentials in CI.
- **Use `InternalsVisibleTo`** (already configured) to test `internal` methods.
- **Naming convention:** `MethodName_Scenario_ExpectedResult`
- **Cover edge cases:** null inputs, empty strings, boundary values, concurrent access, large inputs.
- **Test file naming:** `<ClassName>Tests.cs` matching the source file.
- **One test class per source class** — keep tests focused and discoverable.

### Test Categories

| Category | Example |
|----------|---------|
| Unit | `PromptTemplate_Render_ReplacesVariables` |
| Integration | `Conversation_MultiTurn_MaintainsHistory` |
| Edge case | `PromptSplitter_EmptyInput_ReturnsEmptyList` |
| Concurrency | `PromptCache_ConcurrentAccess_ThreadSafe` |
| Security | `PromptInjectionDetector_KnownAttacks_Detected` |

## Code Style & Conventions

| Rule | Detail |
|------|--------|
| Language | C# 12, .NET 8.0 |
| Nullability | All code is `#nullable enable` |
| Naming | PascalCase for public members, `_camelCase` for private fields |
| Visibility | Prefer `internal`; only `public` for API surface |
| Comments | XML doc comments on all public members |
| Dependencies | Minimize — only `Azure.AI.OpenAI` and `System.ClientModel` |
| Error handling | Use exceptions for truly exceptional cases; return `null`/empty for expected "not found" |
| Async | Use `async`/`await` for I/O; follow the `Async` suffix convention |

## CI/CD Pipeline

Every push and PR triggers:

| Workflow | Purpose |
|----------|---------|
| `ci.yml` | Build (.NET 8) + run all tests |
| `codeql.yml` | CodeQL security scanning |
| `nuget-publish.yml` | NuGet package publishing on release |
| `docker.yml` | Docker image build |
| `pages.yml` | Documentation site deployment |
| `auto-label.yml` | Auto-label PRs by file paths |
| `pr-size.yml` | PR size annotations |
| `stale.yml` | Auto-close stale issues/PRs |

**All CI checks must pass before merging.** Don't rely on bypasses.

## Submitting a Pull Request

1. **Commit** with clear messages:
   ```bash
   git commit -m "feat: add streaming response support to Conversation"
   ```

2. **Push** to your fork and open a PR against `main`.

3. **PR description** should include:
   - **What** does this change do?
   - **Why** is it needed?
   - **How** was it tested?
   - Link related issues (e.g., `Closes #42`)

### PR Checklist

- [ ] `dotnet build -c Release` produces zero warnings
- [ ] All tests pass (`dotnet test`)
- [ ] New public APIs have XML doc comments
- [ ] New functionality has test coverage
- [ ] No breaking changes (or discussed and approved in the PR)
- [ ] Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`)

## Reporting Issues

Include:

- **Expected behavior** vs **actual behavior**
- **Steps to reproduce**
- **.NET version** (`dotnet --version`)
- **OS** (Windows / Linux / macOS)
- **Prompt library version** (NuGet package version or commit SHA)

For feature requests, describe the use case and why it matters.

## Architecture Notes

Key design decisions to keep in mind:

- **`Main` uses a singleton `ChatClient`** — thread-safe via double-checked locking. Preserve this pattern when modifying client initialization.
- **`Conversation` is not thread-safe** — designed for single-threaded use per instance. Document thread safety expectations on any new stateful class.
- **Environment variable resolution** is cross-platform — Windows checks Process → User → Machine scopes; Linux/macOS checks Process only.
- **Retry handling** is delegated to Azure.Core's `ClientRetryPolicy` pipeline. Don't add custom retry loops around API calls.
- **175 modules, one per file** — each class lives in its own `.cs` file. Keep this convention.
- **Minimal dependencies** — the library only depends on `Azure.AI.OpenAI` and `System.ClientModel`. Think hard before adding a new dependency.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
