namespace Prompt.Tests
{
    using Xunit;

    public class PromptConditionalTests
    {
        [Fact]
        public void Render_SimpleIfTrue_IncludesContent()
        {
            var vars = new Dictionary<string, string> { ["name"] = "Alice" };
            string result = PromptConditional.Render("Hello {{#if name}}{{name}}{{/if}}!", vars);
            Assert.Equal("Hello {{name}}!", result); // note: variable substitution is separate
        }

        [Fact]
        public void Render_SimpleIfFalse_ExcludesContent()
        {
            var vars = new Dictionary<string, string>();
            string result = PromptConditional.Render("Hello {{#if name}}friend{{/if}}world", vars);
            Assert.Equal("Hello world", result);
        }

        [Fact]
        public void Render_IfElse_TrueCase()
        {
            var vars = new Dictionary<string, string> { ["tone"] = "formal" };
            string template = "{{#if tone == \"formal\"}}Dear Sir{{else}}Hey{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Dear Sir", result);
        }

        [Fact]
        public void Render_IfElse_FalseCase()
        {
            var vars = new Dictionary<string, string> { ["tone"] = "casual" };
            string template = "{{#if tone == \"formal\"}}Dear Sir{{else}}Hey{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Hey", result);
        }

        [Fact]
        public void Render_NegatedCondition()
        {
            var vars = new Dictionary<string, string>();
            string template = "{{#if !debug}}Production mode{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Production mode", result);
        }

        [Fact]
        public void Render_NegatedCondition_WhenSet()
        {
            var vars = new Dictionary<string, string> { ["debug"] = "true" };
            string template = "{{#if !debug}}Production mode{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("", result);
        }

        [Fact]
        public void Render_ContainsOperator()
        {
            var vars = new Dictionary<string, string> { ["lang"] = "C# programming" };
            string template = "{{#if lang contains \"C#\"}}dotnet{{else}}other{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("dotnet", result);
        }

        [Fact]
        public void Render_StartsWithOperator()
        {
            var vars = new Dictionary<string, string> { ["model"] = "gpt-4-turbo" };
            string template = "{{#if model startsWith \"gpt\"}}OpenAI{{else}}Other{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("OpenAI", result);
        }

        [Fact]
        public void Render_EndsWithOperator()
        {
            var vars = new Dictionary<string, string> { ["file"] = "data.csv" };
            string template = "{{#if file endsWith \".csv\"}}CSV{{else}}Unknown{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("CSV", result);
        }

        [Fact]
        public void Render_MatchesOperator()
        {
            var vars = new Dictionary<string, string> { ["version"] = "v2.1.0" };
            string template = "{{#if version matches \"^v\\d+\\.\\d+\"}}Valid{{else}}Invalid{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Valid", result);
        }

        [Fact]
        public void Render_NotEqualsOperator()
        {
            var vars = new Dictionary<string, string> { ["env"] = "staging" };
            string template = "{{#if env != \"production\"}}Non-prod{{else}}Prod{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Non-prod", result);
        }

        [Fact]
        public void Render_SwitchCase_MatchesFirst()
        {
            var vars = new Dictionary<string, string> { ["role"] = "admin" };
            string template = "{{#switch role}}{{#case \"admin\"}}Full access{{#case \"user\"}}Limited{{#default}}None{{/switch}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Full access", result);
        }

        [Fact]
        public void Render_SwitchCase_MatchesSecond()
        {
            var vars = new Dictionary<string, string> { ["role"] = "user" };
            string template = "{{#switch role}}{{#case \"admin\"}}Full access{{#case \"user\"}}Limited{{#default}}None{{/switch}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Limited", result);
        }

        [Fact]
        public void Render_SwitchCase_Default()
        {
            var vars = new Dictionary<string, string> { ["role"] = "guest" };
            string template = "{{#switch role}}{{#case \"admin\"}}Full access{{#case \"user\"}}Limited{{#default}}None{{/switch}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("None", result);
        }

        [Fact]
        public void Render_SwitchCase_CaseInsensitive()
        {
            var vars = new Dictionary<string, string> { ["role"] = "ADMIN" };
            string template = "{{#switch role}}{{#case \"admin\"}}Full access{{#default}}None{{/switch}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("Full access", result);
        }

        [Fact]
        public void Render_NullTemplate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PromptConditional.Render(null!, new Dictionary<string, string>()));
        }

        [Fact]
        public void Render_NullVariables_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PromptConditional.Render("test", null!));
        }

        [Fact]
        public void Render_NoConditionals_ReturnsUnchanged()
        {
            var vars = new Dictionary<string, string> { ["x"] = "1" };
            string result = PromptConditional.Render("Hello world", vars);
            Assert.Equal("Hello world", result);
        }

        [Fact]
        public void ParseExpression_SimpleVariable()
        {
            var expr = PromptConditional.ParseExpression("name");
            Assert.NotNull(expr);
            Assert.Equal("name", expr!.Variable);
            Assert.Equal(ConditionalOperator.Exists, expr.Operator);
            Assert.False(expr.Negated);
        }

        [Fact]
        public void ParseExpression_NegatedVariable()
        {
            var expr = PromptConditional.ParseExpression("!debug");
            Assert.NotNull(expr);
            Assert.True(expr!.Negated);
            Assert.Equal("debug", expr.Variable);
        }

        [Fact]
        public void ParseExpression_EqualsOperator()
        {
            var expr = PromptConditional.ParseExpression("tone == \"formal\"");
            Assert.NotNull(expr);
            Assert.Equal(ConditionalOperator.Equals, expr!.Operator);
            Assert.Equal("formal", expr.Value);
        }

        [Fact]
        public void ParseExpression_InvalidExpression()
        {
            var expr = PromptConditional.ParseExpression("");
            Assert.Null(expr);
        }

        [Fact]
        public void ExtractConditions_FindsAll()
        {
            string template = "{{#if a}}x{{/if}} {{#if b == \"c\"}}y{{/if}} {{#switch d}}{{#case \"e\"}}z{{/switch}}";
            var conditions = PromptConditional.ExtractConditions(template);
            Assert.Equal(3, conditions.Count);
        }

        [Fact]
        public void ExtractConditionalVariables_ReturnsUniqueVars()
        {
            string template = "{{#if a}}x{{/if}} {{#if a == \"b\"}}y{{/if}} {{#switch c}}{{#case \"d\"}}z{{/switch}}";
            var vars = PromptConditional.ExtractConditionalVariables(template);
            Assert.Equal(2, vars.Count);
            Assert.Contains("a", vars);
            Assert.Contains("c", vars);
        }

        [Fact]
        public void Validate_ValidTemplate_NoErrors()
        {
            string template = "{{#if x}}y{{/if}} {{#switch z}}{{#case \"a\"}}b{{/switch}}";
            var errors = PromptConditional.Validate(template);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MismatchedIf_ReportsError()
        {
            string template = "{{#if x}}y";
            var errors = PromptConditional.Validate(template);
            Assert.Single(errors);
            Assert.Contains("Mismatched if/endif", errors[0]);
        }

        [Fact]
        public void Validate_SwitchWithoutCase_ReportsError()
        {
            string template = "{{#switch x}}no cases here{{/switch}}";
            var errors = PromptConditional.Validate(template);
            Assert.Single(errors);
            Assert.Contains("no case clauses", errors[0]);
        }

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            string template = "{{#if lang == \"python\"}}snake_case{{else}}camelCase{{/if}}";
            string json = PromptConditional.ToJson(template);
            Assert.Contains("\"template\"", json);
            Assert.Contains("\"conditions\"", json);
            Assert.Contains("\"conditionalVariables\"", json);
        }

        [Fact]
        public void Evaluate_NullExpression_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PromptConditional.Evaluate(null!, new Dictionary<string, string>()));
        }

        [Fact]
        public void Evaluate_NullVariables_Throws()
        {
            var expr = new ConditionalExpression { Variable = "x", Operator = ConditionalOperator.Exists };
            Assert.Throws<ArgumentNullException>(() =>
                PromptConditional.Evaluate(expr, null!));
        }

        [Fact]
        public void Render_EmptyVariable_TreatedAsFalse()
        {
            var vars = new Dictionary<string, string> { ["name"] = "" };
            string result = PromptConditional.Render("{{#if name}}yes{{else}}no{{/if}}", vars);
            Assert.Equal("no", result);
        }

        [Fact]
        public void Render_MultipleConditions()
        {
            var vars = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
            string template = "{{#if a}}A{{/if}}-{{#if b}}B{{/if}}-{{#if c}}C{{else}}noC{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("A-B-noC", result);
        }

        [Fact]
        public void Render_NestedIf()
        {
            var vars = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
            string template = "{{#if a}}outer{{#if b}}inner{{/if}}{{/if}}";
            string result = PromptConditional.Render(template, vars);
            Assert.Equal("outerinner", result);
        }
    }
}
