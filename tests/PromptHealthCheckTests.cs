namespace Prompt.Tests
{
    using System.Linq;
    using Xunit;

    public class PromptHealthCheckTests
    {
        private static PromptLibrary CreateLibrary(params (string name, string template, string? desc, string? category)[] items)
        {
            var lib = new PromptLibrary();
            foreach (var (name, template, desc, category) in items)
            {
                lib.Add(name, new PromptTemplate(template), description: desc, category: category);
            }
            return lib;
        }

        [Fact]
        public void EmptyLibrary_ReturnsInfoFinding()
        {
            var checker = new PromptHealthCheck();
            var report = checker.Check(new PromptLibrary());
            Assert.Single(report.Findings);
            Assert.Equal("EMPTY_LIBRARY", report.Findings[0].RuleCode);
            Assert.True(report.IsHealthy);
        }

        [Fact]
        public void HealthyEntry_NoWarningsOrErrors()
        {
            var lib = CreateLibrary(
                ("summarize", "Summarize the following {{text}} in {{style}} style.", "Summarizes text", "writing"));
            var report = new PromptHealthCheck().Check(lib);
            Assert.Equal(0, report.ErrorCount);
            Assert.Equal(0, report.WarningCount);
        }

        [Fact]
        public void MissingDescription_WarningRaised()
        {
            var lib = CreateLibrary(("test-prompt", "Do something with {{input}}.", null, "general"));
            var report = new PromptHealthCheck().Check(lib);
            Assert.Contains(report.Findings, f => f.RuleCode == "MISSING_DESCRIPTION");
        }

        [Fact]
        public void LongTemplate_WarningRaised()
        {
            var longText = new string('x', 5000);
            var lib = CreateLibrary(("long-one", longText, "A long prompt", "test"));
            var report = new PromptHealthCheck().Check(lib);
            Assert.Contains(report.Findings, f => f.RuleCode == "LONG_TEMPLATE");
        }

        [Fact]
        public void VeryLongTemplate_ErrorRaised()
        {
            var longText = new string('x', 9000);
            var lib = CreateLibrary(("very-long", longText, "Too long", "test"));
            var report = new PromptHealthCheck().Check(lib);
            Assert.Contains(report.Findings, f => f.RuleCode == "TEMPLATE_TOO_LONG");
        }

        [Fact]
        public void DuplicateTemplates_WarningRaised()
        {
            var lib = CreateLibrary(
                ("prompt-a", "Tell me about {{topic}} in detail.", "First", "cat"),
                ("prompt-b", "Tell me about {{topic}} in detail.", "Second", "cat"));
            var report = new PromptHealthCheck().Check(lib);
            Assert.Contains(report.Findings, f => f.RuleCode == "DUPLICATE_TEMPLATE");
        }

        [Fact]
        public void ContainsTodo_WarningRaised()
        {
            var lib = CreateLibrary(
                ("wip", "TODO: improve this {{input}} prompt.", "Work in progress", "dev"));
            var report = new PromptHealthCheck().Check(lib);
            Assert.Contains(report.Findings, f => f.RuleCode == "CONTAINS_TODO");
        }

        [Fact]
        public void SingleCharVariable_WarningRaised()
        {
            var lib = CreateLibrary(
                ("bad-vars", "Process {{x}} with {{y}}.", "Short vars", "test"));
            var report = new PromptHealthCheck().Check(lib);
            Assert.Contains(report.Findings, f => f.RuleCode == "SHORT_VARIABLE_NAME");
        }

        [Fact]
        public void ScoreDecreasesWithIssues()
        {
            var lib = CreateLibrary(
                ("no-desc", "Do {{thing}}.", null, null),
                ("no-desc2", "Do {{other}}.", null, null));
            var report = new PromptHealthCheck().Check(lib);
            Assert.True(report.Score < 100);
        }

        [Fact]
        public void ToSummary_ContainsScoreAndCounts()
        {
            var lib = CreateLibrary(("ok", "Summarize {{text}} clearly.", "Good prompt", "writing"));
            var report = new PromptHealthCheck().Check(lib);
            var summary = report.ToSummary();
            Assert.Contains("Score:", summary);
            Assert.Contains("Errors:", summary);
        }

        [Fact]
        public void ToJson_IsValidJson()
        {
            var lib = CreateLibrary(("ok", "Hello {{name}}.", "Greeting", "social"));
            var report = new PromptHealthCheck().Check(lib);
            var json = report.ToJson();
            Assert.Contains("\"score\"", json);
            Assert.Contains("\"findings\"", json);
        }

        [Fact]
        public void CustomOptions_RespectedForLength()
        {
            var opts = new HealthCheckOptions { MaxTemplateLength = 20, MaxTemplateLengthError = 50 };
            var lib = CreateLibrary(("medium", "This is a somewhat longer template for {{testing}} purposes right here.", "Test", "test"));
            var report = new PromptHealthCheck().Check(lib, opts);
            Assert.Contains(report.Findings, f => f.RuleCode == "LONG_TEMPLATE" || f.RuleCode == "TEMPLATE_TOO_LONG");
        }
    }
}
