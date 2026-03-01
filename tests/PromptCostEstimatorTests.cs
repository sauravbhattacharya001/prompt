namespace Prompt.Tests;

using Prompt;
using Xunit;

public class PromptCostEstimatorTests
{
    private readonly PromptCostEstimator _estimator = new();

    // ── Constructor / Built-in Models ──

    [Fact]
    public void Constructor_LoadsBuiltInModels()
    {
        Assert.True(_estimator.ModelCount >= 10, "Should have at least 10 built-in models");
    }

    [Fact]
    public void GetModels_ReturnsReadonlyList()
    {
        var models = _estimator.GetModels();
        Assert.NotEmpty(models);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-4o-mini")]
    [InlineData("claude-3-5-sonnet")]
    [InlineData("gemini-2.0-flash")]
    [InlineData("deepseek-v3")]
    public void GetModel_ReturnsKnownModels(string modelId)
    {
        var model = _estimator.GetModel(modelId);
        Assert.NotNull(model);
        Assert.Equal(modelId, model.ModelId);
    }

    [Fact]
    public void GetModel_UnknownReturnsNull()
    {
        Assert.Null(_estimator.GetModel("nonexistent-model-xyz"));
    }

    [Fact]
    public void GetModel_CaseInsensitive()
    {
        Assert.NotNull(_estimator.GetModel("GPT-4O"));
        Assert.NotNull(_estimator.GetModel("Claude-3-5-Sonnet"));
    }

    // ── GetProviders ──

    [Fact]
    public void GetProviders_ReturnsAllProviders()
    {
        var providers = _estimator.GetProviders();
        Assert.Contains("OpenAI", providers);
        Assert.Contains("Anthropic", providers);
        Assert.Contains("Google", providers);
        Assert.Contains("DeepSeek", providers);
    }

    [Fact]
    public void GetModelsByProvider_FiltersCorrectly()
    {
        var openai = _estimator.GetModelsByProvider("OpenAI");
        Assert.True(openai.Count >= 4);
        Assert.All(openai, m => Assert.Equal("OpenAI", m.Provider));
    }

    [Fact]
    public void GetModelsByProvider_CaseInsensitive()
    {
        var models = _estimator.GetModelsByProvider("openai");
        Assert.True(models.Count >= 4);
    }

    [Fact]
    public void GetModelsByProvider_UnknownReturnsEmpty()
    {
        var models = _estimator.GetModelsByProvider("UnknownProvider");
        Assert.Empty(models);
    }

    // ── AddModel ──

    [Fact]
    public void AddModel_CustomModelRegisters()
    {
        var est = new PromptCostEstimator();
        var custom = new ModelPricing("test-model", "TestCo", "Test", 1.0m, 2.0m, 100_000, 4_096);
        est.AddModel(custom);
        Assert.NotNull(est.GetModel("test-model"));
    }

    [Fact]
    public void AddModel_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => _estimator.AddModel(null!));
    }

    [Fact]
    public void AddModel_EmptyIdThrows()
    {
        var p = new ModelPricing("", "Co", "Name", 1m, 2m, 100_000, 4_096);
        Assert.Throws<ArgumentException>(() => _estimator.AddModel(p));
    }

    [Fact]
    public void AddModel_WhitespaceIdThrows()
    {
        var p = new ModelPricing("  ", "Co", "Name", 1m, 2m, 100_000, 4_096);
        Assert.Throws<ArgumentException>(() => _estimator.AddModel(p));
    }

    [Fact]
    public void AddModel_NegativeInputCostThrows()
    {
        var p = new ModelPricing("x", "Co", "Name", -1m, 2m, 100_000, 4_096);
        Assert.Throws<ArgumentException>(() => _estimator.AddModel(p));
    }

    [Fact]
    public void AddModel_NegativeOutputCostThrows()
    {
        var p = new ModelPricing("x", "Co", "Name", 1m, -2m, 100_000, 4_096);
        Assert.Throws<ArgumentException>(() => _estimator.AddModel(p));
    }

    [Fact]
    public void AddModel_ZeroContextWindowThrows()
    {
        var p = new ModelPricing("x", "Co", "Name", 1m, 2m, 0, 4_096);
        Assert.Throws<ArgumentException>(() => _estimator.AddModel(p));
    }

    [Fact]
    public void AddModel_ZeroMaxOutputThrows()
    {
        var p = new ModelPricing("x", "Co", "Name", 1m, 2m, 100_000, 0);
        Assert.Throws<ArgumentException>(() => _estimator.AddModel(p));
    }

    [Fact]
    public void AddModel_DuplicateIdThrows()
    {
        var est = new PromptCostEstimator();
        est.AddModel(new ModelPricing("dup", "Co", "Name", 1m, 2m, 100_000, 4_096));
        Assert.Throws<ArgumentException>(() =>
            est.AddModel(new ModelPricing("dup", "Co2", "Name2", 1m, 2m, 100_000, 4_096)));
    }

    [Fact]
    public void AddModel_DuplicateIdCaseInsensitive()
    {
        var est = new PromptCostEstimator();
        est.AddModel(new ModelPricing("my-model", "Co", "Name", 1m, 2m, 100_000, 4_096));
        Assert.Throws<ArgumentException>(() =>
            est.AddModel(new ModelPricing("MY-MODEL", "Co2", "Name2", 1m, 2m, 100_000, 4_096)));
    }

    [Fact]
    public void AddModel_ExceedsLimitThrows()
    {
        var est = new PromptCostEstimator();
        // Built-in models already take some slots; add until full
        for (int i = est.ModelCount; i < PromptCostEstimator.MaxModels; i++)
        {
            est.AddModel(new ModelPricing($"fill-{i}", "Co", $"Fill {i}", 1m, 2m, 100_000, 4_096));
        }
        Assert.Throws<InvalidOperationException>(() =>
            est.AddModel(new ModelPricing("overflow", "Co", "Overflow", 1m, 2m, 100_000, 4_096)));
    }

    // ── RemoveModel ──

    [Fact]
    public void RemoveModel_ReturnsTrue()
    {
        var est = new PromptCostEstimator();
        est.AddModel(new ModelPricing("removable", "Co", "Name", 1m, 2m, 100_000, 4_096));
        Assert.True(est.RemoveModel("removable"));
        Assert.Null(est.GetModel("removable"));
    }

    [Fact]
    public void RemoveModel_UnknownReturnsFalse()
    {
        Assert.False(_estimator.RemoveModel("nonexistent-model-xyz"));
    }

    [Fact]
    public void RemoveModel_CaseInsensitive()
    {
        var est = new PromptCostEstimator();
        est.AddModel(new ModelPricing("case-test", "Co", "Name", 1m, 2m, 100_000, 4_096));
        Assert.True(est.RemoveModel("CASE-TEST"));
    }

    // ── ModelPricing calculations ──

    [Fact]
    public void ModelPricing_InputCost_CalculatesCorrectly()
    {
        // $10 per million tokens, 1000 tokens = $0.01
        var model = new ModelPricing("test", "Co", "Test", 10.0m, 20.0m, 128_000, 4_096);
        Assert.Equal(0.01m, model.InputCost(1_000));
    }

    [Fact]
    public void ModelPricing_OutputCost_CalculatesCorrectly()
    {
        var model = new ModelPricing("test", "Co", "Test", 10.0m, 20.0m, 128_000, 4_096);
        Assert.Equal(0.02m, model.OutputCost(1_000));
    }

    [Fact]
    public void ModelPricing_TotalCost_SumsInputAndOutput()
    {
        var model = new ModelPricing("test", "Co", "Test", 10.0m, 20.0m, 128_000, 4_096);
        Assert.Equal(0.03m, model.TotalCost(1_000, 1_000));
    }

    [Fact]
    public void ModelPricing_ZeroTokens_ReturnsZeroCost()
    {
        var model = new ModelPricing("test", "Co", "Test", 10.0m, 20.0m, 128_000, 4_096);
        Assert.Equal(0m, model.InputCost(0));
        Assert.Equal(0m, model.OutputCost(0));
    }

    [Fact]
    public void ModelPricing_NegativeTokens_ReturnsZeroCost()
    {
        var model = new ModelPricing("test", "Co", "Test", 10.0m, 20.0m, 128_000, 4_096);
        Assert.Equal(0m, model.InputCost(-100));
        Assert.Equal(0m, model.OutputCost(-100));
    }

    // ── Estimate ──

    [Fact]
    public void Estimate_ReturnsReportForAllModels()
    {
        var report = _estimator.Estimate("Hello, world!");
        Assert.NotEmpty(report.Estimates);
        Assert.Equal(_estimator.ModelCount, report.Estimates.Count);
    }

    [Fact]
    public void Estimate_SortsByCostAscending()
    {
        var report = _estimator.Estimate("Test prompt for cost estimation.");
        for (int i = 1; i < report.Estimates.Count; i++)
        {
            Assert.True(report.Estimates[i].TotalCost >= report.Estimates[i - 1].TotalCost,
                $"{report.Estimates[i].Model.DisplayName} should cost >= {report.Estimates[i - 1].Model.DisplayName}");
        }
    }

    [Fact]
    public void Estimate_InputTokensMatchesGuardEstimate()
    {
        var text = "This is a test prompt with several words.";
        var report = _estimator.Estimate(text);
        var expected = PromptGuard.EstimateTokens(text);
        Assert.Equal(expected, report.InputTokens);
    }

    [Fact]
    public void Estimate_OutputTokensPreserved()
    {
        var report = _estimator.Estimate("Hello", 500);
        Assert.Equal(500, report.EstimatedOutputTokens);
    }

    [Fact]
    public void Estimate_DefaultOutputTokens()
    {
        var report = _estimator.Estimate("Hello");
        Assert.Equal(PromptCostEstimator.DefaultOutputTokens, report.EstimatedOutputTokens);
    }

    [Fact]
    public void Estimate_NullPromptThrows()
    {
        Assert.Throws<ArgumentNullException>(() => _estimator.Estimate(null!));
    }

    [Fact]
    public void Estimate_NegativeOutputTokensThrows()
    {
        Assert.Throws<ArgumentException>(() => _estimator.Estimate("Hello", -1));
    }

    [Fact]
    public void Estimate_ExcessiveOutputTokensThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _estimator.Estimate("Hello", PromptCostEstimator.MaxOutputTokens + 1));
    }

    [Fact]
    public void Estimate_ZeroOutputTokens_AllOutputCostsZero()
    {
        var report = _estimator.Estimate("Hello", 0);
        Assert.All(report.Estimates, e => Assert.Equal(0m, e.OutputCost));
    }

    [Fact]
    public void Estimate_EmptyPrompt_Works()
    {
        var report = _estimator.Estimate("");
        Assert.Equal(0, report.InputTokens);
        Assert.All(report.Estimates, e => Assert.Equal(0m, e.InputCost));
    }

    // ── CostReport properties ──

    [Fact]
    public void CostReport_CheapestViable_ReturnsFirst()
    {
        var report = _estimator.Estimate("Test");
        var cheapest = report.CheapestViable;
        Assert.NotNull(cheapest);
        Assert.False(cheapest.ExceedsContext);
    }

    [Fact]
    public void CostReport_MostExpensiveViable_ReturnsLast()
    {
        var report = _estimator.Estimate("Test");
        var expensive = report.MostExpensiveViable;
        Assert.NotNull(expensive);
        Assert.False(expensive.ExceedsContext);
        Assert.True(expensive.TotalCost >= report.CheapestViable!.TotalCost);
    }

    [Fact]
    public void CostReport_MaxSavings_CalculatesCorrectly()
    {
        var report = _estimator.Estimate("Test");
        if (report.MaxSavings.HasValue)
        {
            Assert.True(report.MaxSavings >= 0);
            Assert.Equal(
                report.MostExpensiveViable!.TotalCost - report.CheapestViable!.TotalCost,
                report.MaxSavings.Value);
        }
    }

    [Fact]
    public void CostReport_ViableModelCount_MatchesNonExceeding()
    {
        var report = _estimator.Estimate("Short prompt");
        var expected = report.Estimates.Count(e => !e.ExceedsContext);
        Assert.Equal(expected, report.ViableModelCount);
    }

    // ── Context window warnings ──

    [Fact]
    public void Estimate_SmallContextModel_MarksFitsOrExceeds()
    {
        var est = new PromptCostEstimator();
        est.AddModel(new ModelPricing("tiny", "Co", "Tiny", 1m, 2m, 100, 50));
        var report = est.Estimate("Hello world", 200);
        var tiny = report.Estimates.First(e => e.Model.ModelId == "tiny");
        Assert.True(tiny.ExceedsContext);
    }

    [Fact]
    public void Estimate_TightContext_ShowsWarning()
    {
        var est = new PromptCostEstimator();
        // 10_000 context, and we'll request 8500 output — should be tight
        est.AddModel(new ModelPricing("tight", "Co", "Tight", 1m, 2m, 10_000, 10_000));
        var report = est.Estimate("Hello", 8500);
        var tight = report.Estimates.First(e => e.Model.ModelId == "tight");
        Assert.True(tight.ContextUsagePercent > 80);
    }

    [Fact]
    public void Estimate_OutputCappedToModelMax()
    {
        var est = new PromptCostEstimator();
        est.AddModel(new ModelPricing("limited", "Co", "Limited", 1m, 2m, 1_000_000, 100));
        var report = est.Estimate("Hello", 50_000);
        var limited = report.Estimates.First(e => e.Model.ModelId == "limited");
        Assert.Equal(100, limited.EstimatedOutputTokens);
        Assert.NotNull(limited.Warning);
        Assert.Contains("capped", limited.Warning);
    }

    // ── EstimateFromTokens ──

    [Fact]
    public void EstimateFromTokens_Works()
    {
        var report = _estimator.EstimateFromTokens(1000, 500);
        Assert.Equal(1000, report.InputTokens);
        Assert.Equal(500, report.EstimatedOutputTokens);
        Assert.NotEmpty(report.Estimates);
    }

    [Fact]
    public void EstimateFromTokens_NegativeInputThrows()
    {
        Assert.Throws<ArgumentException>(() => _estimator.EstimateFromTokens(-1));
    }

    [Fact]
    public void EstimateFromTokens_NegativeOutputThrows()
    {
        Assert.Throws<ArgumentException>(() => _estimator.EstimateFromTokens(100, -1));
    }

    [Fact]
    public void EstimateFromTokens_ExcessiveOutputThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _estimator.EstimateFromTokens(100, PromptCostEstimator.MaxOutputTokens + 1));
    }

    [Fact]
    public void EstimateFromTokens_ZeroTokens_AllCostsZero()
    {
        var report = _estimator.EstimateFromTokens(0, 0);
        Assert.All(report.Estimates, e =>
        {
            Assert.Equal(0m, e.InputCost);
            Assert.Equal(0m, e.OutputCost);
            Assert.Equal(0m, e.TotalCost);
        });
    }

    [Fact]
    public void EstimateFromTokens_MatchesEstimateForSameTokenCount()
    {
        var text = "Compare this with token-based estimation.";
        var tokens = PromptGuard.EstimateTokens(text);
        var fromText = _estimator.Estimate(text, 500);
        var fromTokens = _estimator.EstimateFromTokens(tokens, 500);

        for (int i = 0; i < fromText.Estimates.Count; i++)
        {
            Assert.Equal(fromText.Estimates[i].TotalCost, fromTokens.Estimates[i].TotalCost);
        }
    }

    // ── EstimateCallsInBudget ──

    [Fact]
    public void EstimateCallsInBudget_CalculatesCorrectly()
    {
        // GPT-4o: $2.50/M input, $10/M output
        // 1000 in + 1000 out = $0.0025 + $0.01 = $0.0125 per call
        // $1.00 budget = 80 calls
        var calls = _estimator.EstimateCallsInBudget("gpt-4o", 1.00m, 1000, 1000);
        Assert.Equal(80, calls);
    }

    [Fact]
    public void EstimateCallsInBudget_ZeroBudgetThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _estimator.EstimateCallsInBudget("gpt-4o", 0m, 100, 100));
    }

    [Fact]
    public void EstimateCallsInBudget_NegativeBudgetThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _estimator.EstimateCallsInBudget("gpt-4o", -5m, 100, 100));
    }

    [Fact]
    public void EstimateCallsInBudget_ZeroInputTokensThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _estimator.EstimateCallsInBudget("gpt-4o", 1m, 0, 100));
    }

    [Fact]
    public void EstimateCallsInBudget_ZeroOutputTokensThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _estimator.EstimateCallsInBudget("gpt-4o", 1m, 100, 0));
    }

    [Fact]
    public void EstimateCallsInBudget_UnknownModelThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _estimator.EstimateCallsInBudget("nonexistent", 1m, 100, 100));
    }

    [Fact]
    public void EstimateCallsInBudget_FreeModel_ReturnsMaxInt()
    {
        var est = new PromptCostEstimator();
        est.AddModel(new ModelPricing("free", "Co", "Free", 0m, 0m, 100_000, 4_096));
        var calls = est.EstimateCallsInBudget("free", 1m, 100, 100);
        Assert.Equal(int.MaxValue, calls);
    }

    [Fact]
    public void EstimateCallsInBudget_LargeBudget()
    {
        var calls = _estimator.EstimateCallsInBudget("gpt-4o-mini", 100m, 500, 500);
        Assert.True(calls > 100_000);
    }

    // ── CostReport serialization ──

    [Fact]
    public void CostReport_ToJson_ValidJson()
    {
        var report = _estimator.Estimate("Test");
        var json = report.ToJson();
        Assert.NotEmpty(json);
        // Should parse without error
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.NotNull(doc.RootElement.GetProperty("inputTokens"));
        Assert.NotNull(doc.RootElement.GetProperty("estimates"));
    }

    [Fact]
    public void CostReport_ToJson_ContainsExpectedFields()
    {
        var report = _estimator.Estimate("Test", 500);
        var json = report.ToJson();
        Assert.Contains("\"inputTokens\"", json);
        Assert.Contains("\"estimatedOutputTokens\"", json);
        Assert.Contains("\"viableModels\"", json);
        Assert.Contains("\"cheapest\"", json);
        Assert.Contains("\"estimates\"", json);
    }

    [Fact]
    public void CostReport_ToJson_NotIndented()
    {
        var report = _estimator.Estimate("Test");
        var json = report.ToJson(indented: false);
        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void CostReport_ToText_ContainsHeader()
    {
        var report = _estimator.Estimate("Test");
        var text = report.ToText();
        Assert.Contains("Prompt Cost Estimator Report", text);
    }

    [Fact]
    public void CostReport_ToText_ContainsModelNames()
    {
        var report = _estimator.Estimate("Test");
        var text = report.ToText();
        Assert.Contains("GPT-4o", text);
        Assert.Contains("Claude 3.5 Sonnet", text);
    }

    [Fact]
    public void CostReport_ToText_ContainsTokenCounts()
    {
        var report = _estimator.Estimate("Test");
        var text = report.ToText();
        Assert.Contains("Input tokens:", text);
        Assert.Contains("Output estimate:", text);
    }

    [Fact]
    public void CostReport_ToText_ShowsBestValue()
    {
        var report = _estimator.Estimate("Test");
        var text = report.ToText();
        Assert.Contains("Best value:", text);
    }

    // ── Edge cases ──

    [Fact]
    public void Estimate_VeryLongPrompt_StillWorks()
    {
        var longPrompt = new string('x', 100_000);
        var report = _estimator.Estimate(longPrompt);
        Assert.True(report.InputTokens > 0);
        Assert.NotEmpty(report.Estimates);
    }

    [Fact]
    public void Estimate_AllModelsRemoved_EmptyReport()
    {
        var est = new PromptCostEstimator();
        foreach (var m in est.GetModels().ToList())
        {
            est.RemoveModel(m.ModelId);
        }
        var report = est.Estimate("Hello");
        Assert.Empty(report.Estimates);
        Assert.Null(report.CheapestViable);
        Assert.Null(report.MostExpensiveViable);
        Assert.Null(report.MaxSavings);
        Assert.Equal(0, report.ViableModelCount);
    }

    [Fact]
    public void Estimate_OnlyOneModel_MaxSavingsNull()
    {
        var est = new PromptCostEstimator();
        foreach (var m in est.GetModels().ToList())
        {
            est.RemoveModel(m.ModelId);
        }
        est.AddModel(new ModelPricing("solo", "Co", "Solo", 1m, 2m, 100_000, 4_096));
        var report = est.Estimate("Hello");
        Assert.Null(report.MaxSavings);
    }

    [Fact]
    public void CostReport_CheapestViable_SkipsExceedingModels()
    {
        var est = new PromptCostEstimator();
        // Remove all built-in models for isolated test
        foreach (var m in est.GetModels().ToList())
            est.RemoveModel(m.ModelId);

        // Cheap but tiny context (will exceed)
        est.AddModel(new ModelPricing("cheap-tiny", "Co", "Cheap Tiny", 0.01m, 0.01m, 10, 5));
        // Expensive but big context (will fit)
        est.AddModel(new ModelPricing("expensive-big", "Co", "Expensive Big", 100m, 200m, 1_000_000, 100_000));

        var report = est.Estimate("Hello world", 100);
        var cheapest = report.CheapestViable;
        Assert.NotNull(cheapest);
        Assert.Equal("expensive-big", cheapest.Model.ModelId);
    }

    // ── Cross-integration with PromptComposer ──

    [Fact]
    public void Estimate_WorksWithComposerOutput()
    {
        var prompt = PromptComposer.Create()
            .WithPersona("You are a senior code reviewer")
            .WithContext("The user is building a REST API in C#")
            .WithTask("Review the code for security issues")
            .WithConstraint("Be concise")
            .Build();

        var report = _estimator.Estimate(prompt);
        Assert.True(report.InputTokens > 10);
        Assert.NotNull(report.CheapestViable);
    }

    [Fact]
    public void Estimate_WorksWithTemplateOutput()
    {
        var template = new PromptTemplate("Summarize the following: {{text}}");
        var prompt = template.Render(new Dictionary<string, string> { ["text"] = "Hello world" });

        var report = _estimator.Estimate(prompt);
        Assert.True(report.InputTokens > 0);
    }
}
