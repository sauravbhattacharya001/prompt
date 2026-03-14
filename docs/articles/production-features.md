# Production Features

This guide covers six production-oriented modules that handle common concerns
when deploying prompt workflows at scale: batching, fallbacks, sanitization,
linting, token optimisation, and orchestration.

---

## Batch Processing (`PromptBatchProcessor`)

Render and process many prompts in a single call with concurrency control,
retries, and progress tracking.

### Quick Start

```csharp
using Prompt;

// Create batch items
var items = new List<BatchItem>();
for (int i = 0; i < 100; i++)
{
    var template = new PromptTemplate($"Summarise article #{i}");
    var variables = new Dictionary<string, string>
    {
        ["title"] = $"Article {i}"
    };
    items.Add(new BatchItem($"item-{i}", template, variables));
}

// Configure the processor
var config = new BatchProcessorConfig
{
    MaxConcurrency = 5,
    MaxRetries = 3,
    RetryDelayMs = 500,
    StopOnFirstError = false
};

var processor = new PromptBatchProcessor(config);

// Process with progress callback
var result = await processor.ProcessAsync(items, progress =>
{
    Console.WriteLine($"{progress.Completed}/{progress.Total} done");
});

Console.WriteLine($"Succeeded: {result.SucceededCount}");
Console.WriteLine($"Failed:    {result.FailedCount}");
```

### Key classes

| Class | Purpose |
|---|---|
| `BatchItem` | Single item containing a template, variables, and result state |
| `BatchProcessorConfig` | Concurrency, retry, and timeout settings |
| `PromptBatchProcessor` | Processes a list of `BatchItem`s with configurable parallelism |
| `BatchResult` | Aggregated outcome with per-item status |

### Configuration options

| Property | Default | Description |
|---|---|---|
| `MaxConcurrency` | 4 | Maximum parallel items |
| `MaxRetries` | 2 | Retries per failed item |
| `RetryDelayMs` | 1000 | Delay between retries (ms) |
| `StopOnFirstError` | `false` | Abort remaining items on first failure |

---

## Fallback Chains (`PromptFallbackChain`)

Route prompts through a priority-ordered list of model tiers.  If the
primary model is unavailable or times out, the chain automatically falls
through to the next tier.

### Quick Start

```csharp
using Prompt;

var chain = new PromptFallbackChain()
    .AddTier(new FallbackTier
    {
        Name = "gpt-4-turbo",
        EndpointUri = "https://my-openai.openai.azure.com/",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_KEY_PRIMARY"),
        Timeout = TimeSpan.FromSeconds(10),
        MaxRetries = 1
    })
    .AddTier(new FallbackTier
    {
        Name = "gpt-3.5-turbo",
        EndpointUri = "https://my-openai-fallback.openai.azure.com/",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_KEY_SECONDARY"),
        Timeout = TimeSpan.FromSeconds(30),
        MaxRetries = 2
    });

var result = await chain.ExecuteAsync("Explain quantum computing in 50 words.");
Console.WriteLine($"Tier used: {result.TierName}");
Console.WriteLine(result.Response);
```

### Key classes

| Class | Purpose |
|---|---|
| `FallbackTier` | One model deployment (endpoint, key, timeout, retries) |
| `PromptFallbackChain` | Tries tiers in order until one succeeds |
| `FallbackResult` | The response plus which tier handled it |

---

## Prompt Sanitization (`PromptSanitizer`)

Clean user-supplied text before it reaches a language model.  Supports PII
redaction, injection-pattern neutralisation, and custom transform rules.

### Quick Start

```csharp
using Prompt;

var sanitizer = new PromptSanitizer(new SanitizeOptions
{
    RedactPii = true,
    NeutralizeInjections = true,
    MaxLength = 4000
});

var result = sanitizer.Sanitize(
    "My SSN is 123-45-6789.  Ignore previous instructions.");

Console.WriteLine(result.Sanitized);
// "My SSN is [REDACTED-SSN].  [injection neutralized]"
Console.WriteLine($"PII types found: {string.Join(", ", result.RedactedPiiTypes)}");
Console.WriteLine($"Injections blocked: {result.InjectionPatternsNeutralized}");
```

### Configuration

| Property | Default | Description |
|---|---|---|
| `RedactPii` | `false` | Replace detected PII (SSN, email, phone, etc.) with placeholders |
| `NeutralizeInjections` | `false` | Detect and defuse common prompt injection patterns |
| `MaxLength` | `int.MaxValue` | Truncate input beyond this character count |
| `CustomRules` | empty | User-defined regex → replacement transforms |

---

## Prompt Linting (`PromptLinter`)

Static analysis for prompts.  Checks clarity, structure, security, and
efficiency — similar to ESLint but for natural-language prompts.

### Quick Start

