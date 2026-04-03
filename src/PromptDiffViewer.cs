using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Prompt
{
    /// <summary>
    /// Type of change in a line-level diff hunk.
    /// Renamed from LineDiffType to avoid collision with <see cref="Prompt.LineDiffType"/>
    /// in PromptDiff.cs which serves a different (field-level) purpose.
    /// </summary>
    public enum LineDiffType
    {
        /// <summary>Line is unchanged.</summary>
        Equal,
        /// <summary>Line was added.</summary>
        Added,
        /// <summary>Line was removed.</summary>
        Removed
    }

    /// <summary>
    /// A single line in a diff result.
    /// </summary>
    public class ViewerDiffLine
    {
        /// <summary>Gets the type of change.</summary>
        public LineDiffType Type { get; init; }

        /// <summary>Gets the line content.</summary>
        public string Content { get; init; } = "";

        /// <summary>Gets the line number in the old version (null if added).</summary>
        public int? OldLineNumber { get; init; }

        /// <summary>Gets the line number in the new version (null if removed).</summary>
        public int? NewLineNumber { get; init; }
    }

    /// <summary>
    /// Statistics about a diff result.
    /// </summary>
    public class DiffStats
    {
        /// <summary>Gets the number of added lines.</summary>
        public int Added { get; init; }

        /// <summary>Gets the number of removed lines.</summary>
        public int Removed { get; init; }

        /// <summary>Gets the number of unchanged lines.</summary>
        public int Unchanged { get; init; }

        /// <summary>Gets the similarity ratio (0.0 to 1.0).</summary>
        public double Similarity { get; init; }
    }

    /// <summary>
    /// Result of comparing two prompt versions.
    /// </summary>
    public class LineDiffResult
    {
        /// <summary>Gets the diff lines.</summary>
        public List<ViewerDiffLine> Lines { get; init; } = new();

        /// <summary>Gets the diff statistics.</summary>
        public DiffStats Stats { get; init; } = new();

        /// <summary>
        /// Renders the diff as a unified-diff-style string.
        /// </summary>
        public string ToUnifiedDiff(string oldLabel = "old", string newLabel = "new")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {oldLabel}");
            sb.AppendLine($"+++ {newLabel}");

            foreach (var line in Lines)
            {
                var prefix = line.Type switch
                {
                    LineDiffType.Added => "+",
                    LineDiffType.Removed => "-",
                    _ => " "
                };
                sb.AppendLine($"{prefix} {line.Content}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"  {Stats.Added} added, {Stats.Removed} removed, {Stats.Unchanged} unchanged ({Stats.Similarity:P0} similar)");
            return sb.ToString();
        }

        /// <summary>
        /// Renders the diff as an HTML fragment with color-coded lines.
        /// </summary>
        public string ToHtml()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<div class=\"prompt-diff\" style=\"font-family:monospace;font-size:13px;\">");

            foreach (var line in Lines)
            {
                var (bg, symbol) = line.Type switch
                {
                    LineDiffType.Added => ("#e6ffec", "+"),
                    LineDiffType.Removed => ("#ffebe9", "-"),
                    _ => ("#f6f8fa", " ")
                };

                var oldNum = line.OldLineNumber?.ToString() ?? "";
                var newNum = line.NewLineNumber?.ToString() ?? "";
                var escaped = System.Net.WebUtility.HtmlEncode(line.Content);

                sb.AppendLine($"  <div style=\"background:{bg};padding:1px 8px;white-space:pre-wrap;\">" +
                    $"<span style=\"color:#888;min-width:24px;display:inline-block;\">{oldNum,3}</span> " +
                    $"<span style=\"color:#888;min-width:24px;display:inline-block;\">{newNum,3}</span> " +
                    $"{symbol} {escaped}</div>");
            }

            sb.AppendLine("</div>");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Compares two prompt versions and produces a line-by-line diff.
    /// Uses the Myers diff algorithm (LCS-based) for optimal results.
    /// </summary>
    public static class PromptDiffViewer
    {
        /// <summary>
        /// Compares two prompt strings line by line.
        /// </summary>
        /// <param name="oldPrompt">The original prompt version.</param>
        /// <param name="newPrompt">The updated prompt version.</param>
        /// <param name="ignoreWhitespace">If true, trims lines before comparison.</param>
        /// <returns>A <see cref="LineDiffResult"/> with all changes and statistics.</returns>
        public static LineDiffResult Compare(string oldPrompt, string newPrompt, bool ignoreWhitespace = false)
        {
            if (oldPrompt == null) throw new ArgumentNullException(nameof(oldPrompt));
            if (newPrompt == null) throw new ArgumentNullException(nameof(newPrompt));

            var oldLines = SplitLines(oldPrompt);
            var newLines = SplitLines(newPrompt);

            var lcs = ComputeLCS(oldLines, newLines, ignoreWhitespace);
            var diffLines = BuildDiffLines(oldLines, newLines, lcs);

            int added = diffLines.Count(l => l.Type == LineDiffType.Added);
            int removed = diffLines.Count(l => l.Type == LineDiffType.Removed);
            int unchanged = diffLines.Count(l => l.Type == LineDiffType.Equal);
            int total = Math.Max(oldLines.Length, newLines.Length);
            double similarity = total == 0 ? 1.0 : (double)unchanged / total;

            return new LineDiffResult
            {
                Lines = diffLines,
                Stats = new DiffStats
                {
                    Added = added,
                    Removed = removed,
                    Unchanged = unchanged,
                    Similarity = similarity
                }
            };
        }

        /// <summary>
        /// Compares two prompts at the word level within each line, returning
        /// a list of per-line word-level diffs for changed lines.
        /// </summary>
        public static List<(int LineIndex, List<(LineDiffType Type, string Word)> Words)> CompareWords(
            string oldPrompt, string newPrompt)
        {
            if (oldPrompt == null) throw new ArgumentNullException(nameof(oldPrompt));
            if (newPrompt == null) throw new ArgumentNullException(nameof(newPrompt));

            var result = Compare(oldPrompt, newPrompt);
            var wordDiffs = new List<(int, List<(LineDiffType, string)>)>();

            // Find paired removed/added lines for word-level diff
            for (int i = 0; i < result.Lines.Count - 1; i++)
            {
                if (result.Lines[i].Type == LineDiffType.Removed &&
                    result.Lines[i + 1].Type == LineDiffType.Added)
                {
                    var oldWords = result.Lines[i].Content.Split(' ');
                    var newWords = result.Lines[i + 1].Content.Split(' ');
                    var wlcs = ComputeLCS(oldWords, newWords, false);
                    var wdiff = BuildWordDiff(oldWords, newWords, wlcs);
                    wordDiffs.Add((i, wdiff));
                }
            }

            return wordDiffs;
        }

        private static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }

        private static int[,] ComputeLCS(string[] a, string[] b, bool ignoreWhitespace)
        {
            int m = a.Length, n = b.Length;
            var dp = new int[m + 1, n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    var ai = ignoreWhitespace ? a[i - 1].Trim() : a[i - 1];
                    var bj = ignoreWhitespace ? b[j - 1].Trim() : b[j - 1];

                    if (ai == bj)
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    else
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }

            return dp;
        }

        private static List<ViewerDiffLine> BuildDiffLines(string[] oldLines, string[] newLines, int[,] dp)
        {
            var result = new List<ViewerDiffLine>();
            int i = oldLines.Length, j = newLines.Length;

            var stack = new Stack<ViewerDiffLine>();

            while (i > 0 || j > 0)
            {
                if (i > 0 && j > 0 && oldLines[i - 1] == newLines[j - 1])
                {
                    stack.Push(new ViewerDiffLine
                    {
                        Type = LineDiffType.Equal,
                        Content = oldLines[i - 1],
                        OldLineNumber = i,
                        NewLineNumber = j
                    });
                    i--; j--;
                }
                else if (j > 0 && (i == 0 || dp[i, j - 1] >= dp[i - 1, j]))
                {
                    stack.Push(new ViewerDiffLine
                    {
                        Type = LineDiffType.Added,
                        Content = newLines[j - 1],
                        OldLineNumber = null,
                        NewLineNumber = j
                    });
                    j--;
                }
                else
                {
                    stack.Push(new ViewerDiffLine
                    {
                        Type = LineDiffType.Removed,
                        Content = oldLines[i - 1],
                        OldLineNumber = i,
                        NewLineNumber = null
                    });
                    i--;
                }
            }

            while (stack.Count > 0)
                result.Add(stack.Pop());

            return result;
        }

        private static List<(LineDiffType, string)> BuildWordDiff(string[] oldWords, string[] newWords, int[,] dp)
        {
            var result = new List<(LineDiffType, string)>();
            int i = oldWords.Length, j = newWords.Length;
            var stack = new Stack<(LineDiffType, string)>();

            while (i > 0 || j > 0)
            {
                if (i > 0 && j > 0 && oldWords[i - 1] == newWords[j - 1])
                {
                    stack.Push((LineDiffType.Equal, oldWords[i - 1]));
                    i--; j--;
                }
                else if (j > 0 && (i == 0 || dp[i, j - 1] >= dp[i - 1, j]))
                {
                    stack.Push((LineDiffType.Added, newWords[j - 1]));
                    j--;
                }
                else
                {
                    stack.Push((LineDiffType.Removed, oldWords[i - 1]));
                    i--;
                }
            }

            while (stack.Count > 0)
                result.Add(stack.Pop());

            return result;
        }
    }
}
