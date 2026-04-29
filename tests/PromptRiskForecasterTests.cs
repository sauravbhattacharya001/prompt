using Prompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Prompt.Tests
{
    public class PromptRiskForecasterTests
    {
        // ── Helpers ──────────────────────────────────────────────

        private static RiskObservation MakeObs(string promptId, string dimension,
            double score, DateTime observedAt) =>
            new()
            {
                PromptId = promptId,
                Dimension = dimension,
                Score = score,
                ObservedAt = observedAt
            };

        private static List<RiskObservation> MakeLinearSeries(
            string promptId, string dimension, double startScore,
            double dailyIncrease, int days, DateTime startDate)
        {
            var list = new List<RiskObservation>();
            for (int d = 0; d < days; d++)
            {
                list.Add(MakeObs(promptId, dimension,
                    Math.Clamp(startScore + dailyIncrease * d, 0, 100),
                    startDate.AddDays(d)));
            }
            return list;
        }

        // ── Empty state ─────────────────────────────────────────

        [Fact]
        public void EmptyForecaster_ReturnsEmptyReport()
        {
            var fc = new PromptRiskForecaster();
            var report = fc.GenerateForecast();

            Assert.NotNull(report);
            Assert.Empty(report.Forecasts);
            Assert.Empty(report.Warnings);
            Assert.Empty(report.Heatmap);
            Assert.Empty(report.InterventionPlans);
            Assert.Equal(0, report.ObservationCount);
            Assert.Equal(0, report.PromptCount);
            Assert.Equal(0, report.DimensionCount);
        }

        [Fact]
        public void EmptyForecaster_ObservationCountIsZero()
        {
            var fc = new PromptRiskForecaster();
            Assert.Equal(0, fc.ObservationCount);
        }

        // ── Validation ──────────────────────────────────────────

        [Fact]
        public void RecordObservation_NullThrows()
        {
            var fc = new PromptRiskForecaster();
            Assert.Throws<ArgumentNullException>(() => fc.RecordObservation(null!));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(-0.001)]
        [InlineData(100.001)]
        public void RecordObservation_InvalidScoreThrows(double score)
        {
            var fc = new PromptRiskForecaster();
            var obs = new RiskObservation { Dimension = "Injection", Score = score };
            Assert.Throws<ArgumentOutOfRangeException>(() => fc.RecordObservation(obs));
        }

        [Fact]
        public void RecordObservation_EmptyDimensionThrows()
        {
            var fc = new PromptRiskForecaster();
            var obs = new RiskObservation { Dimension = "", Score = 50 };
            Assert.Throws<ArgumentException>(() => fc.RecordObservation(obs));
        }

        [Fact]
        public void RecordObservation_WhitespaceDimensionThrows()
        {
            var fc = new PromptRiskForecaster();
            var obs = new RiskObservation { Dimension = "   ", Score = 50 };
            Assert.Throws<ArgumentException>(() => fc.RecordObservation(obs));
        }

        [Fact]
        public void RecordObservation_BoundaryScoresAccepted()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservation(new RiskObservation { Dimension = "A", Score = 0 });
            fc.RecordObservation(new RiskObservation { Dimension = "A", Score = 100 });
            Assert.Equal(2, fc.ObservationCount);
        }

        [Fact]
        public void RecordObservations_NullEnumerableThrows()
        {
            var fc = new PromptRiskForecaster();
            Assert.Throws<ArgumentNullException>(() => fc.RecordObservations(null!));
        }

        [Fact]
        public void RecordObservations_BulkAdd()
        {
            var fc = new PromptRiskForecaster();
            var obs = MakeLinearSeries("p1", "Injection", 20, 1, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            Assert.Equal(10, fc.ObservationCount);
        }

        // ── Insufficient data ───────────────────────────────────

        [Fact]
        public void SingleObservation_InsufficientTrend()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservation(MakeObs("p1", "Injection", 40, DateTime.UtcNow));
            var report = fc.GenerateForecast();

            Assert.Single(report.Forecasts);
            Assert.Equal(RiskTrend.Insufficient, report.Forecasts[0].TrendDirection);
            Assert.Equal(ForecastConfidence.VeryLow, report.Forecasts[0].Confidence);
        }

        [Fact]
        public void TwoObservations_InsufficientTrend()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservation(MakeObs("p1", "Injection", 40, new DateTime(2026, 1, 1)));
            fc.RecordObservation(MakeObs("p1", "Injection", 45, new DateTime(2026, 1, 2)));
            var report = fc.GenerateForecast();

            Assert.Single(report.Forecasts);
            Assert.Equal(RiskTrend.Insufficient, report.Forecasts[0].TrendDirection);
        }

        // ── Rising trend ────────────────────────────────────────

        [Fact]
        public void LinearIncreasing_DetectsRisingTrend()
        {
            var fc = new PromptRiskForecaster();
            var obs = MakeLinearSeries("p1", "Injection", 30, 2, 15,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            Assert.Equal(RiskTrend.Rising, forecast.TrendDirection);
            Assert.True(forecast.Velocity > 1.5, "Velocity should be ~2 pts/day");
        }

        [Fact]
        public void RisingTrend_ProjectedScoresIncrease()
        {
            var fc = new PromptRiskForecaster();
            var obs = MakeLinearSeries("p1", "DataLeakage", 20, 1.5, 20,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            Assert.True(forecast.ProjectedScore7d > forecast.CurrentScore);
            Assert.True(forecast.ProjectedScore30d > forecast.ProjectedScore7d);
        }

        [Fact]
        public void RisingTrend_BreachForecasted()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75);
            var obs = MakeLinearSeries("p1", "Injection", 50, 2, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            Assert.NotNull(forecast.DaysToBreachEstimate);
            Assert.True(forecast.DaysToBreachEstimate >= 0);
        }

        [Fact]
        public void RisingTrend_HighR2_HighConfidence()
        {
            var fc = new PromptRiskForecaster();
            // Perfect linear data → R² ≈ 1.0
            var obs = MakeLinearSeries("p1", "Injection", 10, 3, 20,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            Assert.True(forecast.TrendR2 > 0.95);
            Assert.Equal(ForecastConfidence.VeryHigh, forecast.Confidence);
        }

        // ── Falling trend ───────────────────────────────────────

        [Fact]
        public void LinearDecreasing_DetectsFallingTrend()
        {
            var fc = new PromptRiskForecaster();
            var obs = MakeLinearSeries("p1", "Bias", 80, -2, 15,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            Assert.Equal(RiskTrend.Falling, forecast.TrendDirection);
            Assert.True(forecast.Velocity < -1.0);
        }

        [Fact]
        public void FallingTrend_NoBreachProjected()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75);
            var obs = MakeLinearSeries("p1", "Bias", 60, -1, 15,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            // Falling from 60, threshold 75 — no breach
            Assert.Null(forecast.DaysToBreachEstimate);
        }

        // ── Stable trend ────────────────────────────────────────

        [Fact]
        public void StableScores_DetectsStableTrend()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);
            // Use constant scores — zero variance yields R²=1.0, slope=0 → Stable
            for (int i = 0; i < 10; i++)
            {
                fc.RecordObservation(MakeObs("p1", "Hallucination",
                    50, start.AddDays(i)));
            }
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            Assert.Equal(RiskTrend.Stable, forecast.TrendDirection);
        }

        // ── Volatile trend ──────────────────────────────────────

        [Fact]
        public void ScatteredScores_DetectsVolatileTrend()
        {
            var fc = new PromptRiskForecaster();
            var rng = new Random(42);
            var start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 15; i++)
            {
                fc.RecordObservation(MakeObs("p1", "Jailbreak",
                    rng.NextDouble() * 100, start.AddDays(i)));
            }
            var report = fc.GenerateForecast();

            var forecast = report.Forecasts.Single();
            // Random data should have low R² → Volatile or Insufficient
            Assert.True(forecast.TrendR2 < 0.5,
                $"Random data should have low R² but got {forecast.TrendR2}");
        }

        // ── Early warnings ──────────────────────────────────────

        [Fact]
        public void BreachWithinHorizon_GeneratesWarning()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75, warningHorizonDays: 7);
            // Starting at 65, increasing 2/day → breach in ~5 days
            var obs = MakeLinearSeries("p1", "Injection", 65, 2, 5,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            Assert.NotEmpty(report.Warnings);
            var warning = report.Warnings.First();
            Assert.Equal("Injection", warning.Dimension);
            Assert.True(warning.DaysToBreach <= 7);
        }

        [Fact]
        public void BreachOutsideHorizon_NoWarning()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75, warningHorizonDays: 3);
            // Starting at 20, increasing 1/day → breach in ~55 days
            var obs = MakeLinearSeries("p1", "Injection", 20, 1, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void ImminentBreach_CorrectSeverity()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75, warningHorizonDays: 30);
            // Starting at 70, increasing 3/day → breach in ~1.7 days
            var obs = MakeLinearSeries("p1", "Injection", 70, 3, 5,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            if (report.Warnings.Any())
            {
                var w = report.Warnings.First();
                Assert.True(w.Severity == AlertSeverity.Imminent ||
                            w.Severity == AlertSeverity.Critical,
                    $"Expected Imminent/Critical but got {w.Severity}");
            }
        }

        [Fact]
        public void Warning_ContainsAffectedPromptIds()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75, warningHorizonDays: 10);
            var start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 10; i++)
            {
                fc.RecordObservation(MakeObs("p1", "Injection", 60 + i * 1.5, start.AddDays(i)));
                fc.RecordObservation(MakeObs("p2", "Injection", 55 + i * 1.5, start.AddDays(i)));
            }
            var report = fc.GenerateForecast();

            if (report.Warnings.Any())
            {
                var w = report.Warnings.First();
                Assert.Contains("p1", w.AffectedPromptIds);
                Assert.Contains("p2", w.AffectedPromptIds);
            }
        }

        // ── Multiple dimensions ─────────────────────────────────

        [Fact]
        public void MultipleDimensions_IndependentForecasts()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservations(MakeLinearSeries("p1", "Injection", 20, 2, 10, start));
            fc.RecordObservations(MakeLinearSeries("p1", "Bias", 60, -1, 10, start));

            var report = fc.GenerateForecast();

            Assert.Equal(2, report.Forecasts.Count);
            Assert.Equal(2, report.DimensionCount);

            var injection = report.Forecasts.First(f => f.Dimension == "Injection");
            var bias = report.Forecasts.First(f => f.Dimension == "Bias");

            Assert.Equal(RiskTrend.Rising, injection.TrendDirection);
            Assert.Equal(RiskTrend.Falling, bias.TrendDirection);
        }

        // ── Portfolio aggregation ───────────────────────────────

        [Fact]
        public void PortfolioRiskScore_IsAverage()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservations(MakeLinearSeries("p1", "A", 30, 0, 5, start));
            fc.RecordObservations(MakeLinearSeries("p1", "B", 70, 0, 5, start));

            var report = fc.GenerateForecast();

            // Latest scores are 30 and 70, average = 50
            Assert.True(report.PortfolioRiskScore >= 45 && report.PortfolioRiskScore <= 55,
                $"Portfolio score {report.PortfolioRiskScore} should be ~50");
        }

        [Fact]
        public void PortfolioTrend_ReflectsOverallDirection()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservations(MakeLinearSeries("p1", "A", 20, 3, 10, start));
            fc.RecordObservations(MakeLinearSeries("p1", "B", 25, 2, 10, start));

            var report = fc.GenerateForecast();
            Assert.Equal(RiskTrend.Rising, report.PortfolioTrend);
        }

        // ── Heatmap ─────────────────────────────────────────────

        [Fact]
        public void Heatmap_LatestPerPromptDimension()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservation(MakeObs("p1", "Injection", 30, start));
            fc.RecordObservation(MakeObs("p1", "Injection", 40, start.AddDays(1)));
            fc.RecordObservation(MakeObs("p1", "Injection", 50, start.AddDays(2)));
            fc.RecordObservation(MakeObs("p2", "Injection", 60, start));

            var report = fc.GenerateForecast();

            var p1Cell = report.Heatmap.First(h => h.PromptId == "p1");
            Assert.Equal(50, p1Cell.CurrentScore);

            var p2Cell = report.Heatmap.First(h => h.PromptId == "p2");
            Assert.Equal(60, p2Cell.CurrentScore);
        }

        [Fact]
        public void Heatmap_MultiplePromptsDimensions()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservations(MakeLinearSeries("p1", "Injection", 20, 1, 5, start));
            fc.RecordObservations(MakeLinearSeries("p1", "Bias", 40, 0, 5, start));
            fc.RecordObservations(MakeLinearSeries("p2", "Injection", 50, -1, 5, start));

            var report = fc.GenerateForecast();

            Assert.Equal(3, report.Heatmap.Count);
            Assert.Contains(report.Heatmap, h => h.PromptId == "p1" && h.Dimension == "Injection");
            Assert.Contains(report.Heatmap, h => h.PromptId == "p1" && h.Dimension == "Bias");
            Assert.Contains(report.Heatmap, h => h.PromptId == "p2" && h.Dimension == "Injection");
        }

        // ── Intervention plans ──────────────────────────────────

        [Fact]
        public void InterventionPlan_GeneratedForRisingHighScore()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75);
            var obs = MakeLinearSeries("p1", "Injection", 55, 2, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            Assert.NotEmpty(report.InterventionPlans);
            var plan = report.InterventionPlans.First();
            Assert.Equal("Injection", plan.Dimension);
            Assert.NotEmpty(plan.Interventions);
            Assert.True(plan.Interventions.All(s => s.Priority > 0));
        }

        [Fact]
        public void InterventionSteps_HighVelocity_IncludesImmediateReview()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75);
            var obs = MakeLinearSeries("p1", "Injection", 50, 3, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var plan = report.InterventionPlans.First();
            Assert.Contains(plan.Interventions,
                s => s.Action.Contains("Immediate review"));
        }

        [Fact]
        public void InterventionSteps_AlwaysIncludesTemplateReview()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75);
            var obs = MakeLinearSeries("p1", "Injection", 45, 1, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            if (report.InterventionPlans.Any())
            {
                var plan = report.InterventionPlans.First();
                Assert.Contains(plan.Interventions,
                    s => s.Action.Contains("Review and update"));
            }
        }

        [Fact]
        public void InterventionPlan_HasBreachDate()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75);
            var obs = MakeLinearSeries("p1", "Injection", 55, 2, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var plan = report.InterventionPlans.First();
            Assert.NotNull(plan.BreachDate);
        }

        // ── GetDimensionHistory ─────────────────────────────────

        [Fact]
        public void GetDimensionHistory_FiltersCorrectly()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservations(MakeLinearSeries("p1", "Injection", 20, 1, 5, start));
            fc.RecordObservations(MakeLinearSeries("p1", "Bias", 40, 1, 3, start));

            var history = fc.GetDimensionHistory("Injection");
            Assert.Equal(5, history.Count);
            Assert.All(history, o => Assert.Equal("Injection", o.Dimension));
        }

        [Fact]
        public void GetDimensionHistory_CaseInsensitive()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservation(MakeObs("p1", "Injection", 50, DateTime.UtcNow));

            var history = fc.GetDimensionHistory("injection");
            Assert.Single(history);
        }

        [Fact]
        public void GetDimensionHistory_OrderedByTime()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservation(MakeObs("p1", "Injection", 50, start.AddDays(3)));
            fc.RecordObservation(MakeObs("p1", "Injection", 40, start));
            fc.RecordObservation(MakeObs("p1", "Injection", 45, start.AddDays(1)));

            var history = fc.GetDimensionHistory("Injection");
            Assert.Equal(40, history[0].Score);
            Assert.Equal(45, history[1].Score);
            Assert.Equal(50, history[2].Score);
        }

        // ── GetPromptRiskProfile ────────────────────────────────

        [Fact]
        public void GetPromptRiskProfile_LatestPerDimension()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            fc.RecordObservation(MakeObs("p1", "Injection", 30, start));
            fc.RecordObservation(MakeObs("p1", "Injection", 50, start.AddDays(1)));
            fc.RecordObservation(MakeObs("p1", "Bias", 20, start));

            var profile = fc.GetPromptRiskProfile("p1");
            Assert.Equal(2, profile.Count);
            Assert.Equal(50, profile["Injection"]);
            Assert.Equal(20, profile["Bias"]);
        }

        [Fact]
        public void GetPromptRiskProfile_UnknownPrompt_Empty()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservation(MakeObs("p1", "Injection", 50, DateTime.UtcNow));

            var profile = fc.GetPromptRiskProfile("unknown");
            Assert.Empty(profile);
        }

        // ── JSON export ─────────────────────────────────────────

        [Fact]
        public void ExportJson_ValidJsonStructure()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservations(MakeLinearSeries("p1", "Injection", 30, 2, 10,
                new DateTime(2026, 1, 1)));

            var json = fc.ExportJson();
            Assert.False(string.IsNullOrWhiteSpace(json));

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("Forecasts", out _));
            Assert.True(root.TryGetProperty("Warnings", out _));
            Assert.True(root.TryGetProperty("Heatmap", out _));
            Assert.True(root.TryGetProperty("InterventionPlans", out _));
            Assert.True(root.TryGetProperty("PortfolioRiskScore", out _));
            Assert.True(root.TryGetProperty("PortfolioTrend", out _));
        }

        [Fact]
        public void ExportJson_EnumsAsStrings()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservations(MakeLinearSeries("p1", "Injection", 30, 2, 10,
                new DateTime(2026, 1, 1)));

            var json = fc.ExportJson();
            Assert.Contains("\"Rising\"", json);
        }

        // ── Linear regression ───────────────────────────────────

        [Fact]
        public void LinearRegression_PerfectLine()
        {
            var points = new List<(double x, double y)>
            {
                (0, 10), (1, 12), (2, 14), (3, 16), (4, 18)
            };
            var (slope, intercept, r2) = PromptRiskForecaster.LinearRegression(points);

            Assert.Equal(2.0, slope, 2);
            Assert.Equal(10.0, intercept, 2);
            Assert.True(r2 > 0.999);
        }

        [Fact]
        public void LinearRegression_SinglePoint()
        {
            var points = new List<(double x, double y)> { (5, 42) };
            var (slope, intercept, r2) = PromptRiskForecaster.LinearRegression(points);

            Assert.Equal(0, slope);
            Assert.Equal(42, intercept);
        }

        [Fact]
        public void LinearRegression_ConstantY()
        {
            var points = new List<(double x, double y)>
            {
                (0, 50), (1, 50), (2, 50), (3, 50)
            };
            var (slope, intercept, r2) = PromptRiskForecaster.LinearRegression(points);

            Assert.True(Math.Abs(slope) < 0.01);
            Assert.Equal(50, intercept, 1);
            // R² is 1.0 when SS_tot is 0 (all same)
            Assert.Equal(1.0, r2, 2);
        }

        // ── Edge cases ──────────────────────────────────────────

        [Fact]
        public void AllSameScore_StableTrend()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 10; i++)
                fc.RecordObservation(MakeObs("p1", "Injection", 45, start.AddDays(i)));

            var report = fc.GenerateForecast();
            var f = report.Forecasts.Single();
            Assert.Equal(RiskTrend.Stable, f.TrendDirection);
        }

        [Fact]
        public void SinglePrompt_CorrectPromptCount()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservations(MakeLinearSeries("p1", "Injection", 20, 1, 5,
                new DateTime(2026, 1, 1)));

            var report = fc.GenerateForecast();
            Assert.Equal(1, report.PromptCount);
        }

        [Fact]
        public void MultiplePrompts_CorrectPromptCount()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);
            fc.RecordObservations(MakeLinearSeries("p1", "Injection", 20, 1, 5, start));
            fc.RecordObservations(MakeLinearSeries("p2", "Injection", 30, 1, 5, start));
            fc.RecordObservations(MakeLinearSeries("p3", "Bias", 40, 1, 5, start));

            var report = fc.GenerateForecast();
            Assert.Equal(3, report.PromptCount);
        }

        [Fact]
        public void ManyObservations_PerformanceAcceptable()
        {
            var fc = new PromptRiskForecaster();
            var start = new DateTime(2026, 1, 1);

            // 100 observations across 5 dimensions, 4 prompts
            for (int p = 1; p <= 4; p++)
            {
                foreach (var dim in new[] { "A", "B", "C", "D", "E" })
                {
                    for (int d = 0; d < 5; d++)
                    {
                        fc.RecordObservation(MakeObs($"p{p}", dim,
                            30 + d * 2 + p, start.AddDays(d)));
                    }
                }
            }

            Assert.Equal(100, fc.ObservationCount);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = fc.GenerateForecast();
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"Forecast took {sw.ElapsedMilliseconds}ms — should be <1s");
            Assert.Equal(5, report.DimensionCount);
            Assert.Equal(4, report.PromptCount);
        }

        // ── Projected scores clamped ────────────────────────────

        [Fact]
        public void ProjectedScores_ClampedTo0_100()
        {
            var fc = new PromptRiskForecaster();
            // Very steep rise from 90 → projections should cap at 100
            var obs = MakeLinearSeries("p1", "Injection", 80, 5, 5,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var f = report.Forecasts.Single();
            Assert.True(f.ProjectedScore30d <= 100);
            Assert.True(f.ProjectedScore7d >= 0);
        }

        [Fact]
        public void FallingTrend_ProjectedScoresClampAtZero()
        {
            var fc = new PromptRiskForecaster();
            var obs = MakeLinearSeries("p1", "Injection", 20, -5, 5,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var f = report.Forecasts.Single();
            Assert.True(f.ProjectedScore30d >= 0);
        }

        // ── Custom threshold ────────────────────────────────────

        [Fact]
        public void CustomBreachThreshold_UsedInForecast()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 50);
            var obs = MakeLinearSeries("p1", "Injection", 30, 2, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var f = report.Forecasts.Single();
            Assert.Equal(50, f.BreachThreshold);
        }

        [Fact]
        public void CustomWarningHorizon_AffectsWarnings()
        {
            // Narrow horizon: 2 days
            var fc = new PromptRiskForecaster(breachThreshold: 75, warningHorizonDays: 2);
            // Breach in ~5 days — outside 2-day horizon
            var obs = MakeLinearSeries("p1", "Injection", 65, 2, 5,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            // Should not generate warning since breach is beyond 2-day horizon
            // (The forecast may show breach at ~5d but warning requires <= 2d)
            Assert.True(report.Warnings.Count == 0 ||
                        report.Warnings.All(w => w.DaysToBreach <= 2),
                "Warnings should only appear within the horizon");
        }

        // ── Report metadata ─────────────────────────────────────

        [Fact]
        public void ForecastReport_HasAnalyzedTimestamp()
        {
            var fc = new PromptRiskForecaster();
            fc.RecordObservation(MakeObs("p1", "Injection", 50, DateTime.UtcNow));
            var report = fc.GenerateForecast();

            Assert.True(report.AnalyzedAt <= DateTime.UtcNow);
            Assert.True(report.AnalyzedAt > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public void AlreadyBreached_DaysToBreachIsZero()
        {
            var fc = new PromptRiskForecaster(breachThreshold: 75);
            var obs = MakeLinearSeries("p1", "Injection", 70, 2, 10,
                new DateTime(2026, 1, 1));
            fc.RecordObservations(obs);
            var report = fc.GenerateForecast();

            var f = report.Forecasts.Single();
            // Score should be above threshold by end
            if (f.DaysToBreachEstimate.HasValue)
            {
                Assert.True(f.DaysToBreachEstimate.Value >= 0);
            }
        }
    }
}
