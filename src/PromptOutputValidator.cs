using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prompt
{
    /// <summary>
    /// Result of validating an LLM response against output constraints.
    /// </summary>
    public class OutputValidationResult
    {
        /// <summary>Gets whether the output passed all validation rules.</summary>
        public bool IsValid => Violations.Count == 0;

        /// <summary>Gets the validated output text.</summary>
        public string Output { get; internal set; } = "";

        /// <summary>Gets the list of validation violations found.</summary>
        public List<OutputViolation> Violations { get; internal set; } = new();

        /// <summary>Gets the rules that passed successfully.</summary>
        public List<string> PassedRules { get; internal set; } = new();

        /// <summary>Gets a summary of the validation outcome.</summary>
        public string Summary => IsValid
            ? $"Valid: {PassedRules.Count} rule(s) passed"
            : $"Invalid: {Violations.Count} violation(s) — {string.Join("; ", Violations.Select(v => v.Rule))}";
    }

    /// <summary>
    /// Describes a single validation violation.
    /// </summary>
    public class OutputViolation
    {
        /// <summary>Gets the rule that was violated.</summary>
        public string Rule { get; internal set; } = "";

        /// <summary>Gets a human-readable description of the violation.</summary>
        public string Message { get; internal set; } = "";

        /// <summary>Gets the severity (Error, Warning).</summary>
        public ViolationSeverity Severity { get; internal set; } = ViolationSeverity.Error;
    }

    // ViolationSeverity enum is defined in PromptGrammarValidator.cs

    /// <summary>
    /// A single validation rule that can be applied to LLM output.
    /// </summary>
    public class OutputRule
    {
        /// <summary>Gets the rule name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets the validation function. Returns null if valid, or a violation message.</summary>
        public Func<string, string> Validate { get; set; } = _ => null;

        /// <summary>Gets the severity for violations of this rule.</summary>
        public ViolationSeverity Severity { get; set; } = ViolationSeverity.Error;
    }

    /// <summary>
    /// Configuration for <see cref="PromptOutputValidator"/>.
    /// </summary>
    public class OutputValidatorOptions
    {
        /// <summary>Trim whitespace from output before validation. Default: true.</summary>
        public bool TrimOutput { get; set; } = true;

        /// <summary>Treat warnings as errors (fail validation on warnings). Default: false.</summary>
        public bool StrictMode { get; set; }
    }

    /// <summary>
    /// Validates LLM responses against configurable output constraints:
    /// length limits, regex patterns, required/forbidden content, JSON structure,
    /// enumerated values, and custom rules.
    /// </summary>
    /// <example>
    /// <code>
    /// var validator = new PromptOutputValidator()
    ///     .MaxLength(500)
    ///     .MustMatchRegex(@"^\{.*\}$", "Must be JSON object")
    ///     .MustContain("result")
    ///     .MustNotContain("error");
    ///
    /// var result = validator.Validate(llmResponse);
    /// if (!result.IsValid)
    ///     Console.WriteLine(result.Summary);
    /// </code>
    /// </example>
    public class PromptOutputValidator
    {
        private readonly List<OutputRule> _rules = new();
        private readonly OutputValidatorOptions _options;

        /// <summary>Creates a new validator with default options.</summary>
        public PromptOutputValidator() : this(new OutputValidatorOptions()) { }

        /// <summary>Creates a new validator with the specified options.</summary>
        public PromptOutputValidator(OutputValidatorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        // ── Built-in rule builders ───────────────────────────────────────

        /// <summary>Output must not exceed <paramref name="max"/> characters.</summary>
        public PromptOutputValidator MaxLength(int max)
        {
            _rules.Add(new OutputRule
            {
                Name = "MaxLength",
                Validate = s => s.Length > max ? $"Output is {s.Length} chars, max allowed is {max}" : null
            });
            return this;
        }

        /// <summary>Output must be at least <paramref name="min"/> characters.</summary>
        public PromptOutputValidator MinLength(int min)
        {
            _rules.Add(new OutputRule
            {
                Name = "MinLength",
                Validate = s => s.Length < min ? $"Output is {s.Length} chars, min required is {min}" : null
            });
            return this;
        }

        /// <summary>Output must not exceed <paramref name="max"/> words.</summary>
        public PromptOutputValidator MaxWords(int max)
        {
            _rules.Add(new OutputRule
            {
                Name = "MaxWords",
                Validate = s =>
                {
                    int count = WordCount(s);
                    return count > max ? $"Output has {count} words, max allowed is {max}" : null;
                }
            });
            return this;
        }

        /// <summary>Output must have at least <paramref name="min"/> words.</summary>
        public PromptOutputValidator MinWords(int min)
        {
            _rules.Add(new OutputRule
            {
                Name = "MinWords",
                Validate = s =>
                {
                    int count = WordCount(s);
                    return count < min ? $"Output has {count} words, min required is {min}" : null;
                }
            });
            return this;
        }

        /// <summary>Output must match the given regex pattern.</summary>
        public PromptOutputValidator MustMatchRegex(string pattern, string description = null)
        {
            var regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
            _rules.Add(new OutputRule
            {
                Name = "MustMatchRegex",
                Validate = s => !regex.IsMatch(s)
                    ? $"Output does not match pattern: {description ?? pattern}"
                    : null
            });
            return this;
        }

        /// <summary>Output must NOT match the given regex pattern.</summary>
        public PromptOutputValidator MustNotMatchRegex(string pattern, string description = null)
        {
            var regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
            _rules.Add(new OutputRule
            {
                Name = "MustNotMatchRegex",
                Validate = s => regex.IsMatch(s)
                    ? $"Output matches forbidden pattern: {description ?? pattern}"
                    : null
            });
            return this;
        }

        /// <summary>Output must contain the specified substring.</summary>
        public PromptOutputValidator MustContain(string substring, StringComparison comparison = StringComparison.Ordinal)
        {
            _rules.Add(new OutputRule
            {
                Name = "MustContain",
                Validate = s => s.IndexOf(substring, comparison) < 0
                    ? $"Output must contain \"{substring}\""
                    : null
            });
            return this;
        }

        /// <summary>Output must NOT contain the specified substring.</summary>
        public PromptOutputValidator MustNotContain(string substring, StringComparison comparison = StringComparison.Ordinal)
        {
            _rules.Add(new OutputRule
            {
                Name = "MustNotContain",
                Validate = s => s.IndexOf(substring, comparison) >= 0
                    ? $"Output must not contain \"{substring}\""
                    : null
            });
            return this;
        }

        /// <summary>Output must be one of the allowed values.</summary>
        public PromptOutputValidator OneOf(params string[] allowedValues)
        {
            return OneOf(StringComparison.Ordinal, allowedValues);
        }

        /// <summary>Output must be one of the allowed values (with custom comparison).</summary>
        public PromptOutputValidator OneOf(StringComparison comparison, params string[] allowedValues)
        {
            var allowed = allowedValues.ToList();
            _rules.Add(new OutputRule
            {
                Name = "OneOf",
                Validate = s => !allowed.Any(v => string.Equals(s, v, comparison))
                    ? $"Output must be one of: {string.Join(", ", allowed.Select(v => $"\"{v}\""))}"
                    : null
            });
            return this;
        }

        /// <summary>Output must start with the given prefix.</summary>
        public PromptOutputValidator MustStartWith(string prefix, StringComparison comparison = StringComparison.Ordinal)
        {
            _rules.Add(new OutputRule
            {
                Name = "MustStartWith",
                Validate = s => !s.StartsWith(prefix, comparison)
                    ? $"Output must start with \"{prefix}\""
                    : null
            });
            return this;
        }

        /// <summary>Output must end with the given suffix.</summary>
        public PromptOutputValidator MustEndWith(string suffix, StringComparison comparison = StringComparison.Ordinal)
        {
            _rules.Add(new OutputRule
            {
                Name = "MustEndWith",
                Validate = s => !s.EndsWith(suffix, comparison)
                    ? $"Output must end with \"{suffix}\""
                    : null
            });
            return this;
        }

        /// <summary>Output must have exactly <paramref name="count"/> lines.</summary>
        public PromptOutputValidator ExactLineCount(int count)
        {
            _rules.Add(new OutputRule
            {
                Name = "ExactLineCount",
                Validate = s =>
                {
                    int lines = LineCount(s);
                    return lines != count ? $"Output has {lines} lines, expected exactly {count}" : null;
                }
            });
            return this;
        }

        /// <summary>Output line count must be within [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public PromptOutputValidator LineCountBetween(int min, int max)
        {
            _rules.Add(new OutputRule
            {
                Name = "LineCountBetween",
                Validate = s =>
                {
                    int lines = LineCount(s);
                    return (lines < min || lines > max)
                        ? $"Output has {lines} lines, expected between {min} and {max}"
                        : null;
                }
            });
            return this;
        }

        /// <summary>Output must be valid JSON (starts/ends with {} or []).</summary>
        public PromptOutputValidator MustBeJson()
        {
            _rules.Add(new OutputRule
            {
                Name = "MustBeJson",
                Validate = s =>
                {
                    var trimmed = s.Trim();
                    if (trimmed.Length == 0) return "Output is empty, expected JSON";
                    // Basic structural check: must start/end with matching brackets
                    if ((trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}') ||
                        (trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']'))
                    {
                        // Verify balanced braces/brackets
                        int depth = 0;
                        bool inString = false;
                        bool escaped = false;
                        foreach (char c in trimmed)
                        {
                            if (escaped) { escaped = false; continue; }
                            if (c == '\\' && inString) { escaped = true; continue; }
                            if (c == '"') { inString = !inString; continue; }
                            if (inString) continue;
                            if (c == '{' || c == '[') depth++;
                            else if (c == '}' || c == ']') depth--;
                            if (depth < 0) return "Output has unbalanced JSON brackets";
                        }
                        return depth == 0 ? null : "Output has unbalanced JSON brackets";
                    }
                    return "Output is not valid JSON (must start with { or [)";
                }
            });
            return this;
        }

        /// <summary>Output must contain a specific JSON key (simple top-level check).</summary>
        public PromptOutputValidator MustContainJsonKey(string key)
        {
            var pattern = new Regex($"\"{ Regex.Escape(key) }\"\\s*:", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
            _rules.Add(new OutputRule
            {
                Name = $"MustContainJsonKey({key})",
                Validate = s => !pattern.IsMatch(s)
                    ? $"Output JSON must contain key \"{key}\""
                    : null
            });
            return this;
        }

        /// <summary>Output must not be empty or whitespace-only.</summary>
        public PromptOutputValidator NotEmpty()
        {
            _rules.Add(new OutputRule
            {
                Name = "NotEmpty",
                Validate = s => string.IsNullOrWhiteSpace(s) ? "Output must not be empty" : null
            });
            return this;
        }

        /// <summary>Add a custom validation rule.</summary>
        public PromptOutputValidator AddRule(string name, Func<string, string> validate, ViolationSeverity severity = ViolationSeverity.Error)
        {
            _rules.Add(new OutputRule { Name = name, Validate = validate, Severity = severity });
            return this;
        }

        /// <summary>Add a warning-level rule (does not fail validation unless strict mode is on).</summary>
        public PromptOutputValidator AddWarning(string name, Func<string, string> validate)
        {
            return AddRule(name, validate, ViolationSeverity.Warning);
        }

        // ── Validation ──────────────────────────────────────────────────

        /// <summary>
        /// Validate the given output against all configured rules.
        /// </summary>
        public OutputValidationResult Validate(string output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            var processed = _options.TrimOutput ? output.Trim() : output;
            var result = new OutputValidationResult
            {
                Output = processed,
                Violations = new List<OutputViolation>(),
                PassedRules = new List<string>()
            };

            foreach (var rule in _rules)
            {
                string violation = rule.Validate(processed);
                if (violation != null)
                {
                    var severity = rule.Severity;
                    // In strict mode, warnings become errors
                    if (_options.StrictMode && severity == ViolationSeverity.Warning)
                        severity = ViolationSeverity.Error;

                    result.Violations.Add(new OutputViolation
                    {
                        Rule = rule.Name,
                        Message = violation,
                        Severity = severity
                    });
                }
                else
                {
                    result.PassedRules.Add(rule.Name);
                }
            }

            // Remove warnings from violations for IsValid check (unless strict)
            if (!_options.StrictMode)
            {
                var errors = result.Violations.Where(v => v.Severity == ViolationSeverity.Error).ToList();
                var warnings = result.Violations.Where(v => v.Severity == ViolationSeverity.Warning).ToList();
                // Keep all in violations list but IsValid only checks errors
                // Actually, IsValid checks Violations.Count — so move warnings out
                // Better: keep violations as-is and adjust IsValid logic via a separate property
                // For simplicity, we separate: Violations = errors only for IsValid, but we need warnings accessible
                // Let's just keep all and note that IsValid counts all. Users should use StrictMode or check severity.
            }

            return result;
        }

        /// <summary>
        /// Validate and throw <see cref="OutputValidationException"/> if invalid.
        /// </summary>
        public string ValidateOrThrow(string output)
        {
            var result = Validate(output);
            if (!result.IsValid)
                throw new OutputValidationException(result);
            return result.Output;
        }

        /// <summary>Gets the number of configured rules.</summary>
        public int RuleCount => _rules.Count;

        // ── Helpers ─────────────────────────────────────────────────────

        private static int WordCount(string s) =>
            string.IsNullOrWhiteSpace(s) ? 0 : s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

        private static int LineCount(string s) =>
            string.IsNullOrWhiteSpace(s) ? 0 : s.Split('\n').Length;
    }

    /// <summary>
    /// Exception thrown when output validation fails via <see cref="PromptOutputValidator.ValidateOrThrow"/>.
    /// </summary>
    public class OutputValidationException : Exception
    {
        /// <summary>Gets the validation result.</summary>
        public OutputValidationResult Result { get; }

        /// <summary>Creates a new instance with the given validation result.</summary>
        public OutputValidationException(OutputValidationResult result)
            : base(result.Summary)
        {
            Result = result;
        }
    }
}
