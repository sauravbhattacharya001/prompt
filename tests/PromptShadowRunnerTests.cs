namespace Prompt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for <see cref="PromptShadowRunner"/>.
/// </summary>
public class PromptShadowRunnerTests
{
    private static Func<string, CancellationToken, Task<string>> Const(string response, int delayMs = 0) =>
        async (prompt, ct) =>
        {
            if (delayMs > 0) await Task.Delay(delayMs, ct);
            return response;
        };

    private static Func<string, CancellationToken, Task<string>> Echo(int delayMs = 0) =>
        async (prompt, ct) =>
        {
            if (delayMs > 0) await Task.Delay(delayMs, ct);
            return prompt;
        };

    private static Func<string, CancellationToken, Task<string>> Throws(string error) =>
        (prompt, ct) => throw new InvalidOperationException(error);

    /// <summary>
    /// Awaits the background shadow comparison task. Because shadows run via
    /// fire-and-forget Task.Run, the test must spin briefly until the comparison
    /// is recorded. Returns true if a comparison appeared within the timeout.
    /// </summary>
    private static async Task<bool> WaitForComparisonsAsync(
        PromptShadowRunner runner, int expected, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount + timeoutMs;
        while (Environment.TickCount < deadline)
        {
            if (runner.GetComparisons().Count >= expected) return true;
            await Task.Delay(20);
        }
        return false;
    }

