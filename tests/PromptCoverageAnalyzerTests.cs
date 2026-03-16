using Xunit;
using Prompt;
using System.Linq;
using System.Text.Json;

namespace Prompt.Tests
{
    public class PromptCoverageAnalyzerTests
    {
        private PromptLibrary CreateSampleLibrary()
        {
            var lib = new PromptLibrary();
            lib.Add("summarize",
                new PromptTemplate("Summarize the following {{text}} in {{style}} style."),
                description: "Summarizes text",
                category: "writing",
                tags: new[] { "summary", "text" });
            lib.Add("translate",
                new PromptTemplate("Translate {{text}} to {{language}}."),
                description: "Translates text",
                category: "writing",
                tags: new[] { "translation", "text" });
            lib.Add("code-review",
                new PromptTemplate("Review this {{language}} code:\n{{code}}\n\nProvide feedback on:\n- Correctness\n- Style\n- Performance"),
                description: "Reviews code",
                category: "coding",
                tags: new[] { "code", "review" });
            lib.Add("debug",
                new PromptTemplate("Debug this error:\n{{error}}\n\nIn context:\n{{code}}"),
                category: "coding",
                tags: new[] { "code", "debug" });
            lib.Add("email",
                new PromptTemplate("Write an email to {{recipient}} about {{topic}}."),
                description: "Drafts emails",
                category: "communication",
                tags: new[] { "email" });
            return lib;
        }

