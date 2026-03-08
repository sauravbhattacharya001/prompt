namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Xunit;

    public class PromptEnsembleTests
    {
        [Fact]
        public void MajorityVote_PicksMostCommonResponse()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("2+2?", 5));
            e.AddResponses("4", "4", "4", "3", "5");
            var r = e.Aggregate();
            Assert.Equal("4", r.SelectedResponse);
            Assert.Equal(0.6, r.Confidence, 2);
            Assert.True(r.ConsensusReached);
            Assert.Equal(3, r.ClusterCount);
        }

        [Fact]
        public void MajorityVote_Normalized_IgnoresCaseAndWhitespace()
        {
            var c = EnsembleConfig.SelfConsistency("Capital?", 3);
            c.MatchMode = FuzzyMatchMode.Normalized;
            var e = new PromptEnsemble(c);
            e.AddResponses("Paris", "paris", "  PARIS  ");
            Assert.Equal(1.0, e.Aggregate().Confidence, 2);
        }

        [Fact]
        public void MajorityVote_Exact_CaseSensitive()
        {
            var c = EnsembleConfig.SelfConsistency("Capital?", 3);
            c.MatchMode = FuzzyMatchMode.Exact;
            var e = new PromptEnsemble(c);
            e.AddResponses("Paris", "paris", "Paris");
            var r = e.Aggregate();
            Assert.Equal(2.0 / 3, r.Confidence, 2);
            Assert.Equal(2, r.ClusterCount);
        }

        [Fact]
        public void MajorityVote_Jaccard_ClustersSimilar()
        {
            var c = EnsembleConfig.SelfConsistency("Benefits?", 3);
            c.MatchMode = FuzzyMatchMode.Jaccard;
            c.JaccardThreshold = 0.5;
            var e = new PromptEnsemble(c);
            e.AddResponses("Better health and more energy", "More energy and better health", "Cats are great");
            Assert.Equal(2, e.Aggregate().ClusterCount);
        }

        [Fact]
        public void BestOfN_PicksHighestScore()
        {
            var c = EnsembleConfig.PromptVariants(("a", "p1"), ("b", "p2"), ("c", "p3"));
            c.Strategy = EnsembleStrategy.BestOfN;
            c.Scorer = s => s.Length;
            var e = new PromptEnsemble(c);
            e.AddResponse("a", "Yes");
            e.AddResponse("b", "Yes, correct.");
            e.AddResponse("c", "Yes, absolutely correct and well-reasoned.");
            Assert.Equal("c", e.Aggregate().WinnerLabel);
        }

        [Fact]
        public void Consensus_Reached()
        {
            var c = EnsembleConfig.CrossModel("France?", "gpt-4", "claude", "gemini");
            c.ConsensusThreshold = 0.6;
            var e = new PromptEnsemble(c);
            e.AddResponse("gpt-4", "Paris");
            e.AddResponse("claude", "Paris.");
            e.AddResponse("gemini", "London");
            var r = e.Aggregate();
            Assert.True(r.ConsensusReached);
            Assert.Equal(2.0 / 3, r.Confidence, 2);
        }

        [Fact]
        public void Consensus_NotReached()
        {
            var c = EnsembleConfig.CrossModel("Year?", "a", "b", "c");
            c.ConsensusThreshold = 0.8;
            var e = new PromptEnsemble(c);
            e.AddResponse("a", "2020");
            e.AddResponse("b", "2021");
            e.AddResponse("c", "2020");
            var r = e.Aggregate();
            Assert.False(r.ConsensusReached);
            Assert.Empty(r.SelectedResponse);
        }

        [Fact]
        public void Random_PicksValid()
        {
            var c = EnsembleConfig.SelfConsistency("T", 3);
            c.Strategy = EnsembleStrategy.Random;
            var e = new PromptEnsemble(c, seed: 42);
            e.AddResponses("A", "B", "C");
            Assert.Contains(e.Aggregate().SelectedResponse, new[] { "A", "B", "C" });
        }

        [Fact]
        public void Custom_Works()
        {
            var c = EnsembleConfig.SelfConsistency("T", 2);
            c.Strategy = EnsembleStrategy.Custom;
            c.CustomAggregator = rs => new EnsembleAggregation
            {
                SelectedResponse = string.Join("+", rs.Select(r => r.Response)),
                WinnerLabel = "combined", Confidence = 1.0, ConsensusReached = true, ClusterCount = 1
            };
            var e = new PromptEnsemble(c);
            e.AddResponses("A", "B");
            Assert.Equal("A+B", e.Aggregate().SelectedResponse);
        }

        [Fact]
        public void Custom_ThrowsWithoutAggregator()
        {
            var c = EnsembleConfig.SelfConsistency("T", 2);
            c.Strategy = EnsembleStrategy.Custom;
            var e = new PromptEnsemble(c);
            e.AddResponses("A", "B");
            Assert.Throws<InvalidOperationException>(() => e.Aggregate());
        }

        [Fact]
        public void AddError_ExcludedFromAggregation()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 3));
            e.AddResponse("run-1", "OK");
            e.AddError("run-2", "timeout");
            e.AddResponse("run-3", "OK");
            var r = e.Aggregate();
            Assert.Equal("OK", r.SelectedResponse);
            Assert.Single(r.Members.Where(m => m.IsError));
        }

        [Fact]
        public void AllErrors_Throws()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            e.AddError("run-1", "f");
            e.AddError("run-2", "f");
            Assert.Throws<InvalidOperationException>(() => e.Aggregate());
        }

        [Fact] public void DuplicateLabel_Throws()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            e.AddResponse("run-1", "OK");
            Assert.Throws<InvalidOperationException>(() => e.AddResponse("run-1", "X"));
        }

        [Fact] public void UnknownLabel_Throws()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            Assert.Throws<ArgumentException>(() => e.AddResponse("nope", "X"));
        }

        [Fact] public void SelfConsistency_Min2() => Assert.Throws<ArgumentException>(() => EnsembleConfig.SelfConsistency("T", 1));
        [Fact] public void CrossModel_Min2() => Assert.Throws<ArgumentException>(() => EnsembleConfig.CrossModel("T", "x"));
        [Fact] public void Variants_Min2() => Assert.Throws<ArgumentException>(() => EnsembleConfig.PromptVariants(("a", "t")));
        [Fact] public void Constructor_Min2()
        {
            var c = new EnsembleConfig(); c.Members.Add(new EnsembleMember { Label = "x" });
            Assert.Throws<ArgumentException>(() => new PromptEnsemble(c));
        }

        [Fact]
        public void Reset_Clears()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            e.AddResponses("A", "B");
            Assert.True(e.IsComplete);
            e.Reset();
            Assert.Equal(0, e.ResponseCount);
        }

        [Fact]
        public void IsComplete_Tracking()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            Assert.False(e.IsComplete);
            e.AddResponse("run-1", "A");
            Assert.False(e.IsComplete);
            e.AddResponse("run-2", "B");
            Assert.True(e.IsComplete);
        }

        [Fact]
        public void ToJson_Valid()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            e.AddResponses("A", "A");
            var j = e.ToJson();
            Assert.Contains("MajorityVote", j);
            Assert.Contains("\"Confidence\": 1", j);
        }

        [Fact]
        public void Duration_Tracked()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            e.AddResponse("run-1", "A", TimeSpan.FromSeconds(1));
            e.AddResponse("run-2", "A", TimeSpan.FromSeconds(3));
            Assert.Equal(TimeSpan.FromSeconds(3), e.Aggregate().TotalDuration);
        }

        [Fact]
        public void Summary_Format()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 3));
            e.AddResponses("X", "X", "Y");
            var s = e.Aggregate().Summary;
            Assert.Contains("MajorityVote", s);
            Assert.Contains("members=3", s);
        }

        [Fact]
        public void AddResponses_TooMany_Throws()
        {
            var e = new PromptEnsemble(EnsembleConfig.SelfConsistency("T", 2));
            Assert.Throws<ArgumentException>(() => e.AddResponses("A", "B", "C"));
        }
    }
}
