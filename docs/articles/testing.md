# Testing & Quality Assurance

This guide covers the Prompt library's testing, debugging, and quality
assurance tools. These features help you systematically evaluate prompt
effectiveness, catch regressions, and validate response quality — all
without requiring LLM calls.

## Prompt Debugger

`PromptDebugger` performs deep structural analysis of your prompts. It
detects anti-patterns, identifies components (instructions, constraints,
examples), measures clarity, and suggests specific improvements.

```csharp
var report = PromptDebugger.Analyze(
    "Tell me about dogs. Make it good. Cover everything."
);

Console.WriteLine($"Clarity: {report.ClarityScore}/100");
// Clarity: 35/100

foreach (var issue in report.Issues)
    Console.WriteLine($"[{issue.Severity}] {issue.Id}: {issue.Message}");
// [Warning] AP001: Overly broad scope — asking the model to do 'everything'...
// [Warning] AP002: Vague quality instruction — 'make it good' gives no criteria...

foreach (var fix in report.SuggestedFixes)
    Console.WriteLine($"→ {fix}");
// → Break the task into specific sub-tasks or focus on one aspect
// → Specify what 'good' means: e.g., 'use active voice, keep under 20 words'
```

### Conversation Analysis

Analyze multi-turn conversations to catch issues across the full message
history:

```csharp
var messages = new[]
{
    new DebugChatMessage("system", "You are a helpful assistant."),
    new DebugChatMessage("user", "Summarize this article."),
    new DebugChatMessage("assistant", "Sure! Here's a summary..."),
    new DebugChatMessage("user", "Make it better.")
};

var report = PromptDebugger.AnalyzeConversation(messages);
// Detects vague follow-ups, missing context, contradictions across turns
```

### What It Detects

| Anti-Pattern | Example | Severity |
|---|---|---|
| Overly broad scope | "handle all edge cases" | Warning |
| Vague quality | "make it nice" | Warning |
| Contradictory instructions | "don't use jargon, but also be technical" | Error |
| Instruction stacking | "also... additionally... furthermore..." | Warning |
| Missing context | Single-word prompts | Info |
| Repetition | Same instruction repeated | Warning |

---

## Prompt Test Suite

`PromptTestSuite` provides a structured testing framework for prompts.
Define test cases with assertions, run them against responses, and get
detailed pass/fail results.

### Defining Tests

```csharp
var suite = new PromptTestSuite("Customer Service Bot Tests");

// Add a test case with assertions
suite.AddTest(new PromptTestCase
{
    Name = "Greeting Response",
    Prompt = "Hello, I need help with my order",
    ExpectedAssertions = new List<TestAssertion>
    {
        new TestAssertion(AssertionType.Contains, "help"),
        new TestAssertion(AssertionType.HasMinLength, "20"),
        new TestAssertion(AssertionType.NotContains, "error"),
        new TestAssertion(AssertionType.MatchesRegex, @"\b(hi|hello|welcome)\b")
    }
});

suite.AddTest(new PromptTestCase
{
    Name = "JSON Format Check",
    Prompt = "Return order status as JSON",
    ExpectedAssertions = new List<TestAssertion>
    {
        new TestAssertion(AssertionType.ContainsJson, ""),
        new TestAssertion(AssertionType.HasMaxLength, "500")
    }
});
```

### Running Tests

```csharp
// Evaluate a response against a test case
string response = "Hello! I'd be happy to help you with your order.";
var result = suite.RunTest("Greeting Response", response);

Console.WriteLine($"Passed: {result.Passed}");
Console.WriteLine($"Assertions: {result.PassedCount}/{result.TotalCount}");

foreach (var assertion in result.AssertionResults)
    Console.WriteLine($"  {assertion.Type}: {(assertion.Passed ? "✓" : "✗")}");
```

### Assertion Types

| Type | Value Parameter | What It Checks |
|---|---|---|
| `Contains` | text | Response contains the text (case-insensitive) |
| `NotContains` | text | Response does not contain the text |
| `MatchesRegex` | pattern | Response matches the regex |
| `StartsWith` | prefix | Response starts with the prefix |
| `EndsWith` | suffix | Response ends with the suffix |
| `HasMinLength` | number | Response has at least N characters |
| `HasMaxLength` | number | Response has at most N characters |
| `ContainsJson` | *(unused)* | Response contains valid JSON |
| `ContainsCodeBlock` | *(unused)* | Response has a fenced code block |
| `ContainsAllOf` | "a,b,c" | Response contains all comma-separated values |

