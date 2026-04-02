namespace Prompt.Tests
{
    using Xunit;
    using Prompt;

    public class PromptTournamentTests
    {
        [Fact]
        public void RoundRobin_RanksContenders()
        {
            var result = new PromptTournament()
                .AddContender("Summarize this article in 3 bullet points.", "Bullets")
                .AddContender("idk tell me stuff about the thing", "Vague")
                .AddContender("Extract the 5 key insights from this text as a JSON array.", "Specific")
                .WithCriterion(TournamentCriterion.Clarity, 2.0)
                .WithCriterion(TournamentCriterion.Conciseness, 1.0)
                .WithCriterion(TournamentCriterion.Specificity, 1.5)
                .RunRoundRobin();

            Assert.Equal(3, result.Rankings.Count);
            Assert.NotNull(result.Champion);
            Assert.True(result.Matches.Count > 0);
            // The specific prompt should beat the vague one
            Assert.True(result.Champion!.DisplayName != "Vague");
        }

        [Fact]
        public void Elimination_ProducesChampion()
        {
            var result = new PromptTournament()
                .AddContender("Write a poem about cats")
                .AddContender("Generate exactly 4 lines of rhyming poetry about cats in AABB format")
                .AddContender("cats poem pls")
                .AddContender("Compose a haiku about cats. Format: 5-7-5 syllables.")
                .RunElimination();

            Assert.NotNull(result.Champion);
            Assert.True(result.Rankings.Count == 4);
        }

        [Fact]
        public void RenderBracket_ProducesOutput()
        {
            var result = new PromptTournament()
                .AddContender("Explain quantum computing", "Simple")
                .AddContender("Explain quantum computing in 3 paragraphs for a 10-year-old", "Detailed")
                .RunRoundRobin();

            var bracket = result.RenderBracket();
            Assert.Contains("PROMPT TOURNAMENT", bracket);
            Assert.Contains("Champion", bracket);
        }

        [Fact]
        public void CustomScorer_IsUsed()
        {
            var result = new PromptTournament()
                .AddContender("short", "Short")
                .AddContender("this is a much longer prompt with many words in it", "Long")
                .WithCriterion(TournamentCriterion.Custom, 1.0, "Length")
                .RunRoundRobin((prompt, _) => prompt.Length); // longer = higher score

            Assert.Equal("Long", result.Champion!.DisplayName);
        }

        [Fact]
        public void ToJson_SerializesResult()
        {
            var result = new PromptTournament()
                .AddContender("A")
                .AddContender("B")
                .RunRoundRobin();

            var json = result.ToJson();
            Assert.Contains("Rankings", json);
            Assert.Contains("Matches", json);
        }

        [Fact]
        public void TooFewContenders_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new PromptTournament()
                    .AddContender("Only one")
                    .RunRoundRobin());
        }

        [Fact]
        public void DefaultCriteria_AppliedWhenNoneSet()
        {
            var result = new PromptTournament()
                .AddContender("Summarize the document")
                .AddContender("Give me a summary")
                .RunRoundRobin();

            Assert.True(result.Criteria.Count >= 3);
        }
    }
}
