namespace Prompt.Tests
{
    using Xunit;

    public class PromptExpanderTests
    {
        [Fact]
        public void Expand_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => PromptExpander.Expand(""));
            Assert.Throws<ArgumentException>(() => PromptExpander.Expand("   "));
        }

        [Fact]
        public void Expand_ExpandsAbbreviations()
        {
            var result = PromptExpander.ExpandWithStats(
                "check the config and docs for the app", ExpansionLevel.Light);

            Assert.Contains("configuration", result.Expanded);
            Assert.Contains("documentation", result.Expanded);
            Assert.Contains("application", result.Expanded);
            Assert.True(result.Transformations.Count > 0);
        }

        [Fact]
        public void Expand_EnrichesTaskVerbs()
        {
            var result = PromptExpander.ExpandWithStats("summarize this text", ExpansionLevel.Light);
            Assert.Contains("comprehensive summary", result.Expanded);
        }

        [Fact]
        public void Expand_ExplainVerb()
        {
            var result = PromptExpander.Expand("explain recursion", ExpansionLevel.Light);
            Assert.Contains("clear and detailed explanation", result);
        }

        [Fact]
        public void Expand_ListVerb()
        {
            var result = PromptExpander.Expand("list sorting algorithms", ExpansionLevel.Light);
            Assert.Contains("comprehensive list", result);
        }

        [Fact]
        public void Expand_MediumAddsStructure()
        {
            var result = PromptExpander.ExpandWithStats(
                "list pros and cons of microservices", ExpansionLevel.Medium);
            Assert.Contains("sections", result.Expanded);
            Assert.True(result.Transformations.Any(t => t.Contains("pros/cons")));
        }

        [Fact]
        public void Expand_MediumAddsListFormat()
        {
            var result = PromptExpander.ExpandWithStats(
                "enumerate the design patterns", ExpansionLevel.Medium);
            Assert.Contains("well-organized list", result.Expanded);
        }

        [Fact]
        public void Expand_MediumAddsStepByStep()
        {
            var result = PromptExpander.ExpandWithStats(
                "how to deploy a docker container", ExpansionLevel.Medium);
            Assert.Contains("step-by-step", result.Expanded);
        }

        [Fact]
        public void Expand_DetailedAddsRoleAndConstraints()
        {
            var result = PromptExpander.ExpandWithStats(
                "fix the bug in the auth module", ExpansionLevel.Detailed);
            Assert.Contains("knowledgeable", result.Expanded);
            Assert.Contains("Constraints:", result.Expanded);
        }

        [Fact]
        public void Expand_DoesNotAddDuplicateRole()
        {
            var result = PromptExpander.Expand(
                "You are an expert. Explain SQL joins", ExpansionLevel.Detailed);
            // Should not add another "You are" prefix
            int count = result.Split("You are").Length - 1;
            Assert.Equal(1, count);
        }

        [Fact]
        public void Expand_EnsuresCapitalization()
        {
            var result = PromptExpander.Expand("summarize the report", ExpansionLevel.Light);
            Assert.True(char.IsUpper(result[0]));
        }

        [Fact]
        public void Expand_EnsuresPunctuation()
        {
            var result = PromptExpander.Expand("explain polymorphism", ExpansionLevel.Light);
            Assert.True(result.TrimEnd().EndsWith(".") || result.TrimEnd().EndsWith("?")
                || result.TrimEnd().EndsWith("!") || result.TrimEnd().EndsWith(":"));
        }

        [Fact]
        public void Expand_TokensAdded_IsPositive()
        {
            var result = PromptExpander.ExpandWithStats(
                "summarize the doc", ExpansionLevel.Medium);
            Assert.True(result.TokensAdded > 0);
            Assert.True(result.ExpansionRatio > 1.0);
        }

        [Fact]
        public void ExpandBatch_Works()
        {
            var prompts = new[] { "summarize this", "explain that", "list items" };
            var results = PromptExpander.ExpandBatch(prompts, ExpansionLevel.Light);
            Assert.Equal(3, results.Count);
            foreach (var r in results)
                Assert.NotEmpty(r.Expanded);
        }

        [Fact]
        public void ExpandBatch_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => PromptExpander.ExpandBatch(null!));
        }

        [Fact]
        public void SuggestLevel_ShortPrompt_ReturnsDetailed()
        {
            Assert.Equal(ExpansionLevel.Detailed, PromptExpander.SuggestLevel("fix bug"));
        }

        [Fact]
        public void SuggestLevel_MediumPrompt_ReturnsMedium()
        {
            Assert.Equal(ExpansionLevel.Medium,
                PromptExpander.SuggestLevel(
                    "explain the differences between REST and GraphQL APIs"));
        }

        [Fact]
        public void SuggestLevel_LongPrompt_ReturnsLight()
        {
            var longPrompt = string.Join(" ",
                Enumerable.Range(0, 30).Select(i => $"word{i}"));
            Assert.Equal(ExpansionLevel.Light, PromptExpander.SuggestLevel(longPrompt));
        }

        [Fact]
        public void SuggestLevel_Empty_ReturnsDetailed()
        {
            Assert.Equal(ExpansionLevel.Detailed, PromptExpander.SuggestLevel(""));
        }

        [Fact]
        public void Expand_MultipleAbbreviationsInOneLine()
        {
            var result = PromptExpander.Expand(
                "check the db config and auth params", ExpansionLevel.Light);
            Assert.Contains("database", result);
            Assert.Contains("configuration", result);
            Assert.Contains("authentication", result);
            Assert.Contains("parameters", result);
        }

        [Fact]
        public void Expand_CompareVerb()
        {
            var result = PromptExpander.Expand("compare Python and Java", ExpansionLevel.Light);
            Assert.Contains("detailed comparison", result);
        }

        [Fact]
        public void Expand_WriteVerb()
        {
            var result = PromptExpander.Expand("write a REST API", ExpansionLevel.Light);
            Assert.Contains("well-structured", result);
        }

        [Fact]
        public void Expand_GenerateVerb()
        {
            var result = PromptExpander.Expand("generate a report", ExpansionLevel.Light);
            Assert.Contains("well-formed", result);
        }

        [Fact]
        public void Expand_PreservesExistingContent()
        {
            var result = PromptExpander.Expand(
                "explain quantum computing to a beginner", ExpansionLevel.Light);
            Assert.Contains("quantum computing", result);
            Assert.Contains("beginner", result);
        }
    }
}
