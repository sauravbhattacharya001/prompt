# Prompt Chains

The `PromptChain` class connects multiple prompt steps into a pipeline where each step's output feeds into the next as a template variable. This enables multi-step reasoning patterns like summarize→translate, extract→analyze, or generate→review→refine.

## Basic Chain

```csharp
using Prompt;

var chain = new PromptChain()
    .AddStep("summarize",
        new PromptTemplate("Summarize this text in 2 sentences: {{text}}"),
        "summary")
    .AddStep("translate",
        new PromptTemplate("Translate to French: {{summary}}"),
        "french")
    .AddStep("keywords",
        new PromptTemplate("Extract 5 keywords from: {{summary}}"),
        "keywords");

var result = await chain.RunAsync(new Dictionary<string, string>
{
    ["text"] = "Your long article text here..."
});

Console.WriteLine(result.FinalResponse);          // keywords output
Console.WriteLine(result.GetOutput("summary"));   // the summary
Console.WriteLine(result.GetOutput("french"));     // French translation
```

### How It Works

1. Each step renders its template using all accumulated variables
2. The rendered prompt is sent to Azure OpenAI
3. The response is stored under the step's `outputVariable` name
4. Subsequent steps can reference it as `{{outputVariable}}`

## Configuration

Set a system prompt, retry policy, and model options for the entire chain:

```csharp
var chain = new PromptChain()
    .WithSystemPrompt("You are a senior technical writer.")
    .WithMaxRetries(5)
    .WithOptions(new PromptOptions
    {
        Temperature = 0.3f,
        MaxTokens = 2000
    })
    .AddStep("outline",
        new PromptTemplate("Create an outline for a guide about {{topic}}."),
        "outline")
    .AddStep("draft",
        new PromptTemplate("Write the full guide based on this outline:\n\n{{outline}}"),
        "draft");
```

## Working with Results

The `ChainResult` provides detailed information about every step:

```csharp
ChainResult result = await chain.RunAsync(initialVars);

// Final output
string? finalAnswer = result.FinalResponse;

// Access any step's output by variable name
string? summary = result.GetOutput("summary");

// Total execution time
Console.WriteLine($"Total time: {result.TotalElapsed}");

// Iterate over individual steps
foreach (StepResult step in result.Steps)
{
    Console.WriteLine($"Step: {step.StepName}");
    Console.WriteLine($"  Prompt: {step.RenderedPrompt}");
    Console.WriteLine($"  Response: {step.Response}");
    Console.WriteLine($"  Time: {step.Elapsed}");
}

// All accumulated variables (inputs + outputs)
foreach (var (key, value) in result.Variables)
{
    Console.WriteLine($"{key} = {value}");
}
```

### Export Results as JSON

```csharp
string json = result.ToJson();
// Includes step details, timing, all variables — useful for logging and debugging
```

## Validation

Check that all template variables can be satisfied before running:

```csharp
var errors = chain.Validate(new Dictionary<string, string>
{
    ["text"] = "sample input"
});

if (errors.Count > 0)
{
    foreach (var error in errors)
        Console.WriteLine($"⚠ {error}");
}
else
{
    Console.WriteLine("Chain is valid.");
}
```

Validation traces variable flow through the chain — it checks that each step's required variables are either in the initial input or produced by a prior step.

## Common Patterns

### Summarize → Translate

```csharp
var chain = new PromptChain()
    .AddStep("summarize",
        new PromptTemplate("Summarize in 3 sentences:\n\n{{text}}"),
        "summary")
    .AddStep("translate",
        new PromptTemplate("Translate to {{language}}:\n\n{{summary}}"),
        "translation");

var result = await chain.RunAsync(new Dictionary<string, string>
{
    ["text"] = longArticle,
    ["language"] = "Spanish"
});
```

### Generate → Review → Refine

```csharp
var chain = new PromptChain()
    .WithOptions(PromptOptions.ForCodeGeneration())
    .AddStep("generate",
        new PromptTemplate("Write a {{language}} function that {{task}}."),
        "code")
    .AddStep("review",
        new PromptTemplate("Review this code for bugs and edge cases:\n\n{{code}}"),
        "review")
    .AddStep("refine",
        new PromptTemplate("Fix the issues found in the review and return the improved code.\n\nCode:\n{{code}}\n\nReview:\n{{review}}"),
        "final_code");
```

### Extract → Analyze

```csharp
var chain = new PromptChain()
    .WithOptions(PromptOptions.ForDataExtraction())
    .AddStep("extract",
        new PromptTemplate("Extract all dates, names, and monetary amounts from:\n\n{{document}}"),
        "entities")
    .AddStep("analyze",
        new PromptTemplate("Analyze these extracted entities and identify patterns or anomalies:\n\n{{entities}}"),
        "analysis");
```

## Serialization

Save and load chain definitions as JSON for reuse:

```csharp
// Save definition
string json = chain.ToJson();
await chain.SaveToFileAsync("chains/summarize-translate.json");

// Load definition
var loaded = PromptChain.FromJson(json);
var fromFile = await PromptChain.LoadFromFileAsync("chains/summarize-translate.json");

// Run the loaded chain
var result = await loaded.RunAsync(initialVars);
```

## Cancellation

Cancel long-running chains:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

try
{
    var result = await chain.RunAsync(initialVars, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Chain cancelled — partial results may be available.");
}
```

Cancellation is checked between steps and passed through to each Azure OpenAI call, so a cancel request will stop the chain as soon as the current step completes.
