namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Strategy for refining prompts when validation fails.
    /// </summary>
    public enum RefinementStrategy
    {
        /// <summary>Append validation feedback as constraints to the original prompt.</summary>
        AppendConstraints,

        /// <summary>Wrap the prompt with explicit format instructions based on failures.</summary>
        WrapWithFormat,

        /// <summary>Incrementally tighten instructions, adding more specificity each round.</summary>
        ProgressiveTightening,

        /// <summary>Replace the prompt with a structured template derived from the schema.</summary>
        SchemaDirected,

        /// <summary>Apply all strategies in escalating order.</summary>
        Escalating
    }

    /// <summary>
    /// Type of validation rule for checking responses.
    /// </summary>
    public enum ValidationRuleKind
    {
        /// <summary>Response must be valid JSON.</summary>
        JsonFormat,

        /// <summary>Response must match a regex pattern.</summary>
        RegexMatch,

        /// <summary>Response length must be within bounds.</summary>
        LengthRange,

        /// <summary>Response must contain specific keywords.</summary>
        ContainsKeywords,

        /// <summary>Response must NOT contain specific keywords.</summary>
        ExcludesKeywords,

        /// <summary>Response must be one of the allowed values.</summary>
        EnumValue,

        /// <summary>JSON response must have required fields.</summary>
        JsonFields,

        /// <summary>Custom validation via delegate (not serializable).</summary>
        Custom
    }

    /// <summary>
    /// A single validation rule for checking prompt responses.
    /// </summary>
    public class ValidationRule
    {
        /// <summary>Gets or sets the rule kind.</summary>
        public ValidationRuleKind Kind { get; set; }

        /// <summary>Gets or sets the rule name for reporting.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the rule parameter (pattern, keywords, etc.).</summary>
        public string Parameter { get; set; } = "";

        /// <summary>Gets or sets additional parameters as key-value pairs.</summary>
        public Dictionary<string, string> ExtraParams { get; set; } = new();

        /// <summary>Gets or sets the severity weight (0.0-1.0). Higher = more critical.</summary>
        public double Weight { get; set; } = 1.0;

        /// <summary>Gets or sets the human-readable feedback message when this rule fails.</summary>
        public string FailureMessage { get; set; } = "";

        /// <summary>Custom validation function (not serialized).</summary>
        [JsonIgnore]
        public Func<string, bool>? CustomValidator { get; set; }

        /// <summary>
        /// Creates a JSON format validation rule.
        /// </summary>
        public static ValidationRule Json(string name = "json_format") =>
            new() { Kind = ValidationRuleKind.JsonFormat, Name = name, FailureMessage = "Response must be valid JSON" };

        /// <summary>
        /// Creates a regex match validation rule.
        /// </summary>
        public static ValidationRule Regex(string pattern, string name = "regex_match") =>
            new() { Kind = ValidationRuleKind.RegexMatch, Name = name, Parameter = pattern,
                     FailureMessage = $"Response must match pattern: {pattern}" };

        /// <summary>
        /// Creates a length range validation rule.
        /// </summary>
        public static ValidationRule Length(int min, int max, string name = "length_range") =>
            new() { Kind = ValidationRuleKind.LengthRange, Name = name,
                     ExtraParams = new() { ["min"] = min.ToString(), ["max"] = max.ToString() },
                     FailureMessage = $"Response length must be between {min} and {max} characters" };

        /// <summary>
        /// Creates a contains-keywords validation rule.
        /// </summary>
        public static ValidationRule Contains(IEnumerable<string> keywords, string name = "contains_keywords") =>
            new() { Kind = ValidationRuleKind.ContainsKeywords, Name = name,
                     Parameter = string.Join("|", keywords),
                     FailureMessage = $"Response must contain: {string.Join(", ", keywords)}" };

        /// <summary>
        /// Creates an excludes-keywords validation rule.
        /// </summary>
        public static ValidationRule Excludes(IEnumerable<string> keywords, string name = "excludes_keywords") =>
            new() { Kind = ValidationRuleKind.ExcludesKeywords, Name = name,
                     Parameter = string.Join("|", keywords),
                     FailureMessage = $"Response must not contain: {string.Join(", ", keywords)}" };

        /// <summary>
        /// Creates an enum value validation rule.
        /// </summary>
        public static ValidationRule Enum(IEnumerable<string> allowed, string name = "enum_value") =>
            new() { Kind = ValidationRuleKind.EnumValue, Name = name,
                     Parameter = string.Join("|", allowed),
                     FailureMessage = $"Response must be one of: {string.Join(", ", allowed)}" };

        /// <summary>
        /// Creates a JSON fields validation rule.
        /// </summary>
        public static ValidationRule JsonFields(IEnumerable<string> fields, string name = "json_fields") =>
            new() { Kind = ValidationRuleKind.JsonFields, Name = name,
                     Parameter = string.Join("|", fields),
                     FailureMessage = $"JSON response must have fields: {string.Join(", ", fields)}" };

        /// <summary>
        /// Creates a custom validation rule.
        /// </summary>
        public static ValidationRule FromDelegate(Func<string, bool> validator, string name, string failureMessage) =>
            new() { Kind = ValidationRuleKind.Custom, Name = name,
                     FailureMessage = failureMessage, CustomValidator = validator };
    }

    /// <summary>
    /// Result of validating a single response against all rules.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>Gets whether all rules passed.</summary>
        public bool IsValid => FailedRules.Count == 0;

        /// <summary>Gets the response that was validated.</summary>
        public string Response { get; set; } = "";

        /// <summary>Gets the rules that passed.</summary>
        public List<string> PassedRules { get; set; } = new();

        /// <summary>Gets the rules that failed with their messages.</summary>
        public Dictionary<string, string> FailedRules { get; set; } = new();

        /// <summary>Gets the overall score (0.0-1.0) based on weighted rule passes.</summary>
        public double Score { get; set; }

        /// <summary>Gets feedback text combining all failure messages.</summary>
        public string Feedback => FailedRules.Count == 0
            ? "All validation rules passed."
            : string.Join("; ", FailedRules.Values);
    }

    /// <summary>
    /// Record of a single negotiation round.
    /// </summary>
    public class NegotiationRound
    {
        /// <summary>Gets or sets the round number (1-based).</summary>
        public int Round { get; set; }

        /// <summary>Gets or sets the prompt sent in this round.</summary>
        public string Prompt { get; set; } = "";

        /// <summary>Gets or sets the simulated/actual response.</summary>
        public string Response { get; set; } = "";

        /// <summary>Gets or sets the validation result.</summary>
        public ValidationResult Validation { get; set; } = new();

        /// <summary>Gets or sets the refinement strategy used for the next round.</summary>
        public RefinementStrategy? StrategyUsed { get; set; }

        /// <summary>Gets or sets the timestamp.</summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Outcome of a negotiation session.
    /// </summary>
    public enum NegotiationOutcome
    {
        /// <summary>Validation passed within max rounds.</summary>
        Success,

        /// <summary>All rounds exhausted without passing validation.</summary>
        Exhausted,

        /// <summary>Score improved but didn't reach threshold.</summary>
        PartialSuccess,

        /// <summary>Score did not improve across rounds.</summary>
        Stalled
    }

    /// <summary>
    /// Complete result of a negotiation session.
    /// </summary>
    public class NegotiationResult
    {
        /// <summary>Gets the outcome.</summary>
        public NegotiationOutcome Outcome { get; set; }

        /// <summary>Gets all rounds in order.</summary>
        public List<NegotiationRound> Rounds { get; set; } = new();

        /// <summary>Gets the best response seen (highest validation score).</summary>
        public string BestResponse { get; set; } = "";

        /// <summary>Gets the best validation score achieved.</summary>
        public double BestScore { get; set; }

        /// <summary>Gets the round number that produced the best response.</summary>
        public int BestRound { get; set; }

        /// <summary>Gets the total number of rounds attempted.</summary>
        public int TotalRounds => Rounds.Count;

        /// <summary>Gets the original prompt.</summary>
        public string OriginalPrompt { get; set; } = "";

        /// <summary>Gets the final refined prompt.</summary>
        public string FinalPrompt { get; set; } = "";

        /// <summary>Gets the score improvement from first to best round.</summary>
        public double ScoreImprovement => Rounds.Count > 0
            ? BestScore - Rounds[0].Validation.Score
            : 0;

        /// <summary>Gets a summary of the negotiation.</summary>
        public string Summary =>
            $"Outcome: {Outcome}, Rounds: {TotalRounds}, Best score: {BestScore:F2} (round {BestRound}), " +
            $"Improvement: {ScoreImprovement:+0.00;-0.00;0.00}";
    }

    /// <summary>
    /// Configuration for a negotiation session.
    /// </summary>
    public class NegotiationOptions
    {
        /// <summary>Gets or sets the maximum number of refinement rounds.</summary>
        public int MaxRounds { get; set; } = 5;

        /// <summary>Gets or sets the validation score threshold to accept (0.0-1.0).</summary>
        public double AcceptThreshold { get; set; } = 1.0;

        /// <summary>Gets or sets the refinement strategy.</summary>
        public RefinementStrategy Strategy { get; set; } = RefinementStrategy.Escalating;

        /// <summary>Gets or sets the minimum score improvement per round to continue (stall detection).</summary>
        public double MinImprovement { get; set; } = 0.0;

        /// <summary>Gets or sets the number of stalled rounds before giving up.</summary>
        public int StallThreshold { get; set; } = 2;

        /// <summary>Gets or sets whether to return the best response even on failure.</summary>
        public bool ReturnBestOnFailure { get; set; } = true;
    }

    /// <summary>
    /// Iteratively refines prompts based on validation feedback until the response
    /// meets all specified criteria. Simulates a negotiation loop where each failed
    /// validation informs the next prompt refinement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The negotiator works with a response provider function that takes a prompt and
    /// returns a response (could be a real LLM call or a test stub). It validates each
    /// response against configurable rules and refines the prompt based on failures.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var negotiator = new PromptNegotiator()
    ///     .AddRule(ValidationRule.Json())
    ///     .AddRule(ValidationRule.JsonFields(new[] { "name", "age", "email" }))
    ///     .AddRule(ValidationRule.Length(50, 500))
    ///     .WithOptions(o => o.MaxRounds = 3);
    ///
    /// var result = negotiator.Negotiate(
    ///     "Return a user profile as JSON",
    ///     prompt => myLlmCall(prompt)
    /// );
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptNegotiator
    {
        private readonly List<ValidationRule> _rules = new();
        private NegotiationOptions _options = new();

        /// <summary>Gets the configured validation rules.</summary>
        public IReadOnlyList<ValidationRule> Rules => _rules.AsReadOnly();

        /// <summary>Gets the negotiation options.</summary>
        public NegotiationOptions Options => _options;

        /// <summary>
        /// Adds a validation rule.
        /// </summary>
        public PromptNegotiator AddRule(ValidationRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            _rules.Add(rule);
            return this;
        }

        /// <summary>
        /// Adds multiple validation rules.
        /// </summary>
        public PromptNegotiator AddRules(IEnumerable<ValidationRule> rules)
        {
            foreach (var rule in rules) AddRule(rule);
            return this;
        }

        /// <summary>
        /// Configures negotiation options.
        /// </summary>
        public PromptNegotiator WithOptions(Action<NegotiationOptions> configure)
        {
            configure(_options);
            return this;
        }

        /// <summary>
        /// Removes all validation rules.
        /// </summary>
        public PromptNegotiator ClearRules()
        {
            _rules.Clear();
            return this;
        }

        /// <summary>
        /// Validates a response against all configured rules.
        /// </summary>
        public ValidationResult Validate(string response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var result = new ValidationResult { Response = response };
            double totalWeight = 0;
            double passedWeight = 0;

            foreach (var rule in _rules)
            {
                totalWeight += rule.Weight;
                bool passed = EvaluateRule(rule, response);

                if (passed)
                {
                    result.PassedRules.Add(rule.Name);
                    passedWeight += rule.Weight;
                }
                else
                {
                    result.FailedRules[rule.Name] = rule.FailureMessage;
                }
            }

            result.Score = totalWeight > 0 ? passedWeight / totalWeight : 1.0;
            return result;
        }

        /// <summary>
        /// Runs a negotiation session with a response provider.
        /// </summary>
        /// <param name="prompt">The initial prompt to refine.</param>
        /// <param name="responseProvider">Function that takes a prompt and returns a response.</param>
        /// <returns>The negotiation result with all rounds and best response.</returns>
        public NegotiationResult Negotiate(string prompt, Func<string, string> responseProvider)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            if (responseProvider == null) throw new ArgumentNullException(nameof(responseProvider));
            if (_rules.Count == 0) throw new InvalidOperationException("No validation rules configured.");

            var result = new NegotiationResult { OriginalPrompt = prompt };
            string currentPrompt = prompt;
            int stallCount = 0;
            double lastScore = -1;

            for (int i = 1; i <= _options.MaxRounds; i++)
            {
                string response = responseProvider(currentPrompt);
                var validation = Validate(response);

                var round = new NegotiationRound
                {
                    Round = i,
                    Prompt = currentPrompt,
                    Response = response,
                    Validation = validation
                };

                if (validation.Score > result.BestScore)
                {
                    result.BestScore = validation.Score;
                    result.BestResponse = response;
                    result.BestRound = i;
                }

                result.Rounds.Add(round);

                // Success check
                if (validation.Score >= _options.AcceptThreshold)
                {
                    result.Outcome = NegotiationOutcome.Success;
                    result.FinalPrompt = currentPrompt;
                    return result;
                }

                // Stall detection
                double improvement = validation.Score - lastScore;
                if (lastScore >= 0 && improvement <= _options.MinImprovement)
                {
                    stallCount++;
                    if (stallCount >= _options.StallThreshold)
                    {
                        result.Outcome = NegotiationOutcome.Stalled;
                        result.FinalPrompt = currentPrompt;
                        return result;
                    }
                }
                else
                {
                    stallCount = 0;
                }
                lastScore = validation.Score;

                // Refine for next round if not last
                if (i < _options.MaxRounds)
                {
                    var strategy = GetStrategyForRound(i);
                    round.StrategyUsed = strategy;
                    currentPrompt = RefinePrompt(currentPrompt, validation, strategy, i);
                }
            }

            result.Outcome = result.BestScore > result.Rounds[0].Validation.Score
                ? NegotiationOutcome.PartialSuccess
                : NegotiationOutcome.Exhausted;
            result.FinalPrompt = currentPrompt;
            return result;
        }

        /// <summary>
        /// Generates a refined prompt based on validation failures and the chosen strategy.
        /// </summary>
        public string RefinePrompt(string prompt, ValidationResult validation,
                                    RefinementStrategy strategy, int round = 1)
        {
            return strategy switch
            {
                RefinementStrategy.AppendConstraints => ApplyAppendConstraints(prompt, validation),
                RefinementStrategy.WrapWithFormat => ApplyWrapWithFormat(prompt, validation),
                RefinementStrategy.ProgressiveTightening => ApplyProgressiveTightening(prompt, validation, round),
                RefinementStrategy.SchemaDirected => ApplySchemaDirected(prompt, validation),
                RefinementStrategy.Escalating => ApplyEscalating(prompt, validation, round),
                _ => prompt
            };
        }

        /// <summary>
        /// Creates a pre-configured negotiator for JSON output.
        /// </summary>
        public static PromptNegotiator ForJson(IEnumerable<string>? requiredFields = null)
        {
            var n = new PromptNegotiator().AddRule(ValidationRule.Json());
            if (requiredFields != null)
                n.AddRule(ValidationRule.JsonFields(requiredFields));
            return n;
        }

        /// <summary>
        /// Creates a pre-configured negotiator for enum/categorical output.
        /// </summary>
        public static PromptNegotiator ForEnum(IEnumerable<string> allowedValues)
        {
            return new PromptNegotiator()
                .AddRule(ValidationRule.Enum(allowedValues))
                .WithOptions(o => o.MaxRounds = 3);
        }

        /// <summary>
        /// Creates a pre-configured negotiator for structured text output.
        /// </summary>
        public static PromptNegotiator ForStructuredText(int minLength, int maxLength,
                                                          IEnumerable<string>? requiredKeywords = null)
        {
            var n = new PromptNegotiator()
                .AddRule(ValidationRule.Length(minLength, maxLength));
            if (requiredKeywords != null)
                n.AddRule(ValidationRule.Contains(requiredKeywords));
            return n;
        }

        /// <summary>
        /// Gets a report of the negotiator's configuration.
        /// </summary>
        public string GetConfigReport()
        {
            var lines = new List<string>
            {
                "=== Prompt Negotiator Configuration ===",
                $"Rules: {_rules.Count}",
                $"Max rounds: {_options.MaxRounds}",
                $"Accept threshold: {_options.AcceptThreshold:F2}",
                $"Strategy: {_options.Strategy}",
                $"Stall threshold: {_options.StallThreshold}",
                ""
            };

            foreach (var rule in _rules)
            {
                lines.Add($"  [{rule.Kind}] {rule.Name} (weight: {rule.Weight:F1})");
                if (!string.IsNullOrEmpty(rule.Parameter))
                    lines.Add($"    Parameter: {rule.Parameter}");
                lines.Add($"    On fail: {rule.FailureMessage}");
            }

            return string.Join("\n", lines);
        }

        // --- Private helpers ---

        private bool EvaluateRule(ValidationRule rule, string response)
        {
            return rule.Kind switch
            {
                ValidationRuleKind.JsonFormat => IsValidJson(response),
                ValidationRuleKind.RegexMatch => !string.IsNullOrEmpty(rule.Parameter) &&
                    System.Text.RegularExpressions.Regex.IsMatch(response, rule.Parameter, RegexOptions.None, TimeSpan.FromMilliseconds(500)),
                ValidationRuleKind.LengthRange => EvaluateLength(rule, response),
                ValidationRuleKind.ContainsKeywords => EvaluateContains(rule, response),
                ValidationRuleKind.ExcludesKeywords => EvaluateExcludes(rule, response),
                ValidationRuleKind.EnumValue => EvaluateEnum(rule, response),
                ValidationRuleKind.JsonFields => EvaluateJsonFields(rule, response),
                ValidationRuleKind.Custom => rule.CustomValidator?.Invoke(response) ?? true,
                _ => true
            };
        }

        private static bool IsValidJson(string text)
        {
            try
            {
                text = text.Trim();
                if (!(text.StartsWith("{") || text.StartsWith("["))) return false;
                JsonDocument.Parse(text);
                return true;
            }
            catch { return false; }
        }

        private static bool EvaluateLength(ValidationRule rule, string response)
        {
            int min = rule.ExtraParams.TryGetValue("min", out var minS) && int.TryParse(minS, out var m) ? m : 0;
            int max = rule.ExtraParams.TryGetValue("max", out var maxS) && int.TryParse(maxS, out var x) ? x : int.MaxValue;
            return response.Length >= min && response.Length <= max;
        }

        private static bool EvaluateContains(ValidationRule rule, string response)
        {
            if (string.IsNullOrEmpty(rule.Parameter)) return true;
            var keywords = rule.Parameter.Split('|');
            return keywords.All(k => response.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EvaluateExcludes(ValidationRule rule, string response)
        {
            if (string.IsNullOrEmpty(rule.Parameter)) return true;
            var keywords = rule.Parameter.Split('|');
            return keywords.All(k => !response.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EvaluateEnum(ValidationRule rule, string response)
        {
            if (string.IsNullOrEmpty(rule.Parameter)) return true;
            var allowed = rule.Parameter.Split('|');
            var trimmed = response.Trim();
            return allowed.Any(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EvaluateJsonFields(ValidationRule rule, string response)
        {
            if (string.IsNullOrEmpty(rule.Parameter)) return true;
            try
            {
                var doc = JsonDocument.Parse(response.Trim());
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
                var fields = rule.Parameter.Split('|');
                return fields.All(f => doc.RootElement.TryGetProperty(f, out _));
            }
            catch { return false; }
        }

        private RefinementStrategy GetStrategyForRound(int round)
        {
            if (_options.Strategy != RefinementStrategy.Escalating)
                return _options.Strategy;

            return round switch
            {
                1 => RefinementStrategy.AppendConstraints,
                2 => RefinementStrategy.WrapWithFormat,
                3 => RefinementStrategy.ProgressiveTightening,
                _ => RefinementStrategy.SchemaDirected
            };
        }

        private static string ApplyAppendConstraints(string prompt, ValidationResult validation)
        {
            if (validation.IsValid) return prompt;

            var constraints = new List<string> { prompt, "", "IMPORTANT CONSTRAINTS:" };
            foreach (var (name, message) in validation.FailedRules)
            {
                constraints.Add($"- {message}");
            }
            return string.Join("\n", constraints);
        }

        private static string ApplyWrapWithFormat(string prompt, ValidationResult validation)
        {
            if (validation.IsValid) return prompt;

            var parts = new List<string>();

            // Add format preamble based on failure types
            if (validation.FailedRules.ContainsKey("json_format") ||
                validation.FailedRules.Keys.Any(k => k.StartsWith("json")))
            {
                parts.Add("You MUST respond with valid JSON only. No explanations, no markdown, no code fences.");
            }

            if (validation.FailedRules.Keys.Any(k => k.Contains("length")))
            {
                parts.Add("Pay careful attention to the required response length.");
            }

            if (validation.FailedRules.Keys.Any(k => k.Contains("enum")))
            {
                parts.Add("Respond with EXACTLY one of the allowed values, nothing else.");
            }

            parts.Add("");
            parts.Add(prompt);

            return string.Join("\n", parts);
        }

        private static string ApplyProgressiveTightening(string prompt, ValidationResult validation, int round)
        {
            if (validation.IsValid) return prompt;

            var strictness = round switch
            {
                1 => "Please ensure your response meets these requirements:",
                2 => "Your previous response did not meet requirements. You MUST:",
                3 => "CRITICAL: Your response MUST EXACTLY follow these rules:",
                _ => "FINAL ATTEMPT: Respond with ONLY the required format. NO extra text:"
            };

            var lines = new List<string> { strictness };
            foreach (var (name, message) in validation.FailedRules)
            {
                lines.Add($"  - {message}");
            }
            lines.Add("");
            lines.Add(prompt);

            return string.Join("\n", lines);
        }

        private static string ApplySchemaDirected(string prompt, ValidationResult validation)
        {
            if (validation.IsValid) return prompt;

            var parts = new List<string> { "Respond with EXACTLY the following structure:" };

            // Build schema hint from failed JSON field rules
            var jsonFieldRules = validation.FailedRules.Keys
                .Where(k => k.Contains("json_fields") || k.Contains("json"))
                .ToList();

            if (jsonFieldRules.Count > 0 || validation.FailedRules.ContainsKey("json_format"))
            {
                parts.Add("{");
                // Extract field names from failure messages
                foreach (var (name, message) in validation.FailedRules)
                {
                    if (message.Contains("fields:"))
                    {
                        var fieldsPart = message.Substring(message.IndexOf("fields:") + 7).Trim();
                        var fields = fieldsPart.Split(',').Select(f => f.Trim());
                        foreach (var field in fields)
                        {
                            parts.Add($"  \"{field}\": \"<value>\",");
                        }
                    }
                }
                parts.Add("}");
                parts.Add("");
            }

            parts.Add(prompt);
            return string.Join("\n", parts);
        }

        private string ApplyEscalating(string prompt, ValidationResult validation, int round)
        {
            var strategy = GetStrategyForRound(round);
            return RefinePrompt(prompt, validation, strategy, round);
        }
    }
}
