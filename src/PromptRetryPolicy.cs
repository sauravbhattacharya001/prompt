namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;

    /// <summary>Backoff strategy for retry delays.</summary>
    public enum BackoffStrategy
    {
        /// <summary>Fixed delay between retries.</summary>
        Fixed,
        /// <summary>Delay doubles each retry.</summary>
        Exponential,
        /// <summary>Delay follows Fibonacci sequence.</summary>
        Fibonacci,
        /// <summary>Linear increase per retry.</summary>
        Linear
    }

    /// <summary>State of a circuit breaker.</summary>
    public enum CircuitState
    {
        /// <summary>Normal operation — requests pass through.</summary>
        Closed,
        /// <summary>Too many failures — requests are rejected.</summary>
        Open,
        /// <summary>Testing recovery — limited requests allowed.</summary>
        HalfOpen
    }

    /// <summary>Categories of errors for per-type retry behavior.</summary>
    public enum ErrorCategory
    {
        /// <summary>Rate limiting (429).</summary>
        RateLimit,
        /// <summary>Server errors (5xx).</summary>
        ServerError,
        /// <summary>Network/timeout errors.</summary>
        Timeout,
        /// <summary>Authentication errors (401/403).</summary>
        AuthError,
        /// <summary>Content policy / safety filter.</summary>
        ContentFilter,
        /// <summary>Invalid request (400).</summary>
        BadRequest,
        /// <summary>Model overloaded.</summary>
        Overloaded,
        /// <summary>Unknown / unclassified.</summary>
        Unknown
    }

    /// <summary>Configuration for how a specific error category should be retried.</summary>
    public class ErrorCategoryPolicy
    {
        /// <summary>Whether to retry this error type at all.</summary>
        public bool ShouldRetry { get; set; } = true;
        /// <summary>Max retries for this category (overrides global).</summary>
        public int? MaxRetries { get; set; }
        /// <summary>Base delay override for this category.</summary>
        public TimeSpan? BaseDelay { get; set; }
        /// <summary>Backoff strategy override.</summary>
        public BackoffStrategy? Backoff { get; set; }
    }

    /// <summary>Configuration for the retry policy.</summary>
    public class RetryPolicyConfig
    {
        /// <summary>Maximum number of retry attempts (default 3).</summary>
        public int MaxRetries { get; set; } = 3;
        /// <summary>Base delay between retries (default 1s).</summary>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>Maximum delay cap (default 60s).</summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(60);
        /// <summary>Backoff strategy (default Exponential).</summary>
        public BackoffStrategy Backoff { get; set; } = BackoffStrategy.Exponential;
        /// <summary>Add random jitter to delays (default true).</summary>
        public bool EnableJitter { get; set; } = true;
        /// <summary>Jitter factor 0.0-1.0 (default 0.25).</summary>
        public double JitterFactor { get; set; } = 0.25;
        /// <summary>Per-category overrides.</summary>
        public Dictionary<ErrorCategory, ErrorCategoryPolicy> CategoryPolicies { get; set; } = new();
        /// <summary>Enable circuit breaker (default true).</summary>
        public bool EnableCircuitBreaker { get; set; } = true;
        /// <summary>Failures to trip the circuit (default 5).</summary>
        public int CircuitBreakerThreshold { get; set; } = 5;
        /// <summary>Time window for failure counting (default 60s).</summary>
        public TimeSpan CircuitBreakerWindow { get; set; } = TimeSpan.FromSeconds(60);
        /// <summary>How long circuit stays open before half-open (default 30s).</summary>
        public TimeSpan CircuitBreakerCooldown { get; set; } = TimeSpan.FromSeconds(30);
        /// <summary>Total timeout across all retries (default 5min, null=unlimited).</summary>
        public TimeSpan? TotalTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>Record of a single retry attempt.</summary>
    public class RetryAttempt
    {
        /// <summary>Attempt number (0 = original, 1+ = retries).</summary>
        public int AttemptNumber { get; set; }
        /// <summary>Error category classified.</summary>
        public ErrorCategory Category { get; set; }
        /// <summary>Error message.</summary>
        public string ErrorMessage { get; set; } = string.Empty;
        /// <summary>Delay before this attempt (zero for first).</summary>
        public TimeSpan Delay { get; set; }
        /// <summary>Timestamp of this attempt.</summary>
        public DateTime Timestamp { get; set; }
        /// <summary>Whether this attempt succeeded.</summary>
        public bool Succeeded { get; set; }
    }

    /// <summary>Result of executing with retry policy.</summary>
    public class RetryResult
    {
        /// <summary>Whether the operation ultimately succeeded.</summary>
        public bool Succeeded { get; set; }
        /// <summary>Final result if succeeded.</summary>
        public string? Result { get; set; }
        /// <summary>All attempts made.</summary>
        public List<RetryAttempt> Attempts { get; set; } = new();
        /// <summary>Total elapsed time.</summary>
        public TimeSpan TotalElapsed { get; set; }
        /// <summary>Final error if failed.</summary>
        public string? FinalError { get; set; }
        /// <summary>Whether circuit breaker tripped.</summary>
        public bool CircuitBreakerTripped { get; set; }
        /// <summary>Total retry count (excludes original attempt).</summary>
        public int RetryCount => Math.Max(0, Attempts.Count - 1);
    }

    /// <summary>
    /// Configurable retry policy for LLM API calls with exponential backoff,
    /// jitter, circuit breaker, and per-error-type handling.
    /// </summary>
    public class PromptRetryPolicy
    {
        private readonly RetryPolicyConfig _config;
        private readonly List<DateTime> _recentFailures = new();
        private CircuitState _circuitState = CircuitState.Closed;
        private DateTime _circuitOpenedAt = DateTime.MinValue;
        private int _halfOpenSuccesses;
        private readonly List<RetryResult> _history = new();
        private const int MaxHistorySize = 1000;
        private readonly Random _rng = new();
        private int _totalExecutions;
        private int _totalRetries;
        private int _totalSuccesses;
        private int _totalCircuitBreaks;
        private readonly Dictionary<ErrorCategory, int> _errorCounts = new();

        /// <summary>Create a retry policy with the given configuration.</summary>
        public PromptRetryPolicy(RetryPolicyConfig? config = null)
        {
            _config = config ?? new RetryPolicyConfig();
            if (_config.MaxRetries < 0) _config.MaxRetries = 0;
            if (_config.JitterFactor < 0) _config.JitterFactor = 0;
            if (_config.JitterFactor > 1) _config.JitterFactor = 1;
            if (_config.CircuitBreakerThreshold < 1) _config.CircuitBreakerThreshold = 1;

            if (!_config.CategoryPolicies.ContainsKey(ErrorCategory.AuthError))
                _config.CategoryPolicies[ErrorCategory.AuthError] = new ErrorCategoryPolicy { ShouldRetry = false };
            if (!_config.CategoryPolicies.ContainsKey(ErrorCategory.BadRequest))
                _config.CategoryPolicies[ErrorCategory.BadRequest] = new ErrorCategoryPolicy { ShouldRetry = false };
            if (!_config.CategoryPolicies.ContainsKey(ErrorCategory.ContentFilter))
                _config.CategoryPolicies[ErrorCategory.ContentFilter] = new ErrorCategoryPolicy { ShouldRetry = false };
            if (!_config.CategoryPolicies.ContainsKey(ErrorCategory.RateLimit))
                _config.CategoryPolicies[ErrorCategory.RateLimit] = new ErrorCategoryPolicy
                {
                    ShouldRetry = true,
                    Backoff = BackoffStrategy.Exponential,
                    BaseDelay = TimeSpan.FromSeconds(2)
                };
            if (!_config.CategoryPolicies.ContainsKey(ErrorCategory.Overloaded))
                _config.CategoryPolicies[ErrorCategory.Overloaded] = new ErrorCategoryPolicy
                {
                    ShouldRetry = true,
                    Backoff = BackoffStrategy.Exponential,
                    BaseDelay = TimeSpan.FromSeconds(5)
                };
        }

        /// <summary>Current circuit breaker state.</summary>
        public CircuitState CircuitState => _circuitState;

        /// <summary>Classify an error message into a category.</summary>
        public ErrorCategory ClassifyError(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage)) return ErrorCategory.Unknown;
            var lower = errorMessage.ToLowerInvariant();

            if (lower.Contains("429") || lower.Contains("rate limit") || lower.Contains("too many requests"))
                return ErrorCategory.RateLimit;
            if (lower.Contains("overloaded") || lower.Contains("capacity") || lower.Contains("model_overloaded"))
                return ErrorCategory.Overloaded;
            if (lower.Contains("timeout") || lower.Contains("timed out") || lower.Contains("network")
                || lower.Contains("connection") || lower.Contains("econnrefused") || lower.Contains("enotfound"))
                return ErrorCategory.Timeout;
            if (lower.Contains("401") || lower.Contains("403") || lower.Contains("unauthorized")
                || lower.Contains("forbidden") || lower.Contains("invalid api key") || lower.Contains("authentication"))
                return ErrorCategory.AuthError;
            if (lower.Contains("content_filter") || lower.Contains("content policy") || lower.Contains("safety")
                || lower.Contains("flagged") || lower.Contains("moderation"))
                return ErrorCategory.ContentFilter;
            if (lower.Contains("400") || lower.Contains("bad request") || lower.Contains("invalid")
                || lower.Contains("malformed"))
                return ErrorCategory.BadRequest;
            if (lower.Contains("500") || lower.Contains("502") || lower.Contains("503") || lower.Contains("504")
                || lower.Contains("server error") || lower.Contains("internal error"))
                return ErrorCategory.ServerError;

            return ErrorCategory.Unknown;
        }

        /// <summary>Calculate delay for a given attempt and category.</summary>
        public TimeSpan CalculateDelay(int attempt, ErrorCategory category)
        {
            if (attempt <= 0) return TimeSpan.Zero;

            var baseDelay = _config.BaseDelay;
            var backoff = _config.Backoff;

            if (_config.CategoryPolicies.TryGetValue(category, out var cp))
            {
                if (cp.BaseDelay.HasValue) baseDelay = cp.BaseDelay.Value;
                if (cp.Backoff.HasValue) backoff = cp.Backoff.Value;
            }

            double delayMs = baseDelay.TotalMilliseconds;

            switch (backoff)
            {
                case BackoffStrategy.Exponential:
                    delayMs *= Math.Pow(2, attempt - 1);
                    break;
                case BackoffStrategy.Linear:
                    delayMs *= attempt;
                    break;
                case BackoffStrategy.Fibonacci:
                    delayMs *= Fib(attempt);
                    break;
                case BackoffStrategy.Fixed:
                    break;
            }

            if (_config.EnableJitter && _config.JitterFactor > 0)
            {
                var jitter = delayMs * _config.JitterFactor * (2.0 * _rng.NextDouble() - 1.0);
                delayMs = Math.Max(0, delayMs + jitter);
            }

            delayMs = Math.Min(delayMs, _config.MaxDelay.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(delayMs);
        }

        /// <summary>Check whether the circuit breaker allows a request.</summary>
        public bool IsCircuitOpen()
        {
            if (!_config.EnableCircuitBreaker) return false;

            if (_circuitState == CircuitState.Open)
            {
                if (DateTime.UtcNow - _circuitOpenedAt >= _config.CircuitBreakerCooldown)
                {
                    _circuitState = CircuitState.HalfOpen;
                    _halfOpenSuccesses = 0;
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Execute an operation with retry policy. The operation function receives
        /// the attempt number and returns (success, result_or_error).
        /// </summary>
        public RetryResult Execute(Func<int, (bool success, string resultOrError)> operation)
        {
            var result = new RetryResult();
            var startTime = DateTime.UtcNow;
            var maxRetries = _config.MaxRetries;

            _totalExecutions++;

            if (IsCircuitOpen())
            {
                result.CircuitBreakerTripped = true;
                result.FinalError = "Circuit breaker is open — too many recent failures.";
                result.TotalElapsed = DateTime.UtcNow - startTime;
                _totalCircuitBreaks++;
                AddToHistory(result);
                return result;
            }

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                var attemptRecord = new RetryAttempt
                {
                    AttemptNumber = attempt,
                    Timestamp = DateTime.UtcNow
                };

                if (attempt > 0)
                {
                    var lastAttempt = result.Attempts.Last();
                    var delay = CalculateDelay(attempt, lastAttempt.Category);
                    attemptRecord.Delay = delay;

                    if (_config.TotalTimeout.HasValue &&
                        (DateTime.UtcNow - startTime + delay) > _config.TotalTimeout.Value)
                    {
                        result.FinalError = "Total timeout exceeded.";
                        break;
                    }

                    // Actually wait for the computed backoff delay before retrying
                    if (delay > TimeSpan.Zero)
                        Thread.Sleep(delay);
                }

                try
                {
                    var (success, resultOrError) = operation(attempt);

                    if (success)
                    {
                        attemptRecord.Succeeded = true;
                        result.Succeeded = true;
                        result.Result = resultOrError;
                        result.Attempts.Add(attemptRecord);
                        _totalRetries += attempt;
                        _totalSuccesses++;
                        RecordSuccess();
                        break;
                    }
                    else
                    {
                        var category = ClassifyError(resultOrError);
                        attemptRecord.Category = category;
                        attemptRecord.ErrorMessage = resultOrError;
                        result.Attempts.Add(attemptRecord);
                        result.FinalError = resultOrError;

                        if (HandleFailedAttempt(result, category, resultOrError))
                            break;
                    }
                }
                catch (Exception ex)
                {
                    var category = ClassifyError(ex.Message);
                    attemptRecord.Category = category;
                    attemptRecord.ErrorMessage = ex.Message;
                    result.Attempts.Add(attemptRecord);
                    result.FinalError = ex.Message;

                    if (HandleFailedAttempt(result, category, ex.Message))
                        break;
                }
            }

            // Count retries for failed executions (successful ones counted above)
            if (!result.Succeeded && result.RetryCount > 0)
                _totalRetries += result.RetryCount;

            result.TotalElapsed = DateTime.UtcNow - startTime;
            AddToHistory(result);
            return result;
        }

        /// <summary>
        /// Handles a failed attempt: records the failure, checks category policies
        /// and circuit breaker. Returns true if retries should stop.
        /// </summary>
        private bool HandleFailedAttempt(RetryResult result, ErrorCategory category, string errorMessage)
        {
            IncrementError(category);
            RecordFailure();

            if (_config.CategoryPolicies.TryGetValue(category, out var policy) && !policy.ShouldRetry)
            {
                result.FinalError = $"Non-retryable error ({category}): {errorMessage}";
                return true;
            }

            if (policy?.MaxRetries != null)
            {
                var categoryAttempts = result.Attempts.Count(a => a.Category == category && !a.Succeeded);
                if (categoryAttempts >= policy.MaxRetries.Value)
                {
                    result.FinalError = $"Max retries for {category} exceeded: {errorMessage}";
                    return true;
                }
            }

            if (IsCircuitOpen())
            {
                result.CircuitBreakerTripped = true;
                result.FinalError = "Circuit breaker tripped during retries.";
                _totalCircuitBreaks++;
                return true;
            }

            return false;
        }

        /// <summary>Whether a given error category is retryable under current config.</summary>
        public bool IsRetryable(ErrorCategory category)
        {
            if (_config.CategoryPolicies.TryGetValue(category, out var policy))
                return policy.ShouldRetry;
            return true;
        }

        /// <summary>Get execution statistics.</summary>
        public RetryPolicyStats GetStats()
        {
            return new RetryPolicyStats
            {
                TotalExecutions = _totalExecutions,
                TotalRetries = _totalRetries,
                TotalSuccesses = _totalSuccesses,
                TotalCircuitBreaks = _totalCircuitBreaks,
                CircuitState = _circuitState,
                ErrorCounts = new Dictionary<ErrorCategory, int>(_errorCounts),
                SuccessRate = _totalExecutions > 0 ? (double)_totalSuccesses / _totalExecutions : 0,
                HistoryCount = _history.Count
            };
        }

        /// <summary>Get recent execution history.</summary>
        public List<RetryResult> GetHistory(int? limit = null)
        {
            if (limit.HasValue && limit.Value > 0 && limit.Value < _history.Count)
                return _history.Skip(_history.Count - limit.Value).ToList();
            return new List<RetryResult>(_history);
        }

        /// <summary>Reset circuit breaker state.</summary>
        public void ResetCircuitBreaker()
        {
            _circuitState = CircuitState.Closed;
            _recentFailures.Clear();
            _halfOpenSuccesses = 0;
        }

        /// <summary>Clear all stats and history.</summary>
        public void Reset()
        {
            _totalExecutions = 0;
            _totalRetries = 0;
            _totalSuccesses = 0;
            _totalCircuitBreaks = 0;
            _errorCounts.Clear();
            _history.Clear();
            ResetCircuitBreaker();
        }

        /// <summary>Export configuration as JSON.</summary>
        public string ExportConfig()
        {
            var export = new Dictionary<string, object>
            {
                ["maxRetries"] = _config.MaxRetries,
                ["baseDelayMs"] = _config.BaseDelay.TotalMilliseconds,
                ["maxDelayMs"] = _config.MaxDelay.TotalMilliseconds,
                ["backoff"] = _config.Backoff.ToString(),
                ["enableJitter"] = _config.EnableJitter,
                ["jitterFactor"] = _config.JitterFactor,
                ["enableCircuitBreaker"] = _config.EnableCircuitBreaker,
                ["circuitBreakerThreshold"] = _config.CircuitBreakerThreshold,
                ["circuitBreakerWindowMs"] = _config.CircuitBreakerWindow.TotalMilliseconds,
                ["circuitBreakerCooldownMs"] = _config.CircuitBreakerCooldown.TotalMilliseconds
            };
            return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Generate a human-readable summary of recent retry behavior.</summary>
        public string GenerateReport()
        {
            var stats = GetStats();
            var lines = new List<string>
            {
                "=== Retry Policy Report ===",
                $"Total Executions: {stats.TotalExecutions}",
                $"Success Rate: {stats.SuccessRate:P1}",
                $"Total Retries: {stats.TotalRetries}",
                $"Circuit Breaks: {stats.TotalCircuitBreaks}",
                $"Circuit State: {stats.CircuitState}",
                ""
            };

            if (stats.ErrorCounts.Any())
            {
                lines.Add("Error Breakdown:");
                foreach (var kvp in stats.ErrorCounts.OrderByDescending(e => e.Value))
                    lines.Add($"  {kvp.Key}: {kvp.Value}");
                lines.Add("");
            }

            var recent = GetHistory(5);
            if (recent.Any())
            {
                lines.Add("Recent Executions:");
                foreach (var r in recent)
                {
                    var status = r.Succeeded ? "OK" : "FAIL";
                    lines.Add($"  [{status}] {r.Attempts.Count} attempt(s), {r.TotalElapsed.TotalMilliseconds:F0}ms" +
                              (r.CircuitBreakerTripped ? " [CIRCUIT BREAK]" : ""));
                }
            }

            return string.Join("\n", lines);
        }

        private void RecordFailure()
        {
            if (!_config.EnableCircuitBreaker) return;
            var now = DateTime.UtcNow;
            _recentFailures.Add(now);

            var cutoff = now - _config.CircuitBreakerWindow;
            _recentFailures.RemoveAll(t => t < cutoff);

            if (_circuitState == CircuitState.HalfOpen)
            {
                _circuitState = CircuitState.Open;
                _circuitOpenedAt = now;
                return;
            }

            if (_recentFailures.Count >= _config.CircuitBreakerThreshold)
            {
                _circuitState = CircuitState.Open;
                _circuitOpenedAt = now;
            }
        }

        private void RecordSuccess()
        {
            if (!_config.EnableCircuitBreaker) return;
            if (_circuitState == CircuitState.HalfOpen)
            {
                _halfOpenSuccesses++;
                if (_halfOpenSuccesses >= 2)
                {
                    _circuitState = CircuitState.Closed;
                    _recentFailures.Clear();
                }
            }
        }

        private void IncrementError(ErrorCategory category)
        {
            if (!_errorCounts.ContainsKey(category))
                _errorCounts[category] = 0;
            _errorCounts[category]++;
        }

        /// <summary>
        /// Adds a result to history, trimming oldest entries if the history
        /// exceeds <see cref="MaxHistorySize"/> to prevent unbounded memory growth.
        /// </summary>
        private void AddToHistory(RetryResult result)
        {
            AddToHistory(result);
            if (_history.Count > MaxHistorySize)
            {
                // Remove the oldest 10% to avoid trimming on every call
                int removeCount = MaxHistorySize / 10;
                _history.RemoveRange(0, removeCount);
            }
        }

        private static int Fib(int n)
        {
            if (n <= 1) return 1;
            int a = 1, b = 1;
            for (int i = 2; i <= n; i++)
            {
                var tmp = a + b;
                a = b;
                b = tmp;
            }
            return b;
        }
    }

    /// <summary>Retry policy statistics.</summary>
    public class RetryPolicyStats
    {
        /// <summary>Total number of Execute calls.</summary>
        public int TotalExecutions { get; set; }
        /// <summary>Total retry attempts across all executions.</summary>
        public int TotalRetries { get; set; }
        /// <summary>Total successful executions.</summary>
        public int TotalSuccesses { get; set; }
        /// <summary>Times circuit breaker prevented execution.</summary>
        public int TotalCircuitBreaks { get; set; }
        /// <summary>Current circuit breaker state.</summary>
        public CircuitState CircuitState { get; set; }
        /// <summary>Error counts by category.</summary>
        public Dictionary<ErrorCategory, int> ErrorCounts { get; set; } = new();
        /// <summary>Ratio of successes to total executions.</summary>
        public double SuccessRate { get; set; }
        /// <summary>Number of results in history.</summary>
        public int HistoryCount { get; set; }
    }
}
