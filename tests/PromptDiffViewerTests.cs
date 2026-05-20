using Prompt;
using Xunit;

namespace Prompt.Tests
{
    public class PromptDiffViewerTests
    {
        [Fact]
        public void Compare_DefaultWhitespaceSensitive_ReportsIndentChange()
        {
            // Lines differ only in leading whitespace — by default we must report
            // them as different (Removed + Added).
            var oldText = "alpha\n  beta\ngamma";
            var newText = "alpha\nbeta\ngamma";

            var diff = PromptDiffViewer.Compare(oldText, newText);

            Assert.Equal(1, diff.Stats.Added);
            Assert.Equal(1, diff.Stats.Removed);
            Assert.Equal(2, diff.Stats.Unchanged);
        }

        [Fact]
        public void Compare_IgnoreWhitespace_TreatsIndentOnlyChangesAsEqual()
        {
            // Regression: previously the LCS table treated trimmed lines as equal,
            // but the backtracking pass used raw equality. Result: whitespace-only
            // changes were still reported as Removed/Added pairs and Stats.Unchanged
            // disagreed with the LCS — even with ignoreWhitespace=true.
            var oldText = "alpha\n  beta\ngamma";
            var newText = "alpha\nbeta\ngamma";

            var diff = PromptDiffViewer.Compare(oldText, newText, ignoreWhitespace: true);

            Assert.Equal(0, diff.Stats.Added);
            Assert.Equal(0, diff.Stats.Removed);
            Assert.Equal(3, diff.Stats.Unchanged);
            Assert.All(diff.Lines, l => Assert.Equal(LineDiffType.Equal, l.Type));
            // Original (un-trimmed) content is still preserved on the Equal line.
            Assert.Equal("  beta", diff.Lines[1].Content);
            Assert.Equal(1.0, diff.Stats.Similarity);
        }

        [Fact]
        public void Compare_IgnoreWhitespace_MixedRealAndWhitespaceChanges()
        {
            var oldText = "header\n  body line\nfooter";
            var newText = "header\nbody line\nfooter changed";

            var diff = PromptDiffViewer.Compare(oldText, newText, ignoreWhitespace: true);

            // "  body line" vs "body line" -> Equal under ignoreWhitespace
            // "footer" vs "footer changed" -> Removed + Added
            Assert.Equal(1, diff.Stats.Added);
            Assert.Equal(1, diff.Stats.Removed);
            Assert.Equal(2, diff.Stats.Unchanged);
        }

        [Fact]
        public void Compare_IdenticalText_AllEqual()
        {
            var text = "one\ntwo\nthree";
            var diff = PromptDiffViewer.Compare(text, text);

            Assert.Equal(0, diff.Stats.Added);
            Assert.Equal(0, diff.Stats.Removed);
            Assert.Equal(3, diff.Stats.Unchanged);
            Assert.Equal(1.0, diff.Stats.Similarity);
        }

        [Fact]
        public void Compare_PureAddition_NoRemovals()
        {
            var diff = PromptDiffViewer.Compare("a\nb", "a\nb\nc");
            Assert.Equal(1, diff.Stats.Added);
            Assert.Equal(0, diff.Stats.Removed);
            Assert.Equal(2, diff.Stats.Unchanged);
        }
    }
}
