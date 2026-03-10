namespace Prompt
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    // ── Enums ──────────────────────────────────────────────────────────

    /// <summary>Rule type for grammar validation.</summary>
    public enum GrammarRuleType
    {
        /// <summary>Response must match a regex pattern.</summary>
        Regex,
        /// <summary>Response must be valid JSON matching a schema.</summary>
        JsonSchema,
        /// <summary>Response must be one of a set of allowed values.</summary>
        Enum,
        /// <summary>Response must start with a specific prefix.</summary>
        StartsWith,
        /// <summary>Response must end with a specific suffix.</summary>
        EndsWith,
        /// <summary>Response must contain a required substring.</summary>
        Contains,
        /// <summary>Response must NOT contain a forbidden substring.</summary>
        NotContains,
        /// <summary>Response length must be within bounds.</summary>
        Length,
        /// <summary>Response must have a specific line count range.</summary>
        LineCount,
        /// <summary>Response must match a structural template (sections, bullets, etc.).</summary>
        Structure,
        /// <summary>Custom validation via delegate.</summary>
        Custom
    }

    /// <summary>Severity of a validation violation.</summary>
    public enum ViolationSeverity
    {
        Info,
        Warning,
        Error
    }

    // ── DTOs ───────────────────────────────────────────────────────────

    /// <summary>A single grammar validation rule.</summary>
    public class GrammarRule
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        [JsonPropertyName("type")]
        public GrammarRuleType Type { get; init; }

        [JsonPropertyName("severity")]
        public ViolationSeverity Severity { get; init; } = ViolationSeverity.Error;

        /// <summary>Pattern string for Regex, prefix/suffix for StartsWith/EndsWith, etc.</summary>
        [JsonPropertyName("pattern")]
        public string Pattern { get; init; } = "";

        /// <summary>Allowed values for Enum type.</summary>
        [JsonPropertyName("allowedValues")]
        public List<string> AllowedValues { get; init; } = new();

        /// <summary>Min value for Length/LineCount rules.</summary>
        [JsonPropertyName("min")]
        public int? Min { get; init; }

        /// <summary>Max value for Length/LineCount rules.</summary>
        [JsonPropertyName("max")]
        public int? Max { get; init; }

        /// <summary>Whether comparison is case-insensitive.</summary>
        [JsonPropertyName("ignoreCase")]
        public bool IgnoreCase { get; init; }

        /// <summary>Structural elements for Structure type.</summary>
        [JsonPropertyName("requiredSections")]
        public List<string> RequiredSections { get; init; } = new();

        /// <summary>JSON property paths required (dot-notation) for JsonSchema type.</summary>
        [JsonPropertyName("requiredProperties")]
        public List<string> RequiredProperties { get; init; } = new();

        /// <summary>Expected JSON property types (path → "string"|"number"|"boolean"|"array"|"object").</summary>
        [JsonPropertyName("propertyTypes")]
        public Dictionary<string, string> PropertyTypes { get; init; } = new();

        /// <summary>Custom validator delegate (not serialized).</summary>
        [JsonIgnore]
        public Func<string, (bool valid, string message)>? CustomValidator { get; init; }

        /// <summary>Whether this rule is enabled.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; } = true;
    }

    /// <summary>A single validation violation.</summary>
    public record GrammarViolation
    {
        [JsonPropertyName("ruleId")]
        public string RuleId { get; init; } = "";

        [JsonPropertyName("ruleName")]
        public string RuleName { get; init; } = "";

        [JsonPropertyName("severity")]
        public ViolationSeverity Severity { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; } = "";

        [JsonPropertyName("expected")]
        public string Expected { get; init; } = "";

        [JsonPropertyName("actual")]
        public string Actual { get; init; } = "";

        [JsonPropertyName("position")]
        public int? Position { get; init; }
    }

    /// <summary>Result of validating a response against a grammar rule set.</summary>
    public class GrammarValidationResult
    {
        [JsonPropertyName("isValid")]
        public bool IsValid { get; init; }

        [JsonPropertyName("response")]
        public string Response { get; init; } = "";

        [JsonPropertyName("ruleSetName")]
        public string RuleSetName { get; init; } = "";

        [JsonPropertyName("totalRules")]
        public int TotalRules { get; init; }

        [JsonPropertyName("passedRules")]
        public int PassedRules { get; init; }

        [JsonPropertyName("failedRules")]
        public int FailedRules { get; init; }

        [JsonPropertyName("score")]
        public double Score { get; init; }

        [JsonPropertyName("violations")]
        public List<GrammarViolation> Violations { get; init; } = new();

        [JsonPropertyName("passedRuleIds")]
        public List<string> PassedRuleIds { get; init; } = new();
    }

    /// <summary>Named set of grammar rules.</summary>
    public class GrammarRuleSet
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        [JsonPropertyName("rules")]
        public List<GrammarRule> Rules { get; init; } = new();

        /// <summary>If true, validation stops at first error-severity violation.</summary>
        [JsonPropertyName("failFast")]
        public bool FailFast { get; init; }
    }

    /// <summary>Batch validation result for multiple responses.</summary>
    public class BatchValidationResult
    {
        [JsonPropertyName("totalResponses")]
        public int TotalResponses { get; init; }

        [JsonPropertyName("validCount")]
        public int ValidCount { get; init; }

        [JsonPropertyName("invalidCount")]
        public int InvalidCount { get; init; }

        [JsonPropertyName("averageScore")]
        public double AverageScore { get; init; }

        [JsonPropertyName("results")]
        public List<GrammarValidationResult> Results { get; init; } = new();

        [JsonPropertyName("commonViolations")]
        public List<CommonViolation> CommonViolations { get; init; } = new();
    }

    /// <summary>A frequently occurring violation across batch results.</summary>
    public class CommonViolation
    {
        [JsonPropertyName("ruleId")]
        public string RuleId { get; init; } = "";

        [JsonPropertyName("ruleName")]
        public string RuleName { get; init; } = "";

        [JsonPropertyName("occurrences")]
        public int Occurrences { get; init; }

        [JsonPropertyName("percentage")]
        public double Percentage { get; init; }
    }

    /// <summary>Suggestion for fixing a violation.</summary>
    public class FixSuggestion
    {
        [JsonPropertyName("ruleId")]
        public string RuleId { get; init; } = "";

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        [JsonPropertyName("suggestedFix")]
        public string SuggestedFix { get; init; } = "";

        [JsonPropertyName("autoFixable")]
        public bool AutoFixable { get; init; }
    }

    /// <summary>Report with violations and suggested fixes.</summary>
    public class GrammarFixReport
    {
        [JsonPropertyName("result")]
        public GrammarValidationResult Result { get; init; } = new();

        [JsonPropertyName("suggestions")]
        public List<FixSuggestion> Suggestions { get; init; } = new();

        [JsonPropertyName("autoFixed")]
        public string? AutoFixed { get; init; }

        [JsonPropertyName("autoFixApplied")]
        public bool AutoFixApplied { get; init; }
    }

    // ── Service ────────────────────────────────────────────────────────

    /// <summary>
    /// Validates LLM response outputs against formal grammar constraints.
    /// Supports regex, JSON schema, enums, structure templates, length/line limits,
    /// and custom validators with detailed violation reports and auto-fix suggestions.
    /// </summary>
    public class PromptGrammarValidator
    {
        private readonly Dictionary<string, GrammarRuleSet> _ruleSets = new();
        private readonly Dictionary<string, GrammarRuleSet> _presets;

        public PromptGrammarValidator()
        {
            _presets = BuildPresets();
        }

        // ── Rule Set Management ────────────────────────────────────────

        /// <summary>Register a named rule set.</summary>
        public void AddRuleSet(GrammarRuleSet ruleSet)
        {
            if (string.IsNullOrWhiteSpace(ruleSet.Name))
                throw new ArgumentException("Rule set name is required.");
            _ruleSets[ruleSet.Name] = ruleSet;
        }

        /// <summary>Get a registered rule set by name.</summary>
        public GrammarRuleSet? GetRuleSet(string name) =>
            _ruleSets.TryGetValue(name, out var rs) ? rs : null;

        /// <summary>List all registered rule set names.</summary>
        public List<string> ListRuleSets() => _ruleSets.Keys.OrderBy(k => k).ToList();

        /// <summary>Remove a rule set.</summary>
        public bool RemoveRuleSet(string name) => _ruleSets.Remove(name);

        /// <summary>Get a built-in preset rule set.</summary>
        public GrammarRuleSet? GetPreset(string name) =>
            _presets.TryGetValue(name, out var rs) ? rs : null;

        /// <summary>List all preset names.</summary>
        public List<string> ListPresets() => _presets.Keys.OrderBy(k => k).ToList();

        // ── Validation ─────────────────────────────────────────────────

        /// <summary>Validate a response against a rule set.</summary>
        public GrammarValidationResult Validate(string response, GrammarRuleSet ruleSet)
        {
            var violations = new List<GrammarViolation>();
            var passed = new List<string>();
            var enabledRules = ruleSet.Rules.Where(r => r.Enabled).ToList();

            foreach (var rule in enabledRules)
            {
                var violation = ValidateRule(response, rule);
                if (violation != null)
                {
                    violations.Add(violation);
                    if (ruleSet.FailFast && violation.Severity == ViolationSeverity.Error)
                        break;
                }
                else
                {
                    passed.Add(rule.Id);
                }
            }

            var errorCount = violations.Count(v => v.Severity == ViolationSeverity.Error);
            var total = enabledRules.Count;
            var passedCount = total - violations.Count;
            var score = total > 0 ? Math.Round((double)passedCount / total * 100, 1) : 100.0;

            return new GrammarValidationResult
            {
                IsValid = errorCount == 0,
                Response = response,
                RuleSetName = ruleSet.Name,
                TotalRules = total,
                PassedRules = passedCount,
                FailedRules = violations.Count,
                Score = score,
                Violations = violations,
                PassedRuleIds = passed
            };
        }

        /// <summary>Validate using a registered rule set name.</summary>
        public GrammarValidationResult Validate(string response, string ruleSetName)
        {
            if (!_ruleSets.TryGetValue(ruleSetName, out var ruleSet))
                throw new ArgumentException($"Rule set '{ruleSetName}' not found.");
            return Validate(response, ruleSet);
        }

        /// <summary>Validate multiple responses against a rule set (batch).</summary>
        public BatchValidationResult ValidateBatch(IEnumerable<string> responses, GrammarRuleSet ruleSet)
        {
            var results = responses.Select(r => Validate(r, ruleSet)).ToList();
            var total = results.Count;
            if (total == 0)
                return new BatchValidationResult { TotalResponses = 0, AverageScore = 0, Results = results };

            var violationCounts = new Dictionary<string, (string name, int count)>();
            foreach (var v in results.SelectMany(r => r.Violations))
            {
                if (!violationCounts.ContainsKey(v.RuleId))
                    violationCounts[v.RuleId] = (v.RuleName, 0);
                var cur = violationCounts[v.RuleId];
                violationCounts[v.RuleId] = (cur.name, cur.count + 1);
            }

            var common = violationCounts
                .OrderByDescending(kv => kv.Value.count)
                .Select(kv => new CommonViolation
                {
                    RuleId = kv.Key,
                    RuleName = kv.Value.name,
                    Occurrences = kv.Value.count,
                    Percentage = Math.Round((double)kv.Value.count / total * 100, 1)
                }).ToList();

            return new BatchValidationResult
            {
                TotalResponses = total,
                ValidCount = results.Count(r => r.IsValid),
                InvalidCount = results.Count(r => !r.IsValid),
                AverageScore = Math.Round(results.Average(r => r.Score), 1),
                Results = results,
                CommonViolations = common
            };
        }

        // ── Fix Suggestions ────────────────────────────────────────────

        /// <summary>Validate and generate fix suggestions.</summary>
        public GrammarFixReport ValidateWithFixes(string response, GrammarRuleSet ruleSet, bool applyAutoFix = false)
        {
            var result = Validate(response, ruleSet);
            var suggestions = new List<FixSuggestion>();
            var fixedResponse = response;
            var anyFixed = false;

            foreach (var violation in result.Violations)
            {
                var rule = ruleSet.Rules.FirstOrDefault(r => r.Id == violation.RuleId);
                if (rule == null) continue;

                var suggestion = GenerateSuggestion(fixedResponse, rule, violation);
                suggestions.Add(suggestion);

                if (applyAutoFix && suggestion.AutoFixable && !string.IsNullOrEmpty(suggestion.SuggestedFix))
                {
                    fixedResponse = suggestion.SuggestedFix;
                    anyFixed = true;
                }
            }

            return new GrammarFixReport
            {
                Result = result,
                Suggestions = suggestions,
                AutoFixed = anyFixed ? fixedResponse : null,
                AutoFixApplied = anyFixed
            };
        }

        // ── Rule Builders (fluent helpers) ──────────────────────────────

        /// <summary>Create a regex rule.</summary>
        public static GrammarRule RegexRule(string id, string name, string pattern,
            ViolationSeverity severity = ViolationSeverity.Error, bool ignoreCase = false) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.Regex, Pattern = pattern, Severity = severity, IgnoreCase = ignoreCase };

        /// <summary>Create an enum rule.</summary>
        public static GrammarRule EnumRule(string id, string name, IEnumerable<string> allowed,
            ViolationSeverity severity = ViolationSeverity.Error, bool ignoreCase = false) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.Enum, AllowedValues = allowed.ToList(), Severity = severity, IgnoreCase = ignoreCase };

        /// <summary>Create a length constraint rule.</summary>
        public static GrammarRule LengthRule(string id, string name, int? min = null, int? max = null,
            ViolationSeverity severity = ViolationSeverity.Error) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.Length, Min = min, Max = max, Severity = severity };

        /// <summary>Create a line count rule.</summary>
        public static GrammarRule LineCountRule(string id, string name, int? min = null, int? max = null,
            ViolationSeverity severity = ViolationSeverity.Error) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.LineCount, Min = min, Max = max, Severity = severity };

        /// <summary>Create a starts-with rule.</summary>
        public static GrammarRule StartsWithRule(string id, string name, string prefix,
            ViolationSeverity severity = ViolationSeverity.Error, bool ignoreCase = false) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.StartsWith, Pattern = prefix, Severity = severity, IgnoreCase = ignoreCase };

        /// <summary>Create an ends-with rule.</summary>
        public static GrammarRule EndsWithRule(string id, string name, string suffix,
            ViolationSeverity severity = ViolationSeverity.Error, bool ignoreCase = false) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.EndsWith, Pattern = suffix, Severity = severity, IgnoreCase = ignoreCase };

        /// <summary>Create a contains rule.</summary>
        public static GrammarRule ContainsRule(string id, string name, string substring,
            ViolationSeverity severity = ViolationSeverity.Error, bool ignoreCase = false) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.Contains, Pattern = substring, Severity = severity, IgnoreCase = ignoreCase };

        /// <summary>Create a not-contains rule.</summary>
        public static GrammarRule NotContainsRule(string id, string name, string substring,
            ViolationSeverity severity = ViolationSeverity.Error, bool ignoreCase = false) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.NotContains, Pattern = substring, Severity = severity, IgnoreCase = ignoreCase };

        /// <summary>Create a JSON schema rule.</summary>
        public static GrammarRule JsonSchemaRule(string id, string name,
            List<string>? requiredProperties = null, Dictionary<string, string>? propertyTypes = null,
            ViolationSeverity severity = ViolationSeverity.Error) =>
            new()
            {
                Id = id, Name = name, Type = GrammarRuleType.JsonSchema, Severity = severity,
                RequiredProperties = requiredProperties ?? new(),
                PropertyTypes = propertyTypes ?? new()
            };

        /// <summary>Create a structure rule.</summary>
        public static GrammarRule StructureRule(string id, string name, List<string> requiredSections,
            ViolationSeverity severity = ViolationSeverity.Error) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.Structure, RequiredSections = requiredSections, Severity = severity };

        /// <summary>Create a custom rule.</summary>
        public static GrammarRule CustomRule(string id, string name, Func<string, (bool valid, string message)> validator,
            ViolationSeverity severity = ViolationSeverity.Error) =>
            new() { Id = id, Name = name, Type = GrammarRuleType.Custom, CustomValidator = validator, Severity = severity };

        // ── Text Report ─────────────────────────────────────────────────

        /// <summary>Generate a text report from a validation result.</summary>
        public static string GenerateReport(GrammarValidationResult result)
        {
            var lines = new List<string>
            {
                "═══ Grammar Validation Report ═══",
                $"Rule Set: {result.RuleSetName}",
                $"Status: {(result.IsValid ? "✅ VALID" : "❌ INVALID")}",
                $"Score: {result.Score}%  ({result.PassedRules}/{result.TotalRules} rules passed)",
                ""
            };

            if (result.Violations.Count > 0)
            {
                lines.Add("── Violations ──");
                foreach (var v in result.Violations)
                {
                    var icon = v.Severity switch
                    {
                        ViolationSeverity.Error => "❌",
                        ViolationSeverity.Warning => "⚠️",
                        _ => "ℹ️"
                    };
                    lines.Add($"  {icon} [{v.Severity}] {v.RuleName} ({v.RuleId})");
                    lines.Add($"     {v.Message}");
                    if (!string.IsNullOrEmpty(v.Expected))
                        lines.Add($"     Expected: {v.Expected}");
                    if (!string.IsNullOrEmpty(v.Actual))
                        lines.Add($"     Actual:   {v.Actual}");
                }
                lines.Add("");
            }

            if (result.PassedRuleIds.Count > 0)
            {
                lines.Add($"── Passed Rules ({result.PassedRuleIds.Count}) ──");
                foreach (var id in result.PassedRuleIds)
                    lines.Add($"  ✅ {id}");
            }

            return string.Join("\n", lines);
        }

        /// <summary>Generate a batch report.</summary>
        public static string GenerateBatchReport(BatchValidationResult batch)
        {
            var lines = new List<string>
            {
                "═══ Batch Validation Report ═══",
                $"Total: {batch.TotalResponses} | Valid: {batch.ValidCount} | Invalid: {batch.InvalidCount}",
                $"Average Score: {batch.AverageScore}%",
                ""
            };

            if (batch.CommonViolations.Count > 0)
            {
                lines.Add("── Most Common Violations ──");
                foreach (var cv in batch.CommonViolations.Take(10))
                    lines.Add($"  • {cv.RuleName} ({cv.RuleId}): {cv.Occurrences}x ({cv.Percentage}%)");
                lines.Add("");
            }

            for (int i = 0; i < batch.Results.Count; i++)
            {
                var r = batch.Results[i];
                var status = r.IsValid ? "✅" : "❌";
                lines.Add($"  [{i + 1}] {status} Score: {r.Score}% | Violations: {r.FailedRules}");
            }

            return string.Join("\n", lines);
        }

        // ── Serialization ──────────────────────────────────────────────

        /// <summary>Export rule sets to JSON.</summary>
        public string ExportRuleSets()
        {
            return JsonSerializer.Serialize(_ruleSets, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Import rule sets from JSON (merges with existing).</summary>
        public int ImportRuleSets(string json)
        {
            SerializationGuards.ValidateJsonInput(json);

            var imported = JsonSerializer.Deserialize<Dictionary<string, GrammarRuleSet>>(json)
                ?? new();
            foreach (var kv in imported)
                _ruleSets[kv.Key] = kv.Value;
            return imported.Count;
        }

        // ── Private: Individual Rule Validation ─────────────────────────

        private GrammarViolation? ValidateRule(string response, GrammarRule rule)
        {
            return rule.Type switch
            {
                GrammarRuleType.Regex => ValidateRegex(response, rule),
                GrammarRuleType.JsonSchema => ValidateJsonSchema(response, rule),
                GrammarRuleType.Enum => ValidateEnum(response, rule),
                GrammarRuleType.StartsWith => ValidateStartsWith(response, rule),
                GrammarRuleType.EndsWith => ValidateEndsWith(response, rule),
                GrammarRuleType.Contains => ValidateContains(response, rule),
                GrammarRuleType.NotContains => ValidateNotContains(response, rule),
                GrammarRuleType.Length => ValidateLength(response, rule),
                GrammarRuleType.LineCount => ValidateLineCount(response, rule),
                GrammarRuleType.Structure => ValidateStructure(response, rule),
                GrammarRuleType.Custom => ValidateCustom(response, rule),
                _ => null
            };
        }

        private GrammarViolation? ValidateRegex(string response, GrammarRule rule)
        {
            try
            {
                var opts = rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                if (Regex.IsMatch(response, rule.Pattern, opts, TimeSpan.FromMilliseconds(500)))
                    return null;
                return MakeViolation(rule, "Response does not match pattern.", rule.Pattern, Truncate(response, 100));
            }
            catch (RegexParseException ex)
            {
                return MakeViolation(rule, $"Invalid regex pattern: {ex.Message}", rule.Pattern, "");
            }
        }

        private GrammarViolation? ValidateJsonSchema(string response, GrammarRule rule)
        {
            // Try to extract JSON from response (may be wrapped in markdown code blocks)
            var json = ExtractJson(response);
            if (json == null)
                return MakeViolation(rule, "Response is not valid JSON.", "Valid JSON", Truncate(response, 100));

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check required properties
                foreach (var prop in rule.RequiredProperties)
                {
                    if (!TryGetNestedProperty(root, prop, out _))
                        return MakeViolation(rule, $"Missing required property: {prop}", prop, "not found");
                }

                // Check property types
                foreach (var kv in rule.PropertyTypes)
                {
                    if (TryGetNestedProperty(root, kv.Key, out var element))
                    {
                        var actualType = element.ValueKind switch
                        {
                            JsonValueKind.String => "string",
                            JsonValueKind.Number => "number",
                            JsonValueKind.True or JsonValueKind.False => "boolean",
                            JsonValueKind.Array => "array",
                            JsonValueKind.Object => "object",
                            JsonValueKind.Null => "null",
                            _ => "unknown"
                        };
                        if (!actualType.Equals(kv.Value, StringComparison.OrdinalIgnoreCase))
                            return MakeViolation(rule, $"Property '{kv.Key}' has wrong type.", kv.Value, actualType);
                    }
                }

                return null;
            }
            catch (JsonException ex)
            {
                return MakeViolation(rule, $"Invalid JSON: {ex.Message}", "Valid JSON", Truncate(response, 100));
            }
        }

        private GrammarViolation? ValidateEnum(string response, GrammarRule rule)
        {
            var trimmed = response.Trim();
            var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (rule.AllowedValues.Any(v => v.Equals(trimmed, comparison)))
                return null;
            var allowed = string.Join(", ", rule.AllowedValues.Take(10));
            return MakeViolation(rule, "Response is not one of the allowed values.",
                $"One of: [{allowed}]", Truncate(trimmed, 60));
        }

        private GrammarViolation? ValidateStartsWith(string response, GrammarRule rule)
        {
            var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (response.StartsWith(rule.Pattern, comparison))
                return null;
            return MakeViolation(rule, $"Response must start with: {rule.Pattern}",
                rule.Pattern, Truncate(response, 60));
        }

        private GrammarViolation? ValidateEndsWith(string response, GrammarRule rule)
        {
            var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (response.EndsWith(rule.Pattern, comparison))
                return null;
            return MakeViolation(rule, $"Response must end with: {rule.Pattern}",
                rule.Pattern, Truncate(response, 60));
        }

        private GrammarViolation? ValidateContains(string response, GrammarRule rule)
        {
            var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (response.Contains(rule.Pattern, comparison))
                return null;
            return MakeViolation(rule, $"Response must contain: {rule.Pattern}",
                rule.Pattern, "not found");
        }

        private GrammarViolation? ValidateNotContains(string response, GrammarRule rule)
        {
            var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!response.Contains(rule.Pattern, comparison))
                return null;
            var pos = response.IndexOf(rule.Pattern, comparison);
            return MakeViolation(rule, $"Response must not contain: {rule.Pattern}",
                $"Absent: {rule.Pattern}", $"Found at position {pos}") with { Position = pos };
        }

        private GrammarViolation? ValidateLength(string response, GrammarRule rule)
        {
            var len = response.Length;
            if (rule.Min.HasValue && len < rule.Min.Value)
                return MakeViolation(rule, $"Response too short ({len} chars).",
                    $"≥{rule.Min}", len.ToString());
            if (rule.Max.HasValue && len > rule.Max.Value)
                return MakeViolation(rule, $"Response too long ({len} chars).",
                    $"≤{rule.Max}", len.ToString());
            return null;
        }

        private GrammarViolation? ValidateLineCount(string response, GrammarRule rule)
        {
            var lines = response.Split('\n').Length;
            if (rule.Min.HasValue && lines < rule.Min.Value)
                return MakeViolation(rule, $"Too few lines ({lines}).",
                    $"≥{rule.Min} lines", lines.ToString());
            if (rule.Max.HasValue && lines > rule.Max.Value)
                return MakeViolation(rule, $"Too many lines ({lines}).",
                    $"≤{rule.Max} lines", lines.ToString());
            return null;
        }

        private GrammarViolation? ValidateStructure(string response, GrammarRule rule)
        {
            foreach (var section in rule.RequiredSections)
            {
                // Look for markdown headings or "SECTION:" patterns
                var headingPattern = $@"(?m)^#+\s+{Regex.Escape(section)}|^{Regex.Escape(section)}\s*[:：]";
                if (!Regex.IsMatch(response, headingPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500)))
                    return MakeViolation(rule, $"Missing required section: {section}",
                        section, "not found");
            }
            return null;
        }

        private GrammarViolation? ValidateCustom(string response, GrammarRule rule)
        {
            if (rule.CustomValidator == null)
                return MakeViolation(rule, "Custom rule has no validator function.", "", "");

            var (valid, message) = rule.CustomValidator(response);
            if (valid) return null;
            return MakeViolation(rule, message, "", "");
        }

        // ── Private: Helpers ────────────────────────────────────────────

        private static GrammarViolation MakeViolation(GrammarRule rule, string message, string expected, string actual) =>
            new()
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Severity = rule.Severity,
                Message = message,
                Expected = expected,
                Actual = actual
            };

        private static string? ExtractJson(string text)
        {
            // Try raw parse first
            text = text.Trim();
            if ((text.StartsWith('{') && text.EndsWith('}')) || (text.StartsWith('[') && text.EndsWith(']')))
            {
                try { JsonDocument.Parse(text); return text; } catch { }
            }

            // Try extracting from markdown code block
            var match = Regex.Match(text, @"```(?:json)?\s*\n?([\s\S]*?)\n?\s*```", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            if (match.Success)
            {
                var inner = match.Groups[1].Value.Trim();
                try { JsonDocument.Parse(inner); return inner; } catch { }
            }

            return null;
        }

        private static bool TryGetNestedProperty(JsonElement element, string path, out JsonElement result)
        {
            result = element;
            var parts = path.Split('.');
            foreach (var part in parts)
            {
                if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(part, out var next))
                {
                    result = default;
                    return false;
                }
                result = next;
            }
            return true;
        }

        private static string Truncate(string text, int maxLen) =>
            text.Length <= maxLen ? text : text[..maxLen] + "…";

        private FixSuggestion GenerateSuggestion(string response, GrammarRule rule, GrammarViolation violation)
        {
            return rule.Type switch
            {
                GrammarRuleType.StartsWith => new FixSuggestion
                {
                    RuleId = rule.Id,
                    Description = $"Add prefix: {rule.Pattern}",
                    SuggestedFix = rule.Pattern + response,
                    AutoFixable = true
                },
                GrammarRuleType.EndsWith => new FixSuggestion
                {
                    RuleId = rule.Id,
                    Description = $"Add suffix: {rule.Pattern}",
                    SuggestedFix = response + rule.Pattern,
                    AutoFixable = true
                },
                GrammarRuleType.Length when rule.Max.HasValue && response.Length > rule.Max.Value => new FixSuggestion
                {
                    RuleId = rule.Id,
                    Description = $"Truncate to {rule.Max} characters",
                    SuggestedFix = response[..rule.Max.Value],
                    AutoFixable = true
                },
                GrammarRuleType.Enum => new FixSuggestion
                {
                    RuleId = rule.Id,
                    Description = $"Response must be one of: {string.Join(", ", rule.AllowedValues.Take(5))}",
                    SuggestedFix = "",
                    AutoFixable = false
                },
                GrammarRuleType.NotContains => new FixSuggestion
                {
                    RuleId = rule.Id,
                    Description = $"Remove forbidden text: {rule.Pattern}",
                    SuggestedFix = response.Replace(rule.Pattern, "",
                        rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal).Trim(),
                    AutoFixable = true
                },
                _ => new FixSuggestion
                {
                    RuleId = rule.Id,
                    Description = violation.Message,
                    SuggestedFix = "",
                    AutoFixable = false
                }
            };
        }

        // ── Presets ─────────────────────────────────────────────────────

        private static Dictionary<string, GrammarRuleSet> BuildPresets() => new()
        {
            ["json-response"] = new GrammarRuleSet
            {
                Name = "json-response",
                Description = "Validates response is well-formed JSON",
                Rules = new()
                {
                    JsonSchemaRule("json-valid", "Valid JSON", new()),
                    NotContainsRule("no-markdown-fence", "No Markdown Fences", "```", ViolationSeverity.Warning),
                }
            },
            ["yes-no"] = new GrammarRuleSet
            {
                Name = "yes-no",
                Description = "Response must be exactly 'yes' or 'no'",
                Rules = new()
                {
                    EnumRule("yes-no", "Yes/No Answer", new[] { "yes", "no", "Yes", "No", "YES", "NO" }),
                    LengthRule("short", "Short Response", max: 3)
                }
            },
            ["classification"] = new GrammarRuleSet
            {
                Name = "classification",
                Description = "Single-word or single-line classification output",
                Rules = new()
                {
                    LineCountRule("single-line", "Single Line", max: 1),
                    LengthRule("reasonable-length", "Reasonable Length", min: 1, max: 100),
                    NotContainsRule("no-explanation", "No Explanation", "because", ViolationSeverity.Warning, ignoreCase: true),
                }
            },
            ["markdown-doc"] = new GrammarRuleSet
            {
                Name = "markdown-doc",
                Description = "Validates markdown document structure",
                Rules = new()
                {
                    RegexRule("has-heading", "Has Heading", @"(?m)^#\s+\S"),
                    LengthRule("min-length", "Minimum Length", min: 50),
                    LineCountRule("min-lines", "Minimum Lines", min: 3),
                }
            },
            ["bullet-list"] = new GrammarRuleSet
            {
                Name = "bullet-list",
                Description = "Response is a bullet-point list",
                Rules = new()
                {
                    RegexRule("has-bullets", "Has Bullets", @"(?m)^[\-\*•]\s+\S"),
                    LineCountRule("min-items", "Minimum Items", min: 2),
                }
            },
            ["code-block"] = new GrammarRuleSet
            {
                Name = "code-block",
                Description = "Response contains a fenced code block",
                Rules = new()
                {
                    ContainsRule("has-fence", "Has Code Fence", "```"),
                    RegexRule("fence-lang", "Language Tag", @"```\w+", ViolationSeverity.Warning),
                }
            }
        };
    }
}
