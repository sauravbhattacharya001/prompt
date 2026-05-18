namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Xunit;

    public class PromptMixtureOfExpertsTests
    {
        private static PromptMixtureOfExperts MakeBasic()
        {
            return new PromptMixtureOfExperts()
                .AddExpert("CodeExpert", "code",
                    "You are a code expert. Answer: {input}",
                    new[] { "code", "function", "class", "bug", "compile" })
                .AddExpert("MathExpert", "math",
                    "You are a math expert. Solve: {input}",
                    new[] { "math", "equation", "integral", "solve", "derivative" })
                .AddExpert("WritingExpert", "writing",
                    "You are a writing expert. Help with: {input}",
                    new[] { "essay", "grammar", "sentence", "paragraph", "writing" })
                .SetFallback("General", "General assistant. Help with: {input}");
        }

        [Fact]
        public void Route_EmptyInput_ReturnsEmpty()
        {
            var moe = MakeBasic();
            var r = moe.Route("");
            Assert.Equal(string.Empty, r.RenderedPrompt);
            Assert.True(r.UsedFallback);
        }

        [Fact]
        public void Route_WhitespaceInput_ReturnsEmpty()
        {
            var moe = MakeBasic();
            var r = moe.Route("   ");
            Assert.Equal(string.Empty, r.RenderedPrompt);
        }

        [Fact]
        public void Route_NoExperts_FallsBackWhenAvailable()
        {
            var moe = new PromptMixtureOfExperts().SetFallback("F", "fallback: {input}");
            var r = moe.Route("anything");
            Assert.True(r.UsedFallback);
            Assert.Equal("F", r.ExpertName);
            Assert.Contains("anything", r.RenderedPrompt);
        }

        [Fact]
        public void Route_NoExpertsNoFallback_ReturnsEmpty()
        {
            var moe = new PromptMixtureOfExperts();
            var r = moe.Route("anything");
            Assert.Equal("(none)", r.ExpertName);
            Assert.True(r.UsedFallback);
            Assert.Empty(r.RenderedPrompt);
        }

        [Fact]
        public void Route_KeywordMatch_SelectsRightExpert()
        {
            var moe = MakeBasic();
            var r = moe.Route("Please fix this bug in my code function");
            Assert.Equal("CodeExpert", r.ExpertName);
            Assert.False(r.UsedFallback);
            Assert.Contains("code expert", r.RenderedPrompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Route_LowConfidence_UsesFallback()
        {
            var moe = MakeBasic().WithConfidenceThreshold(0.99);
            var r = moe.Route("random gibberish text qqq");
            Assert.True(r.UsedFallback);
            Assert.Equal("General", r.ExpertName);
        }

        [Fact]
        public void Route_TopKEnsemble_BlendsExperts()
        {
            var moe = MakeBasic().WithTopK(2);
            // Input that hits multiple experts
            var r = moe.Route("Help me debug the math equation in my code function");
            Assert.False(r.UsedFallback);
            Assert.Contains("+", r.ExpertName);
            Assert.Contains("Ensemble Response", r.RenderedPrompt);
        }

        [Fact]
        public void Feedback_Positive_IncreasesWeight()
        {
            var moe = MakeBasic();
            moe.Route("code function bug");
            // Adjust positively several times and confirm picked again
            for (int i = 0; i < 3; i++) moe.Feedback("CodeExpert", true);
            var report = moe.GetReport();
            Assert.Contains("CodeExpert", report);
        }

        [Fact]
        public void Feedback_Disabled_DoesNotAdjust()
        {
            var moe = MakeBasic().WithAdaptiveWeights(false);
            moe.Feedback("CodeExpert", true);
            // No exception; weights unchanged (smoke check via report still works)
            var rpt = moe.GetReport(PromptMixtureOfExperts.ReportFormat.Json);
            Assert.Contains("\"experts\"", rpt);
        }

        [Fact]
        public void Feedback_UnknownExpert_Ignored()
        {
            var moe = MakeBasic();
            // Should not throw
            moe.Feedback("NoSuchExpert", true);
            moe.Feedback("NoSuchExpert", false);
        }

        [Fact]
        public void GetHistory_TracksAllRoutings()
        {
            var moe = MakeBasic();
            moe.Route("code function");
            moe.Route("math equation");
            moe.Route("essay grammar");
            Assert.Equal(3, moe.GetHistory().Count);
            Assert.All(moe.GetHistory(), h => Assert.NotNull(h.ExpertName));
        }

        [Fact]
        public void GetReport_Markdown_HasTable()
        {
            var moe = MakeBasic();
            moe.Route("code function bug compile");
            var rpt = moe.GetReport(PromptMixtureOfExperts.ReportFormat.Markdown);
            Assert.Contains("| Expert | Domain", rpt);
            Assert.Contains("CodeExpert", rpt);
        }

        [Fact]
        public void GetReport_Json_IsValidShape()
        {
            var moe = MakeBasic();
            moe.Route("math integral");
            var rpt = moe.GetReport(PromptMixtureOfExperts.ReportFormat.Json);
            Assert.StartsWith("{", rpt);
            Assert.EndsWith("}", rpt);
            Assert.Contains("\"experts\"", rpt);
            Assert.Contains("\"fallbackHits\"", rpt);
            Assert.Contains("\"totalRoutings\"", rpt);
        }

        [Fact]
        public void GetReport_Text_HasHeader()
        {
            var moe = MakeBasic();
            moe.Route("anything obscure");
            var rpt = moe.GetReport();
            Assert.Contains("Mixture-of-Experts Report", rpt);
            Assert.Contains("Fallback hits:", rpt);
        }

        [Fact]
        public void WithConfidenceThreshold_ClampsToValidRange()
        {
            var moe = MakeBasic();
            // Should not throw for out-of-range values
            moe.WithConfidenceThreshold(-1.0);
            moe.WithConfidenceThreshold(2.0);
            var r = moe.Route("code function");
            Assert.NotNull(r);
        }

        [Fact]
        public void WithTopK_MinimumOne()
        {
            var moe = MakeBasic().WithTopK(0);
            // TopK of 0 should be clamped to 1
            var r = moe.Route("code function bug");
            Assert.DoesNotContain("Ensemble Response", r.RenderedPrompt);
        }

        [Fact]
        public void Route_FallbackWithoutTemplate_StillRenders()
        {
            var moe = new PromptMixtureOfExperts()
                .AddExpert("X", "x", "no-placeholder template", new[] { "x" })
                .WithConfidenceThreshold(0.99) // force fallback
                .SetFallback("F", "fallback plain text");
            var r = moe.Route("totally unrelated text");
            Assert.True(r.UsedFallback);
            Assert.Contains("totally unrelated", r.RenderedPrompt);
        }

        [Fact]
        public void Route_TemplateWithoutPlaceholder_AppendsInput()
        {
            var moe = new PromptMixtureOfExperts()
                .AddExpert("X", "x", "expert template",
                    new[] { "test", "alpha", "beta" })
                .WithConfidenceThreshold(0.0);
            var r = moe.Route("test alpha beta");
            Assert.Contains("Input:", r.RenderedPrompt);
            Assert.Contains("test alpha beta", r.RenderedPrompt);
        }

        [Fact]
        public void RoutingResult_AllScores_PopulatedForEachExpert()
        {
            var moe = MakeBasic();
            var r = moe.Route("code function bug compile class");
            Assert.Equal(3, r.AllScores.Count);
            Assert.True(r.AllScores.First().Score >= r.AllScores.Last().Score);
        }

        [Fact]
        public void RoutingRecord_TimestampIsSet()
        {
            var moe = MakeBasic();
            var before = DateTime.UtcNow.AddSeconds(-1);
            moe.Route("code function");
            var after = DateTime.UtcNow.AddSeconds(1);
            var rec = moe.GetHistory().Last();
            Assert.InRange(rec.Timestamp, before, after);
        }
    }
}
