# Autonomous Prompt Optimization

Prompt includes a suite of autonomous engines that improve prompts without manual intervention. These systems cover the full optimization lifecycle — from single-pass rule-based fixes to multi-generation genetic evolution.

## Overview

| Engine | Purpose | Approach |
|--------|---------|----------|
| [`PromptAutoImprover`](#promptautoimprover) | One-shot prompt rewriting | Multi-pass rule analysis across 9 categories |
| [`PromptAutopilot`](#promptautopilot) | Iterative autonomous refinement | Diagnose → fix → score loops with strategy selection |
| [`PromptEvolutionEngine`](#promptevolutionenergy) | Genetic algorithm optimization | Population-based evolution with crossover and mutation |
| [`PromptFeedbackLoop`](#promptfeedbackloop) | Feedback-driven improvement | Collects output feedback, detects patterns, suggests fixes |

---

## PromptAutoImprover

Takes a raw prompt and produces concrete improved versions through multiple analysis passes. Each pass identifies issues in a specific category, rewrites the prompt, and explains what changed.

### Quick Start

```csharp
using Prompt;

var improver = new PromptAutoImprover();
var result = improver.Improve("Tell me about dogs", new ImprovementConfig
{
    Intensity = ImprovementIntensity.Moderate,
    MaxTokenBudget = 500
});

Console.WriteLine($"Before: {result.OriginalScore:F2}");
Console.WriteLine($"After:  {result.ImprovedScore:F2}");
Console.WriteLine($"Improved prompt:\n{result.ImprovedText}");

foreach (var pass in result.Passes)
{
    Console.WriteLine($"  [{pass.Category}] {pass.Explanation}");
}
```

### Improvement Categories

| Category | What it fixes |
|----------|---------------|
| `Specificity` | Replaces vague language with precise instructions |
| `Clarity` | Simplifies wording for better model comprehension |
| `Structure` | Adds missing role, format, and constraint elements |
| `TokenEfficiency` | Reduces token count without losing meaning |
| `Completeness` | Adds missing instructions the model likely needs |
| `AntiPattern` | Removes known patterns that degrade output quality |
| `FormatSpec` | Improves output format specification |
| `ExampleQuality` | Adds or improves few-shot examples |
| `SafetyGuardrail` | Strengthens guardrails and safety constraints |

### Intensity Levels

- **Light** — Conservative fixes for clear issues only
- **Moderate** — Balanced improvements across categories (default)
- **Deep** — Aggressive rewriting for maximum quality gain

### Configuration

```csharp
var config = new ImprovementConfig
{
    Intensity = ImprovementIntensity.Deep,
    MaxTokenBudget = 1000,           // Upper bound on improved prompt length
    FocusCategories = new[]          // Limit to specific categories (optional)
    {
        ImprovementCategory.Specificity,
        ImprovementCategory.Structure
    }
};
```

### HTML Reports

The engine can generate a self-contained HTML report showing before/after scores, per-pass changes, and quality breakdowns:

```csharp
string html = result.ToHtml();
File.WriteAllText("improvement-report.html", html);
```

---

## PromptAutopilot

An iterative refinement engine that runs a diagnose → fix → score loop until the prompt meets a target quality bar or the generation budget is exhausted.

### Quick Start

```csharp
using Prompt;

var autopilot = new PromptAutopilot();
var result = autopilot.Run("Summarize this article", new AutopilotConfig
{
    Strategy = AutopilotStrategy.WorstFirst,
    MaxGenerations = 10,
    TargetScore = 0.85
});

Console.WriteLine($"Generations: {result.Generations}");
Console.WriteLine($"Final score: {result.FinalScore:F2}");
Console.WriteLine($"Final prompt:\n{result.FinalText}");
```

### Strategies

| Strategy | Behavior |
|----------|----------|
| `WorstFirst` | Fix the most severe issue first each generation |
| `Shotgun` | Fix all issues in a single pass |
| `Rotating` | Focus on one quality dimension per generation (cycling through) |
| `Conservative` | Apply only high-confidence, low-risk fixes |
| `Aggressive` | Aggressive rewriting targeting maximum score gain |

### Key Classes

- **`AutopilotDiagnosis`** — A single finding from analysis (category, severity, description, suggested fix)
- **`AutopilotGeneration`** — Snapshot of one refinement iteration (prompt text, score, fixes applied)
- **`AutopilotResult`** — Full history of the refinement run with before/after comparison

---

## PromptEvolutionEngine

A genetic-algorithm optimizer that evolves a population of prompt variants over multiple generations. The engine applies crossover, mutation, and selection to discover high-fitness prompt formulations.

### Quick Start

```csharp
using Prompt;

// Seed population with variations of your prompt
var seeds = new[]
{
    "Explain quantum computing in simple terms.",
    "Describe quantum computing for a beginner.",
    "What is quantum computing? Keep it simple."
};

var engine = new PromptEvolutionEngine();
var result = engine.Evolve(seeds, new EvolutionConfig
{
    PopulationSize = 20,
    MaxGenerations = 50,
    MutationRate = 0.15,
    CrossoverRate = 0.7,
    EliteCount = 2,
    FitnessTargetThreshold = 0.95
});

Console.WriteLine($"Best: {result.BestOrganism.Text}");
Console.WriteLine($"Fitness: {result.BestOrganism.Fitness:F3}");
Console.WriteLine($"Generations: {result.TotalGenerations}");
```

### Mutation Strategies

| Strategy | Effect |
|----------|--------|
| `WordShuffle` | Randomly reorder words within a sentence |
| `SynonymReplace` | Replace common words with synonyms |
| `SentenceReorder` | Reorder sentences in the prompt |
| `ToneShift` | Shift formality level |
| `Compress` | Remove filler words and redundancy |
| `Expand` | Add clarifying or emphatic phrases |
| `InstructionRephrase` | Rephrase imperative sentences with different verb forms |

### Crossover Methods

| Method | Behavior |
|--------|----------|
| `SinglePoint` | Split at midpoint sentence boundary |
| `Uniform` | Randomly pick sentences from each parent |
| `SemanticBlend` | Interleave sentences alternating parents |

### Selection Methods

| Method | Behavior |
|--------|----------|
| `Tournament` | Tournament selection among random candidates (default) |
| `RouletteWheel` | Probability proportional to fitness |
| `RankBased` | Rank-based probability with linear ranking |
| `Elitism` | Top-N organisms pass through unchanged |

### Configuration

```csharp
var config = new EvolutionConfig
{
    PopulationSize = 30,          // Organisms per generation (min 4)
    MaxGenerations = 100,         // Max generations to run
    MutationRate = 0.2,           // Base mutation probability per offspring
    CrossoverRate = 0.8,          // Crossover probability per pair
    EliteCount = 3,               // Top organisms that pass unchanged
    Selection = SelectionMethod.Tournament,
    TournamentSize = 4,           // Candidates per tournament
    AdaptiveMutationRate = true,  // Auto-adjust mutation to diversity
    FitnessTargetThreshold = 0.95 // Early-stop fitness target
};
```

### Key Classes

- **`PromptOrganism`** — An individual prompt variant with text, fitness score, generation, parentage, and mutation history
- **`GenerationStats`** — Per-generation statistics (best/avg/worst fitness, diversity, stagnation count)
- **`EvolutionResult`** — Full evolution run output with best organism, history, population, and summary

---

## PromptFeedbackLoop

Collects structured feedback on prompt outputs, detects recurring issue patterns through keyword frequency and category analysis, and autonomously suggests (or auto-applies) prompt refinements.

### Quick Start

```csharp
using Prompt;

var loop = new PromptFeedbackLoop();

// Record feedback on prompt outputs
loop.AddFeedback(new PromptFeedback
{
    PromptId = "summarizer-v1",
    Rating = FeedbackRating.Poor,
    Category = "accuracy",
    Comment = "The summary missed key financial figures"
});

loop.AddFeedback(new PromptFeedback
{
    PromptId = "summarizer-v1",
    Rating = FeedbackRating.Acceptable,
    Category = "accuracy",
    Comment = "Numbers were approximate but acceptable"
});

// Get analysis and suggestions
var analysis = loop.Analyze("summarizer-v1");
Console.WriteLine($"Health score: {analysis.HealthScore:F2}");

foreach (var suggestion in analysis.Suggestions)
{
    Console.WriteLine($"  [{suggestion.Category}] {suggestion.Description}");
}
```

### Feedback Ratings

| Rating | Value | Meaning |
|--------|-------|---------|
| `Excellent` | 5 | Outstanding output |
| `Good` | 4 | Good with minor issues |
| `Acceptable` | 3 | Meets minimum bar |
| `Poor` | 2 | Below expectations |
| `Terrible` | 1 | Unacceptable output |

### Features

- **Pattern detection** — Identifies recurring issues through keyword frequency and category analysis
- **Health scoring** — Aggregate quality score based on feedback distribution
- **Timeline analysis** — Tracks how prompt quality trends over time
- **Category heatmaps** — Visualize which issue categories are most common
- **Proactive insights** — Automatically surfaces emerging problems before they become patterns
- **Multi-format export** — Export analysis as JSON, HTML, or Markdown

---

## Combining the Engines

These tools work well together in a prompt optimization pipeline:

```csharp
// 1. Start with AutoImprover for quick structural fixes
var improver = new PromptAutoImprover();
var improved = improver.Improve(rawPrompt, new ImprovementConfig
{
    Intensity = ImprovementIntensity.Moderate
});

// 2. Feed improved versions into the Evolution Engine for exploration
var engine = new PromptEvolutionEngine();
var evolved = engine.Evolve(
    new[] { improved.ImprovedText, rawPrompt },
    new EvolutionConfig { MaxGenerations = 30 }
);

// 3. Use Autopilot for final polishing
var autopilot = new PromptAutopilot();
var final = autopilot.Run(evolved.BestOrganism.Text, new AutopilotConfig
{
    Strategy = AutopilotStrategy.Conservative,
    TargetScore = 0.9
});

// 4. Deploy and collect feedback
var loop = new PromptFeedbackLoop();
// ... record production feedback, analyze, iterate
```

## See Also

- [Advanced Features](advanced-features.md) — Caching, versioning, A/B testing
- [Production Features](production-features.md) — Batch processing, sanitization, linting
- [Safety](safety.md) — Injection detection, PII scanning, content filtering
- [Testing](testing.md) — Prompt contract testing and coverage analysis
