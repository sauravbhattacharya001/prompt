namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class FallbackTierTests
    {
        [Fact]
        public void Defaults_AreCorrect()
        {
            var tier = new FallbackTier();
            Assert.Equal("", tier.Name);
            Assert.Null(tier.EndpointUri);
            Assert.Null(tier.ApiKey);
            Assert.Null(tier.Model);
            Assert.Null(tier.Timeout);
            Assert.Equal(1, tier.MaxRetries);
            Assert.Null(tier.Options);
            Assert.Equal(0, tier.Priority);
        }

        [Fact]
        public void Properties_CanBeSet()
        {
            var opts = new PromptOptions();
            var tier = new FallbackTier
            {
                Name = "gpt-4",
                EndpointUri = "https://example.com",
                ApiKey = "key-123",
                Model = "gpt-4-turbo",
                Timeout = TimeSpan.FromSeconds(30),
                MaxRetries = 3,
                Options = opts,
                Priority = 2,
            };

            Assert.Equal("gpt-4", tier.Name);
            Assert.Equal("https://example.com", tier.EndpointUri);
            Assert.Equal("key-123", tier.ApiKey);
            Assert.Equal("gpt-4-turbo", tier.Model);
            Assert.Equal(TimeSpan.FromSeconds(30), tier.Timeout);
            Assert.Equal(3, tier.MaxRetries);
            Assert.Same(opts, tier.Options);
            Assert.Equal(2, tier.Priority);
        }
    }

    public class FallbackResultTests
    {
        [Fact]
        public void Defaults_AreCorrect()
        {
            var result = new FallbackResult();
            Assert.Null(result.Response);
            Assert.False(result.Success);
            Assert.Null(result.RespondingTier);
            Assert.Equal(-1, result.TierIndex);
            Assert.Equal(0, result.FallbackCount);
            Assert.Equal(TimeSpan.Zero, result.TotalLatency);
            Assert.NotNull(result.Attempts);
            Assert.Empty(result.Attempts);
        }

        [Fact]
        public void Properties_CanBeSet()
        {
            var result = new FallbackResult
            {
                Response = "Hello",
                Success = true,
                RespondingTier = "gpt-4",
                TierIndex = 1,
                FallbackCount = 1,
                TotalLatency = TimeSpan.FromSeconds(2.5),
                Attempts = new List<TierAttempt>
                {
                    new TierAttempt { TierName = "gpt-4o", Success = false, Error = "timeout" },
                    new TierAttempt { TierName = "gpt-4", Success = true, Response = "Hello" },
                },
            };

            Assert.Equal("Hello", result.Response);
            Assert.True(result.Success);
            Assert.Equal("gpt-4", result.RespondingTier);
            Assert.Equal(1, result.TierIndex);
            Assert.Equal(2, result.Attempts.Count);
        }
    }

    public class TierAttemptTests
    {
        [Fact]
        public void Defaults_AreCorrect()
        {
            var attempt = new TierAttempt();
            Assert.Equal("", attempt.TierName);
            Assert.False(attempt.Success);
            Assert.Equal(TimeSpan.Zero, attempt.Latency);
            Assert.Null(attempt.Error);
            Assert.Null(attempt.Response);
        }
    }

    public class PromptFallbackChainConfigurationTests
    {
        [Fact]
        public void NewChain_HasZeroTiers()
        {
            var chain = new PromptFallbackChain();
            Assert.Equal(0, chain.TierCount);
            Assert.Empty(chain.TierNames);
        }

        [Fact]
        public void AddTier_IncrementsTierCount()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "tier-1" });

            Assert.Equal(1, chain.TierCount);
            Assert.Single(chain.TierNames);
            Assert.Equal("tier-1", chain.TierNames[0]);
        }

        [Fact]
        public void AddTier_MultipleTiers_Counted()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "primary" })
                .AddTier(new FallbackTier { Name = "secondary" })
                .AddTier(new FallbackTier { Name = "fallback" });

            Assert.Equal(3, chain.TierCount);
        }

        [Fact]
        public void AddTier_ReturnsSameInstance_ForFluentChaining()
        {
            var chain = new PromptFallbackChain();
            var result = chain.AddTier(new FallbackTier { Name = "t1" });
            Assert.Same(chain, result);
        }

        [Fact]
        public void AddTier_NullTier_Throws()
        {
            var chain = new PromptFallbackChain();
            Assert.Throws<ArgumentNullException>(() => chain.AddTier(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void AddTier_EmptyOrNullName_Throws(string? name)
        {
            var chain = new PromptFallbackChain();
            Assert.Throws<ArgumentException>(
                () => chain.AddTier(new FallbackTier { Name = name! }));
        }

        [Fact]
        public void AddTier_DuplicateName_Throws()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "primary" });

            Assert.Throws<ArgumentException>(
                () => chain.AddTier(new FallbackTier { Name = "primary" }));
        }

        [Fact]
        public void RemoveTier_ExistingName_ReturnsTrue()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "primary" })
                .AddTier(new FallbackTier { Name = "secondary" });

            Assert.True(chain.RemoveTier("primary"));
            Assert.Equal(1, chain.TierCount);
            Assert.Equal("secondary", chain.TierNames[0]);
        }

        [Fact]
        public void RemoveTier_NonExistentName_ReturnsFalse()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "primary" });

            Assert.False(chain.RemoveTier("nonexistent"));
            Assert.Equal(1, chain.TierCount);
        }

        [Fact]
        public void WithOptions_ReturnsSameInstance()
        {
            var chain = new PromptFallbackChain();
            var result = chain.WithOptions(new PromptOptions());
            Assert.Same(chain, result);
        }

        [Fact]
        public void WithQualityGate_ReturnsSameInstance()
        {
            var chain = new PromptFallbackChain();
            var result = chain.WithQualityGate((r, t) => true);
            Assert.Same(chain, result);
        }

        [Fact]
        public void WithMaxAttempts_ReturnsSameInstance()
        {
            var chain = new PromptFallbackChain();
            var result = chain.WithMaxAttempts(5);
            Assert.Same(chain, result);
        }

        [Fact]
        public void WithMaxAttempts_ZeroOrNegative_Throws()
        {
            var chain = new PromptFallbackChain();
            Assert.Throws<ArgumentOutOfRangeException>(() => chain.WithMaxAttempts(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => chain.WithMaxAttempts(-1));
        }

        [Fact]
        public void WithMaxAttempts_One_Succeeds()
        {
            var chain = new PromptFallbackChain();
            chain.WithMaxAttempts(1); // should not throw
        }
    }

    public class PromptFallbackChainTierOrderTests
    {
        [Fact]
        public void TierNames_SortedByPriority()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "low-priority", Priority = 10 })
                .AddTier(new FallbackTier { Name = "high-priority", Priority = 0 })
                .AddTier(new FallbackTier { Name = "mid-priority", Priority = 5 });

            Assert.Equal(
                new[] { "high-priority", "mid-priority", "low-priority" },
                chain.TierNames.ToArray());
        }

        [Fact]
        public void TierNames_SamePriority_InsertionOrder()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "first", Priority = 0 })
                .AddTier(new FallbackTier { Name = "second", Priority = 0 })
                .AddTier(new FallbackTier { Name = "third", Priority = 0 });

            Assert.Equal(
                new[] { "first", "second", "third" },
                chain.TierNames.ToArray());
        }

        [Fact]
        public void TierNames_MixedPriorityAndInsertion()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "D", Priority = 1 })
                .AddTier(new FallbackTier { Name = "A", Priority = 0 })
                .AddTier(new FallbackTier { Name = "C", Priority = 1 })
                .AddTier(new FallbackTier { Name = "B", Priority = 0 });

            Assert.Equal(
                new[] { "A", "B", "D", "C" },
                chain.TierNames.ToArray());
        }
    }

    public class PromptFallbackChainExecuteValidationTests
    {
        [Fact]
        public async Task ExecuteAsync_EmptyPrompt_Throws()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            await Assert.ThrowsAsync<ArgumentException>(
                () => chain.ExecuteAsync(""));
        }

        [Fact]
        public async Task ExecuteAsync_NullPrompt_Throws()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            await Assert.ThrowsAsync<ArgumentException>(
                () => chain.ExecuteAsync(null!));
        }

        [Fact]
        public async Task ExecuteAsync_WhitespacePrompt_Throws()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            await Assert.ThrowsAsync<ArgumentException>(
                () => chain.ExecuteAsync("   "));
        }

        [Fact]
        public async Task ExecuteAsync_NoTiers_Throws()
        {
            var chain = new PromptFallbackChain();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => chain.ExecuteAsync("Hello"));

            Assert.Contains("No tiers configured", ex.Message);
        }

        [Fact]
        public async Task ExecuteAsync_CancelledToken_Throws()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => chain.ExecuteAsync("Hello", cancellationToken: cts.Token));
        }
    }

    public class PromptFallbackChainJsonTests
    {
        [Fact]
        public void ToJson_EmptyChain_ValidJson()
        {
            var chain = new PromptFallbackChain();
            var json = chain.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal(0, doc.RootElement.GetProperty("tierCount").GetInt32());
            Assert.Equal(10, doc.RootElement.GetProperty("maxTotalAttempts").GetInt32());
            Assert.False(doc.RootElement.GetProperty("hasQualityGate").GetBoolean());
        }

        [Fact]
        public void ToJson_WithTiers_IncludesTierInfo()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier
                {
                    Name = "gpt-4",
                    Priority = 0,
                    Timeout = TimeSpan.FromSeconds(30),
                    MaxRetries = 2,
                    Model = "gpt-4-turbo",
                })
                .AddTier(new FallbackTier
                {
                    Name = "gpt-35",
                    Priority = 1,
                    MaxRetries = 1,
                });

            var json = chain.ToJson();
            var doc = JsonDocument.Parse(json);

            Assert.Equal(2, doc.RootElement.GetProperty("tierCount").GetInt32());
            var tiers = doc.RootElement.GetProperty("tiers");
            Assert.Equal(2, tiers.GetArrayLength());

            // First tier (priority 0) should be gpt-4
            Assert.Equal("gpt-4", tiers[0].GetProperty("name").GetString());
            Assert.Equal(30, tiers[0].GetProperty("timeout").GetDouble());
            Assert.Equal("gpt-4-turbo", tiers[0].GetProperty("model").GetString());

            // Second tier
            Assert.Equal("gpt-35", tiers[1].GetProperty("name").GetString());
        }

        [Fact]
        public void ToJson_WithQualityGate_FlagIsTrue()
        {
            var chain = new PromptFallbackChain()
                .WithQualityGate((r, t) => r.Length > 10);

            var json = chain.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("hasQualityGate").GetBoolean());
        }

        [Fact]
        public void ToJson_WithMaxAttempts_Serialised()
        {
            var chain = new PromptFallbackChain().WithMaxAttempts(5);
            var json = chain.ToJson();
            var doc = JsonDocument.Parse(json);
            Assert.Equal(5, doc.RootElement.GetProperty("maxTotalAttempts").GetInt32());
        }

        [Fact]
        public void ToJson_WithResult_IncludesResultSection()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            var result = new FallbackResult
            {
                Success = true,
                RespondingTier = "t1",
                TierIndex = 0,
                FallbackCount = 0,
                TotalLatency = TimeSpan.FromMilliseconds(150),
                Attempts = new List<TierAttempt>
                {
                    new TierAttempt
                    {
                        TierName = "t1",
                        Success = true,
                        Latency = TimeSpan.FromMilliseconds(150),
                    },
                },
            };

            var json = chain.ToJson(result);
            var doc = JsonDocument.Parse(json);

            var res = doc.RootElement.GetProperty("result");
            Assert.True(res.GetProperty("success").GetBoolean());
            Assert.Equal("t1", res.GetProperty("respondingTier").GetString());
            Assert.Equal(0, res.GetProperty("fallbackCount").GetInt32());
            Assert.Equal(150, res.GetProperty("totalLatencyMs").GetDouble(), 1);

            var attempts = res.GetProperty("attempts");
            Assert.Equal(1, attempts.GetArrayLength());
            Assert.Equal("t1", attempts[0].GetProperty("tier").GetString());
        }

        [Fact]
        public void ToJson_FailedResult_IncludesErrors()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            var result = new FallbackResult
            {
                Success = false,
                Attempts = new List<TierAttempt>
                {
                    new TierAttempt
                    {
                        TierName = "t1",
                        Success = false,
                        Error = "Connection refused",
                    },
                },
            };

            var json = chain.ToJson(result);
            var doc = JsonDocument.Parse(json);

            var res = doc.RootElement.GetProperty("result");
            Assert.False(res.GetProperty("success").GetBoolean());
            Assert.Equal("Connection refused",
                res.GetProperty("attempts")[0].GetProperty("error").GetString());
        }

        [Fact]
        public void ToJson_NullResult_NoResultSection()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            var json = chain.ToJson(null);
            var doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.TryGetProperty("result", out _));
        }

        [Fact]
        public void ToJson_TiersSortedByPriority()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "low", Priority = 10 })
                .AddTier(new FallbackTier { Name = "high", Priority = 0 });

            var json = chain.ToJson();
            var doc = JsonDocument.Parse(json);
            var tiers = doc.RootElement.GetProperty("tiers");
            Assert.Equal("high", tiers[0].GetProperty("name").GetString());
            Assert.Equal("low", tiers[1].GetProperty("name").GetString());
        }
    }

    public class PromptFallbackChainEdgeCaseTests
    {
        [Fact]
        public void AddTier_AfterRemove_AllowsSameName()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            chain.RemoveTier("t1");
            chain.AddTier(new FallbackTier { Name = "t1" }); // should not throw
            Assert.Equal(1, chain.TierCount);
        }

        [Fact]
        public void RemoveTier_AllTiers_LeavesEmpty()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "a" })
                .AddTier(new FallbackTier { Name = "b" });

            chain.RemoveTier("a");
            chain.RemoveTier("b");
            Assert.Equal(0, chain.TierCount);
        }

        [Fact]
        public void TierNames_IsReadOnly()
        {
            var chain = new PromptFallbackChain()
                .AddTier(new FallbackTier { Name = "t1" });

            var names = chain.TierNames;
            Assert.IsAssignableFrom<IReadOnlyList<string>>(names);
        }

        [Fact]
        public void FallbackResult_AttemptsCanBePopulated()
        {
            var result = new FallbackResult();
            result.Attempts.Add(new TierAttempt { TierName = "a", Success = false, Error = "fail" });
            result.Attempts.Add(new TierAttempt { TierName = "b", Success = true, Response = "ok" });
            Assert.Equal(2, result.Attempts.Count);
            Assert.False(result.Attempts[0].Success);
            Assert.True(result.Attempts[1].Success);
        }
    }
}
