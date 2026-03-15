# Architecture & Module Reference

This document provides a comprehensive overview of the Prompt library's architecture, organizing all 73 modules into functional categories with descriptions, key classes, and usage guidance.

## System Overview

```
┌──────────────────────────────────────────────────────────────┐
│                     Application Layer                        │
│  PromptWorkflow · PromptPipeline · PromptChain · Main        │
├──────────────────────────────────────────────────────────────┤
│                    Routing & Composition                     │
│  PromptRouter · PromptComposer · PromptConditional           │
│  PromptFallbackChain · PromptEnsemble · PromptNegotiator     │
├──────────────────────────────────────────────────────────────┤
│                   Template & Rendering                       │
│  PromptTemplate · PromptInterpolator · PromptSlotFiller      │
│  PromptChatFormatter · PromptToolFormatter · FewShotBuilder  │
│  PromptContextBuilder · PromptContextCompressor              │
├──────────────────────────────────────────────────────────────┤
│                   Analysis & Quality                         │
│  PromptLinter · PromptComplexityScorer · PromptGuard         │
│  PromptGrammarValidator · PromptQualityGate                  │
│  PromptSimilarityAnalyzer · PromptResponseEvaluator          │
│  PromptComplianceChecker · PromptFuzzer · PromptFingerprint  │
├──────────────────────────────────────────────────────────────┤
│                   Optimization & Cost                        │
│  TokenBudget · PromptTokenOptimizer · PromptCostEstimator    │
│  PromptMinifier · PromptSplitter · PromptSamplerConfig       │
│  PromptRateLimiter · PromptBatchProcessor                    │
├──────────────────────────────────────────────────────────────┤
│                   Persistence & History                      │
│  PromptLibrary · PromptHistory · PromptVersionManager        │
│  PromptCache · PromptAuditLog · PromptReplayRecorder         │
│  PromptAnnotation · PromptDiff                               │
├──────────────────────────────────────────────────────────────┤
│                   Testing & Debugging                        │
│  PromptTestSuite · PromptGoldenTester · PromptDebugger       │
│  PromptABTester · PromptScorecardBuilder                     │
├──────────────────────────────────────────────────────────────┤
│                    Export & Transformation                    │
│  PromptMarkdownExporter · PromptDocGenerator                 │
│  PromptSchemaGenerator · PromptDatasetBuilder                │
│  PromptStyleTransfer · PromptLocalizer · PromptRefactorer    │
│  PromptExplainer · PromptMerger                              │
├──────────────────────────────────────────────────────────────┤
│                    Infrastructure                            │
│  PromptOptions · Conversation · StreamChunk                  │
│  PromptRetryPolicy · ResponseParser · SerializationGuards    │
│  PromptEnvironmentManager · PromptMigrationAssistant         │
│  PromptSanitizer · PromptMetadataExtractor                   │
│  PromptDependencyGraph · PromptSemanticSearch · PromptAnalytics│
└──────────────────────────────────────────────────────────────┘
```

---

## 1. Orchestration & Pipelines

These modules control how prompts flow through the system, from routing to execution.

### Main

**File:** `Main.cs` · **Purpose:** Primary entry point and fluent API facade.

Provides the top-level `Prompt` class with fluent builder methods for creating, rendering, and executing prompts. Wraps the template system, options, and execution pipeline into a single cohesive API.

### PromptPipeline

**File:** `PromptPipeline.cs` · **Purpose:** Middleware-based execution pipeline.

Implements a composable middleware pipeline (similar to ASP.NET middleware) for prompt processing. Each middleware receives a `PromptPipelineContext` and can modify the prompt before/after execution. Supports short-circuiting, timing, and metadata injection.

**Key classes:** `PromptPipelineContext`, `IPromptMiddleware`, `PromptPipeline`, `CachingMiddleware`, `LoggingMiddleware`, `RetryMiddleware`, `ValidationMiddleware`

### PromptChain

**File:** `PromptChain.cs` · **Purpose:** Sequential multi-step prompt execution.

Chains multiple prompts together where each step's output feeds into the next step's input. Supports variable mapping between steps, conditional branching, and rollback on failure.

### PromptWorkflow

**File:** `PromptWorkflow.cs` · **Purpose:** Complex workflow orchestration with branching.

Extends chains with parallel execution, conditional branching, loops, and state management. Supports workflow serialization and resumption.

