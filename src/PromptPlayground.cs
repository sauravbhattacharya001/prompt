namespace Prompt
{
    using System.Text;

    /// <summary>
    /// An interactive playground for experimenting with prompt templates.
    /// Load templates, try different variable combinations, compare rendered
    /// outputs side-by-side, and track iteration history — all in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Useful during prompt engineering to quickly iterate on wording,
    /// variable values, and template structure without re-running your
    /// full application.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var playground = new PromptPlayground();
    /// playground.LoadTemplate("greeting",
    ///     new PromptTemplate("Hello {{name}}, you are a {{role}}.",
    ///         new Dictionary&lt;string, string&gt; { ["role"] = "developer" }));
    ///
    /// var result = playground.Render("greeting",
    ///     new Dictionary&lt;string, string&gt; { ["name"] = "Alice" });
    /// // result.Output → "Hello Alice, you are a developer."
    ///
    /// var result2 = playground.Render("greeting",
    ///     new Dictionary&lt;string, string&gt; { ["name"] = "Bob", ["role"] = "designer" });
    ///
    /// string diff = playground.Compare("greeting", 0, 1);
    /// // Shows side-by-side comparison of the two renders
    /// </code>
    /// </para>
    /// </remarks>
    public class PromptPlayground
    {
        private readonly Dictionary<string, PromptTemplate> _templates = new();
        private readonly Dictionary<string, List<PlaygroundIteration>> _history = new();
        private readonly List<string> _activityLog = new();

        /// <summary>
        /// Gets all loaded template names.
        /// </summary>
        public IReadOnlyCollection<string> TemplateNames => _templates.Keys;

        /// <summary>
        /// Gets the iteration history for a given template.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <returns>Read-only list of iterations, or empty if template not found.</returns>
        public IReadOnlyList<PlaygroundIteration> GetHistory(string templateName)
        {
            return _history.TryGetValue(templateName, out var list)
                ? list.AsReadOnly()
                : Array.Empty<PlaygroundIteration>();
        }

        /// <summary>
        /// Gets the full activity log of actions performed in this playground.
        /// </summary>
        public IReadOnlyList<string> ActivityLog => _activityLog.AsReadOnly();

        /// <summary>
        /// Loads or replaces a named template in the playground.
        /// </summary>
        /// <param name="name">Unique name for this template.</param>
        /// <param name="template">The prompt template to load.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="name"/> or <paramref name="template"/> is null.
        /// </exception>
        public void LoadTemplate(string name, PromptTemplate template)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(template);

            _templates[name] = template;
            if (!_history.ContainsKey(name))
                _history[name] = new List<PlaygroundIteration>();

            _activityLog.Add($"[{DateTime.UtcNow:u}] Loaded template '{name}'");
        }

        /// <summary>
        /// Removes a template and its history from the playground.
        /// </summary>
        /// <param name="name">Name of the template to remove.</param>
        /// <returns>True if the template was found and removed.</returns>
        public bool RemoveTemplate(string name)
        {
            var removed = _templates.Remove(name);
            _history.Remove(name);
            if (removed)
                _activityLog.Add($"[{DateTime.UtcNow:u}] Removed template '{name}'");
            return removed;
        }

        /// <summary>
        /// Renders a template with the given variables and records the
        /// iteration in history.
        /// </summary>
        /// <param name="templateName">Name of a loaded template.</param>
        /// <param name="variables">Variables to fill in the template.</param>
        /// <param name="note">Optional note describing this iteration (e.g. "more formal tone").</param>
        /// <returns>A <see cref="PlaygroundIteration"/> with the rendered output and metadata.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when the template name is not found.
        /// </exception>
        public PlaygroundIteration Render(string templateName,
            Dictionary<string, string> variables,
            string? note = null)
        {
            if (!_templates.TryGetValue(templateName, out var template))
                throw new KeyNotFoundException($"Template '{templateName}' not found. Load it first with LoadTemplate().");

            var rendered = template.Render(variables);
            var iteration = new PlaygroundIteration
            {
                Index = _history[templateName].Count,
                Output = rendered,
                Variables = new Dictionary<string, string>(variables),
                Note = note,
                Timestamp = DateTime.UtcNow,
                CharacterCount = rendered.Length,
                WordCount = rendered.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
            };

            _history[templateName].Add(iteration);
            _activityLog.Add($"[{DateTime.UtcNow:u}] Rendered '{templateName}' iteration #{iteration.Index}" +
                (note != null ? $" ({note})" : ""));

            return iteration;
        }

        /// <summary>
        /// Compares two iterations of the same template side-by-side.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <param name="iterationA">Index of first iteration.</param>
        /// <param name="iterationB">Index of second iteration.</param>
        /// <returns>A formatted comparison string showing both outputs and their differences.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when template not found.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when iteration index is invalid.</exception>
        public string Compare(string templateName, int iterationA, int iterationB)
        {
            if (!_history.TryGetValue(templateName, out var history))
                throw new KeyNotFoundException($"Template '{templateName}' not found.");

            if (iterationA < 0 || iterationA >= history.Count)
                throw new ArgumentOutOfRangeException(nameof(iterationA),
                    $"Iteration {iterationA} does not exist. History has {history.Count} entries.");
            if (iterationB < 0 || iterationB >= history.Count)
                throw new ArgumentOutOfRangeException(nameof(iterationB),
                    $"Iteration {iterationB} does not exist. History has {history.Count} entries.");

            var a = history[iterationA];
            var b = history[iterationB];

            var sb = new StringBuilder();
            sb.AppendLine($"=== Comparison: '{templateName}' iteration #{iterationA} vs #{iterationB} ===");
            sb.AppendLine();

            // Variable diff
            var allKeys = a.Variables.Keys.Union(b.Variables.Keys).OrderBy(k => k);
            sb.AppendLine("Variables:");
            foreach (var key in allKeys)
            {
                var valA = a.Variables.GetValueOrDefault(key, "(not set)");
                var valB = b.Variables.GetValueOrDefault(key, "(not set)");
                var marker = valA == valB ? " " : "~";
                sb.AppendLine($"  {marker} {key}: \"{valA}\" → \"{valB}\"");
            }
            sb.AppendLine();

            sb.AppendLine($"--- Iteration #{iterationA} ({a.CharacterCount} chars, {a.WordCount} words) ---");
            if (a.Note != null) sb.AppendLine($"Note: {a.Note}");
            sb.AppendLine(a.Output);
            sb.AppendLine();

            sb.AppendLine($"--- Iteration #{iterationB} ({b.CharacterCount} chars, {b.WordCount} words) ---");
            if (b.Note != null) sb.AppendLine($"Note: {b.Note}");
            sb.AppendLine(b.Output);

            // Line-by-line diff
            sb.AppendLine();
            sb.AppendLine("--- Line Diff ---");
            var linesA = a.Output.Split('\n');
            var linesB = b.Output.Split('\n');
            var maxLines = Math.Max(linesA.Length, linesB.Length);
            for (int i = 0; i < maxLines; i++)
            {
                var lineA = i < linesA.Length ? linesA[i].TrimEnd() : "";
                var lineB = i < linesB.Length ? linesB[i].TrimEnd() : "";
                if (lineA != lineB)
                {
                    if (!string.IsNullOrEmpty(lineA))
                        sb.AppendLine($"  - {lineA}");
                    if (!string.IsNullOrEmpty(lineB))
                        sb.AppendLine($"  + {lineB}");
                }
            }

            _activityLog.Add($"[{DateTime.UtcNow:u}] Compared '{templateName}' #{iterationA} vs #{iterationB}");
            return sb.ToString();
        }

        /// <summary>
        /// Renders all permutations of the provided variable options and returns them as iterations.
        /// Useful for A/B testing different variable combinations.
        /// </summary>
        /// <param name="templateName">Name of a loaded template.</param>
        /// <param name="variableOptions">Dictionary mapping variable names to lists of possible values.</param>
        /// <returns>List of all rendered iterations.</returns>
        /// <exception cref="ArgumentException">Thrown when variableOptions is empty.</exception>
        public List<PlaygroundIteration> Sweep(string templateName,
            Dictionary<string, List<string>> variableOptions)
        {
            if (variableOptions == null || variableOptions.Count == 0)
                throw new ArgumentException("Variable options must contain at least one variable.", nameof(variableOptions));

            var keys = variableOptions.Keys.ToList();
            var combos = new List<Dictionary<string, string>>();
            GenerateCombinations(keys, variableOptions, 0, new Dictionary<string, string>(), combos);

            var results = new List<PlaygroundIteration>();
            int comboIndex = 0;
            foreach (var combo in combos)
            {
                var note = $"Sweep #{comboIndex}: {string.Join(", ", combo.Select(kv => $"{kv.Key}={kv.Value}"))}";
                results.Add(Render(templateName, combo, note));
                comboIndex++;
            }

            _activityLog.Add($"[{DateTime.UtcNow:u}] Swept '{templateName}' with {combos.Count} combinations");
            return results;
        }

        /// <summary>
        /// Generates a summary report of all templates and their iteration counts.
        /// </summary>
        /// <returns>A formatted summary string.</returns>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Playground Summary ===");
            sb.AppendLine($"Templates loaded: {_templates.Count}");
            sb.AppendLine($"Total iterations: {_history.Values.Sum(h => h.Count)}");
            sb.AppendLine();

            foreach (var (name, history) in _history.OrderBy(kv => kv.Key))
            {
                sb.AppendLine($"  {name}: {history.Count} iteration(s)");
                if (history.Count > 0)
                {
                    var latest = history[^1];
                    sb.AppendLine($"    Latest: {latest.CharacterCount} chars, {latest.WordCount} words" +
                        (latest.Note != null ? $" — {latest.Note}" : ""));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Finds the shortest rendered output across all iterations of a template.
        /// Useful for prompt minimization / token budget optimization.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <returns>The iteration with the fewest characters, or null if no history.</returns>
        public PlaygroundIteration? FindShortest(string templateName)
        {
            if (!_history.TryGetValue(templateName, out var history) || history.Count == 0)
                return null;

            return history.MinBy(i => i.CharacterCount);
        }

        /// <summary>
        /// Finds the longest rendered output across all iterations of a template.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        /// <returns>The iteration with the most characters, or null if no history.</returns>
        public PlaygroundIteration? FindLongest(string templateName)
        {
            if (!_history.TryGetValue(templateName, out var history) || history.Count == 0)
                return null;

            return history.MaxBy(i => i.CharacterCount);
        }

        /// <summary>
        /// Clears all iteration history for a template without removing the template itself.
        /// </summary>
        /// <param name="templateName">Name of the template.</param>
        public void ClearHistory(string templateName)
        {
            if (_history.TryGetValue(templateName, out var history))
            {
                history.Clear();
                _activityLog.Add($"[{DateTime.UtcNow:u}] Cleared history for '{templateName}'");
            }
        }

        private static void GenerateCombinations(
            List<string> keys,
            Dictionary<string, List<string>> options,
            int depth,
            Dictionary<string, string> current,
            List<Dictionary<string, string>> results)
        {
            if (depth == keys.Count)
            {
                results.Add(new Dictionary<string, string>(current));
                return;
            }

            var key = keys[depth];
            foreach (var value in options[key])
            {
                current[key] = value;
                GenerateCombinations(keys, options, depth + 1, current, results);
            }
            current.Remove(key);
        }
    }

    /// <summary>
    /// Represents a single rendered iteration in the playground,
    /// capturing the output, variables used, and metadata.
    /// </summary>
    public class PlaygroundIteration
    {
        /// <summary>Index of this iteration in the template's history.</summary>
        public int Index { get; init; }

        /// <summary>The rendered prompt output.</summary>
        public string Output { get; init; } = string.Empty;

        /// <summary>Variables used for this render.</summary>
        public Dictionary<string, string> Variables { get; init; } = new();

        /// <summary>Optional note describing this iteration.</summary>
        public string? Note { get; init; }

        /// <summary>When this iteration was rendered (UTC).</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>Character count of the rendered output.</summary>
        public int CharacterCount { get; init; }

        /// <summary>Word count of the rendered output.</summary>
        public int WordCount { get; init; }

        /// <inheritdoc />
        public override string ToString() =>
            $"[#{Index}] {CharacterCount} chars, {WordCount} words" +
            (Note != null ? $" — {Note}" : "") +
            $"\n{Output}";
    }
}
