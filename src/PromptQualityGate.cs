namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ── Gate Check Severity ──────────────────────────────────

    /// <summary>
    /// Severity levels for quality gate check results.
    /// </summary>
    public enum GateSeverity
    {
        /// <summary>Informational note — does not block.</summary>
        Info,
        /// <summary>Warning — may block depending on gate policy.</summary>
        Warning,
        /// <summary>Error — blocks unless gate policy is lenient.</summary>
        Error,
        /// <summary>Critical — always blocks the gate.</summary>
        Critical
    }

    // ── Gate Check Result ────────────────────────────────────

    /// <summary>
    /// Result of a single quality gate check.
    /// </summary>
    public class GateCheckResult
    {
        /// <summary>Name of the check that produced this result.</summary>
        public string CheckName { get; set; } = "";

        /// <summary>Whether this check passed.</summary>
        public bool Passed { get; set; }

        /// <summary>Severity of any issues found.</summary>
        public GateSeverity Severity { get; set; } = GateSeverity.Info;

        /// <summary>Human-readable message describing the result.</summary>
        public string Message { get; set; } = "";

        /// <summary>Optional details (e.g., line numbers, specific violations).</summary>
        public List<string> Details { get; set; } = new();

        /// <summary>Duration of this check in milliseconds.</summary>
        public double ElapsedMs { get; set; }
    }

    // ── Gate Verdict ─────────────────────────────────────────

    /// <summary>
    /// Overall verdict from the quality gate evaluation.
    /// </summary>
    public class GateVerdict
    {
        /// <summary>Whether the prompt passed all required checks.</summary>
        public bool Passed { get; set; }

        /// <summary>Summary message.</summary>
        public string Summary { get; set; } = "";

        /// <summary>Total number of checks run.</summary>
        public int TotalChecks { get; set; }

        /// <summary>Number of checks that passed.</summary>
        public int PassedChecks { get; set; }

        /// <summary>Number of checks that failed.</summary>
        public int FailedChecks { get; set; }

        /// <summary>Individual check results.</summary>
        public List<GateCheckResult> Results { get; set; } = new();

        /// <summary>Total evaluation time in milliseconds.</summary>
        public double TotalElapsedMs { get; set; }

        /// <summary>The gate policy that was applied.</summary>
        public GatePolicy Policy { get; set; } = GatePolicy.Standard;

        /// <summary>Timestamp when the evaluation was performed.</summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Serializes the verdict to JSON.
        /// </summary>
        public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    // ── Gate Policy ──────────────────────────────────────────

    /// <summary>
    /// How strict the quality gate should be when evaluating results.
    /// </summary>
    public enum GatePolicy
    {
        /// <summary>
        /// Only Critical-severity failures block the gate.
        /// </summary>
        Lenient,

        /// <summary>
        /// Error and Critical failures block the gate (default).
        /// </summary>
        Standard,

        /// <summary>
        /// Warning, Error, and Critical failures all block the gate.
        /// </summary>
        Strict
    }

    // ── Quality Gate Check (delegate) ────────────────────────

    /// <summary>
    /// A named quality check that evaluates a prompt string.
    /// </summary>
    public class QualityCheck
    {
        /// <summary>Unique name for this check.</summary>
        public string Name { get; }

        /// <summary>The function that performs the check.</summary>
        public Func<string, GateCheckResult> Evaluate { get; }

        /// <summary>Whether this check is enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Creates a new quality check.
        /// </summary>
        /// <param name="name">Unique name.</param>
        /// <param name="evaluate">Evaluation function.</param>
        public QualityCheck(string name, Func<string, GateCheckResult> evaluate)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Check name cannot be null or empty.", nameof(name));
            Name = name;
            Evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        }
    }

    // ── PromptQualityGate ────────────────────────────────────

    /// <summary>
    /// A configurable quality gate that runs a set of checks against a prompt
    /// and produces a pass/fail verdict. Combines length validation, token
    /// estimation, content safety, structural analysis, and custom rules
    /// into a single evaluation pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gate ships with built-in checks covering common prompt quality
    /// concerns. Users can add custom checks, disable built-in ones, and
    /// configure the gate policy (lenient/standard/strict) to control which
    /// severity levels block the gate.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var gate = new PromptQualityGate();
    /// var verdict = gate.Evaluate("You are a helpful assistant. Answer the question.");
    /// if (!verdict.Passed)
    /// {
    ///     foreach (var r in verdict.Results.Where(r => !r.Passed))
    ///         Console.WriteLine($"  [{r.Severity}] {r.CheckName}: {r.Message}");
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptQualityGate
    {
        private readonly List<QualityCheck> _checks = new();
        private readonly object _lock = new();

        /// <summary>Gets or sets the gate policy.</summary>
        public GatePolicy Policy { get; set; } = GatePolicy.Standard;

        /// <summary>Gets or sets the maximum allowed prompt length in characters.</summary>
        public int MaxLength { get; set; } = 100_000;

        /// <summary>Gets or sets the minimum prompt length in characters.</summary>
        public int MinLength { get; set; } = 5;

        /// <summary>Gets or sets the maximum estimated token count.</summary>
        public int MaxTokens { get; set; } = 32_000;

        /// <summary>Gets or sets the maximum line count before triggering a warning.</summary>
        public int MaxLines { get; set; } = 500;

        /// <summary>Gets or sets blocked phrases (case-insensitive).</summary>
        public List<string> BlockedPhrases { get; set; } = new();

        /// <summary>Gets or sets required phrases (case-insensitive) that must be present.</summary>
        public List<string> RequiredPhrases { get; set; } = new();

        /// <summary>
        /// Creates a new quality gate with the built-in checks.
        /// </summary>
        public PromptQualityGate()
        {
            RegisterBuiltInChecks();
        }

        /// <summary>
        /// Creates a new quality gate with a specific policy.
        /// </summary>
        public PromptQualityGate(GatePolicy policy) : this()
        {
            Policy = policy;
        }

        // ── Built-in checks ──────────────────────────────────

        private void RegisterBuiltInChecks()
        {
            _checks.Add(new QualityCheck("length", CheckLength));
            _checks.Add(new QualityCheck("token-estimate", CheckTokenEstimate));
            _checks.Add(new QualityCheck("empty-content", CheckEmptyContent));
            _checks.Add(new QualityCheck("structure", CheckStructure));
            _checks.Add(new QualityCheck("blocked-phrases", CheckBlockedPhrases));
            _checks.Add(new QualityCheck("required-phrases", CheckRequiredPhrases));
            _checks.Add(new QualityCheck("injection-patterns", CheckInjectionPatterns));
            _checks.Add(new QualityCheck("encoding", CheckEncoding));
            _checks.Add(new QualityCheck("repetition", CheckRepetition));
            _checks.Add(new QualityCheck("whitespace", CheckWhitespace));
        }

        private GateCheckResult CheckLength(string prompt)
        {
            var result = new GateCheckResult { CheckName = "length" };

            if (prompt.Length < MinLength)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Error;
                result.Message = $"Prompt too short ({prompt.Length} chars, minimum {MinLength}).";
            }
            else if (prompt.Length > MaxLength)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Critical;
                result.Message = $"Prompt exceeds maximum length ({prompt.Length:N0} chars, max {MaxLength:N0}).";
            }
            else
            {
                result.Passed = true;
                result.Message = $"Length OK ({prompt.Length:N0} chars).";
            }
            return result;
        }

        private GateCheckResult CheckTokenEstimate(string prompt)
        {
            var result = new GateCheckResult { CheckName = "token-estimate" };
            int estimatedTokens = (int)Math.Ceiling(prompt.Length / 4.0);

            if (estimatedTokens > MaxTokens)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Error;
                result.Message = $"Estimated token count ({estimatedTokens:N0}) exceeds limit ({MaxTokens:N0}).";
            }
            else if (estimatedTokens > MaxTokens * 0.8)
            {
                result.Passed = true;
                result.Severity = GateSeverity.Warning;
                result.Message = $"Token count ({estimatedTokens:N0}) approaching limit ({MaxTokens:N0}).";
            }
            else
            {
                result.Passed = true;
                result.Message = $"Token estimate OK ({estimatedTokens:N0}).";
            }
            return result;
        }

        private GateCheckResult CheckEmptyContent(string prompt)
        {
            var result = new GateCheckResult { CheckName = "empty-content" };
            var trimmed = prompt.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                result.Passed = false;
                result.Severity = GateSeverity.Critical;
                result.Message = "Prompt is empty or whitespace-only.";
            }
            else
            {
                result.Passed = true;
                result.Message = "Content present.";
            }
            return result;
        }

        private GateCheckResult CheckStructure(string prompt)
        {
            var result = new GateCheckResult { CheckName = "structure" };
            result.Passed = true;
            var details = new List<string>();

            var lines = prompt.Split('\n');
            if (lines.Length > MaxLines)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Warning;
                result.Message = $"Prompt has {lines.Length} lines (max recommended: {MaxLines}).";
                details.Add($"Line count: {lines.Length}");
            }

            int braces = 0, brackets = 0, parens = 0;
            foreach (char c in prompt)
            {
                switch (c)
                {
                    case '{': braces++; break;
                    case '}': braces--; break;
                    case '[': brackets++; break;
                    case ']': brackets--; break;
                    case '(': parens++; break;
                    case ')': parens--; break;
                }
            }

            if (braces != 0) details.Add($"Unbalanced braces: off by {Math.Abs(braces)}");
            if (brackets != 0) details.Add($"Unbalanced brackets: off by {Math.Abs(brackets)}");
            if (parens != 0) details.Add($"Unbalanced parentheses: off by {Math.Abs(parens)}");

            if (details.Count > 0 && (braces != 0 || brackets != 0 || parens != 0))
            {
                result.Severity = GateSeverity.Warning;
                result.Message = "Structural issues found.";
                result.Passed = Policy == GatePolicy.Lenient;
            }

            if (result.Passed && string.IsNullOrEmpty(result.Message))
                result.Message = "Structure OK.";
            result.Details = details;
            return result;
        }

        private GateCheckResult CheckBlockedPhrases(string prompt)
        {
            var result = new GateCheckResult { CheckName = "blocked-phrases" };
            if (BlockedPhrases.Count == 0)
            {
                result.Passed = true;
                result.Message = "No blocked phrases configured.";
                return result;
            }

            var found = BlockedPhrases
                .Where(bp => prompt.Contains(bp, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (found.Count > 0)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Critical;
                result.Message = $"Found {found.Count} blocked phrase(s).";
                result.Details = found.Select(f => $"Contains: \"{f}\"").ToList();
            }
            else
            {
                result.Passed = true;
                result.Message = "No blocked phrases found.";
            }
            return result;
        }

        private GateCheckResult CheckRequiredPhrases(string prompt)
        {
            var result = new GateCheckResult { CheckName = "required-phrases" };
            if (RequiredPhrases.Count == 0)
            {
                result.Passed = true;
                result.Message = "No required phrases configured.";
                return result;
            }

            var missing = RequiredPhrases
                .Where(rp => !prompt.Contains(rp, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (missing.Count > 0)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Error;
                result.Message = $"Missing {missing.Count} required phrase(s).";
                result.Details = missing.Select(m => $"Missing: \"{m}\"").ToList();
            }
            else
            {
                result.Passed = true;
                result.Message = "All required phrases present.";
            }
            return result;
        }

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

        private GateCheckResult CheckInjectionPatterns(string prompt)
        {
            var result = new GateCheckResult { CheckName = "injection-patterns" };
            var patterns = new (string pattern, string name)[]
            {
                (@"(?i)ignore\s+(all\s+)?previous\s+instructions", "ignore-instructions"),
                (@"(?i)disregard\s+(all\s+)?above", "disregard-above"),
                (@"(?i)you\s+are\s+now\s+(?:a|an)\s+\w+", "role-override"),
                (@"(?i)system\s*:\s*", "system-prefix-injection"),
                (@"(?i)<<\s*SYS\s*>>", "llama-system-tag"),
                (@"(?i)\[INST\]", "llama-inst-tag"),
            };

            var findings = new List<string>();
            foreach (var (pattern, name) in patterns)
            {
                try
                {
                    if (Regex.IsMatch(prompt, pattern, RegexOptions.None, RegexTimeout))
                        findings.Add(name);
                }
                catch (RegexMatchTimeoutException)
                {
                    findings.Add($"{name} (timeout)");
                }
            }

            if (findings.Count > 0)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Critical;
                result.Message = $"Potential prompt injection detected ({findings.Count} pattern(s)).";
                result.Details = findings.Select(f => $"Matched: {f}").ToList();
            }
            else
            {
                result.Passed = true;
                result.Message = "No injection patterns detected.";
            }
            return result;
        }

        private GateCheckResult CheckEncoding(string prompt)
        {
            var result = new GateCheckResult { CheckName = "encoding" };
            var details = new List<string>();

            int controlChars = 0;
            int nullBytes = 0;

            foreach (char c in prompt)
            {
                if (c == '\0') nullBytes++;
                else if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                    controlChars++;
            }

            if (nullBytes > 0)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Critical;
                result.Message = $"Prompt contains {nullBytes} null byte(s).";
                details.Add($"Null bytes: {nullBytes}");
            }
            else if (controlChars > 0)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Warning;
                result.Message = $"Prompt contains {controlChars} unexpected control character(s).";
                details.Add($"Control characters: {controlChars}");
            }
            else
            {
                result.Passed = true;
                result.Message = "Encoding OK.";
            }
            result.Details = details;
            return result;
        }

        private GateCheckResult CheckRepetition(string prompt)
        {
            var result = new GateCheckResult { CheckName = "repetition" };
            var lineList = prompt.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (lineList.Count == 0)
            {
                result.Passed = true;
                result.Message = "No content to check.";
                return result;
            }

            var groups = lineList.GroupBy(l => l)
                .Where(g => g.Count() > 2)
                .Select(g => new { Line = g.Key.Length > 50 ? g.Key[..50] + "..." : g.Key, Count = g.Count() })
                .ToList();

            if (groups.Count > 0)
            {
                result.Passed = false;
                result.Severity = GateSeverity.Warning;
                var totalDupes = groups.Sum(g => g.Count);
                result.Message = $"Found {totalDupes} repeated lines across {groups.Count} pattern(s).";
                result.Details = groups.Select(g => $"\"{g.Line}\" repeated {g.Count}x").ToList();
            }
            else
            {
                result.Passed = true;
                result.Message = "No significant repetition detected.";
            }
            return result;
        }

        private GateCheckResult CheckWhitespace(string prompt)
        {
            var result = new GateCheckResult { CheckName = "whitespace" };
            var details = new List<string>();

            bool excessiveBlanks;
            try
            {
                excessiveBlanks = Regex.IsMatch(prompt, @"\n{5,}", RegexOptions.None, RegexTimeout);
            }
            catch (RegexMatchTimeoutException)
            {
                excessiveBlanks = false;
            }

            if (excessiveBlanks)
                details.Add("Contains 5+ consecutive blank lines");

            var lines = prompt.Split('\n');
            int trailingCount = lines.Count(l => l.Length > 0 && l.TrimEnd().Length < l.Length);
            double trailingPct = lines.Length > 0 ? (double)trailingCount / lines.Length * 100 : 0;
            if (trailingPct > 50)
                details.Add($"{trailingPct:F0}% of lines have trailing whitespace");

            if (details.Count > 0)
            {
                result.Passed = true;
                result.Severity = GateSeverity.Info;
                result.Message = "Whitespace issues noted.";
            }
            else
            {
                result.Passed = true;
                result.Message = "Whitespace OK.";
            }
            result.Details = details;
            return result;
        }

        // ── Custom check management ──────────────────────────

        /// <summary>
        /// Adds a custom quality check.
        /// </summary>
        public void AddCheck(QualityCheck check)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            lock (_lock)
            {
                if (_checks.Any(c => c.Name == check.Name))
                    throw new ArgumentException($"A check named '{check.Name}' already exists.");
                _checks.Add(check);
            }
        }

        /// <summary>
        /// Removes a check by name.
        /// </summary>
        public bool RemoveCheck(string name)
        {
            lock (_lock)
            {
                return _checks.RemoveAll(c => c.Name == name) > 0;
            }
        }

        /// <summary>
        /// Enables or disables a check by name.
        /// </summary>
        public bool SetCheckEnabled(string name, bool enabled)
        {
            lock (_lock)
            {
                var check = _checks.FirstOrDefault(c => c.Name == name);
                if (check == null) return false;
                check.Enabled = enabled;
                return true;
            }
        }

        /// <summary>
        /// Gets the names of all registered checks.
        /// </summary>
        public IReadOnlyList<string> GetCheckNames()
        {
            lock (_lock)
            {
                return _checks.Select(c => c.Name).ToList().AsReadOnly();
            }
        }

        // ── Evaluation ───────────────────────────────────────

        /// <summary>
        /// Evaluates the prompt against all enabled checks and returns a verdict.
        /// </summary>
        public GateVerdict Evaluate(string prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var verdict = new GateVerdict { Policy = Policy };

            List<QualityCheck> checks;
            lock (_lock)
            {
                checks = _checks.Where(c => c.Enabled).ToList();
            }

            foreach (var check in checks)
            {
                var checkSw = System.Diagnostics.Stopwatch.StartNew();
                GateCheckResult result;
                try
                {
                    result = check.Evaluate(prompt);
                }
                catch (Exception ex)
                {
                    result = new GateCheckResult
                    {
                        CheckName = check.Name,
                        Passed = false,
                        Severity = GateSeverity.Error,
                        Message = $"Check threw exception: {ex.Message}"
                    };
                }
                checkSw.Stop();
                result.ElapsedMs = checkSw.Elapsed.TotalMilliseconds;
                result.CheckName = check.Name;
                verdict.Results.Add(result);
            }

            sw.Stop();
            verdict.TotalElapsedMs = sw.Elapsed.TotalMilliseconds;
            verdict.TotalChecks = verdict.Results.Count;
            verdict.PassedChecks = verdict.Results.Count(r => r.Passed);
            verdict.FailedChecks = verdict.Results.Count(r => !r.Passed);

            verdict.Passed = DetermineOverallPass(verdict.Results);

            if (verdict.Passed)
            {
                verdict.Summary = verdict.FailedChecks == 0
                    ? $"All {verdict.TotalChecks} checks passed."
                    : $"Gate passed with {verdict.FailedChecks} non-blocking issue(s).";
            }
            else
            {
                var blocking = verdict.Results
                    .Where(r => !r.Passed && IsBlockingSeverity(r.Severity))
                    .Select(r => r.CheckName)
                    .ToList();
                verdict.Summary = $"Gate FAILED — {blocking.Count} blocking check(s): {string.Join(", ", blocking)}.";
            }

            return verdict;
        }

        /// <summary>
        /// Quick check — returns true if the prompt would pass the gate.
        /// </summary>
        public bool IsAcceptable(string prompt) => Evaluate(prompt).Passed;

        // ── Policy logic ─────────────────────────────────────

        private bool DetermineOverallPass(List<GateCheckResult> results)
        {
            foreach (var r in results)
            {
                if (!r.Passed && IsBlockingSeverity(r.Severity))
                    return false;
            }
            return true;
        }

        private bool IsBlockingSeverity(GateSeverity severity)
        {
            return Policy switch
            {
                GatePolicy.Lenient => severity >= GateSeverity.Critical,
                GatePolicy.Standard => severity >= GateSeverity.Error,
                GatePolicy.Strict => severity >= GateSeverity.Warning,
                _ => severity >= GateSeverity.Error
            };
        }
    }
}
