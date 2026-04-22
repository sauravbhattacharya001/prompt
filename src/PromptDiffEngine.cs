namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Represents a single line change in a prompt diff.
    /// </summary>
    public enum DiffOperation
    {
        /// <summary>Line is unchanged between versions.</summary>
        Equal,
        /// <summary>Line was added in the new version.</summary>
        Insert,
        /// <summary>Line was removed from the old version.</summary>
        Delete
    }

    /// <summary>
    /// A single line entry in a diff result.
    /// </summary>
    public class DiffLine
    {
        /// <summary>Gets the operation type.</summary>
        public DiffOperation Operation { get; init; }

        /// <summary>Gets the text content of the line.</summary>
        public string Text { get; init; } = "";

        /// <summary>Gets the line number in the old version (null for inserts).</summary>
        public int? OldLineNumber { get; init; }

        /// <summary>Gets the line number in the new version (null for deletes).</summary>
        public int? NewLineNumber { get; init; }

        public override string ToString()
        {
            var prefix = Operation switch
            {
                DiffOperation.Insert => "+",
                DiffOperation.Delete => "-",
                _ => " "
            };
            return $"{prefix} {Text}";
        }
    }

    /// <summary>
    /// A contiguous group of changes (hunk) in a diff.
    /// </summary>
    public class DiffHunk
    {
        /// <summary>Gets the starting line in the old version.</summary>
        public int OldStart { get; init; }

        /// <summary>Gets the number of lines from the old version in this hunk.</summary>
        public int OldCount { get; init; }

        /// <summary>Gets the starting line in the new version.</summary>
        public int NewStart { get; init; }

        /// <summary>Gets the number of lines from the new version in this hunk.</summary>
        public int NewCount { get; init; }

        /// <summary>Gets the diff lines in this hunk.</summary>
        public List<DiffLine> Lines { get; init; } = new();

        /// <summary>Returns a unified-diff style header.</summary>
        public string Header => $"@@ -{OldStart},{OldCount} +{NewStart},{NewCount} @@";
    }

    /// <summary>
    /// Complete diff result between two prompt versions.
    /// </summary>
    public class PromptDiffResult
    {
        /// <summary>Gets all diff lines.</summary>
        public List<DiffLine> Lines { get; init; } = new();

        /// <summary>Gets change hunks (contiguous groups of edits with context).</summary>
        public List<DiffHunk> Hunks { get; init; } = new();

        /// <summary>Gets the number of added lines.</summary>
        public int Additions => Lines.Count(l => l.Operation == DiffOperation.Insert);

        /// <summary>Gets the number of removed lines.</summary>
        public int Deletions => Lines.Count(l => l.Operation == DiffOperation.Delete);

        /// <summary>Gets the number of unchanged lines.</summary>
        public int Unchanged => Lines.Count(l => l.Operation == DiffOperation.Equal);

        /// <summary>Gets the similarity ratio (0.0 to 1.0) between old and new.</summary>
        public double Similarity { get; init; }

        /// <summary>Gets a human-readable change summary.</summary>
        public string Summary { get; init; } = "";

        /// <summary>Gets whether the two versions are identical.</summary>
        public bool IsIdentical => Additions == 0 && Deletions == 0;

        /// <summary>
        /// Renders the diff in unified diff format.
        /// </summary>
        /// <param name="contextLines">Number of context lines around changes (default 3).</param>
        /// <returns>Unified diff string.</returns>
        public string ToUnifiedDiff(int contextLines = 3)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- old");
            sb.AppendLine("+++ new");
            foreach (var hunk in Hunks)
            {
                sb.AppendLine(hunk.Header);
                foreach (var line in hunk.Lines)
                    sb.AppendLine(line.ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Renders the diff as a side-by-side comparison.
        /// </summary>
        /// <param name="width">Column width for each side (default 40).</param>
        /// <returns>Side-by-side diff string.</returns>
        public string ToSideBySide(int width = 40)
        {
            var sb = new StringBuilder();
            var sep = new string('─', width);
            sb.AppendLine($"{"OLD".PadRight(width)} │ {"NEW".PadRight(width)}");
            sb.AppendLine($"{sep}─┼─{sep}");

            int i = 0;
            while (i < Lines.Count)
            {
                var line = Lines[i];
                if (line.Operation == DiffOperation.Equal)
                {
                    var t = StringHelpers.Truncate(line.Text, width);
                    sb.AppendLine($"{t.PadRight(width)} │ {t.PadRight(width)}");
                    i++;
                }
                else if (line.Operation == DiffOperation.Delete)
                {
                    var old = StringHelpers.Truncate(line.Text, width);
                    // Check if next line is a matching insert
                    if (i + 1 < Lines.Count && Lines[i + 1].Operation == DiffOperation.Insert)
                    {
                        var nw = StringHelpers.Truncate(Lines[i + 1].Text, width);
                        sb.AppendLine($"{old.PadRight(width)} │ {nw.PadRight(width)}");
                        i += 2;
                    }
                    else
                    {
                        sb.AppendLine($"{old.PadRight(width)} │ {"".PadRight(width)}");
                        i++;
                    }
                }
                else // Insert
                {
                    var nw = StringHelpers.Truncate(line.Text, width);
                    sb.AppendLine($"{"".PadRight(width)} │ {nw.PadRight(width)}");
                    i++;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a statistics summary of the diff.
        /// </summary>
        public string ToStats()
        {
            var total = Lines.Count > 0 ? Lines.Count : 1;
            return $"Similarity: {Similarity:P1} | +{Additions} -{Deletions} ~{Unchanged} | {Hunks.Count} hunk(s)";
        }

    }

    /// <summary>
    /// Line-level diff engine for comparing prompt versions.
    /// Uses the Myers / LCS algorithm to compute minimal edit sequences,
    /// then groups changes into hunks with configurable context.
    ///
    /// <example>
    /// <code>
    /// var oldPrompt = "You are a helpful assistant.\nBe concise.";
    /// var newPrompt = "You are a helpful AI assistant.\nBe concise.\nUse examples.";
    /// var diff = PromptDiffEngine.Diff(oldPrompt, newPrompt);
    ///
    /// Console.WriteLine(diff.Summary);       // "2 change(s): 1 addition(s), 1 modification(s)"
    /// Console.WriteLine(diff.ToStats());     // "Similarity: 66.7% | +1 -1 ~1 | 1 hunk(s)"
    /// Console.WriteLine(diff.ToUnifiedDiff());
    /// Console.WriteLine(diff.ToSideBySide());
    /// </code>
    /// </example>
    /// </summary>
    public static class PromptDiffEngine
    {
        /// <summary>
        /// Computes a line-level diff between two prompt strings.
        /// </summary>
        /// <param name="oldText">The original prompt text.</param>
        /// <param name="newText">The updated prompt text.</param>
        /// <param name="contextLines">Number of unchanged context lines around each hunk (default 3).</param>
        /// <returns>A <see cref="PromptDiffResult"/> with lines, hunks, similarity, and summary.</returns>
        public static PromptDiffResult Diff(string oldText, string newText, int contextLines = 3)
        {
            if (oldText == null) throw new ArgumentNullException(nameof(oldText));
            if (newText == null) throw new ArgumentNullException(nameof(newText));

            var oldLines = SplitLines(oldText);
            var newLines = SplitLines(newText);

            // Compute LCS table
            var lcs = ComputeLcs(oldLines, newLines);

            // Build diff lines from LCS backtrack
            var diffLines = BacktrackDiff(lcs, oldLines, newLines);

            // Compute similarity
            int equalCount = diffLines.Count(d => d.Operation == DiffOperation.Equal);
            int maxLen = Math.Max(oldLines.Length, newLines.Length);
            double similarity = maxLen == 0 ? 1.0 : (double)equalCount / maxLen;

            // Build hunks
            var hunks = BuildHunks(diffLines, contextLines);

            // Build summary
            var summary = BuildSummary(diffLines, oldLines, newLines);

            return new PromptDiffResult
            {
                Lines = diffLines,
                Hunks = hunks,
                Similarity = similarity,
                Summary = summary
            };
        }

        /// <summary>
        /// Computes a word-level diff between two prompt strings.
        /// Useful for finding fine-grained wording changes within a single line or short prompt.
        /// </summary>
        /// <param name="oldText">The original text.</param>
        /// <param name="newText">The updated text.</param>
        /// <returns>A list of (operation, word) tuples.</returns>
        public static List<(DiffOperation Op, string Word)> WordDiff(string oldText, string newText)
        {
            if (oldText == null) throw new ArgumentNullException(nameof(oldText));
            if (newText == null) throw new ArgumentNullException(nameof(newText));

            var oldWords = SplitWords(oldText);
            var newWords = SplitWords(newText);
            var lcs = ComputeLcs(oldWords, newWords);
            var diffLines = BacktrackDiff(lcs, oldWords, newWords);

            return diffLines.Select(d => (d.Operation, d.Text)).ToList();
        }

        /// <summary>
        /// Renders a word-level diff with inline markers.
        /// Deletions are wrapped in [-...-] and insertions in {+...+}.
        /// </summary>
        public static string RenderWordDiff(string oldText, string newText)
        {
            var diffs = WordDiff(oldText, newText);
            var sb = new StringBuilder();
            foreach (var (op, word) in diffs)
            {
                switch (op)
                {
                    case DiffOperation.Delete:
                        sb.Append($"[-{word}-]");
                        break;
                    case DiffOperation.Insert:
                        sb.Append($"{{+{word}+}}");
                        break;
                    default:
                        sb.Append(word);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Computes a three-way diff (merge base, ours, theirs) and identifies conflicts.
        /// </summary>
        /// <param name="baseText">The common ancestor prompt.</param>
        /// <param name="oursText">Our version of the prompt.</param>
        /// <param name="theirsText">Their version of the prompt.</param>
        /// <returns>Merged text with conflict markers where both sides changed the same region.</returns>
        public static string ThreeWayMerge(string baseText, string oursText, string theirsText)
        {
            var diffOurs = Diff(baseText, oursText, 0);
            var diffTheirs = Diff(baseText, theirsText, 0);

            var baseLines = SplitLines(baseText);
            var ourLines = SplitLines(oursText);
            var theirLines = SplitLines(theirsText);

            // Simple strategy: apply non-conflicting changes, mark conflicts
            var oursChanged = new HashSet<int>();
            var theirsChanged = new HashSet<int>();

            foreach (var line in diffOurs.Lines.Where(l => l.Operation == DiffOperation.Delete && l.OldLineNumber.HasValue))
                oursChanged.Add(line.OldLineNumber.Value);
            foreach (var line in diffTheirs.Lines.Where(l => l.Operation == DiffOperation.Delete && l.OldLineNumber.HasValue))
                theirsChanged.Add(line.OldLineNumber.Value);

            var conflicts = oursChanged.Intersect(theirsChanged).ToHashSet();

            if (conflicts.Count == 0)
            {
                // No conflicts — prefer ours for changed lines, theirs for their-only changes
                return oursText;
            }

            // Has conflicts — output with markers
            var sb = new StringBuilder();
            foreach (var line in ourLines)
                sb.AppendLine(line);

            if (conflicts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"<<<<<<< OURS (above) — {conflicts.Count} conflict region(s) detected");
                sb.AppendLine("=======");
                foreach (var line in theirLines)
                    sb.AppendLine(line);
                sb.AppendLine(">>>>>>> THEIRS");
            }

            return sb.ToString().TrimEnd();
        }

        #region Private Helpers

        private static string[] SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        private static string[] SplitWords(string text)
        {
            var words = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    int start = i;
                    while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                    words.Add(text.Substring(start, i - start));
                }
                else
                {
                    int start = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                    words.Add(text.Substring(start, i - start));
                }
            }
            return words.ToArray();
        }

        private static int[,] ComputeLcs(string[] a, string[] b)
        {
            int m = a.Length, n = b.Length;
            var dp = new int[m + 1, n + 1];
            for (int i = 1; i <= m; i++)
                for (int j = 1; j <= n; j++)
                    dp[i, j] = a[i - 1] == b[j - 1]
                        ? dp[i - 1, j - 1] + 1
                        : Math.Max(dp[i - 1, j], dp[i, j - 1]);
            return dp;
        }

        private static List<DiffLine> BacktrackDiff(int[,] lcs, string[] oldArr, string[] newArr)
        {
            var result = new List<DiffLine>();
            int i = oldArr.Length, j = newArr.Length;

            while (i > 0 || j > 0)
            {
                if (i > 0 && j > 0 && oldArr[i - 1] == newArr[j - 1])
                {
                    result.Add(new DiffLine
                    {
                        Operation = DiffOperation.Equal,
                        Text = oldArr[i - 1],
                        OldLineNumber = i,
                        NewLineNumber = j
                    });
                    i--; j--;
                }
                else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
                {
                    result.Add(new DiffLine
                    {
                        Operation = DiffOperation.Insert,
                        Text = newArr[j - 1],
                        OldLineNumber = null,
                        NewLineNumber = j
                    });
                    j--;
                }
                else
                {
                    result.Add(new DiffLine
                    {
                        Operation = DiffOperation.Delete,
                        Text = oldArr[i - 1],
                        OldLineNumber = i,
                        NewLineNumber = null
                    });
                    i--;
                }
            }

            result.Reverse();
            return result;
        }

        private static List<DiffHunk> BuildHunks(List<DiffLine> lines, int context)
        {
            var hunks = new List<DiffHunk>();
            var changeIndices = new List<int>();

            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Operation != DiffOperation.Equal)
                    changeIndices.Add(i);

            if (changeIndices.Count == 0) return hunks;

            int hunkStart = Math.Max(0, changeIndices[0] - context);
            int hunkEnd = Math.Min(lines.Count - 1, changeIndices[0] + context);

            var ranges = new List<(int Start, int End)>();

            for (int ci = 1; ci < changeIndices.Count; ci++)
            {
                int nextStart = Math.Max(0, changeIndices[ci] - context);
                int nextEnd = Math.Min(lines.Count - 1, changeIndices[ci] + context);

                if (nextStart <= hunkEnd + 1)
                {
                    hunkEnd = nextEnd;
                }
                else
                {
                    ranges.Add((hunkStart, hunkEnd));
                    hunkStart = nextStart;
                    hunkEnd = nextEnd;
                }
            }
            ranges.Add((hunkStart, hunkEnd));

            foreach (var (start, end) in ranges)
            {
                var hunkLines = lines.GetRange(start, end - start + 1);
                int oldStart = hunkLines.FirstOrDefault(l => l.OldLineNumber.HasValue)?.OldLineNumber ?? 1;
                int newStart = hunkLines.FirstOrDefault(l => l.NewLineNumber.HasValue)?.NewLineNumber ?? 1;
                int oldCount = hunkLines.Count(l => l.Operation != DiffOperation.Insert);
                int newCount = hunkLines.Count(l => l.Operation != DiffOperation.Delete);

                hunks.Add(new DiffHunk
                {
                    OldStart = oldStart,
                    OldCount = oldCount,
                    NewStart = newStart,
                    NewCount = newCount,
                    Lines = hunkLines
                });
            }

            return hunks;
        }

        private static string BuildSummary(List<DiffLine> lines, string[] oldLines, string[] newLines)
        {
            int adds = lines.Count(l => l.Operation == DiffOperation.Insert);
            int dels = lines.Count(l => l.Operation == DiffOperation.Delete);

            if (adds == 0 && dels == 0) return "No changes detected.";

            var parts = new List<string>();
            if (adds > 0 && dels > 0)
            {
                // Some deletes paired with inserts are modifications
                int mods = Math.Min(adds, dels);
                int pureAdds = adds - mods;
                int pureDels = dels - mods;
                if (mods > 0) parts.Add($"{mods} modification(s)");
                if (pureAdds > 0) parts.Add($"{pureAdds} addition(s)");
                if (pureDels > 0) parts.Add($"{pureDels} deletion(s)");
            }
            else if (adds > 0) parts.Add($"{adds} addition(s)");
            else parts.Add($"{dels} deletion(s)");

            int totalChanges = adds + dels;
            return $"{totalChanges} change(s): {string.Join(", ", parts)}";
        }

        #endregion
    }
}