namespace Prompt.Tests
{
    using Xunit;
    using Prompt;
    using System;

    public class PromptReadabilityAnalyzerTests
    {
        private readonly PromptReadabilityAnalyzer _analyzer = new();

        [Fact]
        public void Analyze_SimpleText_ReturnsEasyGrade()
        {
            var report = _analyzer.Analyze("You are a helpful assistant. Answer questions clearly.");
            Assert.True(report.FleschKincaidGradeLevel <= 12);
            Assert.True(report.FleschReadingEase > 30);
            Assert.Equal(2, report.TotalSentences);
            Assert.NotEmpty(report.Summary);
        }

        [Fact]
        public void Analyze_ComplexText_ReturnsDifficultGrade()
        {
            var text = "Notwithstanding the aforementioned considerations, you must simultaneously " +
                       "synthesize multidimensional perspectives while contextualizing the epistemological " +
                       "frameworks that undergird contemporary methodological approaches.";
            var report = _analyzer.Analyze(text);
            Assert.True(report.FleschKincaidGradeLevel > 10);
            Assert.True(report.Suggestions.Count > 0);
        }

        [Fact]
        public void Analyze_EmptyText_Throws()
        {
            Assert.Throws<ArgumentException>(() => _analyzer.Analyze(""));
            Assert.Throws<ArgumentException>(() => _analyzer.Analyze("   "));
        }

        [Fact]
        public void Analyze_VocabularyDiversity_Calculated()
        {
            var report = _analyzer.Analyze("The cat sat on the mat. The cat ate the food. The cat slept.");
            Assert.True(report.VocabularyDiversity > 0);
            Assert.True(report.VocabularyDiversity <= 1.0);
        }

        [Fact]
        public void Analyze_FlaggedSentences_Detected()
        {
            var longSentence = "You need to carefully consider all the possible options and alternatives " +
                               "that might be available to you when you are trying to make a decision about " +
                               "which approach to take in this particular situation and context and scenario.";
            var report = _analyzer.Analyze(longSentence);
            Assert.Contains(report.Sentences, s => s.IsFlagged);
        }

        [Fact]
        public void Compare_TwoPrompts_IdentifiesEasier()
        {
            var easy = "Be helpful. Answer questions. Keep it short.";
            var hard = "Notwithstanding the aforementioned considerations, synthesize multidimensional perspectives.";
            var comparison = _analyzer.Compare(easy, hard);
            Assert.Equal("A", comparison.EasierPrompt);
            Assert.NotEmpty(comparison.Summary);
        }

        [Fact]
        public void CountSyllables_CommonWords_Correct()
        {
            Assert.Equal(1, PromptReadabilityAnalyzer.CountSyllables("cat"));
            Assert.Equal(2, PromptReadabilityAnalyzer.CountSyllables("hello"));
            Assert.True(PromptReadabilityAnalyzer.CountSyllables("communication") >= 4);
        }

        [Fact]
        public void Analyze_MultilinePrompt_HandlesNewlines()
        {
            var text = "You are a helpful assistant.\nAnswer clearly.\nBe concise.";
            var report = _analyzer.Analyze(text);
            Assert.Equal(3, report.TotalSentences);
        }

        [Fact]
        public void Analyze_Metrics_AllPopulated()
        {
            var report = _analyzer.Analyze("Explain the concept of machine learning to a beginner. Use simple words.");
            Assert.True(report.Metrics.Count >= 5);
            Assert.All(report.Metrics, m => Assert.False(string.IsNullOrEmpty(m.Name)));
        }
    }
}
