namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class PromptRateLimiterTests
{
    // ── Profile Management ──

    [Fact]
    public void AddProfile_ValidProfile_Succeeds()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "test-model", RequestsPerMinute = 100 });
        Assert.Contains("test-model", limiter.GetProfileNames());
    }

    [Fact]
    public void AddProfile_NullProfile_ThrowsArgumentNull()
    {
        var limiter = new PromptRateLimiter();
        Assert.Throws<ArgumentNullException>(() => limiter.AddProfile(null!));
    }

    [Fact]
    public void AddProfile_EmptyName_ThrowsArgument()
    {
        var limiter = new PromptRateLimiter();
        Assert.Throws<ArgumentException>(() =>
            limiter.AddProfile(new RateLimitProfile { Name = "" }));
    }

    [Fact]
    public void AddProfile_WhitespaceName_ThrowsArgument()
    {
        var limiter = new PromptRateLimiter();
        Assert.Throws<ArgumentException>(() =>
            limiter.AddProfile(new RateLimitProfile { Name = "   " }));
    }

    [Fact]
    public void AddProfile_DuplicateName_OverwritesExisting()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "model", RequestsPerMinute = 10 });
        limiter.AddProfile(new RateLimitProfile { Name = "model", RequestsPerMinute = 50 });
        var profile = limiter.GetProfile("model");
        Assert.Equal(50, profile!.RequestsPerMinute);
    }

    [Fact]
    public void RemoveProfile_Existing_ReturnsTrue()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "model" });
        Assert.True(limiter.RemoveProfile("model"));
        Assert.DoesNotContain("model", limiter.GetProfileNames());
    }

    [Fact]
    public void RemoveProfile_NonExistent_ReturnsFalse()
    {
        var limiter = new PromptRateLimiter();
        Assert.False(limiter.RemoveProfile("ghost"));
    }

    [Fact]
    public void RemoveProfile_NullOrEmpty_ReturnsFalse()
    {
        var limiter = new PromptRateLimiter();
        Assert.False(limiter.RemoveProfile(""));
        Assert.False(limiter.RemoveProfile(null!));
    }

    [Fact]
    public void GetProfileNames_Empty_ReturnsEmptyList()
    {
        var limiter = new PromptRateLimiter();
        Assert.Empty(limiter.GetProfileNames());
    }

    [Fact]
    public void GetProfileNames_MultipleProfiles_ReturnsAll()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "a" });
        limiter.AddProfile(new RateLimitProfile { Name = "b" });
        limiter.AddProfile(new RateLimitProfile { Name = "c" });
        Assert.Equal(3, limiter.GetProfileNames().Count);
    }

    [Fact]
    public void GetProfile_Existing_ReturnsProfile()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "gpt-4",
            RequestsPerMinute = 40,
            TokensPerMinute = 40_000,
            MaxConcurrent = 5
        });
        var profile = limiter.GetProfile("gpt-4");
        Assert.NotNull(profile);
        Assert.Equal(40, profile.RequestsPerMinute);
        Assert.Equal(40_000, profile.TokensPerMinute);
        Assert.Equal(5, profile.MaxConcurrent);
    }

    [Fact]
    public void GetProfile_NonExistent_ReturnsNull()
    {
        var limiter = new PromptRateLimiter();
        Assert.Null(limiter.GetProfile("ghost"));
    }

    [Fact]
    public void GetProfile_NullOrEmpty_ReturnsNull()
    {
        var limiter = new PromptRateLimiter();
        Assert.Null(limiter.GetProfile(""));
        Assert.Null(limiter.GetProfile(null!));
    }

    [Fact]
    public void GetProfile_CaseInsensitive()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "GPT-4" });
        Assert.NotNull(limiter.GetProfile("gpt-4"));
        Assert.NotNull(limiter.GetProfile("GPT-4"));
    }

    // ── WithDefaults ──

    [Fact]
    public void WithDefaults_HasExpectedProfiles()
    {
        var limiter = PromptRateLimiter.WithDefaults();
        var names = limiter.GetProfileNames();
        Assert.Contains("gpt-3.5-turbo", names);
        Assert.Contains("gpt-4", names);
        Assert.Contains("gpt-4-turbo", names);
        Assert.Contains("gpt-4o", names);
        Assert.Contains("claude-3-opus", names);
        Assert.Contains("claude-3-sonnet", names);
        Assert.Equal(6, names.Count);
    }

    [Fact]
    public void WithDefaults_Gpt4_HasCorrectLimits()
    {
        var limiter = PromptRateLimiter.WithDefaults();
        var profile = limiter.GetProfile("gpt-4");
        Assert.Equal(500, profile!.RequestsPerMinute);
        Assert.Equal(40_000, profile.TokensPerMinute);
        Assert.Equal(20, profile.MaxConcurrent);
    }

    // ── TryAcquire ──

    [Fact]
    public void TryAcquire_FirstRequest_IsPermitted()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m", RequestsPerMinute = 10 });
        var result = limiter.TryAcquire("m");
        Assert.True(result.Permitted);
        Assert.Equal(0, result.WaitMs);
        Assert.Null(result.DenialReason);
        Assert.Equal("m", result.ProfileName);
    }

    [Fact]
    public void TryAcquire_UnknownProfile_Denied()
    {
        var limiter = new PromptRateLimiter();
        var result = limiter.TryAcquire("nonexistent");
        Assert.False(result.Permitted);
        Assert.Contains("Unknown profile", result.DenialReason);
    }

    [Fact]
    public void TryAcquire_EmptyProfileName_Denied()
    {
        var limiter = new PromptRateLimiter();
        var result = limiter.TryAcquire("");
        Assert.False(result.Permitted);
        Assert.Contains("required", result.DenialReason);
    }

    [Fact]
    public void TryAcquire_NullProfileName_Denied()
    {
        var limiter = new PromptRateLimiter();
        var result = limiter.TryAcquire(null!);
        Assert.False(result.Permitted);
    }

    [Fact]
    public void TryAcquire_ExceedsRPM_Denied()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 3,
            MaxConcurrent = 100
        });

        // Acquire 3 (the limit)
        Assert.True(limiter.TryAcquire("m").Permitted);
        limiter.RecordCompletion("m");
        Assert.True(limiter.TryAcquire("m").Permitted);
        limiter.RecordCompletion("m");
        Assert.True(limiter.TryAcquire("m").Permitted);
        limiter.RecordCompletion("m");

        // 4th should be denied
        var result = limiter.TryAcquire("m");
        Assert.False(result.Permitted);
        Assert.Contains("RPM", result.DenialReason);
    }

    [Fact]
    public void TryAcquire_ExceedsConcurrent_Denied()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 100,
            MaxConcurrent = 2
        });

        Assert.True(limiter.TryAcquire("m").Permitted);
        Assert.True(limiter.TryAcquire("m").Permitted);
        var result = limiter.TryAcquire("m");
        Assert.False(result.Permitted);
        Assert.Contains("Concurrent", result.DenialReason);
    }

    [Fact]
    public void TryAcquire_ExceedsTPM_Denied()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 100,
            TokensPerMinute = 1000,
            MaxConcurrent = 100
        });

        Assert.True(limiter.TryAcquire("m", estimatedTokens: 600).Permitted);
        limiter.RecordCompletion("m");

        // Next request would push over 1000 TPM
        var result = limiter.TryAcquire("m", estimatedTokens: 500);
        Assert.False(result.Permitted);
        Assert.Contains("TPM", result.DenialReason);
    }

    [Fact]
    public void TryAcquire_WithZeroTokens_SkipsTPMCheck()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 100,
            TokensPerMinute = 100,
            MaxConcurrent = 100
        });

        // With 0 tokens, TPM check is skipped
        Assert.True(limiter.TryAcquire("m", estimatedTokens: 0).Permitted);
        limiter.RecordCompletion("m");
    }

    [Fact]
    public void TryAcquire_ReportsCurrentState()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 100,
            MaxConcurrent = 100
        });

        var r1 = limiter.TryAcquire("m", estimatedTokens: 200);
        Assert.Equal(1, r1.CurrentRequests);
        Assert.Equal(200, r1.CurrentTokens);
        Assert.Equal(1, r1.ConcurrentRequests);

        var r2 = limiter.TryAcquire("m", estimatedTokens: 300);
        Assert.Equal(2, r2.CurrentRequests);
        Assert.Equal(500, r2.CurrentTokens);
        Assert.Equal(2, r2.ConcurrentRequests);
    }

    [Fact]
    public void TryAcquire_AfterCompletion_ReleasesConcurrency()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 100,
            MaxConcurrent = 1
        });

        Assert.True(limiter.TryAcquire("m").Permitted);
        Assert.False(limiter.TryAcquire("m").Permitted); // concurrent limit

        limiter.RecordCompletion("m");
        Assert.True(limiter.TryAcquire("m").Permitted); // slot freed
    }

    // ── RecordCompletion ──

    [Fact]
    public void RecordCompletion_UnknownProfile_NoThrow()
    {
        var limiter = new PromptRateLimiter();
        limiter.RecordCompletion("ghost"); // should not throw
    }

    [Fact]
    public void RecordCompletion_EmptyProfile_NoThrow()
    {
        var limiter = new PromptRateLimiter();
        limiter.RecordCompletion(""); // should not throw
    }

    [Fact]
    public void RecordCompletion_WithActualTokens_UpdatesTotals()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m" });

        limiter.TryAcquire("m", estimatedTokens: 100);
        limiter.RecordCompletion("m", actualTokens: 80);

        var usage = limiter.GetUsage("m");
        Assert.Equal(1, usage!.CompletedRequests);
        // TotalTokens must reflect the actual value (80), NOT estimated + actual (180)
        Assert.Equal(80, usage.TotalTokens);
        // Sliding window should also show only the actual tokens
        Assert.Equal(80, usage.WindowTokens);
    }

    [Fact]
    public void RecordCompletion_NeverGoesBelowZero()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m" });

        // Complete without acquiring — should not go negative
        limiter.RecordCompletion("m");
        var usage = limiter.GetUsage("m");
        Assert.Equal(0, usage!.ConcurrentRequests);
    }

    [Fact]
    public void RecordCompletion_ActualTokensReplacesEstimate_NoPrematureDenial()
    {
        // Regression: RecordCompletion used to ADD actual tokens on top of
        // the estimate, causing TotalTokens and WindowTokens to be inflated.
        // This led to premature TPM denials.
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            TokensPerMinute = 200,
            RequestsPerMinute = 100,
            MaxConcurrent = 10
        });

        // Request 1: estimate 100, actual 80
        var r1 = limiter.TryAcquire("m", estimatedTokens: 100);
        Assert.True(r1.Permitted);
        limiter.RecordCompletion("m", actualTokens: 80);

        // Request 2: estimate 100 — should succeed because window has 80, not 180
        var r2 = limiter.TryAcquire("m", estimatedTokens: 100);
        Assert.True(r2.Permitted, $"Should be permitted: window={r2.CurrentTokens}, limit=200");
        limiter.RecordCompletion("m", actualTokens: 90);

        var usage = limiter.GetUsage("m");
        // Total should be 80 + 90 = 170, not 100 + 80 + 100 + 90 = 370
        Assert.Equal(170, usage!.TotalTokens);
    }

    [Fact]
    public void RecordCompletion_NoEstimate_ThenActual_AddsCorrectly()
    {
        // When TryAcquire was called with 0 estimated tokens,
        // RecordCompletion with actualTokens should add a new entry
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m" });

        limiter.TryAcquire("m", estimatedTokens: 0);
        limiter.RecordCompletion("m", actualTokens: 50);

        var usage = limiter.GetUsage("m");
        Assert.Equal(50, usage!.TotalTokens);
        Assert.Equal(50, usage.WindowTokens);
    }

    // ── WaitAndAcquireAsync ──

    [Fact]
    public async Task WaitAndAcquireAsync_FirstRequest_ReturnsImmediately()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 10,
            MaxConcurrent = 10
        });

        var result = await limiter.WaitAndAcquireAsync("m", estimatedTokens: 100);
        Assert.True(result.Permitted);
    }

    [Fact]
    public async Task WaitAndAcquireAsync_CancelledToken_ThrowsOperationCancelled()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 1,
            MaxConcurrent = 1
        });

        // Fill up
        limiter.TryAcquire("m");

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            limiter.WaitAndAcquireAsync("m", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task WaitAndAcquireAsync_Timeout_ThrowsTimeoutException()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 1,
            MaxConcurrent = 1
        });

        limiter.TryAcquire("m");

        await Assert.ThrowsAsync<TimeoutException>(() =>
            limiter.WaitAndAcquireAsync("m", maxWaitMs: 200));
    }

    // ── GetUsage ──

    [Fact]
    public void GetUsage_UnknownProfile_ReturnsNull()
    {
        var limiter = new PromptRateLimiter();
        Assert.Null(limiter.GetUsage("ghost"));
    }

    [Fact]
    public void GetUsage_EmptyProfile_ReturnsNull()
    {
        var limiter = new PromptRateLimiter();
        Assert.Null(limiter.GetUsage(""));
    }

    [Fact]
    public void GetUsage_FreshProfile_AllZeros()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m", RequestsPerMinute = 60 });
        var usage = limiter.GetUsage("m");
        Assert.NotNull(usage);
        Assert.Equal(0, usage.TotalRequests);
        Assert.Equal(0, usage.TotalTokens);
        Assert.Equal(0, usage.DeniedRequests);
        Assert.Equal(0, usage.CompletedRequests);
        Assert.Equal(0, usage.WindowRequests);
        Assert.Equal(0, usage.WindowTokens);
        Assert.Equal(0, usage.ConcurrentRequests);
    }

    [Fact]
    public void GetUsage_AfterRequests_TracksCorrectly()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 100,
            TokensPerMinute = 100_000,
            MaxConcurrent = 100
        });

        limiter.TryAcquire("m", estimatedTokens: 500);
        limiter.RecordCompletion("m", actualTokens: 400);

        var usage = limiter.GetUsage("m");
        Assert.Equal(1, usage!.TotalRequests);
        Assert.Equal(1, usage.CompletedRequests);
        Assert.True(usage.WindowRequests >= 1);
    }

    [Fact]
    public void GetUsage_Utilization_CalculatesCorrectly()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 10,
            TokensPerMinute = 1000,
            MaxConcurrent = 100
        });

        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire("m", estimatedTokens: 100);
            limiter.RecordCompletion("m");
        }

        var usage = limiter.GetUsage("m");
        Assert.Equal(50.0, usage!.RequestUtilization, 1);
        Assert.Equal(50.0, usage.TokenUtilization, 1);
    }

    [Fact]
    public void GetUsage_UtilizationCapsAt100()
    {
        var usage = new RateLimitUsage
        {
            WindowRequests = 200,
            RequestsPerMinuteLimit = 100,
            WindowTokens = 5000,
            TokensPerMinuteLimit = 1000
        };
        Assert.Equal(100.0, usage.RequestUtilization);
        Assert.Equal(100.0, usage.TokenUtilization);
    }

    [Fact]
    public void GetUsage_ZeroLimits_UtilizationIsZero()
    {
        var usage = new RateLimitUsage
        {
            WindowRequests = 10,
            RequestsPerMinuteLimit = 0,
            WindowTokens = 500,
            TokensPerMinuteLimit = 0
        };
        Assert.Equal(0.0, usage.RequestUtilization);
        Assert.Equal(0.0, usage.TokenUtilization);
    }

    // ── GetAllUsage ──

    [Fact]
    public void GetAllUsage_NoProfiles_ReturnsEmpty()
    {
        var limiter = new PromptRateLimiter();
        Assert.Empty(limiter.GetAllUsage());
    }

    [Fact]
    public void GetAllUsage_MultipleProfiles_ReturnsAll()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "a" });
        limiter.AddProfile(new RateLimitProfile { Name = "b" });
        var all = limiter.GetAllUsage();
        Assert.Equal(2, all.Count);
        Assert.Contains("a", all.Keys);
        Assert.Contains("b", all.Keys);
    }

    // ── GenerateReport ──

    [Fact]
    public void GenerateReport_EmptyLimiter_IncludesNoProfiles()
    {
        var limiter = new PromptRateLimiter();
        var report = limiter.GenerateReport();
        Assert.Contains("No profiles configured", report);
    }

    [Fact]
    public void GenerateReport_WithProfiles_IncludesNames()
    {
        var limiter = PromptRateLimiter.WithDefaults();
        var report = limiter.GenerateReport();
        Assert.Contains("gpt-4", report);
        Assert.Contains("RPM", report);
        Assert.Contains("TPM", report);
    }

    [Fact]
    public void GenerateReport_WithUsage_ShowsStats()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "test-model", RequestsPerMinute = 10 });

        limiter.TryAcquire("test-model", estimatedTokens: 500);
        limiter.RecordCompletion("test-model", actualTokens: 400);

        var report = limiter.GenerateReport();
        Assert.Contains("test-model", report);
        Assert.Contains("1 total", report);
        Assert.Contains("1 completed", report);
    }

    // ── ResetUsage ──

    [Fact]
    public void ResetUsage_ClearsCounters()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m", RequestsPerMinute = 100, MaxConcurrent = 100 });

        limiter.TryAcquire("m", estimatedTokens: 500);
        limiter.RecordCompletion("m");

        Assert.True(limiter.ResetUsage("m"));
        var usage = limiter.GetUsage("m");
        Assert.Equal(0, usage!.TotalRequests);
        Assert.Equal(0, usage.TotalTokens);
        Assert.Equal(0, usage.CompletedRequests);
        Assert.Equal(0, usage.ConcurrentRequests);
    }

    [Fact]
    public void ResetUsage_UnknownProfile_ReturnsFalse()
    {
        var limiter = new PromptRateLimiter();
        Assert.False(limiter.ResetUsage("ghost"));
    }

    [Fact]
    public void ResetUsage_EmptyName_ReturnsFalse()
    {
        var limiter = new PromptRateLimiter();
        Assert.False(limiter.ResetUsage(""));
    }

    [Fact]
    public void ResetAllUsage_ClearsAllProfiles()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "a", MaxConcurrent = 100, RequestsPerMinute = 100 });
        limiter.AddProfile(new RateLimitProfile { Name = "b", MaxConcurrent = 100, RequestsPerMinute = 100 });

        limiter.TryAcquire("a");
        limiter.RecordCompletion("a");
        limiter.TryAcquire("b");
        limiter.RecordCompletion("b");

        limiter.ResetAllUsage();

        Assert.Equal(0, limiter.GetUsage("a")!.TotalRequests);
        Assert.Equal(0, limiter.GetUsage("b")!.TotalRequests);
    }

    // ── Serialization ──

    [Fact]
    public void ToJson_SerializesProfiles()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "model-a",
            RequestsPerMinute = 100,
            TokensPerMinute = 50_000,
            MaxConcurrent = 10
        });

        var json = limiter.ToJson();
        Assert.Contains("model-a", json);
        Assert.Contains("100", json);
    }

    [Fact]
    public void FromJson_DeserializesProfiles()
    {
        var json = @"[{""Name"":""my-model"",""RequestsPerMinute"":42,""TokensPerMinute"":10000,""MaxConcurrent"":5,""Priority"":3}]";
        var limiter = PromptRateLimiter.FromJson(json);

        Assert.Single(limiter.GetProfileNames());
        var profile = limiter.GetProfile("my-model");
        Assert.Equal(42, profile!.RequestsPerMinute);
        Assert.Equal(10_000, profile.TokensPerMinute);
        Assert.Equal(5, profile.MaxConcurrent);
        Assert.Equal(3, profile.Priority);
    }

    [Fact]
    public void FromJson_EmptyString_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => PromptRateLimiter.FromJson(""));
    }

    [Fact]
    public void FromJson_EmptyArray_ReturnsEmptyLimiter()
    {
        var limiter = PromptRateLimiter.FromJson("[]");
        Assert.Empty(limiter.GetProfileNames());
    }

    [Fact]
    public void RoundTrip_ToJsonFromJson_PreservesProfiles()
    {
        var original = new PromptRateLimiter();
        original.AddProfile(new RateLimitProfile { Name = "x", RequestsPerMinute = 77, TokensPerMinute = 5000, MaxConcurrent = 3 });
        original.AddProfile(new RateLimitProfile { Name = "y", RequestsPerMinute = 200, TokensPerMinute = 100_000, MaxConcurrent = 25 });

        var json = original.ToJson();
        var restored = PromptRateLimiter.FromJson(json);

        Assert.Equal(2, restored.GetProfileNames().Count);
        Assert.Equal(77, restored.GetProfile("x")!.RequestsPerMinute);
        Assert.Equal(200, restored.GetProfile("y")!.RequestsPerMinute);
    }

    // ── RateLimitProfile Defaults ──

    [Fact]
    public void RateLimitProfile_Defaults()
    {
        var profile = new RateLimitProfile();
        Assert.Equal("", profile.Name);
        Assert.Equal(60, profile.RequestsPerMinute);
        Assert.Equal(90_000, profile.TokensPerMinute);
        Assert.Equal(10, profile.MaxConcurrent);
        Assert.Equal(5, profile.Priority);
    }

    // ── Denied Results ──

    [Fact]
    public void DeniedResult_RPM_HasPositiveWaitMs()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m", RequestsPerMinute = 1, MaxConcurrent = 100 });

        limiter.TryAcquire("m");
        limiter.RecordCompletion("m");

        var denied = limiter.TryAcquire("m");
        Assert.False(denied.Permitted);
        Assert.True(denied.WaitMs >= 0);
    }

    [Fact]
    public void DeniedResult_Concurrent_SuggestsRetry()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m", RequestsPerMinute = 100, MaxConcurrent = 1 });

        limiter.TryAcquire("m");
        var denied = limiter.TryAcquire("m");
        Assert.Equal(1000, denied.WaitMs);
    }

    [Fact]
    public void DeniedRequests_AreTracked()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m", RequestsPerMinute = 1, MaxConcurrent = 100 });

        limiter.TryAcquire("m");
        limiter.RecordCompletion("m");
        limiter.TryAcquire("m"); // denied

        var usage = limiter.GetUsage("m");
        Assert.Equal(1, usage!.DeniedRequests);
    }

    // ── Thread Safety ──

    [Fact]
    public async Task ConcurrentAcquire_RespectsConcurrencyLimit()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 10000,
            MaxConcurrent = 5,
            TokensPerMinute = 10_000_000
        });

        int permitted = 0;
        int denied = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var result = limiter.TryAcquire("m");
            if (result.Permitted)
                Interlocked.Increment(ref permitted);
            else
                Interlocked.Increment(ref denied);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(5, permitted); // MaxConcurrent = 5
        Assert.Equal(15, denied);
    }

    // ── Edge Cases ──

    [Fact]
    public void MultipleCompletions_WithoutAcquire_DontGoNegative()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile { Name = "m" });

        limiter.RecordCompletion("m");
        limiter.RecordCompletion("m");
        limiter.RecordCompletion("m");

        var usage = limiter.GetUsage("m");
        Assert.Equal(0, usage!.ConcurrentRequests);
    }

    [Fact]
    public void AcquireAndComplete_ManyTimes_TracksCorrectly()
    {
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "m",
            RequestsPerMinute = 10000,
            MaxConcurrent = 100
        });

        for (int i = 0; i < 50; i++)
        {
            var result = limiter.TryAcquire("m");
            Assert.True(result.Permitted);
            limiter.RecordCompletion("m");
        }

        var usage = limiter.GetUsage("m");
        Assert.Equal(50, usage!.TotalRequests);
        Assert.Equal(50, usage.CompletedRequests);
        Assert.Equal(0, usage.ConcurrentRequests);
    }

    [Fact]
    public void RateLimitResult_Defaults()
    {
        var result = new RateLimitResult();
        Assert.False(result.Permitted);
        Assert.Equal(0, result.WaitMs);
        Assert.Null(result.DenialReason);
        Assert.Equal(0, result.CurrentRequests);
        Assert.Equal(0, result.CurrentTokens);
        Assert.Equal(0, result.ConcurrentRequests);
        Assert.Equal("", result.ProfileName);
    }

    [Fact]
    public void RateLimitUsage_Defaults()
    {
        var usage = new RateLimitUsage();
        Assert.Equal("", usage.ProfileName);
        Assert.Equal(0, usage.TotalRequests);
        Assert.Equal(0, usage.TotalTokens);
        Assert.Equal(0, usage.DeniedRequests);
        Assert.Equal(0, usage.CompletedRequests);
        Assert.Equal(0, usage.WindowRequests);
        Assert.Equal(0, usage.WindowTokens);
        Assert.Equal(0, usage.ConcurrentRequests);
        Assert.Equal(0, usage.RequestsPerMinuteLimit);
        Assert.Equal(0, usage.TokensPerMinuteLimit);
        Assert.Equal(0, usage.MaxConcurrentLimit);
    }

    // ── Bug regression: RecordCompletion with pruned timestamp ──────

    [Fact]
    public void RecordCompletion_PrunedTimestamp_DoesNotCorruptOtherRequests()
    {
        // Scenario: two concurrent requests (A then B).  A's token record
        // gets pruned from the sliding window (request took > 60s).
        // RecordCompletion(A) should NOT fall back to modifying B's record.

        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "test",
            RequestsPerMinute = 100,
            TokensPerMinute = 100_000,
            MaxConcurrent = 10
        });

        // Acquire request A with 500 estimated tokens
        var resultA = limiter.TryAcquire("test", estimatedTokens: 500);
        Assert.True(resultA.Permitted);
        var tsA = resultA.AcquireTimestamp;

        // Acquire request B with 300 estimated tokens
        var resultB = limiter.TryAcquire("test", estimatedTokens: 300);
        Assert.True(resultB.Permitted);
        var tsB = resultB.AcquireTimestamp;

        // Complete B normally — should update B's record
        limiter.RecordCompletion("test", actualTokens: 250, acquireTimestamp: tsB);

        var usage = limiter.GetUsage("test");
        Assert.NotNull(usage);
        // Total: started at 500+300=800, B adjusted 300→250, so 750
        Assert.Equal(750, usage!.TotalTokens);

        // Now "complete" A with a bogus old timestamp that won't be found.
        // This simulates the window having pruned A's record (> 60s).
        // The key property: it should NOT modify B's record.
        limiter.RecordCompletion("test", actualTokens: 100, acquireTimestamp: tsA + 999_999);

        usage = limiter.GetUsage("test");
        // TotalTokens should still be 750 — A's pruned record can't be
        // adjusted, but B's record must remain untouched.
        Assert.Equal(750, usage!.TotalTokens);
    }

    [Fact]
    public void RecordCompletion_LegacyNoTimestamp_FallsBackToLast()
    {
        // Legacy callers that don't pass acquireTimestamp should still
        // fall back to the last entry (backward-compatible behaviour).
        var limiter = new PromptRateLimiter();
        limiter.AddProfile(new RateLimitProfile
        {
            Name = "test",
            RequestsPerMinute = 100,
            TokensPerMinute = 100_000,
            MaxConcurrent = 10
        });

        var result = limiter.TryAcquire("test", estimatedTokens: 500);
        Assert.True(result.Permitted);

        // Legacy call without acquireTimestamp
        limiter.RecordCompletion("test", actualTokens: 400);

        var usage = limiter.GetUsage("test");
        Assert.NotNull(usage);
        // Adjusted: 500→400
        Assert.Equal(400, usage!.TotalTokens);
    }
}
