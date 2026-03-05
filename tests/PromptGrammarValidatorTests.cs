using Xunit;
using Prompt;

namespace Prompt.Tests
{
    public class PromptGrammarValidatorTests
    {
        private readonly PromptGrammarValidator _validator = new();

        // ── Regex ──────────────────────────────────────────────────────

        [Fact]
        public void Regex_MatchingPattern_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.RegexRule("r1", "Email", @"\w+@\w+\.\w+") } };
            var result = _validator.Validate("user@example.com", rs);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Regex_NonMatching_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.RegexRule("r1", "Email", @"^\w+@\w+\.\w+$") } };
            var result = _validator.Validate("not an email", rs);
            Assert.False(result.IsValid);
            Assert.Single(result.Violations);
        }

        [Fact]
        public void Regex_IgnoreCase_Works()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.RegexRule("r1", "Upper", @"^hello$", ignoreCase: true) } };
            Assert.True(_validator.Validate("HELLO", rs).IsValid);
        }

        [Fact]
        public void Regex_InvalidPattern_ReportsViolation()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.RegexRule("r1", "Bad", @"[invalid") } };
            var result = _validator.Validate("anything", rs);
            Assert.False(result.IsValid);
            Assert.Contains("Invalid regex", result.Violations[0].Message);
        }

        // ── Enum ───────────────────────────────────────────────────────

        [Fact]
        public void Enum_AllowedValue_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EnumRule("e1", "Sentiment", new[] { "positive", "negative", "neutral" }) } };
            Assert.True(_validator.Validate("positive", rs).IsValid);
        }

        [Fact]
        public void Enum_DisallowedValue_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EnumRule("e1", "Sentiment", new[] { "positive", "negative" }) } };
            Assert.False(_validator.Validate("maybe", rs).IsValid);
        }

        [Fact]
        public void Enum_IgnoreCase_Works()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EnumRule("e1", "YN", new[] { "yes", "no" }, ignoreCase: true) } };
            Assert.True(_validator.Validate("YES", rs).IsValid);
        }

        [Fact]
        public void Enum_TrimsWhitespace()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EnumRule("e1", "Val", new[] { "ok" }) } };
            Assert.True(_validator.Validate("  ok  ", rs).IsValid);
        }

        // ── Length ─────────────────────────────────────────────────────

        [Fact]
        public void Length_WithinBounds_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "Len", min: 5, max: 20) } };
            Assert.True(_validator.Validate("hello world", rs).IsValid);
        }

        [Fact]
        public void Length_TooShort_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "Len", min: 10) } };
            var result = _validator.Validate("hi", rs);
            Assert.False(result.IsValid);
            Assert.Contains("too short", result.Violations[0].Message);
        }

        [Fact]
        public void Length_TooLong_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "Len", max: 5) } };
            Assert.False(_validator.Validate("this is too long", rs).IsValid);
        }

        // ── LineCount ──────────────────────────────────────────────────

        [Fact]
        public void LineCount_WithinBounds_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LineCountRule("lc1", "Lines", min: 2, max: 5) } };
            Assert.True(_validator.Validate("line1\nline2\nline3", rs).IsValid);
        }

        [Fact]
        public void LineCount_TooFew_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LineCountRule("lc1", "Lines", min: 3) } };
            Assert.False(_validator.Validate("one line", rs).IsValid);
        }

        [Fact]
        public void LineCount_TooMany_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LineCountRule("lc1", "Lines", max: 2) } };
            Assert.False(_validator.Validate("a\nb\nc\nd", rs).IsValid);
        }

        // ── StartsWith / EndsWith ──────────────────────────────────────

        [Fact]
        public void StartsWith_Matching_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StartsWithRule("s1", "Prefix", "Answer:") } };
            Assert.True(_validator.Validate("Answer: 42", rs).IsValid);
        }

        [Fact]
        public void StartsWith_NotMatching_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StartsWithRule("s1", "Prefix", "Answer:") } };
            Assert.False(_validator.Validate("42 is the answer", rs).IsValid);
        }

        [Fact]
        public void EndsWith_Matching_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EndsWithRule("e1", "Suffix", ".") } };
            Assert.True(_validator.Validate("Done.", rs).IsValid);
        }

        [Fact]
        public void EndsWith_NotMatching_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EndsWithRule("e1", "Suffix", ".") } };
            Assert.False(_validator.Validate("No period", rs).IsValid);
        }

        // ── Contains / NotContains ─────────────────────────────────────

        [Fact]
        public void Contains_Found_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.ContainsRule("c1", "Has Keyword", "important") } };
            Assert.True(_validator.Validate("This is important stuff", rs).IsValid);
        }

        [Fact]
        public void Contains_NotFound_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.ContainsRule("c1", "Has Keyword", "missing") } };
            Assert.False(_validator.Validate("nothing here", rs).IsValid);
        }

        [Fact]
        public void NotContains_Absent_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.NotContainsRule("nc1", "No PII", "SSN") } };
            Assert.True(_validator.Validate("safe text", rs).IsValid);
        }

        [Fact]
        public void NotContains_Present_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.NotContainsRule("nc1", "No PII", "SSN") } };
            var result = _validator.Validate("Your SSN is...", rs);
            Assert.False(result.IsValid);
            Assert.NotNull(result.Violations[0].Position);
        }

        // ── JsonSchema ─────────────────────────────────────────────────

        [Fact]
        public void JsonSchema_ValidJson_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON") } };
            Assert.True(_validator.Validate("{\"key\": \"value\"}", rs).IsValid);
        }

        [Fact]
        public void JsonSchema_InvalidJson_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON") } };
            Assert.False(_validator.Validate("not json", rs).IsValid);
        }

        [Fact]
        public void JsonSchema_RequiredProperty_Present()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON", new() { "name", "age" }) } };
            Assert.True(_validator.Validate("{\"name\": \"Alice\", \"age\": 30}", rs).IsValid);
        }

        [Fact]
        public void JsonSchema_RequiredProperty_Missing()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON", new() { "name", "email" }) } };
            var result = _validator.Validate("{\"name\": \"Alice\"}", rs);
            Assert.False(result.IsValid);
            Assert.Contains("email", result.Violations[0].Message);
        }

        [Fact]
        public void JsonSchema_PropertyType_Correct()
        {
            var types = new Dictionary<string, string> { { "count", "number" }, { "active", "boolean" } };
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON", propertyTypes: types) } };
            Assert.True(_validator.Validate("{\"count\": 5, \"active\": true}", rs).IsValid);
        }

        [Fact]
        public void JsonSchema_PropertyType_Wrong()
        {
            var types = new Dictionary<string, string> { { "count", "number" } };
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON", propertyTypes: types) } };
            var result = _validator.Validate("{\"count\": \"five\"}", rs);
            Assert.False(result.IsValid);
            Assert.Contains("wrong type", result.Violations[0].Message);
        }

        [Fact]
        public void JsonSchema_NestedProperty()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON", new() { "user.name" }) } };
            Assert.True(_validator.Validate("{\"user\": {\"name\": \"Bob\"}}", rs).IsValid);
        }

        [Fact]
        public void JsonSchema_ExtractsFromCodeBlock()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON", new() { "x" }) } };
            Assert.True(_validator.Validate("```json\n{\"x\": 1}\n```", rs).IsValid);
        }

        [Fact]
        public void JsonSchema_Array_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.JsonSchemaRule("j1", "JSON") } };
            Assert.True(_validator.Validate("[1, 2, 3]", rs).IsValid);
        }

        // ── Structure ──────────────────────────────────────────────────

        [Fact]
        public void Structure_AllSectionsPresent_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StructureRule("st1", "Sections", new() { "Summary", "Details" }) } };
            Assert.True(_validator.Validate("# Summary\nOverview\n# Details\nMore info", rs).IsValid);
        }

        [Fact]
        public void Structure_MissingSection_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StructureRule("st1", "Sections", new() { "Summary", "Conclusion" }) } };
            var result = _validator.Validate("# Summary\nOnly summary here", rs);
            Assert.False(result.IsValid);
            Assert.Contains("Conclusion", result.Violations[0].Message);
        }

        [Fact]
        public void Structure_ColonFormat_Passes()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StructureRule("st1", "Sections", new() { "Summary" }) } };
            Assert.True(_validator.Validate("Summary: here is the summary", rs).IsValid);
        }

        // ── Custom ─────────────────────────────────────────────────────

        [Fact]
        public void Custom_PassingValidator_Passes()
        {
            var rule = PromptGrammarValidator.CustomRule("c1", "Even Length",
                s => (s.Length % 2 == 0, "Length must be even"));
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { rule } };
            Assert.True(_validator.Validate("abcd", rs).IsValid);
        }

        [Fact]
        public void Custom_FailingValidator_Fails()
        {
            var rule = PromptGrammarValidator.CustomRule("c1", "Even Length",
                s => (s.Length % 2 == 0, "Length must be even"));
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { rule } };
            Assert.False(_validator.Validate("abc", rs).IsValid);
        }

        [Fact]
        public void Custom_NullValidator_Fails()
        {
            var rule = new GrammarRule { Id = "c1", Name = "Null", Type = GrammarRuleType.Custom };
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { rule } };
            Assert.False(_validator.Validate("x", rs).IsValid);
        }

        // ── Multiple Rules ─────────────────────────────────────────────

        [Fact]
        public void MultipleRules_AllPass()
        {
            var rs = new GrammarRuleSet
            {
                Name = "multi",
                Rules = new()
                {
                    PromptGrammarValidator.LengthRule("l1", "Min", min: 5),
                    PromptGrammarValidator.ContainsRule("c1", "Has Hello", "hello"),
                    PromptGrammarValidator.EndsWithRule("e1", "Ends", "!")
                }
            };
            Assert.True(_validator.Validate("say hello!", rs).IsValid);
            Assert.Equal(100.0, _validator.Validate("say hello!", rs).Score);
        }

        [Fact]
        public void MultipleRules_SomeFail()
        {
            var rs = new GrammarRuleSet
            {
                Name = "multi",
                Rules = new()
                {
                    PromptGrammarValidator.LengthRule("l1", "Min", min: 5),
                    PromptGrammarValidator.ContainsRule("c1", "Has Hello", "hello"),
                    PromptGrammarValidator.EndsWithRule("e1", "Ends", "!")
                }
            };
            var result = _validator.Validate("say hello.", rs);
            Assert.False(result.IsValid);
            Assert.Equal(2, result.PassedRules);
            Assert.Equal(1, result.FailedRules);
        }

        [Fact]
        public void Score_CalculatedCorrectly()
        {
            var rs = new GrammarRuleSet
            {
                Name = "test",
                Rules = new()
                {
                    PromptGrammarValidator.LengthRule("l1", "A", min: 1),
                    PromptGrammarValidator.LengthRule("l2", "B", max: 1),
                    PromptGrammarValidator.LengthRule("l3", "C", max: 1),
                    PromptGrammarValidator.LengthRule("l4", "D", max: 1)
                }
            };
            var result = _validator.Validate("hello", rs);
            Assert.Equal(25.0, result.Score); // 1 of 4
        }

        // ── FailFast ───────────────────────────────────────────────────

        [Fact]
        public void FailFast_StopsAtFirstError()
        {
            var rs = new GrammarRuleSet
            {
                Name = "fast",
                FailFast = true,
                Rules = new()
                {
                    PromptGrammarValidator.LengthRule("l1", "TooShort", min: 100),
                    PromptGrammarValidator.ContainsRule("c1", "Missing", "xyz"),
                }
            };
            var result = _validator.Validate("hi", rs);
            Assert.Single(result.Violations);
        }

        [Fact]
        public void FailFast_WarningDoesNotStop()
        {
            var rs = new GrammarRuleSet
            {
                Name = "fast",
                FailFast = true,
                Rules = new()
                {
                    PromptGrammarValidator.LengthRule("l1", "Short", min: 100, severity: ViolationSeverity.Warning),
                    PromptGrammarValidator.ContainsRule("c1", "Missing", "xyz"),
                }
            };
            var result = _validator.Validate("hi", rs);
            Assert.Equal(2, result.Violations.Count);
        }

        // ── Disabled Rules ─────────────────────────────────────────────

        [Fact]
        public void DisabledRule_IsSkipped()
        {
            var rule = new GrammarRule { Id = "d1", Name = "Disabled", Type = GrammarRuleType.Contains, Pattern = "xyz", Enabled = false };
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { rule } };
            var result = _validator.Validate("no xyz here", rs);
            Assert.True(result.IsValid);
            Assert.Equal(0, result.TotalRules);
        }

        // ── Severity ───────────────────────────────────────────────────

        [Fact]
        public void WarningOnly_StillValid()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "Long", max: 3, severity: ViolationSeverity.Warning) } };
            var result = _validator.Validate("hello", rs);
            Assert.True(result.IsValid);
            Assert.Single(result.Violations);
        }

        [Fact]
        public void InfoOnly_StillValid()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.ContainsRule("i1", "Tip", "please", severity: ViolationSeverity.Info) } };
            Assert.True(_validator.Validate("do this", rs).IsValid);
        }

        // ── Rule Set Management ────────────────────────────────────────

        [Fact]
        public void AddAndGetRuleSet()
        {
            var rs = new GrammarRuleSet { Name = "mySet" };
            _validator.AddRuleSet(rs);
            Assert.NotNull(_validator.GetRuleSet("mySet"));
        }

        [Fact]
        public void ListRuleSets_ReturnsNames()
        {
            _validator.AddRuleSet(new GrammarRuleSet { Name = "b" });
            _validator.AddRuleSet(new GrammarRuleSet { Name = "a" });
            var names = _validator.ListRuleSets();
            Assert.Equal(new[] { "a", "b" }, names);
        }

        [Fact]
        public void RemoveRuleSet_Works()
        {
            _validator.AddRuleSet(new GrammarRuleSet { Name = "temp" });
            Assert.True(_validator.RemoveRuleSet("temp"));
            Assert.Null(_validator.GetRuleSet("temp"));
        }

        [Fact]
        public void ValidateByName_Works()
        {
            var rs = new GrammarRuleSet { Name = "named", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 1) } };
            _validator.AddRuleSet(rs);
            Assert.True(_validator.Validate("ok", "named").IsValid);
        }

        [Fact]
        public void ValidateByName_NotFound_Throws()
        {
            Assert.Throws<ArgumentException>(() => _validator.Validate("x", "nonexistent"));
        }

        [Fact]
        public void AddRuleSet_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => _validator.AddRuleSet(new GrammarRuleSet { Name = "" }));
        }

        // ── Presets ────────────────────────────────────────────────────

        [Fact]
        public void Presets_Available()
        {
            var presets = _validator.ListPresets();
            Assert.Contains("json-response", presets);
            Assert.Contains("yes-no", presets);
            Assert.Contains("classification", presets);
            Assert.Contains("markdown-doc", presets);
            Assert.Contains("bullet-list", presets);
            Assert.Contains("code-block", presets);
        }

        [Fact]
        public void Preset_YesNo_Valid()
        {
            var preset = _validator.GetPreset("yes-no")!;
            Assert.True(_validator.Validate("yes", preset).IsValid);
        }

        [Fact]
        public void Preset_YesNo_Invalid()
        {
            var preset = _validator.GetPreset("yes-no")!;
            Assert.False(_validator.Validate("maybe", preset).IsValid);
        }

        [Fact]
        public void Preset_JsonResponse_Valid()
        {
            var preset = _validator.GetPreset("json-response")!;
            Assert.True(_validator.Validate("{\"status\": \"ok\"}", preset).IsValid);
        }

        [Fact]
        public void Preset_BulletList_Valid()
        {
            var preset = _validator.GetPreset("bullet-list")!;
            Assert.True(_validator.Validate("- item 1\n- item 2", preset).IsValid);
        }

        [Fact]
        public void Preset_CodeBlock_Valid()
        {
            var preset = _validator.GetPreset("code-block")!;
            Assert.True(_validator.Validate("```python\nprint('hi')\n```", preset).IsValid);
        }

        // ── Batch Validation ───────────────────────────────────────────

        [Fact]
        public void Batch_AllValid()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 1) } };
            var batch = _validator.ValidateBatch(new[] { "a", "bb", "ccc" }, rs);
            Assert.Equal(3, batch.TotalResponses);
            Assert.Equal(3, batch.ValidCount);
            Assert.Equal(0, batch.InvalidCount);
            Assert.Equal(100.0, batch.AverageScore);
        }

        [Fact]
        public void Batch_MixedResults()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 3) } };
            var batch = _validator.ValidateBatch(new[] { "hi", "hello", "ok" }, rs);
            Assert.Equal(1, batch.ValidCount);
            Assert.Equal(2, batch.InvalidCount);
        }

        [Fact]
        public void Batch_CommonViolations()
        {
            var rs = new GrammarRuleSet
            {
                Name = "test",
                Rules = new()
                {
                    PromptGrammarValidator.ContainsRule("c1", "Has X", "x"),
                    PromptGrammarValidator.ContainsRule("c2", "Has Y", "y")
                }
            };
            var batch = _validator.ValidateBatch(new[] { "a", "b", "x" }, rs);
            var topViolation = batch.CommonViolations.First();
            Assert.Equal("c2", topViolation.RuleId);
            Assert.Equal(3, topViolation.Occurrences);
        }

        [Fact]
        public void Batch_Empty()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() };
            var batch = _validator.ValidateBatch(Array.Empty<string>(), rs);
            Assert.Equal(0, batch.TotalResponses);
        }

        // ── Fix Suggestions ────────────────────────────────────────────

        [Fact]
        public void Fix_StartsWith_AutoFix()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StartsWithRule("s1", "Prefix", "Answer: ") } };
            var report = _validator.ValidateWithFixes("42", rs, applyAutoFix: true);
            Assert.True(report.AutoFixApplied);
            Assert.Equal("Answer: 42", report.AutoFixed);
        }

        [Fact]
        public void Fix_EndsWith_AutoFix()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EndsWithRule("e1", "Period", ".") } };
            var report = _validator.ValidateWithFixes("Done", rs, applyAutoFix: true);
            Assert.Equal("Done.", report.AutoFixed);
        }

        [Fact]
        public void Fix_Length_Truncate()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "Max", max: 5) } };
            var report = _validator.ValidateWithFixes("toolongtext", rs, applyAutoFix: true);
            Assert.Equal("toolo", report.AutoFixed);
        }

        [Fact]
        public void Fix_NotContains_RemovesForbidden()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.NotContainsRule("nc1", "No Bad", "BAD") } };
            var report = _validator.ValidateWithFixes("this is BAD stuff", rs, applyAutoFix: true);
            Assert.DoesNotContain("BAD", report.AutoFixed);
        }

        [Fact]
        public void Fix_Enum_NotAutoFixable()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.EnumRule("e1", "Val", new[] { "a", "b" }) } };
            var report = _validator.ValidateWithFixes("c", rs, applyAutoFix: true);
            Assert.False(report.AutoFixApplied);
            Assert.Null(report.AutoFixed);
        }

        [Fact]
        public void Fix_WithoutAutoFix_NoChange()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StartsWithRule("s1", "Prefix", "X") } };
            var report = _validator.ValidateWithFixes("hello", rs, applyAutoFix: false);
            Assert.False(report.AutoFixApplied);
            Assert.Null(report.AutoFixed);
            Assert.Single(report.Suggestions);
        }

        // ── Reports ────────────────────────────────────────────────────

        [Fact]
        public void Report_ValidResult()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 1) } };
            var result = _validator.Validate("ok", rs);
            var report = PromptGrammarValidator.GenerateReport(result);
            Assert.Contains("VALID", report);
            Assert.Contains("100%", report);
        }

        [Fact]
        public void Report_InvalidResult()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.ContainsRule("c1", "Missing X", "x") } };
            var result = _validator.Validate("no", rs);
            var report = PromptGrammarValidator.GenerateReport(result);
            Assert.Contains("INVALID", report);
            Assert.Contains("Missing X", report);
        }

        [Fact]
        public void BatchReport_Generated()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 3) } };
            var batch = _validator.ValidateBatch(new[] { "hi", "hello" }, rs);
            var report = PromptGrammarValidator.GenerateBatchReport(batch);
            Assert.Contains("Batch Validation Report", report);
            Assert.Contains("Valid: 1", report);
        }

        // ── Serialization ──────────────────────────────────────────────

        [Fact]
        public void Export_Import_RuleSets()
        {
            var rs = new GrammarRuleSet { Name = "exported", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 5) } };
            _validator.AddRuleSet(rs);
            var json = _validator.ExportRuleSets();

            var v2 = new PromptGrammarValidator();
            var count = v2.ImportRuleSets(json);
            Assert.Equal(1, count);
            Assert.NotNull(v2.GetRuleSet("exported"));
        }

        [Fact]
        public void EmptyRuleSet_AllPass()
        {
            var rs = new GrammarRuleSet { Name = "empty", Rules = new() };
            var result = _validator.Validate("anything", rs);
            Assert.True(result.IsValid);
            Assert.Equal(100.0, result.Score);
        }

        // ── Edge Cases ─────────────────────────────────────────────────

        [Fact]
        public void EmptyResponse_LengthMin_Fails()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 1) } };
            Assert.False(_validator.Validate("", rs).IsValid);
        }

        [Fact]
        public void VeryLongResponse_Works()
        {
            var long_text = new string('x', 100000);
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.LengthRule("l1", "L", min: 1) } };
            Assert.True(_validator.Validate(long_text, rs).IsValid);
        }

        [Fact]
        public void StartsWith_IgnoreCase()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.StartsWithRule("s1", "P", "answer", ignoreCase: true) } };
            Assert.True(_validator.Validate("ANSWER: 42", rs).IsValid);
        }

        [Fact]
        public void Contains_IgnoreCase()
        {
            var rs = new GrammarRuleSet { Name = "test", Rules = new() { PromptGrammarValidator.ContainsRule("c1", "C", "HELLO", ignoreCase: true) } };
            Assert.True(_validator.Validate("say hello world", rs).IsValid);
        }

        [Fact]
        public void PassedRuleIds_Populated()
        {
            var rs = new GrammarRuleSet
            {
                Name = "test",
                Rules = new()
                {
                    PromptGrammarValidator.LengthRule("l1", "A", min: 1),
                    PromptGrammarValidator.LengthRule("l2", "B", max: 100)
                }
            };
            var result = _validator.Validate("hello", rs);
            Assert.Contains("l1", result.PassedRuleIds);
            Assert.Contains("l2", result.PassedRuleIds);
        }
    }
}
