# Prompt Templates

The `PromptTemplate` class lets you create reusable prompts with `{{variable}}` placeholders. Variables are filled at render time, with support for defaults, strict validation, and template composition.

## Creating a Template

```csharp
using Prompt;

// Simple template
var template = new PromptTemplate(
    "Explain {{topic}} to a {{audience}} in {{style}} style."
);

// With default values
var template = new PromptTemplate(
    "You are a {{role}} assistant. Help the user with {{topic}}.",
    new Dictionary<string, string>
    {
        ["role"] = "helpful",
        ["style"] = "concise"
    }
);
```

## Rendering

Replace placeholders with values:

```csharp
var template = new PromptTemplate(
    "Translate '{{text}}' from {{source}} to {{target}}.",
    new Dictionary<string, string> { ["source"] = "English" }
);

string prompt = template.Render(new Dictionary<string, string>
{
    ["text"] = "Good morning",
    ["target"] = "Japanese"
});
// → "Translate 'Good morning' from English to Japanese."
```

### Strict vs Non-Strict Mode

By default, rendering throws if a required variable is missing:

```csharp
// Strict mode (default) — throws InvalidOperationException
template.Render(new Dictionary<string, string>
{
    ["text"] = "Hello"
    // Missing "target" → exception
});

// Non-strict mode — leaves unresolved variables as-is
string result = template.Render(
    new Dictionary<string, string> { ["text"] = "Hello" },
    strict: false
);
// → "Translate 'Hello' from English to {{target}}."
```

## Variable Introspection

Discover what variables a template needs:

```csharp
var template = new PromptTemplate(
    "Summarize {{text}} in {{language}} for a {{audience}} audience."
);

HashSet<string> allVars = template.GetVariables();
// { "text", "language", "audience" }

HashSet<string> required = template.GetRequiredVariables();
// { "text", "language", "audience" } (none have defaults)
```

## Managing Defaults

```csharp
var template = new PromptTemplate("Explain {{topic}} in {{style}} style.");

// Add a default
template.SetDefault("style", "concise");

// Remove a default (makes it required again)
template.RemoveDefault("style");

// Read defaults
IReadOnlyDictionary<string, string> defaults = template.Defaults;
```

## Render and Send

Render the template and send directly to Azure OpenAI in one call:

```csharp
// One-shot: render + send via Main.GetResponseAsync
string? response = await template.RenderAndSendAsync(
    new Dictionary<string, string> { ["topic"] = "recursion" },
    systemPrompt: "You are a CS professor."
);

// With custom options
var opts = PromptOptions.ForCodeGeneration();
string? code = await template.RenderAndSendAsync(
    new Dictionary<string, string> { ["topic"] = "merge sort in C#" },
    options: opts
);

// Within a Conversation
var conv = new Conversation("You are a tutor.");
string? reply = await template.RenderAndSendAsync(
    conv,
    new Dictionary<string, string> { ["topic"] = "closures" }
);
```

## Composition

Combine templates by concatenating them with merged defaults:

```csharp
var context = new PromptTemplate(
    "Context: The user is working on a {{language}} project.",
    new Dictionary<string, string> { ["language"] = "C#" }
);

var task = new PromptTemplate(
    "Task: {{instruction}}\n\nRespond with clean, tested code."
);

// Compose: context + task
PromptTemplate combined = context.Compose(task);

string prompt = combined.Render(new Dictionary<string, string>
{
    ["instruction"] = "Write a binary search function"
});
// → "Context: The user is working on a C# project.\n\nTask: Write a binary search function\n\nRespond with clean, tested code."
```

The `Compose` method concatenates templates with a separator (default: `\n\n`) and merges defaults — the second template's defaults take precedence on conflicts.

## Serialization

Save and load templates as JSON for reuse:

```csharp
// To JSON string
string json = template.ToJson();
// {
//   "template": "Explain {{topic}} in {{style}} style.",
//   "defaults": { "style": "concise" }
// }

// From JSON string
var loaded = PromptTemplate.FromJson(json);

// File-based
await template.SaveToFileAsync("templates/summarize.json");
var restored = await PromptTemplate.LoadFromFileAsync("templates/summarize.json");
```

## Building a Template Library

A practical pattern for organizing reusable templates:

```csharp
public static class Templates
{
    public static PromptTemplate Summarize => new(
        "Summarize the following text in {{sentences}} sentences:\n\n{{text}}",
        new Dictionary<string, string> { ["sentences"] = "3" }
    );

    public static PromptTemplate CodeReview => new(
        "Review this {{language}} code for bugs, performance, and style:\n\n```{{language}}\n{{code}}\n```",
        new Dictionary<string, string> { ["language"] = "C#" }
    );

    public static PromptTemplate Translate => new(
        "Translate the following from {{source}} to {{target}}:\n\n{{text}}",
        new Dictionary<string, string> { ["source"] = "English" }
    );
}
```
