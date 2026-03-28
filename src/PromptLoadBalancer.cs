namespace Prompt
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Load-balancing strategy for distributing requests across endpoints.
    /// </summary>
    public enum LoadBalanceStrategy
    {
        /// <summary>Weighted round-robin: endpoints with higher weight get proportionally more traffic.</summary>
        WeightedRoundRobin,

        /// <summary>Least connections: routes to the endpoint with fewest in-flight requests.</summary>
        LeastConnections,

        /// <summary>Least latency: routes to the endpoint with lowest recent average latency.</summary>
        LeastLatency,

        /// <summary>Random: uniformly random selection among healthy endpoints.</summary>
        Random
    }

    /// <summary>
    /// Health state of a load-balanced endpoint.
    /// </summary>
    public enum EndpointHealth
    {
        /// <summary>Endpoint is accepting requests normally.</summary>
        Healthy,

        /// <summary>Endpoint has recent failures and is being given reduced traffic.</summary>
        Degraded,

        /// <summary>Endpoint is temporarily removed from the pool due to repeated failures.</summary>
        Unhealthy
    }

    /// <summary>
    /// Configuration for a single endpoint in the load balancer pool.
    /// </summary>
    public class LoadBalancerEndpoint
    {
        /// <summary>Human-readable name for this endpoint.</summary>
        public string Name { get; set; } = "";

        /// <summary>Azure OpenAI endpoint URI.</summary>
        public string? EndpointUri { get; set; }

        /// <summary>Azure OpenAI API key.</summary>
        public string? ApiKey { get; set; }

        /// <summary>Deployed model name.</summary>
        public string? Model { get; set; }

        /// <summary>Relative weight for weighted round-robin (default 1). Higher = more traffic.</summary>
        public int Weight { get; set; } = 1;

        /// <summary>Maximum concurrent requests for this endpoint (0 = unlimited).</summary>
        public int MaxConcurrent { get; set; } = 0;

        /// <summary>Per-request timeout. Null means no endpoint-specific timeout.</summary>
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Configuration for the <see cref="PromptLoadBalancer"/>.
    /// </summary>
    public class LoadBalancerConfig
    {
        /// <summary>Endpoints in the pool.</summary>
        public List<LoadBalancerEndpoint> Endpoints { get; set; } = new();

        /// <summary>Load-balancing strategy. Default: WeightedRoundRobin.</summary>
        public LoadBalanceStrategy Strategy { get; set; } = LoadBalanceStrategy.WeightedRoundRobin;

        /// <summary>Number of consecutive failures before marking an endpoint unhealthy.</summary>
        public int UnhealthyThreshold { get; set; } = 3;

        /// <summary>Number of consecutive failures before marking an endpoint degraded.</summary>
        public int DegradedThreshold { get; set; } = 1;

        /// <summary>How long an unhealthy endpoint stays out of rotation before a health probe.</summary>
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Size of the latency sample window for LeastLatency strategy.</summary>
        public int LatencySampleSize { get; set; } = 20;

        /// <summary>Whether to retry on a different endpoint when one fails.</summary>
        public bool RetryOnFailover { get; set; } = true;

        /// <summary>Maximum failover retries across different endpoints (0 = try all healthy).</summary>
        public int MaxFailoverRetries { get; set; } = 0;

        /// <summary>Enable request logging for diagnostics.</summary>
        public bool EnableLogging { get; set; } = false;
    }

    /// <summary>
    /// Snapshot of an endpoint's runtime statistics.
    /// </summary>
    public class EndpointStats
    {
        /// <summary>Endpoint name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Current health state.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EndpointHealth Health { get; set; }

        /// <summary>Total requests routed to this endpoint.</summary>
        public long TotalRequests { get; set; }

        /// <summary>Total successful responses.</summary>
        public long Successes { get; set; }

        /// <summary>Total failures.</summary>
        public long Failures { get; set; }

        /// <summary>Current in-flight requests.</summary>
        public int InFlight { get; set; }

        /// <summary>Consecutive failures (resets on success).</summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>Average latency in milliseconds (from recent samples).</summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>P95 latency in milliseconds (from recent samples).</summary>
        public double P95LatencyMs { get; set; }

        /// <summary>Success rate (0.0–1.0).</summary>
        public double SuccessRate { get; set; }

        /// <summary>When the endpoint last became unhealthy (null if never).</summary>
        public DateTimeOffset? LastUnhealthyAt { get; set; }

        /// <summary>Weight configured for this endpoint.</summary>
        public int Weight { get; set; }
    }

    /// <summary>
    /// Load balancer summary report.
    /// </summary>
    public class LoadBalancerReport
    {
        /// <summary>Strategy in use.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LoadBalanceStrategy Strategy { get; set; }

        /// <summary>Total requests across all endpoints.</summary>
        public long TotalRequests { get; set; }

        /// <summary>Total failures across all endpoints.</summary>
        public long TotalFailures { get; set; }

        /// <summary>Overall success rate.</summary>
        public double OverallSuccessRate { get; set; }

        /// <summary>Number of failover events.</summary>
        public long FailoverEvents { get; set; }

        /// <summary>Per-endpoint statistics.</summary>
        public List<EndpointStats> Endpoints { get; set; } = new();
    }

    /// <summary>
    /// Log entry for a load-balanced request.
    /// </summary>
    public class LoadBalancerLogEntry
    {
        /// <summary>Timestamp of the request.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>Endpoint that handled the request.</summary>
        public string EndpointName { get; set; } = "";

        /// <summary>Whether the request succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Latency in milliseconds.</summary>
        public double LatencyMs { get; set; }

        /// <summary>Whether this was a failover attempt.</summary>
        public bool IsFailover { get; set; }

        /// <summary>Error message if failed.</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Internal runtime state for an endpoint.
    /// </summary>
    internal class EndpointState
    {
        public LoadBalancerEndpoint Config { get; }
        public EndpointHealth Health { get; set; } = EndpointHealth.Healthy;
        public long TotalRequests;
        public long Successes;
        public long Failures;
        public int InFlight;
        public int ConsecutiveFailures;
        public DateTimeOffset? LastUnhealthyAt;
        private readonly ConcurrentQueue<double> _latencySamples = new();
        private readonly int _maxSamples;

        public EndpointState(LoadBalancerEndpoint config, int maxSamples)
        {
            Config = config;
            _maxSamples = maxSamples;
        }

        public void RecordLatency(double ms)
        {
            _latencySamples.Enqueue(ms);
            while (_latencySamples.Count > _maxSamples)
                _latencySamples.TryDequeue(out _);
        }

        public double AverageLatencyMs()
        {
            var samples = _latencySamples.ToArray();
            return samples.Length == 0 ? 0 : samples.Average();
        }

        public double P95LatencyMs()
        {
            var samples = _latencySamples.ToArray();
            if (samples.Length == 0) return 0;
            Array.Sort(samples);
            int idx = (int)Math.Ceiling(samples.Length * 0.95) - 1;
            return samples[Math.Max(0, idx)];
        }

        public double SuccessRate()
        {
            long total = Interlocked.Read(ref TotalRequests);
            return total == 0 ? 1.0 : (double)Interlocked.Read(ref Successes) / total;
        }
    }

    /// <summary>
    /// Distributes prompt requests across multiple API endpoints with health tracking,
    /// automatic failover, and configurable load-balancing strategies.
    /// </summary>
    /// <remarks>
    /// <para>Unlike <see cref="PromptFallbackChain"/> which tries endpoints sequentially in
    /// priority order, the load balancer distributes traffic across all healthy endpoints
    /// simultaneously based on the chosen strategy.</para>
    /// <para>Thread-safe for concurrent use.</para>
    /// </remarks>
    public class PromptLoadBalancer
    {
        private readonly LoadBalancerConfig _config;
        private readonly List<EndpointState> _endpoints;
        private readonly ConcurrentQueue<LoadBalancerLogEntry> _log = new();
        private long _roundRobinCounter;
        private long _failoverEvents;
        private readonly Random _random = new();
        private readonly object _lock = new();

        /// <summary>
        /// Creates a new load balancer with the given configuration.
        /// </summary>
        /// <param name="config">Load balancer configuration.</param>
        /// <exception cref="ArgumentException">Thrown when no endpoints are provided.</exception>
        public PromptLoadBalancer(LoadBalancerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Endpoints == null || config.Endpoints.Count == 0)
                throw new ArgumentException("At least one endpoint is required.", nameof(config));

            _config = config;
            _endpoints = config.Endpoints
                .Select(e => new EndpointState(e, config.LatencySampleSize))
                .ToList();
        }

        /// <summary>
        /// Gets the number of endpoints in the pool.
        /// </summary>
        public int EndpointCount => _endpoints.Count;

        /// <summary>
        /// Gets the count of healthy endpoints.
        /// </summary>
        public int HealthyCount => _endpoints.Count(e => e.Health != EndpointHealth.Unhealthy);

        /// <summary>
        /// Executes a request through the load balancer, routing to the best available endpoint.
        /// </summary>
        /// <typeparam name="T">Return type of the request function.</typeparam>
        /// <param name="requestFunc">Async function that takes (endpointUri, apiKey, model) and returns a result.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result from the first successful endpoint.</returns>
        /// <exception cref="InvalidOperationException">Thrown when all endpoints are exhausted.</exception>
        public async Task<T> ExecuteAsync<T>(
            Func<string?, string?, string?, CancellationToken, Task<T>> requestFunc,
            CancellationToken cancellationToken = default)
        {
            RefreshHealthStates();

            var tried = new HashSet<int>();
            int maxRetries = _config.MaxFailoverRetries > 0
                ? _config.MaxFailoverRetries
                : _endpoints.Count;
            Exception? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int idx = SelectEndpoint(tried);
                if (idx < 0) break;

                tried.Add(idx);
                var state = _endpoints[idx];
                bool isFailover = attempt > 0;

                if (isFailover)
                    Interlocked.Increment(ref _failoverEvents);

                // Enforce max concurrent
                if (state.Config.MaxConcurrent > 0 &&
                    Interlocked.CompareExchange(ref state.InFlight, 0, 0) >= state.Config.MaxConcurrent)
                {
                    continue; // skip overloaded endpoint
                }

                Interlocked.Increment(ref state.TotalRequests);
                Interlocked.Increment(ref state.InFlight);
                var sw = Stopwatch.StartNew();

                try
                {
                    Task<T> task;
                    if (state.Config.Timeout.HasValue)
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        cts.CancelAfter(state.Config.Timeout.Value);
                        task = requestFunc(state.Config.EndpointUri, state.Config.ApiKey, state.Config.Model, cts.Token);
                    }
                    else
                    {
                        task = requestFunc(state.Config.EndpointUri, state.Config.ApiKey, state.Config.Model, cancellationToken);
                    }

                    var result = await task.ConfigureAwait(false);
                    sw.Stop();

                    RecordSuccess(state, sw.Elapsed.TotalMilliseconds, isFailover);
                    return result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Decrement(ref state.InFlight);
                    throw;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Interlocked.Decrement(ref state.InFlight);
                    RecordFailure(state, sw.Elapsed.TotalMilliseconds, isFailover, ex);
                    lastException = ex;

                    if (!_config.RetryOnFailover)
                        break;
                }
            }

            throw new InvalidOperationException(
                "All endpoints exhausted. Last error: " + (lastException?.Message ?? "none"),
                lastException);
        }

        /// <summary>
        /// Executes a void request through the load balancer.
        /// </summary>
        public async Task ExecuteAsync(
            Func<string?, string?, string?, CancellationToken, Task> requestFunc,
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync<object?>(async (uri, key, model, ct) =>
            {
                await requestFunc(uri, key, model, ct).ConfigureAwait(false);
                return null;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a snapshot report of all endpoint statistics and overall health.
        /// </summary>
        public LoadBalancerReport GetReport()
        {
            var report = new LoadBalancerReport
            {
                Strategy = _config.Strategy,
                FailoverEvents = Interlocked.Read(ref _failoverEvents)
            };

            foreach (var state in _endpoints)
            {
                var stats = new EndpointStats
                {
                    Name = state.Config.Name,
                    Health = state.Health,
                    TotalRequests = Interlocked.Read(ref state.TotalRequests),
                    Successes = Interlocked.Read(ref state.Successes),
                    Failures = Interlocked.Read(ref state.Failures),
                    InFlight = Interlocked.CompareExchange(ref state.InFlight, 0, 0),
                    ConsecutiveFailures = state.ConsecutiveFailures,
                    AverageLatencyMs = Math.Round(state.AverageLatencyMs(), 2),
                    P95LatencyMs = Math.Round(state.P95LatencyMs(), 2),
                    SuccessRate = Math.Round(state.SuccessRate(), 4),
                    LastUnhealthyAt = state.LastUnhealthyAt,
                    Weight = state.Config.Weight
                };
                report.Endpoints.Add(stats);
                report.TotalRequests += stats.TotalRequests;
                report.TotalFailures += stats.Failures;
            }

            report.OverallSuccessRate = report.TotalRequests == 0
                ? 1.0
                : Math.Round(1.0 - (double)report.TotalFailures / report.TotalRequests, 4);

            return report;
        }

        /// <summary>
        /// Gets the request log (only populated when <see cref="LoadBalancerConfig.EnableLogging"/> is true).
        /// </summary>
        public List<LoadBalancerLogEntry> GetLog() => _log.ToArray().ToList();

        /// <summary>
        /// Resets all endpoint statistics and health states.
        /// </summary>
        public void Reset()
        {
            foreach (var state in _endpoints)
            {
                state.Health = EndpointHealth.Healthy;
                Interlocked.Exchange(ref state.TotalRequests, 0);
                Interlocked.Exchange(ref state.Successes, 0);
                Interlocked.Exchange(ref state.Failures, 0);
                Interlocked.Exchange(ref state.InFlight, 0);
                state.ConsecutiveFailures = 0;
                state.LastUnhealthyAt = null;
            }
            Interlocked.Exchange(ref _failoverEvents, 0);
            while (_log.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Manually marks an endpoint as healthy, degraded, or unhealthy.
        /// </summary>
        public void SetEndpointHealth(string name, EndpointHealth health)
        {
            var state = _endpoints.FirstOrDefault(e =>
                string.Equals(e.Config.Name, name, StringComparison.OrdinalIgnoreCase));
            if (state == null)
                throw new ArgumentException($"Endpoint '{name}' not found.");
            state.Health = health;
            if (health == EndpointHealth.Unhealthy)
                state.LastUnhealthyAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Serializes the current report to JSON.
        /// </summary>
        public string ToJson(bool indented = false)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(GetReport(), options);
        }

        // ── Private helpers ──

        private int SelectEndpoint(HashSet<int> exclude)
        {
            var candidates = new List<(int Index, EndpointState State)>();
            for (int i = 0; i < _endpoints.Count; i++)
            {
                if (exclude.Contains(i)) continue;
                if (_endpoints[i].Health == EndpointHealth.Unhealthy) continue;
                candidates.Add((i, _endpoints[i]));
            }

            if (candidates.Count == 0) return -1;

            return _config.Strategy switch
            {
                LoadBalanceStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(candidates),
                LoadBalanceStrategy.LeastConnections => SelectLeastConnections(candidates),
                LoadBalanceStrategy.LeastLatency => SelectLeastLatency(candidates),
                LoadBalanceStrategy.Random => SelectRandom(candidates),
                _ => candidates[0].Index
            };
        }

        private int SelectWeightedRoundRobin(List<(int Index, EndpointState State)> candidates)
        {
            int totalWeight = candidates.Sum(c => GetEffectiveWeight(c.State));
            if (totalWeight == 0) return candidates[0].Index;

            long counter = Interlocked.Increment(ref _roundRobinCounter);
            int slot = (int)(counter % totalWeight);

            int cumulative = 0;
            foreach (var (idx, state) in candidates)
            {
                cumulative += GetEffectiveWeight(state);
                if (slot < cumulative) return idx;
            }

            return candidates[^1].Index;
        }

        private int SelectLeastConnections(List<(int Index, EndpointState State)> candidates)
        {
            return candidates
                .OrderBy(c => Interlocked.CompareExchange(ref c.State.InFlight, 0, 0))
                .ThenBy(c => c.State.AverageLatencyMs())
                .First().Index;
        }

        private int SelectLeastLatency(List<(int Index, EndpointState State)> candidates)
        {
            return candidates
                .OrderBy(c => c.State.AverageLatencyMs())
                .ThenBy(c => Interlocked.CompareExchange(ref c.State.InFlight, 0, 0))
                .First().Index;
        }

        private int SelectRandom(List<(int Index, EndpointState State)> candidates)
        {
            lock (_lock)
            {
                return candidates[_random.Next(candidates.Count)].Index;
            }
        }

        private static int GetEffectiveWeight(EndpointState state)
        {
            return state.Health == EndpointHealth.Degraded
                ? Math.Max(1, state.Config.Weight / 2)
                : state.Config.Weight;
        }

        private void RecordSuccess(EndpointState state, double latencyMs, bool isFailover)
        {
            Interlocked.Increment(ref state.Successes);
            Interlocked.Decrement(ref state.InFlight);
            state.ConsecutiveFailures = 0;
            state.RecordLatency(latencyMs);

            if (state.Health == EndpointHealth.Degraded)
                state.Health = EndpointHealth.Healthy;

            if (_config.EnableLogging)
            {
                _log.Enqueue(new LoadBalancerLogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    EndpointName = state.Config.Name,
                    Success = true,
                    LatencyMs = Math.Round(latencyMs, 2),
                    IsFailover = isFailover
                });
            }
        }

        private void RecordFailure(EndpointState state, double latencyMs, bool isFailover, Exception ex)
        {
            Interlocked.Increment(ref state.Failures);
            state.ConsecutiveFailures++;
            state.RecordLatency(latencyMs);

            if (state.ConsecutiveFailures >= _config.UnhealthyThreshold)
            {
                state.Health = EndpointHealth.Unhealthy;
                state.LastUnhealthyAt = DateTimeOffset.UtcNow;
            }
            else if (state.ConsecutiveFailures >= _config.DegradedThreshold)
            {
                state.Health = EndpointHealth.Degraded;
            }

            if (_config.EnableLogging)
            {
                _log.Enqueue(new LoadBalancerLogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    EndpointName = state.Config.Name,
                    Success = false,
                    LatencyMs = Math.Round(latencyMs, 2),
                    IsFailover = isFailover,
                    Error = ex.Message
                });
            }
        }

        private void RefreshHealthStates()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var state in _endpoints)
            {
                if (state.Health == EndpointHealth.Unhealthy &&
                    state.LastUnhealthyAt.HasValue &&
                    now - state.LastUnhealthyAt.Value >= _config.CooldownPeriod)
                {
                    // Promote back to degraded for a probe attempt
                    state.Health = EndpointHealth.Degraded;
                    state.ConsecutiveFailures = _config.DegradedThreshold;
                }
            }
        }
    }
}
