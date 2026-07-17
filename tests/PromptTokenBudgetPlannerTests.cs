namespace Prompt.Tests;

using System.Linq;
using Xunit;

/// <summary>
/// Direct coverage for <see cref="PromptTokenBudgetPlanner"/>, <see cref="BudgetPlan"/>,
/// and <see cref="BudgetSection"/>. Previously untested (TokenBudgetTests covers the
/// unrelated <see cref="TokenBudget"/> conversation-trimming class).
/// </summary>
public class PromptTokenBudgetPlannerTests
{
    // ──────────────── Construction / validation ────────────────

    [Fact]
    public void Constructor_RejectsNonPositiveContextWindow()
    {
        Assert.Throws<ArgumentException>(() => new PromptTokenBudgetPlanner("m", 0));
        Assert.Throws<ArgumentException>(() => new PromptTokenBudgetPlanner("m", -1));
    }

    [Fact]
    public void Constructor_RejectsEmptyModelName()
    {
        Assert.Throws<ArgumentException>(() => new PromptTokenBudgetPlanner("  ", 1000));
    }

    [Fact]
    public void Constructor_DefaultResponseReserve_IsCappedByQuarterWindow()
    {
        // 8000/4 = 2000 < 4000, so default reserve is 2000.
        var plan = new PromptTokenBudgetPlanner("m", 8_000).BuildPlan();
        Assert.Equal(2_000, plan.ResponseReserve);
        Assert.Equal(6_000, plan.AvailableForPrompt);
    }

    [Fact]
    public void SetResponseReserve_RejectsNegativeAndOverWindow()
    {
        var p = new PromptTokenBudgetPlanner("m", 1000);
        Assert.Throws<ArgumentException>(() => p.SetResponseReserve(-1));
        Assert.Throws<ArgumentException>(() => p.SetResponseReserve(1000));
    }

    // ──────────────── Section management ────────────────

    [Fact]
    public void AddSection_RejectsDuplicateNamesCaseInsensitively()
    {
        var p = new PromptTokenBudgetPlanner("m", 10_000);
        p.AddSection("Sys", "d", 100);
        Assert.Throws<ArgumentException>(() => p.AddSection("sys", "d", 200));
    }

    [Fact]
    public void AddSection_EnforcesMaxSections()
    {
        var p = new PromptTokenBudgetPlanner("m", 1_000_000);
        for (int i = 0; i < PromptTokenBudgetPlanner.MaxSections; i++)
            p.AddSection($"s{i}", "d", 10);
        Assert.Throws<InvalidOperationException>(() => p.AddSection("overflow", "d", 10));
    }

    [Fact]
    public void RemoveSection_ReturnsFalseWhenAbsent()
    {
        var p = new PromptTokenBudgetPlanner("m", 10_000);
        Assert.False(p.RemoveSection("nope"));
        p.AddSection("Sys", "d", 100);
        Assert.True(p.RemoveSection("SYS"));
    }

    // ──────────────── BudgetSection math ────────────────

    [Fact]
    public void BudgetSection_ReportsUsageAndOverBudget()
    {
        var content = string.Join(" ", Enumerable.Repeat("word", 100));
        var actual = PromptGuard.EstimateTokens(content);

        var underBudget = new BudgetSection("s", "d", actual + 1000, content);
        Assert.False(underBudget.IsOverBudget);
        Assert.Equal(1000, underBudget.Remaining);

        var overBudget = new BudgetSection("s", "d", 1, content);
        Assert.True(overBudget.IsOverBudget);
        Assert.Equal(0, overBudget.Remaining); // never negative
    }

    [Fact]
    public void BudgetSection_WithoutContent_HasZeroActualTokens()
    {
        var s = new BudgetSection("s", "d", 500);
        Assert.Equal(0, s.ActualTokens);
        Assert.Equal(0, s.UsagePercent);
        Assert.False(s.IsOverBudget);
    }

