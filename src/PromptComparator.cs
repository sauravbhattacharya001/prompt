namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A single dimension in a prompt comparison.
    /// </summary>
    public class PromptComparisonDimension
    {
        /// <summary>Gets the dimension name (e.g., "Word Count", "Variables").</summary>
        public string Name { get; init; } = "";

        /// <summary>Gets the value for prompt A.</summary>
        public string ValueA { get; init; } = "";

        /// <summary>Gets the value for prompt B.</summary>
        public string ValueB { get; init; } = "";

        /// <summary>Gets a short verdict: "A wins", "B wins", "Tie", or a neutral note.</summary>
        public string Verdict { get; init; } = "";
    }

    /// <summary>
    /// High-level structural comparison result for two prompts.
    /// </summary>
    public class PromptComparisonResult
    {
        /// <summary>Gets the individual dimension comparisons.</summary>
        public List<PromptComparisonDimension> Dimensions { get; init; } = new();

        /// <summary>Gets variables unique to prompt A.</summary>
        public List<string> VariablesOnlyInA { get; init; } = new();

        /// <summary>Gets variables unique to prompt B.</summary>
        public List<string> VariablesOnlyInB { get; init; } = new();

        /// <summary>Gets variables common to both prompts.</summary>
        public List<string> SharedVariables { get; init; } = new();

        /// <summary>Gets sections unique to prompt A.</summary>
        public List<string> SectionsOnlyInA { get; init; } = new();

        /// <summary>Gets sections unique to prompt B.</summary>
        public List<string> SectionsOnlyInB { get; init; } = new();

        /// <summary>Gets sections common to both prompts.</summary>
        public List<string> SharedSections { get; init; } = new();

        /// <summary>Gets overall similarity score (0.0 to 1.0).</summary>
        public double Similarity { get; init; }

        /// <summary>Gets a plain-text summary of the comparison.</summary>
        public string Summary { get; init; } = "";

        /// <summary>
        /// Renders the comparison as a formatted text report.
        /// </summary>
        public string ToReport(string labelA = "Prompt A", string labelB = "Prompt B")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"╔══════════════════════════════════════════════════╗");
            sb.AppendLine($"║          Prompt Comparison Report                ║");
            sb.AppendLine($"╚══════════════════════════════════════════════════╝");
            sb.AppendLine();

            // Dimensions table
            int nameWidth = Math.Max(20, Dimensions.Max(d => d.Name.Length) + 2);
            int valWidth = 18;
            sb.AppendLine($"  {"Dimension".PadRight(nameWidth)} {labelA.PadRight(valWidth)} {labelB.PadRight(valWidth)} Verdict");
            sb.AppendLine($"  {new string('─', nameWidth)} {new string('─', valWidth)} {new string('─', valWidth)} ───────────");

            foreach (var dim in Dimensions)
            {
                sb.AppendLine($"  {dim.Name.PadRight(nameWidth)} {dim.ValueA.PadRight(valWidth)} {dim.ValueB.PadRight(valWidth)} {dim.Verdict}");
            }

            sb.AppendLine();

            // Variables
            if (SharedVariables.Count > 0 || VariablesOnlyInA.Count > 0 || VariablesOnlyInB.Count > 0)
            {
                sb.AppendLine("  Variables:");
                if (SharedVariables.Count > 0)
                    sb.AppendLine($"    Shared:       {string.Join(", ", SharedVariables)}");
                if (VariablesOnlyInA.Count > 0)
                    sb.AppendLine($"    Only in {labelA}: {string.Join(", ", VariablesOnlyInA)}");
                if (VariablesOnlyInB.Count > 0)
                    sb.AppendLine($"    Only in {labelB}: {string.Join(", ", VariablesOnlyInB)}");
                sb.AppendLine();
            }

            // Sections
            if (SharedSections.Count > 0 || SectionsOnlyInA.Count > 0 || SectionsOnlyInB.Count > 0)
            {
                sb.AppendLine("  Sections:");
                if (SharedSections.Count > 0)
                    sb.AppendLine($"    Shared:       {string.Join(", ", SharedSections)}");
                if (SectionsOnlyInA.Count > 0)
                    sb.AppendLine($"    Only in {labelA}: {string.Join(", ", SectionsOnlyInA)}");
                if (SectionsOnlyInB.Count > 0)
                    sb.AppendLine($"    Only in {labelB}: {string.Join(", ", SectionsOnlyInB)}");
                sb.AppendLine();
            }

            sb.AppendLine($"  Overall Similarity: {Similarity:P0}");
            sb.AppendLine();
            sb.AppendLine($"  Summary: {Summary}");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Compares two prompts at a structural/analytical level, producing a high-level
    /// comparison report covering word count, variables, sections, estimated tokens,
    /// readability, and structural similarity. Unlike <see cref="PromptDiffViewer"/>
    /// which shows line-by-line changes, this provides a bird's-eye analytical view.
    /// </summary>
    public static class PromptComparator
    {
        private static readonly Regex VariablePattern = new(@"\{\{?\s*(\w+)\s*\}?\}", RegexOptions.Compiled);
        private static readonly Regex SectionPattern = new(@"^#{1,6}\s+(.+)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex SentencePattern = new(@"[.!?]+\s+|[.!?]+$", RegexOptions.Compiled);
        private static readonly Regex BulletPattern = new(@"^\s*[-*•]\s", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex NumberedPattern = new(@"^\s*\d+[.)]\s", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex CodeBlockPattern = new(@"```[\s\S]*?```", RegexOptions.Compiled);
        private static readonly Regex XmlTagPattern = new(@"<\w+[^>]*>", RegexOptions.Compiled);

        /// <summary>
        /// Compares two prompts and returns a structured comparison result.
        /// </summary>
        /// <param name="promptA">The first prompt.</param>
        /// <param name="promptB">The second prompt.</param>
        /// <returns>A <see cref="PromptComparisonResult"/> with dimensional analysis.</returns>
        public static PromptComparisonResult Compare(string promptA, string promptB)
        {
            if (promptA == null) throw new ArgumentNullException(nameof(promptA));
            if (promptB == null) throw new ArgumentNullException(nameof(promptB));

            var statsA = Analyze(promptA);
            var statsB = Analyze(promptB);

            var dimensions = new List<PromptComparisonDimension>
            {
                NumericDimension("Characters", statsA.CharCount, statsB.CharCount, lowerBetter: true),
                NumericDimension("Words", statsA.WordCount, statsB.WordCount, lowerBetter: true),
                NumericDimension("Lines", statsA.LineCount, statsB.LineCount),
                NumericDimension("Sentences", statsA.SentenceCount, statsB.SentenceCount),
                NumericDimension("Est. Tokens", statsA.EstimatedTokens, statsB.EstimatedTokens, lowerBetter: true),
                NumericDimension("Variables", statsA.VariableCount, statsB.VariableCount),
                NumericDimension("Sections", statsA.SectionCount, statsB.SectionCount),
                NumericDimension("Bullet Points", statsA.BulletCount, statsB.BulletCount),
                NumericDimension("Code Blocks", statsA.CodeBlockCount, statsB.CodeBlockCount),
                NumericDimension("XML Tags", statsA.XmlTagCount, statsB.XmlTagCount),
                new PromptComparisonDimension
                {
                    Name = "Avg Word Length",
                    ValueA = $"{statsA.AvgWordLength:F1}",
                    ValueB = $"{statsB.AvgWordLength:F1}",
                    Verdict = Math.Abs(statsA.AvgWordLength - statsB.AvgWordLength) < 0.5 ? "Similar" : "Different"
                },
                new PromptComparisonDimension
                {
                    Name = "Avg Sentence Length",
                    ValueA = $"{statsA.AvgSentenceLength:F1} words",
                    ValueB = $"{statsB.AvgSentenceLength:F1} words",
                    Verdict = statsA.AvgSentenceLength > 0 && statsB.AvgSentenceLength > 0
                        ? (Math.Abs(statsA.AvgSentenceLength - statsB.AvgSentenceLength) < 5 ? "Similar" : "Different")
                        : "N/A"
                },
                new PromptComparisonDimension
                {
                    Name = "Structure",
                    ValueA = statsA.StructureType,
                    ValueB = statsB.StructureType,
                    Verdict = statsA.StructureType == statsB.StructureType ? "Same" : "Different"
                }
            };

            var varsA = new HashSet<string>(statsA.Variables);
            var varsB = new HashSet<string>(statsB.Variables);
            var sharedVars = varsA.Intersect(varsB).OrderBy(v => v).ToList();
            var onlyA = varsA.Except(varsB).OrderBy(v => v).ToList();
            var onlyB = varsB.Except(varsA).OrderBy(v => v).ToList();

            var secsA = new HashSet<string>(statsA.Sections, StringComparer.OrdinalIgnoreCase);
            var secsB = new HashSet<string>(statsB.Sections, StringComparer.OrdinalIgnoreCase);
            var sharedSecs = secsA.Intersect(secsB, StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            var secsOnlyA = secsA.Except(secsB, StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            var secsOnlyB = secsB.Except(secsA, StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();

            double similarity = ComputeSimilarity(statsA, statsB, sharedVars.Count, sharedSecs.Count);

            string summary = BuildSummary(statsA, statsB, similarity);

            return new PromptComparisonResult
            {
                Dimensions = dimensions,
                VariablesOnlyInA = onlyA,
                VariablesOnlyInB = onlyB,
                SharedVariables = sharedVars,
                SectionsOnlyInA = secsOnlyA,
                SectionsOnlyInB = secsOnlyB,
                SharedSections = sharedSecs,
                Similarity = similarity,
                Summary = summary
            };
        }

        private static PromptComparisonDimension NumericDimension(string name, int a, int b, bool lowerBetter = false)
        {
            string verdict;
            if (a == b)
                verdict = "Tie";
            else if (lowerBetter)
                verdict = a < b ? "A more concise" : "B more concise";
            else
                verdict = Math.Abs(a - b) <= Math.Max(1, (int)(Math.Max(a, b) * 0.1)) ? "Similar" : $"Δ {Math.Abs(a - b)}";

            return new PromptComparisonDimension
            {
                Name = name,
                ValueA = a.ToString(),
                ValueB = b.ToString(),
                Verdict = verdict
            };
        }

        private static double ComputeSimilarity(PromptStats a, PromptStats b, int sharedVars, int sharedSecs)
        {
            // Weighted similarity across multiple dimensions
            double wordSim = 1.0 - Math.Abs(a.WordCount - b.WordCount) / (double)Math.Max(Math.Max(a.WordCount, b.WordCount), 1);
            double lineSim = 1.0 - Math.Abs(a.LineCount - b.LineCount) / (double)Math.Max(Math.Max(a.LineCount, b.LineCount), 1);

            int totalVars = a.Variables.Union(b.Variables).Count();
            double varSim = totalVars == 0 ? 1.0 : (double)sharedVars / totalVars;

            int totalSecs = a.Sections.Union(b.Sections, StringComparer.OrdinalIgnoreCase).Count();
            double secSim = totalSecs == 0 ? 1.0 : (double)sharedSecs / totalSecs;

            double structSim = a.StructureType == b.StructureType ? 1.0 : 0.3;

            // Jaccard similarity on word sets — delegates to shared helper
            // to avoid duplicating intersection/union logic
            double jaccardSim = a.WordSet.Count == 0 && b.WordSet.Count == 0
                ? 1.0
                : TextAnalysisHelpers.JaccardSimilarity(a.WordSet, b.WordSet);

            return Math.Clamp(
                wordSim * 0.1 + lineSim * 0.05 + varSim * 0.15 + secSim * 0.15 + structSim * 0.1 + jaccardSim * 0.45,
                0.0, 1.0);
        }

        private static string BuildSummary(PromptStats a, PromptStats b, double similarity)
        {
            var parts = new List<string>();

            if (similarity > 0.85)
                parts.Add("The prompts are very similar structurally and share most content.");
            else if (similarity > 0.5)
                parts.Add("The prompts share moderate overlap but have notable differences.");
            else
                parts.Add("The prompts are substantially different.");

            int tokenDiff = Math.Abs(a.EstimatedTokens - b.EstimatedTokens);
            if (tokenDiff > 100)
            {
                string cheaper = a.EstimatedTokens < b.EstimatedTokens ? "A" : "B";
                parts.Add($"Prompt {cheaper} uses ~{tokenDiff} fewer estimated tokens.");
            }

            if (a.StructureType != b.StructureType)
                parts.Add($"They use different structures ({a.StructureType} vs {b.StructureType}).");

            return string.Join(" ", parts);
        }

        private static PromptStats Analyze(string prompt)
        {
            var lines = prompt.Split('\n');
            var words = prompt.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var variables = VariablePattern.Matches(prompt).Select(m => m.Groups[1].Value).Distinct().ToList();
            var sections = SectionPattern.Matches(prompt).Select(m => m.Groups[1].Value.Trim()).ToList();
            int sentenceCount = Math.Max(1, SentencePattern.Matches(prompt).Count);
            int bulletCount = BulletPattern.Matches(prompt).Count;
            int numberedCount = NumberedPattern.Matches(prompt).Count;
            int codeBlockCount = CodeBlockPattern.Matches(prompt).Count;
            int xmlTagCount = XmlTagPattern.Matches(prompt).Count;

            double avgWordLen = words.Length == 0 ? 0 : words.Average(w => w.Length);
            double avgSentenceLen = sentenceCount == 0 ? 0 : (double)words.Length / sentenceCount;

            string structureType = DetermineStructure(sections.Count, bulletCount, numberedCount, codeBlockCount, xmlTagCount);

            return new PromptStats
            {
                CharCount = prompt.Length,
                WordCount = words.Length,
                LineCount = lines.Length,
                SentenceCount = sentenceCount,
                EstimatedTokens = (int)(words.Length * 1.3),
                VariableCount = variables.Count,
                Variables = variables,
                SectionCount = sections.Count,
                Sections = sections,
                BulletCount = bulletCount + numberedCount,
                CodeBlockCount = codeBlockCount,
                XmlTagCount = xmlTagCount,
                AvgWordLength = avgWordLen,
                AvgSentenceLength = avgSentenceLen,
                StructureType = structureType,
                WordSet = new HashSet<string>(
                    words.Select(w => w.ToLowerInvariant().Trim(',', '.', '!', '?', ';', ':'))
                         .Where(w => w.Length > 0),
                    StringComparer.OrdinalIgnoreCase)
            };
        }

        private static string DetermineStructure(int sections, int bullets, int numbered, int codeBlocks, int xmlTags)
        {
            if (sections >= 3) return "Document";
            if (xmlTags >= 3) return "XML-structured";
            if (codeBlocks >= 2) return "Code-heavy";
            if (bullets >= 5 || numbered >= 5) return "List-based";
            if (sections >= 1) return "Sectioned";
            if (bullets >= 1 || numbered >= 1) return "Mixed";
            return "Prose";
        }

        private class PromptStats
        {
            public int CharCount;
            public int WordCount;
            public int LineCount;
            public int SentenceCount;
            public int EstimatedTokens;
            public int VariableCount;
            public List<string> Variables = new();
            public int SectionCount;
            public List<string> Sections = new();
            public int BulletCount;
            public int CodeBlockCount;
            public int XmlTagCount;
            public double AvgWordLength;
            public double AvgSentenceLength;
            public string StructureType = "";
            public HashSet<string> WordSet = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
