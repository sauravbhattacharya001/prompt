# Observability & Debugging

This guide covers the modules that help you understand, debug, and monitor
prompt behavior in development and production: **PromptDebugger**,
**PromptReplayRecorder**, **PromptPerformanceProfiler**, **PromptAnalytics**,
and **PromptAuditLog**.

---

## Prompt Debugger

`PromptDebugger` performs deep structural analysis of prompts — detecting
anti-patterns, measuring clarity, identifying components, and suggesting
specific improvements.

### Quick Analysis

```csharp
using Prompt;

var report = PromptDebugger.Analyze("Tell me about dogs");

Console.WriteLine($"Clarity: {report.ClarityScore}/100");
Console.WriteLine($"Word count: {report.WordCount}");
Console.WriteLine($"Estimated tokens: {report.EstimatedTokens}");

foreach (var issue in report.Issues)
    Console.WriteLine($"[{issue.Severity}] {issue.Id}: {issue.Message}");

foreach (var fix in report.SuggestedFixes)
    Console.WriteLine($"Suggested fix: {fix}");
```

### Conversation Analysis

Analyze multi-turn conversations for structural issues like role imbalance,
excessive context, or missing system prompts:

```csharp
var messages = new[]
{
    new DebugChatMessage("system", "You are a helpful assistant."),
    new DebugChatMessage("user", "Summarize this article."),
    new DebugChatMessage("assistant", "Here's a summary..."),
    new DebugChatMessage("user", "Make it shorter.")
};

var report = PromptDebugger.AnalyzeConversation(messages);
// Checks for: role balance, context window usage, contradictions, etc.
```

### Common Anti-Patterns Detected

| ID | Description |
|---|---|
| AP001 | Overly broad scope ("do everything") |
| AP002 | Vague quality instructions ("make it good") |
| AP003 | Contradictory instructions |
| AP004 | Missing output format specification |
| AP005 | Prompt injection indicators |

---

## Replay Recorder

`PromptReplayRecorder` captures prompt interactions (request + response +
metadata) for later replay, regression testing, and analysis.

### Recording Interactions

```csharp
using Prompt;

var recorder = new PromptReplayRecorder();

// Record an interaction
var recording = new RecordedInteraction
{
    Prompt = "Explain quantum computing",
    SystemPrompt = "You are a physics teacher.",
    Response = "Quantum computing uses qubits...",
    Model = "gpt-4",
    LatencyMs = 1200,
    InputTokens = 15,
    OutputTokens = 150,
    EstimatedCostUsd = 0.003
};

recorder.Record(recording);
```

### Exporting & Importing Sessions

```csharp
// Export to JSON for storage/sharing
string json = recorder.ExportJson();
File.WriteAllText("session-recording.json", json);

// Import a previous session
var imported = PromptReplayRecorder.ImportJson(json);
foreach (var interaction in imported.Interactions)
{
    Console.WriteLine($"[{interaction.Timestamp}] {interaction.Prompt[..50]}...");
    Console.WriteLine($"  Latency: {interaction.LatencyMs}ms, Cost: ${interaction.EstimatedCostUsd:F4}");
}
```

### Replay for Regression Testing

Use recorded sessions to verify that prompt changes don't degrade output
quality:

```csharp
// Compare current model output against recorded "golden" responses
foreach (var golden in recorder.Interactions)
{
    string currentResponse = await Main.GetResponseAsync(
        golden.Prompt, systemPrompt: golden.SystemPrompt);

    // Compare using your preferred evaluation method
    var similarity = PromptSimilarityAnalyzer.Compare(golden.Response, currentResponse);
    Console.WriteLine($"Similarity: {similarity.Score:P0}");
}
```

---

## Performance Profiler

`PromptPerformanceProfiler` collects execution samples and produces
statistical summaries — latency percentiles, token throughput, cost
tracking, and trend detection.

### Collecting Samples

```csharp
using Prompt;

var profiler = new PromptPerformanceProfiler();

// Record samples (typically in a loop or middleware)
var sw = Stopwatch.StartNew();
string response = await Main.GetResponseAsync("Summarize: ...");
sw.Stop();

profiler.Record(new ProfileSample(
    label: "summarize-v2",
    duration: sw.Elapsed,
    inputTokens: 500,
    outputTokens: 120,
    success: true
));
```

### Generating Reports

```csharp
var report = profiler.GetReport("summarize-v2");

Console.WriteLine($"Samples:   {report.SampleCount}");
Console.WriteLine($"P50:       {report.P50Latency.TotalMilliseconds}ms");
Console.WriteLine($"P95:       {report.P95Latency.TotalMilliseconds}ms");
Console.WriteLine($"P99:       {report.P99Latency.TotalMilliseconds}ms");
Console.WriteLine($"Avg tokens: {report.AvgOutputTokens}");
Console.WriteLine($"Success:   {report.SuccessRate:P0}");
```

