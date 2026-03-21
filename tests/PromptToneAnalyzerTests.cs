namespace Prompt.Tests
{
    using Xunit;
    using System;
    using System.Linq;

    public class PromptToneAnalyzerTests
    {
        [Fact]
        public void Analyze_FormalPrompt_DetectsFormalTone()
        {
            var result = PromptToneAnalyzer.Analyze(
                "Furthermore, please utilize the aforementioned parameters to facilitate the endeavor.");
            Assert.Equal(ToneCategory.Formal, result.DominantTone);
            Assert.True(result.DominantConfidence > 0.3);
        }

        [Fact]
        public void Analyze_CasualPrompt_DetectsCasualTone()
        {
            var result = PromptToneAnalyzer.Analyze(
                "Hey, gonna need you to do some cool stuff for me, ok?");
            Assert.Equal(ToneCategory.Casual, result.DominantTone);
        }

        [Fact]
        public void Analyze_AssertivePrompt_DetectsAssertiveTone()
        {
            var result = PromptToneAnalyzer.Analyze(
                "You MUST always ensure the output is exactly correct. Never deviate. This is CRITICAL.");
            Assert.Equal(ToneCategory.Assertive, result.DominantTone);
        }

        [Fact]
        public void Analyze_PolitePrompt_DetectsPolite()
        {
            var result = PromptToneAnalyzer.Analyze(
                "Could you please kindly consider perhaps providing a response? Thank you, I would appreciate it.");
            Assert.Equal(ToneCategory.Polite, result.DominantTone);
        }

        [Fact]
        public void Analyze_TechnicalPrompt_DetectsTechnical()
        {
            var result = PromptToneAnalyzer.Analyze(
                "Parse the JSON payload from the API endpoint and deserialize it using the schema.");
            Assert.Equal(ToneCategory.Technical, result.DominantTone);
        }

        [Fact]
        public void Analyze_CreativePrompt_DetectsCreative()
        {
            var result = PromptToneAnalyzer.Analyze(
                "Imagine a whimsical fantasy adventure where a character crafts a magical poem!");
            Assert.Equal(ToneCategory.Creative, result.DominantTone);
        }

        [Fact]
        public void Analyze_NeutralPrompt_DefaultsToNeutral()
        {
            var result = PromptToneAnalyzer.Analyze("The cat sat on the mat.");
            Assert.Equal(ToneCategory.Neutral, result.DominantTone);
            Assert.Equal(1.0, result.DominantConfidence);
        }

        [Fact]
        public void Analyze_EmptyPrompt_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PromptToneAnalyzer.Analyze(""));
            Assert.Throws<ArgumentException>(() => PromptToneAnalyzer.Analyze("   "));
            Assert.Throws<ArgumentException>(() => PromptToneAnalyzer.Analyze(null!));
        }

        [Fact]
        public void GetDominantTone_ReturnsCorrectTone()
        {
            var tone = PromptToneAnalyzer.GetDominantTone(
                "Hey gonna do some cool stuff, awesome!");
            Assert.Equal(ToneCategory.Casual, tone);
        }

        [Fact]
        public void AnalyzeWithTarget_GeneratesSuggestions()
        {
            var result = PromptToneAnalyzer.AnalyzeWithTarget(
                "Hey, gonna need you to do some cool stuff for me.",
                ToneCategory.Formal);
            Assert.NotEmpty(result.Suggestions);
            Assert.Contains(result.Suggestions, s => s.Original == "gonna");
        }

        [Fact]
        public void AnalyzeWithTarget_FormalToCasual_GeneratesSuggestions()
        {
            var result = PromptToneAnalyzer.AnalyzeWithTarget(
                "Furthermore, utilize the aforementioned parameters to facilitate this endeavor.",
                ToneCategory.Casual);
            Assert.NotEmpty(result.Suggestions);
        }

        [Fact]
        public void AnalyzeWithTarget_SameTone_NoSuggestions()
        {
            var result = PromptToneAnalyzer.AnalyzeWithTarget(
                "Hey gonna do some cool awesome stuff, ok?",
                ToneCategory.Casual);
            Assert.Empty(result.Suggestions);
        }

        [Fact]
        public void Analyze_MixedTone_DetectsInconsistency()
        {
            // Mix formal and casual markers
            var result = PromptToneAnalyzer.Analyze(
                "Furthermore, this is gonna be awesome. Nevertheless, cool stuff ahead, yo.");
            // Should detect some inconsistency or mixed tones
            Assert.True(result.Scores.Count >= 2);
        }

        [Fact]
        public void Analyze_ScoresOrderedByConfidence()
        {
            var result = PromptToneAnalyzer.Analyze(
                "Please kindly use the API endpoint to parse JSON from the schema.");
            for (int i = 1; i < result.Scores.Count; i++)
            {
                Assert.True(result.Scores[i - 1].Confidence >= result.Scores[i].Confidence);
            }
        }

        [Fact]
        public void Analyze_SummaryNotEmpty()
        {
            var result = PromptToneAnalyzer.Analyze("Hello world, please help me.");
            Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        }

        [Fact]
        public void Analyze_EvidenceCapped()
        {
            // Lots of repeated markers
            var prompt = string.Join(" ", Enumerable.Repeat("please kindly would could might perhaps thank appreciate sorry pardon", 5));
            var result = PromptToneAnalyzer.Analyze(prompt);
            var politeScore = result.Scores.First(s => s.Tone == ToneCategory.Polite);
            Assert.True(politeScore.Evidence.Count <= 5, "Evidence should be capped at 5 items");
        }

        [Fact]
        public void AnalyzeWithTarget_AssertiveToPolite()
        {
            var result = PromptToneAnalyzer.AnalyzeWithTarget(
                "You must always ensure this is done immediately. Never fail.",
                ToneCategory.Polite);
            Assert.NotEmpty(result.Suggestions);
            Assert.Contains(result.Suggestions, s => s.Original == "must" || s.Original == "always" || s.Original == "never");
        }
    }
}
