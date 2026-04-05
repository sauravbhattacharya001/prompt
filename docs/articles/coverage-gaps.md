# Documentation Coverage Gaps

This file tracks features and classes that lack dedicated documentation articles.

## High Priority

These are major features with no corresponding article in `docs/articles/`:

| Feature | Classes | Suggested Article |
|---------|---------|-------------------|
| Prompt Debugging & Analysis | `PromptDebugger`, `PromptExplainer`, `PromptComplexityScorer` | `debugging.md` |
| A/B Testing & Variants | `PromptABTester`, `PromptVariantGenerator`, `PromptMatrix` | `ab-testing.md` |
| Analytics & Reporting | `PromptAnalytics`, `PromptUsageReport`, `PromptCostEstimator` | `analytics.md` |
| Prompt Linting & Quality | `PromptLinter`, `PromptQualityGate`, `PromptComplianceChecker` | `linting.md` |
| Workflow & Pipelines | `PromptWorkflow`, `PromptPipeline`, `PromptDependencyGraph` | `workflows.md` |
| Template Inheritance | `PromptInheritance`, `PromptConditional`, `PromptInterpolator` | `advanced-templates.md` |
| Caching & Performance | `PromptCache`, `PromptRateLimiter`, `PromptPerformanceProfiler` | `caching.md` |
| Snapshot & Promotion | `PromptSnapshotManager`, `PromptPromotionManager` | `lifecycle.md` |
| Resilience | `PromptFallbackChain`, `PromptRetryPolicy`, `PromptEnsemble` | `resilience.md` |

## Medium Priority

| Feature | Classes | Suggested Article |
|---------|---------|-------------------|
| Export & Import | `PromptCatalogExporter`, `PromptMarkdownExporter`, `PromptDocGenerator` | `export.md` |
| Audit & Replay | `PromptAuditLog`, `PromptReplayRecorder`, `PromptHistory` | `auditing.md` |
| Schema & Validation | `PromptSchemaGenerator`, `PromptOutputValidator`, `PromptGrammarValidator` | `validation.md` |
| Risk & Security | `PromptRiskAssessor`, `PromptSanitizer`, `PromptFingerprint` | `risk-assessment.md` |
| Cross-Provider | `PromptCompatibilityChecker`, `PromptMigrationAssistant`, `PromptChatFormatter` | `cross-provider.md` |
| Scoring & Evaluation | `PromptScorecardBuilder`, `PromptResponseEvaluator`, `PromptGoldenTester`, `PromptBenchmarkSuite` | `evaluation.md` |

## Low Priority

| Feature | Classes | Suggested Article |
|---------|---------|-------------------|
| Prompt Utilities | `PromptMinifier`, `PromptSplitter`, `PromptMerger`, `PromptTokenOptimizer` | `utilities.md` |
| Context Management | `PromptContextBuilder`, `PromptContextCompressor` | `context.md` |
| Metadata & Search | `PromptMetadataExtractor`, `PromptSemanticSearch`, `PromptSimilarityAnalyzer` | `search.md` |
| Slot Filling & Negotiation | `PromptSlotFiller`, `PromptNegotiator` | `interactive.md` |
| Signatures & Formatting | `PromptSignature`, `PromptToolFormatter`, `PromptStyleTransfer` | `formatting.md` |
| Annotations & Datasets | `PromptAnnotation`, `PromptDatasetBuilder` | `datasets.md` |
| Visualization | `PromptChainVisualizer`, `PromptChangeImpactAnalyzer` | `visualization.md` |
| Changelog & Diff | `PromptChangelogGenerator`, `PromptDiff`, `PromptRefactorer` | `changelog.md` |

## Existing Coverage

The following topics are covered by current articles:

- Getting started → `getting-started.md`
- Templates → `templates.md`
- Chains → `chains.md`
- Conversations → `conversations.md`
- Options & configuration → `options.md`
- Safety & guards → `safety.md`
- Testing → `testing.md`
- Error handling → `error-handling.md`
- Architecture → `architecture.md`
- Advanced features → `advanced-features.md`
- Production features → `production-features.md`
- Migration → `migration.md`
- Security hardening → `security-hardening.md`
- Streaming → `streaming.md`