### PromptFallbackChain

**File:** `PromptFallbackChain.cs` · **Purpose:** Cascading fallback strategy.

Tries a sequence of prompts/models in order, falling back to the next on failure. Configurable retry policies and failure criteria per level.

### PromptEnsemble

**File:** `PromptEnsemble.cs` · **Purpose:** Multi-model consensus.

Sends the same prompt to multiple models and aggregates results using configurable strategies (majority vote, best score, weighted average).

---

## 2. Routing & Composition

### PromptRouter

**File:** `PromptRouter.cs` · **Purpose:** Intent-based template selection.

Routes user input to the most appropriate prompt template using keyword matching, regex patterns, and priority scoring. Supports wildcard routes and dynamic route registration. All user-supplied regex patterns are executed with a timeout to prevent ReDoS.

### PromptComposer

**File:** `PromptComposer.cs` · **Purpose:** Section-based prompt assembly.

Builds complex prompts from named sections (system, context, instructions, examples, constraints) with ordering rules and conditional inclusion.

### PromptConditional

**File:** `PromptConditional.cs` · **Purpose:** Conditional prompt construction.

Applies if/else/switch logic to include or exclude prompt sections based on runtime variables, enabling dynamic prompt adaptation.

### PromptNegotiator

**File:** `PromptNegotiator.cs` · **Purpose:** Multi-round prompt refinement.

Implements iterative prompt-response cycles where each round refines the prompt based on previous responses, converging toward a desired output.

---

## 3. Template & Rendering

### PromptTemplate

**File:** `PromptTemplate.cs` · **Purpose:** Core template rendering engine.

Handles variable substitution (`{{variable}}`), partial templates, default values, and template inheritance. The foundational rendering primitive used by most other modules.

### PromptInterpolator

**File:** `PromptInterpolator.cs` · **Purpose:** Advanced string interpolation.

Extends basic variable substitution with expressions, filters, and formatting directives. Supports nested interpolation and custom filter registration.

### PromptSlotFiller

**File:** `PromptSlotFiller.cs` · **Purpose:** Named slot population.

Fills predefined slots in structured prompts with type-validated content. Supports required/optional slots and slot-level constraints.

### FewShotBuilder

**File:** `FewShotBuilder.cs` · **Purpose:** Few-shot example management.

Builds, selects, and formats few-shot examples for inclusion in prompts. Supports example scoring, diversity selection, and format customization (e.g., `Q:` / `A:` or `User:` / `Assistant:`).

### PromptChatFormatter

**File:** `PromptChatFormatter.cs` · **Purpose:** Chat message formatting.

Converts between different chat formats (OpenAI messages array, Anthropic XML, plain text). Handles role mapping, system message injection, and multi-turn formatting.

### PromptToolFormatter

**File:** `PromptToolFormatter.cs` · **Purpose:** Tool/function call formatting.

Formats tool definitions and function calls for different model APIs. Generates JSON schemas from C# types and formats tool results for inclusion in conversations.

### PromptContextBuilder

**File:** `PromptContextBuilder.cs` · **Purpose:** Dynamic context assembly.

Builds context sections from multiple sources (documents, database results, API responses) with priority ranking and token budget awareness.

### PromptContextCompressor

**File:** `PromptContextCompressor.cs` · **Purpose:** Context window optimization.

Compresses context to fit within token limits using summarization, truncation, and relevance scoring. Preserves the most important information.

---

## 4. Analysis & Quality

### PromptLinter

**File:** `PromptLinter.cs` · **Purpose:** Prompt quality linting.

Analyzes prompts for common issues: ambiguity, missing constraints, injection vulnerabilities, excessive length, and style inconsistencies. Reports issues with severity levels and fix suggestions.

### PromptComplexityScorer

**File:** `PromptComplexityScorer.cs` · **Purpose:** Complexity metrics.

Scores prompt complexity based on token count, nesting depth, variable count, conditional branches, and instruction density. Useful for cost estimation and optimization targeting.

### PromptGuard

**File:** `PromptGuard.cs` · **Purpose:** Input/output safety guardrails.

Validates prompts and responses against configurable safety rules: injection detection, PII scanning, content policy enforcement, and output format validation.

### PromptGrammarValidator

**File:** `PromptGrammarValidator.cs` · **Purpose:** Structural validation.

Validates prompt structure against grammar rules: balanced delimiters, valid template syntax, proper section nesting, and character encoding correctness.

