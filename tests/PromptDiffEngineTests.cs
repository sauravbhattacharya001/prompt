namespace Prompt.Tests
{
    using System;
    using System.Linq;
    using Prompt;
    using Xunit;

    public class PromptDiffEngineTests
    {
        // ---------- Diff: basic shape ----------

        [Fact]
        public void Diff_IdenticalInputs_ReportsNoChanges()
        {
            var r = PromptDiffEngine.Diff("a\nb\nc", "a\nb\nc");
            Assert.True(r.IsIdentical);
            Assert.Equal(0, r.Additions);
            Assert.Equal(0, r.Deletions);
            Assert.Equal(3, r.Unchanged);
            Assert.Equal(1.0, r.Similarity);
            Assert.Empty(r.Hunks);
            Assert.Equal("No changes detected.", r.Summary);
        }

        [Fact]
        public void Diff_PureAddition_CountsAndSummary()
        {
            var r = PromptDiffEngine.Diff("a\nb", "a\nb\nc");
            Assert.Equal(1, r.Additions);
            Assert.Equal(0, r.Deletions);
            Assert.Contains("1 addition(s)", r.Summary);
            Assert.False(r.IsIdentical);
        }

        [Fact]
        public void Diff_PureDeletion_CountsAndSummary()
        {
            var r = PromptDiffEngine.Diff("a\nb\nc", "a\nb");
            Assert.Equal(0, r.Additions);
            Assert.Equal(1, r.Deletions);
            Assert.Contains("1 deletion(s)", r.Summary);
        }

        [Fact]
        public void Diff_Modification_ReportedAsModification()
        {
            var r = PromptDiffEngine.Diff("a\nb\nc", "a\nB\nc");
            Assert.Equal(1, r.Additions);
            Assert.Equal(1, r.Deletions);
            Assert.Contains("1 modification(s)", r.Summary);
        }

        [Fact]
        public void Diff_NullInputs_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => PromptDiffEngine.Diff(null!, "x"));
            Assert.Throws<ArgumentNullException>(() => PromptDiffEngine.Diff("x", null!));
        }

        [Fact]
        public void Diff_EmptyVsEmpty_SimilarityIsOne()
        {
            var r = PromptDiffEngine.Diff("", "");
            Assert.Equal(1.0, r.Similarity);
            Assert.True(r.IsIdentical);
        }

        [Fact]
        public void Diff_HandlesMixedLineEndings()
        {
            var r = PromptDiffEngine.Diff("a\r\nb\r\nc", "a\nb\nc");
            // Lines compare equal because split normalizes both \r\n and \n.
            Assert.True(r.IsIdentical);
        }

        // ---------- Hunks & line numbers ----------

        [Fact]
        public void Diff_HunkLineNumbersArePopulated()
        {
            var r = PromptDiffEngine.Diff("a\nb\nc\nd\ne", "a\nb\nX\nd\ne");
            Assert.Single(r.Hunks);
            var hunk = r.Hunks[0];
            // The change is on line 3 of both sides
            Assert.True(hunk.OldStart >= 1);
            Assert.True(hunk.NewStart >= 1);
            Assert.Contains(hunk.Lines, l => l.Operation == DiffOperation.Delete && l.OldLineNumber == 3);
            Assert.Contains(hunk.Lines, l => l.Operation == DiffOperation.Insert && l.NewLineNumber == 3);
        }

        [Fact]
        public void Diff_MultipleFarApartChanges_ProduceMultipleHunks()
        {
            var oldText = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"line{i}"));
            var newText = oldText.Replace("line2", "LINE2").Replace("line18", "LINE18");
            var r = PromptDiffEngine.Diff(oldText, newText, contextLines: 1);
            Assert.Equal(2, r.Hunks.Count);
        }

        [Fact]
        public void Diff_NearbyChanges_CollapseIntoOneHunk()
        {
            var oldText = "a\nb\nc\nd\ne\nf\ng";
            var newText = "a\nB\nc\nD\ne\nf\ng";
            // With default context=3 the two changes (lines 2 and 4) collapse.
            var r = PromptDiffEngine.Diff(oldText, newText);
            Assert.Single(r.Hunks);
        }

        // ---------- ToUnifiedDiff context-lines regression ----------

        [Fact]
        public void ToUnifiedDiff_RespectsCallerSuppliedContext()
        {
            var oldText = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"line{i}"));
            var newText = oldText.Replace("line10", "LINE10");

            // Built with context=0
            var r = PromptDiffEngine.Diff(oldText, newText, contextLines: 0);
            var zeroCtx = r.ToUnifiedDiff(0);
            var bigCtx = r.ToUnifiedDiff(5);

            // The big-context render should include the surrounding lines even though
            // the result was originally built with context=0.
            Assert.Contains("line8", bigCtx);
            Assert.Contains("line12", bigCtx);
            Assert.DoesNotContain("line8", zeroCtx);
        }

        [Fact]
        public void ToUnifiedDiff_NegativeContext_Throws()
        {
            var r = PromptDiffEngine.Diff("a", "b");
            Assert.Throws<ArgumentOutOfRangeException>(() => r.ToUnifiedDiff(-1));
        }

        [Fact]
        public void ToUnifiedDiff_AlwaysIncludesFileHeaders()
        {
            var r = PromptDiffEngine.Diff("a", "b");
            var s = r.ToUnifiedDiff();
            Assert.Contains("--- old", s);
            Assert.Contains("+++ new", s);
            Assert.Contains("@@", s);
        }

        // ---------- ToSideBySide ----------

        [Fact]
        public void ToSideBySide_PairsDeleteWithFollowingInsert()
        {
            var r = PromptDiffEngine.Diff("alpha\nbeta", "alpha\nBETA");
            var sbs = r.ToSideBySide(width: 10);
            // The "beta" delete should be paired with the "BETA" insert on the same row.
            Assert.Matches(@"(?m)^beta\s*\u2502\s*BETA", sbs);
        }

        // ---------- ToStats ----------

        [Fact]
        public void ToStats_FormatsCountsAndHunks()
        {
            var r = PromptDiffEngine.Diff("a\nb\nc", "a\nB\nc");
            var stats = r.ToStats();
            Assert.Contains("+1", stats);
            Assert.Contains("-1", stats);
            Assert.Contains("hunk", stats);
        }

        // ---------- WordDiff / RenderWordDiff ----------

        [Fact]
        public void WordDiff_NullInputs_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => PromptDiffEngine.WordDiff(null!, "x"));
            Assert.Throws<ArgumentNullException>(() => PromptDiffEngine.WordDiff("x", null!));
        }

        [Fact]
        public void WordDiff_FindsInsertedAndDeletedWords()
        {
            var diffs = PromptDiffEngine.WordDiff("the quick brown fox", "the slow brown fox");
            Assert.Contains(diffs, d => d.Op == DiffOperation.Delete && d.Word == "quick");
            Assert.Contains(diffs, d => d.Op == DiffOperation.Insert && d.Word == "slow");
        }

        [Fact]
        public void RenderWordDiff_WrapsDeletionsAndInsertions()
        {
            var rendered = PromptDiffEngine.RenderWordDiff("the quick brown fox", "the slow brown fox");
            Assert.Contains("[-quick-]", rendered);
            Assert.Contains("{+slow+}", rendered);
            Assert.Contains("the ", rendered);
            Assert.Contains(" brown ", rendered);
        }

        [Fact]
        public void RenderWordDiff_NoChange_EqualsOriginal()
        {
            var s = PromptDiffEngine.RenderWordDiff("hello world", "hello world");
            Assert.Equal("hello world", s);
        }

        // ---------- ThreeWayMerge ----------

        [Fact]
        public void ThreeWayMerge_NoChanges_ReturnsBase()
        {
            var s = PromptDiffEngine.ThreeWayMerge("a\nb\nc", "a\nb\nc", "a\nb\nc");
            Assert.Equal("a\nb\nc", s);
        }

        [Fact]
        public void ThreeWayMerge_OnlyOursChanged_ReturnsOurs()
        {
            var s = PromptDiffEngine.ThreeWayMerge("a\nb\nc", "a\nB\nc", "a\nb\nc");
            Assert.Equal("a\nB\nc", s);
        }

        [Fact]
        public void ThreeWayMerge_OnlyTheirsChanged_ReturnsTheirs()
        {
            // Regression: previous implementation silently dropped theirs-only changes
            // when ours was unchanged it returned oursText (which happens to equal base
            // here, but the previous code path went through the delete-only tracker and
            // would also discard theirs-only inserts in more complex cases).
            var s = PromptDiffEngine.ThreeWayMerge("a\nb\nc", "a\nb\nc", "a\nB\nc");
            Assert.Equal("a\nB\nc", s);
        }

        [Fact]
        public void ThreeWayMerge_BothSidesEditSameLine_EmitsConflictMarkers()
        {
            var s = PromptDiffEngine.ThreeWayMerge("a\nb\nc", "a\nOURS\nc", "a\nTHEIRS\nc");
            Assert.Contains("<<<<<<< OURS", s);
            Assert.Contains("=======", s);
            Assert.Contains(">>>>>>> THEIRS", s);
            Assert.Contains("OURS", s);
            Assert.Contains("THEIRS", s);
        }

        [Fact]
        public void ThreeWayMerge_NonOverlappingEdits_DoesNotLoseTheirsChanges()
        {
            // Regression for the silent-data-loss bug: ours adds a line at the top,
            // theirs adds a line at the bottom. The old implementation returned oursText
            // and silently discarded the bottom addition. We don't claim to do a full
            // diff3, but we MUST not lose data — surface both sides via conflict markers.
            var baseT = "alpha\nbeta\ngamma";
            var ours = "PREFIX\nalpha\nbeta\ngamma";
            var theirs = "alpha\nbeta\ngamma\nSUFFIX";

            var s = PromptDiffEngine.ThreeWayMerge(baseT, ours, theirs);
            Assert.Contains("PREFIX", s);
            Assert.Contains("SUFFIX", s);
        }

        [Fact]
        public void ThreeWayMerge_IdenticalOursAndTheirs_ReturnsThatText()
        {
            var s = PromptDiffEngine.ThreeWayMerge("a\nb", "a\nB", "a\nB");
            Assert.Equal("a\nB", s);
        }

        [Fact]
        public void ThreeWayMerge_NullInputs_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => PromptDiffEngine.ThreeWayMerge(null!, "a", "a"));
            Assert.Throws<ArgumentNullException>(() => PromptDiffEngine.ThreeWayMerge("a", null!, "a"));
            Assert.Throws<ArgumentNullException>(() => PromptDiffEngine.ThreeWayMerge("a", "a", null!));
        }

        // ---------- DiffLine.ToString ----------

        [Theory]
        [InlineData(DiffOperation.Insert, "+ hello")]
        [InlineData(DiffOperation.Delete, "- hello")]
        [InlineData(DiffOperation.Equal,  "  hello")]
        public void DiffLine_ToString_UsesExpectedPrefix(DiffOperation op, string expected)
        {
            var line = new DiffLine { Operation = op, Text = "hello" };
            Assert.Equal(expected, line.ToString());
        }

        // ---------- Similarity bounds ----------

        [Fact]
        public void Similarity_IsBetweenZeroAndOne()
        {
            var r1 = PromptDiffEngine.Diff("a\nb\nc", "x\ny\nz");
            Assert.InRange(r1.Similarity, 0.0, 1.0);

            var r2 = PromptDiffEngine.Diff("a\nb\nc", "a\nb\nc\nd\ne\nf");
            Assert.InRange(r2.Similarity, 0.0, 1.0);
        }
    }
}
