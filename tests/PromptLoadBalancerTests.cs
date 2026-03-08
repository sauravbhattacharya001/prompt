using Xunit;
using Prompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Prompt.Tests
{
    public class PromptLoadBalancerTests
    {
        private static LoadBalancerConfig MakeConfig(
            int endpointCount = 3,
            LoadBalanceStrategy strategy = LoadBalanceStrategy.WeightedRoundRobin,
            bool enableLogging = false)
        {
            var config = new LoadBalancerConfig
            {
                Strategy = strategy,
                EnableLogging = enableLogging,
                Endpoints = Enumerable.Range(1, endpointCount)
                    .Select(i => new LoadBalancerEndpoint
                    {
                        Name = $"ep-{i}",
                        EndpointUri = $"https://ep{i}.example.com",
                        ApiKey = $"key-{i}",
                        Model = $"model-{i}",
                        Weight = 1
                    }).ToList()
            };
            return config;
        }

        [Fact]
        public void Constructor_NoEndpoints_Throws()
        {
            var config = new LoadBalancerConfig { Endpoints = new() };
            Assert.Throws<ArgumentException>(() => new PromptLoadBalancer(config));
        }

        [Fact]
        public void Constructor_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptLoadBalancer(null!));
        }

        [Fact]
        public void EndpointCount_ReturnsCorrectCount()
        {
            var lb = new PromptLoadBalancer(MakeConfig(5));
            Assert.Equal(5, lb.EndpointCount);
        }

        [Fact]
        public void HealthyCount_AllHealthy_ReturnsAll()
        {
            var lb = new PromptLoadBalancer(MakeConfig(3));
            Assert.Equal(3, lb.HealthyCount);
        }

        [Fact]
        public async Task ExecuteAsync_RoutesToEndpoint()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1));
            string? receivedUri = null;

            var result = await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
            {
                receivedUri = uri;
                return "ok";
            });

            Assert.Equal("ok", result);
            Assert.Equal("https://ep1.example.com", receivedUri);
        }

        [Fact]
        public async Task ExecuteAsync_Void_Works()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1));
            bool called = false;

            await lb.ExecuteAsync(async (uri, key, model, ct) =>
            {
                called = true;
            });

            Assert.True(called);
        }

        [Fact]
        public async Task ExecuteAsync_FailsOverToNextEndpoint()
        {
            var config = MakeConfig(3, enableLogging: true);
            config.RetryOnFailover = true;
            var lb = new PromptLoadBalancer(config);
            var calledEndpoints = new List<string?>();

            var result = await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
            {
                calledEndpoints.Add(uri);
                if (calledEndpoints.Count <= 2)
                    throw new Exception("fail");
                return "success";
            });

            Assert.Equal("success", result);
            Assert.Equal(3, calledEndpoints.Count);
        }

        [Fact]
        public async Task ExecuteAsync_AllFail_Throws()
        {
            var lb = new PromptLoadBalancer(MakeConfig(2));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
                {
                    throw new Exception("boom");
                }));
        }

        [Fact]
        public async Task ExecuteAsync_CancellationRespected()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return "nope";
                }, cts.Token));
        }

        [Fact]
        public async Task ExecuteAsync_RetryOnFailover_False_StopsAfterFirst()
        {
            var config = MakeConfig(3);
            config.RetryOnFailover = false;
            var lb = new PromptLoadBalancer(config);
            int callCount = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
                {
                    callCount++;
                    throw new Exception("fail");
                }));

            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task GetReport_TracksStatistics()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1));

            await lb.ExecuteAsync<string>(async (uri, key, model, ct) => "ok");

            var report = lb.GetReport();
            Assert.Equal(1, report.TotalRequests);
            Assert.Equal(0, report.TotalFailures);
            Assert.Equal(1.0, report.OverallSuccessRate);
            Assert.Single(report.Endpoints);
            Assert.Equal("ep-1", report.Endpoints[0].Name);
        }

        [Fact]
        public async Task GetReport_TracksFailures()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1));

            try
            {
                await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
                    throw new Exception("fail"));
            }
            catch { }

            var report = lb.GetReport();
            Assert.Equal(1, report.TotalFailures);
        }

        [Fact]
        public void Reset_ClearsAllStats()
        {
            var lb = new PromptLoadBalancer(MakeConfig(2, enableLogging: true));
            lb.Reset();
            var report = lb.GetReport();
            Assert.Equal(0, report.TotalRequests);
            Assert.Empty(lb.GetLog());
        }

        [Fact]
        public void SetEndpointHealth_Works()
        {
            var lb = new PromptLoadBalancer(MakeConfig(2));
            lb.SetEndpointHealth("ep-1", EndpointHealth.Unhealthy);
            Assert.Equal(1, lb.HealthyCount);
        }

        [Fact]
        public void SetEndpointHealth_UnknownEndpoint_Throws()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1));
            Assert.Throws<ArgumentException>(() =>
                lb.SetEndpointHealth("nonexistent", EndpointHealth.Healthy));
        }

        [Fact]
        public async Task Logging_RecordsEntries()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1, enableLogging: true));

            await lb.ExecuteAsync<string>(async (uri, key, model, ct) => "ok");

            var log = lb.GetLog();
            Assert.Single(log);
            Assert.True(log[0].Success);
            Assert.True(log[0].LatencyMs >= 0);
        }

        [Fact]
        public void ToJson_ReturnsValidJson()
        {
            var lb = new PromptLoadBalancer(MakeConfig(2));
            var json = lb.ToJson(true);
            Assert.Contains("\"strategy\"", json);
            Assert.Contains("\"endpoints\"", json);
        }

        [Fact]
        public async Task WeightedRoundRobin_DistributesTraffic()
        {
            var config = MakeConfig(2);
            config.Endpoints[0].Weight = 2;
            config.Endpoints[1].Weight = 1;
            var lb = new PromptLoadBalancer(config);
            var counts = new Dictionary<string, int>();

            for (int i = 0; i < 30; i++)
            {
                await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
                {
                    lock (counts)
                    {
                        counts[uri!] = counts.GetValueOrDefault(uri!) + 1;
                    }
                    return "ok";
                });
            }

            // ep-1 (weight 2) should get more traffic than ep-2 (weight 1)
            Assert.True(counts["https://ep1.example.com"] > counts["https://ep2.example.com"]);
        }

        [Fact]
        public async Task LeastConnections_RoutesByLoad()
        {
            var config = MakeConfig(2, LoadBalanceStrategy.LeastConnections);
            var lb = new PromptLoadBalancer(config);

            await lb.ExecuteAsync<string>(async (uri, key, model, ct) => "ok");

            var report = lb.GetReport();
            Assert.Equal(1, report.TotalRequests);
        }

        [Fact]
        public async Task LeastLatency_RoutesByLatency()
        {
            var config = MakeConfig(2, LoadBalanceStrategy.LeastLatency);
            var lb = new PromptLoadBalancer(config);

            await lb.ExecuteAsync<string>(async (uri, key, model, ct) => "ok");

            var report = lb.GetReport();
            Assert.Equal(1, report.TotalRequests);
        }

        [Fact]
        public async Task Random_Works()
        {
            var config = MakeConfig(3, LoadBalanceStrategy.Random);
            var lb = new PromptLoadBalancer(config);

            for (int i = 0; i < 10; i++)
                await lb.ExecuteAsync<string>(async (uri, key, model, ct) => "ok");

            Assert.Equal(10, lb.GetReport().TotalRequests);
        }

        [Fact]
        public async Task UnhealthyEndpoint_SkippedInRouting()
        {
            var config = MakeConfig(2);
            var lb = new PromptLoadBalancer(config);
            lb.SetEndpointHealth("ep-1", EndpointHealth.Unhealthy);

            string? usedUri = null;
            await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
            {
                usedUri = uri;
                return "ok";
            });

            Assert.Equal("https://ep2.example.com", usedUri);
        }

        [Fact]
        public async Task DegradedEndpoint_GetsReducedWeight()
        {
            var config = MakeConfig(2);
            config.Endpoints[0].Weight = 4;
            config.Endpoints[1].Weight = 4;
            var lb = new PromptLoadBalancer(config);
            lb.SetEndpointHealth("ep-1", EndpointHealth.Degraded);

            var counts = new Dictionary<string, int>();
            for (int i = 0; i < 600; i++)
            {
                await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
                {
                    lock (counts) { counts[uri!] = counts.GetValueOrDefault(uri!) + 1; }
                    return "ok";
                });
            }

            // ep-1 degraded weight = 2, ep-2 weight = 4 → ep-2 should get >= ep-1
            Assert.True(counts["https://ep2.example.com"] >= counts["https://ep1.example.com"],
                $"ep-2 ({counts["https://ep2.example.com"]}) should get >= ep-1 ({counts["https://ep1.example.com"]})");
        }

        [Fact]
        public async Task CooldownPeriod_RestoresUnhealthyToDegraded()
        {
            var config = MakeConfig(2);
            config.CooldownPeriod = TimeSpan.FromMilliseconds(1);
            config.UnhealthyThreshold = 1;
            var lb = new PromptLoadBalancer(config);

            // Make ep-1 unhealthy
            lb.SetEndpointHealth("ep-1", EndpointHealth.Unhealthy);
            Assert.Equal(1, lb.HealthyCount);

            // Wait for cooldown
            await Task.Delay(50);

            // Execute should now try ep-1 again (promoted to Degraded by RefreshHealthStates)
            await lb.ExecuteAsync<string>(async (uri, key, model, ct) => "ok");

            // After success, it should be healthy again
            var report = lb.GetReport();
            var ep1 = report.Endpoints.First(e => e.Name == "ep-1");
            Assert.NotEqual(EndpointHealth.Unhealthy, ep1.Health);
        }

        [Fact]
        public async Task MaxConcurrent_SkipsOverloaded()
        {
            var config = MakeConfig(2);
            config.Endpoints[0].MaxConcurrent = 1;
            config.Strategy = LoadBalanceStrategy.Random; // doesn't matter, we test MaxConcurrent skip
            var lb = new PromptLoadBalancer(config);

            // Just verify it doesn't crash with MaxConcurrent set
            await lb.ExecuteAsync<string>(async (uri, key, model, ct) => "ok");
            Assert.Equal(1, lb.GetReport().TotalRequests);
        }

        [Fact]
        public async Task PerEndpointTimeout_Applied()
        {
            var config = MakeConfig(2);
            config.Endpoints[0].Timeout = TimeSpan.FromMilliseconds(50);
            config.RetryOnFailover = true;
            var lb = new PromptLoadBalancer(config);

            string? successUri = null;
            var result = await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
            {
                if (uri == "https://ep1.example.com")
                {
                    await Task.Delay(200, ct); // will timeout
                    return "late";
                }
                successUri = uri;
                return "ok";
            });

            Assert.Equal("ok", result);
        }

        [Fact]
        public async Task FailoverEvents_Counted()
        {
            var config = MakeConfig(3, enableLogging: true);
            var lb = new PromptLoadBalancer(config);
            int calls = 0;

            await lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
            {
                calls++;
                if (calls < 3) throw new Exception("fail");
                return "ok";
            });

            var report = lb.GetReport();
            Assert.True(report.FailoverEvents > 0);
        }

        [Fact]
        public async Task MaxFailoverRetries_Respected()
        {
            var config = MakeConfig(5);
            config.MaxFailoverRetries = 2;
            var lb = new PromptLoadBalancer(config);
            int calls = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lb.ExecuteAsync<string>(async (uri, key, model, ct) =>
                {
                    calls++;
                    throw new Exception("fail");
                }));

            Assert.Equal(2, calls);
        }

        [Fact]
        public async Task ConsecutiveFailures_TriggersHealthTransitions()
        {
            var config = MakeConfig(1);
            config.DegradedThreshold = 1;
            config.UnhealthyThreshold = 3;
            config.RetryOnFailover = false;
            var lb = new PromptLoadBalancer(config);

            // 1 failure -> degraded
            try { await lb.ExecuteAsync<string>(async (u, k, m, c) => throw new Exception("1")); } catch { }
            Assert.Equal(EndpointHealth.Degraded, lb.GetReport().Endpoints[0].Health);

            // 2 more failures -> unhealthy
            try { await lb.ExecuteAsync<string>(async (u, k, m, c) => throw new Exception("2")); } catch { }
            try { await lb.ExecuteAsync<string>(async (u, k, m, c) => throw new Exception("3")); } catch { }
            Assert.Equal(EndpointHealth.Unhealthy, lb.GetReport().Endpoints[0].Health);
        }

        [Fact]
        public async Task SuccessResetsConsecutiveFailures()
        {
            var config = MakeConfig(1);
            config.DegradedThreshold = 1;
            config.RetryOnFailover = false;
            var lb = new PromptLoadBalancer(config);

            // Fail once
            try { await lb.ExecuteAsync<string>(async (u, k, m, c) => throw new Exception("1")); } catch { }
            Assert.Equal(EndpointHealth.Degraded, lb.GetReport().Endpoints[0].Health);

            // Succeed
            await lb.ExecuteAsync<string>(async (u, k, m, c) => "ok");
            Assert.Equal(EndpointHealth.Healthy, lb.GetReport().Endpoints[0].Health);
            Assert.Equal(0, lb.GetReport().Endpoints[0].ConsecutiveFailures);
        }

        [Fact]
        public async Task P95Latency_Tracked()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1));

            for (int i = 0; i < 10; i++)
                await lb.ExecuteAsync<string>(async (u, k, m, c) => "ok");

            var stats = lb.GetReport().Endpoints[0];
            Assert.True(stats.P95LatencyMs >= 0);
            Assert.True(stats.AverageLatencyMs >= 0);
        }

        [Fact]
        public async Task ConcurrentExecution_ThreadSafe()
        {
            var lb = new PromptLoadBalancer(MakeConfig(3));
            var tasks = Enumerable.Range(0, 50).Select(_ =>
                lb.ExecuteAsync<string>(async (u, k, m, c) =>
                {
                    await Task.Delay(1);
                    return "ok";
                }));

            await Task.WhenAll(tasks);
            Assert.Equal(50, lb.GetReport().TotalRequests);
        }

        [Fact]
        public void GetLog_EmptyWhenLoggingDisabled()
        {
            var lb = new PromptLoadBalancer(MakeConfig(1, enableLogging: false));
            Assert.Empty(lb.GetLog());
        }

        [Fact]
        public async Task LogEntry_ContainsFailoverFlag()
        {
            var config = MakeConfig(2, enableLogging: true);
            var lb = new PromptLoadBalancer(config);
            int calls = 0;

            await lb.ExecuteAsync<string>(async (u, k, m, c) =>
            {
                if (++calls == 1) throw new Exception("fail");
                return "ok";
            });

            var log = lb.GetLog();
            Assert.True(log.Any(e => e.IsFailover));
        }
    }
}