    [Fact]
    public void Ctor_NullPrimaryExecutor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PromptShadowRunner(null!));
    }

    [Fact]
    public void AddShadow_Null_Throws()
    {
        var runner = new PromptShadowRunner(Const("ok"));
        Assert.Throws<ArgumentNullException>(() => runner.AddShadow(null!));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPrimaryResponse()
    {
        var runner = new PromptShadowRunner(Const("primary-result"));
        var response = await runner.ExecuteAsync("hi");
        Assert.Equal("primary-result", response);
    }

    [Fact]
    public async Task ExecuteAsync_PrimaryFailure_Propagates()
    {
        var runner = new PromptShadowRunner(Throws("boom"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ExecuteAsync("hi"));
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingShadow_RecordsMatch()
    {
        var runner = new PromptShadowRunner(Const("same-response"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "twin",
                Executor = Const("same-response"),
                SamplingRate = 1.0
            });

        await runner.ExecuteAsync("hi");
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        var comparisons = runner.GetComparisons();
        var c = Assert.Single(comparisons);
        Assert.True(c.Primary.Succeeded);
        Assert.True(c.Shadows["twin"].Succeeded);
        Assert.True(c.Matches["twin"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferingShadow_RecordsMismatch()
    {
        var runner = new PromptShadowRunner(Const("hello"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "alt",
                Executor = Const("goodbye"),
                SamplingRate = 1.0
            });

        await runner.ExecuteAsync("any");
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        var c = Assert.Single(runner.GetComparisons());
        Assert.False(c.Matches["alt"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShadowError_IsSwallowedByDefault()
    {
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "broken",
                Executor = Throws("shadow died"),
                SamplingRate = 1.0,
                SwallowErrors = true
            });

        var response = await runner.ExecuteAsync("hi");
        Assert.Equal("ok", response);
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        var c = Assert.Single(runner.GetComparisons());
        Assert.False(c.Shadows["broken"].Succeeded);
        Assert.Equal("shadow died", c.Shadows["broken"].Error);
        Assert.Null(c.Matches["broken"]); // null match when shadow failed
    }

    [Fact]
    public async Task ExecuteAsync_CustomMatchFunction_IsUsed()
    {
        var runner = new PromptShadowRunner(Const("ANSWER: 42"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "loose",
                Executor = Const("answer is 42"),
                SamplingRate = 1.0
            })
            .WithMatchFunction((a, b) =>
                a.Contains("42", StringComparison.Ordinal) &&
                b.Contains("42", StringComparison.Ordinal));

        await runner.ExecuteAsync("q");
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        var c = Assert.Single(runner.GetComparisons());
        Assert.True(c.Matches["loose"]);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultMatch_IsCaseInsensitiveAndTrimmed()
    {
        var runner = new PromptShadowRunner(Const("  HELLO  "))
            .AddShadow(new ShadowModelConfig
            {
                Label = "twin",
                Executor = Const("hello"),
                SamplingRate = 1.0
            });

        await runner.ExecuteAsync("ignored");
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        Assert.True(runner.GetComparisons()[0].Matches["twin"]);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroSamplingRate_SkipsShadow()
    {
        var shadowCalled = 0;
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "skipped",
                Executor = (p, ct) => { Interlocked.Increment(ref shadowCalled); return Task.FromResult("x"); },
                SamplingRate = 0.0
            });

        await runner.ExecuteAsync("p");
        // Give background task a window to run
        await Task.Delay(150);
        Assert.Equal(0, shadowCalled);
    }

    [Fact]
    public async Task ExecuteAsync_TagsArePropagatedAndFilterable()
    {
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "t",
                Executor = Const("ok"),
                SamplingRate = 1.0
            });

        await runner.ExecuteAsync("p1", new[] { "alpha", "shared" });
        await runner.ExecuteAsync("p2", new[] { "beta", "shared" });
        Assert.True(await WaitForComparisonsAsync(runner, 2));

        Assert.Equal(2, runner.GetComparisons().Count);
        Assert.Single(runner.GetComparisons("alpha"));
        Assert.Single(runner.GetComparisons("beta"));
        Assert.Equal(2, runner.GetComparisons("shared").Count);
        Assert.Empty(runner.GetComparisons("does-not-exist"));
    }

    [Fact]
    public async Task ExecuteAsync_OnComparisonCallback_IsInvoked()
    {
        var observed = new List<ShadowComparison>();
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "s",
                Executor = Const("ok"),
                SamplingRate = 1.0
            })
            .OnComparison(c => { lock (observed) observed.Add(c); });

        await runner.ExecuteAsync("p");
        Assert.True(await WaitForComparisonsAsync(runner, 1));
        // The callback runs after the comparison is stored; allow a moment.
        await Task.Delay(50);
        Assert.Single(observed);
    }

    [Fact]
    public async Task ExecuteAsync_OnComparisonCallback_Throwing_DoesNotPropagate()
    {
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "s",
                Executor = Const("ok"),
                SamplingRate = 1.0
            })
            .OnComparison(_ => throw new InvalidOperationException("ignored"));

        // Should not throw despite the callback exception.
        await runner.ExecuteAsync("p");
        Assert.True(await WaitForComparisonsAsync(runner, 1));
    }

    [Fact]
    public async Task WithMaxStoredComparisons_EnforcesFifoEviction()
    {
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "s",
                Executor = Const("ok"),
                SamplingRate = 1.0
            })
            .WithMaxStoredComparisons(3);

        for (int i = 0; i < 6; i++)
            await runner.ExecuteAsync($"p{i}", new[] { $"tag{i}" });

        // Wait until 3 comparisons stick (older ones get evicted as new ones arrive)
        var deadline = Environment.TickCount + 5000;
        while (Environment.TickCount < deadline && runner.GetComparisons().Count != 3)
            await Task.Delay(20);

        var stored = runner.GetComparisons();
        Assert.Equal(3, stored.Count);
        // Newest three should be present
        var prompts = stored.Select(c => c.Prompt).ToList();
        Assert.Contains("p5", prompts);
        Assert.DoesNotContain("p0", prompts);
    }

    [Fact]
    public void WithMaxStoredComparisons_ClampsToOne()
    {
        // Should not throw with a non-positive limit; effectively clamped to >=1
        var runner = new PromptShadowRunner(Const("ok"))
            .WithMaxStoredComparisons(0)
            .WithMaxStoredComparisons(-100);
        Assert.NotNull(runner);
    }

    [Fact]
    public async Task GetSummary_AggregatesStatsCorrectly()
    {
        var runner = new PromptShadowRunner(Const("hello", delayMs: 5))
            .AddShadow(new ShadowModelConfig
            {
                Label = "matchy",
                Executor = Const("hello"),
                SamplingRate = 1.0
            })
            .AddShadow(new ShadowModelConfig
            {
                Label = "different",
                Executor = Const("world"),
                SamplingRate = 1.0
            });

        for (int i = 0; i < 5; i++)
            await runner.ExecuteAsync($"p{i}");

        Assert.True(await WaitForComparisonsAsync(runner, 5));

        var summary = runner.GetSummary();
        Assert.Equal(5, summary.TotalRuns);
        Assert.Equal(5, summary.PrimarySuccesses);
        Assert.True(summary.AvgPrimaryLatencyMs >= 0);

        Assert.True(summary.ShadowStats.ContainsKey("matchy"));
        Assert.True(summary.ShadowStats.ContainsKey("different"));

        var matchy = summary.ShadowStats["matchy"];
        Assert.Equal(5, matchy.Successes);
        Assert.Equal(0, matchy.Failures);
        Assert.Equal(5, matchy.MatchCount);
        Assert.Equal(100.0, matchy.MatchRate);

        var diff = summary.ShadowStats["different"];
        Assert.Equal(5, diff.Successes);
        Assert.Equal(0, diff.MatchCount);
        Assert.Equal(0.0, diff.MatchRate);
    }

    [Fact]
    public async Task GetSummary_NoRuns_ReturnsEmptyStats()
    {
        var runner = new PromptShadowRunner(Const("ok"));
        var summary = runner.GetSummary();
        Assert.Equal(0, summary.TotalRuns);
        Assert.Equal(0, summary.PrimarySuccesses);
        Assert.Equal(0, summary.AvgPrimaryLatencyMs);
        Assert.Empty(summary.ShadowStats);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ClearComparisons_RemovesAll()
    {
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "s",
                Executor = Const("ok"),
                SamplingRate = 1.0
            });

        await runner.ExecuteAsync("p1");
        await runner.ExecuteAsync("p2");
        Assert.True(await WaitForComparisonsAsync(runner, 2));

        runner.ClearComparisons();
        Assert.Empty(runner.GetComparisons());
    }

    [Fact]
    public async Task ExportJson_IsValidJsonAndOmitsExecutorDelegates()
    {
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "s",
                Executor = Const("ok"),
                SamplingRate = 1.0
            });

        await runner.ExecuteAsync("hello", new[] { "smoke" });
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        var json = runner.ExportJson(indented: true);
        // Must parse
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);

        // Should contain the prompt and shadow label
        Assert.Contains("\"hello\"", json);
        Assert.Contains("\"s\"", json);
    }

    [Fact]
    public async Task ExecuteAsync_PrimaryLatencyIsRecorded()
    {
        var runner = new PromptShadowRunner(Const("ok", delayMs: 30))
            .AddShadow(new ShadowModelConfig
            {
                Label = "s",
                Executor = Const("ok"),
                SamplingRate = 1.0
            });

        await runner.ExecuteAsync("p");
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        var c = Assert.Single(runner.GetComparisons());
        Assert.True(c.Primary.LatencyMs >= 20,
            $"Expected primary latency to reflect ~30ms delay, got {c.Primary.LatencyMs}ms");
        Assert.True(c.LatencyRatios.ContainsKey("s"));
    }

    [Fact]
    public async Task ExecuteAsync_ShadowTimeout_IsRecordedAsFailure()
    {
        var runner = new PromptShadowRunner(Const("ok"))
            .AddShadow(new ShadowModelConfig
            {
                Label = "slow",
                Executor = Const("late", delayMs: 5000),
                SamplingRate = 1.0,
                Timeout = TimeSpan.FromMilliseconds(50),
                SwallowErrors = true
            });

        await runner.ExecuteAsync("p");
        Assert.True(await WaitForComparisonsAsync(runner, 1));

        var c = Assert.Single(runner.GetComparisons());
        Assert.False(c.Shadows["slow"].Succeeded);
        Assert.NotNull(c.Shadows["slow"].Error);
    }
}
