namespace Prompt.Tests
{
    using Xunit;
    using System.Collections.Generic;
    using System.Linq;

    public class PromptAudienceAdapterTests
    {
        // ──────────── Constructor ────────────

        [Fact]
        public void Constructor_DefaultOptions_NoThrow()
        {
            var adapter = new PromptAudienceAdapter();
            Assert.NotNull(adapter);
        }

        [Fact]
        public void Constructor_CustomOptions_Accepted()
        {
            var opts = new AudienceAdapterOptions { SimplifyJargon = false };
            var adapter = new PromptAudienceAdapter(opts);
            Assert.NotNull(adapter);
        }

        // ──────────── Adapt - Basic ────────────

        [Fact]
        public void Adapt_EmptyPrompt_Throws()
        {
            var adapter = new PromptAudienceAdapter();
            Assert.Throws<System.ArgumentException>(() => adapter.Adapt("", AudienceLevel.Beginner));
        }

        [Fact]
        public void Adapt_NullPrompt_Throws()
        {
            var adapter = new PromptAudienceAdapter();
            Assert.Throws<System.ArgumentException>(() => adapter.Adapt(null!, AudienceLevel.Expert));
        }

        [Fact]
        public void Adapt_Beginner_ReplacesJargon()
        {
            var adapter = new PromptAudienceAdapter();
            var result = adapter.Adapt("Build a REST API with CRUD operations.", AudienceLevel.Beginner);

            Assert.True(result.JargonReplacements > 0);
            Assert.Contains("API", result.DetectedJargon);
            Assert.True(result.PreambleInjected);
            Assert.True(result.FormatHintAppended);
            Assert.Equal(AudienceLevel.Beginner, result.Level);
        }

        [Fact]
        public void Adapt_Expert_NoJargonReplacement()
        {
            var adapter = new PromptAudienceAdapter();
            var result = adapter.Adapt("Build a REST API with CRUD operations.", AudienceLevel.Expert);

            Assert.Equal(0, result.JargonReplacements);
            Assert.True(result.PreambleInjected);
        }

        [Fact]
        public void Adapt_Child_SimplestLanguage()
        {
            var adapter = new PromptAudienceAdapter();
            var result = adapter.Adapt("Explain how an API works with JSON.", AudienceLevel.Child);

            Assert.True(result.JargonReplacements > 0);
            // Child mode should NOT include parenthetical original term
            Assert.DoesNotContain("(API)", result.Adapted);
            Assert.Contains("[Audience: Young learner", result.Adapted);
        }

        [Fact]
        public void Adapt_Beginner_IncludesParentheticalTerms()
        {
            var adapter = new PromptAudienceAdapter();
            var result = adapter.Adapt("Use the CLI to deploy.", AudienceLevel.Beginner);

            // Beginner keeps original term in parentheses
            Assert.Contains("(CLI)", result.Adapted);
        }

        [Fact]
        public void Adapt_Executive_HasOutcomeFocus()
        {
            var adapter = new PromptAudienceAdapter();
            var result = adapter.Adapt("Evaluate the scalability of the system.", AudienceLevel.Executive);

            Assert.Contains("bottom line", result.Adapted);
            Assert.True(result.PreambleInjected);
        }

        // ──────────── Adapt - Options ────────────

        [Fact]
        public void Adapt_NoPreamble_WhenDisabled()
        {
            var opts = new AudienceAdapterOptions { InjectAudiencePreamble = false };
            var adapter = new PromptAudienceAdapter(opts);
            var result = adapter.Adapt("Test prompt.", AudienceLevel.Beginner);

            Assert.False(result.PreambleInjected);
            Assert.DoesNotContain("[Audience:", result.Adapted);
        }

        [Fact]
        public void Adapt_NoFormatHint_WhenDisabled()
        {
            var opts = new AudienceAdapterOptions { AppendFormatHint = false };
            var adapter = new PromptAudienceAdapter(opts);
            var result = adapter.Adapt("Test prompt.", AudienceLevel.Expert);

            Assert.False(result.FormatHintAppended);
        }

