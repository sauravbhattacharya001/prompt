namespace Prompt
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Context passed through the middleware pipeline for each prompt execution.
    /// Contains the input prompt, variables, response, timing, and metadata that
    /// middleware components can read and modify.
    /// </summary>
    public class PromptPipelineContext
    {
        /// <summary>The prompt template being executed.</summary>
        public string PromptText { get; set; } = string.Empty;

        /// <summary>Variables available for template rendering.</summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        /// <summary>The rendered/final prompt text after variable substitution.</summary>
        public string RenderedPrompt { get; set; } = string.Empty;

        /// <summary>The model response (populated after execution).</summary>
        public string? Response { get; set; }

        /// <summary>Whether the pipeline was short-circuited (skipped model call).</summary>
        public bool ShortCircuited { get; set; }

        /// <summary>Execution duration of the model call.</summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>Estimated token count for the prompt.</summary>
        public int EstimatedTokens { get; set; }

        /// <summary>Arbitrary metadata that middleware can attach.</summary>
        public Dictionary<string, object> Metadata { get; } = new();

        /// <summary>Errors collected during pipeline execution.</summary>
        public List<string> Errors { get; } = new();

        /// <summary>Warnings collected during pipeline execution.</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>Whether the pipeline has encountered a fatal error.</summary>
        public bool HasError => Errors.Count > 0;

        /// <summary>Unique identifier for this execution.</summary>
        public string ExecutionId { get; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>When this context was created.</summary>
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Delegate representing the next middleware in the pipeline.
    /// </summary>
    public delegate Task PromptPipelineDelegate(PromptPipelineContext context);

    /// <summary>
    /// Interface for prompt middleware components. Each middleware can inspect
    /// and modify the context before and after the next middleware executes.
    /// </summary>
    public interface IPromptMiddleware
    {
        /// <summary>Display name for logging/diagnostics.</summary>
        string Name { get; }

        /// <summary>Execution order (lower runs first).</summary>
        int Order { get; }

        /// <summary>
        /// Invokes this middleware. Call <paramref name="next"/> to continue
        /// the pipeline, or skip it to short-circuit.
        /// </summary>
        Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next);
    }

    /// <summary>
    /// Convenience base class for creating middleware with a lambda.
    /// </summary>
    public class LambdaMiddleware : IPromptMiddleware
    {
        private readonly Func<PromptPipelineContext, PromptPipelineDelegate, Task> _handler;

        public string Name { get; }
        public int Order { get; }

        public LambdaMiddleware(string name, int order,
            Func<PromptPipelineContext, PromptPipelineDelegate, Task> handler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Order = order;
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next)
            => _handler(context, next);
    }

    /// <summary>
    /// Logs prompt execution details (timing, tokens, errors) to a provided action.
    /// </summary>
    public class LoggingMiddleware : IPromptMiddleware
    {
        private readonly Action<string> _log;

        public string Name => "Logging";
        public int Order { get; }

        public LoggingMiddleware(Action<string> log, int order = 0)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            Order = order;
        }

        public async Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next)
        {
            _log($"[{context.ExecutionId}] Starting prompt ({context.EstimatedTokens} est. tokens)");
            var sw = Stopwatch.StartNew();
            try
            {
                await next(context);
                sw.Stop();
                _log($"[{context.ExecutionId}] Completed in {sw.ElapsedMilliseconds}ms" +
                     (context.ShortCircuited ? " (short-circuited)" : ""));
            }
            catch (Exception ex)
            {
                sw.Stop();
                _log($"[{context.ExecutionId}] Failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Caches responses by prompt text. Returns cached response without calling the model.
    /// </summary>
    public class CachingMiddleware : IPromptMiddleware
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly TimeSpan _ttl;
        private readonly int _maxEntries;
        private readonly LinkedList<string> _accessOrder = new();
        private readonly object _evictLock = new();

        public string Name => "Caching";
        public int Order { get; }

        /// <summary>Number of cache hits.</summary>
        public int HitCount { get; private set; }

        /// <summary>Number of cache misses.</summary>
        public int MissCount { get; private set; }

        /// <summary>Number of entries evicted due to capacity limits.</summary>
        public int EvictionCount { get; private set; }

        /// <summary>
        /// Creates a caching middleware with TTL and optional max capacity.
        /// </summary>
        /// <param name="ttl">Cache entry time-to-live (default: 30 minutes).</param>
        /// <param name="order">Middleware execution order.</param>
        /// <param name="maxEntries">Maximum cached entries before LRU eviction (default: 1000, 0 = unlimited).</param>
        public CachingMiddleware(TimeSpan? ttl = null, int order = 10, int maxEntries = 1000)
        {
            _ttl = ttl ?? TimeSpan.FromMinutes(30);
            Order = order;
            _maxEntries = maxEntries;
        }

        public async Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next)
        {
            var key = context.RenderedPrompt;
            if (string.IsNullOrEmpty(key)) key = context.PromptText;

            if (_cache.TryGetValue(key, out var entry) &&
                DateTimeOffset.UtcNow - entry.CreatedAt < _ttl)
            {
                context.Response = entry.Response;
                context.ShortCircuited = true;
                context.Metadata["cache"] = "hit";
                HitCount++;
                PromoteKey(key);
                return;
            }

            MissCount++;
            context.Metadata["cache"] = "miss";
            await next(context);

            if (context.Response != null)
            {
                _cache[key] = new CacheEntry(context.Response, DateTimeOffset.UtcNow);
                TrackKey(key);
                EvictIfNeeded();
            }
        }

        /// <summary>Clears all cached entries.</summary>
        public void Clear()
        {
            _cache.Clear();
            lock (_evictLock) { _accessOrder.Clear(); }
        }

        /// <summary>Current number of cached entries.</summary>
        public int Count => _cache.Count;

        private void PromoteKey(string key)
        {
            lock (_evictLock)
            {
                _accessOrder.Remove(key);
                _accessOrder.AddLast(key);
            }
        }

        private void TrackKey(string key)
        {
            lock (_evictLock)
            {
                _accessOrder.Remove(key);
                _accessOrder.AddLast(key);
            }
        }

        private void EvictIfNeeded()
        {
            if (_maxEntries <= 0) return;

            lock (_evictLock)
            {
                while (_cache.Count > _maxEntries && _accessOrder.Count > 0)
                {
                    var oldest = _accessOrder.First!.Value;
                    _accessOrder.RemoveFirst();
                    _cache.TryRemove(oldest, out _);
                    EvictionCount++;
                }
            }
        }

        private record CacheEntry(string Response, DateTimeOffset CreatedAt);
    }

    /// <summary>
    /// Validates prompt content before execution. Checks for empty prompts,
    /// token limits, and required variables.
    /// </summary>
    public class ValidationMiddleware : IPromptMiddleware
    {
        private readonly int _maxTokens;
        private readonly HashSet<string> _requiredVariables;

        public string Name => "Validation";
        public int Order { get; }

        public ValidationMiddleware(int maxTokens = 128000,
            IEnumerable<string>? requiredVariables = null, int order = -10)
        {
            _maxTokens = maxTokens;
            _requiredVariables = requiredVariables != null
                ? new HashSet<string>(requiredVariables)
                : new HashSet<string>();
            Order = order;
        }

        public async Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next)
        {
            if (string.IsNullOrWhiteSpace(context.PromptText))
            {
                context.Errors.Add("Prompt text is empty.");
                return;
            }

            // Rough token estimate: ~4 chars per token
            context.EstimatedTokens = (context.PromptText.Length + 3) / 4;
            if (context.EstimatedTokens > _maxTokens)
            {
                context.Errors.Add(
                    $"Estimated tokens ({context.EstimatedTokens}) exceeds limit ({_maxTokens}).");
                return;
            }

            foreach (var v in _requiredVariables)
            {
                if (!context.Variables.ContainsKey(v))
                {
                    context.Errors.Add($"Required variable '{v}' is missing.");
                }
            }

            if (!context.HasError)
            {
                await next(context);
            }
        }
    }

    /// <summary>
    /// Retries failed executions with exponential backoff.
    /// </summary>
    public class RetryMiddleware : IPromptMiddleware
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;

        public string Name => "Retry";
        public int Order { get; }

        /// <summary>Number of retries performed across all invocations.</summary>
        public int TotalRetries { get; private set; }

        public RetryMiddleware(int maxRetries = 3, TimeSpan? baseDelay = null, int order = -5)
        {
            _maxRetries = maxRetries;
            _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(500);
            Order = order;
        }

        public async Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next)
        {
            Exception? lastException = null;
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    await next(context);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    TotalRetries++;
                    context.Warnings.Add($"Attempt {attempt + 1} failed: {ex.Message}");
                    if (attempt < _maxRetries)
                    {
                        var delay = TimeSpan.FromMilliseconds(
                            _baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                        await Task.Delay(delay);
                    }
                }
            }
            context.Errors.Add($"All {_maxRetries + 1} attempts failed.");
            throw lastException!;
        }
    }

    /// <summary>
    /// Collects execution metrics across pipeline runs.
    /// </summary>
    public class MetricsMiddleware : IPromptMiddleware
    {
        private readonly List<ExecutionMetric> _metrics = new();
        private readonly object _lock = new();

        public string Name => "Metrics";
        public int Order { get; }

        public MetricsMiddleware(int order = 100) => Order = order;

        public async Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                sw.Stop();
                lock (_lock)
                {
                    _metrics.Add(new ExecutionMetric(
                        context.ExecutionId,
                        context.CreatedAt,
                        sw.Elapsed,
                        context.EstimatedTokens,
                        context.HasError,
                        context.ShortCircuited));
                }
            }
        }

        /// <summary>Returns all collected metrics.</summary>
        public IReadOnlyList<ExecutionMetric> GetMetrics()
        {
            lock (_lock) { return _metrics.ToList().AsReadOnly(); }
        }

        /// <summary>Average execution time across all runs.</summary>
        public TimeSpan AverageExecutionTime()
        {
            lock (_lock)
            {
                if (_metrics.Count == 0) return TimeSpan.Zero;
                return TimeSpan.FromMilliseconds(
                    _metrics.Average(m => m.Duration.TotalMilliseconds));
            }
        }

        /// <summary>Total estimated tokens processed.</summary>
        public long TotalTokens()
        {
            lock (_lock) { return _metrics.Sum(m => (long)m.EstimatedTokens); }
        }

        /// <summary>Error rate as a fraction (0.0 – 1.0).</summary>
        public double ErrorRate()
        {
            lock (_lock)
            {
                if (_metrics.Count == 0) return 0;
                return (double)_metrics.Count(m => m.HasError) / _metrics.Count;
            }
        }

        /// <summary>Clears all collected metrics.</summary>
        public void Clear() { lock (_lock) { _metrics.Clear(); } }
    }

    /// <summary>Single execution metric record.</summary>
    public record ExecutionMetric(
        string ExecutionId,
        DateTimeOffset Timestamp,
        TimeSpan Duration,
        int EstimatedTokens,
        bool HasError,
        bool WasCached);

    /// <summary>
    /// Content filtering middleware that checks prompts and responses
    /// against blocked patterns.
    /// </summary>
    public class ContentFilterMiddleware : IPromptMiddleware
    {
        private readonly List<string> _blockedPatterns;
        private readonly bool _filterResponse;

        public string Name => "ContentFilter";
        public int Order { get; }

        /// <summary>Number of prompts blocked.</summary>
        public int BlockedCount { get; private set; }

        public ContentFilterMiddleware(IEnumerable<string> blockedPatterns,
            bool filterResponse = true, int order = -8)
        {
            _blockedPatterns = blockedPatterns?.ToList()
                ?? throw new ArgumentNullException(nameof(blockedPatterns));
            _filterResponse = filterResponse;
            Order = order;
        }

        public async Task InvokeAsync(PromptPipelineContext context, PromptPipelineDelegate next)
        {
            foreach (var pattern in _blockedPatterns)
            {
                if (context.PromptText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    context.Errors.Add($"Prompt contains blocked content: '{pattern}'");
                    BlockedCount++;
                    return;
                }
            }

            await next(context);

            if (_filterResponse && context.Response != null)
            {
                foreach (var pattern in _blockedPatterns)
                {
                    if (context.Response.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        context.Warnings.Add($"Response contained blocked content: '{pattern}'");
                        context.Response = "[Content filtered]";
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Configurable prompt middleware pipeline. Middleware components are sorted
    /// by <see cref="IPromptMiddleware.Order"/> and executed in sequence.
    /// Supports both pre-built middleware components and inline lambdas.
    /// </summary>
    /// <example>
    /// <code>
    /// var pipeline = new PromptPipeline()
    ///     .Use(new ValidationMiddleware(maxTokens: 4000))
    ///     .Use(new LoggingMiddleware(Console.WriteLine))
    ///     .Use(new CachingMiddleware(TimeSpan.FromMinutes(5)))
    ///     .Use(new RetryMiddleware(maxRetries: 2))
    ///     .Use(new MetricsMiddleware());
    ///
    /// var context = new PromptPipelineContext
    /// {
    ///     PromptText = "Summarize: {{text}}",
    ///     Variables = new() { ["text"] = "Hello world" }
    /// };
    ///
    /// await pipeline.ExecuteAsync(context, async ctx =>
    /// {
    ///     // Your actual model call here
    ///     ctx.Response = await CallModel(ctx.RenderedPrompt);
    /// });
    /// </code>
    /// </example>
    public class PromptPipeline
    {
        private readonly List<IPromptMiddleware> _middleware = new();

        /// <summary>Adds a middleware component to the pipeline.</summary>
        public PromptPipeline Use(IPromptMiddleware middleware)
        {
            _middleware.Add(middleware ?? throw new ArgumentNullException(nameof(middleware)));
            return this;
        }

        /// <summary>Adds an inline middleware function.</summary>
        public PromptPipeline Use(string name, int order,
            Func<PromptPipelineContext, PromptPipelineDelegate, Task> handler)
        {
            _middleware.Add(new LambdaMiddleware(name, order, handler));
            return this;
        }

        /// <summary>Returns the current middleware components (sorted by order).</summary>
        public IReadOnlyList<IPromptMiddleware> GetMiddleware()
            => _middleware.OrderBy(m => m.Order).ToList().AsReadOnly();

        /// <summary>Removes a middleware by name.</summary>
        public bool Remove(string name)
            => _middleware.RemoveAll(m => m.Name == name) > 0;

        /// <summary>
        /// Executes the pipeline with the given context and terminal handler.
        /// The terminal handler is where the actual model call happens.
        /// </summary>
        public async Task ExecuteAsync(PromptPipelineContext context,
            Func<PromptPipelineContext, Task> terminalHandler)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (terminalHandler == null) throw new ArgumentNullException(nameof(terminalHandler));

            // Render variables into prompt
            context.RenderedPrompt = RenderVariables(context.PromptText, context.Variables);

            var sorted = _middleware.OrderBy(m => m.Order).ToList();
            var sw = Stopwatch.StartNew();

            // Build the pipeline chain from inside out
            PromptPipelineDelegate pipeline = async ctx =>
            {
                await terminalHandler(ctx);
            };

            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var mw = sorted[i];
                var next = pipeline;
                pipeline = ctx => mw.InvokeAsync(ctx, next);
            }

            await pipeline(context);
            sw.Stop();
            context.ExecutionTime = sw.Elapsed;
        }

        /// <summary>
        /// Single-pass variable substitution: replaces {{varName}} with values
        /// using regex matching.  This avoids the O(n·k) cost of repeated
        /// String.Replace calls and prevents double-substitution when a
        /// variable's value itself contains {{...}} syntax.
        /// </summary>
        internal static string RenderVariables(string template, Dictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(template) || variables.Count == 0)
                return template;

            return Regex.Replace(template, @"\{\{(\w[\w.-]*)\}\}", match =>
            {
                var key = match.Groups[1].Value;
                return variables.TryGetValue(key, out var value) ? value : match.Value;
            });
        }

        /// <summary>
        /// Returns a summary of the pipeline configuration.
        /// </summary>
        public string Describe()
        {
            var sorted = _middleware.OrderBy(m => m.Order).ToList();
            if (sorted.Count == 0) return "Empty pipeline (no middleware)";

            var lines = new List<string> { $"Pipeline with {sorted.Count} middleware:" };
            for (int i = 0; i < sorted.Count; i++)
            {
                lines.Add($"  {i + 1}. {sorted[i].Name} (order: {sorted[i].Order})");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
