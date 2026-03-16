namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    // ── Result types ─────────────────────────────────────────

    /// <summary>
    /// A single cell in the prompt matrix: one specific combination of variable values.
    /// </summary>
    public class MatrixCell
    {
        /// <summary>Zero-based index of this cell in the matrix.</summary>
        public int Index { get; }

        /// <summary>Human-readable label summarizing the variable values.</summary>
        public string Label { get; }

        /// <summary>The fully rendered prompt text.</summary>
        public string Text { get; }

        /// <summary>Estimated token count (chars / 4 heuristic).</summary>
        public int EstimatedTokens { get; }

        /// <summary>The specific variable values used for this cell.</summary>
        public IReadOnlyDictionary<string, string> Variables { get; }

        /// <summary>
        /// Creates a new matrix cell.
        /// </summary>
        public MatrixCell(int index, string label, string text,
            int estimatedTokens, IReadOnlyDictionary<string, string> variables)
        {
            Index = index;
            Label = label;
            Text = text;
            EstimatedTokens = estimatedTokens;
            Variables = variables;
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"[{Index}] {Label} — {EstimatedTokens} tokens";
    }

    /// <summary>
    /// Result of expanding a prompt matrix: all combinations with summary statistics.
    /// </summary>
    public class MatrixResult
    {
        /// <summary>The original template.</summary>
        public string Template { get; }

        /// <summary>Variable names and their possible values.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Axes { get; }

        /// <summary>All generated cells (one per combination).</summary>
        public IReadOnlyList<MatrixCell> Cells { get; }

        /// <summary>Total number of combinations.</summary>
        public int TotalCombinations => Cells.Count;

        /// <summary>Average estimated tokens across all cells.</summary>
        public double AverageTokens { get; }

        /// <summary>Minimum estimated tokens across all cells.</summary>
        public int MinTokens { get; }

        /// <summary>Maximum estimated tokens across all cells.</summary>
        public int MaxTokens { get; }

        /// <summary>
        /// Creates a new matrix result.
        /// </summary>
        public MatrixResult(string template,
            IReadOnlyDictionary<string, IReadOnlyList<string>> axes,
            IReadOnlyList<MatrixCell> cells)
        {
            Template = template;
            Axes = axes;
            Cells = cells;
            if (cells.Count > 0)
            {
                AverageTokens = cells.Average(c => c.EstimatedTokens);
                MinTokens = cells.Min(c => c.EstimatedTokens);
                MaxTokens = cells.Max(c => c.EstimatedTokens);
            }
        }

        /// <summary>
        /// Filters cells where a specific variable has a given value.
        /// </summary>
        public IReadOnlyList<MatrixCell> Where(string variable, string value)
        {
            if (string.IsNullOrWhiteSpace(variable))
                throw new ArgumentException("Variable name cannot be null or empty.", nameof(variable));

            return Cells.Where(c =>
                c.Variables.TryGetValue(variable, out var v) &&
                string.Equals(v, value, StringComparison.Ordinal))
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Groups cells by a specific variable, returning value → cells mapping.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<MatrixCell>> GroupBy(string variable)
        {
            if (string.IsNullOrWhiteSpace(variable))
                throw new ArgumentException("Variable name cannot be null or empty.", nameof(variable));

            return Cells
                .Where(c => c.Variables.ContainsKey(variable))
                .GroupBy(c => c.Variables[variable])
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<MatrixCell>)g.ToList().AsReadOnly());
        }

        /// <summary>
        /// Returns a human-readable summary of the matrix.
        /// </summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Prompt Matrix Summary ===");
            sb.AppendLine($"Template variables: {Axes.Count}");
            foreach (var axis in Axes)
                sb.AppendLine($"  {axis.Key}: {axis.Value.Count} values [{string.Join(", ", axis.Value.Select(v => Quote(v)))}]");
            sb.AppendLine($"Total combinations: {TotalCombinations}");
            sb.AppendLine($"Token range: {MinTokens}–{MaxTokens} (avg {AverageTokens:F1})");
            sb.AppendLine();
            foreach (var cell in Cells)
                sb.AppendLine(cell.ToString());
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the result to JSON.
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var obj = new
            {
                template = Template,
                axes = Axes.ToDictionary(a => a.Key, a => a.Value.ToArray()),
                totalCombinations = TotalCombinations,
                tokenStats = new
                {
                    min = MinTokens,
                    max = MaxTokens,
                    average = Math.Round(AverageTokens, 1)
                },
                cells = Cells.Select(c => new
                {
                    index = c.Index,
                    label = c.Label,
                    text = c.Text,
                    estimatedTokens = c.EstimatedTokens,
                    variables = c.Variables
                }).ToArray()
            };
            return JsonSerializer.Serialize(obj,
                indented ? SerializationGuards.WriteIndented : new JsonSerializerOptions());
        }

        /// <summary>
        /// Exports the matrix as a CSV string.
        /// </summary>
        public string ToCsv()
        {
            var sb = new StringBuilder();
            var varNames = Axes.Keys.ToList();

            // Header
            sb.Append("index,label,estimatedTokens");
            foreach (var name in varNames)
                sb.Append($",{EscapeCsv(name)}");
            sb.AppendLine(",text");

            // Rows
            foreach (var cell in Cells)
            {
                sb.Append($"{cell.Index},{EscapeCsv(cell.Label)},{cell.EstimatedTokens}");
                foreach (var name in varNames)
                {
                    var val = cell.Variables.TryGetValue(name, out var v) ? v : "";
                    sb.Append($",{EscapeCsv(val)}");
                }
                sb.AppendLine($",{EscapeCsv(cell.Text)}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Loads a matrix result from JSON.
        /// </summary>
        public static MatrixResult FromJson(string json)
        {
            SerializationGuards.ValidateJsonInput(json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var template = root.GetProperty("template").GetString() ?? "";
            var axesElement = root.GetProperty("axes");
            var axes = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var prop in axesElement.EnumerateObject())
            {
                var values = prop.Value.EnumerateArray()
                    .Select(v => v.GetString() ?? "")
                    .ToList()
                    .AsReadOnly();
                axes[prop.Name] = values;
            }

            var cells = new List<MatrixCell>();
            foreach (var cellEl in root.GetProperty("cells").EnumerateArray())
            {
                var variables = new Dictionary<string, string>();
                foreach (var vp in cellEl.GetProperty("variables").EnumerateObject())
                    variables[vp.Name] = vp.Value.GetString() ?? "";

                cells.Add(new MatrixCell(
                    cellEl.GetProperty("index").GetInt32(),
                    cellEl.GetProperty("label").GetString() ?? "",
                    cellEl.GetProperty("text").GetString() ?? "",
                    cellEl.GetProperty("estimatedTokens").GetInt32(),
                    variables));
            }

            return new MatrixResult(template, axes, cells.AsReadOnly());
        }

        private static string Quote(string s) => $"\"{s}\"";

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }

    // ── Configuration ────────────────────────────────────────

    /// <summary>
    /// Configuration for prompt matrix expansion.
    /// </summary>
    public class MatrixConfig
    {
        /// <summary>
        /// Variable definitions: variable name → list of possible values.
        /// Template placeholders use <c>{{variableName}}</c> syntax.
        /// </summary>
        public Dictionary<string, List<string>> Variables { get; set; } = new();

        /// <summary>
        /// Maximum number of combinations to generate (safety limit).
        /// Default: 1000.
        /// </summary>
        public int MaxCombinations { get; set; } = 1000;

        /// <summary>
        /// If true, include a "baseline" cell with the first value of each variable.
        /// Default: true.
        /// </summary>
        public bool IncludeBaseline { get; set; } = true;

        /// <summary>
        /// Optional filter predicate: only include combinations where the predicate returns true.
        /// Receives the variable → value dictionary for each combination.
        /// </summary>
        public Func<Dictionary<string, string>, bool>? Filter { get; set; }

        /// <summary>
        /// Adds a variable with its possible values. Returns this for chaining.
        /// </summary>
        public MatrixConfig AddVariable(string name, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name cannot be null or empty.", nameof(name));
            if (values == null || values.Length == 0)
                throw new ArgumentException("Must provide at least one value.", nameof(values));

            Variables[name] = values.ToList();
            return this;
        }

        /// <summary>
        /// Returns the total number of combinations (before filtering).
        /// </summary>
        public long EstimateCombinations()
        {
            if (Variables.Count == 0) return 0;
            long total = 1;
            foreach (var vals in Variables.Values)
            {
                total *= vals.Count;
                if (total > MaxCombinations * 10L) return total; // overflow guard
            }
            return total;
        }
    }

    // ── Generator ────────────────────────────────────────────

    /// <summary>
    /// Generates a combinatorial matrix of prompt instances by filling template
    /// variables with all possible value combinations. Useful for systematic
    /// prompt testing, A/B evaluation, and sensitivity analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Templates use <c>{{variableName}}</c> placeholder syntax. Each variable
    /// can have multiple possible values; the matrix generates the cartesian
    /// product of all variable values.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var matrix = new PromptMatrix();
    /// var config = new MatrixConfig()
    ///     .AddVariable("role", "expert", "beginner", "critic")
    ///     .AddVariable("format", "bullet points", "paragraph", "table")
    ///     .AddVariable("tone", "formal", "casual");
    ///
    /// var result = matrix.Expand(
    ///     "You are a {{role}}. {{tone}} tone. Respond in {{format}}.",
    ///     config);
    ///
    /// // result.TotalCombinations == 18 (3 × 3 × 2)
    /// foreach (var cell in result.Cells)
    ///     Console.WriteLine($"{cell.Label}: {cell.Text}");
    /// </code>
    /// </para>
    /// <para>
    /// Pairs well with <see cref="PromptABTester"/> for evaluating which
    /// variable values perform best, and with <see cref="PromptVariantGenerator"/>
    /// for further transforming each cell.
    /// </para>
    /// </remarks>
    public class PromptMatrix
    {
        /// <summary>Maximum template length accepted (500 KB).</summary>
        public const int MaxTemplateLength = 500_000;

        private static readonly Regex PlaceholderPattern = new(
            @"\{\{(\w+)\}\}",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        /// <summary>
        /// Creates a new prompt matrix generator.
        /// </summary>
        public PromptMatrix() { }

        /// <summary>
        /// Expands a template into all combinations of variable values.
        /// </summary>
        /// <param name="template">Prompt template with <c>{{variable}}</c> placeholders.</param>
        /// <param name="config">Matrix configuration with variable definitions.</param>
        /// <returns>A <see cref="MatrixResult"/> with all generated combinations.</returns>
        /// <exception cref="ArgumentException">If template is null/empty/too long or config has no variables.</exception>
        /// <exception cref="InvalidOperationException">If combination count exceeds limit.</exception>
        public MatrixResult Expand(string template, MatrixConfig config)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Template cannot be null or empty.", nameof(template));
            if (template.Length > MaxTemplateLength)
                throw new ArgumentException(
                    $"Template exceeds maximum length of {MaxTemplateLength} characters.", nameof(template));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (config.Variables.Count == 0)
                throw new ArgumentException("Config must define at least one variable.", nameof(config));

            // Validate all values are non-null
            foreach (var kvp in config.Variables)
            {
                if (kvp.Value.Any(v => v == null))
                    throw new ArgumentException(
                        $"Variable '{kvp.Key}' contains null values.", nameof(config));
            }

            // Check combination count
            long estimated = config.EstimateCombinations();
            if (estimated > config.MaxCombinations)
                throw new InvalidOperationException(
                    $"Estimated {estimated} combinations exceeds limit of {config.MaxCombinations}. " +
                    "Reduce variable values or increase MaxCombinations.");

            // Detect template variables
            var templateVars = ExtractPlaceholders(template);
            var configVars = config.Variables.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Build axes (only variables that appear in both template and config)
            var axes = new Dictionary<string, IReadOnlyList<string>>();
            var activeVarNames = new List<string>();
            foreach (var name in config.Variables.Keys)
            {
                if (templateVars.Contains(name))
                {
                    axes[name] = config.Variables[name].AsReadOnly();
                    activeVarNames.Add(name);
                }
            }

            if (activeVarNames.Count == 0)
                throw new ArgumentException(
                    "No config variables match template placeholders. " +
                    $"Template has: [{string.Join(", ", templateVars)}]. " +
                    $"Config has: [{string.Join(", ", configVars)}].",
                    nameof(config));

            // Generate cartesian product
            var cells = new List<MatrixCell>();
            var valueLists = activeVarNames.Select(n => config.Variables[n]).ToList();
            var indices = new int[activeVarNames.Count];
            int cellIndex = 0;

            while (true)
            {
                // Build current combination
                var variables = new Dictionary<string, string>();
                var labelParts = new List<string>();
                for (int i = 0; i < activeVarNames.Count; i++)
                {
                    var name = activeVarNames[i];
                    var value = valueLists[i][indices[i]];
                    variables[name] = value;
                    labelParts.Add($"{name}={Truncate(value, 20)}");
                }

                // Apply filter if present
                if (config.Filter == null || config.Filter(variables))
                {
                    var text = Render(template, variables);
                    var tokens = PromptGuard.EstimateTokens(text);
                    var label = string.Join(" | ", labelParts);

                    cells.Add(new MatrixCell(cellIndex, label, text, tokens,
                        new Dictionary<string, string>(variables)));
                    cellIndex++;
                }

                // Increment indices (odometer style)
                int pos = activeVarNames.Count - 1;
                while (pos >= 0)
                {
                    indices[pos]++;
                    if (indices[pos] < valueLists[pos].Count)
                        break;
                    indices[pos] = 0;
                    pos--;
                }
                if (pos < 0) break;

                if (cellIndex >= config.MaxCombinations) break;
            }

            return new MatrixResult(template, axes, cells.AsReadOnly());
        }

        /// <summary>
        /// Extracts all placeholder names from a template.
        /// </summary>
        /// <param name="template">Template string with <c>{{variable}}</c> placeholders.</param>
        /// <returns>Set of unique variable names found in the template.</returns>
        public HashSet<string> ExtractPlaceholders(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return new HashSet<string>();

            var matches = PlaceholderPattern.Matches(template);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in matches)
                names.Add(m.Groups[1].Value);
            return names;
        }

        /// <summary>
        /// Renders a template with the given variable values.
        /// Unresolved placeholders are left as-is.
        /// </summary>
        /// <param name="template">Template string with <c>{{variable}}</c> placeholders.</param>
        /// <param name="variables">Variable name → value mapping.</param>
        /// <returns>The rendered prompt text.</returns>
        public string Render(string template, IDictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Template cannot be null or empty.", nameof(template));
            if (variables == null)
                throw new ArgumentNullException(nameof(variables));

            return PlaceholderPattern.Replace(template, match =>
            {
                var name = match.Groups[1].Value;
                foreach (var kvp in variables)
                {
                    if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                        return kvp.Value;
                }
                return match.Value; // leave unresolved
            });
        }

        /// <summary>
        /// Quick convenience method: expand a template with inline variable definitions.
        /// </summary>
        /// <param name="template">Template string.</param>
        /// <param name="variables">Alternating variable name and comma-separated values.
        /// E.g., "role", "expert,beginner", "tone", "formal,casual"</param>
        /// <returns>A <see cref="MatrixResult"/> with all combinations.</returns>
        public MatrixResult QuickExpand(string template, params string[] variables)
        {
            if (variables.Length % 2 != 0)
                throw new ArgumentException(
                    "Variables must be provided as name/values pairs.", nameof(variables));

            var config = new MatrixConfig();
            for (int i = 0; i < variables.Length; i += 2)
            {
                var name = variables[i];
                var values = variables[i + 1].Split(',')
                    .Select(v => v.Trim())
                    .Where(v => v.Length > 0)
                    .ToArray();
                config.AddVariable(name, values);
            }
            return Expand(template, config);
        }

        private static string Truncate(string text, int maxLen)
        {
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen - 3) + "...";
        }
    }
}