### Comparing Variants

Profile multiple prompt variants side-by-side to find the best
cost/quality tradeoff:

```csharp
// After profiling both variants...
var reportA = profiler.GetReport("summarize-v1");
var reportB = profiler.GetReport("summarize-v2");

Console.WriteLine($"V1 P50: {reportA.P50Latency.TotalMilliseconds}ms " +
                  $"vs V2 P50: {reportB.P50Latency.TotalMilliseconds}ms");
```

---

## Analytics

`PromptAnalytics` tracks usage patterns across your prompt library —
call frequency, token consumption, cost breakdowns, and anomaly
detection.

### Tracking Events

```csharp
using Prompt;

var analytics = new PromptAnalytics();

// Track each prompt call
analytics.Track(new AnalyticsEvent
{
    PromptName = "customer-support-v3",
    InputTokens = 800,
    OutputTokens = 200,
    LatencyMs = 950,
    Model = "gpt-4",
    Success = true
});
```

### Usage Reports

```csharp
var summary = analytics.GetSummary(
    from: DateTime.UtcNow.AddDays(-7),
    to: DateTime.UtcNow);

Console.WriteLine($"Total calls:  {summary.TotalCalls}");
Console.WriteLine($"Total tokens: {summary.TotalTokens}");
Console.WriteLine($"Total cost:   ${summary.EstimatedCostUsd:F2}");
Console.WriteLine($"Error rate:   {summary.ErrorRate:P1}");

// Per-prompt breakdown
foreach (var prompt in summary.ByPrompt.OrderByDescending(p => p.Value.CallCount))
    Console.WriteLine($"  {prompt.Key}: {prompt.Value.CallCount} calls, " +
                      $"${prompt.Value.EstimatedCostUsd:F2}");
```

---

## Audit Log

`PromptAuditLog` provides an append-only log of prompt interactions for
compliance, security review, and troubleshooting. Unlike the Replay
Recorder, audit logs are designed for production compliance rather than
testing workflows.

### Logging Interactions

```csharp
using Prompt;

var auditLog = new PromptAuditLog();

auditLog.Log(new AuditEntry
{
    UserId = "user-123",
    PromptName = "pii-redaction",
    InputHash = ComputeHash(promptText),  // Don't log raw PII
    OutputTokens = 50,
    Model = "gpt-4",
    Outcome = AuditOutcome.Success,
    PolicyFlags = new[] { "pii-detected", "redacted" }
});
```

### Querying the Log

```csharp
// Find all failed interactions for a specific user
var failures = auditLog.Query(
    userId: "user-123",
    outcome: AuditOutcome.Failure,
    from: DateTime.UtcNow.AddHours(-24));

foreach (var entry in failures)
    Console.WriteLine($"[{entry.Timestamp}] {entry.PromptName}: {entry.ErrorMessage}");
```

---

## Putting It Together

A typical production setup combines these modules in a pipeline:

```csharp
var pipeline = new PromptPipeline()
    .Use(async (ctx, next) =>
    {
        // 1. Debug analysis (development only)
        if (isDev)
        {
            var debug = PromptDebugger.Analyze(ctx.PromptText);
            if (debug.ClarityScore < 50)
                logger.Warn($"Low clarity prompt: {debug.ClarityScore}/100");
        }

        // 2. Profile execution
        var sw = Stopwatch.StartNew();
        await next(ctx);
        sw.Stop();

        profiler.Record(new ProfileSample(
            ctx.PromptName, sw.Elapsed,
            ctx.InputTokens, ctx.OutputTokens));

        // 3. Track analytics
        analytics.Track(new AnalyticsEvent
        {
            PromptName = ctx.PromptName,
            InputTokens = ctx.InputTokens ?? 0,
            OutputTokens = ctx.OutputTokens ?? 0,
            LatencyMs = sw.ElapsedMilliseconds,
            Success = ctx.Error == null
        });

        // 4. Audit log
        auditLog.Log(new AuditEntry
        {
            PromptName = ctx.PromptName,
            Outcome = ctx.Error == null
                ? AuditOutcome.Success
                : AuditOutcome.Failure
        });

        // 5. Record for replay (staging/testing)
        if (isStaging)
        {
            recorder.Record(new RecordedInteraction
            {
                Prompt = ctx.PromptText,
                Response = ctx.RenderedPrompt,
                LatencyMs = sw.ElapsedMilliseconds
            });
        }
    });
```

## Next Steps

- [Testing & Quality Assurance](testing.md) — Automated prompt testing with golden datasets
- [Production Features](production-features.md) — Batching, fallbacks, and rate limiting
- [Security Hardening](security-hardening.md) — Guarding against prompt injection
