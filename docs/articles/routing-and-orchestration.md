# Routing & Orchestration

This guide covers two powerful modules for building dynamic, multi-path prompt
applications: **PromptRouter** for intent-based template selection and
**PromptWorkflow** for DAG-based prompt orchestration with branching, merging,
and parallel execution.

---

## Prompt Routing (`PromptRouter`)

When your application handles multiple types of user requests, hardcoding
template selection becomes brittle. `PromptRouter` classifies user input and
automatically selects the best template based on keyword matching, regex
patterns, and priority scoring.

### Basic Setup

```csharp
using Prompt;

var library = new PromptLibrary();
library.Register("code-review", new PromptTemplate("Review this code:\n{{code}}"));
library.Register("explain", new PromptTemplate("Explain this concept:\n{{topic}}"));
library.Register("summarize", new PromptTemplate("Summarize:\n{{text}}"));

var router = new PromptRouter(library);

router.AddRoute("code-review", new RouteConfig
{
    Keywords = new[] { "review", "code", "bug", "fix", "refactor" },
    Patterns = new[] { @"review\s+(this|my)\s+code", @"find\s+bugs?" },
    TemplateName = "code-review",
    Priority = 1.0
});

router.AddRoute("explain", new RouteConfig
{
    Keywords = new[] { "explain", "what", "how", "why", "concept" },
    TemplateName = "explain",
    Priority = 0.8
});

router.AddRoute("summarize", new RouteConfig
{
    Keywords = new[] { "summarize", "summary", "tldr", "brief" },
    TemplateName = "summarize",
    Priority = 0.9
});
```

### Routing a Request

```csharp
var match = router.Route("Can you review my code for bugs?");
// match.RouteName == "code-review"
// match.Score > 0
// match.TemplateName == "code-review"
```

The router scores each route against the input using:

1. **Keyword matches** — case-insensitive word boundary matching, each hit adds
   to the score
2. **Regex patterns** — bonus score for pattern matches (run with a 2-second
   timeout to prevent ReDoS)
3. **Priority weight** — multiplied into the final score

The highest-scoring route above the minimum threshold (default `0.1`) wins.

### Fallback Route

Set a fallback for when no route meets the threshold:

```csharp
router.SetFallback("explain");

var match = router.Route("hello there");
// If no route scores above 0.1, falls back to "explain"
```

### Adjusting Sensitivity

```csharp
router.SetMinScore(0.3); // Require stronger matches
```

### Integration with PromptLibrary

When constructed with a `PromptLibrary`, the router can look up templates
directly:

```csharp
var match = router.Route(userInput);
if (match != null)
{
    var template = library.Get(match.TemplateName);
    string rendered = template.Render(new { code = userCode });
    string response = await Main.GetResponseAsync(rendered);
}
```

---

## Prompt Workflows (`PromptWorkflow`)

For complex applications that need branching logic, parallel execution, and
result merging, `PromptWorkflow` provides a DAG (directed acyclic graph) engine
for prompt orchestration.

### Core Concepts

| Concept | Description |
|---------|-------------|
| **Node** | A single step — either a prompt template, a branch point, or a merge point |
| **Edge** | A connection between nodes, optionally with a condition |
| **Branch** | Conditional fork — routes to different nodes based on the previous output |
| **Merge** | Combines outputs from multiple parent nodes using a configurable strategy |
| **Status** | Each node tracks its state: `Pending`, `Running`, `Completed`, `Skipped`, `Failed` |

### Building a Workflow

```csharp
using Prompt;

var workflow = new PromptWorkflow();

// Add prompt nodes
workflow.AddNode(new WorkflowNode("classify")
{
    Template = "Classify this request as 'technical' or 'general': {{input}}"
});

workflow.AddNode(new WorkflowNode("technical-response")
{
    Template = "Provide a detailed technical answer to: {{input}}"
});

workflow.AddNode(new WorkflowNode("general-response")
{
    Template = "Provide a friendly, accessible answer to: {{input}}"
});

workflow.AddNode(new WorkflowNode("format-output")
{
    Template = "Format this response for the user:\n{{response}}",
    MergeStrategy = MergeStrategy.FirstCompleted
});

// Define edges with conditions
workflow.AddEdge("classify", "technical-response",
    condition: output => output.Contains("technical"));
workflow.AddEdge("classify", "general-response",
    condition: output => output.Contains("general"));
workflow.AddEdge("technical-response", "format-output");
workflow.AddEdge("general-response", "format-output");

// Set entry point
workflow.SetEntryNode("classify");
```

### Running a Workflow

```csharp
var result = await workflow.ExecuteAsync(new Dictionary<string, string>
{
    ["input"] = "How does TCP three-way handshake work?"
});

Console.WriteLine(result.FinalOutput);
Console.WriteLine($"Nodes executed: {result.ExecutedNodes.Count}");
Console.WriteLine($"Total duration: {result.Duration}");
```

### Merge Strategies

When a node receives inputs from multiple parents, choose how to combine them:

| Strategy | Behavior |
|----------|----------|
| `ConcatenateAll` | Wait for all parents, join outputs with newlines |
| `JoinWithSeparator` | Wait for all parents, join with a custom separator |
| `FirstCompleted` | Take the first parent that finishes |
| `LongestOutput` | Take the longest output among parents |
| `ShortestOutput` | Take the shortest output among parents |
| `CustomTemplate` | Use a merge template that references parent outputs by node ID |

### Parallel Execution

Nodes without dependencies on each other execute in parallel automatically.
In the example above, `technical-response` and `general-response` are
independent branches — only one runs based on the classify output, but if
both edges matched, they would execute concurrently.

### Error Handling

Failed nodes record their exception and transition to `Failed` status.
Downstream nodes that depend on a failed node are automatically `Skipped`.
Check `result.FailedNodes` for diagnostics:

```csharp
var result = await workflow.ExecuteAsync(variables);
if (result.FailedNodes.Any())
{
    foreach (var (nodeId, ex) in result.FailedNodes)
    {
        Console.WriteLine($"Node '{nodeId}' failed: {ex.Message}");
    }
}
```

### Serialization

Workflows serialize to JSON for storage and reuse:

```csharp
string json = workflow.ToJson();
var restored = PromptWorkflow.FromJson(json);
```

---

## Combining Router + Workflow

A common pattern is using the router for top-level intent classification, then
dispatching to different workflows based on the route:

```csharp
var match = router.Route(userInput);

var workflow = match.RouteName switch
{
    "code-review" => codeReviewWorkflow,
    "data-analysis" => dataAnalysisWorkflow,
    _ => generalWorkflow
};

var result = await workflow.ExecuteAsync(new Dictionary<string, string>
{
    ["input"] = userInput
});
```

This gives you keyword-based fast routing at the top level with full DAG
orchestration for complex multi-step tasks underneath.
