# Advanced Features

This guide covers the advanced capabilities of the Prompt library beyond basic
template rendering and conversations. Each feature is designed to be composable
— you can use them individually or combine them into sophisticated prompt
engineering workflows.

## Prompt Pipeline

The `PromptPipeline` lets you chain middleware components that process prompts
before they reach the model. Each middleware can inspect, validate, transform,
or log the prompt at different stages.

```csharp
var pipeline = new PromptPipeline()
    .Use(new ValidationMiddleware())     // Reject unsafe prompts
    .Use(new LoggingMiddleware(logger))  // Log every request
    .Use(new CachingMiddleware(cache))   // Return cached responses
    .Use(new RetryMiddleware(maxRetries: 3))
    .Use(new MetricsMiddleware());       // Track latency & tokens

var context = new PromptPipelineContext
{
    PromptText = "Summarize this article: {{content}}",
    Variables = new Dictionary<string, string>
    {
        ["content"] = articleText
    }
};

await pipeline.ExecuteAsync(context);
string result = context.RenderedPrompt;
```

### Custom Middleware

Implement `IPromptMiddleware` or pass a lambda:

```csharp
pipeline.Use(async (context, next) =>
{
    context.Variables["timestamp"] = DateTime.UtcNow.ToString("o");
    await next(context);
    // Post-process the response here
});
```

## Prompt Caching

`PromptCache` provides an LRU cache with TTL expiration for prompt responses.
Avoid redundant API calls when the same prompt is sent repeatedly.

```csharp
var cache = new PromptCache(maxEntries: 1000, ttl: TimeSpan.FromMinutes(30));

// Store a response
cache.Set("summarize-article-123", response);

// Retrieve (returns null if expired or evicted)
var cached = cache.Get("summarize-article-123");

// Statistics
CacheStats stats = cache.GetStats();
Console.WriteLine($"Hit rate: {stats.HitRate:P1}");  // e.g., "87.3%"
```

## Cost Estimation

`PromptCostEstimator` tracks token usage and estimates costs across models.

```csharp
var estimator = new PromptCostEstimator();

// Estimate before sending
decimal inputCost = estimator.InputCost(tokenCount: 1500);
decimal outputCost = estimator.OutputCost(tokenCount: 500);
decimal total = estimator.TotalCost(inputTokens: 1500, outputTokens: 500);

// Track cumulative spending
estimator.RecordUsage(inputTokens: 1500, outputTokens: 500);
var report = estimator.GetReport();
Console.WriteLine($"Total spend: ${report.TotalCost:F4}");
```

## Token Budget

`TokenBudget` helps you stay within model context limits by allocating tokens
across prompt sections.

```csharp
var budget = new TokenBudget(maxTokens: 4096);

budget.Allocate("system", maxTokens: 500);
budget.Allocate("examples", maxTokens: 1000);
budget.Allocate("conversation", maxTokens: 2000);
budget.Allocate("response", maxTokens: 596);  // Reserve for output

// Check before adding content
if (budget.CanFit("examples", exampleText))
{
    budget.Fill("examples", exampleText);
}

// Auto-truncate to fit
string truncated = budget.FitToSection("conversation", longHistory);
```

## Few-Shot Examples

`FewShotBuilder` manages example sets for in-context learning.

```csharp
var builder = new FewShotBuilder()
    .AddExample(new FewShotExample
    {
        Input = "The food was amazing and the service was great!",
        Output = "positive"
    })
    .AddExample(new FewShotExample
    {
        Input = "Terrible experience, would not recommend.",
        Output = "negative"
    });

// Select most relevant examples for a new input
var selected = builder.SelectBest(
    query: "The hotel room was clean but noisy",
    maxExamples: 2
);

// Render as prompt text
string fewShotBlock = builder.Render(selected);
```

## Prompt Fingerprinting

`PromptFingerprint` generates content-aware hashes for deduplication and
similarity detection.

```csharp
var fp = new PromptFingerprint(NormalizationLevel.CaseInsensitive);

// Hash a prompt
string hash = fp.Compute("Summarize the following article...");

// Compare two prompts for similarity
double similarity = fp.Similarity(promptA, promptB);
// Returns 0.0 (completely different) to 1.0 (identical after normalization)

// Detect near-duplicates in a collection
var duplicates = fp.FindDuplicates(promptList, threshold: 0.9);
```

### Normalization Levels

| Level | Effect |
|-------|--------|
| `None` | Raw text, exact matching |
| `Whitespace` | Collapse whitespace, trim |
| `CaseInsensitive` | + lowercase |
| `Punctuation` | + remove punctuation |
| `OrderIndependent` | + sort words (bag-of-words matching) |

## Prompt Composer

`PromptComposer` builds structured prompts from reusable blocks.

```csharp
var composer = new PromptComposer()
    .WithRole("You are a senior code reviewer.")
    .WithContext("Language: C#, Framework: .NET 8")
    .AddConstraint("Focus on security vulnerabilities")
    .AddConstraint("Suggest specific fixes, not just descriptions")
    .AddSection("Code to Review", codeBlock)
    .AddExample("Good review", exampleReview);

string prompt = composer.Build();
```

## Prompt Versioning

`PromptVersionManager` tracks prompt evolution with full history.

```csharp
var versions = new PromptVersionManager("classify-sentiment");

// Save a new version
versions.Save(templateText, metadata: new { author = "team-ml" });

// Compare versions
var diff = versions.Diff(fromVersion: 2, toVersion: 3);

// Rollback
versions.Rollback(toVersion: 2);
```

## A/B Testing

`PromptABTester` runs controlled experiments across prompt variants.

```csharp
var tester = new PromptABTester();
tester.AddVariant("concise", conciseTemplate);
tester.AddVariant("detailed", detailedTemplate);
tester.AddVariant("structured", structuredTemplate);

// Route traffic
var variant = tester.SelectVariant();  // Weighted random selection

// Record outcome
tester.RecordResult(variant.Name, score: 0.92);

// Analyze results
var analysis = tester.Analyze();
Console.WriteLine($"Winner: {analysis.Winner} (p={analysis.PValue:F4})");
```

## Prompt Routing

`PromptRouter` directs prompts to different templates based on content.

```csharp
var router = new PromptRouter();
router.AddRoute("code", @"\b(function|class|def|var)\b", codeReviewTemplate);
router.AddRoute("math", @"\b(calculate|equation|formula)\b", mathTemplate);
router.AddRoute("creative", @"\b(write|story|poem)\b", creativeTemplate);
router.SetDefault(generalTemplate);

// Automatically select the best template
var matched = router.Route(userInput);
```

## Localization

`PromptLocalizer` manages prompt translations for multilingual applications.

```csharp
var localizer = new PromptLocalizer(defaultLocale: "en");
localizer.Add("en", "greeting", "Hello! How can I help you?");
localizer.Add("es", "greeting", "\u00a1Hola! \u00bfC\u00f3mo puedo ayudarte?");
localizer.Add("ja", "greeting", "\u3053\u3093\u306b\u3061\u306f\uff01");

string prompt = localizer.Get("greeting", locale: "es");
```

## See Also

- [Getting Started](getting-started.md) \u2014 Basic setup and first prompt
- [Templates](templates.md) \u2014 Template syntax and variable substitution
- [Prompt Chains](chains.md) \u2014 Multi-step prompt workflows
- [Prompt Safety](safety.md) \u2014 Input validation and output filtering
