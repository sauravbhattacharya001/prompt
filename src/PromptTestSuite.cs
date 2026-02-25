using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Prompt
{
    // ── Enums ────────────────────────────────────────────────

    public enum AssertionType
    {
        Contains,
        NotContains,
        MatchesRegex,
        StartsWith,
        EndsWith,
        HasMinLength,
        HasMaxLength,
        ContainsJson,
        ContainsCodeBlock,
        ContainsAllOf
    }

    // ── TestAssertion ────────────────────────────────────────

    public class TestAssertion
    {
        public AssertionType Type { get; }
        public string Value { get; }
        public bool Negate { get; }

        public TestAssertion(AssertionType type, string value, bool negate = false)
        {
            Type = type;
            Value = value ?? string.Empty;
            Negate = negate;
        }

        public bool Evaluate(string response)
        {
            response ??= string.Empty;
            bool result = Type switch
            {
                AssertionType.Contains => response.Contains(Value, StringComparison.OrdinalIgnoreCase),
                AssertionType.NotContains => !response.Contains(Value, StringComparison.OrdinalIgnoreCase),
                AssertionType.MatchesRegex => SafeRegexMatch(response, Value),
                AssertionType.StartsWith => response.StartsWith(Value, StringComparison.OrdinalIgnoreCase),
                AssertionType.EndsWith => response.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
                AssertionType.HasMinLength => int.TryParse(Value, out var min) && response.Length >= min,
                AssertionType.HasMaxLength => int.TryParse(Value, out var max) && response.Length <= max,
                AssertionType.ContainsJson => ContainsValidJson(response),
                AssertionType.ContainsCodeBlock => response.Contains("```"),
                AssertionType.ContainsAllOf => CheckContainsAllOf(response, Value),
                _ => false
            };
            return Negate ? !result : result;
        }

        private static bool SafeRegexMatch(string input, string pattern)
        {
            try { return Regex.IsMatch(input, pattern); }
            catch { return false; }
        }

        private static bool ContainsValidJson(string response)
        {
            // Try to find a JSON object or array in the response
            int braceStart = response.IndexOf('{');
            int bracketStart = response.IndexOf('[');
            var starts = new List<int>();
            if (braceStart >= 0) starts.Add(braceStart);
            if (bracketStart >= 0) starts.Add(bracketStart);
            foreach (var start in starts)
            {
                char open = response[start];
                char close = open == '{' ? '}' : ']';
                int lastClose = response.LastIndexOf(close);
                if (lastClose > start)
                {
                    var candidate = response.Substring(start, lastClose - start + 1);
                    try { JsonDocument.Parse(candidate); return true; } catch { }
                }
            }
            return false;
        }

        private static bool CheckContainsAllOf(string response, string csv)
        {
            if (string.IsNullOrEmpty(csv)) return true;
            var values = csv.Split(',');
            return values.All(v => response.Contains(v.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    // ── AssertionResult ──────────────────────────────────────

    public class AssertionResult
    {
        public TestAssertion Assertion { get; }
        public bool Passed { get; }
        public string Message { get; }

        public AssertionResult(TestAssertion assertion, bool passed, string message)
        {
            Assertion = assertion;
            Passed = passed;
            Message = message;
        }
    }

    // ── PromptTestCase ───────────────────────────────────────

    public class PromptTestCase
    {
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public string PromptText { get; private set; } = string.Empty;
        public Dictionary<string, string> Variables { get; } = new();
        public List<TestAssertion> Assertions { get; } = new();
        public string? Category { get; private set; }
        public TimeSpan? Timeout { get; private set; }

        private PromptTestCase() { }

        public static PromptTestCase Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Test case name cannot be empty.", nameof(name));
            if (name.Length > PromptTestSuite.MaxNameLength)
                throw new ArgumentException($"Name exceeds {PromptTestSuite.MaxNameLength} characters.", nameof(name));
            return new PromptTestCase { Name = name };
        }

        public PromptTestCase WithDescription(string desc) { Description = desc ?? string.Empty; return this; }
        public PromptTestCase WithPrompt(string prompt)
        {
            PromptText = prompt ?? throw new ArgumentNullException(nameof(prompt));
            return this;
        }
        public PromptTestCase WithVariable(string key, string value) { Variables[key] = value; return this; }
        public PromptTestCase ExpectContains(string text) => AddAssertion(AssertionType.Contains, text);
        public PromptTestCase ExpectNotContains(string text) => AddAssertion(AssertionType.NotContains, text);
        public PromptTestCase ExpectMatchesRegex(string pattern) => AddAssertion(AssertionType.MatchesRegex, pattern);
        public PromptTestCase ExpectStartsWith(string text) => AddAssertion(AssertionType.StartsWith, text);
        public PromptTestCase ExpectEndsWith(string text) => AddAssertion(AssertionType.EndsWith, text);
        public PromptTestCase ExpectMinLength(int length) => AddAssertion(AssertionType.HasMinLength, length.ToString());
        public PromptTestCase ExpectMaxLength(int length) => AddAssertion(AssertionType.HasMaxLength, length.ToString());
        public PromptTestCase ExpectContainsJson() => AddAssertion(AssertionType.ContainsJson, "");
        public PromptTestCase ExpectContainsCodeBlock() => AddAssertion(AssertionType.ContainsCodeBlock, "");
        public PromptTestCase ExpectContainsAllOf(params string[] values) => AddAssertion(AssertionType.ContainsAllOf, string.Join(",", values));
        public PromptTestCase InCategory(string category) { Category = category; return this; }
        public PromptTestCase WithTimeout(TimeSpan timeout) { Timeout = timeout; return this; }

        private PromptTestCase AddAssertion(AssertionType type, string value)
        {
            if (Assertions.Count >= PromptTestSuite.MaxAssertionsPerTest)
                throw new InvalidOperationException($"Cannot exceed {PromptTestSuite.MaxAssertionsPerTest} assertions per test.");
            Assertions.Add(new TestAssertion(type, value));
            return this;
        }

        internal string GetResolvedPrompt()
        {
            var prompt = PromptText;
            foreach (var kvp in Variables)
                prompt = prompt.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            return prompt;
        }
    }

    // ── TestCaseResult ───────────────────────────────────────

    public class TestCaseResult
    {
        public string TestName { get; }
        public bool Passed { get; }
        public string Response { get; }
        public List<AssertionResult> AssertionResults { get; }
        public TimeSpan Duration { get; }
        public string? ErrorMessage { get; }
        public string? Category { get; }

        public TestCaseResult(string testName, bool passed, string response,
            List<AssertionResult> assertionResults, TimeSpan duration, string? errorMessage = null, string? category = null)
        {
            TestName = testName;
            Passed = passed;
            Response = response;
            AssertionResults = assertionResults;
            Duration = duration;
            ErrorMessage = errorMessage;
            Category = category;
        }
    }

    // ── TestSuiteResult ──────────────────────────────────────

    public class TestSuiteResult
    {
        public string SuiteName { get; }
        public List<TestCaseResult> Results { get; }
        public int TotalTests => Results.Count;
        public int PassedTests => Results.Count(r => r.Passed);
        public int FailedTests => Results.Count(r => !r.Passed);
        public double PassRate => TotalTests == 0 ? 1.0 : (double)PassedTests / TotalTests;
        public TimeSpan TotalDuration => TimeSpan.FromTicks(Results.Sum(r => r.Duration.Ticks));
        public bool AllPassed => Results.All(r => r.Passed);

        public TestSuiteResult(string suiteName, List<TestCaseResult> results)
        {
            SuiteName = suiteName;
            Results = results;
        }

        public List<TestCaseResult> GetFailed() => Results.Where(r => !r.Passed).ToList();
        public List<TestCaseResult> GetPassed() => Results.Where(r => r.Passed).ToList();
        public List<TestCaseResult> GetByCategory(string category) =>
            Results.Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

        public string GenerateReport()
        {
            var lines = new List<string>
            {
                $"Test Suite: {SuiteName}",
                $"Total: {TotalTests} | Passed: {PassedTests} | Failed: {FailedTests} | Pass Rate: {PassRate:P1}",
                $"Duration: {TotalDuration.TotalMilliseconds:F0}ms",
                ""
            };
            foreach (var r in Results)
            {
                var status = r.Passed ? "PASS" : "FAIL";
                lines.Add($"[{status}] {r.TestName} ({r.Duration.TotalMilliseconds:F0}ms)");
                if (!r.Passed)
                {
                    if (r.ErrorMessage != null)
                        lines.Add($"  Error: {r.ErrorMessage}");
                    foreach (var a in r.AssertionResults.Where(a => !a.Passed))
                        lines.Add($"  - {a.Message}");
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        public string ToJson()
        {
            var data = new
            {
                suiteName = SuiteName,
                totalTests = TotalTests,
                passedTests = PassedTests,
                failedTests = FailedTests,
                passRate = PassRate,
                totalDurationMs = TotalDuration.TotalMilliseconds,
                allPassed = AllPassed,
                results = Results.Select(r => new
                {
                    testName = r.TestName,
                    passed = r.Passed,
                    response = r.Response,
                    durationMs = r.Duration.TotalMilliseconds,
                    errorMessage = r.ErrorMessage,
                    category = r.Category,
                    assertions = r.AssertionResults.Select(a => new
                    {
                        type = a.Assertion.Type.ToString(),
                        value = a.Assertion.Value,
                        passed = a.Passed,
                        message = a.Message
                    })
                })
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    // ── PromptTestSuite ──────────────────────────────────────

    public class PromptTestSuite
    {
        public const int MaxTestCases = 200;
        public const int MaxAssertionsPerTest = 20;
        public const int MaxNameLength = 100;

        public string Name { get; }
        public List<PromptTestCase> TestCases { get; } = new();
        public int TestCount => TestCases.Count;

        public PromptTestSuite(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Suite name cannot be empty.", nameof(name));
            Name = name;
        }

        public void AddTestCase(PromptTestCase testCase)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));
            if (TestCases.Any(t => string.Equals(t.Name, testCase.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Test case '{testCase.Name}' already exists.");
            if (TestCases.Count >= MaxTestCases)
                throw new InvalidOperationException($"Cannot exceed {MaxTestCases} test cases.");
            TestCases.Add(testCase);
        }

        public bool RemoveTestCase(string name) =>
            TestCases.RemoveAll(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;

        public PromptTestCase? GetTestCase(string name) =>
            TestCases.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        public List<PromptTestCase> GetByCategory(string category) =>
            TestCases.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

        public List<string> GetCategories() =>
            TestCases.Where(t => t.Category != null).Select(t => t.Category!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public TestSuiteResult Run(Func<string, string> responseProvider)
        {
            if (responseProvider == null) throw new ArgumentNullException(nameof(responseProvider));
            var results = new List<TestCaseResult>();
            foreach (var tc in TestCases)
                results.Add(RunTestCase(tc, responseProvider));
            return new TestSuiteResult(Name, results);
        }

        public TestCaseResult RunSingle(string testName, Func<string, string> responseProvider)
        {
            if (responseProvider == null) throw new ArgumentNullException(nameof(responseProvider));
            var tc = GetTestCase(testName)
                ?? throw new KeyNotFoundException($"Test case '{testName}' not found.");
            return RunTestCase(tc, responseProvider);
        }

        private static TestCaseResult RunTestCase(PromptTestCase tc, Func<string, string> responseProvider)
        {
            var sw = Stopwatch.StartNew();
            string response;
            try
            {
                response = responseProvider(tc.GetResolvedPrompt()) ?? string.Empty;
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new TestCaseResult(tc.Name, false, string.Empty, new List<AssertionResult>(), sw.Elapsed, ex.Message, tc.Category);
            }

            var assertionResults = new List<AssertionResult>();
            foreach (var assertion in tc.Assertions)
            {
                bool passed = assertion.Evaluate(response);
                string neg = assertion.Negate ? " (negated)" : "";
                string msg = passed
                    ? $"PASS: {assertion.Type}{neg} '{assertion.Value}'"
                    : $"FAIL: {assertion.Type}{neg} '{assertion.Value}'";
                assertionResults.Add(new AssertionResult(assertion, passed, msg));
            }
            sw.Stop();
            bool allPassed = assertionResults.All(a => a.Passed);
            return new TestCaseResult(tc.Name, allPassed, response, assertionResults, sw.Elapsed, category: tc.Category);
        }

        // ── Serialization ───────────────────────────────────

        public string ToJson(bool indented = true)
        {
            var data = new SuiteData
            {
                Name = Name,
                TestCases = TestCases.Select(tc => new TestCaseData
                {
                    Name = tc.Name,
                    Description = tc.Description,
                    PromptText = tc.PromptText,
                    Variables = tc.Variables.Count > 0 ? new Dictionary<string, string>(tc.Variables) : null,
                    Assertions = tc.Assertions.Select(a => new AssertionData
                    {
                        Type = a.Type.ToString(),
                        Value = a.Value,
                        Negate = a.Negate
                    }).ToList(),
                    Category = tc.Category,
                    TimeoutMs = tc.Timeout?.TotalMilliseconds
                }).ToList()
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        public static PromptTestSuite FromJson(string json)
        {
            SerializationGuards.ThrowIfPayloadTooLarge(json);
            var data = JsonSerializer.Deserialize<SuiteData>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }) ?? throw new JsonException("Failed to deserialize PromptTestSuite.");

            var suite = new PromptTestSuite(data.Name ?? "Unnamed");
            if (data.TestCases != null)
            {
                foreach (var tcd in data.TestCases)
                {
                    var tc = PromptTestCase.Create(tcd.Name ?? "Unnamed")
                        .WithDescription(tcd.Description ?? "")
                        .WithPrompt(tcd.PromptText ?? "");
                    if (tcd.Variables != null)
                        foreach (var kvp in tcd.Variables)
                            tc.WithVariable(kvp.Key, kvp.Value);
                    if (tcd.Category != null)
                        tc.InCategory(tcd.Category);
                    if (tcd.TimeoutMs.HasValue)
                        tc.WithTimeout(TimeSpan.FromMilliseconds(tcd.TimeoutMs.Value));
                    if (tcd.Assertions != null)
                        foreach (var ad in tcd.Assertions)
                        {
                            if (Enum.TryParse<AssertionType>(ad.Type, out var aType))
                                tc.Assertions.Add(new TestAssertion(aType, ad.Value ?? "", ad.Negate));
                        }
                    suite.AddTestCase(tc);
                }
            }
            return suite;
        }

        public async Task SaveToFileAsync(string path)
        {
            var json = ToJson();
            await File.WriteAllTextAsync(path, json);
        }

        public static async Task<PromptTestSuite> LoadFromFileAsync(string path)
        {
            SerializationGuards.ThrowIfFileTooLarge(path);
            var json = await File.ReadAllTextAsync(path);
            return FromJson(json);
        }

        // ── Serialization DTOs ──────────────────────────────

        private class SuiteData
        {
            public string? Name { get; set; }
            public List<TestCaseData>? TestCases { get; set; }
        }

        private class TestCaseData
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? PromptText { get; set; }
            public Dictionary<string, string>? Variables { get; set; }
            public List<AssertionData>? Assertions { get; set; }
            public string? Category { get; set; }
            public double? TimeoutMs { get; set; }
        }

        private class AssertionData
        {
            public string? Type { get; set; }
            public string? Value { get; set; }
            public bool Negate { get; set; }
        }
    }
}