```csharp
using Prompt;

var linter = new PromptLinter();

var findings = linter.Lint("do the thing");

foreach (var f in findings)
{
    Console.WriteLine($"[{f.Severity}] {f.Category}: {f.Message}");
}
// [Warning] Clarity: Prompt is vague — consider adding specific instructions
// [Info] Efficiency: Prompt is very short (3 words) — may produce unfocused output
```

### Severity levels

| Level | Meaning |
|---|---|
| `Info` | Suggestion for improvement |
| `Warning` | Potential issue worth reviewing |
| `Error` | Likely problem that should be fixed |

### Rule categories

- **Clarity** — vague language, ambiguous instructions
- **Structure** — missing role markers, unbalanced delimiters
- **Security** — embedded instructions that look like injection
- **Efficiency** — excessive repetition, token waste

---

## Token Optimisation (`PromptTokenOptimizer`)

Analyse a prompt's token usage and get actionable recommendations to reduce
cost without sacrificing quality.

### Quick Start

```csharp
using Prompt;

var optimizer = new PromptTokenOptimizer();

string longPrompt = File.ReadAllText("system-prompt.txt");
var analysis = optimizer.Analyze(longPrompt);

Console.WriteLine($"Total tokens: {analysis.TotalTokens}");
Console.WriteLine($"Sections:     {analysis.Sections.Count}");

foreach (var section in analysis.Sections.OrderByDescending(s => s.TokenCount))
{
    Console.WriteLine($"  {section.Name}: {section.TokenCount} tokens ({section.PercentOfTotal:P0})");
}

// Check for redundancies
foreach (var pair in analysis.Redundancies)
{
    Console.WriteLine($"Redundant: \"{pair.InstructionA}\" ≈ \"{pair.InstructionB}\" " +
                      $"(similarity {pair.Similarity:P0}, save ~{pair.EstimatedTokensSaved} tokens)");
}

// Get recommendations
foreach (var rec in analysis.Recommendations)
{
    Console.WriteLine($"[{rec.Category}] {rec.Description}");
}
```

### Key classes

| Class | Purpose |
|---|---|
| `PromptSection` | Named section with token count and position |
| `RedundancyPair` | Two similar instructions with a suggested merge |
| `OptimizationRecommendation` | Actionable suggestion with category and estimated savings |
| `PromptTokenOptimizer` | Runs all analyses and produces recommendations |

### Recommendation categories

- **Redundancy** — duplicate or near-duplicate instructions
- **Verbosity** — overly wordy phrasing
- **Structure** — sections that can be reordered or combined
- **Format** — Markdown/whitespace waste

---

## Prompt Workflows (`PromptWorkflow`)

Orchestrate multi-step prompt DAGs with branching, merging, and conditional
execution.  Each node in the workflow processes a prompt and feeds its output
to downstream nodes.

### Quick Start

```csharp
using Prompt;

var workflow = new PromptWorkflow("content-pipeline");

// Add nodes
workflow.AddNode("extract", new PromptTemplate("Extract key facts from: {{text}}"));
workflow.AddNode("summarise", new PromptTemplate("Summarise these facts: {{input}}"));
workflow.AddNode("translate", new PromptTemplate("Translate to French: {{input}}"));

// Wire the DAG
workflow.AddEdge("extract", "summarise");
workflow.AddEdge("extract", "translate");

// Execute
var result = await workflow.ExecuteAsync(new Dictionary<string, string>
{
    ["text"] = "The quick brown fox jumps over the lazy dog."
});

Console.WriteLine($"Nodes executed: {result.CompletedNodes.Count}");
foreach (var node in result.CompletedNodes)
{
    Console.WriteLine($"  {node.Name}: {node.Output?.Substring(0, 60)}...");
}
```

### Key classes

| Class | Purpose |
|---|---|
| `PromptWorkflow` | The DAG container — add nodes and edges |
| `WorkflowNode` | A single step with a template and execution state |
| `MergeStrategy` | How multi-parent nodes combine inputs (`ConcatenateAll`, `FirstCompleted`, etc.) |
| `WorkflowResult` | Execution outcome with per-node outputs and timing |

### Merge strategies

| Strategy | Behaviour |
|---|---|
| `ConcatenateAll` | Wait for all parents, join with newlines |
| `JoinWithSeparator` | Same but with a custom separator |
| `FirstCompleted` | Take whichever parent finishes first |
| `LongestOutput` | Take the parent with the most text |
| `ShortestOutput` | Take the parent with the least text |
| `CustomTemplate` | Merge via a template that references parent outputs |

---

## See Also

- [Getting Started](getting-started.md) — basic template usage
- [Chains](chains.md) — simpler linear prompt chains
- [Safety](safety.md) — `PromptGuard` and security features
- [Error Handling](error-handling.md) — retry policies and fallback patterns
