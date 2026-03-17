namespace Prompt
{
    using System.Text;

    /// <summary>
    /// Output format for chain visualization.
    /// </summary>
    public enum ChainVisualizationFormat
    {
        /// <summary>Mermaid flowchart syntax (renders in GitHub, GitLab, etc.).</summary>
        Mermaid,

        /// <summary>Graphviz DOT language.</summary>
        Dot,

        /// <summary>Plain-text ASCII art flowchart.</summary>
        Ascii
    }

    /// <summary>
    /// Options for customizing chain visualization output.
    /// </summary>
    public class ChainVisualizationOptions
    {
        /// <summary>Gets or sets whether to show template variable names on edges. Default true.</summary>
        public bool ShowVariables { get; set; } = true;

        /// <summary>Gets or sets whether to show step indices. Default true.</summary>
        public bool ShowStepNumbers { get; set; } = true;

        /// <summary>Gets or sets the Mermaid flowchart direction (TB, LR, BT, RL). Default "TB".</summary>
        public string MermaidDirection { get; set; } = "TB";

        /// <summary>Gets or sets the DOT graph label. Default null (no label).</summary>
        public string? GraphLabel { get; set; }

        /// <summary>Gets or sets whether to include a legend. Default false.</summary>
        public bool IncludeLegend { get; set; }
    }

    /// <summary>
    /// Generates visual flowchart representations of <see cref="PromptChain"/> instances
    /// in Mermaid, DOT (Graphviz), and ASCII formats.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to document, debug, or share prompt chain architectures.
    /// Mermaid output renders directly in GitHub/GitLab markdown. DOT output
    /// can be rendered with Graphviz tools. ASCII output works anywhere.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var chain = new PromptChain()
    ///     .AddStep("extract", extractTemplate, "entities")
    ///     .AddStep("analyze", analyzeTemplate, "analysis")
    ///     .AddStep("summarize", summaryTemplate, "summary");
    ///
    /// // Mermaid (paste into GitHub markdown)
    /// string mermaid = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Mermaid);
    ///
    /// // DOT (render with `dot -Tpng`)
    /// string dot = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Dot);
    ///
    /// // ASCII (print to console)
    /// string ascii = PromptChainVisualizer.Visualize(chain, ChainVisualizationFormat.Ascii);
    /// </code>
    /// </para>
    /// </remarks>
    public static class PromptChainVisualizer
    {
        /// <summary>
        /// Generates a visual representation of a <see cref="PromptChain"/>.
        /// </summary>
        /// <param name="chain">The prompt chain to visualize.</param>
        /// <param name="format">The output format.</param>
        /// <param name="options">Optional visualization options.</param>
        /// <returns>A string containing the flowchart in the requested format.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="chain"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown format values.</exception>
        public static string Visualize(
            PromptChain chain,
            ChainVisualizationFormat format,
            ChainVisualizationOptions? options = null)
        {
            if (chain == null)
                throw new ArgumentNullException(nameof(chain));

            options ??= new ChainVisualizationOptions();

            return format switch
            {
                ChainVisualizationFormat.Mermaid => GenerateMermaid(chain, options),
                ChainVisualizationFormat.Dot => GenerateDot(chain, options),
                ChainVisualizationFormat.Ascii => GenerateAscii(chain, options),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown visualization format.")
            };
        }

        /// <summary>
        /// Generates a visual representation of a completed <see cref="ChainResult"/>,
        /// including execution timing for each step.
        /// </summary>
        /// <param name="result">The chain result to visualize.</param>
        /// <param name="format">The output format.</param>
        /// <param name="options">Optional visualization options.</param>
        /// <returns>A string containing the flowchart with timing annotations.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
        public static string VisualizeResult(
            ChainResult result,
            ChainVisualizationFormat format,
            ChainVisualizationOptions? options = null)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            options ??= new ChainVisualizationOptions();

            return format switch
            {
                ChainVisualizationFormat.Mermaid => GenerateMermaidFromResult(result, options),
                ChainVisualizationFormat.Dot => GenerateDotFromResult(result, options),
                ChainVisualizationFormat.Ascii => GenerateAsciiFromResult(result, options),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown visualization format.")
            };
        }

        #region Mermaid

        private static string GenerateMermaid(PromptChain chain, ChainVisualizationOptions opts)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"flowchart {opts.MermaidDirection}");

            var steps = chain.Steps;
            if (steps.Count == 0)
            {
                sb.AppendLine("    empty[\"Empty Chain\"]");
                return sb.ToString();
            }

            // Start node
            sb.AppendLine("    start([\"▶ Start\"])");

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var id = $"step{i}";
                var label = opts.ShowStepNumbers
                    ? $"{i + 1}. {EscapeMermaid(step.Name)}"
                    : EscapeMermaid(step.Name);

                sb.AppendLine($"    {id}[\"{label}\"]");
            }

            // End node
            sb.AppendLine("    finish([\"✔ Done\"])");

            // Edges
            var firstId = "step0";
            sb.AppendLine($"    start --> {firstId}");

            for (int i = 0; i < steps.Count - 1; i++)
            {
                var edgeLabel = opts.ShowVariables
                    ? $" -- \"{EscapeMermaid(steps[i].OutputVariable)}\" -->"
                    : " -->";
                sb.AppendLine($"    step{i}{edgeLabel} step{i + 1}");
            }

            var lastVar = opts.ShowVariables
                ? $" -- \"{EscapeMermaid(steps[steps.Count - 1].OutputVariable)}\" -->"
                : " -->";
            sb.AppendLine($"    step{steps.Count - 1}{lastVar} finish");

            if (opts.IncludeLegend)
            {
                sb.AppendLine();
                sb.AppendLine($"    subgraph Legend");
                sb.AppendLine($"        note[\"Steps: {steps.Count}\"]");
                sb.AppendLine($"    end");
            }

            return sb.ToString();
        }

        private static string GenerateMermaidFromResult(ChainResult result, ChainVisualizationOptions opts)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"flowchart {opts.MermaidDirection}");

            var steps = result.Steps;
            if (steps.Count == 0)
            {
                sb.AppendLine("    empty[\"Empty Result\"]");
                return sb.ToString();
            }

            sb.AppendLine("    start([\"▶ Start\"])");

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var elapsed = $"{step.Elapsed.TotalMilliseconds:F0}ms";
                var label = opts.ShowStepNumbers
                    ? $"{i + 1}. {EscapeMermaid(step.StepName)} ({elapsed})"
                    : $"{EscapeMermaid(step.StepName)} ({elapsed})";

                sb.AppendLine($"    step{i}[\"{label}\"]");
            }

            sb.AppendLine("    finish([\"✔ Done\"])");

            sb.AppendLine($"    start --> step0");

            for (int i = 0; i < steps.Count - 1; i++)
            {
                var edgeLabel = opts.ShowVariables
                    ? $" -- \"{EscapeMermaid(steps[i].OutputVariable)}\" -->"
                    : " -->";
                sb.AppendLine($"    step{i}{edgeLabel} step{i + 1}");
            }

            var lastVar = opts.ShowVariables
                ? $" -- \"{EscapeMermaid(steps[steps.Count - 1].OutputVariable)}\" -->"
                : " -->";
            sb.AppendLine($"    step{steps.Count - 1}{lastVar} finish");

            return sb.ToString();
        }

        private static string EscapeMermaid(string text)
        {
            return text.Replace("\"", "#quot;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        #endregion

        #region DOT

        private static string GenerateDot(PromptChain chain, ChainVisualizationOptions opts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph PromptChain {");
            sb.AppendLine("    rankdir=TB;");
            sb.AppendLine("    node [shape=box, style=\"rounded,filled\", fillcolor=\"#e8f4fd\", fontname=\"Segoe UI,Helvetica,Arial,sans-serif\"];");
            sb.AppendLine("    edge [fontname=\"Segoe UI,Helvetica,Arial,sans-serif\", fontsize=10, color=\"#666666\"];");

            if (opts.GraphLabel != null)
                sb.AppendLine($"    label=\"{EscapeDot(opts.GraphLabel)}\";");

            var steps = chain.Steps;
            if (steps.Count == 0)
            {
                sb.AppendLine("    empty [label=\"Empty Chain\", shape=note];");
                sb.AppendLine("}");
                return sb.ToString();
            }

            sb.AppendLine("    start [label=\"Start\", shape=circle, fillcolor=\"#90EE90\"];");
            sb.AppendLine("    finish [label=\"Done\", shape=doublecircle, fillcolor=\"#FFB6C1\"];");

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var label = opts.ShowStepNumbers
                    ? $"{i + 1}. {EscapeDot(step.Name)}"
                    : EscapeDot(step.Name);
                sb.AppendLine($"    step{i} [label=\"{label}\"];");
            }

            sb.AppendLine($"    start -> step0;");

            for (int i = 0; i < steps.Count - 1; i++)
            {
                var edgeLabel = opts.ShowVariables
                    ? $" [label=\"{EscapeDot(steps[i].OutputVariable)}\"]"
                    : "";
                sb.AppendLine($"    step{i} -> step{i + 1}{edgeLabel};");
            }

            var lastLabel = opts.ShowVariables
                ? $" [label=\"{EscapeDot(steps[steps.Count - 1].OutputVariable)}\"]"
                : "";
            sb.AppendLine($"    step{steps.Count - 1} -> finish{lastLabel};");

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateDotFromResult(ChainResult result, ChainVisualizationOptions opts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph PromptChainResult {");
            sb.AppendLine("    rankdir=TB;");
            sb.AppendLine("    node [shape=box, style=\"rounded,filled\", fillcolor=\"#e8f4fd\", fontname=\"Segoe UI,Helvetica,Arial,sans-serif\"];");
            sb.AppendLine("    edge [fontname=\"Segoe UI,Helvetica,Arial,sans-serif\", fontsize=10, color=\"#666666\"];");

            if (opts.GraphLabel != null)
                sb.AppendLine($"    label=\"{EscapeDot(opts.GraphLabel)}\";");

            var steps = result.Steps;
            if (steps.Count == 0)
            {
                sb.AppendLine("    empty [label=\"Empty Result\", shape=note];");
                sb.AppendLine("}");
                return sb.ToString();
            }

            sb.AppendLine($"    start [label=\"Start\", shape=circle, fillcolor=\"#90EE90\"];");
            sb.AppendLine($"    finish [label=\"Done\\n{result.TotalElapsed.TotalMilliseconds:F0}ms total\", shape=doublecircle, fillcolor=\"#FFB6C1\"];");

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var elapsed = $"{step.Elapsed.TotalMilliseconds:F0}ms";
                var label = opts.ShowStepNumbers
                    ? $"{i + 1}. {EscapeDot(step.StepName)}\\n{elapsed}"
                    : $"{EscapeDot(step.StepName)}\\n{elapsed}";
                sb.AppendLine($"    step{i} [label=\"{label}\"];");
            }

            sb.AppendLine($"    start -> step0;");

            for (int i = 0; i < steps.Count - 1; i++)
            {
                var edgeLabel = opts.ShowVariables
                    ? $" [label=\"{EscapeDot(steps[i].OutputVariable)}\"]"
                    : "";
                sb.AppendLine($"    step{i} -> step{i + 1}{edgeLabel};");
            }

            var lastLabel = opts.ShowVariables
                ? $" [label=\"{EscapeDot(steps[steps.Count - 1].OutputVariable)}\"]"
                : "";
            sb.AppendLine($"    step{steps.Count - 1} -> finish{lastLabel};");

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EscapeDot(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        #endregion

        #region ASCII

        private static string GenerateAscii(PromptChain chain, ChainVisualizationOptions opts)
        {
            var sb = new StringBuilder();
            var steps = chain.Steps;

            if (steps.Count == 0)
            {
                sb.AppendLine("(empty chain)");
                return sb.ToString();
            }

            sb.AppendLine("  ┌─────────┐");
            sb.AppendLine("  │  Start  │");
            sb.AppendLine("  └────┬────┘");

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var label = opts.ShowStepNumbers
                    ? $"{i + 1}. {step.Name}"
                    : step.Name;

                sb.AppendLine("       │");

                if (opts.ShowVariables && i > 0)
                {
                    var prevVar = steps[i - 1].OutputVariable;
                    sb.AppendLine($"   ({prevVar})");
                    sb.AppendLine("       │");
                }

                var boxWidth = Math.Max(label.Length + 4, 12);
                var padded = label.PadLeft((boxWidth - 2 + label.Length) / 2).PadRight(boxWidth - 2);
                var border = new string('─', boxWidth - 2);

                var leftPad = Math.Max(0, (7 - boxWidth / 2));
                var indent = new string(' ', leftPad);

                sb.AppendLine($"{indent}┌─{border}─┐");
                sb.AppendLine($"{indent}│ {padded} │");
                sb.AppendLine($"{indent}└─{border}─┘");
            }

            sb.AppendLine("       │");
            if (opts.ShowVariables)
            {
                sb.AppendLine($"   ({steps[steps.Count - 1].OutputVariable})");
                sb.AppendLine("       │");
            }

            sb.AppendLine("  ┌────┴────┐");
            sb.AppendLine("  │  Done   │");
            sb.AppendLine("  └─────────┘");

            return sb.ToString();
        }

        private static string GenerateAsciiFromResult(ChainResult result, ChainVisualizationOptions opts)
        {
            var sb = new StringBuilder();
            var steps = result.Steps;

            if (steps.Count == 0)
            {
                sb.AppendLine("(empty result)");
                return sb.ToString();
            }

            sb.AppendLine("  ┌─────────┐");
            sb.AppendLine("  │  Start  │");
            sb.AppendLine("  └────┬────┘");

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var elapsed = $"{step.Elapsed.TotalMilliseconds:F0}ms";
                var label = opts.ShowStepNumbers
                    ? $"{i + 1}. {step.StepName} ({elapsed})"
                    : $"{step.StepName} ({elapsed})";

                sb.AppendLine("       │");

                if (opts.ShowVariables && i > 0)
                {
                    var prevVar = steps[i - 1].OutputVariable;
                    sb.AppendLine($"   ({prevVar})");
                    sb.AppendLine("       │");
                }

                var boxWidth = Math.Max(label.Length + 4, 12);
                var padded = label.PadLeft((boxWidth - 2 + label.Length) / 2).PadRight(boxWidth - 2);
                var border = new string('─', boxWidth - 2);

                var leftPad = Math.Max(0, (7 - boxWidth / 2));
                var indent = new string(' ', leftPad);

                sb.AppendLine($"{indent}┌─{border}─┐");
                sb.AppendLine($"{indent}│ {padded} │");
                sb.AppendLine($"{indent}└─{border}─┘");
            }

            sb.AppendLine("       │");
            if (opts.ShowVariables)
            {
                sb.AppendLine($"   ({steps[steps.Count - 1].OutputVariable})");
                sb.AppendLine("       │");
            }

            var totalTime = $"{result.TotalElapsed.TotalMilliseconds:F0}ms total";
            sb.AppendLine("  ┌────┴────┐");
            sb.AppendLine($"  │  Done   │");
            sb.AppendLine($"  │ {totalTime.PadLeft(7)} │");
            sb.AppendLine("  └─────────┘");

            return sb.ToString();
        }

        #endregion
    }
}