        [Fact]
        public void Adapt_CustomJargon_Merged()
        {
            var opts = new AudienceAdapterOptions
            {
                CustomJargonMap = new Dictionary<string, string>
                {
                    ["FOOBAR"] = "custom thing"
                }
            };
            var adapter = new PromptAudienceAdapter(opts);
            var result = adapter.Adapt("Use the FOOBAR system.", AudienceLevel.Beginner);

            Assert.Contains("FOOBAR", result.DetectedJargon);
            Assert.Contains("custom thing", result.Adapted);
        }

        // ──────────── AdaptAll ────────────

        [Fact]
        public void AdaptAll_ReturnsAllLevels()
        {
            var adapter = new PromptAudienceAdapter();
            var results = adapter.AdaptAll("Implement a REST API.");

            Assert.Equal(5, results.Count);
            Assert.Contains(AudienceLevel.Child, results.Keys);
            Assert.Contains(AudienceLevel.Expert, results.Keys);
        }

        [Fact]
        public void AdaptAll_SubsetLevels_ReturnsOnlyRequested()
        {
            var adapter = new PromptAudienceAdapter();
            var results = adapter.AdaptAll("Test.", new[] { AudienceLevel.Beginner, AudienceLevel.Expert });

            Assert.Equal(2, results.Count);
        }

        // ──────────── Analyze ────────────

        [Fact]
        public void Analyze_TechnicalPrompt_HighJargonDensity()
        {
            var adapter = new PromptAudienceAdapter();
            var analysis = adapter.Analyze(
                "Build a REST API endpoint with CRUD operations using an ORM. Add OAuth and JWT for auth. Use Docker and Kubernetes for deployment.");

            Assert.True(analysis.JargonDensity > 10);
            Assert.True(analysis.JargonTerms.Count > 3);
            Assert.True(analysis.WordCount > 10);
            Assert.True(analysis.SentenceCount >= 1);
        }

        [Fact]
        public void Analyze_SimplePrompt_LowJargonDensity()
        {
            var adapter = new PromptAudienceAdapter();
            var analysis = adapter.Analyze("Write a story about a dog who goes on an adventure.");

            Assert.True(analysis.JargonDensity < 5);
            // Simple prompt with no jargon should be suitable for non-expert levels
            Assert.NotEqual(AudienceLevel.Expert, analysis.RecommendedLevel);
        }

        [Fact]
        public void Analyze_EmptyPrompt_Throws()
        {
            var adapter = new PromptAudienceAdapter();
            Assert.Throws<System.ArgumentException>(() => adapter.Analyze(""));
        }

        [Fact]
        public void Analyze_SuitabilityScores_AllLevelsPresent()
        {
            var adapter = new PromptAudienceAdapter();
            var analysis = adapter.Analyze("Test the API endpoint.");

            Assert.Equal(5, analysis.Suitability.Count);
            foreach (var level in System.Enum.GetValues<AudienceLevel>())
                Assert.Contains(level, analysis.Suitability.Keys);
        }

        // ──────────── Static Helpers ────────────

        [Fact]
        public void GetJargonDictionary_NotEmpty()
        {
            var dict = PromptAudienceAdapter.GetJargonDictionary();
            Assert.True(dict.Count > 50);
        }

        [Fact]
        public void GetPreamble_ValidLevel_ReturnsText()
        {
            var preamble = PromptAudienceAdapter.GetPreamble(AudienceLevel.Expert);
            Assert.Contains("Expert", preamble);
        }

        [Fact]
        public void GetFormatHint_ValidLevel_ReturnsText()
        {
            var hint = PromptAudienceAdapter.GetFormatHint(AudienceLevel.Child);
            Assert.Contains("emoji", hint.ToLower());
        }

        // ──────────── Sentence Simplification ────────────

        [Fact]
        public void Adapt_LongSentence_SplitForChild()
        {
            var adapter = new PromptAudienceAdapter();
            var longPrompt = "The system processes data from multiple sources and transforms it into a unified format, which is then stored in the database for later retrieval and analysis.";
            var result = adapter.Adapt(longPrompt, AudienceLevel.Child);

            Assert.True(result.SentencesSimplified > 0);
        }
    }
}
