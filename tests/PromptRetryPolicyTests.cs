namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptRetryPolicyTests
    {
        [Fact]
        public void DefaultConfig_CreatesWorkingPolicy()
        {
            var policy = new PromptRetryPolicy();
            var stats = policy.GetStats();
            Assert.Equal(0, stats.TotalExecutions);
            Assert.Equal(CircuitState.Closed, stats.CircuitState);
        }

        [Fact]
        public void CustomConfig_AppliesSettings()
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = 5,
                BaseDelay = TimeSpan.FromSeconds(2),
                Backoff = BackoffStrategy.Linear,
                EnableJitter = false
            };
            var policy = new PromptRetryPolicy(config);
            var delay = policy.CalculateDelay(3, ErrorCategory.Unknown);
            Assert.Equal(6000, delay.TotalMilliseconds);
        }

        [Fact]
        public void NegativeMaxRetries_ClampedToZero()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = -5 });
            var result = policy.Execute(attempt => (false, "fail"));
            Assert.Single(result.Attempts);
        }

        [Theory]
        [InlineData("429 Too Many Requests", ErrorCategory.RateLimit)]
        [InlineData("rate limit exceeded", ErrorCategory.RateLimit)]
        [InlineData("500 Internal Server Error", ErrorCategory.ServerError)]
        [InlineData("502 Bad Gateway", ErrorCategory.ServerError)]
        [InlineData("Connection timeout", ErrorCategory.Timeout)]
        [InlineData("ECONNREFUSED", ErrorCategory.Timeout)]
        [InlineData("401 Unauthorized", ErrorCategory.AuthError)]
        [InlineData("Invalid API key", ErrorCategory.AuthError)]
        [InlineData("content_filter triggered", ErrorCategory.ContentFilter)]
        [InlineData("model_overloaded", ErrorCategory.Overloaded)]
        [InlineData("something weird", ErrorCategory.Unknown)]
        [InlineData("", ErrorCategory.Unknown)]
        public void ClassifyError_CorrectCategories(string msg, ErrorCategory expected)
        {
            var policy = new PromptRetryPolicy();
            Assert.Equal(expected, policy.ClassifyError(msg));
        }

        [Fact]
        public void ExponentialBackoff_DoublesEachAttempt()
        {
            var config = new RetryPolicyConfig
            {
                BaseDelay = TimeSpan.FromSeconds(1),
                Backoff = BackoffStrategy.Exponential,
                EnableJitter = false
            };
            var policy = new PromptRetryPolicy(config);
            Assert.Equal(1000, policy.CalculateDelay(1, ErrorCategory.Unknown).TotalMilliseconds);
            Assert.Equal(2000, policy.CalculateDelay(2, ErrorCategory.Unknown).TotalMilliseconds);
            Assert.Equal(4000, policy.CalculateDelay(3, ErrorCategory.Unknown).TotalMilliseconds);
        }

        [Fact]
        public void FixedBackoff_SameDelayEachTime()
        {
            var config = new RetryPolicyConfig
            {
                BaseDelay = TimeSpan.FromSeconds(1),
                Backoff = BackoffStrategy.Fixed,
                EnableJitter = false
            };
            var policy = new PromptRetryPolicy(config);
            Assert.Equal(1000, policy.CalculateDelay(1, ErrorCategory.Unknown).TotalMilliseconds);
            Assert.Equal(1000, policy.CalculateDelay(3, ErrorCategory.Unknown).TotalMilliseconds);
        }

        [Fact]
        public void FibonacciBackoff_FollowsSequence()
        {
            var config = new RetryPolicyConfig
            {
                BaseDelay = TimeSpan.FromSeconds(1),
                Backoff = BackoffStrategy.Fibonacci,
                EnableJitter = false
            };
            var policy = new PromptRetryPolicy(config);
            Assert.Equal(1000, policy.CalculateDelay(1, ErrorCategory.Unknown).TotalMilliseconds);
            Assert.Equal(3000, policy.CalculateDelay(3, ErrorCategory.Unknown).TotalMilliseconds);
            Assert.Equal(8000, policy.CalculateDelay(5, ErrorCategory.Unknown).TotalMilliseconds);
        }

        [Fact]
        public void MaxDelay_CapsBackoff()
        {
            var config = new RetryPolicyConfig
            {
                BaseDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(5),
                Backoff = BackoffStrategy.Exponential,
                EnableJitter = false
            };
            var policy = new PromptRetryPolicy(config);
            Assert.Equal(5000, policy.CalculateDelay(10, ErrorCategory.Unknown).TotalMilliseconds);
        }

        [Fact]
        public void Jitter_ModifiesDelay()
        {
            var config = new RetryPolicyConfig
            {
                BaseDelay = TimeSpan.FromSeconds(1),
                Backoff = BackoffStrategy.Fixed,
                EnableJitter = true,
                JitterFactor = 0.5
            };
            var policy = new PromptRetryPolicy(config);
            var delays = new HashSet<double>();
            for (int i = 0; i < 20; i++)
                delays.Add(policy.CalculateDelay(1, ErrorCategory.Unknown).TotalMilliseconds);
            Assert.True(delays.Count > 1, "Jitter should produce varied delays");
        }

        [Fact]
        public void ZeroAttempt_ReturnsZeroDelay()
        {
            var policy = new PromptRetryPolicy();
            Assert.Equal(TimeSpan.Zero, policy.CalculateDelay(0, ErrorCategory.Unknown));
        }

        [Fact]
        public void Execute_SuccessOnFirstTry()
        {
            var policy = new PromptRetryPolicy();
            var result = policy.Execute(attempt => (true, "hello"));
            Assert.True(result.Succeeded);
            Assert.Equal("hello", result.Result);
            Assert.Single(result.Attempts);
            Assert.Equal(0, result.RetryCount);
        }

        [Fact]
        public void Execute_SuccessAfterRetries()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 3 });
            var result = policy.Execute(attempt =>
                attempt < 2 ? (false, "500 server error") : (true, "ok"));
            Assert.True(result.Succeeded);
            Assert.Equal("ok", result.Result);
            Assert.Equal(3, result.Attempts.Count);
            Assert.Equal(2, result.RetryCount);
        }

        [Fact]
        public void Execute_ExhaustsRetries()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 2 });
            var result = policy.Execute(attempt => (false, "503 unavailable"));
            Assert.False(result.Succeeded);
            Assert.Equal(3, result.Attempts.Count);
            Assert.Contains("503", result.FinalError!);
        }

        [Fact]
        public void Execute_StopsOnNonRetryableError()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 5 });
            var result = policy.Execute(attempt => (false, "401 Unauthorized"));
            Assert.False(result.Succeeded);
            Assert.Single(result.Attempts);
            Assert.Contains("Non-retryable", result.FinalError!);
        }

        [Fact]
        public void Execute_HandlesExceptions()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 1 });
            var result = policy.Execute(attempt =>
            {
                if (attempt == 0) throw new InvalidOperationException("network timeout");
                return (true, "recovered");
            });
            Assert.True(result.Succeeded);
            Assert.Equal(2, result.Attempts.Count);
        }

        [Fact]
        public void CategoryPolicy_OverridesBaseDelay()
        {
            var config = new RetryPolicyConfig
            {
                BaseDelay = TimeSpan.FromSeconds(1),
                Backoff = BackoffStrategy.Fixed,
                EnableJitter = false,
                CategoryPolicies = new Dictionary<ErrorCategory, ErrorCategoryPolicy>
                {
                    [ErrorCategory.RateLimit] = new ErrorCategoryPolicy
                    {
                        ShouldRetry = true,
                        BaseDelay = TimeSpan.FromSeconds(5)
                    }
                }
            };
            var policy = new PromptRetryPolicy(config);
            Assert.Equal(1000, policy.CalculateDelay(1, ErrorCategory.ServerError).TotalMilliseconds);
            Assert.Equal(5000, policy.CalculateDelay(1, ErrorCategory.RateLimit).TotalMilliseconds);
        }

        [Fact]
        public void CategoryPolicy_MaxRetriesPerCategory()
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = 10,
                CategoryPolicies = new Dictionary<ErrorCategory, ErrorCategoryPolicy>
                {
                    [ErrorCategory.ServerError] = new ErrorCategoryPolicy
                    {
                        ShouldRetry = true,
                        MaxRetries = 2
                    }
                }
            };
            var policy = new PromptRetryPolicy(config);
            var result = policy.Execute(attempt => (false, "500 server error"));
            Assert.True(result.Attempts.Count <= 3);
            Assert.Contains("Max retries for ServerError", result.FinalError!);
        }

        [Fact]
        public void IsRetryable_ReflectsConfig()
        {
            var policy = new PromptRetryPolicy();
            Assert.False(policy.IsRetryable(ErrorCategory.AuthError));
            Assert.False(policy.IsRetryable(ErrorCategory.BadRequest));
            Assert.False(policy.IsRetryable(ErrorCategory.ContentFilter));
            Assert.True(policy.IsRetryable(ErrorCategory.RateLimit));
            Assert.True(policy.IsRetryable(ErrorCategory.ServerError));
            Assert.True(policy.IsRetryable(ErrorCategory.Timeout));
        }

        [Fact]
        public void CircuitBreaker_TripsAfterThreshold()
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = 0,
                EnableCircuitBreaker = true,
                CircuitBreakerThreshold = 3,
                CircuitBreakerWindow = TimeSpan.FromMinutes(5)
            };
            var policy = new PromptRetryPolicy(config);
            for (int i = 0; i < 3; i++)
                policy.Execute(attempt => (false, "500 error"));
            Assert.Equal(CircuitState.Open, policy.CircuitState);

            var result = policy.Execute(attempt => (true, "should not run"));
            Assert.True(result.CircuitBreakerTripped);
            Assert.False(result.Succeeded);
        }

        [Fact]
        public void CircuitBreaker_Disabled()
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = 0,
                EnableCircuitBreaker = false
            };
            var policy = new PromptRetryPolicy(config);
            for (int i = 0; i < 20; i++)
                policy.Execute(attempt => (false, "500 error"));
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void CircuitBreaker_Reset()
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = 0,
                CircuitBreakerThreshold = 2
            };
            var policy = new PromptRetryPolicy(config);
            policy.Execute(attempt => (false, "500"));
            policy.Execute(attempt => (false, "500"));
            Assert.Equal(CircuitState.Open, policy.CircuitState);
            policy.ResetCircuitBreaker();
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void Stats_TrackCorrectly()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 1 });
            policy.Execute(attempt => (true, "ok"));
            policy.Execute(attempt => (false, "500 error"));
            policy.Execute(attempt => attempt == 1 ? (true, "ok") : (false, "timeout"));

            var stats = policy.GetStats();
            Assert.Equal(3, stats.TotalExecutions);
            Assert.Equal(2, stats.TotalSuccesses);
        }

        [Fact]
        public void History_LimitWorks()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 0 });
            for (int i = 0; i < 10; i++)
                policy.Execute(attempt => (true, "ok"));
            Assert.Equal(10, policy.GetHistory().Count);
            Assert.Equal(3, policy.GetHistory(3).Count);
        }

        [Fact]
        public void ErrorCounts_TrackedByCategory()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 0 });
            policy.Execute(attempt => (false, "500 server error"));
            policy.Execute(attempt => (false, "500 server error"));
            policy.Execute(attempt => (false, "connection timeout"));

            var stats = policy.GetStats();
            Assert.Equal(2, stats.ErrorCounts[ErrorCategory.ServerError]);
            Assert.Equal(1, stats.ErrorCounts[ErrorCategory.Timeout]);
        }

        [Fact]
        public void Reset_ClearsEverything()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 0 });
            policy.Execute(attempt => (true, "ok"));
            policy.Execute(attempt => (false, "500"));
            policy.Reset();

            var stats = policy.GetStats();
            Assert.Equal(0, stats.TotalExecutions);
            Assert.Empty(policy.GetHistory());
        }

        [Fact]
        public void GenerateReport_IncludesKeyInfo()
        {
            var policy = new PromptRetryPolicy(new RetryPolicyConfig { MaxRetries = 1 });
            policy.Execute(attempt => (true, "ok"));
            policy.Execute(attempt => (false, "500 error"));

            var report = policy.GenerateReport();
            Assert.Contains("Retry Policy Report", report);
            Assert.Contains("Total Executions: 2", report);
        }

        [Fact]
        public void ExportConfig_ReturnsValidJson()
        {
            var policy = new PromptRetryPolicy();
            var json = policy.ExportConfig();
            Assert.Contains("maxRetries", json);
            Assert.Contains("backoff", json);
            System.Text.Json.JsonDocument.Parse(json);
        }

        [Fact]
        public void Execute_ActuallyWaitsBetweenRetries()
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = 1,
                BaseDelay = TimeSpan.FromMilliseconds(200),
                Backoff = BackoffStrategy.Fixed,
                EnableJitter = false
            };
            var policy = new PromptRetryPolicy(config);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = policy.Execute(attempt =>
                attempt == 0 ? (false, "500 error") : (true, "ok"));
            sw.Stop();
            Assert.True(result.Succeeded);
            // Should have waited at least ~200ms for the retry delay
            Assert.True(sw.ElapsedMilliseconds >= 150,
                $"Expected >= 150ms delay but only {sw.ElapsedMilliseconds}ms elapsed");
        }

        [Fact]
        public void Execute_DelayScalesWithBackoff()
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = 2,
                BaseDelay = TimeSpan.FromMilliseconds(100),
                Backoff = BackoffStrategy.Exponential,
                EnableJitter = false
            };
            var policy = new PromptRetryPolicy(config);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = policy.Execute(attempt => (false, "503 unavailable"));
            sw.Stop();
            Assert.False(result.Succeeded);
            // Attempt 1: 100ms, Attempt 2: 200ms = 300ms total delay minimum
            Assert.True(sw.ElapsedMilliseconds >= 250,
                $"Expected >= 250ms total delay but only {sw.ElapsedMilliseconds}ms elapsed");
        }
    }
}
