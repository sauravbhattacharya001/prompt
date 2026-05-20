namespace Prompt.Tests
{
    using System.Collections.Generic;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="TextAnalysisHelpers"/> — shared tokenization and
    /// similarity utilities used across many prompt analyzers. Because the
    /// type is <c>internal</c>, these tests rely on <c>InternalsVisibleTo</c>.
    /// </summary>
    public class TextAnalysisHelpersTests
    {
        // ── TokenizeToWordSet ────────────────────────────────────────────

        [Fact]
        public void TokenizeToWordSet_NullOrEmpty_ReturnsEmptySet()
        {
            Assert.Empty(TextAnalysisHelpers.TokenizeToWordSet(null!));
            Assert.Empty(TextAnalysisHelpers.TokenizeToWordSet(string.Empty));
        }

        [Fact]
        public void TokenizeToWordSet_LowercasesAndDeduplicates()
        {
            var set = TextAnalysisHelpers.TokenizeToWordSet("Hello hello WORLD world");
            Assert.Equal(2, set.Count);
            Assert.Contains("hello", set);
            Assert.Contains("world", set);
        }

        [Fact]
        public void TokenizeToWordSet_FiltersOutSingleCharTokens()
        {
            // "a", "I", "x" should be dropped (length == 1)
            var set = TextAnalysisHelpers.TokenizeToWordSet("a cat I see x ray");
            Assert.DoesNotContain("a", set);
            Assert.DoesNotContain("i", set);
            Assert.DoesNotContain("x", set);
            Assert.Contains("cat", set);
            Assert.Contains("see", set);
            Assert.Contains("ray", set);
        }

        [Fact]
        public void TokenizeToWordSet_HandlesPunctuationAndWhitespace()
        {
            var set = TextAnalysisHelpers.TokenizeToWordSet("foo, bar!  baz.\n\tqux?");
            Assert.Equal(new HashSet<string> { "foo", "bar", "baz", "qux" }, set);
        }

        [Fact]
        public void TokenizeToWordSet_TreatsUnderscoresAsWordChars()
        {
            var set = TextAnalysisHelpers.TokenizeToWordSet("snake_case camelCase");
            Assert.Contains("snake_case", set);
            Assert.Contains("camelcase", set);
        }

        // ── TokenizeToWordList ───────────────────────────────────────────

        [Fact]
        public void TokenizeToWordList_PreservesDuplicates()
        {
            var list = TextAnalysisHelpers.TokenizeToWordList("foo bar foo bar foo");
            Assert.Equal(5, list.Count);
            Assert.Equal(3, list.FindAll(w => w == "foo").Count);
            Assert.Equal(2, list.FindAll(w => w == "bar").Count);
        }

        [Fact]
        public void TokenizeToWordList_FiltersSingleChars_ButPreservesOrder()
        {
            var list = TextAnalysisHelpers.TokenizeToWordList("the quick a brown b fox");
            Assert.Equal(new List<string> { "the", "quick", "brown", "fox" }, list);
        }

        [Fact]
        public void TokenizeToWordList_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Empty(TextAnalysisHelpers.TokenizeToWordList(null!));
            Assert.Empty(TextAnalysisHelpers.TokenizeToWordList(""));
        }

        // ── TokenizeToWordSetUnfiltered ──────────────────────────────────

        [Fact]
        public void TokenizeToWordSetUnfiltered_IncludesSingleCharTokens()
        {
            var set = TextAnalysisHelpers.TokenizeToWordSetUnfiltered("a I cat");
            Assert.Contains("a", set);
            Assert.Contains("i", set);
            Assert.Contains("cat", set);
        }

        [Fact]
        public void TokenizeToWordSetUnfiltered_IsCaseInsensitive()
        {
            var set = TextAnalysisHelpers.TokenizeToWordSetUnfiltered("Foo");
            Assert.Contains("FOO", set);
            Assert.Contains("foo", set);
        }

        [Fact]
        public void TokenizeToWordSetUnfiltered_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Empty(TextAnalysisHelpers.TokenizeToWordSetUnfiltered(null!));
            Assert.Empty(TextAnalysisHelpers.TokenizeToWordSetUnfiltered(""));
        }

        // ── JaccardSimilarity (sets) ─────────────────────────────────────

        [Fact]
        public void JaccardSimilarity_IdenticalSets_ReturnsOne()
        {
            var setA = new HashSet<string> { "foo", "bar", "baz" };
            var setB = new HashSet<string> { "foo", "bar", "baz" };
            Assert.Equal(1.0, TextAnalysisHelpers.JaccardSimilarity(setA, setB));
        }

        [Fact]
        public void JaccardSimilarity_DisjointSets_ReturnsZero()
        {
            var setA = new HashSet<string> { "foo", "bar" };
            var setB = new HashSet<string> { "baz", "qux" };
            Assert.Equal(0.0, TextAnalysisHelpers.JaccardSimilarity(setA, setB));
        }

        [Fact]
        public void JaccardSimilarity_PartialOverlap_ReturnsExpectedRatio()
        {
            var setA = new HashSet<string> { "a", "b", "c" };
            var setB = new HashSet<string> { "b", "c", "d" };
            // intersection = 2 (b, c), union = 4 (a, b, c, d) → 0.5
            Assert.Equal(0.5, TextAnalysisHelpers.JaccardSimilarity(setA, setB), 6);
        }

        [Fact]
        public void JaccardSimilarity_EitherEmpty_ReturnsZero()
        {
            var setA = new HashSet<string> { "foo" };
            var setB = new HashSet<string>();
            Assert.Equal(0.0, TextAnalysisHelpers.JaccardSimilarity(setA, setB));
            Assert.Equal(0.0, TextAnalysisHelpers.JaccardSimilarity(setB, setA));
            Assert.Equal(0.0, TextAnalysisHelpers.JaccardSimilarity(setB, setB));
        }

        [Fact]
        public void JaccardSimilarity_IsSymmetric()
        {
            var setA = new HashSet<string> { "a", "b", "c", "d" };
            var setB = new HashSet<string> { "c", "d", "e" };
            var ab = TextAnalysisHelpers.JaccardSimilarity(setA, setB);
            var ba = TextAnalysisHelpers.JaccardSimilarity(setB, setA);
            Assert.Equal(ab, ba, 9);
        }

        [Fact]
        public void JaccardSimilarity_StringOverload_ReturnsSameResultAsSetOverload()
        {
            const string a = "the quick brown fox";
            const string b = "the lazy brown dog";
            var setA = TextAnalysisHelpers.TokenizeToWordSet(a);
            var setB = TextAnalysisHelpers.TokenizeToWordSet(b);
            var expected = TextAnalysisHelpers.JaccardSimilarity(setA, setB);
            var actual = TextAnalysisHelpers.JaccardSimilarity(a, b);
            Assert.Equal(expected, actual, 9);
        }

        [Fact]
        public void JaccardSimilarity_StringOverload_BothEmpty_ReturnsZero()
        {
            Assert.Equal(0.0, TextAnalysisHelpers.JaccardSimilarity("", ""));
            Assert.Equal(0.0, TextAnalysisHelpers.JaccardSimilarity((string)null!, (string)null!));
        }

        // ── GetNgrams ────────────────────────────────────────────────────

        [Fact]
        public void GetNgrams_ReturnsExpectedBigramFrequencies()
        {
            // "abcab" → bigrams: ab, bc, ca, ab → {ab:2, bc:1, ca:1}
            var ngrams = TextAnalysisHelpers.GetNgrams("abcab", 2);
            Assert.Equal(3, ngrams.Count);
            Assert.Equal(2, ngrams["ab"]);
            Assert.Equal(1, ngrams["bc"]);
            Assert.Equal(1, ngrams["ca"]);
        }

        [Fact]
        public void GetNgrams_TextShorterThanN_ReturnsEmpty()
        {
            Assert.Empty(TextAnalysisHelpers.GetNgrams("ab", 3));
            Assert.Empty(TextAnalysisHelpers.GetNgrams("", 2));
        }

        [Fact]
        public void GetNgrams_NEqualsLength_ReturnsSingleGram()
        {
            var ngrams = TextAnalysisHelpers.GetNgrams("foo", 3);
            Assert.Single(ngrams);
            Assert.Equal(1, ngrams["foo"]);
        }

        // ── NgramCosineSimilarity ────────────────────────────────────────

        [Fact]
        public void NgramCosineSimilarity_IdenticalStrings_ReturnsOne()
        {
            var sim = TextAnalysisHelpers.NgramCosineSimilarity("hello world", "hello world");
            Assert.Equal(1.0, sim, 9);
        }

        [Fact]
        public void NgramCosineSimilarity_IsCaseInsensitive()
        {
            var lower = TextAnalysisHelpers.NgramCosineSimilarity("hello", "HELLO");
            Assert.Equal(1.0, lower, 9);
        }

        [Fact]
        public void NgramCosineSimilarity_CompletelyDisjointBigrams_ReturnsZero()
        {
            // "ab" → {ab}; "cd" → {cd}; no shared bigrams.
            var sim = TextAnalysisHelpers.NgramCosineSimilarity("ab", "cd", 2);
            Assert.Equal(0.0, sim, 9);
        }

        [Fact]
        public void NgramCosineSimilarity_EitherEmpty_ReturnsZero()
        {
            Assert.Equal(0.0, TextAnalysisHelpers.NgramCosineSimilarity("", "hello"));
            Assert.Equal(0.0, TextAnalysisHelpers.NgramCosineSimilarity("hello", ""));
            Assert.Equal(0.0, TextAnalysisHelpers.NgramCosineSimilarity(null!, "hello"));
        }

        [Fact]
        public void NgramCosineSimilarity_PartialOverlap_BetweenZeroAndOne()
        {
            var sim = TextAnalysisHelpers.NgramCosineSimilarity("the quick brown fox", "the slow brown dog");
            Assert.InRange(sim, 0.0, 1.0);
            Assert.True(sim > 0.0);
            Assert.True(sim < 1.0);
        }

        [Fact]
        public void NgramCosineSimilarity_IsSymmetric()
        {
            var ab = TextAnalysisHelpers.NgramCosineSimilarity("alpha beta", "beta gamma");
            var ba = TextAnalysisHelpers.NgramCosineSimilarity("beta gamma", "alpha beta");
            Assert.Equal(ab, ba, 9);
        }

        // ── WordOverlap ──────────────────────────────────────────────────

        [Fact]
        public void WordOverlap_AllReferenceWordsPresent_ReturnsOne()
        {
            var overlap = TextAnalysisHelpers.WordOverlap(
                candidate: "the quick brown fox jumps",
                reference: "quick brown fox");
            Assert.Equal(1.0, overlap, 9);
        }

        [Fact]
        public void WordOverlap_NoReferenceWordsPresent_ReturnsZero()
        {
            var overlap = TextAnalysisHelpers.WordOverlap(
                candidate: "alpha beta gamma",
                reference: "delta epsilon");
            Assert.Equal(0.0, overlap, 9);
        }

        [Fact]
        public void WordOverlap_PartialOverlap_ReturnsExpectedRatio()
        {
            // reference has 4 words; candidate contains 2 of them → 0.5
            var overlap = TextAnalysisHelpers.WordOverlap(
                candidate: "hello world",
                reference: "hello brave new world");
            Assert.Equal(0.5, overlap, 9);
        }

        [Fact]
        public void WordOverlap_EmptyReference_ReturnsZero()
        {
            Assert.Equal(0.0, TextAnalysisHelpers.WordOverlap("anything", ""));
        }

        [Fact]
        public void WordOverlap_IsCaseInsensitive()
        {
            var overlap = TextAnalysisHelpers.WordOverlap(
                candidate: "HELLO world",
                reference: "hello WORLD");
            Assert.Equal(1.0, overlap, 9);
        }

        // ── SplitSentences ───────────────────────────────────────────────

        [Fact]
        public void SplitSentences_NullOrWhitespace_ReturnsEmpty()
        {
            Assert.Empty(TextAnalysisHelpers.SplitSentences(null!));
            Assert.Empty(TextAnalysisHelpers.SplitSentences("   "));
        }

        [Fact]
        public void SplitSentences_PunctuationOnly_SplitsOnPeriodExclamationQuestion()
        {
            var sentences = TextAnalysisHelpers.SplitSentences("First. Second! Third? Fourth.");
            Assert.Equal(4, sentences.Count);
            Assert.Equal("First.", sentences[0]);
            Assert.Equal("Second!", sentences[1]);
            Assert.Equal("Third?", sentences[2]);
            Assert.Equal("Fourth.", sentences[3]);
        }

        [Fact]
        public void SplitSentences_PunctuationOnly_DoesNotSplitOnNewline()
        {
            // Single newline + non-space char: punctuation-only regex never splits here.
            var sentences = TextAnalysisHelpers.SplitSentences("First line\nSecond line", splitOnNewlines: false);
            Assert.Single(sentences);
            Assert.Equal("First line\nSecond line", sentences[0]);
        }

        [Fact]
        public void SplitSentences_WithNewlines_SplitsOnNewlineFollowedByWhitespace()
        {
            // The newline-aware pattern is `(?<=[.!?\n])\s+` — newline must be followed
            // by additional whitespace to trigger a split.
            var sentences = TextAnalysisHelpers.SplitSentences("First line\n  Second line", splitOnNewlines: true);
            Assert.Equal(2, sentences.Count);
            Assert.Equal("First line", sentences[0]);
            Assert.Equal("Second line", sentences[1]);
        }

        [Fact]
        public void SplitSentences_TrimsWhitespaceAndSkipsEmpty()
        {
            var sentences = TextAnalysisHelpers.SplitSentences("Hello.    World.   ");
            Assert.Equal(2, sentences.Count);
            Assert.Equal("Hello.", sentences[0]);
            Assert.Equal("World.", sentences[1]);
        }

        [Fact]
        public void SplitSentences_SingleSentenceNoPunctuation_ReturnsOne()
        {
            var sentences = TextAnalysisHelpers.SplitSentences("just one chunk");
            Assert.Single(sentences);
            Assert.Equal("just one chunk", sentences[0]);
        }

        // ── EstimateTokens ───────────────────────────────────────────────

        [Fact]
        public void EstimateTokens_NullOrEmpty_ReturnsZero()
        {
            Assert.Equal(0, TextAnalysisHelpers.EstimateTokens(null!));
            Assert.Equal(0, TextAnalysisHelpers.EstimateTokens(""));
        }

        [Theory]
        [InlineData("a", 1)]            // ceil(1/4) = 1
        [InlineData("abcd", 1)]         // ceil(4/4) = 1
        [InlineData("abcde", 2)]        // ceil(5/4) = 2
        [InlineData("abcdefgh", 2)]     // ceil(8/4) = 2
        [InlineData("abcdefghi", 3)]    // ceil(9/4) = 3
        public void EstimateTokens_FollowsCeilingOfLengthOverFour(string text, int expected)
        {
            Assert.Equal(expected, TextAnalysisHelpers.EstimateTokens(text));
        }

        [Fact]
        public void EstimateTokens_GrowsMonotonicallyWithLength()
        {
            var short_ = TextAnalysisHelpers.EstimateTokens("hello");
            var long_ = TextAnalysisHelpers.EstimateTokens("hello world how are you today");
            Assert.True(long_ > short_);
        }
    }
}
