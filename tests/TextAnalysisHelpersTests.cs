namespace Prompt.Tests
{
    using Xunit;

    /// <summary>
    /// Tests for <see cref="TextAnalysisHelpers"/> - the shared token-count
    /// estimation utility used across the provider adapters. Because the type is
    /// <c>internal</c>, these tests rely on <c>InternalsVisibleTo</c>.
    /// </summary>
    public class TextAnalysisHelpersTests
    {
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
