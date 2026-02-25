using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Prompt.Tests
{
    public class PromptTestSuiteTests
    {
        // ── TestAssertion Tests ──────────────────────────────

        [Fact]
        public void Assertion_Contains_Pass() =>
            Assert.True(new TestAssertion(AssertionType.Contains, "hello").Evaluate("Say Hello World"));

        [Fact]
        public void Assertion_Contains_Fail() =>
            Assert.False(new TestAssertion(AssertionType.Contains, "xyz").Evaluate("hello"));

        [Fact]
        public void Assertion_NotContains_Pass() =>
            Assert.True(new TestAssertion(AssertionType.NotContains, "xyz").Evaluate("hello"));

        [Fact]
        public void Assertion_NotContains_Fail() =>
            Assert.False(new TestAssertion(AssertionType.NotContains, "hello").Evaluate("hello world"));

        [Fact]
        public void Assertion_MatchesRegex_Pass() =>
            Assert.True(new TestAssertion(AssertionType.MatchesRegex, @"\d+").Evaluate("abc123"));

        [Fact]
        public void Assertion_MatchesRegex_Fail() =>
            Assert.False(new TestAssertion(AssertionType.MatchesRegex, @"^\d+$").Evaluate("abc"));

        [Fact]
        public void Assertion_MatchesRegex_InvalidPattern() =>
            Assert.False(new TestAssertion(AssertionType.MatchesRegex, "[invalid").Evaluate("test"));

        [Fact]
        public void Assertion_StartsWith_Pass() =>
            Assert.True(new TestAssertion(AssertionType.StartsWith, "hello").Evaluate("Hello World"));

        [Fact]
        public void Assertion_StartsWith_Fail() =>
            Assert.False(new TestAssertion(AssertionType.StartsWith, "world").Evaluate("Hello"));

        [Fact]
        public void Assertion_EndsWith_Pass() =>
            Assert.True(new TestAssertion(AssertionType.EndsWith, "world").Evaluate("Hello World"));

        [Fact]
        public void Assertion_EndsWith_Fail() =>
            Assert.False(new TestAssertion(AssertionType.EndsWith, "hello").Evaluate("Hello World"));

        [Fact]
        public void Assertion_HasMinLength_Pass() =>
            Assert.True(new TestAssertion(AssertionType.HasMinLength, "5").Evaluate("hello"));

        [Fact]
        public void Assertion_HasMinLength_Fail() =>
            Assert.False(new TestAssertion(AssertionType.HasMinLength, "10").Evaluate("hi"));

        [Fact]
        public void Assertion_HasMaxLength_Pass() =>
            Assert.True(new TestAssertion(AssertionType.HasMaxLength, "10").Evaluate("hello"));

        [Fact]
        public void Assertion_HasMaxLength_Fail() =>
            Assert.False(new TestAssertion(AssertionType.HasMaxLength, "2").Evaluate("hello"));

        [Fact]
        public void Assertion_ContainsJson_Pass() =>
            Assert.True(new TestAssertion(AssertionType.ContainsJson, "").Evaluate("Here is data: {\"a\":1}"));

        [Fact]
        public void Assertion_ContainsJson_Fail() =>
            Assert.False(new TestAssertion(AssertionType.ContainsJson, "").Evaluate("no json here"));

        [Fact]
        public void Assertion_ContainsCodeBlock_Pass() =>
            Assert.True(new TestAssertion(AssertionType.ContainsCodeBlock, "").Evaluate("Here:\n```\ncode\n```"));

        [Fact]
        public void Assertion_ContainsCodeBlock_Fail() =>
            Assert.False(new TestAssertion(AssertionType.ContainsCodeBlock, "").Evaluate("no code block"));

        [Fact]
        public void Assertion_ContainsAllOf_Pass() =>
            Assert.True(new TestAssertion(AssertionType.ContainsAllOf, "hello,world").Evaluate("Hello World"));

        [Fact]
        public void Assertion_ContainsAllOf_Fail() =>
            Assert.False(new TestAssertion(AssertionType.ContainsAllOf, "hello,xyz").Evaluate("Hello World"));

        [Fact]
        public void Assertion_Negate_Inverts() =>
            Assert.False(new TestAssertion(AssertionType.Contains, "hello", negate: true).Evaluate("hello world"));

        [Fact]
        public void Assertion_Negate_InvertsFail() =>
            Assert.True(new TestAssertion(AssertionType.Contains, "xyz", negate: true).Evaluate("hello"));

        [Fact]
        public void Assertion_EmptyResponse() =>
            Assert.False(new TestAssertion(AssertionType.Contains, "x").Evaluate(""));

        [Fact]
        public void Assertion_NullResponse() =>
            Assert.False(new TestAssertion(AssertionType.Contains, "x").Evaluate(null!));

        [Fact]
        public void Assertion_ContainsAllOf_EmptyValue() =>
            Assert.True(new TestAssertion(AssertionType.ContainsAllOf, "").Evaluate("anything"));

        // ── PromptTestCase Tests ─────────────────────────────

        [Fact]
        public void TestCase_Create_SetsName()
        {
            var tc = PromptTestCase.Create("test1");
            Assert.Equal("test1", tc.Name);
        }

        [Fact]
        public void TestCase_Create_EmptyName_Throws() =>
            Assert.Throws<ArgumentException>(() => PromptTestCase.Create(""));

        [Fact]
        public void TestCase_Create_NullName_Throws() =>
            Assert.Throws<ArgumentException>(() => PromptTestCase.Create(null!));

        [Fact]
        public void TestCase_Create_TooLongName_Throws() =>
            Assert.Throws<ArgumentException>(() => PromptTestCase.Create(new string('a', 101)));

        [Fact]
        public void TestCase_WithPrompt_Null_Throws() =>
            Assert.Throws<ArgumentNullException>(() => PromptTestCase.Create("t").WithPrompt(null!));

        [Fact]
        public void TestCase_Builder_Fluent()
        {
            var tc = PromptTestCase.Create("t")
                .WithDescription("desc")
                .WithPrompt("prompt")
                .WithVariable("k", "v")
                .ExpectContains("x")
                .ExpectNotContains("y")
                .ExpectMatchesRegex("z")
                .ExpectStartsWith("a")
                .ExpectEndsWith("b")
                .ExpectMinLength(1)
                .ExpectMaxLength(100)
                .ExpectContainsJson()
                .ExpectContainsCodeBlock()
                .InCategory("cat")
                .WithTimeout(TimeSpan.FromSeconds(5));

            Assert.Equal("desc", tc.Description);
            Assert.Equal("prompt", tc.PromptText);
            Assert.Equal("v", tc.Variables["k"]);
            Assert.Equal(9, tc.Assertions.Count);
            Assert.Equal("cat", tc.Category);
            Assert.Equal(TimeSpan.FromSeconds(5), tc.Timeout);
        }

        [Fact]
        public void TestCase_ExpectContainsAllOf()
        {
            var tc = PromptTestCase.Create("t").ExpectContainsAllOf("a", "b", "c");
            Assert.Single(tc.Assertions);
            Assert.Equal("a,b,c", tc.Assertions[0].Value);
        }

        [Fact]
        public void TestCase_VariableSubstitution()
        {
            var tc = PromptTestCase.Create("t")
                .WithPrompt("Hello {{name}}, you are {{age}}")
                .WithVariable("name", "Alice")
                .WithVariable("age", "30");
            Assert.Equal("Hello Alice, you are 30", tc.GetResolvedPrompt());
        }

        [Fact]
        public void TestCase_MaxAssertions_Throws()
        {
            var tc = PromptTestCase.Create("t");
            for (int i = 0; i < 20; i++) tc.ExpectContains($"v{i}");
            Assert.Throws<InvalidOperationException>(() => tc.ExpectContains("extra"));
        }

        // ── PromptTestSuite CRUD Tests ───────────────────────

        [Fact]
        public void Suite_Constructor_EmptyName_Throws() =>
            Assert.Throws<ArgumentException>(() => new PromptTestSuite(""));

        [Fact]
        public void Suite_AddAndGet()
        {
            var suite = new PromptTestSuite("s");
            var tc = PromptTestCase.Create("t1").WithPrompt("p");
            suite.AddTestCase(tc);
            Assert.Equal(1, suite.TestCount);
            Assert.Same(tc, suite.GetTestCase("t1"));
        }

        [Fact]
        public void Suite_AddDuplicate_Throws()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p"));
            Assert.Throws<InvalidOperationException>(() =>
                suite.AddTestCase(PromptTestCase.Create("T1").WithPrompt("p")));
        }

        [Fact]
        public void Suite_AddNull_Throws()
        {
            var suite = new PromptTestSuite("s");
            Assert.Throws<ArgumentNullException>(() => suite.AddTestCase(null!));
        }

        [Fact]
        public void Suite_Remove()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p"));
            Assert.True(suite.RemoveTestCase("t1"));
            Assert.Equal(0, suite.TestCount);
        }

        [Fact]
        public void Suite_RemoveNonexistent() =>
            Assert.False(new PromptTestSuite("s").RemoveTestCase("nope"));

        [Fact]
        public void Suite_GetNonexistent() =>
            Assert.Null(new PromptTestSuite("s").GetTestCase("nope"));

        [Fact]
        public void Suite_GetByCategory()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").InCategory("A"));
            suite.AddTestCase(PromptTestCase.Create("t2").WithPrompt("p").InCategory("B"));
            suite.AddTestCase(PromptTestCase.Create("t3").WithPrompt("p").InCategory("a"));
            Assert.Equal(2, suite.GetByCategory("A").Count);
        }

        [Fact]
        public void Suite_GetCategories()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").InCategory("X"));
            suite.AddTestCase(PromptTestCase.Create("t2").WithPrompt("p").InCategory("Y"));
            suite.AddTestCase(PromptTestCase.Create("t3").WithPrompt("p"));
            var cats = suite.GetCategories();
            Assert.Equal(2, cats.Count);
        }

        [Fact]
        public void Suite_MaxTestCases_Throws()
        {
            var suite = new PromptTestSuite("s");
            for (int i = 0; i < 200; i++)
                suite.AddTestCase(PromptTestCase.Create($"t{i}").WithPrompt("p"));
            Assert.Throws<InvalidOperationException>(() =>
                suite.AddTestCase(PromptTestCase.Create("extra").WithPrompt("p")));
        }

        // ── Run Tests ────────────────────────────────────────

        [Fact]
        public void Run_CallsProviderWithResolvedPrompt()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("Hello {{x}}").WithVariable("x", "World"));
            string? captured = null;
            suite.Run(p => { captured = p; return "ok"; });
            Assert.Equal("Hello World", captured);
        }

        [Fact]
        public void Run_AssertionsPassed()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p").ExpectContains("hello"));
            var result = suite.Run(_ => "hello world");
            Assert.True(result.AllPassed);
            Assert.Equal(1, result.PassedTests);
        }

        [Fact]
        public void Run_AssertionsFailed()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p").ExpectContains("xyz"));
            var result = suite.Run(_ => "hello");
            Assert.False(result.AllPassed);
            Assert.Equal(1, result.FailedTests);
        }

        [Fact]
        public void Run_ProviderException()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p").ExpectContains("x"));
            var result = suite.Run(_ => throw new InvalidOperationException("boom"));
            Assert.False(result.AllPassed);
            Assert.Equal("boom", result.Results[0].ErrorMessage);
        }

        [Fact]
        public void Run_EmptySuite()
        {
            var result = new PromptTestSuite("s").Run(_ => "x");
            Assert.True(result.AllPassed);
            Assert.Equal(0, result.TotalTests);
            Assert.Equal(1.0, result.PassRate);
        }

        [Fact]
        public void Run_NoAssertions_Passes()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p"));
            var result = suite.Run(_ => "anything");
            Assert.True(result.AllPassed);
        }

        [Fact]
        public void Run_EmptyResponse()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p").ExpectMaxLength(0));
            var result = suite.Run(_ => "");
            Assert.True(result.AllPassed);
        }

        [Fact]
        public void Run_VeryLongResponse()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p").ExpectMinLength(1000));
            var result = suite.Run(_ => new string('a', 5000));
            Assert.True(result.AllPassed);
        }

        // ── RunSingle Tests ─────────────────────────────────

        [Fact]
        public void RunSingle_ByName()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").ExpectContains("ok"));
            suite.AddTestCase(PromptTestCase.Create("t2").WithPrompt("p"));
            var r = suite.RunSingle("t1", _ => "ok");
            Assert.True(r.Passed);
            Assert.Equal("t1", r.TestName);
        }

        [Fact]
        public void RunSingle_Nonexistent_Throws()
        {
            var suite = new PromptTestSuite("s");
            Assert.Throws<KeyNotFoundException>(() => suite.RunSingle("nope", _ => "x"));
        }

        // ── TestSuiteResult Tests ────────────────────────────

        [Fact]
        public void SuiteResult_PassRate()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").ExpectContains("a"));
            suite.AddTestCase(PromptTestCase.Create("t2").WithPrompt("p").ExpectContains("b"));
            var result = suite.Run(_ => "a");
            Assert.Equal(0.5, result.PassRate);
        }

        [Fact]
        public void SuiteResult_GetFailed()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").ExpectContains("x"));
            suite.AddTestCase(PromptTestCase.Create("t2").WithPrompt("p"));
            var result = suite.Run(_ => "y");
            Assert.Single(result.GetFailed());
            Assert.Single(result.GetPassed());
        }

        [Fact]
        public void SuiteResult_GetByCategory()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").InCategory("A"));
            suite.AddTestCase(PromptTestCase.Create("t2").WithPrompt("p").InCategory("B"));
            var result = suite.Run(_ => "x");
            Assert.Single(result.GetByCategory("A"));
        }

        [Fact]
        public void SuiteResult_GenerateReport_ContainsInfo()
        {
            var suite = new PromptTestSuite("MySuite");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").ExpectContains("ok"));
            var result = suite.Run(_ => "ok");
            var report = result.GenerateReport();
            Assert.Contains("MySuite", report);
            Assert.Contains("PASS", report);
            Assert.Contains("t1", report);
        }

        [Fact]
        public void SuiteResult_GenerateReport_ShowsFailures()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t1").WithPrompt("p").ExpectContains("xyz"));
            var result = suite.Run(_ => "abc");
            var report = result.GenerateReport();
            Assert.Contains("FAIL", report);
        }

        [Fact]
        public void SuiteResult_AllPassed_True()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p"));
            Assert.True(suite.Run(_ => "x").AllPassed);
        }

        [Fact]
        public void SuiteResult_ToJson()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p").ExpectContains("x"));
            var json = suite.Run(_ => "x").ToJson();
            Assert.Contains("\"suiteName\"", json);
            Assert.Contains("\"allPassed\": true", json);
        }

        // ── JSON Serialization Tests ─────────────────────────

        [Fact]
        public void Suite_ToJson_FromJson_Roundtrip()
        {
            var suite = new PromptTestSuite("MySuite");
            suite.AddTestCase(PromptTestCase.Create("t1")
                .WithDescription("desc")
                .WithPrompt("prompt {{v}}")
                .WithVariable("v", "val")
                .ExpectContains("x")
                .ExpectMatchesRegex(@"\d+")
                .InCategory("cat")
                .WithTimeout(TimeSpan.FromSeconds(10)));
            suite.AddTestCase(PromptTestCase.Create("t2").WithPrompt("p2"));

            var json = suite.ToJson();
            var restored = PromptTestSuite.FromJson(json);

            Assert.Equal("MySuite", restored.Name);
            Assert.Equal(2, restored.TestCount);
            var tc1 = restored.GetTestCase("t1")!;
            Assert.Equal("desc", tc1.Description);
            Assert.Equal("prompt {{v}}", tc1.PromptText);
            Assert.Equal("val", tc1.Variables["v"]);
            Assert.Equal(2, tc1.Assertions.Count);
            Assert.Equal("cat", tc1.Category);
            Assert.NotNull(tc1.Timeout);
        }

        [Fact]
        public void Suite_FromJson_InvalidJson_Throws()
        {
            Assert.ThrowsAny<Exception>(() => PromptTestSuite.FromJson("not json"));
        }

        // ── File I/O Tests ──────────────────────────────────

        [Fact]
        public async Task Suite_SaveAndLoad_Roundtrip()
        {
            var path = Path.GetTempFileName();
            try
            {
                var suite = new PromptTestSuite("FileSuite");
                suite.AddTestCase(PromptTestCase.Create("ft").WithPrompt("fp").ExpectContains("x"));
                await suite.SaveToFileAsync(path);
                var loaded = await PromptTestSuite.LoadFromFileAsync(path);
                Assert.Equal("FileSuite", loaded.Name);
                Assert.Equal(1, loaded.TestCount);
            }
            finally { File.Delete(path); }
        }

        // ── Edge Case Tests ─────────────────────────────────

        [Fact]
        public void Assertion_ContainsJson_ArrayJson() =>
            Assert.True(new TestAssertion(AssertionType.ContainsJson, "").Evaluate("data: [1,2,3]"));

        [Fact]
        public void Assertion_HasMinLength_InvalidValue() =>
            Assert.False(new TestAssertion(AssertionType.HasMinLength, "abc").Evaluate("hello"));

        [Fact]
        public void Run_NullProvider_Throws()
        {
            var suite = new PromptTestSuite("s");
            Assert.Throws<ArgumentNullException>(() => suite.Run(null!));
        }

        [Fact]
        public void RunSingle_NullProvider_Throws()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p"));
            Assert.Throws<ArgumentNullException>(() => suite.RunSingle("t", null!));
        }

        [Fact]
        public void SuiteResult_Duration_Tracked()
        {
            var suite = new PromptTestSuite("s");
            suite.AddTestCase(PromptTestCase.Create("t").WithPrompt("p"));
            var result = suite.Run(_ => "x");
            Assert.True(result.TotalDuration.Ticks >= 0);
        }

        [Fact]
        public void TestCase_WithDescription_Null_SetsEmpty()
        {
            var tc = PromptTestCase.Create("t").WithDescription(null!);
            Assert.Equal("", tc.Description);
        }

        [Fact]
        public void Assertion_StartsWith_EmptyValue() =>
            Assert.True(new TestAssertion(AssertionType.StartsWith, "").Evaluate("anything"));
    }
}
