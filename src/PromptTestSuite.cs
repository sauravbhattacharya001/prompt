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

    /// <summary>
    /// Defines the type of assertion to evaluate against a prompt response.
    /// </summary>
    public enum AssertionType
    {
        /// <summary>Response contains the expected text (case-insensitive).</summary>
        Contains,
        /// <summary>Response does not contain the expected text (case-insensitive).</summary>
        NotContains,
        /// <summary>Response matches the given regular expression pattern.</summary>
        MatchesRegex,
        /// <summary>Response starts with the expected text (case-insensitive).</summary>
        StartsWith,
        /// <summary>Response ends with the expected text (case-insensitive).</summary>
        EndsWith,
        /// <summary>Response has at least the specified minimum character length.</summary>
        HasMinLength,
        /// <summary>Response has at most the specified maximum character length.</summary>
        HasMaxLength,
        /// <summary>Response contains a valid JSON object or array.</summary>
        ContainsJson,
        /// <summary>Response contains a fenced code block (triple backticks).</summary>
        ContainsCodeBlock,
        /// <summary>Response contains all of the specified comma-separated values.</summary>
        ContainsAllOf
    }

    // ── TestAssertion ────────────────────────────────────────

    /// <summary>
    /// A single assertion that evaluates whether a response meets an expected condition.
    /// Supports negation to invert the assertion logic.
    /// </summary>
    public class TestAssertion
    {
        /// <summary>The type of assertion to evaluate.</summary>
        public AssertionType Type { get; }
        /// <summary>The expected value or pattern to match against.</summary>
        public string Value { get; }
        /// <summary>When true, the assertion result is inverted.</summary>
        public bool Negate { get; }

        /// <summary>
        /// Creates a new test assertion.
        /// </summary>
        /// <param name="type">The assertion type.</param>
        /// <param name="value">The value or pattern to evaluate against.</param>
        /// <param name="negate">Whether to invert the assertion result.</param>
        public TestAssertion(AssertionType type, string value, bool negate = false)
        {
            Type = type;
            Value = value ?? string.Empty;
            Negate = negate;
        }

        /// <summary>
        /// Evaluates this assertion against the given response text.
        /// </summary>
        /// <param name="response">The response text to evaluate.</param>
        /// <returns>True if the assertion passes (respecting negation).</returns>
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
            try { return Regex.IsMatch(input, pattern, RegexOptions.None, TimeSpan.FromSeconds(2)); }
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

    /// <summary>
    /// The result of evaluating a single <see cref="TestAssertion"/> against a response.
    /// </summary>
    public class AssertionResult
    {
        /// <summary>The assertion that was evaluated.</summary>
        public TestAssertion Assertion { get; }
        /// <summary>Whether the assertion passed.</summary>
        public bool Passed { get; }
        /// <summary>A human-readable description of the result.</summary>
        public string Message { get; }

        public AssertionResult(TestAssertion assertion, bool passed, string message)
        {
            Assertion = assertion;
            Passed = passed;
            Message = message;
        }
    }

    // ── PromptTestCase ───────────────────────────────────────

    /// <summary>
    /// Defines a single test case for evaluating prompt responses.
    /// Uses a builder pattern for fluent configuration of prompt text,
    /// variables, assertions, category, and timeout.
    /// </summary>
    public class PromptTestCase
    {
        /// <summary>The unique name identifying this test case.</summary>
        public string Name { get; private set; } = string.Empty;
        /// <summary>An optional description of what this test validates.</summary>
        public string Description { get; private set; } = string.Empty;
        /// <summary>The prompt text template (may contain {{variable}} placeholders).</summary>
        public string PromptText { get; private set; } = string.Empty;
        /// <summary>Variable substitutions for the prompt template.</summary>
        public Dictionary<string, string> Variables { get; } = new();
        /// <summary>The list of assertions to evaluate against the response.</summary>
        public List<TestAssertion> Assertions { get; } = new();
        /// <summary>An optional category for grouping test cases.</summary>
        public string? Category { get; private set; }
        /// <summary>An optional timeout for response generation.</summary>
        public TimeSpan? Timeout { get; private set; }

        private PromptTestCase() { }

        /// <summary>
        /// Creates a new test case with the given name.
        /// </summary>
        /// <param name="name">The unique name for this test case (max <see cref="PromptTestSuite.MaxNameLength"/> characters).</param>
        /// <returns>A new <see cref="PromptTestCase"/> instance.</returns>
        /// <exception cref="ArgumentException">If name is empty or exceeds the maximum length.</exception>
        public static PromptTestCase Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Test case name cannot be empty.", nameof(name));
            if (name.Length > PromptTestSuite.MaxNameLength)
                throw new ArgumentException($"Name exceeds {PromptTestSuite.MaxNameLength} characters.", nameof(name));
            return new PromptTestCase { Name = name };
        }

        /// <summary>Sets the description for this test case.</summary>
        public PromptTestCase WithDescription(string desc) { Description = desc ?? string.Empty; return this; }
        /// <summary>Sets the prompt text template for this test case.</summary>
        /// <exception cref="ArgumentNullException">If prompt is null.</exception>
        public PromptTestCase WithPrompt(string prompt)
        {
            PromptText = prompt ?? throw new ArgumentNullException(nameof(prompt));
            return this;
        }
        /// <summary>Adds a variable substitution for {{key}} placeholders in the prompt.</summary>
        public PromptTestCase WithVariable(string key, string value) { Variables[key] = value; return this; }
        /// <summary>Asserts the response contains the given text.</summary>
        public PromptTestCase ExpectContains(string text) => AddAssertion(AssertionType.Contains, text);
        /// <summary>Asserts the response does not contain the given text.</summary>
        public PromptTestCase ExpectNotContains(string text) => AddAssertion(AssertionType.NotContains, text);
        /// <summary>Asserts the response matches the given regex pattern.</summary>
        public PromptTestCase ExpectMatchesRegex(string pattern) => AddAssertion(AssertionType.MatchesRegex, pattern);
        /// <summary>Asserts the response starts with the given text.</summary>
        public PromptTestCase ExpectStartsWith(string text) => AddAssertion(AssertionType.StartsWith, text);
        /// <summary>Asserts the response ends with the given text.</summary>
        public PromptTestCase ExpectEndsWith(string text) => AddAssertion(AssertionType.EndsWith, text);
        /// <summary>Asserts the response has at least the given character count.</summary>
        public PromptTestCase ExpectMinLength(int length) => AddAssertion(AssertionType.HasMinLength, length.ToString());
        /// <summary>Asserts the response has at most the given character count.</summary>
        public PromptTestCase ExpectMaxLength(int length) => AddAssertion(AssertionType.HasMaxLength, length.ToString());
        /// <summary>Asserts the response contains valid JSON.</summary>
        public PromptTestCase ExpectContainsJson() => AddAssertion(AssertionType.ContainsJson, "");
        /// <summary>Asserts the response contains a fenced code block.</summary>
        public PromptTestCase ExpectContainsCodeBlock() => AddAssertion(AssertionType.ContainsCodeBlock, "");
        /// <summary>Asserts the response contains all of the specified values.</summary>
        public PromptTestCase ExpectContainsAllOf(params string[] values) => AddAssertion(AssertionType.ContainsAllOf, string.Join(",", values));
        /// <summary>Sets the category for this test case.</summary>
        public PromptTestCase InCategory(string category) { Category = category; return this; }
        /// <summary>Sets the timeout for response generation.</summary>
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

    /// <summary>
    /// The result of running a single <see cref="PromptTestCase"/>, including
    /// the response, individual assertion results, duration, and any error.
    /// </summary>
    public class TestCaseResult
    {
        /// <summary>The name of the test case that was run.</summary>
        public string TestName { get; }
        /// <summary>Whether all assertions passed.</summary>
        public bool Passed { get; }
        /// <summary>The response text returned by the response provider.</summary>
        public string Response { get; }
        /// <summary>Individual results for each assertion in the test case.</summary>
        public List<AssertionResult> AssertionResults { get; }
        /// <summary>How long the test case took to execute.</summary>
        public TimeSpan Duration { get; }
        /// <summary>Error message if the response provider threw an exception.</summary>
        public string? ErrorMessage { get; }
        /// <summary>The category of the test case, if any.</summary>
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

    /// <summary>
    /// Aggregated results from running all test cases in a <see cref="PromptTestSuite"/>.
    /// Provides pass/fail counts, pass rate, duration, and filtering by category.
    /// </summary>
    public class TestSuiteResult
    {
        /// <summary>The name of the test suite.</summary>
        public string SuiteName { get; }
        /// <summary>The list of individual test case results.</summary>
        public List<TestCaseResult> Results { get; }
        /// <summary>Total number of test cases that were run.</summary>
        public int TotalTests => Results.Count;
        /// <summary>Number of test cases that passed all assertions.</summary>
        public int PassedTests => Results.Count(r => r.Passed);
        /// <summary>Number of test cases with at least one failed assertion.</summary>
        public int FailedTests => Results.Count(r => !r.Passed);
        /// <summary>The ratio of passed tests to total tests (1.0 if no tests).</summary>
        public double PassRate => TotalTests == 0 ? 1.0 : (double)PassedTests / TotalTests;
        /// <summary>The combined duration of all test case executions.</summary>
        public TimeSpan TotalDuration => TimeSpan.FromTicks(Results.Sum(r => r.Duration.Ticks));
        /// <summary>Whether every test case passed.</summary>
        public bool AllPassed => Results.All(r => r.Passed);

        public TestSuiteResult(string suiteName, List<TestCaseResult> results)
        {
            SuiteName = suiteName;
            Results = results;
        }

        /// <summary>Returns all failed test case results.</summary>
        public List<TestCaseResult> GetFailed() => Results.Where(r => !r.Passed).ToList();
        /// <summary>Returns all passed test case results.</summary>
        public List<TestCaseResult> GetPassed() => Results.Where(r => r.Passed).ToList();
        /// <summary>Returns test case results matching the given category.</summary>
        public List<TestCaseResult> GetByCategory(string category) =>
            Results.Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// Generates a human-readable text report of all test results.
        /// </summary>
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

        /// <summary>
        /// Serializes the test results to a JSON string.
        /// </summary>
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
            return JsonSerializer.Serialize(data, SerializationGuards.WriteCamelCase);
        }
    }

    // ── PromptTestSuite ──────────────────────────────────────

    /// <summary>
    /// A named collection of <see cref="PromptTestCase"/> instances that can be run
    /// against a response provider function. Supports CRUD operations, categorization,
    /// serialization (JSON), and file I/O.
    /// </summary>
    public class PromptTestSuite
    {
        /// <summary>Maximum number of test cases allowed in a suite.</summary>
        public const int MaxTestCases = 200;
        /// <summary>Maximum number of assertions allowed per test case.</summary>
        public const int MaxAssertionsPerTest = 20;
        /// <summary>Maximum character length for test case and suite names.</summary>
        public const int MaxNameLength = 100;

        /// <summary>The name of this test suite.</summary>
        public string Name { get; }
        /// <summary>The list of test cases in this suite.</summary>
        public List<PromptTestCase> TestCases { get; } = new();
        /// <summary>The number of test cases in this suite.</summary>
        public int TestCount => TestCases.Count;

        /// <summary>
        /// Creates a new test suite with the given name.
        /// </summary>
        /// <param name="name">The suite name.</param>
        /// <exception cref="ArgumentException">If name is empty or whitespace.</exception>
        public PromptTestSuite(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Suite name cannot be empty.", nameof(name));
            Name = name;
        }

        /// <summary>
        /// Adds a test case to the suite. Names must be unique (case-insensitive).
        /// </summary>
        /// <exception cref="ArgumentNullException">If testCase is null.</exception>
        /// <exception cref="InvalidOperationException">If a test case with the same name exists or the suite is full.</exception>
        public void AddTestCase(PromptTestCase testCase)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));
            if (TestCases.Any(t => string.Equals(t.Name, testCase.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Test case '{testCase.Name}' already exists.");
            if (TestCases.Count >= MaxTestCases)
                throw new InvalidOperationException($"Cannot exceed {MaxTestCases} test cases.");
            TestCases.Add(testCase);
        }

        /// <summary>Removes a test case by name. Returns true if found and removed.</summary>
        public bool RemoveTestCase(string name) =>
            TestCases.RemoveAll(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;

        /// <summary>Gets a test case by name, or null if not found.</summary>
        public PromptTestCase? GetTestCase(string name) =>
            TestCases.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns all test cases in the given category.</summary>
        public List<PromptTestCase> GetByCategory(string category) =>
            TestCases.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>Returns a deduplicated list of all categories used across test cases.</summary>
        public List<string> GetCategories() =>
            TestCases.Where(t => t.Category != null).Select(t => t.Category!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>
        /// Runs all test cases using the provided response provider function.
        /// </summary>
        /// <param name="responseProvider">A function that takes a prompt string and returns a response string.</param>
        /// <returns>A <see cref="TestSuiteResult"/> with all results.</returns>
        public TestSuiteResult Run(Func<string, string> responseProvider)
        {
            if (responseProvider == null) throw new ArgumentNullException(nameof(responseProvider));
            var results = new List<TestCaseResult>();
            foreach (var tc in TestCases)
                results.Add(RunTestCase(tc, responseProvider));
            return new TestSuiteResult(Name, results);
        }

        /// <summary>
        /// Runs a single test case by name using the provided response provider.
        /// </summary>
        /// <param name="testName">The name of the test case to run.</param>
        /// <param name="responseProvider">A function that takes a prompt string and returns a response.</param>
        /// <returns>The result of running the specified test case.</returns>
        /// <exception cref="KeyNotFoundException">If no test case with the given name exists.</exception>
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

        /// <summary>
        /// Serializes this test suite to a JSON string.
        /// </summary>
        /// <param name="indented">Whether to indent the JSON output.</param>
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
            return JsonSerializer.Serialize(data, SerializationGuards.WriteOptions(indented));
        }

        /// <summary>
        /// Deserializes a test suite from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>A new <see cref="PromptTestSuite"/> populated from the JSON.</returns>
        /// <exception cref="JsonException">If deserialization fails.</exception>
        public static PromptTestSuite FromJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json);
            var data = JsonSerializer.Deserialize<SuiteData>(json, SerializationGuards.ReadCamelCase) ?? throw new JsonException("Failed to deserialize PromptTestSuite.");

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

        /// <summary>
        /// Saves this test suite to a JSON file.
        /// </summary>
        /// <param name="path">The file path to write to.</param>
        public async Task SaveToFileAsync(string path)
        {
            var json = ToJson();
            await File.WriteAllTextAsync(path, json);
        }

        /// <summary>
        /// Loads a test suite from a JSON file.
        /// </summary>
        /// <param name="path">The file path to read from.</param>
        /// <returns>A new <see cref="PromptTestSuite"/> populated from the file.</returns>
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