### PromptQualityGate

**File:** `PromptQualityGate.cs` · **Purpose:** Pre-execution quality checks.

Runs a configurable set of quality checks before prompt execution. Blocks or warns on prompts that fail minimum quality thresholds.

### PromptSimilarityAnalyzer

**File:** `PromptSimilarityAnalyzer.cs` · **Purpose:** Prompt comparison.

Computes similarity between prompts using Levenshtein distance, LCS, Jaccard similarity, and semantic overlap. Uses optimized two-row DP for memory efficiency.

### PromptResponseEvaluator

**File:** `PromptResponseEvaluator.cs` · **Purpose:** Response quality scoring.

Evaluates model responses against configurable criteria: relevance, completeness, format compliance, factual consistency, and custom scoring functions.

### PromptComplianceChecker

**File:** `PromptComplianceChecker.cs` · **Purpose:** Regulatory compliance.

Checks prompts against compliance frameworks (GDPR, HIPAA, SOC2) for data handling, consent language, and information disclosure requirements.

### PromptFuzzer

**File:** `PromptFuzzer.cs` · **Purpose:** Adversarial testing.

Generates adversarial prompt variants to test robustness: injection attempts, encoding tricks, boundary values, and edge-case inputs.

### PromptFingerprint

**File:** `PromptFingerprint.cs` · **Purpose:** Prompt identity hashing.

Generates stable fingerprints for prompts that ignore whitespace and variable values, enabling deduplication and change detection across versions.

---

## 5. Optimization & Cost

### TokenBudget

**File:** `TokenBudget.cs` · **Purpose:** Token allocation management.

Manages token budgets across prompt sections with priorities and minimum/maximum allocations. Supports dynamic rebalancing as content changes.

### PromptTokenOptimizer

**File:** `PromptTokenOptimizer.cs` · **Purpose:** Token usage minimization.

Reduces token usage through synonym substitution, whitespace normalization, redundancy removal, and abbreviation while preserving semantic meaning.

### PromptCostEstimator

**File:** `PromptCostEstimator.cs` · **Purpose:** API cost calculation.

Estimates execution cost based on model pricing, token counts, and expected response lengths. Supports multiple pricing models and bulk discount calculations.

### PromptMinifier

**File:** `PromptMinifier.cs` · **Purpose:** Prompt compression.

Aggressively minifies prompts by removing unnecessary whitespace, comments, and redundant instructions while maintaining model comprehension.

### PromptSplitter

**File:** `PromptSplitter.cs` · **Purpose:** Long prompt segmentation.

Splits prompts that exceed token limits into coherent segments with overlap for context continuity. Handles section boundaries intelligently.

### PromptSamplerConfig

**File:** `PromptSamplerConfig.cs` · **Purpose:** Sampling parameter management.

Manages temperature, top-p, top-k, frequency/presence penalties, and stop sequences. Provides presets (creative, deterministic, balanced) and parameter validation.

### PromptRateLimiter

**File:** `PromptRateLimiter.cs` · **Purpose:** API rate limiting.

Implements token bucket and sliding window rate limiting with per-model and per-key quotas. Supports queuing and priority-based scheduling.

### PromptBatchProcessor

**File:** `PromptBatchProcessor.cs` · **Purpose:** Bulk prompt execution.

Processes multiple prompts in parallel with configurable concurrency, retry policies, progress reporting, and result aggregation.

---

## 6. Persistence & History

### PromptLibrary

**File:** `PromptLibrary.cs` · **Purpose:** Template repository.

Stores and manages a collection of prompt templates with tagging, searching, versioning, and access control. Supports import/export and sharing.

### PromptHistory

**File:** `PromptHistory.cs` · **Purpose:** Execution history tracking.

Records prompt executions with inputs, outputs, timings, and metadata. Supports querying, filtering, and replay of historical executions.

### PromptVersionManager

**File:** `PromptVersionManager.cs` · **Purpose:** Prompt versioning.

Tracks prompt versions with semantic versioning, changelogs, and rollback capability. Supports branching and diff between versions.

### PromptCache

**File:** `PromptCache.cs` · **Purpose:** Response caching.

Caches model responses keyed by prompt fingerprint with TTL, LRU eviction, and invalidation rules. Supports both in-memory and persistent backends.

### PromptAuditLog

