# Caching & Performance

This guide covers the Prompt library's caching, rate limiting, performance
profiling, and caching optimisation tools. Together they help you reduce
latency, control costs, and understand where time and tokens go.

## Prompt Cache

`PromptCache` is an in-memory LRU response cache with optional TTL. It
stores responses keyed on (prompt + model) so identical requests are served
instantly without an LLM call.

### Basic Usage

```csharp
// 512-entry cache, entries expire after 1 hour
var cache = new PromptCache(capacity: 512, defaultTtl: TimeSpan.FromHours(1));

// Cache a response
cache.Put("Explain photosynthesis.", "Photosynthesis is...", model: "gpt-4");

// Later — instant hit
CacheEntry? entry = cache.Get("Explain photosynthesis.", model: "gpt-4");
if (entry != null)
    Console.WriteLine(entry.Response);
```

### Key Generation

Cache keys are deterministic SHA-256 hashes of the prompt (and model when
specified). You can inspect or precompute them:

```csharp
string key = PromptCache.ComputeKey("What is gravity?", model: "gpt-4");
// "a1b2c3..." — stable across process restarts
```

### Metadata & TTL

Attach arbitrary metadata and per-entry TTL overrides:

```csharp
cache.Put(
    prompt:   "Summarize the report.",
    response: "Revenue grew 12%...",
    model:    "gpt-4",
    metadata: new Dictionary<string, string> { ["source"] = "Q4-report.pdf" },
    ttl:      TimeSpan.FromMinutes(30)   // override default TTL
);
```

### Cache Statistics

Monitor hit rates to decide whether your cache size is adequate:

```csharp
CacheStats stats = cache.GetStats();
Console.WriteLine($"Hits: {stats.Hits}, Misses: {stats.Misses}");
Console.WriteLine($"Hit rate: {stats.HitRate:P1}");
Console.WriteLine($"Entries: {stats.Count}/{stats.Capacity}");
```

### Persistence

Save and restore the cache across process restarts:

```csharp
await cache.SaveToFileAsync("prompt-cache.json");

// On next startup
var restored = await PromptCache.LoadFromFileAsync(
    "prompt-cache.json",
    capacity: 512,
    defaultTtl: TimeSpan.FromHours(1)
);
```

### Eviction & Invalidation

```csharp
// Remove a specific entry
cache.Remove("Explain photosynthesis.", model: "gpt-4");

// Clear everything
cache.Clear();

// LRU eviction is automatic when capacity is exceeded
```

---

## Caching Optimizer

