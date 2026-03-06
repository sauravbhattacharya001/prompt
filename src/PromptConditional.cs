namespace Prompt
{
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Represents an operator used in conditional expressions.
    /// </summary>
    public enum ConditionalOperator
    {
        /// <summary>Checks if a variable is set and non-empty.</summary>
        Exists,
        /// <summary>Checks if a variable equals a specific value (case-insensitive).</summary>
        Equals,
        /// <summary>Checks if a variable does not equal a specific value.</summary>
        NotEquals,
        /// <summary>Checks if a variable contains a substring.</summary>
        Contains,
        /// <summary>Checks if a variable starts with a prefix.</summary>
        StartsWith,
        /// <summary>Checks if a variable ends with a suffix.</summary>
        EndsWith,
        /// <summary>Checks if a variable matches a regex pattern.</summary>
        Matches
    }

    /// <summary>
    /// Represents a parsed conditional expression.
    /// </summary>
    public class ConditionalExpression
    {
        /// <summary>Gets the variable name being tested.</summary>
        [JsonPropertyName("variable")]
        public string Variable { get; init; } = "";

        /// <summary>Gets the operator for the condition.</summary>
        [JsonPropertyName("operator")]
        public ConditionalOperator Operator { get; init; }

        /// <summary>Gets the comparison value (for operators that need one).</summary>
        [JsonPropertyName("value")]
        public string? Value { get; init; }

        /// <summary>Gets whether the condition is negated.</summary>
        [JsonPropertyName("negated")]
        public bool Negated { get; init; }
    }

    /// <summary>
    /// Adds conditional logic (if/else/switch/case) to prompt templates.
    /// Enables dynamic prompt construction based on variable values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports the following syntax within template strings:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <c>{{#if variable}}...{{/if}}</c> — renders content if variable is set and non-empty
    /// </item>
    /// <item>
    /// <c>{{#if variable == "value"}}...{{/if}}</c> — renders if variable equals value
    /// </item>
    /// <item>
    /// <c>{{#if variable != "value"}}...{{else}}...{{/if}}</c> — if/else branching
    /// </item>
    /// <item>
    /// <c>{{#if variable contains "text"}}...{{/if}}</c> — substring check
    /// </item>
    /// <item>
    /// <c>{{#if variable startsWith "prefix"}}...{{/if}}</c> — prefix check
    /// </item>
    /// <item>
    /// <c>{{#if variable endsWith "suffix"}}...{{/if}}</c> — suffix check
    /// </item>
    /// <item>
    /// <c>{{#if variable matches "pattern"}}...{{/if}}</c> — regex match
    /// </item>
    /// <item>
    /// <c>{{#if !variable}}...{{/if}}</c> — negated existence check
    /// </item>
    /// <item>
    /// <c>{{#switch variable}}{{#case "val1"}}...{{#case "val2"}}...{{#default}}...{{/switch}}</c>
    /// — multi-way branching
    /// </item>
    /// </list>
    /// <para>
    /// Example usage:
    /// <code>
    /// var template = "{{#if tone == \"formal\"}}Dear Sir/Madam,{{else}}Hey there!{{/if}} " +
    ///                "Please help with {{topic}}.";
    /// var vars = new Dictionary&lt;string, string&gt; { ["tone"] = "formal", ["topic"] = "C#" };
    /// string result = PromptConditional.Render(template, vars);
    /// // → "Dear Sir/Madam, Please help with C#."
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptConditional
    {
        // Matches {{#if ...}}...{{else}}...{{/if}} blocks (outermost only)
        private static readonly Regex IfBlockPattern = new Regex(
            @"\{\{#if\s+(.+?)\}\}(.*?)\{\{/if\}\}",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Matches {{#switch variable}}...{{/switch}} blocks
        private static readonly Regex SwitchBlockPattern = new Regex(
            @"\{\{#switch\s+(\w+)\}\}(.*?)\{\{/switch\}\}",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Matches {{#case "value"}}...
        private static readonly Regex CasePattern = new Regex(
            @"\{\{#case\s+""([^""]*)""\}\}(.*?)(?=\{\{#case|\{\{#default\}\}|\z)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Matches {{#default}}...
        private static readonly Regex DefaultPattern = new Regex(
            @"\{\{#default\}\}(.*?)(?=\{\{#case|\z)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Condition expression parser: variable [op "value"]
        private static readonly Regex ConditionPattern = new Regex(
            @"^(!?)(\w+)(?:\s+(==|!=|contains|startsWith|endsWith|matches)\s+""([^""]*)"")?\s*$",
            RegexOptions.Compiled);

        /// <summary>
        /// Renders a template string by evaluating all conditional blocks
        /// (if/else/switch/case) against the provided variables.
        /// </summary>
        /// <param name="template">The template string with conditional blocks.</param>
        /// <param name="variables">Variable values to evaluate conditions against.</param>
        /// <returns>The rendered string with conditions resolved.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="template"/> or <paramref name="variables"/> is null.
        /// </exception>
        public static string Render(string template, IDictionary<string, string> variables)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (variables == null) throw new ArgumentNullException(nameof(variables));

            string result = template;

            // Process switch blocks first (they may contain nested ifs)
            result = ProcessSwitchBlocks(result, variables);

            // Process if blocks (may need multiple passes for nesting)
            int maxIterations = 10;
            for (int i = 0; i < maxIterations; i++)
            {
                string previous = result;
                result = ProcessIfBlocks(result, variables);
                if (result == previous) break;
            }

            return result;
        }

        /// <summary>
        /// Parses a conditional expression string into a <see cref="ConditionalExpression"/>.
        /// </summary>
        /// <param name="expression">The expression string (e.g., "variable == \"value\"").</param>
        /// <returns>The parsed expression, or null if the expression is invalid.</returns>
        public static ConditionalExpression? ParseExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            var match = ConditionPattern.Match(expression.Trim());
            if (!match.Success) return null;

            bool negated = match.Groups[1].Value == "!";
            string variable = match.Groups[2].Value;
            string? op = match.Groups[3].Success ? match.Groups[3].Value : null;
            string? value = match.Groups[4].Success ? match.Groups[4].Value : null;

            ConditionalOperator condOp = op switch
            {
                "==" => ConditionalOperator.Equals,
                "!=" => ConditionalOperator.NotEquals,
                "contains" => ConditionalOperator.Contains,
                "startsWith" => ConditionalOperator.StartsWith,
                "endsWith" => ConditionalOperator.EndsWith,
                "matches" => ConditionalOperator.Matches,
                _ => ConditionalOperator.Exists
            };

            return new ConditionalExpression
            {
                Variable = variable,
                Operator = condOp,
                Value = value,
                Negated = negated
            };
        }

        /// <summary>
        /// Evaluates a conditional expression against the provided variables.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="variables">The variable values.</param>
        /// <returns>True if the condition is met; false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="expression"/> or <paramref name="variables"/> is null.
        /// </exception>
        public static bool Evaluate(ConditionalExpression expression, IDictionary<string, string> variables)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (variables == null) throw new ArgumentNullException(nameof(variables));

            bool hasValue = variables.TryGetValue(expression.Variable, out string? varValue)
                            && !string.IsNullOrEmpty(varValue);

            bool result = expression.Operator switch
            {
                ConditionalOperator.Exists => hasValue,
                ConditionalOperator.Equals => hasValue &&
                    string.Equals(varValue, expression.Value, StringComparison.OrdinalIgnoreCase),
                ConditionalOperator.NotEquals => !hasValue ||
                    !string.Equals(varValue, expression.Value, StringComparison.OrdinalIgnoreCase),
                ConditionalOperator.Contains => hasValue &&
                    varValue!.Contains(expression.Value ?? "", StringComparison.OrdinalIgnoreCase),
                ConditionalOperator.StartsWith => hasValue &&
                    varValue!.StartsWith(expression.Value ?? "", StringComparison.OrdinalIgnoreCase),
                ConditionalOperator.EndsWith => hasValue &&
                    varValue!.EndsWith(expression.Value ?? "", StringComparison.OrdinalIgnoreCase),
                ConditionalOperator.Matches => hasValue &&
                    Regex.IsMatch(varValue!, expression.Value ?? ""),
                _ => false
            };

            return expression.Negated ? !result : result;
        }

        /// <summary>
        /// Extracts all conditional blocks from a template and returns their expressions.
        /// Useful for analyzing template logic before rendering.
        /// </summary>
        /// <param name="template">The template to analyze.</param>
        /// <returns>A list of conditional expressions found in the template.</returns>
        public static IReadOnlyList<ConditionalExpression> ExtractConditions(string template)
        {
            if (string.IsNullOrEmpty(template)) return Array.Empty<ConditionalExpression>();

            var conditions = new List<ConditionalExpression>();

            foreach (Match match in IfBlockPattern.Matches(template))
            {
                var expr = ParseExpression(match.Groups[1].Value);
                if (expr != null) conditions.Add(expr);
            }

            foreach (Match match in SwitchBlockPattern.Matches(template))
            {
                conditions.Add(new ConditionalExpression
                {
                    Variable = match.Groups[1].Value,
                    Operator = ConditionalOperator.Exists
                });
            }

            return conditions.AsReadOnly();
        }

        /// <summary>
        /// Extracts all variable names referenced in conditional expressions.
        /// </summary>
        /// <param name="template">The template to analyze.</param>
        /// <returns>A set of variable names used in conditions.</returns>
        public static IReadOnlySet<string> ExtractConditionalVariables(string template)
        {
            var conditions = ExtractConditions(template);
            var vars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in conditions) vars.Add(c.Variable);
            return vars;
        }

        /// <summary>
        /// Validates that a template's conditional syntax is well-formed.
        /// Returns a list of errors found.
        /// </summary>
        /// <param name="template">The template to validate.</param>
        /// <returns>A list of validation error messages. Empty if valid.</returns>
        public static IReadOnlyList<string> Validate(string template)
        {
            if (string.IsNullOrEmpty(template)) return Array.Empty<string>();

            var errors = new List<string>();

            // Check balanced if/endif
            int ifCount = Regex.Matches(template, @"\{\{#if\s").Count;
            int endifCount = Regex.Matches(template, @"\{\{/if\}\}").Count;
            if (ifCount != endifCount)
                errors.Add($"Mismatched if/endif: {ifCount} opening vs {endifCount} closing tags.");

            // Check balanced switch/endswitch
            int switchCount = Regex.Matches(template, @"\{\{#switch\s").Count;
            int endSwitchCount = Regex.Matches(template, @"\{\{/switch\}\}").Count;
            if (switchCount != endSwitchCount)
                errors.Add($"Mismatched switch/endswitch: {switchCount} opening vs {endSwitchCount} closing tags.");

            // Check for invalid condition expressions in if blocks
            foreach (Match match in IfBlockPattern.Matches(template))
            {
                string exprStr = match.Groups[1].Value;
                var expr = ParseExpression(exprStr);
                if (expr == null)
                    errors.Add($"Invalid condition expression: \"{exprStr}\"");
            }

            // Check switch blocks have at least one case
            foreach (Match match in SwitchBlockPattern.Matches(template))
            {
                string body = match.Groups[2].Value;
                if (!CasePattern.IsMatch(body))
                    errors.Add($"Switch on '{match.Groups[1].Value}' has no case clauses.");
            }

            return errors.AsReadOnly();
        }

        /// <summary>
        /// Serializes a template with its conditional metadata to JSON.
        /// </summary>
        /// <param name="template">The template string.</param>
        /// <returns>JSON string with template text, conditions, and variables.</returns>
        public static string ToJson(string template)
        {
            var data = new
            {
                template,
                conditions = ExtractConditions(template),
                conditionalVariables = ExtractConditionalVariables(template).ToList()
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        #region Private helpers

        private static string ProcessIfBlocks(string text, IDictionary<string, string> variables)
        {
            return IfBlockPattern.Replace(text, match =>
            {
                string exprStr = match.Groups[1].Value;
                string body = match.Groups[2].Value;

                var expr = ParseExpression(exprStr);
                if (expr == null) return match.Value; // invalid — leave as-is

                // Split on {{else}}
                string thenPart, elsePart;
                int elseIndex = FindElse(body);
                if (elseIndex >= 0)
                {
                    thenPart = body.Substring(0, elseIndex);
                    elsePart = body.Substring(elseIndex + "{{else}}".Length);
                }
                else
                {
                    thenPart = body;
                    elsePart = "";
                }

                bool conditionMet = Evaluate(expr, variables);
                return conditionMet ? thenPart : elsePart;
            });
        }

        private static int FindElse(string body)
        {
            // Find {{else}} that is not nested inside another {{#if}}
            int depth = 0;
            int i = 0;
            while (i < body.Length)
            {
                if (i + 5 <= body.Length && body.Substring(i, 5) == "{{#if")
                {
                    depth++;
                    i += 5;
                }
                else if (i + 7 <= body.Length && body.Substring(i, 7) == "{{/if}}")
                {
                    depth--;
                    i += 7;
                }
                else if (depth == 0 && i + 8 <= body.Length && body.Substring(i, 8) == "{{else}}")
                {
                    return i;
                }
                else
                {
                    i++;
                }
            }
            return -1;
        }

        private static string ProcessSwitchBlocks(string text, IDictionary<string, string> variables)
        {
            return SwitchBlockPattern.Replace(text, match =>
            {
                string variable = match.Groups[1].Value;
                string body = match.Groups[2].Value;

                variables.TryGetValue(variable, out string? varValue);
                varValue ??= "";

                // Try to match a case
                foreach (Match caseMatch in CasePattern.Matches(body))
                {
                    string caseValue = caseMatch.Groups[1].Value;
                    if (string.Equals(caseValue, varValue, StringComparison.OrdinalIgnoreCase))
                        return caseMatch.Groups[2].Value;
                }

                // Try default
                var defaultMatch = DefaultPattern.Match(body);
                if (defaultMatch.Success)
                    return defaultMatch.Groups[1].Value;

                return ""; // no matching case
            });
        }

        #endregion
    }
}
