// Issue #191 regression tests: ensure the converged char-based token
// estimators agree on edge cases and never silently return 0 for non-empty
// input (which is what the old `(int)(len / 3.5)` impl in
// PromptCompatibilityChecker did for short strings).
namespace Prompt.Tests
{
    using Prompt;
    using Xunit;

    public class TokenEstimatorConvergenceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void BothEstimators_ReturnZero_ForNullOrEmpty(string? input)
        {
            Assert.Equal(0, TextAnalysisHelpers.EstimateTokens(input!));
            Assert.Equal(0, PromptGuard.EstimateTokens(input!));
        }

        [Theory]
        [InlineData("a")]
        [InlineData("ab")]
        [InlineData("hello")]
        [InlineData("hello world")]
        [InlineData("The quick brown fox jumps over the lazy dog.")]
        public void BothEstimators_ReturnPositive_ForNonEmpty(string input)
        {
            // Before #191 fix, PromptCompatibilityChecker's local estimator
            // returned 0 for "a" (since (int)(1/3.5) == 0), which is a
            // correctness hazard. After convergence on the ~4-chars-per-token
            // formula, all canonical estimators agree that any non-empty
            // string produces >= 1 token.
            Assert.True(
                TextAnalysisHelpers.EstimateTokens(input) > 0,
                $"TextAnalysisHelpers.EstimateTokens(\"{input}\") should be > 0");
            Assert.True(
                PromptGuard.EstimateTokens(input) > 0,
                $"PromptGuard.EstimateTokens(\"{input}\") should be > 0");
        }

        [Theory]
        [InlineData("a", 1)]            // ceil(1/4) = 1
        [InlineData("abcd", 1)]         // ceil(4/4) = 1
        [InlineData("abcdefgh", 2)]     // ceil(8/4) = 2
        [InlineData("abcdefghi", 3)]    // ceil(9/4) = 3
        public void CanonicalEstimator_IsCeilingOfLengthOverFour(string input, int expected)
        {
            // This is the pinned contract for the internal canonical estimator
            // that all converged callers now delegate to.
            Assert.Equal(expected, TextAnalysisHelpers.EstimateTokens(input));
        }

        [Fact]
        public void Estimators_AgreeWithinReasonableBounds_OnTypicalEnglish()
        {
            // PromptGuard.EstimateTokens uses a blended char+word formula with
            // adjustments for special characters and newlines, so it won't be
            // identical to TextAnalysisHelpers' pure ceil(len/4). But for
            // typical English text they should agree within ~50% of each
            // other — anything wider would indicate one of them drifted.
            string sample = "The quick brown fox jumps over the lazy dog. " +
                            "Sphinx of black quartz, judge my vow.";

            int helpers = TextAnalysisHelpers.EstimateTokens(sample);
            int guard = PromptGuard.EstimateTokens(sample);

            Assert.True(helpers > 0 && guard > 0);
            double ratio = (double)System.Math.Min(helpers, guard) /
                           System.Math.Max(helpers, guard);
            Assert.True(
                ratio >= 0.5,
                $"Estimators drifted: TextAnalysisHelpers={helpers}, PromptGuard={guard}");
        }
    }
}