    // ──────────────── BudgetPlan aggregation ────────────────

    [Fact]
    public void BuildPlan_AggregatesAllocationAndDetectsOverBudget()
    {
        var p = new PromptTokenBudgetPlanner("m", 10_000);
        p.SetResponseReserve(2_000); // available = 8000
        p.AddSection("A", "d", 5_000, BudgetPriority.Fixed);
        p.AddSection("B", "d", 4_000, BudgetPriority.Low);

        var plan = p.BuildPlan();
        Assert.Equal(9_000, plan.TotalAllocated);
        Assert.Equal(8_000, plan.AvailableForPrompt);
        Assert.True(plan.IsOverBudget);
        Assert.Equal(0, plan.Unallocated); // clamped, not negative
    }

    // ──────────────── AutoDistribute (the fix) ────────────────

    [Fact]
    public void AutoDistribute_FillsEntireRemainingBudget_NoTruncationLoss()
    {
        // Choose a window that makes the proportional split not divide evenly, so
        // the pre-fix integer-truncation would drop tokens. available = 10001,
        // fixed = 1, remaining = 10000 split across three equal flex sections:
        // each floor(10000/3)=3333, sum=9999, leftover 1 must not vanish.
        var p = new PromptTokenBudgetPlanner("m", 20_002);
        p.SetResponseReserve(10_001); // available = 10001
        p.AddSection("Fixed", "d", 1, BudgetPriority.Fixed);
        p.AddSection("F1", "d", 100, BudgetPriority.Normal);
        p.AddSection("F2", "d", 100, BudgetPriority.Low);
        p.AddSection("F3", "d", 100, BudgetPriority.High);

        p.AutoDistribute();
        var plan = p.BuildPlan();

        // Flexible sections must sum to the full remaining (10000); with the fixed
        // section that is the whole available budget — nothing dropped.
        var flexSum = plan.Sections
            .Where(s => s.Priority != BudgetPriority.Fixed)
            .Sum(s => s.Section.AllocatedTokens);
        Assert.Equal(10_000, flexSum);
        Assert.Equal(10_001, plan.TotalAllocated);
    }

    [Fact]
    public void AutoDistribute_EvenSplitWhenFlexSectionsStartAtZero()
    {
        var p = new PromptTokenBudgetPlanner("m", 12_000);
        p.SetResponseReserve(2_000); // available = 10000
        p.AddSection("F1", "d", 0, BudgetPriority.Normal);
        p.AddSection("F2", "d", 0, BudgetPriority.Low);

        p.AutoDistribute();
        var plan = p.BuildPlan();

        Assert.Equal(10_000, plan.TotalAllocated);
        Assert.All(plan.Sections, s => Assert.True(s.Section.AllocatedTokens >= 5_000));
    }

    [Fact]
    public void AutoDistribute_NoOpWhenFixedExceedsAvailable()
    {
        var p = new PromptTokenBudgetPlanner("m", 12_000);
        p.SetResponseReserve(2_000); // available = 10000
        p.AddSection("Fixed", "d", 10_000, BudgetPriority.Fixed);
        p.AddSection("Flex", "d", 500, BudgetPriority.Low);

        p.AutoDistribute();
        var plan = p.BuildPlan();

        // remaining <= 0, so flexible allocation is left untouched.
        var flex = plan.Sections.Single(s => s.Section.Name == "Flex");
        Assert.Equal(500, flex.Section.AllocatedTokens);
    }

    [Fact]
    public void CreateStandard_ProducesFourSectionsWithinWindow()
    {
        var plan = PromptTokenBudgetPlanner
            .CreateStandard("gpt-4o", 128_000, responseReserve: 4_000)
            .BuildPlan();

        Assert.Equal(4, plan.Sections.Count);
        Assert.Equal(4_000, plan.ResponseReserve);
        Assert.False(plan.IsOverBudget);
    }
}