        [Fact]
        public void Analyze_NullLibrary_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => PromptCoverageAnalyzer.Analyze(null!));
        }

        [Fact]
        public void Analyze_EmptyLibrary_ReturnsEmptyReport()
        {
            var lib = new PromptLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.Equal(0, report.TotalPrompts);
            Assert.Equal("N/A", report.Grade);
            Assert.Single(report.Recommendations);
            Assert.Equal("Critical", report.Recommendations[0].Severity);
        }

        [Fact]
        public void Analyze_CountsPromptsCorrectly()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.Equal(5, report.TotalPrompts);
        }

        [Fact]
        public void Analyze_DetectsCategories()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.Equal(3, report.UniqueCategories);
            Assert.True(report.CategoryDistribution.Any(c => c.Name == "writing"));
            Assert.True(report.CategoryDistribution.Any(c => c.Name == "coding"));
            Assert.True(report.CategoryDistribution.Any(c => c.Name == "communication"));
        }

        [Fact]
        public void Analyze_CategoryDistribution_HasPercentages()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            var writing = report.CategoryDistribution.First(c => c.Name == "writing");
            Assert.Equal(2, writing.Count);
            Assert.Equal(40.0, writing.Percentage);
        }

        [Fact]
        public void Analyze_DetectsTags()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.True(report.UniqueTags > 0);
            Assert.True(report.TagDistribution.Any(t => t.Name.Equals("text", System.StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void Analyze_DetectsVariables()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.True(report.UniqueVariables > 0);
            Assert.True(report.VariableUsage.Any(v => v.Variable == "text"));
            Assert.True(report.VariableUsage.Any(v => v.Variable == "code"));
        }

        [Fact]
        public void Analyze_VariableUsageCount_IsCorrect()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            var textVar = report.VariableUsage.First(v => v.Variable == "text");
            Assert.Equal(2, textVar.UsageCount); // summarize + translate
        }

        [Fact]
        public void Analyze_DetectsUncategorizedPrompts()
        {
            var lib = new PromptLibrary();
            lib.Add("no-cat", new PromptTemplate("Hello {{name}}"));
            lib.Add("has-cat", new PromptTemplate("Hi {{name}}"), category: "greeting");

            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.Single(report.UncategorizedPrompts);
            Assert.Contains("no-cat", report.UncategorizedPrompts);
        }

        [Fact]
        public void Analyze_DetectsUntaggedPrompts()
        {
            var lib = new PromptLibrary();
            lib.Add("no-tags", new PromptTemplate("Hello"));
            lib.Add("has-tags", new PromptTemplate("Hi"), tags: new[] { "greeting" });

            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.Single(report.UntaggedPrompts);
            Assert.Contains("no-tags", report.UntaggedPrompts);
        }

        [Fact]
        public void Analyze_CalculatesAverageVariables()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.True(report.AverageVariablesPerPrompt > 0);
        }

        [Fact]
        public void Analyze_CalculatesAverageTemplateLength()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.True(report.AverageTemplateLength > 0);
        }

        [Fact]
        public void Analyze_HasComplexityDistribution()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.Equal(4, report.ComplexityDistribution.Count);
            Assert.True(report.ComplexityDistribution.Sum(b => b.Count) == 5);
        }

        [Fact]
        public void Analyze_HealthScore_InRange()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.InRange(report.HealthScore, 0, 100);
            Assert.True(new[] { "A", "B", "C", "D", "F" }.Contains(report.Grade));
        }

        [Fact]
        public void Analyze_Summary_ContainsKey()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.Contains("5 prompts", report.Summary);
            Assert.Contains("3 categories", report.Summary);
        }

        [Fact]
        public void Analyze_WarnsOnConcentratedCategory()
        {
            var lib = new PromptLibrary();
            for (int i = 0; i < 8; i++)
                lib.Add($"p{i}", new PromptTemplate($"Prompt {i}"), category: "writing");
            lib.Add("p8", new PromptTemplate("Other"), category: "other");

            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.True(report.Recommendations.Any(r => r.Category == "Distribution" && r.Severity == "Warning"));
        }

        [Fact]
        public void Analyze_WarnsOnShortPrompts()
        {
            var lib = new PromptLibrary();
            lib.Add("tiny", new PromptTemplate("Hi"), category: "test");

            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.True(report.Recommendations.Any(r => r.Category == "Quality" && r.Message.Contains("short")));
        }

        [Fact]
        public void Analyze_NoDescriptionWarning()
        {
            var lib = new PromptLibrary();
            lib.Add("no-desc", new PromptTemplate("Hello {{name}}"), category: "test");

            var report = PromptCoverageAnalyzer.Analyze(lib);

            Assert.True(report.Recommendations.Any(r => r.Message.Contains("no description")));
        }

        [Fact]
        public void ExportText_NullReport_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => PromptCoverageAnalyzer.ExportText(null!));
        }

        [Fact]
        public void ExportText_ContainsReportSections()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);
            var text = PromptCoverageAnalyzer.ExportText(report);

            Assert.Contains("PROMPT LIBRARY COVERAGE REPORT", text);
            Assert.Contains("Category Distribution", text);
            Assert.Contains("Variable Usage", text);
            Assert.Contains("Complexity Distribution", text);
        }

        [Fact]
        public void ExportJson_NullReport_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => PromptCoverageAnalyzer.ExportJson(null!));
        }

        [Fact]
        public void ExportJson_IsValidJson()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);
            var json = PromptCoverageAnalyzer.ExportJson(report);

            var doc = JsonDocument.Parse(json);
            Assert.Equal(5, doc.RootElement.GetProperty("totalPrompts").GetInt32());
        }

        [Fact]
        public void ExportJson_Compact()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);
            var json = PromptCoverageAnalyzer.ExportJson(report, indented: false);

            Assert.DoesNotContain("\n", json);
        }

        [Fact]
        public void ExportHtml_NullReport_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => PromptCoverageAnalyzer.ExportHtml(null!));
        }

        [Fact]
        public void ExportHtml_ContainsStructure()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);
            var html = PromptCoverageAnalyzer.ExportHtml(report);

            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("Coverage Report", html);
            Assert.Contains("Category Distribution", html);
        }

        [Fact]
        public void ExportHtml_ContainsGrade()
        {
            var lib = CreateSampleLibrary();
            var report = PromptCoverageAnalyzer.Analyze(lib);
            var html = PromptCoverageAnalyzer.ExportHtml(report);

            Assert.Contains(report.Grade, html);
        }

        [Fact]
        public void Analyze_WellTaggedLibrary_HigherScore()
        {
            var good = new PromptLibrary();
            good.Add("a", new PromptTemplate("Prompt about {{topic}} in detail"),
                description: "Desc A", category: "cat1", tags: new[] { "t1", "t2" });
            good.Add("b", new PromptTemplate("Another {{topic}} prompt for {{user}}"),
                description: "Desc B", category: "cat2", tags: new[] { "t1", "t3" });

            var bad = new PromptLibrary();
            bad.Add("c", new PromptTemplate("Hi"));
            bad.Add("d", new PromptTemplate("Yo"));

            var goodReport = PromptCoverageAnalyzer.Analyze(good);
            var badReport = PromptCoverageAnalyzer.Analyze(bad);

            Assert.True(goodReport.HealthScore > badReport.HealthScore);
        }

        [Fact]
        public void Analyze_VariableDefaultDetection()
        {
            var lib = new PromptLibrary();
            lib.Add("with-default",
                new PromptTemplate("Hello {{name}}", new System.Collections.Generic.Dictionary<string, string> { ["name"] = "World" }),
                category: "test");

            var report = PromptCoverageAnalyzer.Analyze(lib);

            var nameVar = report.VariableUsage.First(v => v.Variable == "name");
            Assert.True(nameVar.AlwaysHasDefault);
        }
    }
}
