namespace Prompt.Tests
{
    using System;
    using Xunit;

    public class PromptNormalizerTests
    {
        // ─── Individual Rules ───

        [Fact]
        public void CollapseWhitespace_CollapsesSpacesAndTabs()
        {
            var n = new PromptNormalizer().CollapseWhitespace();
            Assert.Equal("hello world", n.Normalize("hello   \t  world"));
        }

        [Fact]
        public void TrimLines_TrimsEachLine()
        {
            var n = new PromptNormalizer().TrimLines();
            Assert.Equal("hello\nworld", n.Normalize("  hello  \n  world  "));
        }

        [Fact]
        public void NormalizeLineEndings_ConvertsToUnix()
        {
            var n = new PromptNormalizer().NormalizeLineEndings();
            Assert.Equal("a\nb\nc", n.Normalize("a\r\nb\rc"));
        }

        [Fact]
        public void CollapseBlankLines_ReducesExcessiveBlankLines()
        {
            var n = new PromptNormalizer().CollapseBlankLines(1);
            var input = "a\n\n\n\nb";
            var result = n.Normalize(input);
            Assert.DoesNotContain("\n\n\n", result);
            Assert.Contains("a\n\nb", result);
        }

        [Fact]
        public void RemoveTrailingPunctuation_RemovesPeriodsAndBangs()
        {
            var n = new PromptNormalizer().RemoveTrailingPunctuation();
            Assert.Equal("hello", n.Normalize("hello..."));
            Assert.Equal("wow", n.Normalize("wow!!!"));
        }

        [Fact]
        public void LowercaseDirectives_LowercasesKnownPrefixes()
        {
            var n = new PromptNormalizer().LowercaseDirectives();
            Assert.Equal("you are a helpful assistant", n.Normalize("You Are a helpful assistant"));
        }

        [Fact]
        public void StripHtml_RemovesTags()
        {
            var n = new PromptNormalizer().StripHtml();
            Assert.Equal("hello world", n.Normalize("<b>hello</b> <i>world</i>"));
        }

        [Fact]
        public void NormalizeUnicode_ReplacesSmartQuotesAndDashes()
        {
            var n = new PromptNormalizer().NormalizeUnicode();
            var input = "\u201CHello\u201D \u2018world\u2019 \u2013 \u2014 \u2026";
            Assert.Equal("\"Hello\" 'world' - - ...", n.Normalize(input));
        }

        // ─── Custom Rules ───

        [Fact]
        public void AddRule_AppliesCustomTransform()
        {
            var n = new PromptNormalizer().AddRule(t => t.ToUpperInvariant());
            Assert.Equal("HELLO", n.Normalize("hello"));
        }

        [Fact]
        public void AddRule_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PromptNormalizer().AddRule(null!));
        }

        // ─── Freeze ───

        [Fact]
        public void Freeze_PreventsNewRules()
        {
            var n = new PromptNormalizer().Freeze();
            Assert.Throws<InvalidOperationException>(() => n.CollapseWhitespace());
            Assert.Throws<InvalidOperationException>(() => n.TrimLines());
            Assert.Throws<InvalidOperationException>(() => n.AddRule(t => t));
        }

        // ─── Normalize Edge Cases ───

        [Fact]
        public void Normalize_Null_ReturnsEmpty()
        {
            var n = new PromptNormalizer();
            Assert.Equal(string.Empty, n.Normalize(null!));
        }

        [Fact]
        public void Normalize_Empty_ReturnsEmpty()
        {
            var n = new PromptNormalizer();
            Assert.Equal(string.Empty, n.Normalize(""));
        }

        [Fact]
        public void Normalize_TrimsResult()
        {
            var n = new PromptNormalizer();
            Assert.Equal("hello", n.Normalize("  hello  "));
        }

        // ─── Fingerprint ───

        [Fact]
        public void Fingerprint_SameNormalizedText_SameHash()
        {
            var n = new PromptNormalizer().CollapseWhitespace();
            string h1 = n.Fingerprint("hello   world");
            string h2 = n.Fingerprint("hello world");
            Assert.Equal(h1, h2);
        }

        [Fact]
        public void Fingerprint_DifferentText_DifferentHash()
        {
            var n = new PromptNormalizer();
            Assert.NotEqual(n.Fingerprint("hello"), n.Fingerprint("world"));
        }

        [Fact]
        public void Fingerprint_ReturnsValidHexString()
        {
            var n = new PromptNormalizer();
            string fp = n.Fingerprint("test");
            Assert.Equal(64, fp.Length); // SHA-256 = 64 hex chars
            Assert.Matches("^[0-9a-f]+$", fp);
        }

        // ─── AreEquivalent ───

        [Fact]
        public void AreEquivalent_TrueForNormalizedMatch()
        {
            var n = new PromptNormalizer().CollapseWhitespace().TrimLines();
            Assert.True(n.AreEquivalent("  hello   world  ", "hello world"));
        }

        [Fact]
        public void AreEquivalent_FalseForDifferentContent()
        {
            var n = new PromptNormalizer();
            Assert.False(n.AreEquivalent("hello", "world"));
        }

        // ─── Static Presets ───

        [Fact]
        public void Default_IsFrozen_CanNormalize()
        {
            var d = PromptNormalizer.Default;
            Assert.Throws<InvalidOperationException>(() => d.CollapseWhitespace());
            // But normalization works
            string result = d.Normalize("  Hello   World  \r\n  foo  ");
            Assert.Equal("Hello World\nfoo", result);
        }

        [Fact]
        public void Aggressive_StripsHtmlAndLowercasesDirectives()
        {
            var a = PromptNormalizer.Aggressive;
            string result = a.Normalize("<b>You Are</b> a helpful assistant!!!");
            Assert.Equal("you are a helpful assistant", result);
        }

        // ─── Chained Rules ───

        [Fact]
        public void ChainedRules_ApplyInOrder()
        {
            var n = new PromptNormalizer()
                .NormalizeLineEndings()
                .CollapseWhitespace()
                .TrimLines();

            string result = n.Normalize("  hello   world  \r\n  foo   bar  ");
            Assert.Equal("hello world\nfoo bar", result);
        }
    }
}
