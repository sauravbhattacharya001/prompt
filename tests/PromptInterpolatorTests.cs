namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class PromptInterpolatorTests
    {
        private readonly PromptInterpolator _interpolator = new();

        [Fact]
        public void BasicInterpolation()
        {
            var result = _interpolator.Interpolate("Hello {{name}}", new Dictionary<string, object> { ["name"] = "World" });
            Assert.Equal("Hello World", result.Output);
            Assert.True(result.IsComplete);
            Assert.Contains("name", result.ResolvedVariables);
        }

        [Fact]
        public void MultipleVariables()
        {
            var vars = new Dictionary<string, object> { ["first"] = "Jane", ["last"] = "Doe" };
            var result = _interpolator.Interpolate("{{first}} {{last}}", vars);
            Assert.Equal("Jane Doe", result.Output);
            Assert.Equal(2, result.ResolvedVariables.Count);
        }

        [Fact]
        public void UnresolvedKeep()
        {
            var result = _interpolator.Interpolate("{{missing}}", new Dictionary<string, object>());
            Assert.Contains("missing", result.Output);
            Assert.False(result.IsComplete);
            Assert.Contains("missing", result.UnresolvedVariables);
        }

        [Fact]
        public void UnresolvedRemove()
        {
            var interp = new PromptInterpolator().OnUnresolved(UnresolvedBehavior.Remove);
            var result = interp.Interpolate("Hello {{missing}}!", new Dictionary<string, object>());
            Assert.Equal("Hello !", result.Output);
        }

        [Fact]
        public void UnresolvedThrow()
        {
            var interp = new PromptInterpolator().OnUnresolved(UnresolvedBehavior.Throw);
            Assert.Throws<KeyNotFoundException>(() =>
                interp.Interpolate("{{missing}}", new Dictionary<string, object>()));
        }

        [Fact]
        public void UpperFilter()
        {
            var result = _interpolator.Interpolate("{{name | upper}}", new Dictionary<string, object> { ["name"] = "hello" });
            Assert.Equal("HELLO", result.Output);
            Assert.Contains("upper", result.AppliedFilters);
        }

        [Fact]
        public void LowerFilter()
        {
            var result = _interpolator.Interpolate("{{name | lower}}", new Dictionary<string, object> { ["name"] = "HELLO" });
            Assert.Equal("hello", result.Output);
        }

        [Fact]
        public void TrimFilter()
        {
            var result = _interpolator.Interpolate("{{name | trim}}", new Dictionary<string, object> { ["name"] = "  hi  " });
            Assert.Equal("hi", result.Output);
        }

        [Fact]
        public void CapitalizeFilter()
        {
            var result = _interpolator.Interpolate("{{name | capitalize}}", new Dictionary<string, object> { ["name"] = "hello" });
            Assert.Equal("Hello", result.Output);
        }

        [Fact]
        public void TitleFilter()
        {
            var result = _interpolator.Interpolate("{{name | title}}", new Dictionary<string, object> { ["name"] = "hello world" });
            Assert.Equal("Hello World", result.Output);
        }

        [Fact]
        public void ReverseFilter()
        {
            var result = _interpolator.Interpolate("{{name | reverse}}", new Dictionary<string, object> { ["name"] = "abc" });
            Assert.Equal("cba", result.Output);
        }

        [Fact]
        public void TruncateFilter()
        {
            var result = _interpolator.Interpolate("{{name | truncate:5}}", new Dictionary<string, object> { ["name"] = "abcdefgh" });
            Assert.Equal("abcde", result.Output);
        }

        [Fact]
        public void DefaultFilter()
        {
            var result = _interpolator.Interpolate("{{name | default:Anonymous}}", new Dictionary<string, object> { ["name"] = "" });
            Assert.Equal("Anonymous", result.Output);
        }

        [Fact]
        public void ReplaceFilter()
        {
            var result = _interpolator.Interpolate("{{text | replace:foo:bar}}", new Dictionary<string, object> { ["text"] = "foo baz foo" });
            Assert.Equal("bar baz bar", result.Output);
        }

        [Fact]
        public void RepeatFilter()
        {
            var result = _interpolator.Interpolate("{{text | repeat:3}}", new Dictionary<string, object> { ["text"] = "ha" });
            Assert.Equal("hahaha", result.Output);
        }

        [Fact]
        public void PrefixFilter()
        {
            var result = _interpolator.Interpolate("{{name | prefix:Mr.}}", new Dictionary<string, object> { ["name"] = "Smith" });
            Assert.Equal("Mr.Smith", result.Output);
        }

        [Fact]
        public void SuffixFilter()
        {
            var result = _interpolator.Interpolate("{{name | suffix:!}}", new Dictionary<string, object> { ["name"] = "Hello" });
            Assert.Equal("Hello!", result.Output);
        }

        [Fact]
        public void PluralizeFilter()
        {
            var vars1 = new Dictionary<string, object> { ["count"] = "1" };
            var vars3 = new Dictionary<string, object> { ["count"] = "3" };
            Assert.Equal("item", _interpolator.Interpolate("{{count | pluralize:item:items}}", vars1).Output);
            Assert.Equal("items", _interpolator.Interpolate("{{count | pluralize:item:items}}", vars3).Output);
        }

        [Fact]
        public void FormatNumberFilter()
        {
            var result = _interpolator.Interpolate("{{price | format_number:2}}", new Dictionary<string, object> { ["price"] = 1234.5 });
            Assert.Equal("1,234.50", result.Output);
        }

        [Fact]
        public void FormatDateFilter()
        {
            var result = _interpolator.Interpolate("{{date | format_date:yyyy-MM-dd}}", new Dictionary<string, object> { ["date"] = "2026-03-08T15:00:00" });
            Assert.Equal("2026-03-08", result.Output);
        }

        [Fact]
        public void Base64RoundTrip()
        {
            var vars = new Dictionary<string, object> { ["text"] = "Hello!" };
            var encoded = _interpolator.Interpolate("{{text | base64_encode}}", vars).Output;
            var decoded = _interpolator.Interpolate("{{text | base64_decode}}", new Dictionary<string, object> { ["text"] = encoded }).Output;
            Assert.Equal("Hello!", decoded);
        }

        [Fact]
        public void UrlEncodeFilter()
        {
            var result = _interpolator.Interpolate("{{url | url_encode}}", new Dictionary<string, object> { ["url"] = "hello world&foo=bar" });
            Assert.Equal("hello%20world%26foo%3Dbar", result.Output);
        }

        [Fact]
        public void WordcountFilter()
        {
            var result = _interpolator.Interpolate("{{text | wordcount}}", new Dictionary<string, object> { ["text"] = "one two three" });
            Assert.Equal("3", result.Output);
        }

        [Fact]
        public void CharcountFilter()
        {
            var result = _interpolator.Interpolate("{{text | charcount}}", new Dictionary<string, object> { ["text"] = "hello" });
            Assert.Equal("5", result.Output);
        }

        [Fact]
        public void InitialsFilter()
        {
            var result = _interpolator.Interpolate("{{name | initials}}", new Dictionary<string, object> { ["name"] = "John Fitzgerald Kennedy" });
            Assert.Equal("JFK", result.Output);
        }

        [Fact]
        public void SlugFilter()
        {
            var result = _interpolator.Interpolate("{{title | slug}}", new Dictionary<string, object> { ["title"] = "Hello World! 2026" });
            Assert.Equal("hello-world-2026", result.Output);
        }

        [Fact]
        public void EllipsisFilter()
        {
            var result = _interpolator.Interpolate("{{text | ellipsis:10}}", new Dictionary<string, object> { ["text"] = "A very long string" });
            Assert.Equal("A very...", result.Output);
        }

        [Fact]
        public void EllipsisShortString()
        {
            var result = _interpolator.Interpolate("{{text | ellipsis:50}}", new Dictionary<string, object> { ["text"] = "Short" });
            Assert.Equal("Short", result.Output);
        }

        [Fact]
        public void ChainedFilters()
        {
            var result = _interpolator.Interpolate("{{name | trim | upper}}", new Dictionary<string, object> { ["name"] = "  hello  " });
            Assert.Equal("HELLO", result.Output);
            Assert.Equal(2, result.AppliedFilters.Count);
        }

        [Fact]
        public void PadLeftFilter()
        {
            var result = _interpolator.Interpolate("{{num | pad_left:5:0}}", new Dictionary<string, object> { ["num"] = "42" });
            Assert.Equal("00042", result.Output);
        }

        [Fact]
        public void PadRightFilter()
        {
            var result = _interpolator.Interpolate("{{text | pad_right:8:.}}", new Dictionary<string, object> { ["text"] = "hi" });
            Assert.Equal("hi......", result.Output);
        }

        [Fact]
        public void JsonFilter()
        {
            var result = _interpolator.Interpolate("{{text | json}}", new Dictionary<string, object> { ["text"] = "hello" });
            Assert.Equal("\"hello\"", result.Output);
        }

        [Fact]
        public void CustomFilter()
        {
            var interp = new PromptInterpolator();
            interp.RegisterFilter("shout", (input, _) => input.ToUpper() + "!!!");
            var result = interp.Interpolate("{{msg | shout}}", new Dictionary<string, object> { ["msg"] = "hey" });
            Assert.Equal("HEY!!!", result.Output);
        }

        [Fact]
        public void CustomFilterWithArgs()
        {
            var interp = new PromptInterpolator();
            interp.RegisterFilter("wrap", (input, args) =>
                args.Length >= 2 ? $"{args[0]}{input}{args[1]}" : $"[{input}]");
            var result = interp.Interpolate("{{text | wrap:<:>}}", new Dictionary<string, object> { ["text"] = "hi" });
            Assert.Equal("<hi>", result.Output);
        }

        [Fact]
        public void CustomDelimiters()
        {
            var interp = new PromptInterpolator().WithDelimiters("<%", "%>");
            var result = interp.Interpolate("Hello <%name%>!", new Dictionary<string, object> { ["name"] = "World" });
            Assert.Equal("Hello World!", result.Output);
        }

        [Fact]
        public void ExtractVariables()
        {
            var vars = _interpolator.ExtractVariables("{{name}} is {{age}} and {{name}}");
            Assert.Equal(2, vars.Count);
            Assert.Contains("name", vars);
            Assert.Contains("age", vars);
        }

        [Fact]
        public void ValidateTemplate()
        {
            var issues = _interpolator.Validate("{{ok}} {{bad | unknown_filter}}", new Dictionary<string, object> { ["ok"] = "yes" });
            Assert.Contains(issues, i => i.Contains("unknown_filter"));
            Assert.Contains(issues, i => i.Contains("bad"));
        }

        [Fact]
        public void ValidateMismatchedDelimiters()
        {
            var issues = _interpolator.Validate("{{ok}} {{ unclosed");
            Assert.Contains(issues, i => i.Contains("Mismatched"));
        }

        [Fact]
        public void ToJsonReport()
        {
            var result = _interpolator.Interpolate("{{name | upper}}", new Dictionary<string, object> { ["name"] = "test" });
            var json = _interpolator.ToJson(result);
            Assert.Contains("isComplete", json);
            Assert.Contains("TEST", json);
        }

        [Fact]
        public void SummaryString()
        {
            var result = _interpolator.Interpolate("{{a}} {{b}}", new Dictionary<string, object> { ["a"] = "x" });
            Assert.Contains("Resolved: 1", result.Summary);
            Assert.Contains("Unresolved: 1", result.Summary);
        }

        [Fact]
        public void EmptyTemplate()
        {
            var result = _interpolator.Interpolate("", new Dictionary<string, object>());
            Assert.Equal("", result.Output);
            Assert.True(result.IsComplete);
        }

        [Fact]
        public void NullTemplate()
        {
            Assert.Throws<ArgumentNullException>(() => _interpolator.Interpolate(null!, new Dictionary<string, object>()));
        }

        [Fact]
        public void NullVariables()
        {
            var result = _interpolator.Interpolate("{{x}}", (Dictionary<string, object>)null!);
            Assert.False(result.IsComplete);
        }

        [Fact]
        public void StringDictionaryOverload()
        {
            var result = _interpolator.Interpolate("{{name}}", new Dictionary<string, string> { ["name"] = "hi" });
            Assert.Equal("hi", result.Output);
        }

        [Fact]
        public void ListFiltersReturnsAll()
        {
            var interp = new PromptInterpolator();
            interp.RegisterFilter("my_filter", s => s);
            var filters = interp.ListFilters();
            Assert.Contains("upper", filters);
            Assert.Contains("slug", filters);
            Assert.Contains("my_filter", filters);
        }

        [Fact]
        public void DateTimeObjectVariable()
        {
            var dt = new DateTime(2026, 3, 8, 15, 0, 0);
            var result = _interpolator.Interpolate("{{date | format_date:yyyy-MM-dd}}", new Dictionary<string, object> { ["date"] = dt });
            Assert.Equal("2026-03-08", result.Output);
        }

        [Fact]
        public void WarningOnBadFilterInput()
        {
            var result = _interpolator.Interpolate("{{text | base64_decode}}", new Dictionary<string, object> { ["text"] = "not-base64!!!" });
            Assert.True(result.Warnings.Count > 0);
        }

        [Fact]
        public void InvalidDelimiterThrows()
        {
            Assert.Throws<ArgumentException>(() => new PromptInterpolator().WithDelimiters("", "}}"));
        }

        [Fact]
        public void EmptyFilterNameThrows()
        {
            Assert.Throws<ArgumentException>(() => _interpolator.RegisterFilter("", s => s));
        }

        [Fact]
        public void NullFilterHandlerThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _interpolator.RegisterFilter("x", (Func<string, string>)null!));
        }

        [Fact]
        public void CustomFilterOverridesBuiltIn()
        {
            var interp = new PromptInterpolator();
            interp.RegisterFilter("upper", (input, _) => "CUSTOM:" + input);
            var result = interp.Interpolate("{{x | upper}}", new Dictionary<string, object> { ["x"] = "test" });
            Assert.Equal("CUSTOM:test", result.Output);
        }

        [Fact]
        public void NoFiltersPlainText()
        {
            var result = _interpolator.Interpolate("No variables here", new Dictionary<string, object>());
            Assert.Equal("No variables here", result.Output);
            Assert.True(result.IsComplete);
            Assert.Empty(result.ResolvedVariables);
        }
    }
}