**File:** `PromptAuditLog.cs` · **Purpose:** Compliance audit trail.

Immutable log of all prompt operations for compliance and debugging. Records who, what, when, and why for each operation.

### PromptReplayRecorder

**File:** `PromptReplayRecorder.cs` · **Purpose:** Session recording.

Records complete prompt sessions (inputs, outputs, timings, errors) for replay, debugging, and training data generation.

### PromptAnnotation

**File:** `PromptAnnotation.cs` · **Purpose:** Prompt metadata tagging.

Attaches annotations (labels, scores, comments) to prompts and responses for review workflows, quality tracking, and dataset curation.

### PromptDiff

**File:** `PromptDiff.cs` · **Purpose:** Prompt change tracking.

Computes structural diffs between prompt versions showing added, removed, and modified sections, variables, and instructions.

---

## 7. Testing & Debugging

### PromptTestSuite

**File:** `PromptTestSuite.cs` · **Purpose:** Automated prompt testing.

Defines test cases with expected outputs, assertions, and pass/fail criteria. Supports parameterized tests, test fixtures, and result reporting.

### PromptGoldenTester

**File:** `PromptGoldenTester.cs` · **Purpose:** Golden file testing.

Compares prompt outputs against stored golden files with configurable tolerance for non-deterministic outputs (regex matching, semantic similarity).

### PromptDebugger

**File:** `PromptDebugger.cs` · **Purpose:** Step-through debugging.

Provides step-by-step prompt execution visibility: variable resolution, template rendering, middleware effects, and model interaction tracing.

### PromptABTester

**File:** `PromptABTester.cs` · **Purpose:** A/B testing framework.

Runs controlled experiments comparing prompt variants with statistical significance testing, metric tracking, and winner selection.

### PromptScorecardBuilder

**File:** `PromptScorecardBuilder.cs` · **Purpose:** Quality scorecards.

Generates summary scorecards for prompt performance across multiple metrics, time periods, and model versions.

---

## 8. Export & Transformation

### PromptMarkdownExporter

**File:** `PromptMarkdownExporter.cs` · **Purpose:** Markdown documentation export.

Exports prompts as formatted Markdown documents with metadata, examples, and usage instructions.

### PromptDocGenerator

**File:** `PromptDocGenerator.cs` · **Purpose:** API documentation generation.

Generates comprehensive API documentation from prompt definitions, including parameter descriptions, examples, and type information.

### PromptSchemaGenerator

**File:** `PromptSchemaGenerator.cs` · **Purpose:** JSON schema generation.

Generates JSON schemas from prompt input/output specifications for validation and API contract definition.

### PromptDatasetBuilder

**File:** `PromptDatasetBuilder.cs` · **Purpose:** Training data generation.

Builds datasets from prompt executions for fine-tuning, evaluation, and benchmarking. Supports multiple output formats (JSONL, CSV, Parquet).

### PromptStyleTransfer

**File:** `PromptStyleTransfer.cs` · **Purpose:** Prompt style adaptation.

Transforms prompt style (formal ↔ casual, technical ↔ simple) while preserving semantic intent. Useful for audience-specific prompt variants.

### PromptLocalizer

**File:** `PromptLocalizer.cs` · **Purpose:** Multi-language support.

Manages prompt translations with locale-specific formatting, cultural adaptation, and fallback language chains.

### PromptRefactorer

**File:** `PromptRefactorer.cs` · **Purpose:** Automated prompt improvement.

Analyzes and suggests refactoring actions: extract reusable sections, simplify complex conditionals, deduplicate similar prompts, and normalize formatting.

### PromptExplainer

**File:** `PromptExplainer.cs` · **Purpose:** Prompt explanation generation.

Generates human-readable explanations of what a prompt does, why it's structured that way, and what each section contributes.

### PromptMerger

**File:** `PromptMerger.cs` · **Purpose:** Prompt combination.

Merges multiple prompts into one, handling conflicting instructions, variable namespacing, and section deduplication.

---

## 9. Infrastructure

### PromptOptions

**File:** `PromptOptions.cs` · **Purpose:** Global configuration.

Central configuration for model selection, API endpoints, default parameters, timeout settings, and feature flags.

### Conversation

**File:** `Conversation.cs` · **Purpose:** Multi-turn conversation state.

Manages conversation history with message roles, token counting, context window management, and serialization. Supports branching and forking conversations.

### StreamChunk