Any assertion can be negated:

```csharp
// This assertion passes when the response DOES contain "error"
new TestAssertion(AssertionType.Contains, "error", negate: true)
// → passes only when response does NOT contain "error"
```

### Serialization

Test suites can be serialized to JSON for storage, version control, or
sharing across teams:

```csharp
string json = suite.ToJson();
var loaded = PromptTestSuite.FromJson(json);
```

---

## Response Evaluator

`PromptResponseEvaluator` scores prompt-response pairs across multiple
quality dimensions. It's fully heuristic-based (no LLM calls needed)
and deterministic — the same input always produces the same score.

### Basic Evaluation

```csharp
var evaluator = new PromptResponseEvaluator();

var result = evaluator.Evaluate(
    prompt: "List 3 benefits of exercise",
    response: "1. Better cardiovascular health\n2. Increased energy levels\n3. Improved mood and mental clarity"
);

Console.WriteLine($"Score: {result.CompositeScore:F2}");  // ~0.92
Console.WriteLine($"Grade: {result.Grade}");               // A
```

### Quality Dimensions

Each dimension produces a 0.0–1.0 score:

| Dimension | What It Measures |
|---|---|
| **Relevance** | How well the response addresses the prompt keywords |
| **Completeness** | Whether the response covers all aspects of the request |
| **Conciseness** | Length efficiency — not too short, not padded |
| **Structure** | Use of formatting (lists, paragraphs, headings) |
| **Specificity** | Concrete details vs vague generalities |

### Custom Weights

Adjust dimension weights for your use case:

```csharp
var config = new EvaluatorConfig
{
    Weights = new Dictionary<string, double>
    {
        ["relevance"] = 2.0,      // Relevance matters most
        ["completeness"] = 1.5,
        ["conciseness"] = 0.5,    // We don't mind verbose answers
        ["structure"] = 1.0,
        ["specificity"] = 1.0
    }
};

var evaluator = new PromptResponseEvaluator(config);
```

### Batch Evaluation

Evaluate multiple prompt-response pairs for regression testing:

```csharp
var pairs = new[]
{
    ("Explain REST APIs", response1),
    ("Write a haiku about coding", response2),
    ("List 5 programming languages", response3)
};

foreach (var (prompt, response) in pairs)
{
    var result = evaluator.Evaluate(prompt, response);
    Console.WriteLine($"{result.Grade} ({result.CompositeScore:F2}): {prompt}");
}
```

---

## Grammar Validator

`PromptGrammarValidator` validates responses against structural rules.
Define expected formats, lengths, patterns, and content requirements —
then validate responses automatically.

### Defining Rules

```csharp
var validator = new PromptGrammarValidator();

// Response must be valid JSON
validator.AddRule(new GrammarRule
{
    Id = "json-format",
    Type = GrammarRuleType.JsonSchema,
    Severity = ViolationSeverity.Error
});

// Response must be 50–500 characters
validator.AddRule(new GrammarRule
{
    Id = "length-check",
    Type = GrammarRuleType.Length,
    Min = 50,
    Max = 500,
    Severity = ViolationSeverity.Warning
});

// Response must contain a specific section
validator.AddRule(new GrammarRule
{
    Id = "has-summary",
    Type = GrammarRuleType.Contains,
    Value = "Summary:",
    Severity = ViolationSeverity.Error
});

// Response must NOT contain filler phrases
validator.AddRule(new GrammarRule
{
    Id = "no-filler",
    Type = GrammarRuleType.NotContains,
    Value = "As an AI language model",
    Severity = ViolationSeverity.Warning
});
```

### Validating Responses

```csharp
var result = validator.Validate(response);

if (!result.IsValid)
{
    foreach (var violation in result.Violations)
        Console.WriteLine($"[{violation.Severity}] Rule '{violation.RuleId}': {violation.Message}");
}
```

### Rule Types

| Type | Purpose |
|---|---|
| `Regex` | Match a regex pattern |
| `JsonSchema` | Valid JSON structure |
| `Enum` | One of allowed values |
| `StartsWith` / `EndsWith` | Prefix/suffix matching |
| `Contains` / `NotContains` | Substring presence/absence |
| `Length` | Character count bounds |
| `LineCount` | Line count bounds |
| `Structure` | Section/bullet structure validation |
| `Custom` | Delegate-based custom logic |

