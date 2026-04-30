namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptMetabolismEngineTests
    {
        private PromptMetabolismEngine CreateEngine() => new();

        private ConsumptionSample MakeSample(string promptId, int inputTokens, int outputTokens, string model = "gpt-4o", double? quality = null, DateTime? ts = null, bool success = true)
        {
            return new ConsumptionSample
            {
                PromptId = promptId,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Model = model,
                QualityScore = quality,
                Timestamp = ts ?? DateTime.UtcNow,
                Success = success,
                Tags = new List<string>()
            };
        }

        [Fact]
        public void RecordSample_IncrementsSampleCount()
        {
            var engine = CreateEngine();
            engine.RecordSample(MakeSample("p1", 100, 50));
            Assert.Equal(1, engine.SampleCount);
        }

        [Fact]
        public void RecordSample_AutoComputesCost()
        {
            var engine = CreateEngine();
            var sample = MakeSample("p1", 1000, 500, "gpt-4o");
            engine.RecordSample(sample);
            Assert.NotNull(sample.CostUsd);
            Assert.True(sample.CostUsd > 0);
        }

        [Fact]
        public void RecordSample_NullThrows()
        {
            var engine = CreateEngine();
            Assert.Throws<ArgumentNullException>(() => engine.RecordSample(null!));
        }

        [Fact]
        public void RecordSamples_AddsMultiple()
        {
            var engine = CreateEngine();
            var samples = Enumerable.Range(0, 10).Select(i => MakeSample("p1", 100 + i, 50)).ToList();
            engine.RecordSamples(samples);
            Assert.Equal(10, engine.SampleCount);
        }

        [Fact]
        public void EstimateCost_KnownModel()
        {
            var engine = CreateEngine();
            // gpt-4o: 0.005/1K input, 0.015/1K output
            var cost = engine.EstimateCost("gpt-4o", 2000, 1000);
            Assert.Equal(0.025, cost, 4); // 2*0.005 + 1*0.015
        }

        [Fact]
        public void EstimateCost_UnknownModelUsesDefault()
        {
            var engine = CreateEngine();
            var cost = engine.EstimateCost("unknown-model", 1000, 1000);
            Assert.True(cost > 0);
        }

        [Fact]
        public void SetModelPricing_OverridesDefault()
        {
            var engine = CreateEngine();
            engine.SetModelPricing("custom-model", 0.001, 0.002, 32000);
            var cost = engine.EstimateCost("custom-model", 1000, 1000);
            Assert.Equal(0.003, cost, 4);
        }

        [Fact]
        public void Analyze_EmptySamples_ReturnsStarvingState()
        {
            var engine = CreateEngine();
            var report = engine.Analyze();
            Assert.Equal(MetabolicState.Starving, report.OverallState);
            Assert.Equal(0, report.HealthScore);
        }

        [Fact]
        public void Analyze_SinglePrompt_ProducesProfile()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("summarize", 800, 400, quality: 75, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.Single(report.Profiles);
            Assert.Equal("summarize", report.Profiles[0].PromptId);
        }

        [Fact]
        public void Analyze_MultiplePrompts_ProducesMultipleProfiles()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
            {
                engine.RecordSample(MakeSample("p1", 500, 200, ts: DateTime.UtcNow.AddHours(-i)));
                engine.RecordSample(MakeSample("p2", 2000, 800, ts: DateTime.UtcNow.AddHours(-i)));
            }
            var report = engine.Analyze();
            Assert.Equal(2, report.Profiles.Count);
        }

        [Fact]
        public void Analyze_HighTokens_ClassifiesAsFastBurnOrGorging()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("big", 8000, 4000, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            var state = report.Profiles[0].State;
            Assert.True(state == MetabolicState.FastBurn || state == MetabolicState.Gorging);
        }

        [Fact]
        public void Analyze_LowTokens_ClassifiesAsSlowBurnOrStarving()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("tiny", 100, 50, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            var state = report.Profiles[0].State;
            Assert.True(state == MetabolicState.SlowBurn || state == MetabolicState.Starving);
        }

        [Fact]
        public void Analyze_BalancedTokens_ClassifiesAsBalanced()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("mid", 2000, 1000, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.Equal(MetabolicState.Balanced, report.Profiles[0].State);
        }

        [Fact]
        public void Analyze_ErraticTokens_ClassifiesAsErratic()
        {
            var engine = CreateEngine();
            var rng = new Random(42);
            for (int i = 0; i < 15; i++)
            {
                var tokens = i % 2 == 0 ? rng.Next(100, 500) : rng.Next(8000, 15000);
                engine.RecordSample(MakeSample("wild", tokens, tokens / 2, ts: DateTime.UtcNow.AddHours(-i)));
            }
            var report = engine.Analyze();
            Assert.Equal(MetabolicState.Erratic, report.Profiles[0].State);
        }

        [Fact]
        public void Analyze_DetectsCostSpike()
        {
            var engine = CreateEngine();
            // Baseline: normal cost
            for (int i = 10; i > 2; i--)
                engine.RecordSample(MakeSample("spiky", 500, 200, ts: DateTime.UtcNow.AddHours(-i)));
            // Recent: 5x cost spike
            engine.RecordSample(MakeSample("spiky", 5000, 3000, ts: DateTime.UtcNow.AddHours(-1)));
            engine.RecordSample(MakeSample("spiky", 5500, 3200, ts: DateTime.UtcNow));

            var report = engine.Analyze();
            Assert.Contains(report.Disorders, d => d.Disorder == MetabolicDisorder.CostSpike);
        }

        [Fact]
        public void Analyze_DetectsDiminishingReturns()
        {
            var engine = CreateEngine();
            // First half: low tokens, decent quality
            for (int i = 12; i > 6; i--)
                engine.RecordSample(MakeSample("dr", 500, 200, quality: 70, ts: DateTime.UtcNow.AddHours(-i)));
            // Second half: much more tokens, same quality
            for (int i = 6; i > 0; i--)
                engine.RecordSample(MakeSample("dr", 1500, 800, quality: 71, ts: DateTime.UtcNow.AddHours(-i)));

            var report = engine.Analyze();
            Assert.Contains(report.Disorders, d => d.Disorder == MetabolicDisorder.DiminishingReturns);
        }

        [Fact]
        public void Analyze_DetectsTokenBloat()
        {
            var engine = CreateEngine();
            for (int i = 10; i > 0; i--)
                engine.RecordSample(MakeSample("bloat", 200 + (10 - i) * 300, 100 + (10 - i) * 150, ts: DateTime.UtcNow.AddHours(-i)));

            var report = engine.Analyze();
            Assert.Contains(report.Disorders, d => d.Disorder == MetabolicDisorder.TokenBloat);
        }

        [Fact]
        public void Analyze_DetectsOverfeeding()
        {
            var engine = CreateEngine();
            // Simple tasks on expensive model
            for (int i = 0; i < 8; i++)
                engine.RecordSample(MakeSample("simple", 200, 100, "gpt-4", quality: 85, ts: DateTime.UtcNow.AddHours(-i)));

            var report = engine.Analyze();
            Assert.Contains(report.Disorders, d => d.Disorder == MetabolicDisorder.Overfeeding);
        }

        [Fact]
        public void Analyze_DetectsRedundancyWaste()
        {
            var engine = CreateEngine();
            // Same input tokens repeatedly
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("repeat", 1000, 500, ts: DateTime.UtcNow.AddHours(-i)));

            var report = engine.Analyze();
            Assert.Contains(report.Disorders, d => d.Disorder == MetabolicDisorder.RedundancyWaste);
        }

        [Fact]
        public void Analyze_DetectsCapacityExhaustion()
        {
            var engine = CreateEngine();
            // Near context limit for gpt-4 (8192)
            engine.RecordSample(MakeSample("huge", 5000, 2500, "gpt-4", ts: DateTime.UtcNow.AddHours(-2)));
            engine.RecordSample(MakeSample("huge", 5200, 2700, "gpt-4", ts: DateTime.UtcNow.AddHours(-1)));
            engine.RecordSample(MakeSample("huge", 5500, 2800, "gpt-4", ts: DateTime.UtcNow));

            var report = engine.Analyze();
            Assert.Contains(report.Disorders, d => d.Disorder == MetabolicDisorder.CapacityExhaustion);
        }

        [Fact]
        public void Analyze_GeneratesRecommendations()
        {
            var engine = CreateEngine();
            // Create a scenario with disorders
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("wasteful", 200, 100, "gpt-4", quality: 85, ts: DateTime.UtcNow.AddHours(-i)));

            var report = engine.Analyze();
            Assert.True(report.Recommendations.Count > 0);
        }

        [Fact]
        public void Analyze_RecommendationsHavePriority()
        {
            var engine = CreateEngine();
            for (int i = 10; i > 2; i--)
                engine.RecordSample(MakeSample("s1", 500, 200, ts: DateTime.UtcNow.AddHours(-i)));
            engine.RecordSample(MakeSample("s1", 5000, 3000, ts: DateTime.UtcNow.AddHours(-1)));
            engine.RecordSample(MakeSample("s1", 5500, 3200, ts: DateTime.UtcNow));

            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("s2", 1000, 500, ts: DateTime.UtcNow.AddHours(-i)));

            var report = engine.Analyze();
            if (report.Recommendations.Count >= 2)
            {
                var priorities = report.Recommendations.Select(r => r.Priority).ToList();
                Assert.Equal(priorities.OrderBy(p => p).ToList(), priorities);
            }
        }

        [Fact]
        public void Analyze_HealthScoreInRange()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("p1", 1000, 500, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.InRange(report.HealthScore, 0, 100);
        }

        [Fact]
        public void Analyze_HealthyPortfolioHighScore()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("healthy", 1500, 700, "gpt-4o-mini", quality: 80, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.True(report.HealthScore >= 70);
        }

        [Fact]
        public void Analyze_UnhealthyPortfolioLowScore()
        {
            var engine = CreateEngine();
            // Multiple disorder triggers
            for (int i = 20; i > 2; i--)
                engine.RecordSample(MakeSample("bad1", 200, 100, "gpt-4", quality: 80, ts: DateTime.UtcNow.AddHours(-i)));
            engine.RecordSample(MakeSample("bad1", 8000, 4000, "gpt-4", quality: 40, ts: DateTime.UtcNow.AddHours(-1)));
            engine.RecordSample(MakeSample("bad1", 7500, 3800, "gpt-4", quality: 35, ts: DateTime.UtcNow));

            var report = engine.Analyze();
            Assert.True(report.HealthScore < 80);
        }

        [Fact]
        public void Analyze_WindowFiltersOldSamples()
        {
            var engine = CreateEngine();
            // Old samples
            for (int i = 0; i < 5; i++)
                engine.RecordSample(MakeSample("old", 5000, 3000, ts: DateTime.UtcNow.AddDays(-30)));
            // Recent samples
            for (int i = 0; i < 5; i++)
                engine.RecordSample(MakeSample("new", 800, 400, ts: DateTime.UtcNow.AddHours(-i)));

            var report = engine.Analyze(TimeSpan.FromDays(7));
            Assert.Single(report.Profiles);
            Assert.Equal("new", report.Profiles[0].PromptId);
        }

        [Fact]
        public void Analyze_TotalTokensComputed()
        {
            var engine = CreateEngine();
            engine.RecordSample(MakeSample("t1", 1000, 500, ts: DateTime.UtcNow));
            engine.RecordSample(MakeSample("t1", 2000, 800, ts: DateTime.UtcNow));
            var report = engine.Analyze();
            Assert.Equal(4300, report.TotalTokensConsumed);
        }

        [Fact]
        public void Analyze_TotalCostComputed()
        {
            var engine = CreateEngine();
            var s = MakeSample("c1", 1000, 1000, "gpt-4o");
            engine.RecordSample(s);
            var report = engine.Analyze();
            Assert.True(report.TotalCostUsd > 0);
        }

        [Fact]
        public void Analyze_EfficiencyGradeAssigned()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordSample(MakeSample("eff", 1000, 500, quality: 80, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.False(string.IsNullOrEmpty(report.PortfolioEfficiency.Grade));
        }

        [Fact]
        public void Analyze_SuccessRateComputed()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 8; i++)
                engine.RecordSample(MakeSample("sr", 1000, 500, ts: DateTime.UtcNow.AddHours(-i)));
            engine.RecordSample(MakeSample("sr", 1000, 500, ts: DateTime.UtcNow, success: false));
            engine.RecordSample(MakeSample("sr", 1000, 500, ts: DateTime.UtcNow, success: false));

            var report = engine.Analyze();
            Assert.Equal(0.8, report.PortfolioEfficiency.SuccessRate, 1);
        }

        [Fact]
        public void GetPromptState_FewSamples_ReturnsStarving()
        {
            var engine = CreateEngine();
            engine.RecordSample(MakeSample("x", 500, 200));
            Assert.Equal(MetabolicState.Starving, engine.GetPromptState("x"));
        }

        [Fact]
        public void GetPromptState_UnknownPrompt_ReturnsStarving()
        {
            var engine = CreateEngine();
            Assert.Equal(MetabolicState.Starving, engine.GetPromptState("nonexistent"));
        }

        [Fact]
        public void GetPromptState_EnoughSamples_ReturnsValid()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordSample(MakeSample("test", 2000, 1000, ts: DateTime.UtcNow.AddHours(-i)));
            var state = engine.GetPromptState("test");
            Assert.True(Enum.IsDefined(typeof(MetabolicState), state));
        }

        [Fact]
        public void ExportJson_ProducesValidJson()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordSample(MakeSample("j1", 1000, 500, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            var json = engine.ExportJson(report);
            Assert.Contains("HealthScore", json);
            Assert.Contains("OverallState", json);
            // Verify it's valid JSON
            var parsed = System.Text.Json.JsonDocument.Parse(json);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void ExportHtml_ProducesCompleteDashboard()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("h1", 1000, 500, quality: 75, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            var html = engine.ExportHtml(report);
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Prompt Metabolism Dashboard", html);
            Assert.Contains("Health Score", html);
            Assert.Contains("</html>", html);
        }

        [Fact]
        public void ExportHtml_IncludesDisorders()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("overfed", 200, 100, "gpt-4", quality: 85, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            var html = engine.ExportHtml(report);
            Assert.Contains("Metabolic Disorders", html);
        }

        [Fact]
        public void ExportHtml_IncludesRecommendations()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("rec", 200, 100, "gpt-4", quality: 85, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            var html = engine.ExportHtml(report);
            Assert.Contains("Dietary Recommendations", html);
        }

        [Fact]
        public void Analyze_SummaryNonEmpty()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordSample(MakeSample("sum", 1000, 500, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.False(string.IsNullOrWhiteSpace(report.Summary));
        }

        [Fact]
        public void Analyze_PotentialSavingsNonNegative()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("sav", 1000, 500, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.True(report.PotentialMonthlySavings >= 0);
        }

        [Fact]
        public void ConsumptionSample_TotalTokensCorrect()
        {
            var sample = new ConsumptionSample { InputTokens = 300, OutputTokens = 150 };
            Assert.Equal(450, sample.TotalTokens);
        }

        [Fact]
        public void Analyze_ConsumptionTrendPositiveForGrowingTokens()
        {
            var engine = CreateEngine();
            for (int i = 10; i > 0; i--)
                engine.RecordSample(MakeSample("grow", 500 + (10 - i) * 200, 200, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.True(report.Profiles[0].ConsumptionTrend > 0);
        }

        [Fact]
        public void Analyze_ConsumptionTrendNegativeForShrinkingTokens()
        {
            var engine = CreateEngine();
            for (int i = 10; i > 0; i--)
                engine.RecordSample(MakeSample("shrink", 2500 - (10 - i) * 200, 200, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.True(report.Profiles[0].ConsumptionTrend < 0);
        }

        [Fact]
        public void Analyze_ProjectedMonthlySpend_Computed()
        {
            var engine = CreateEngine();
            // 10 samples over 10 hours = 24 per day = 720 per month
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("proj", 1000, 500, "gpt-4o", ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.True(report.Profiles[0].ProjectedMonthlySpend > 0);
        }

        [Fact]
        public void Analyze_QualityPerKToken_ComputedWhenQualityPresent()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
                engine.RecordSample(MakeSample("qp", 2000, 1000, quality: 80, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            Assert.True(report.Profiles[0].Efficiency.QualityPerKToken > 0);
        }

        [Fact]
        public void Analyze_DetectsBingeStarveCycle()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 20; i++)
            {
                var tokens = i % 2 == 0 ? 200 : 5000;
                engine.RecordSample(MakeSample("binge", tokens, tokens / 2, ts: DateTime.UtcNow.AddHours(-i)));
            }
            var report = engine.Analyze();
            Assert.Contains(report.Disorders, d => d.Disorder == MetabolicDisorder.BingeStarveCycle);
        }

        [Fact]
        public void Analyze_NoDisordersForHealthyUsage()
        {
            var engine = CreateEngine();
            var rng = new Random(123);
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("healthy", 1400 + rng.Next(0, 200), 650 + rng.Next(0, 100), "gpt-4o-mini", quality: 80, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            // Healthy usage with varied inputs should have no serious disorders
            var seriousDisorders = report.Disorders.Where(d => d.Severity >= DisorderSeverity.Moderate).ToList();
            Assert.Empty(seriousDisorders);
        }

        [Fact]
        public void Analyze_DisorderHasEvidence()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("ev", 200, 100, "gpt-4", quality: 85, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            foreach (var d in report.Disorders)
            {
                Assert.NotEmpty(d.Evidence);
            }
        }

        [Fact]
        public void Analyze_RecommendationHasSteps()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 10; i++)
                engine.RecordSample(MakeSample("steps", 200, 100, "gpt-4", quality: 85, ts: DateTime.UtcNow.AddHours(-i)));
            var report = engine.Analyze();
            foreach (var r in report.Recommendations)
            {
                Assert.NotEmpty(r.Steps);
            }
        }

        [Fact]
        public void Analyze_GeneratedAtIsRecent()
        {
            var engine = CreateEngine();
            engine.RecordSample(MakeSample("t", 1000, 500, ts: DateTime.UtcNow));
            var report = engine.Analyze();
            Assert.True((DateTime.UtcNow - report.GeneratedAt).TotalMinutes < 5);
        }

        [Fact]
        public void Analyze_PercentileInRange()
        {
            var engine = CreateEngine();
            for (int i = 0; i < 5; i++)
            {
                engine.RecordSample(MakeSample("low", 200, 100, ts: DateTime.UtcNow.AddHours(-i)));
                engine.RecordSample(MakeSample("high", 8000, 4000, ts: DateTime.UtcNow.AddHours(-i)));
            }
            var report = engine.Analyze();
            foreach (var p in report.Profiles)
            {
                Assert.InRange(p.Efficiency.Percentile, 0, 100);
            }
        }
    }
}
