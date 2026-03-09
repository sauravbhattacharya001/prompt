namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Result of interpolating a prompt template with variables and pipe filters.
    /// </summary>
    public class InterpolationResult
    {
        /// <summary>Gets the original template text.</summary>
        public string Template { get; internal set; } = "";

        /// <summary>Gets the interpolated output text.</summary>
        public string Output { get; internal set; } = "";

        /// <summary>Gets variables that were successfully resolved.</summary>
        public List<string> ResolvedVariables { get; internal set; } = new();

        /// <summary>Gets variables that were referenced but not provided.</summary>
        public List<string> UnresolvedVariables { get; internal set; } = new();

        /// <summary>Gets the filters that were applied during interpolation.</summary>
        public List<string> AppliedFilters { get; internal set; } = new();

        /// <summary>Gets any warnings generated during interpolation.</summary>
        public List<string> Warnings { get; internal set; } = new();

        /// <summary>Gets whether all variables were resolved.</summary>
        public bool IsComplete => UnresolvedVariables.Count == 0;

        /// <summary>Gets a summary of the interpolation.</summary>
        public string Summary =>
            $"Resolved: {ResolvedVariables.Count}, Unresolved: {UnresolvedVariables.Count}, " +
            $"Filters applied: {AppliedFilters.Count}, Warnings: {Warnings.Count}";
    }

    /// <summary>
    /// Behavior when a variable is not found in the provided values.
    /// </summary>
    public enum UnresolvedBehavior
    {
        /// <summary>Leave the placeholder as-is in the output.</summary>
        Keep,
        /// <summary>Remove the placeholder from the output.</summary>
        Remove,
        /// <summary>Throw an exception.</summary>
        Throw
    }

    /// <summary>
    /// Advanced template variable interpolation with chainable pipe filters.
    /// <para>
    /// Supports <c>{{variable}}</c> and <c>{{variable | filter1 | filter2:arg}}</c> syntax.
    /// </para>
    /// <para>
    /// Built-in filters: upper, lower, trim, capitalize, title, reverse,
    /// truncate:N, pad_left:N, pad_right:N, default:value, replace:old:new,
    /// repeat:N, prefix:text, suffix:text, pluralize:singular:plural,
    /// format_number:decimals, format_date:format, base64_encode, base64_decode,
    /// url_encode, json, wordcount, charcount, initials, slug, ellipsis:N.
    /// </para>
    /// </summary>
    public class PromptInterpolator
    {
        private readonly Dictionary<string, Func<string, string[], string>> _customFilters = new();
        private UnresolvedBehavior _unresolvedBehavior = UnresolvedBehavior.Keep;
        private string _openDelimiter = "{{";
        private string _closeDelimiter = "}}";

        // Cached compiled regex — rebuilt only when delimiters change.
        private Regex? _cachedPattern;
        private string _cachedPatternKey = "";

        /// <summary>
        /// Built-in filter names, allocated once and reused across all
        /// <see cref="IsKnownFilter"/> calls instead of allocating a new
        /// <see cref="HashSet{T}"/> on every invocation.
        /// </summary>
        private static readonly HashSet<string> BuiltInFilters = new(StringComparer.OrdinalIgnoreCase)
        {
            "upper", "lower", "trim", "capitalize", "title", "reverse",
            "truncate", "pad_left", "pad_right", "default", "replace",
            "repeat", "prefix", "suffix", "pluralize",
            "format_number", "format_date", "base64_encode", "base64_decode",
            "url_encode", "json", "wordcount", "charcount", "initials",
            "slug", "ellipsis"
        };

        /// <summary>
        /// Returns a compiled <see cref="Regex"/> for the current delimiters,
        /// caching and reusing it as long as the delimiters haven't changed.
        /// Compiled regexes JIT to IL, making construction ~100× slower than
        /// interpreted mode; caching eliminates that overhead on repeat calls.
        /// </summary>
        private Regex BuildPattern()
        {
            var key = _openDelimiter + "|" + _closeDelimiter;
            if (_cachedPattern != null && _cachedPatternKey == key)
                return _cachedPattern;

            _cachedPattern = new Regex(
                Regex.Escape(_openDelimiter) + @"\s*(.+?)\s*" + Regex.Escape(_closeDelimiter),
                RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
            _cachedPatternKey = key;
            return _cachedPattern;
        }

        /// <summary>Sets the behavior for unresolved variables.</summary>
        public PromptInterpolator OnUnresolved(UnresolvedBehavior behavior)
        {
            _unresolvedBehavior = behavior;
            return this;
        }

        /// <summary>Sets custom delimiters (e.g., &lt;% %&gt;) and invalidates the cached pattern.</summary>
        public PromptInterpolator WithDelimiters(string open, string close)
        {
            if (string.IsNullOrEmpty(open)) throw new ArgumentException("Open delimiter cannot be empty.", nameof(open));
            if (string.IsNullOrEmpty(close)) throw new ArgumentException("Close delimiter cannot be empty.", nameof(close));
            _openDelimiter = open;
            _closeDelimiter = close;
            _cachedPattern = null; // force rebuild on next use
            return this;
        }

        /// <summary>Registers a custom filter function.</summary>
        public PromptInterpolator RegisterFilter(string name, Func<string, string[], string> handler)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Filter name cannot be empty.", nameof(name));
            _customFilters[name.ToLowerInvariant()] = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>Registers a simple custom filter with no arguments.</summary>
        public PromptInterpolator RegisterFilter(string name, Func<string, string> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return RegisterFilter(name, (input, _) => handler(input));
        }

        /// <summary>Interpolates a template with the given variables.</summary>
        public InterpolationResult Interpolate(string template, Dictionary<string, object> variables)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            variables ??= new Dictionary<string, object>();

            var result = new InterpolationResult { Template = template };
            var pattern = BuildPattern();

            var output = pattern.Replace(template, match =>
            {
                var expression = match.Groups[1].Value;
                return ProcessExpression(expression, variables, result);
            });

            result.Output = output;
            return result;
        }

        /// <summary>Interpolates a template with string-only variables (convenience overload).</summary>
        public InterpolationResult Interpolate(string template, Dictionary<string, string> variables)
        {
            var objVars = new Dictionary<string, object>();
            if (variables != null)
                foreach (var kv in variables) objVars[kv.Key] = kv.Value;
            return Interpolate(template, objVars);
        }

        /// <summary>Extracts all variable names from a template.</summary>
        public List<string> ExtractVariables(string template)
        {
            if (template == null) return new List<string>();
            var pattern = BuildPattern();
            var vars = new HashSet<string>();
            foreach (Match m in pattern.Matches(template))
            {
                var expr = m.Groups[1].Value;
                var varName = expr.Split('|')[0].Trim();
                vars.Add(varName);
            }
            return vars.ToList();
        }

        /// <summary>Lists all available filter names (built-in + custom).</summary>
        public List<string> ListFilters()
        {
            var builtIn = new List<string>
            {
                "upper", "lower", "trim", "capitalize", "title", "reverse",
                "truncate", "pad_left", "pad_right", "default", "replace",
                "repeat", "prefix", "suffix", "pluralize",
                "format_number", "format_date", "base64_encode", "base64_decode",
                "url_encode", "json", "wordcount", "charcount", "initials",
                "slug", "ellipsis"
            };
            builtIn.AddRange(_customFilters.Keys);
            return builtIn;
        }

        /// <summary>Validates a template, returning any issues found.</summary>
        public List<string> Validate(string template, Dictionary<string, object>? variables = null)
        {
            var issues = new List<string>();
            if (template == null) { issues.Add("Template is null."); return issues; }

            var pattern = BuildPattern();
            foreach (Match m in pattern.Matches(template))
            {
                var expr = m.Groups[1].Value;
                var parts = SplitPipes(expr);
                var varName = parts[0].Trim();
                if (string.IsNullOrEmpty(varName))
                    issues.Add($"Empty variable name at position {m.Index}.");

                if (variables != null && !variables.ContainsKey(varName))
                    issues.Add($"Variable '{varName}' not provided.");

                for (int i = 1; i < parts.Count; i++)
                {
                    var filterExpr = parts[i].Trim();
                    var filterParts = filterExpr.Split(':');
                    var filterName = filterParts[0].Trim().ToLowerInvariant();
                    if (!IsKnownFilter(filterName))
                        issues.Add($"Unknown filter '{filterName}' on variable '{varName}'.");
                }
            }

            var openCount = CountOccurrences(template, _openDelimiter);
            var closeCount = CountOccurrences(template, _closeDelimiter);
            if (openCount != closeCount)
                issues.Add($"Mismatched delimiters: {openCount} open, {closeCount} close.");

            return issues;
        }

        /// <summary>Generates a JSON report of the interpolation.</summary>
        public string ToJson(InterpolationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var report = new
            {
                template = result.Template,
                output = result.Output,
                isComplete = result.IsComplete,
                resolvedVariables = result.ResolvedVariables,
                unresolvedVariables = result.UnresolvedVariables,
                appliedFilters = result.AppliedFilters,
                warnings = result.Warnings,
                summary = result.Summary
            };
            return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        }

        private string ProcessExpression(string expression, Dictionary<string, object> variables, InterpolationResult result)
        {
            var parts = SplitPipes(expression);
            var varName = parts[0].Trim();

            string value;
            if (variables.TryGetValue(varName, out var obj))
            {
                value = ConvertToString(obj);
                if (!result.ResolvedVariables.Contains(varName))
                    result.ResolvedVariables.Add(varName);
            }
            else
            {
                if (!result.UnresolvedVariables.Contains(varName))
                    result.UnresolvedVariables.Add(varName);

                switch (_unresolvedBehavior)
                {
                    case UnresolvedBehavior.Throw:
                        throw new KeyNotFoundException($"Variable '{varName}' not found.");
                    case UnresolvedBehavior.Remove:
                        value = "";
                        break;
                    default:
                        return $"{_openDelimiter} {expression} {_closeDelimiter}";
                }
            }

            for (int i = 1; i < parts.Count; i++)
            {
                var filterExpr = parts[i].Trim();
                var filterParts = filterExpr.Split(':');
                var filterName = filterParts[0].Trim().ToLowerInvariant();
                var args = filterParts.Length > 1
                    ? filterParts.Skip(1).ToArray()
                    : Array.Empty<string>();

                try
                {
                    value = ApplyFilter(filterName, value, args);
                    result.AppliedFilters.Add(filterName);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Filter '{filterName}' on '{varName}': {ex.Message}");
                }
            }

            return value;
        }

        private string ApplyFilter(string name, string input, string[] args)
        {
            if (_customFilters.TryGetValue(name, out var custom))
                return custom(input, args);

            return name switch
            {
                "upper" => input.ToUpperInvariant(),
                "lower" => input.ToLowerInvariant(),
                "trim" => input.Trim(),
                "capitalize" => input.Length > 0 ? char.ToUpper(input[0]) + input.Substring(1) : input,
                "title" => ToTitleCase(input),
                "reverse" => new string(input.Reverse().ToArray()),
                "truncate" => Truncate(input, args),
                "pad_left" => PadLeft(input, args),
                "pad_right" => PadRight(input, args),
                "default" => string.IsNullOrEmpty(input) && args.Length > 0 ? args[0] : input,
                "replace" => args.Length >= 2 ? input.Replace(args[0], args[1]) : input,
                "repeat" => Repeat(input, args),
                "prefix" => args.Length > 0 ? args[0] + input : input,
                "suffix" => args.Length > 0 ? input + args[0] : input,
                "pluralize" => Pluralize(input, args),
                "format_number" => FormatNumber(input, args),
                "format_date" => FormatDate(input, args),
                "base64_encode" => Convert.ToBase64String(Encoding.UTF8.GetBytes(input)),
                "base64_decode" => Encoding.UTF8.GetString(Convert.FromBase64String(input)),
                "url_encode" => Uri.EscapeDataString(input),
                "json" => JsonSerializer.Serialize(input),
                "wordcount" => input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length.ToString(),
                "charcount" => input.Length.ToString(),
                "initials" => GetInitials(input),
                "slug" => Slugify(input),
                "ellipsis" => Ellipsis(input, args),
                _ => throw new ArgumentException($"Unknown filter: {name}")
            };
        }

        private static List<string> SplitPipes(string expression)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '\\' && i + 1 < expression.Length && expression[i + 1] == '|')
                {
                    current.Append('|');
                    i++;
                }
                else if (expression[i] == '|')
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(expression[i]);
                }
            }
            parts.Add(current.ToString());
            return parts;
        }

        private static string ConvertToString(object obj) => obj switch
        {
            null => "",
            string s => s,
            DateTime dt => dt.ToString("o"),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            _ => obj.ToString() ?? ""
        };

        private static string ToTitleCase(string input) =>
            CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());

        private static string Truncate(string input, string[] args)
        {
            if (args.Length == 0) return input;
            if (int.TryParse(args[0], out int len) && len >= 0 && input.Length > len)
                return input.Substring(0, len);
            return input;
        }

        private static string PadLeft(string input, string[] args)
        {
            if (args.Length == 0 || !int.TryParse(args[0], out int width)) return input;
            char pad = args.Length > 1 && args[1].Length > 0 ? args[1][0] : ' ';
            return input.PadLeft(width, pad);
        }

        private static string PadRight(string input, string[] args)
        {
            if (args.Length == 0 || !int.TryParse(args[0], out int width)) return input;
            char pad = args.Length > 1 && args[1].Length > 0 ? args[1][0] : ' ';
            return input.PadRight(width, pad);
        }

        private static string Repeat(string input, string[] args)
        {
            if (args.Length == 0 || !int.TryParse(args[0], out int count)) return input;
            count = Math.Clamp(count, 0, 100);
            return string.Concat(Enumerable.Repeat(input, count));
        }

        private static string Pluralize(string input, string[] args)
        {
            if (args.Length < 2 || !double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double n))
                return input;
            return Math.Abs(n - 1.0) < 0.0001 ? args[0] : args[1];
        }

        private static string FormatNumber(string input, string[] args)
        {
            if (!double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double n))
                return input;
            int decimals = args.Length > 0 && int.TryParse(args[0], out int d) ? d : 2;
            return n.ToString($"N{decimals}", CultureInfo.InvariantCulture);
        }

        private static string FormatDate(string input, string[] args)
        {
            if (!DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                return input;
            string fmt = args.Length > 0 ? args[0] : "yyyy-MM-dd";
            return dt.ToString(fmt, CultureInfo.InvariantCulture);
        }

        private static string GetInitials(string input)
        {
            var words = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(words.Select(w => char.ToUpper(w[0])));
        }

        private static string Slugify(string input)
        {
            var slug = input.ToLowerInvariant().Trim();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            slug = Regex.Replace(slug, @"[\s-]+", "-", RegexOptions.None, TimeSpan.FromMilliseconds(500));
            return slug.Trim('-');
        }

        private static string Ellipsis(string input, string[] args)
        {
            if (args.Length == 0 || !int.TryParse(args[0], out int max)) return input;
            if (max < 4) max = 4;
            if (input.Length <= max) return input;
            // Find last space within limit to avoid cutting words
            var cutoff = max - 3;
            var lastSpace = input.LastIndexOf(' ', cutoff - 1);
            if (lastSpace > 0)
                return input.Substring(0, lastSpace) + "...";
            return input.Substring(0, cutoff) + "...";
        }

        private bool IsKnownFilter(string name)
        {
            return BuiltInFilters.Contains(name) || _customFilters.ContainsKey(name);
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0, i = 0;
            while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
            {
                count++;
                i += pattern.Length;
            }
            return count;
        }
    }
}
