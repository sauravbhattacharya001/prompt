namespace Prompt.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class StringHelpersTests
    {
        // ─── LevenshteinDistance ───

        [Fact]
        public void LevenshteinDistance_IdenticalStrings_ReturnsZero()
        {
            Assert.Equal(0, StringHelpers.LevenshteinDistance("hello", "hello"));
        }

        [Fact]
        public void LevenshteinDistance_EmptyToNonEmpty_ReturnsLength()
        {
            Assert.Equal(5, StringHelpers.LevenshteinDistance("", "hello"));
            Assert.Equal(3, StringHelpers.LevenshteinDistance("abc", ""));
        }

        [Fact]
        public void LevenshteinDistance_BothEmpty_ReturnsZero()
        {
            Assert.Equal(0, StringHelpers.LevenshteinDistance("", ""));
        }

        [Fact]
        public void LevenshteinDistance_SingleCharDifference()
        {
            Assert.Equal(1, StringHelpers.LevenshteinDistance("cat", "bat"));
            Assert.Equal(1, StringHelpers.LevenshteinDistance("cat", "cats"));
            Assert.Equal(1, StringHelpers.LevenshteinDistance("cats", "cat"));
        }

        [Fact]
        public void LevenshteinDistance_CompletelyDifferent()
        {
            Assert.Equal(3, StringHelpers.LevenshteinDistance("abc", "xyz"));
        }

        [Theory]
        [InlineData("kitten", "sitting", 3)]
        [InlineData("saturday", "sunday", 3)]
        public void LevenshteinDistance_KnownCases(string a, string b, int expected)
        {
            Assert.Equal(expected, StringHelpers.LevenshteinDistance(a, b));
        }

        // ─── LevenshteinDistance (bounded overload) ───
        //
        // The bounded overload returns the *exact* distance when it is within
        // maxDistance, and a `maxDistance + 1` sentinel (NOT the true distance)
        // once the minimum possible distance provably exceeds the bound. These
        // early-exit paths (the length-difference prefilter and the per-row
        // rowMin check) were previously unpinned.

        [Fact]
        public void LevenshteinBounded_WithinBound_ReturnsExactDistance()
        {
            // kitten->sitting is 3; a generous bound must yield the true value.
            Assert.Equal(3, StringHelpers.LevenshteinDistance("kitten", "sitting", 10));
        }

        [Fact]
        public void LevenshteinBounded_DistanceEqualsBound_ReturnsExactDistance()
        {
            // Boundary case: true distance == maxDistance must NOT trip the
            // early-exit; the exact distance is still returned.
            Assert.Equal(3, StringHelpers.LevenshteinDistance("kitten", "sitting", 3));
        }

        [Fact]
        public void LevenshteinBounded_ExceedsBound_ReturnsSentinel()
        {
            // true distance 3 > bound 2 => sentinel (max+1), not the real value.
            Assert.Equal(3, StringHelpers.LevenshteinDistance("kitten", "sitting", 2));
            Assert.Equal(1, StringHelpers.LevenshteinDistance("kitten", "sitting", 0));
        }

        [Fact]
        public void LevenshteinBounded_LengthDifferencePrefilter_ReturnsSentinel()
        {
            // |len(a)-len(b)| alone exceeds the bound, so the answer is the
            // sentinel without computing the full matrix.
            Assert.Equal(2, StringHelpers.LevenshteinDistance("a", "abcd", 1));
        }

        [Fact]
        public void LevenshteinBounded_ZeroDistanceWithinBound_ReturnsZero()
        {
            Assert.Equal(0, StringHelpers.LevenshteinDistance("same", "same", 0));
        }

        [Fact]
        public void LevenshteinBounded_ArgumentOrderIndependent()
        {
            // The internal shorter/longer swap must not change the result.
            Assert.Equal(
                StringHelpers.LevenshteinDistance("abcd", "a", 1),
                StringHelpers.LevenshteinDistance("a", "abcd", 1));
            Assert.Equal(
                StringHelpers.LevenshteinDistance("sitting", "kitten", 10),
                StringHelpers.LevenshteinDistance("kitten", "sitting", 10));
        }

        // ─── Truncate ───

        [Fact]
        public void Truncate_ShortString_ReturnsSame()
        {
            Assert.Equal("hi", StringHelpers.Truncate("hi", 10));
        }

        [Fact]
        public void Truncate_ExactLength_ReturnsSame()
        {
            Assert.Equal("hello", StringHelpers.Truncate("hello", 5));
        }

        [Fact]
        public void Truncate_LongString_AddsEllipsis()
        {
            Assert.Equal("hel...", StringHelpers.Truncate("hello world", 6));
        }

        [Fact]
        public void Truncate_MaxLenThreeOrLess_NoEllipsis()
        {
            Assert.Equal("hel", StringHelpers.Truncate("hello", 3));
        }

        [Fact]
        public void Truncate_NullReturnsEmpty()
        {
            Assert.Equal(string.Empty, StringHelpers.Truncate(null!, 10));
        }

        [Fact]
        public void Truncate_EmptyReturnsEmpty()
        {
            Assert.Equal(string.Empty, StringHelpers.Truncate("", 10));
        }

        // ─── ComputeSimilarity ───

        [Fact]
        public void ComputeSimilarity_IdenticalStrings_ReturnsOne()
        {
            Assert.Equal(1.0, StringHelpers.ComputeSimilarity("hello", "hello"));
        }

        [Fact]
        public void ComputeSimilarity_NullOrEmpty_ReturnsZero()
        {
            Assert.Equal(0.0, StringHelpers.ComputeSimilarity("", "hello"));
            Assert.Equal(0.0, StringHelpers.ComputeSimilarity("hello", ""));
        }

        [Fact]
        public void ComputeSimilarity_SimilarStrings_HighValue()
        {
            double sim = StringHelpers.ComputeSimilarity("hello", "hallo");
            Assert.True(sim > 0.5 && sim < 1.0);
        }

        [Fact]
        public void ComputeSimilarity_LongStrings_UsesLineBased()
        {
            // Over 5000 chars triggers line-based comparison
            var lineA = new string('a', 100);
            var lines = new List<string>();
            for (int i = 0; i < 60; i++) lines.Add(lineA);
            string a = string.Join("\n", lines);
            // Same content = 1.0
            Assert.Equal(1.0, StringHelpers.ComputeSimilarity(a, a));
        }

        // ─── SafeRegexMatch ───

        [Fact]
        public void SafeRegexMatch_ValidPattern_ReturnsTrue()
        {
            Assert.True(StringHelpers.SafeRegexMatch("hello123", @"\d+"));
        }

        [Fact]
        public void SafeRegexMatch_NoMatch_ReturnsFalse()
        {
            Assert.False(StringHelpers.SafeRegexMatch("hello", @"^\d+$"));
        }

        [Fact]
        public void SafeRegexMatch_InvalidPattern_ReturnsFalse()
        {
            Assert.False(StringHelpers.SafeRegexMatch("hello", @"[invalid"));
        }
    }
}
