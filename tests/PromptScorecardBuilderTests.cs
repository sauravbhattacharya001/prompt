namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptScorecardBuilderTests
    {
        [Fact]
        public void Build_WithCriteria_CreatesScorecard()
        {
            var sc = new PromptScorecardBuilder("Test")
                .AddCriterion("accuracy", "Is the answer correct", 2.0)
                .AddCriterion("clarity", "Is the answer clear", 1.0)
                .Build();

            Assert.Equal("Test", sc.Name);
            Assert.Equal(2, sc.CriterionNames.Count);
            Assert.Contains("accuracy", sc.CriterionNames);
        }

        [Fact]
        public void Build_NoCriteria_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new PromptScorecardBuilder("Empty").Build());
        }

        [Fact]
        public void Build_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PromptScorecardBuilder(""));
        }

        [Fact]
        public void AddCriterion_DuplicateName_Throws()
        {
            var b = new PromptScorecardBuilder("Dup")
                .AddCriterion("x", "first", 1.0);
            Assert.Throws<InvalidOperationException>(() => b.AddCriterion("x", "second", 1.0));
        }

        [Fact]
        public void AddCriterion_NegativeWeight_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PromptScorecardBuilder("W").AddCriterion("c", "d", -1.0));
        }

        [Fact]
        public void Score_EqualWeights_AveragesScores()
        {
            var sc = new PromptScorecardBuilder("Avg")
                .AddCriterion("a", "", 1.0)
                .AddCriterion("b", "", 1.0)
                .Build();

            var result = sc.Score(new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 0.5 });
            Assert.Equal(0.75, result.WeightedScore, 2);
        }

        [Fact]
        public void Score_WeightedCriteria_WeightsCorrectly()
        {
            var sc = new PromptScorecardBuilder("Weighted")
                .AddCriterion("important", "", 3.0)
                .AddCriterion("minor", "", 1.0)
                .Build();

            var result = sc.Score(new Dictionary<string, double>
                { ["important"] = 1.0, ["minor"] = 0.0 });
            Assert.Equal(0.75, result.WeightedScore, 2);
        }

        [Fact]
        public void Score_MissingCriterion_DefaultsToZero()
        {
            var sc = new PromptScorecardBuilder("Miss")
                .AddCriterion("present", "", 1.0)
                .AddCriterion("missing", "", 1.0)
                .Build();

            var result = sc.Score(new Dictionary<string, double> { ["present"] = 1.0 });
            Assert.Equal(0.5, result.WeightedScore, 2);
        }

        [Fact]
        public void Score_ClampsToRange()
        {
            var sc = new PromptScorecardBuilder("Clamp")
                .AddCriterion("x", "", 1.0)
                .Build();

            var result = sc.Score(new Dictionary<string, double> { ["x"] = 1.5 });
            Assert.Equal(1.0, result.WeightedScore, 2);
        }

        [Fact]
        public void DefaultGradeThresholds_AssignCorrectGrades()
        {
            var sc = new PromptScorecardBuilder("Grades")
                .AddCriterion("x", "", 1.0)
                .Build();

            Assert.Equal("A", sc.Score(new() { ["x"] = 0.95 }).Grade);
            Assert.Equal("B", sc.Score(new() { ["x"] = 0.85 }).Grade);
            Assert.Equal("C", sc.Score(new() { ["x"] = 0.75 }).Grade);
            Assert.Equal("D", sc.Score(new() { ["x"] = 0.65 }).Grade);
            Assert.Equal("F", sc.Score(new() { ["x"] = 0.3 }).Grade);
        }

        [Fact]
        public void CustomGradeThresholds_Work()
        {
            var sc = new PromptScorecardBuilder("Custom")
                .AddCriterion("x", "", 1.0)
                .WithGradeThresholds(new() { ["Pass"] = 0.5, ["Fail"] = 0.0 })
                .Build();

            Assert.Equal("Pass", sc.Score(new() { ["x"] = 0.7 }).Grade);
            Assert.Equal("Fail", sc.Score(new() { ["x"] = 0.3 }).Grade);
        }

        [Fact]
        public void WithGradeThresholds_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PromptScorecardBuilder("T")
                    .WithGradeThresholds(new Dictionary<string, double>()));
        }

        [Fact]
        public void AutoScore_UsesAutoScorer()
        {
            var sc = new PromptScorecardBuilder("Auto")
                .AddAutoScoredCriterion("length", "Longer is better",
                    text => Math.Min(text.Length / 100.0, 1.0), 1.0)
                .Build();

            var short_ = sc.AutoScore("hi");
            var long_ = sc.AutoScore(new string('x', 100));
            Assert.True(long_.WeightedScore > short_.WeightedScore);
        }

        [Fact]
        public void AutoScore_FallsBackToManual()
        {
            var sc = new PromptScorecardBuilder("Fallback")
                .AddCriterion("manual", "No auto-scorer", 1.0)
                .Build();

            var result = sc.AutoScore("text", new() { ["manual"] = 0.8 });
            Assert.Equal(0.8, result.WeightedScore, 2);
        }

        [Fact]
        public void AutoScoredCriterion_NullScorer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PromptScorecardBuilder("T")
                    .AddAutoScoredCriterion("x", "d", null!, 1.0));
        }

        [Fact]
        public void Compare_RanksCorrectly()
        {
            var sc = new PromptScorecardBuilder("Cmp")
                .AddCriterion("q", "", 1.0)
                .Build();

            var ranked = sc.Compare(new()
            {
                ["modelA"] = new() { ["q"] = 0.9 },
                ["modelB"] = new() { ["q"] = 0.5 },
                ["modelC"] = new() { ["q"] = 0.7 }
            });

            Assert.Equal(3, ranked.Count);
            Assert.Equal("modelA", ranked[0].Label);
            Assert.Equal(1, ranked[0].Rank);
            Assert.Equal("modelC", ranked[1].Label);
            Assert.Equal("modelB", ranked[2].Label);
        }

        [Fact]
        public void Compare_Empty_Throws()
        {
            var sc = new PromptScorecardBuilder("E")
                .AddCriterion("x", "", 1.0)
                .Build();

            Assert.Throws<ArgumentException>(() =>
                sc.Compare(new Dictionary<string, Dictionary<string, double>>()));
        }

        [Fact]
        public void Passed_ReflectsThreshold()
        {
            var sc = new PromptScorecardBuilder("P")
                .AddCriterion("x", "", 1.0)
                .Build();

            Assert.True(sc.Score(new() { ["x"] = 0.6 }).Passed);
            Assert.False(sc.Score(new() { ["x"] = 0.3 }).Passed);
        }

        [Fact]
        public void FormatReport_ContainsCriterionNames()
        {
            var sc = new PromptScorecardBuilder("Report")
                .AddCriterion("accuracy", "desc", 1.0)
                .Build();

            var report = Scorecard.FormatReport(sc.Score(new() { ["accuracy"] = 0.8 }));
            Assert.Contains("accuracy", report);
            Assert.Contains("Report", report);
            Assert.Contains("Grade:", report);
        }

        [Fact]
        public void ToJson_AndFromJson_Roundtrips()
        {
            var original = new PromptScorecardBuilder("RT")
                .WithDescription("roundtrip test")
                .AddTag("test")
                .AddCriterion("a", "alpha", 2.0)
                .AddCriterion("b", "beta", 1.0)
                .WithGradeThresholds(new() { ["Good"] = 0.7, ["Bad"] = 0.0 });

            var json = original.ToJson();
            var restored = PromptScorecardBuilder.FromJson(json).Build();

            Assert.Equal("RT", restored.Name);
            Assert.Equal(2, restored.CriterionNames.Count);
            Assert.Equal("Good", restored.Score(new() { ["a"] = 0.9, ["b"] = 0.8 }).Grade);
        }

        [Fact]
        public void FromJson_EmptyJson_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptScorecardBuilder.FromJson(""));
        }

        [Fact]
        public void WithDescription_SetsDescription()
        {
            var sc = new PromptScorecardBuilder("D")
                .WithDescription("Hello")
                .AddCriterion("x", "", 1.0)
                .Build();
            Assert.Equal("Hello", sc.Description);
        }

        [Fact]
        public void AddTag_DeduplicatesTags()
        {
            var sc = new PromptScorecardBuilder("Tags")
                .AddTag("qa").AddTag("qa").AddTag("prod")
                .AddCriterion("x", "", 1.0)
                .Build();
            Assert.Equal(2, sc.Tags.Count);
        }

        [Fact]
        public void CriterionResult_ShowsSource()
        {
            var sc = new PromptScorecardBuilder("Src")
                .AddCriterion("manual", "", 1.0)
                .AddAutoScoredCriterion("auto", "", _ => 0.5, 1.0)
                .Build();

            var result = sc.AutoScore("text", new() { ["manual"] = 0.7 });
            var manualCr = result.CriterionResults.First(c => c.Name == "manual");
            var autoCr = result.CriterionResults.First(c => c.Name == "auto");
            Assert.Equal("manual", manualCr.Source);
            Assert.Equal("auto", autoCr.Source);
        }

        [Fact]
        public void ToJson_IncludesAllFields()
        {
            var json = new PromptScorecardBuilder("Full")
                .WithDescription("desc")
                .AddTag("t1")
                .AddCriterion("c1", "d1", 2.5)
                .AddAutoScoredCriterion("c2", "d2", _ => 0.5, 1.0)
                .ToJson();

            Assert.Contains("\"name\": \"Full\"", json);
            Assert.Contains("\"description\": \"desc\"", json);
            Assert.Contains("\"t1\"", json);
            Assert.Contains("\"c1\"", json);
            Assert.Contains("\"hasAutoScorer\": true", json);
        }

        [Fact]
        public void ScorecardResult_ToJson_IsValid()
        {
            var sc = new PromptScorecardBuilder("J")
                .AddCriterion("x", "", 1.0)
                .Build();

            var json = sc.Score(new() { ["x"] = 0.8 }).ToJson();
            Assert.Contains("\"WeightedScore\"", json);
            Assert.Contains("\"Grade\"", json);
        }

        [Fact]
        public void Score_AllZeroWeights_ReturnsZero()
        {
            var sc = new PromptScorecardBuilder("Zero")
                .AddCriterion("x", "", 0.0)
                .Build();

            Assert.Equal(0.0, sc.Score(new() { ["x"] = 1.0 }).WeightedScore);
        }

        [Fact]
        public void MultipleAutoScorers_AllEvaluated()
        {
            var sc = new PromptScorecardBuilder("Multi")
                .AddAutoScoredCriterion("has_hello", "", t => t.Contains("hello") ? 1.0 : 0.0, 1.0)
                .AddAutoScoredCriterion("has_world", "", t => t.Contains("world") ? 1.0 : 0.0, 1.0)
                .Build();

            var both = sc.AutoScore("hello world");
            var one = sc.AutoScore("hello");
            Assert.Equal(1.0, both.WeightedScore, 2);
            Assert.Equal(0.5, one.WeightedScore, 2);
        }

        [Fact]
        public void Compare_SingleEntry_GetsRankOne()
        {
            var sc = new PromptScorecardBuilder("Single")
                .AddCriterion("x", "", 1.0)
                .Build();

            var ranked = sc.Compare(new() { ["only"] = new() { ["x"] = 0.5 } });
            Assert.Single(ranked);
            Assert.Equal(1, ranked[0].Rank);
        }
    }
}
