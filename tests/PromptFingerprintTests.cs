namespace Prompt.Tests
{
    using Xunit;

    public class PromptFingerprintTests
    {
        private readonly PromptFingerprintGenerator _gen = new();

        // === Basic Fingerprinting ===

        [Fact]
        public void Fingerprint_SameText_SameHash()
        {
            var fp1 = _gen.Fingerprint("Hello world");
            var fp2 = _gen.Fingerprint("Hello world");
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Fingerprint_DifferentText_DifferentHash()
        {
            var fp1 = _gen.Fingerprint("Hello world");
            var fp2 = _gen.Fingerprint("Goodbye world");
            Assert.NotEqual(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Fingerprint_RecordsOriginalLength()
        {
            var fp = _gen.Fingerprint("Hello world");
            Assert.Equal(11, fp.OriginalLength);
        }

        [Fact]
        public void Fingerprint_RecordsWordCount()
        {
            var fp = _gen.Fingerprint("The quick brown fox jumps");
            Assert.Equal(5, fp.WordCount);
        }

        [Fact]
        public void Fingerprint_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _gen.Fingerprint(null!));
        }

        [Fact]
        public void Fingerprint_EmptyString_Works()
        {
            var fp = _gen.Fingerprint("");
            Assert.NotNull(fp.Hash);
            Assert.Equal(0, fp.WordCount);
        }

        [Fact]
        public void Fingerprint_HasTimestamp()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var fp = _gen.Fingerprint("test");
            Assert.True(fp.CreatedAt >= before);
        }

        // === Normalization Levels ===

        [Fact]
        public void None_WhitespaceDifference_DifferentHash()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.None };
            var fp1 = _gen.Fingerprint("hello  world", opts);
            var fp2 = _gen.Fingerprint("hello world", opts);
            Assert.NotEqual(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Whitespace_CollapseSpaces_SameHash()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.Whitespace };
            var fp1 = _gen.Fingerprint("hello  world", opts);
            var fp2 = _gen.Fingerprint("hello world", opts);
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Whitespace_Trims()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.Whitespace };
            var fp1 = _gen.Fingerprint("  hello  ", opts);
            var fp2 = _gen.Fingerprint("hello", opts);
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void CaseInsensitive_DifferentCase_SameHash()
        {
            var fp1 = _gen.Fingerprint("Hello World");
            var fp2 = _gen.Fingerprint("hello world");
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Structural_RemovesPunctuation()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.Structural };
            var fp1 = _gen.Fingerprint("Hello, world!", opts);
            var fp2 = _gen.Fingerprint("Hello world", opts);
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Structural_RemovesStopWords()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.Structural };
            var fp1 = _gen.Fingerprint("the quick fox", opts);
            var fp2 = _gen.Fingerprint("quick fox", opts);
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Semantic_OrderIndependent()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.Semantic };
            var fp1 = _gen.Fingerprint("cat dog bird", opts);
            var fp2 = _gen.Fingerprint("bird cat dog", opts);
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Semantic_StillDiffersOnDifferentWords()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.Semantic };
            var fp1 = _gen.Fingerprint("cat dog", opts);
            var fp2 = _gen.Fingerprint("fish bird", opts);
            Assert.NotEqual(fp1.Hash, fp2.Hash);
        }

        // === Custom Stop Words ===

        [Fact]
        public void CustomStopWords_Respected()
        {
            var opts = new PromptFingerprintOptions
            {
                Normalization = NormalizationLevel.Structural,
                StopWords = new HashSet<string> { "please", "kindly" }
            };
            var fp1 = _gen.Fingerprint("please summarize", opts);
            var fp2 = _gen.Fingerprint("summarize", opts);
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        // === Tags ===

        [Fact]
        public void Tag_IsPreserved()
        {
            var fp = _gen.Fingerprint("hello", new PromptFingerprintOptions { Tag = "v1" });
            Assert.Equal("v1", fp.Tag);
        }

        // === Matches ===

        [Fact]
        public void Matches_TrueForSameContent()
        {
            var fp1 = _gen.Fingerprint("hello");
            var fp2 = _gen.Fingerprint("HELLO");
            Assert.True(fp1.Matches(fp2));
        }

        [Fact]
        public void Matches_FalseForDifferent()
        {
            var fp1 = _gen.Fingerprint("hello");
            var fp2 = _gen.Fingerprint("world");
            Assert.False(fp1.Matches(fp2));
        }

        [Fact]
        public void Matches_NullReturnsFalse()
        {
            var fp = _gen.Fingerprint("hello");
            Assert.False(fp.Matches(null!));
        }

        // === Similarity ===

        [Fact]
        public void Similarity_IdenticalTexts_One()
        {
            var opts = new PromptFingerprintOptions { EnableSimilarity = true };
            var fp1 = _gen.Fingerprint("the quick brown fox jumps over the lazy dog", opts);
            var fp2 = _gen.Fingerprint("the quick brown fox jumps over the lazy dog", opts);
            Assert.Equal(1.0, fp1.SimilarityTo(fp2));
        }

        [Fact]
        public void Similarity_CompletelyDifferent_Low()
        {
            var opts = new PromptFingerprintOptions { EnableSimilarity = true };
            var fp1 = _gen.Fingerprint("alpha beta gamma delta", opts);
            var fp2 = _gen.Fingerprint("one two three four five six", opts);
            Assert.True(fp1.SimilarityTo(fp2) < 0.2);
        }

        [Fact]
        public void Similarity_Partial_Moderate()
        {
            var opts = new PromptFingerprintOptions { EnableSimilarity = true };
            var fp1 = _gen.Fingerprint("summarize this article about climate change", opts);
            var fp2 = _gen.Fingerprint("summarize this article about global warming", opts);
            var sim = fp1.SimilarityTo(fp2);
            Assert.True(sim > 0.2 && sim < 0.9);
        }

        [Fact]
        public void Similarity_WithoutEnabled_ReturnsZero()
        {
            var fp1 = _gen.Fingerprint("hello");
            var fp2 = _gen.Fingerprint("hello");
            Assert.Equal(0.0, fp1.SimilarityTo(fp2));
        }

        [Fact]
        public void Similarity_NullOther_ReturnsZero()
        {
            var opts = new PromptFingerprintOptions { EnableSimilarity = true };
            var fp = _gen.Fingerprint("hello world test", opts);
            Assert.Equal(0.0, fp.SimilarityTo(null!));
        }

        // === Batch Fingerprinting ===

        [Fact]
        public void Batch_DetectsDuplicates()
        {
            var prompts = new[] { "Hello", "World", "hello", "HELLO" };
            var result = _gen.FingerprintBatch(prompts);
            Assert.Equal(4, result.TotalCount);
            Assert.Equal(2, result.UniqueCount);
            Assert.Single(result.DuplicateGroups);
            Assert.Equal(3, result.DuplicateGroups[0].Count);
        }

        [Fact]
        public void Batch_NoDuplicates()
        {
            var prompts = new[] { "alpha", "beta", "gamma" };
            var result = _gen.FingerprintBatch(prompts);
            Assert.Equal(3, result.UniqueCount);
            Assert.Empty(result.DuplicateGroups);
            Assert.Equal(0, result.DuplicateRate);
        }

        [Fact]
        public void Batch_EmptyList()
        {
            var result = _gen.FingerprintBatch(Array.Empty<string>());
            Assert.Equal(0, result.TotalCount);
            Assert.Empty(result.DuplicateGroups);
        }

        [Fact]
        public void Batch_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _gen.FingerprintBatch(null!));
        }

        [Fact]
        public void Batch_DuplicateRate_Correct()
        {
            var prompts = new[] { "a", "a", "b", "b" };
            var result = _gen.FingerprintBatch(prompts);
            Assert.Equal(0.5, result.DuplicateRate);
        }

        // === FindSimilar ===

        [Fact]
        public void FindSimilar_ReturnsExactMatches()
        {
            var candidates = new[] { "hello world", "goodbye world", "HELLO WORLD" };
            var matches = _gen.FindSimilar("hello world", candidates, threshold: 0.5);
            Assert.True(matches.Count >= 1);
            Assert.True(matches[0].IsExactMatch);
        }

        [Fact]
        public void FindSimilar_SortedByDescending()
        {
            var candidates = new[]
            {
                "completely different text here now",
                "summarize article about climate",
                "summarize article about climate change impacts"
            };
            var matches = _gen.FindSimilar("summarize article about climate change", candidates, threshold: 0.1);
            for (int i = 1; i < matches.Count; i++)
                Assert.True(matches[i - 1].Similarity >= matches[i].Similarity);
        }

        [Fact]
        public void FindSimilar_ThresholdFilters()
        {
            var candidates = new[] { "xyz completely unrelated stuff here" };
            var matches = _gen.FindSimilar("hello world test prompt", candidates, threshold: 0.9);
            Assert.Empty(matches);
        }

        // === Diff ===

        [Fact]
        public void Diff_IdenticalTexts_NoChange()
        {
            var diff = _gen.Diff("hello world", "hello world");
            Assert.False(diff.IsChanged);
            Assert.Equal(1.0, diff.Similarity);
        }

        [Fact]
        public void Diff_DifferentTexts_ShowsChanges()
        {
            var diff = _gen.Diff("hello world", "hello universe");
            Assert.True(diff.IsChanged);
            Assert.Contains("universe", diff.AddedWords);
            Assert.Contains("world", diff.RemovedWords);
        }

        [Fact]
        public void Diff_TracksLengthDelta()
        {
            var diff = _gen.Diff("short", "much longer text here");
            Assert.True(diff.LengthDelta > 0);
        }

        [Fact]
        public void Diff_TracksWordCountDelta()
        {
            var diff = _gen.Diff("one two", "one two three four");
            Assert.Equal(2, diff.WordCountDelta);
        }

        [Fact]
        public void Diff_Summary_NoChange()
        {
            var diff = _gen.Diff("test", "test");
            Assert.Equal("No changes detected.", diff.Summary());
        }

        [Fact]
        public void Diff_Summary_WithChanges()
        {
            var diff = _gen.Diff("hello world", "goodbye world");
            var summary = diff.Summary();
            Assert.Contains("Similarity", summary);
        }

        // === Serialization ===

        [Fact]
        public void ToJson_ProducesValidJson()
        {
            var fp = _gen.Fingerprint("test");
            var json = fp.ToJson();
            Assert.Contains("\"hash\"", json);
            Assert.Contains("\"normalization\"", json);
            Assert.Contains("\"wordCount\"", json);
        }

        [Fact]
        public void ToString_ContainsHashPrefix()
        {
            var fp = _gen.Fingerprint("test");
            var str = fp.ToString();
            Assert.Contains("Fingerprint[", str);
            Assert.Contains(fp.Hash[..12], str);
        }

        // === Equality ===

        [Fact]
        public void Equals_SameHash_True()
        {
            var fp1 = _gen.Fingerprint("hello");
            var fp2 = _gen.Fingerprint("HELLO");
            Assert.Equal(fp1, fp2);
        }

        [Fact]
        public void Equals_DifferentHash_False()
        {
            var fp1 = _gen.Fingerprint("hello");
            var fp2 = _gen.Fingerprint("world");
            Assert.NotEqual(fp1, fp2);
        }

        [Fact]
        public void GetHashCode_SameForEqual()
        {
            var fp1 = _gen.Fingerprint("hello");
            var fp2 = _gen.Fingerprint("HELLO");
            Assert.Equal(fp1.GetHashCode(), fp2.GetHashCode());
        }

        // === Shingle Size ===

        [Fact]
        public void ShingleSize_AffectsSimilarity()
        {
            var opts1 = new PromptFingerprintOptions { EnableSimilarity = true, ShingleSize = 2 };
            var opts2 = new PromptFingerprintOptions { EnableSimilarity = true, ShingleSize = 5 };

            var text1 = "the quick brown fox jumps over the lazy dog";
            var text2 = "the quick brown cat jumps over the lazy dog";

            var fp1a = _gen.Fingerprint(text1, opts1);
            var fp1b = _gen.Fingerprint(text2, opts1);
            var sim1 = fp1a.SimilarityTo(fp1b);

            var fp2a = _gen.Fingerprint(text1, opts2);
            var fp2b = _gen.Fingerprint(text2, opts2);
            var sim2 = fp2a.SimilarityTo(fp2b);

            // Larger shingles are more sensitive to changes
            Assert.True(sim1 >= sim2);
        }

        // === Edge Cases ===

        [Fact]
        public void SingleWord_Fingerprints()
        {
            var fp = _gen.Fingerprint("hello");
            Assert.Equal(1, fp.WordCount);
            Assert.NotEmpty(fp.Hash);
        }

        [Fact]
        public void VeryLongText_Works()
        {
            var text = string.Join(" ", Enumerable.Repeat("word", 10000));
            var fp = _gen.Fingerprint(text);
            Assert.Equal(10000, fp.WordCount);
        }

        [Fact]
        public void UnicodeText_Works()
        {
            var fp = _gen.Fingerprint("こんにちは世界 🌍");
            Assert.NotEmpty(fp.Hash);
        }

        [Fact]
        public void Newlines_Normalized()
        {
            var fp1 = _gen.Fingerprint("hello\nworld");
            var fp2 = _gen.Fingerprint("hello world");
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Tabs_Normalized()
        {
            var fp1 = _gen.Fingerprint("hello\tworld");
            var fp2 = _gen.Fingerprint("hello world");
            Assert.Equal(fp1.Hash, fp2.Hash);
        }

        [Fact]
        public void Similarity_SingleWord_Works()
        {
            var opts = new PromptFingerprintOptions { EnableSimilarity = true };
            var fp1 = _gen.Fingerprint("hello", opts);
            var fp2 = _gen.Fingerprint("hello", opts);
            Assert.Equal(1.0, fp1.SimilarityTo(fp2));
        }

        [Fact]
        public void Batch_AllIdentical()
        {
            var prompts = new[] { "same", "SAME", "Same" };
            var result = _gen.FingerprintBatch(prompts);
            Assert.Equal(1, result.UniqueCount);
            Assert.Single(result.DuplicateGroups);
        }

        [Fact]
        public void Structural_OnlyStopWords_EmptyResult()
        {
            var opts = new PromptFingerprintOptions { Normalization = NormalizationLevel.Structural };
            var fp = _gen.Fingerprint("the a an is", opts);
            Assert.Equal(0, fp.WordCount);
        }
    }
}