**File:** `StreamChunk.cs` · **Purpose:** Streaming response handling.

Data structures for processing streaming API responses chunk-by-chunk with aggregation and event-based processing.

### PromptRetryPolicy

**File:** `PromptRetryPolicy.cs` · **Purpose:** Retry and resilience.

Configurable retry policies with exponential backoff, jitter, circuit breaker patterns, and per-error-type handling.

### ResponseParser

**File:** `ResponseParser.cs` · **Purpose:** Response format extraction.

Parses model responses to extract structured data (JSON, XML, tables, key-value pairs) with format detection and validation.

### SerializationGuards

**File:** `SerializationGuards.cs` · **Purpose:** Safe deserialization.

Validates and sanitizes serialized data during deserialization to prevent injection, type confusion, and data corruption.

### PromptSanitizer

**File:** `PromptSanitizer.cs` · **Purpose:** Input sanitization.

Cleans user input before prompt insertion: HTML/script stripping, encoding normalization, and injection pattern removal.

### PromptMetadataExtractor

**File:** `PromptMetadataExtractor.cs` · **Purpose:** Automatic metadata extraction.

Extracts metadata (language, topic, complexity, required capabilities) from prompt content for indexing and routing.

### PromptDependencyGraph

**File:** `PromptDependencyGraph.cs` · **Purpose:** Template dependency tracking.

Builds and analyzes the dependency graph between templates, partials, and shared variables. Detects cycles and orphaned templates.

### PromptSemanticSearch

**File:** `PromptSemanticSearch.cs` · **Purpose:** Similarity-based template search.

Searches the prompt library using semantic similarity (TF-IDF, BM25) rather than exact keyword matching.

### PromptAnalytics

**File:** `PromptAnalytics.cs` · **Purpose:** Usage analytics.

Tracks prompt usage patterns, response quality trends, cost over time, and error rates with dashboard-ready data export.

### PromptEnvironmentManager

**File:** `PromptEnvironmentManager.cs` · **Purpose:** Environment-specific configuration.

Manages different configurations for development, staging, and production environments with variable overrides and feature flags.

### PromptMigrationAssistant

**File:** `PromptMigrationAssistant.cs` · **Purpose:** Cross-model migration.

Assists migration of prompts between different model APIs (OpenAI → Anthropic → Google) with format conversion and behavioral testing.

---

## Module Statistics

| Category | Modules | Description |
|----------|---------|-------------|
| Orchestration | 5 | Pipeline, chains, workflows, fallbacks, ensembles |
| Routing | 4 | Intent routing, composition, conditionals, negotiation |
| Template | 8 | Rendering, interpolation, formatting, context building |
| Analysis | 10 | Linting, safety, compliance, fuzzing, similarity |
| Optimization | 8 | Tokens, cost, rate limiting, batching, splitting |
| Persistence | 8 | Library, history, versioning, caching, audit |
| Testing | 5 | Test suites, golden tests, A/B testing, debugging |
| Export | 9 | Markdown, docs, schemas, datasets, localization |
| Infrastructure | 13 | Config, conversation, parsing, search, analytics |
| **Total** | **70** | |

---

## Common Patterns

### Fluent Builder

Many modules use a fluent builder pattern for configuration:

```csharp
var result = new PromptPipeline()
    .Use(new CachingMiddleware(cache))
    .Use(new RetryMiddleware(retryPolicy))
    .Use(new LoggingMiddleware(logger))
    .Execute(context);
```

### Serialization

All major data structures support JSON serialization via `System.Text.Json` with custom converters where needed. Use `SerializationGuards` for safe deserialization of untrusted data.

### Token Awareness

Modules that manipulate prompt content integrate with `TokenBudget` to stay within context window limits. Token counting is available through `PromptOptions.TokenCounter`.

### Thread Safety

Most modules are designed as stateless processors or use `ConcurrentDictionary` / `ReaderWriterLockSlim` for thread-safe state. See `PromptCache` and `PromptRateLimiter` for examples of concurrent-safe implementations.

---

## See Also

- [Getting Started](getting-started.md) — Quick setup and first prompt
- [Templates](templates.md) — Template syntax and rendering
- [Chains](chains.md) — Multi-step prompt chains
- [Safety](safety.md) — Security and guardrails
- [Testing](testing.md) — Testing strategies
- [Production Features](production-features.md) — Caching, rate limiting, batching
