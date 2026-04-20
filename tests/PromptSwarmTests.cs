namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptSwarmTests
    {
        [Fact]
        public void AddMember_IncreasesCount()
        {
            var swarm = new PromptSwarm();
            swarm.AddMember("a", SwarmRole.Contributor, "Test", 1.0);
            Assert.Single(swarm.Members);
        }

        [Fact]
        public void AddDefaultTeam_Adds5Members()
        {
            var swarm = new PromptSwarm().AddDefaultTeam();
            Assert.Equal(5, swarm.Members.Count);
        }

        [Fact]
        public void RemoveMember_Works()
        {
            var swarm = new PromptSwarm().AddDefaultTeam();
            Assert.True(swarm.RemoveMember("analyst"));
            Assert.Equal(4, swarm.Members.Count);
        }

        [Fact]
        public void Deliberate_MajorityVote_PicksMajority()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0)
                .AddMember("b", SwarmRole.Contributor, "", 1.0)
                .AddMember("c", SwarmRole.Challenger, "", 1.0)
                .WithStrategy(SwarmConsensusStrategy.MajorityVote);

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["a"] = ("Answer A", 0.9, "reason"),
                ["b"] = ("Answer A", 0.8, "reason"),
                ["c"] = ("Answer B", 0.7, "reason")
            };

            var result = swarm.Deliberate("test query", responses);
            Assert.True(result.ConsensusReached);
            Assert.Equal("Answer A", result.Winner?.Text);
            Assert.Single(result.Dissents);
        }

        [Fact]
        public void Deliberate_WeightedConfidence_PicksHighestWeighted()
        {
            var swarm = new PromptSwarm()
                .AddMember("low", SwarmRole.Contributor, "", 0.3)
                .AddMember("high", SwarmRole.Contributor, "", 1.0)
                .WithStrategy(SwarmConsensusStrategy.WeightedConfidence);

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["low"] = ("Low answer", 0.9, "reason"),
                ["high"] = ("High answer", 0.9, "reason")
            };

            var result = swarm.Deliberate("test", responses);
            Assert.Equal("high", result.Winner?.MemberId);
        }

        [Fact]
        public void Deliberate_Unanimous_FailsOnDisagreement()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0)
                .AddMember("b", SwarmRole.Contributor, "", 1.0)
                .WithStrategy(SwarmConsensusStrategy.Unanimous);

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["a"] = ("Yes", 0.9, ""),
                ["b"] = ("No", 0.9, "")
            };

            var result = swarm.Deliberate("test", responses);
            Assert.False(result.ConsensusReached);
        }

        [Fact]
        public void Deliberate_Unanimous_SucceedsOnAgreement()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0)
                .AddMember("b", SwarmRole.Contributor, "", 1.0)
                .WithStrategy(SwarmConsensusStrategy.Unanimous);

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["a"] = ("Same answer", 0.9, ""),
                ["b"] = ("Same answer", 0.8, "")
            };

            var result = swarm.Deliberate("test", responses);
            Assert.True(result.ConsensusReached);
        }

        [Fact]
        public void Deliberate_Synthesis_AlwaysReachesConsensus()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0)
                .AddMember("b", SwarmRole.Innovator, "", 1.0)
                .WithStrategy(SwarmConsensusStrategy.Synthesis);

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["a"] = ("The sky is blue. Clouds are white.", 0.8, ""),
                ["b"] = ("The ocean is vast. Fish swim freely.", 0.7, "")
            };

            var result = swarm.Deliberate("test", responses);
            Assert.True(result.ConsensusReached);
            Assert.NotNull(result.SynthesizedOutput);
            Assert.Contains("sky", result.SynthesizedOutput!);
            Assert.Contains("ocean", result.SynthesizedOutput!);
        }

        [Fact]
        public void Deliberate_MeritBased_FavorsHighAccuracy()
        {
            var swarm = new PromptSwarm()
                .WithStrategy(SwarmConsensusStrategy.MeritBased);
            swarm.AddMember("novice", SwarmRole.Contributor, "", 1.0);
            swarm.AddMember("expert", SwarmRole.Contributor, "", 1.0);

            // Manually set accuracy
            swarm.Members.First(m => m.Id == "novice").Accuracy = 0.2;
            swarm.Members.First(m => m.Id == "expert").Accuracy = 0.95;

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["novice"] = ("Wrong answer", 0.9, ""),
                ["expert"] = ("Right answer", 0.9, "")
            };

            var result = swarm.Deliberate("test", responses);
            Assert.Equal("expert", result.Winner?.MemberId);
        }

        [Fact]
        public void ProvideFeedback_UpdatesAccuracy()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0)
                .AddMember("b", SwarmRole.Contributor, "", 1.0);

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["a"] = ("Correct", 0.9, ""),
                ["b"] = ("Wrong", 0.5, "")
            };

            var delib = swarm.Deliberate("test", responses);
            double initialA = swarm.Members.First(m => m.Id == "a").Accuracy;
            swarm.ProvideFeedback(delib, "a");
            double updatedA = swarm.Members.First(m => m.Id == "a").Accuracy;

            Assert.True(updatedA > initialA); // Positive feedback should increase accuracy
        }

        [Fact]
        public void SimulateDeliberation_Works()
        {
            var swarm = new PromptSwarm().AddDefaultTeam();
            var result = swarm.SimulateDeliberation("What is 2+2?", new List<string>
            {
                "4", "4", "4", "4", "5"
            });

            Assert.NotNull(result.Winner);
            Assert.Equal(5, result.Responses.Count);
        }

        [Fact]
        public void GetHealthReport_ProducesReport()
        {
            var swarm = new PromptSwarm().AddDefaultTeam();
            var report = swarm.GetHealthReport();

            Assert.Equal(5, report.TotalMembers);
            Assert.True(report.DiversityScore > 0);
            Assert.NotEmpty(report.ToText());
        }

        [Fact]
        public void GetHealthReport_LowDiversity_Warns()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0)
                .AddMember("b", SwarmRole.Contributor, "", 1.0);

            var report = swarm.GetHealthReport();
            Assert.Contains(report.Recommendations, r => r.Contains("diversity"));
        }

        [Fact]
        public void GetHealthReport_SmallSwarm_Warns()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0);

            var report = swarm.GetHealthReport();
            Assert.Contains(report.Recommendations, r => r.Contains("Fewer than 3"));
        }

        [Fact]
        public void Export_IncludesAllFields()
        {
            var swarm = new PromptSwarm()
                .AddDefaultTeam()
                .WithStrategy(SwarmConsensusStrategy.MeritBased)
                .WithThreshold(0.7);

            var exported = swarm.Export();
            Assert.Equal("MeritBased", exported["strategy"]);
            Assert.Equal(0.7, exported["threshold"]);
            Assert.Equal(5, ((IEnumerable<object>)exported["members"]).Count());
        }

        [Fact]
        public void Deliberate_EmptySwarm_Throws()
        {
            var swarm = new PromptSwarm();
            Assert.Throws<InvalidOperationException>(() =>
                swarm.Deliberate("test", new Dictionary<string, (string, double, string)>()));
        }

        [Fact]
        public void Deliberate_NoMatchingResponses_Throws()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0);

            Assert.Throws<InvalidOperationException>(() =>
                swarm.Deliberate("test", new Dictionary<string, (string, double, string)>
                {
                    ["nonexistent"] = ("answer", 0.5, "")
                }));
        }

        [Fact]
        public void WithThreshold_ClampsValues()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0)
                .WithStrategy(SwarmConsensusStrategy.MajorityVote)
                .WithThreshold(2.0); // Should clamp to 1.0

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["a"] = ("answer", 1.0, "")
            };

            var result = swarm.Deliberate("test", responses);
            // With threshold 1.0 and only one member, agreement ratio is 1.0
            Assert.True(result.ConsensusReached);
        }

        [Fact]
        public void History_TracksDeliberations()
        {
            var swarm = new PromptSwarm()
                .AddMember("a", SwarmRole.Contributor, "", 1.0);

            var responses = new Dictionary<string, (string, double, string)>
            {
                ["a"] = ("answer", 0.9, "")
            };

            swarm.Deliberate("q1", responses);
            swarm.Deliberate("q2", responses);

            Assert.Equal(2, swarm.History.Count);
            Assert.Equal("q1", swarm.History[0].Query);
            Assert.Equal("q2", swarm.History[1].Query);
        }
    }
}
