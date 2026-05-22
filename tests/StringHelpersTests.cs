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

        // ─── JaccardSimilarity ───

        [Fact]
        public void JaccardSimilarity_BothEmpty_ReturnsOne()
        {
            Assert.Equal(1.0, StringHelpers.JaccardSimilarity(
                new HashSet<string>(), new HashSet<string>()));
        }

        [Fact]
        public void JaccardSimilarity_Identical_ReturnsOne()
        {
            var set = new HashSet<string> { "a", "b", "c" };
            Assert.Equal(1.0, StringHelpers.JaccardSimilarity(set, set));
        }

        [Fact]
        public void JaccardSimilarity_Disjoint_ReturnsZero()
        {
            var a = new HashSet<string> { "a", "b" };
            var b = new HashSet<string> { "c", "d" };
            Assert.Equal(0.0, StringHelpers.JaccardSimilarity(a, b));
        }

        [Fact]
        public void JaccardSimilarity_Overlap_ReturnsCorrectRatio()
        {
            var a = new HashSet<string> { "a", "b", "c" };
            var b = new HashSet<string> { "b", "c", "d" };
            // intersection=2, union=4
            Assert.Equal(0.5, StringHelpers.JaccardSimilarity(a, b));
        }

        // ─── CsvEscape ───

        [Fact]
        public void CsvEscape_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, StringHelpers.CsvEscape(null!));
            Assert.Equal(string.Empty, StringHelpers.CsvEscape(""));
        }

        [Fact]
        public void CsvEscape_PlainValue_ReturnsSame()
        {
            Assert.Equal("hello", StringHelpers.CsvEscape("hello"));
        }

        [Fact]
        public void CsvEscape_WithComma_WrapsInQuotes()
        {
            Assert.Equal("\"a,b\"", StringHelpers.CsvEscape("a,b"));
        }

        [Fact]
        public void CsvEscape_WithQuotes_EscapesAndWraps()
        {
            Assert.Equal("\"say \"\"hi\"\"\"", StringHelpers.CsvEscape("say \"hi\""));
        }

        [Fact]
        public void CsvEscape_WithNewline_WrapsInQuotes()
        {
            Assert.Equal("\"line1\nline2\"", StringHelpers.CsvEscape("line1\nline2"));
        }

        [Fact]
        public void CsvEscape_WithCarriageReturn_WrapsInQuotes()
        {
            // Regression: prior to the fix, a bare \r (or \r\n on Windows) was
            // not treated as a CSV special character, producing records that
            // strict RFC 4180 parsers terminated early.
            Assert.Equal("\"line1\rline2\"", StringHelpers.CsvEscape("line1\rline2"));
        }

        [Fact]
        public void CsvEscape_WithCrLf_WrapsInQuotes()
        {
            Assert.Equal("\"line1\r\nline2\"", StringHelpers.CsvEscape("line1\r\nline2"));
        }

        [Fact]
        public void CsvEscape_WithCrAndQuote_EscapesAndWraps()
        {
            Assert.Equal("\"say \"\"hi\"\"\rnow\"", StringHelpers.CsvEscape("say \"hi\"\rnow"));
        }

        // ─── CountOccurrences ───

        [Fact]
        public void CountOccurrences_NullOrEmpty_ReturnsZero()
        {
            Assert.Equal(0, StringHelpers.CountOccurrences(null!, "a"));
            Assert.Equal(0, StringHelpers.CountOccurrences("abc", null!));
            Assert.Equal(0, StringHelpers.CountOccurrences("", "a"));
        }

        [Fact]
        public void CountOccurrences_MultipleHits()
        {
            Assert.Equal(3, StringHelpers.CountOccurrences("abcabcabc", "abc"));
        }

        [Fact]
        public void CountOccurrences_NonOverlapping()
        {
            Assert.Equal(2, StringHelpers.CountOccurrences("aaaa", "aa"));
        }

        [Fact]
        public void CountOccurrences_NoMatch_ReturnsZero()
        {
            Assert.Equal(0, StringHelpers.CountOccurrences("hello", "xyz"));
        }

        // ─── HtmlEncode ───

        [Fact]
        public void HtmlEncode_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, StringHelpers.HtmlEncode(null!));
            Assert.Equal(string.Empty, StringHelpers.HtmlEncode(""));
        }

        [Fact]
        public void HtmlEncode_SpecialChars_Encoded()
        {
            Assert.Equal("&amp;&lt;&gt;&quot;", StringHelpers.HtmlEncode("&<>\""));
        }

        [Fact]
        public void HtmlEncode_PlainText_ReturnsSame()
        {
            Assert.Equal("hello world", StringHelpers.HtmlEncode("hello world"));
        }

        // ─── StdDev ───

        [Fact]
        public void StdDev_SingleValue_ReturnsZero()
        {
            Assert.Equal(0.0, StringHelpers.StdDev(new List<double> { 42.0 }));
        }

        [Fact]
        public void StdDev_IdenticalValues_ReturnsZero()
        {
            Assert.Equal(0.0, StringHelpers.StdDev(new List<double> { 5.0, 5.0, 5.0 }));
        }

        [Fact]
        public void StdDev_KnownValues()
        {
            // {2, 4, 4, 4, 5, 5, 7, 9} => sample stddev ≈ 2.138
            var values = new List<double> { 2, 4, 4, 4, 5, 5, 7, 9 };
            double sd = StringHelpers.StdDev(values);
            Assert.InRange(sd, 2.1, 2.2);
        }

        // ─── Percentile ───

        [Fact]
        public void Percentile_EmptyArray_ReturnsZero()
        {
            Assert.Equal(0.0, StringHelpers.Percentile(Array.Empty<double>(), 50));
        }

        [Fact]
        public void Percentile_Median()
        {
            var values = new double[] { 1, 2, 3, 4, 5 };
            Assert.Equal(3.0, StringHelpers.Percentile(values, 50));
        }

        [Fact]
        public void Percentile_P0_ReturnsMin()
        {
            var values = new double[] { 10, 20, 30 };
            Assert.Equal(10.0, StringHelpers.Percentile(values, 0));
        }

        [Fact]
        public void Percentile_P100_ReturnsMax()
        {
            var values = new double[] { 10, 20, 30 };
            Assert.Equal(30.0, StringHelpers.Percentile(values, 100));
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