---

## Prompt Fuzzer

`PromptFuzzer` generates variations of your prompts to test robustness.
It systematically applies mutations (typos, synonym swaps, case changes,
word drops) to discover how sensitive your prompt is to phrasing.

### Basic Fuzzing

```csharp
var fuzzer = new PromptFuzzer();

var result = fuzzer.Fuzz(
    "Summarize the following article in 3 bullet points",
    count: 5
);

Console.WriteLine($"Original: {result.Original}");
foreach (var variant in result.Variants)
{
    Console.WriteLine($"  [{variant.Strategy}] {variant.Text}");
    Console.WriteLine($"   Similarity: {variant.Similarity:P0}");
}
```

### Fuzzing Strategies

Combine strategies using flags:

```csharp
var result = fuzzer.Fuzz(prompt,
    strategies: FuzzStrategy.TypoInjection | FuzzStrategy.WordDrop,
    count: 10
);
```

| Strategy | What It Does |
|---|---|
| `SynonymSwap` | Replaces words with synonyms |
| `TypoInjection` | Introduces realistic typos |
| `CaseChange` | Randomizes letter casing |
| `WordDrop` | Removes random words |
| `WordShuffle` | Swaps adjacent words |
| `NoiseInjection` | Adds filler words or whitespace |
| `Truncation` | Truncates at various points |
| `All` | Applies all strategies |

### Robustness Testing Workflow

Combine fuzzing with the test suite for automated robustness checks:

```csharp
var fuzzer = new PromptFuzzer();
var suite = new PromptTestSuite("Robustness Tests");

// Original prompt
string prompt = "Extract the email addresses from this text";

// Generate 20 variations
var fuzzed = fuzzer.Fuzz(prompt, count: 20, strategies: FuzzStrategy.All);

// Test each variation against your assertions
foreach (var variant in fuzzed.Variants)
{
    string response = await GetModelResponse(variant.Text);
    var testResult = suite.RunTest("Email Extraction", response);

    if (!testResult.Passed)
    {
        Console.WriteLine($"FAILED with variant: {variant.Text}");
        Console.WriteLine($"Strategy: {variant.Strategy}");
        Console.WriteLine($"Similarity: {variant.Similarity:P0}");
    }
}
```

---

## Putting It All Together

These tools compose into a complete prompt QA pipeline:

```csharp
// 1. Debug the prompt structure first
var debugReport = PromptDebugger.Analyze(myPrompt);
if (debugReport.ClarityScore < 50)
    Console.WriteLine("⚠️ Prompt clarity is low — review suggested fixes");

// 2. Define quality expectations
var suite = new PromptTestSuite("Production Tests");
suite.AddTest(new PromptTestCase
{
    Name = "Format Check",
    Prompt = myPrompt,
    ExpectedAssertions = new List<TestAssertion>
    {
        new TestAssertion(AssertionType.ContainsJson, ""),
        new TestAssertion(AssertionType.HasMinLength, "100"),
        new TestAssertion(AssertionType.NotContains, "I'm sorry")
    }
});

// 3. Validate response grammar
var validator = new PromptGrammarValidator();
validator.AddRule(new GrammarRule
{
    Id = "valid-json", Type = GrammarRuleType.JsonSchema,
    Severity = ViolationSeverity.Error
});

// 4. Evaluate response quality
var evaluator = new PromptResponseEvaluator();

// 5. Fuzz for robustness
var fuzzer = new PromptFuzzer();
var variants = fuzzer.Fuzz(myPrompt, count: 10);

// Run the full pipeline
foreach (var variant in variants.Variants)
{
    string response = await GetModelResponse(variant.Text);

    var testResult = suite.RunTest("Format Check", response);
    var grammarResult = validator.Validate(response);
    var evalResult = evaluator.Evaluate(variant.Text, response);

    Console.WriteLine($"Variant ({variant.Strategy}): " +
        $"Test={testResult.Passed}, Grammar={grammarResult.IsValid}, " +
        $"Quality={evalResult.Grade}");
}
```

This pipeline gives you confidence that your prompts are:

- **Well-structured** (Debugger catches anti-patterns)
- **Producing correct output** (TestSuite validates assertions)
- **Following format rules** (GrammarValidator enforces structure)
- **High quality** (ResponseEvaluator scores dimensions)
- **Robust to variation** (Fuzzer tests edge cases)
