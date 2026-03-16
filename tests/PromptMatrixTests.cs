namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class PromptMatrixTests
    {
        private readonly PromptMatrix _matrix = new();

        // ── Basic expansion ──────────────────────────────────

        [Fact]
        public void Expand_SingleVariable_GeneratesAllValues()
        {
            var config = new MatrixConfig()
                .AddVariable("role", "expert", "beginner");

            var result = _matrix.Expand("You are a {{role}}.", config);

            Assert.Equal(2, result.TotalCombinations);
            Assert.Equal("You are a expert.", result.Cells[0].Text);
            Assert.Equal("You are a beginner.", result.Cells[1].Text);
        }

        [Fact]
        public void Expand_TwoVariables_CartesianProduct()
        {
            var config = new MatrixConfig()
                .AddVariable("role", "expert", "novice")
                .AddVariable("tone", "formal", "casual");

            var result = _matrix.Expand("{{role}} in {{tone}} tone.", config);

            Assert.Equal(4, result.TotalCombinations);
            var texts = result.Cells.Select(c => c.Text).ToList();
            Assert.Contains("expert in formal tone.", texts);
            Assert.Contains("expert in casual tone.", texts);
            Assert.Contains("novice in formal tone.", texts);
            Assert.Contains("novice in casual tone.", texts);
        }

        [Fact]
        public void Expand_ThreeVariables_CorrectCount()
        {
            var config = new MatrixConfig()
                .AddVariable("a", "1", "2")
                .AddVariable("b", "x", "y", "z")
                .AddVariable("c", "p", "q");

            var result = _matrix.Expand("{{a}}-{{b}}-{{c}}", config);

            Assert.Equal(12, result.TotalCombinations); // 2 × 3 × 2
        }

        // ── Placeholder extraction ──────────────────────────

        [Fact]
        public void ExtractPlaceholders_FindsAllVariables()
        {
            var vars = _matrix.ExtractPlaceholders("Hello {{name}}, your {{role}} is {{level}}.");
            Assert.Equal(3, vars.Count);
            Assert.Contains("name", vars);
            Assert.Contains("role", vars);
            Assert.Contains("level", vars);
        }

        [Fact]
        public void ExtractPlaceholders_DeduplicatesRepeatedVars()
        {
            var vars = _matrix.ExtractPlaceholders("{{x}} and {{x}} and {{y}}");
            Assert.Equal(2, vars.Count);
        }

        [Fact]
        public void ExtractPlaceholders_EmptyTemplate_EmptySet()
        {
            var vars = _matrix.ExtractPlaceholders("");
            Assert.Empty(vars);
        }

        // ── Render ───────────────────────────────────────────

        [Fact]
        public void Render_SubstitutesVariables()
        {
            var result = _matrix.Render("Hello {{name}}!",
                new Dictionary<string, string> { ["name"] = "World" });
            Assert.Equal("Hello World!", result);
        }

        [Fact]
        public void Render_LeavesUnresolvedPlaceholders()
        {
            var result = _matrix.Render("{{a}} and {{b}}",
                new Dictionary<string, string> { ["a"] = "X" });
            Assert.Equal("X and {{b}}", result);
        }

        // ── QuickExpand ──────────────────────────────────────

        [Fact]
        public void QuickExpand_ConvenienceSyntax()
        {
            var result = _matrix.QuickExpand(
                "{{role}} says {{greeting}}",
                "role", "doctor,nurse",
                "greeting", "hello,hi");

            Assert.Equal(4, result.TotalCombinations);
        }

        [Fact]
        public void QuickExpand_OddArgs_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _matrix.QuickExpand("{{x}}", "x"));
        }

        // ── Filter ───────────────────────────────────────────

        [Fact]
        public void Expand_WithFilter_ExcludesCombinations()
        {
            var config = new MatrixConfig()
                .AddVariable("x", "1", "2", "3")
                .AddVariable("y", "a", "b");

            // Only keep combinations where x != "2"
            config.Filter = vars => vars["x"] != "2";

            var result = _matrix.Expand("{{x}}-{{y}}", config);

            Assert.Equal(4, result.TotalCombinations); // 6 total - 2 filtered
            Assert.True(result.Cells.All(c => c.Variables["x"] != "2"));
        }

        // ── Where / GroupBy ──────────────────────────────────

        [Fact]
        public void Where_FiltersResultsByVariable()
        {
            var config = new MatrixConfig()
                .AddVariable("role", "expert", "novice")
                .AddVariable("format", "list", "paragraph");

            var result = _matrix.Expand("{{role}} in {{format}}", config);
            var experts = result.Where("role", "expert");

            Assert.Equal(2, experts.Count);
            Assert.True(experts.All(c => c.Variables["role"] == "expert"));
        }

        [Fact]
        public void GroupBy_GroupsCellsByVariable()
        {
            var config = new MatrixConfig()
                .AddVariable("role", "expert", "novice")
                .AddVariable("tone", "formal", "casual");

            var result = _matrix.Expand("{{role}} {{tone}}", config);
            var groups = result.GroupBy("role");

            Assert.Equal(2, groups.Count);
            Assert.Equal(2, groups["expert"].Count);
            Assert.Equal(2, groups["novice"].Count);
        }

        // ── Statistics ───────────────────────────────────────

        [Fact]
        public void Result_HasTokenStats()
        {
            var config = new MatrixConfig()
                .AddVariable("content", "short", "a much longer value that uses more tokens");

            var result = _matrix.Expand("Analyze: {{content}}", config);

            Assert.True(result.MinTokens > 0);
            Assert.True(result.MaxTokens >= result.MinTokens);
            Assert.True(result.AverageTokens > 0);
        }

        // ── Serialization ────────────────────────────────────

        [Fact]
        public void ToJson_RoundTrips()
        {
            var config = new MatrixConfig()
                .AddVariable("x", "a", "b")
                .AddVariable("y", "1", "2");

            var result = _matrix.Expand("{{x}}-{{y}}", config);
            var json = result.ToJson();
            var restored = MatrixResult.FromJson(json);

            Assert.Equal(result.TotalCombinations, restored.TotalCombinations);
            Assert.Equal(result.Cells[0].Text, restored.Cells[0].Text);
            Assert.Equal(result.Template, restored.Template);
        }

        [Fact]
        public void ToCsv_ContainsAllCells()
        {
            var config = new MatrixConfig()
                .AddVariable("x", "a", "b");

            var result = _matrix.Expand("test {{x}}", config);
            var csv = result.ToCsv();

            Assert.Contains("index,label,estimatedTokens", csv);
            Assert.Contains("test a", csv);
            Assert.Contains("test b", csv);
        }

        [Fact]
        public void GetSummary_IncludesStats()
        {
            var config = new MatrixConfig()
                .AddVariable("x", "a", "b");

            var result = _matrix.Expand("{{x}}", config);
            var summary = result.GetSummary();

            Assert.Contains("Prompt Matrix Summary", summary);
            Assert.Contains("Total combinations: 2", summary);
        }

        // ── Validation ───────────────────────────────────────

        [Fact]
        public void Expand_EmptyTemplate_Throws()
        {
            var config = new MatrixConfig().AddVariable("x", "a");
            Assert.Throws<ArgumentException>(() => _matrix.Expand("", config));
        }

        [Fact]
        public void Expand_NoVariables_Throws()
        {
            var config = new MatrixConfig();
            Assert.Throws<ArgumentException>(() => _matrix.Expand("hello", config));
        }

        [Fact]
        public void Expand_NoMatchingPlaceholders_Throws()
        {
            var config = new MatrixConfig().AddVariable("x", "a");
            Assert.Throws<ArgumentException>(() =>
                _matrix.Expand("no placeholders here", config));
        }

        [Fact]
        public void Expand_ExceedsMaxCombinations_Throws()
        {
            var config = new MatrixConfig { MaxCombinations = 5 };
            config.AddVariable("a", "1", "2", "3");
            config.AddVariable("b", "x", "y", "z");
            // 3 × 3 = 9 > 5

            Assert.Throws<InvalidOperationException>(() =>
                _matrix.Expand("{{a}}-{{b}}", config));
        }

        [Fact]
        public void AddVariable_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new MatrixConfig().AddVariable("", "val"));
        }

        [Fact]
        public void AddVariable_NoValues_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new MatrixConfig().AddVariable("x"));
        }

        // ── Cell metadata ────────────────────────────────────

        [Fact]
        public void Cells_HaveCorrectIndicesAndLabels()
        {
            var config = new MatrixConfig()
                .AddVariable("x", "a", "b");

            var result = _matrix.Expand("{{x}}", config);

            Assert.Equal(0, result.Cells[0].Index);
            Assert.Equal(1, result.Cells[1].Index);
            Assert.Contains("x=a", result.Cells[0].Label);
            Assert.Contains("x=b", result.Cells[1].Label);
        }

        [Fact]
        public void Cells_VariablesDictionaryIsCorrect()
        {
            var config = new MatrixConfig()
                .AddVariable("role", "dev")
                .AddVariable("lang", "C#");

            var result = _matrix.Expand("{{role}} uses {{lang}}", config);

            Assert.Single(result.Cells);
            Assert.Equal("dev", result.Cells[0].Variables["role"]);
            Assert.Equal("C#", result.Cells[0].Variables["lang"]);
        }

        [Fact]
        public void Expand_RepeatedPlaceholder_SubstitutedEverywhere()
        {
            var config = new MatrixConfig()
                .AddVariable("x", "hello");

            var result = _matrix.Expand("{{x}} and {{x}}", config);

            Assert.Equal("hello and hello", result.Cells[0].Text);
        }
    }
}