`PromptCachingOptimizer` analyses a set of prompts to identify which
segments are cacheable and how much you can save. Use it to restructure
prompts for maximum cache reuse — especially with API-level prompt caching
(e.g. Anthropic's cache_control).

```csharp
var optimizer = new PromptCachingOptimizer();

CachingAnalysis analysis = optimizer.Analyze(
    "You are a legal assistant. ...",
    "You are a legal assistant. Summarize this contract: ..."
);

Console.WriteLine($"Total tokens: {analysis.TotalTokens}");
Console.WriteLine($"Cacheable: {analysis.CacheableTokens} ({analysis.EstimatedSavingsPercent:F1}%)");
Console.WriteLine($"Efficiency: {analysis.CacheEfficiencyScore:F2}");

foreach (var seg in analysis.Segments)
    Console.WriteLine($"  [{seg.Type}] {seg.EstimatedTokens} tokens — {seg.Recommendation}");

foreach (var prefix in analysis.CommonPrefixes)
    Console.WriteLine($"  Shared prefix ({prefix.PrefixTokens} tokens) across {prefix.PromptIndices.Count} prompts");
```

### Segment Types

| Type | Meaning |
|------|---------|
| `SystemInstruction` | System prompt — highly cacheable (shared across turns) |
| `FewShotExample` | Few-shot examples — cacheable if reused |
| `DynamicContent` | User-specific input — rarely cacheable |
| `Template` | Template boilerplate — cacheable prefix |

---

## Rate Limiter

`PromptRateLimiter` enforces per-profile request and token rate limits with
concurrency control. Use it to stay within API quotas and prevent runaway
costs.

### Defining Profiles

Create profiles for different API tiers, models, or teams:

```csharp
var limiter = PromptRateLimiter.WithDefaults();

limiter.AddProfile(new RateLimitProfile
{
    Name = "gpt-4",
    RequestsPerMinute = 60,
    TokensPerMinute = 90_000,
    MaxConcurrent = 10,
    Priority = 5
});

limiter.AddProfile(new RateLimitProfile
{
    Name = "gpt-4-bulk",
    RequestsPerMinute = 20,
    TokensPerMinute = 150_000,
    MaxConcurrent = 5,
    Priority = 1  // lower priority
});
```

### Acquiring & Releasing

```csharp
// Synchronous check
RateLimitResult result = limiter.TryAcquire("gpt-4", estimatedTokens: 500);

if (result.Permitted)
{
    try
    {
        // ... call LLM ...
        limiter.RecordCompletion("gpt-4", actualTokens: 480,
                                acquireTimestamp: result.AcquireTimestamp);
    }
    catch
    {
        limiter.RecordCompletion("gpt-4", acquireTimestamp: result.AcquireTimestamp);
        throw;
    }
}
else
{
    Console.WriteLine($"Denied: {result.DenialReason}. Wait {result.WaitMs}ms.");
}
```

### Async Wait-and-Acquire

For batch pipelines, wait for a slot instead of polling:

```csharp
RateLimitResult slot = await limiter.WaitAndAcquireAsync(
    "gpt-4",
    estimatedTokens: 1000,
    timeout: TimeSpan.FromSeconds(30)
);
```

### Usage Monitoring

```csharp
RateLimitUsage? usage = limiter.GetUsage("gpt-4");
Console.WriteLine($"Window: {usage.WindowRequests}/{usage.RequestsPerMinuteLimit} RPM");
Console.WriteLine($"Tokens: {usage.WindowTokens}/{usage.TokensPerMinuteLimit} TPM");
Console.WriteLine($"Concurrent: {usage.ConcurrentRequests}/{usage.MaxConcurrentLimit}");
Console.WriteLine($"Request utilization: {usage.RequestUtilization:P1}");
Console.WriteLine($"Denied: {usage.DeniedRequests}");

// Full report across all profiles
Console.WriteLine(limiter.GenerateReport());
```

### Serialization

Save and restore limiter state:

```csharp
string json = limiter.ToJson();
var restored = PromptRateLimiter.FromJson(json);
```

---

## Performance Profiler

`PromptPerformanceProfiler` collects latency and token samples per label
(prompt variant, model, chain step) and computes percentile statistics.
Use it to find slow prompts and compare alternatives.

### Recording Samples

Manual recording:

```csharp
var profiler = new PromptPerformanceProfiler();

profiler.Record(
    label: "summarize-v2",
    duration: TimeSpan.FromMilliseconds(1250),
    inputTokens: 3200,
    outputTokens: 450,
    tags: new Dictionary<string, string> { ["model"] = "gpt-4" }
);
```

### Automatic Timing with `StartSample`

Wrap LLM calls in a disposable context that auto-records on dispose:

```csharp
using (var ctx = profiler.StartSample("summarize-v2"))
{
    // ... call LLM ...
    ctx.SetTokens(inputTokens: 3200, outputTokens: 450);
}
// Duration recorded automatically when ctx is disposed
```

Capture errors without stopping the clock:

```csharp
using (var ctx = profiler.StartSample("summarize-v2"))
{
    try
    {
        // ... call LLM ...
        ctx.SetTokens(3200, 450);
    }
    catch (Exception ex)
    {
        ctx.SetError(ex.Message);
        throw;
    }
}
```

### Statistics

```csharp
ProfileStats stats = profiler.GetStats("summarize-v2");
Console.WriteLine($"Samples: {stats.SampleCount}");
Console.WriteLine($"Success rate: {stats.SuccessRate:P1}");
Console.WriteLine($"Mean: {stats.MeanLatencyMs:F0}ms");
Console.WriteLine($"Median: {stats.MedianLatencyMs:F0}ms");
Console.WriteLine($"P95: {stats.P95LatencyMs:F0}ms");
Console.WriteLine($"P99: {stats.P99LatencyMs:F0}ms");
Console.WriteLine($"Tokens/sec: {stats.TokensPerSecond:F1}");
```

### Comparing Prompt Variants

A/B-test latency and token efficiency:

```csharp
ProfileComparison cmp = profiler.Compare("summarize-v1", "summarize-v2");
Console.WriteLine($"Latency diff: {cmp.LatencyDiffPercent:+0.0;-0.0}%");
Console.WriteLine($"Token diff: {cmp.TokenDiffPercent:+0.0;-0.0}%");
Console.WriteLine($"Winner: {cmp.Winner} — {cmp.Reason}");
```

### Reports & Export

```csharp
// Human-readable text report
Console.WriteLine(profiler.GenerateReport(title: "Weekly Prompt Perf"));

// Machine-readable JSON export
File.WriteAllText("perf.json", profiler.ExportJson());
```

---

## Putting It All Together

A typical production loop combines all three:

```csharp
var cache    = new PromptCache(capacity: 1024, defaultTtl: TimeSpan.FromHours(2));
var limiter  = PromptRateLimiter.WithDefaults();
var profiler = new PromptPerformanceProfiler();

limiter.AddProfile(new RateLimitProfile
{
    Name = "gpt-4", RequestsPerMinute = 60, TokensPerMinute = 90_000
});

async Task<string> Complete(string prompt)
{
    // 1. Cache hit?
    var cached = cache.Get(prompt, model: "gpt-4");
    if (cached != null) return cached.Response;

    // 2. Rate-limit
    var slot = await limiter.WaitAndAcquireAsync("gpt-4", estimatedTokens: 500);

    // 3. Profile
    using var ctx = profiler.StartSample("main-completion");
    try
    {
        string response = await CallLlmAsync(prompt);
        ctx.SetTokens(500, 200);
        limiter.RecordCompletion("gpt-4", actualTokens: 200,
                                 acquireTimestamp: slot.AcquireTimestamp);
        cache.Put(prompt, response, model: "gpt-4");
        return response;
    }
    catch (Exception ex)
    {
        ctx.SetError(ex.Message);
        limiter.RecordCompletion("gpt-4", acquireTimestamp: slot.AcquireTimestamp);
        throw;
    }
}
```

## See Also

- [Production Features](production-features.md) — deployment checklist
- [Error Handling](error-handling.md) — retry and fallback patterns
- [Observability & Debugging](observability.md) — logging and tracing
- [Streaming Responses](streaming.md) — streaming with profiler integration
