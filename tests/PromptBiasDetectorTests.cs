namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Xunit;

    public class PromptBiasDetectorTests
    {
        [Fact]
        public void Constructor_PopulatesDefaultRules()
        {
            var detector = new PromptBiasDetector();
            Assert.True(detector.RuleCount > 0);
        }

        [Fact]
        public void Analyze_NullOrWhitespace_ReturnsCleanReport()
        {
            var detector = new PromptBiasDetector();

            var nullReport = detector.Analyze(null!);
            Assert.True(nullReport.IsClean);
            Assert.Equal(string.Empty, nullReport.OriginalText);
            Assert.Equal(string.Empty, nullReport.DebiasedText);
            Assert.Equal(0, nullReport.BiasScore);

            var blank = detector.Analyze("   ");
            Assert.True(blank.IsClean);
            Assert.Equal("   ", blank.OriginalText);
        }

        [Fact]
        public void Analyze_NeutralText_ProducesNoFindings()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Summarize the document into three concise bullet points.");

            Assert.True(report.IsClean);
            Assert.Empty(report.Findings);
            Assert.Equal(0, report.BiasScore);
            Assert.Equal(report.OriginalText, report.DebiasedText);
            Assert.Contains("No bias detected", report.Render());
        }

        [Fact]
        public void Analyze_GenderedTerm_FlagsAndDebiases()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Ask the businessman to explain his strategy");

            Assert.False(report.IsClean);
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Gender);
            Assert.Contains("business professional", report.DebiasedText);
            Assert.DoesNotContain("businessman", report.DebiasedText, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("chairman", "chairperson")]
        [InlineData("fireman", "firefighter")]
        [InlineData("policeman", "police officer")]
        [InlineData("mankind", "humankind")]
        [InlineData("manpower", "workforce")]
        public void Analyze_GenderedTerms_AreReplacedWithNeutralEquivalents(string biased, string suggested)
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze($"The {biased} arrived on time.");

            Assert.False(report.IsClean);
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Gender);
            Assert.Contains(suggested, report.DebiasedText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_ConfirmationBiasWords_FlaggedHighSeverity()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Obviously the new policy is better.");

            Assert.Contains(report.Findings, f =>
                f.Category == BiasCategory.Confirmation && f.Severity == BiasSeverity.High);
        }

        [Fact]
        public void Analyze_LeadingQuestion_Flagged()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Don't you think this approach is the right one?");
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Confirmation);
        }

        [Fact]
        public void Analyze_AnchoringPhrase_Flagged()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Most experts agree the answer is 42.");
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Anchoring);
        }

        [Fact]
        public void Analyze_AuthorityAppeal_Flagged()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Studies show that this works.");
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Authority);
        }

        [Fact]
        public void Analyze_FramingPhrases_Flagged()
        {
            var detector = new PromptBiasDetector();
            var minimizing = detector.Analyze("Only 5% of users complained.");
            Assert.Contains(minimizing.Findings, f => f.Category == BiasCategory.Framing);

            var amplifying = detector.Analyze("A whopping 80 percent of users loved it.");
            Assert.Contains(amplifying.Findings, f => f.Category == BiasCategory.Framing);
        }

        [Fact]
        public void Analyze_ExclusionPhrase_FlaggedAndReplaced()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Normal people prefer dark mode.");

            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Exclusion);
            Assert.Contains("most people", report.DebiasedText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_AgeismTerm_Flagged()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Design this UI for the elderly.");

            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Age);
            Assert.Contains("older adults", report.DebiasedText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_AbleistLanguage_Flagged()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("That idea is crazy.");
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Ability);
        }

        [Fact]
        public void Analyze_SocioeconomicTerm_Flagged()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Focus the campaign on the poor.");
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Socioeconomic);
        }

        [Fact]
        public void Analyze_MultipleBiases_AggregatesFindingsAndScore()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Obviously the businessman is correct, as experts say.");

            Assert.True(report.Findings.Count >= 3);
            Assert.True(report.BiasScore > 0);
            Assert.True(report.BiasScore <= 1.0);
            // Category breakdown should include at least these categories
            Assert.True(report.CategoryBreakdown.ContainsKey(BiasCategory.Confirmation));
            Assert.True(report.CategoryBreakdown.ContainsKey(BiasCategory.Gender));
            Assert.True(report.CategoryBreakdown.ContainsKey(BiasCategory.Authority));
        }

        [Fact]
        public void Analyze_BiasScore_IsClampedToOne()
        {
            var detector = new PromptBiasDetector();
            // Pack lots of high-severity confirmation triggers
            var prompt = string.Join(" ", Enumerable.Repeat("obviously", 50));
            var report = detector.Analyze(prompt);

            Assert.Equal(1.0, report.BiasScore);
        }

        [Fact]
        public void Analyze_FindingPosition_PointsAtMatch()
        {
            var detector = new PromptBiasDetector();
            const string text = "The businessman is here.";
            var report = detector.Analyze(text);

            var finding = Assert.Single(report.Findings.Where(f => f.MatchedText.Equals("businessman", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(text.IndexOf("businessman", StringComparison.OrdinalIgnoreCase), finding.Position);
        }

        [Fact]
        public void AnalyzeBatch_HandlesAllInputs()
        {
            var detector = new PromptBiasDetector();
            var inputs = new[]
            {
                "He should fix this fireman issue.",
                "Summarize the article.",
                "Obviously the answer is yes."
            };

            var reports = detector.AnalyzeBatch(inputs);
            Assert.Equal(3, reports.Count);
            Assert.False(reports[0].IsClean);
            Assert.True(reports[1].IsClean);
            Assert.False(reports[2].IsClean);
        }

        [Fact]
        public void AnalyzeBatch_NullInput_Throws()
        {
            var detector = new PromptBiasDetector();
            Assert.Throws<ArgumentNullException>(() => detector.AnalyzeBatch(null!));
        }

        [Fact]
        public void AddRule_NullRule_Throws()
        {
            var detector = new PromptBiasDetector();
            Assert.Throws<ArgumentNullException>(() => detector.AddRule(null!));
        }

        [Fact]
        public void AddRule_CustomRule_IsApplied()
        {
            var detector = new PromptBiasDetector();
            var before = detector.RuleCount;

            detector.AddRule(new BiasRule
            {
                Category = BiasCategory.Cultural,
                Severity = BiasSeverity.High,
                Pattern = new Regex(@"\bcustom-banned-word\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500)),
                Description = "Custom test rule",
                Suggestion = "neutral-word"
            });

            Assert.Equal(before + 1, detector.RuleCount);

            var report = detector.Analyze("This is a custom-banned-word example.");
            Assert.Contains(report.Findings, f => f.Category == BiasCategory.Cultural && f.Severity == BiasSeverity.High);
            Assert.Contains("neutral-word", report.DebiasedText);
        }

        [Fact]
        public void Render_WithFindings_IncludesDebiasedSection()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("Obviously the chairman is right.");
            var rendered = report.Render();

            Assert.Contains("Bias Report", rendered);
            Assert.Contains("Debiased version", rendered);
            Assert.Contains(report.DebiasedText, rendered);
        }

        [Fact]
        public void BiasReport_CategoryBreakdown_CountsByCategory()
        {
            var detector = new PromptBiasDetector();
            var report = detector.Analyze("The fireman and the policeman arrived.");

            Assert.True(report.CategoryBreakdown.TryGetValue(BiasCategory.Gender, out var count));
            Assert.True(count >= 2);
        }

        [Fact]
        public void BiasReport_IsClean_ReflectsFindingsCount()
        {
            var clean = new BiasReport();
            Assert.True(clean.IsClean);

            clean.Findings.Add(new BiasFinding { Category = BiasCategory.Gender });
            Assert.False(clean.IsClean);
        }
    }
}
